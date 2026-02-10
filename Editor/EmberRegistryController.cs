using System;
using System.Collections;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace Ember.Editor
{
    /// <summary>
    /// Ember MCP 注册控制器
    ///
    /// 控制器生命周期：
    ///
    /// EmberRegistryController.Initialize()
    ///       ├─> 加载配置
    ///       ├─> 生成/获取实例ID
    ///       ├─> 查找可用端口
    ///       ├─> 启动HTTP监听器
    ///       ├─> 注册到 ember-mcp
    ///       ├─> 启动心跳定时器
    ///       └─> 状态变为 Registered
    ///
    /// EmberRegistryController.OnDestroy()
    ///       ├─> 停止心跳定时器
    ///       ├─> 注销实例
    ///       ├─> 关闭HTTP监听器
    ///       └─> 状态变为 Unregistered
    ///
    /// 编译后自动恢复：
    /// OnAssemblyCompilationFinished()
    ///       ├─> 检查 AutoRestoreOnRecompile 配置
    ///       ├─> 检查是否之前已连接
    ///       ├─> 自动恢复连接
    ///       └─> 保存会话状态
    /// </summary>
    [InitializeOnLoad]
    public class EmberRegistryController : ScriptableObject
    {
        // ============= 状态枚举 =============

        public enum ControllerState
        {
            Uninitialized,      // 未初始化
            Initializing,       // 初始化中
            FindingPort,        // 查找端口
            StartingListener,   // 启动监听器
            Registering,        // 注册中
            Registered,         // 已注册
            Heartbeating,       // 心跳中
            Unregistering,      // 注销中
            Unregistered,       // 已注销
            Failed,             // 失败
            Disconnected        // 已断开
        }

        // ============= 字段 =============

        private static EmberRegistryController _instance;
        private static bool _isInitialized = false;
        private static DateTime _lastCompilationCheckTime = DateTime.MinValue;

        private bool _isHeartbeatRunning = false;
        private bool _isInitializing = false;

        private EmberConfig _config;
        private EmberHttpClient _httpClient;
        private UnityHttpListener _httpListener;

        private ControllerState _state = ControllerState.Uninitialized;
        private string _lastError;
        private DateTime _registerTime;
        private int _successfulHeartbeats;
        private int _failedHeartbeats;

        // ============= 属性 =============

        public static EmberRegistryController Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = CreateInstance<EmberRegistryController>();
                    _instance.hideFlags = HideFlags.HideAndDontSave;
                }
                return _instance;
            }
        }

        public ControllerState State => _state;
        public string LastError => _lastError;
        public UnityInstance UnityInstance { get; private set; }
        public bool IsRegistered => _state == ControllerState.Registered || _state == ControllerState.Heartbeating;
        public bool IsConnecting => _state == ControllerState.Initializing ||
                                    _state == ControllerState.FindingPort ||
                                    _state == ControllerState.StartingListener ||
                                    _state == ControllerState.Registering;
        public TimeSpan Uptime => DateTime.Now - _registerTime;
        public int SuccessfulHeartbeats => _successfulHeartbeats;
        public int FailedHeartbeats => _failedHeartbeats;

        // ============= 事件 =============

        public event Action<ControllerState> OnStateChanged;
        public event Action<string> OnError;
        public event Action OnRegistered;
        public event Action OnUnregistered;
        public event Action OnHeartbeatSent;
        public event Action<string> OnHeartbeatSuccess;
        public event Action<string> OnHeartbeatFailed;

        // ============= 静态构造函数 =============

        /// <summary>
        /// 静态构造函数 - Unity 初始化时自动调用
        ///
        /// EmberRegistryController 静态构造
        ///       ├─> 调用 Initialize()
        ///       ├─> 加载配置
        ///       ├─> 注册 Unity 事件
        ///       ├─> 检查会话状态
        ///       └─> 自动启动（如配置）
        /// </summary>
        static EmberRegistryController()
        {
            InitializeController();
        }

        /// <summary>
        /// 初始化控制器（静态）
        ///
        /// 初始化流程：
        /// InitializeController()
        ///     ├─> 加载配置
        ///     ├─> 注册 Unity 事件
        ///     ├─> 检查会话状态
        ///     └─> 自动启动（如配置）
        /// </summary>
        private static void InitializeController()
        {
            if (_isInitialized)
            {
                return;
            }

            try
            {
                // 加载配置
                var config = EmberConfig.Load();

                // 注册 Unity 事件
                CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
                EditorApplication.quitting += OnEditorQuitting;

                // 检查会话状态（是否之前已连接）
                bool wasConnected = EditorPrefs.GetBool("EmberMCP_Connected", false);

                // 自动启动（如果配置启用）
                if (config.AutoRegister || (config.AutoRestoreOnRecompile && wasConnected))
                {
                    // 延迟启动，避免影响 Unity 启动速度
                    EditorApplication.delayCall += () =>
                    {
                        if (EditorApplication.isPlayingOrWillChangePlaymode)
                        {
                            return;
                        }
                        Instance.InitializeAsync(config);
                    };
                }

                _isInitialized = true;

                if (config.VerboseLogging)
                {
                    Debug.Log("[Ember] 控制器已初始化");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Ember] 控制器初始化失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 程序集编译完成事件处理 - 自动恢复连接
        ///
        /// OnAssemblyCompilationFinished()
        ///       ├─> 检查 AutoRestoreOnRecompile 配置
        ///       ├─> 防抖处理（2秒内只处理一次）
        ///       ├─> 延迟执行（确保编译完全完成）
        ///       ├─> 检查是否之前已连接
        ///       ├─> 自动恢复连接
        ///       └─> 保存会话状态
        /// </summary>
        private static void OnAssemblyCompilationFinished(string assemblyPath, CompilerMessage[] messages)
        {
            // 获取配置
            var config = EmberConfig.Load();

            // 如果未启用自动恢复，直接返回
            if (!config.AutoRestoreOnRecompile)
            {
                return;
            }

            // 防抖：如果在 2 秒内已经检查过，跳过
            var now = DateTime.Now;
            if ((now - _lastCompilationCheckTime).TotalSeconds < 2)
            {
                return;
            }
            _lastCompilationCheckTime = now;

            // 延迟执行，确保所有程序集编译完成
            EditorApplication.delayCall += () =>
            {
                try
                {
                    // 等待一段时间，确保编译完全完成
                    var waitCoroutine = Instance.WaitForCompilationAndRestore();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Ember] 编译后恢复连接失败: {ex.Message}");
                }
            };
        }

        // ============= 初始化 =============

        /// <summary>
        /// 初始化控制器
        ///
        /// InitializeAsync()
        ///       ├─> 加载配置
        ///       ├─> 创建 HTTP 客户端
        ///       ├─> 查找可用端口
        ///       ├─> 启动 HTTP 监听器
        ///       ├─> 注册到服务器
        ///       └─> 启动心跳
        /// </summary>
        public void InitializeAsync(EmberConfig config = null, Action<bool> callback = null)
        {
            if (IsConnecting || IsRegistered)
            {
                Debug.LogWarning("Ember 控制器已在运行或正在连接");
                callback?.Invoke(false);
                return;
            }

            _isInitializing = true;
            EditorCoroutineUtility.StartCoroutineOwnerless(
                InitializeCoroutine(config, callback)
            );
        }

        private IEnumerator InitializeCoroutine(EmberConfig config, Action<bool> callback)
        {
            SetState(ControllerState.Initializing);

            // 加载配置
            _config = config ?? EmberConfig.Load();
            if (!_config.IsValid())
            {
                SetError("配置无效，请检查配置项");
                callback?.Invoke(false);
                _isInitializing = false;
                yield break;
            }

            // 创建 HTTP 客户端
            _httpClient = new EmberHttpClient(_config.ApiKey);

            // 查找可用端口
            SetState(ControllerState.FindingPort);
            int port = PortDetector.GetRecommendedPort(_config.ProjectName, _config.MinPort, _config.MaxPort);
            if (port == -1)
            {
                port = PortDetector.FindAvailablePort(_config.MinPort, _config.MaxPort);
            }

            if (port == -1)
            {
                SetError($"在端口范围 {_config.MinPort}-{_config.MaxPort} 内未找到可用端口");
                callback?.Invoke(false);
                _isInitializing = false;
                yield break;
            }

            // 创建 Unity 实例信息
            UnityInstance = new UnityInstance
            {
                Id = _config.InstanceId,
                Name = _config.ProjectName,
                Port = port
            };

            // 启动 HTTP 监听器
            SetState(ControllerState.StartingListener);
            _httpListener = UnityHttpListener.CreateListener(UnityInstance.Port);
            if (_httpListener == null)
            {
                SetError($"无法在端口 {UnityInstance.Port} 上启动 HTTP 监听器");
                callback?.Invoke(false);
                _isInitializing = false;
                yield break;
            }

            // 注册到服务器
            SetState(ControllerState.Registering);
            bool registerSuccess = false;
            RegisterResponse registerResponse = null;

            yield return _httpClient.RegisterAsync(
                UnityInstance,
                _config.GetServerUrl(),
                response =>
                {
                    registerResponse = response;
                    registerSuccess = response.success;
                }
            );

            yield return null; // 等待回调完成

            if (!registerSuccess)
            {
                SetError($"注册失败: {registerResponse?.message ?? "未知错误"}");
                callback?.Invoke(false);
                _isInitializing = false;
                yield break;
            }

            // 注册成功
            SetState(ControllerState.Registered);
            _registerTime = DateTime.Now;
            OnRegistered?.Invoke();

            // 保存会话状态
            SaveSession();

            // 启动心跳
            if (_config.AutoRegister)
            {
                StartHeartbeat();
            }

            callback?.Invoke(true);
            _isInitializing = false;
        }

        // ============= 心跳管理 =============

        /// <summary>
        /// 启动心跳定时器
        ///
        /// StartHeartbeat()
        ///       ├─> 启动协程定时发送心跳
        ///       ├─> 每隔 HeartbeatInterval 发送一次
        ///       ├─> 成功: 更新统计，继续心跳
        ///       └─> 失败: 记录错误，尝试重新注册
        /// </summary>
        private void StartHeartbeat()
        {
            if (_isHeartbeatRunning)
            {
                return;
            }

            SetState(ControllerState.Heartbeating);
            _isHeartbeatRunning = true;
            EditorCoroutineUtility.StartCoroutineOwnerless(HeartbeatCoroutine());
        }

        private IEnumerator HeartbeatCoroutine()
        {
            WaitForSeconds waitInterval = new WaitForSeconds(_config.HeartbeatInterval / 1000f);

            while (_state == ControllerState.Heartbeating)
            {
                yield return waitInterval;

                bool heartbeatSuccess = false;
                HeartbeatResponse heartbeatResponse = null;

                yield return _httpClient.SendHeartbeatAsync(
                    UnityInstance.Id,
                    _config.GetServerUrl(),
                    response =>
                    {
                        heartbeatResponse = response;
                        heartbeatSuccess = response.success;
                    }
                );

                yield return null; // 等待回调完成

                OnHeartbeatSent?.Invoke();

                if (heartbeatSuccess)
                {
                    _successfulHeartbeats++;
                    UnityInstance.LastHeartbeat = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                    if (_config.VerboseLogging)
                    {
                        Debug.Log($"Ember 心跳成功 (第 {_successfulHeartbeats} 次)");
                    }

                    OnHeartbeatSuccess?.Invoke(heartbeatResponse?.message ?? "OK");
                }
                else
                {
                    _failedHeartbeats++;
                    string errorMsg = heartbeatResponse?.message ?? "未知错误";
                    SetError($"心跳失败: {errorMsg}");
                    OnHeartbeatFailed?.Invoke(errorMsg);

                    // 连续失败3次，尝试重新注册
                    if (_failedHeartbeats >= 3)
                    {
                        Debug.LogWarning("心跳连续失败，尝试重新注册");
                        yield return ReconnectCoroutine();
                    }
                }
            }

            _isHeartbeatRunning = false;
        }

        private void StopHeartbeat()
        {
            _isHeartbeatRunning = false;
        }

        // ============= 注销 =============

        /// <summary>
        /// 注销实例
        ///
        /// UnregisterAsync()
        ///       ├─> 停止心跳
        ///       ├─> 发送注销请求
        ///       ├─> 关闭 HTTP 监听器
        ///       └─> 清理资源
        /// </summary>
        public void UnregisterAsync(Action<bool> callback = null)
        {
            EditorCoroutineUtility.StartCoroutineOwnerless(UnregisterCoroutine(callback));
        }

        private IEnumerator UnregisterCoroutine(Action<bool> callback)
        {
            SetState(ControllerState.Unregistering);
            StopHeartbeat();

            if (UnityInstance != null && _httpClient != null)
            {
                bool unregisterSuccess = false;
                UnregisterResponse unregisterResponse = null;

                yield return _httpClient.UnregisterAsync(
                    UnityInstance.Id,
                    _config.GetServerUrl(),
                    response =>
                    {
                        unregisterResponse = response;
                        unregisterSuccess = response.success;
                    }
                );

                yield return null;

                if (unregisterSuccess)
                {
                    Debug.Log($"Ember 注销成功: {UnityInstance.Id}");
                }
                else
                {
                    Debug.LogWarning($"Ember 注销失败: {unregisterResponse?.message ?? "未知错误"}");
                }
            }

            // 关闭 HTTP 监听器
            if (_httpListener != null)
            {
                _httpListener.Stop();
                _httpListener = null;
            }

            // 清除会话状态
            ClearSession();

            SetState(ControllerState.Unregistered);
            OnUnregistered?.Invoke();
            callback?.Invoke(true);
        }

        // ============= 重连 =============

        private IEnumerator ReconnectCoroutine()
        {
            SetState(ControllerState.Disconnected);
            StopHeartbeat();

            // 等待一段时间后重试
            yield return new WaitForSeconds(5f);

            InitializeAsync(_config);
        }

        // ============= 状态管理 =============

        private void SetState(ControllerState newState)
        {
            if (_state != newState)
            {
                if (_config != null && _config.VerboseLogging)
                {
                    Debug.Log($"Ember 状态变更: {_state} -> {newState}");
                }
                _state = newState;
                OnStateChanged?.Invoke(newState);
            }
        }

        private void SetError(string error)
        {
            _lastError = error;
            Debug.LogError($"Ember 错误: {error}");
            SetState(ControllerState.Failed);
            OnError?.Invoke(error);
        }

        // ============= 清理 =============

        public void OnDestroy()
        {
            if (IsRegistered)
            {
                UnregisterAsync();
            }

            StopHeartbeat();

            if (_httpListener != null)
            {
                _httpListener.Stop();
                _httpListener = null;
            }
        }

        // ============= 工具方法 =============

        /// <summary>
        /// 获取状态描述
        /// </summary>
        public string GetStateDescription()
        {
            return _state switch
            {
                ControllerState.Uninitialized => "未初始化",
                ControllerState.Initializing => "初始化中",
                ControllerState.FindingPort => "查找端口中",
                ControllerState.StartingListener => "启动监听器",
                ControllerState.Registering => "注册中",
                ControllerState.Registered => "已注册",
                ControllerState.Heartbeating => "心跳中",
                ControllerState.Unregistering => "注销中",
                ControllerState.Unregistered => "已注销",
                ControllerState.Failed => $"失败: {_lastError}",
                ControllerState.Disconnected => "已断开",
                _ => "未知状态"
            };
        }

        /// <summary>
        /// 获取统计信息
        /// </summary>
        public string GetStatistics()
        {
            if (!IsRegistered)
            {
                return "未连接到服务器";
            }

            return $"运行时间: {FormatUptime()}\n" +
                   $"成功心跳: {_successfulHeartbeats}\n" +
                   $"失败心跳: {_failedHeartbeats}\n" +
                   $"实例ID: {UnityInstance?.Id}\n" +
                   $"本地端口: {UnityInstance?.Port}";
        }

        // ============= 编译恢复相关 =============

        /// <summary>
        /// 等待编译完成后恢复连接
        ///
        /// WaitForCompilationAndRestore()
        ///       ├─> 等待1秒确保编译完成
        ///       ├─> 检查当前连接状态
        ///       ├─> 检查会话状态
        ///       ├─> 自动恢复连接
        ///       └─> 保存会话状态
        /// </summary>
        private IEnumerator WaitForCompilationAndRestore()
        {
            // 等待一段时间，确保编译完全完成
            yield return new WaitForSeconds(1f);

            // 检查是否已在运行
            if (IsRegistered)
            {
                if (_config.VerboseLogging)
                {
                    Debug.Log("[Ember] 编译完成，连接已在运行");
                }
                yield break;
            }

            // 检查会话状态（是否之前已连接）
            bool wasConnected = EditorPrefs.GetBool("EmberMCP_Connected", false);

            if (wasConnected)
            {
                Debug.Log("[Ember] 编译完成，自动恢复连接");
                InitializeAsync(_config);
            }
        }

        // ============= 会话管理 =============

        /// <summary>
        /// 保存会话信息
        ///
        /// SaveSession()
        ///       ├─> 保存连接状态
        ///       ├─> 保存实例ID
        ///       ├─> 保存项目名称
        ///       └─> 保存服务器信息
        /// </summary>
        private void SaveSession()
        {
            try
            {
                EditorPrefs.SetBool("EmberMCP_Connected", IsRegistered);
                if (UnityInstance != null)
                {
                    EditorPrefs.SetString("EmberMCP_InstanceId", UnityInstance.Id);
                }
                if (_config != null)
                {
                    EditorPrefs.SetString("EmberMCP_ServerUrl", _config.GetServerUrl());
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Ember] 保存会话失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 清除会话信息
        ///
        /// ClearSession()
        ///       ├─> 清除连接状态
        ///       ├─> 清除实例ID
        ///       ├─> 清除项目名称
        ///       └─> 清除服务器信息
        /// </summary>
        private void ClearSession()
        {
            try
            {
                EditorPrefs.DeleteKey("EmberMCP_Connected");
                EditorPrefs.DeleteKey("EmberMCP_InstanceId");
                EditorPrefs.DeleteKey("EmberMCP_ServerUrl");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Ember] 清除会话失败: {ex.Message}");
            }
        }

        /// <summary>
        /// Unity 编辑器退出事件处理
        ///
        /// OnEditorQuitting()
        ///       ├─> 停止心跳
        ///       ├─> 注销实例
        ///       ├─> 关闭 HTTP 监听器
        ///       └─> 清除会话
        /// </summary>
        private static void OnEditorQuitting()
        {
            if (_instance != null && _instance.IsRegistered)
            {
                Debug.Log("[Ember] Unity 编辑器退出，断开连接");
                _instance.UnregisterAsync();
            }
        }

        private string FormatUptime()
        {
            var uptime = Uptime;
            return $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
        }
    }
}

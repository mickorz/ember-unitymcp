using System;
using UnityEditor;
using UnityEngine;

namespace Ember.Editor
{
    /// <summary>
    /// Ember MCP 控制器示例窗口
    ///
    /// 窗口功能：
    /// EmberRegistryControllerWindow
    ///       ├─> 显示连接状态
    ///       ├─> 手动连接/断开
    ///       ├─> 显示实例信息
    ///       ├─> 显示统计信息
    ///       └─> 显示事件日志
    ///
    /// 使用方式：
    /// 1. 菜单: Window -> Ember -> 控制器示例
    /// 2. 或通过 EmberWindow 打开
    ///
    /// EmberRegistryController 窗口调用流程：
    ///
    /// ShowWindow()
    ///       ├─> 创建/获取窗口实例
    ///       ├─> 订阅控制器事件
    ///       ├─> 启动自动刷新
    ///       └─> 显示窗口
    ///
    /// OnGUI()
    ///       ├─> 绘制状态区域
    ///       ├─> 绘制控制按钮
    ///       ├─> 绘制实例信息
    ///       ├─> 绘制统计信息
    ///       └─> 绘制事件日志
    ///
    /// OnDestroy()
    ///       ├─> 取消事件订阅
    ///       └─> 停止自动刷新
    /// </summary>
    public class EmberRegistryControllerWindow : EditorWindow
    {
        // ============= 字段 =============

        private Vector2 _scrollPosition;
        private bool _showConfig = false;
        private bool _showStatistics = true;
        private bool _showEvents = true;
        private bool _autoScroll = true;

        private string _eventLog = "";
        private int _maxLogLines = 50;

        private double _lastRefreshTime = 0;
        private const double REFRESH_INTERVAL = 0.5; // 刷新间隔（秒）

        // ============= 菜单项 =============

        [MenuItem("Window/Ember/控制器示例")]
        public static void ShowWindow()
        {
            var window = GetWindow<EmberRegistryControllerWindow>("Ember 控制器");
            window.minSize = new Vector2(400, 500);
            window.Show();
        }

        // ============= Unity 生命周期 =============

        private void OnEnable()
        {
            // 订阅控制器事件
            SubscribeToEvents();

            AddLog("[窗口] 已打开");
        }

        private void OnDisable()
        {
            // 取消订阅事件
            UnsubscribeFromEvents();

            AddLog("[窗口] 已关闭");
        }

        private void Update()
        {
            // 自动刷新
            if (EditorApplication.timeSinceStartup - _lastRefreshTime > REFRESH_INTERVAL)
            {
                _lastRefreshTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }

        private void OnGUI()
        {
            // 标题
            GUILayout.Label("Ember MCP 控制器示例", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            // 获取控制器
            var controller = EmberRegistryController.Instance;
            if (controller == null)
            {
                EditorGUILayout.HelpBox("控制器未初始化", MessageType.Warning);
                return;
            }

            // 状态区域
            DrawStatusSection(controller);
            EditorGUILayout.Space();

            // 控制按钮
            DrawControlButtons(controller);
            EditorGUILayout.Space();

            // 实例信息
            DrawInstanceInfo(controller);
            EditorGUILayout.Space();

            // 统计信息
            DrawStatisticsSection(controller);
            EditorGUILayout.Space();

            // 事件日志
            DrawEventLogSection();
        }

        // ============= 绘制方法 =============

        /// <summary>
        /// 绘制状态区域
        ///
        /// DrawStatusSection()
        ///       ├─> 显示当前状态
        ///       ├─> 显示运行时间
        ///       ├─> 显示最后错误
        ///       └─> 颜色编码状态显示
        /// </summary>
        private void DrawStatusSection(EmberRegistryController controller)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("连接状态", EditorStyles.boldLabel);

            // 状态
            var stateColor = GetStateColor(controller.State);
            var oldColor = GUI.color;
            GUI.color = stateColor;
            EditorGUILayout.LabelField($"状态: {controller.GetStateDescription()}", EditorStyles.boldLabel);
            GUI.color = oldColor;

            // 运行时间
            if (controller.IsRegistered)
            {
                EditorGUILayout.LabelField($"运行时间: {FormatUptime(controller.Uptime)}");
            }

            // 最后错误
            if (!string.IsNullOrEmpty(controller.LastError))
            {
                EditorGUILayout.HelpBox($"错误: {controller.LastError}", MessageType.Error);
            }

            EditorGUILayout.EndVertical();
        }

        /// <summary>
        /// 绘制控制按钮
        ///
        /// DrawControlButtons()
        ///       ├─> 连接按钮
        ///       ├─> 断开按钮
        ///       ├─> 打印配置按钮
        ///       └─> 清空日志按钮
        /// </summary>
        private void DrawControlButtons(EmberRegistryController controller)
        {
            EditorGUILayout.BeginHorizontal();

            // 连接/断开按钮
            GUI.enabled = !controller.IsConnecting && !controller.IsRegistered;
            if (GUILayout.Button("连接到服务器", GUILayout.Height(30)))
            {
                ConnectToServer();
            }
            GUI.enabled = true;

            GUI.enabled = controller.IsRegistered;
            if (GUILayout.Button("断开连接", GUILayout.Height(30)))
            {
                DisconnectFromServer();
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();

            // 其他按钮
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("打印配置"))
            {
                PrintConfig();
            }

            if (GUILayout.Button("清空日志"))
            {
                _eventLog = "";
            }

            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// 绘制实例信息
        ///
        /// DrawInstanceInfo()
        ///       ├─> 实例ID
        ///       ├─> 实例名称
        ///       └─> 本地端口
        /// </summary>
        private void DrawInstanceInfo(EmberRegistryController controller)
        {
            if (controller.UnityInstance == null)
            {
                return;
            }

            _showConfig = EditorGUILayout.Foldout(_showConfig, "实例信息");
            if (_showConfig)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"实例 ID: {controller.UnityInstance.Id}");
                EditorGUILayout.LabelField($"实例名称: {controller.UnityInstance.Name}");
                EditorGUILayout.LabelField($"本地端口: {controller.UnityInstance.Port}");
                EditorGUILayout.LabelField($"Unity 版本: {controller.UnityInstance.UnityVersion}");
                EditorGUILayout.EndVertical();
            }
        }

        /// <summary>
        /// 绘制统计信息
        ///
        /// DrawStatisticsSection()
        ///       ├─> 成功心跳次数
        ///       ├─> 失败心跳次数
        ///       └─> 心跳成功率
        /// </summary>
        private void DrawStatisticsSection(EmberRegistryController controller)
        {
            _showStatistics = EditorGUILayout.Foldout(_showStatistics, "统计信息");
            if (_showStatistics)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField($"成功心跳: {controller.SuccessfulHeartbeats}");
                EditorGUILayout.LabelField($"失败心跳: {controller.FailedHeartbeats}");

                int total = controller.SuccessfulHeartbeats + controller.FailedHeartbeats;
                if (total > 0)
                {
                    float successRate = (float)controller.SuccessfulHeartbeats / total * 100;
                    EditorGUILayout.LabelField($"成功率: {successRate:F1}%");
                }

                EditorGUILayout.EndVertical();
            }
        }

        /// <summary>
        /// 绘制事件日志
        ///
        /// DrawEventLogSection()
        ///       ├─> 显示事件历史
        ///       ├─> 自动滚动开关
        ///       └─> 清空日志按钮
        /// </summary>
        private void DrawEventLogSection()
        {
            _showEvents = EditorGUILayout.Foldout(_showEvents, "事件日志");
            if (_showEvents)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                // 选项
                EditorGUILayout.BeginHorizontal();
                _autoScroll = EditorGUILayout.Toggle("自动滚动", _autoScroll);
                EditorGUILayout.EndHorizontal();

                // 日志内容
                _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(150));
                EditorGUILayout.TextArea(_eventLog, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();

                EditorGUILayout.EndVertical();

                // 自动滚动到底部
                if (_autoScroll)
                {
                    // 注意：这里需要在下一个帧执行，因为文本框内容可能刚更新
                }
            }
        }

        // ============= 事件订阅 =============

        /// <summary>
        /// 订阅控制器事件
        ///
        /// SubscribeToEvents()
        ///       ├─> 订阅状态变更
        ///       ├─> 订阅错误
        ///       ├─> 订阅注册成功
        ///       ├─> 订阅注销
        ///       ├─> 订阅心跳成功
        ///       └─> 订阅心跳失败
        /// </summary>
        private void SubscribeToEvents()
        {
            var controller = EmberRegistryController.Instance;
            if (controller == null) return;

            controller.OnStateChanged += OnStateChanged;
            controller.OnError += OnError;
            controller.OnRegistered += OnRegistered;
            controller.OnUnregistered += OnUnregistered;
            controller.OnHeartbeatSuccess += OnHeartbeatSuccess;
            controller.OnHeartbeatFailed += OnHeartbeatFailed;
        }

        private void UnsubscribeFromEvents()
        {
            var controller = EmberRegistryController.Instance;
            if (controller == null) return;

            controller.OnStateChanged -= OnStateChanged;
            controller.OnError -= OnError;
            controller.OnRegistered -= OnRegistered;
            controller.OnUnregistered -= OnUnregistered;
            controller.OnHeartbeatSuccess -= OnHeartbeatSuccess;
            controller.OnHeartbeatFailed -= OnHeartbeatFailed;
        }

        // ============= 事件处理 =============

        private void OnStateChanged(EmberRegistryController.ControllerState newState)
        {
            AddLog($"[状态] {newState}");
        }

        private void OnError(string error)
        {
            AddLog($"[错误] {error}");
        }

        private void OnRegistered()
        {
            AddLog("[注册] 已成功注册到服务器");

            var controller = EmberRegistryController.Instance;
            if (controller.UnityInstance != null)
            {
                AddLog($"[注册] 实例ID: {controller.UnityInstance.Id}");
                AddLog($"[注册] 本地端口: {controller.UnityInstance.Port}");
            }
        }

        private void OnUnregistered()
        {
            AddLog("[注销] 已从服务器注销");
        }

        private void OnHeartbeatSuccess(string message)
        {
            // 可选：记录每次心跳（可能会产生大量日志）
            // AddLog($"[心跳] 成功: {message}");
        }

        private void OnHeartbeatFailed(string error)
        {
            AddLog($"[心跳] 失败: {error}");
        }

        // ============= 公共方法 =============

        /// <summary>
        /// 手动连接到服务器
        ///
        /// ConnectToServer()
        ///       ├─> 获取配置
        ///       ├─> 调用控制器初始化
        ///       └─> 记录结果
        /// </summary>
        public void ConnectToServer()
        {
            var controller = EmberRegistryController.Instance;
            var config = EmberConfig.Load();

            AddLog("[操作] 开始连接...");

            controller.InitializeAsync(config, success =>
            {
                AddLog($"[操作] 连接结果: {(success ? "成功" : "失败")}");
            });
        }

        /// <summary>
        /// 断开与服务器的连接
        ///
        /// DisconnectFromServer()
        ///       ├─> 调用控制器注销
        ///       └─> 记录结果
        /// </summary>
        public void DisconnectFromServer()
        {
            var controller = EmberRegistryController.Instance;

            AddLog("[操作] 开始断开...");

            controller.UnregisterAsync(success =>
            {
                AddLog($"[操作] 断开结果: {(success ? "成功" : "失败")}");
            });
        }

        /// <summary>
        /// 打印配置信息
        /// </summary>
        public void PrintConfig()
        {
            var config = EmberConfig.Load();
            AddLog("[配置] " + config.GetSummary());
            Debug.Log("[Ember] 配置信息:\n" + config.GetSummary());
        }

        // ============= 私有辅助方法 =============

        /// <summary>
        /// 添加日志
        /// </summary>
        private void AddLog(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            _eventLog += $"[{timestamp}] {message}\n";

            // 限制日志行数
            string[] lines = _eventLog.Split('\n');
            if (lines.Length > _maxLogLines)
            {
                int skip = lines.Length - _maxLogLines;
                _eventLog = string.Join("\n", lines, skip, _maxLogLines);
            }
        }

        /// <summary>
        /// 获取状态对应的颜色
        /// </summary>
        private Color GetStateColor(EmberRegistryController.ControllerState state)
        {
            return state switch
            {
                EmberRegistryController.ControllerState.Registered => Color.green,
                EmberRegistryController.ControllerState.Heartbeating => new Color(0f, 0.5f, 1f),
                EmberRegistryController.ControllerState.Failed => Color.red,
                EmberRegistryController.ControllerState.Disconnected => Color.yellow,
                _ => Color.white
            };
        }

        /// <summary>
        /// 格式化运行时间
        /// </summary>
        private string FormatUptime(TimeSpan uptime)
        {
            return $"{uptime.Hours:D2}:{uptime.Minutes:D2}:{uptime.Seconds:D2}";
        }
    }
}

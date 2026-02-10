using System;
using UnityEditor;
using UnityEngine;

namespace Ember.Editor
{
    /// <summary>
    /// Ember MCP 管理窗口
    ///
    /// 窗口更新流程：
    ///
    /// EmberWindow.OnGUI()
    ///       ├─> 每帧重新绘制界面
    ///       ├─> 绘制状态卡片
    ///       ├─> 绘制控制按钮
    ///       ├─> 绘制配置区域
    ///       └─> 每秒自动刷新状态
    /// </summary>
    public class EmberWindow : EditorWindow
    {
        // ============= 常量 =============

        private const string WINDOW_TITLE = "Ember MCP";
        private const int REFRESH_INTERVAL = 1; // 秒

        // ============= 字段 =============

        private static EmberWindow _instance;
        private EmberConfig _config;
        private EmberRegistryController _controller;
        private Vector2 _scrollPosition;
        private double _lastRefreshTime;
        private bool _showConfig;
        private bool _showStatistics;

        // ============= 菜单项 =============

        [MenuItem("Window/Ember MCP Manager")]
        public static void ShowWindow()
        {
            var window = GetWindow<EmberWindow>(WINDOW_TITLE);
            window.minSize = new Vector2(400, 300);
            window.Show();
        }

        // ============= Unity 生命周期 =============

        private void OnEnable()
        {
            _instance = this;
            _config = EmberConfig.Load();
            EnsureControllerInstance();

            // 订阅控制器事件
            if (_controller != null)
            {
                _controller.OnStateChanged += OnControllerStateChanged;
                _controller.OnError += OnControllerError;
                _controller.OnRegistered += OnControllerRegistered;
                _controller.OnUnregistered += OnControllerUnregistered;
            }

            // 启动自动刷新
            EditorApplication.update += OnEditorUpdate;

            // 自动启动（如果配置启用）
            if (_config.AutoRegister)
            {
                ConnectAsync();
            }
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;

            if (_controller != null)
            {
                _controller.OnStateChanged -= OnControllerStateChanged;
                _controller.OnError -= OnControllerError;
                _controller.OnRegistered -= OnControllerRegistered;
                _controller.OnUnregistered -= OnControllerUnregistered;
            }
        }

        private void OnDestroy()
        {
            // 不要在窗口关闭时断开连接
            // 连接由控制器管理，Unity 退出时自动清理
        }

        // ============= 更新 =============

        private void OnEditorUpdate()
        {
            // 定期刷新界面
            double currentTime = EditorApplication.timeSinceStartup;
            if (currentTime - _lastRefreshTime >= REFRESH_INTERVAL)
            {
                _lastRefreshTime = currentTime;
                Repaint();
            }
        }

        // ============= GUI =============

        private void OnGUI()
        {
            EditorGUILayout.Space(8);

            // 标题
            DrawHeader();

            EditorGUILayout.Space(8);

            // 状态卡片
            DrawStatusCard();

            EditorGUILayout.Space(8);

            // 控制按钮
            DrawControlButtons();

            EditorGUILayout.Space(8);

            // 配置区域
            if (_showConfig)
            {
                DrawConfigSection();
                EditorGUILayout.Space(8);
            }

            // 统计信息
            if (_showStatistics)
            {
                DrawStatisticsSection();
                EditorGUILayout.Space(8);
            }

            // 快捷链接
            DrawQuickLinks();

            EditorGUILayout.Space(8);
        }

        // ============= 标题 =============

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Ember MCP 连接管理", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();

                _showConfig = GUILayout.Toggle(_showConfig, "配置", EditorStyles.toolbarButton, GUILayout.Width(60));
                _showStatistics = GUILayout.Toggle(_showStatistics, "统计", EditorStyles.toolbarButton, GUILayout.Width(60));
            }
        }

        // ============= 状态卡片 =============

        /// <summary>
        /// 绘制状态卡片
        ///
        /// DrawStatusCard()
        ///       ├─> 绘制状态指示器
        ///       ├─> 显示连接状态
        ///       ├─> 显示实例信息
        ///       └─> 显示服务器信息
        /// </summary>
        private void DrawStatusCard()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                DrawStatusIndicator();
                EditorGUILayout.Space(4);
                DrawInstanceInfo();
                DrawServerInfo();
            }
        }

        private void DrawStatusIndicator()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("状态:", GUILayout.Width(50));

                GUIContent statusContent;
                GUIStyle statusStyle;

                if (_controller == null)
                {
                    statusContent = new GUIContent("未初始化");
                    statusStyle = GetStatusStyle("未初始化");
                }
                else
                {
                    string stateText = _controller.GetStateDescription();
                    statusContent = new GUIContent(stateText);
                    statusStyle = GetStatusStyle(stateText);
                }

                EditorGUILayout.LabelField(statusContent, statusStyle);
                GUILayout.FlexibleSpace();

                // 运行时间
                if (_controller != null && _controller.IsRegistered)
                {
                    EditorGUILayout.LabelField($"运行时间: {_controller.Uptime:hh\\:mm\\:ss}", EditorStyles.miniLabel, GUILayout.Width(120));
                }
            }
        }

        private void DrawInstanceInfo()
        {
            if (_config == null) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("实例ID:", EditorStyles.miniLabel, GUILayout.Width(70));
                EditorGUILayout.LabelField(_config.InstanceId ?? "无", EditorStyles.miniLabel);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("项目名称:", EditorStyles.miniLabel, GUILayout.Width(70));
                EditorGUILayout.LabelField(_config.ProjectName ?? "无", EditorStyles.miniLabel);
            }

            if (_controller?.UnityInstance != null)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField("本地端口:", EditorStyles.miniLabel, GUILayout.Width(70));
                    EditorGUILayout.LabelField(_controller.UnityInstance.Port.ToString(), EditorStyles.miniLabel);
                }
            }
        }

        private void DrawServerInfo()
        {
            if (_config == null) return;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("服务器:", EditorStyles.miniLabel, GUILayout.Width(70));
                EditorGUILayout.LabelField($"{_config.ServerUrl}:{_config.ServerPort}", EditorStyles.miniLabel);
            }
        }

        // ============= 控制按钮 =============

        /// <summary>
        /// 绘制控制按钮
        ///
        /// DrawControlButtons()
        ///       ├─> 连接按钮（未连接时显示）
        ///       ├─> 断开按钮（已连接时显示）
        ///       ├─> 刷新按钮
        ///       └─> 重置配置按钮
        /// </summary>
        private void DrawControlButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = !(_controller?.IsConnecting ?? false) && !(_controller?.IsRegistered ?? false);

                if (GUILayout.Button("连接到服务器", GUILayout.Height(30)))
                {
                    ConnectAsync();
                }

                GUI.enabled = true;

                GUI.enabled = _controller?.IsRegistered ?? false;

                if (GUILayout.Button("断开连接", GUILayout.Height(30)))
                {
                    DisconnectAsync();
                }

                GUI.enabled = true;

                if (GUILayout.Button("刷新状态", GUILayout.Height(30)))
                {
                    RefreshStatus();
                }

                if (GUILayout.Button("重置配置", GUILayout.Height(30)))
                {
                    ResetConfig();
                }
            }
        }

        // ============= 配置区域 =============

        private void DrawConfigSection()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("服务器配置", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                DrawServerConfig();
                DrawInstanceConfig();
                DrawBehaviorConfig();
                DrawConfigButtons();
            }
        }

        private void DrawServerConfig()
        {
            EditorGUILayout.LabelField("服务器设置", EditorStyles.miniLabel);

            _config.ServerUrl = EditorGUILayout.TextField("服务器地址:", _config.ServerUrl);
            _config.ServerPort = EditorGUILayout.IntField("服务器端口:", _config.ServerPort);
            _config.ApiKey = EditorGUILayout.PasswordField("API 密钥 (可选):", _config.ApiKey);

            EditorGUILayout.Space(4);
        }

        private void DrawInstanceConfig()
        {
            EditorGUILayout.LabelField("实例设置", EditorStyles.miniLabel);

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.TextField("实例 ID:", _config.InstanceId);
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.LabelField("端口范围:");

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("从", GUILayout.Width(30));
                _config.MinPort = EditorGUILayout.IntField(_config.MinPort, GUILayout.Width(60));
                EditorGUILayout.LabelField("到", GUILayout.Width(20));
                _config.MaxPort = EditorGUILayout.IntField(_config.MaxPort, GUILayout.Width(60));
                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.Space(4);
        }

        private void DrawBehaviorConfig()
        {
            EditorGUILayout.LabelField("行为设置", EditorStyles.miniLabel);

            _config.AutoRegister = EditorGUILayout.Toggle("启动时自动连接:", _config.AutoRegister);
            _config.AutoRestoreOnRecompile = EditorGUILayout.Toggle(new GUIContent(
                "编译后自动恢复:",
                "Unity 重新编译后，自动恢复连接状态\n\n勾选后，当 Unity 编译完成时：\n- 如果之前已连接，会自动重新连接\n- 避免每次编译后手动重新连接"
            ), _config.AutoRestoreOnRecompile);
            _config.HeartbeatInterval = EditorGUILayout.IntSlider("心跳间隔 (毫秒):", _config.HeartbeatInterval, 5000, 60000);
            _config.VerboseLogging = EditorGUILayout.Toggle("显示详细日志:", _config.VerboseLogging);

            EditorGUILayout.Space(4);
        }

        private void DrawConfigButtons()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("保存配置"))
                {
                    _config.Save();
                    Debug.Log("Ember 配置已保存");
                }

                if (GUILayout.Button("重新生成实例ID"))
                {
                    _config.GenerateInstanceId();
                    _config.Save();
                    Debug.Log($"已重新生成实例ID: {_config.InstanceId}");
                }
            }
        }

        // ============= 统计信息 =============

        private void DrawStatisticsSection()
        {
            if (_controller == null) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("连接统计", EditorStyles.boldLabel);
                EditorGUILayout.Space(4);

                EditorGUILayout.LabelField(_controller.GetStatistics(), EditorStyles.wordWrappedLabel);
            }
        }

        // ============= 快捷链接 =============

        private void DrawQuickLinks()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("健康检查"))
                {
                    CheckHealth();
                }

                if (GUILayout.Button("打开文档"))
                {
                    Application.OpenURL("https://github.com/mickorz/ember-mcp");
                }

                if (GUILayout.Button("反馈问题"))
                {
                    Application.OpenURL("https://github.com/mickorz/ember-mcp/issues");
                }
            }
        }

        // ============= 辅助方法 =============

        private GUIStyle GetStatusStyle(string status)
        {
            var style = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold
            };

            if (status.Contains("心跳中") || status.Contains("已注册"))
            {
                style.normal.textColor = new Color(0.2f, 0.8f, 0.2f); // 绿色
            }
            else if (status.Contains("连接中") || status.Contains("注册中") || status.Contains("初始化"))
            {
                style.normal.textColor = new Color(0.8f, 0.6f, 0.2f); // 橙色
            }
            else if (status.Contains("失败") || status.Contains("错误") || status.Contains("断开"))
            {
                style.normal.textColor = new Color(0.8f, 0.2f, 0.2f); // 红色
            }
            else
            {
                style.normal.textColor = new Color(0.5f, 0.5f, 0.5f); // 灰色
            }

            return style;
        }

        private void EnsureControllerInstance()
        {
            if (_controller == null)
            {
                _controller = EmberRegistryController.Instance;
            }
        }

        // ============= 操作方法 =============

        private void ConnectAsync()
        {
            EnsureControllerInstance();

            // 重新加载配置以获取最新设置
            _config = EmberConfig.Load();

            Debug.Log("正在连接到 Ember MCP 服务器...");
            _controller.InitializeAsync(_config, success =>
            {
                if (success)
                {
                    Debug.Log("已成功连接到 Ember MCP 服务器");
                }
                else
                {
                    Debug.LogError("连接到 Ember MCP 服务器失败");
                }
            });
        }

        private void DisconnectAsync()
        {
            if (_controller == null) return;

            Debug.Log("正在从 Ember MCP 服务器断开...");
            _controller.UnregisterAsync(success =>
            {
                if (success)
                {
                    Debug.Log("已从 Ember MCP 服务器断开");
                }
            });
        }

        private void RefreshStatus()
        {
            EnsureControllerInstance();
            Repaint();
        }

        private void ResetConfig()
        {
            if (EditorUtility.DisplayDialog("重置配置", "确定要重置所有配置为默认值吗？", "确定", "取消"))
            {
                _config.ResetToDefaults();
                _config.Save();
                Debug.Log("Ember 配置已重置为默认值");
            }
        }

        private async void CheckHealth()
        {
            if (_config == null) return;

            var client = new EmberHttpClient(_config.ApiKey);
            var healthUrl = _config.GetHealthUrl();
            Debug.Log($"正在检查服务器健康状态: {healthUrl}");

            EditorCoroutineUtility.StartCoroutineOwnerless(
                client.HealthCheckAsync(_config.GetServerUrl(), response =>
                {
                    if (response.isOnline)
                    {
                        Debug.Log($"服务器在线: {response.message}");
                        EditorUtility.DisplayDialog("健康检查", "服务器在线", "确定");
                    }
                    else
                    {
                        Debug.LogWarning($"服务器离线: {response.message}");
                        EditorUtility.DisplayDialog("健康检查", "服务器离线", "确定");
                    }
                })
            );
        }

        // ============= 事件处理器 =============

        private void OnControllerStateChanged(EmberRegistryController.ControllerState newState)
        {
            Repaint();
        }

        private void OnControllerError(string error)
        {
            Debug.LogError($"Ember 错误: {error}");
            EditorUtility.DisplayDialog("Ember 错误", error, "确定");
        }

        private void OnControllerRegistered()
        {
            Debug.Log("Ember 实例已成功注册");
        }

        private void OnControllerUnregistered()
        {
            Debug.Log("Ember 实例已注销");
        }
    }
}

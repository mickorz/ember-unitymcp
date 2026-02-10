using System;
using UnityEditor;
using UnityEngine;

namespace Ember.Editor
{
    /// <summary>
    /// Ember MCP 配置管理类
    ///
    /// 配置加载和保存流程：
    ///
    /// EmberConfig.Load()
    ///       ├─> 从 EditorPrefs 读取 JSON 字符串
    ///       ├─> 解析为配置对象
    ///       └─> 返回配置实例
    ///
    /// EmberConfig.Save()
    ///       ├─> 序列化为 JSON 字符串
    ///       └─> 保存到 EditorPrefs
    /// </summary>
    public class EmberConfig
    {
        // ============= 常量定义 =============

        private const string PREF_KEY = "EmberMCP_Config";
        private const string DEFAULT_SERVER_URL = "localhost";
        private const int DEFAULT_SERVER_PORT = 4000;
        private const int DEFAULT_HEARTBEAT_INTERVAL = 10000;
        private const string VERSION = "1.0.0";

        // ============= 服务器配置 =============

        /// <summary>
        /// ember-mcp 服务器地址
        /// </summary>
        public string ServerUrl { get; set; } = DEFAULT_SERVER_URL;

        /// <summary>
        /// ember-mcp 服务器端口
        /// </summary>
        public int ServerPort { get; set; } = DEFAULT_SERVER_PORT;

        /// <summary>
        /// API 密钥（可选）
        /// </summary>
        public string ApiKey { get; set; } = string.Empty;

        // ============= 实例配置 =============

        /// <summary>
        /// Unity 实例唯一标识符（自动生成）
        /// </summary>
        public string InstanceId { get; set; } = string.Empty;

        /// <summary>
        /// Unity 项目名称（自动获取）
        /// </summary>
        public string ProjectName { get; set; } = string.Empty;

        /// <summary>
        /// 本地监听端口范围（最小值）
        /// </summary>
        public int MinPort { get; set; } = 8000;

        /// <summary>
        /// 本地监听端口范围（最大值）
        /// </summary>
        public int MaxPort { get; set; } = 9000;

        // ============= 行为配置 =============

        /// <summary>
        /// 启动时自动注册到 ember-mcp
        /// </summary>
        public bool AutoRegister { get; set; } = true;

        /// <summary>
        /// 编译后自动恢复连接
        /// </summary>
        public bool AutoRestoreOnRecompile { get; set; } = true;

        /// <summary>
        /// 心跳间隔（毫秒）
        /// </summary>
        public int HeartbeatInterval { get; set; } = DEFAULT_HEARTBEAT_INTERVAL;

        /// <summary>
        /// 显示详细日志
        /// </summary>
        public bool VerboseLogging { get; set; } = false;

        // ============= 获取完整服务器 URL =============

        /// <summary>
        /// 获取完整的服务器注册 URL
        /// </summary>
        public string GetServerUrl()
        {
            return $"http://{ServerUrl}:{ServerPort}";
        }

        /// <summary>
        /// 获取注册接口 URL
        /// </summary>
        public string GetRegisterUrl()
        {
            return $"{GetServerUrl()}/register";
        }

        /// <summary>
        /// 获取心跳接口 URL
        /// </summary>
        public string GetHeartbeatUrl()
        {
            return $"{GetServerUrl()}/heartbeat";
        }

        /// <summary>
        /// 获取注销接口 URL
        /// </summary>
        public string GetUnregisterUrl()
        {
            return $"{GetServerUrl()}/unregister";
        }

        /// <summary>
        /// 获取健康检查接口 URL
        /// </summary>
        public string GetHealthUrl()
        {
            return $"{GetServerUrl()}/health";
        }

        // ============= 静态方法 =============

        /// <summary>
        /// 加载配置
        ///
        /// EmberConfig.Load()
        ///       ├─> 从 EditorPrefs 读取 JSON
        ///       ├─> 反序列化为对象
        ///       ├─> 补充默认值
        ///       └─> 返回配置实例
        /// </summary>
        public static EmberConfig Load()
        {
            try
            {
                string json = EditorPrefs.GetString(PREF_KEY, string.Empty);
                if (string.IsNullOrEmpty(json))
                {
                    return CreateDefault();
                }

                var config = JsonUtility.FromJson<EmberConfig>(json);
                if (config == null)
                {
                    return CreateDefault();
                }

                // 确保项目名称是最新的
                config.ProjectName = GetCurrentProjectName();

                // 如果没有实例ID，生成一个
                if (string.IsNullOrEmpty(config.InstanceId))
                {
                    config.GenerateInstanceId();
                }

                return config;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"加载 Ember 配置失败: {e.Message}，使用默认配置");
                return CreateDefault();
            }
        }

        /// <summary>
        /// 保存配置
        ///
        /// EmberConfig.Save()
        ///       ├─> 序列化为 JSON
        ///       └─> 保存到 EditorPrefs
        /// </summary>
        public void Save()
        {
            try
            {
                string json = JsonUtility.ToJson(this, true);
                EditorPrefs.SetString(PREF_KEY, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"保存 Ember 配置失败: {e.Message}");
            }
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        public static EmberConfig CreateDefault()
        {
            var config = new EmberConfig
            {
                ServerUrl = DEFAULT_SERVER_URL,
                ServerPort = DEFAULT_SERVER_PORT,
                ApiKey = string.Empty,
                InstanceId = string.Empty,
                ProjectName = GetCurrentProjectName(),
                MinPort = 8000,
                MaxPort = 9000,
                AutoRegister = true,
                AutoRestoreOnRecompile = true,
                HeartbeatInterval = DEFAULT_HEARTBEAT_INTERVAL,
                VerboseLogging = false
            };
            config.GenerateInstanceId();
            return config;
        }

        /// <summary>
        /// 重置为默认值
        /// </summary>
        public void ResetToDefaults()
        {
            ServerUrl = DEFAULT_SERVER_URL;
            ServerPort = DEFAULT_SERVER_PORT;
            ApiKey = string.Empty;
            MinPort = 8000;
            MaxPort = 9000;
            AutoRegister = true;
            AutoRestoreOnRecompile = true;
            HeartbeatInterval = DEFAULT_HEARTBEAT_INTERVAL;
            VerboseLogging = false;
            ProjectName = GetCurrentProjectName();
            GenerateInstanceId();
        }

        /// <summary>
        /// 生成实例唯一标识符
        ///
        /// 生成格式：{项目名}-{短GUID}
        /// 示例：MyProject-a3f5b2
        /// </summary>
        public void GenerateInstanceId()
        {
            string projectName = GetCurrentProjectName();
            string shortGuid = Guid.NewGuid().ToString("N").Substring(0, 8);
            InstanceId = $"{SanitizeInstanceId(projectName)}-{shortGuid}";
        }

        /// <summary>
        /// 清理项目名称作为实例ID前缀
        /// </summary>
        private static string SanitizeInstanceId(string projectName)
        {
            if (string.IsNullOrEmpty(projectName))
            {
                return "Unity";
            }

            // 移除特殊字符，只保留字母、数字和连字符
            var sanitized = System.Text.RegularExpressions.Regex.Replace(projectName, "[^a-zA-Z0-9-]", "");
            return string.IsNullOrEmpty(sanitized) ? "Unity" : sanitized;
        }

        /// <summary>
        /// 获取当前 Unity 项目名称
        /// </summary>
        private static string GetCurrentProjectName()
        {
            // 从路径中提取项目名称
            string[] pathParts = Application.dataPath.Split('/');
            string projectName = pathParts.Length > 0 ? pathParts[pathParts.Length - 2] : "UnityProject";
            return System.IO.Path.GetFileNameWithoutExtension(projectName);
        }

        /// <summary>
        /// 获取配置摘要信息
        /// </summary>
        public string GetSummary()
        {
            return $"Ember MCP v{VERSION}\n" +
                   $"服务器: {ServerUrl}:{ServerPort}\n" +
                   $"实例ID: {InstanceId}\n" +
                   $"项目: {ProjectName}\n" +
                   $"自动注册: {(AutoRegister ? "是" : "否")}";
        }

        /// <summary>
        /// 删除配置
        /// </summary>
        public static void Delete()
        {
            EditorPrefs.DeleteKey(PREF_KEY);
        }

        /// <summary>
        /// 检查配置是否有效
        /// </summary>
        public bool IsValid()
        {
            return !string.IsNullOrEmpty(ServerUrl) &&
                   ServerPort > 0 && ServerPort <= 65535 &&
                   !string.IsNullOrEmpty(InstanceId) &&
                   MinPort >= 1024 && MinPort <= 65535 &&
                   MaxPort >= MinPort && MaxPort <= 65535 &&
                   HeartbeatInterval >= 1000;
        }
    }
}

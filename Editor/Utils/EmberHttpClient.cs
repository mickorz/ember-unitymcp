using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Ember.Editor
{
    /// <summary>
    /// ember-mcp HTTP 客户端
    ///
    /// HTTP 请求流程：
    ///
    /// EmberHttpClient.SendRequest()
    ///       ├─> 构建 UnityWebRequest
    ///       ├─> 添加请求头（Content-Type, X-API-Key）
    ///       ├─> 发送请求
    ///       ├─> 等待响应
    ///       └─> 解析并返回结果
    /// </summary>
    public class EmberHttpClient
    {
        private readonly string _apiKey;
        private readonly int _timeoutSeconds = 10;

        public EmberHttpClient(string apiKey = null)
        {
            _apiKey = apiKey;
        }

        // ============= 注册相关 =============

        /// <summary>
        /// 注册 Unity 实例到 ember-mcp
        ///
        /// RegisterAsync()
        ///       ├─> 构建注册请求数据
        ///       ├─> POST /register
        ///       ├─> 等待响应
        ///       └─> 返回注册结果
        /// </summary>
        public IEnumerator RegisterAsync(UnityInstance instance, string serverUrl, Action<RegisterResponse> callback)
        {
            string url = $"{serverUrl}/register";
            string jsonData = JsonUtility.ToJson(new RegisterRequest
            {
                id = instance.Id,
                name = instance.Name,
                port = instance.Port
            });

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonData));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                if (!string.IsNullOrEmpty(_apiKey))
                {
                    request.SetRequestHeader("X-API-Key", _apiKey);
                }

                request.timeout = _timeoutSeconds;

                yield return request.SendWebRequest();

                var response = new RegisterResponse();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        response = JsonUtility.FromJson<RegisterResponse>(request.downloadHandler.text);
                        response.success = true;
                    }
                    catch (Exception e)
                    {
                        response.success = false;
                        response.message = $"解析响应失败: {e.Message}";
                    }
                }
                else
                {
                    response.success = false;
                    response.message = GetErrorMessage(request);
                }

                callback?.Invoke(response);
            }
        }

        // ============= 心跳相关 =============

        /// <summary>
        /// 发送心跳维持在线状态
        ///
        /// SendHeartbeatAsync()
        ///       ├─> 构建心跳请求数据
        ///       ├─> POST /heartbeat
        ///       ├─> 等待响应
        ///       └─> 返回心跳结果
        /// </summary>
        public IEnumerator SendHeartbeatAsync(string instanceId, string serverUrl, Action<HeartbeatResponse> callback)
        {
            string url = $"{serverUrl}/heartbeat";
            string jsonData = JsonUtility.ToJson(new HeartbeatRequest
            {
                id = instanceId
            });

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonData));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                if (!string.IsNullOrEmpty(_apiKey))
                {
                    request.SetRequestHeader("X-API-Key", _apiKey);
                }

                request.timeout = _timeoutSeconds;

                yield return request.SendWebRequest();

                var response = new HeartbeatResponse();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        response = JsonUtility.FromJson<HeartbeatResponse>(request.downloadHandler.text);
                        response.success = true;
                    }
                    catch (Exception e)
                    {
                        response.success = false;
                        response.message = $"解析响应失败: {e.Message}";
                    }
                }
                else
                {
                    response.success = false;
                    response.message = GetErrorMessage(request);
                }

                callback?.Invoke(response);
            }
        }

        // ============= 注销相关 =============

        /// <summary>
        /// 从 ember-mcp 注销 Unity 实例
        ///
        /// UnregisterAsync()
        ///       ├─> 构建注销请求数据
        ///       ├─> POST /unregister
        ///       ├─> 等待响应
        ///       └─> 返回注销结果
        /// </summary>
        public IEnumerator UnregisterAsync(string instanceId, string serverUrl, Action<UnregisterResponse> callback)
        {
            string url = $"{serverUrl}/unregister";
            string jsonData = JsonUtility.ToJson(new UnregisterRequest
            {
                id = instanceId
            });

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonData));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                if (!string.IsNullOrEmpty(_apiKey))
                {
                    request.SetRequestHeader("X-API-Key", _apiKey);
                }

                request.timeout = _timeoutSeconds;

                yield return request.SendWebRequest();

                var response = new UnregisterResponse();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        response = JsonUtility.FromJson<UnregisterResponse>(request.downloadHandler.text);
                        response.success = true;
                    }
                    catch (Exception e)
                    {
                        response.success = false;
                        response.message = $"解析响应失败: {e.Message}";
                    }
                }
                else
                {
                    response.success = false;
                    response.message = GetErrorMessage(request);
                }

                callback?.Invoke(response);
            }
        }

        // ============= 健康检查 =============

        /// <summary>
        /// 检查 ember-mcp 服务器健康状态
        ///
        /// HealthCheckAsync()
        ///       ├─> GET /health
        ///       ├─> 等待响应
        ///       └─> 返回健康状态
        /// </summary>
        public IEnumerator HealthCheckAsync(string serverUrl, Action<HealthResponse> callback)
        {
            string url = $"{serverUrl}/health";

            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                if (!string.IsNullOrEmpty(_apiKey))
                {
                    request.SetRequestHeader("X-API-Key", _apiKey);
                }

                request.timeout = _timeoutSeconds;

                yield return request.SendWebRequest();

                var response = new HealthResponse();

                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        response = JsonUtility.FromJson<HealthResponse>(request.downloadHandler.text);
                        response.isOnline = true;
                    }
                    catch (Exception e)
                    {
                        response.isOnline = true;
                        response.message = $"解析响应失败: {e.Message}";
                    }
                }
                else
                {
                    response.isOnline = false;
                    response.message = GetErrorMessage(request);
                }

                callback?.Invoke(response);
            }
        }

        // ============= 私有辅助方法 =============

        /// <summary>
        /// 获取友好的错误消息
        /// </summary>
        private string GetErrorMessage(UnityWebRequest request)
        {
            switch (request.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                    return $"连接错误: 无法连接到服务器 ({request.error})";
                case UnityWebRequest.Result.DataProcessingError:
                    return $"数据处理错误: {request.error}";
                case UnityWebRequest.Result.ProtocolError:
                    return $"协议错误: HTTP {request.responseCode} - {request.downloadHandler?.text}";
                default:
                    return $"未知错误: {request.error}";
            }
        }
    }

    // ============= 数据模型 =============

    /// <summary>
    /// Unity 实例信息
    /// </summary>
    [Serializable]
    public class UnityInstance
    {
        public string Id;
        public string Name;
        public int Port;
        public string UnityVersion;
        public long LastHeartbeat;

        public UnityInstance()
        {
            UnityVersion = Application.unityVersion;
            LastHeartbeat = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }

    /// <summary>
    /// 注册请求
    /// </summary>
    [Serializable]
    public class RegisterRequest
    {
        public string id;
        public string name;
        public int port;
    }

    /// <summary>
    /// 注册响应
    /// </summary>
    [Serializable]
    public class RegisterResponse
    {
        public bool success;
        public string message;
        public string instanceId;
    }

    /// <summary>
    /// 心跳请求
    /// </summary>
    [Serializable]
    public class HeartbeatRequest
    {
        public string id;
    }

    /// <summary>
    /// 心跳响应
    /// </summary>
    [Serializable]
    public class HeartbeatResponse
    {
        public bool success;
        public string message;
    }

    /// <summary>
    /// 注销请求
    /// </summary>
    [Serializable]
    public class UnregisterRequest
    {
        public string id;
    }

    /// <summary>
    /// 注销响应
    /// </summary>
    [Serializable]
    public class UnregisterResponse
    {
        public bool success;
        public string message;
    }

    /// <summary>
    /// 健康检查响应
    /// </summary>
    [Serializable]
    public class HealthResponse
    {
        public bool isOnline;
        public string message;
        public string status;
        public double uptime;
        public InstanceStats instances;
    }

    /// <summary>
    /// 实例统计
    /// </summary>
    [Serializable]
    public class InstanceStats
    {
        public int total;
        public int online;
        public int offline;
    }
}

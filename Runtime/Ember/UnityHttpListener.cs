using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Ember
{
    /// <summary>
    /// Unity HTTP 监听器
    ///
    /// 监听器工作流程：
    ///
    /// UnityHttpListener.Start()
    ///       ├─> 创建 TcpListener
    ///       ├─> 绑定到指定端口
    ///       ├─> 启动后台监听线程
    ///       ├─> 循环接收传入连接
    ///       ├─> 解析 HTTP 请求
    ///       └─> 触发 OnRequestReceived 事件
    /// </summary>
    public class UnityHttpListener : IDisposable
    {
        // ============= 字段 =============

        private readonly int _port;
        private TcpListener _tcpListener;
        private Thread _listenerThread;
        private bool _isRunning;
        private readonly object _lockObject = new object();

        // ============= 事件 =============

        /// <summary>
        /// 请求接收事件
        /// </summary>
        public event Action<HttpRequest> OnRequestReceived;

        // ============= 属性 =============

        public int Port => _port;
        public bool IsRunning => _isRunning;

        // ============= 构造函数 =============

        private UnityHttpListener(int port)
        {
            _port = port;
        }

        /// <summary>
        /// 创建监听器
        ///
        /// CreateListener()
        ///       ├─> 检查端口可用性
        ///       ├─> 创建 TcpListener 实例
        ///       ├─> 自动启动监听
        ///       └─> 返回监听器实例
        /// </summary>
        public static UnityHttpListener CreateListener(int port)
        {
            if (!PortDetector.IsPortAvailable(port))
            {
                Debug.LogError($"端口 {port} 已被占用");
                return null;
            }

            var listener = new UnityHttpListener(port);
            if (listener.Start())
            {
                return listener;
            }

            return null;
        }

        // ============= 启动/停止 =============

        /// <summary>
        /// 启动监听器
        ///
        /// Start()
        ///       ├─> 创建 TcpListener
        ///       ├─> 绑定到指定端口
        ///       ├─> 创建后台线程
        ///       ├─> 开始监听
        ///       └─> 返回启动结果
        /// </summary>
        public bool Start()
        {
            lock (_lockObject)
            {
                if (_isRunning)
                {
                    Debug.LogWarning($"监听器已在端口 {_port} 上运行");
                    return true;
                }

                try
                {
                    _tcpListener = new TcpListener(IPAddress.Loopback, _port);
                    _tcpListener.Start();

                    _listenerThread = new Thread(ListenLoop)
                    {
                        IsBackground = true,
                        Name = $"EmberHttpListener-{_port}"
                    };
                    _listenerThread.Start();

                    _isRunning = true;
                    Debug.Log($"Ember HTTP 监听器已启动，端口: {_port}");
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogError($"启动 HTTP 监听器失败: {e.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// 停止监听器
        ///
        /// Stop()
        ///       ├─> 设置停止标志
        ///       ├─> 关闭 TcpListener
        ///       ├─> 等待线程结束
        ///       └─> 清理资源
        /// </summary>
        public void Stop()
        {
            lock (_lockObject)
            {
                if (!_isRunning)
                {
                    return;
                }

                _isRunning = false;

                try
                {
                    _tcpListener?.Stop();
                    _tcpListener = null;

                    if (_listenerThread != null && _listenerThread.IsAlive)
                    {
                        if (!_listenerThread.Join(1000))
                        {
                            _listenerThread.Interrupt();
                        }
                    }
                    _listenerThread = null;

                    Debug.Log($"Ember HTTP 监听器已停止，端口: {_port}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"停止 HTTP 监听器时发生错误: {e.Message}");
                }
            }
        }

        // ============= 监听循环 =============

        /// <summary>
        /// 监听循环（在后台线程中运行）
        ///
        /// ListenLoop()
        ///       ├─> 循环等待传入连接
        ///       ├─> 接受 TCP 连接
        ///       ├─> 读取 HTTP 请求数据
        ///       ├─> 解析请求内容
        ///       ├─> 触发 OnRequestReceived 事件
        ///       ├─> 发送 HTTP 响应
        ///       └─> 关闭连接
        /// </summary>
        private void ListenLoop()
        {
            while (_isRunning)
            {
                try
                {
                    if (!_tcpListener.Pending())
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    using (TcpClient client = _tcpListener.AcceptTcpClient())
                    {
                        ProcessClient(client);
                    }
                }
                catch (Exception e) when (_isRunning)
                {
                    Debug.LogError($"HTTP 监听器错误: {e.Message}");
                }
                catch (Exception)
                {
                    // 停止时的异常可以忽略
                    break;
                }
            }
        }

        private void ProcessClient(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                stream.ReadTimeout = 5000;
                stream.WriteTimeout = 5000;

                // 读取请求
                string requestData = ReadRequest(stream);
                if (string.IsNullOrEmpty(requestData))
                {
                    return;
                }

                // 解析请求
                var request = ParseRequest(requestData);
                if (request != null)
                {
                    // 触发事件（切换到主线程）
                    QueueOnMainThread(() => OnRequestReceived?.Invoke(request));

                    // 发送响应
                    string response = GenerateResponse(request);
                    WriteResponse(stream, response);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"处理客户端请求时发生错误: {e.Message}");
            }
        }

        private string ReadRequest(NetworkStream stream)
        {
            byte[] buffer = new byte[8192];
            StringBuilder requestBuilder = new StringBuilder();

            int bytesRead;
            int totalBytes = 0;
            int maxRequestSize = 1024 * 1024; // 1MB 限制

            while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                totalBytes += bytesRead;
                if (totalBytes > maxRequestSize)
                {
                    Debug.LogWarning("HTTP 请求过大，已拒绝");
                    return null;
                }

                requestBuilder.Append(Encoding.UTF8.GetString(buffer, 0, bytesRead));

                // 检查是否读取完整的 HTTP 头
                string requestSoFar = requestBuilder.ToString();
                int headerEndIndex = requestSoFar.IndexOf("\r\n\r\n", StringComparison.Ordinal);
                if (headerEndIndex >= 0)
                {
                    // 解析 Content-Length
                    var headers = ParseHeaders(requestSoFar.Substring(0, headerEndIndex));
                    if (headers.TryGetValue("Content-Length", out string contentLengthStr) &&
                        int.TryParse(contentLengthStr, out int contentLength))
                    {
                        int bodyStart = headerEndIndex + 4;
                        int currentLength = requestSoFar.Length - bodyStart;

                        if (currentLength >= contentLength)
                        {
                            // 已读取完整的请求体
                            break;
                        }
                    }
                    else
                    {
                        // 没有 Content-Length，假设请求已完整
                        break;
                    }
                }
            }

            return requestBuilder.ToString();
        }

        private HttpRequest ParseRequest(string requestData)
        {
            try
            {
                var lines = requestData.Split(new[] { "\r\n" }, StringSplitOptions.None);
                if (lines.Length == 0)
                {
                    return null;
                }

                // 解析请求行
                string[] requestLineParts = lines[0].Split(' ');
                if (requestLineParts.Length < 2)
                {
                    return null;
                }

                var request = new HttpRequest
                {
                    Method = requestLineParts[0],
                    Path = requestLineParts[1],
                    Protocol = requestLineParts.Length > 2 ? requestLineParts[2] : "HTTP/1.1"
                };

                // 解析头部
                int bodyStart = -1;
                var headers = new Dictionary<string, string>();

                for (int i = 1; i < lines.Length; i++)
                {
                    if (string.IsNullOrEmpty(lines[i]))
                    {
                        bodyStart = i + 1;
                        break;
                    }

                    int colonIndex = lines[i].IndexOf(':');
                    if (colonIndex > 0)
                    {
                        string key = lines[i].Substring(0, colonIndex).Trim();
                        string value = lines[i].Substring(colonIndex + 1).Trim();
                        headers[key] = value;
                    }
                }

                request.Headers = headers;

                // 解析请求体
                if (bodyStart > 0 && bodyStart < lines.Length)
                {
                    request.Body = string.Join("\r\n", lines, bodyStart, lines.Length - bodyStart);
                }

                return request;
            }
            catch (Exception e)
            {
                Debug.LogError($"解析 HTTP 请求失败: {e.Message}");
                return null;
            }
        }

        private Dictionary<string, string> ParseHeaders(string headerSection)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lines = headerSection.Split(new[] { "\r\n" }, StringSplitOptions.None);

            foreach (var line in lines)
            {
                int colonIndex = line.IndexOf(':');
                if (colonIndex > 0)
                {
                    string key = line.Substring(0, colonIndex).Trim();
                    string value = line.Substring(colonIndex + 1).Trim();
                    headers[key] = value;
                }
            }

            return headers;
        }

        private string GenerateResponse(HttpRequest request)
        {
            // TODO: 实现实际的处理逻辑
            return "{\"success\":true,\"message\":\"Request received\"}";
        }

        private void WriteResponse(NetworkStream stream, string responseContent)
        {
            string response = $"HTTP/1.1 200 OK\r\n" +
                              $"Content-Type: application/json\r\n" +
                              $"Content-Length: {Encoding.UTF8.GetByteCount(responseContent)}\r\n" +
                              $"Connection: close\r\n" +
                              $"\r\n" +
                              $"{responseContent}";

            byte[] responseBytes = Encoding.UTF8.GetBytes(response);
            stream.Write(responseBytes, 0, responseBytes.Length);
        }

        private void QueueOnMainThread(Action action)
        {
            // 使用 EditorApplication 的回调机制在主线程执行
            UnityEditor.EditorApplication.delayCall += () => action?.Invoke();
        }

        // ============= IDisposable =============

        public void Dispose()
        {
            Stop();
        }
    }

    // ============= HTTP 请求数据结构 =============

    /// <summary>
    /// HTTP 请求数据结构
    /// </summary>
    public class HttpRequest
    {
        public string Method { get; set; }
        public string Path { get; set; }
        public string Protocol { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public string Body { get; set; }

        /// <summary>
        /// 获取查询参数
        /// </summary>
        public Dictionary<string, string> QueryParameters
        {
            get
            {
                var result = new Dictionary<string, string>();
                if (string.IsNullOrEmpty(Path))
                {
                    return result;
                }

                int queryIndex = Path.IndexOf('?');
                if (queryIndex < 0 || queryIndex == Path.Length - 1)
                {
                    return result;
                }

                string queryString = Path.Substring(queryIndex + 1);
                string[] pairs = queryString.Split('&');

                foreach (string pair in pairs)
                {
                    string[] keyValue = pair.Split('=');
                    if (keyValue.Length == 2)
                    {
                        result[keyValue[0]] = Uri.UnescapeDataString(keyValue[1]);
                    }
                }

                return result;
            }
        }
    }
}

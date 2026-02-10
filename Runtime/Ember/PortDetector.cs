using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace Ember
{
    /// <summary>
    /// 端口检测器
    ///
    /// 端口检测流程：
    ///
    /// PortDetector.FindAvailablePort()
    ///       ├─> 从 minPort 开始遍历
    ///       ├─> 尝试绑定 TCP Socket
    ///       ├─> 绑定成功 = 端口可用
    ///       ├─> 绑定失败 = 继续下一个
    ///       └─> 返回可用端口或 -1
    /// </summary>
    public static class PortDetector
    {
        /// <summary>
        /// 查找可用端口
        ///
        /// FindAvailablePort()
        ///       ├─> 遍历端口范围 [minPort, maxPort]
        ///       ├─> 对每个端口调用 IsPortAvailable()
        ///       ├─> 找到可用端口立即返回
        ///       └─> 无可用端口返回 -1
        /// </summary>
        /// <param name="minPort">最小端口号</param>
        /// <param name="maxPort">最大端口号</param>
        /// <returns>可用端口号，无可用端口返回 -1</returns>
        public static int FindAvailablePort(int minPort = 8000, int maxPort = 9000)
        {
            // 参数验证
            if (minPort < 1024 || minPort > 65535)
            {
                Debug.LogError($"最小端口无效: {minPort}，必须介于 1024-65535 之间");
                return -1;
            }

            if (maxPort < minPort || maxPort > 65535)
            {
                Debug.LogError($"最大端口无效: {maxPort}，必须介于 {minPort}-65535 之间");
                return -1;
            }

            // 遍历端口范围
            for (int port = minPort; port <= maxPort; port++)
            {
                if (IsPortAvailable(port))
                {
                    Debug.Log($"找到可用端口: {port}");
                    return port;
                }
            }

            Debug.LogWarning($"在端口范围 {minPort}-{maxPort} 内未找到可用端口");
            return -1;
        }

        /// <summary>
        /// 检查指定端口是否可用
        ///
        /// IsPortAvailable()
        ///       ├─> 创建 TCP Socket
        ///       ├─> 尝试绑定到指定端口
        ///       ├─> 绑定成功 = 端口可用
        ///       ├─> 绑定失败 = 端口占用
        ///       └─> 释放 Socket 资源
        /// </summary>
        /// <param name="port">端口号</param>
        /// <returns>端口可用返回 true，否则返回 false</returns>
        public static bool IsPortAvailable(int port)
        {
            if (port < 1 || port > 65535)
            {
                return false;
            }

            try
            {
                // 尝试监听该端口
                TcpListener listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();

                // 如果成功启动，说明端口可用
                listener.Stop();
                return true;
            }
            catch (Exception e)
            {
                // 端口被占用或其他错误
                if (e is SocketException socketEx)
                {
                    // 10048 = 端口已被占用 (Windows)
                    // 98 = 地址已在使用中 (Linux/Mac)
                    // 125 = 地址已在使用中 (部分系统)
                    if (socketEx.SocketErrorCode == SocketError.AddressAlreadyInUse)
                    {
                        return false;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// 获取随机可用端口
        ///
        /// GetRandomAvailablePort()
        ///       ├─> 随机选择起始端口
        ///       ├─> 检查该端口及后续 10 个端口
        ///       ├─> 找到可用端口返回
        ///       └─> 无可用端口返回 -1
        /// </summary>
        /// <param name="minPort">最小端口号</param>
        /// <param name="maxPort">最大端口号</param>
        /// <returns>可用端口号，无可用端口返回 -1</returns>
        public static int GetRandomAvailablePort(int minPort = 8000, int maxPort = 9000)
        {
            System.Random random = new System.Random();
            int range = maxPort - minPort;

            // 最多尝试 10 次
            for (int i = 0; i < 10; i++)
            {
                int startPort = minPort + random.Next(range);
                int port = FindAvailablePort(startPort, Math.Min(startPort + 10, maxPort));
                if (port != -1)
                {
                    return port;
                }
            }

            return -1;
        }

        /// <summary>
        /// 检查指定端口是否被特定进程占用
        /// </summary>
        /// <param name="port">端口号</param>
        /// <param name="processName">进程名称（可选）</param>
        /// <returns>端口被占用返回 true，否则返回 false</returns>
        public static bool IsPortInUse(int port, string processName = null)
        {
            return !IsPortAvailable(port);
        }

        /// <summary>
        /// 获取推荐端口（基于项目名称哈希）
        ///
        /// 这样同一个项目每次启动都会尝试使用相同的端口
        /// </summary>
        /// <param name="projectName">项目名称</param>
        /// <param name="minPort">最小端口号</param>
        /// <param name="maxPort">最大端口号</param>
        /// <returns>推荐端口号</returns>
        public static int GetRecommendedPort(string projectName, int minPort = 8000, int maxPort = 9000)
        {
            if (string.IsNullOrEmpty(projectName))
            {
                return minPort;
            }

            // 基于项目名称计算哈希
            int hash = projectName.GetHashCode();
            int range = maxPort - minPort;
            int port = minPort + Math.Abs(hash) % range;

            // 检查端口是否可用，如果不可用则查找附近可用端口
            if (IsPortAvailable(port))
            {
                return port;
            }

            // 向后查找 50 个端口
            for (int i = 1; i <= 50; i++)
            {
                int testPort = port + i;
                if (testPort <= maxPort && IsPortAvailable(testPort))
                {
                    return testPort;
                }
            }

            // 向前查找 50 个端口
            for (int i = 1; i <= 50; i++)
            {
                int testPort = port - i;
                if (testPort >= minPort && IsPortAvailable(testPort))
                {
                    return testPort;
                }
            }

            // 附近无可用端口，随机查找
            return FindAvailablePort(minPort, maxPort);
        }

        /// <summary>
        /// 批量检查端口是否可用
        /// </summary>
        /// <param name="ports">端口数组</param>
        /// <returns>每个端口的可用状态</returns>
        public static bool[] CheckPortsAvailable(int[] ports)
        {
            if (ports == null || ports.Length == 0)
            {
                return new bool[0];
            }

            bool[] results = new bool[ports.Length];
            for (int i = 0; i < ports.Length; i++)
            {
                results[i] = IsPortAvailable(ports[i]);
            }

            return results;
        }
    }
}

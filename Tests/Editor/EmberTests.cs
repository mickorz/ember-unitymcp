using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Ember.Tests
{
    /// <summary>
    /// Ember MCP 单元测试
    ///
    /// 测试覆盖：
    /// EmberTests
    ///       ├─> TestPortDetector端口检测
    ///       ├─> TestEmberConfig配置管理
    ///       ├─> TestEmberConfig配置验证
    ///       └─> TestEmberConfig实例ID生成
    ///
    /// 运行方式：
    /// 1. 打开 Unity Test Runner (Window > General > Test Runner)
    /// 2. 选择 EditMode
    /// 3. 点击 Run All
    /// </summary>
    public class EmberTests
    {
        // ============= PortDetector 测试 =============

        [Test]
        /// <summary>
        /// 测试端口可用性检查
        /// </summary>
        public void TestPortDetector_IsPortAvailable_WithInvalidPort_ReturnsFalse()
        {
            // Arrange & Act & Assert
            Assert.IsFalse(PortDetector.IsPortAvailable(-1), "端口 -1 应该不可用");
            Assert.IsFalse(PortDetector.IsPortAvailable(0), "端口 0 应该不可用");
            Assert.IsFalse(PortDetector.IsPortAvailable(65536), "端口 65536 应该不可用");
        }

        [Test]
        /// <summary>
        /// 测试端口范围查找
        /// </summary>
        public void TestPortDetector_FindAvailablePort_WithValidRange_ReturnsPort()
        {
            // Arrange & Act
            int port = PortDetector.FindAvailablePort(8100, 8200);

            // Assert
            Assert.That(port, Is.GreaterThanOrEqualTo(8100), "端口应该在范围内");
            Assert.That(port, Is.LessThanOrEqualTo(8200), "端口应该在范围内");
        }

        [Test]
        /// <summary>
        /// 测试无效端口范围
        /// </summary>
        public void TestPortDetector_FindAvailablePort_WithInvalidRange_ReturnsNegativeOne()
        {
            // Arrange & Act & Assert
            Assert.That(PortDetector.FindAvailablePort(100, 50), Is.EqualTo(-1), "无效范围应返回 -1");
        }

        // ============= EmberConfig 测试 =============

        [Test]
        /// <summary>
        /// 测试配置创建
        /// </summary>
        public void TestEmberConfig_CreateDefault_ReturnsValidConfig()
        {
            // Arrange & Act
            var config = Ember.Editor.EmberConfig.CreateDefault();

            // Assert
            Assert.IsNotNull(config, "配置不应为空");
            Assert.IsNotEmpty(config.InstanceId, "实例ID应已生成");
            Assert.AreEqual("localhost", config.ServerUrl, "默认服务器应为 localhost");
            Assert.AreEqual(4000, config.ServerPort, "默认端口应为 4000");
            Assert.IsTrue(config.AutoRegister, "默认应启用自动注册");
            Assert.IsTrue(config.AutoRestoreOnRecompile, "默认应启用编译后恢复");
        }

        [Test]
        /// <summary>
        /// 测试配置保存和加载
        /// </summary>
        public void TestEmberConfig_SaveAndLoad_ReturnsSameConfig()
        {
            // Arrange
            var originalConfig = Ember.Editor.EmberConfig.CreateDefault();
            originalConfig.ServerUrl = "127.0.0.1";
            originalConfig.ServerPort = 5000;
            originalConfig.AutoRestoreOnRecompile = false;

            // Act
            originalConfig.Save();
            var loadedConfig = Ember.Editor.EmberConfig.Load();

            // Assert
            Assert.AreEqual(originalConfig.ServerUrl, loadedConfig.ServerUrl, "服务器URL应一致");
            Assert.AreEqual(originalConfig.ServerPort, loadedConfig.ServerPort, "端口应一致");
            Assert.AreEqual(originalConfig.AutoRestoreOnRecompile, loadedConfig.AutoRestoreOnRecompile, "编译后恢复配置应一致");
        }

        [Test]
        /// <summary>
        /// 测试配置验证
        /// </summary>
        public void TestEmberConfig_Validate_WithValidConfig_ReturnsTrue()
        {
            // Arrange & Act
            var config = Ember.Editor.EmberConfig.CreateDefault();

            // Assert
            Assert.IsTrue(config.IsValid(), "默认配置应该有效");
        }

        [Test]
        /// <summary>
        /// 测试配置验证失败情况
        /// </summary>
        public void TestEmberConfig_Validate_WithInvalidConfig_ReturnsFalse()
        {
            // Arrange
            var config = Ember.Editor.EmberConfig.CreateDefault();

            // Act & Assert - 无效端口
            config.ServerPort = 0;
            Assert.IsFalse(config.IsValid(), "端口为0时应无效");

            // 重置并测试空服务器URL
            config = Ember.Editor.EmberConfig.CreateDefault();
            config.ServerUrl = "";
            Assert.IsFalse(config.IsValid(), "服务器URL为空时应无效");
        }

        [Test]
        /// <summary>
        /// 测试实例ID生成
        /// </summary>
        public void TestEmberConfig_GenerateInstanceId_ReturnsUniqueId()
        {
            // Arrange
            var config1 = Ember.Editor.EmberConfig.CreateDefault();
            var config2 = Ember.Editor.EmberConfig.CreateDefault();

            // Act
            config1.GenerateInstanceId();
            config2.GenerateInstanceId();

            // Assert
            Assert.IsNotEmpty(config1.InstanceId, "实例ID不应为空");
            Assert.IsNotEmpty(config2.InstanceId, "实例ID不应为空");
            Assert.AreNotEqual(config1.InstanceId, config2.InstanceId, "每次生成的ID应不同");
        }

        // ============= URL 生成测试 =============

        [Test]
        /// <summary>
        /// 测试服务器URL生成
        /// </summary>
        public void TestEmberConfig_GetServerUrl_ReturnsCorrectUrl()
        {
            // Arrange
            var config = Ember.Editor.EmberConfig.CreateDefault();
            config.ServerUrl = "192.168.1.1";
            config.ServerPort = 3000;

            // Act
            string url = config.GetServerUrl();

            // Assert
            Assert.AreEqual("http://192.168.1.1:3000", url, "URL格式应正确");
        }

        [Test]
        /// <summary>
        /// 测试注册URL生成
        /// </summary>
        public void TestEmberConfig_GetRegisterUrl_ReturnsCorrectUrl()
        {
            // Arrange
            var config = Ember.Editor.EmberConfig.CreateDefault();

            // Act
            string url = config.GetRegisterUrl();

            // Assert
            Assert.IsTrue(url.EndsWith("/register"), "注册URL应以 /register 结尾");
        }

        [Test]
        /// <summary>
        /// 测试心跳URL生成
        /// </summary>
        public void TestEmberConfig_GetHeartbeatUrl_ReturnsCorrectUrl()
        {
            // Arrange
            var config = Ember.Editor.EmberConfig.CreateDefault();

            // Act
            string url = config.GetHeartbeatUrl();

            // Assert
            Assert.IsTrue(url.EndsWith("/heartbeat"), "心跳URL应以 /heartbeat 结尾");
        }

        [Test]
        /// <summary>
        /// 测试注销URL生成
        /// </summary>
        public void TestEmberConfig_GetUnregisterUrl_ReturnsCorrectUrl()
        {
            // Arrange
            var config = Ember.Editor.EmberConfig.CreateDefault();

            // Act
            string url = config.GetUnregisterUrl();

            // Assert
            Assert.IsTrue(url.EndsWith("/unregister"), "注销URL应以 /unregister 结尾");
        }

        [Test]
        /// <summary>
        /// 测试健康检查URL生成
        /// </summary>
        public void TestEmberConfig_GetHealthUrl_ReturnsCorrectUrl()
        {
            // Arrange
            var config = Ember.Editor.EmberConfig.CreateDefault();

            // Act
            string url = config.GetHealthUrl();

            // Assert
            Assert.IsTrue(url.EndsWith("/health"), "健康检查URL应以 /health 结尾");
        }

        // ============= 配置重置测试 =============

        [Test]
        /// <summary>
        /// 测试配置重置
        /// </summary>
        public void TestEmberConfig_ResetToDefaults_ReturnsDefaultValues()
        {
            // Arrange
            var config = Ember.Editor.EmberConfig.CreateDefault();
            config.ServerUrl = "custom.server.com";
            config.ServerPort = 9999;
            config.AutoRegister = false;
            config.AutoRestoreOnRecompile = false;

            // Act
            config.ResetToDefaults();

            // Assert
            Assert.AreEqual("localhost", config.ServerUrl, "重置后应为默认服务器");
            Assert.AreEqual(4000, config.ServerPort, "重置后应为默认端口");
            Assert.IsTrue(config.AutoRegister, "重置后应启用自动注册");
            Assert.IsTrue(config.AutoRestoreOnRecompile, "重置后应启用编译后恢复");
        }
    }
}

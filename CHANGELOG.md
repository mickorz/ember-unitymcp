# ember-mcp 修改日志

本文档记录 ember-mcp 项目的所有重要变更。

---

## [1.0.0] - 2024-02-09

### 新增

#### 核心功能
- **实例注册中心**
  - Unity 实例注册接口 (`POST /register`)
  - 心跳续期接口 (`POST /heartbeat`)
  - 主动注销接口 (`POST /unregister`)
  - 健康检查接口 (`GET /health`)

- **实例生命周期管理**
  - 自动清理超时实例 (超过 15 秒未收到心跳)
  - 定时清理任务 (每 5 秒检查一次)
  - 实例状态追踪 (在线/离线)
  - 运行时长计算

- **MCP 工具接口**
  - `list_unity_instances`: 列出所有 Unity 实例
  - `unity_tool`: 在指定 Unity 实例中执行工具

- **自动推断功能**
  - 单实例场景: 自动选中唯一的在线实例
  - 多实例场景: 提示用户选择目标实例
  - 无实例场景: 提示用户启动 Unity

- **请求转发**
  - 路由请求到指定的 Unity 实例
  - 请求超时配置 (默认 5 秒)
  - 连接错误处理

#### 安全性
- API Key 认证 (可选)
- 请求头验证 (`X-API-Key`)

#### 配置管理
- 环境变量支持 (`.env` 文件)
- 可配置的参数:
  - 注册中心端口
  - 心跳间隔
  - 超时时间
  - 清理间隔
  - 请求超时
  - 日志级别

#### 日志系统
- 结构化日志 (pino)
- 彩色输出 (pino-pretty)
- 多级别日志 (trace/debug/info/warn/error/fatal)

#### 项目结构
```
ember-mcp/
├── src/
│   ├── index.ts      # MCP Server & HTTP 服务
│   ├── registry.ts   # 实例注册管理
│   ├── config.ts     # 配置管理
│   ├── types.ts      # TypeScript 类型定义
│   └── utils.ts      # 工具函数
├── package.json
├── tsconfig.json
├── .env.example
├── README.md
└── ChangeLog.md
```

### 技术栈
- **运行时**: Node.js (ES2022)
- **语言**: TypeScript 5.3
- **框架**:
  - Express 4.18 (HTTP 服务)
  - @modelcontextprotocol/sdk 1.0 (MCP 协议)
  - Axios 1.6 (HTTP 请求)
  - Pino 8.17 (日志)
  - Dotenv 16.3 (环境变量)

---

## 计划中的功能

### [1.1.0] - 计划中
- [ ] 持久化存储 (SQLite/Redis)
- [ ] 实例状态变更通知 (推送)
- [ ] WebSocket 支持 (实时通信)
- [ ] 实例分组管理
- [ ] 更详细的监控指标

### [1.2.0] - 计划中
- [ ] 分布式支持 (多注册中心)
- [ ] 负载均衡
- [ ] 实例迁移
- [ ] Web 管理界面

---

## 变更说明

### 版本号规则
遵循 [语义化版本 2.0.0](https://semver.org/lang/zh-CN/):
- **主版本号**: 不兼容的 API 变更
- **次版本号**: 向下兼容的功能新增
- **修订号**: 向下兼容的问题修复

### 变更类型标识
- **新增**: 新功能
- **变更**: 现有功能的变更
- **废弃**: 即将移除的功能
- **移除**: 已移除的功能
- **修复**: 问题修复
- **安全**: 安全相关的修复或改进

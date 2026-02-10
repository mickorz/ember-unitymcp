# ember-mcp

Unity 实例注册中心与 MCP (Model Context Protocol) 网关服务。

## 简介

ember-mcp 是一个**无状态网关架构**的 MCP 服务器，用于管理多个 Unity 实例的注册和路由转发。它允许不同的 Claude Code 客户端通过 `instanceId` 独立操作不同的 Unity 实例。

### 核心特性

- **多实例管理**: 支持多个 Unity 实例同时注册
- **自动推断**: 单实例场景下自动选中目标实例，无需手动指定
- **心跳机制**: 自动检测和清理离线实例
- **API Key 认证**: 可选的 API 密钥验证
- **结构化日志**: 基于 pino 的日志系统

---

## 架构概览

```
                    ┌─────────────────────────────────────┐
                    │         ember-mcp (Node.js)         │
                    ├─────────────────────────────────────┤
                    │  ┌─────────────┐  ┌──────────────┐ │
Unity 1 ──HTTP──────┼─►│    Express  │  │      MCP     │ │
Unity 2 ──HTTP──────┼─►│  注册中心   │  │   (stdio)    │ │
Unity 3 ──HTTP──────┼─►│   :4000     │  │              │ │
                    │  └─────────────┘  └──────┬───────┘ │
                    │         │                  │        │
                    │         └──────Map─────────┘        │
                    │        unityInstances               │
                    └─────────────────────────────────────┘
                                           │
                                           ▼
                              Claude Code (通过 stdio)
```

---

## 安装

### 1. 克隆项目

```bash
git clone <repository-url>
cd ember-mcp
```

### 2. 安装依赖

```bash
npm install
```

### 3. 编译 TypeScript

```bash
npm run build
```

### 4. 配置环境变量 (可选)

复制 `.env.example` 为 `.env` 并根据需要修改配置：

```bash
cp .env.example .env
```

---

## 配置

### 环境变量

| 变量名 | 说明 | 默认值 |
|--------|------|--------|
| `EMBER_PORT` | 注册中心端口 | `4000` |
| `HEARTBEAT_INTERVAL` | Unity 心跳间隔 (毫秒) | `10000` |
| `HEARTBEAT_TIMEOUT` | 实例超时时间 (毫秒) | `15000` |
| `CLEANUP_INTERVAL` | 清理检查间隔 (毫秒) | `5000` |
| `REQUEST_TIMEOUT` | 请求超时时间 (毫秒) | `5000` |
| `LOG_LEVEL` | 日志级别 | `info` |
| `EMBER_API_KEY` | API 密钥 (可选) | - |

### Claude Code 配置

#### 方式一：使用 .mcp.json 配置文件 (推荐)

在项目根目录创建 `.mcp.json` 文件：

```json
{
  "mcpServers": {
    "ember-mcp": {
      "type": "stdio",
      "command": "node",
      "args": [
        "Packages/com.ember.mcp/dist/index.js"
      ],
      "env": {}
    }
  }
}
```

**配置说明:**
- `type`: 通信类型，固定为 `stdio`
- `command`: 启动命令，使用 `node`
- `args`: 启动参数，指向编译后的入口文件路径
- `env`: 环境变量对象，可在此配置 `EMBER_PORT` 等变量

**路径说明:** 路径为相对于Unity项目根目录的路径，假设Package安装在 `Packages/com.ember.mcp` 目录下。

#### 方式二：使用全局 Claude Code 配置

编辑 Claude Code 配置文件 (通常位于 `~/.claude.json`)：

```json
{
  "mcpServers": {
    "ember-mcp": {
      "command": "node",
      "args": ["Packages/com.ember.mcp/dist/index.js"]
    }
  }
}
```

---

## 使用

### 启动服务

```bash
npm start
```

### Unity 端集成

Unity 需要实现以下逻辑：

1. **选择可用端口** (8000-9000)
2. **启动 HTTP 监听**
3. **发送注册请求**:

```http
POST http://localhost:4000/register
Content-Type: application/json
X-API-Key: your-secret-key (可选)

{
  "id": "unique-instance-id",
  "name": "MyUnityProject",
  "port": 8081
}
```

4. **定期发送心跳** (每 10 秒):

```http
POST http://localhost:4000/heartbeat
Content-Type: application/json
X-API-Key: your-secret-key (可选)

{
  "id": "unique-instance-id"
}
```

5. **退出时注销**:

```http
POST http://localhost:4000/unregister
Content-Type: application/json
X-API-Key: your-secret-key (可选)

{
  "id": "unique-instance-id"
}
```

### Claude Code 使用案例

#### 案例 1: 查看当前 Unity 实例列表

**用户输入:**
```
请列出当前的 Unity 实例
```

**Claude Code 执行流程:**
1. 调用 `list_unity_instances` 工具
2. ember-mcp 返回已注册的 Unity 实例列表
3. 显示实例信息表格

**返回示例:**
```
当前已注册的 Unity 实例:

| ID | 名称 | 端口 | 状态 | 最后心跳 |
|----|------|------|------|----------|
| unity-project-01 | MyProject | 8081 | 在线 | 2秒前 |
```

---

#### 案例 2: 获取 Unity 日志

**用户输入:**
```
获取 Unity 的控制台日志
```

**Claude Code 执行流程:**
1. 调用 `list_unity_instances` 检查在线实例
2. 如果只有一个实例，自动选中；如果有多个，提示用户选择
3. 调用 `unity_tool` 工具，参数:
   - `toolName`: `"get_logs"`
   - `instanceId`: `"unity-project-01"`
4. ember-mcp 转发请求到对应 Unity 实例的 HTTP 接口
5. Unity 返回日志内容
6. Claude Code 展示日志

**返回示例:**
```
Unity 控制台日志:

[Info] 10:23:45 - 场景加载完成
[Warn] 10:23:47 - 未找到材质资源
[Error] 10:23:50 - 空引用异常
```

---

#### 案例 3: 执行 Unity 命令

**用户输入:**
```
在 Unity 中执行GameObject.Find("Player")并获取结果
```

**Claude Code 执行流程:**
1. 确认目标 Unity 实例
2. 调用 `unity_tool` 工具，参数:
   - `toolName`: `"execute_command"`
   - `instanceId`: `"unity-project-01"`
   - `arguments`: `{ "command": "GameObject.Find('Player')" }`
3. 转发到 Unity 执行
4. 返回执行结果

---

### MCP 工具参考

#### list_unity_instances

列出所有 Unity 实例：

```
AI: 请列出当前的 Unity 实例
→ 调用 list_unity_instances
→ 返回实例列表表格
```

#### unity_tool

在指定 Unity 实例中执行工具：

```
AI: 获取 Unity 日志
→ 调用 unity_tool(toolName: "get_logs")
→ 自动选中唯一实例或提示选择
→ 转发请求到 Unity
→ 返回结果
```

---

## API 接口

### POST /register

注册或续期 Unity 实例。

**请求头:**
```
Content-Type: application/json
X-API-Key: your-secret-key (可选)
```

**请求体:**
```json
{
  "id": "string",   // 必填: 实例唯一标识符
  "name": "string", // 可选: 项目名称
  "port": "number"  // 必填: HTTP 监听端口
}
```

**响应:**
```json
{
  "success": true,
  "message": "Registered successfully",
  "instanceId": "string"
}
```

### POST /heartbeat

发送心跳以维持在线状态。

**请求体:**
```json
{
  "id": "string"   // 必填: 实例唯一标识符
}
```

**响应:**
```json
{
  "success": true,
  "message": "Heartbeat received"
}
```

### POST /unregister

主动注销 Unity 实例。

**请求体:**
```json
{
  "id": "string"   // 必填: 实例唯一标识符
}
```

**响应:**
```json
{
  "success": true,
  "message": "Unregistered successfully"
}
```

### GET /health

健康检查接口。

**响应:**
```json
{
  "status": "ok",
  "uptime": 123.456,
  "instances": {
    "total": 2,
    "online": 1,
    "offline": 1
  }
}
```

---

## 项目结构

```
ember-mcp/
├── src/
│   ├── index.ts      # MCP Server & HTTP 服务入口
│   ├── registry.ts   # 实例注册管理
│   ├── config.ts     # 配置管理
│   ├── types.ts      # TypeScript 类型定义
│   └── utils.ts      # 工具函数
├── dist/             # 编译输出目录
├── package.json      # 项目配置
├── tsconfig.json     # TypeScript 配置
├── .env.example      # 环境变量示例
├── README.md         # 项目文档
└── ChangeLog.md      # 修改日志
```

---

## 开发

### 构建命令

```bash
# 编译
npm run build

# 监听模式编译
npm run dev

# 清理编译输出
npm run clean

# 启动服务
npm start
```

### 代码风格

- 使用 TypeScript 严格模式
- 所有函数都有 JSDoc 注释
- 遵循流程图注释规范

---

## 许可证

MIT License

---

## 参考资源

- [Model Context Protocol](https://modelcontextprotocol.io/)
- [Express.js](https://expressjs.com/)
- [Claude Code](https://claude.ai/code)

---

## 更新日志

详见 [ChangeLog.md](./ChangeLog.md)

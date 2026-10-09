# Message Replier

项目包含两部分：保留用于 UI 对照的浏览器 Demo，以及可在真实微信中工作的原生 Windows AI Bar。当前开发重点是原生版本；最初的模拟微信版本保存在 `archive/uidemo` 分支。

## 原生 Windows 版本

`native-windows` 使用 .NET 8、Windows UI Automation 和系统级选区监听。它只在微信窗口中触发，支持解读、回复建议和表达优化，点击建议后写入微信输入框，但不会替用户发送。

客户端会自动启动发布目录中的 `algorithm-server`，并通过 `/health` 检查服务，无需用户手动运行 Node。托盘菜单可以把 OpenRouter API Key 保存到 Windows 凭据管理器，也可以开关“使用附近聊天上下文”。开启后，程序最多读取选中文字附近 5 条 UI Automation 可见文本；读取失败时自动退回仅处理选中文字。

首次构建运行 `native-windows/setup-node.ps1`，然后运行 `native-windows/build.ps1`。完整发布目录位于 `dist`，需要连同 `SnkMessage-native-v0.3.exe`、`algorithm-server` 和 `runtime` 一起分发。

## 运行

在本目录执行 `node server.mjs`，打开 http://127.0.0.1:4173 。执行 `node --test tests/*.test.mjs` 运行状态测试。

## 体验

1. 拖动选中林经理的消息，点击浮出的「解读」。
2. 等待加载，查看解读卡片；「重新思考」可再次调用模拟服务。
3. 在输入框写下「我不认同，想先问清楚具体问题。」。
4. 选中输入内容（可用 Ctrl+A），点击「回复建议」。
5. 点击建议，完整替换当前输入内容。仍可编辑。
6. 点击「发送」或 Ctrl+Enter，消息才会进入本页模拟会话。Enter 仅换行。

Esc 或点击外部关闭浮层；Tab 可从选区进入工具条。右上角可重置演示。输入与消息不持久化，不访问剪贴板，不向网络发送聊天内容。

## 结构

- `src/state.js`：集中状态转换与请求版本控制，防止过期结果覆盖新输入。
- `src/app.js`：选区、浮层定位、组件渲染与手动发送。
- `src/ai.js`：可替换的异步模拟适配器，预留 interpret / express 接口；无真实模型。
- `src/styles.css`：Figma 核心尺寸、颜色、阴影与 Windows 字体回退。
- `assets/`：从 Figma 下载的原始图标。

Figma 来源：94eWQhY6bIxNKLZDDoHebE；工具条 35:3986、加载 35:6593、解读 35:6656、表达 35:10381。背景按用户要求独立简化为 Windows 风格。字体使用 Segoe UI / Microsoft YaHei UI；表达项省略复制图标，点击直接替换。工具条箭头可展开菜单，切换解读和回复建议；切换不直接生成。解读加载期间按 Figma 35:6589 高亮当前会话区域，结束或取消时清除。

本节描述的是保留下来的浏览器 UI Demo；真实微信集成位于 `native-windows`。

## Windows 桌面版

安装依赖后执行 `pnpm desktop` 可启动桌面版，执行 `pnpm run build:win` 生成免安装目录 `dist/SnkMessage-win32-x64`。用户可解压发布的 ZIP 后双击 `SnkMessage.exe`。

桌面版默认保持置顶。顶部栏可拖动窗口；“置顶”可切换窗口层级；“—”最小化；“×”隐藏到系统托盘。单击托盘图标切换显示/隐藏，右键菜单可以恢复、切换置顶或退出。

当前桌面版仍展示模拟聊天 Demo。它已经成为真实 Windows 进程和独立窗口，但系统级选区检测、覆盖到其他聊天软件附近以及跨应用输入替换属于下一阶段。


## 三种选区功能（更新）

任何页面句子或段落选区（包括收到、发出、新发送的消息和输入框）均可触发 AI Bar。菜单有三项：
- 解读：解释选中文本含义。
- 回复建议：对选中文本给出三条回复，不要求输入框已有草稿。
- 表达优化：仅优化选中文本，不读取整个输入框作为优化对象。

回复与优化使用相同卡片布局、独立标题；点击任一建议完整替换输入框，发送仍由用户手动完成。三种加载过程均高亮当前会话。接口分别为 ai.interpret / ai.reply / ai.express，均接收 selectedText。当前仍为本地模拟输出。

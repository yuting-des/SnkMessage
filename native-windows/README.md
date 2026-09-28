# SnkMessage Native Windows

这是不包含模拟聊天窗口的原生后台版本。启动后常驻托盘，用户在微信或其他 Windows 应用中拖动选中文字时，程序尝试通过 UI Automation 获取选区；微信、浏览器和文本编辑器中读取失败时，短暂使用复制操作并恢复原剪贴板。浮层使用不激活工具窗口，不夺走原应用焦点。

AI Bar 提供“解读 / 回复建议 / 表达优化”。点击回复或优化建议时，程序尝试定位目标窗口最下方的可编辑控件并写入；定位失败时把建议复制到剪贴板。程序不会模拟 Enter 或点击发送。

原生端已经升级到 .NET 8，并通过 `IAiService` 隔离 UI 与算法。未设置环境变量时使用 `MockAiService`，便于独立调试交互；设置 `SNKMESSAGE_AI_ENDPOINT` 后使用 `RemoteAiService` 调用后端。请求由 `AiCoordinator` 统一完成超时、取消、长度限制、去重和结果校验。新选区、关闭浮窗或重新生成会取消旧请求，旧结果不会重新打开浮窗。

当前版本通过独立的 `AppRestrictions.WeChatOnly` 开关限制为只在微信中触发。拖动选中文字后，程序优先读取 UI Automation 选区并直接显示 AI Bar；微信不暴露选区时，用户按 `Ctrl+C` 后显示。程序不会为了获取选区模拟复制，因此不会抢占或恢复用户剪贴板。

选择回复或表达优化建议后，程序先聚焦微信输入区并清除消息选区，再把所选建议持久写入剪贴板并执行粘贴。即使自动粘贴失败，剪贴板中也已经是建议文本，可以立即再次按 `Ctrl+V`，无需手动取消消息选区。重复启动程序时，已运行实例会恢复托盘图标并显示提示。

在 PowerShell 中运行 `./build.ps1`，产物会同步到 `../dist/SnkMessage-native-v0.3.exe`。项目优先使用仓库 `.tools/dotnet` 中的 SDK；没有本地 SDK 时使用系统 `dotnet`。

本地联调算法接口：

```powershell
node ../algorithm-server/server.mjs
$env:SNKMESSAGE_AI_ENDPOINT='http://127.0.0.1:8787/v1/generate'
../dist/SnkMessage-native-v0.3.exe
```

生产环境的算法地址必须使用 HTTPS，模型密钥只保存在服务端。请求只包含当前选中文本；`context` 字段目前为空，为后续增加可控上下文预留。

# 桌面派总（.NET 8 / WPF）

这是一个使用 C#、.NET 8 和原生 WPF 从头重构的 Windows 桌宠。新版不使用 WebView2、透明网页、白色色键、全屏透明窗口、鼠标轮询或阻塞式拖拽。

当前默认分支是 `main`，发布代码为桌面派总的 .NET 8 / WPF 实现；`wpf` 分支用于后续 WPF 开发。旧 Tauri/Vite/Rust 源码已从当前工作树移除，仅通过 `legacy-tauri-v1` 标签留档；当前 WPF 项目不依赖 Node.js、Vite、Rust 或 Tauri。

## 功能

- 透明、无边框、置顶且不抢焦点的原生 WPF 桌宠窗口。
- 当前动画帧的 Alpha 通道会转换为 Win32 HRGN：派大星可见像素接收鼠标，透明像素直接点击下方应用。
- Per-Monitor V2 DPI、多显示器、负坐标和位置恢复。
- 鼠标捕获式非阻塞拖动；松手后固定，不下落、不吸附任务栏、不自行走开。
- `idle`、`walk`、`sleep`、`fall`、`drag`、`reaction`、`happy`、`sad` 动画。
- 单击反应与气泡、双击聊天、右键互动菜单；点击/双击/拖动互斥。
- 独立、置顶、不激活、完全鼠标穿透的气泡窗口。
- 独立可调整大小的聊天窗口，支持 Enter 发送、Shift+Enter 换行、停止生成和清空历史。
- OpenAI-compatible Chat Completions 流式 SSE，兼容百炼、DeepSeek、Moonshot 和其他兼容服务。
- API Key 使用 Windows DPAPI CurrentUser 加密；明文不会写入设置、日志、源码或安装包。
- 设置页、原生托盘、开机启动、桌面快捷方式、日志目录、本地数据清理。
- Mutex + 当前用户 ACL 命名管道单实例；第二次启动只唤醒或操作现有实例。
- 当前用户 Inno Setup 安装器和自包含单文件发布。

## 使用

运行 `DesktopPet.exe` 后：

- 单击派大星：反应动画、随机气泡、好感度 +1。
- 双击派大星：打开聊天窗口。
- 拖动派大星：超过 Windows 系统拖动阈值后开始移动；松手保存位置。
- 右键派大星：喂食、抚摸、玩耍、聊天、设置、隐藏或退出。
- 托盘菜单：显示/隐藏、聊天、设置、关于和退出；双击托盘图标恢复派大星。

内部诊断命令也可用于唤醒现有实例：`--show`、`--hide`、`--chat`、`--settings`、`--exit`。正常用户无需使用这些参数。

## AI 设置

默认预设：

- API 地址：`https://dashscope.aliyuncs.com/compatible-mode/v1`
- 模型：`qwen3.6-flash`

地址既可以是 `/v1` 基础地址，也可以是完整 `/chat/completions` 地址。远程服务必须使用 HTTPS；只有 `localhost`、`127.0.0.1` 和 `::1` 可以使用 HTTP。`bailian.console.aliyun.com` 是控制台网页地址，会被拒绝。

API Key 仅在运行时进入 `Authorization: Bearer` 请求头。远程服务没有 Key 时不会发送测试或聊天请求。请只在设置页中填写自己新创建的 Key；不要把 Key 写入任何仓库文件、命令行或问题报告。

## 本地数据

正式版本固定使用：

```text
%LOCALAPPDATA%\DesktopPet\
├── settings.json
├── chat-history.json
└── logs\app.log
```

- `settings.json` 中的 API Key 是 DPAPI CurrentUser 密文，不是明文。
- JSON 使用同目录临时文件和原子替换写入。
- 损坏配置会备份为 `.corrupt-*`，随后恢复默认值。
- 聊天只保存最近 20 条 user/assistant 消息。
- 日志不记录 Key、Authorization 请求头或完整聊天内容。
- 卸载程序默认保留此数据目录；设置页可主动清除本地数据。

## 开发与测试

要求：Windows 10/11、.NET 8 SDK（更高 SDK 且安装 .NET 8 targeting pack 也可）、Inno Setup 6（仅制作安装器时需要）。

```powershell
$env:DOTNET_CLI_HOME = "$PWD\.dotnet-home"
$env:NUGET_PACKAGES = "$PWD\.nuget\packages"
dotnet restore .\DesktopPet.sln
dotnet test .\tests\DesktopPet.Tests\DesktopPet.Tests.csproj -c Debug
dotnet run --project .\app\DesktopPet.App\DesktopPet.App.csproj
```

自动测试覆盖：

- 状态机取消与睡眠、点击/双击/拖动互斥、好感度边界。
- API URL 规范化、安全拒绝规则、401/403/模型/超时分类。
- SSE 任意网络分块、跨 UTF-8 分块、空增量、`[DONE]` 和取消。
- DPAPI、设置明文泄漏检查、原子写入、配置损坏恢复、20 条历史上限。
- Alpha HRGN 在 100%/125%/150% 输出尺寸下的角落与中心命中几何。
- Mutex + 命名管道单实例往返。

GUI 回归脚本：

```powershell
.\scripts\gui-validation.ps1 -DurationSeconds 65
```

它使用工作区内隔离数据，验证真实 HWND/HRGN、透明像素下层命中、派大星中心命中、不抢焦点、气泡、拖动、右键、双击、聊天、设置、隐藏/恢复、单实例、持续运行、清洁退出和位置重启恢复。

## 发布与安装包

```powershell
.\build-release.ps1
```

脚本严格依次执行 restore、test、publish 和 Inno Setup。任一步失败会立即停止。默认输出：

```text
artifacts\release\publish\DesktopPet.exe
artifacts\release\installer\DesktopPaiZong-Setup-1.0.0-win-x64.exe
artifacts\release\test-results\desktop-pet-release-tests.trx
artifacts\release\checksums.sha256
artifacts\release\build-release.log
```

安装器以当前用户权限安装到 `%LOCALAPPDATA%\Programs\DesktopPet`，创建桌面和开始菜单快捷方式，支持覆盖升级和标准卸载。卸载时删除程序和 `HKCU\...\Run` 开机启动项，默认保留用户设置。

## 安全与验收状态

- 源码和提交前会扫描 `sk-`、`sk-ws-` 与真实 Bearer Token。
- 仓库和发布包不包含 API Key。
- 真实百炼 `qwen3.6-flash` 调用必须由用户之后在设置页填写新 Key 后验证；开发过程没有复用任何旧 Key。
- 当前机器实际 GUI 验证 DPI 为 96（100%）；125%/150% 使用与运行时相同的 Alpha 缩放几何自动测试覆盖。若验收机器有 125%/150% 显示器，还应进行一次实际目视复核。
- 自动测试通过不等于用户实际验收。旧 Tauri 源码已按用户要求从当前工作树移除，历史版本仍可从 `legacy-tauri-v1` 标签取回。

# 桌宠 WPF 完整重构计划

## 1. 重构目标与边界

- 在 `D:\TestMimo` 中使用 C#、.NET 8、原生 WPF 从头实现稳定、完整、可安装的 Windows 桌宠。
- 不延续旧 Tauri/Vite/Rust 的透明 WebView、白色色键、全屏透明窗、光标轮询或阻塞式拖拽方案。
- 旧 Tauri/Vite/Rust 源码通过 `main` 分支和 `legacy-tauri-v1` 标签留档，不再保留在当前 WPF 工作树中。
- 首次启动使用 `%LOCALAPPDATA%\DesktopPet` 下的全新配置，不迁移旧 WebView localStorage。
- 不读取、使用或保存聊天中曾暴露的旧 API Key；任何 Key 都不得进入源码、Git、日志、测试快照、配置模板或安装包。
- 发布产物、构建缓存、用户配置和日志不纳入版本管理。

## 2. 执行与版本管理规则

1. 审计 `桌宠开发计划.md`、旧项目源码、配置、资源和日志，保留用户文件。
2. 若 `.git` 经确认确实为空，则移除空目录并执行 `git init -b main`。
3. 创建严格 `.gitignore`，至少排除 `node_modules`、`dist`、`target`、`.codex-target*`、`bin`、`obj`、`publish`、安装包输出、日志、用户配置、Key 和密钥文件。
4. 在提交前扫描 `sk-`、`sk-ws-`、Bearer Token 及疑似密钥。
5. 第一个提交为 `chore: snapshot legacy Tauri implementation`，并创建 `legacy-tauri-v1` 标签。
6. 创建并切换 `rewrite/wpf` 分支；按独立功能节点使用 Conventional Commits。
7. 用户明确完成实际验收前，不删除旧项目、不合并到 `main`、不创建 `v1.0.0` 标签。
8. 用户验收后才执行旧版清理、完整回归、合并、README/CHANGELOG 最终更新和 `v1.0.0` 标记。

## 3. 目标工程结构

```text
D:\TestMimo\
├── DesktopPet.sln
├── app\DesktopPet.App\
├── tests\DesktopPet.Tests\
├── installer\
├── build-release.ps1
├── README.md
└── CHANGELOG.md
```

- 应用：`net8.0-windows`、WPF、`win-x64`。
- 测试：独立测试项目，覆盖纯逻辑、网络流解析、持久化与安全边界。
- 发布：`SelfContained=true`、`PublishSingleFile=true`、`IncludeNativeLibrariesForSelfExtract=true`、`PublishTrimmed=false`。

## 4. 原生桌宠窗口

- `PetWindow` 使用 WPF 透明无边框小窗口，仅包围猫咪；置顶、跳过任务栏、显示时不激活且不抢占当前应用焦点。
- 应用清单启用 Per-Monitor V2 DPI，所有坐标换算区分 WPF DIP 与 Win32 物理像素。
- 按当前动画帧 Alpha 通道生成 Win32 HRGN，并用 `SetWindowRgn` 更新窗口区域；透明像素不属于窗口命中区域，可直接点击其下方桌面或应用。
- 禁止白色色键、矩形背景、全屏透明窗口、透明 WebView2、鼠标轮询和同步阻塞拖拽。
- 位置恢复支持多显示器、负坐标以及 100%/125%/150% 缩放；如果显示器布局变化，钳制到虚拟屏幕并保证至少部分猫咪可见。

## 5. 动画、状态机与交互

- 复用 `public\cat-sprites.png`（512×384，每帧 64×64），作为 WPF Resource 嵌入程序集。
- 定义 `idle`、`walk`、`sleep`、`fall`、`drag`、`reaction`、`happy`、`sad` 的帧集合与帧速率；`walk` 仅为原地玩耍动画，不移动窗口。
- 使用单一可取消状态机协调临时动作和睡眠，不允许多个计时器竞争覆盖状态。
- 左键按下后，只有移动超过系统拖动阈值才进入拖动；使用 WPF 鼠标捕获和非阻塞移动，移出旧窗口区域仍连续拖动。
- 拖动期间保持 `drag` 帧可见，松手后位置固定、保存并返回 `idle`；不下落、不吸附任务栏、不自行走开。
- 延迟提交单击以与双击互斥：单击触发 `reaction`、随机气泡、好感度 +1；第二击在系统双击区间内到达时取消待执行单击并打开聊天。
- 右键菜单：喂食（+5）、抚摸（+3）、玩耍（+8）、打开聊天、设置、隐藏、退出；好感度始终限制在 0–100。
- 长时间无交互可进入 `sleep`，但绝不改变窗口位置。

## 6. 气泡、聊天和设置窗口

- `BubbleWindow` 是独立透明置顶窗口，Win32 扩展样式设为不激活且完全鼠标穿透；根据猫咪和工作区位置选择上下左右方向并避开屏幕边缘，超时自动隐藏。
- `ChatWindow` 是正常可调整大小的 WPF 窗口，具有合理最小尺寸；全应用最多一个实例，重复打开时恢复、置前并聚焦。
- 聊天支持发送、Enter 发送、Shift+Enter 换行、停止生成、清空历史；关闭窗口仅取消当前请求并隐藏/关闭聊天，不退出应用。
- 保存最近 20 条 `user`/`assistant` 消息；系统提示不进入持久化历史。
- `SettingsWindow` 提供猫咪名字、大小、API 地址、API Key、模型、开机启动和 API 测试；另提供创建桌面快捷方式、打开日志目录、清空历史、清除本地数据。
- 默认百炼预设：`https://dashscope.aliyuncs.com/compatible-mode/v1` 与 `qwen3.6-flash`；支持百炼业务空间、DeepSeek、Moonshot 和其他 OpenAI-compatible 地址。

## 7. OpenAI-compatible Chat Completions

- 提供 `IChatCompletionClient`、`OpenAiCompatibleChatClient`、`ChatRequest`、`ChatMessage`、`ChatDelta`、`ApiTestResult`。
- 规范化 `/v1` 基础地址与完整 `/chat/completions` 地址；拒绝 `bailian.console.aliyun.com` 控制台网页地址。
- 远程地址强制 HTTPS，仅 `localhost`/`127.0.0.1` 允许 HTTP；远程服务未提供 API Key 时禁止请求。
- 使用 `HttpClient`、`HttpCompletionOption.ResponseHeadersRead` 和请求级取消/超时。
- SSE 解析器维护跨网络分块缓冲，按事件边界处理 `data: JSON`、空增量、`choices[0].delta.content` 和 `[DONE]`。
- API Key 只进入 `Authorization: Bearer` 请求头，不进入请求体、错误文本或日志。
- API 测试明确区分 URL 错误、401、403、模型不存在、TLS、一般网络错误和超时。

## 8. 设置、安全与持久化

- 数据目录固定为 `%LOCALAPPDATA%\DesktopPet`：`settings.json`、`chat-history.json`、`logs\app.log`。
- API Key 使用 Windows DPAPI CurrentUser 加密；`settings.json` 仅保存密文，永不保存明文。
- 设置和历史写入先写同目录临时文件，再以原子替换/移动提交；任何阶段失败不破坏最后一份有效数据。
- JSON 损坏时，将原文件备份为带时间戳的 `.corrupt-*` 文件并恢复默认值。
- 日志实现脱敏，不记录 Key、Authorization 请求头或完整聊天内容。
- 使用 HKCU `Software\Microsoft\Windows\CurrentVersion\Run` 管理开机启动，命令行路径正确加引号；关闭和卸载均清除注册表项。
- 桌面快捷方式使用 Windows Shell COM 创建，且不嵌入任何敏感配置。

## 9. 托盘、单实例与生命周期

- 使用原生 `NotifyIcon`，菜单包含显示/隐藏、聊天、设置、关于、退出；双击托盘恢复猫咪。
- WPF `ShutdownMode=OnExplicitShutdown`；关闭聊天或设置不终止桌宠。
- 使用命名 Mutex 判定首实例，使用命名管道把第二次启动请求转发到首实例；第二实例立即退出，不产生第二只猫或第二个托盘图标。
- 正常退出按顺序取消请求和状态机、关闭管道、隐藏/关闭窗口、释放托盘图标和计时器、保存状态并显式关闭应用。

## 10. 测试与 GUI 验证

- 状态机：可取消切换、临时状态恢复、睡眠不移动位置。
- 交互：单击/双击/拖动互斥、系统阈值、好感度 0–100 边界。
- 网络：URL 规范化和拒绝规则、SSE 任意分块、跨 UTF-8 分块、空增量、`[DONE]`、取消、超时和错误分类。
- 持久化：DPAPI 往返与密文不含明文、原子写入、配置损坏备份恢复、聊天历史最多 20 条。
- 实际启动 Debug/Release GUI，检查窗口结构、资源加载、异常日志、单实例和关闭行为。
- 在可用显示环境中检查 100%/125%/150% DPI、Alpha 区域点击穿透、猫咪点击/双击/右键/拖动、拖动不消失且松手固定、聊天和设置布局、托盘菜单与显示/隐藏。
- 连续至少 60 秒运行并重复拖动、隐藏、恢复和开窗；结束前关闭全部测试/开发进程。
- 使用用户之后主动填写的新 Key 验证百炼 `qwen3.6-flash`；在此之前仅验证请求构造、错误分类和本地模拟服务，不猜测或复用旧 Key。

## 11. 发布、安装和交付

- `build-release.ps1` 严格依次执行 restore、test、publish、Inno Setup；设置错误终止，任一步失败即停止且不报告伪成功。
- Inno Setup 6 当前用户安装到 `%LOCALAPPDATA%\Programs\DesktopPet`，创建桌面与开始菜单快捷方式，支持覆盖升级和标准卸载。
- 卸载删除程序与 DesktopPet 开机启动项，默认保留 `%LOCALAPPDATA%\DesktopPet` 用户设置；提供可选清理说明。
- 生成自包含单文件 EXE 和安装包，并报告 EXE、安装包与验证日志的绝对路径、哈希和测试结论。
- 自动验证通过后保持 `rewrite/wpf` 分支和旧项目共存，明确列出仍需用户亲自完成的实际视觉、交互与真实 API 验收项。

## 12. 里程碑提交建议

1. `chore: snapshot legacy Tauri implementation`
2. `chore: scaffold .NET 8 WPF solution`
3. `feat: add secure settings and chat persistence`
4. `feat: add OpenAI-compatible streaming client`
5. `feat: implement cancellable pet state and interactions`
6. `feat: implement alpha-shaped desktop pet window`
7. `feat: add chat settings tray and single-instance lifecycle`
8. `test: cover state networking and persistence`
9. `build: add self-contained release and Inno installer`
10. `docs: document WPF desktop pet usage and verification`

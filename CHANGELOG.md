# Changelog

本项目遵循 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/) 的结构。

## [Unreleased]

### Added

- 从头创建 .NET 8 WPF 解决方案、应用项目和 MSTest 测试项目。
- 原生 Alpha HRGN 桌宠窗口、独立穿透气泡窗口、可取消动画状态机和非阻塞拖动。
- 单击/双击/拖动互斥、好感度和右键互动菜单。
- OpenAI-compatible Chat Completions 流式客户端、跨分块 SSE 解析、超时/取消和错误分类。
- DPAPI CurrentUser Key 加密、原子 JSON、损坏恢复和 20 条聊天历史。
- 聊天窗口、设置窗口、NotifyIcon 托盘、HKCU 开机启动和快捷方式创建。
- Mutex + 当前用户 ACL 命名管道单实例。
- 自包含单文件发布脚本、Inno Setup 6 当前用户安装器和 SHA-256 清单。
- 自动化 Win32 GUI 验证脚本与 100%/125%/150% Alpha 几何测试。

### Changed

- 默认桌宠大小由 200% 调整为 100%，即原默认尺寸的 1/2；精灵图保持等比例缩放。
- 默认 AI 服务改为阿里云百炼兼容地址，默认模型为 `qwen3.6-flash`。
- `walk` 只作为原地玩耍动画，不再移动桌宠窗口。
- 拖动释放后固定当前位置，不再下落或吸附任务栏。

### Security

- API Key 不进入源码、Git、日志、测试快照、配置模板或安装包。
- 远程 API 强制 HTTPS，控制台网页地址被拒绝，远程空 Key 请求被阻止。
- 日志对 Authorization 和疑似 API Key 进行脱敏。

### Validation

- 自动单元测试覆盖状态、交互、网络、SSE、DPAPI、持久化、单实例和 DPI Alpha 几何。
- 实际 GUI 验证覆盖 HRGN 点击穿透、焦点、拖动、气泡、右键、聊天、设置、隐藏/恢复、65 秒稳定性、单实例、清洁退出和位置恢复。

### Pending User Acceptance

- 使用用户新填写的百炼 API Key 验证 `qwen3.6-flash` 真实对话。
- 用户在自己的 125%/150% DPI 显示器上进行最终目视与交互验收（如有）。
- 验收后删除旧 Tauri/Vite/Rust 项目，完整回归，合并 `main` 并创建 `v1.0.0` 标签。

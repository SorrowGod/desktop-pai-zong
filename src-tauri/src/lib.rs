mod window_ext;

use serde::{Deserialize, Serialize};
use std::sync::Mutex;
use tauri::{
    AppHandle, Emitter, Manager, WebviewUrl, WebviewWindowBuilder,
    menu::{Menu, MenuItem},
    tray::TrayIconBuilder,
};

static PENDING_ACTION: once_cell::sync::Lazy<Mutex<Option<String>>> =
    once_cell::sync::Lazy::new(|| Mutex::new(None));

#[derive(Debug, Serialize, Deserialize, Clone)]
pub struct ChatMessage { pub role: String, pub content: String }

#[derive(Debug, Serialize, Deserialize)]
pub struct ChatRequest {
    pub api_base: String,
    pub api_key: String,
    pub model: String,
    pub messages: Vec<ChatMessage>,
}

#[derive(Debug, Serialize, Deserialize, Clone)]
pub struct ChatChunk { pub content: String, pub done: bool }

#[derive(Debug, Clone, Serialize, Deserialize, Default)]
pub struct Rect { pub x: f64, pub y: f64, pub width: f64, pub height: f64 }

#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct DragResult { pub x: i32, pub y: i32, pub moved: bool }

fn chat_completions_url(api_base: &str) -> String {
    let trimmed = api_base.trim().trim_end_matches('/');
    if trimmed.contains("bailian.console.aliyun.com") {
        return "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions".to_string();
    }
    if trimmed.ends_with("/chat/completions") {
        trimmed.to_string()
    } else {
        format!("{}/chat/completions", trimmed)
    }
}

fn format_api_error(status: reqwest::StatusCode, text: String) -> String {
    if text.contains("invalid_api_key")
        || text.contains("Invalid API-key")
        || text.contains("Incorrect API key")
    {
        return format!(
            "API 错误 {}: 百炼鉴权失败，API Key 不正确或不属于当前业务空间。原始返回: {}",
            status, text
        );
    }
    if text.contains("AllocationQuota.FreeTierOnly") {
        return format!(
            "API 错误 {}: 百炼免费额度不可用或账号当前只允许免费额度调用。原始返回: {}",
            status, text
        );
    }
    if text.contains("model_not_found")
        || text.contains("Model.NotFound")
        || (text.contains("model") && text.contains("does not exist"))
    {
        return format!(
            "API 错误 {}: 模型名称不可用，请确认模型 ID 和业务空间权限。原始返回: {}",
            status, text
        );
    }
    format!("API 错误 {}: {}", status, text)
}

#[tauri::command]
fn update_interactive_regions(_regions: Vec<Rect>) {
}

#[tauri::command]
fn take_pending_action() -> Option<String> {
    PENDING_ACTION.lock().ok().and_then(|mut action| action.take())
}

fn set_pending_action(action: &str) {
    if let Ok(mut pending) = PENDING_ACTION.lock() {
        *pending = Some(action.to_string());
    }
}

#[tauri::command]
fn set_main_window_size(app: AppHandle, width: u32, height: u32) -> Result<(), String> {
    let window = app
        .get_webview_window("main")
        .ok_or_else(|| "找不到主窗口".to_string())?;
    let raw_hwnd = window.hwnd().map_err(|e| e.to_string())?.0 as isize;
    window_ext::set_window_size(raw_hwnd, width as i32, height as i32)?;
    window_ext::enable_white_colorkey(raw_hwnd);
    Ok(())
}

fn show_or_create_panel_window(
    app: &AppHandle,
    label: &str,
    title: &str,
    page: &str,
    width: f64,
    height: f64,
) -> Result<(), String> {
    if let Some(window) = app.get_webview_window(label) {
        let _ = window.destroy();
    }

    let url = WebviewUrl::App(page.into());
    let window = WebviewWindowBuilder::new(app, label, url)
        .title(title)
        .inner_size(width, height)
        .resizable(false)
        .decorations(true)
        .always_on_top(true)
        .skip_taskbar(false)
        .center()
        .build()
        .map_err(|e| e.to_string())?;
    window.set_focus().map_err(|e| e.to_string())
}

#[tauri::command]
fn open_chat_window(app: AppHandle) -> Result<(), String> {
    show_or_create_panel_window(&app, "chat", "和猫咪聊天", "chat.html", 360.0, 500.0)
}

#[tauri::command]
fn open_settings_window(app: AppHandle) -> Result<(), String> {
    show_or_create_panel_window(&app, "settings", "桌面猫咪设置", "settings.html", 360.0, 500.0)
}

#[tauri::command]
fn close_current_window(app: AppHandle, label: String) -> Result<(), String> {
    let window = app
        .get_webview_window(&label)
        .ok_or_else(|| format!("找不到窗口: {}", label))?;
    window.destroy().map_err(|e| e.to_string())
}

#[tauri::command]
fn hide_main_window(app: AppHandle) -> Result<(), String> {
    let window = app
        .get_webview_window("main")
        .ok_or_else(|| "找不到主窗口".to_string())?;
    window.hide().map_err(|e| e.to_string())
}

#[tauri::command]
fn show_main_window(app: AppHandle) -> Result<(), String> {
    let window = app
        .get_webview_window("main")
        .ok_or_else(|| "找不到主窗口".to_string())?;
    let raw_hwnd = window.hwnd().map_err(|e| e.to_string())?.0 as isize;
    window_ext::set_window_size(raw_hwnd, 180, 180)?;
    window_ext::enable_white_colorkey(raw_hwnd);
    window.show().map_err(|e| e.to_string())?;
    window.set_focus().map_err(|e| e.to_string())
}

#[tauri::command]
fn exit_app(app: AppHandle) {
    app.exit(0);
}

#[tauri::command]
fn drag_main_window(app: AppHandle) -> Result<DragResult, String> {
    let window = app
        .get_webview_window("main")
        .ok_or_else(|| "找不到主窗口".to_string())?;
    let raw_hwnd = window.hwnd().map_err(|e| e.to_string())?.0 as isize;
    let (x, y, moved) = window_ext::drag_window_until_mouse_up(raw_hwnd)?;
    Ok(DragResult { x, y, moved })
}

#[tauri::command]
async fn send_chat_message(app: AppHandle, request: ChatRequest) -> Result<(), String> {
    let url = chat_completions_url(&request.api_base);
    let client = reqwest::Client::new();
    let body = serde_json::json!({
        "model": request.model, "messages": request.messages, "stream": true,
    });
    let response = client.post(&url)
        .header("Content-Type", "application/json")
        .header("Authorization", format!("Bearer {}", request.api_key))
        .json(&body).send().await
        .map_err(|e| format!("请求失败: {}", e))?;
    if !response.status().is_success() {
        let status = response.status();
        let text = response.text().await.unwrap_or_default();
        return Err(format_api_error(status, text));
    }
    let mut stream = response.bytes_stream();
    use futures_util::StreamExt;
    let mut buffer = String::new();
    while let Some(chunk_result) = stream.next().await {
        let chunk = chunk_result.map_err(|e| format!("流读取错误: {}", e))?;
        for ch in String::from_utf8_lossy(&chunk).chars() {
            buffer.push(ch);
            if ch == '\n' {
                let line = buffer.trim().to_string();
                buffer.clear();
                if line.starts_with("data:") {
                    let data = line[5..].trim();
                    if data == "[DONE]" {
                        let _ = app.emit("chat-stream", ChatChunk { content: String::new(), done: true });
                        return Ok(());
                    }
                    if let Ok(json) = serde_json::from_str::<serde_json::Value>(data) {
                        if let Some(delta) = json["choices"][0]["delta"]["content"].as_str() {
                            let _ = app.emit("chat-stream", ChatChunk { content: delta.to_string(), done: false });
                        }
                    }
                }
            }
        }
    }
    let _ = app.emit("chat-stream", ChatChunk { content: String::new(), done: true });
    Ok(())
}

#[tauri::command]
async fn test_chat_api(request: ChatRequest) -> Result<String, String> {
    let url = chat_completions_url(&request.api_base);
    let client = reqwest::Client::new();
    let body = serde_json::json!({
        "model": request.model,
        "messages": request.messages,
        "stream": false,
    });

    let response = client
        .post(&url)
        .header("Content-Type", "application/json")
        .header("Authorization", format!("Bearer {}", request.api_key.trim()))
        .json(&body)
        .send()
        .await
        .map_err(|e| format!("请求失败: {}", e))?;

    let status = response.status();
    let text = response.text().await.unwrap_or_default();
    if !status.is_success() {
        return Err(format_api_error(status, text));
    }

    let json: serde_json::Value = serde_json::from_str(&text)
        .map_err(|e| format!("返回内容不是有效 JSON: {}。原始返回: {}", e, text))?;
    let content = json["choices"][0]["message"]["content"]
        .as_str()
        .unwrap_or("")
        .trim()
        .to_string();
    Ok(content)
}

#[tauri::command]
fn set_autostart(enable: bool) -> Result<(), String> {
    use windows::Win32::System::Registry::*;
    let key_path = b"Software\\Microsoft\\Windows\\CurrentVersion\\Run\0";
    let app_name = b"DesktopPet\0";
    unsafe {
        let mut key = HKEY::default();
        let result = RegOpenKeyExA(HKEY_CURRENT_USER, windows::core::PCSTR(key_path.as_ptr()), 0, KEY_SET_VALUE | KEY_READ, &mut key);
        if result.is_err() { return Err("无法打开注册表".into()); }
        if enable {
            let exe_path = std::env::current_exe().map_err(|e| e.to_string())?.to_string_lossy().to_string();
            let value = format!("\"{}\"\0", exe_path);
            let _ = RegSetValueExA(key, windows::core::PCSTR(app_name.as_ptr()), 0, REG_SZ, Some(value.as_bytes()));
        } else {
            let _ = RegDeleteValueA(key, windows::core::PCSTR(app_name.as_ptr()));
        }
        let _ = RegCloseKey(key);
    }
    Ok(())
}

#[tauri::command]
fn check_autostart() -> bool {
    use windows::Win32::System::Registry::*;
    let key_path = b"Software\\Microsoft\\Windows\\CurrentVersion\\Run\0";
    let app_name = b"DesktopPet\0";
    unsafe {
        let mut key = HKEY::default();
        let result = RegOpenKeyExA(HKEY_CURRENT_USER, windows::core::PCSTR(key_path.as_ptr()), 0, KEY_READ, &mut key);
        if result.is_err() { return false; }
        let mut buf = [0u8; 512];
        let mut buf_size = buf.len() as u32;
        let mut val_type: REG_VALUE_TYPE = REG_VALUE_TYPE(0);
        let result = RegQueryValueExA(key, windows::core::PCSTR(app_name.as_ptr()), None, Some(&mut val_type), Some(buf.as_mut_ptr()), Some(&mut buf_size));
        let _ = RegCloseKey(key);
        result.is_ok()
    }
}

pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_shell::init())
        .invoke_handler(tauri::generate_handler![
            send_chat_message, update_interactive_regions, take_pending_action,
            test_chat_api,
            set_main_window_size, hide_main_window, show_main_window, drag_main_window,
            exit_app, set_autostart, check_autostart, open_chat_window, open_settings_window,
            close_current_window,
        ])
        .setup(|app| {
            let show_item = MenuItem::with_id(app, "show", "显示/隐藏猫咪", true, None::<&str>)?;
            let settings_item = MenuItem::with_id(app, "settings", "设置", true, None::<&str>)?;
            let about_item = MenuItem::with_id(app, "about", "关于", true, None::<&str>)?;
            let quit_item = MenuItem::with_id(app, "quit", "退出", true, None::<&str>)?;
            let menu = Menu::with_items(app, &[&show_item, &settings_item, &about_item, &quit_item])?;
            let _tray = TrayIconBuilder::new()
                .icon(app.default_window_icon().unwrap().clone())
                .menu(&menu)
                .tooltip("桌面猫咪")
                .on_menu_event(|app, event| match event.id.as_ref() {
                    "show" => {
                        if let Some(w) = app.get_webview_window("main") {
                            match w.is_visible() {
                                Ok(true) => { let _ = w.hide(); }
                                _ => {
                                    if let Ok(hwnd) = w.hwnd() {
                                        let _ = window_ext::set_window_size(hwnd.0 as isize, 180, 180);
                                        window_ext::enable_white_colorkey(hwnd.0 as isize);
                                    }
                                    let _ = w.show();
                                    let _ = w.set_focus();
                                }
                            }
                        }
                    }
                    "settings" => {
                        let _ = open_settings_window(app.clone());
                    }
                    "about" => {
                        if let Some(w) = app.get_webview_window("main") {
                            set_pending_action("show-about");
                            if let Ok(hwnd) = w.hwnd() {
                                let _ = window_ext::set_window_size(hwnd.0 as isize, 180, 180);
                            }
                            let _ = w.show();
                            let _ = w.set_focus();
                            let _ = w.emit("show-about", ());
                        }
                    }
                    "quit" => { app.exit(0); }
                    _ => {}
                })
                .build(app)?;

            if let Some(window) = app.get_webview_window("main") {
                // 获取 HWND 并启动穿透轮询
                let raw_hwnd = window.hwnd().unwrap().0 as isize;

                // 移除边框样式
                window_ext::remove_border(raw_hwnd);
                window_ext::enable_white_colorkey(raw_hwnd);
            }
            Ok(())
        })
        .run(tauri::generate_context!())
        .expect("failed to run app");
}

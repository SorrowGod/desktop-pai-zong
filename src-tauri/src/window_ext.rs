use windows::Win32::Foundation::{BOOL, COLORREF, HWND, LPARAM, POINT, RECT};
use windows::Win32::UI::Input::KeyboardAndMouse::{GetAsyncKeyState, VK_LBUTTON};
use windows::Win32::UI::WindowsAndMessaging::{
    EnumChildWindows, GetCursorPos, GetWindowLongW, GetWindowRect, SetLayeredWindowAttributes,
    SetWindowLongW, SetWindowPos, GWL_EXSTYLE, LWA_COLORKEY, SWP_FRAMECHANGED, SWP_NOMOVE,
    SWP_NOSIZE, SWP_NOZORDER, SWP_SHOWWINDOW, WS_EX_CLIENTEDGE, WS_EX_LAYERED, WS_EX_WINDOWEDGE,
};

/// 移除窗口边框样式（解决 WebView2 可见边框问题）。
pub fn remove_border(hwnd: isize) {
    let hwnd = HWND(hwnd as *mut _);
    unsafe {
        remove_border_from_hwnd(hwnd);
        let _ = EnumChildWindows(hwnd, Some(enum_remove_border), LPARAM(0));
    }
}

pub fn enable_white_colorkey(hwnd: isize) {
    let hwnd = HWND(hwnd as *mut _);
    unsafe {
        enable_white_colorkey_for_hwnd(hwnd);
    }
}

unsafe fn enable_white_colorkey_for_hwnd(hwnd: HWND) {
    let ex_style = GetWindowLongW(hwnd, GWL_EXSTYLE);
    SetWindowLongW(hwnd, GWL_EXSTYLE, ex_style | WS_EX_LAYERED.0 as i32);
    let _ = SetLayeredWindowAttributes(hwnd, COLORREF(0x00ff_ffff), 0, LWA_COLORKEY);
    let _ = SetWindowPos(
        hwnd,
        None,
        0,
        0,
        0,
        0,
        SWP_NOZORDER | SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED,
    );
}

unsafe extern "system" fn enum_remove_border(hwnd: HWND, _lparam: LPARAM) -> BOOL {
    remove_border_from_hwnd(hwnd);
    BOOL(1)
}

unsafe fn remove_border_from_hwnd(hwnd: HWND) {
    let ex_style = GetWindowLongW(hwnd, GWL_EXSTYLE);
    let new_style = ex_style
        & !(WS_EX_CLIENTEDGE.0 as i32)
        & !(WS_EX_WINDOWEDGE.0 as i32);
    SetWindowLongW(hwnd, GWL_EXSTYLE, new_style);
    let _ = SetWindowPos(
        hwnd,
        None,
        0,
        0,
        0,
        0,
        SWP_NOZORDER | SWP_NOMOVE | SWP_NOSIZE | SWP_FRAMECHANGED,
    );
}

pub fn set_window_size(hwnd: isize, width: i32, height: i32) -> Result<(), String> {
    let hwnd = HWND(hwnd as *mut _);
    unsafe {
        let mut rect = RECT::default();
        GetWindowRect(hwnd, &mut rect as *mut RECT).map_err(|e| e.to_string())?;
        SetWindowPos(hwnd, None, rect.left, rect.top, width, height, SWP_NOZORDER | SWP_SHOWWINDOW)
            .map_err(|e| e.to_string())
    }
}

pub fn drag_window_until_mouse_up(hwnd: isize) -> Result<(i32, i32, bool), String> {
    let hwnd = HWND(hwnd as *mut _);
    unsafe {
        let start_cursor = cursor_pos().ok_or_else(|| "无法读取鼠标位置".to_string())?;
        let mut rect = RECT::default();
        GetWindowRect(hwnd, &mut rect as *mut RECT).map_err(|e| e.to_string())?;
        let start_x = rect.left;
        let start_y = rect.top;
        let mut moved = false;

        while (GetAsyncKeyState(VK_LBUTTON.0 as i32) as u16 & 0x8000) != 0 {
            if let Some(cursor) = cursor_pos() {
                let x = start_x + cursor.x - start_cursor.x;
                let y = start_y + cursor.y - start_cursor.y;
                if (x - start_x).abs() + (y - start_y).abs() > 4 {
                    moved = true;
                }
                if moved {
                    let _ = SetWindowPos(hwnd, None, x, y, 0, 0, SWP_NOZORDER | SWP_NOSIZE);
                }
            }
            std::thread::sleep(std::time::Duration::from_millis(8));
        }

        let mut final_rect = RECT::default();
        GetWindowRect(hwnd, &mut final_rect as *mut RECT).map_err(|e| e.to_string())?;
        Ok((final_rect.left, final_rect.top, moved))
    }
}

unsafe fn cursor_pos() -> Option<POINT> {
    let mut point = POINT { x: 0, y: 0 };
    if GetCursorPos(&mut point).is_ok() {
        Some(point)
    } else {
        None
    }
}

import { Cat } from './cat.js';
import { ChatManager } from './chat.js';
import { loadConfig, saveConfig } from './config.js';

let tauriInvoke = null;
let tauriWindow = null;

document.body.dataset.view = 'pet';
async function getInvoke() {
  if (!tauriInvoke) {
    try {
      const { invoke } = await import('@tauri-apps/api/core');
      tauriInvoke = invoke;
    } catch {}
  }
  return tauriInvoke;
}

async function getTauriWindow() {
  if (!tauriWindow) {
    try {
      const { getCurrentWindow } = await import('@tauri-apps/api/window');
      tauriWindow = getCurrentWindow();
    } catch {}
  }
  return tauriWindow;
}

async function makeTauriWindowTransparent() {
  try {
    const { getCurrentWebview } = await import('@tauri-apps/api/webview');
    await getCurrentWebview().setBackgroundColor('#00000000');
  } catch {}
}

const canvas = document.getElementById('pet-canvas');
const ctx = canvas.getContext('2d');
const contextMenu = document.getElementById('context-menu');
const settingsPanel = document.getElementById('settings-panel');

let config = loadConfig();
config.pinnedMode = true;
let cat;
let chatManager;
let lastTime = 0;
let petVisible = true;
const WINDOW_SIZE = 180;

function normalizeModelName(modelName) {
  const value = (modelName || '').trim();
  return value === 'qwen' ? 'qwen-plus' : value;
}

function resizeCanvas() {
  canvas.width = window.innerWidth;
  canvas.height = window.innerHeight;
  if (cat) cat.groundY = canvas.height - 24;
}
window.addEventListener('resize', resizeCanvas);
resizeCanvas();
makeTauriWindowTransparent();

cat = new Cat(canvas, config);
chatManager = new ChatManager();
chatManager.onVisibilityChange = () => updatePanelMode();

async function applyPetWindowSize() {
  const size = Math.round(WINDOW_SIZE * (config.petSize || 1));
  const invoke = await getInvoke();
  if (invoke) {
    try {
      await invoke('set_main_window_size', { width: size, height: size });
      return;
    } catch {}
  }
}
applyPetWindowSize();

function keepPetCentered() {
  if (!cat) return;
  cat.x = canvas.width / 2;
  cat.y = canvas.height - 24;
  cat.groundY = cat.y;
}

function getPetHitRegion() {
  const size = 64 * cat.scale;
  const padding = 18 * cat.scale;
  return {
    left: cat.x - size / 2 - padding,
    top: cat.y - size - padding,
    right: cat.x + size / 2 + padding,
    bottom: cat.y + padding,
  };
}

function isInPetRegion(x, y) {
  const region = getPetHitRegion();
  return x >= region.left && x <= region.right && y >= region.top && y <= region.bottom;
}

function isInteractiveElement(target) {
  return contextMenu.contains(target) ||
    settingsPanel.contains(target) ||
    chatManager.windowEl.contains(target);
}

async function setPanelWindowMode(open) {
  document.body.classList.toggle('panel-open', open);
  const invoke = await getInvoke();
  if (invoke) {
    const size = Math.round(WINDOW_SIZE * (config.petSize || 1));
    const width = size;
    const height = size;
    try {
      await invoke('set_main_window_size', { width, height });
    } catch {}
  }
  resizeCanvas();
  setTimeout(resizeCanvas, 80);
  setTimeout(makeTauriWindowTransparent, 0);
  setTimeout(makeTauriWindowTransparent, 120);
}

function updatePanelMode() {
  const open = !chatManager.windowEl.classList.contains('hidden') ||
    !settingsPanel.classList.contains('hidden') ||
    !contextMenu.classList.contains('hidden');
  setPanelWindowMode(open);
}

async function persistWindowPosition() {
  try {
    const currentWindow = await getTauriWindow();
    const position = await currentWindow?.outerPosition();
    if (position) {
      config.windowPosition = { x: position.x, y: position.y };
      saveConfig(config);
    }
  } catch {}
}

async function restoreWindowPosition() {
  if (!config.windowPosition) return;
  try {
    const currentWindow = await getTauriWindow();
    const { PhysicalPosition } = await import('@tauri-apps/api/dpi');
    await currentWindow?.setPosition(new PhysicalPosition(
      config.windowPosition.x,
      config.windowPosition.y,
    ));
  }
  catch {}
}
restoreWindowPosition();

function gameLoop(timestamp) {
  const deltaTime = timestamp - lastTime;
  lastTime = timestamp;
  ctx.clearRect(0, 0, canvas.width, canvas.height);
  if (petVisible) {
    keepPetCentered();
    cat.update(deltaTime, canvas.width, canvas.height);
    cat.draw();
  }
  requestAnimationFrame(gameLoop);
}
requestAnimationFrame(gameLoop);

// === 鼠标交互 ===
let isDragging = false;
let isPointerDownOnPet = false;
let pointerDownX = 0;
let pointerDownY = 0;
let clickTimeout = null;

canvas.addEventListener('mousedown', async (e) => {
  if (e.button === 2) return; // 右键不处理
  if (isInteractiveElement(e.target)) return;

  const mx = e.clientX;
  const my = e.clientY;

  if (isInPetRegion(mx, my)) {
    isPointerDownOnPet = true;
    pointerDownX = mx;
    pointerDownY = my;
    e.preventDefault();
  }
});

canvas.addEventListener('mousemove', async (e) => {
  if (!isPointerDownOnPet || isDragging) return;

  const moved = Math.abs(e.clientX - pointerDownX) + Math.abs(e.clientY - pointerDownY);
  if (moved <= 4) return;

  isDragging = true;
  cat.onDragStart();
  const invoke = await getInvoke();
  if (invoke) {
    try {
      const result = await invoke('drag_main_window');
      if (result && typeof result.x === 'number' && typeof result.y === 'number') {
        config.windowPosition = { x: result.x, y: result.y };
        saveConfig(config);
      }
    } catch {}
  }
  isDragging = false;
  isPointerDownOnPet = false;
  cat.onDragEnd();
});

canvas.addEventListener('mouseup', (e) => {
  if (e.button === 2) return;

  if (isPointerDownOnPet && !isDragging) {
    isPointerDownOnPet = false;
    processPetClick(e.clientX, e.clientY);
  }
});

function processPetClick(x, y) {
  // 延迟判断单击/双击
  if (clickTimeout) {
    clearTimeout(clickTimeout);
    clickTimeout = null;
    handleDoubleClick(x, y);
  } else {
    clickTimeout = setTimeout(() => {
      clickTimeout = null;
      cat.onClick();
    }, 250);
  }
}

async function handleDoubleClick(x, y) {
  await openChatWindow();
}

// === 右键菜单 ===
canvas.addEventListener('contextmenu', async (e) => {
  e.preventDefault();
  if (isInPetRegion(e.clientX, e.clientY)) {
    await setPanelWindowMode(true);
    showContextMenu(e.clientX, e.clientY);
  }
});

function showContextMenu(x, y) {
  contextMenu.classList.remove('hidden');
  contextMenu.style.left = '16px';
  contextMenu.style.top = '16px';
  updatePanelMode();
}

function hideContextMenu() {
  contextMenu.classList.add('hidden');
  updatePanelMode();
}

document.addEventListener('click', (e) => {
  if (!contextMenu.contains(e.target)) {
    hideContextMenu();
  }
});

contextMenu.addEventListener('click', (e) => {
  const item = e.target.closest('.context-item');
  if (!item) return;
  const action = item.dataset.action;
  hideContextMenu();

  switch (action) {
    case 'feed': cat.feed(); break;
    case 'pet': cat.pet(); break;
    case 'play': cat.play(); break;
    case 'settings': openSettings(); break;
    case 'hide': hidePetWindow(); break;
    case 'exit': exitApp(); break;
  }
});

// === 设置面板 ===
async function openSettings() {
  await openSettingsWindow();
}

document.getElementById('settings-close').addEventListener('click', () => {
  settingsPanel.classList.add('hidden');
  updatePanelMode();
});

document.getElementById('settings-save').addEventListener('click', async () => {
  config.petName = document.getElementById('pet-name').value || '小喵';
  config.apiBase = document.getElementById('api-base').value || 'https://llm-dujne1fkrf6fj1k5.cn-beijing.maas.aliyuncs.com/compatible-mode/v1';
  config.apiKey = document.getElementById('api-key').value;
  config.modelName = normalizeModelName(document.getElementById('model-name').value) || 'qwen3.6-flash';
  config.petSize = parseFloat(document.getElementById('pet-size').value) || 1;

  cat.config = config;
  cat.scale = config.petSize;
  cat.pinnedMode = true;
  chatManager.refreshConfig();
  saveConfig(config);
  applyPetWindowSize();

  // 设置开机自启
  const autoStart = document.getElementById('auto-start').checked;
  try {
    const invoke = await getInvoke();
    if (invoke) {
      await invoke('set_autostart', { enable: autoStart });
    }
  } catch {}

  settingsPanel.classList.add('hidden');
  updatePanelMode();
});

async function closeCurrentWindow() {
  try {
    const currentWindow = await getTauriWindow();
    await currentWindow?.close();
  } catch {}
}

async function openChatWindow() {
  const invoke = await getInvoke();
  if (invoke) {
    try {
      await invoke('open_chat_window');
      return;
    } catch (err) {
      cat.showBubble('聊天窗口打不开，请重启试试');
      return;
    }
  }
}

async function openSettingsWindow() {
  const invoke = await getInvoke();
  if (invoke) {
    try {
      await invoke('open_settings_window');
      return;
    } catch (err) {
      cat.showBubble('设置窗口打不开，请重启试试');
      return;
    }
  }
}

// === 功能函数 ===
function togglePet() {
  hidePetWindow();
}

function showPet() {
  petVisible = true;
  updatePanelMode();
}

async function hidePetWindow() {
  petVisible = true;
  chatManager.close();
  settingsPanel.classList.add('hidden');
  hideContextMenu();
  await setPanelWindowMode(false);
  try {
    const invoke = await getInvoke();
    if (invoke) {
      await invoke('hide_main_window');
      return;
    }
  } catch {}
}

async function exitApp() {
  try {
    const invoke = await getInvoke();
    if (invoke) {
      await invoke('exit_app');
      return;
    }
  } catch {
    window.close();
  }
}

// 点击空白区域关闭设置
document.addEventListener('mousedown', (e) => {
  if (!settingsPanel.classList.contains('hidden') &&
      !settingsPanel.contains(e.target) &&
      !contextMenu.contains(e.target)) {
    settingsPanel.classList.add('hidden');
  }
});

// 防止 canvas 上的默认拖拽行为
canvas.addEventListener('dragstart', (e) => e.preventDefault());

async function bindTauriEvents() {
  try {
    const { listen } = await import('@tauri-apps/api/event');
    await listen('open-settings', () => handleAppAction('open-settings'));
    await listen('toggle-pet', () => {
      showPet();
    });
    await listen('show-about', () => handleAppAction('show-about'));
  } catch {}
}

bindTauriEvents();

async function handleAppAction(action) {
  if (action === 'open-settings') {
    petVisible = true;
    await openSettingsWindow();
  } else if (action === 'show-about') {
    petVisible = true;
    await setPanelWindowMode(false);
    cat.showBubble('桌面猫咪 v1.0');
  }
}

async function pollPendingAction() {
  try {
    const invoke = await getInvoke();
    if (!invoke) return;
    const action = await invoke('take_pending_action');
    if (action) {
      await handleAppAction(action);
    }
  } catch {}
}

setInterval(pollPendingAction, 500);
pollPendingAction();

import { ChatManager, formatApiError } from './chat.js';
import { loadConfig, saveConfig } from './config.js';

let tauriInvoke = null;
let tauriWindow = null;

async function getInvoke() {
  if (!tauriInvoke) {
    try {
      const { invoke } = await import('@tauri-apps/api/core');
      tauriInvoke = invoke;
    } catch {}
  }
  return tauriInvoke;
}

async function getCurrentTauriWindow() {
  if (!tauriWindow) {
    try {
      const { getCurrentWindow } = await import('@tauri-apps/api/window');
      tauriWindow = getCurrentWindow();
    } catch {}
  }
  return tauriWindow;
}

function normalizeModelName(modelName) {
  const value = (modelName || '').trim();
  return value === 'qwen' ? 'qwen-plus' : value;
}

function readSettingsForm() {
  return {
    petName: document.getElementById('pet-name').value || '小喵',
    apiBase: document.getElementById('api-base').value || 'https://llm-dujne1fkrf6fj1k5.cn-beijing.maas.aliyuncs.com/compatible-mode/v1',
    apiKey: document.getElementById('api-key').value,
    modelName: normalizeModelName(document.getElementById('model-name').value) || 'qwen3.6-flash',
    petSize: parseFloat(document.getElementById('pet-size').value) || 1,
  };
}

async function closeCurrentWindow() {
  try {
    const invoke = await getInvoke();
    const label = document.body.dataset.view;
    if (invoke && label) {
      await invoke('close_current_window', { label });
      return;
    }
  } catch {}

  try {
    const currentWindow = await getCurrentTauriWindow();
    await currentWindow?.close();
  } catch {}
}

async function initChatPanel() {
  const chatManager = new ChatManager();
  chatManager.open(0, 0, window.innerWidth);
}

async function initSettingsPanel() {
  let config = loadConfig();

  document.getElementById('pet-name').value = config.petName || '';
  document.getElementById('api-base').value = config.apiBase || '';
  document.getElementById('api-key').value = config.apiKey || '';
  document.getElementById('model-name').value = config.modelName || '';
  document.getElementById('pet-size').value = String(config.petSize || 1);

  try {
    const invoke = await getInvoke();
    if (invoke) {
      const enabled = await invoke('check_autostart');
      document.getElementById('auto-start').checked = enabled;
    }
  } catch {}

  document.getElementById('settings-close').addEventListener('click', closeCurrentWindow);

  document.getElementById('api-test').addEventListener('click', async () => {
    const resultEl = document.getElementById('api-test-result');
    const testBtn = document.getElementById('api-test');
    const form = readSettingsForm();

    resultEl.className = 'api-test-result';
    if (!form.apiKey.trim()) {
      resultEl.classList.add('error');
      resultEl.textContent = '请先填写 API Key';
      return;
    }

    resultEl.textContent = '测试中...';
    testBtn.disabled = true;
    try {
      const invoke = await getInvoke();
      if (!invoke) throw new Error('当前不是 Tauri 环境，无法从设置页直接测试');
      const reply = await invoke('test_chat_api', {
        request: {
          api_base: form.apiBase,
          api_key: form.apiKey,
          model: form.modelName,
          messages: [{ role: 'user', content: '请只回复：测试成功' }],
        },
      });
      resultEl.classList.add('ok');
      resultEl.textContent = `测试通过：${reply || '接口已返回成功'}`;
    } catch (err) {
      resultEl.classList.add('error');
      resultEl.textContent = '测试失败：' + formatApiError(typeof err === 'string' ? err : err?.message || String(err));
    } finally {
      testBtn.disabled = false;
    }
  });

  document.getElementById('settings-save').addEventListener('click', async () => {
    config = loadConfig();
    Object.assign(config, readSettingsForm());
    saveConfig(config);

    const autoStart = document.getElementById('auto-start').checked;
    try {
      const invoke = await getInvoke();
      if (invoke) {
        await invoke('set_autostart', { enable: autoStart });
      }
    } catch {}

    await closeCurrentWindow();
  });
}

if (document.body.dataset.view === 'chat') {
  initChatPanel();
} else if (document.body.dataset.view === 'settings') {
  initSettingsPanel();
}

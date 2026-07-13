import { loadConfig, saveConfig } from './config.js';

let tauriInvoke = null;
let tauriListen = null;

// 动态加载 Tauri API
async function getTauriAPI() {
  if (!tauriInvoke) {
    try {
      const { invoke } = await import('@tauri-apps/api/core');
      const { listen } = await import('@tauri-apps/api/event');
      tauriInvoke = invoke;
      tauriListen = listen;
    } catch {
      // Not in Tauri context
    }
  }
  return { invoke: tauriInvoke, listen: tauriListen };
}

const SYSTEM_PROMPT_TEMPLATE = `你是一只住在用户桌面上的可爱猫咪桌宠。你的名字叫{name}。
你性格活泼可爱，喜欢撒娇，会用"喵~"、"呜呜"、"嗷~"等语气词。
回复要简短可爱，一般 1-3 句话，不要太长。
当用户夸你时你会开心，当用户凶你时你会难过。
偶尔会关心用户有没有好好休息。`;

function getErrorMessage(err) {
  if (!err) return '未知错误';
  const raw = typeof err === 'string' ? err : err.message;
  if (raw) return formatApiError(raw);
  try {
    return formatApiError(JSON.stringify(err));
  } catch {
    return String(err);
  }
}

export function formatApiError(message) {
  if (!message) return '未知错误';
  if (message.includes('invalid_api_key') || message.includes('Invalid API-key') || message.includes('Incorrect API key')) {
    return '百炼返回 401：API Key 不正确，或这个 key 不属于当前百炼业务空间。请确认 key 没有多复制空格，并且用的是这个 OpenAI-compatible 地址对应业务空间的 key。';
  }
  if (message.includes('AccessDenied') || message.includes('Unauthorized')) {
    return '百炼返回鉴权错误：请检查 API Key、业务空间、模型权限是否匹配。';
  }
  if (message.includes('AllocationQuota.FreeTierOnly')) {
    return '百炼返回 403：这个模型的免费额度已经用完，或账号当前只允许免费额度调用。请到百炼控制台关闭 use free tier only / 开通付费额度，或者换一个还有额度的模型。';
  }
  if (
    (message.includes('model') && message.includes('does not exist')) ||
    message.includes('Model.NotFound') ||
    message.includes('model_not_found')
  ) {
    return '模型名称不可用。百炼里请填真实模型 ID，例如 qwen3.6-flash 或 qwen-plus；如果还不行，请确认该业务空间已开通这个模型。';
  }
  return message;
}

function getChatCompletionsUrl(apiBase) {
  const trimmed = (apiBase || '').trim().replace(/\/+$/, '');
  if (!trimmed) {
    throw new Error('请先在设置中填写 API 地址');
  }
  if (trimmed.includes('bailian.console.aliyun.com')) {
    throw new Error('API 地址填错了：不要填百炼控制台网页链接，请填 OpenAI-compatible 地址，例如 https://llm-dujne1fkrf6fj1k5.cn-beijing.maas.aliyuncs.com/compatible-mode/v1');
  }
  if (trimmed.endsWith('/chat/completions')) return trimmed;
  return `${trimmed}/chat/completions`;
}

function normalizeModelName(modelName) {
  const value = (modelName || '').trim();
  return value === 'qwen' ? 'qwen-plus' : value;
}

export class ChatManager {
  constructor() {
    this.config = loadConfig();
    this.messages = this.config.chatHistory || [];
    this.isOpen = false;
    this.isStreaming = false;
    this.abortController = null;
    this.onVisibilityChange = null;

    this.windowEl = document.getElementById('chat-window');
    this.messagesEl = document.getElementById('chat-messages');
    this.inputEl = document.getElementById('chat-input');
    this.sendBtn = document.getElementById('chat-send');
    this.closeBtn = document.getElementById('chat-close');

    this.bindEvents();
    this.renderMessages();
  }

  bindEvents() {
    this.sendBtn.addEventListener('click', () => this.sendMessage());
    this.inputEl.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        this.sendMessage();
      }
    });
    this.closeBtn.addEventListener('click', () => this.close());
  }

  open(catX, catY, canvasWidth) {
    this.isOpen = true;
    this.windowEl.classList.remove('hidden');

    // 定位窗口在猫咪旁边
    let left = 16;
    let top = 16;
    if (canvasWidth > 360) {
      left = catX + 50;
      top = Math.max(20, catY - 250);
      if (left + 340 > canvasWidth) {
        left = Math.max(16, catX - 390);
      }
    }
    this.windowEl.style.left = left + 'px';
    this.windowEl.style.top = top + 'px';

    this.inputEl.focus();
    this.renderMessages();
    this.onVisibilityChange?.(true);
  }

  close() {
    if (document.body.dataset.view === 'chat') {
      this.closeCurrentWindow();
      return;
    }
    this.isOpen = false;
    this.windowEl.classList.add('hidden');
    if (this.abortController) {
      this.abortController.abort();
      this.abortController = null;
    }
    this.onVisibilityChange?.(false);
  }

  async closeCurrentWindow() {
    try {
      const { getCurrentWindow } = await import('@tauri-apps/api/window');
      await getCurrentWindow().close();
    } catch {}
  }

  renderMessages() {
    this.messagesEl.innerHTML = '';
    for (const msg of this.messages) {
      this.appendMessageEl(msg.role, msg.content);
    }
    this.scrollToBottom();
  }

  appendMessageEl(role, content) {
    const div = document.createElement('div');
    div.className = `chat-msg ${role}`;
    const bubble = document.createElement('div');
    bubble.className = 'chat-bubble';
    bubble.textContent = content;
    div.appendChild(bubble);
    this.messagesEl.appendChild(div);
    return bubble;
  }

  scrollToBottom() {
    this.messagesEl.scrollTop = this.messagesEl.scrollHeight;
  }

  async sendMessage() {
    const text = this.inputEl.value.trim();
    if (!text || this.isStreaming) return;

    if (!this.config.apiKey) {
      this.appendMessageEl('assistant', '请先在设置中配置 API Key 喵~');
      return;
    }

    this.inputEl.value = '';

    // 添加用户消息
    this.messages.push({ role: 'user', content: text });
    this.appendMessageEl('user', text);
    this.scrollToBottom();

    // 创建助手消息占位
    const assistantBubble = this.appendMessageEl('assistant', '思考中...');
    this.isStreaming = true;

    try {
      const systemPrompt = SYSTEM_PROMPT_TEMPLATE.replace('{name}', this.config.petName || '小喵');
      const apiMessages = [
        { role: 'system', content: systemPrompt },
        ...this.messages.slice(-20),
      ];

      const response = await this.callAPI(apiMessages, assistantBubble);
      this.messages.push({ role: 'assistant', content: response });

      // 保存历史（最多20条）
      this.config.chatHistory = this.messages.slice(-20);
      saveConfig(this.config);
    } catch (err) {
      assistantBubble.textContent = '出错了: ' + getErrorMessage(err);
    } finally {
      this.isStreaming = false;
    }
  }

  async callAPI(messages, bubbleEl) {
    const { invoke, listen } = await getTauriAPI();
    const apiBase = (this.config.apiBase || '').trim();
    const apiKey = (this.config.apiKey || '').trim();
    const modelName = normalizeModelName(this.config.modelName) || 'qwen3.6-flash';
    let fullText = '';

    if (invoke && listen) {
      // 使用 Tauri 后端发送请求（避免 CORS）
      const unlisten = await listen('chat-stream', (event) => {
        const chunk = event.payload;
        if (chunk.content) {
          fullText += chunk.content;
          bubbleEl.textContent = fullText;
          this.scrollToBottom();
        }
      });

      try {
        await invoke('send_chat_message', {
          request: {
            api_base: apiBase,
            api_key: apiKey,
            model: modelName,
            messages: messages.map(m => ({ role: m.role, content: m.content })),
          },
        });
      } finally {
        unlisten();
      }
    } else {
      // 降级：直接 fetch（开发模式或非 Tauri 环境）
      this.abortController = new AbortController();
      const url = getChatCompletionsUrl(apiBase);

      const res = await fetch(url, {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${apiKey}`,
        },
        body: JSON.stringify({
          model: modelName,
          messages,
          stream: true,
        }),
        signal: this.abortController.signal,
      });

      if (!res.ok) {
        const errText = await res.text().catch(() => res.statusText);
        throw new Error(`${res.status}: ${errText}`);
      }

      const reader = res.body.getReader();
      const decoder = new TextDecoder();
      let buffer = '';

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split('\n');
        buffer = lines.pop() || '';

        for (const line of lines) {
          const trimmed = line.trim();
          if (!trimmed || !trimmed.startsWith('data:')) continue;
          const data = trimmed.slice(5).trim();
          if (data === '[DONE]') break;

          try {
            const json = JSON.parse(data);
            const delta = json.choices?.[0]?.delta?.content;
            if (delta) {
              fullText += delta;
              bubbleEl.textContent = fullText;
              this.scrollToBottom();
            }
          } catch {
            // skip parse errors
          }
        }
      }
    }

    bubbleEl.textContent = fullText || '(空回复)';
    this.scrollToBottom();
    return fullText;
  }

  refreshConfig() {
    this.config = loadConfig();
  }
}

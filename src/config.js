const DEFAULT_CONFIG = {
  petName: '小喵',
  apiBase: 'https://llm-dujne1fkrf6fj1k5.cn-beijing.maas.aliyuncs.com/compatible-mode/v1',
  apiKey: '',
  modelName: 'qwen3.6-flash',
  petSize: 1,
  affection: 50,
  chatHistory: [],
};

function normalizeConfig(config) {
  const next = { ...DEFAULT_CONFIG, ...config };
  if ((next.modelName || '').trim() === 'qwen') {
    next.modelName = 'qwen-plus';
  }
  return next;
}

export function loadConfig() {
  try {
    const saved = localStorage.getItem('desktop-pet-config');
    if (saved) {
      const parsed = JSON.parse(saved);
      const normalized = normalizeConfig(parsed);
      if (normalized.modelName !== parsed.modelName) {
        saveConfig(normalized);
      }
      return normalized;
    }
  } catch (e) {
    console.warn('Failed to load config:', e);
  }
  return normalizeConfig({});
}

export function saveConfig(config) {
  try {
    localStorage.setItem('desktop-pet-config', JSON.stringify(config));
  } catch (e) {
    console.warn('Failed to save config:', e);
  }
}

export function getConfig() {
  return loadConfig();
}

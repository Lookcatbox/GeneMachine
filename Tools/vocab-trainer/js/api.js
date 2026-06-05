/**
 * DeepSeek API client via local proxy.
 */

import { loadSettings } from './storage.js';

/** 仅图示题补生成使用 v4-pro + Thinking High（不可在 UI 关闭） */
export const DIAGRAM_MODEL = 'deepseek-v4-pro';
export const DIAGRAM_REASONING_EFFORT = 'high';

export async function chatCompletion(messages, options = {}) {
  const {
    jsonMode = true,
    temperature = 0.8,
    maxTokens,
    model,
    thinking,
    reasoningEffort,
  } = options;

  const settings = loadSettings();
  if (!settings.apiKey) {
    throw new Error('请先在「设置」中填写 DeepSeek API Key');
  }

  const body = {
    model: model || settings.apiModel,
    messages,
    temperature,
    stream: false,
  };

  if (maxTokens != null) body.max_tokens = maxTokens;
  if (reasoningEffort) body.reasoning_effort = reasoningEffort;
  if (thinking) body.thinking = thinking;

  if (jsonMode) {
    body.response_format = { type: 'json_object' };
  }

  return postChat(body, settings);
}

/**
 * 仅用于单独生成/补全 diagram 图示时：强制 deepseek-v4-pro + thinking enabled + reasoning high。
 */
export async function diagramChatCompletion(messages, { jsonMode = true, temperature = 0.5, maxTokens = 16384 } = {}) {
  const settings = loadSettings();
  if (!settings.apiKey) {
    throw new Error('请先在「设置」中填写 DeepSeek API Key');
  }

  const body = {
    model: DIAGRAM_MODEL,
    messages,
    temperature,
    stream: false,
    reasoning_effort: DIAGRAM_REASONING_EFFORT,
    thinking: { type: 'enabled' },
    max_tokens: maxTokens,
  };

  if (jsonMode) {
    body.response_format = { type: 'json_object' };
  }

  return postChat(body, settings, { diagramMode: true });
}

async function postChat(body, settings, { diagramMode = false } = {}) {
  const res = await fetch(settings.proxyUrl, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'X-Api-Key': settings.apiKey,
      'X-Api-Base': settings.apiBase,
      ...(diagramMode ? { 'X-Diagram-Mode': '1' } : {}),
    },
    body: JSON.stringify(body),
  });

  const data = await res.json().catch(() => ({}));
  if (!res.ok) {
    const msg = data.error?.message || data.detail || data.error || res.statusText;
    throw new Error(`API 错误 (${res.status}): ${msg}`);
  }

  const content = data.choices?.[0]?.message?.content;
  if (!content) throw new Error('API 返回空内容');
  return content;
}

export async function testConnection() {
  const content = await chatCompletion(
    [{ role: 'user', content: 'Reply with JSON: {"ok":true}' }],
    { jsonMode: true, temperature: 0 }
  );
  const parsed = JSON.parse(content);
  return parsed.ok === true;
}

export async function testDiagramConnection() {
  const content = await diagramChatCompletion(
    [{ role: 'user', content: 'Reply with JSON: {"ok":true,"mode":"diagram"}' }],
    { jsonMode: true, temperature: 0 }
  );
  const parsed = JSON.parse(content);
  return parsed.ok === true;
}

export function parseJsonResponse(text) {
  let cleaned = text.trim();
  const fence = cleaned.match(/```(?:json)?\s*([\s\S]*?)```/);
  if (fence) cleaned = fence[1].trim();
  return JSON.parse(cleaned);
}

export function getDiagramModeLabel() {
  return `${DIAGRAM_MODEL} · Thinking High（仅图示题）`;
}

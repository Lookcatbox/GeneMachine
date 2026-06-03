/**
 * Question rendering and answer checking.
 */

import { renderDiagram } from './diagram-ui.js';
import {
  normalizeDiagram,
  assessDiagramQuality,
  applyCircuitTemplateFallback,
} from './diagram-normalize.js';

export function normalizeAnswer(s) {
  if (s == null) return '';
  return String(s).trim().toLowerCase().replace(/\s+/g, ' ');
}

export function checkAnswer(user, correct, type) {
  const u = normalizeAnswer(user);
  const c = normalizeAnswer(correct);

  if (type === 'true_false' || c === 'true' || c === 'false') {
    const uBool = u === 'true' || u === '是' || u === '对' || u === '正确';
    const uFalse = u === 'false' || u === '否' || u === '错' || u === '错误';
    if (c === 'true') return uBool;
    if (c === 'false') return uFalse;
  }

  if (/^[a-d]$/i.test(c) && u.length <= 2) {
    return u.replace(/\./g, '') === c.replace(/\./g, '');
  }

  if (u === c) return true;

  // fuzzy match for spelling/fill
  if (type === 'spelling' || type === 'fill' || type === 'fill_blank' || type === 'short') {
    return levenshtein(u, c) <= Math.max(1, Math.floor(c.length * 0.15));
  }

  return false;
}

function levenshtein(a, b) {
  const m = a.length, n = b.length;
  const dp = Array.from({ length: m + 1 }, () => new Array(n + 1).fill(0));
  for (let i = 0; i <= m; i++) dp[i][0] = i;
  for (let j = 0; j <= n; j++) dp[0][j] = j;
  for (let i = 1; i <= m; i++) {
    for (let j = 1; j <= n; j++) {
      dp[i][j] = a[i - 1] === b[j - 1]
        ? dp[i - 1][j - 1]
        : 1 + Math.min(dp[i - 1][j], dp[i][j - 1], dp[i - 1][j - 1]);
    }
  }
  return dp[m][n];
}

export function renderQuestion(container, question, onSubmit) {
  container.innerHTML = '';

  const card = document.createElement('div');
  card.className = 'question-card';

  const typeBadge = document.createElement('span');
  typeBadge.className = 'q-type';
  typeBadge.textContent = question.typeLabel || question.type;
  card.appendChild(typeBadge);

  if (question.knowledgeTag) {
    const tag = document.createElement('span');
    tag.className = 'q-tag';
    tag.textContent = question.knowledgeTag;
    card.appendChild(tag);
  }

  const prompt = document.createElement('p');
  prompt.className = 'q-prompt';
  prompt.textContent = question.prompt;
  card.appendChild(prompt);

  let diagramData = normalizeDiagram(question.diagram);
  const ctx = [question.prompt, ...(question.options || [])].join(' ');
  if (diagramData && !assessDiagramQuality(diagramData, ctx).ok) {
    const fixed = applyCircuitTemplateFallback(question);
    if (fixed.diagram && assessDiagramQuality(fixed.diagram, ctx).ok) {
      diagramData = fixed.diagram;
    }
  }
  const diagram = renderDiagram(diagramData);
  if (diagram) card.appendChild(diagram);
  else if (question.type === 'visual_diagram' || question.requiresDiagram) {
    const warn = document.createElement('p');
    warn.className = 'diagram-missing';
    warn.textContent = '⚠ 本题应有图示但 AI 未返回有效 diagram 数据';
    card.appendChild(warn);
  }

  const actions = document.createElement('div');
  actions.className = 'q-actions';

  const hasOptions = question.options?.length >= 2;
  let inputEl;

  if (hasOptions) {
    const opts = document.createElement('div');
    opts.className = 'q-options';
    for (const opt of question.options) {
      const btn = document.createElement('button');
      btn.className = 'btn option-btn';
      btn.textContent = opt;
      btn.dataset.value = opt.charAt(0).toUpperCase();
      btn.onclick = () => onSubmit(btn.dataset.value);
      opts.appendChild(btn);
    }
    actions.appendChild(opts);
  } else if (question.type === 'true_false') {
    const opts = document.createElement('div');
    opts.className = 'q-options';
    for (const [label, val] of [['正确', 'true'], ['错误', 'false']]) {
      const btn = document.createElement('button');
      btn.className = 'btn option-btn';
      btn.textContent = label;
      btn.onclick = () => onSubmit(val);
      opts.appendChild(btn);
    }
    actions.appendChild(opts);
  } else {
    inputEl = document.createElement('input');
    inputEl.className = 'q-input';
    inputEl.type = 'text';
    inputEl.placeholder = '输入你的答案…';
    actions.appendChild(inputEl);

    const submit = document.createElement('button');
    submit.className = 'btn primary';
    submit.textContent = '提交';
    submit.onclick = () => {
      const val = inputEl.value.trim();
      if (!val) return;
      onSubmit(val);
    };
    inputEl.addEventListener('keydown', e => {
      if (e.key === 'Enter') submit.click();
    });
    actions.appendChild(submit);
  }

  card.appendChild(actions);
  container.appendChild(card);
}

export function renderMarkdownReport(container, report) {
  container.innerHTML = '';
  container.classList.remove('hidden');

  const html = (report.report || '')
    .replace(/^### (.*)$/gm, '<h4>$1</h4>')
    .replace(/^## (.*)$/gm, '<h3>$1</h3>')
    .replace(/^# (.*)$/gm, '<h2>$1</h2>')
    .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
    .replace(/^- (.*)$/gm, '<li>$1</li>')
    .replace(/(<li>.*<\/li>\n?)+/g, m => `<ul>${m}</ul>`)
    .replace(/\n\n/g, '</p><p>');

  const div = document.createElement('div');
  div.className = 'report-body';
  div.innerHTML = `<p>${html}</p>`;

  if (report.highlights?.length) {
    const h = document.createElement('div');
    h.className = 'highlights';
    h.innerHTML = '<h4>要点</h4><ul>' +
      report.highlights.map(x => `<li>${x}</li>`).join('') + '</ul>';
    div.appendChild(h);
  }

  if (report.weakAreas?.length) {
    const w = document.createElement('div');
    w.className = 'weak-areas';
    w.innerHTML = '<h4>薄弱点</h4><ul>' +
      report.weakAreas.map(x => `<li>${x}</li>`).join('') + '</ul>';
    div.appendChild(w);
  }

  container.appendChild(div);

  const btn = document.createElement('button');
  btn.className = 'btn secondary';
  btn.textContent = '返回';
  btn.onclick = () => {
    container.classList.add('hidden');
    container.innerHTML = '';
  };
  container.appendChild(btn);
}

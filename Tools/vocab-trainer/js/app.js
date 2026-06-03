/**
 * Main application controller.
 */

import {
  loadWords, saveWords, loadSettings, saveSettings,
  parseImportText, mergeWords, loadWrongAnswers, saveWrongAnswers,
  loadReports,
} from './storage.js';
import {
  getDueWords, getNewWords, getMasteredWords, formatNextReview, ebbinghausLabel,
} from './srs.js';
import { testConnection, testDiagramConnection, getDiagramModeLabel } from './api.js';
import { StudySession } from './session.js';
import { AgentSession } from './agent.js';
import { renderMarkdownReport } from './question-ui.js';

// --- DOM helpers ---

function $(sel) { return document.querySelector(sel); }
function showToast(msg, ms = 3000) {
  const t = $('#toast');
  t.textContent = msg;
  t.classList.remove('hidden');
  setTimeout(() => t.classList.add('hidden'), ms);
}
function setLoading(on, text = 'AI 正在生成题目…') {
  const el = $('#loading-overlay');
  $('#loading-text').textContent = text;
  el.classList.toggle('hidden', !on);
}

// --- Tabs ---

document.querySelectorAll('.tab').forEach(tab => {
  tab.addEventListener('click', () => {
    document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
    document.querySelectorAll('.panel').forEach(p => p.classList.remove('active'));
    tab.classList.add('active');
    $(`#panel-${tab.dataset.tab}`).classList.add('active');
    refreshUI();
  });
});

// --- Words ---

function renderWordList() {
  const words = loadWords();
  $('#word-count').textContent = words.length;
  const list = $('#word-list');
  if (!words.length) {
    list.innerHTML = '<p class="empty">暂无单词，请导入</p>';
    return;
  }
  list.innerHTML = words.map(w => `
    <div class="word-item">
      <strong>${esc(w.word)}</strong>
      <span>${esc(w.meaning || '—')}</span>
      <small>${ebbinghausLabel(w.srs.repetitions)} · 下次 ${formatNextReview(w.srs.nextReview)}</small>
    </div>
  `).join('');
}

function esc(s) {
  const d = document.createElement('div');
  d.textContent = s;
  return d.innerHTML;
}

$('#btn-import').addEventListener('click', () => {
  const text = $('#import-text').value;
  if (!text.trim()) return showToast('请输入或粘贴单词');
  const incoming = parseImportText(text);
  const merged = mergeWords(loadWords(), incoming);
  saveWords(merged);
  $('#import-text').value = '';
  showToast(`已导入 ${incoming.length} 个单词`);
  refreshUI();
});

$('#import-file').addEventListener('change', async (e) => {
  const file = e.target.files?.[0];
  if (!file) return;
  const text = await file.text();
  $('#import-text').value = text;
  e.target.value = '';
  showToast('文件已加载，点击「导入」确认');
});

$('#btn-clear-words').addEventListener('click', () => {
  if (confirm('确定清空全部词库？')) {
    saveWords([]);
    refreshUI();
  }
});

// --- Review ---

function renderReviewPanel() {
  const words = loadWords();
  const due = getDueWords(words);
  const newW = getNewWords(words);
  const mastered = getMasteredWords(words);

  $('#stat-due').textContent = due.length;
  $('#stat-new').textContent = newW.length;
  $('#stat-mastered').textContent = mastered.length;
  $('#review-count').textContent = due.length;

  const sched = $('#review-schedule');
  const upcoming = [...words]
    .filter(w => !w.srs.mastered)
    .sort((a, b) => a.srs.nextReview - b.srs.nextReview)
    .slice(0, 15);

  sched.innerHTML = upcoming.length
    ? upcoming.map(w => `
      <div class="schedule-item ${isDueNow(w) ? 'due' : ''}">
        <span>${esc(w.word)}</span>
        <span>${formatNextReview(w.srs.nextReview)}</span>
      </div>`).join('')
    : '<p class="empty">暂无复习计划</p>';
}

function isDueNow(w) {
  return w.srs.nextReview <= Date.now();
}

$('#btn-start-review').addEventListener('click', () => {
  document.querySelector('[data-tab="study"]').click();
  startStudy('due');
});

// --- Study session ---

let studySession = null;

function startStudy(mode) {
  const words = loadWords();
  if (!words.length) return showToast('词库为空，请先导入单词');

  const batchSize = parseInt($('#study-batch-size').value, 10) || 5;
  let batch;

  if (mode === 'due') {
    batch = getDueWords(words).slice(0, batchSize);
    if (!batch.length) {
      batch = shuffle([...words]).slice(0, batchSize);
      showToast('暂无到期单词，随机抽取');
    }
  } else {
    batch = shuffle([...words]).slice(0, batchSize);
  }

  $('#study-idle').classList.add('hidden');
  $('#study-active').classList.remove('hidden');
  $('#study-report').classList.add('hidden');
  $('#question-area').innerHTML = '';

  setLoading(true, 'AI 正在用 v4-pro Max Thinking 生成题目与图示…');

  studySession = new StudySession({
    onProgress: (done, total) => {
      const pct = total ? (done / total) * 100 : 0;
      $('#study-progress').style.width = `${pct}%`;
    },
    onStatus: (msg) => { $('#study-status').textContent = msg; },
    onComplete: (report) => {
      setLoading(false);
      $('#study-active').classList.add('hidden');
      renderMarkdownReport($('#study-report'), report);
      showToast('本轮学习完成！');
      refreshUI();
    },
    onError: (err) => {
      setLoading(false);
      showToast(err.message || String(err));
      resetStudyUI();
    },
  });

  studySession.start(batch).finally(() => setLoading(false));
}

function resetStudyUI() {
  $('#study-idle').classList.remove('hidden');
  $('#study-active').classList.add('hidden');
}

$('#btn-start-study-due').addEventListener('click', () => startStudy('due'));
$('#btn-start-study-all').addEventListener('click', () => startStudy('random'));

// --- Agent session ---

let agentSession = null;

$('#btn-start-agent').addEventListener('click', () => {
  const topic = $('#agent-topic').value.trim();
  const count = parseInt($('#agent-count').value, 10) || 10;
  const difficulty = $('#agent-difficulty').value;

  if (!topic) return showToast('请输入学习主题');

  $('#agent-idle').classList.add('hidden');
  $('#agent-active').classList.remove('hidden');
  $('#agent-report').classList.add('hidden');
  $('#agent-question-area').innerHTML = '';
  setLoading(true, 'Agent 正在用 v4-pro Max Thinking 生成训练题…');

  agentSession = new AgentSession({
    onProgress: (done, total) => {
      const pct = total ? (done / total) * 100 : 0;
      $('#agent-progress').style.width = `${pct}%`;
    },
    onStatus: (msg) => { $('#agent-status').textContent = msg; },
    onComplete: (report) => {
      setLoading(false);
      $('#agent-active').classList.add('hidden');
      renderMarkdownReport($('#agent-report'), report);
      showToast('Agent 训练完成！');
      refreshUI();
    },
    onError: (err) => {
      setLoading(false);
      showToast(err.message || String(err));
      $('#agent-idle').classList.remove('hidden');
      $('#agent-active').classList.add('hidden');
    },
  });

  agentSession.start({ topic, count, difficulty }).finally(() => setLoading(false));
});

// --- History ---

function renderHistory() {
  const wrong = loadWrongAnswers();
  const reports = loadReports();

  $('#wrong-list').innerHTML = wrong.length
    ? wrong.slice(0, 50).map(w => `
      <div class="wrong-item">
        <div class="wrong-head">
          <strong>${esc(w.word || w.topic || '')}</strong>
          <span>${esc(w.type || '')}</span>
        </div>
        <p>${esc(w.prompt || '')}</p>
        <small>你的答案：${esc(String(w.userAnswer))} · 正确：${esc(String(w.correctAnswer))}</small>
      </div>`).join('')
    : '<p class="empty">暂无错题</p>';

  $('#report-list').innerHTML = reports.length
    ? reports.map(r => `
      <details class="report-item">
        <summary>${r.type === 'agent' ? '🤖 ' + esc(r.topic || '') : '📚 单词学习'} · ${new Date(r.timestamp).toLocaleString()} · 错题 ${r.wrongCount || 0}</summary>
        <div class="report-snippet">${esc((r.report || '').slice(0, 300))}…</div>
      </details>`).join('')
    : '<p class="empty">暂无报告</p>';
}

$('#btn-clear-wrong').addEventListener('click', () => {
  if (confirm('清空错题本？')) {
    saveWrongAnswers([]);
    renderHistory();
  }
});

// --- Settings ---

function loadSettingsUI() {
  const s = loadSettings();
  $('#api-key').value = s.apiKey;
  $('#api-base').value = s.apiBase;
  $('#api-model').value = s.apiModel;
  $('#proxy-url').value = s.proxyUrl;
  const label = $('#diagram-mode-label');
  if (label) label.textContent = getDiagramModeLabel();
}

$('#btn-save-settings').addEventListener('click', () => {
  saveSettings({
    apiKey: $('#api-key').value.trim(),
    apiBase: $('#api-base').value.trim(),
    apiModel: $('#api-model').value.trim(),
    proxyUrl: $('#proxy-url').value.trim(),
  });
  showToast('设置已保存');
});

$('#btn-test-api').addEventListener('click', async () => {
  $('#btn-save-settings').click();
  $('#api-test-result').textContent = '测试中…';
  try {
    await testConnection();
    $('#api-test-result').textContent = '✓ 普通模型连接成功';
    try {
      await testDiagramConnection();
      $('#api-test-result').textContent += ' · ✓ 图示模型 (v4-pro Max Thinking) 可用';
    } catch (e2) {
      $('#api-test-result').textContent += ` · 图示模型失败: ${e2.message}`;
    }
  } catch (e) {
    $('#api-test-result').textContent = `✗ ${e.message}`;
  }
});

// --- Utils ---

function shuffle(arr) {
  for (let i = arr.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    [arr[i], arr[j]] = [arr[j], arr[i]];
  }
  return arr;
}

function refreshUI() {
  renderWordList();
  renderReviewPanel();
  renderHistory();
}

loadSettingsUI();
refreshUI();

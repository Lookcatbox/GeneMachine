/**
 * Learning Agent session: topic-based adaptive training.
 */

import { chatCompletion, diagramChatCompletion, parseJsonResponse } from './api.js';
import {
  buildAgentQuestionsPrompt,
  buildAgentWeakPrompt,
  buildSummaryReportPrompt,
} from './prompts.js';
import { addWrongAnswers, saveReport } from './storage.js';
import { judgeAnswer, requiresAiJudgement } from './answer-judge.js';
import { renderQuestion } from './question-ui.js';
import { normalizeQuestion, countDiagramQuestions, fixQuestionDiagram, isDiagramUsable } from './diagram-normalize.js';

export class AgentSession {
  constructor({ onProgress, onStatus, onComplete, onError }) {
    this.onProgress = onProgress;
    this.onStatus = onStatus;
    this.onComplete = onComplete;
    this.onError = onError;

    this.topic = '';
    this.queue = [];
    this.current = null;
    this.tagStats = new Map();
    this.wrongLog = [];
    this.totalAnswered = 0;
    this.totalPlanned = 0;
    this.weakTags = [];
    this.extraRoundDone = false;
  }

  async start({ topic, count, difficulty }) {
    this.topic = topic;
    this.tagStats.clear();
    this.wrongLog = [];
    this.totalAnswered = 0;
    this.extraRoundDone = false;
    this.weakTags = [];

    this.onStatus(`Agent 正在为「${topic}」生成 ${count} 道题…`);

    try {
      const content = await chatCompletion([
        { role: 'system', content: '你是全能学习教练，只输出合法 JSON。' },
        { role: 'user', content: buildAgentQuestionsPrompt(topic, count, difficulty) },
      ]);
      const data = parseJsonResponse(content);

      this.queue = (data.questions || []).map(q => fixQuestionDiagram({
        ...q,
        source: 'initial',
      }));

      if (this.queue.length === 0) throw new Error('AI 未返回有效题目');

      const minDiagrams = Math.max(1, Math.ceil(count * 0.3));
      if (countDiagramQuestions(this.queue) < minDiagrams) {
        this.onStatus('图示题不足，正在用 v4-pro Thinking High 补生成…');
        await this.supplementDiagrams(topic, minDiagrams - countDiagramQuestions(this.queue));
      }

      this.totalPlanned = this.queue.length;
      this.onStatus(`共 ${this.queue.length} 道题，开始 Agent 训练`);
      this.showNext();
    } catch (err) {
      this.onError(err);
    }
  }

  async supplementDiagrams(topic, needCount) {
    try {
      const content = await diagramChatCompletion([
        { role: 'system', content: '你是全能学习教练，只输出合法 JSON。diagram 必须完整，电路用 template。' },
        {
          role: 'user',
          content: `主题为「${topic}」。请再生成 ${needCount} 道带 diagram 的训练题。电路题必须用 template（opamp_inverting_feedback 等），不要手写零散坐标。只输出 JSON：{"questions":[...]}`,
        },
      ]);
      const data = parseJsonResponse(content);
      for (const q of data.questions || []) {
        this.queue.push(fixQuestionDiagram({ ...q, source: 'diagram_supplement', requiresDiagram: true }));
      }
    } catch {
      // 补图失败不阻断
    }
  }

  showNext() {
    if (this.queue.length === 0) {
      this.checkWeakAndContinue();
      return;
    }

    this.current = this.queue.shift();
    this.onProgress(this.totalAnswered, this.totalPlanned);
    renderQuestion(document.getElementById('agent-question-area'), this.current, (answer) =>
      this.handleAnswer(answer)
    );
  }

  async handleAnswer(userAnswer) {
    const q = this.current;
    const area = document.getElementById('agent-question-area');
    area.querySelector('.q-actions')?.remove();

    let judgement;
    if (requiresAiJudgement(q)) {
      this.onStatus('AI 正在判分…');
    }

    try {
      judgement = await judgeAnswer(q, userAnswer);
    } catch (err) {
      this.onError(err);
      renderQuestion(area, q, (answer) => this.handleAnswer(answer));
      return;
    }

    const correct = judgement.correct;
    const tag = q.knowledgeTag || '综合';

    if (!this.tagStats.has(tag)) {
      this.tagStats.set(tag, { total: 0, correct: 0 });
    }
    const stats = this.tagStats.get(tag);
    stats.total += 1;
    if (correct) stats.correct += 1;
    else {
      this.wrongLog.push({
        sessionType: 'agent',
        topic: this.topic,
        knowledgeTag: tag,
        type: q.typeLabel || q.type,
        prompt: q.prompt,
        userAnswer,
        correctAnswer: q.answer,
        explanation: judgement.feedback || q.explanation || '',
        timestamp: Date.now(),
      });
    }

    this.totalAnswered += 1;
    this.onProgress(this.totalAnswered, this.totalPlanned);

    const feedback = document.createElement('div');
    feedback.className = `feedback ${correct ? 'correct' : 'wrong'}`;
    const explanation = judgement.feedback || q.explanation || '';
    feedback.innerHTML = correct
      ? `✓ 正确${judgement.judgedByAi ? '（AI 判分）' : ''}${explanation ? `<p>${explanation}</p>` : ''}`
      : `✗ 正确答案：<strong>${q.answer}</strong>${explanation ? `<p>${explanation}</p>` : ''}`;

    area.appendChild(feedback);

    const btn = document.createElement('button');
    btn.className = 'btn primary';
    btn.textContent = '下一题';
    btn.onclick = () => this.showNext();
    area.appendChild(btn);
  }

  async checkWeakAndContinue() {
    this.weakTags = [];
    for (const [tag, stats] of this.tagStats) {
      if (stats.total >= 1 && stats.correct / stats.total < 0.7) {
        this.weakTags.push(tag);
      }
    }

    if (this.weakTags.length > 0 && !this.extraRoundDone) {
      this.extraRoundDone = true;
      this.onStatus(`薄弱知识点：${this.weakTags.join('、')}，正在加练…`);
      await this.generateWeakExtras();
    } else {
      await this.finalize();
    }
  }

  async generateWeakExtras() {
    try {
      const content = await chatCompletion([
        { role: 'system', content: '你是全能学习教练，只输出合法 JSON。' },
        { role: 'user', content: buildAgentWeakPrompt(this.topic, this.weakTags) },
      ]);
      const data = parseJsonResponse(content);

      for (const q of data.questions || []) {
        this.queue.push({ ...q, source: 'weak_retry' });
      }

      this.totalPlanned = this.totalAnswered + this.queue.length;
      this.onStatus(`已追加 ${data.questions?.length || 0} 道强化题`);
      this.showNext();
    } catch (err) {
      this.onError(err);
      await this.finalize();
    }
  }

  async finalize() {
    this.onStatus('Agent 正在撰写总结报告…');

    const results = [];
    for (const [tag, stats] of this.tagStats) {
      results.push({
        label: tag,
        total: stats.total,
        correct: stats.correct,
        weakTypes: stats.correct / stats.total < 0.7 ? [tag] : [],
      });
    }

    if (this.wrongLog.length) addWrongAnswers(this.wrongLog);

    let reportData = { report: '', highlights: [], weakAreas: this.weakTags };
    try {
      const content = await chatCompletion([
        { role: 'system', content: '你是学习报告撰写助手，只输出合法 JSON。' },
        {
          role: 'user',
          content: buildSummaryReportPrompt(`Agent 训练: ${this.topic}`, results, this.wrongLog),
        },
      ], { temperature: 0.5 });
      reportData = parseJsonResponse(content);
    } catch {
      reportData.report = this.buildFallbackReport(results);
    }

    const report = {
      id: crypto.randomUUID(),
      type: 'agent',
      topic: this.topic,
      timestamp: Date.now(),
      results,
      wrongCount: this.wrongLog.length,
      ...reportData,
    };
    saveReport(report);
    this.onComplete(report);
  }

  buildFallbackReport(results) {
    const lines = [`## Agent 训练总结：${this.topic}\n`];
    for (const r of results) {
      const pct = r.total ? Math.round((r.correct / r.total) * 100) : 0;
      lines.push(`- **${r.label}**: ${r.correct}/${r.total} (${pct}%)`);
    }
    return lines.join('\n');
  }
}

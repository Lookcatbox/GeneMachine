/**
 * Study session: word drills with weak-word retry.
 */

import { chatCompletion, diagramChatCompletion, parseJsonResponse } from './api.js';
import {
  buildWordQuestionsPrompt,
  buildWeakWordExtraPrompt,
  buildSummaryReportPrompt,
  buildSupplementDiagramPrompt,
} from './prompts.js';
import { updateSrs, accuracyToQuality } from './srs.js';
import { saveWords, loadWords, addWrongAnswers, saveReport } from './storage.js';
import { judgeAnswer, requiresAiJudgement } from './answer-judge.js';
import { renderQuestion } from './question-ui.js';
import { normalizeQuestion, normalizeDiagram, fixQuestionDiagram } from './diagram-normalize.js';

export class StudySession {
  constructor({ onProgress, onStatus, onComplete, onError }) {
    this.onProgress = onProgress;
    this.onStatus = onStatus;
    this.onComplete = onComplete;
    this.onError = onError;

    this.words = [];
    this.queue = [];
    this.current = null;
    this.wordStats = new Map();
    this.wrongLog = [];
    this.totalAnswered = 0;
    this.totalPlanned = 0;
    this.phase = 'idle';
    this.weakWords = [];
  }

  async start(words) {
    this.words = words;
    this.wordStats.clear();
    this.wrongLog = [];
    this.weakWords = [];
    this.totalAnswered = 0;

    for (const w of words) {
      this.wordStats.set(w.id, {
        word: w,
        total: 0,
        correct: 0,
        wrongTypes: new Set(),
        questions: [],
      });
    }

    this.onStatus('正在向 AI 请求全新题目…');
    this.phase = 'generating';

    try {
      const content = await chatCompletion([
        { role: 'system', content: '你是英语词汇教练，只输出合法 JSON。' },
        { role: 'user', content: buildWordQuestionsPrompt(words) },
      ]);
      const data = parseJsonResponse(content);

      this.queue = [];
      for (const set of data.wordSets || []) {
        const wordObj = words.find(w => w.word.toLowerCase() === set.word?.toLowerCase()) || words[0];
        for (const q of set.questions || []) {
          this.queue.push(fixQuestionDiagram({
            ...q,
            wordId: wordObj?.id,
            wordLabel: set.word,
            source: 'initial',
          }));
        }
      }

      if (this.queue.length === 0) throw new Error('AI 未返回有效题目');

      await this.ensureDiagramQuestions(words);

      this.totalPlanned = this.queue.length;
      this.phase = 'answering';
      this.onStatus(`共 ${this.queue.length} 道题，开始答题`);
      this.showNext();
    } catch (err) {
      this.onError(err);
    }
  }

  async ensureDiagramQuestions(words) {
    const wordsMissing = words.filter(w => {
      const forWord = this.queue.filter(q => q.wordId === w.id);
      return !forWord.some(q => normalizeDiagram(q.diagram));
    });
    if (!wordsMissing.length) return;

    this.onStatus(`补生成 ${wordsMissing.length} 个单词的图示题（v4-pro Thinking High）…`);
    try {
      const content = await diagramChatCompletion([
        { role: 'system', content: '你是英语词汇教练，只输出合法 JSON。diagram 必须非空且布局完整。' },
        { role: 'user', content: buildSupplementDiagramPrompt(wordsMissing) },
      ]);
      const data = parseJsonResponse(content);
      for (const q of data.questions || []) {
        const wordObj = words.find(w => w.word.toLowerCase() === q.word?.toLowerCase());
        if (!wordObj) continue;
        this.queue.push(fixQuestionDiagram({
          ...q,
          wordId: wordObj.id,
          wordLabel: wordObj.word,
          source: 'diagram_supplement',
          requiresDiagram: true,
        }));
      }
    } catch {
      // 补图失败不阻断学习，question-ui 会显示警告
    }
  }

  showNext() {
    if (this.queue.length === 0) {
      this.finishInitialRound();
      return;
    }

    this.current = this.queue.shift();
    this.onProgress(this.totalAnswered, this.totalPlanned);
    renderQuestion(document.getElementById('question-area'), this.current, (answer) =>
      this.handleAnswer(answer)
    );
  }

  async handleAnswer(userAnswer) {
    const q = this.current;
    const area = document.getElementById('question-area');
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
    const stats = this.wordStats.get(q.wordId);

    if (stats) {
      stats.total += 1;
      if (correct) stats.correct += 1;
      else {
        stats.wrongTypes.add(q.typeLabel || q.type);
        this.wrongLog.push({
          sessionType: 'word_study',
          word: q.wordLabel,
          type: q.typeLabel || q.type,
          prompt: q.prompt,
          userAnswer,
          correctAnswer: q.answer,
          explanation: judgement.feedback || q.explanation || '',
          timestamp: Date.now(),
        });
      }
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

  async finishInitialRound() {
    this.phase = 'weak-check';
    this.weakWords = [];

    for (const [, stats] of this.wordStats) {
      if (stats.total === 0) continue;
      const acc = stats.correct / stats.total;
      if (acc < 0.7 || stats.wrongTypes.size >= 2) {
        this.weakWords.push(stats);
      }
    }

    if (this.weakWords.length > 0) {
      this.onStatus(`检测到 ${this.weakWords.length} 个掌握不佳的单词，正在加练…`);
      await this.generateWeakExtras();
    } else {
      await this.finalize();
    }
  }

  async generateWeakExtras() {
    try {
      for (const stats of this.weakWords) {
        const content = await chatCompletion([
          { role: 'system', content: '你是英语词汇教练，只输出合法 JSON。' },
          {
            role: 'user',
            content: buildWeakWordExtraPrompt(stats.word, [...stats.wrongTypes]),
          },
        ]);
        const data = parseJsonResponse(content);
        for (const q of data.questions || []) {
          this.queue.push({
            ...q,
            wordId: stats.word.id,
            wordLabel: stats.word.word,
            source: 'weak_retry',
          });
        }
      }
      this.totalPlanned = this.totalAnswered + this.queue.length;
      this.phase = 'answering';
      this.onStatus('加练题目已生成，继续答题');
      this.showNext();
    } catch (err) {
      this.onError(err);
      await this.finalize();
    }
  }

  async finalize() {
    this.phase = 'report';
    this.onStatus('正在生成总结报告…');

    const words = loadWords();
    const results = [];

    for (const [, stats] of this.wordStats) {
      const acc = stats.total ? stats.correct / stats.total : 0;
      results.push({
        label: stats.word.word,
        total: stats.total,
        correct: stats.correct,
        weakTypes: [...stats.wrongTypes],
      });

      const idx = words.findIndex(w => w.id === stats.word.id);
      if (idx >= 0) {
        words[idx].srs = updateSrs(words[idx].srs, accuracyToQuality(acc));
        if (acc < 0.7) words[idx].srs.mastered = false;
      }
    }
    saveWords(words);

    if (this.wrongLog.length) addWrongAnswers(this.wrongLog);

    let reportData = { report: '', highlights: [], weakAreas: [] };
    try {
      const content = await chatCompletion([
        { role: 'system', content: '你是学习报告撰写助手，只输出合法 JSON。' },
        {
          role: 'user',
          content: buildSummaryReportPrompt('单词学习', results, this.wrongLog),
        },
      ], { temperature: 0.5 });
      reportData = parseJsonResponse(content);
    } catch {
      reportData.report = this.buildFallbackReport(results);
    }

    const report = {
      id: crypto.randomUUID(),
      type: 'word_study',
      timestamp: Date.now(),
      results,
      wrongCount: this.wrongLog.length,
      ...reportData,
    };
    saveReport(report);
    this.onComplete(report);
  }

  buildFallbackReport(results) {
    const lines = ['## 学习总结\n'];
    for (const r of results) {
      const pct = r.total ? Math.round((r.correct / r.total) * 100) : 0;
      lines.push(`- **${r.label}**: ${r.correct}/${r.total} (${pct}%)`);
    }
    return lines.join('\n');
  }
}

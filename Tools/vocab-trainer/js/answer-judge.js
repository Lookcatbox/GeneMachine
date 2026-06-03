/**
 * AI-assisted answer judgement for open-ended questions.
 */

import { chatCompletion, parseJsonResponse } from './api.js';
import { checkAnswer } from './question-ui.js';

const AI_JUDGE_TYPES = new Set([
  'translation',
  'context',
  'usage',
  'short',
  'application',
  'fill',
  'fill_blank',
]);

export function requiresAiJudgement(question) {
  if (!question) return false;
  if (AI_JUDGE_TYPES.has(question.type)) return true;
  if (question.options?.length >= 2) return false;
  if (question.type === 'true_false') return false;
  // Open text without strict single-letter answer
  return !/^[a-d]$/i.test(String(question.answer || '').trim());
}

export async function judgeAnswer(question, userAnswer) {
  const type = question.type;
  const reference = question.answer;

  if (!requiresAiJudgement(question)) {
    const correct = checkAnswer(userAnswer, reference, type);
    return {
      correct,
      feedback: correct ? (question.explanation || '') : (question.explanation || ''),
    };
  }

  const content = await chatCompletion([
    {
      role: 'system',
      content: '你是阅卷助手。判断学生答案是否在语义上正确，不要求逐字一致。只输出 JSON。',
    },
    {
      role: 'user',
      content: buildJudgePrompt(question, userAnswer),
    },
  ], { jsonMode: true, temperature: 0.2 });

  const data = parseJsonResponse(content);
  return {
    correct: Boolean(data.correct),
    feedback: data.feedback || data.explanation || question.explanation || '',
  };
}

function buildJudgePrompt(question, userAnswer) {
  return `请判断学生答案是否正确。

题型：${question.typeLabel || question.type}
题目：${question.prompt}
参考答案：${question.answer}
学生答案：${userAnswer}
题目解析：${question.explanation || '无'}
题目图示 JSON：${question.diagram ? JSON.stringify(question.diagram) : '无'}

输出 JSON：
{
  "correct": true,
  "feedback": "简短反馈，说明为何对/错"
}`;
}

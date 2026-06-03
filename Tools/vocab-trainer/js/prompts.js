/**
 * AI prompt templates for question generation.
 */

export const QUESTION_TYPES = [
  { id: 'meaning_choice', label: '释义选择' },
  { id: 'visual_diagram', label: '图示联想' },
  { id: 'spelling', label: '拼写填空' },
  { id: 'fill_blank', label: '句子填空' },
  { id: 'synonym_antonym', label: '近义/反义' },
  { id: 'usage', label: '用法辨析' },
  { id: 'translation', label: '翻译' },
  { id: 'context', label: '语境理解' },
  { id: 'root_affix', label: '词根词缀' },
];

const TYPE_IDS = QUESTION_TYPES.map(t => t.id).join(', ');

const DIAGRAM_TOOL_INSTRUCTIONS = `【重要】带图题目必须使用 diagram 字段，由前端渲染为 SVG（不是图片链接）。

## 电路/电子题 — 必须用 template（禁止手写零散坐标）
涉及运放、反馈、串联、并联时，diagram 必须使用 template，不要自己拼 elements：

可选 template：
- opamp_inverting_feedback — 反相放大器 + Rf 反馈（最常见）
- opamp_non_inverting_feedback — 同相放大器 + 反馈
- series_circuit — 串联电路
- parallel_circuit — 并联电路

示例（反馈类型判断题）：
"diagram": {
  "template": "opamp_inverting_feedback",
  "width": 480,
  "height": 280,
  "params": { "R1": "R1", "Rf": "Rf", "Vi": "Vi", "Vo": "Vo", "title": "反相放大器反馈电路" }
}

## 词汇助记/几何题 — 使用 elements（必须连通）
- 先画 wire/line 连接各部件，再画组件，最后 label
- wire 数量必须 ≥ 组件数量
- 每个 resistor 的 (x1,y1)-(x2,y2) 必须落在 wire 端点上

elements 支持 type：
- 连线：wire、line、path（points 数组）
- 电路组件：resistor、battery、switch、lamp、opamp、ground、cap
- 通用：rect、circle、icon（emoji）、label、arrow、node

词汇示例 elements 至少 4 个且布局紧凑，不要散落标签。`;

export function buildWordQuestionsPrompt(words) {
  const wordList = words.map(w =>
    w.meaning ? `"${w.word}" (${w.meaning})` : `"${w.word}"`
  ).join('\n');

  return `你是专业的英语词汇教练。为以下单词各生成一套全新的练习题（每次内容必须不同）。

单词：
${wordList}

要求：
1. 每个单词必须包含至少 6 种不同题型，从以下类型中选择：${TYPE_IDS}
2. **每个单词至少 1 道图示题（type 必须为 "visual_diagram"），且 diagram 不能为 null，elements 至少 4 个元素**
3. 图示题用 rect/icon/circle/label/arrow 等组合助记场景（如：abundant 画多个苹果，ephemeral 画转瞬即逝的泡泡）
4. 题型需多样化，不要重复同一题型超过 2 次
5. 选择题必须有 4 个选项，选项用 A/B/C/D 表示，answer 字段为正确选项字母
6. 填空题 answer 为完整正确答案（字符串）
7. 拼写题 prompt 中用 _____ 表示空格
8. 所有题目使用中文出题说明，英文内容保持英文
9. 翻译题 type 必须为 "translation"，语境/用法这类开放题分别使用 "context" / "usage"，这些题会由 AI 判分
10. ${DIAGRAM_TOOL_INSTRUCTIONS}
11. 必须 JSON 格式输出，结构如下：

{
  "wordSets": [
    {
      "word": "单词原形",
      "questions": [
        {
          "type": "visual_diagram",
          "typeLabel": "图示联想",
          "prompt": "观察下图助记场景，选择对应的单词",
          "options": ["A. ...", "B. ...", "C. ...", "D. ..."],
          "diagram": { "title": "...", "width": 420, "height": 240, "elements": [] },
          "answer": "A",
          "explanation": "简短解析"
        }
      ]
    }
  ]
}

只输出 JSON，不要 markdown 代码块。`;
}

export function buildWeakWordExtraPrompt(word, wrongTypes) {
  return `单词 "${word.word}"${word.meaning ? ` (${word.meaning})` : ''} 在以下题型中掌握不佳：${wrongTypes.join('、')}。

请再生成 4 道针对薄弱点的练习题（题型必须与之前不同或变式加深），其中至少 1 道必须带 diagram 图示。
${DIAGRAM_TOOL_INSTRUCTIONS}

{
  "questions": [
    {
      "type": "...",
      "typeLabel": "...",
      "prompt": "...",
      "options": ["A. ...", "B. ...", "C. ...", "D. ..."],
      "diagram": null,
      "answer": "...",
      "explanation": "..."
    }
  ]
}

只输出 JSON。`;
}

export function buildAgentQuestionsPrompt(topic, count, difficulty) {
  const diffMap = { easy: '基础入门', medium: '中等', hard: '进阶挑战' };
  return `你是全能学习教练。用户想学习：「${topic}」
难度：${diffMap[difficulty] || difficulty}
请生成 ${count} 道多样化的训练题（至少 4 种题型：选择、填空、简答、判断、匹配、应用题等）。
**至少 ${Math.max(1, Math.ceil(count * 0.3))} 道题必须带非空 diagram 图示**（电路/几何/流程/对比图/助记场景等，依主题而定）。
开放答案题请使用 type="short" 或 type="application"，翻译题请使用 type="translation"，这些题会由 AI 判分，不要求学生答案逐字一致。
${DIAGRAM_TOOL_INSTRUCTIONS}

每道题需标注 knowledgeTag（所属知识点标签，用于分析薄弱点）。

JSON 格式：
{
  "questions": [
    {
      "type": "choice|fill|short|true_false|application|translation",
      "typeLabel": "题型中文名",
      "knowledgeTag": "知识点标签",
      "prompt": "题目",
      "options": ["A. ...", "B. ...", "C. ...", "D. ..."],
      "diagram": null,
      "answer": "正确答案（选择题为字母，判断题为 true/false，简答/填空为字符串）",
      "explanation": "解析"
    }
  ]
}

只输出 JSON，不要 markdown。`;
}

export function buildAgentWeakPrompt(topic, weakTags) {
  return `用户在「${topic}」训练中，以下知识点掌握不佳：${weakTags.join('、')}。

请针对这些薄弱点再生成 3-5 道强化练习题，至少 1 道带 diagram 图示。
${DIAGRAM_TOOL_INSTRUCTIONS}

{
  "questions": [
    {
      "type": "...",
      "typeLabel": "...",
      "knowledgeTag": "...",
      "prompt": "...",
      "options": [...],
      "diagram": null,
      "answer": "...",
      "explanation": "..."
    }
  ]
}

只输出 JSON。`;
}

export function buildRegenerateDiagramPrompt(question) {
  return `以下题目的 diagram 图示不完整/不可用，请重新生成 diagram。

题目：${question.prompt}
选项：${JSON.stringify(question.options || [])}

规则：
- 若是电路/运放/反馈题，必须使用 template（opamp_inverting_feedback / opamp_non_inverting_feedback / series_circuit / parallel_circuit）
- 禁止只输出零散 label 而无连线的 diagram

只输出 JSON：
{ "diagram": { "template": "...", "width": 480, "height": 280, "params": {...} } }
或
{ "diagram": { "title": "...", "width": 420, "height": 240, "elements": [先wire后组件，至少8个元素] } }`;
}

export function buildSupplementDiagramPrompt(words) {
  const wordList = words.map(w =>
    w.meaning ? `"${w.word}" (${w.meaning})` : `"${w.word}"`
  ).join('\n');

  return `以下单词的题目缺少图示，请仅为每个单词各生成 1 道 visual_diagram 图示联想题。
${DIAGRAM_TOOL_INSTRUCTIONS}

单词：
${wordList}

JSON 输出：
{
  "questions": [
    {
      "word": "单词原形",
      "type": "visual_diagram",
      "typeLabel": "图示联想",
      "prompt": "观察下图，选择对应单词/释义",
      "options": ["A. ...", "B. ...", "C. ...", "D. ..."],
      "diagram": { "title": "...", "width": 420, "height": 240, "elements": [至少4个元素] },
      "answer": "A",
      "explanation": "..."
    }
  ]
}

只输出 JSON。`;
}

export function buildSummaryReportPrompt(sessionType, results, wrongItems) {
  const summary = results.map(r => ({
    wordOrTag: r.label,
    total: r.total,
    correct: r.correct,
    accuracy: r.total ? (r.correct / r.total).toFixed(2) : '0',
    weakTypes: r.weakTypes || [],
  }));

  return `请根据以下学习数据生成一份中文总结报告（Markdown 格式，但放在 JSON 的 report 字段中）。

会话类型：${sessionType}
统计数据：${JSON.stringify(summary, null, 2)}
错题详情：${JSON.stringify(wrongItems.slice(0, 20), null, 2)}

JSON 输出：
{
  "report": "Markdown 格式的总结报告，包含：总体表现、优势、薄弱点、学习建议、下一步计划",
  "highlights": ["要点1", "要点2"],
  "weakAreas": ["薄弱点1", "薄弱点2"]
}

只输出 JSON。`;
}

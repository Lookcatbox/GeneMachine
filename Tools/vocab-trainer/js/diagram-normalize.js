/**
 * Normalize AI-returned diagram payloads before rendering.
 */

import { resolveDiagram, TEMPLATE_IDS } from './diagram-templates.js';

const WIRE_TYPES = new Set(['wire', 'line', 'path', 'polyline']);
const COMPONENT_TYPES = new Set([
  'resistor', 'battery', 'switch', 'lamp', 'bulb', 'ammeter', 'voltmeter',
  'meter', 'opamp', 'op-amp', 'amplifier', 'cap', 'capacitor', 'inductor', 'ground',
]);
const LABEL_TYPES = new Set(['label', 'text']);

export function normalizeDiagram(raw) {
  if (raw == null || raw === 'null' || raw === '') return null;

  let diagram = raw;
  if (typeof raw === 'string') {
    try {
      diagram = JSON.parse(raw);
    } catch {
      return null;
    }
  }

  if (!diagram || typeof diagram !== 'object') return null;

  // Template-based diagrams expand to full element lists
  diagram = resolveDiagram(diagram);

  const elements = diagram.elements ?? diagram.items ?? diagram.shapes;
  if (!Array.isArray(elements) || elements.length === 0) return null;

  return {
    title: diagram.title || diagram.caption || '',
    width: diagram.width,
    height: diagram.height,
    elements: sortElementsByLayer(elements),
    template: diagram.template || diagram.templateId || null,
  };
}

/** Layer: wires → nodes → components → labels */
function sortElementsByLayer(elements) {
  const layer = (el) => {
    const t = String(el?.type || '').toLowerCase();
    if (WIRE_TYPES.has(t)) return 0;
    if (t === 'node') return 1;
    if (COMPONENT_TYPES.has(t)) return 2;
    if (LABEL_TYPES.has(t) || t === 'icon' || t === 'emoji') return 4;
    if (t === 'arrow') return 3;
    return 2;
  };
  return [...elements].sort((a, b) => layer(a) - layer(b));
}

export function assessDiagramQuality(diagram, questionContext = '') {
  if (!diagram?.elements?.length) {
    return { ok: false, score: 0, reason: 'empty' };
  }

  if (diagram.template) {
    return { ok: true, score: 100, reason: 'template' };
  }

  const types = diagram.elements.map(e => String(e.type || '').toLowerCase());
  const wires = types.filter(t => WIRE_TYPES.has(t)).length;
  const components = types.filter(t => COMPONENT_TYPES.has(t)).length;
  const labels = types.filter(t => LABEL_TYPES.has(t)).length;
  const total = diagram.elements.length;
  const ctx = String(questionContext);
  const needsOpamp = /反馈|Rf|R1|运放|放大器|opamp|feedback|电路/i.test(ctx);
  const hasOpampEl = types.some(t => ['opamp', 'op-amp', 'amplifier'].includes(t));

  if (needsOpamp && !hasOpampEl) {
    return { ok: false, score: 8, reason: 'missing_opamp' };
  }

  const hasCircuit = components > 0 || needsOpamp;
  if (hasCircuit) {
    if (wires < components + 1) {
      return { ok: false, score: 20, reason: 'circuit_missing_wires', wires, components };
    }
    if (components >= 1 && wires <= 3 && labels >= 2 && !hasOpampEl) {
      return { ok: false, score: 15, reason: 'incomplete_feedback_circuit', wires, components };
    }
  }

  if (total < 4) {
    return { ok: false, score: 25, reason: 'too_few_elements', total };
  }

  if (labels > 0 && wires === 0 && components === 0) {
    return { ok: false, score: 5, reason: 'labels_only' };
  }

  return { ok: true, score: Math.min(100, 40 + wires * 8 + components * 10), reason: 'ok' };
}

export function isDiagramUsable(diagram, questionContext = '') {
  return assessDiagramQuality(diagram, questionContext).ok;
}

export function normalizeQuestion(raw) {
  if (!raw || typeof raw !== 'object') return raw;
  const question = { ...raw };
  question.diagram = normalizeDiagram(question.diagram);
  if (question.diagram) {
    const ctx = [question.prompt, ...(question.options || [])].join(' ');
    question.diagramQuality = assessDiagramQuality(question.diagram, ctx);
  }
  return question;
}

export function countDiagramQuestions(questions) {
  return (questions || []).filter(q => {
    const ctx = [q.prompt, ...(q.options || [])].join(' ');
    return isDiagramUsable(normalizeDiagram(q?.diagram), ctx);
  }).length;
}

export function inferCircuitTemplate(question) {
  const text = [
    question.prompt,
    question.explanation,
    ...(question.options || []),
  ].join(' ');
  const hasOpamp = /运放|放大器|op\s*amp|operational/i.test(text);
  const hasFeedback = /反馈|feedback|rf/i.test(text);
  const hasInverting = /反相|inverting/i.test(text);
  const hasNonInverting = /同相|non.?inverting/i.test(text);
  const hasSeries = /串联|series/i.test(text);
  const hasParallel = /并联|parallel/i.test(text);

  if (hasOpamp && hasFeedback) {
    if (hasNonInverting) return 'opamp_non_inverting_feedback';
    return 'opamp_inverting_feedback';
  }
  if (hasSeries) return 'series_circuit';
  if (hasParallel) return 'parallel_circuit';
  if (hasOpamp) return 'opamp_inverting_feedback';
  return null;
}

export function applyCircuitTemplateFallback(question) {
  const templateId = inferCircuitTemplate(question);
  if (!templateId) return question;

  const params = {};
  const prompt = question.prompt || '';
  const rf = prompt.match(/Rf\s*[=为]?\s*[\d.]+\s*\w*/i) || prompt.match(/\bRf\b/);
  const r1 = prompt.match(/R1\s*[=为]?\s*[\d.]+/i) || prompt.match(/\bR1\b/);
  if (rf) params.Rf = 'Rf';
  if (r1) params.R1 = 'R1';
  params.Vi = 'Vi';
  params.Vo = 'Vo';

  return normalizeQuestion({
    ...question,
    diagram: {
      template: templateId,
      width: 480,
      height: 280,
      params,
    },
  });
}

export function fixQuestionDiagram(question) {
  let q = normalizeQuestion(question);
  const ctx = [q.prompt, ...(q.options || [])].join(' ');
  if (!q.diagram) {
    if (/反馈|电路|Rf|运放|串联|并联/i.test(ctx)) {
      return applyCircuitTemplateFallback(q);
    }
    return q;
  }
  if (isDiagramUsable(q.diagram, ctx)) return q;
  const fallback = applyCircuitTemplateFallback(q);
  if (fallback.diagram && isDiagramUsable(fallback.diagram, ctx)) return fallback;
  return q;
}

export { TEMPLATE_IDS };

/**
 * Pre-built diagram templates — reliable rendering for circuits / geometry.
 * AI should prefer template + params over hand-placed coordinates.
 */

const TEMPLATES = {
  opamp_inverting_feedback: renderOpampInvertingFeedback,
  opamp_non_inverting_feedback: renderOpampNonInvertingFeedback,
  series_circuit: renderSeriesCircuit,
  parallel_circuit: renderParallelCircuit,
};

export const TEMPLATE_IDS = Object.keys(TEMPLATES);

export function resolveDiagram(diagram) {
  if (!diagram || typeof diagram !== 'object') return null;
  const templateId = diagram.template || diagram.templateId;
  if (templateId && TEMPLATES[templateId]) {
    const width = clamp(diagram.width, 120, 900, 480);
    const height = clamp(diagram.height, 80, 600, 280);
    const params = diagram.params || diagram.labels || {};
    return TEMPLATES[templateId](width, height, params);
  }
  return diagram;
}

function renderOpampInvertingFeedback(w, h, p) {
  const y = Math.round(h * 0.42);
  const yRf = y - 70;
  const viX = 45;
  const r1x1 = 75;
  const r1x2 = 165;
  const nodeX = 178;
  const opX = Math.round(w * 0.52);
  const opY = y + 8;
  const outX = opX + 55;
  const voX = w - 42;

  const R1 = p.R1 || 'R1';
  const Rf = p.Rf || 'Rf';
  const Vi = p.Vi || p.vi || 'Vi';
  const Vo = p.Vo || p.vo || 'Vo';

  return {
    title: p.title || '反相放大器（含反馈电阻 Rf）',
    width: w,
    height: h,
    elements: [
      { type: 'label', x: w / 2, y: 22, text: p.title || '反相放大器反馈电路' },
      // Signal path wires
      { type: 'wire', x1: viX, y1: y, x2: r1x1, y2: y },
      { type: 'wire', x1: r1x2, y1: y, x2: nodeX, y2: y },
      { type: 'wire', x1: nodeX, y1: y, x2: opX - 38, y2: opY - 12 },
      { type: 'wire', x1: outX, y1: opY, x2: voX, y2: y },
      // Rf feedback loop
      { type: 'wire', x1: outX, y1: opY, x2: outX, y2: yRf },
      { type: 'wire', x1: outX, y1: yRf, x2: nodeX, y2: yRf },
      { type: 'wire', x1: nodeX, y1: yRf, x2: nodeX, y2: y },
      // Non-inverting input to ground
      { type: 'wire', x1: opX - 38, y1: opY + 18, x2: opX - 38, y2: h - 48 },
      { type: 'ground', x: opX - 38, y: h - 48 },
      // Components
      { type: 'resistor', x1: r1x1, y1: y, x2: r1x2, y2: y, label: R1 },
      { type: 'resistor', x1: nodeX + 18, y1: yRf, x2: outX - 18, y2: yRf, label: Rf },
      { type: 'opamp', x: opX, y: opY, label: 'A' },
      { type: 'node', x: nodeX, y },
      { type: 'node', x: outX, y: opY },
      { type: 'label', x: viX - 4, y: y - 16, text: Vi },
      { type: 'label', x: voX, y: y - 16, text: Vo },
      { type: 'arrow', x1: viX - 22, y1: y, x2: viX - 4, y2: y },
    ],
  };
}

function renderOpampNonInvertingFeedback(w, h, p) {
  const cx = w * 0.48;
  const cy = h * 0.45;
  const inX = cx - 55;
  const outX = cx + 70;
  const plusY = cy + 28;
  const viX = 55;
  const Rf = p.Rf || 'Rf';
  const R1 = p.R1 || 'R1';
  const Vi = p.Vi || p.vi || 'Vi';
  const Vo = p.Vo || p.vo || 'Vo';

  return {
    title: p.title || '同相放大器（含反馈）',
    width: w,
    height: h,
    elements: [
      { type: 'label', x: w / 2, y: 22, text: p.title || '同相放大器反馈电路' },
      { type: 'wire', x1: viX, y1: plusY, x2: inX, y2: plusY },
      { type: 'wire', x1: outX, y1: cy, x2: w - 45, y2: cy },
      { type: 'wire', x1: outX, y1: cy, x2: outX, y2: cy - 60 },
      { type: 'wire', x1: outX, y1: cy - 60, x2: inX, y2: cy - 60 },
      { type: 'wire', x1: inX, y1: cy - 60, x2: inX, y2: cy - 18 },
      { type: 'wire', x1: cx - 8, y1: cy + 55, x2: cx - 8, y2: h - 35 },
      { type: 'wire', x1: cx - 8, y1: cy + 55, x2: inX + 80, y2: cy + 55 },
      { type: 'wire', x1: inX + 80, y1: cy + 55, x2: inX + 80, y2: h - 35 },
      { type: 'ground', x: cx - 8, y: h - 35 },
      { type: 'ground', x: inX + 80, y: h - 35 },
      { type: 'resistor', x1: inX + 15, y1: cy - 60, x2: outX - 15, y2: cy - 60, label: Rf },
      { type: 'resistor', x1: inX + 55, y1: cy + 55, x2: inX + 80, y2: cy + 55, label: R1 },
      { type: 'opamp', x: cx, y: cy, label: 'A' },
      { type: 'label', x: viX - 8, y: plusY - 14, text: Vi },
      { type: 'label', x: w - 35, y: cy - 14, text: Vo },
    ],
  };
}

function renderSeriesCircuit(w, h, p) {
  const y = h * 0.5;
  const x0 = 40;
  const x1 = 120;
  const x2 = 220;
  const x3 = 320;
  const x4 = w - 40;

  return {
    title: p.title || '简单串联电路',
    width: w,
    height: h,
    elements: [
      { type: 'label', x: w / 2, y: 24, text: p.title || '串联电路' },
      { type: 'wire', x1: x0, y1: y, x2: x1 - 30, y2: y },
      { type: 'wire', x1: x1 + 30, y1: y, x2: x2 - 35, y2: y },
      { type: 'wire', x1: x2 + 35, y1: y, x2: x3 - 25, y2: y },
      { type: 'wire', x1: x3 + 25, y1: y, x2: x4, y2: y },
      { type: 'wire', x1: x0, y1: y, x2: x0, y2: y + 70 },
      { type: 'wire', x1: x4, y1: y, x2: x4, y2: y + 70 },
      { type: 'wire', x1: x0, y1: y + 70, x2: x4, y2: y + 70 },
      { type: 'battery', x1: x0, y1: y + 70, x2: x0, y2: y, label: p.V || 'V' },
      { type: 'resistor', x1: x1 - 30, y1: y, x2: x1 + 30, y2: y, label: p.R1 || 'R1' },
      { type: 'lamp', x: x2, y, r: 22, label: p.L || 'L' },
      { type: 'switch', x1: x3 - 25, y1: y, x2: x3 + 25, y2: y, label: p.S || 'S', open: p.open !== false },
    ],
  };
}

function renderParallelCircuit(w, h, p) {
  const xL = 50;
  const xR = w - 50;
  const yM = h * 0.5;
  const yT = yM - 55;
  const yB = yM + 55;
  const xMid = w * 0.5;

  return {
    title: p.title || '并联电路',
    width: w,
    height: h,
    elements: [
      { type: 'label', x: w / 2, y: 24, text: p.title || '并联电路' },
      { type: 'wire', x1: xL, y1: yT, x2: xR, y2: yT },
      { type: 'wire', x1: xL, y1: yB, x2: xR, y2: yB },
      { type: 'wire', x1: xL, y1: yT, x2: xL, y2: yB },
      { type: 'wire', x1: xR, y1: yT, x2: xR, y2: yB },
      { type: 'wire', x1: xL, y1: yB, x2: xL, y2: h - 40 },
      { type: 'wire', x1: xR, y1: yB, x2: xR, y2: h - 40 },
      { type: 'wire', x1: xL, y1: h - 40, x2: xR, y2: h - 40 },
      { type: 'battery', x1: xMid, y1: h - 40, x2: xMid, y2: yB, label: 'V' },
      { type: 'resistor', x1: xMid - 40, y1: yT, x2: xMid + 40, y2: yT, label: p.R1 || 'R1' },
      { type: 'resistor', x1: xMid - 40, y1: yB - 0, x2: xMid + 40, y2: yB, label: p.R2 || 'R2' },
      { type: 'node', x: xL, y: yT },
      { type: 'node', x: xL, y: yB },
      { type: 'node', x: xR, y: yT },
      { type: 'node', x: xR, y: yB },
    ],
  };
}

function clamp(value, min, max, fallback) {
  const n = Number(value);
  if (!Number.isFinite(n)) return fallback;
  return Math.max(min, Math.min(max, n));
}

/**
 * Tiny SVG diagram renderer for AI-generated questions.
 */

const SVG_NS = 'http://www.w3.org/2000/svg';

export function renderDiagram(diagram) {
  if (!diagram || !Array.isArray(diagram.elements)) return null;

  const width = clampNumber(diagram.width, 120, 900, 420);
  const height = clampNumber(diagram.height, 80, 600, 240);
  const svg = createSvg('svg', {
    class: 'q-diagram',
    viewBox: `0 0 ${width} ${height}`,
    role: 'img',
    'aria-label': diagram.title || '题目图示',
  });

  const title = createSvg('title');
  title.textContent = diagram.title || '题目图示';
  svg.appendChild(title);

  for (const element of diagram.elements.slice(0, 80)) {
    const rendered = renderElement(element);
    if (rendered) svg.appendChild(rendered);
  }

  return svg;
}

function renderElement(element) {
  if (!element || typeof element !== 'object') return null;
  switch (String(element.type || '').toLowerCase()) {
    case 'wire':
    case 'line':
      return renderLine(element, 'wire');
    case 'arrow':
      return renderArrow(element);
    case 'resistor':
      return renderResistor(element);
    case 'battery':
      return renderBattery(element);
    case 'switch':
      return renderSwitch(element);
    case 'lamp':
    case 'bulb':
      return renderLamp(element);
    case 'ammeter':
    case 'voltmeter':
    case 'meter':
      return renderMeter(element);
    case 'node':
      return renderNode(element);
    case 'label':
    case 'text':
      return renderLabel(element);
    case 'rect':
    case 'rectangle':
    case 'box':
      return renderRect(element);
    case 'ellipse':
    case 'oval':
      return renderEllipse(element);
    case 'circle':
    case 'dot':
      return renderCircleShape(element);
    case 'polygon':
    case 'triangle':
      return renderPolygon(element);
    case 'icon':
    case 'emoji':
    case 'symbol':
      return renderIcon(element);
    case 'arc':
      return renderArc(element);
    case 'opamp':
    case 'op-amp':
    case 'amplifier':
      return renderOpamp(element);
    case 'ground':
    case 'gnd':
      return renderGround(element);
    case 'cap':
    case 'capacitor':
      return renderCapacitor(element);
    case 'path':
    case 'polyline':
      return renderPolyline(element);
    default:
      return null;
  }
}

function renderLine(element, className) {
  return createSvg('line', {
    class: className,
    x1: num(element.x1),
    y1: num(element.y1),
    x2: num(element.x2),
    y2: num(element.y2),
  });
}

function renderArrow(element) {
  const group = createSvg('g', { class: 'arrow' });
  group.appendChild(renderLine(element, 'wire'));

  const x1 = num(element.x1);
  const y1 = num(element.y1);
  const x2 = num(element.x2);
  const y2 = num(element.y2);
  const angle = Math.atan2(y2 - y1, x2 - x1);
  const size = 8;
  const left = pointFrom(x2, y2, angle + Math.PI * 0.82, size);
  const right = pointFrom(x2, y2, angle - Math.PI * 0.82, size);
  group.appendChild(createSvg('path', {
    d: `M ${left.x} ${left.y} L ${x2} ${y2} L ${right.x} ${right.y}`,
    fill: 'none',
  }));
  return group;
}

function renderResistor(element) {
  const x1 = num(element.x1);
  const y1 = num(element.y1);
  const x2 = num(element.x2);
  const y2 = num(element.y2);
  const dx = x2 - x1;
  const dy = y2 - y1;
  const length = Math.max(1, Math.hypot(dx, dy));
  const ux = dx / length;
  const uy = dy / length;
  const px = -uy;
  const py = ux;
  const lead = Math.min(24, length * 0.25);
  const amp = 8;
  const start = { x: x1 + ux * lead, y: y1 + uy * lead };
  const end = { x: x2 - ux * lead, y: y2 - uy * lead };
  const points = [`${x1},${y1}`, `${start.x},${start.y}`];

  for (let i = 1; i <= 6; i++) {
    const t = i / 7;
    const sign = i % 2 === 0 ? -1 : 1;
    points.push(`${start.x + (end.x - start.x) * t + px * amp * sign},${start.y + (end.y - start.y) * t + py * amp * sign}`);
  }
  points.push(`${end.x},${end.y}`, `${x2},${y2}`);

  const group = createSvg('g', { class: 'component resistor' });
  group.appendChild(createSvg('polyline', { points: points.join(' '), fill: 'none' }));
  appendOptionalLabel(group, element);
  return group;
}

function renderBattery(element) {
  const x1 = num(element.x1);
  const y1 = num(element.y1);
  const x2 = num(element.x2);
  const y2 = num(element.y2);
  const dx = x2 - x1;
  const dy = y2 - y1;
  const length = Math.max(1, Math.hypot(dx, dy));
  const ux = dx / length;
  const uy = dy / length;
  const px = -uy;
  const py = ux;
  const cx = (x1 + x2) * 0.5;
  const cy = (y1 + y2) * 0.5;
  const gap = 6;

  const group = createSvg('g', { class: 'component battery' });
  group.appendChild(createSvg('line', { x1, y1, x2: cx - ux * 14, y2: cy - uy * 14 }));
  group.appendChild(createSvg('line', { x1: cx + ux * 14, y1: cy + uy * 14, x2, y2 }));
  group.appendChild(createSvg('line', {
    x1: cx - ux * gap - px * 18,
    y1: cy - uy * gap - py * 18,
    x2: cx - ux * gap + px * 18,
    y2: cy - uy * gap + py * 18,
  }));
  group.appendChild(createSvg('line', {
    x1: cx + ux * gap - px * 10,
    y1: cy + uy * gap - py * 10,
    x2: cx + ux * gap + px * 10,
    y2: cy + uy * gap + py * 10,
  }));
  appendOptionalLabel(group, element);
  return group;
}

function renderSwitch(element) {
  const x1 = num(element.x1);
  const y1 = num(element.y1);
  const x2 = num(element.x2);
  const y2 = num(element.y2);
  const open = element.open !== false;
  const group = createSvg('g', { class: 'component switch' });
  group.appendChild(createSvg('line', { x1, y1, x2: x1 + (x2 - x1) * 0.35, y2: y1 + (y2 - y1) * 0.35 }));
  group.appendChild(createSvg('line', { x1: x1 + (x2 - x1) * 0.65, y1: y1 + (y2 - y1) * 0.65, x2, y2 }));
  group.appendChild(createSvg('line', {
    x1: x1 + (x2 - x1) * 0.35,
    y1: y1 + (y2 - y1) * 0.35,
    x2: open ? x1 + (x2 - x1) * 0.68 : x1 + (x2 - x1) * 0.65,
    y2: open ? y1 + (y2 - y1) * 0.20 - 10 : y1 + (y2 - y1) * 0.65,
  }));
  appendOptionalLabel(group, element);
  return group;
}

function renderLamp(element) {
  const cx = num(element.x);
  const cy = num(element.y);
  const r = clampNumber(element.r, 8, 60, 18);
  const group = createSvg('g', { class: 'component lamp' });
  group.appendChild(createSvg('circle', { cx, cy, r }));
  group.appendChild(createSvg('line', { x1: cx - r * 0.65, y1: cy - r * 0.65, x2: cx + r * 0.65, y2: cy + r * 0.65 }));
  group.appendChild(createSvg('line', { x1: cx - r * 0.65, y1: cy + r * 0.65, x2: cx + r * 0.65, y2: cy - r * 0.65 }));
  appendOptionalLabel(group, element);
  return group;
}

function renderMeter(element) {
  const cx = num(element.x);
  const cy = num(element.y);
  const r = clampNumber(element.r, 8, 60, 18);
  const label = element.label || (String(element.type).toLowerCase() === 'voltmeter' ? 'V' : 'A');
  const group = createSvg('g', { class: 'component meter' });
  group.appendChild(createSvg('circle', { cx, cy, r }));
  group.appendChild(text(label, cx, cy + 5, 'middle'));
  return group;
}

function renderNode(element) {
  return createSvg('circle', {
    class: 'node',
    cx: num(element.x),
    cy: num(element.y),
    r: clampNumber(element.r, 2, 12, 4),
  });
}

function renderLabel(element) {
  return text(element.text || element.label || '', num(element.x), num(element.y), element.anchor || 'middle');
}

function renderRect(element) {
  const x = num(element.x);
  const y = num(element.y);
  const w = clampNumber(element.w ?? element.width, 1, 900, 80);
  const h = clampNumber(element.h ?? element.height, 1, 600, 50);
  return createSvg('rect', {
    class: 'shape rect',
    x,
    y,
    width: w,
    height: h,
    rx: clampNumber(element.rx, 0, 40, 6),
    fill: element.fill || 'none',
    stroke: element.stroke || '#172033',
    'stroke-width': element.strokeWidth ?? 2,
  });
}

function renderEllipse(element) {
  const cx = num(element.cx ?? element.x);
  const cy = num(element.cy ?? element.y);
  const rx = clampNumber(element.rx ?? element.r, 4, 200, 30);
  const ry = clampNumber(element.ry ?? element.r, 4, 200, 20);
  return createSvg('ellipse', {
    class: 'shape ellipse',
    cx,
    cy,
    rx,
    ry,
    fill: element.fill || 'none',
    stroke: element.stroke || '#172033',
    'stroke-width': element.strokeWidth ?? 2,
  });
}

function renderCircleShape(element) {
  const cx = num(element.cx ?? element.x);
  const cy = num(element.cy ?? element.y);
  const r = clampNumber(element.r, 4, 120, 20);
  const group = createSvg('g', { class: 'shape circle' });
  group.appendChild(createSvg('circle', {
    cx,
    cy,
    r,
    fill: element.fill || 'rgba(79, 140, 255, 0.15)',
    stroke: element.stroke || '#172033',
    'stroke-width': element.strokeWidth ?? 2,
  }));
  if (element.label || element.emoji) {
    group.appendChild(text(element.emoji || element.label, cx, cy + 5, 'middle'));
  }
  return group;
}

function renderPolygon(element) {
  const points = element.points;
  if (!points) return null;
  const pointStr = Array.isArray(points)
    ? points.map(p => (Array.isArray(p) ? `${num(p[0])},${num(p[1])}` : String(p))).join(' ')
    : String(points);
  return createSvg('polygon', {
    class: 'shape polygon',
    points: pointStr,
    fill: element.fill || 'rgba(255, 200, 87, 0.25)',
    stroke: element.stroke || '#172033',
    'stroke-width': element.strokeWidth ?? 2,
  });
}

function renderIcon(element) {
  const size = clampNumber(element.size ?? element.fontSize, 12, 72, 28);
  const node = createSvg('text', {
    class: 'icon',
    x: num(element.x),
    y: num(element.y),
    'text-anchor': 'middle',
    'font-size': size,
  });
  node.textContent = String(element.emoji || element.icon || element.text || '●').slice(0, 4);
  return node;
}

function renderArc(element) {
  const cx = num(element.cx ?? element.x);
  const cy = num(element.cy ?? element.y);
  const r = clampNumber(element.r, 4, 200, 40);
  const start = num(element.startAngle ?? 0);
  const end = num(element.endAngle ?? 180);
  const startRad = (start * Math.PI) / 180;
  const endRad = (end * Math.PI) / 180;
  const x1 = cx + r * Math.cos(startRad);
  const y1 = cy + r * Math.sin(startRad);
  const x2 = cx + r * Math.cos(endRad);
  const y2 = cy + r * Math.sin(endRad);
  const large = Math.abs(end - start) > 180 ? 1 : 0;
  return createSvg('path', {
    class: 'shape arc',
    d: `M ${x1} ${y1} A ${r} ${r} 0 ${large} 1 ${x2} ${y2}`,
    fill: 'none',
    stroke: element.stroke || '#172033',
    'stroke-width': element.strokeWidth ?? 2,
  });
}

function renderOpamp(element) {
  const cx = num(element.x);
  const cy = num(element.y);
  const w = clampNumber(element.w, 40, 120, 70);
  const h = clampNumber(element.h, 30, 100, 50);
  const group = createSvg('g', { class: 'component opamp' });
  const x0 = cx - w / 2;
  const y0 = cy - h / 2;
  group.appendChild(createSvg('path', {
    d: `M ${x0} ${y0} L ${x0 + w} ${cy} L ${x0} ${y0 + h} Z`,
    fill: '#fff',
    stroke: '#172033',
    'stroke-width': 3,
  }));
  group.appendChild(text('−', x0 + 12, cy - 8, 'start'));
  group.appendChild(text('+', x0 + 12, cy + 18, 'start'));
  if (element.label) {
    group.appendChild(text(element.label, cx, y0 - 8, 'middle'));
  }
  return group;
}

function renderGround(element) {
  const x = num(element.x);
  const y = num(element.y);
  const group = createSvg('g', { class: 'component ground' });
  group.appendChild(createSvg('line', { x1: x, y1: y - 14, x2: x, y2: y }));
  group.appendChild(createSvg('line', { x1: x - 18, y1: y, x2: x + 18, y2: y }));
  group.appendChild(createSvg('line', { x1: x - 12, y1: y + 6, x2: x + 12, y2: y + 6 }));
  group.appendChild(createSvg('line', { x1: x - 6, y1: y + 12, x2: x + 6, y2: y + 12 }));
  return group;
}

function renderCapacitor(element) {
  const x1 = num(element.x1);
  const y1 = num(element.y1);
  const x2 = num(element.x2);
  const y2 = num(element.y2);
  const dx = x2 - x1;
  const dy = y2 - y1;
  const length = Math.max(1, Math.hypot(dx, dy));
  const ux = dx / length;
  const uy = dy / length;
  const px = -uy;
  const py = ux;
  const cx = (x1 + x2) * 0.5;
  const cy = (y1 + y2) * 0.5;
  const gap = 5;
  const group = createSvg('g', { class: 'component capacitor' });
  group.appendChild(createSvg('line', { x1, y1, x2: cx - ux * gap, y2: cy - uy * gap }));
  group.appendChild(createSvg('line', { x1: cx + ux * gap, y1: cy + uy * gap, x2, y2 }));
  group.appendChild(createSvg('line', {
    x1: cx - ux * gap - px * 14, y1: cy - uy * gap - py * 14,
    x2: cx - ux * gap + px * 14, y2: cy - uy * gap + py * 14,
  }));
  group.appendChild(createSvg('line', {
    x1: cx + ux * gap - px * 14, y1: cy + uy * gap - py * 14,
    x2: cx + ux * gap + px * 14, y2: cy + uy * gap + py * 14,
  }));
  appendOptionalLabel(group, element);
  return group;
}

function renderPolyline(element) {
  const points = element.points;
  if (!points) return renderLine(element, 'wire');
  const pointStr = Array.isArray(points)
    ? points.map(p => (Array.isArray(p) ? `${num(p[0])},${num(p[1])}` : String(p))).join(' ')
    : String(points);
  return createSvg('polyline', {
    class: 'wire',
    points: pointStr,
    fill: 'none',
  });
}

function appendOptionalLabel(group, element) {
  if (!element.label) return;
  const x = element.x ?? ((num(element.x1) + num(element.x2)) * 0.5);
  const y = element.y ?? ((num(element.y1) + num(element.y2)) * 0.5 - 14);
  group.appendChild(text(element.label, x, y, 'middle'));
}

function text(value, x, y, anchor) {
  const node = createSvg('text', { x, y, 'text-anchor': anchor || 'middle' });
  node.textContent = String(value).slice(0, 80);
  return node;
}

function pointFrom(x, y, angle, distance) {
  return { x: x + Math.cos(angle) * distance, y: y + Math.sin(angle) * distance };
}

function num(value) {
  return Number.isFinite(Number(value)) ? Number(value) : 0;
}

function clampNumber(value, min, max, fallback) {
  const n = Number(value);
  if (!Number.isFinite(n)) return fallback;
  return Math.max(min, Math.min(max, n));
}

function createSvg(tag, attrs = {}) {
  const element = document.createElementNS(SVG_NS, tag);
  for (const [key, value] of Object.entries(attrs)) {
    element.setAttribute(key, String(value));
  }
  return element;
}

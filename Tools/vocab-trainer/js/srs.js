/**
 * Ebbinghaus-inspired spaced repetition (SM-2 variant).
 * Intervals in days: 1, 2, 4, 7, 15, 30, 60 ...
 */

const DAY_MS = 24 * 60 * 60 * 1000;

/** Quality 0-5: 0=complete fail, 5=perfect */
export function updateSrs(srs, quality) {
  const q = Math.max(0, Math.min(5, Math.round(quality)));
  const next = { ...srs, lastReview: Date.now() };

  if (q < 3) {
    next.repetitions = 0;
    next.interval = 1;
    next.ease = Math.max(1.3, next.ease - 0.2);
    next.mastered = false;
  } else {
    next.repetitions += 1;
    next.ease = Math.max(1.3, next.ease + (0.1 - (5 - q) * (0.08 + (5 - q) * 0.02)));

    if (next.repetitions === 1) next.interval = 1;
    else if (next.repetitions === 2) next.interval = 3;
    else next.interval = Math.round(next.interval * next.ease);

    if (next.repetitions >= 5 && next.interval >= 21) next.mastered = true;
  }

  next.nextReview = Date.now() + next.interval * DAY_MS;
  return next;
}

/** Map session accuracy (0-1) to SM-2 quality */
export function accuracyToQuality(accuracy) {
  if (accuracy >= 0.95) return 5;
  if (accuracy >= 0.85) return 4;
  if (accuracy >= 0.7) return 3;
  if (accuracy >= 0.5) return 2;
  if (accuracy >= 0.3) return 1;
  return 0;
}

export function isDue(word, now = Date.now()) {
  return !word.srs.mastered && word.srs.nextReview <= now;
}

export function getDueWords(words, now = Date.now()) {
  return words.filter(w => isDue(w, now));
}

export function getNewWords(words) {
  return words.filter(w => w.srs.repetitions === 0);
}

export function getMasteredWords(words) {
  return words.filter(w => w.srs.mastered);
}

export function formatNextReview(timestamp) {
  const diff = timestamp - Date.now();
  if (diff <= 0) return '今日';
  const days = Math.ceil(diff / DAY_MS);
  if (days === 1) return '明天';
  if (days <= 7) return `${days} 天后`;
  const d = new Date(timestamp);
  return `${d.getMonth() + 1}/${d.getDate()}`;
}

export function ebbinghausLabel(repetitions) {
  const labels = ['新词', '第1次复习', '第2次复习', '第3次复习', '第4次复习', '长期记忆'];
  return labels[Math.min(repetitions, labels.length - 1)];
}

/**
 * localStorage persistence for words, SRS, wrong answers, reports, settings.
 */

const KEYS = {
  words: 'vocab_words',
  wrong: 'vocab_wrong',
  reports: 'vocab_reports',
  settings: 'vocab_settings',
};

const DEFAULT_SETTINGS = {
  apiKey: '',
  apiBase: 'https://api.deepseek.com',
  apiModel: 'deepseek-chat',
  proxyUrl: 'http://127.0.0.1:8765/api/chat',
};

export function loadSettings() {
  try {
    return { ...DEFAULT_SETTINGS, ...JSON.parse(localStorage.getItem(KEYS.settings) || '{}') };
  } catch {
    return { ...DEFAULT_SETTINGS };
  }
}

export function saveSettings(settings) {
  localStorage.setItem(KEYS.settings, JSON.stringify(settings));
}

export function loadWords() {
  try {
    return JSON.parse(localStorage.getItem(KEYS.words) || '[]');
  } catch {
    return [];
  }
}

export function saveWords(words) {
  localStorage.setItem(KEYS.words, JSON.stringify(words));
}

export function loadWrongAnswers() {
  try {
    return JSON.parse(localStorage.getItem(KEYS.wrong) || '[]');
  } catch {
    return [];
  }
}

export function saveWrongAnswers(items) {
  localStorage.setItem(KEYS.wrong, JSON.stringify(items));
}

export function addWrongAnswers(entries) {
  const existing = loadWrongAnswers();
  saveWrongAnswers([...entries, ...existing].slice(0, 500));
}

export function loadReports() {
  try {
    return JSON.parse(localStorage.getItem(KEYS.reports) || '[]');
  } catch {
    return [];
  }
}

export function saveReport(report) {
  const reports = loadReports();
  reports.unshift(report);
  saveReports(reports.slice(0, 50));
}

function saveReports(reports) {
  localStorage.setItem(KEYS.reports, JSON.stringify(reports));
}

export function createWord(word, meaning = '') {
  const now = Date.now();
  return {
    id: crypto.randomUUID(),
    word: word.trim(),
    meaning: meaning.trim(),
    createdAt: now,
    srs: {
      interval: 0,
      ease: 2.5,
      repetitions: 0,
      nextReview: now,
      lastReview: null,
      mastered: false,
    },
  };
}

export function parseImportText(text) {
  const lines = text.split(/\r?\n/).map(l => l.trim()).filter(Boolean);
  const words = [];
  for (const line of lines) {
    const pipe = line.split('|').map(s => s.trim());
    if (pipe.length >= 2) {
      words.push(createWord(pipe[0], pipe.slice(1).join(' | ')));
    } else {
      const comma = line.split(/[,，\t]/).map(s => s.trim());
      if (comma.length >= 2) {
        words.push(createWord(comma[0], comma.slice(1).join(', ')));
      } else {
        words.push(createWord(line));
      }
    }
  }
  return words;
}

export function mergeWords(existing, incoming) {
  const byWord = new Map(existing.map(w => [w.word.toLowerCase(), w]));
  for (const w of incoming) {
    const key = w.word.toLowerCase();
    if (byWord.has(key)) {
      const old = byWord.get(key);
      if (w.meaning && !old.meaning) old.meaning = w.meaning;
    } else {
      byWord.set(key, w);
    }
  }
  return Array.from(byWord.values());
}

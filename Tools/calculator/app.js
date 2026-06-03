(function () {
  'use strict';

  const display = document.getElementById('display');

  // State
  let currentInput = '';
  let previousInput = '';
  let operator = null;
  let shouldResetDisplay = false;
  let lastResult = null;

  function updateDisplay(value) {
    display.value = value || '0';
  }

  function clearAll() {
    currentInput = '';
    previousInput = '';
    operator = null;
    shouldResetDisplay = false;
    lastResult = null;
    updateDisplay('0');
  }

  function inputDigit(digit) {
    if (shouldResetDisplay) {
      currentInput = '';
      shouldResetDisplay = false;
    }
    // Prevent multiple leading zeros
    if (currentInput === '0' && digit === '0') return;
    if (currentInput === '0' && digit !== '0') {
      currentInput = digit;
    } else if (currentInput === '-0') {
      currentInput = '-' + digit;
    } else {
      currentInput += digit;
    }
    updateDisplay(currentInput);
  }

  function inputDecimal() {
    if (shouldResetDisplay) {
      currentInput = '0';
      shouldResetDisplay = false;
    }
    if (!currentInput.includes('.')) {
      currentInput = currentInput || '0';
      currentInput += '.';
    }
    updateDisplay(currentInput);
  }

  /**
   * Evaluate a single operation safely — no eval().
   * Returns the numeric result or throws on division by zero.
   */
  function compute(a, op, b) {
    const x = parseFloat(a);
    const y = parseFloat(b);
    switch (op) {
      case '+':
        return x + y;
      case '−':
        return x - y;
      case '×':
        return x * y;
      case '÷':
        if (y === 0) {
          throw new Error('Cannot divide by zero');
        }
        return x / y;
      default:
        return y;
    }
  }

  function formatResult(value) {
    // Avoid floating-point noise for reasonable decimals
    if (Number.isInteger(value)) return String(value);
    // Round to 10 significant digits to suppress FP drift
    const rounded = parseFloat(value.toPrecision(10));
    return String(rounded);
  }

  function handleOperator(op) {
    if (operator && !shouldResetDisplay && currentInput !== '') {
      // Chain: evaluate pending operation first (left-to-right)
      try {
        const result = compute(previousInput, operator, currentInput);
        previousInput = formatResult(result);
        updateDisplay(previousInput);
      } catch (e) {
        updateDisplay(e.message);
        // Reset state so user can continue
        currentInput = '';
        previousInput = '';
        operator = null;
        shouldResetDisplay = true;
        return;
      }
    } else {
      previousInput = currentInput || previousInput || '0';
    }
    operator = op;
    shouldResetDisplay = true;
  }

  function handleEquals() {
    if (!operator) {
      // No pending operation — just repeat last result if applicable
      if (lastResult !== null) {
        updateDisplay(formatResult(lastResult));
      }
      return;
    }
    const right = currentInput || previousInput;
    try {
      const result = compute(previousInput, operator, right);
      const formatted = formatResult(result);
      updateDisplay(formatted);
      lastResult = result;
      previousInput = formatted;
      currentInput = formatted;
      operator = null;
      shouldResetDisplay = true;
    } catch (e) {
      updateDisplay(e.message);
      currentInput = '';
      previousInput = '';
      operator = null;
      shouldResetDisplay = true;
      lastResult = null;
    }
  }

  // ---- Event delegation ----
  document.querySelector('.buttons').addEventListener('click', function (e) {
    const btn = e.target.closest('button');
    if (!btn) return;

    const action = btn.dataset.action;

    switch (action) {
      case 'digit':
        inputDigit(btn.dataset.value);
        break;
      case 'decimal':
        inputDecimal();
        break;
      case 'operator':
        handleOperator(btn.dataset.value);
        break;
      case 'equals':
        handleEquals();
        break;
      case 'clear':
        clearAll();
        break;
    }
  });

  // ---- Keyboard support ----
  document.addEventListener('keydown', function (e) {
    const key = e.key;
    if (key >= '0' && key <= '9') {
      inputDigit(key);
    } else if (key === '.') {
      inputDecimal();
    } else if (key === '+') {
      handleOperator('+');
    } else if (key === '-') {
      handleOperator('−');
    } else if (key === '*') {
      handleOperator('×');
    } else if (key === '/') {
      e.preventDefault(); // avoid Firefox quick-find
      handleOperator('÷');
    } else if (key === 'Enter' || key === '=') {
      e.preventDefault();
      handleEquals();
    } else if (key === 'Escape' || key === 'c' || key === 'C') {
      clearAll();
    } else if (key === 'Backspace') {
      if (shouldResetDisplay) return;
      currentInput = currentInput.slice(0, -1);
      updateDisplay(currentInput || '0');
    }
  });

  // Init
  updateDisplay('0');
})();

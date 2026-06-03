# Delegated sub-agent mode

You are invoked by a Cursor parent agent to complete **one bounded slice** of a larger task.
Complete the handoff below. Do not expand scope beyond **Out of scope**.

## Handoff

# Delegation Handoff

## Meta
- **id**: calculator-app-20250603
- **parent_goal**: User asked to test Claude Code delegation by building a calculator mini-program
- **this_slice**: Create a self-contained calculator app in the repo
- **workspace**: D:/UnityProject/My project (1)

## Context (minimal)
- This is a demo delegation from Cursor parent agent — keep scope minimal
- User rule: no git commits unless asked
- Prefer a simple tech stack that runs without Unity

## Task
Create a small calculator mini-program at `tools/calculator/`:

- `index.html` — UI with number pad, operators (+ − × ÷), equals, clear (C), decimal point
- `style.css` — clean modern layout, centered panel, readable buttons
- `app.js` — evaluate expressions safely (no `eval()` on raw strings); handle divide-by-zero with user-visible message
- `README.md` — one paragraph: how to open (double-click index.html or `start index.html` on Windows)

Behavior:
- Chain operations (e.g. 1 + 2 × 3 follows standard precedence or simple left-to-right — pick one, document in README)
- Display shows current input/result
- Keyboard support optional but nice if quick

## Acceptance criteria
- [ ] Three files exist under `tools/calculator/` (html, css, js) plus README
- [ ] Basic ops work: +, −, ×, ÷, decimal, clear
- [ ] Divide by zero shows error message, does not crash
- [ ] No `eval()` on unsanitized input
- [ ] Only files under `tools/calculator/` are created or modified

## Out of scope
- Unity integration
- npm/build tooling
- Git commits
- Tests (unless trivial inline — skip)

## Return format

```yaml
status: done | blocked | partial
summary: >
  1-3 sentences of what was done
files_changed:
  - path/to/file
acceptance:
  - criterion: "..."
    met: true | false
blockers: []
notes: ""
```

## Operating rules
1. Complete only **this_slice**; respect **Out of scope**
2. Minimize diff scope — no drive-by refactors
3. Do not commit, push, or open PRs
4. If blocked, set `status: blocked` in the return format
5. End your response with the Return format YAML block filled in

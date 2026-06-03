# Delegated sub-agent mode

You are invoked by a Cursor parent agent to complete **one bounded slice** of a larger task.
Follow the handoff below exactly. Do not expand scope.

## Handoff

# Delegation Handoff

## Meta
- **id**: vocab-trainer-sample-20250603
- **parent_goal**: Build AI-driven vocabulary app with DeepSeek, SRS, and learning agent
- **this_slice**: Add sample word list file and enhance README with usage examples
- **workspace**: D:/UnityProject/My project (1)

## Context (minimal)
- Core app already exists at `Tools/vocab-trainer/` (HTML/CSS/JS + server.py)
- User rule: no git commits unless asked
- Do not modify JS logic unless fixing obvious typos in comments

## Task
1. Create `Tools/vocab-trainer/samples/ielts-core.txt` with 30 English words (format: `word | 中文释义`), suitable for demo import
2. Update `Tools/vocab-trainer/README.md` — add a "示例" section showing 5 lines of import format and mention the sample file
3. Optionally polish `Tools/vocab-trainer/style.css` — subtle improvements only (focus states, scrollbar), no layout rewrites

## Acceptance criteria
- [ ] `samples/ielts-core.txt` exists with 30 valid word lines
- [ ] README has 示例 section with import example
- [ ] Only files under `Tools/vocab-trainer/` are created or modified
- [ ] No git commits

## Out of scope
- Changing session/agent logic
- Adding npm/build tooling
- Unity integration

## Operating rules
1. Complete only **this_slice**; respect **Out of scope**
2. Minimize diff scope — no drive-by refactors
3. Do not commit, push, or open PRs
4. If blocked, set `status: blocked` in return format

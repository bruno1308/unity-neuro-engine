# Neuro-Engine Git Hooks

Automated enforcement of the `code-reviewer-layers` agent for engine infrastructure changes.

## Installation

**Windows:**
```cmd
githooks\install.bat
```

**Unix/Mac/Git Bash:**
```bash
./githooks/install.sh
```

This sets `git config core.hooksPath githooks` so git uses these hooks.

## Hooks

### pre-commit

Runs before every commit. Checks for:

| Check | Severity | Description |
|-------|----------|-------------|
| Hidden dependencies | ERROR | `GetComponent<>` or `FindObjectOfType<>` in protected paths |
| Observation purity | ERROR | `SetActive`/`Destroy`/`Instantiate` in Capture/Detector/Analysis files |
| Atomic writes | WARNING | `File.WriteAllText` without `.tmp` pattern in Writer files |
| Hardcoded secrets | ERROR | API keys or passwords in code |

**Behavior:**
- If errors found: **BLOCKS** commit (exit 1)
- If warnings only: Allows commit with warning
- Checks for approval in `hooks/reviews/pending-approval.json`

### pre-push

Runs before every push. Checks:

1. Whether protected paths are in the push
2. Looks for layer review in `hooks/reviews/layer-review-*.json`
3. **BLOCKS** if latest review verdict is `BLOCKED`
4. **WARNS** if verdict is `CHANGES_REQUESTED`
5. **ALLOWS** if `APPROVED` or no engine files changed

### commit-msg

Runs after writing commit message. Provides:

- Suggests layer tags (`[L1]`, `[L2]`, etc.) for commits touching engine code
- Non-blocking, informational only

## Protected Paths

These paths trigger the hooks:

```
Packages/com.neuroengine.core/Runtime/Core/
Packages/com.neuroengine.core/Runtime/Services/
Packages/com.neuroengine.core/Editor/
Packages/com.neuroengine.core/Tests/
```

## Layer Mapping

| Tag | Layer | Files |
|-----|-------|-------|
| `[L1]` | Code-First Foundation | NeuroEngineLifetimeScope, I*.cs interfaces |
| `[L2]` | Observation System | *Capture*, *Detector*, *Analysis*, *Accessibility* |
| `[L3]` | Interaction System | *InputSimulation*, *Interaction* |
| `[L4]` | Persistent Artifacts | *Writer*, *TaskManager*, *Transcript* |
| `[L5]` | Evaluation Framework | *Validation*, *Evaluator* |
| `[tests]` | Test Coverage | Tests/* |

## Bypassing Hooks

**Not recommended** but available for emergencies:

```bash
# Bypass pre-commit
git commit --no-verify

# Bypass pre-push
git push --no-verify
```

## Creating Review Approval

To approve changes and unblock commits:

1. Create `hooks/reviews/pending-approval.json`:
```json
{
  "reviewer": "code-reviewer-layers",
  "timestamp": "2026-01-21T12:00:00Z",
  "verdict": "APPROVED",
  "files_reviewed": ["..."],
  "findings": []
}
```

2. Or create a full review: `hooks/reviews/layer-review-{timestamp}.json`

## Uninstalling

```bash
git config --unset core.hooksPath
```

This reverts to the default `.git/hooks/` directory.

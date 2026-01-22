---
name: layer-review
description: Review code changes to neuroengine.core for layer violations and architectural compliance. Blocks anti-patterns.
---

# Skill: layer-review

## When to Use

Claude should invoke this skill proactively when:
- Any modification to `Packages/com.neuroengine.core/`
- Before committing engine changes
- When user mentions "engine", "core", or "layer" changes
- After creating new services or interfaces
- When reviewing pull requests touching engine code

## Authority

This skill enforces architectural quality. Changes violating layer principles should be flagged and corrected before proceeding.

## Layer Checklist

### Layer 1: Code-First Foundation

Files: `Runtime/Core/*.cs`, `Runtime/Services/*Service.cs`

| Rule | Check |
|------|-------|
| DI Required | No `GetComponent<T>()` or `FindObjectOfType<T>()` |
| Interface First | Services implement interfaces |
| No Inspector Wiring | Dependencies explicit in code |
| UI Toolkit | New UI uses UXML/USS, not UGUI |
| Serializable State | State convertible to JSON |

### Layer 2: Observation (Eyes)

Files: `*Capture*.cs`, `*Detector*.cs`, `*Analysis*.cs`

| Rule | Check |
|------|-------|
| Pure Observation | No mutation in observation code |
| Complete Snapshots | All relevant state captured |
| Missing Refs | Arrays/lists checked for nulls |
| Camera Independence | Works without camera when possible |

### Layer 3: Interaction (Hands)

Files: `InputSimulation*.cs`, `*Interact*.cs`

| Rule | Check |
|------|-------|
| State Tracking | KeyDown/KeyUp balanced |
| Temporal Order | Input queue maintains order |
| Clean Release | ReleaseAll() clears all state |
| Thread Safe | If accessed from multiple contexts |

### Layer 4: Persistence (Memory)

Files: `*Writer*.cs`, `*Manager*.cs` (for tasks)

| Rule | Check |
|------|-------|
| Atomic Writes | Write to .tmp, then move |
| UTC Timestamps | ISO-8601 format |
| Git Friendly | JSON, deterministic, no binary |
| Directory Structure | Follows hooks/ convention |

### Layer 5: Evaluation (Judgment)

Files: `*Grader*.cs`, `*Evaluation*.cs`

| Rule | Check |
|------|-------|
| Tier Cascade | Lower tiers must pass first |
| No Tier Skip | Explicit justification required |
| Measurable | Results are machine-parseable |
| Batched VLM | No per-frame VLM calls |

## Anti-Pattern Detection

### Critical Anti-Patterns (BLOCK)

```csharp
// BLOCK: Hidden dependency
var player = FindObjectOfType<PlayerController>();
var comp = GetComponent<Rigidbody>();

// BLOCK: Observation that mutates
public SceneSnapshot Capture() {
    gameObject.SetActive(true); // NO!
    return snapshot;
}

// BLOCK: Hardcoded keys
private const string API_KEY = "abc123";

// BLOCK: Non-atomic write
File.WriteAllText(path, content);
```

### Warning Anti-Patterns (FLAG)

```csharp
// WARN: Might need thread safety
private List<InputEvent> _queue = new();

// WARN: Missing null check
foreach (var item in _items) { /* item could be null */ }

// WARN: Blocking async on main thread
task.Wait();
```

## Review Output

```json
{
  "reviewer": "layer-review",
  "timestamp": "2026-01-21T14:30:00Z",
  "files_reviewed": [
    "Runtime/Services/NewService.cs"
  ],
  "verdict": "CHANGES_REQUESTED",
  "findings": [
    {
      "severity": "error",
      "layer": 1,
      "file": "Runtime/Services/NewService.cs",
      "line": 42,
      "rule": "no-hidden-dependencies",
      "message": "Use VContainer injection instead of GetComponent<T>()",
      "suggestion": "[Inject] private readonly IPlayerController _player;"
    }
  ],
  "approval_conditions": [
    "Replace GetComponent with injection"
  ]
}
```

## Verdicts

| Verdict | Meaning | Action |
|---------|---------|--------|
| APPROVED | All checks pass | Proceed |
| CHANGES_REQUESTED | Warnings present | Fix before merge |
| BLOCKED | Errors present | Must fix |

## Integration

This skill should be triggered by hooks.json on:
- `PostToolUse` for Edit/Write to `com.neuroengine.core/**`
- Before any git commit touching engine code

---
name: hooks-persist
description: Persist state to the hooks/ directory for cross-session memory. Saves snapshots, task progress, and artifacts.
---

# Skill: hooks-persist

## When to Use

Claude should invoke this skill proactively when:
- Completing a task (save progress)
- After significant code changes
- Before ending a session
- After running evaluations
- When user says "save", "checkpoint", "remember this"
- After any operation that should survive session reset

## Context

The `hooks/` directory is the Neuro-Engine's persistent memory. AI sessions are ephemeral, but hooks/ survives. All important state must be written here.

## Directory Structure

```
hooks/
├── iterations/
│   └── Iteration{N}/
│       ├── status.json           # Overall status
│       ├── tasks/                # Task artifacts
│       │   └── task-{id}.json
│       └── evaluations/          # Eval results
│           └── {timestamp}.json
├── tasks/                        # Global task registry
│   └── {taskId}/
│       ├── assignment.json
│       ├── progress.json
│       └── transcript.md
├── blockers/                     # Open blockers
│   └── {issue-id}.json
├── orchestration/                # Agent coordination
│   ├── status.json
│   └── budget.json
├── validation/                   # Validation results
│   └── {timestamp}.json
└── status-snapshots/             # Historical status
    └── {timestamp}.json
```

## Write Procedures

### Save Task Progress

When a task is completed or updated:

```json
// hooks/tasks/{taskId}/progress.json
{
  "taskId": "task-001",
  "status": "completed",
  "startedAt": "2026-01-21T14:00:00Z",
  "completedAt": "2026-01-21T14:32:00Z",
  "artifacts": [
    "Assets/Iteration1/Scripts/Services/IScoreService.cs",
    "Assets/Iteration1/Scripts/Services/ScoreService.cs"
  ],
  "notes": "Interface and implementation created, registered in DI"
}
```

### Save Iteration Status

After significant progress:

```json
// hooks/iterations/Iteration1/status.json
{
  "iteration": "Iteration1",
  "name": "Target Clicker",
  "status": "in_progress",
  "tasksTotal": 12,
  "tasksCompleted": 3,
  "currentTask": "task-004",
  "lastEvaluation": {
    "tier": 2,
    "result": "pass",
    "timestamp": "2026-01-21T14:30:00Z"
  },
  "blockers": [],
  "updatedAt": "2026-01-21T14:35:00Z"
}
```

### Save Evaluation Results

After running evaluations:

```json
// hooks/iterations/Iteration1/evaluations/2026-01-21T14-30-00Z.json
{
  "timestamp": "2026-01-21T14:30:00Z",
  "iteration": "Iteration1",
  "tiers": {
    "1": { "result": "pass", "errors": 0, "warnings": 0 },
    "2": { "result": "pass", "criteria": 5, "passed": 5 },
    "3": { "result": "skip", "reason": "Not yet implemented" }
  },
  "overall": "pass",
  "nextRecommendation": "Proceed to Tier 3 behavioral tests"
}
```

### Save Validation Results

After validation checks:

```json
// hooks/validation/2026-01-21T14-00-00Z.json
{
  "timestamp": "2026-01-21T14:00:00Z",
  "checks": {
    "projectStructure": "pass",
    "environment": "pass",
    "unityPackages": "pass",
    "mcpConnection": "fail"
  },
  "issues": ["Unity not running - MCP unavailable"],
  "canProceed": true
}
```

## File Writing Rules

### Atomic Writes

Always use atomic writes to prevent corruption:
1. Write to `{path}.tmp`
2. Move/rename to `{path}`

### Timestamps

Use ISO-8601 UTC format: `2026-01-21T14:30:00Z`

### JSON Formatting

Pretty-print for git-friendliness (readable diffs):
```json
{
  "key": "value",
  "nested": {
    "inner": "value"
  }
}
```

### No Binary Files

Only write text/JSON to hooks/. Binary artifacts go elsewhere.

## Auto-Persist Triggers

This skill should be invoked automatically (via hooks.json) after:
- Any Edit/Write to `Assets/Iteration*/`
- Running `/neuro:evaluate`
- Completing a task
- Session end

## Verification

After writing, read back and verify:
1. File exists at expected path
2. JSON parses correctly
3. Required fields present

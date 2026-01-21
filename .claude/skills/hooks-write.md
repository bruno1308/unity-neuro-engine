# Skill: hooks-write

## Purpose
Write persistent state to the hooks/ directory for cross-session memory.

## When to Use
- Saving task progress
- Recording validation results
- Storing scene snapshots
- Inter-agent communication

## Context

### Directory Structure
```
hooks/
├── scenes/{SceneName}/    # Scene state snapshots, screenshots
├── tasks/{TaskId}/        # Task assignment, progress, transcript
├── convoys/{ConvoyId}/    # Grouped task deliverables
├── messages/{AgentId}/    # Agent inboxes
├── validation/            # Error reports, test results
├── assets/                # Asset generation registry
└── snapshots/             # Point-in-time backups (gitignored)
```

### File Formats
- State data: JSON
- Logs/transcripts: Markdown
- Screenshots: PNG (in snapshots/, gitignored)

## Procedure

### Writing Task Progress

Location: `hooks/tasks/{TaskId}/progress.json`

```json
{
  "taskId": "task-001",
  "status": "in_progress|completed|failed",
  "startedAt": "ISO-8601",
  "updatedAt": "ISO-8601",
  "completedAt": "ISO-8601|null",
  "steps": [
    {"name": "Step 1", "status": "completed", "output": "..."},
    {"name": "Step 2", "status": "in_progress", "output": null}
  ],
  "errors": []
}
```

### Writing Validation Results

Location: `hooks/validation/{timestamp}-{type}.json`

```json
{
  "type": "compilation|test|visual|behavioral",
  "timestamp": "ISO-8601",
  "passed": true|false,
  "results": [...],
  "errors": [...]
}
```

### Writing Messages

Location: `hooks/messages/{recipientAgentId}/{timestamp}.json`

```json
{
  "from": "agent-id",
  "to": "recipient-id",
  "timestamp": "ISO-8601",
  "type": "request|response|notification",
  "payload": {...}
}
```

## Verification
- File created in correct location
- Valid JSON (parseable)
- Required fields present

# Layer 4: Persistent Artifact System (Memory) - Examples

> Code samples for hook directory structure, transcripts, and convoy files

---

## 1. Hook Directory Structure

```
hooks/
├── scenes/
│   └── {SceneName}/
│       ├── state.json          # Full scene state snapshot
│       ├── hierarchy.json      # Scene graph only
│       ├── validation.json     # Validation results
│       ├── ui-graph.json       # Accessibility tree
│       ├── screenshot-game.png
│       └── metadata.json       # Timestamps, versions
│
├── compiler/
│   ├── last-build.json         # Build result
│   ├── errors.json             # Compile errors
│   └── warnings.json           # Compile warnings
│
├── tests/
│   ├── last-run.json           # Test results
│   └── coverage.json           # Code coverage
│
├── tasks/
│   └── {TaskId}/
│       ├── assignment.json     # Task details
│       ├── progress.json       # Current status
│       ├── transcript.json     # Full reasoning log
│       └── artifacts/          # Created files
│
├── messages/
│   ├── inbox-mayor.json
│   └── inbox-{agent}.json
│
└── convoys/
    └── {ConvoyId}.yaml         # Task groupings
```

---

## 2. Transcript Format

```json
{
  "task_id": "inv-001",
  "agent": "script-polecat-1",
  "started_at": "2026-01-20T10:00:00Z",
  "status": "success",

  "turns": [
    {
      "turn": 1,
      "type": "reasoning",
      "content": "I need to create an InventoryItem ScriptableObject..."
    },
    {
      "turn": 2,
      "type": "tool_call",
      "tool": "unity_create_script",
      "params": {"path": "Assets/Scripts/Inventory/InventoryItem.cs"},
      "result": {"success": true}
    },
    {
      "turn": 3,
      "type": "observation",
      "source": "compiler",
      "content": {"errors": 0, "warnings": 0}
    }
  ],

  "outcome": {
    "files_created": ["Assets/Scripts/Inventory/InventoryItem.cs"],
    "compilation": "success",
    "validation": "pass"
  }
}
```

---

## 3. Why Git-Backed?

- **Survives crashes** — domain reloads, agent restarts
- **Full history** — "When did this break?"
- **Diff capabilities** — "What changed?"
- **Branch support** — experiments without risk

---

## References

- [GasTown Multi-Agent Orchestration](https://github.com/steveyegge/gastown)

---
description: Create a GitHub issue for a blocker with layer fault attribution
---

# /neuro:blocker - Report Engine Blocker

You are creating a GitHub issue for a blocker encountered during development.

## Parse Arguments

The user invoked: `/neuro:blocker $ARGUMENTS`

The argument should be a description of the blocker in quotes.

## Procedure

### 1. Gather Information

Ask the user (if not clear from context):
- Which iteration is this for?
- What GDD task was being attempted?
- What happened vs what was expected?

### 2. Determine Layer Fault

Analyze which layer(s) are at fault:

| Layer | Responsible For |
|-------|-----------------|
| L1: Code-First | VContainer DI, UI Toolkit, serialization |
| L2: Observation | State capture, spatial analysis, missing refs |
| L3: Interaction | Input simulation, clicks, keypresses |
| L4: Persistence | hooks/, transcripts, file writes |
| L5: Evaluation | Graders, verification tiers |
| L6: Orchestration | Agent coordination, task management |
| L7: Asset Gen | Meshy, ElevenLabs, asset imports |

### 3. Create GitHub Issue

Use the `gh` CLI to create an issue:

```bash
gh issue create \
  --title "Blocker: {short description}" \
  --body "{body from template}" \
  --label "blocker,layer-{N},iteration-{N}"
```

### Issue Body Template

```markdown
## Blocker: {description}

**Iteration:** Iteration{N}
**GDD Task:** {task being attempted}

### What Happened
{description of the problem}

### Expected Behavior
{what should have happened}

### Layer Fault Attribution

| Layer | At Fault? | Reason |
|-------|-----------|--------|
| L1: Code-First | {Yes/No} | {reason} |
| L2: Observation | {Yes/No} | {reason} |
| L3: Interaction | {Yes/No} | {reason} |
| L4: Persistence | {Yes/No} | {reason} |
| L5: Evaluation | {Yes/No} | {reason} |
| L6: Orchestration | {Yes/No} | {reason} |
| L7: Asset Gen | {Yes/No} | {reason} |

### Root Cause Analysis
{why this layer failed}

### Proposed Fix
{how to improve the engine}

### Workaround Used
{if any, describe}
```

### 4. Record Locally

Write to `hooks/blockers/{issue-number}.json`:
```json
{
  "id": "{issue-number}",
  "iteration": "Iteration{N}",
  "description": "{description}",
  "layers": ["L2", "L3"],
  "status": "open",
  "created": "{ISO timestamp}",
  "url": "{github issue url}"
}
```

### 5. Report

Output the issue URL and summary:
```
Created blocker issue #42: {title}
URL: https://github.com/...
Layers at fault: L2 (Observation), L3 (Interaction)
```

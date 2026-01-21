# Neuro-Engine Development Workflow

## Overview

This document defines the operational workflow for the Neuro-Engine: how games are built, how iterations are tracked, and how blockers are handled.

---

## 1. Iteration Folder Structure

Each game iteration lives in a dedicated folder under `Assets/`:

```
Assets/
├── Iteration1/           # First test game (Target Clicker)
│   ├── Scripts/
│   ├── Scenes/
│   ├── Prefabs/
│   ├── UI/
│   └── GDD.md            # The GDD used to generate this iteration
├── Iteration2/           # Second test game (after engine improvements)
│   └── ...
├── Iteration3/
│   └── ...
```

### Why Separate Iterations?

| Reason | Benefit |
|--------|---------|
| **Comparison** | See engine improvement across versions |
| **Isolation** | One game's bugs don't affect another |
| **Reproducibility** | Re-run same GDD after engine changes |
| **History** | Git history shows evolution |

### Naming Convention

- `Iteration{N}` - numbered sequentially
- Each iteration folder contains its own `GDD.md`
- Iteration-specific hooks: `hooks/iterations/Iteration{N}/`

---

## 2. GDD-Driven One-Shot Workflow

### The Vision

```
Human writes GDD → Engine builds complete game → Human plays and judges
```

### The Reality (Current State)

**Layer 6 (Agent Orchestration) is NOT YET IMPLEMENTED.**

The agent prompts exist (`.claude/agents/*.md`) but there is no:
- GDD parser
- Task decomposition logic
- Agent spawning/coordination code
- Budget/safety enforcement runtime

### Current Process (Manual Mayor)

Until Layer 6 is built, Claude (or human) manually acts as the Mayor:

```
1. Human provides GDD
2. Claude parses GDD into tasks (manually)
3. Claude executes tasks using Layer 1-5 tools
4. Claude evaluates results using Layer 5 graders
5. Blockers → GitHub Issues
6. Repeat until game complete
```

### GDD Format

Each iteration's GDD must include:

```markdown
# Game Design Document: [Game Name]

## Overview
[One paragraph description]

## Win/Lose Conditions
- Win: [Condition]
- Lose: [Condition, if any]

## Core Mechanics
1. [Mechanic 1]
2. [Mechanic 2]

## Controls
| Input | Action |
|-------|--------|
| ... | ... |

## UI Elements
- [Element 1]
- [Element 2]

## Success Criteria (Testable)
- [ ] [Criterion 1 - must be verifiable]
- [ ] [Criterion 2]

## Out of Scope
- [What this game does NOT include]
```

---

## 3. Blocker → GitHub Issue Workflow

### Definition: Blocker

A **blocker** is any situation where:
1. AI cannot proceed autonomously
2. Human intervention is required
3. A capability is missing from the engine

### Process

```
1. STOP as soon as blocker is identified
2. CREATE GitHub issue immediately
3. DOCUMENT layer fault attribution
4. ATTEMPT workaround if possible
5. CONTINUE or WAIT for fix
```

### Issue Template

```markdown
## Blocker: [Short Description]

**Iteration:** Iteration{N}
**GDD Task:** [Which GDD requirement was being attempted]

### What Happened
[Description of the blocker]

### Expected Behavior
[What should have happened if engine was complete]

### Layer Fault Attribution

| Layer | At Fault? | Reason |
|-------|-----------|--------|
| L1: Code-First | ⬜/✅ | [Explanation] |
| L2: Observation | ⬜/✅ | [Explanation] |
| L3: Interaction | ⬜/✅ | [Explanation] |
| L4: Persistence | ⬜/✅ | [Explanation] |
| L5: Evaluation | ⬜/✅ | [Explanation] |
| L6: Orchestration | ⬜/✅ | [Explanation] |
| L7: Asset Gen | ⬜/✅ | [Explanation] |

### Root Cause Analysis
[Why did this layer fail? What's missing?]

### Proposed Fix
[How should the engine be improved?]

### Workaround Used
[If any - how was this worked around for now?]

### Labels
- `blocker`
- `layer-{N}` (whichever layer is primarily at fault)
- `iteration-{N}`
```

### Layer Fault Determination

| Layer | Responsible For | Example Faults |
|-------|-----------------|----------------|
| **L1** | DI, UI Toolkit, serialization | Can't inject service, UI not updating |
| **L2** | State observation, spatial analysis | Can't query object positions, missing refs not detected |
| **L3** | Input simulation, interaction | Click not registered, can't simulate keypress |
| **L4** | Persistence, hooks | State not saved, transcript lost |
| **L5** | Evaluation, grading | Can't verify success, wrong pass/fail |
| **L6** | Orchestration, task management | Wrong task assignment, no coordination |
| **L7** | Asset generation | Bad 3D model, wrong texture |

---

## 4. Convoy Completion Checklist

Before marking ANY convoy as complete, the Mayor MUST verify ALL items.

### Pre-Completion Checklist

```
CONVOY COMPLETION VERIFICATION
==============================

[ ] 1. DELEGATION CHECK
      - All tasks assigned to appropriate Polecats
      - Mayor made <3 MCP calls between agent spawns
      - No direct implementation by Mayor

[ ] 2. AGENT SUCCESS CHECK
      - All spawned agents returned success status
      - No agent failures unaddressed
      - Retry attempts documented if any

[ ] 3. STATE VERIFICATION
      - Eyes Polecat spawned for final state check
      - Scene hierarchy matches expectations
      - No missing references detected
      - Component configurations valid

[ ] 4. CONSOLE CHECK
      - read_console executed after all changes
      - No NEW errors (compare to baseline)
      - No NEW warnings (or documented as acceptable)
      - Pre-existing issues noted separately

[ ] 5. PLAY MODE TESTING
      - Entered play mode via manage_editor
      - User-facing features exercised
      - Expected behaviors verified
      - Exited play mode cleanly

[ ] 6. PERSISTENCE CHECK
      - Relevant state saved to hooks/
      - Task status updated
      - Convoy status updated
      - Metrics recorded

[ ] 7. DOCUMENTATION
      - Any workarounds documented
      - Any blockers filed as GitHub issues
      - Any new problems added to ENGINE_PROBLEMS.md
```

### Checklist Failure Handling

```
IF any item unchecked:
  → DO NOT mark convoy complete
  → Address the failing item
  → Re-run full checklist

IF verification fails 3+ times:
  → Create GitHub blocker issue
  → Document in ENGINE_PROBLEMS.md
  → Escalate to user
  → Pause convoy pending resolution
```

### Quick Verification Commands

```bash
# Console check (run after changes)
read_console types=["error", "warning"] count=20

# State verification (Eyes Polecat task)
manage_scene action="get_hierarchy"

# Play mode test
manage_editor action="play"
# ... exercise features ...
manage_editor action="stop"

# Check for console changes
read_console types=["error", "warning"] count=20
```

---

## 5. Iteration Lifecycle

```
┌─────────────────────────────────────────────────────────────────┐
│  ITERATION START                                                 │
├─────────────────────────────────────────────────────────────────┤
│  1. Create Assets/Iteration{N}/ folder                          │
│  2. Write GDD.md in that folder                                 │
│  3. Parse GDD into task list                                    │
├─────────────────────────────────────────────────────────────────┤
│  DEVELOPMENT LOOP                                                │
├─────────────────────────────────────────────────────────────────┤
│  For each task:                                                  │
│    a. Delegate to appropriate Polecat (Mayor doesn't implement) │
│    b. Wait for agent completion                                 │
│    c. Spawn Eyes Polecat for verification                       │
│    d. Check console for new errors/warnings                     │
│    e. If blocker → GitHub Issue → Continue/Wait                 │
│    f. If success → Next task                                    │
├─────────────────────────────────────────────────────────────────┤
│  CONVOY COMPLETE                                                 │
├─────────────────────────────────────────────────────────────────┤
│  Run Convoy Completion Checklist (Section 4)                    │
│  ALL items must pass before marking complete                    │
├─────────────────────────────────────────────────────────────────┤
│  ITERATION COMPLETE                                              │
├─────────────────────────────────────────────────────────────────┤
│  1. All GDD success criteria checked                            │
│  2. All convoys passed completion checklist                     │
│  3. List of blockers encountered (GitHub issues)                │
│  4. Metrics: tasks completed, human interventions, time spent   │
│  5. Retrospective: what to improve for next iteration           │
└─────────────────────────────────────────────────────────────────┘
```

---

## 6. Metrics to Track Per Iteration

| Metric | Description |
|--------|-------------|
| **Tasks Total** | Number of tasks from GDD |
| **Tasks Completed Autonomously** | No human help needed |
| **Blockers Encountered** | GitHub issues created |
| **Human Interventions** | Times human had to act |
| **Layer Faults** | Count per layer |
| **Time to Completion** | From GDD to playable |

These metrics inform which layers need improvement.

---

## 7. What Layer 6 Must Eventually Do

When Layer 6 is implemented, it will automate this entire workflow:

1. **GDD Parser** - Extract tasks from markdown GDD
2. **Task Decomposition** - Break high-level tasks into polecat-sized work
3. **Agent Spawning** - Launch polecats via Task tool
4. **Progress Tracking** - Monitor hooks/ for completion
5. **Evaluation Orchestration** - Run graders after each task
6. **Blocker Detection** - Identify when agents are stuck
7. **Budget Enforcement** - Track tokens, API costs, time
8. **Rollback Triggers** - Revert on regression

Until then, Claude manually performs these steps.

---

## 8. Quick Reference: Starting a New Iteration

```bash
# 1. Create folder structure
mkdir -p Assets/Iteration{N}/Scripts
mkdir -p Assets/Iteration{N}/Scenes
mkdir -p Assets/Iteration{N}/Prefabs
mkdir -p Assets/Iteration{N}/UI

# 2. Create GDD
# Write Assets/Iteration{N}/GDD.md following template

# 3. Create hooks folder for this iteration
mkdir -p hooks/iterations/Iteration{N}

# 4. Begin development
# Claude parses GDD, creates tasks, executes
```

---

## 9. Agent Spawning Mechanism

### How to Spawn Agents

Agents are spawned using the **Task tool** with `subagent_type="general-purpose"`:

```
Task(
  description="Write Player.cs",
  prompt="Read .claude/agents/script-polecat.md for your role. Your task: Write Player.cs with movement logic...",
  subagent_type="general-purpose"
)
```

### Agent Prompt File Convention

Each agent has a definition file at `.claude/agents/{agent-name}.md`. When spawning:
1. Reference the agent file in the prompt
2. Provide the specific task
3. Include any relevant context (current state, constraints)

### Example Agent Spawns

```
# Script Polecat - Writing code
Task(
  description="Create score service",
  prompt="You are Script Polecat (.claude/agents/script-polecat.md).
         Create IScoreService interface and ScoreService implementation
         in Assets/Iteration1/Scripts/Services/",
  subagent_type="general-purpose"
)

# Scene Polecat - Modifying Unity scene
Task(
  description="Setup game scene",
  prompt="You are Scene Polecat (.claude/agents/scene-polecat.md).
         Create the main camera and directional light in Iteration1Scene.",
  subagent_type="general-purpose"
)

# Eyes Polecat - Verification
Task(
  description="Verify audio integration",
  prompt="You are Eyes Polecat (.claude/agents/eyes-polecat.md).
         Verify that AudioSource is properly configured on TargetClicker.
         Check for missing clip references.",
  subagent_type="general-purpose"
)
```

---

## 10. Hooks Directory Structure

### Canonical Layout

All persistent state is stored under `hooks/`:

```
hooks/
├── orchestration/              # Mayor/orchestration state
│   ├── budget.json             # Token/cost tracking
│   └── violations.json         # Discipline violations log
│
├── convoys/                    # Convoy status tracking
│   └── {convoyId}/
│       └── status.json         # Convoy progress and completion
│
├── tasks/                      # Individual task assignments
│   └── {taskId}/
│       └── assignment.json     # Task details and agent assignment
│
├── iterations/                 # Per-iteration state
│   └── Iteration{N}/
│       ├── state.json          # Iteration metadata
│       └── blockers/           # Blocker notes before GitHub issue
│
├── scenes/                     # Scene state snapshots
│   └── {SceneName}/
│       └── {timestamp}.json    # SceneStateCaptureService output
│
├── compiler/                   # Compilation state
│   └── latest.json             # Last compile result
│
├── tests/                      # Test results
│   └── latest.json             # Last test run results
│
├── validation/                 # Eyes Polecat verification results
│   └── {convoyId}/
│       └── verification.json   # Verification pass/fail details
│
├── assets/                     # Generated asset metadata
│   └── {assetId}.json          # Meshy/ElevenLabs asset records
│
└── messages/                   # Inter-agent communication
    └── {messageId}.json        # Cross-agent messages
```

### Usage Patterns

| Purpose | Path | Written By |
|---------|------|------------|
| Budget tracking | `hooks/orchestration/budget.json` | Mayor |
| Discipline violations | `hooks/orchestration/violations.json` | Mayor (self-monitoring) |
| Convoy status | `hooks/convoys/{id}/status.json` | Mayor |
| Task assignment | `hooks/tasks/{id}/assignment.json` | Mayor |
| Scene snapshots | `hooks/scenes/{name}/*.json` | SceneStateCaptureService |
| Verification results | `hooks/validation/{convoy}/verification.json` | Eyes Polecat |
| Asset records | `hooks/assets/{id}.json` | Asset Polecat |

# Neuro-Engine Orchestration Rules

**MANDATORY**: These rules apply to ALL Neuro-Engine agents and MUST be followed.

---

## Rule 0: Plan Decomposition BEFORE Acting

**CRITICAL**: Before spawning ANY implementation agent, the Mayor MUST:

### Phase 1: Analyze & Decompose (NO implementation yet)

```
1. READ the entire GDD thoroughly
2. IDENTIFY all deliverables (scripts, scenes, assets, audio)
3. GROUP deliverables by agent type:
   - script-polecat: All C# code (list every script needed)
   - scene-polecat: All scene setup, prefabs, materials
   - asset-polecat: All generated assets (3D, audio, textures)
4. IDENTIFY dependencies between groups
5. WRITE the breakdown to hooks/orchestration/task-breakdown.json
```

### Phase 2: Create Task Breakdown File

**MANDATORY**: Write `hooks/orchestration/task-breakdown.json` BEFORE spawning agents:

```json
{
  "iteration": "Iteration2",
  "gdd": "Assets/Iteration2/GDD.md",
  "created": "2026-01-22T17:00:00Z",
  "workstreams": {
    "code": {
      "agent": "script-polecat",
      "tasks": [
        {"id": "code-001", "name": "GameEvents.cs", "description": "Central event system"},
        {"id": "code-002", "name": "Ball.cs", "description": "Ball physics and collision"},
        {"id": "code-003", "name": "Paddle.cs", "description": "Paddle input and movement"}
      ],
      "dependencies": [],
      "estimated_files": 15
    },
    "scene": {
      "agent": "scene-polecat",
      "tasks": [
        {"id": "scene-001", "name": "Create scene", "description": "Camera, lights, boundaries"},
        {"id": "scene-002", "name": "Create prefabs", "description": "Ball, Paddle, Brick prefabs"}
      ],
      "dependencies": ["code"],
      "estimated_objects": 20
    },
    "audio": {
      "agent": "asset-polecat",
      "tasks": [
        {"id": "audio-001", "name": "paddle_hit.wav", "description": "Satisfying thwack"},
        {"id": "audio-002", "name": "brick_destroy.wav", "description": "Shatter sound"}
      ],
      "dependencies": [],
      "estimated_assets": 8
    }
  },
  "parallel_groups": [
    ["code", "audio"],
    ["scene"]
  ],
  "execution_order": [
    "Phase 1: code + audio (parallel)",
    "Phase 2: scene (after code compiles)",
    "Phase 3: integration + verification"
  ]
}
```

### Phase 3: Review Before Execution

Before spawning agents, verify:
- [ ] All GDD requirements mapped to tasks
- [ ] Dependencies correctly identified
- [ ] Parallel groups don't have hidden dependencies
- [ ] Task breakdown file written to hooks/

### Why This Matters

```
WITHOUT planning:
  - Start writing code
  - Realize mid-way that audio could run in parallel
  - Already wasted time on sequential work
  - No record of what was planned vs executed

WITH planning:
  - Full picture of all work upfront
  - Optimal parallelization identified
  - Persistent record for debugging/resumption
  - Can estimate completion and track progress
```

---

## Rule 0.5: Maximum Parallelization Granularity

**CRITICAL**: Spawn ONE agent PER deliverable, not one agent per category.

### WRONG (Coarse Parallelization)
```
Task 1: script-polecat → "Write all 15 scripts"
Task 2: asset-polecat → "Generate all 8 audio files"

Result: 2 agents running sequentially within themselves
```

### CORRECT (Fine Parallelization)
```
# Scripts (15 parallel agents)
Task 1: script-polecat → "Write GameEvents.cs"
Task 2: script-polecat → "Write Ball.cs"
Task 3: script-polecat → "Write Paddle.cs"
...
Task 15: script-polecat → "Write Iteration2LifetimeScope.cs"

# Audio (8 parallel agents)
Task 16: asset-polecat → "Generate paddle_hit.wav"
Task 17: asset-polecat → "Generate brick_destroy.wav"
...
Task 23: asset-polecat → "Generate launch.wav"

# 2D Sprites (5 parallel agents)
Task 24: asset-polecat → "Generate ball sprite"
Task 25: asset-polecat → "Generate paddle sprite"
...

Result: 25+ agents running TRULY in parallel
```

### Task Breakdown Must List Individual Files

The `hooks/orchestration/task-breakdown.json` must list EACH file as a separate task:

```json
{
  "workstreams": {
    "code": {
      "tasks": [
        {"id": "code-001", "file": "GameEvents.cs", "agent_instance": "separate"},
        {"id": "code-002", "file": "Ball.cs", "agent_instance": "separate"},
        ...
      ],
      "spawn_strategy": "one_agent_per_task"
    }
  }
}
```

### Don't Forget Asset Types

When analyzing a GDD, identify ALL asset types:
- Scripts (.cs files)
- Audio (.wav/.mp3 files)
- **2D Sprites** (ball, paddle, bricks, particles)
- **3D Models** (if applicable)
- **Textures** (if applicable)
- **Materials**
- **Prefabs**

Missing an entire asset category = major planning failure.

---

## Rule 0.6: Progress Monitor Agent

**REQUIRED**: Spawn a progress-polecat every ~30 seconds during execution.

### Purpose
The Progress Monitor continuously ensures:
1. Work aligns with GDD vision
2. Console has no new errors/warnings
3. Agents haven't gone off-track
4. Blockers are identified early

### Protocol
```
Every 30 seconds during active development:
  1. Spawn progress-polecat
  2. Agent checks:
     - Current files vs task-breakdown.json
     - Console for errors/warnings
     - Scene state vs GDD requirements
  3. Agent reports:
     - Tasks completed
     - Tasks in progress
     - Issues found
     - Recommended actions
  4. Mayor decides:
     - Continue as planned
     - Spawn fixer agents for issues
     - Adjust priorities
```

### What Progress Monitor Checks
- [ ] New compilation errors? → Spawn fixer
- [ ] New warnings? → Log and potentially fix
- [ ] Scripts match GDD requirements?
- [ ] Assets generated match GDD specs?
- [ ] Scene structure matches plan?
- [ ] Any agents stuck/failed?

---

## Rule 0.7: UI Toolkit Only (NO Legacy uGUI)

**CRITICAL**: All UI must use **UI Toolkit**, NOT legacy Unity GUI/uGUI.

### FORBIDDEN
```
- Canvas (uGUI)
- UnityEngine.UI namespace
- Button, Text, Image (uGUI components)
- EventSystem (uGUI)
- GraphicRaycaster
```

### REQUIRED
```
- UIDocument component
- .uxml files for structure
- .uss files for styling
- UnityEngine.UIElements namespace
- VisualElement, Label, Button (UI Toolkit)
```

### Why This Matters
- UI Toolkit is Unity's modern UI system
- Better performance, better styling
- Consistent with engine architecture
- Legacy uGUI causes compatibility issues

### Script Pattern for UI Toolkit
```csharp
using UnityEngine;
using UnityEngine.UIElements;

public class GameHUD : MonoBehaviour
{
    [SerializeField] private UIDocument uiDocument;

    private Label scoreLabel;
    private Label comboLabel;

    private void OnEnable()
    {
        var root = uiDocument.rootVisualElement;
        scoreLabel = root.Q<Label>("score-label");
        comboLabel = root.Q<Label>("combo-label");
    }
}
```

### Asset Structure
```
Assets/Iteration{N}/
├── UI/
│   ├── GameHUD.uxml       # UI structure
│   ├── GameHUD.uss        # UI styles
│   └── GameHUD.cs         # Code-behind
```

---

## Rule 0.8: No Modal Dialogs

**CRITICAL**: Agents must NEVER trigger operations that cause modal dialogs.

### FORBIDDEN Operations
```
- manage_scene action="save" without proper path → triggers Save As dialog
- Any operation requiring user confirmation
- EditorUtility.SaveFilePanel or similar
- Operations that block Unity Editor
```

### Safe Alternatives
```
# Instead of save without path:
manage_scene action="save" path="Assets/Iteration2/Scenes/MyScene.unity"

# Always specify full paths for asset creation
manage_asset action="create" path="Assets/Iteration2/Prefabs/Ball.prefab"

# Use manage_scene action="create" with explicit path
manage_scene action="create" path="Assets/Iteration2/Scenes/Iteration2Scene.unity"
```

### If Dialog Appears
If a modal dialog appears during agent execution:
1. Agent is BLOCKED - cannot continue
2. Human must intervene
3. Log as blocker: "Modal dialog triggered by {operation}"
4. Fix the operation to include explicit paths

### Scene-Polecat Must
- Always save scenes with explicit path
- Never rely on "current scene" being set
- Create scenes with full path specified
- Avoid any "Save As" operations

---

## Rule 1: The Mayor Never Implements

```
FORBIDDEN for Mayor/Orchestrator:
- Writing scripts directly (use script-polecat)
- Creating GameObjects directly (use scene-polecat)
- Generating assets directly (use asset-polecat)
- Making >3 consecutive MCP tool calls without spawning an agent

The Mayor DELEGATES. The Mayor does NOT do manual labor.
```

### What Counts as Implementation (FORBIDDEN)
- `mcp__unity-mcp__create_script`
- `mcp__unity-mcp__manage_gameobject` action="create"
- `mcp__unity-mcp__manage_asset` action="create"
- `mcp__unity-mcp__manage_material` action="create"
- Any Write/Edit to C# files
- Any asset generation API calls

### What Mayor CAN Do (ALLOWED)
- `mcp__unity-mcp__read_console` (observation)
- `mcp__unity-mcp__manage_scene` action="get_hierarchy" (observation)
- `mcp__unity-mcp__manage_editor` action="play/stop/pause" (control)
- Spawning agents via Task tool
- Reading files for planning
- Writing to hooks/ for persistence

---

## Rule 2: Parallel Agent Spawning

```
REQUIRED: When implementing a GDD, spawn agents IN PARALLEL for independent work.

Example - Building a game with code + audio + scene:

  CORRECT (parallel):
    Task 1: script-polecat → "Write all C# scripts"
    Task 2: asset-polecat → "Generate all audio"
    Task 3: scene-polecat → "Set up scene and prefabs"
    [All three run SIMULTANEOUSLY]

  WRONG (sequential):
    1. Write script A
    2. Write script B
    3. Generate audio
    4. Set up scene
    [Wastes time and tokens]
```

### Agent Assignment Matrix

| Task Type | Assign To | Verify With |
|-----------|-----------|-------------|
| Write C# code | script-polecat | code-reviewer-layers |
| Create/modify scene | scene-polecat | eyes-polecat |
| Generate 3D assets | asset-polecat (meshy skill) | evaluator |
| Generate audio | asset-polecat (elevenlabs skill) | evaluator |
| Generate textures | asset-polecat | evaluator |
| Verify integration | eyes-polecat | - |
| Grade quality | evaluator | - |
| Find bugs | game-tester | - |
| Fix bugs | game-fixer | eyes-polecat |

---

## Rule 3: Mandatory Verification

```
REQUIRED: After ANY integration task, spawn eyes-polecat to verify.

Integration tasks include:
- Wiring AudioSource to clips
- Connecting ParticleSystem to triggers
- Linking UI events
- Setting up input handlers
- Connecting dependency injection
- Any "wire up" or "connect" task
```

### Verification Protocol

```
1. Spawn eyes-polecat
   - Task: "Verify {integration_task.description}"

2. Enter Play Mode
   - manage_editor action="play"

3. Baseline Console
   - read_console to capture current state

4. Test Functionality
   - Simulate the interaction

5. Check Console
   - read_console again
   - NEW errors = FAIL

6. Exit Play Mode
   - manage_editor action="stop"

7. Report
   - PASS: Mark task complete
   - FAIL: Re-spawn implementation agent with context
```

---

## Rule 4: The 3 MCP Call Limit

```
VIOLATION: Making >3 MCP tool calls without spawning an agent.

Counter resets when:
- An agent is spawned via Task tool
- Counter reaches 0 after delegation

If you catch yourself making direct MCP calls:
  1. STOP immediately
  2. Count your recent MCP calls
  3. If >3: You violated discipline
  4. Identify which polecat should do this work
  5. Spawn that polecat
  6. Reset counter
```

### Exemptions (Don't Count Toward Limit)
- `read_console` (observation)
- `manage_scene` action="get_hierarchy" (observation)
- `manage_editor` action="play/stop/pause" (control)
- Reading MCP resources (not tools)

---

## Rule 5: No Self-Approval

```
REQUIRED: Agents never approve their own work.

Work flows through verification:
  script-polecat → code-reviewer-layers (for engine changes)
  script-polecat → eyes-polecat (for game code)
  scene-polecat → eyes-polecat
  asset-polecat → evaluator

  Evaluator results → Mayor (for final approval)
```

---

## Rule 6: Convoy Completion Checklist

Before marking ANY convoy complete:

```
[ ] All tasks delegated to agents (Mayor didn't implement)
[ ] All agents returned success status
[ ] Eyes Polecat verified final state
[ ] Console has no NEW errors
[ ] Console has no NEW warnings (or documented)
[ ] Features tested in play mode
[ ] State persisted to hooks/ if required

IF ANY UNCHECKED:
  → DO NOT mark complete
  → Address the failing item
  → Re-run verification
```

---

## Safety Controls

| Control | Limit | On Breach |
|---------|-------|-----------|
| Max iterations per task | 50 | Fail task, escalate |
| Max total iterations | 500 | Abort orchestration |
| Max cost per hour | $10 | Pause, alert user |
| Max parallel agents | 5 | Queue new tasks |
| Regression detected | - | Auto-rollback |

---

## Violation Logging

All violations MUST be logged to: `hooks/orchestration/violations.json`

```json
{
  "timestamp": "ISO-8601",
  "rule": "Rule N",
  "description": "What happened",
  "corrective_action": "What was done to fix",
  "convoy_id": "convoy-XXX",
  "task_id": "task-XXX"
}
```

---

## Quick Reference Card

```
+----------------------------------------------------------+
|              NEURO-ENGINE ORCHESTRATION                   |
+----------------------------------------------------------+
|                                                          |
|  MAYOR = DELEGATE ONLY (never implement directly)        |
|                                                          |
|  3 MCP CALLS MAX without spawning agent                  |
|                                                          |
|  PARALLELIZE independent agent tasks                     |
|                                                          |
|  VERIFY all integrations with eyes-polecat               |
|                                                          |
|  NO SELF-APPROVAL - work flows through verification      |
|                                                          |
|  COMPLETE only after checklist passes                    |
|                                                          |
+----------------------------------------------------------+
```

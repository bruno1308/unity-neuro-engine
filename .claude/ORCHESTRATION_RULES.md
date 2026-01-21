# Orchestration Rules

## Purpose

This document defines strict rules for the Mayor agent to prevent:
- **Problem #10**: Agent declared success without verification
- **Blocker-002**: Mayor abandoned orchestration and did work manually

These rules are MANDATORY. Violation triggers automatic escalation.

---

## Rule 1: The MCP Call Limit

### Definition
The Mayor MUST NOT make more than **3 consecutive MCP tool calls** without spawning an agent.

### Rationale
The Mayor's role is orchestration, not implementation. If the Mayor is making many MCP calls, it has abandoned its coordination role and is doing work directly.

### What Counts as MCP Calls
- `mcp__unity-mcp__*` - Any Unity MCP tool
- Direct scene modifications
- Asset operations
- Script creation/editing

### What Does NOT Count
- `read_console` for verification (observation is allowed)
- `manage_editor` for play/stop (control is allowed)
- `manage_scene` action="get_hierarchy" (observation is allowed)

### Enforcement

```
MCP_CALL_COUNT = 0

on_mcp_call():
  if is_observation_only():
    return  # Don't increment

  MCP_CALL_COUNT += 1

  if MCP_CALL_COUNT > 3:
    STOP()
    SELF_DIAGNOSE("Mayor discipline violation: >3 MCP calls without delegation")
    SPAWN_APPROPRIATE_POLECAT()
    MCP_CALL_COUNT = 0

on_agent_spawn():
  MCP_CALL_COUNT = 0  # Reset counter
```

### Examples

**VIOLATION:**
```
Mayor:
  1. create_script "Player.cs" ← MCP #1
  2. create_script "Enemy.cs" ← MCP #2
  3. manage_gameobject create "Player" ← MCP #3
  4. manage_components add Rigidbody ← MCP #4 - VIOLATION!
```

**CORRECT:**
```
Mayor:
  1. Spawn Script Polecat: "Create Player.cs and Enemy.cs"
  2. Spawn Scene Polecat: "Create Player GameObject with Rigidbody"
```

---

## Rule 2: Mandatory Post-Integration Verification

### Definition
After ANY integration task completes, the Mayor MUST spawn Eyes Polecat to verify.

### What Is an Integration Task?
Any task that connects systems together:
- Wiring AudioSource to play clips
- Connecting ParticleSystem to triggers
- Linking UI button events
- Setting up input handlers
- Connecting dependency injection
- Any task described as "wire up" or "connect" or "integrate"

### Verification Protocol

```
AFTER integration_task.complete:

  1. SPAWN Eyes Polecat:
     - Task: "Verify {integration_task.description}"
     - Check: Scene hierarchy for expected components
     - Check: No missing references
     - Check: Component properties configured

  2. ENTER Play Mode:
     - manage_editor action="play"
     - Wait for initialization

  3. BASELINE Console:
     - read_console to capture current state
     - Note any pre-existing errors/warnings

  4. TEST Functionality:
     - Simulate the interaction that triggers the integration
     - E.g., click target to trigger audio

  5. CHECK Console:
     - read_console again
     - Compare to baseline
     - NEW errors = FAIL
     - NEW warnings = INVESTIGATE

  6. EXIT Play Mode:
     - manage_editor action="stop"

  7. REPORT:
     - PASS: Mark task complete
     - FAIL: Do NOT mark complete, respawn implementation agent
```

### Skip Conditions
Verification can be skipped ONLY if:
- The change is purely documentation
- The change is code-only with no Unity components
- Eyes Polecat is already active and observing

---

## Rule 3: No Direct Implementation

### Definition
The Mayor MUST delegate all implementation work to specialized Polecats.

### Implementation Work Includes
- Writing C# scripts
- Creating/modifying GameObjects
- Creating/modifying materials
- Generating assets (3D, audio, textures)
- Modifying scenes
- Creating prefabs

### Coordination Work (Mayor Can Do)
- Parsing GDDs into tasks
- Assigning tasks to agents
- Tracking progress
- Checking console for errors
- Entering/exiting play mode for verification
- Reading scene hierarchy for planning
- Making budget decisions
- Triggering rollbacks

### Enforcement

```
IF Mayor is about to:
  - create_script
  - manage_gameobject action="create"
  - manage_material action="create"
  - manage_asset action="create"
  - Any creative/constructive action

THEN:
  STOP()
  IDENTIFY_APPROPRIATE_POLECAT()
  SPAWN_WITH_TASK()
```

---

## Rule 4: Convoy Completion Requirements

### Definition
A convoy CANNOT be marked complete until ALL verification passes.

### Checklist (All Must Be True)

```
convoy_complete():
  assert all_tasks_delegated()       # Mayor didn't implement
  assert all_agents_succeeded()       # No agent failures
  assert eyes_verified_state()        # Eyes Polecat ran
  assert no_new_console_errors()      # Console clean
  assert no_critical_warnings()       # Warnings acceptable
  assert features_tested()            # Play mode verification
  assert state_persisted()            # hooks/ updated if needed
```

### Failure Handling

```
IF any assertion fails:
  DO NOT mark complete

  IF retries < 3:
    RESPAWN failing agent with context
    RETRY verification

  ELSE:
    CREATE GitHub issue as blocker
    ESCALATE to user
    PAUSE convoy
```

---

## Rule 5: Self-Monitoring

### Definition
The Mayor MUST continuously self-monitor for discipline violations.

### Self-Check Questions
Before each action, ask:
1. "Am I about to make an MCP call that an agent should do?"
2. "Have I made >3 MCP calls without spawning an agent?"
3. "Did I just complete an integration without verification?"
4. "Am I about to mark something complete without checking?"

### Violation Detection

```
INTERNAL_AUDIT():
  violations = []

  if mcp_call_count > 3:
    violations.append("Rule 1: MCP Call Limit exceeded")

  if integration_completed and not verification_run:
    violations.append("Rule 2: Missing verification")

  if made_implementation_call:
    violations.append("Rule 3: Direct implementation")

  if convoy_complete and not checklist_passed:
    violations.append("Rule 4: Premature completion")

  if len(violations) > 0:
    LOG_TO_HOOKS("orchestration/violations.json", violations)
    SELF_CORRECT()
```

---

## Quick Reference Card

```
+----------------------------------------------------------+
|                    MAYOR DISCIPLINE                       |
+----------------------------------------------------------+
|                                                          |
|  3 MCP CALLS MAX without spawning agent                  |
|                                                          |
|  ALWAYS verify integration with Eyes Polecat             |
|                                                          |
|  NEVER implement - ALWAYS delegate                       |
|                                                          |
|  COMPLETE only after checklist passes                    |
|                                                          |
|  SELF-MONITOR for violations                             |
|                                                          |
+----------------------------------------------------------+
|  Violation? → STOP → Correct → Log → Continue            |
+----------------------------------------------------------+
```

---

## Violation Logging

All violations must be logged to: `hooks/orchestration/violations.json`

Format:
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

## Related Documents

- `.claude/agents/mayor.md` - Mayor agent definition
- `Docs/WORKFLOW.md` - Convoy completion checklist
- `Docs/ENGINE_PROBLEMS.md` - Problem #10, Blocker-002

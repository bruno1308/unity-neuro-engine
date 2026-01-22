---
description: Start or manage Mayor-led orchestration for autonomous game development
---

# /neuro:orchestrate - Agent Orchestration

You are managing the Mayor agent and polecat workers for autonomous development.

## Parse Arguments

The user invoked: `/neuro:orchestrate $ARGUMENTS`

Actions:
- `start <gdd-path>` - Parse GDD and begin autonomous development
- `status` - Show orchestration status
- `abort [reason]` - Stop all agents and optionally rollback

## Action: start

**WARNING:** This is resource-intensive. Confirm with user before proceeding.

### Procedure

**CRITICAL: Planning comes BEFORE any agent spawning.**

#### Phase 0: Planning & Decomposition

1. **Read GDD Thoroughly**
   - Read the entire GDD.md file
   - Understand all requirements, mechanics, and success criteria

2. **Decompose by Agent Type**
   - List ALL scripts needed → script-polecat workstream
   - List ALL scene/prefab work → scene-polecat workstream
   - List ALL generated assets → asset-polecat workstream
   - Identify dependencies between workstreams

3. **Write Task Breakdown File**
   - Create `hooks/orchestration/task-breakdown.json`
   - This file is MANDATORY before proceeding
   - Contains: workstreams, tasks, dependencies, parallel groups

4. **Review Plan**
   - Verify all GDD requirements mapped
   - Verify dependencies correct
   - Verify parallel groups identified

#### Phase 1: Parse GDD into Tasks

1. **Parse GDD**
   - Read the GDD.md file
   - Extract: game name, mechanics, controls, UI elements, success criteria
   - Break into discrete tasks

2. **Create Task List**
   Write to `hooks/tasks/`:
   ```json
   {
     "taskId": "task-001",
     "iteration": "Iteration1",
     "description": "Create IScoreService interface and implementation",
     "assignTo": "script-polecat",
     "dependencies": [],
     "status": "pending",
     "criteria": ["Interface exists", "Implementation registered in DI"]
   }
   ```

3. **Create Convoy**
   Group related tasks:
   ```json
   {
     "convoyId": "convoy-001",
     "name": "Score System",
     "tasks": ["task-001", "task-002", "task-003"],
     "status": "pending"
   }
   ```

4. **Spawn Mayor Agent**
   Use Task tool to spawn mayor agent with context:
   - Iteration path
   - Task list
   - Safety limits (max iterations, budget)

5. **Report**
   ```
   Orchestration started for Iteration1: Target Clicker

   Parsed 12 tasks in 4 convoys:
   - Convoy 1: Score System (3 tasks)
   - Convoy 2: Target Spawning (4 tasks)
   - Convoy 3: Input Handling (3 tasks)
   - Convoy 4: UI System (2 tasks)

   Mayor agent spawned. Monitoring via hooks/orchestration/
   ```

## Action: status

1. Read `hooks/orchestration/status.json`
2. Read active task statuses from `hooks/tasks/*/status.json`
3. Display:
   - Active agents
   - Current task
   - Convoy progress
   - Budget consumed
   - Any blockers

## Action: abort

1. Write abort signal to `hooks/orchestration/abort.json`
2. If `--rollback` flag or regression detected:
   - `git stash` current changes
   - Or `git checkout` to last good state
3. Log reason to `hooks/orchestration/aborts/{timestamp}.json`

## Safety Controls

| Control | Limit | On Breach |
|---------|-------|-----------|
| Max iterations per task | 50 | Fail task, escalate |
| Max total iterations | 500 | Abort orchestration |
| Max cost per hour | $10 | Pause, alert user |
| Regression detected | - | Auto-rollback |

## Agent Assignment Matrix

| Task Type | Agent |
|-----------|-------|
| Write C# interface | script-polecat |
| Write C# implementation | script-polecat |
| Create scene | scene-polecat |
| Create prefab | scene-polecat |
| Generate 3D model | asset-polecat |
| Generate texture | asset-polecat |
| Generate audio | asset-polecat |
| Verify state | eyes-polecat |
| Grade quality | evaluator |
| Find bugs | game-tester |
| Fix bugs | game-fixer |

## Note on Current Implementation

**Layer 6 is NOT fully implemented.** This command provides the structure, but actual agent spawning and coordination may require manual intervention. Document any blockers with `/neuro:blocker`.

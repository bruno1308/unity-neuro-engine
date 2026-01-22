---
name: status
description: Show current Neuro-Engine status including iteration progress, active tasks, blockers, and recent activity.
---

# Skill: status

## When to Use

Claude should invoke this skill proactively when:
- User asks "what's happening?", "where are we?", "progress?"
- User returns after being away ("I'm back", "continuing from yesterday")
- At the start of a session to restore context
- Before starting new work to understand current state
- User seems confused about current project state

## Context

The Neuro-Engine tracks state across sessions via the `hooks/` directory:
- `hooks/iterations/` - Per-iteration status and artifacts
- `hooks/tasks/` - Individual task progress
- `hooks/blockers/` - Open blockers
- `hooks/orchestration/` - Agent coordination state

## Procedure

### 1. Determine Current Iteration

Look for the most recently modified iteration:
- Glob `Assets/Iteration*/`
- Check modification times
- Or read `hooks/current-iteration.json` if exists

### 2. Gather Iteration Status

For the current iteration, collect:
- GDD name and description
- File counts (scripts, scenes, prefabs)
- Last evaluation results (if any)

### 3. Check Task Progress

Read from `hooks/tasks/`:
- Total tasks
- Completed tasks
- Current in-progress task
- Blocked tasks

### 4. Check Blockers

Read from `hooks/blockers/`:
- Open blocker count
- Brief description of each
- GitHub issue links

### 5. Check Recent Activity

Look at recent file modifications:
- Last modified scripts
- Last evaluation
- Last commit

## Output Format

```
## Neuro-Engine Status

### Current Iteration
**Iteration1: Target Clicker**
- Phase: Development
- GDD: Assets/Iteration1/GDD.md

### Progress
| Metric | Value |
|--------|-------|
| Tasks | 3/12 completed |
| Scripts | 5 files |
| Scenes | 1 scene |
| Last Eval | Tier 2 PASS (2 hours ago) |

### Active Work
Currently: Implementing TargetSpawnerService

### Blockers (1 open)
- #42: MCP cannot detect UI button state [L2, L3]

### Recent Activity
- 14:32 - Created ScoreService.cs
- 14:28 - Created IScoreService.cs
- 14:15 - Started Iteration1

### Quick Actions
- Continue with: TargetSpawnerService
- Or run: `/neuro:evaluate` to check progress
```

## When No State Exists

If hooks/ is empty or no iterations exist:

```
## Neuro-Engine Status

No active iterations found.

To get started:
1. Run `/neuro:iteration create "Game Name"` to create a new iteration
2. Edit the GDD.md with your game design
3. Start building!

Or read Docs/WORKFLOW.md for the full process.
```

## Persistence

After gathering status, optionally write a summary to:
`hooks/status-snapshots/{timestamp}.json`

This helps track progress over time.

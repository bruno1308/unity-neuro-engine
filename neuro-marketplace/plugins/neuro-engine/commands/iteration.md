---
description: Manage game iterations - create new iterations, check status, or list all
---

# /neuro:iteration - Iteration Management

You are managing game iterations for the Neuro-Engine. Each iteration is a separate game in `Assets/Iteration{N}/` with its own GDD.

## Parse Arguments

The user invoked: `/neuro:iteration $ARGUMENTS`

Parse the action from arguments:
- `create "<name>"` - Create new iteration
- `status [iteration]` - Show status (default: current)
- `list` - List all iterations

## Action: create

1. Find the next iteration number by listing `Assets/Iteration*/` directories
2. Create the folder structure:
   ```
   Assets/Iteration{N}/
   ├── Scripts/Services/
   ├── Scripts/Components/
   ├── Scenes/
   ├── Prefabs/
   ├── UI/
   └── GDD.md
   ```
3. Create `hooks/iterations/Iteration{N}/`
4. Create a GDD.md template with the given name
5. Report: "Created Iteration{N}: {name}"

## Action: status

1. Read `hooks/iterations/Iteration{N}/status.json` if exists
2. If not, scan the iteration folder for:
   - Scripts count
   - Scenes count
   - Whether GDD.md exists
   - Any blockers in hooks/blockers/ referencing this iteration
3. Display formatted status

## Action: list

1. Glob `Assets/Iteration*/GDD.md`
2. For each, extract:
   - Iteration number
   - Game name from GDD title
   - File counts (scripts, scenes)
3. Display as table

## Output Format

Always output structured, scannable information. Example:

```
## Iteration3: Doom Clone

**Status:** In Progress
**Tasks:** 7/23 completed
**Blockers:** 1 open (#42)
**Last Modified:** 2026-01-21

Files: 12 scripts, 3 scenes, 8 prefabs
```

---
name: unity-state
description: Query Unity Editor state via MCP. Get scene hierarchy, GameObject details, component values, and spatial analysis.
---

# Skill: unity-state

## When to Use

Claude should invoke this skill proactively when:
- User asks "what's in the scene?", "show me the objects"
- User asks about specific GameObjects or components
- Debugging why something isn't working in Unity
- Verifying that code changes were applied correctly
- Need to understand current game state for decision-making

## Prerequisites

- Unity Editor must be running
- Unity-MCP server must be connected
- Use `validate` skill first if unsure about connection

## Available MCP Tools

| Tool | Purpose | Layer |
|------|---------|-------|
| `manage_scene(action="get_hierarchy")` | Full scene tree | L2 |
| `find_gameobjects` | Search by name/tag/component | L2 |
| `CaptureWorldState` | Custom state snapshot | L2 |
| `GetUIAccessibilityGraph` | UI element tree | L2 |
| `AnalyzeSpatial` | Positions, bounds, overlaps | L2 |
| `ScanMissingReferences` | Find null refs | L2 |
| `read_console` | Unity console logs | L2 |

## Common Queries

### "What's in the scene?"

```
1. Call manage_scene(action="get_hierarchy", max_depth=3)
2. Summarize: root objects, counts by type
3. Highlight any issues (missing refs, disabled objects)
```

### "Tell me about [GameObject]"

```
1. Call find_gameobjects(search_term="Name", search_method="by_name")
2. Get instance ID
3. Call manage_gameobject with the ID to get components
4. Summarize: position, components, key property values
```

### "Is my change applied?"

```
1. Identify what should have changed (from conversation context)
2. Query the specific object/component
3. Compare expected vs actual values
4. Report match or discrepancy
```

### "Where is everything positioned?"

```
1. Call AnalyzeSpatial for the scene
2. Report: object positions, any off-screen, any overlapping
3. Visualize bounds if helpful
```

### "Are there any errors?"

```
1. Call read_console(types=["error", "warning"])
2. Call ScanMissingReferences
3. Report: errors, warnings, missing refs
4. Suggest fixes if patterns recognized
```

## Output Format

Keep it concise and actionable:

```
## Scene State: Iteration1Scene

### Hierarchy (12 objects)
- Main Camera (Camera)
- Directional Light (Light)
- GameManager (TargetSpawnerService, ScoreService)
- Canvas (UI Toolkit)
  - ScoreLabel
  - WinPanel (inactive)
- Target (3 instances)
  - Position: (2, 1, 5), (-1, 2, 5), (0, 0, 5)

### Issues Found
- WinPanel has missing reference on `scoreService` field

### Console
- 0 errors, 1 warning: "Score display not bound"
```

## Error Handling

If MCP not connected:
```
Cannot query Unity state - MCP not connected.

Options:
1. Start Unity and ensure MCP server is running
2. Work with code only (I can still read/write scripts)
3. Check .mcp.json configuration
```

## Layer Boundary

This skill is **observation only** (Layer 2). It does NOT:
- Modify GameObjects (use scene-polecat agent)
- Simulate input (use Layer 3 tools)
- Write files (use hooks-persist skill)

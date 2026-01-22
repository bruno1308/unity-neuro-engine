# Neuro-Engine Plugin

Autonomous AI-driven Unity game development plugin for Claude Code.

## Overview

The Neuro-Engine Plugin provides orchestration, validation, and evaluation tools for building Unity games from Game Design Documents (GDDs) with minimal human intervention.

---

## MANDATORY: Orchestration Rules

**When implementing a GDD, you MUST follow the orchestration rules in `ORCHESTRATION_RULES.md`.**

### Key Rules

1. **Mayor Never Implements** - Delegate to polecat agents, never use MCP tools directly
2. **3 MCP Call Limit** - Max 3 MCP calls before spawning an agent
3. **Parallel Execution** - Spawn independent agents simultaneously
4. **Mandatory Verification** - Eyes Polecat must verify all integrations
5. **No Self-Approval** - Work flows through verification agents

### Quick Start for GDD Implementation

```
# CORRECT: Use orchestration skill
/neuro-engine:orchestrate start Assets/Iteration1/GDD.md

# CORRECT: Manual Mayor pattern with parallel agents
Task 1: script-polecat → "Write all scripts"
Task 2: scene-polecat → "Set up scene"
Task 3: asset-polecat → "Generate audio"
[All run in parallel]

# WRONG: Direct implementation
mcp__unity-mcp__create_script (violation!)
mcp__unity-mcp__manage_gameobject (violation!)
```

### Enforcement

- **Hooks** warn on direct MCP tool use
- **Agent frontmatter** restricts tool access
- **Verification checklist** must pass before completion

---

## Installation

### From Local Path

```bash
claude --plugin-dir ./neuro-engine
```

### From Marketplace (Future)

```bash
/plugin install neuro-engine@neuro-engine-marketplace
```

## Commands

| Command | Description |
|---------|-------------|
| `/neuro:iteration create\|status\|list` | Manage game iterations |
| `/neuro:blocker "<description>"` | Create GitHub issue with layer attribution |
| `/neuro:evaluate [tier] [target]` | Run evaluation tiers (1-5) |
| `/neuro:orchestrate start\|status\|abort` | Start/manage Mayor orchestration |

### Examples

```bash
# Create a new iteration
/neuro:iteration create "Doom Clone"

# Check current status
/neuro:iteration status

# Report a blocker
/neuro:blocker "MCP cannot detect UI button clicks"

# Run full evaluation
/neuro:evaluate all

# Start autonomous development
/neuro:orchestrate start Assets/Iteration1/GDD.md
```

## Skills

Skills are invoked proactively by Claude when relevant:

| Skill | Trigger |
|-------|---------|
| `validate` | Before development work, after setup changes |
| `status` | When asking about progress, at session start |
| `unity-state` | When asking about scene state |
| `hooks-persist` | After completing tasks |
| `layer-review` | When modifying engine core |
| `meshy` | When creating 3D assets |
| `elevenlabs` | When creating audio |

## Agents

Agents are spawned via the Task tool for complex work:

| Agent | Role |
|-------|------|
| `mayor` | Orchestrates work, assigns tasks |
| `script-polecat` | Writes C# code |
| `scene-polecat` | Modifies scenes/prefabs |
| `asset-polecat` | Generates assets |
| `eyes-polecat` | Observes game state |
| `evaluator` | Grades quality |
| `game-tester` | Finds bugs |
| `game-fixer` | Fixes bugs |
| `code-reviewer-layers` | Reviews engine changes |

## Architecture

The plugin operates within the Neuro-Engine 7-layer architecture:

```
L7: Generative Assets    (meshy, elevenlabs skills)
L6: Agent Orchestration  (commands, agents)
L5: Evaluation           (evaluate command, evaluator agent)
L4: Persistence          (hooks-persist skill)
L3: Interaction          (scene-polecat agent)
L2: Observation          (unity-state skill, eyes-polecat)
L1: Code-First           (script-polecat agent)
```

## Hooks

The plugin includes automatic event handlers:

| Event | Action |
|-------|--------|
| `SessionStart` | Show status (context restoration) |
| `PreToolUse` (MCP) | Quick validation |
| `PostToolUse` (engine) | Layer review |
| `PostToolUse` (iteration) | Auto-persist |

## Configuration

### MCP Servers

The plugin requires Unity-MCP for Unity Editor interaction. Configure in `.mcp.json`:

```json
{
  "mcpServers": {
    "unity-mcp": {
      "command": "uvx",
      "args": ["--from", "mcpforunityserver", "mcp-for-unity", "--transport", "stdio"]
    }
  }
}
```

### Environment Variables

Set in project root `.env`:

```
MESHY_API_KEY=your_meshy_key      # For 3D generation
ELEVENLABS_API_KEY=your_key       # For audio generation
GEMINI_API_KEY=your_key           # For video analysis
```

## Workflow

1. **Create Iteration**: `/neuro:iteration create "Game Name"`
2. **Edit GDD**: Modify `Assets/Iteration{N}/GDD.md`
3. **Build**: Manually or via `/neuro:orchestrate start`
4. **Evaluate**: `/neuro:evaluate all`
5. **Report Blockers**: `/neuro:blocker "description"` when stuck
6. **Repeat**: Fix issues, re-evaluate

## Development

### Testing Locally

```bash
claude --plugin-dir ./neuro-engine
```

### Plugin Structure

```
neuro-engine/
├── .claude-plugin/plugin.json
├── commands/
├── skills/
├── agents/
├── hooks/hooks.json
├── .mcp.json
└── README.md
```

## Current Status

- **Layers 1-5**: Implemented (Unity runtime services)
- **Layer 6**: Plugin provides structure, full orchestration WIP
- **Layer 7**: Skills defined, API integration ready

## Contributing

When modifying engine core (`Packages/com.neuroengine.core/`):
1. `layer-review` skill will auto-trigger
2. Follow layer principles in `Docs/Architecture.md`
3. Run `/neuro:evaluate 1` before committing

## License

[Your License Here]

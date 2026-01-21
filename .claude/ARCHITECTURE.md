# Claude Skills & Agents Architecture

## Overview

The Neuro-Engine uses two types of specialized capabilities:

- **Skills**: Quick, inline operations for specific tasks (invoked via `/skill-name`)
- **Agents**: Autonomous workers for complex multi-step tasks (spawned via Task tool)

## Directory Structure

```
.claude/
├── ARCHITECTURE.md      # This file
├── settings.json        # Project settings
├── mcp.json            # MCP server config
├── skills/             # Skill definitions
│   ├── unity-package.md
│   ├── env-config.md
│   ├── hooks-write.md
│   ├── state-query.md
│   └── validation.md
└── agents/             # Agent prompts
    ├── script-polecat.md
    ├── scene-polecat.md
    ├── asset-polecat.md
    ├── eyes-polecat.md
    ├── evaluator.md
    └── mayor.md
```

## Skills (Quick Operations)

| Skill | Purpose | When to Use |
|-------|---------|-------------|
| `unity-package` | Add/remove Unity packages | Installing dependencies |
| `env-config` | Manage .env file | API key setup |
| `hooks-write` | Write to hooks/ directory | Persisting state |
| `state-query` | Query game state via MCP | Debugging, verification |
| `validation` | Run validation checks | Pre-flight checks |
| `meshy-generation` | Generate 3D assets via Meshy.ai | Creating models, textures |
| `review-layers` | Review engine layer changes | Before committing to neuroengine.core |

## Agents (Autonomous Workers)

| Agent | Role | Spawned For |
|-------|------|-------------|
| `script-polecat` | Write C# scripts | Code generation tasks |
| `scene-polecat` | Modify scenes/prefabs | Scene setup, prefab creation |
| `asset-polecat` | Generate assets | 3D models, textures, audio |
| `eyes-polecat` | Observe game state | Continuous monitoring |
| `evaluator` | Grade outcomes | Quality verification |
| `mayor` | Orchestrate work | Complex multi-agent tasks |
| `game-tester` | Find bugs, create issues | Automated E2E testing |
| `game-fixer` | Fix bugs from issues | Bug resolution |
| `code-reviewer-layers` | **Quality keeper** for engine layers | **REQUIRED** for all neuroengine.core changes |

### Tester/Fixer Workflow

```
game-tester → Finds bugs → Creates GitHub issues
                                    ↓
game-fixer  ← Reads issues ← Fixes bugs → Closes issues
```

This separation ensures:
- Clean audit trail in GitHub
- No mid-test code changes
- Parallel fix work possible
- Clear accountability

## Skill Format

Each skill file contains:
```markdown
# Skill: [name]

## Purpose
[What this skill does]

## When to Use
[Triggers for this skill]

## Context
[Domain knowledge, common problems, relevant ENGINE_PROBLEMS.md entries]

## Procedure
[Step-by-step instructions]

## Verification
[How to confirm success]
```

## Agent Format

Each agent file contains:
```markdown
# Agent: [name]

## Role
[Primary responsibility]

## Capabilities
[What tools/actions available]

## Context
[Domain knowledge, architecture layer, relevant docs]

## Known Problems
[Relevant ENGINE_PROBLEMS.md entries]

## Communication
[How to report progress, where to write results]

## Boundaries
[What NOT to do, when to escalate]
```

## Orchestration Flow

```
User Request
    │
    ▼
┌─────────────────┐
│  CLAUDE.md      │  (Minimal: points to this architecture)
│  Entry Point    │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Identify Task  │
│  Type           │
└────────┬────────┘
         │
    ┌────┴────┐
    │         │
    ▼         ▼
┌───────┐ ┌───────┐
│ Skill │ │ Agent │
│ Quick │ │ Spawn │
└───────┘ └───────┘
```

## Adding New Skills/Agents

1. Create new .md file in appropriate directory
2. Follow the format above
3. Add to this ARCHITECTURE.md table
4. If it addresses an ENGINE_PROBLEM, reference it in Context/Known Problems

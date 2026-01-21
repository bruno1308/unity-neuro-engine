# CLAUDE.md - Neuro-Engine Protocol

## Architecture

See `Docs/Architecture.md` for the 7-layer architecture we are trying to achieve.

## Quick Start

1. **Read** `.claude/ARCHITECTURE.md` for skills & agents overview
2. **Read** `Docs/ENGINE_PROBLEMS.md` for known issues to avoid
3. **Run** validation skill before any development work

## Autonomy Directive

```
BEFORE asking the user to do ANYTHING:
1. Try to do it yourself first
2. If blocked, research alternatives
3. If still blocked, build a tool to solve it
4. ONLY ask the user as an absolute last resort

NEVER say "please do X" when you could do X yourself.
```

## Refactoring Principle

```
PRIORITY #1: Robust and scalable framework

We are building foundations. NEVER avoid refactors out of fear of "introducing bugs."
- If code is duplicated → consolidate it properly
- If architecture is wrong → fix it now, not later
- If a pattern doesn't scale → replace it

Big refactors are EXPECTED and ENCOURAGED at this stage.
Technical debt compounds. Fix it immediately.
```


## Key Paths

| Path | Purpose |
|------|---------|
| `.claude/skills/` | Quick operations (use via skill name) |
| `.claude/agents/` | Autonomous workers (spawn via Task tool) |
| `hooks/` | Persistent memory across sessions |
| `Docs/` | Architecture and setup documentation |

## External Services

| Service | Purpose | Config |
|---------|---------|--------|
| Unity-MCP | AI ↔ Unity bridge | Port 8080 |
| Meshy.ai | 3D generation | `.env` |
| ElevenLabs | Audio generation | `.env` |
| Gemini | Video analysis | `.env` |

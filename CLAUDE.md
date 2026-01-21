# CLAUDE.md - Neuro-Engine Protocol

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

## Architecture

See `Docs/Final.md` for the 7-layer architecture.

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

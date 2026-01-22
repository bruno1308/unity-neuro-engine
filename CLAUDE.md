# CLAUDE.md - Neuro-Engine Protocol

---

## 🛑 STOP - READ THIS BEFORE EVERY ACTION 🛑

**Before making ANY MCP call or writing ANY code, ask yourself:**

1. **Am I about to do work that should be delegated to an agent?**
   - If implementing features → STOP → spawn script-polecat agents
   - If creating assets → STOP → spawn asset-polecat agents
   - If setting up scene → STOP → spawn scene-polecat agent
   - If wiring references → STOP → spawn scene-polecat agent

2. **Have I made more than 3 MCP calls without delegating?**
   - If YES → STOP → You are violating Mayor discipline
   - Spawn agents NOW for remaining work

3. **Am I doing tasks sequentially that could be parallel?**
   - If YES → STOP → Spawn multiple agents in ONE Task tool call

**FORBIDDEN PATTERNS:**
- ❌ "Let me wire up this reference..." (direct MCP call)
- ❌ "Let me create this prefab..." (direct MCP call)
- ❌ "Let me check this component..." then "Let me check that component..." (sequential)

**REQUIRED PATTERN:**
- ✅ "I need to wire up 5 references → spawning 5 scene-polecat agents in parallel"
- ✅ "I need to create assets → spawning asset-polecat agents"

---

## Architecture

See `Docs/Architecture.md` for the 7-layer architecture we are trying to achieve.

## Quick Start

1. **Read** `.claude/ARCHITECTURE.md` for skills & agents overview
2. **Read** `Docs/WORKFLOW.md` for iteration workflow and blocker handling
3. **Read** `Docs/ENGINE_PROBLEMS.md` for known issues to avoid
4. **Run** validation skill before any development work

---

## ⛔ MANDATORY: Orchestration for GDD Implementation

**See:** `neuro-engine/ORCHESTRATION_RULES.md` for full rules enforced by the plugin.

**Quick Summary:**
- Mayor DELEGATES to polecat agents (never implements directly)
- Max 3 MCP calls before spawning an agent
- Spawn agents IN PARALLEL for independent tasks
- Verify all integrations with eyes-polecat

**To implement a GDD:** `/neuro-engine:orchestrate start <gdd-path>`

---

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
| `Assets/Iteration{N}/` | Game iterations (each with GDD.md) |

## External Services

| Service | Purpose | Config |
|---------|---------|--------|
| Unity-MCP | AI ↔ Unity bridge | Port 8080 |
| Meshy.ai | 3D generation | `.env` |
| ElevenLabs | Audio generation | `.env` |
| Gemini | Video analysis | `.env` |

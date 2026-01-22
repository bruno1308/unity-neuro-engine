---
name: validate
description: Run pre-flight validation checks for Neuro-Engine development. Automatically verifies prerequisites, project structure, API keys, and MCP connectivity.
---

# Skill: validate

## When to Use

Claude should invoke this skill proactively when:
- User says "let's build...", "create...", "implement...", or "start working on..."
- User asks to start work on an iteration
- After modifying .env, package manifests, or engine configuration
- When errors, failures, or "it's not working" are mentioned
- Before any significant development task

## Context

The Neuro-Engine requires several prerequisites to function:
- Unity project structure with VContainer, UI Toolkit
- Unity-MCP connection for observation/interaction
- API keys for generative services (Meshy, ElevenLabs)
- Proper hooks/ directory structure for persistence

## Procedure

### 1. Check Project Structure

Verify these paths exist:
- `CLAUDE.md` - Entry point
- `.claude/` or `neuro-engine/` - Plugin/skills
- `Docs/Architecture.md` - Layer documentation
- `Docs/WORKFLOW.md` - Iteration workflow
- `hooks/` - Persistence directory
- `Packages/com.neuroengine.core/` - Engine package

### 2. Check Environment

Verify `.env` file:
- Exists at project root
- Contains non-placeholder values for:
  - `MESHY_API_KEY` (if asset generation needed)
  - `ELEVENLABS_API_KEY` (if audio generation needed)
  - `GEMINI_API_KEY` (if video analysis needed)

### 3. Check Unity Package Manifest

Read `Packages/manifest.json`:
- VContainer installed: `"jp.hadashikick.vcontainer"`
- Unity-MCP installed: `"com.coplaydev.unity-mcp"`

### 4. Check MCP Connectivity (if Unity should be running)

Try a simple MCP call:
- `mcp__unity-mcp__manage_editor` with action `telemetry_ping`
- If fails, report Unity not connected (not a blocker for code-only work)

### 5. Check Iteration Structure (if working on iteration)

For the target iteration:
- `Assets/Iteration{N}/GDD.md` exists
- `hooks/iterations/Iteration{N}/` exists
- Scripts folder structure in place

## Output

### All Checks Pass
```
Validation PASSED

Project structure: OK
Environment: OK
Unity packages: OK
MCP connection: OK (Unity running)
Iteration1: OK

Ready for development.
```

### Some Checks Fail
```
Validation ISSUES FOUND

Project structure: OK
Environment: WARN - MESHY_API_KEY is placeholder
Unity packages: OK
MCP connection: FAIL - Unity not running

Issues:
1. Set MESHY_API_KEY in .env if asset generation needed
2. Start Unity and ensure MCP server is running for interaction tests

Proceeding with code-only work...
```

## Auto-Fix Attempts

Before reporting failures, try to fix:
- Missing `hooks/` directories → Create them
- Missing iteration structure → Offer to create via `/neuro:iteration create`
- MCP not connected → Note but don't block (code can still be written)

## Known Issues

Reference `Docs/ENGINE_PROBLEMS.md` for common problems:
- Problem #4: MCP config location (`.mcp.json` in project root)
- Problem #5: HTTP vs stdio transport

# Neuro-Engine Setup Wizard

## Purpose

This document enables Claude Code to set up a new Neuro-Engine Protocol project from scratch.
Read this completely before starting any setup work.

---

## CRITICAL: Pre-Development Gate

**BEFORE writing ANY game code, Claude MUST verify the following setup is complete.**

```
┌─────────────────────────────────────────────────────────────────────────────┐
│  STOP! Do not proceed with game development until ALL checks pass.          │
│                                                                             │
│  Run this validation sequence:                                              │
│                                                                             │
│  □ 1. Prerequisites installed (node, python, git)                           │
│  □ 2. Unity packages present (VContainer, Unity-MCP)                        │
│  □ 3. Project structure exists (hooks/, .claude/, .env, CLAUDE.md)          │
│  □ 4. Skills & Agents present (.claude/skills/, .claude/agents/)            │
│  □ 5. API keys configured (non-placeholder values in .env)                  │
│  □ 6. MCP server responsive (localhost:8080/health returns OK)              │
│                                                                             │
│  If ANY check fails → Fix it first. Do NOT attempt workarounds.             │
│  If user asks to "just start coding" → Explain why setup must complete.     │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Validation Commands

Run these in sequence. ALL must succeed:

```bash
# 1. Prerequisites
node --version      # Expect: v18.x.x or higher
python --version    # Expect: Python 3.10+
git --version       # Expect: git version 2.25+

# 2. Check .env has real keys (not placeholders)
grep -v "your_" .env | grep -E "^(MESHY|ELEVENLABS|GEMINI)_API_KEY=.+"
# Expect: 3 lines with actual keys

# 3. Check project structure
ls -la hooks/ .claude/ CLAUDE.md .env
# Expect: All exist

# 4. Check skills & agents
ls .claude/skills/ .claude/agents/
# Expect: .md files in both directories

# 5. Test MCP connection (if server running)
curl -s http://localhost:8080/health
# Expect: OK or JSON response
```

### If Validation Fails

| Failed Check | Action |
|--------------|--------|
| Node.js missing | Ask user to install from nodejs.org |
| Python missing | Ask user to install from python.org |
| Git missing | Ask user to install from git-scm.com |
| .env missing | Run Phase 3 setup below |
| API keys are placeholders | Ask user for their actual API keys |
| hooks/ missing | Run Phase 1 setup below |
| skills/agents missing | Run Phase 2 setup below (create .claude/skills/ and .claude/agents/ with all .md files) |
| MCP not responding | Check McpAutoStart.cs exists, verify Unity-MCP installed, try NeuroEngine > MCP Auto-Start > Start Now |

**Do not proceed to game development until all 5 checks pass.**

---

## Prerequisites Checklist

Before setup, verify these are installed on the user's system:

| Prerequisite | Minimum Version | Check Command | Install Guide |
|--------------|-----------------|---------------|---------------|
| Unity | 2021.3 LTS+ (6000.x recommended) | Check Unity Hub | [unity.com/download](https://unity.com/download) |
| Node.js | 18.0+ | `node --version` | [nodejs.org](https://nodejs.org/) |
| Python | 3.10+ | `python --version` | [python.org](https://python.org/) |
| Git | 2.25+ | `git --version` | [git-scm.com](https://git-scm.com/) |

### Verification Script

Run these commands to verify prerequisites:

```bash
node --version    # Should output v18.x.x or higher
python --version  # Should output Python 3.10+
git --version     # Should output git version 2.25+
```

If any prerequisite is missing, **stop and ask the user to install it**.

---

## Setup Process

### Phase 1: Project Structure

Create these directories and files in the Unity project root:

```
{PROJECT_ROOT}/
├── .mcp.json              # MCP server config (Claude Code reads this)
├── .claude/
│   ├── ARCHITECTURE.md    # Skills/Agents architecture
│   ├── settings.json      # Project settings (engine use)
│   ├── skills/            # Skill definitions (quick operations)
│   │   ├── unity-package.md
│   │   ├── env-config.md
│   │   ├── hooks-write.md
│   │   ├── state-query.md
│   │   └── validation.md
│   └── agents/            # Agent prompts (autonomous workers)
│       ├── script-polecat.md
│       ├── scene-polecat.md
│       ├── asset-polecat.md
│       ├── eyes-polecat.md
│       ├── evaluator.md
│       └── mayor.md
├── .env                   # API keys (from .env.template)
├── .env.template          # Template for API keys
├── .gitignore             # Git ignore rules
├── CLAUDE.md              # Entry point (minimal, points to .claude/)
├── Docs/
│   ├── Wizard.md          # This file
│   ├── Architecture.md           # Architecture documentation
│   ├── ENGINE_PROBLEMS.md # Known issues requiring manual intervention
│   └── examples/          # Code examples
└── hooks/
    ├── README.md          # Hooks documentation
    ├── scenes/            # Scene state snapshots
    ├── tasks/             # Task tracking
    ├── convoys/           # Feature groupings
    ├── messages/          # Agent communication
    ├── validation/        # Error reports
    ├── snapshots/         # Project snapshots (gitignored)
    └── assets/            # Asset registry
```

### Phase 2: Skills & Agents

The engine uses specialized skills (quick operations) and agents (autonomous workers) for different task types.

#### Skills (in `.claude/skills/`)

| Skill | Purpose | Use When |
|-------|---------|----------|
| `unity-package` | Manage Unity packages | Adding/verifying dependencies |
| `env-config` | Manage .env configuration | Setting up API keys |
| `hooks-write` | Write to hooks/ directory | Persisting state |
| `state-query` | Query game state via MCP | Debugging, verification |
| `validation` | Run pre-flight checks | Before any development |

#### Agents (in `.claude/agents/`)

| Agent | Role | Spawned For |
|-------|------|-------------|
| `script-polecat` | Write C# scripts | Code generation |
| `scene-polecat` | Modify scenes/prefabs | Scene setup |
| `asset-polecat` | Generate assets | 3D, textures, audio |
| `eyes-polecat` | Observe game state | Monitoring |
| `evaluator` | Grade outcomes | Quality verification |
| `mayor` | Orchestrate work | Complex multi-agent tasks |

**Each file contains:**
- Purpose and capabilities
- Required context and architecture layer
- Known problems from ENGINE_PROBLEMS.md
- Communication patterns (where to write results)
- Boundaries (what NOT to do)

**See:** `.claude/ARCHITECTURE.md` for full documentation.

---

### Phase 3: Unity Packages

Install these packages via Unity Package Manager:

| Package | Source | Purpose |
|---------|--------|---------|
| VContainer | `https://github.com/hadashiA/VContainer.git?path=VContainer/Assets/VContainer` | Dependency Injection |
| Unity-MCP | `com.coplaydev.unity-mcp` via `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity` | AI ↔ Unity bridge |

**Installation Steps:**
1. Open Unity project
2. Window → Package Manager
3. Click "+" → "Add package from git URL"
4. Paste URL and click Add
5. Repeat for each package

### Phase 4: Configuration Files

#### .env.template

```env
# ═══════════════════════════════════════════════════════════════════════════════
# NEURO-ENGINE PROTOCOL - Environment Configuration
# ═══════════════════════════════════════════════════════════════════════════════

# === GENERATIVE ASSET APIs ===
MESHY_API_KEY=your_meshy_api_key_here
ELEVENLABS_API_KEY=your_elevenlabs_api_key_here

# === VISION LANGUAGE MODELS ===
GEMINI_API_KEY=your_gemini_api_key_here

# === UNITY COMMUNICATION ===
UNITY_MCP_PORT=8090

# === ORCHESTRATION ===
MAX_PARALLEL_AGENTS=5
MAX_TASK_ITERATIONS=50
MAX_API_COST_PER_HOUR=10.00

# === HOOKS ===
HOOKS_PATH=./hooks
HOOKS_GIT_ENABLED=true
```

#### .gitignore (Essential entries)

```gitignore
# Secrets
.env
*.env.local

# Unity
[Ll]ibrary/
[Tt]emp/
[Oo]bj/
[Ll]ogs/
[Uu]ser[Ss]ettings/
*.csproj
*.sln

# Hooks (partial)
hooks/snapshots/
hooks/screenshots/
hooks/recordings/

# IDE
.idea/
.vs/
.vscode/
```

#### .claude/settings.json

```json
{
  "project_name": "Neuro-Engine Project",
  "version": "0.1.0",
  "permissions": {
    "allow_file_creation": true,
    "allow_file_deletion": true,
    "allow_bash_commands": true,
    "allow_mcp_tools": true
  },
  "paths": {
    "hooks": "./hooks",
    "docs": "./Docs",
    "assets": "./Assets"
  },
  "unity": {
    "version": "6000.x",
    "mcp_port": 8090
  }
}
```

#### .claude/mcp.json

```json
{
  "mcpServers": {
    "unity": {
      "command": "python",
      "args": ["-m", "mcp_unity"],
      "env": {
        "UNITY_PORT": "8090"
      }
    }
  }
}
```

#### CLAUDE.md

```markdown
# CLAUDE.md - Neuro-Engine Project

## Overview
This is a Neuro-Engine Protocol project for autonomous Unity game development.

## Architecture
See `Docs/Architecture.md` for full architecture documentation.

## Key Directories
- `Docs/` - Architecture and examples
- `hooks/` - Persistent agent memory
- `Assets/` - Unity project assets

## External Services
| Service | Purpose | Env Var |
|---------|---------|---------|
| Meshy.ai | 3D generation | MESHY_API_KEY |
| ElevenLabs | Audio generation | ELEVENLABS_API_KEY |
| Gemini | Video analysis | GEMINI_API_KEY |
| Unity-MCP | AI ↔ Unity | Port 8090 |

## Development Rules
1. Use VContainer for dependency injection
2. All state must be queryable as JSON
3. Write progress to hooks/
4. Multi-tier verification (compile → test → visual)
```

### Phase 5: API Key Configuration

**Ask the user for their API keys:**

1. **Meshy.ai** - Get from https://www.meshy.ai/api
2. **ElevenLabs** - Get from https://elevenlabs.io/api
3. **Google Gemini** - Get from https://ai.google.dev/

Copy `.env.template` to `.env` and replace placeholder values with actual keys.

**Validation:** Keys should be non-empty strings, typically 30+ characters.

### Phase 6: Unity-MCP Activation

**Automated:** The MCP server starts automatically when Unity Editor loads.

This is handled by `Assets/Editor/NeuroEngine/McpAutoStart.cs` which:
- Uses `[InitializeOnLoad]` to run on domain reload
- Checks if Unity-MCP package is installed
- Starts the server if not already running
- Can be toggled via `NeuroEngine > MCP Auto-Start` menu

**Manual fallback (if auto-start fails):**
1. Window → MCP for Unity
2. Click "Start Server"
3. Verify status shows "Connected"

### Phase 7: Validation

Run these checks to verify setup:

| Check | How to Verify | Expected Result |
|-------|---------------|-----------------|
| VContainer installed | Open Unity, no compile errors referencing VContainer | ✓ Compiles |
| Unity-MCP installed | Window → MCP for Unity menu exists | ✓ Menu visible |
| MCP server running | MCP window shows "Connected" | ✓ Connected |
| .env exists | `cat .env` shows API keys (not placeholders) | ✓ Keys present |
| hooks/ structure | `ls hooks/` shows subdirectories | ✓ Dirs exist |

---

## Troubleshooting

### "Package not found" in Package Manager
- Ensure git URL is exact (no trailing spaces)
- Check internet connection
- Try: Window → Package Manager → Clear Cache

### MCP server won't start
- Check Python is installed: `python --version`
- Install mcp_unity: `pip install mcp-unity`
- Check port 8090 is not in use

### Unity compile errors after package install
- Let Unity fully import packages (may take 1-2 minutes)
- If errors persist, restart Unity

---

## Post-Setup

After setup is complete:
1. The project is ready for AI-driven development
2. GDDs go in `Docs/GDD/` directory
3. Use Claude to build games from GDDs
4. Monitor progress in `hooks/tasks/`

---

## Session Start Protocol

**When starting a new Claude session on this project, run this validation first:**

```
I'm starting work on a Neuro-Engine project. Before proceeding, I need to verify the setup is complete.

Let me check:
1. Prerequisites: node --version, python --version, git --version
2. Project structure: hooks/, .claude/, CLAUDE.md, .env exist
3. API keys: .env contains non-placeholder keys for MESHY, ELEVENLABS, GEMINI
4. Unity packages: Check Packages/manifest.json for VContainer and unity-mcp
5. MCP connection: Test localhost:8090/health if MCP server should be running

I will NOT proceed with any game development until all checks pass.
```

### Quick Validation Script

Save this as `validate-setup.sh` in project root for quick checks:

```bash
#!/bin/bash
echo "=== Neuro-Engine Setup Validation ==="

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
NC='\033[0m'

PASS=0
FAIL=0

check() {
    if [ $1 -eq 0 ]; then
        echo -e "${GREEN}✓${NC} $2"
        ((PASS++))
    else
        echo -e "${RED}✗${NC} $2"
        ((FAIL++))
    fi
}

# Prerequisites
node --version > /dev/null 2>&1
check $? "Node.js installed"

python --version > /dev/null 2>&1
check $? "Python installed"

git --version > /dev/null 2>&1
check $? "Git installed"

# Structure
[ -d "hooks" ]
check $? "hooks/ directory exists"

[ -d ".claude" ]
check $? ".claude/ directory exists"

[ -f ".env" ]
check $? ".env file exists"

[ -f "CLAUDE.md" ]
check $? "CLAUDE.md exists"

# API Keys (check not placeholders)
if [ -f ".env" ]; then
    grep -q "MESHY_API_KEY=your_" .env
    [ $? -ne 0 ]
    check $? "Meshy API key configured"

    grep -q "ELEVENLABS_API_KEY=your_" .env
    [ $? -ne 0 ]
    check $? "ElevenLabs API key configured"

    grep -q "GEMINI_API_KEY=your_" .env
    [ $? -ne 0 ]
    check $? "Gemini API key configured"
fi

echo ""
echo "=== Results: $PASS passed, $FAIL failed ==="

if [ $FAIL -gt 0 ]; then
    echo -e "${RED}Setup incomplete. Fix issues before proceeding.${NC}"
    exit 1
else
    echo -e "${GREEN}Setup complete! Ready for development.${NC}"
    exit 0
fi
```

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 0.1.0 | 2026-01 | Initial setup wizard |
| 0.1.1 | 2026-01 | Added pre-development gate and session validation |
| 0.2.0 | 2026-01 | Added Skills & Agents architecture (Phase 2) |

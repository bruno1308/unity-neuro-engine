# Neuro-Engine Development Plan

**Last Updated:** 2026-01-21 (Session 3 - In Progress)

---

## Session 3 Summary

### Architecture Fix ✅
**Critical change:** Removed Unity C# services for external APIs (Meshy, ElevenLabs).
These are now accessed via Claude skills (direct HTTP calls) to enable parallelization.

| Removed from Unity | Replaced With |
|--------------------|---------------|
| `MeshyClientService.cs` | `.claude/skills/meshy-generation.md` |
| `ElevenLabsClientService.cs` | `.claude/skills/elevenlabs.md` |
| `IMeshyClient.cs` | Direct HTTP via skill |
| `IElevenLabsClient.cs` | Direct HTTP via skill |

**Why:** Unity scripts block parallelization. Skills/agents can make HTTP calls
concurrently, enabling multiple assets to generate simultaneously.

### What Stays in Unity (local operations only)
- `EnvConfigService` - reads local .env file
- `HooksWriterService` - writes to local filesystem
- `SceneStateCaptureService` - captures Unity scene state
- `IGameEventProvider` - interface for games to report events

### Session Status
- [x] Removed incorrect Unity HTTP services
- [x] Created `elevenlabs.md` skill
- [x] Updated `NeuroEngineLifetimeScope` (removed Layer 7 registrations)
- [x] Updated `ServiceTests.cs` (removed Meshy test)
- [ ] Verify Unity compiles (refresh Unity on next session start)

---

## Session 2 Summary

### MCP Connection ✅
- Instance: `Engine@971db8c5` on port 6400 (stdio transport)
- Unity Version: 6000.2.6f2
- Tools discovered: 22 (18 built-in + 4 NeuroEngine)

### New MCP Tools Added
| Tool | Purpose |
|------|---------|
| `get_ui_state` | Query UI Toolkit screens/buttons/fields |
| `click_ui_button` | Click UI buttons by name |
| `set_text_field` | Enter text into UI fields |
| `get_game_events` | Poll game events (requires IGameEventProvider) |

### New Interfaces Added
| Interface | Purpose |
|-----------|---------|
| `IGameEventProvider` | Games implement for AI event notifications |
| `GameEvent` | Event structure with priority levels |
| `GameStateSnapshot` | Current game state for AI context |

### New Agents/Skills Added
| File | Purpose |
|------|---------|
| `agents/game-tester.md` | Find bugs → Create GitHub issues |
| `agents/game-fixer.md` | Read issues → Fix bugs → Close issues |
| `skills/meshy-generation.md` | Detailed Meshy.ai workflow (direct HTTP) |
| `skills/elevenlabs.md` | Audio generation (direct HTTP) |

### API Key Status
- Meshy: ✅ Valid
- ElevenLabs: ✅ Sound effects work, add `text_to_speech` permission if you need TTS
- Gemini: ✅ Valid

---

## Next Steps (Priority Order)

### 0. Resume Checklist (Start of Next Session)
- [ ] Open Unity and verify it compiles (removed 4 files in Session 3)
- [ ] Run `/mcp` to verify MCP connection
- [ ] Check console for errors

### 1. Test Runtime Services ✅
- [x] Test `SceneStateCaptureService.CaptureScene()` - Verified
- [x] Test `HooksWriterService.Write()` - Verified
- [x] Test Meshy API via skill (direct HTTP) - API key valid

### 2. ElevenLabs Skill ✅
- [x] Created `.claude/skills/elevenlabs.md` with full API documentation

### 3. Build Sample Game (NEXT PRIORITY)
Create a minimal game to validate the full pipeline:
- [ ] Simple scene with interactable objects
- [ ] UI with buttons and text fields
- [ ] Implement `IGameEventProvider` example
- [ ] Test game-tester/game-fixer workflow

### 4. Complete Layer 2 (Observation)
The `IStateProvider` interface exists but has no implementation.
- [ ] Implement `GameStateProvider` that games can extend
- [ ] Connect to `get_game_events` MCP tool

### 5. Documentation
- [ ] Update ENGINE_PROBLEMS.md with any new issues
- [ ] Create getting-started guide for external projects
- [ ] Document MCP tool usage patterns

---

## What Has Been Built

### Documentation
| File | Purpose | Status |
|------|---------|--------|
| `Docs/Final.md` | 7-layer architecture specification | ✅ Complete |
| `Docs/Wizard.md` | Setup guide for new projects | ✅ Complete |
| `Docs/ENGINE_PROBLEMS.md` | Known issues and solutions | ✅ Ongoing |
| `CLAUDE.md` | Entry point for Claude sessions | ✅ Complete |
| `.claude/ARCHITECTURE.md` | Skills/Agents architecture | ✅ Complete |

### Skills (`.claude/skills/`)
| Skill | Purpose | Status |
|-------|---------|--------|
| `unity-package.md` | Manage Unity packages | ✅ Defined |
| `env-config.md` | Manage .env configuration | ✅ Defined |
| `hooks-write.md` | Write to hooks/ directory | ✅ Defined |
| `state-query.md` | Query game state via MCP | ✅ Defined |
| `validation.md` | Run pre-flight checks | ✅ Defined |
| `meshy-generation.md` | 3D asset generation via Meshy.ai (direct HTTP) | ✅ Defined |
| `elevenlabs.md` | Audio generation via ElevenLabs (direct HTTP) | ✅ Defined |

### Agents (`.claude/agents/`)
| Agent | Role | Status |
|-------|------|--------|
| `script-polecat.md` | Write C# scripts | ✅ Defined |
| `scene-polecat.md` | Modify scenes/prefabs | ✅ Defined |
| `asset-polecat.md` | Generate assets (Meshy, ElevenLabs) | ✅ Defined |
| `eyes-polecat.md` | Observe game state | ✅ Defined |
| `evaluator.md` | Grade outcomes | ✅ Defined |
| `mayor.md` | Orchestrate work | ✅ Defined |
| `game-tester.md` | Find bugs, create GitHub issues | ✅ Defined |
| `game-fixer.md` | Fix bugs from GitHub issues | ✅ Defined |

### Project Structure
```
Engine/
├── .mcp.json                    # ✅ MCP server config (stdio mode)
├── .env                         # ✅ API keys configured
├── .gitignore                   # ✅ Configured
├── CLAUDE.md                    # ✅ Entry point
├── PLAN.md                      # ✅ This file
├── .claude/
│   ├── ARCHITECTURE.md          # ✅ Skills/Agents docs
│   ├── settings.json            # ✅ Project settings
│   ├── skills/                  # ✅ 5 skills defined
│   └── agents/                  # ✅ 6 agents defined
├── Docs/
│   ├── Final.md                 # ✅ Architecture
│   ├── Wizard.md                # ✅ Setup guide
│   └── ENGINE_PROBLEMS.md       # ✅ Known issues
├── hooks/                       # ✅ Directory structure
│   ├── scenes/
│   ├── tasks/
│   ├── convoys/
│   ├── messages/
│   ├── validation/
│   ├── assets/
│   └── snapshots/
└── Packages/
    └── com.neuroengine.core/    # ✅ Engine package
```

### Engine Package (`Packages/com.neuroengine.core/`)

**Runtime Components:**
| Component | Layer | Status |
|-----------|-------|--------|
| `IEnvConfig` / `EnvConfigService` | 1 - Config | ✅ Built |
| `IStateProvider` | 2 - Observation | ✅ Interface only |
| `ISceneStateCapture` / `SceneStateCaptureService` | 2 - Observation | ✅ Built |
| `IGameEventProvider` / `GameEvent` | 2 - Observation | ✅ Built (Session 2) |
| `IHooksWriter` / `HooksWriterService` | 4 - Persistence | ✅ Built |
| `NeuroEngineLifetimeScope` | VContainer | ✅ Built |

**Layer 7 - Generative Assets (via Skills, not Unity services):**
| Skill | Purpose | Status |
|-------|---------|--------|
| `meshy-generation.md` | 3D asset generation via Meshy API | ✅ Built |
| `elevenlabs.md` | Audio generation via ElevenLabs API | ✅ Built |

> **Architecture Note:** Meshy and ElevenLabs are accessed via Claude skills (direct HTTP calls),
> NOT Unity C# services. This enables parallelization across multiple agents.

**Editor MCP Tools:**
| Tool | Purpose | Status |
|------|---------|--------|
| `McpAutoStart` | Auto-configure stdio mode | ✅ Built |
| `GetUIState` | Query UI Toolkit screens | ✅ Built (Session 2) |
| `ClickUIButton` | Click UI buttons | ✅ Built (Session 2) |
| `SetTextField` | Enter text in UI fields | ✅ Built (Session 2) |
| `GetGameEvents` | Poll game events | ✅ Built (Session 2) |

### Unity Packages Installed
| Package | Status |
|---------|--------|
| VContainer | ✅ In manifest |
| Unity-MCP (com.coplaydev.unity-mcp) | ✅ In manifest |

---

## What Needs Validation

### Immediate (Before Proceeding)
| Item | How to Validate | Status |
|------|-----------------|--------|
| MCP connection | Run `/mcp` after Claude Code restart | ✅ Connected (`Engine@971db8c5` port 6400) |
| Unity-MCP package compiles | Open Unity, check console for errors | ✅ Compiles (0 errors) |
| Engine package compiles | Open Unity, check console for errors | ✅ Compiles (0 errors) |
| API keys work | Test Meshy/ElevenLabs endpoints | ✅ Meshy valid, ✅ ElevenLabs SFX works |

### After MCP Works
| Item | How to Validate | Status |
|------|-----------------|--------|
| MCP can query Unity scene | Use MCP tools to list GameObjects | ✅ Verified (Main Camera, Global Light 2D) |
| SceneStateCaptureService works | Call CaptureScene(), check JSON output | ✅ Verified |
| HooksWriterService works | Write test file to hooks/, verify exists | ✅ Verified |
| Meshy API key | Test via skill (direct HTTP) | ✅ Valid |
| ElevenLabs sound effects | Test via skill (direct HTTP) | ✅ Works |
| ElevenLabs TTS | Test via skill (direct HTTP) | ⚠️ Add `text_to_speech` permission if needed |
| Gemini API key | List models | ✅ Valid |

---

## What's Next to Build

### Priority 1: Complete Layer 2 (Observation)
| Component | Purpose | Complexity |
|-----------|---------|------------|
| Missing reference detector | Find null serialized fields | Medium |
| UI accessibility graph | DOM-like UI structure | Medium |
| Spatial analysis | Off-screen objects, overlaps | Medium |
| Validation rules engine | YAML-based rules | High |

### Priority 2: Layer 5 (Evaluation)
| Component | Purpose | Complexity |
|-----------|---------|------------|
| Tier 1 grader | Compilation check | Low |
| Tier 2 grader | State assertions | Medium |
| Tier 3 grader | Behavioral tests | High |
| Tier 4 grader | VLM image analysis (Claude) | Medium |
| Gemini client | Video analysis | Medium |

### Priority 3: Layer 3 (Interaction)
| Component | Purpose | Complexity |
|-----------|---------|------------|
| Input simulation | Keyboard/mouse injection | Medium |
| Automated playtest runner | Execute test sequences | High |

### Priority 4: Layer 6 (Orchestration)
| Component | Purpose | Complexity |
|-----------|---------|------------|
| Budget tracker | Token/API cost limits | Low |
| Safety controls | Max iterations, rollback | Medium |
| Convoy manager | Task grouping | Medium |

### Priority 5: Test Game
| Item | Purpose |
|------|---------|
| Breakout GDD | Document game requirements |
| One-shot trial | Test engine with real game |
| Measure metrics | pass@k, time, human interventions |

---

## Known Issues (Summary)

See `Docs/ENGINE_PROBLEMS.md` for full details.

| # | Issue | Resolution |
|---|-------|------------|
| 1 | Claude asked instead of acting | Added autonomy directive |
| 2 | Package name mismatch | Use exact name from package.json |
| 3 | MCP confirmation dialog | Bypass via direct process launch |
| 4 | MCP config wrong location | Use `.mcp.json` in project root |
| 5 | MCP HTTP mode needs Unity first | Use stdio mode instead |
| 6 | Claude keeps asking | Reinforce at multiple points |

---

## Session Continuity

When starting a new session:
1. Read `CLAUDE.md` (entry point)
2. Read `PLAN.md` (this file) for current state
3. Read `Docs/ENGINE_PROBLEMS.md` for pitfalls
4. Run `/mcp` to verify MCP connection
5. Check Unity console for compile errors
6. Continue from "What's Next to Build"

---

## Commands Reference

```bash
# Verify MCP
/mcp

# Test MCP server manually
curl http://localhost:8080/health

# Check Unity packages
grep -E "vcontainer|unity-mcp" Packages/manifest.json

# Check API keys configured
grep -v "your_" .env | grep -E "API_KEY"
```

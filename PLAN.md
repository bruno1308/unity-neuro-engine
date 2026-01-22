# Neuro-Engine Development Plan

**Last Updated:** 2026-01-22 (Post-Iteration 1)

---

## Session 4 Summary (Iteration 1 Complete)

### Iteration 1: TargetClicker ✅
Built and tested "Target Clicker" - a simple game to validate Layers 1-5.
- 15 tasks across 7 convoys completed
- Full orchestration system tested
- Game reached win state (10 targets clicked)
- Screenshots captured at multiple stages
- Iteration files cleaned up (gitignored) after completion

### All 7 Layers Implemented ✅
The architecture described in `Docs/Architecture.md` is now fully implemented:
- **Layer 1:** DI, UI Toolkit, Scene State
- **Layer 2:** Missing refs, UI accessibility, spatial analysis, validation rules
- **Layer 3:** Input simulation, UI interaction, playtesting
- **Layer 4:** Hooks persistence, transcripts, task management
- **Layer 5:** Syntactic/State/Behavioral/Visual/Polish/Quality graders
- **Layer 6:** Orchestration, safety controls, convoys
- **Layer 7:** Meshy (3D), ElevenLabs (audio), asset registry, style guide

### Session Status
- [x] All layers implemented and compiling
- [x] MCP connection verified (Engine@971db8c5)
- [x] Sample game built and tested
- [x] Orchestration system validated
- [x] Ready for Iteration 2

---

## Previous Sessions Summary

### Session 3: Architecture Cleanup
- Removed Unity C# HTTP services for Meshy/ElevenLabs (moved to skills)
- Created `elevenlabs.md` skill for audio generation

### Session 2: MCP Integration
- Instance: `Engine@971db8c5` on port 6400 (stdio transport)
- Unity Version: 6000.2.6f2
- API Keys verified: Meshy ✅, ElevenLabs ✅, Gemini ✅

---

## What's Next: Iteration 2

### Iteration 2 Planning
- [ ] Define game concept for Iteration 2 (more complex than TargetClicker)
- [ ] Create GDD.md for new game
- [ ] Set up Assets/Iteration2/ folder structure
- [ ] Test orchestration with Mayor agent
- [ ] Validate Layer 7 asset generation (Meshy, ElevenLabs) in real workflow

### Documentation Improvements
- [ ] Document lessons learned from Iteration 1
- [ ] Update ENGINE_PROBLEMS.md with any new issues
- [ ] Create getting-started guide for external projects

---

## What Has Been Built

### Documentation
| File | Purpose | Status |
|------|---------|--------|
| `Docs/Architecture.md` | 7-layer architecture specification | ✅ Complete |
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
| `review-layers.md` | Review code for layer violations | ✅ Defined |

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
| `code-reviewer-layers.md` | Review code for layer compliance | ✅ Defined |

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
│   ├── Architecture.md                 # ✅ Architecture
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

**Layer 1 - Foundation:**
| Component | Purpose | Status |
|-----------|---------|--------|
| `IEnvConfig` / `EnvConfigService` | Read .env configuration | ✅ Built |
| `ISceneStateCapture` / `SceneStateCaptureService` | Capture scene as JSON | ✅ Built |
| `NeuroEngineLifetimeScope` | VContainer DI setup | ✅ Built |

**Layer 2 - Observation:**
| Component | Purpose | Status |
|-----------|---------|--------|
| `IStateProvider` | Game state interface | ✅ Interface |
| `IGameEventProvider` | Game events interface | ✅ Built |
| `IMissingReferenceDetector` / `MissingReferenceDetector` | Find null refs | ✅ Built |
| `IUIAccessibility` / `UIAccessibilityService` | UI DOM tree | ✅ Built |
| `ISpatialAnalysis` / `SpatialAnalysisService` | Spatial queries | ✅ Built |
| `IValidationRules` / `ValidationRulesEngine` | YAML validation rules | ✅ Built |

**Layer 3 - Interaction:**
| Component | Purpose | Status |
|-----------|---------|--------|
| `IInputSimulation` / `InputSimulationService` | Inject input events | ✅ Built |

**Layer 4 - Persistence:**
| Component | Purpose | Status |
|-----------|---------|--------|
| `IHooksWriter` / `HooksWriterService` | Write to hooks/ dir | ✅ Built |
| `ITranscriptWriter` / `TranscriptWriterService` | Log transcripts | ✅ Built |
| `ITaskManager` / `TaskManagerService` | Manage tasks | ✅ Built |

**Layer 5 - Evaluation:**
| Component | Purpose | Status |
|-----------|---------|--------|
| `IEvaluation` (interface) | Evaluation contracts | ✅ Built |
| `SyntacticGraderService` | Tier 1: Compilation | ✅ Built |
| `StateGraderService` | Tier 2: State assertions | ✅ Built |
| `BehavioralGraderService` | Tier 3: Behavior tests | ✅ Built |
| `VisualGraderService` | Tier 4: VLM analysis | ✅ Built |
| `PolishGraderService` | Tier 5: Polish metrics | ✅ Built |
| `QualityGraderService` | Tier 6: Quality score | ✅ Built |
| `EvaluationRunnerService` | Run all tiers | ✅ Built |

**Layer 6 - Orchestration:**
| Component | Purpose | Status |
|-----------|---------|--------|
| `IOrchestration` / `OrchestrationService` | Task orchestration | ✅ Built |
| `ISafetyControl` / `SafetyControlService` | Safety limits | ✅ Built |
| `ConvoyService` | Convoy management | ✅ Built |

**Layer 7 - Generative:**
| Component | Purpose | Status |
|-----------|---------|--------|
| `IMeshyService` / `MeshyService` | 3D generation | ✅ Built |
| `IElevenLabsService` / `ElevenLabsService` | Audio generation | ✅ Built |
| `IAssetRegistry` / `AssetRegistryService` | Track generated assets | ✅ Built |
| `IStyleGuide` / `StyleGuideService` | Style consistency | ✅ Built |

**Editor MCP Tools (30+ tools):**
| Tool | Purpose | Layer |
|------|---------|-------|
| `CaptureWorldState` | Full world snapshot | 2 |
| `CaptureScreenshot` | Game view capture | 2 |
| `ScanMissingReferences` | Find null refs | 2 |
| `GetUIAccessibilityGraph` | UI DOM tree | 2 |
| `AnalyzeSpatial` | Spatial analysis | 2 |
| `ValidateScene` | Rule validation | 2 |
| `SimulateInput` | Input injection | 3 |
| `InteractUIElement` | UI interaction | 3 |
| `PlaytestClick` | Playtest automation | 3 |
| `WaitForCondition` | Wait for state | 3 |
| `ManageTasks` | Task CRUD | 4 |
| `ManageTranscripts` | Transcript logging | 4 |
| `EvaluateSyntactic` | Tier 1 eval | 5 |
| `EvaluateState` | Tier 2 eval | 5 |
| `EvaluateBehavioral` | Tier 3 eval | 5 |
| `EvaluateVisual` | Tier 4 eval | 5 |
| `EvaluatePolish` | Tier 5 eval | 5 |
| `EvaluateQuality` | Tier 6 eval | 5 |
| `RunEvaluation` | Run all tiers | 5 |
| `ManageOrchestration` | Orchestration control | 6 |
| `ManageSafety` | Safety controls | 6 |
| `ManageConvoys` | Convoy management | 6 |
| `GenerateMeshyAsset` | 3D generation | 7 |
| `GenerateAudio` | Audio generation | 7 |
| `ManageAssetRegistry` | Asset tracking | 7 |
| `ValidateStyle` | Style validation | 7 |
| `CheckInputSystem` | Input system detect | Util |

**Unit Tests:**
| Test Suite | Layer | Status |
|------------|-------|--------|
| `DependencyInjectionTests` | 1 | ✅ |
| `SceneStateCaptureTests` | 1 | ✅ |
| `EnvConfigTests` | 1 | ✅ |
| `MissingReferenceDetectorTests` | 2 | ✅ |
| `UIAccessibilityTests` | 2 | ✅ |
| `SpatialAnalysisTests` | 2 | ✅ |
| `ValidationRulesTests` | 2 | ✅ |
| `InputSimulationTests` | 3 | ✅ |
| `GameEventProviderTests` | 3 | ✅ |
| `SafetyControlTests` | 6 | ✅ |

### Unity Packages Installed
| Package | Status |
|---------|--------|
| VContainer | ✅ In manifest |
| Unity-MCP (com.coplaydev.unity-mcp) | ✅ In manifest |

---

## Validation Status

### Core Systems ✅
| Item | Status |
|------|--------|
| MCP connection (Engine@971db8c5) | ✅ Verified |
| Unity compiles (0 errors/warnings) | ✅ Verified |
| All 7 layers implemented | ✅ Verified |
| API keys (Meshy, ElevenLabs, Gemini) | ✅ Valid |
| Hooks persistence working | ✅ Verified |
| Orchestration system tested | ✅ Iteration 1 |

---

## Iteration 1 Results (TargetClicker)

### What Was Built
- **Game:** Click 10 targets to win
- **Tasks:** 15 tasks across 7 convoys
- **Components:** DI scope, target spawning, score tracking, win condition

### Evidence (in Assets/Screenshots/)
- `iteration1_test.png` - Initial test
- `playtest_initial_state.png` - Game start
- `playtest_win_state.png` - Win achieved
- `iteration1_final.png` - Final state

### Lessons Learned (hooks/blockers/)
- 2 blockers encountered and resolved
- Orchestration system validated
- Full evaluation pipeline tested

---

## What's Next to Build

### Iteration 2: Juicy Breakout
**GDD:** `Assets/Iteration2/GDD.md`

| Item | Purpose | Status |
|------|---------|--------|
| GDD Created | Arkanoid clone with extreme juice | ✅ Done |
| Core mechanics | Ball, paddle, bricks | Pending |
| Juice systems | Shake, particles, hit pause | Pending |
| Audio generation | ElevenLabs SFX | Pending |
| Video evaluation | Gemini judges 10-20s clip | Pending |

**Success Criteria:**
- Functional Arkanoid gameplay
- VLM video analyzer rates juice ≥ 7/10
- No major bugs in gameplay video

### Potential Improvements
| Area | Idea |
|------|------|
| VLM Integration | Use Claude vision for visual QA |
| Gemini Video | Gameplay feel analysis |
| CI/CD | Headless test runs |
| External Projects | Package for other Unity projects |

---

## Known Issues (Summary)

See `Docs/ENGINE_PROBLEMS.md` for full details.

| # | Issue | Resolution |
|---|-------|------------|
| 1 | Claude asked instead of acting | Added autonomy directive in CLAUDE.md |
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
3. Check Unity console via `read_console` MCP tool
4. Review hooks/ for persisted state from previous sessions
5. Continue from "What's Next to Build"

---

## Hooks Directory State

Current persisted data in `hooks/`:
- **orchestration/**: iteration1-plan.json
- **iterations/**: Iteration1/eyes-report.json
- **convoys/**: convoy-001 through convoy-007
- **tasks/**: task-001 through task-015
- **blockers/**: blocker-001, blocker-002
- **assets/**: registry.json
- **snapshots/**: 50+ scene snapshots
- **reviews/**: pending-approval.json

---

## Quick Reference

```bash
# Skills (invoke via /skill-name)
/validation          # Pre-flight checks
/meshy-generation    # 3D asset generation
/elevenlabs          # Audio generation
/review-layers       # Code layer review

# Agents (spawn via Task tool)
neuro-engine:mayor           # Orchestration
neuro-engine:script-polecat  # C# scripts
neuro-engine:scene-polecat   # Scene/prefabs
neuro-engine:asset-polecat   # Asset generation
neuro-engine:eyes-polecat    # Observation
neuro-engine:evaluator       # Evaluation
```

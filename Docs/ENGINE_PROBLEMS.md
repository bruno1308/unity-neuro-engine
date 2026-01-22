# ENGINE_PROBLEMS.md

Problems encountered during engine development that required manual user intervention.
Each entry documents: what happened, why it was a problem, and how to prevent it.

---

## Template for New Problems

```markdown
## Problem #N: [Short Title]

**Date:** YYYY-MM-DD
**Iteration:** [If during game iteration, which one]

**What happened:**
[Describe the observable problem]

**Why this was a problem:**
[Explain impact on autonomous operation]

**Root cause:**
[Technical explanation of why it happened]

**Layer Fault Attribution:**
| Layer | At Fault? | Reason |
|-------|-----------|--------|
| L1: Code-First | No | - |
| L2: Observation | No | - |
| L3: Interaction | No | - |
| L4: Persistence | No | - |
| L5: Evaluation | No | - |
| L6: Orchestration | No | - |
| L7: Asset Gen | No | - |

**Resolution:**
[What was done to fix it]

**Prevention:**
[How to avoid this in future]

**GitHub Issue:** [Link if created]
```

---

---

## Problem #11: Claude Bypassed Orchestration for GDD Implementation

**Date:** 2026-01-22
**Iteration:** Iteration2

**What happened:**
When asked to implement a GDD, Claude began writing scripts directly via MCP instead of spawning polecat agents. Created 5+ scripts sequentially without any parallelization.

**Why this was a problem:**
- Violated Mayor discipline rules (>3 MCP calls without delegation)
- No parallelization - code, assets, and audio should run concurrently
- No verification after tasks (eyes-polecat never spawned)
- Wasted time and tokens on sequential work

**Root cause:**
CLAUDE.md did not explicitly MANDATE orchestration. It said "see docs" but didn't enforce the rules. Claude followed its default behavior of doing work directly instead of delegating.

**Layer Fault Attribution:**
| Layer | At Fault? | Reason |
|-------|-----------|--------|
| L1: Code-First | No | - |
| L2: Observation | No | - |
| L3: Interaction | No | - |
| L4: Persistence | No | - |
| L5: Evaluation | No | - |
| L6: Orchestration | **YES** | Rules existed but weren't enforced at entry point |
| L7: Asset Gen | No | - |

**Resolution:**
Updated CLAUDE.md with explicit "MANDATORY: Orchestration for GDD Implementation" section that forbids direct implementation and requires parallel agent spawning.

**Prevention:**
- CLAUDE.md now contains explicit rules that MUST be followed
- Rules include: never implement directly, parallelize with agents, 3 MCP call limit, verification after integration
- Anyone implementing a GDD must use `/neuro-engine:orchestrate` or manually follow Mayor discipline

---

## Problem #12: Coarse Parallelization (1 Agent Per Category)

**Date:** 2026-01-22
**Iteration:** Iteration2

**What happened:**
Mayor spawned 1 script-polecat for 15 scripts and 1 asset-polecat for 8 audio files, instead of spawning 15+8 parallel agents.

**Why this was a problem:**
- Did not utilize parallel execution capability
- Sequential work within each agent
- Slower overall execution
- Defeats purpose of multi-agent architecture

**Root cause:**
ORCHESTRATION_RULES.md did not specify granularity of parallelization. Said "spawn agents in parallel" but not "one agent per file."

**Resolution:**
Added Rule 0.5: Maximum Parallelization Granularity
- Spawn ONE agent PER deliverable
- Task breakdown must list each file separately
- Example shows 15 script agents, 8 audio agents, etc.

---

## Problem #13: Missing Asset Categories (2D Sprites)

**Date:** 2026-01-22
**Iteration:** Iteration2

**What happened:**
GDD Section 7.1 listed 2D sprites (Ball, Paddle, Bricks) but no asset-polecat agents were spawned for them.

**Why this was a problem:**
- Incomplete asset generation
- Scene setup blocked waiting for sprites
- Manual intervention required

**Root cause:**
Task breakdown analysis focused on code and audio, missed visual assets. No checklist to verify all GDD asset types covered.

**Resolution:**
Added checklist to Rule 0.5:
- Scripts, Audio, 2D Sprites, 3D Models, Textures, Materials, Prefabs
- "Missing an entire asset category = major planning failure"

---

## Problem #14: No Progress Monitor Agent

**Date:** 2026-01-22
**Iteration:** Iteration2

**What happened:**
No mechanism to periodically check if development was on track with GDD vision.

**Why this was a problem:**
- Errors accumulated without detection
- Drift from GDD requirements unnoticed
- No early warning system
- Mayor flying blind

**Root cause:**
Architecture had eyes-polecat for verification but no continuous monitoring agent.

**Resolution:**
Created progress-polecat agent (Rule 0.6):
- Spawns every ~30 seconds
- Checks console for errors/warnings
- Compares state to task-breakdown.json
- Verifies GDD criteria alignment
- Reports health status (green/yellow/red)
- Recommends fixer agents

---

## Problem #15: Used Legacy uGUI Instead of UI Toolkit

**Date:** 2026-01-22
**Iteration:** Iteration2

**What happened:**
Scripts and scene setup used Canvas/uGUI system instead of UI Toolkit.

**Why this was a problem:**
- UI Toolkit is required architecture standard
- Legacy uGUI causes compatibility issues
- Inconsistent with engine patterns
- Breaks UI code-behind pattern

**Root cause:**
No explicit prohibition of uGUI in agent instructions. Agents defaulted to more common (but outdated) system.

**Resolution:**
Added Rule 0.7: UI Toolkit Only
- FORBIDDEN: Canvas, UnityEngine.UI, uGUI components
- REQUIRED: UIDocument, .uxml, .uss, UnityEngine.UIElements
- Added to script-polecat and scene-polecat agents

---

## Problem #16: Modal "Save Scene" Dialog Blocked Agent

**Date:** 2026-01-22
**Iteration:** Iteration2

**What happened:**
Scene-polecat triggered a modal "Save Scene" dialog that blocked Unity Editor and required human intervention.

**Why this was a problem:**
- Agent cannot continue
- Human must click button
- Breaks autonomous operation
- Defeats purpose of AI-driven development

**Root cause:**
manage_scene called without explicit path, triggering "Save As" dialog.

**Resolution:**
Added Rule 0.8: No Modal Dialogs
- ALWAYS specify explicit paths
- Never rely on "current scene"
- Added safe alternatives to scene-polecat
- Document as blocker if dialog appears

---

## Problem #17: MCP Scene Path Corruption (Doubled Path)

**Date:** 2026-01-22
**Iteration:** Iteration2

**What happened:**
Scene created via MCP had corrupted internal path: `Assets/Iteration2/Scenes/Iteration2Scene.unity/Iteration2Scene.unity` (path doubled).

**Why this was a problem:**
- Unity's internal scene registry has wrong path
- ANY save operation triggers modal dialog or "Moving file failed" error
- Cannot proceed with autonomous scene modifications
- Human intervention required to dismiss dialogs

**Root cause:**
MCP `manage_scene` action="create" or action="save" appears to have a bug that doubles the path in certain circumstances. The actual filesystem has the correct file, but Unity's internal AssetDatabase path is corrupted.

Evidence:
- Filesystem: `Assets/Iteration2/Scenes/Iteration2Scene.unity` (correct)
- Unity internal path: `Assets/Iteration2/Scenes/Iteration2Scene.unity/Iteration2Scene.unity` (wrong)
- Scene GUID is zeroed: `m_SceneGUID: 00000000000000000000000000000000`

**Layer Fault Attribution:**
| Layer | At Fault? | Reason |
|-------|-----------|--------|
| L1: Code-First | No | - |
| L2: Observation | No | - |
| L3: Interaction | **YES** | MCP bridge corrupts scene path |
| L4: Persistence | No | - |
| L5: Evaluation | No | - |
| L6: Orchestration | No | - |
| L7: Asset Gen | No | - |

**Resolution:**
WORKAROUND: Human must manually save the scene via Unity Editor (File > Save Scene) which corrects the internal path. Then refresh AssetDatabase.

PROPER FIX NEEDED: Unity-MCP bug report - scene path handling is broken.

**Prevention:**
- Avoid using MCP to create new scenes when possible
- If MCP must create scenes, verify path immediately after with get_active
- If path is doubled, flag as blocker requiring human save
- Consider pre-creating scenes manually before agent work

**GitHub Issue:** TBD - Unity-MCP upstream bug

---

## Problem #18: MCP manage_asset Search Fails to Find Prefabs

**Date:** 2026-01-22
**Iteration:** Iteration2

**What happened:**
`mcp__unity-mcp__manage_asset` with `action="search"` and `search_pattern="*.prefab"` returned 0 results, despite 24 prefabs existing in the project.

**Why this was a problem:**
- Agent concluded no prefabs existed
- Attempted to create prefabs that already existed
- Wasted time and caused confusion
- Could lead to duplicate assets

**Root cause:**
MCP `manage_asset` search does not properly find `.prefab` files. The exact cause is unclear - possibly a filter issue or AssetDatabase query problem.

Evidence:
- MCP search: `{"totalAssets":0,"pageSize":50,"pageNumber":1,"assets":[]}`
- Filesystem (Glob): Found 24 prefabs in `Assets/Iteration2/Prefabs/`

**Layer Fault Attribution:**
| Layer | At Fault? | Reason |
|-------|-----------|--------|
| L1: Code-First | No | - |
| L2: Observation | **YES** | MCP asset search returns incorrect results |
| L3: Interaction | No | - |
| L4: Persistence | No | - |
| L5: Evaluation | No | - |
| L6: Orchestration | No | - |
| L7: Asset Gen | No | - |

**Resolution:**
WORKAROUND: Use `Glob` tool to search for assets instead of MCP `manage_asset` search.

**Prevention:**
- Always verify asset searches with Glob as backup
- Don't trust empty results from MCP asset search
- Consider creating a custom asset search tool

**GitHub Issue:** TBD - Unity-MCP upstream bug

---

## Problem #19: Claude Repeatedly Falls Back to Linear Execution

**Date:** 2026-01-22
**Iteration:** Iteration2

**What happened:**
Despite explicit orchestration rules, Claude repeatedly fell back to sequential MCP calls instead of spawning parallel agents. Made 10+ sequential calls to wire up references one-by-one.

**Why this was a problem:**
- Violated Mayor discipline (>3 MCP calls)
- No parallelization
- Slow execution
- User had to intervene multiple times

**Root cause:**
CLAUDE.md had rules but they were buried below other content. Claude's default behavior is to "help directly" which overrides documented rules unless they are prominent.

**Layer Fault Attribution:**
| Layer | At Fault? | Reason |
|-------|-----------|--------|
| L1: Code-First | No | - |
| L2: Observation | No | - |
| L3: Interaction | No | - |
| L4: Persistence | No | - |
| L5: Evaluation | No | - |
| L6: Orchestration | **YES** | Rules not enforced at entry point |
| L7: Asset Gen | No | - |

**Resolution:**
Added prominent "🛑 STOP - READ THIS BEFORE EVERY ACTION" section at TOP of CLAUDE.md with:
- Explicit checklist before ANY action
- Forbidden patterns clearly listed
- Required patterns with examples
- 3 MCP call limit reminder

**Prevention:**
- Rules must be at TOP of CLAUDE.md, not buried
- Use emoji/formatting to make critical rules unmissable
- Consider adding a pre-action hook that validates behavior

---

## Problem #20: Play Mode Not Stopped Before Operations

**Date:** 2026-01-22
**Iteration:** Iteration2

**What happened:**
Agent entered Play mode for testing but context was lost/interrupted. Later operations (refresh_unity) failed because Unity was still in Play mode.

**Why this was a problem:**
- refresh_unity fails in Play mode
- Scene modifications fail in Play mode
- MCP commands return cryptic errors
- Human had to diagnose and intervene

**Root cause:**
No pre-check for Play mode before operations. No cleanup when agent is interrupted.

**Layer Fault Attribution:**
| Layer | At Fault? | Reason |
|-------|-----------|--------|
| L1: Code-First | No | - |
| L2: Observation | **YES** | Should check editor state before operations |
| L3: Interaction | No | - |
| L4: Persistence | No | - |
| L5: Evaluation | No | - |
| L6: Orchestration | No | - |
| L7: Asset Gen | No | - |

**Resolution:**
Always check `mcpforunity://editor_state` before operations and stop play mode if active.

**Prevention:**
- Add pre-flight check: if isPlayingOrWillChangePlaymode → stop first
- Game-tester agent MUST stop play mode in finally block
- Create recovery skill that stops play mode and clears state

---

## Problem #21: No Visual Asset Generation - Plain Rectangles Instead of Juicy Graphics

**Date:** 2026-01-22
**Iteration:** Iteration2

**What happened:**
"Juicy Breakout" game has plain colored rectangles for bricks, paddle, and ball. No textures, no visual polish, no juice in the visuals themselves.

**Why this was a problem:**
- GDD explicitly requires "juicy" visuals
- Plain rectangles cannot be judged as "juicy" by VLM
- Layer 7 (Asset Generation) was never properly utilized
- Engine claimed to generate assets but produced nothing visual

**Root cause:**
Architecture has Layer 7 for asset generation (Meshy, ElevenLabs) but:
1. No automated visual asset generation pipeline
2. Asset-polecat agents don't generate 2D sprites/textures
3. Procedural generation not implemented
4. Focus was on code, not visual quality

**Layer Fault Attribution:**
| Layer | At Fault? | Reason |
|-------|-----------|--------|
| L1: Code-First | No | - |
| L2: Observation | No | - |
| L3: Interaction | No | - |
| L4: Persistence | No | - |
| L5: Evaluation | No | - |
| L6: Orchestration | Partial | Didn't prioritize visual assets |
| L7: Asset Gen | **YES** | Core failure - no visual generation |

**Resolution:**
REQUIRED: Implement visual asset generation:
1. 2D sprite generation via AI image APIs (DALL-E, Midjourney, Stable Diffusion)
2. Procedural sprite generation in Unity (shader-based)
3. Asset-polecat must generate actual textures, not just placeholders
4. Add visual quality gate before evaluation

**Prevention:**
- Add "Visual Asset Quality" as explicit evaluation criterion
- Asset-polecat must produce actual graphics, not colored primitives
- VLM pre-check for asset quality before gameplay evaluation

---

## Problem #22: No Auto-Play Layer - Engine Cannot Test Its Own Games

**Date:** 2026-01-22
**Iteration:** Iteration2

**What happened:**
When the engine enters Play mode to test a game, nothing happens. The game waits for human input. The engine has no way to autonomously play/simulate gameplay.

**Why this was a problem:**
- Cannot evaluate gameplay without human intervention
- Cannot reach success criteria autonomously
- Auto-launch hack was a band-aid, not a solution
- Engine is fundamentally incomplete without simulation capability

**Root cause:**
Architecture is missing a critical layer: **Autonomous Gameplay Simulation**

The 7-layer architecture covers:
- L1-4: Code, observation, interaction, persistence
- L5: Evaluation (but can't evaluate what it can't play)
- L6-7: Orchestration and asset generation

MISSING: Layer that can simulate player input and play the game.

**Layer Fault Attribution:**
| Layer | At Fault? | Reason |
|-------|-----------|--------|
| L1: Code-First | No | - |
| L2: Observation | No | - |
| L3: Interaction | Partial | Should include input simulation |
| L4: Persistence | No | - |
| L5: Evaluation | **YES** | Cannot evaluate without gameplay |
| L6: Orchestration | **YES** | Should coordinate auto-play |
| L7: Asset Gen | No | - |

**Resolution:**
REQUIRED: Add Auto-Play/Simulation capability:

**Option A: AI-Controlled Player Agent**
- Create `AutoPlayer` component that uses simple AI to play
- For Breakout: move paddle toward ball X position
- For other games: game-specific AI or random valid inputs

**Option B: Input Recording/Playback**
- Record human gameplay sessions
- Replay inputs for automated testing

**Option C: Reinforcement Learning Agent**
- Train ML agent to play games
- Most sophisticated but highest effort

**Recommended: Option A (AI-Controlled Player)**
- Simple to implement per-game
- Deterministic and debuggable
- Can be part of test framework

**Prevention:**
- Every game must have an `AutoPlayer` component
- AutoPlayer is REQUIRED before evaluation can run
- Add to GDD template: "Auto-Play Strategy" section

---

## Blocker Issues

For blockers encountered during game iterations, create GitHub issues instead of adding here.
Use the template in `Docs/WORKFLOW.md` section 3.

Issues should be labeled:
- `blocker`
- `layer-{N}` (1-7)
- `iteration-{N}`

# The Neuro-Engine Protocol: Final Unified Architecture

## Autonomous AI-Driven Unity Game Development

**Version:** 4.0 Final (Modular Documentation)
**Date:** January 2026

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [The Problem](#2-the-problem)
3. [Core Principles](#3-core-principles)
4. [Architecture Overview](#4-architecture-overview)
5. [Layer 1: Code-First Foundation](#5-layer-1-code-first-foundation)
6. [Layer 2: Observation System (Eyes)](#6-layer-2-observation-system-eyes)
7. [Layer 3: Interaction System (Hands)](#7-layer-3-interaction-system-hands)
8. [Layer 4: Persistent Artifact System (Memory)](#8-layer-4-persistent-artifact-system-memory)
9. [Layer 5: Evaluation Framework (Judgment)](#9-layer-5-evaluation-framework-judgment)
10. [Layer 6: Agent Orchestration (Governance)](#10-layer-6-agent-orchestration-governance)
11. [Layer 7: Generative Asset Pipeline (Creation)](#11-layer-7-generative-asset-pipeline-creation)
12. [Implementation Roadmap](#12-implementation-roadmap)
13. [Evaluating the Engine](#13-evaluating-the-engine)
14. [Success Metrics](#14-success-metrics)
15. [Reference Links](#15-reference-links)

---

## 1. Executive Summary

This document presents the **Neuro-Engine Protocol** — a comprehensive architecture for enabling AI agents to autonomously develop Unity games from Game Design Documents (GDDs).

**The Core Insight:** AI fails at Unity not due to intelligence limitations, but because Unity hides truth. The solution is not better AI — it's a **transparent, queryable, self-correcting Unity environment**.

**The Vision:**
- Games are *grown* through continuous autonomous iteration
- Human developers become **directors and judges**, not button-clickers
- Unity transforms from an opaque editor into a **machine-legible runtime**

| Traditional Workflow | Neuro-Engine Protocol |
|---------------------|----------------------|
| Inspector-driven wiring | Code-first architecture |
| Human observes bugs | AI perceives state directly |
| Manual playtesting | Automated interaction simulation |
| Ephemeral context | Persistent artifact memory |
| "It compiled" = success | Multi-layer verification pyramid |
| Single developer | Agent swarm with governance |
| Manual asset creation | Generative 3D, textures, and audio |

---

## 2. The Problem

### Why AI Fails at Unity Today

| Capability | Web Development | Unity (Current) |
|------------|-----------------|-----------------|
| Execute | Write code, run server | Write C#, compile |
| Observe | DOM queries, console | **Cannot see renders, spatial state** |
| Verify | Tests, screenshots | **Cannot test "feel", wiring, visuals** |
| Iterate | Fast feedback (seconds) | Slow feedback (5-30s reload) |

### The Specific Gaps

1. **Hidden State** — Inspector wiring, prefab overrides invisible to text-based AI
2. **Visual Blindness** — AI cannot see what rendered on screen
3. **Spatial Ignorance** — AI cannot know where objects are in 3D space
4. **No Agency** — AI cannot click buttons or navigate as players do
5. **Subjective Quality** — "Feel", "juice", and "fun" resist programmatic verification

---

## 3. Core Principles

| # | Principle | Rule |
|---|-----------|------|
| 1 | **Observability** | If AI cannot observe it, it does not exist |
| 2 | **Agency** | If AI cannot act on it, it cannot fix it |
| 3 | **Persistence** | AI agents are ephemeral; project state must be permanent |
| 4 | **Verification Layering** | No single evaluation is sufficient |
| 5 | **Decomposed Subjectivity** | "Feel" is measurable components |
| 6 | **Human Authority** | AI proposes; humans approve taste |

---

## 4. Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                    LAYER 7: GENERATIVE ASSET PIPELINE                │
│        Meshy.ai (3D/Textures) • ElevenLabs (Audio) • Style Control   │
├─────────────────────────────────────────────────────────────────────┤
│                    LAYER 6: AGENT ORCHESTRATION                      │
│         Mayor Agent • Polecats • Convoys • Safety Controls           │
├─────────────────────────────────────────────────────────────────────┤
│                    LAYER 5: EVALUATION FRAMEWORK                     │
│      Syntactic • Semantic • State • Visual • Behavioral • Quality    │
├─────────────────────────────────────────────────────────────────────┤
│                    LAYER 4: PERSISTENT ARTIFACTS                     │
│              Git-Backed Hooks • Transcripts • Diffs                  │
├─────────────────────────────────────────────────────────────────────┤
│                    LAYER 3: INTERACTION SYSTEM                       │
│         Selenium-for-Unity • Input Simulation • Playtesting          │
├─────────────────────────────────────────────────────────────────────┤
│                    LAYER 2: OBSERVATION SYSTEM                       │
│     State Snapshots • UI Graphs • Spatial Analysis • Screenshots     │
├─────────────────────────────────────────────────────────────────────┤
│                    LAYER 1: CODE-FIRST FOUNDATION                    │
│       Dependency Injection • UI Toolkit • ECS/Serialization          │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 5. Layer 1: Code-First Foundation

**Goal:** Make Unity structurally understandable to machines.

### 5.1 Dependency Injection (The Skeleton)

**Problem:** `GetComponent<T>()` and Inspector drag-drop are invisible to AI.

**Solution:** Enforce [VContainer](https://github.com/hadashiA/VContainer) to make dependencies explicit in code.

**AI Advantage:** AI reads `GameLifetimeScope.cs` to understand all services instantly. Enables mock injection for testing.

### 5.2 Generative UI (The Face)

**Problem:** UGUI buries layout in binary prefabs.

**Solution:** Mandate **UI Toolkit** with UXML (structure) and USS (style).

**AI Advantage:** LLMs excel at XML/HTML. Complete UI restyling via single text file edit.

### 5.3 Data-Oriented State (The Brain)

**Problem:** Nested OOP hierarchies are hard to serialize and reason about.

**Solution:** Use **ECS** or **Runtime Scene Serialization** to expose state as queryable JSON.

**AI Advantage:** Debug via text analysis. "Enemy not dying" → Read JSON → See logic bug.

📁 **Code Examples:** [examples/layer1-code-first.md](examples/layer1-code-first.md)

---

## 6. Layer 2: Observation System (Eyes)

**Goal:** Give agents ground truth about what the game actually is.

### Key Components

- **Eyes Polecat** — Continuously-running observer capturing game state
- **Scene State Snapshots** — Full hierarchy as JSON
- **Missing Reference Detection** — Find null serialized fields
- **UI Accessibility Graph** — "DOM for games" via Unity's Accessibility API
- **Spatial Analysis** — Detect off-screen objects, scale anomalies, overlaps
- **Validation Rules Engine** — Configurable rules (YAML) with auto-fix support

📁 **Code Examples:** [examples/layer2-observation.md](examples/layer2-observation.md)

---

## 7. Layer 3: Interaction System (Hands)

**Goal:** Allow agents to behave like players.

### Key Components

- **Selenium-for-Unity API** — Find elements, click, type, wait
- **Input Simulation** — Keyboard, mouse, gamepad injection
- **Headless Execution** — Docker containers with virtual framebuffer
- **Integration Options:** [GameDriver](https://www.gamedriver.io/), [AltTester](https://alttester.com/), [Unity-MCP](https://github.com/CoplayDev/unity-mcp)

📁 **Code Examples:** [examples/layer3-interaction.md](examples/layer3-interaction.md)

---

## 8. Layer 4: Persistent Artifact System (Memory)

**Goal:** Give agents memory that survives crashes and context resets.

### The Hook Directory Structure

```
hooks/
├── scenes/{SceneName}/     # State snapshots, screenshots
├── compiler/               # Build results, errors
├── tests/                  # Test results, coverage
├── tasks/{TaskId}/         # Assignment, progress, transcript
├── messages/               # Agent inboxes
└── convoys/                # Task groupings
```

**Why Git-Backed:** Full history, diff capabilities, branch support, survives crashes.

📁 **Code Examples:** [examples/layer4-persistence.md](examples/layer4-persistence.md)

---

## 9. Layer 5: Evaluation Framework (Judgment)

**Goal:** Replace "it compiled" with measurable success.

### The Swiss Cheese Model

Stack imperfect verification layers:

| Tier | Type | Examples |
|------|------|----------|
| 6 | Human Playtest | Taste, fun, balance |
| 5 | Quality Metrics | Juice proxies, polish |
| 4 | Visual (VLM) | Screenshots, video analysis |
| 3 | Behavioral | Automated playtests |
| 2 | State | JSON snapshot assertions |
| 1 | Syntactic | Compilation, null refs |

### Dual-VLM Visual Intelligence

| Model | Role |
|-------|------|
| **Claude** | Static image analysis, asset QA, UI verification |
| **Gemini 3 Pro** | Video analysis, gameplay feel, temporal consistency |

**Key Pattern:** Batched capture-then-analyze. Don't block game loop — collect evidence during runtime, analyze afterward.

### Decomposing "Feel"

| Quality | Measurable Proxy |
|---------|------------------|
| Responsive | Input-to-movement < 16ms |
| Snappy | Time to max velocity < 100ms |
| Punchy | Screen shake > 5 units |

📁 **Code Examples:** [examples/layer5-evaluation.md](examples/layer5-evaluation.md), [examples/dual-vlm-system.md](examples/dual-vlm-system.md)

---

## 10. Layer 6: Agent Orchestration (Governance)

**Goal:** Scale intelligence without chaos.

### Agent Roles

| Agent | Responsibility |
|-------|---------------|
| **Mayor** | Orchestrates work, assigns tasks |
| **Script Polecat** | Writes C# scripts |
| **Scene Polecat** | Modifies scenes, prefabs |
| **Asset Polecat** | Generates 3D, textures, audio |
| **Eyes Polecat** | Observes and reports state |
| **Evaluator** | Grades outcomes |

### The Convoy System

Tasks grouped into Convoys for coordinated delivery with dependencies and completion criteria.

### Safety Controls

- Max iterations per task
- Budget limits (tokens, API cost)
- Auto-rollback on regression
- Human approval gates

**Rule:** Agents never self-approve. Cross-verification is mandatory.

📁 **Code Examples:** [examples/layer6-orchestration.md](examples/layer6-orchestration.md)

---

## 11. Layer 7: Generative Asset Pipeline (Creation)

**Goal:** Fully autonomous asset creation from text descriptions.

### Visual Assets (Meshy.ai)

- **Text-to-3D** — Generate models from descriptions
- **AI Texturing** — Re-texture existing models
- **Image-to-3D** — Convert concept art to models

### Audio (ElevenLabs)

- **Sound Effects** — Generate SFX from descriptions
- **Voice** — NPC dialogue, announcer lines
- **Music** — Ambient and action tracks

### Animation (Mixamo)

- **Auto-Rigging** — Rig Meshy-generated models automatically
- **Animation Presets** — Pre-defined sets per character type
- **Procedural Animation** — IK-based alternative for non-humanoids

### Level Generation

- **Graph-Based Levels** — AI-friendly YAML representation
- **Wave Function Collapse** — Detail geometry generation
- **VLM Review** — Layout and walkthrough analysis

### Feel Presets

- **Reference Games** — Doom, Quake, DUSK, Call of Duty parameters
- **Genetic Optimization** — Evolve parameters toward target feel

### Style Consistency

- **Style Guide (YAML)** — Color palette, polycount budgets, audio style
- **Art Direction Enforcer** — Claude-based batch style review

📁 **Code Examples:**
- [examples/layer7-asset-generation.md](examples/layer7-asset-generation.md)
- [examples/animation-pipeline.md](examples/animation-pipeline.md)
- [examples/level-generation.md](examples/level-generation.md)
- [examples/feel-presets.md](examples/feel-presets.md)

---

## 12. Implementation Roadmap

### Phase 1: Foundation (Weeks 1-4)
- VContainer integration
- UI Toolkit migration
- Eyes Polecat v1
- HTTP/MCP bridge

### Phase 2: Interaction (Weeks 5-8)
- Selenium-for-Unity driver
- Input simulation
- Automated playtest suite
- Validation rules engine

### Phase 3: Persistence (Weeks 9-12)
- Hook directory system
- Transcript logging
- Diff-based debugging

### Phase 4: Evaluation (Weeks 13-16)
- Tier 1-4 graders
- Feel metrics dashboard

### Phase 5: Orchestration (Weeks 17-20)
- Mayor agent
- Polecat specialization
- Safety controls

### Phase 6: Integration (Weeks 21-24)
- GDD parser
- Headless CI pipeline
- Full loop demonstration

### Phase 7: Asset Pipeline (Weeks 25-30)
- Meshy.ai integration
- ElevenLabs integration
- Style guide system
- Mixamo animation pipeline
- Level generation system
- Feel presets library

### Phase 8: Visual Intelligence (Weeks 31-34)
- Batched screenshot capture
- Claude image analysis
- Gemini video analysis
- Art direction enforcer

### Phase 9: Full Autonomous Loop (Weeks 35-40)
- GDD-to-asset-list parser
- Parallel asset generation
- Level generation integration
- **Doom Clone Demonstration**

---

## 13. Evaluating the Engine

### Core Metrics

| Metric | Measures | Use |
|--------|----------|-----|
| **pass@k** | Success in k attempts | Capability ceiling |
| **pass^k** | Success in ALL k attempts | Reliability floor |
| **Gap** | pass@k - pass^k | Reliability vs capability |

### Grader Types

| Type | Examples | Use |
|------|----------|-----|
| **Code-based** | Compilation, tests, null refs | Fast, deterministic |
| **Model-based** | VLM quality, feel rating | Subjective criteria |
| **Human-calibrated** | Bias-adjusted model graders | Final judgments |

### Eval Lifecycle

1. **Design** — Define new capability eval
2. **Baseline** — Establish current metrics
3. **Improve** — Make changes
4. **Validate** — Confirm improvement
5. **Graduate** — Move stable evals to regression suite
6. **Maintain** — Monitor for regressions

### Engine 1.0 Criteria

| Metric | Target |
|--------|--------|
| Core regression suite | pass^10 = 100% |
| Simple feature generation | pass@10 ≥ 90% |
| Full game from GDD | pass@10 ≥ 50% |
| Human interventions per game | < 3 |
| API cost per successful game | < $100 |

📁 **Code Examples:** [examples/engine-evaluation.md](examples/engine-evaluation.md)

---

## 14. Success Metrics

| Metric | 6 Month | 12 Month |
|--------|---------|----------|
| Simple feature (pass@10) | 70% | 90% |
| Complex feature (pass@10) | 35% | 60% |
| Full game from GDD | 20% | 50% |
| Human intervention rate | 30% | 10% |
| Asset generation success | 75% | 90% |
| Level generation quality | 6.5/10 | 8/10 |
| Style consistency (VLM) | 7.5/10 | 9/10 |
| Doom clone (human hours) | 4-6 | **<1** |

---

## 15. Reference Links

### Core Concepts

| Concept | Link |
|---------|------|
| GasTown Multi-Agent | [GitHub](https://github.com/steveyegge/gastown) |
| Anthropic Agent Evals | [Blog](https://www.anthropic.com/engineering/demystifying-evals-for-ai-agents) |
| Unity ECS/DOTS | [Unity](https://unity.com/ecs) |

### Unity Tools

| Tool | Link |
|------|------|
| VContainer | [GitHub](https://github.com/hadashiA/VContainer) |
| UI Toolkit | [Unity Manual](https://docs.unity3d.com/Manual/UIElements.html) |
| Unity Test Framework | [Docs](https://docs.unity3d.com/Packages/com.unity.test-framework@1.1/manual/index.html) |

### Testing & Automation

| Tool | Link |
|------|------|
| GameDriver | [Website](https://www.gamedriver.io/) |
| AltTester | [Website](https://alttester.com/) |
| Unity-MCP | [GitHub](https://github.com/CoplayDev/unity-mcp) |
| GameCI | [Website](https://game.ci/) |

### Generative APIs

| Service | Link |
|---------|------|
| Meshy.ai | [Website](https://www.meshy.ai/) |
| ElevenLabs | [Website](https://elevenlabs.io/) |
| Mixamo | [Website](https://www.mixamo.com/) |

### VLMs

| Model | Link |
|-------|------|
| Claude Opus 4.5 | [Anthropic](https://www.anthropic.com/claude) |
| Gemini 3 Pro | [Google](https://ai.google.dev/gemini-api/docs/gemini-3) |
| GPT-5.2 | [OpenAI](https://openai.com/) |

### Level Generation

| Tool | Link |
|------|------|
| Wave Function Collapse | [GitHub](https://github.com/mxgmn/WaveFunctionCollapse) |
| Edgar for Unity | [GitHub](https://github.com/OndrejNepozitek/Edgar-Unity) |

---

## Final Principle

> **"The future of AI-assisted game development isn't giving AI eyes. It's building games that don't require eyes to build — but when eyes help, use two: one for stills, one for motion."**

**The <1 hour Doom clone is no longer theoretical. With this architecture, it's an engineering target.**

---

*Document Version: 4.0 Final (Modular Documentation)*
*Code examples extracted to `examples/` directory*
*Date: January 2026*

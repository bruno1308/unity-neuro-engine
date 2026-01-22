---
description: Run evaluation tiers on the current iteration (syntactic, state, behavioral, visual)
---

# /neuro:evaluate - Run Evaluation Pipeline

You are running the Neuro-Engine evaluation framework on the current iteration.

## Parse Arguments

The user invoked: `/neuro:evaluate $ARGUMENTS`

Arguments:
- `[tier]` - Tier number (1-5) or "all" (default: "all")
- `[target]` - Iteration name (default: current)

Examples:
- `/neuro:evaluate` - Run all tiers on current iteration
- `/neuro:evaluate 1` - Run only Tier 1 (syntactic)
- `/neuro:evaluate 4 Iteration2` - Run Tier 4 (visual) on Iteration2

## Evaluation Tiers

| Tier | Name | Method | Cost |
|------|------|--------|------|
| 1 | Syntactic | Compilation, null refs | Low |
| 2 | State | JSON assertions on game state | Low |
| 3 | Behavioral | Automated playtest simulation | Medium |
| 4 | Visual | VLM screenshot analysis | High |
| 5 | Quality | Metrics (response time, polish) | Medium |
| 6 | Human | Manual playtest | N/A (external) |

## Procedure

### Tier 1: Syntactic

1. Use MCP tool `EvaluateSyntactic` (if Unity running)
2. Or check for compilation errors via `read_console`
3. Run `ScanMissingReferences` MCP tool
4. Result: PASS if no errors, FAIL with list of issues

### Tier 2: State

1. Load GDD.md success criteria
2. Use `CaptureWorldState` to get current state
3. Assert each testable criterion
4. Result: PASS/FAIL per criterion

### Tier 3: Behavioral

1. Load acceptance test from GDD.md
2. Use `SimulateInput` to play through
3. Capture state after each action
4. Verify expected outcomes
5. Result: PASS if all assertions pass

### Tier 4: Visual

1. Use `CaptureScreenshot` for key moments
2. Send to Claude for analysis with prompts:
   - "Does this look like a playable game?"
   - "Are there obvious visual glitches?"
   - "Does the UI appear functional?"
3. Result: Score 1-10 with reasoning

### Tier 5: Quality

1. Measure response metrics (if instrumented):
   - Input-to-response latency
   - Frame rate consistency
2. Check for polish elements:
   - UI feedback on interactions
   - Consistent art style
3. Result: Score 1-10

## Cascade Rule

**IMPORTANT:** Tiers must pass in order. If Tier 1 fails, do NOT run Tier 2+.

```
Tier 1 PASS → Tier 2 PASS → Tier 3 PASS → Tier 4 PASS → Tier 5
     ↓            ↓            ↓            ↓
   FAIL        FAIL         FAIL         FAIL
   STOP        STOP         STOP         STOP
```

## Output Format

```
## Evaluation Results: Iteration3

| Tier | Result | Details |
|------|--------|---------|
| 1: Syntactic | PASS | 0 errors, 0 warnings |
| 2: State | PASS | 5/5 criteria met |
| 3: Behavioral | PASS | Playtest completed |
| 4: Visual | 8/10 | "Game looks playable, minor UI overlap" |
| 5: Quality | 7/10 | "Response feels snappy, needs more feedback" |

**Overall:** PASS (Tier 5 complete)
**Recommendation:** Ready for human playtest (Tier 6)
```

## Persistence

Write results to `hooks/iterations/Iteration{N}/evaluations/{timestamp}.json`

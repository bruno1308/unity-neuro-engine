# Layer 5: Evaluation Framework (Judgment) - Examples

> Code samples for Swiss Cheese model, feel metrics, and multi-grader system

---

## 1. The Swiss Cheese Model

No single check catches everything. Stack imperfect layers:

```
┌─────────────────────────────────────────┐
│  TIER 6: Human Playtest (Final 10%)     │  ← Taste, fun, balance
├─────────────────────────────────────────┤
│  TIER 5: Quality Metrics                │  ← Juice proxies, polish
├─────────────────────────────────────────┤
│  TIER 4: Visual Verification (VLM)      │  ← Screenshots, video analysis
├─────────────────────────────────────────┤
│  TIER 3: Behavioral Tests               │  ← Automated playtests
├─────────────────────────────────────────┤
│  TIER 2: State Verification             │  ← JSON snapshot assertions
├─────────────────────────────────────────┤
│  TIER 1: Syntactic/Semantic             │  ← Compilation, null refs
└─────────────────────────────────────────┘
```

---

## 2. Decomposing Subjective "Feel"

### Character Controller "Feels Tight"

| Subjective Quality | Measurable Proxy | Threshold |
|-------------------|------------------|-----------|
| Responsive | Input-to-movement latency | < 16ms (1 frame) |
| Snappy | Time to max velocity | < 100ms |
| Precise | Direction change frames | ≤ 2 frames |
| Controlled | Air steering authority | 0.3-0.7 |
| Weighty | Gravity multiplier | 2.0-3.0 |

```csharp
[Test]
public void CharacterController_FeelsTight()
{
    var player = SpawnPlayer();

    // Input latency
    var inputTime = Time.realtimeSinceStartup;
    SimulateInput(Vector2.right);
    yield return WaitUntil(() => player.Velocity.x > 0);
    var latency = Time.realtimeSinceStartup - inputTime;
    Assert.Less(latency, 0.016f, "Input latency exceeds 16ms");

    // Time to max velocity
    var startTime = Time.realtimeSinceStartup;
    yield return WaitUntil(() => player.Velocity.x >= player.MaxSpeed * 0.95f);
    Assert.Less(Time.realtimeSinceStartup - startTime, 0.1f, "Acceleration too slow");
}
```

### Combat "Feels Impactful"

| Subjective Quality | Measurable Proxy | Threshold |
|-------------------|------------------|-----------|
| Punchy | Screen shake magnitude | > 5 units |
| Dramatic | Time scale minimum | < 0.5 |
| Explosive | Particle count on hit | > 50 |
| Loud | Sound effect volume | > 0.7 |
| Lasting | Effect duration | 0.3-1.0s |

---

## 3. Multi-Grader System

### graders.yaml

```yaml
graders:
  # Tier 1: Code-based (fast, deterministic)
  compilation:
    type: code
    check: "dotnet build"
    expect: exit_code_0

  null_references:
    type: code
    check: "eyes_polecat.find_null_refs(scene)"
    expect: empty_list

  # Tier 3: Interaction tests
  ui_flow:
    type: driver_test
    sequence:
      - find: "StartButton"
      - assert: visible
      - click: "StartButton"
      - wait_for: scene_change
      - assert: current_scene == "Gameplay"

  # Tier 4: Vision model
  visual_quality:
    type: vlm
    model: "gemini-3-pro"
    input: screenshot
    prompt: |
      Rate this game screenshot:
      1. UI Clarity (1-10)
      2. Visual bugs present? (list)
      3. Professional polish (1-10)
    threshold:
      ui_clarity: ">= 7"
      bugs: "length == 0"
```

---

## 4. The Video Haystack

For complex visual/temporal issues:

```python
import google.generativeai as genai

# Record 60 seconds of gameplay
video_path = record_gameplay(duration=60)

# Analyze with VLM
model = genai.GenerativeModel('gemini-3-pro')
response = model.generate_content([
    "You are a senior game QA engineer. Watch this gameplay video and report:",
    "1. Any texture flickering or z-fighting",
    "2. Character clipping through geometry",
    "3. UI elements extending beyond bounds",
    "4. Animation transitions that feel 'poppy' or unnatural",
    "Provide timestamps and severity for each issue.",
    video_path
])
```

---

## References

- [Anthropic Agent Evals](https://www.anthropic.com/engineering/demystifying-evals-for-ai-agents)
- [Gemini 3 Pro](https://ai.google.dev/gemini-api/docs/gemini-3)
- [Unity Test Framework](https://docs.unity3d.com/Packages/com.unity.test-framework@1.1/manual/index.html)

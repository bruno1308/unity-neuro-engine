# Engine Evaluation System - Examples

> Code samples for pass@k metrics, graders, eval tasks, and progress tracking

---

## 1. Core Metrics: pass@k vs pass^k

### pass@k (Capability Ceiling)

```python
def pass_at_k(task_id: str, k: int) -> bool:
    """Run task k times. Return True if ANY attempt succeeds."""
    for attempt in range(k):
        result = run_task(task_id)
        if result.success:
            return True
    return False

# Example: Can the engine create a main menu?
can_create_menu = pass_at_k("create_main_menu", k=10)
```

### pass^k (Reliability Floor)

```python
def pass_power_k(task_id: str, k: int) -> bool:
    """Run task k times. Return True only if ALL attempts succeed."""
    for attempt in range(k):
        result = run_task(task_id)
        if not result.success:
            return False
    return True

# Example: Is menu creation reliable enough for production?
menu_reliable = pass_power_k("create_main_menu", k=10)
```

### Interpreting the Gap

| Metric | Value | Interpretation |
|--------|-------|----------------|
| pass@10 | 80% | Engine CAN do it |
| pass^10 | 30% | Engine often FAILS |
| **Gap** | 50% | Reliability problem, not capability |

---

## 2. Code-Based Graders (Deterministic)

```python
class CodeGrader:
    """Deterministic graders that check objective criteria."""

    @staticmethod
    def compilation_check(project_path: str) -> GradeResult:
        """Did the Unity project compile without errors?"""
        result = run_unity_build(project_path, mode="scripts_only")
        return GradeResult(
            passed=result.exit_code == 0,
            score=1.0 if result.exit_code == 0 else 0.0,
            evidence=result.logs
        )

    @staticmethod
    def null_reference_check(scene_path: str) -> GradeResult:
        """Are there any null serialized fields?"""
        snapshot = capture_scene_state(scene_path)
        nulls = find_null_references(snapshot)
        return GradeResult(
            passed=len(nulls) == 0,
            score=1.0 - (len(nulls) / 100),
            evidence=nulls
        )

    @staticmethod
    def test_suite_check(test_assembly: str) -> GradeResult:
        """Do all unit tests pass?"""
        result = run_unity_tests(test_assembly)
        return GradeResult(
            passed=result.failed == 0,
            score=result.passed / result.total,
            evidence=result.failures
        )

    @staticmethod
    def performance_check(scene_path: str, min_fps: float = 60) -> GradeResult:
        """Does the scene maintain minimum FPS?"""
        metrics = profile_scene(scene_path, duration=10)
        avg_fps = metrics.average_fps
        return GradeResult(
            passed=avg_fps >= min_fps,
            score=min(avg_fps / min_fps, 1.0),
            evidence={"avg_fps": avg_fps, "min_fps": metrics.min_fps}
        )
```

---

## 3. Model-Based Graders (Probabilistic)

```python
class ModelGrader:
    """VLM/LLM graders for subjective or complex criteria."""

    @staticmethod
    async def visual_quality_check(screenshot_path: str, expectations: dict) -> GradeResult:
        result = await claude.analyze_image(
            image=screenshot_path,
            prompt=f"""
            EXPECTATIONS: {json.dumps(expectations)}
            Evaluate this game screenshot. Rate each criterion 1-10.
            Return JSON: {{"scores": {{}}, "overall": N, "issues": []}}
            """
        )
        return GradeResult(
            passed=result["overall"] >= 7,
            score=result["overall"] / 10,
            evidence=result
        )

    @staticmethod
    async def gameplay_feel_check(video_path: str, target_feel: str) -> GradeResult:
        result = await gemini.analyze_video(
            video=video_path,
            prompt=f"""
            TARGET FEEL: {target_feel}
            Watch this gameplay and rate:
            1. Movement responsiveness (1-10)
            2. Combat impact (1-10)
            3. Overall feel match (1-10)
            Return JSON with scores and feedback.
            """
        )
        overall = result["overall_feel_match"]
        return GradeResult(
            passed=overall >= 7,
            score=overall / 10,
            evidence=result
        )
```

---

## 4. Human-Calibrated Graders

```python
class HumanCalibratedGrader:
    """Model graders calibrated against human judgments."""

    def __init__(self, calibration_set_path: str):
        self.calibration = load_calibration_set(calibration_set_path)

    async def grade(self, input_data: any, model_grader: callable) -> GradeResult:
        raw_result = await model_grader(input_data)
        bias = self.calculate_bias(model_grader)
        adjusted_score = raw_result.score - bias

        return GradeResult(
            passed=adjusted_score >= 0.7,
            score=adjusted_score,
            evidence={
                "raw_score": raw_result.score,
                "bias_adjustment": bias,
                "model_evidence": raw_result.evidence
            }
        )

    def calculate_bias(self, model_grader: callable) -> float:
        """How much does the model typically over/under-rate?"""
        total_bias = 0
        for item in self.calibration:
            model_score = model_grader(item["input"])
            human_score = item["human_score"]
            total_bias += (model_score - human_score)
        return total_bias / len(self.calibration)
```

---

## 5. Eval Task Library

```yaml
# eval_task_library.yaml
tasks:
  code_first_001:
    name: "Generate Service Registration"
    category: "layer_1"
    input:
      service_name: "AudioService"
      interface: "IAudioService"
    expected_output:
      file_exists: "GameLifetimeScope.cs"
      contains_registration: true
      compiles: true
    graders: ["code_compilation", "code_pattern_match"]

  generation_001:
    name: "Generate Enemy from Description"
    category: "layer_7"
    input:
      description: "Demonic imp, red skin, horns, aggressive"
    expected_output:
      model_exists: true
      polycount_lte: 8000
      style_score_gte: 7
    graders: ["file_exists", "mesh_stats", "vlm_style_check"]

  e2e_001:
    name: "Doom Clone MVP"
    category: "end_to_end"
    input:
      gdd: "TestGDDs/doom_mvp.md"
    expected_output:
      playable: true
      enemies_exist: 3
      weapons_exist: 2
      vlm_overall_gte: 7
    graders: ["playability", "entity_count", "vlm_overall"]
    timeout: "4 hours"
```

---

## 6. Running Evals

### Nightly Capability Sweep

```python
async def nightly_capability_sweep():
    results = {}

    for task in CAPABILITY_EVAL_TASKS:
        pass_at_k = await evaluate_pass_at_k(task, k=10)
        pass_power_k = await evaluate_pass_power_k(task, k=5)

        results[task.id] = {
            "pass_at_10": pass_at_k,
            "pass_power_5": pass_power_k,
            "gap": pass_at_k - pass_power_k,
            "timestamp": datetime.utcnow().isoformat()
        }

    save_to_timeseries_db(results)
    report = generate_capability_report(results)

    regressions = find_regressions(results)
    if regressions:
        alert_team(regressions)

    return results
```

### CI Regression Gate

```python
def ci_regression_gate() -> bool:
    results = run_regression_suite()
    failures = [r for r in results if not r.passed]

    if failures:
        print("❌ REGRESSION GATE FAILED")
        for f in failures:
            print(f"  - {f.task_id}: {f.failure_reason}")
        return False

    print("✅ Regression gate passed")
    return True
```

---

## 7. Graduation Criteria

```python
def check_graduation(task_id: str) -> bool:
    """Should this capability eval become a regression test?"""
    history = get_task_history(task_id, days=30)

    avg_pass_at_10 = mean([h["pass_at_10"] for h in history])
    avg_pass_power_5 = mean([h["pass_power_5"] for h in history])
    recent_regression = any([h["regressed"] for h in history[-14:]])

    if avg_pass_at_10 > 0.9 and avg_pass_power_5 > 0.7 and not recent_regression:
        print(f"🎓 Task {task_id} ready for graduation to regression suite")
        return True

    return False
```

---

## 8. Budget Management

```yaml
# eval_budget.yaml
budget:
  daily:
    capability_evals: $50
    regression_evals: $20
    vlm_graders: $30

  per_eval:
    max_api_cost: $5
    max_duration: 30m

  optimization:
    cascade_graders: true      # Run expensive VLM graders only after code graders pass
    cache_code_graders: true   # Cache deterministic results
    sample_rate_large_suites: 0.3
    full_suite_frequency: "nightly"
    ci_suite_frequency: "per_commit"
```

---

## References

- [Anthropic Agent Evals](https://www.anthropic.com/engineering/demystifying-evals-for-ai-agents)

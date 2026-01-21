# Dual-VLM Visual Intelligence System - Examples

> Code samples for batched screenshot analysis (Claude) and video analysis (Gemini)

---

## 1. VLM Role Summary

| Model | Strength | Role |
|-------|----------|------|
| **Claude** | Image analysis + code context | Real-time asset QA, UI verification, style checks |
| **Gemini 3 Pro** | Video analysis, 1M context, multimodal | Gameplay feel, animation quality, temporal consistency |

---

## 2. Batched Capture-Then-Analyze Pattern

**Key Insight:** VLM analysis is slow. Don't block the game loop — batch everything.

```python
class VisualTestRun:
    """
    Captures visual evidence during test run, analyzes afterward.
    """
    def __init__(self):
        self.captures = []  # Collected during runtime
        self.video_path = None

    # ═══════════════════════════════════════════════════════════
    # PHASE 1: CAPTURE (Fast, during runtime)
    # ═══════════════════════════════════════════════════════════

    def capture_screenshot(self, name: str, expectations: dict):
        """
        Take screenshot with annotated expectations.
        Does NOT analyze yet - just records.
        """
        screenshot_path = f"hooks/visual_test/{name}.png"
        take_screenshot(screenshot_path)

        self.captures.append({
            "type": "screenshot",
            "name": name,
            "path": screenshot_path,
            "timestamp": time.time(),
            "expectations": expectations  # What we expect to see
        })

    def start_video_recording(self):
        """Begin gameplay recording for Gemini analysis."""
        self.video_path = f"hooks/visual_test/gameplay_{int(time.time())}.mp4"
        start_recording(self.video_path)

    def stop_video_recording(self, expectations: dict):
        """Stop recording and annotate expectations."""
        stop_recording()
        self.video_expectations = expectations

    # ═══════════════════════════════════════════════════════════
    # PHASE 2: ANALYZE (Batch, after test run)
    # ═══════════════════════════════════════════════════════════

    async def analyze_all(self) -> VisualTestReport:
        """
        After test run completes, analyze all captures.
        Can run Claude and Gemini in parallel.
        """
        report = VisualTestReport()

        # Parallel analysis
        claude_task = self.analyze_screenshots_with_claude()
        gemini_task = self.analyze_video_with_gemini()

        claude_results, gemini_results = await asyncio.gather(
            claude_task, gemini_task
        )

        report.screenshot_results = claude_results
        report.video_results = gemini_results
        report.compute_verdict()

        return report

    async def analyze_screenshots_with_claude(self) -> list:
        """Claude analyzes all screenshots against expectations."""
        results = []

        for capture in self.captures:
            if capture["type"] != "screenshot":
                continue

            result = await claude.analyze_image(
                image_path=capture["path"],
                prompt=f"""
                Analyze this game screenshot.

                EXPECTED:
                {json.dumps(capture["expectations"], indent=2)}

                VERIFY:
                1. Are all expected elements present?
                2. Are elements in expected positions?
                3. Any unexpected visual issues?
                4. Style consistency with game aesthetic?

                Return JSON: {{"pass": bool, "issues": [...], "details": str}}
                """
            )
            results.append({"name": capture["name"], "result": result})

        return results

    async def analyze_video_with_gemini(self) -> dict:
        """Gemini analyzes gameplay video against expectations."""
        if not self.video_path:
            return None

        result = await gemini.analyze_video(
            video_path=self.video_path,
            prompt=f"""
            Analyze this gameplay footage.

            EXPECTED BEHAVIORS:
            {json.dumps(self.video_expectations, indent=2)}

            VERIFY:
            1. Do all expected behaviors occur?
            2. Animation quality - any jerkiness or popping?
            3. Visual consistency throughout?
            4. Any temporal bugs (flickering, z-fighting)?
            5. Does movement/combat "feel" match expectations?

            Return JSON with timestamps for any issues found.
            """
        )

        return result
```

---

## 3. Full Visual Test Example

```python
async def test_enemy_spawn_and_combat():
    """Complete visual test with batched VLM analysis."""
    test = VisualTestRun()
    driver = UnityDriver.connect()

    # ─── RUNTIME PHASE (fast) ───────────────────────────────

    driver.load_scene("Gameplay")
    test.start_video_recording()

    # Capture: Initial state
    test.capture_screenshot("initial_state", {
        "expected_elements": ["Player", "HUD", "Crosshair"],
        "player_visible": True,
        "hud_readable": True
    })

    # Trigger enemy spawn
    driver.invoke("EnemySpawner.SpawnEnemy", position=[10, 0, 10])
    driver.wait_for_frames(30)

    # Capture: Enemy spawned
    test.capture_screenshot("enemy_spawned", {
        "expected_elements": ["Enemy_Imp"],
        "enemy_visible": True,
        "enemy_style_matches": "demonic, red skin, horns"
    })

    # Combat sequence
    driver.look_at([10, 1, 10])
    driver.press_key(Keys.MOUSE1)  # Shoot
    driver.wait_for_frames(10)

    # Capture: Combat feedback
    test.capture_screenshot("combat_feedback", {
        "expected_elements": ["MuzzleFlash", "HitEffect", "DamageNumber"],
        "screen_shake_visible": True,
        "enemy_hit_reaction": True
    })

    # Kill enemy
    for _ in range(5):
        driver.press_key(Keys.MOUSE1)
        driver.wait_for_frames(5)

    # Capture: Enemy death
    test.capture_screenshot("enemy_death", {
        "expected_elements": ["DeathEffect", "GibParticles"],
        "enemy_removed": True
    })

    test.stop_video_recording({
        "expected_feel": "punchy, responsive combat",
        "animation_quality": "smooth enemy movement and death",
        "style_consistency": "maintains dark, gritty aesthetic"
    })

    # ─── ANALYSIS PHASE (batch) ─────────────────────────────

    report = await test.analyze_all()

    assert report.passed, f"Visual test failed: {report.issues}"
    return report
```

---

## 4. Art Direction Enforcer

```python
class ArtDirectionEnforcer:
    """Ensures all generated assets match the target style."""

    def __init__(self, style_guide_path: str):
        self.style_guide = load_yaml(style_guide_path)
        self.approved_references = []
        self.pending_reviews = []

    def queue_for_review(self, asset_render_path: str, asset_metadata: dict):
        """Queue an asset for style review (doesn't block)."""
        self.pending_reviews.append({
            "render": asset_render_path,
            "metadata": asset_metadata,
            "timestamp": time.time()
        })

    async def batch_review(self) -> list:
        """Review all pending assets in one Claude call."""
        if not self.pending_reviews:
            return []

        prompt = f"""
        STYLE GUIDE:
        {yaml.dump(self.style_guide)}

        APPROVED REFERENCE ASSETS:
        [Images 1-{len(self.approved_references)}]

        NEW ASSETS TO REVIEW:
        [Images {len(self.approved_references)+1}-{len(self.approved_references)+len(self.pending_reviews)}]

        For each new asset, evaluate:
        1. Does it match the style guide specifications?
        2. Is it visually consistent with approved references?
        3. Color palette match (1-10)
        4. Detail level match (1-10)
        5. Overall style match (1-10)

        Return JSON array with verdict for each.
        """

        all_images = self.approved_references + [p["render"] for p in self.pending_reviews]
        results = await claude.analyze_images(images=all_images, prompt=prompt)

        approved, rejected = [], []
        for i, result in enumerate(results):
            asset = self.pending_reviews[i]
            if result["approved"] and result["score"] >= 7:
                approved.append(asset)
                self.approved_references.append(asset["render"])
            else:
                rejected.append({**asset, "feedback": result["feedback"]})

        self.pending_reviews = []
        return {"approved": approved, "rejected": rejected}
```

---

## 5. UI Unit Tests (Inline Analysis)

For simple UI checks, inline analysis is acceptable:

```python
class UIUnitTest:
    @staticmethod
    async def verify_button_visible(button_name: str) -> bool:
        screenshot = capture_ui_element(button_name)
        result = await claude.quick_check(
            image=screenshot,
            prompt=f"Is the '{button_name}' button clearly visible? Answer: YES or NO"
        )
        return "YES" in result.upper()

    @staticmethod
    async def verify_hud_elements(expected_elements: list) -> dict:
        screenshot = capture_game_view()
        result = await claude.analyze_image(
            image=screenshot,
            prompt=f"""
            Check for these HUD elements: {expected_elements}
            For each: present (bool), readable (bool), position (correct/off/missing)
            Return JSON.
            """
        )
        return result
```

---

## 6. VLM Configuration

```yaml
# vlm_config.yaml
visual_intelligence:
  claude:
    model: "claude-sonnet-4-5"  # Or claude-opus-4-5 for complex analysis
    role: "Static image analysis"
    use_cases:
      - asset_style_review
      - ui_verification
      - screenshot_comparison
      - art_direction_enforcement
    batch_mode: true
    max_images_per_call: 20

  gemini:
    model: "gemini-3-pro"  # Or gemini-3-deep-think for complex analysis
    role: "Video and temporal analysis"
    use_cases:
      - gameplay_feel_analysis
      - animation_quality_check
      - temporal_consistency
      - long_playtest_review
    max_video_duration: 600  # 10 minutes

  workflow:
    during_runtime:
      - capture_screenshots_with_expectations
      - record_gameplay_video
      - queue_assets_for_review
    after_runtime:
      - batch_analyze_screenshots (Claude)
      - analyze_video (Gemini)
      - art_direction_review (Claude)
      - combine_verdicts
```

---

## References

- [Claude Vision Docs](https://docs.anthropic.com/claude/docs/vision)
- [Gemini 3 Pro](https://ai.google.dev/gemini-api/docs/gemini-3)
- [Unity Recorder](https://docs.unity3d.com/Packages/com.unity.recorder@4.0/manual/index.html)

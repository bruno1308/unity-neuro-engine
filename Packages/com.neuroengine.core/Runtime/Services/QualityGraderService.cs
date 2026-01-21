using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NeuroEngine.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NeuroEngine.Services
{
    /// <summary>
    /// Tier 5: Quality grading - juice metrics, polish, feel.
    /// Measures subjective quality through objective proxies.
    /// Integrates with PolishGraderService for polish assessment.
    /// </summary>
    public class QualityGraderService : IQualityGrader
    {
        private const string GRADER_ID_JUICE = "quality.juice";
        private const string GRADER_ID_POLISH = "quality.polish";
        private const string GRADER_ID_ACCESSIBILITY = "quality.accessibility";
        private const string GRADER_ID_PERFORMANCE = "quality.performance";

        private readonly IPolishGrader _polishGrader;

        // Screen shake detection - common component names/patterns
        private static readonly string[] ScreenShakePatterns = new[]
        {
            "screenshake", "screen_shake", "shake", "camerashake", "camera_shake",
            "cinemachineshake", "impulse", "camerajolt", "cameratrauma"
        };

        // Juice-related component patterns
        private static readonly string[] JuiceComponentPatterns = new[]
        {
            "juice", "feedback", "impact", "hit", "punch", "squash", "stretch",
            "wobble", "bounce", "pulse", "flash", "shake", "rumble"
        };

        public QualityGraderService()
        {
            _polishGrader = new PolishGraderService();
        }

        public QualityGraderService(IPolishGrader polishGrader)
        {
            _polishGrader = polishGrader ?? new PolishGraderService();
        }

        public GraderResult MeasureJuice(JuiceConfig config)
        {
            var sw = Stopwatch.StartNew();
            var issues = new List<GradingIssue>();
            var metrics = new Dictionary<string, object>();

            try
            {
                // 1. Count particle systems
                var particleSystems = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
                metrics["particle_system_count"] = particleSystems.Length;

                var activeParticleSystems = particleSystems.Where(ps => ps.isPlaying || ps.isPaused).ToList();
                metrics["active_particle_systems"] = activeParticleSystems.Count;

                // Check particle settings
                var particlesWithGoodSettings = 0;
                foreach (var ps in particleSystems)
                {
                    var main = ps.main;
                    if (main.maxParticles >= (config?.MinParticlesPerAction ?? 10) &&
                        main.startLifetime.constantMax > 0)
                    {
                        particlesWithGoodSettings++;
                    }
                }
                metrics["particles_with_good_settings"] = particlesWithGoodSettings;

                if (particleSystems.Length == 0)
                {
                    issues.Add(new GradingIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Code = "JUICE_NO_PARTICLES",
                        Message = "No particle systems found - consider adding visual feedback effects",
                        SuggestedFix = "Add particle effects for impacts, actions, and ambient atmosphere"
                    });
                }

                // 2. Detect screen shake components
                var allBehaviours = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                var screenShakeComponents = new List<MonoBehaviour>();
                var juiceComponents = new List<MonoBehaviour>();

                foreach (var behaviour in allBehaviours)
                {
                    if (behaviour == null) continue;

                    var typeName = behaviour.GetType().Name.ToLowerInvariant();

                    if (ScreenShakePatterns.Any(p => typeName.Contains(p)))
                    {
                        screenShakeComponents.Add(behaviour);
                    }

                    if (JuiceComponentPatterns.Any(p => typeName.Contains(p)))
                    {
                        juiceComponents.Add(behaviour);
                    }
                }

                metrics["screen_shake_components"] = screenShakeComponents.Count;
                metrics["juice_components"] = juiceComponents.Count;

                // Check for Cinemachine (common screen shake solution)
                var cinemachineImpulseListeners = allBehaviours.Where(b =>
                    b.GetType().Name.Contains("CinemachineImpulseListener")).ToList();
                var cinemachineImpulseSources = allBehaviours.Where(b =>
                    b.GetType().Name.Contains("CinemachineImpulseSource")).ToList();

                metrics["cinemachine_impulse_listeners"] = cinemachineImpulseListeners.Count;
                metrics["cinemachine_impulse_sources"] = cinemachineImpulseSources.Count;

                bool hasScreenShake = screenShakeComponents.Count > 0 ||
                                      cinemachineImpulseListeners.Count > 0;
                metrics["has_screen_shake"] = hasScreenShake;

                if (!hasScreenShake)
                {
                    issues.Add(new GradingIssue
                    {
                        Severity = IssueSeverity.Info,
                        Code = "JUICE_NO_SCREENSHAKE",
                        Message = "No screen shake system detected",
                        SuggestedFix = "Consider adding camera shake for impactful moments (use Cinemachine Impulse or custom shake)"
                    });
                }

                // 3. Count audio sources (for audio reactivity)
                var audioSources = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
                metrics["audio_source_count"] = audioSources.Length;

                var sourcesWithClips = audioSources.Where(a => a.clip != null).Count();
                var sources3D = audioSources.Where(a => a.spatialBlend > 0.5f).Count();
                metrics["audio_sources_with_clips"] = sourcesWithClips;
                metrics["audio_sources_3d"] = sources3D;

                // Estimate audio reactivity (ratio of interactive sounds)
                float audioReactivity = audioSources.Length > 0
                    ? (float)sourcesWithClips / audioSources.Length
                    : 0f;
                metrics["audio_reactivity"] = audioReactivity;

                if (config?.MinAudioReactivity > 0 && audioReactivity < config.MinAudioReactivity)
                {
                    issues.Add(new GradingIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Code = "JUICE_LOW_AUDIO_REACTIVITY",
                        Message = $"Audio reactivity ({audioReactivity:P0}) is below minimum ({config.MinAudioReactivity:P0})",
                        SuggestedFix = "Ensure AudioSource components have AudioClips assigned for proper audio feedback"
                    });
                }

                // 4. Check for animation components (animation feedback)
                var animators = UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None);
                var activeAnimators = animators.Where(a =>
                    a.runtimeAnimatorController != null &&
                    a.GetComponentInParent<Canvas>() == null).ToList();
                metrics["animator_count"] = animators.Length;
                metrics["game_animators"] = activeAnimators.Count;

                // Check for DOTween/LeanTween (common tweening libraries)
                var tweenTypes = new[] { "DOTweenAnimation", "LeanTween", "TweenBase", "GoTween" };
                var tweenComponents = allBehaviours.Where(b =>
                    tweenTypes.Any(t => b.GetType().Name.Contains(t))).Count();
                metrics["tween_components"] = tweenComponents;

                // 5. Calculate overall juice score
                float juiceScore = CalculateJuiceScore(
                    particleSystems.Length,
                    hasScreenShake,
                    audioReactivity,
                    activeAnimators.Count,
                    juiceComponents.Count,
                    tweenComponents
                );
                metrics["juice_score"] = juiceScore;

                sw.Stop();

                var status = juiceScore >= 0.7f ? GradeStatus.Pass :
                             juiceScore >= 0.4f ? GradeStatus.Warning :
                             GradeStatus.Fail;

                return new GraderResult
                {
                    GraderId = GRADER_ID_JUICE,
                    Tier = EvaluationTier.Quality,
                    Status = status,
                    Score = juiceScore,
                    Weight = 0.6f,
                    DurationMs = sw.ElapsedMilliseconds,
                    Summary = $"Juice score: {juiceScore:P0} - {GetJuiceVerdict(juiceScore)}",
                    Issues = issues,
                    Metadata = metrics
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                var errorResult = GraderResult.Error(GRADER_ID_JUICE, EvaluationTier.Quality,
                    $"Failed to measure juice: {ex.Message}");
                errorResult.DurationMs = sw.ElapsedMilliseconds;
                return errorResult;
            }
        }

        public GraderResult AssessPolish(string scenePath = null)
        {
            var sw = Stopwatch.StartNew();

            try
            {
                // Delegate to PolishGraderService
                var polishReport = _polishGrader.GradePolish(new PolishConfig
                {
                    ScenePath = scenePath
                });

                var graderResult = _polishGrader.ToGraderResult(polishReport);

                sw.Stop();

                // Override grader ID to match our naming
                graderResult.GraderId = GRADER_ID_POLISH;
                graderResult.DurationMs = sw.ElapsedMilliseconds;

                return graderResult;
            }
            catch (Exception ex)
            {
                sw.Stop();
                var errorResult = GraderResult.Error(GRADER_ID_POLISH, EvaluationTier.Quality,
                    $"Failed to assess polish: {ex.Message}");
                errorResult.DurationMs = sw.ElapsedMilliseconds;
                return errorResult;
            }
        }

        public GraderResult CheckAccessibility(string scenePath = null)
        {
            var sw = Stopwatch.StartNew();
            var issues = new List<GradingIssue>();
            var metrics = new Dictionary<string, object>();

            try
            {
                // 1. Check UI text sizes
                var textComponents = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None);

                metrics["ui_text_count"] = textComponents.Length;

                // Check for TMP text if TextMeshPro is available (using reflection)
                int tmpTextCount = 0;
                var tmpTextType = System.Type.GetType("TMPro.TMP_Text, Unity.TextMeshPro");
                Component[] tmpTextComponents = null;
                if (tmpTextType != null)
                {
                    tmpTextComponents = UnityEngine.Object.FindObjectsByType(tmpTextType, FindObjectsSortMode.None) as Component[];
                    tmpTextCount = tmpTextComponents?.Length ?? 0;
                }
                metrics["tmp_text_count"] = tmpTextCount;

                // Check for small text (less than 14pt is hard to read)
                var smallTextCount = 0;
                const float MinReadableSize = 14f;

                foreach (var text in textComponents)
                {
                    if (text.fontSize < MinReadableSize)
                    {
                        smallTextCount++;
                    }
                }

                // Check TMP text font sizes via reflection if available
                if (tmpTextComponents != null)
                {
                    var fontSizeProp = tmpTextType?.GetProperty("fontSize");
                    if (fontSizeProp != null)
                    {
                        foreach (var tmp in tmpTextComponents)
                        {
                            var fontSize = fontSizeProp.GetValue(tmp) as float? ?? 0f;
                            if (fontSize < MinReadableSize)
                            {
                                smallTextCount++;
                            }
                        }
                    }
                }

                metrics["small_text_count"] = smallTextCount;

                if (smallTextCount > 0)
                {
                    issues.Add(new GradingIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Code = "A11Y_SMALL_TEXT",
                        Message = $"Found {smallTextCount} text elements with font size < {MinReadableSize}pt",
                        SuggestedFix = "Increase font size to at least 14pt for better readability"
                    });
                }

                // 2. Check for color contrast (basic check - look for very similar colors)
                // This is a simplified check - full contrast analysis would require VLM
                var images = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Image>(FindObjectsSortMode.None);
                metrics["ui_image_count"] = images.Length;

                // 3. Check for audio alternatives (subtitles/captions)
                var audioSources = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
                var subtitleComponents = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                    .Where(b => b.GetType().Name.ToLowerInvariant().Contains("subtitle") ||
                               b.GetType().Name.ToLowerInvariant().Contains("caption")).ToList();

                metrics["audio_source_count"] = audioSources.Length;
                metrics["subtitle_component_count"] = subtitleComponents.Count;

                if (audioSources.Length > 0 && subtitleComponents.Count == 0)
                {
                    issues.Add(new GradingIssue
                    {
                        Severity = IssueSeverity.Info,
                        Code = "A11Y_NO_SUBTITLES",
                        Message = "Audio sources present but no subtitle/caption system detected",
                        SuggestedFix = "Consider adding subtitles or captions for deaf/hard-of-hearing players"
                    });
                }

                // 4. Check for input remapping (look for input manager or settings)
                var inputRemapComponents = UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                    .Where(b => b.GetType().Name.ToLowerInvariant().Contains("remap") ||
                               b.GetType().Name.ToLowerInvariant().Contains("keybind") ||
                               b.GetType().Name.ToLowerInvariant().Contains("inputsetting")).ToList();

                metrics["input_remap_components"] = inputRemapComponents.Count;

                // 5. Check for UI navigation (for controller/keyboard users)
                var selectables = UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Selectable>(FindObjectsSortMode.None);
                var explicitNavigation = selectables.Count(s =>
                    s.navigation.mode == UnityEngine.UI.Navigation.Mode.Explicit);

                metrics["selectable_count"] = selectables.Length;
                metrics["explicit_navigation_count"] = explicitNavigation;

                if (selectables.Length > 5 && explicitNavigation == 0)
                {
                    issues.Add(new GradingIssue
                    {
                        Severity = IssueSeverity.Info,
                        Code = "A11Y_AUTO_NAVIGATION",
                        Message = "UI uses automatic navigation - explicit navigation may provide better experience",
                        SuggestedFix = "Consider setting explicit navigation for better controller/keyboard support"
                    });
                }

                // 6. Calculate accessibility score
                float accessibilityScore = CalculateAccessibilityScore(
                    smallTextCount,
                    textComponents.Length + tmpTextComponents.Length,
                    subtitleComponents.Count > 0,
                    inputRemapComponents.Count > 0,
                    explicitNavigation > 0 || selectables.Length <= 5
                );
                metrics["accessibility_score"] = accessibilityScore;

                sw.Stop();

                var status = accessibilityScore >= 0.8f ? GradeStatus.Pass :
                             accessibilityScore >= 0.5f ? GradeStatus.Warning :
                             GradeStatus.Fail;

                return new GraderResult
                {
                    GraderId = GRADER_ID_ACCESSIBILITY,
                    Tier = EvaluationTier.Quality,
                    Status = status,
                    Score = accessibilityScore,
                    Weight = 0.4f,
                    DurationMs = sw.ElapsedMilliseconds,
                    Summary = $"Accessibility score: {accessibilityScore:P0}",
                    Issues = issues,
                    Metadata = metrics
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                var errorResult = GraderResult.Error(GRADER_ID_ACCESSIBILITY, EvaluationTier.Quality,
                    $"Failed to check accessibility: {ex.Message}");
                errorResult.DurationMs = sw.ElapsedMilliseconds;
                return errorResult;
            }
        }

        public async Task<GraderResult> ProfilePerformanceAsync(PerformanceConfig config)
        {
            var sw = Stopwatch.StartNew();
            var issues = new List<GradingIssue>();
            var metrics = new Dictionary<string, object>();

            try
            {
                config ??= new PerformanceConfig
                {
                    TargetFPS = 60f,
                    MaxFrameTimeMs = 33.33f, // ~30 FPS minimum
                    MaxMemoryMB = 2048f,
                    DurationSeconds = 5
                };

                // Get current performance metrics
                float currentFPS = 1f / Time.smoothDeltaTime;
                float frameTimeMs = Time.smoothDeltaTime * 1000f;

                metrics["current_fps"] = currentFPS;
                metrics["frame_time_ms"] = frameTimeMs;
                metrics["target_fps"] = config.TargetFPS;

                // Memory metrics
                long totalMemory = GC.GetTotalMemory(false);
                float totalMemoryMB = totalMemory / (1024f * 1024f);
                metrics["gc_memory_mb"] = totalMemoryMB;

#if UNITY_EDITOR
                // Editor-only profiler metrics
                try
                {
                    // Get Unity profiler metrics if available
                    long monoHeapSize = UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong();
                    long monoUsedSize = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                    long totalAllocated = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                    long totalReserved = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong();

                    metrics["mono_heap_mb"] = monoHeapSize / (1024f * 1024f);
                    metrics["mono_used_mb"] = monoUsedSize / (1024f * 1024f);
                    metrics["total_allocated_mb"] = totalAllocated / (1024f * 1024f);
                    metrics["total_reserved_mb"] = totalReserved / (1024f * 1024f);

                    totalMemoryMB = totalAllocated / (1024f * 1024f);
                }
                catch
                {
                    // Profiler API may not be available
                }
#endif

                // Object counts (can impact performance)
                var gameObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                var colliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
                var rigidbodies = UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
                var particleSystems = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);

                metrics["gameobject_count"] = gameObjects.Length;
                metrics["renderer_count"] = renderers.Length;
                metrics["collider_count"] = colliders.Length;
                metrics["rigidbody_count"] = rigidbodies.Length;
                metrics["particle_system_count"] = particleSystems.Length;

                // Active particles count
                int totalActiveParticles = 0;
                foreach (var ps in particleSystems)
                {
                    totalActiveParticles += ps.particleCount;
                }
                metrics["active_particle_count"] = totalActiveParticles;

                // Check for performance issues
                if (currentFPS < config.TargetFPS)
                {
                    issues.Add(new GradingIssue
                    {
                        Severity = currentFPS < config.TargetFPS * 0.5f ? IssueSeverity.Error : IssueSeverity.Warning,
                        Code = "PERF_LOW_FPS",
                        Message = $"Current FPS ({currentFPS:F1}) is below target ({config.TargetFPS})",
                        SuggestedFix = "Profile with Unity Profiler to identify bottlenecks"
                    });
                }

                if (frameTimeMs > config.MaxFrameTimeMs)
                {
                    issues.Add(new GradingIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Code = "PERF_HIGH_FRAME_TIME",
                        Message = $"Frame time ({frameTimeMs:F2}ms) exceeds maximum ({config.MaxFrameTimeMs}ms)",
                        SuggestedFix = "Optimize heavy operations or spread them across frames"
                    });
                }

                if (totalMemoryMB > config.MaxMemoryMB)
                {
                    issues.Add(new GradingIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Code = "PERF_HIGH_MEMORY",
                        Message = $"Memory usage ({totalMemoryMB:F1}MB) exceeds maximum ({config.MaxMemoryMB}MB)",
                        SuggestedFix = "Check for memory leaks, unload unused assets, use object pooling"
                    });
                }

                // Check for potentially expensive scene configurations
                if (gameObjects.Length > 10000)
                {
                    issues.Add(new GradingIssue
                    {
                        Severity = IssueSeverity.Info,
                        Code = "PERF_HIGH_OBJECT_COUNT",
                        Message = $"High GameObject count ({gameObjects.Length}) may impact performance",
                        SuggestedFix = "Consider object pooling, LODs, or occlusion culling"
                    });
                }

                if (totalActiveParticles > 50000)
                {
                    issues.Add(new GradingIssue
                    {
                        Severity = IssueSeverity.Warning,
                        Code = "PERF_HIGH_PARTICLE_COUNT",
                        Message = $"High active particle count ({totalActiveParticles}) may impact performance",
                        SuggestedFix = "Reduce particle emission rates or max particles per system"
                    });
                }

                // Calculate performance score
                float performanceScore = CalculatePerformanceScore(
                    currentFPS, config.TargetFPS,
                    frameTimeMs, config.MaxFrameTimeMs,
                    totalMemoryMB, config.MaxMemoryMB
                );
                metrics["performance_score"] = performanceScore;

                sw.Stop();

                var status = performanceScore >= 0.8f ? GradeStatus.Pass :
                             performanceScore >= 0.5f ? GradeStatus.Warning :
                             GradeStatus.Fail;

                return new GraderResult
                {
                    GraderId = GRADER_ID_PERFORMANCE,
                    Tier = EvaluationTier.Quality,
                    Status = status,
                    Score = performanceScore,
                    Weight = 0.8f,
                    DurationMs = sw.ElapsedMilliseconds,
                    Summary = $"Performance score: {performanceScore:P0} ({currentFPS:F0} FPS, {totalMemoryMB:F0}MB)",
                    Issues = issues,
                    Metadata = metrics
                };
            }
            catch (Exception ex)
            {
                sw.Stop();
                var errorResult = GraderResult.Error(GRADER_ID_PERFORMANCE, EvaluationTier.Quality,
                    $"Failed to profile performance: {ex.Message}");
                errorResult.DurationMs = sw.ElapsedMilliseconds;
                return errorResult;
            }
        }

        #region Score Calculation Helpers

        private float CalculateJuiceScore(
            int particleCount,
            bool hasScreenShake,
            float audioReactivity,
            int animatorCount,
            int juiceComponentCount,
            int tweenCount)
        {
            float score = 0f;
            float totalWeight = 0f;

            // Particles (weight: 25%)
            float particleScore = Math.Min(1f, particleCount / 5f); // 5+ particles = full score
            score += particleScore * 0.25f;
            totalWeight += 0.25f;

            // Screen shake (weight: 15%)
            score += (hasScreenShake ? 1f : 0f) * 0.15f;
            totalWeight += 0.15f;

            // Audio reactivity (weight: 25%)
            score += audioReactivity * 0.25f;
            totalWeight += 0.25f;

            // Animators (weight: 15%)
            float animatorScore = Math.Min(1f, animatorCount / 3f); // 3+ animators = full score
            score += animatorScore * 0.15f;
            totalWeight += 0.15f;

            // Juice/Feedback components (weight: 10%)
            float juiceScore = Math.Min(1f, juiceComponentCount / 2f);
            score += juiceScore * 0.1f;
            totalWeight += 0.1f;

            // Tweens (weight: 10%)
            float tweenScore = Math.Min(1f, tweenCount / 3f);
            score += tweenScore * 0.1f;
            totalWeight += 0.1f;

            return totalWeight > 0 ? score / totalWeight : 0f;
        }

        private string GetJuiceVerdict(float score)
        {
            if (score >= 0.8f) return "Excellent juice and feedback";
            if (score >= 0.6f) return "Good juice, could be enhanced";
            if (score >= 0.4f) return "Basic feedback present";
            if (score >= 0.2f) return "Minimal juice, needs improvement";
            return "No juice detected - add feedback effects";
        }

        private float CalculateAccessibilityScore(
            int smallTextCount,
            int totalTextCount,
            bool hasSubtitles,
            bool hasInputRemapping,
            bool hasGoodNavigation)
        {
            float score = 0f;

            // Text readability (40%)
            float textScore = totalTextCount > 0
                ? 1f - Math.Min(1f, (float)smallTextCount / totalTextCount)
                : 1f;
            score += textScore * 0.4f;

            // Subtitles (20%)
            score += (hasSubtitles ? 1f : 0f) * 0.2f;

            // Input remapping (20%)
            score += (hasInputRemapping ? 1f : 0.5f) * 0.2f; // 0.5 if not present (common to not have)

            // Navigation (20%)
            score += (hasGoodNavigation ? 1f : 0.5f) * 0.2f;

            return score;
        }

        private float CalculatePerformanceScore(
            float currentFPS, float targetFPS,
            float frameTimeMs, float maxFrameTimeMs,
            float memoryMB, float maxMemoryMB)
        {
            float score = 0f;

            // FPS score (50%)
            float fpsRatio = Math.Min(1f, currentFPS / targetFPS);
            score += fpsRatio * 0.5f;

            // Frame time score (25%)
            float frameTimeRatio = Math.Max(0f, 1f - (frameTimeMs / maxFrameTimeMs));
            if (frameTimeMs <= maxFrameTimeMs) frameTimeRatio = 1f;
            score += frameTimeRatio * 0.25f;

            // Memory score (25%)
            float memoryRatio = Math.Max(0f, 1f - (memoryMB / maxMemoryMB));
            if (memoryMB <= maxMemoryMB) memoryRatio = 1f;
            score += memoryRatio * 0.25f;

            return score;
        }

        #endregion
    }
}

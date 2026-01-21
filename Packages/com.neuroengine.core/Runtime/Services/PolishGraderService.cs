using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NeuroEngine.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace NeuroEngine.Services
{
    /// <summary>
    /// Tier 5.5: Polish grading - game feel elements.
    /// Catches missing audio, visual feedback, environment, and debug code.
    /// This addresses Problem #9: agents missing sound, environment, and visual feedback.
    /// </summary>
    public class PolishGraderService : IPolishGrader
    {
        private const string GRADER_ID = "polish";

        // Common ground/floor object name patterns
        private static readonly string[] GroundPatterns = new[]
        {
            "ground", "floor", "terrain", "plane", "platform", "surface", "base"
        };

        // Unity's default camera background color (close to it)
        private static readonly Color DefaultSkyboxColor = new Color(0.192f, 0.302f, 0.475f, 1f);
        private const float ColorTolerance = 0.1f;

        public PolishGraderService()
        {
        }

        public PolishReport GradePolish(PolishConfig config = null)
        {
            config ??= new PolishConfig();
            var sw = Stopwatch.StartNew();

            var report = new PolishReport
            {
                Timestamp = DateTime.UtcNow,
                SceneName = SceneManager.GetActiveScene().name
            };

            // Run enabled checks
            if (config.IsCategoryEnabled(PolishCategory.Audio))
            {
                var result = CheckAudioFeedback(config.ScenePath);
                report.AllChecks.Add(result);
                UpdateCategorySummary(report.AudioSummary, result);
            }

            if (config.IsCategoryEnabled(PolishCategory.VisualFeedback))
            {
                var result = CheckVisualFeedback(config.ScenePath);
                report.AllChecks.Add(result);
                UpdateCategorySummary(report.VisualFeedbackSummary, result);
            }

            if (config.IsCategoryEnabled(PolishCategory.Environment))
            {
                var result = CheckEnvironment(config.ScenePath);
                report.AllChecks.Add(result);
                UpdateCategorySummary(report.EnvironmentSummary, result);
            }

            if (config.IsCategoryEnabled(PolishCategory.CodeCleanliness))
            {
                var result = CheckCodeCleanliness(config.GameScriptsPath);
                report.AllChecks.Add(result);
                UpdateCategorySummary(report.CodeCleanlinessSummary, result);
            }

            // Calculate totals
            report.TotalChecks = report.AllChecks.Count;
            report.PassedChecks = report.AllChecks.Count(c => c.Passed);
            report.FailedChecks = report.TotalChecks - report.PassedChecks;
            report.OverallScore = report.TotalChecks > 0
                ? (float)report.PassedChecks / report.TotalChecks
                : 1.0f;

            // Check for critical issues
            report.HasCriticalIssues =
                !report.AudioSummary.Passed ||
                !report.EnvironmentSummary.Passed;

            sw.Stop();
            report.DurationMs = sw.ElapsedMilliseconds;

            return report;
        }

        private void UpdateCategorySummary(PolishCategorySummary summary, PolishCheckResult result)
        {
            summary.TotalChecks++;
            if (result.Passed)
            {
                summary.PassedChecks++;
            }
            else
            {
                summary.CriticalIssues.AddRange(result.Issues);
            }
            summary.Passed = summary.PassedChecks == summary.TotalChecks;
            summary.Score = summary.TotalChecks > 0
                ? (float)summary.PassedChecks / summary.TotalChecks
                : 1.0f;
        }

        public PolishCheckResult CheckAudioFeedback(string scenePath = null)
        {
            var issues = new List<string>();
            var details = new Dictionary<string, object>();

            try
            {
                // Find all AudioSource components in the scene
                var audioSources = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
                details["audio_source_count"] = audioSources.Length;

                if (audioSources.Length == 0)
                {
                    issues.Add("No AudioSource components found in scene - game has no sound");
                }
                else
                {
                    // Check how many have clips assigned
                    var sourcesWithClips = audioSources.Where(a => a.clip != null).ToList();
                    var sourcesWithoutClips = audioSources.Where(a => a.clip == null).ToList();

                    details["sources_with_clips"] = sourcesWithClips.Count;
                    details["sources_without_clips"] = sourcesWithoutClips.Count;

                    if (sourcesWithClips.Count == 0)
                    {
                        issues.Add("AudioSource components found but none have AudioClip assigned");
                        foreach (var source in sourcesWithoutClips.Take(5)) // Limit to first 5
                        {
                            issues.Add($"  - {GetGameObjectPath(source.gameObject)}: No AudioClip assigned");
                        }
                    }
                    else if (sourcesWithoutClips.Count > 0)
                    {
                        // Partial - some sources have clips, some don't
                        foreach (var source in sourcesWithoutClips.Take(3))
                        {
                            issues.Add($"AudioSource on '{GetGameObjectPath(source.gameObject)}' has no AudioClip assigned");
                        }
                        if (sourcesWithoutClips.Count > 3)
                        {
                            issues.Add($"  ... and {sourcesWithoutClips.Count - 3} more AudioSources without clips");
                        }
                    }
                }

                // Check for AudioListener (required for audio to work)
                var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
                details["audio_listener_count"] = listeners.Length;

                if (listeners.Length == 0)
                {
                    issues.Add("No AudioListener found - audio will not be heard");
                }
                else if (listeners.Length > 1)
                {
                    issues.Add($"Multiple AudioListeners found ({listeners.Length}) - only one should be active");
                }

                if (issues.Count == 0)
                {
                    return new PolishCheckResult
                    {
                        Category = PolishCategory.Audio,
                        Passed = true,
                        CheckName = "audio_feedback",
                        Message = $"Audio setup looks good: {audioSources.Length} AudioSource(s) with clips assigned",
                        Details = details
                    };
                }

                return new PolishCheckResult
                {
                    Category = PolishCategory.Audio,
                    Passed = false,
                    CheckName = "audio_feedback",
                    Message = $"Audio feedback issues found: {issues.Count} problem(s)",
                    Issues = issues,
                    Details = details
                };
            }
            catch (Exception e)
            {
                return PolishCheckResult.Fail(
                    PolishCategory.Audio,
                    "audio_feedback",
                    $"Failed to check audio: {e.Message}",
                    new List<string> { e.Message }
                );
            }
        }

        public PolishCheckResult CheckVisualFeedback(string scenePath = null)
        {
            var issues = new List<string>();
            var details = new Dictionary<string, object>();

            try
            {
                // Find ParticleSystem components
                var particleSystems = UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);
                details["particle_system_count"] = particleSystems.Length;

                // Find Animator components (excluding UI elements)
                var animators = UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsSortMode.None);
                // Filter out UI animators (usually on Canvas children)
                var gameAnimators = animators.Where(a =>
                    a.GetComponentInParent<Canvas>() == null &&
                    a.runtimeAnimatorController != null
                ).ToList();

                details["animator_count"] = animators.Length;
                details["game_animator_count"] = gameAnimators.Count;

                // Find Animation components (legacy)
                var animations = UnityEngine.Object.FindObjectsByType<Animation>(FindObjectsSortMode.None);
                details["legacy_animation_count"] = animations.Length;

                // Check for any visual feedback systems
                bool hasParticles = particleSystems.Length > 0;
                bool hasAnimators = gameAnimators.Count > 0;
                bool hasLegacyAnimations = animations.Length > 0;

                if (!hasParticles && !hasAnimators && !hasLegacyAnimations)
                {
                    issues.Add("No visual feedback systems found - no ParticleSystems, Animators, or Animations");
                    issues.Add("Consider adding: hit effects, muzzle flashes, footstep dust, UI animations");
                }
                else
                {
                    // Check ParticleSystem validity
                    if (hasParticles)
                    {
                        foreach (var ps in particleSystems)
                        {
                            var main = ps.main;
                            if (main.maxParticles == 0)
                            {
                                issues.Add($"ParticleSystem on '{GetGameObjectPath(ps.gameObject)}' has maxParticles=0");
                            }
                        }
                    }

                    // Check Animator validity
                    var animatorsWithoutController = animators.Where(a =>
                        a.GetComponentInParent<Canvas>() == null &&
                        a.runtimeAnimatorController == null
                    ).ToList();

                    if (animatorsWithoutController.Count > 0)
                    {
                        foreach (var anim in animatorsWithoutController.Take(3))
                        {
                            issues.Add($"Animator on '{GetGameObjectPath(anim.gameObject)}' has no AnimatorController assigned");
                        }
                        if (animatorsWithoutController.Count > 3)
                        {
                            issues.Add($"  ... and {animatorsWithoutController.Count - 3} more Animators without controllers");
                        }
                    }
                }

                details["has_particles"] = hasParticles;
                details["has_animators"] = hasAnimators;
                details["has_legacy_animations"] = hasLegacyAnimations;

                if (issues.Count == 0)
                {
                    var feedbackTypes = new List<string>();
                    if (hasParticles) feedbackTypes.Add($"{particleSystems.Length} ParticleSystem(s)");
                    if (hasAnimators) feedbackTypes.Add($"{gameAnimators.Count} Animator(s)");
                    if (hasLegacyAnimations) feedbackTypes.Add($"{animations.Length} Animation(s)");

                    return new PolishCheckResult
                    {
                        Category = PolishCategory.VisualFeedback,
                        Passed = true,
                        CheckName = "visual_feedback",
                        Message = $"Visual feedback present: {string.Join(", ", feedbackTypes)}",
                        Details = details
                    };
                }

                return new PolishCheckResult
                {
                    Category = PolishCategory.VisualFeedback,
                    Passed = false,
                    CheckName = "visual_feedback",
                    Message = $"Visual feedback issues: {issues.Count} problem(s)",
                    Issues = issues,
                    Details = details
                };
            }
            catch (Exception e)
            {
                return PolishCheckResult.Fail(
                    PolishCategory.VisualFeedback,
                    "visual_feedback",
                    $"Failed to check visual feedback: {e.Message}",
                    new List<string> { e.Message }
                );
            }
        }

        public PolishCheckResult CheckEnvironment(string scenePath = null)
        {
            var issues = new List<string>();
            var details = new Dictionary<string, object>();

            try
            {
                // Check for ground/floor objects
                var allObjects = UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
                var groundObjects = allObjects.Where(go =>
                    GroundPatterns.Any(pattern =>
                        go.name.ToLowerInvariant().Contains(pattern)
                    ) && go.GetComponent<Collider>() != null
                ).ToList();

                // Also check for objects with terrain component
                var terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);

                details["ground_object_count"] = groundObjects.Count;
                details["terrain_count"] = terrains.Length;

                bool hasGround = groundObjects.Count > 0 || terrains.Length > 0;

                if (!hasGround)
                {
                    // Check for any large flat colliders that could serve as ground
                    var colliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
                    var potentialGrounds = colliders.Where(c =>
                    {
                        if (c is BoxCollider box)
                        {
                            // Large flat box
                            var size = box.size;
                            return size.x > 5 && size.z > 5 && size.y < 1;
                        }
                        if (c is MeshCollider mesh && mesh.sharedMesh != null)
                        {
                            // Check if mesh is relatively flat
                            var bounds = mesh.sharedMesh.bounds;
                            return bounds.size.x > 5 && bounds.size.z > 5 && bounds.size.y < 2;
                        }
                        return false;
                    }).ToList();

                    details["potential_ground_count"] = potentialGrounds.Count;

                    if (potentialGrounds.Count == 0)
                    {
                        issues.Add("No ground/floor found - objects may be floating in void");
                        issues.Add("Add a ground plane or terrain for the game environment");
                    }
                    else
                    {
                        hasGround = true;
                        details["ground_object_count"] = potentialGrounds.Count;
                    }
                }

                // Check camera background
                var mainCamera = Camera.main;
                if (mainCamera != null)
                {
                    details["camera_clear_flags"] = mainCamera.clearFlags.ToString();
                    details["camera_background_color"] = $"({mainCamera.backgroundColor.r:F2}, {mainCamera.backgroundColor.g:F2}, {mainCamera.backgroundColor.b:F2})";

                    if (mainCamera.clearFlags == CameraClearFlags.SolidColor)
                    {
                        var bg = mainCamera.backgroundColor;
                        bool isDefaultBlue = IsColorSimilar(bg, DefaultSkyboxColor, ColorTolerance);

                        details["is_default_background"] = isDefaultBlue;

                        if (isDefaultBlue)
                        {
                            issues.Add("Camera background is default Unity blue - consider custom skybox or color");
                        }
                    }
                    else if (mainCamera.clearFlags == CameraClearFlags.Skybox)
                    {
                        // Check if skybox material is assigned
                        var skyboxMaterial = RenderSettings.skybox;
                        details["has_skybox_material"] = skyboxMaterial != null;

                        if (skyboxMaterial == null)
                        {
                            issues.Add("Camera uses Skybox but no skybox material is assigned in Lighting settings");
                        }
                    }
                }
                else
                {
                    issues.Add("No Main Camera found in scene");
                    details["has_main_camera"] = false;
                }

                // Check for lighting
                var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
                details["light_count"] = lights.Length;

                if (lights.Length == 0 && RenderSettings.ambientMode == UnityEngine.Rendering.AmbientMode.Flat &&
                    RenderSettings.ambientLight == Color.black)
                {
                    issues.Add("No lights and ambient light is black - scene may be completely dark");
                }

                if (issues.Count == 0)
                {
                    return new PolishCheckResult
                    {
                        Category = PolishCategory.Environment,
                        Passed = true,
                        CheckName = "environment",
                        Message = "Environment setup looks good: ground present, camera configured",
                        Details = details
                    };
                }

                return new PolishCheckResult
                {
                    Category = PolishCategory.Environment,
                    Passed = false,
                    CheckName = "environment",
                    Message = $"Environment issues: {issues.Count} problem(s)",
                    Issues = issues,
                    Details = details
                };
            }
            catch (Exception e)
            {
                return PolishCheckResult.Fail(
                    PolishCategory.Environment,
                    "environment",
                    $"Failed to check environment: {e.Message}",
                    new List<string> { e.Message }
                );
            }
        }

        public PolishCheckResult CheckCodeCleanliness(string gameScriptsPath = "Assets/Scripts")
        {
            var issues = new List<string>();
            var details = new Dictionary<string, object>();

            try
            {
#if UNITY_EDITOR
                // Only run in editor context where we can access files
                var projectPath = Application.dataPath.Replace("/Assets", "");
                var scriptsFullPath = Path.Combine(projectPath, gameScriptsPath);

                details["scripts_path"] = gameScriptsPath;

                if (!Directory.Exists(scriptsFullPath))
                {
                    details["path_exists"] = false;
                    return new PolishCheckResult
                    {
                        Category = PolishCategory.CodeCleanliness,
                        Passed = true,
                        CheckName = "code_cleanliness",
                        Message = $"Scripts path '{gameScriptsPath}' not found - skipping check",
                        Details = details
                    };
                }

                details["path_exists"] = true;

                var csFiles = Directory.GetFiles(scriptsFullPath, "*.cs", SearchOption.AllDirectories);
                details["script_count"] = csFiles.Length;

                var debugLogPattern = new Regex(@"^\s*Debug\.(Log|LogWarning|LogError)\s*\(", RegexOptions.Multiline);
                var todoPattern = new Regex(@"//\s*(TODO|FIXME|HACK|XXX)", RegexOptions.IgnoreCase);

                int totalDebugLogs = 0;
                int totalTodos = 0;
                var filesWithDebugLogs = new List<(string file, int line, string content)>();
                var filesWithTodos = new List<(string file, int line, string content)>();

                foreach (var file in csFiles)
                {
                    var relativePath = file.Replace(projectPath + Path.DirectorySeparatorChar, "")
                                           .Replace(Path.DirectorySeparatorChar, '/');
                    var lines = File.ReadAllLines(file);

                    for (int i = 0; i < lines.Length; i++)
                    {
                        var line = lines[i];

                        // Skip if line is commented out (simple check)
                        var trimmed = line.TrimStart();
                        if (trimmed.StartsWith("//") && !todoPattern.IsMatch(line))
                            continue;

                        // Check for Debug.Log
                        if (debugLogPattern.IsMatch(line))
                        {
                            totalDebugLogs++;
                            if (filesWithDebugLogs.Count < 10) // Limit to first 10
                            {
                                filesWithDebugLogs.Add((relativePath, i + 1, trimmed.Substring(0, Math.Min(trimmed.Length, 60))));
                            }
                        }

                        // Check for TODOs
                        if (todoPattern.IsMatch(line))
                        {
                            totalTodos++;
                            if (filesWithTodos.Count < 5) // Limit to first 5
                            {
                                filesWithTodos.Add((relativePath, i + 1, trimmed.Substring(0, Math.Min(trimmed.Length, 60))));
                            }
                        }
                    }
                }

                details["debug_log_count"] = totalDebugLogs;
                details["todo_count"] = totalTodos;

                if (totalDebugLogs > 0)
                {
                    issues.Add($"Found {totalDebugLogs} Debug.Log statement(s) in game scripts:");
                    foreach (var (file, line, content) in filesWithDebugLogs)
                    {
                        issues.Add($"  {file}:{line} - {content}...");
                    }
                    if (totalDebugLogs > 10)
                    {
                        issues.Add($"  ... and {totalDebugLogs - 10} more Debug.Log statements");
                    }
                }

                if (totalTodos > 0)
                {
                    // TODOs are warnings, not failures
                    details["todo_locations"] = filesWithTodos.Select(t => $"{t.file}:{t.line}").ToList();
                }

                if (issues.Count == 0)
                {
                    var message = $"Code cleanliness good: {csFiles.Length} scripts checked, no Debug.Log found";
                    if (totalTodos > 0)
                    {
                        message += $" (note: {totalTodos} TODO comments found)";
                    }

                    return new PolishCheckResult
                    {
                        Category = PolishCategory.CodeCleanliness,
                        Passed = true,
                        CheckName = "code_cleanliness",
                        Message = message,
                        Details = details
                    };
                }

                return new PolishCheckResult
                {
                    Category = PolishCategory.CodeCleanliness,
                    Passed = false,
                    CheckName = "code_cleanliness",
                    Message = $"Code cleanliness issues: {totalDebugLogs} Debug.Log statement(s) found",
                    Issues = issues,
                    Details = details
                };
#else
                // Not in editor - can't check code
                return new PolishCheckResult
                {
                    Category = PolishCategory.CodeCleanliness,
                    Passed = true,
                    CheckName = "code_cleanliness",
                    Message = "Code cleanliness check only available in editor",
                    Details = new Dictionary<string, object> { { "skipped", true } }
                };
#endif
            }
            catch (Exception e)
            {
                return PolishCheckResult.Fail(
                    PolishCategory.CodeCleanliness,
                    "code_cleanliness",
                    $"Failed to check code cleanliness: {e.Message}",
                    new List<string> { e.Message }
                );
            }
        }

        public GraderResult ToGraderResult(PolishReport report)
        {
            var issues = new List<GradingIssue>();

            foreach (var check in report.AllChecks.Where(c => !c.Passed))
            {
                foreach (var issue in check.Issues)
                {
                    issues.Add(new GradingIssue
                    {
                        Severity = check.Category switch
                        {
                            PolishCategory.Audio => IssueSeverity.Warning,
                            PolishCategory.Environment => IssueSeverity.Warning,
                            PolishCategory.VisualFeedback => IssueSeverity.Info,
                            PolishCategory.CodeCleanliness => IssueSeverity.Warning,
                            _ => IssueSeverity.Info
                        },
                        Code = $"POLISH_{check.Category.ToString().ToUpperInvariant()}",
                        Message = issue
                    });
                }
            }

            var status = report.OverallScore >= 1.0f
                ? GradeStatus.Pass
                : (report.HasCriticalIssues ? GradeStatus.Fail : GradeStatus.Warning);

            return new GraderResult
            {
                GraderId = GRADER_ID,
                Tier = EvaluationTier.Quality, // Polish is part of Quality tier
                Status = status,
                Score = report.OverallScore,
                Weight = 0.5f, // Polish is less critical than syntactic/state
                DurationMs = report.DurationMs,
                Summary = $"Polish: {report.PassedChecks}/{report.TotalChecks} checks passed",
                Issues = issues,
                Metadata = new Dictionary<string, object>
                {
                    { "scene_name", report.SceneName },
                    { "passed_checks", report.PassedChecks },
                    { "failed_checks", report.FailedChecks },
                    { "audio_passed", report.AudioSummary.Passed },
                    { "visual_passed", report.VisualFeedbackSummary.Passed },
                    { "environment_passed", report.EnvironmentSummary.Passed },
                    { "code_passed", report.CodeCleanlinessSummary.Passed }
                }
            };
        }

        #region Helper Methods

        private string GetGameObjectPath(GameObject go)
        {
            var path = go.name;
            var parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        private bool IsColorSimilar(Color a, Color b, float tolerance)
        {
            return Mathf.Abs(a.r - b.r) < tolerance &&
                   Mathf.Abs(a.g - b.g) < tolerance &&
                   Mathf.Abs(a.b - b.b) < tolerance;
        }

        #endregion
    }
}

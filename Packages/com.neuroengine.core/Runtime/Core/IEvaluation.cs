using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NeuroEngine.Core
{
    /// <summary>
    /// Evaluation tier levels following the Swiss Cheese model.
    /// Each tier catches different failure modes.
    /// </summary>
    public enum EvaluationTier
    {
        Syntactic = 1,    // Compilation, null refs, missing refs
        State = 2,        // JSON snapshots, data integrity
        Behavioral = 3,   // Automated playtests
        Visual = 4,       // VLM screenshot/video analysis
        Quality = 5,      // Juice metrics, polish
        Human = 6         // Human playtest (manual trigger only)
    }

    /// <summary>
    /// Status of a grading result.
    /// </summary>
    public enum GradeStatus
    {
        Pass,
        Warning,
        Fail,
        Skipped,
        Error
    }

    /// <summary>
    /// Severity of a grading issue.
    /// </summary>
    public enum IssueSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// A single issue found during grading.
    /// </summary>
    public class GradingIssue
    {
        public IssueSeverity Severity;
        public string Code;           // e.g., "NULL_REF_001"
        public string Message;
        public string ObjectPath;     // Scene hierarchy path or asset path
        public string FilePath;       // Source file if applicable
        public int? Line;             // Line number if applicable
        public string SuggestedFix;   // Actionable when possible
        public Dictionary<string, object> Metadata;

        public GradingIssue() { }

        public GradingIssue(IssueSeverity severity, string message, string objectPath = null)
        {
            Severity = severity;
            Message = message;
            ObjectPath = objectPath;
            Metadata = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Result from a single grader execution.
    /// All graders return this unified format.
    /// </summary>
    public class GraderResult
    {
        public string GraderId;
        public EvaluationTier Tier;
        public GradeStatus Status;
        public float Score;           // 0.0 to 1.0 normalized
        public float Weight;          // How much this affects overall score (default 1.0)
        public long DurationMs;
        public string Summary;
        public List<GradingIssue> Issues;
        public Dictionary<string, object> Metadata;

        public GraderResult()
        {
            Weight = 1.0f;
            Issues = new List<GradingIssue>();
            Metadata = new Dictionary<string, object>();
        }

        public static GraderResult Pass(string graderId, EvaluationTier tier, string summary = null)
        {
            return new GraderResult
            {
                GraderId = graderId,
                Tier = tier,
                Status = GradeStatus.Pass,
                Score = 1.0f,
                Summary = summary ?? "All checks passed"
            };
        }

        public static GraderResult Fail(string graderId, EvaluationTier tier, string summary, List<GradingIssue> issues = null)
        {
            return new GraderResult
            {
                GraderId = graderId,
                Tier = tier,
                Status = GradeStatus.Fail,
                Score = 0.0f,
                Summary = summary,
                Issues = issues ?? new List<GradingIssue>()
            };
        }

        public static GraderResult Skipped(string graderId, EvaluationTier tier, string reason)
        {
            return new GraderResult
            {
                GraderId = graderId,
                Tier = tier,
                Status = GradeStatus.Skipped,
                Score = 0.0f,
                Summary = $"Skipped: {reason}"
            };
        }

        public static GraderResult Error(string graderId, EvaluationTier tier, string errorMessage)
        {
            return new GraderResult
            {
                GraderId = graderId,
                Tier = tier,
                Status = GradeStatus.Error,
                Score = 0.0f,
                Summary = $"Error: {errorMessage}",
                Issues = new List<GradingIssue>
                {
                    new GradingIssue(IssueSeverity.Critical, errorMessage)
                }
            };
        }

        public bool IsBlocking => Status == GradeStatus.Fail && Weight >= 1.0f;
    }

    /// <summary>
    /// Summary for a single evaluation tier.
    /// </summary>
    public class TierSummary
    {
        public EvaluationTier Tier;
        public GradeStatus OverallStatus;
        public float AverageScore;
        public int TotalGraders;
        public int PassedGraders;
        public int FailedGraders;
        public int SkippedGraders;
        public bool HasBlocker;
        public long TotalDurationMs;
    }

    /// <summary>
    /// Full evaluation report aggregating results from multiple graders.
    /// </summary>
    public class EvaluationReport
    {
        public string TargetId;
        public string TargetType;     // "scene", "prefab", "script", etc.
        public DateTime Timestamp;
        public long TotalDurationMs;
        public float OverallScore;
        public GradeStatus OverallStatus;
        public Dictionary<EvaluationTier, TierSummary> TierSummaries;
        public List<GraderResult> AllResults;
        public bool HasBlockingFailure;
        public string Summary;

        public EvaluationReport()
        {
            TierSummaries = new Dictionary<EvaluationTier, TierSummary>();
            AllResults = new List<GraderResult>();
        }
    }

    #region Tier-Specific Interfaces

    /// <summary>
    /// Tier 1: Syntactic grading - compilation, null refs, missing refs.
    /// Fast, deterministic checks that don't require runtime.
    /// </summary>
    public interface ISyntacticGrader
    {
        /// <summary>
        /// Check for compilation errors in the project.
        /// </summary>
        GraderResult CheckCompilation();

        /// <summary>
        /// Detect null serialized field references in scene objects.
        /// </summary>
        GraderResult DetectNullReferences(string scenePath = null);

        /// <summary>
        /// Detect missing asset references (broken links).
        /// </summary>
        GraderResult DetectMissingReferences(string scenePath = null);

        /// <summary>
        /// Run all syntactic checks.
        /// </summary>
        GraderResult GradeAll(string scenePath = null);
    }

    /// <summary>
    /// Tier 2: State grading - JSON snapshots, data integrity.
    /// Verifies game state matches expectations.
    /// </summary>
    public interface IStateGrader
    {
        /// <summary>
        /// Capture current state as a snapshot.
        /// </summary>
        Task<SceneSnapshot> CaptureSnapshotAsync(string scenePath = null);

        /// <summary>
        /// Validate current state against expectations.
        /// </summary>
        GraderResult ValidateExpectations(List<StateExpectation> expectations, string scenePath = null);

        /// <summary>
        /// Compare current state against a baseline snapshot.
        /// </summary>
        GraderResult CompareToBaseline(SceneSnapshot baseline, string scenePath = null);

        /// <summary>
        /// Run validation rules against state.
        /// </summary>
        GraderResult RunValidationRules(string scenePath = null);
    }

    /// <summary>
    /// State expectation for validation.
    /// </summary>
    public class StateExpectation
    {
        public string GameObjectPath;
        public string ComponentType;
        public string PropertyPath;
        public object ExpectedValue;
        public StateComparisonMode Mode;
        public string Description;
    }

    public enum StateComparisonMode
    {
        Exact,
        Contains,
        Range,
        Regex,
        NotNull,
        IsNull
    }

    /// <summary>
    /// Tier 3: Behavioral grading - automated playtests.
    /// Requires PlayMode, simulates player actions.
    /// </summary>
    public interface IBehavioralGrader
    {
        /// <summary>
        /// Run an automated playtest sequence.
        /// </summary>
        Task<GraderResult> RunPlaytestAsync(PlaytestConfig config);

        /// <summary>
        /// Verify a specific game flow (e.g., "can complete tutorial").
        /// </summary>
        Task<GraderResult> VerifyFlowAsync(GameFlowConfig flowConfig);

        /// <summary>
        /// Check if an interaction works as expected.
        /// </summary>
        Task<GraderResult> TestInteractionAsync(InteractionTestConfig config);
    }

    /// <summary>
    /// Configuration for automated playtest.
    /// </summary>
    public class PlaytestConfig
    {
        public string ScenePath;
        public List<InputSequence> InputScripts;
        public List<string> SuccessConditions;
        public List<string> FailureConditions;
        public int TimeoutSeconds;
        public bool RecordVideo;
    }

    /// <summary>
    /// Input sequence for playtest simulation.
    /// </summary>
    public class InputSequence
    {
        public string Name;
        public List<InputAction> Actions;
        public float DelayBetweenActions;
    }

    /// <summary>
    /// Single input action in a sequence.
    /// </summary>
    public class InputAction
    {
        public string Type;       // "key", "mouse", "gamepad"
        public string Action;     // "press", "release", "move"
        public string Target;     // Key name, button name, or coordinates
        public float Duration;    // Hold duration if applicable
    }

    /// <summary>
    /// Configuration for game flow verification.
    /// </summary>
    public class GameFlowConfig
    {
        public string FlowName;
        public string StartScene;
        public List<FlowStep> Steps;
        public int TimeoutSeconds;
    }

    /// <summary>
    /// Single step in a game flow.
    /// </summary>
    public class FlowStep
    {
        public string Description;
        public string Action;          // What to do
        public string ExpectedResult;  // What should happen
        public int MaxWaitSeconds;
    }

    /// <summary>
    /// Configuration for interaction testing.
    /// </summary>
    public class InteractionTestConfig
    {
        public string TargetObject;
        public string InteractionType;
        public string ExpectedOutcome;
        public int TimeoutSeconds;
    }

    /// <summary>
    /// Tier 4: Visual grading - VLM screenshot/video analysis.
    /// Uses external AI models (Claude, Gemini) for visual verification.
    /// </summary>
    public interface IVisualGrader
    {
        /// <summary>
        /// Capture and analyze a screenshot.
        /// </summary>
        Task<GraderResult> AnalyzeScreenshotAsync(VisualAnalysisConfig config);

        /// <summary>
        /// Analyze a pre-captured screenshot.
        /// </summary>
        Task<GraderResult> AnalyzeImageAsync(string imagePath, List<string> queries);

        /// <summary>
        /// Check if UI matches expected layout.
        /// </summary>
        Task<GraderResult> VerifyUILayoutAsync(UILayoutExpectation expectation);

        /// <summary>
        /// Check for visual glitches or artifacts.
        /// </summary>
        Task<GraderResult> DetectVisualGlitchesAsync(string imagePath = null);
    }

    /// <summary>
    /// Configuration for visual analysis.
    /// </summary>
    public class VisualAnalysisConfig
    {
        public string CameraPath;      // null = main camera
        public int Width;
        public int Height;
        public List<string> Queries;   // What to check for
        public string VLMProvider;     // "claude", "gemini", etc.
    }

    /// <summary>
    /// Expected UI layout for verification.
    /// </summary>
    public class UILayoutExpectation
    {
        public string Description;
        public List<UIElementExpectation> Elements;
    }

    /// <summary>
    /// Expected UI element properties.
    /// </summary>
    public class UIElementExpectation
    {
        public string ElementPath;
        public bool ShouldBeVisible;
        public string ExpectedText;
        public string ExpectedPosition;  // "top-left", "center", etc.
    }

    /// <summary>
    /// Tier 5: Quality grading - juice metrics, polish, feel.
    /// Measures subjective quality through objective proxies.
    /// </summary>
    public interface IQualityGrader
    {
        /// <summary>
        /// Measure "juice" metrics (particles, screenshake, feedback).
        /// </summary>
        GraderResult MeasureJuice(JuiceConfig config);

        /// <summary>
        /// Assess overall polish level.
        /// </summary>
        GraderResult AssessPolish(string scenePath = null);

        /// <summary>
        /// Check accessibility compliance.
        /// </summary>
        GraderResult CheckAccessibility(string scenePath = null);

        /// <summary>
        /// Measure performance metrics.
        /// </summary>
        Task<GraderResult> ProfilePerformanceAsync(PerformanceConfig config);
    }

    #region Polish Tier (Tier 5.5 - Game Feel Elements)

    /// <summary>
    /// Polish check categories for game feel elements.
    /// Each category catches missing "feel" elements that technical checks miss.
    /// </summary>
    public enum PolishCategory
    {
        Audio,           // Sound effects, music, audio feedback
        VisualFeedback,  // Particles, animations, visual effects
        Environment,     // Ground, skybox, lighting - not just objects in void
        CodeCleanliness  // Debug.Log removal, TODO comments, etc.
    }

    /// <summary>
    /// Result of a single polish check.
    /// </summary>
    public class PolishCheckResult
    {
        public PolishCategory Category;
        public bool Passed;
        public string CheckName;
        public string Message;
        public List<string> Issues;
        public Dictionary<string, object> Details;

        public PolishCheckResult()
        {
            Issues = new List<string>();
            Details = new Dictionary<string, object>();
        }

        public static PolishCheckResult Pass(PolishCategory category, string checkName, string message = null)
        {
            return new PolishCheckResult
            {
                Category = category,
                Passed = true,
                CheckName = checkName,
                Message = message ?? "Check passed"
            };
        }

        public static PolishCheckResult Fail(PolishCategory category, string checkName, string message, List<string> issues = null)
        {
            return new PolishCheckResult
            {
                Category = category,
                Passed = false,
                CheckName = checkName,
                Message = message,
                Issues = issues ?? new List<string>()
            };
        }
    }

    /// <summary>
    /// Comprehensive polish report covering all game feel elements.
    /// </summary>
    public class PolishReport
    {
        public DateTime Timestamp;
        public string SceneName;
        public float OverallScore;          // 0.0 to 1.0
        public bool HasCriticalIssues;
        public int TotalChecks;
        public int PassedChecks;
        public int FailedChecks;
        public long DurationMs;

        // Category summaries
        public PolishCategorySummary AudioSummary;
        public PolishCategorySummary VisualFeedbackSummary;
        public PolishCategorySummary EnvironmentSummary;
        public PolishCategorySummary CodeCleanlinessSummary;

        // All individual check results
        public List<PolishCheckResult> AllChecks;

        public PolishReport()
        {
            AllChecks = new List<PolishCheckResult>();
            AudioSummary = new PolishCategorySummary { Category = PolishCategory.Audio };
            VisualFeedbackSummary = new PolishCategorySummary { Category = PolishCategory.VisualFeedback };
            EnvironmentSummary = new PolishCategorySummary { Category = PolishCategory.Environment };
            CodeCleanlinessSummary = new PolishCategorySummary { Category = PolishCategory.CodeCleanliness };
        }

        public PolishCategorySummary GetSummary(PolishCategory category)
        {
            return category switch
            {
                PolishCategory.Audio => AudioSummary,
                PolishCategory.VisualFeedback => VisualFeedbackSummary,
                PolishCategory.Environment => EnvironmentSummary,
                PolishCategory.CodeCleanliness => CodeCleanlinessSummary,
                _ => null
            };
        }
    }

    /// <summary>
    /// Summary for a single polish category.
    /// </summary>
    public class PolishCategorySummary
    {
        public PolishCategory Category;
        public bool Passed;
        public int TotalChecks;
        public int PassedChecks;
        public float Score;                 // 0.0 to 1.0
        public List<string> CriticalIssues;

        public PolishCategorySummary()
        {
            CriticalIssues = new List<string>();
        }
    }

    /// <summary>
    /// Configuration for polish grading.
    /// </summary>
    public class PolishConfig
    {
        /// <summary>
        /// Scene path to analyze (null = active scene).
        /// </summary>
        public string ScenePath;

        /// <summary>
        /// Path to game scripts folder for code cleanliness checks.
        /// Defaults to "Assets/Scripts" - excludes engine/packages.
        /// </summary>
        public string GameScriptsPath = "Assets/Scripts";

        /// <summary>
        /// Categories to check (null = all).
        /// </summary>
        public List<PolishCategory> EnabledCategories;

        /// <summary>
        /// Whether to treat Debug.Log as a failure (default: true for release).
        /// </summary>
        public bool FailOnDebugLog = true;

        /// <summary>
        /// Minimum number of AudioSources expected (0 = just check they exist).
        /// </summary>
        public int MinAudioSources = 0;

        /// <summary>
        /// Minimum number of ParticleSystems expected (0 = just check they exist).
        /// </summary>
        public int MinParticleSystems = 0;

        public PolishConfig()
        {
            EnabledCategories = null; // All categories
        }

        public bool IsCategoryEnabled(PolishCategory category)
        {
            return EnabledCategories == null || EnabledCategories.Contains(category);
        }
    }

    /// <summary>
    /// Tier 5.5: Polish grading - game feel elements.
    /// Catches missing audio, visual feedback, environment, and debug code.
    /// This addresses Problem #9: agents missing sound, environment, and visual feedback.
    /// </summary>
    public interface IPolishGrader
    {
        /// <summary>
        /// Run all polish checks and return a comprehensive report.
        /// </summary>
        PolishReport GradePolish(PolishConfig config = null);

        /// <summary>
        /// Check audio feedback - AudioSource components and AudioClip assignments.
        /// </summary>
        PolishCheckResult CheckAudioFeedback(string scenePath = null);

        /// <summary>
        /// Check visual feedback - ParticleSystem and Animator components.
        /// </summary>
        PolishCheckResult CheckVisualFeedback(string scenePath = null);

        /// <summary>
        /// Check environment - ground/floor objects, camera background.
        /// </summary>
        PolishCheckResult CheckEnvironment(string scenePath = null);

        /// <summary>
        /// Check code cleanliness - Debug.Log statements in game scripts.
        /// </summary>
        PolishCheckResult CheckCodeCleanliness(string gameScriptsPath = "Assets/Scripts");

        /// <summary>
        /// Convert polish report to standard GraderResult format.
        /// </summary>
        GraderResult ToGraderResult(PolishReport report);
    }

    #endregion

    /// <summary>
    /// Configuration for juice measurement.
    /// </summary>
    public class JuiceConfig
    {
        public string ScenePath;
        public float MinScreenShakeIntensity;
        public int MinParticlesPerAction;
        public float MaxResponseTimeMs;
        public float MinAudioReactivity;
    }

    /// <summary>
    /// Configuration for performance profiling.
    /// </summary>
    public class PerformanceConfig
    {
        public string ScenePath;
        public int DurationSeconds;
        public float TargetFPS;
        public float MaxFrameTimeMs;
        public float MaxMemoryMB;
    }

    #endregion

    #region Optional Unified Runner

    /// <summary>
    /// Optional unified evaluation runner for full evaluations.
    /// Orchestrates multiple graders across tiers.
    /// </summary>
    public interface IEvaluationRunner
    {
        /// <summary>
        /// Run all configured graders against a target.
        /// </summary>
        Task<EvaluationReport> EvaluateAsync(EvaluationTarget target, EvaluationConfig config = null);

        /// <summary>
        /// Run only specific tiers.
        /// </summary>
        Task<EvaluationReport> EvaluateTiersAsync(EvaluationTarget target, params EvaluationTier[] tiers);

        /// <summary>
        /// Quick syntactic check only (Tier 1).
        /// </summary>
        GraderResult QuickCheck(string scenePath = null);
    }

    /// <summary>
    /// Target for evaluation.
    /// </summary>
    public class EvaluationTarget
    {
        public string Id;
        public string Type;        // "scene", "prefab", "script", "build"
        public string Path;
        public SceneSnapshot BaselineSnapshot;
        public Dictionary<string, object> Context;

        public EvaluationTarget()
        {
            Context = new Dictionary<string, object>();
        }

        public static EvaluationTarget Scene(string scenePath)
        {
            return new EvaluationTarget
            {
                Id = scenePath,
                Type = "scene",
                Path = scenePath
            };
        }

        public static EvaluationTarget Prefab(string prefabPath)
        {
            return new EvaluationTarget
            {
                Id = prefabPath,
                Type = "prefab",
                Path = prefabPath
            };
        }
    }

    /// <summary>
    /// Configuration for evaluation run.
    /// </summary>
    public class EvaluationConfig
    {
        public string Name;
        public bool FailFast;                          // Stop on first blocking failure
        public Dictionary<EvaluationTier, bool> EnabledTiers;
        public Dictionary<string, float> GraderWeights;
        public int TimeoutSeconds;

        public EvaluationConfig()
        {
            EnabledTiers = new Dictionary<EvaluationTier, bool>
            {
                { EvaluationTier.Syntactic, true },
                { EvaluationTier.State, true },
                { EvaluationTier.Behavioral, false },  // Off by default (slow)
                { EvaluationTier.Visual, false },      // Off by default (requires API)
                { EvaluationTier.Quality, false },
                { EvaluationTier.Human, false }
            };
            GraderWeights = new Dictionary<string, float>();
            TimeoutSeconds = 300;
        }

        public bool IsTierEnabled(EvaluationTier tier)
        {
            return EnabledTiers.TryGetValue(tier, out var enabled) && enabled;
        }

        public static EvaluationConfig QuickCheck()
        {
            return new EvaluationConfig
            {
                Name = "Quick Check",
                FailFast = true,
                EnabledTiers = new Dictionary<EvaluationTier, bool>
                {
                    { EvaluationTier.Syntactic, true }
                },
                TimeoutSeconds = 30
            };
        }

        public static EvaluationConfig FullEvaluation()
        {
            return new EvaluationConfig
            {
                Name = "Full Evaluation",
                FailFast = false,
                EnabledTiers = new Dictionary<EvaluationTier, bool>
                {
                    { EvaluationTier.Syntactic, true },
                    { EvaluationTier.State, true },
                    { EvaluationTier.Behavioral, true },
                    { EvaluationTier.Visual, true },
                    { EvaluationTier.Quality, true }
                },
                TimeoutSeconds = 600
            };
        }
    }

    #endregion
}

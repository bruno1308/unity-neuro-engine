using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace NeuroEngine.Core
{
    #region Configuration Classes

    /// <summary>
    /// Complete style guide configuration loaded from YAML.
    /// Defines visual consistency rules for the project.
    /// </summary>
    public class StyleGuideConfig
    {
        public string Name;
        public string Version;
        public string Description;

        // Color palette
        public ColorPaletteConfig Colors;

        // Polycount budgets by asset type
        public PolycountBudgets Polycount;

        // Texture constraints
        public TextureStyleConfig Textures;

        // Audio style
        public AudioStyleConfig Audio;

        // UI style
        public UIStyleConfig UI;

        // Model style
        public ModelStyleConfig Models;

        // General settings
        public Dictionary<string, object> CustomRules;

        public StyleGuideConfig()
        {
            Colors = new ColorPaletteConfig();
            Polycount = new PolycountBudgets();
            Textures = new TextureStyleConfig();
            Audio = new AudioStyleConfig();
            UI = new UIStyleConfig();
            Models = new ModelStyleConfig();
            CustomRules = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// Color palette configuration.
    /// </summary>
    public class ColorPaletteConfig
    {
        public List<ColorEntry> Primary;
        public List<ColorEntry> Secondary;
        public List<ColorEntry> Accent;
        public List<ColorEntry> Neutrals;
        public float ColorTolerance;  // How close a color must be to palette (0-1)

        public ColorPaletteConfig()
        {
            Primary = new List<ColorEntry>();
            Secondary = new List<ColorEntry>();
            Accent = new List<ColorEntry>();
            Neutrals = new List<ColorEntry>();
            ColorTolerance = 0.15f;
        }

        public List<ColorEntry> GetAllColors()
        {
            var all = new List<ColorEntry>();
            all.AddRange(Primary);
            all.AddRange(Secondary);
            all.AddRange(Accent);
            all.AddRange(Neutrals);
            return all;
        }
    }

    /// <summary>
    /// A single color entry in the palette.
    /// </summary>
    public class ColorEntry
    {
        public string Name;
        public string Hex;  // "#RRGGBB" or "#RRGGBBAA"
        public string Usage;  // Description of when to use

        public Color ToColor()
        {
            if (ColorUtility.TryParseHtmlString(Hex, out var color))
                return color;
            return Color.magenta;  // Error color
        }
    }

    /// <summary>
    /// Polycount budgets for different asset types.
    /// </summary>
    public class PolycountBudgets
    {
        public int Character;
        public int Prop;
        public int Environment;
        public int Vehicle;
        public int Weapon;
        public int UI;
        public int VFX;
        public Dictionary<string, int> Custom;

        public PolycountBudgets()
        {
            Character = 15000;
            Prop = 3000;
            Environment = 50000;
            Vehicle = 20000;
            Weapon = 5000;
            UI = 500;
            VFX = 1000;
            Custom = new Dictionary<string, int>();
        }

        public int GetBudget(string assetType)
        {
            var type = assetType?.ToLowerInvariant() ?? "prop";
            return type switch
            {
                "character" => Character,
                "prop" => Prop,
                "environment" => Environment,
                "vehicle" => Vehicle,
                "weapon" => Weapon,
                "ui" => UI,
                "vfx" => VFX,
                _ => Custom.TryGetValue(type, out var budget) ? budget : Prop
            };
        }
    }

    /// <summary>
    /// Texture style constraints.
    /// </summary>
    public class TextureStyleConfig
    {
        public int MaxResolution;  // e.g., 2048
        public int MinResolution;  // e.g., 64
        public List<string> AllowedFormats;  // "png", "tga", etc.
        public bool RequirePowerOfTwo;
        public bool AllowMipmaps;
        public string FilterMode;  // "Point", "Bilinear", "Trilinear"
        public string WrapMode;    // "Repeat", "Clamp", "Mirror"

        public TextureStyleConfig()
        {
            MaxResolution = 2048;
            MinResolution = 64;
            AllowedFormats = new List<string> { "png", "tga", "psd" };
            RequirePowerOfTwo = true;
            AllowMipmaps = true;
            FilterMode = "Bilinear";
            WrapMode = "Repeat";
        }
    }

    /// <summary>
    /// Audio style configuration.
    /// </summary>
    public class AudioStyleConfig
    {
        public int MaxLengthSeconds;
        public int MaxFileSizeKB;
        public List<string> AllowedFormats;  // "wav", "ogg", "mp3"
        public int SampleRate;  // e.g., 44100
        public bool RequireMono;  // For 3D sounds
        public bool AllowCompression;
        public float MaxVolumeNormalized;  // 0-1

        public AudioStyleConfig()
        {
            MaxLengthSeconds = 30;  // 30 second max for SFX
            MaxFileSizeKB = 5120;   // 5MB max
            AllowedFormats = new List<string> { "wav", "ogg" };
            SampleRate = 44100;
            RequireMono = false;
            AllowCompression = true;
            MaxVolumeNormalized = 1.0f;
        }
    }

    /// <summary>
    /// UI style configuration.
    /// </summary>
    public class UIStyleConfig
    {
        public List<string> AllowedFonts;
        public int MinFontSize;
        public int MaxFontSize;
        public float MinButtonSize;  // In pixels
        public float MinTouchTargetSize;  // For mobile
        public bool RequireAccessibleContrast;
        public float MinContrastRatio;  // WCAG standard

        public UIStyleConfig()
        {
            AllowedFonts = new List<string>();
            MinFontSize = 12;
            MaxFontSize = 72;
            MinButtonSize = 44;
            MinTouchTargetSize = 48;
            RequireAccessibleContrast = true;
            MinContrastRatio = 4.5f;  // WCAG AA standard
        }
    }

    /// <summary>
    /// 3D model style configuration.
    /// </summary>
    public class ModelStyleConfig
    {
        public float ScaleUnit;  // 1.0 = 1 meter
        public bool RequireNormals;
        public bool RequireTangents;
        public bool RequireUVs;
        public int MaxBonesPerVertex;
        public bool AllowNGons;
        public float MaxTexelDensityVariance;  // For consistent detail level

        public ModelStyleConfig()
        {
            ScaleUnit = 1.0f;
            RequireNormals = true;
            RequireTangents = true;
            RequireUVs = true;
            MaxBonesPerVertex = 4;
            AllowNGons = false;
            MaxTexelDensityVariance = 0.5f;
        }
    }

    #endregion

    #region Validation Result Classes

    /// <summary>
    /// Severity of a style violation.
    /// </summary>
    public enum StyleViolationSeverity
    {
        Info,       // Suggestion
        Warning,    // Should fix
        Error       // Must fix
    }

    /// <summary>
    /// A single style violation found during validation.
    /// </summary>
    public class StyleViolation
    {
        public StyleViolationSeverity Severity;
        public string Rule;           // Which rule was violated
        public string Message;
        public string AssetPath;
        public object ActualValue;
        public object ExpectedValue;
        public string SuggestedFix;

        public StyleViolation() { }

        public StyleViolation(StyleViolationSeverity severity, string rule, string message)
        {
            Severity = severity;
            Rule = rule;
            Message = message;
        }
    }

    /// <summary>
    /// Result of validating a single asset against the style guide.
    /// </summary>
    public class StyleValidationResult
    {
        public string AssetPath;
        public string AssetType;
        public bool Passed;
        public int ErrorCount;
        public int WarningCount;
        public int InfoCount;
        public List<StyleViolation> Violations;
        public float Score;  // 0.0 to 1.0
        public long ValidationTimeMs;

        public StyleValidationResult()
        {
            Violations = new List<StyleViolation>();
        }

        public static StyleValidationResult Pass(string assetPath, string assetType)
        {
            return new StyleValidationResult
            {
                AssetPath = assetPath,
                AssetType = assetType,
                Passed = true,
                Score = 1.0f
            };
        }

        public static StyleValidationResult Fail(string assetPath, string assetType, List<StyleViolation> violations)
        {
            var result = new StyleValidationResult
            {
                AssetPath = assetPath,
                AssetType = assetType,
                Passed = false,
                Violations = violations ?? new List<StyleViolation>()
            };

            foreach (var v in result.Violations)
            {
                switch (v.Severity)
                {
                    case StyleViolationSeverity.Error: result.ErrorCount++; break;
                    case StyleViolationSeverity.Warning: result.WarningCount++; break;
                    case StyleViolationSeverity.Info: result.InfoCount++; break;
                }
            }

            // Score based on violations (errors = -0.3, warnings = -0.1, info = -0.02)
            result.Score = Mathf.Clamp01(1.0f - (result.ErrorCount * 0.3f) - (result.WarningCount * 0.1f) - (result.InfoCount * 0.02f));
            result.Passed = result.ErrorCount == 0;

            return result;
        }
    }

    /// <summary>
    /// Result of batch validation.
    /// </summary>
    public class BatchValidationResult
    {
        public int TotalAssets;
        public int PassedAssets;
        public int FailedAssets;
        public int TotalErrors;
        public int TotalWarnings;
        public float AverageScore;
        public long TotalValidationTimeMs;
        public List<StyleValidationResult> Results;

        public BatchValidationResult()
        {
            Results = new List<StyleValidationResult>();
        }
    }

    #endregion

    /// <summary>
    /// Interface for style guide loading and asset validation.
    /// Part of Layer 7 (Generative Asset Pipeline) - ensures generated assets
    /// maintain visual consistency with the project's art direction.
    /// </summary>
    public interface IStyleGuide
    {
        /// <summary>
        /// Load a style guide configuration from a YAML file.
        /// </summary>
        /// <param name="path">Path to YAML file (relative to project or absolute)</param>
        /// <returns>Loaded style guide configuration</returns>
        StyleGuideConfig LoadStyleGuide(string path);

        /// <summary>
        /// Get the currently loaded style guide.
        /// </summary>
        StyleGuideConfig GetCurrentStyleGuide();

        /// <summary>
        /// Validate an asset against the current style guide.
        /// </summary>
        /// <param name="assetPath">Unity asset path (e.g., "Assets/Models/Player.fbx")</param>
        /// <returns>Validation result with any violations</returns>
        StyleValidationResult ValidateAsset(string assetPath);

        /// <summary>
        /// Validate multiple assets in batch.
        /// </summary>
        /// <param name="assetPaths">List of asset paths to validate</param>
        /// <returns>List of validation results</returns>
        BatchValidationResult ValidateBatch(List<string> assetPaths);

        /// <summary>
        /// Get all colors in the current palette.
        /// </summary>
        List<Color> GetColorPalette();

        /// <summary>
        /// Check if a color is within the palette tolerance.
        /// </summary>
        bool IsColorInPalette(Color color);

        /// <summary>
        /// Get the polycount budget for an asset type.
        /// </summary>
        /// <param name="assetType">Asset type (character, prop, environment, etc.)</param>
        /// <returns>Maximum polygon count</returns>
        int GetPolycountBudget(string assetType);

        /// <summary>
        /// Get the audio style configuration.
        /// </summary>
        AudioStyleConfig GetAudioStyle();

        /// <summary>
        /// Get the texture style configuration.
        /// </summary>
        TextureStyleConfig GetTextureStyle();

        /// <summary>
        /// Get the UI style configuration.
        /// </summary>
        UIStyleConfig GetUIStyle();

        /// <summary>
        /// Get the model style configuration.
        /// </summary>
        ModelStyleConfig GetModelStyle();

        /// <summary>
        /// Find the closest palette color to a given color.
        /// </summary>
        ColorEntry FindClosestPaletteColor(Color color);

        /// <summary>
        /// Reload the style guide from the default location.
        /// </summary>
        void ReloadStyleGuide();
    }
}

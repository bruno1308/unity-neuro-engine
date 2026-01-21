using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NeuroEngine.Core;
using UnityEngine;
using Debug = UnityEngine.Debug;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace NeuroEngine.Services
{
    /// <summary>
    /// Service for loading style guides and validating assets against them.
    /// Part of Layer 7 (Generative Asset Pipeline).
    ///
    /// Style guides are loaded from YAML files in:
    /// 1. hooks/style-guide.yaml (project-specific, preferred)
    /// 2. Assets/Config/style-guide.yaml (alternative location)
    /// 3. Package default template (fallback)
    /// </summary>
    public class StyleGuideService : IStyleGuide
    {
        private StyleGuideConfig _currentGuide;
        private string _currentGuidePath;
        private readonly string _projectRoot;

        // Search paths in priority order
        private readonly string[] _searchPaths = new[]
        {
            "hooks/style-guide.yaml",
            "Assets/Config/style-guide.yaml"
        };

        public StyleGuideService()
        {
            _projectRoot = Path.GetDirectoryName(Application.dataPath);
            LoadDefaultStyleGuide();
        }

        private void LoadDefaultStyleGuide()
        {
            // Try to find existing style guide
            foreach (var path in _searchPaths)
            {
                var fullPath = Path.Combine(_projectRoot, path);
                if (File.Exists(fullPath))
                {
                    try
                    {
                        _currentGuide = LoadStyleGuide(fullPath);
                        _currentGuidePath = fullPath;
                        Debug.Log($"[StyleGuide] Loaded style guide from: {path}");
                        return;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[StyleGuide] Failed to load {path}: {e.Message}");
                    }
                }
            }

            // Create default style guide
            _currentGuide = CreateDefaultStyleGuide();
            _currentGuidePath = null;
            Debug.Log("[StyleGuide] Using default style guide (no YAML found)");
        }

        private StyleGuideConfig CreateDefaultStyleGuide()
        {
            var guide = new StyleGuideConfig
            {
                Name = "Default Style Guide",
                Version = "1.0.0",
                Description = "Default style guide with sensible defaults for indie games"
            };

            // Default color palette (balanced indie game colors)
            guide.Colors.Primary.Add(new ColorEntry { Name = "Primary Blue", Hex = "#4A90D9", Usage = "Main interactive elements" });
            guide.Colors.Primary.Add(new ColorEntry { Name = "Primary Orange", Hex = "#D97B4A", Usage = "Action/attack elements" });
            guide.Colors.Secondary.Add(new ColorEntry { Name = "Secondary Green", Hex = "#5DB85D", Usage = "Health/positive feedback" });
            guide.Colors.Secondary.Add(new ColorEntry { Name = "Secondary Red", Hex = "#D95B5B", Usage = "Damage/negative feedback" });
            guide.Colors.Accent.Add(new ColorEntry { Name = "Gold", Hex = "#FFD700", Usage = "Highlights and rewards" });
            guide.Colors.Neutrals.Add(new ColorEntry { Name = "Dark", Hex = "#2D2D2D", Usage = "Backgrounds" });
            guide.Colors.Neutrals.Add(new ColorEntry { Name = "Light", Hex = "#F5F5F5", Usage = "Text and UI" });
            guide.Colors.ColorTolerance = 0.15f;

            return guide;
        }

        public StyleGuideConfig LoadStyleGuide(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            // Handle relative paths
            string fullPath = path;
            if (!Path.IsPathRooted(path))
            {
                fullPath = Path.Combine(_projectRoot, path);
            }

            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Style guide not found: {fullPath}");

            var yaml = File.ReadAllText(fullPath);
            var guide = ParseYaml(yaml);

            _currentGuide = guide;
            _currentGuidePath = fullPath;

            return guide;
        }

        /// <summary>
        /// Parse YAML content into StyleGuideConfig.
        /// Uses a simple parser since we can't depend on external YAML libraries at runtime.
        /// </summary>
        private StyleGuideConfig ParseYaml(string yaml)
        {
            var guide = new StyleGuideConfig();
            var lines = yaml.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            string currentSection = null;
            string currentSubSection = null;
            string currentColorGroup = null;

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd();

                // Skip comments and empty lines
                if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#"))
                    continue;

                var indent = line.Length - line.TrimStart().Length;
                var content = line.Trim();

                // Root level properties
                if (indent == 0 && content.Contains(":"))
                {
                    var parts = content.Split(new[] { ':' }, 2);
                    var key = parts[0].Trim().ToLowerInvariant();
                    var value = parts.Length > 1 ? parts[1].Trim() : "";

                    switch (key)
                    {
                        case "name": guide.Name = value; break;
                        case "version": guide.Version = value; break;
                        case "description": guide.Description = value; break;
                        case "colors": currentSection = "colors"; break;
                        case "polycount": currentSection = "polycount"; break;
                        case "textures": currentSection = "textures"; break;
                        case "audio": currentSection = "audio"; break;
                        case "ui": currentSection = "ui"; break;
                        case "models": currentSection = "models"; break;
                    }
                    continue;
                }

                // Section parsing
                if (currentSection != null)
                {
                    ParseSectionLine(guide, currentSection, ref currentSubSection, ref currentColorGroup, indent, content);
                }
            }

            return guide;
        }

        private void ParseSectionLine(StyleGuideConfig guide, string section, ref string subSection, ref string colorGroup, int indent, string content)
        {
            var parts = content.Split(new[] { ':' }, 2);
            var key = parts[0].Trim().ToLowerInvariant().Replace("-", "").Replace("_", "");
            var value = parts.Length > 1 ? parts[1].Trim().Trim('"', '\'') : "";

            switch (section)
            {
                case "colors":
                    ParseColorsSection(guide.Colors, ref colorGroup, indent, key, value);
                    break;
                case "polycount":
                    ParsePolycountSection(guide.Polycount, key, value);
                    break;
                case "textures":
                    ParseTexturesSection(guide.Textures, key, value);
                    break;
                case "audio":
                    ParseAudioSection(guide.Audio, key, value);
                    break;
                case "ui":
                    ParseUISection(guide.UI, key, value);
                    break;
                case "models":
                    ParseModelsSection(guide.Models, key, value);
                    break;
            }
        }

        private void ParseColorsSection(ColorPaletteConfig colors, ref string colorGroup, int indent, string key, string value)
        {
            // Detect color group
            if (indent <= 2)
            {
                switch (key)
                {
                    case "primary": colorGroup = "primary"; return;
                    case "secondary": colorGroup = "secondary"; return;
                    case "accent": colorGroup = "accent"; return;
                    case "neutrals": colorGroup = "neutrals"; return;
                    case "tolerance":
                    case "colortolerance":
                        if (float.TryParse(value, out var tol))
                            colors.ColorTolerance = tol;
                        return;
                }
            }

            // Parse color entry (looking for name or hex)
            if (key == "name" || key == "hex" || key == "usage")
            {
                var list = colorGroup switch
                {
                    "primary" => colors.Primary,
                    "secondary" => colors.Secondary,
                    "accent" => colors.Accent,
                    "neutrals" => colors.Neutrals,
                    _ => null
                };

                if (list != null)
                {
                    // Ensure there's a color entry to update
                    if (list.Count == 0 || (key == "name" && !string.IsNullOrEmpty(list[list.Count - 1].Name)))
                    {
                        list.Add(new ColorEntry());
                    }

                    var entry = list[list.Count - 1];
                    switch (key)
                    {
                        case "name": entry.Name = value; break;
                        case "hex": entry.Hex = value; break;
                        case "usage": entry.Usage = value; break;
                    }
                }
            }
        }

        private void ParsePolycountSection(PolycountBudgets poly, string key, string value)
        {
            if (int.TryParse(value, out var count))
            {
                switch (key)
                {
                    case "character": poly.Character = count; break;
                    case "prop": poly.Prop = count; break;
                    case "environment": poly.Environment = count; break;
                    case "vehicle": poly.Vehicle = count; break;
                    case "weapon": poly.Weapon = count; break;
                    case "ui": poly.UI = count; break;
                    case "vfx": poly.VFX = count; break;
                    default:
                        poly.Custom[key] = count;
                        break;
                }
            }
        }

        private void ParseTexturesSection(TextureStyleConfig tex, string key, string value)
        {
            switch (key)
            {
                case "maxresolution":
                    if (int.TryParse(value, out var maxRes)) tex.MaxResolution = maxRes;
                    break;
                case "minresolution":
                    if (int.TryParse(value, out var minRes)) tex.MinResolution = minRes;
                    break;
                case "requirepoweroftwo":
                    tex.RequirePowerOfTwo = value.ToLowerInvariant() == "true";
                    break;
                case "allowmipmaps":
                    tex.AllowMipmaps = value.ToLowerInvariant() == "true";
                    break;
                case "filtermode":
                    tex.FilterMode = value;
                    break;
                case "wrapmode":
                    tex.WrapMode = value;
                    break;
            }
        }

        private void ParseAudioSection(AudioStyleConfig audio, string key, string value)
        {
            switch (key)
            {
                case "maxlengthseconds":
                case "maxlength":
                    if (int.TryParse(value, out var maxLen)) audio.MaxLengthSeconds = maxLen;
                    break;
                case "maxfilesizekb":
                case "maxfilesize":
                    if (int.TryParse(value, out var maxSize)) audio.MaxFileSizeKB = maxSize;
                    break;
                case "samplerate":
                    if (int.TryParse(value, out var rate)) audio.SampleRate = rate;
                    break;
                case "requiremono":
                    audio.RequireMono = value.ToLowerInvariant() == "true";
                    break;
                case "allowcompression":
                    audio.AllowCompression = value.ToLowerInvariant() == "true";
                    break;
            }
        }

        private void ParseUISection(UIStyleConfig ui, string key, string value)
        {
            switch (key)
            {
                case "minfontsize":
                    if (int.TryParse(value, out var minFont)) ui.MinFontSize = minFont;
                    break;
                case "maxfontsize":
                    if (int.TryParse(value, out var maxFont)) ui.MaxFontSize = maxFont;
                    break;
                case "minbuttonsize":
                    if (float.TryParse(value, out var minBtn)) ui.MinButtonSize = minBtn;
                    break;
                case "mintouchtargetsize":
                    if (float.TryParse(value, out var minTouch)) ui.MinTouchTargetSize = minTouch;
                    break;
                case "mincontrastratio":
                    if (float.TryParse(value, out var contrast)) ui.MinContrastRatio = contrast;
                    break;
                case "requireaccessiblecontrast":
                    ui.RequireAccessibleContrast = value.ToLowerInvariant() == "true";
                    break;
            }
        }

        private void ParseModelsSection(ModelStyleConfig models, string key, string value)
        {
            switch (key)
            {
                case "scaleunit":
                    if (float.TryParse(value, out var scale)) models.ScaleUnit = scale;
                    break;
                case "requirenormals":
                    models.RequireNormals = value.ToLowerInvariant() == "true";
                    break;
                case "requiretangents":
                    models.RequireTangents = value.ToLowerInvariant() == "true";
                    break;
                case "requireuvs":
                    models.RequireUVs = value.ToLowerInvariant() == "true";
                    break;
                case "maxbonespervertex":
                    if (int.TryParse(value, out var bones)) models.MaxBonesPerVertex = bones;
                    break;
                case "allowngons":
                    models.AllowNGons = value.ToLowerInvariant() == "true";
                    break;
            }
        }

        public StyleGuideConfig GetCurrentStyleGuide()
        {
            return _currentGuide ?? CreateDefaultStyleGuide();
        }

        public StyleValidationResult ValidateAsset(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return StyleValidationResult.Fail(assetPath, "unknown", new List<StyleViolation>
                {
                    new StyleViolation(StyleViolationSeverity.Error, "path", "Asset path is null or empty")
                });
            }

            var sw = Stopwatch.StartNew();
            var violations = new List<StyleViolation>();
            var assetType = DetermineAssetType(assetPath);

            switch (assetType)
            {
                case "model":
                    ValidateModel(assetPath, violations);
                    break;
                case "texture":
                    ValidateTexture(assetPath, violations);
                    break;
                case "audio":
                    ValidateAudio(assetPath, violations);
                    break;
                case "ui":
                    ValidateUI(assetPath, violations);
                    break;
                default:
                    // Unknown asset type - just return pass
                    break;
            }

            sw.Stop();

            var result = violations.Count > 0
                ? StyleValidationResult.Fail(assetPath, assetType, violations)
                : StyleValidationResult.Pass(assetPath, assetType);

            result.ValidationTimeMs = sw.ElapsedMilliseconds;
            return result;
        }

        private string DetermineAssetType(string assetPath)
        {
            var ext = Path.GetExtension(assetPath).ToLowerInvariant();
            return ext switch
            {
                ".fbx" or ".obj" or ".blend" or ".dae" or ".3ds" or ".gltf" or ".glb" => "model",
                ".png" or ".jpg" or ".jpeg" or ".tga" or ".psd" or ".tif" or ".tiff" or ".bmp" => "texture",
                ".wav" or ".ogg" or ".mp3" or ".aiff" or ".aif" or ".flac" => "audio",
                ".uxml" or ".uss" => "ui",
                _ => "unknown"
            };
        }

        private void ValidateModel(string assetPath, List<StyleViolation> violations)
        {
#if UNITY_EDITOR
            var modelImporter = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (modelImporter == null)
            {
                violations.Add(new StyleViolation(StyleViolationSeverity.Error, "model.importer", "Could not load model importer")
                {
                    AssetPath = assetPath
                });
                return;
            }

            // Try to determine polycount budget based on path/name
            var assetType = GuessModelType(assetPath);
            var budget = GetPolycountBudget(assetType);

            // Load the mesh to check polycount
            var meshFilter = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (meshFilter != null)
            {
                var polyCount = meshFilter.triangles.Length / 3;
                if (polyCount > budget)
                {
                    violations.Add(new StyleViolation(StyleViolationSeverity.Warning, "model.polycount",
                        $"Model has {polyCount} polygons, exceeds budget of {budget} for type '{assetType}'")
                    {
                        AssetPath = assetPath,
                        ActualValue = polyCount,
                        ExpectedValue = budget,
                        SuggestedFix = $"Reduce polygon count by {polyCount - budget} polygons"
                    });
                }
            }

            // Check model import settings
            var modelStyle = _currentGuide?.Models ?? new ModelStyleConfig();

            if (modelStyle.RequireNormals && modelImporter.importNormals == ModelImporterNormals.None)
            {
                violations.Add(new StyleViolation(StyleViolationSeverity.Warning, "model.normals",
                    "Model is set to not import normals, but style guide requires them")
                {
                    AssetPath = assetPath,
                    SuggestedFix = "Set Import Normals to 'Import' or 'Calculate'"
                });
            }

            if (modelStyle.RequireTangents && modelImporter.importTangents == ModelImporterTangents.None)
            {
                violations.Add(new StyleViolation(StyleViolationSeverity.Info, "model.tangents",
                    "Model is set to not import tangents")
                {
                    AssetPath = assetPath,
                    SuggestedFix = "Set Import Tangents to 'Import' or 'Calculate' if using normal maps"
                });
            }
#endif
        }

        private string GuessModelType(string assetPath)
        {
            var lower = assetPath.ToLowerInvariant();
            if (lower.Contains("character") || lower.Contains("player") || lower.Contains("enemy") || lower.Contains("npc"))
                return "character";
            if (lower.Contains("vehicle") || lower.Contains("car") || lower.Contains("truck"))
                return "vehicle";
            if (lower.Contains("weapon") || lower.Contains("gun") || lower.Contains("sword"))
                return "weapon";
            if (lower.Contains("environment") || lower.Contains("terrain") || lower.Contains("building"))
                return "environment";
            if (lower.Contains("vfx") || lower.Contains("effect") || lower.Contains("particle"))
                return "vfx";
            return "prop";
        }

        private void ValidateTexture(string assetPath, List<StyleViolation> violations)
        {
#if UNITY_EDITOR
            var textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (textureImporter == null)
            {
                violations.Add(new StyleViolation(StyleViolationSeverity.Error, "texture.importer", "Could not load texture importer")
                {
                    AssetPath = assetPath
                });
                return;
            }

            var texStyle = _currentGuide?.Textures ?? new TextureStyleConfig();

            // Check resolution
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture != null)
            {
                var maxDim = Mathf.Max(texture.width, texture.height);
                if (maxDim > texStyle.MaxResolution)
                {
                    violations.Add(new StyleViolation(StyleViolationSeverity.Warning, "texture.resolution",
                        $"Texture resolution ({texture.width}x{texture.height}) exceeds maximum ({texStyle.MaxResolution})")
                    {
                        AssetPath = assetPath,
                        ActualValue = maxDim,
                        ExpectedValue = texStyle.MaxResolution,
                        SuggestedFix = $"Resize texture to {texStyle.MaxResolution}x{texStyle.MaxResolution} or smaller"
                    });
                }

                if (texStyle.RequirePowerOfTwo)
                {
                    if (!IsPowerOfTwo(texture.width) || !IsPowerOfTwo(texture.height))
                    {
                        violations.Add(new StyleViolation(StyleViolationSeverity.Warning, "texture.poweroftwo",
                            $"Texture dimensions ({texture.width}x{texture.height}) are not power of two")
                        {
                            AssetPath = assetPath,
                            SuggestedFix = "Resize to nearest power of two (256, 512, 1024, 2048)"
                        });
                    }
                }
            }
#endif
        }

        private bool IsPowerOfTwo(int value)
        {
            return value > 0 && (value & (value - 1)) == 0;
        }

        private void ValidateAudio(string assetPath, List<StyleViolation> violations)
        {
#if UNITY_EDITOR
            var audioImporter = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (audioImporter == null)
            {
                violations.Add(new StyleViolation(StyleViolationSeverity.Error, "audio.importer", "Could not load audio importer")
                {
                    AssetPath = assetPath
                });
                return;
            }

            var audioStyle = _currentGuide?.Audio ?? new AudioStyleConfig();

            // Load clip to check properties
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip != null)
            {
                // Check length
                if (clip.length > audioStyle.MaxLengthSeconds)
                {
                    violations.Add(new StyleViolation(StyleViolationSeverity.Warning, "audio.length",
                        $"Audio clip is {clip.length:F1} seconds, exceeds maximum of {audioStyle.MaxLengthSeconds} seconds")
                    {
                        AssetPath = assetPath,
                        ActualValue = clip.length,
                        ExpectedValue = audioStyle.MaxLengthSeconds,
                        SuggestedFix = "Trim audio clip or consider if it should be background music"
                    });
                }

                // Check channels if mono required
                if (audioStyle.RequireMono && clip.channels > 1)
                {
                    violations.Add(new StyleViolation(StyleViolationSeverity.Info, "audio.channels",
                        $"Audio clip has {clip.channels} channels, but mono is preferred for 3D sounds")
                    {
                        AssetPath = assetPath,
                        ActualValue = clip.channels,
                        ExpectedValue = 1,
                        SuggestedFix = "Enable 'Force To Mono' in audio import settings for 3D spatial audio"
                    });
                }
            }

            // Check file size
            var fileInfo = new FileInfo(Path.Combine(_projectRoot, assetPath));
            if (fileInfo.Exists)
            {
                var sizeKB = fileInfo.Length / 1024;
                if (sizeKB > audioStyle.MaxFileSizeKB)
                {
                    violations.Add(new StyleViolation(StyleViolationSeverity.Warning, "audio.filesize",
                        $"Audio file is {sizeKB}KB, exceeds maximum of {audioStyle.MaxFileSizeKB}KB")
                    {
                        AssetPath = assetPath,
                        ActualValue = sizeKB,
                        ExpectedValue = audioStyle.MaxFileSizeKB,
                        SuggestedFix = "Compress audio or reduce sample rate"
                    });
                }
            }
#endif
        }

        private void ValidateUI(string assetPath, List<StyleViolation> violations)
        {
            // UI validation for UXML/USS files
            // This is mostly structural - color/font validation requires parsing the files
            var fullPath = Path.Combine(_projectRoot, assetPath);
            if (!File.Exists(fullPath))
            {
                violations.Add(new StyleViolation(StyleViolationSeverity.Error, "ui.file", "UI file not found")
                {
                    AssetPath = assetPath
                });
                return;
            }

            var content = File.ReadAllText(fullPath);
            var uiStyle = _currentGuide?.UI ?? new UIStyleConfig();

            // Check for hardcoded colors in USS (should use variables)
            if (assetPath.EndsWith(".uss"))
            {
                // Look for hex colors that aren't in palette
                var hexPattern = "#[0-9A-Fa-f]{6}";
                var matches = System.Text.RegularExpressions.Regex.Matches(content, hexPattern);
                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (ColorUtility.TryParseHtmlString(match.Value, out var color))
                    {
                        if (!IsColorInPalette(color))
                        {
                            violations.Add(new StyleViolation(StyleViolationSeverity.Info, "ui.color",
                                $"Color {match.Value} not found in style guide palette")
                            {
                                AssetPath = assetPath,
                                ActualValue = match.Value,
                                SuggestedFix = $"Use closest palette color: {FindClosestPaletteColor(color)?.Hex ?? "N/A"}"
                            });
                        }
                    }
                }
            }
        }

        public BatchValidationResult ValidateBatch(List<string> assetPaths)
        {
            var result = new BatchValidationResult();
            var sw = Stopwatch.StartNew();

            foreach (var path in assetPaths)
            {
                var validationResult = ValidateAsset(path);
                result.Results.Add(validationResult);

                result.TotalAssets++;
                if (validationResult.Passed)
                    result.PassedAssets++;
                else
                    result.FailedAssets++;

                result.TotalErrors += validationResult.ErrorCount;
                result.TotalWarnings += validationResult.WarningCount;
            }

            sw.Stop();
            result.TotalValidationTimeMs = sw.ElapsedMilliseconds;
            result.AverageScore = result.Results.Count > 0
                ? result.Results.Average(r => r.Score)
                : 1.0f;

            return result;
        }

        public List<Color> GetColorPalette()
        {
            var palette = _currentGuide?.Colors ?? new ColorPaletteConfig();
            return palette.GetAllColors().Select(c => c.ToColor()).ToList();
        }

        public bool IsColorInPalette(Color color)
        {
            var palette = _currentGuide?.Colors ?? new ColorPaletteConfig();
            var tolerance = palette.ColorTolerance;

            foreach (var entry in palette.GetAllColors())
            {
                var paletteColor = entry.ToColor();
                var distance = ColorDistance(color, paletteColor);
                if (distance <= tolerance)
                    return true;
            }

            return false;
        }

        private float ColorDistance(Color a, Color b)
        {
            // Simple Euclidean distance in RGB space
            var dr = a.r - b.r;
            var dg = a.g - b.g;
            var db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }

        public int GetPolycountBudget(string assetType)
        {
            var poly = _currentGuide?.Polycount ?? new PolycountBudgets();
            return poly.GetBudget(assetType);
        }

        public AudioStyleConfig GetAudioStyle()
        {
            return _currentGuide?.Audio ?? new AudioStyleConfig();
        }

        public TextureStyleConfig GetTextureStyle()
        {
            return _currentGuide?.Textures ?? new TextureStyleConfig();
        }

        public UIStyleConfig GetUIStyle()
        {
            return _currentGuide?.UI ?? new UIStyleConfig();
        }

        public ModelStyleConfig GetModelStyle()
        {
            return _currentGuide?.Models ?? new ModelStyleConfig();
        }

        public ColorEntry FindClosestPaletteColor(Color color)
        {
            var palette = _currentGuide?.Colors ?? new ColorPaletteConfig();
            ColorEntry closest = null;
            float minDistance = float.MaxValue;

            foreach (var entry in palette.GetAllColors())
            {
                var paletteColor = entry.ToColor();
                var distance = ColorDistance(color, paletteColor);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = entry;
                }
            }

            return closest;
        }

        public void ReloadStyleGuide()
        {
            LoadDefaultStyleGuide();
        }
    }
}

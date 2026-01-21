#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using NeuroEngine.Core;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace NeuroEngine.Editor.MCPTools
{
    /// <summary>
    /// MCP tool for validating assets against the project style guide.
    /// Part of Layer 7 (Generative Asset Pipeline).
    ///
    /// Usage:
    /// - validate_style action=validate path="Assets/Models/Player.fbx"
    /// - validate_style action=validate_batch paths=["Assets/Models/Player.fbx", "Assets/Textures/Floor.png"]
    /// - validate_style action=get_style_guide
    /// - validate_style action=reload_style_guide
    /// - validate_style action=get_palette
    /// - validate_style action=check_color hex="#FF5500"
    /// - validate_style action=get_budget asset_type="character"
    /// </summary>
    [McpForUnityTool("validate_style", Description = "Validates assets against the project style guide. Can validate single assets, batches, check colors against palette, and get polycount budgets.")]
    public static class ValidateStyle
    {
        public static object HandleCommand(JObject @params)
        {
            string action = @params["action"]?.ToString() ?? "validate";

            try
            {
                var styleGuide = EditorServiceLocator.Get<IStyleGuide>();

                return action.ToLowerInvariant() switch
                {
                    "validate" => ValidateAsset(@params, styleGuide),
                    "validate_batch" or "validatebatch" => ValidateBatch(@params, styleGuide),
                    "get_style_guide" or "getstyleguide" => GetStyleGuide(styleGuide),
                    "reload_style_guide" or "reloadstyleguide" => ReloadStyleGuide(styleGuide),
                    "get_palette" or "getpalette" => GetPalette(styleGuide),
                    "check_color" or "checkcolor" => CheckColor(@params, styleGuide),
                    "get_budget" or "getbudget" => GetBudget(@params, styleGuide),
                    "find_closest_color" or "findclosestcolor" => FindClosestColor(@params, styleGuide),
                    _ => new ErrorResponse($"Unknown action: {action}. Valid actions: validate, validate_batch, get_style_guide, reload_style_guide, get_palette, check_color, get_budget, find_closest_color")
                };
            }
            catch (Exception e)
            {
                return new ErrorResponse($"Error in validate_style: {e.Message}");
            }
        }

        private static object ValidateAsset(JObject @params, IStyleGuide styleGuide)
        {
            string path = @params["path"]?.ToString();
            if (string.IsNullOrEmpty(path))
            {
                return new ErrorResponse("Missing required parameter: path");
            }

            // Verify asset exists
            if (!File.Exists(Path.Combine(Path.GetDirectoryName(Application.dataPath), path)))
            {
                // Try as Unity asset path
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (asset == null)
                {
                    return new ErrorResponse($"Asset not found: {path}");
                }
            }

            var result = styleGuide.ValidateAsset(path);

            return new SuccessResponse(
                result.Passed
                    ? $"Asset '{path}' passed style validation with score {result.Score:F2}"
                    : $"Asset '{path}' has {result.ErrorCount} errors, {result.WarningCount} warnings",
                new
                {
                    passed = result.Passed,
                    asset_path = result.AssetPath,
                    asset_type = result.AssetType,
                    score = result.Score,
                    error_count = result.ErrorCount,
                    warning_count = result.WarningCount,
                    info_count = result.InfoCount,
                    validation_time_ms = result.ValidationTimeMs,
                    violations = result.Violations.Select(v => new
                    {
                        severity = v.Severity.ToString().ToLowerInvariant(),
                        rule = v.Rule,
                        message = v.Message,
                        actual_value = v.ActualValue?.ToString(),
                        expected_value = v.ExpectedValue?.ToString(),
                        suggested_fix = v.SuggestedFix
                    }).ToList()
                });
        }

        private static object ValidateBatch(JObject @params, IStyleGuide styleGuide)
        {
            var pathsToken = @params["paths"];
            if (pathsToken == null)
            {
                return new ErrorResponse("Missing required parameter: paths (array of asset paths)");
            }

            List<string> paths;
            if (pathsToken.Type == JTokenType.Array)
            {
                paths = pathsToken.ToObject<List<string>>();
            }
            else if (pathsToken.Type == JTokenType.String)
            {
                // Support comma-separated string
                paths = pathsToken.ToString().Split(',').Select(p => p.Trim()).ToList();
            }
            else
            {
                return new ErrorResponse("Parameter 'paths' must be an array of strings or comma-separated string");
            }

            var result = styleGuide.ValidateBatch(paths);

            return new SuccessResponse(
                $"Validated {result.TotalAssets} assets: {result.PassedAssets} passed, {result.FailedAssets} failed (avg score: {result.AverageScore:F2})",
                new
                {
                    total_assets = result.TotalAssets,
                    passed_assets = result.PassedAssets,
                    failed_assets = result.FailedAssets,
                    total_errors = result.TotalErrors,
                    total_warnings = result.TotalWarnings,
                    average_score = result.AverageScore,
                    total_validation_time_ms = result.TotalValidationTimeMs,
                    results = result.Results.Select(r => new
                    {
                        asset_path = r.AssetPath,
                        asset_type = r.AssetType,
                        passed = r.Passed,
                        score = r.Score,
                        error_count = r.ErrorCount,
                        warning_count = r.WarningCount,
                        violations = r.Violations.Select(v => new
                        {
                            severity = v.Severity.ToString().ToLowerInvariant(),
                            rule = v.Rule,
                            message = v.Message
                        }).ToList()
                    }).ToList()
                });
        }

        private static object GetStyleGuide(IStyleGuide styleGuide)
        {
            var guide = styleGuide.GetCurrentStyleGuide();

            return new SuccessResponse($"Style guide: {guide.Name} v{guide.Version}", new
            {
                name = guide.Name,
                version = guide.Version,
                description = guide.Description,
                colors = new
                {
                    primary = guide.Colors.Primary.Select(c => new { name = c.Name, hex = c.Hex, usage = c.Usage }).ToList(),
                    secondary = guide.Colors.Secondary.Select(c => new { name = c.Name, hex = c.Hex, usage = c.Usage }).ToList(),
                    accent = guide.Colors.Accent.Select(c => new { name = c.Name, hex = c.Hex, usage = c.Usage }).ToList(),
                    neutrals = guide.Colors.Neutrals.Select(c => new { name = c.Name, hex = c.Hex, usage = c.Usage }).ToList(),
                    color_tolerance = guide.Colors.ColorTolerance
                },
                polycount = new
                {
                    character = guide.Polycount.Character,
                    prop = guide.Polycount.Prop,
                    environment = guide.Polycount.Environment,
                    vehicle = guide.Polycount.Vehicle,
                    weapon = guide.Polycount.Weapon,
                    ui = guide.Polycount.UI,
                    vfx = guide.Polycount.VFX,
                    custom = guide.Polycount.Custom
                },
                textures = new
                {
                    max_resolution = guide.Textures.MaxResolution,
                    min_resolution = guide.Textures.MinResolution,
                    require_power_of_two = guide.Textures.RequirePowerOfTwo,
                    filter_mode = guide.Textures.FilterMode,
                    wrap_mode = guide.Textures.WrapMode
                },
                audio = new
                {
                    max_length_seconds = guide.Audio.MaxLengthSeconds,
                    max_file_size_kb = guide.Audio.MaxFileSizeKB,
                    sample_rate = guide.Audio.SampleRate,
                    require_mono = guide.Audio.RequireMono
                },
                ui = new
                {
                    min_font_size = guide.UI.MinFontSize,
                    max_font_size = guide.UI.MaxFontSize,
                    min_button_size = guide.UI.MinButtonSize,
                    min_contrast_ratio = guide.UI.MinContrastRatio
                },
                models = new
                {
                    scale_unit = guide.Models.ScaleUnit,
                    require_normals = guide.Models.RequireNormals,
                    require_tangents = guide.Models.RequireTangents,
                    require_uvs = guide.Models.RequireUVs,
                    max_bones_per_vertex = guide.Models.MaxBonesPerVertex
                }
            });
        }

        private static object ReloadStyleGuide(IStyleGuide styleGuide)
        {
            styleGuide.ReloadStyleGuide();
            var guide = styleGuide.GetCurrentStyleGuide();

            return new SuccessResponse($"Style guide reloaded: {guide.Name} v{guide.Version}", new
            {
                name = guide.Name,
                version = guide.Version,
                color_count = guide.Colors.GetAllColors().Count,
                polycount_types = 7 + guide.Polycount.Custom.Count
            });
        }

        private static object GetPalette(IStyleGuide styleGuide)
        {
            var colors = styleGuide.GetColorPalette();
            var guide = styleGuide.GetCurrentStyleGuide();

            var palette = new List<object>();
            foreach (var entry in guide.Colors.GetAllColors())
            {
                palette.Add(new
                {
                    name = entry.Name,
                    hex = entry.Hex,
                    usage = entry.Usage
                });
            }

            return new SuccessResponse($"Color palette with {palette.Count} colors", new
            {
                color_count = palette.Count,
                tolerance = guide.Colors.ColorTolerance,
                colors = palette
            });
        }

        private static object CheckColor(JObject @params, IStyleGuide styleGuide)
        {
            string hex = @params["hex"]?.ToString();
            if (string.IsNullOrEmpty(hex))
            {
                return new ErrorResponse("Missing required parameter: hex (color in #RRGGBB format)");
            }

            // Ensure hex starts with #
            if (!hex.StartsWith("#"))
                hex = "#" + hex;

            if (!ColorUtility.TryParseHtmlString(hex, out var color))
            {
                return new ErrorResponse($"Invalid color format: {hex}. Use #RRGGBB or #RRGGBBAA format.");
            }

            var isInPalette = styleGuide.IsColorInPalette(color);
            var closest = styleGuide.FindClosestPaletteColor(color);

            return new SuccessResponse(
                isInPalette
                    ? $"Color {hex} is within palette tolerance"
                    : $"Color {hex} is NOT in palette. Closest: {closest?.Hex ?? "N/A"}",
                new
                {
                    input_color = hex,
                    in_palette = isInPalette,
                    closest_palette_color = closest != null ? new
                    {
                        name = closest.Name,
                        hex = closest.Hex,
                        usage = closest.Usage
                    } : null
                });
        }

        private static object FindClosestColor(JObject @params, IStyleGuide styleGuide)
        {
            string hex = @params["hex"]?.ToString();
            if (string.IsNullOrEmpty(hex))
            {
                return new ErrorResponse("Missing required parameter: hex (color in #RRGGBB format)");
            }

            if (!hex.StartsWith("#"))
                hex = "#" + hex;

            if (!ColorUtility.TryParseHtmlString(hex, out var color))
            {
                return new ErrorResponse($"Invalid color format: {hex}");
            }

            var closest = styleGuide.FindClosestPaletteColor(color);

            if (closest == null)
            {
                return new ErrorResponse("No colors in palette");
            }

            return new SuccessResponse($"Closest palette color to {hex}: {closest.Name} ({closest.Hex})", new
            {
                input_color = hex,
                closest = new
                {
                    name = closest.Name,
                    hex = closest.Hex,
                    usage = closest.Usage
                }
            });
        }

        private static object GetBudget(JObject @params, IStyleGuide styleGuide)
        {
            string assetType = @params["asset_type"]?.ToString() ?? @params["type"]?.ToString() ?? "prop";

            var budget = styleGuide.GetPolycountBudget(assetType);
            var guide = styleGuide.GetCurrentStyleGuide();

            return new SuccessResponse($"Polycount budget for '{assetType}': {budget} polygons", new
            {
                asset_type = assetType,
                budget = budget,
                all_budgets = new
                {
                    character = guide.Polycount.Character,
                    prop = guide.Polycount.Prop,
                    environment = guide.Polycount.Environment,
                    vehicle = guide.Polycount.Vehicle,
                    weapon = guide.Polycount.Weapon,
                    ui = guide.Polycount.UI,
                    vfx = guide.Polycount.VFX
                }
            });
        }
    }
}
#endif

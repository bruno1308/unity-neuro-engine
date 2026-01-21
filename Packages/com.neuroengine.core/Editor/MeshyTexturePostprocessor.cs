using UnityEditor;
using UnityEngine;

namespace NeuroEngine.Editor
{
    /// <summary>
    /// Automatically configures import settings for Meshy-generated textures.
    ///
    /// Meshy.ai exports textures with specific suffixes that indicate their purpose:
    /// - _Normal.png: Normal maps (need NormalMap texture type)
    /// - _Metallic.png: Metallic maps (need linear color space)
    /// - _Roughness.png: Roughness maps (need linear color space)
    /// - _BaseColor.png: Albedo/diffuse (keep default sRGB)
    ///
    /// Without this processor, Unity imports all textures as default type,
    /// causing popups asking to fix normal map settings and incorrect rendering
    /// for metallic/roughness maps.
    /// </summary>
    public class MeshyTexturePostprocessor : AssetPostprocessor
    {
        /// <summary>
        /// Called before texture import. Configures settings based on texture suffix.
        /// </summary>
        private void OnPreprocessTexture()
        {
            // Early exit for null/empty paths
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            // Only process textures in Meshy output directories
            // Meshy assets are typically placed in Models/Textures folders per iteration
            if (!IsMeshyTexture(assetPath))
            {
                return;
            }

            try
            {
                TextureImporter importer = assetImporter as TextureImporter;
                if (importer == null)
                {
                    return;
                }

                // Check for normal maps
                if (IsNormalMap(assetPath))
                {
                    ConfigureAsNormalMap(importer);
                }
                // Check for metallic/roughness maps (linear data textures)
                else if (IsLinearDataTexture(assetPath))
                {
                    ConfigureAsLinearTexture(importer);
                }
                // Base color textures keep default sRGB settings
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[NeuroEngine] Failed to configure texture import settings for {assetPath}: {ex.Message}");
            }
        }

        /// <summary>
        /// Determines if this texture is from Meshy based on path.
        /// Matches paths containing Models/Textures which is our convention for Meshy outputs.
        /// </summary>
        private static bool IsMeshyTexture(string path)
        {
            // Match iteration-based structure: Assets/IterationX/Models/Textures/
            // Also match generic Models/Textures paths
            return path.Contains("Models/Textures") ||
                   path.Contains("Models\\Textures");
        }

        /// <summary>
        /// Checks if texture is a normal map based on filename suffix.
        /// </summary>
        private static bool IsNormalMap(string path)
        {
            string lowerPath = path.ToLowerInvariant();
            return lowerPath.EndsWith("_normal.png") ||
                   lowerPath.EndsWith("_normal.jpg") ||
                   lowerPath.EndsWith("_normal.tga") ||
                   lowerPath.EndsWith("_normalmap.png") ||
                   lowerPath.EndsWith("_nrm.png");
        }

        /// <summary>
        /// Checks if texture is a linear data texture (metallic, roughness, AO, height).
        /// These textures contain data values, not colors, so must be imported in linear space.
        /// </summary>
        private static bool IsLinearDataTexture(string path)
        {
            string lowerPath = path.ToLowerInvariant();
            return lowerPath.EndsWith("_metallic.png") ||
                   lowerPath.EndsWith("_metallic.jpg") ||
                   lowerPath.EndsWith("_roughness.png") ||
                   lowerPath.EndsWith("_roughness.jpg") ||
                   lowerPath.EndsWith("_metalness.png") ||
                   lowerPath.EndsWith("_ao.png") ||
                   lowerPath.EndsWith("_occlusion.png") ||
                   lowerPath.EndsWith("_height.png") ||
                   lowerPath.EndsWith("_displacement.png") ||
                   lowerPath.EndsWith("_mask.png");
        }

        /// <summary>
        /// Configures texture importer for normal map usage.
        /// </summary>
        private static void ConfigureAsNormalMap(TextureImporter importer)
        {
            importer.textureType = TextureImporterType.NormalMap;
            // Normal maps should not be sprites
            importer.spriteImportMode = SpriteImportMode.None;
            // Ensure proper compression for normal maps
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }

        /// <summary>
        /// Configures texture importer for linear data (non-color) textures.
        /// </summary>
        private static void ConfigureAsLinearTexture(TextureImporter importer)
        {
            // Linear color space for data textures
            importer.sRGBTexture = false;
            // Keep as Default type but with linear space
            importer.textureType = TextureImporterType.Default;
            // Not a sprite
            importer.spriteImportMode = SpriteImportMode.None;
            // Good compression for data textures
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}

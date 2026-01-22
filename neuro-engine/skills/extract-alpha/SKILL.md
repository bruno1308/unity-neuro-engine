---
name: extract-alpha
description: Extract true transparency from AI-generated images using difference matting technique.
---

# Skill: extract-alpha

## When to Use

Claude should invoke this skill when:
- AI-generated images have fake transparency (checkerboard pattern)
- Need true PNG alpha channel from Meshy/AI image outputs
- Asset-polecat generates 2D sprites that need transparency
- Importing sprites that show background artifacts in Unity

## The Problem

AI image generators (Meshy, DALL-E, Midjourney, Stable Diffusion) cannot generate true transparent PNGs. When you ask for "transparent background", they output:
- Checkerboard pattern (simulated transparency)
- Solid white/black background
- JPEG/PNG without alpha channel

## The Solution: Difference Matting

Generate the SAME image twice:
1. On a **pure white** (#FFFFFF) background
2. On a **pure black** (#000000) background

Then use math to calculate the true alpha:
- If a pixel looks the SAME on both → fully opaque (alpha = 1)
- If a pixel matches the background → fully transparent (alpha = 0)
- Semi-transparent pixels show proportional difference

**Formula:** `alpha = 1 - (pixel_distance / max_bg_distance)`

## Prerequisites

```bash
pip install Pillow
# Optional for single-image fallback:
pip install rembg
```

## Procedure

### Step 1: Generate on White Background

Prompt Meshy/AI with:
```
{your subject description} on a pure solid white #FFFFFF background
```

Save as: `{name}_white.png`

### Step 2: Generate on Black Background

Use image editing / regeneration:
```
Change the white background to pure solid black #000000. Keep everything else exactly unchanged.
```

Or regenerate with:
```
{your subject description} on a pure solid black #000000 background
```

Save as: `{name}_black.png`

### Step 3: Extract Alpha

```bash
cd neuro-engine/skills/extract-alpha
python extract_alpha.py {name}_white.png {name}_black.png {name}.png
```

### Step 4: Import to Unity

Move the output PNG to `Assets/Iteration{N}/Sprites/`
Unity will recognize the alpha channel automatically.

## Usage Examples

### Single Image Pair
```bash
python extract_alpha.py ball_white.png ball_black.png ball.png
```

### Batch Processing
```bash
# Process all *_white.png / *_black.png pairs in a directory
python extract_alpha.py --batch ./raw_sprites ./transparent_sprites
```

### Single Image Fallback (using rembg)
```bash
# Less accurate but works with single image
python extract_alpha.py --single sprite.png sprite_transparent.png
```

## Integration with Asset-Polecat

When generating 2D sprites, asset-polecat should:

1. Generate image on white background
2. Generate same image on black background (Meshy image editing)
3. Run extract_alpha.py
4. Import result to Unity
5. Delete intermediate files

Example workflow in asset-polecat:
```
1. POST to Meshy text-to-image: "glowing neon ball, game sprite, on pure white #FFFFFF background"
2. Save result as ball_white.png
3. POST to Meshy image-to-image: "Change background to pure black #000000, keep subject unchanged"
4. Save result as ball_black.png
5. Run: python extract_alpha.py ball_white.png ball_black.png Assets/Sprites/ball.png
6. Cleanup: rm ball_white.png ball_black.png
```

## Quality Tips

1. **Use consistent prompts** - White and black versions must have identical subject
2. **Check alignment** - If images are misaligned, results will have artifacts
3. **Avoid semi-transparent prompts** - Glass/smoke work but need careful handling
4. **Verify in Unity** - Check sprite in Scene view against various backgrounds

## Output

```
Processing: ball
Saved transparent image to: Assets/Sprites/ball.png

Alpha extraction complete:
- Input (white): ball_white.png
- Input (black): ball_black.png
- Output: Assets/Sprites/ball.png
- Alpha channel: Yes (true PNG transparency)
```

## Troubleshooting

| Issue | Cause | Solution |
|-------|-------|----------|
| Halo around edges | Images slightly misaligned | Regenerate black version more carefully |
| Wrong colors | Color shift between versions | Use image editing, not regeneration |
| No transparency | Subject is fully opaque | Expected behavior for solid objects |
| Pillow not found | Missing dependency | `pip install Pillow` |

## Technical Reference

The algorithm compares each pixel:
```python
# Distance between white and black backgrounds
bg_dist = sqrt(255² + 255² + 255²) ≈ 441.67

# For each pixel, calculate distance between white and black versions
pixel_dist = sqrt((rW-rB)² + (gW-gB)² + (bW-bB)²)

# Alpha is inverse of how much the pixel changed
alpha = 1 - (pixel_dist / bg_dist)

# Recover true color from black version
color = pixel_black / alpha
```

Based on: https://jidefr.medium.com/generating-transparent-background-images-with-nano-banana-pro-2-1866c88a33c5

---
name: meshy
description: Generate 3D models and textures using Meshy.ai API. Handles text-to-3D, image-to-3D, and AI texturing.
---

# Skill: meshy

## When to Use

Claude should invoke this skill proactively when:
- User says "create a 3D model of...", "generate a mesh..."
- User describes a visual asset that needs to be created
- Asset Polecat agent needs visual assets
- GDD specifies required 3D models

## Prerequisites

- `MESHY_API_KEY` must be set in `.env`
- Use `validate` skill to check if configured
- Check style guide (if exists) for constraints

## Capabilities

| Feature | Description |
|---------|-------------|
| Text-to-3D | Generate model from text description |
| Image-to-3D | Convert concept art to 3D model |
| AI Texturing | Re-texture existing models |

## Procedure

### 1. Parse Request

Extract from user request:
- Description of the model
- Style (realistic, low-poly, stylized)
- Purpose (prop, character, environment)

### 2. Check Style Guide

If `Assets/Iteration{N}/style-guide.yaml` exists:
- Polycount budget
- Color palette
- Art style reference

### 3. Formulate Prompt

Create a Meshy-optimized prompt:
```
{description}, {style}, game-ready, clean topology,
{polycount} polygons max, {color_hints}
```

### 4. Submit to API

```bash
curl -X POST "https://api.meshy.ai/v2/text-to-3d" \
  -H "Authorization: Bearer $MESHY_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "mode": "preview",
    "prompt": "{prompt}",
    "art_style": "realistic",
    "negative_prompt": "low quality, blurry"
  }'
```

### 5. Poll for Completion

```bash
curl "https://api.meshy.ai/v2/text-to-3d/{task_id}" \
  -H "Authorization: Bearer $MESHY_API_KEY"
```

Poll every 10 seconds until status is "SUCCEEDED" or "FAILED".

### 6. Download and Import

1. Download GLB/FBX from result URL
2. Save to `Assets/Iteration{N}/Models/{name}.glb`
3. Unity will auto-import
4. Verify import via MCP if Unity running

### 7. Register Asset

Write to `hooks/assets/{asset-id}.json`:
```json
{
  "id": "asset-001",
  "type": "model",
  "name": "treasure_chest",
  "prompt": "low-poly treasure chest, wooden, gold trim",
  "meshyTaskId": "task_abc123",
  "path": "Assets/Iteration1/Models/treasure_chest.glb",
  "polycount": 2400,
  "createdAt": "2026-01-21T14:30:00Z",
  "status": "imported"
}
```

## Output

```
Generated 3D model: treasure_chest

- Prompt: "low-poly treasure chest, wooden, gold trim"
- Polygons: 2,400
- Path: Assets/Iteration1/Models/treasure_chest.glb
- Meshy Task: task_abc123

Import status: Success
Preview: [Would show in Unity Scene view]
```

## Error Handling

| Error | Action |
|-------|--------|
| API key missing | Prompt to set in .env |
| Generation failed | Show error, offer retry with modified prompt |
| Import failed | Check Unity console, report issue |
| Over poly budget | Warn, offer to regenerate with lower detail |

## Cost Awareness

Meshy has usage limits. Before generating:
- Check recent usage in `hooks/assets/`
- Warn if approaching limits
- Batch requests when possible

## Style Consistency

For multiple assets in same iteration:
- Use consistent prompts
- Reference same style keywords
- Consider generating a style reference image first

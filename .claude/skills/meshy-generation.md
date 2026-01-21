# Skill: Meshy 3D Asset Generation

## Purpose
Generate 3D assets using Meshy.ai API with proper Unity import workflow.

## When to Use
- Generating 3D models from text descriptions
- Converting concept images to 3D models
- Creating game-ready assets with PBR materials

## API Configuration

| Setting | Value |
|---------|-------|
| Base URL | `https://api.meshy.ai/openapi/v1` |
| Text-to-Image Model | `nano-banana-pro` |
| Image-to-3D Model | `latest` (Meshy 6 Preview) |
| Format | `fbx` (best Unity compatibility) |
| Enable PBR | `true` |
| API Key | `MESHY_API_KEY` from .env |

## Recommended Workflow: Text → Image → 3D

This produces better results than direct text-to-3D.

### Step 1: Generate 4 Concept Images (Parallel)

```bash
curl -X POST "https://api.meshy.ai/openapi/v1/text-to-image" \
  -H "Authorization: Bearer $MESHY_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "ai_model": "nano-banana-pro",
    "prompt": "{description}, isometric view, single object on dark background, game-ready asset",
    "aspect_ratio": "1:1"
  }'
```

Response: `{ "result": "TASK_ID" }`

### Step 2: Poll Image Status

```bash
curl "https://api.meshy.ai/openapi/v1/text-to-image/{TASK_ID}" \
  -H "Authorization: Bearer $MESHY_API_KEY"
```

Poll every 5 seconds until `status` is `SUCCEEDED`.
Response includes `image_url`.

### Step 3: Select Best Image

Evaluate all 4 images for:
- Clear silhouette (good for 3D conversion)
- Single cohesive object (no floating parts)
- Appropriate detail level (not too complex)

### Step 4: Convert Image to 3D

```bash
curl -X POST "https://api.meshy.ai/openapi/v1/image-to-3d" \
  -H "Authorization: Bearer $MESHY_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "image_url": "{selected_image_url}",
    "ai_model": "latest",
    "topology": "triangle",
    "target_polycount": 30000,
    "enable_pbr": true
  }'
```

### Step 5: Poll 3D Generation Status

Poll every 10 seconds until `status` is `SUCCEEDED`.
Response includes:
```json
{
  "model_urls": {
    "fbx": "https://assets.meshy.ai/...",
    "glb": "https://assets.meshy.ai/..."
  }
}
```

### Step 6: Import to Unity

Download the FBX and place in appropriate folder:
```
Assets/Models/{category}/{name}.fbx
```

Use MCP to import:
```
manage_asset(action="import", path="Assets/Models/{category}/{name}.fbx")
```

## Unity Material Setup (URP)

Meshy exports don't render correctly by default in URP. Fix:

1. **Create new material** with shader:
   `Universal Render Pipeline/Autodesk Interactive/AutodeskInteractive`

2. **Enable toggles and assign textures:**
   - `_UseColorMap` = 1, `_MainTex` = color texture
   - `_UseNormalMap` = 1, `_BumpMap` = normal texture
   - `_UseMetallicMap` = 1, `_MetallicGlossMap` = metallic texture
   - `_UseRoughnessMap` = 1, `_SpecGlossMap` = roughness texture

3. **Fix normal map import settings:**
   In the .png.meta file for normal maps:
   ```yaml
   textureType: 1  # Normal map
   sRGBTexture: 0  # Linear, not sRGB
   ```

## Scale Configuration

Keep FBX `globalScale: 100` (default). Adjust scale on prefab's Model child instead:
1. Create parent GameObject
2. Add FBX as child named "Model"
3. Adjust Model's Transform scale (typically 5-15 for buildings)
4. Save as prefab

## Cost Estimate

Per asset:
- 4x Text-to-Image: ~4 credits
- 1x Image-to-3D: ~10 credits
- **Total: ~14 credits**

## Error Handling

- On API failure: Retry up to 3 times with 60s delay
- **Always use `latest` model** - never fall back to older models
- If all 4 images unsuitable: Regenerate with refined prompts

## Prompt Tips

For best results:
- Include "isometric view" or "front view"
- Add "single object on dark background"
- Specify materials: "stone", "wood", "metal"
- Avoid complex scenes - focus on single object

## Verification

After import, verify:
1. Model appears in Unity Project window
2. Material renders correctly (not pink)
3. Scale is appropriate for game
4. Textures are assigned correctly

## Registry

Track generated assets in `hooks/assets/registry.json`:
```json
{
  "id": "asset-001",
  "type": "model",
  "source": "meshy",
  "prompt": "...",
  "path": "Assets/Models/...",
  "generatedAt": "ISO-8601",
  "jobId": "meshy-job-id",
  "cost_credits": 14
}
```

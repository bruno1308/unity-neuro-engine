# Layer 7: Generative Asset Pipeline - Examples

> Code samples for Meshy.ai, ElevenLabs, style guide, and asset quality verification

---

## 1. Visual Asset Generation (Meshy.ai)

### Text-to-3D Pipeline

```python
import meshy

client = meshy.Client(api_key="your_api_key")

# Generate 3D model from description
task = client.text_to_3d.create(
    prompt="Demonic imp enemy, low-poly game-ready, red skin, horns, angry expression",
    art_style="realistic",
    topology="quad",  # Game-ready topology
    target_polycount=5000
)

# Wait for generation
result = client.text_to_3d.wait(task.id)

# Download assets
result.download_glb("Assets/Models/Enemies/imp.glb")
result.download_fbx("Assets/Models/Enemies/imp.fbx")
result.download_textures("Assets/Textures/Enemies/imp/")
```

### AI Texturing Pipeline

```python
# Re-texture existing model with new style
task = client.ai_texturing.create(
    model_path="Assets/Models/Weapons/shotgun_base.glb",
    prompt="Rusty metal shotgun, worn wood grip, apocalyptic style, battle-damaged",
    resolution=2048
)

result = client.ai_texturing.wait(task.id)
result.download_textures("Assets/Textures/Weapons/shotgun/")
```

### Image-to-3D for Concept Art

```python
# Convert 2D concept art to 3D model
task = client.image_to_3d.create(
    image_path="Concepts/health_pickup.png",
    topology="quad",
    target_polycount=1000
)

result = client.image_to_3d.wait(task.id)
result.download_glb("Assets/Models/Pickups/health.glb")
```

---

## 2. Audio Generation (ElevenLabs)

### Sound Effects Generation

```python
from elevenlabs import ElevenLabs

client = ElevenLabs(api_key="your_api_key")

# Generate sound effect
audio = client.sound_effects.generate(
    text="Heavy shotgun blast with metallic echo, aggressive and punchy",
    duration_seconds=1.5,
    prompt_influence=0.7
)

# Save to Unity project
with open("Assets/Audio/SFX/shotgun_blast.wav", "wb") as f:
    f.write(audio)
```

### Voice Generation for NPCs

```python
# Generate voice line
audio = client.text_to_speech.convert(
    text="You have found a secret area!",
    voice_id="announcer_deep",
    model_id="eleven_turbo_v2_5",
    voice_settings={
        "stability": 0.7,
        "similarity_boost": 0.8
    }
)

with open("Assets/Audio/Voice/secret_found.wav", "wb") as f:
    for chunk in audio:
        f.write(chunk)
```

### Ambient Music Generation

```python
# Generate background music
audio = client.music.generate(
    prompt="Dark ambient industrial music, slow tempo, horror game atmosphere, looping",
    duration_seconds=120,
    loop=True
)

with open("Assets/Audio/Music/level1_ambient.wav", "wb") as f:
    f.write(audio)
```

---

## 3. Style Guide System

```yaml
# style_guide.yaml
visual_style:
  art_direction: "90s boomer shooter, Doom/Quake inspired"
  color_palette:
    primary: ["#8B0000", "#2F4F4F", "#8B4513"]  # Dark red, slate, brown
    accent: ["#FFD700", "#FF4500"]  # Gold, orange-red

  model_guidelines:
    polycount_budget:
      character: 5000-8000
      weapon: 2000-4000
      prop: 500-2000
      level_piece: 1000-3000
    style_keywords: "low-poly, chunky, angular, nostalgic, gritty"

  texture_guidelines:
    resolution: 1024
    style_keywords: "pixelated, hand-painted feel, high contrast"

audio_style:
  sfx_guidelines:
    style: "punchy, aggressive, slightly distorted, retro"
    sample_rate: 44100
    bit_depth: 16

  music_guidelines:
    genre: "industrial metal, dark ambient"
    tempo_range: [80, 140]
    instruments: ["heavy guitar", "synthesizer", "industrial percussion"]

  voice_guidelines:
    announcer_voice: "deep, commanding, slightly robotic"
    enemy_voices: "guttural, demonic, distorted"
```

---

## 4. Complete Asset Pipeline Class

```python
class AssetPipeline:
    def __init__(self, style_guide_path: str):
        self.style = load_yaml(style_guide_path)
        self.meshy = meshy.Client(api_key=os.getenv("MESHY_API_KEY"))
        self.eleven = ElevenLabs(api_key=os.getenv("ELEVENLABS_API_KEY"))

    def generate_enemy(self, description: str, enemy_id: str) -> dict:
        """Generate complete enemy with model, textures, and sounds."""

        # Enhance prompt with style guide
        model_prompt = f"{description}, {self.style['visual_style']['model_guidelines']['style_keywords']}"

        # Generate 3D model
        model_task = self.meshy.text_to_3d.create(
            prompt=model_prompt,
            target_polycount=self.style['visual_style']['model_guidelines']['polycount_budget']['character']
        )
        model_result = self.meshy.text_to_3d.wait(model_task.id)

        # Generate sounds
        death_sfx = self.eleven.sound_effects.generate(
            text=f"{description} death cry, {self.style['audio_style']['sfx_guidelines']['style']}",
            duration_seconds=2.0
        )
        attack_sfx = self.eleven.sound_effects.generate(
            text=f"{description} attack sound, aggressive",
            duration_seconds=1.0
        )

        # Save all assets
        base_path = f"Assets/Enemies/{enemy_id}"
        model_result.download_glb(f"{base_path}/model.glb")
        model_result.download_textures(f"{base_path}/textures/")
        save_audio(death_sfx, f"{base_path}/audio/death.wav")
        save_audio(attack_sfx, f"{base_path}/audio/attack.wav")

        return {
            "enemy_id": enemy_id,
            "model_path": f"{base_path}/model.glb",
            "audio": {
                "death": f"{base_path}/audio/death.wav",
                "attack": f"{base_path}/audio/attack.wav"
            }
        }
```

---

## 5. Asset Quality Verification

```yaml
# asset_graders.yaml
asset_graders:
  model_quality:
    type: code
    checks:
      - polycount_within_budget: true
      - no_non_manifold_geometry: true
      - uvs_present: true
      - materials_assigned: true
      - scale_normalized: true  # 1 unit = 1 meter

  texture_quality:
    type: code
    checks:
      - resolution_matches_spec: true
      - power_of_two: true
      - no_obvious_artifacts: true

  style_consistency:
    type: vlm
    model: "claude-sonnet-4-5"
    prompt: |
      Compare this asset render to the style guide references.
      Rate style consistency 1-10.
      Flag mismatches in color, detail level, art style.
    threshold: 7

  audio_quality:
    type: code
    checks:
      - sample_rate_correct: true
      - no_clipping: true
      - duration_within_spec: true
      - loudness_normalized: true
```

---

## 6. Asset Registry

```json
// hooks/assets/registry.json
{
  "enemies": {
    "imp": {
      "generated_at": "2026-01-20T10:00:00Z",
      "prompt": "Demonic imp enemy, low-poly...",
      "prompt_hash": "a1b2c3d4",
      "files": {
        "model": "Assets/Enemies/imp/model.glb",
        "textures": "Assets/Enemies/imp/textures/",
        "audio_death": "Assets/Enemies/imp/audio/death.wav"
      },
      "quality_score": 8.5,
      "approved": true
    }
  }
}
```

---

## References

- [Meshy.ai](https://www.meshy.ai/)
- [Meshy API Docs](https://docs.meshy.ai/)
- [ElevenLabs](https://elevenlabs.io/)
- [ElevenLabs API Docs](https://elevenlabs.io/docs/api-reference)

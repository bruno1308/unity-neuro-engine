---
name: elevenlabs
description: Generate audio using ElevenLabs API. Handles sound effects, voice lines, and ambient audio.
---

# Skill: elevenlabs

## When to Use

Claude should invoke this skill proactively when:
- User says "create sound effect for...", "generate audio..."
- User describes audio that needs to be created
- Asset Polecat agent needs audio assets
- GDD specifies required sound effects or voice

## Prerequisites

- `ELEVENLABS_API_KEY` must be set in `.env`
- Use `validate` skill to check if configured

## Capabilities

| Feature | Description |
|---------|-------------|
| Sound Effects | Generate SFX from descriptions |
| Voice | NPC dialogue, announcer lines |
| Text-to-Speech | UI feedback sounds |

## Procedure

### 1. Parse Request

Extract from user request:
- Type: sfx, voice, ambient
- Description
- Duration (if specified)
- Style/mood

### 2. Determine Endpoint

| Type | Endpoint | Method |
|------|----------|--------|
| Sound Effects | `/v1/sound-generation` | POST |
| Voice/TTS | `/v1/text-to-speech/{voice_id}` | POST |

### 3. Generate Sound Effect

```bash
curl -X POST "https://api.elevenlabs.io/v1/sound-generation" \
  -H "xi-api-key: $ELEVENLABS_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "text": "{description}",
    "duration_seconds": {duration},
    "prompt_influence": 0.3
  }'
```

### 4. Generate Voice

```bash
curl -X POST "https://api.elevenlabs.io/v1/text-to-speech/{voice_id}" \
  -H "xi-api-key: $ELEVENLABS_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "text": "{dialogue}",
    "model_id": "eleven_multilingual_v2",
    "voice_settings": {
      "stability": 0.5,
      "similarity_boost": 0.5
    }
  }'
```

### 5. Download and Import

1. Response is audio bytes (MP3)
2. Save to `Assets/Iteration{N}/Audio/{name}.mp3`
3. Unity will auto-import as AudioClip
4. Set import settings if needed (loop, compression)

### 6. Register Asset

Write to `hooks/assets/{asset-id}.json`:
```json
{
  "id": "asset-002",
  "type": "audio",
  "subtype": "sfx",
  "name": "explosion_small",
  "prompt": "small explosion, arcade style",
  "path": "Assets/Iteration1/Audio/explosion_small.mp3",
  "duration": 1.2,
  "createdAt": "2026-01-21T14:30:00Z",
  "status": "imported"
}
```

## Output

```
Generated audio: explosion_small

- Type: Sound Effect
- Prompt: "small explosion, arcade style"
- Duration: 1.2s
- Path: Assets/Iteration1/Audio/explosion_small.mp3

Import status: Success
```

## Common Sound Effects

| Category | Examples |
|----------|----------|
| UI | click, hover, success, error, notification |
| Actions | jump, shoot, hit, collect, door_open |
| Ambient | wind, rain, crowd, machine_hum |
| Impact | explosion, crash, thud, splash |

## Voice Options

ElevenLabs provides pre-made voices. Common choices:
- `21m00Tcm4TlvDq8ikWAM` - Rachel (female, neutral)
- `AZnzlk1XvdvUeBnXmlld` - Domi (male, assertive)
- `EXAVITQu4vr4xnSDxMaL` - Bella (female, soft)

Or clone a voice with user's permission.

## Audio Normalization

After import, verify:
- Volume levels consistent with other assets
- No clipping
- Appropriate length

## Cost Awareness

ElevenLabs has character/minute limits. Before generating:
- Check usage against plan limits
- Prefer shorter generations when possible
- Batch related audio requests

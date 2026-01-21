# Skill: ElevenLabs Audio Generation

## Purpose
Generate audio assets using ElevenLabs API - sound effects and voice synthesis.

## When to Use
- Generating sound effects from text descriptions
- Creating voice lines for characters
- Producing ambient audio

## API Configuration

| Setting | Value |
|---------|-------|
| Base URL | `https://api.elevenlabs.io/v1` |
| API Key | `ELEVENLABS_API_KEY` from .env |

## Sound Effect Generation

### Generate Sound Effect

```bash
curl -X POST "https://api.elevenlabs.io/v1/sound-generation" \
  -H "xi-api-key: $ELEVENLABS_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "text": "{description of the sound}",
    "duration_seconds": 2.0,
    "prompt_influence": 0.3
  }' \
  --output sound.mp3
```

**Parameters:**
- `text`: Description of the sound (e.g., "wooden door creaking open slowly")
- `duration_seconds`: Length of sound (0.5 - 22 seconds)
- `prompt_influence`: How closely to follow the prompt (0.0 - 1.0)

**Response:** Audio file (mp3) in response body

### Example Prompts

| Sound Type | Example Prompt |
|------------|----------------|
| Footsteps | "footsteps on wooden floor, slow walking" |
| Door | "heavy wooden door creaking open" |
| Ambient | "distant thunder rolling, light rain" |
| UI | "soft click, button press, satisfying" |
| Impact | "sword clashing against metal shield" |

## Text-to-Speech

### List Available Voices

```bash
curl "https://api.elevenlabs.io/v1/voices" \
  -H "xi-api-key: $ELEVENLABS_API_KEY"
```

### Generate Speech

```bash
curl -X POST "https://api.elevenlabs.io/v1/text-to-speech/{voice_id}" \
  -H "xi-api-key: $ELEVENLABS_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "text": "The dialogue text to speak",
    "model_id": "eleven_monolingual_v1",
    "voice_settings": {
      "stability": 0.5,
      "similarity_boost": 0.75
    }
  }' \
  --output voice.mp3
```

**Voice Settings:**
- `stability`: Voice consistency (0.0 - 1.0, higher = more consistent)
- `similarity_boost`: How close to original voice (0.0 - 1.0)

### Common Voice IDs

Use `GET /v1/voices` to list available voices. Some defaults:
- `21m00Tcm4TlvDq8ikWAM` - Rachel (female, calm)
- `AZnzlk1XvdvUeBnXmlld` - Domi (female, confident)
- `EXAVITQu4vr4xnSDxMaL` - Bella (female, soft)
- `ErXwobaYiN019PkySvjV` - Antoni (male, calm)
- `MF3mGyEYCl7XYWbV9V6O` - Elli (female, young)

## Import to Unity

After generating audio, place in project:

```
Assets/Audio/SFX/{category}/{name}.mp3
Assets/Audio/Voice/{character}/{name}.mp3
```

Use MCP to import and configure:
```
manage_asset(action="import", path="Assets/Audio/SFX/doors/door_creak.mp3")
```

## Audio Import Settings

For Unity, configure the .mp3.meta file:

**Sound Effects:**
```yaml
AudioImporter:
  loadType: 1              # Decompress On Load (for short SFX)
  sampleRateSetting: 0     # Preserve Sample Rate
  forceToMono: 1           # Mono for 3D sounds
```

**Voice/Music:**
```yaml
AudioImporter:
  loadType: 0              # Streaming (for longer audio)
  sampleRateSetting: 0     # Preserve Sample Rate
  forceToMono: 0           # Keep stereo
```

## Cost Estimate

| Operation | Cost |
|-----------|------|
| Sound Effect (per generation) | ~100 characters |
| TTS (per 1000 chars) | ~1000 characters |

Check your plan limits at https://elevenlabs.io/subscription

## Rate Limits

- Wait 1 second between requests
- Maximum concurrent requests: 2
- Daily limits depend on subscription tier

## Error Handling

Common errors:
- `401`: Invalid API key
- `429`: Rate limit exceeded (wait and retry)
- `422`: Invalid parameters (check duration, voice_id)

Retry strategy:
- Up to 3 retries
- Exponential backoff (2s, 4s, 8s)

## Registry

Track generated audio in `hooks/assets/registry.json`:
```json
{
  "id": "audio-001",
  "type": "audio",
  "source": "elevenlabs",
  "subtype": "sfx",
  "prompt": "wooden door creaking",
  "path": "Assets/Audio/SFX/doors/door_creak.mp3",
  "generatedAt": "ISO-8601",
  "duration_seconds": 2.0
}
```

## Verification

After import:
1. Audio appears in Unity Project window
2. Can preview by clicking Play in Inspector
3. Duration matches expected length
4. Quality is acceptable

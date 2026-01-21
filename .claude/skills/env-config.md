# Skill: env-config

## Purpose
Manage API keys and configuration in the .env file.

## When to Use
- Setting up API keys (Meshy, ElevenLabs, Gemini)
- Changing configuration values
- Verifying configuration is complete

## Context

### File Locations
- Active config: `.env` (gitignored)
- Template: `.env.template` (committed)

### Required Keys
| Key | Service | Get From |
|-----|---------|----------|
| `MESHY_API_KEY` | 3D generation | https://www.meshy.ai/api |
| `ELEVENLABS_API_KEY` | Audio generation | https://elevenlabs.io/api |
| `GEMINI_API_KEY` | Video analysis | https://ai.google.dev/ |

### Placeholder Detection
Keys containing `your_` are placeholders and not configured.

## Procedure

### Check Configuration Status

```bash
# Check for placeholder values
grep "your_" .env

# Expected: No output if all keys configured
```

### Update a Key

1. Read current .env
2. Replace the specific key line
3. Write back

### Validate Keys

For each key, check:
1. Not empty
2. Does not contain "your_"
3. Length > 20 characters (API keys are typically long)

## Verification
- `.env` exists
- No placeholder values remain
- All required keys present with real values

## Security Notes
- NEVER log or display actual API key values
- NEVER commit .env to git
- Only show masked versions: `MESHY_API_KEY=msh_****...`

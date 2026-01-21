# Skill: validation

## Purpose
Run pre-flight validation checks before proceeding with tasks.

## When to Use
- Before starting any game development work
- After setup changes
- When errors are suspected

## Context

### Validation Tiers (from Architecture.md)
| Tier | Type | Checks |
|------|------|--------|
| 1 | Syntactic | Compilation, null refs |
| 2 | State | JSON snapshot assertions |
| 3 | Behavioral | Automated playtests |
| 4 | Visual | Screenshots, VLM analysis |
| 5 | Quality | Juice metrics, polish |
| 6 | Human | Taste, fun, balance |

This skill covers Tier 1-2 (automated, fast).

### Known Problems (from ENGINE_PROBLEMS.md)
- **Problem #1**: Always validate before asking user
- **Problem #2**: Package name mismatches only visible in Unity console
- **Problem #8**: Input System mismatch - code generated with wrong API

## Procedure

### Pre-Development Gate (from Wizard.md)

Run these checks in sequence:

```bash
# 1. Prerequisites
node --version      # Expect: v18+
python --version    # Expect: 3.10+
git --version       # Expect: 2.25+

# 2. Project structure
ls hooks/ .claude/ CLAUDE.md .env

# 3. API keys configured (no placeholders)
grep -v "your_" .env | grep -E "^(MESHY|ELEVENLABS|GEMINI)_API_KEY=.+"

# 4. Unity packages (check manifest)
grep -E "(vcontainer|unity-mcp)" Packages/manifest.json

# 5. MCP connection (if server should be running)
curl -s http://localhost:8080/health

# 6. Input System configuration (via MCP)
# Call check_input_system(action='get_config') to determine which Input API to use
# Returns: Legacy, InputSystem, or Both
# CRITICAL: Use the correct API or code will fail at runtime!
```

### Validation Results

Write to: `hooks/validation/{timestamp}-preflight.json`

```json
{
  "type": "preflight",
  "timestamp": "ISO-8601",
  "passed": true|false,
  "checks": {
    "prerequisites": {"node": true, "python": true, "git": true},
    "structure": {"hooks": true, "claude": true, "env": true},
    "apiKeys": {"meshy": true, "elevenlabs": true, "gemini": true},
    "packages": {"vcontainer": true, "unityMcp": true},
    "mcp": {"reachable": true|false},
    "inputSystem": {"mode": "Legacy|InputSystem|Both", "api": "recommended API string"}
  },
  "errors": []
}
```

### On Failure

1. Identify which check failed
2. Attempt to fix automatically (use appropriate skill)
3. If cannot fix, document in ENGINE_PROBLEMS.md
4. Only then inform user

## Verification
- All checks pass
- Results written to hooks/validation/
- No manual intervention required

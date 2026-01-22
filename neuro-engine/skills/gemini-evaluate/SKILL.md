# Skill: gemini-evaluate

## When to Use

Claude should invoke this skill when:
- User requests video evaluation of gameplay
- Iteration needs final visual/gameplay validation
- GDD specifies VLM video analysis requirement

## Context

The Neuro-Engine uses Gemini's video analysis capabilities for Tier 4 (Visual) evaluation.
This skill processes gameplay videos and returns structured evaluation scores.

## Prerequisites

- `GEMINI_API_KEY` set in `.env`
- Video file in MP4, WEBM, or similar format
- Python 3.10+ with `google-generativeai` package

## Procedure

### 1. Record Gameplay Video

Use Unity Recorder or capture screenshots and convert to video:
```bash
# Using Unity Recorder (preferred)
# Window > General > Recorder > Add Movie Recorder

# Or using FFmpeg from screenshots
ffmpeg -framerate 30 -i screenshot_%04d.png -c:v libx264 -pix_fmt yuv420p output.mp4
```

### 2. Run Evaluation

```bash
python neuro-engine/skills/gemini-evaluate/evaluate_video.py path/to/video.mp4
```

### 3. Interpret Results

The script outputs JSON with:
- `functionality_score`: 1-10 (gameplay works)
- `juice_score`: 1-10 (game feel/feedback)
- `polish_score`: 1-10 (visual consistency)
- `overall_verdict`: YES/NO
- `details`: Justification text

### Pass Criteria (from GDD)

- `functionality_score >= 7`
- `juice_score >= 7`
- `polish_score >= 6`
- `overall_verdict == "YES"`

## Output

```json
{
  "functionality_score": 8,
  "juice_score": 7,
  "polish_score": 6,
  "overall_verdict": "YES",
  "details": "The gameplay demonstrates functional Arkanoid mechanics with satisfying visual feedback...",
  "timestamp": "2025-01-22T12:00:00Z"
}
```

## Troubleshooting

- **API Error**: Check `GEMINI_API_KEY` in `.env`
- **Video too large**: Compress or trim to under 20 seconds
- **Timeout**: Large videos may need longer processing time

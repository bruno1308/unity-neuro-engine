#!/usr/bin/env python3
"""
Gemini Video Evaluation for Neuro-Engine
Evaluates gameplay videos using Gemini's vision capabilities.
"""

import sys
import os
import json
import re
from datetime import datetime
from pathlib import Path

# Add project root to path for .env loading
PROJECT_ROOT = Path(__file__).parent.parent.parent.parent
sys.path.insert(0, str(PROJECT_ROOT))

try:
    import google.generativeai as genai
except ImportError:
    print("Error: google-generativeai not installed. Run: pip install google-generativeai")
    sys.exit(1)

# Load environment variables from .env
def load_env():
    env_path = PROJECT_ROOT / ".env"
    if env_path.exists():
        with open(env_path, encoding='utf-8') as f:
            for line in f:
                line = line.strip()
                if line and not line.startswith('#') and '=' in line:
                    key, value = line.split('=', 1)
                    os.environ.setdefault(key.strip(), value.strip())

load_env()

# Evaluation prompt from GDD
EVALUATION_PROMPT = """
Watch this Arkanoid/Breakout gameplay clip and evaluate:

1. FUNCTIONALITY (1-10): Does the core gameplay work?
   - Ball bounces correctly
   - Paddle moves smoothly
   - Bricks break when hit

2. JUICE/GAME FEEL (1-10): How satisfying does it feel?
   - Screen shake on impacts
   - Particle effects on destruction
   - Visual feedback (flashes, trails)
   - Animation quality (squash/stretch)
   - Overall "punchy" feeling

3. POLISH (1-10): Does it look like a finished product?
   - Consistent visual style
   - Smooth animations
   - No visual glitches

OVERALL VERDICT: Does this clip demonstrate a juicy, satisfying
Arkanoid clone? Answer YES or NO with brief justification.

Please respond in the following JSON format:
{
  "functionality_score": <number 1-10>,
  "juice_score": <number 1-10>,
  "polish_score": <number 1-10>,
  "overall_verdict": "<YES or NO>",
  "details": "<brief justification>"
}
"""

def evaluate_video(video_path: str) -> dict:
    """
    Upload video to Gemini and get evaluation scores.

    Args:
        video_path: Path to the gameplay video file

    Returns:
        Dictionary with evaluation results
    """
    api_key = os.environ.get("GEMINI_API_KEY")
    if not api_key:
        return {
            "error": "GEMINI_API_KEY not set in environment or .env file",
            "success": False
        }

    video_path = Path(video_path)
    if not video_path.exists():
        return {
            "error": f"Video file not found: {video_path}",
            "success": False
        }

    # Configure Gemini
    genai.configure(api_key=api_key)

    print(f"Uploading video: {video_path}")

    # Upload the video file
    video_file = genai.upload_file(str(video_path))

    print(f"Video uploaded: {video_file.name}")
    print("Waiting for processing...")

    # Wait for processing
    import time
    while video_file.state.name == "PROCESSING":
        time.sleep(2)
        video_file = genai.get_file(video_file.name)

    if video_file.state.name == "FAILED":
        return {
            "error": f"Video processing failed: {video_file.state.name}",
            "success": False
        }

    print("Processing complete. Running evaluation...")

    # Create model and run evaluation
    model = genai.GenerativeModel("gemini-3-pro-preview")

    response = model.generate_content(
        [video_file, EVALUATION_PROMPT],
        generation_config=genai.GenerationConfig(
            temperature=0.1,  # Low temperature for consistent scoring
        )
    )

    # Parse the response
    response_text = response.text.strip()

    # Try to extract JSON from the response
    json_match = re.search(r'\{[^}]+\}', response_text, re.DOTALL)
    if json_match:
        try:
            result = json.loads(json_match.group())
            result["success"] = True
            result["timestamp"] = datetime.now().isoformat()
            result["video_path"] = str(video_path)
            result["raw_response"] = response_text

            # Determine pass/fail
            passes = (
                result.get("functionality_score", 0) >= 7 and
                result.get("juice_score", 0) >= 7 and
                result.get("polish_score", 0) >= 6 and
                result.get("overall_verdict", "").upper() == "YES"
            )
            result["passes_criteria"] = passes

            return result
        except json.JSONDecodeError:
            pass

    # If JSON parsing failed, return raw response
    return {
        "success": True,
        "timestamp": datetime.now().isoformat(),
        "video_path": str(video_path),
        "raw_response": response_text,
        "error": "Could not parse structured response",
        "passes_criteria": False
    }

def main():
    if len(sys.argv) < 2:
        print("Usage: python evaluate_video.py <video_path> [output_json_path]")
        print("\nExample:")
        print("  python evaluate_video.py gameplay.mp4")
        print("  python evaluate_video.py gameplay.mp4 results.json")
        sys.exit(1)

    video_path = sys.argv[1]
    output_path = sys.argv[2] if len(sys.argv) > 2 else None

    result = evaluate_video(video_path)

    # Output result
    result_json = json.dumps(result, indent=2)
    print("\n" + "="*60)
    print("EVALUATION RESULT")
    print("="*60)
    print(result_json)

    if output_path:
        with open(output_path, 'w') as f:
            f.write(result_json)
        print(f"\nResult saved to: {output_path}")

    # Exit with appropriate code
    if result.get("passes_criteria"):
        print("\n*** EVALUATION PASSED ***")
        sys.exit(0)
    else:
        print("\n*** EVALUATION FAILED OR INCOMPLETE ***")
        sys.exit(1)

if __name__ == "__main__":
    main()

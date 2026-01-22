#!/usr/bin/env python3
"""
Difference Matting Alpha Extraction

Extracts true alpha channel from two images of the same subject:
one on a white background, one on a black background.

Based on the technique from:
https://jidefr.medium.com/generating-transparent-background-images-with-nano-banana-pro-2-1866c88a33c5

Usage:
    python extract_alpha.py <white_bg_image> <black_bg_image> <output_png>
    python extract_alpha.py --single <image> <output_png>  # Uses rembg fallback
"""

import sys
import math
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    print("ERROR: Pillow not installed. Run: pip install Pillow")
    sys.exit(1)


def extract_alpha_two_pass(white_bg_path: str, black_bg_path: str, output_path: str) -> None:
    """
    Extract alpha using difference matting from white and black background versions.

    The math:
    - If a pixel looks the same on both backgrounds, it's fully opaque
    - If a pixel matches the background color, it's fully transparent
    - Semi-transparent pixels show a blend based on their alpha

    Formula: alpha = 1 - (pixel_distance / max_bg_distance)
    """
    # Load images
    img_white = Image.open(white_bg_path).convert('RGBA')
    img_black = Image.open(black_bg_path).convert('RGBA')

    if img_white.size != img_black.size:
        raise ValueError(f"Image size mismatch: {img_white.size} vs {img_black.size}")

    width, height = img_white.size

    # Get pixel data
    pixels_white = img_white.load()
    pixels_black = img_black.load()

    # Create output image
    output = Image.new('RGBA', (width, height))
    pixels_out = output.load()

    # Distance between white (255,255,255) and black (0,0,0)
    # sqrt(255^2 + 255^2 + 255^2) ≈ 441.67
    bg_dist = math.sqrt(3 * 255 * 255)

    for y in range(height):
        for x in range(width):
            # Get RGB from both versions
            rW, gW, bW, _ = pixels_white[x, y]
            rB, gB, bB, _ = pixels_black[x, y]

            # Calculate distance between the two observed pixels
            pixel_dist = math.sqrt(
                (rW - rB) ** 2 +
                (gW - gB) ** 2 +
                (bW - bB) ** 2
            )

            # Calculate alpha
            # - Opaque pixels look the same on both (dist = 0) → alpha = 1
            # - Transparent pixels match backgrounds (dist = bg_dist) → alpha = 0
            alpha = 1.0 - (pixel_dist / bg_dist)
            alpha = max(0.0, min(1.0, alpha))  # Clamp to 0-1

            # Recover foreground color from black background version
            # Since BG is black (0,0,0), formula simplifies to: C / alpha
            if alpha > 0.01:
                r_out = min(255, int(rB / alpha))
                g_out = min(255, int(gB / alpha))
                b_out = min(255, int(bB / alpha))
            else:
                r_out = g_out = b_out = 0

            pixels_out[x, y] = (r_out, g_out, b_out, int(alpha * 255))

    # Save as PNG (preserves alpha)
    output.save(output_path, 'PNG')
    print(f"Saved transparent image to: {output_path}")


def extract_alpha_single(input_path: str, output_path: str) -> None:
    """
    Fallback: Use rembg library for single-image background removal.
    Less accurate than two-pass but works with single images.
    """
    try:
        from rembg import remove
    except ImportError:
        print("ERROR: rembg not installed. Run: pip install rembg")
        print("For two-pass extraction (better quality), provide both white and black background images.")
        sys.exit(1)

    with open(input_path, 'rb') as f:
        input_data = f.read()

    output_data = remove(input_data)

    with open(output_path, 'wb') as f:
        f.write(output_data)

    print(f"Saved transparent image to: {output_path}")


def batch_process(input_dir: str, output_dir: str) -> None:
    """
    Process all images in a directory.
    Expects pairs: *_white.png and *_black.png
    """
    input_path = Path(input_dir)
    output_path = Path(output_dir)
    output_path.mkdir(parents=True, exist_ok=True)

    # Find all white background images
    white_images = list(input_path.glob('*_white.*'))

    processed = 0
    for white_img in white_images:
        # Find matching black image
        base_name = white_img.stem.replace('_white', '')
        black_candidates = list(input_path.glob(f'{base_name}_black.*'))

        if not black_candidates:
            print(f"WARNING: No black background match for {white_img.name}")
            continue

        black_img = black_candidates[0]
        output_file = output_path / f'{base_name}.png'

        print(f"Processing: {base_name}")
        extract_alpha_two_pass(str(white_img), str(black_img), str(output_file))
        processed += 1

    print(f"\nProcessed {processed} images")


def main():
    if len(sys.argv) < 3:
        print(__doc__)
        print("\nExamples:")
        print("  python extract_alpha.py ball_white.png ball_black.png ball.png")
        print("  python extract_alpha.py --single sprite.png sprite_transparent.png")
        print("  python extract_alpha.py --batch ./raw ./output")
        sys.exit(1)

    if sys.argv[1] == '--single':
        if len(sys.argv) != 4:
            print("Usage: python extract_alpha.py --single <input> <output>")
            sys.exit(1)
        extract_alpha_single(sys.argv[2], sys.argv[3])

    elif sys.argv[1] == '--batch':
        if len(sys.argv) != 4:
            print("Usage: python extract_alpha.py --batch <input_dir> <output_dir>")
            sys.exit(1)
        batch_process(sys.argv[2], sys.argv[3])

    else:
        if len(sys.argv) != 4:
            print("Usage: python extract_alpha.py <white_bg> <black_bg> <output>")
            sys.exit(1)
        extract_alpha_two_pass(sys.argv[1], sys.argv[2], sys.argv[3])


if __name__ == '__main__':
    main()

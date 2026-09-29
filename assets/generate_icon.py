"""Generate TaskbarFetch's original application icon.

Copyright (c) 2026 Thyann Seng
Licensed under the MIT License.

Requires Pillow:
    py -m pip install pillow

Run from the repository root:
    py assets/generate_icon.py
"""

from pathlib import Path
from PIL import Image, ImageDraw

SIZE = 256
OUT_DIR = Path(__file__).resolve().parent


def draw_icon() -> Image.Image:
    image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    background = (38, 90, 190, 255)
    white = (255, 255, 255, 255)
    accent = (196, 220, 255, 255)

    draw.rounded_rectangle((20, 20, 236, 236), radius=46, fill=background)

    # Left display.
    draw.rounded_rectangle((45, 67, 123, 139), radius=8, outline=white, width=10)
    draw.line((84, 139, 84, 158), fill=white, width=9)
    draw.line((64, 159, 104, 159), fill=white, width=9)

    # Right display.
    draw.rounded_rectangle((133, 103, 211, 175), radius=8, outline=white, width=10)
    draw.line((172, 175, 172, 194), fill=white, width=9)
    draw.line((152, 195, 192, 195), fill=white, width=9)

    # Movement arrow.
    draw.line((104, 104, 161, 104), fill=accent, width=12)
    draw.polygon(((164, 104), (143, 87), (143, 121)), fill=accent)

    return image


def main() -> None:
    image = draw_icon()
    png_path = OUT_DIR / "TaskbarFetch.png"
    ico_path = OUT_DIR / "TaskbarFetch.ico"

    image.save(png_path, "PNG")
    image.save(
        ico_path,
        format="ICO",
        sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)],
    )

    print(f"Wrote {png_path}")
    print(f"Wrote {ico_path}")


if __name__ == "__main__":
    main()

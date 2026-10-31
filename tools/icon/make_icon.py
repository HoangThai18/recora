"""Draws the Recora icon: a pastel-blue rounded tile, white viewfinder corners, a red record dot.

Usage: python3 tools/icon/make_icon.py   (needs Pillow)
Writes src/Recora.App/Assets/recora.ico, src/Recora.App/Assets/recora-256.png and tools/icon/recora-1024.png.
"""
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "src" / "Recora.App" / "Assets"
SS = 4  # supersampling factor; the 1024 master is drawn at 4096 and scaled down for clean edges
SIZE = 1024 * SS


def lerp(a, b, t):
    return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3))


def tile_gradient():
    top_left, bottom_right = (0x5F, 0xB2, 0xF2), (0x1A, 0x62, 0xA8)
    gradient = Image.new("RGB", (SIZE, SIZE))
    pixels = gradient.load()
    step = 16
    for y in range(0, SIZE, step):
        for x in range(0, SIZE, step):
            color = lerp(top_left, bottom_right, (x + y) / (2 * SIZE))
            for dy in range(step):
                for dx in range(step):
                    pixels[x + dx, y + dy] = color
    return gradient


def master():
    image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    margin, radius = 56 * SS, 232 * SS
    mask = Image.new("L", (SIZE, SIZE), 0)
    ImageDraw.Draw(mask).rounded_rectangle((margin, margin, SIZE - margin, SIZE - margin), radius, fill=255)
    image.paste(tile_gradient(), (0, 0), mask)

    draw = ImageDraw.Draw(image)
    white = (255, 255, 255, 255)
    stroke = 62 * SS
    inset, length = 236 * SS, 168 * SS
    a, b = inset, SIZE - inset
    for (x, y, dx, dy) in ((a, a, 1, 1), (b, a, -1, 1), (b, b, -1, -1), (a, b, 1, -1)):
        draw.line((x, y + dy * length, x, y), fill=white, width=stroke)
        draw.line((x, y, x + dx * length, y), fill=white, width=stroke)
        # round caps and joins
        for px, py in ((x, y), (x, y + dy * length), (x + dx * length, y)):
            draw.ellipse((px - stroke / 2, py - stroke / 2, px + stroke / 2, py + stroke / 2), fill=white)

    center, ring, dot = SIZE / 2, 176 * SS, 146 * SS
    draw.ellipse((center - ring, center - ring, center + ring, center + ring), fill=white)
    draw.ellipse((center - dot, center - dot, center + dot, center + dot), fill=(0xE5, 0x48, 0x4D, 255))
    return image.resize((1024, 1024), Image.LANCZOS)


def main():
    ASSETS.mkdir(parents=True, exist_ok=True)
    big = master()
    big.save(ROOT / "tools" / "icon" / "recora-1024.png")
    big.resize((256, 256), Image.LANCZOS).save(ASSETS / "recora-256.png")
    sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    big.save(ASSETS / "recora.ico", sizes=[(s, s) for s in sizes])
    print("wrote", ASSETS / "recora.ico")


if __name__ == "__main__":
    main()

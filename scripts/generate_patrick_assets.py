"""Generate the project-local Patrick-style sprite sheet and application icon.

The sprite sheet intentionally keeps the existing 8x6, 64px frame contract so
the WPF animation and alpha-region code do not need a behavioral migration.
"""

from __future__ import annotations

import math
from pathlib import Path

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
ASSET_DIR = ROOT / "app" / "DesktopPet.App" / "Assets"
SCALE = 4
CELL = 64
HI_CELL = CELL * SCALE

PINK = (241, 137, 169, 255)
PINK_LIGHT = (255, 184, 202, 255)
PINK_DARK = (211, 94, 132, 255)
OUTLINE = (78, 42, 61, 255)
GREEN = (77, 151, 93, 255)
GREEN_DARK = (48, 105, 63, 255)
PURPLE = (135, 83, 155, 255)
WHITE = (255, 255, 255, 255)
BLACK = (38, 29, 36, 255)
YELLOW = (255, 215, 108, 255)
RED = (224, 86, 105, 255)
BLUE = (94, 164, 214, 255)


def s(value: float) -> int:
    return int(round(value * SCALE))


def point(x: float, y: float) -> tuple[int, int]:
    return s(x), s(y)


def line(draw: ImageDraw.ImageDraw, points: list[tuple[float, float]], fill, width: float = 2.5) -> None:
    draw.line([point(*item) for item in points], fill=fill, width=max(1, s(width)), joint="curve")


def ellipse(draw: ImageDraw.ImageDraw, box: tuple[float, float, float, float], fill, outline=None, width: float = 1) -> None:
    draw.ellipse(tuple(s(value) for value in box), fill=fill, outline=outline, width=max(1, s(width)) if outline else 1)


def polygon(draw: ImageDraw.ImageDraw, points: list[tuple[float, float]], fill, outline=None, width: float = 1) -> None:
    scaled = [point(*item) for item in points]
    draw.polygon(scaled, fill=fill)
    if outline:
        draw.line(scaled + [scaled[0]], fill=outline, width=max(1, s(width)), joint="curve")


def star_points(cx: float, cy: float, radius_x: float, radius_y: float) -> list[tuple[float, float]]:
    points: list[tuple[float, float]] = []
    for index in range(10):
        angle = math.radians(-90 + index * 36)
        radius = radius_x if index % 2 == 0 else radius_x * 0.47
        points.append((cx + math.cos(angle) * radius, cy + math.sin(angle) * radius * radius_y))
    return points


def draw_shorts(draw: ImageDraw.ImageDraw, cx: float, cy: float, bob: float = 0) -> None:
    top = cy + 7 + bob
    bottom = cy + 24 + bob
    polygon(
        draw,
        [(cx - 13, top), (cx + 13, top), (cx + 12, bottom - 3), (cx + 6, bottom), (cx, bottom - 3), (cx - 7, bottom), (cx - 12, bottom - 3)],
        GREEN,
        GREEN_DARK,
        1.4,
    )
    # The purple flower marks keep the character readable at 64px without
    # copying any source artwork.
    for fx, fy in ((cx - 7, top + 6), (cx + 6, top + 12)):
        ellipse(draw, (fx - 2.1, fy - 2.1, fx + 2.1, fy + 2.1), PURPLE)
        ellipse(draw, (fx - 0.9, fy - 0.9, fx + 0.9, fy + 0.9), YELLOW)


def draw_face(draw: ImageDraw.ImageDraw, cx: float, cy: float, expression: str) -> None:
    eye_y = cy - 5
    if expression == "sleep":
        line(draw, [(cx - 9, eye_y), (cx - 4, eye_y + 1)], OUTLINE, 1.6)
        line(draw, [(cx + 4, eye_y + 1), (cx + 9, eye_y)], OUTLINE, 1.6)
    else:
        eye_radius = 4.0 if expression == "surprised" else 3.2
        pupil_radius = 1.25 if expression != "surprised" else 1.5
        for ex in (cx - 7, cx + 7):
            ellipse(draw, (ex - eye_radius, eye_y - eye_radius, ex + eye_radius, eye_y + eye_radius), WHITE, OUTLINE, 1)
            ellipse(draw, (ex - pupil_radius, eye_y - pupil_radius, ex + pupil_radius, eye_y + pupil_radius), BLACK)

    mouth_y = cy + 5
    if expression in ("happy", "smile"):
        draw.arc((s(cx - 7), s(mouth_y - 3), s(cx + 7), s(mouth_y + 6)), 15, 165, fill=OUTLINE, width=s(1.5))
    elif expression == "sad":
        draw.arc((s(cx - 7), s(mouth_y - 1), s(cx + 7), s(mouth_y + 8)), 195, 345, fill=OUTLINE, width=s(1.5))
    elif expression == "surprised":
        ellipse(draw, (cx - 3.3, mouth_y - 2, cx + 3.3, mouth_y + 5), OUTLINE)
        ellipse(draw, (cx - 1.4, mouth_y, cx + 1.4, mouth_y + 2.8), PINK_DARK)
    else:
        line(draw, [(cx - 5, mouth_y + 1), (cx + 5, mouth_y + 1)], OUTLINE, 1.4)


def draw_heart(draw: ImageDraw.ImageDraw, cx: float, cy: float, size: float, fill=PINK_DARK) -> None:
    polygon(
        draw,
        [(cx, cy + size), (cx - size * 1.15, cy - size * 0.1), (cx - size * 0.85, cy - size * 0.85), (cx, cy - size * 0.42), (cx + size * 0.85, cy - size * 0.85), (cx + size * 1.15, cy - size * 0.1)],
        fill,
    )


def draw_sandwich(draw: ImageDraw.ImageDraw, cx: float, cy: float, flip: bool = False) -> None:
    direction = -1 if flip else 1
    left = cx + direction * 15
    right = cx + direction * 29
    if left > right:
        left, right = right, left
    ellipse(draw, (left, cy - 5, right, cy + 4), YELLOW, OUTLINE, 1)
    line(draw, [(left + 1, cy - 1), (right - 1, cy - 1)], RED, 2)
    line(draw, [(left + 2, cy + 2), (right - 2, cy + 2)], GREEN_DARK, 1.5)


def draw_character(state: str, frame: int) -> Image.Image:
    image = Image.new("RGBA", (HI_CELL, HI_CELL), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)

    bob = (frame % 2) * 1.1 if state in {"idle", "happy"} else 0
    cx, cy = 32, 30 + bob
    expression = "neutral"
    arm_left = (cx - 18, cy + 2, cx - 27, cy + 8)
    arm_right = (cx + 18, cy + 2, cx + 27, cy + 8)
    leg_left = (cx - 8, cy + 20, cx - 13, cy + 28)
    leg_right = (cx + 8, cy + 20, cx + 13, cy + 28)

    if state == "walk":
        expression = "smile"
        if frame % 2:
            leg_left, leg_right = (cx - 8, cy + 20, cx - 16, cy + 27), (cx + 8, cy + 20, cx + 16, cy + 27)
            arm_left, arm_right = (cx - 18, cy + 2, cx - 26, cy - 4), (cx + 18, cy + 2, cx + 26, cy + 8)
        else:
            arm_left, arm_right = (cx - 18, cy + 2, cx - 26, cy + 8), (cx + 18, cy + 2, cx + 26, cy - 4)
    elif state == "sleep":
        expression = "sleep"
        cy = 31
        arm_left, arm_right = (cx - 17, cy + 4, cx - 23, cy + 10), (cx + 17, cy + 4, cx + 23, cy + 10)
    elif state == "reaction":
        expression = "surprised"
        arm_left, arm_right = (cx - 17, cy + 2, cx - 25, cy - 9), (cx + 17, cy + 2, cx + 25, cy - 9)
    elif state == "happy":
        expression = "happy"
        arm_left, arm_right = (cx - 17, cy + 2, cx - 26, cy - 8), (cx + 17, cy + 2, cx + 26, cy - 8)
    elif state == "sad":
        expression = "sad"
        arm_left, arm_right = (cx - 17, cy + 4, cx - 25, cy + 10), (cx + 17, cy + 4, cx + 25, cy + 10)
    elif state == "drag":
        expression = "surprised"
        cy = 32
        arm_left, arm_right = (cx - 16, cy + 1, cx - 24, cy - 12), (cx + 16, cy + 1, cx + 24, cy - 12)
        leg_left, leg_right = (cx - 7, cy + 18, cx - 11, cy + 26), (cx + 7, cy + 18, cx + 11, cy + 26)

    line(draw, [(arm_left[0], arm_left[1]), (arm_left[2], arm_left[3])], PINK, 5.5)
    line(draw, [(arm_right[0], arm_right[1]), (arm_right[2], arm_right[3])], PINK, 5.5)
    line(draw, [(leg_left[0], leg_left[1]), (leg_left[2], leg_left[3])], PINK, 5.5)
    line(draw, [(leg_right[0], leg_right[1]), (leg_right[2], leg_right[3])], PINK, 5.5)

    if state == "drag":
        points = star_points(cx, cy, 24, 0.82)
    else:
        points = star_points(cx, cy, 26, 0.98)
    polygon(draw, points, PINK, OUTLINE, 1.8)
    # A soft highlight gives the small sprite a readable focal point.
    ellipse(draw, (cx - 10, cy - 18, cx - 3, cy - 11), PINK_LIGHT)
    draw_shorts(draw, cx, cy)
    draw_face(draw, cx, cy, expression)

    if state == "reaction":
        line(draw, [(cx - 27, cy - 18), (cx - 23, cy - 14)], YELLOW, 1.5)
        line(draw, [(cx - 23, cy - 18), (cx - 27, cy - 14)], YELLOW, 1.5)
        line(draw, [(cx + 23, cy - 18), (cx + 27, cy - 14)], YELLOW, 1.5)
        line(draw, [(cx + 25, cy - 20), (cx + 25, cy - 12)], YELLOW, 1.5)
    elif state == "happy":
        draw_heart(draw, cx - 25, cy - 18, 4.2)
        draw_heart(draw, cx + 25, cy - 20, 3.4, YELLOW)
        if frame in (0, 2):
            draw_sandwich(draw, cx, cy - 2, flip=frame == 2)
    elif state == "sad":
        ellipse(draw, (cx - 10, cy + 1, cx - 7, cy + 7), BLUE)
        ellipse(draw, (cx + 7, cy + 1, cx + 10, cy + 7), BLUE)
    elif state == "sleep":
        line(draw, [(cx + 20, cy - 17), (cx + 24, cy - 17), (cx + 20, cy - 12), (cx + 24, cy - 12)], BLUE, 1.2)
        line(draw, [(cx + 26, cy - 25), (cx + 31, cy - 25), (cx + 26, cy - 19), (cx + 31, cy - 19)], BLUE, 1.2)

    if state == "fall":
        image = image.rotate(14 if frame == 0 else -14, resample=Image.Resampling.BICUBIC, expand=False)
    return image.resize((CELL, CELL), Image.Resampling.LANCZOS)


def build_sheet() -> Image.Image:
    sheet = Image.new("RGBA", (CELL * 8, CELL * 6), (0, 0, 0, 0))
    rows = [
        ("idle", 4),
        ("walk", 6),
        ("sleep", 4),
        ("fall", 2),
        ("drag", 1),
        ("reaction", 4),
        ("happy", 4),
        ("sad", 3),
    ]
    # The source contract has six rows. Row 3 contains fall, drag and reaction;
    # rows 4 and 5 contain happy and sad respectively.
    for row, (state, count) in enumerate(rows):
        if state == "drag":
            continue
        actual_row = 3 if state in {"fall", "reaction"} else row - (1 if row > 3 else 0)
        start = 0
        if state == "reaction":
            start = 3
        elif state == "happy":
            actual_row = 4
        elif state == "sad":
            actual_row = 5
        for frame in range(count):
            sheet.alpha_composite(draw_character(state, frame), (CELL * (start + frame), CELL * actual_row))
    sheet.alpha_composite(draw_character("drag", 0), (CELL * 2, CELL * 3))
    return sheet


def build_icon() -> Image.Image:
    source = Image.new("RGBA", (HI_CELL, HI_CELL), (0, 0, 0, 0))
    draw = ImageDraw.Draw(source)
    cx, cy = 32, 31
    line(draw, [(cx - 16, cy + 3), (cx - 25, cy + 11)], PINK, 6)
    line(draw, [(cx + 16, cy + 3), (cx + 25, cy + 11)], PINK, 6)
    line(draw, [(cx - 7, cy + 19), (cx - 12, cy + 28)], PINK, 6)
    line(draw, [(cx + 7, cy + 19), (cx + 12, cy + 28)], PINK, 6)
    polygon(draw, star_points(cx, cy, 26, 1), PINK, OUTLINE, 2)
    draw_shorts(draw, cx, cy)
    draw_face(draw, cx, cy, "smile")
    return source.resize((256, 256), Image.Resampling.LANCZOS)


def main() -> None:
    ASSET_DIR.mkdir(parents=True, exist_ok=True)
    sheet = build_sheet()
    sheet.save(ASSET_DIR / "patrick-sprites.png", format="PNG", optimize=True)
    icon = build_icon()
    icon.save(ASSET_DIR / "DesktopPet.ico", format="ICO", sizes=[(256, 256), (128, 128), (64, 64), (48, 48), (32, 32), (16, 16)])

    assert sheet.size == (512, 384)
    assert sheet.mode == "RGBA"
    assert sheet.getpixel((0, 0))[3] == 0
    assert sheet.getpixel((32, 32))[3] > 0
    print(f"wrote {ASSET_DIR / 'patrick-sprites.png'} ({sheet.size[0]}x{sheet.size[1]})")
    print(f"wrote {ASSET_DIR / 'DesktopPet.ico'}")


if __name__ == "__main__":
    main()

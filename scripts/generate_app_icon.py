from pathlib import Path

from PIL import Image, ImageDraw


OUTPUT = Path(__file__).resolve().parents[1] / "src" / "ProjectScaffolder.Editor" / "Assets" / "ProjectScaffolder.ico"
CANVAS = 1024
UNIT = CANVAS / 24


def point(x: float, y: float) -> tuple[float, float]:
    return x * UNIT, y * UNIT


image = Image.new("RGBA", (CANVAS, CANVAS), (0, 0, 0, 0))
draw = ImageDraw.Draw(image)
draw.rounded_rectangle((32, 32, CANVAS - 32, CANVAS - 32), radius=190, fill="#4f8bc9")
stroke = max(2, round(1.65 * UNIT))
white = "#ffffff"

# File outline and folded corner, adapted directly from resources/icons/file-box.svg.
draw.line([point(14, 2), point(14, 7), point(15, 8), point(20, 8)], fill=white, width=stroke, joint="curve")
draw.line(
    [point(14.7, 22), point(18, 22), point(20, 20), point(20, 8), point(19.3, 6.3),
     point(15.7, 2.7), point(14, 2), point(6, 2), point(4, 4), point(4, 7.8)],
    fill=white,
    width=stroke,
    joint="curve",
)

# Box body, top fold, and center edge.
draw.line([point(2.3, 13.75), point(7, 16.5), point(11.7, 13.75)], fill=white, width=stroke, joint="curve")
draw.line(
    [point(3, 13), point(2, 14.75), point(2, 18.25), point(3, 20), point(6, 21.75),
     point(8, 21.75), point(11, 20), point(12, 18.25), point(12, 14.75), point(11, 13),
     point(8, 11.25), point(6.1, 11.2), point(3, 13)],
    fill=white,
    width=stroke,
    joint="curve",
)
draw.line([point(7, 16.5), point(7, 22)], fill=white, width=stroke)

OUTPUT.parent.mkdir(parents=True, exist_ok=True)
image.save(
    OUTPUT,
    format="ICO",
    sizes=[(16, 16), (20, 20), (24, 24), (32, 32), (40, 40), (48, 48), (64, 64), (128, 128), (256, 256)],
)
print(OUTPUT)

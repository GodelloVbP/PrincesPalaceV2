"""How dark is the backdrop behind the fighters, once the scrim is on it?

TUNED TO A TARGET, NOT TO A MAXIMUM, and that is the whole reason this file
exists rather than a "separation score". Separation between figure and
background is monotonic in "make it darker": optimise it and you optimise the
painting out of existence. v1 did exactly that -- its first scrim scored better
on every readability metric it had and turned the backdrop into a black
rectangle.

So this reports the luminance that actually lands behind each slot and fails
outside a band. Too bright and the cel-shaded figures still sit on a busy
painting; too dark and there is no painting left to sit on.

    python tools/measure_scrim.py [backdrop-name]

Reads the BAKED scrim sprites, so it measures what ships rather than the
formulas that generated them.
"""
import re
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    sys.exit("needs Pillow: python -m pip install pillow")

sys.path.insert(0, str(Path(__file__).resolve().parent))
from measure_stage import constants, lerp, actors, read, SLOTS  # noqa: E402

ROOT = Path(__file__).resolve().parent.parent
GENERATED = ROOT / "Assets/_Project/Art/Generated"
BACKDROPS = ROOT / "Assets/_Project/Art/Backgrounds"
FIGHT_SCREEN = ROOT / "Assets/_Project/Scripts/Domain/UiKit/Screens/FightScreen.cs"

CANVAS_W, CANVAS_H = 1920.0, 1080.0

# The band a backdrop has to stay inside behind a figure. Below 30 the painting
# is gone; above 50 the cel art is competing with it.
TARGET_LOW, TARGET_HIGH = 30.0, 50.0


def scrim_layers():
    """The three layers, parsed out of BuildScrim rather than restated."""
    source = read(FIGHT_SCREEN)

    colours = dict(re.findall(r'Scrim(\w+)Colour\s*=\s*"#([0-9A-Fa-f]{8})"', source))
    if len(colours) != 3:
        sys.exit(f"expected 3 scrim colours in FightScreen, found {sorted(colours)}")

    keys = dict(re.findall(r'Scrim(\w+)Key\s*=\s*"proc:(\w+)"', source))

    pattern = re.compile(
        r'Ui\.Sprite\("Scrim(\w+)",\s*Scrim\w+Key,\s*Place\.At\(\s*(-?[\d.]+)f\s*,\s*(-?[\d.]+)f\s*\),'
        r'\s*UiSize\.Fixed\(\s*(-?[\d.]+)f\s*,\s*(-?[\d.]+)f\s*\)', re.S)

    layers = []
    for name, x, y, w, h in pattern.findall(source):
        if name not in colours or name not in keys:
            sys.exit(f"scrim layer '{name}' has no matching colour or key constant")
        hexa = colours[name]
        layers.append({
            "name": name,
            "sprite": keys[name],
            "x": float(x), "y": float(y), "w": float(w), "h": float(h),
            "rgb": (int(hexa[0:2], 16), int(hexa[2:4], 16), int(hexa[4:6], 16)),
            "alpha": int(hexa[6:8], 16) / 255.0,
        })

    if len(layers) != 3:
        sys.exit(f"expected 3 scrim sprites in BuildScrim, found {len(layers)}")
    return layers


def load(name):
    path = GENERATED / f"{name}.png"
    if not path.exists():
        sys.exit(f"{path} is missing - run tools/run_tests_parallel.ps1 -BuildScenes to bake it")
    return Image.open(path).convert("RGBA")


def layer_alpha(layer, image, x, y):
    """The layer's alpha at a canvas point, 0 outside its rect."""
    left, right = layer["x"] - layer["w"] / 2, layer["x"] + layer["w"] / 2
    bottom, top = layer["y"] - layer["h"] / 2, layer["y"] + layer["h"] / 2
    if not (left <= x <= right and bottom <= y <= top):
        return 0.0

    u = (x - left) / layer["w"]
    v = (y - bottom) / layer["h"]          # 0 at the bottom

    w, h = image.size
    # PNG row 0 is the TOP of the image, which is v = 1.
    px = min(w - 1, max(0, int(u * (w - 1))))
    py = min(h - 1, max(0, int((1.0 - v) * (h - 1))))
    return image.getpixel((px, py))[3] / 255.0 * layer["alpha"]


def luminance(rgb):
    return 0.2126 * rgb[0] + 0.7152 * rgb[1] + 0.0722 * rgb[2]


def main():
    which = sys.argv[1] if len(sys.argv) > 1 else "Fight"
    backdrop_path = BACKDROPS / f"{which}.png"
    if not backdrop_path.exists():
        sys.exit(f"no backdrop at {backdrop_path}")

    backdrop = Image.open(backdrop_path).convert("RGB")
    bw, bh = backdrop.size

    c = constants()
    people = actors()
    widest = max(people, key=lambda a: a["width"])
    tallest = max(people, key=lambda a: a["above"])

    layers = scrim_layers()
    sprites = {layer["name"]: load(layer["sprite"]) for layer in layers}

    print(f"backdrop {which}.png  {bw}x{bh}")
    for layer in layers:
        print(f"  scrim {layer['name']:7s} {layer['sprite']:12s} "
              f"at ({layer['x']:.0f}, {layer['y']:.0f}) {layer['w']:.0f}x{layer['h']:.0f} "
              f"alpha {layer['alpha']:.2f}")
    print()

    failures = []
    print("Behind each slot (backdrop luminance, 0-255):")

    for i in range(SLOTS):
        depth = i / (SLOTS - 1)
        scale = lerp(c["near_scale"], c["far_scale"], depth) * c["sprite_scale"]
        ground = lerp(c["near_y"], c["far_y"], depth)
        slot_x = lerp(c["near_x"], c["far_x"], depth)

        half = widest["canvas"][0] * scale / 2.0
        height = tallest["above"] * scale

        # Both sides: the two armies stand on different halves of the painting.
        for mirrored in (True, False):
            cx = -slot_x if mirrored else slot_x

            bare_total = scrimmed_total = 0.0
            samples = 0
            for sx in range(9):
                for sy in range(9):
                    x = cx - half + (2 * half) * sx / 8.0
                    y = ground + height * sy / 8.0

                    px = min(bw - 1, max(0, int((x + CANVAS_W / 2) / CANVAS_W * bw)))
                    py = min(bh - 1, max(0, int((CANVAS_H / 2 - y) / CANVAS_H * bh)))
                    base = backdrop.getpixel((px, py))

                    out = list(base)
                    for layer in layers:
                        a = layer_alpha(layer, sprites[layer["name"]], x, y)
                        if a <= 0:
                            continue
                        out = [layer["rgb"][k] * a + out[k] * (1 - a) for k in range(3)]

                    bare_total += luminance(base)
                    scrimmed_total += luminance(out)
                    samples += 1

            bare = bare_total / samples
            scrimmed = scrimmed_total / samples
            side = "party" if mirrored else "enemy"

            flag = ""
            if scrimmed < TARGET_LOW:
                flag = " <- too dark, the painting is gone"
                failures.append(f"{side} slot {i}: L {scrimmed:.0f} below {TARGET_LOW:.0f}")
            elif scrimmed > TARGET_HIGH:
                flag = " <- too bright, the figures still compete with it"
                failures.append(f"{side} slot {i}: L {scrimmed:.0f} above {TARGET_HIGH:.0f}")

            print(f"  {side} slot {i}: bare {bare:5.1f} -> scrimmed {scrimmed:5.1f}"
                  f"  (-{bare - scrimmed:4.1f}){flag}")

    print()
    for failure in failures:
        print("FAIL: " + failure)

    if failures:
        print(f"\nTune the alphas in FightScreen.Scrim*Colour. Target L "
              f"{TARGET_LOW:.0f}-{TARGET_HIGH:.0f}.")
        return 1

    print(f"OK - every slot's backdrop sits inside L {TARGET_LOW:.0f}-{TARGET_HIGH:.0f}.")
    return 0


if __name__ == "__main__":
    sys.exit(main())

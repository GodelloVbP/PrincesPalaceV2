"""Draws Resources/Intent/summon.png.

The other seven intent badges are game-icons.net silhouettes (CC BY 3.0, see
CREDITS.md). That set has no summon icon, so this draws one to the same
specification: a WHITE silhouette on transparency, 128x128, tinted at runtime by
FightHudPalette.IntentSummon.

A BARE PENTAGRAM, and the missing ring is the whole point. The obvious drawing
is a summoning circle -- ring plus pentagram -- and it was drawn first; at the
size a badge actually renders (~28px) it collapses into a white disc with
texture in it, which is exactly what the existing "skill" icon (a hand inside a
swirl) also collapses into. Those two are the pair most likely to be confused,
because a summon IS a skill and they sit next to each other on the colour wheel.
Dropping the ring leaves a spiky star against a round blob, which survives the
downsample.

Rendered at 4x and downsampled, which is where the antialiasing comes from --
PIL has no antialiased draw.
"""
import math
from PIL import Image, ImageDraw

SS = 4                      # supersample factor
SIZE = 128
N = SIZE * SS
C = N / 2.0
WHITE = (255, 255, 255, 255)

img = Image.new("RGBA", (N, N), (255, 255, 255, 0))
d = ImageDraw.Draw(img)

# Five points, first one straight up, joined every other one.
R_STAR = N * 0.44
pts = []
for i in range(5):
    a = -math.pi / 2 + i * 2 * math.pi / 5
    pts.append((C + R_STAR * math.cos(a), C + R_STAR * math.sin(a)))

STROKE = int(N * 0.075)
for i in range(5):
    a = pts[i]
    b = pts[(i + 2) % 5]
    d.line([a, b], fill=WHITE, width=STROKE)
    # Round the joins by hand -- PIL's line joins are square, and the star's
    # points come out clipped flat without this.
    d.ellipse([a[0] - STROKE / 2, a[1] - STROKE / 2,
               a[0] + STROKE / 2, a[1] + STROKE / 2], fill=WHITE)

img = img.resize((SIZE, SIZE), Image.LANCZOS)
img.save("Assets/_Project/Resources/Intent/summon.png")
print("wrote Assets/_Project/Resources/Intent/summon.png", img.size)

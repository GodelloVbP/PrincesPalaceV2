"""Draws Resources/Status/chilled.png and Resources/Status/rooted.png.

New folder, not Intent/ -- these are COMBATANT STATUS badges (party-buff row
above the acting character's portrait), not enemy-intent telegraphs, and
Resources.Load resolves the folder name as part of the path exactly like
"Intent/" + slug does (see EnemyIntentIcons.ResourceFor). Reusing Intent/
would make a status badge and an intent badge collide on the same shelf for
no reason other than both being combat art.

Same specification as every Intent icon and as make_summon_icon.py in
particular, which is the exact template this script follows: a WHITE
silhouette on transparency, 128x128, drawn at 4x with PIL's ImageDraw (which
has no antialiasing of its own) and downsampled with LANCZOS, tinted at
runtime rather than baked with colour. Designed and reasoned about
specifically for the size a badge actually renders at -- roughly 28-46px,
the party-buff row's icon slots -- not for how the shape reads full-screen.

Both icons below are self-drawn (no game-icons.net source matched either
status), so there is nothing to add to CREDITS.md -- same as summon.png.
"""
import math
from PIL import Image, ImageDraw

SS = 4                      # supersample factor
SIZE = 128
N = SIZE * SS
C = N / 2.0
WHITE = (255, 255, 255, 255)


def new_canvas():
    return Image.new("RGBA", (N, N), (255, 255, 255, 0))


def save(img, path):
    img = img.resize((SIZE, SIZE), Image.LANCZOS)
    img.save(path)
    print(f"wrote {path}", img.size)


# ---------------------------------------------------------------------------
# Chilled: a six-spike ice burst.
#
# The obvious drawing is an actual snowflake -- six arms, each with its own
# pair of side dendrites branching off. That was tried first and it is the
# same failure summon.py already hit with the ring-plus-pentagram: the side
# branches are individually thin, and six of them downsampled from 512px to
# 128px (then displayed at a further 3-4x reduction on the actual badge)
# fuzz out before they reach the screen, leaving a soft grey dot with no
# readable structure -- exactly the "collapses into a blob" failure this
# project already has a name for. A snowflake's DETAIL is what a snowflake
# is, and detail is precisely what a ~30px badge cannot carry.
#
# What survives is the one thing bare spikes always survive: BOLD RADIAL
# SYMMETRY read as a silhouette, no interior texture required. Six tapered
# wedges (not uniform-width lines -- a line has a blunt flat end, and a
# blunt-ended asterisk reads as a plus sign or a compass rose, not ice) meet
# at a small rounded hub, each wedge a triangle that comes to an actual
# point. That point is the whole "faceted" read the brief asks for: a
# crystal has edges, not a rounded tip, so nothing here gets a curve.
STROKE_HALF = N * 0.085      # half-width of a spike at its base
SPOKE_LEN = N * 0.46
HUB_RADIUS = N * 0.05

chilled = new_canvas()
d = ImageDraw.Draw(chilled)

for i in range(6):
    a = -math.pi / 2 + i * (math.pi / 3)     # six spikes, 60 degrees apart
    dirx, diry = math.cos(a), math.sin(a)
    perpx, perpy = -diry, dirx

    tip = (C + dirx * SPOKE_LEN, C + diry * SPOKE_LEN)
    base_l = (C + perpx * STROKE_HALF, C + perpy * STROKE_HALF)
    base_r = (C - perpx * STROKE_HALF, C - perpy * STROKE_HALF)

    d.polygon([base_l, tip, base_r], fill=WHITE)

# One hub circle to seal the six triangle bases into a single clean centre
# rather than six wedges visibly mitred together -- the same "round the
# joins by hand" fix summon.py used for its star points, applied to a hub
# instead of a vertex because six things meet here instead of two.
d.ellipse([C - HUB_RADIUS, C - HUB_RADIUS, C + HUB_RADIUS, C + HUB_RADIUS], fill=WHITE)

save(chilled, "Assets/_Project/Resources/Status/chilled.png")


# ---------------------------------------------------------------------------
# Rooted: a three-prong root claw.
#
# The obvious drawing is a tangle of curling vines -- the shape a "root"
# search actually turns up. Drawn first, and it fails for a reason distinct
# from Chilled's: a curling vine's silhouette is basically a filled blob with
# a wavy outline, which at badge size reads as an irregular circle -- and
# this project's own Intent/poison.png is ALREADY an irregular filled blob
# (a droplet folded into a spiral). Rooted needs to be a different SHAPE
# CLASS from poison, not just a different curl of the same one, or a player
# glancing at the buff row is reading two blobs and guessing which is which.
#
# Three straight tapering wedges fanned from one hub, tips pointing down and
# outward, is a different class entirely: angular where poison is round,
# radiating where poison is coiled, and it reads at a glance as a claw or a
# root clump digging into the ground -- which is the status's actual effect
# (the target is anchored and cannot melee). The three prongs are
# deliberately UNEVEN -- one long straight prong plus two shorter angled
# ones, not three equally-spaced spikes -- because perfect radial symmetry
# here reads as a trident or a weapon icon; the asymmetry is what keeps it
# reading as a root system instead. A curved, multi-segment version of each
# prong (an actual root bending as it grows) was tried too and rejected for
# the same reason the snowflake's side dendrites were: a bend is a second
# point of detail per prong, and detail this small does not survive LANCZOS
# at 128px let alone the further downscale onto a live badge.
HUB_R = N * 0.055

rooted = new_canvas()
d = ImageDraw.Draw(rooted)

apex = (C, C - N * 0.16)

# (angle from straight-down in degrees, length, base half-width)
prongs = [
    (-30, N * 0.46, N * 0.085),
    (0, N * 0.60, N * 0.10),
    (34, N * 0.44, N * 0.085),
]

for deg, length, half_w in prongs:
    a = math.pi / 2 + math.radians(deg)      # 0 deg = straight down
    dirx, diry = math.cos(a), math.sin(a)
    perpx, perpy = -diry, dirx

    tip = (apex[0] + dirx * length, apex[1] + diry * length)
    base_l = (apex[0] + perpx * half_w, apex[1] + perpy * half_w)
    base_r = (apex[0] - perpx * half_w, apex[1] - perpy * half_w)

    d.polygon([base_l, tip, base_r], fill=WHITE)

d.ellipse([apex[0] - HUB_R, apex[1] - HUB_R, apex[0] + HUB_R, apex[1] + HUB_R], fill=WHITE)

save(rooted, "Assets/_Project/Resources/Status/rooted.png")

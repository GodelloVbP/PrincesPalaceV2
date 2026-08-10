# Hub building art — SOURCES

These are the raw generations, still on their green screen. **Nothing here is
loaded by the game.** `tools/key_green_screen.py` cuts the green off them and
writes the real sprites to `Assets/_Project/Resources/Hub/<building>/f0.png..`,
which is what `SceneBuilder` and `HubBuildingAnimator` actually read.

    py tools/key_green_screen.py

Then rebuild the scene (`Prince's Palace > Build All Scenes`).

## Naming, and how a building gets animated

Frames are grouped by filename with any trailing `_<number>` stripped:

    gate.png                -> gate,          1 frame,  still
    talents_1/_2/_3.png     -> talents,       3 frames, cycles
    principality_1.png      -> principality,  1 frame

So **dropping another numbered variant beside its siblings is the whole
interface for animating a building.** `HubBuildingAnimator` finds the extra
frames at runtime and cycles them slowly; a building with a lone `f0` finds
nothing to cycle and never starts a coroutine, so a still building costs
nothing.

Variants should be the same subject with something small changed — the talents
tree's nodes lit differently between takes is exactly right. Anything that
moves the silhouette will read as a glitch rather than as life.

## Why green, and not transparency

Image generators are unreliable at real alpha, and the obvious workaround —
generate on black and key the alpha from brightness, which is how the spell
frames are cut — is *actively wrong* for a building. A building has dark stone,
deep shadow, and in the gate's case a deliberately dark void inside the arch
that has to stay **opaque**; brightness-keying punches straight through all of
it. Green appears nowhere in this palette, so keying on hue removes the
backdrop and touches nothing else.

The keyer also suppresses green spill on edges, and forces each output PNG's
importer to Sprite with `alphaIsTransparency` on. That last part is not
optional: Unity defaults a new PNG to a plain Texture, `Resources.Load<Sprite>`
on a plain Texture returns **null**, and the building then draws nothing with
no error and no warning. That has cost two debugging sessions already, which is
why the tool does it rather than leaving it to be remembered.

## Sizes

Sources are square, subject centred with roughly 10% padding — the padding is
what gives the engine's 105% hover somewhere to grow. The keyer resamples to
the delivery size per building; it does **not** re-crop or re-centre, because
the framing is what keeps the buildings' apparent sizes consistent with each
other.

Current delivery sizes (2× the on-screen size, so they stay sharp at 4K) live
in `DELIVERY_SIZE` at the top of the keyer. See `docs/archive/2026-08-01_HUB_DESIGN_BRIEF.md` at the
repo root for the composition, the exact positions, and the rules any building
added later has to follow.

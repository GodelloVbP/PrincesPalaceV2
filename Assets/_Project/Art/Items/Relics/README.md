# Relic icon art — SOURCES

**Status: delivered.** All three relic icons landed pre-processed (real alpha
already baked in, not green-screened) and live in `Processed/dual_wield.png`,
`Processed/magical_shield.png`, `Processed/bloodlust.png` — exactly what
`RelicDefinition.iconPath` (via `SceneBuilder.LoadSprite`) reads. Nothing left
to do here; this README stays for the next relic added to the roster.

For a *future* relic delivered the other way (raw, still on green screen),
this folder is still the source directory: `tools/key_green_screen.py` cuts
the green off anything dropped directly in `Assets/_Project/Art/Items/Relics/`
(not `Processed/`) and writes the keyed result to `Processed/<name>.png`.

    py tools/key_green_screen.py

Then rebuild content (`Prince's Palace > Build Default Content`) and the scene
(`Prince's Palace > Build All Scenes`) so the Relics screen picks up the new
icon — remember to also point the new relic's `iconPath` at the file in
`Assets/_Project/ContentData/relics.json`.

## Naming

One file in, one keyed file of the same name out — no grouping, no animation
frames (unlike the Hub buildings). Exact filenames expected:

    dual_wield.png
    magical_shield.png
    bloodlust.png

(One filename per relic id, exactly — `relics.json`'s `iconPath` is keyed off
the same names.)

## Why green, and not transparency

Image generators are unreliable at real alpha, and the obvious workaround —
generate on black and key the alpha from brightness — is wrong here too: some
relic concepts (Magical Shield especially) have deep shadow that has to stay
opaque. Green appears nowhere in this palette, so keying on hue removes the
backdrop and touches nothing else. See `RELIC_AND_TALENT_TREE_ART_PROMPTS.md`
at the repo root for the exact copy-paste prompts.

The keyer also forces each output PNG's importer to Sprite with
`alphaIsTransparency` on — required, or `LoadSprite` silently leaves the icon
slot blank with only a console warning to explain why.

## Sizes

Generate square, subject centred with roughly 10% padding, then downscale to
**256×256** before keying — the image generator's native output is larger
(commonly 1024×1024) and every icon in the game is shown small enough that
the extra resolution just makes the alpha-key edges more expensive to clean
up. Unlike the Hub buildings, the keyer does not resample on its own: whatever
size the file is when it hits `key_green_screen.py` is the size that ships,
so downscale first, key second.

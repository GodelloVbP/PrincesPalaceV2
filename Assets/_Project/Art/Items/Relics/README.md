# Relic icon art — SOURCES

**Status: delivered (36/38).** Three icons landed pre-processed in an earlier
pass (`Processed/dual_wield.png`, `Processed/magical_shield.png`,
`Processed/bloodlust.png`), fifteen more landed in the pass after that, and a
final batch of twenty landed 2026-09-05, raw, in this folder (not
`Processed/`) under their delivery names. Eighteen of that batch keyed/copied
cleanly to `Processed/<relic id>.png`, exactly what `RelicDefinition.iconPath`
(via `SceneBuilder.LoadSprite`) reads. The remaining two relics
(`dancers_anklet`, `loaded_dice`) stay on the `disc_metal.png` placeholder —
see "Two files are unassigned" below.

For a *future* relic delivered raw (still on green screen), this folder is
still the source directory: `tools/key_green_screen.py` cuts the green off
anything dropped directly in `Assets/_Project/Art/Items/Relics/` (not
`Processed/`) and writes the keyed result to `Processed/<name>.png`.

    py tools/key_green_screen.py

Then rebuild content (`Prince's Palace > Build Default Content`) and the scene
(`Prince's Palace > Build All Scenes`) so the Relics screen picks up the new
icon — remember to also point the new relic's `iconPath` at the file in
`Assets/_Project/ContentData/relics.json`.

## The 2026-09-05 batch: twenty relics, two delivered green, eighteen alpha

The twenty files arrived under their own delivery names, not the relic id —
so each had to be copied (never resampled) to a file named for the relic id
before running the keyer:

| Delivery filename | Relic id | Keying |
|---|---|---|
| `amassing_star.png` | `amassing_star` | already had real alpha — copied unchanged |
| `berserkers_vest.png` | `berserkers_vest` | already had real alpha — copied unchanged |
| `cursed_idol.png` | `cursed_idol` | already had real alpha — copied unchanged |
| `dancers_compass.png` | *unassigned* — see below | already had real alpha — not copied to `Processed/<relic id>.png` |
| `disgruntled_lackey.png` | `disgruntled_lackey` | already had real alpha — copied unchanged |
| `essence_siphon.png` | `essence_siphon` | already had real alpha — copied unchanged |
| `ice_fingernail.png` | `ice_fingernail` | already had real alpha — copied unchanged |
| `inconspicuous_key.png` | `inconspicuous_key` | already had real alpha — copied unchanged |
| `jar_of_bear_urine.png` | `jar_of_bear_urine` | already had real alpha — copied unchanged |
| `jo_suns_book_of_anatomy.png` | `jo_suns_book_of_anatomy` | already had real alpha — copied unchanged |
| `magic_marker.png` | `magic_marker` | already had real alpha — copied unchanged |
| `monkey_kings_scepter.png` | `monkey_kings_scepter` | already had real alpha — copied unchanged |
| `phoenix_egg.png` | `phoenix_egg` | already had real alpha — copied unchanged |
| `pointy_nail_on_a_stick.png` | `pointy_nail_on_the_end_of_a_stick` | already had real alpha — copied unchanged |
| `rampaging_bulls_horn.png` | `rampaging_bulls_horn` | already had real alpha — copied unchanged |
| `sparring_buckler.png` | `sparring_buckler` | already had real alpha — copied unchanged |
| `sparring_saber-v2.png` | `sparring_saber` | green-screened (`#08D111`-ish) — keyed |
| `trickshot_token.png` | *unassigned* — see below | already had real alpha — not copied to `Processed/<relic id>.png` |
| `vampire_dentures.png` | `vampire_dentures` | already had real alpha — copied unchanged |
| `world_enders_crown-v2.png` | `world_enders_crown` | green-screened (`#08D111`-ish) — keyed |

Only two of the twenty were actually green-screened; the other eighteen
already carried real alpha (transparent corners, no green residue) and were
copied straight to `Processed/` **without running the keyer on them** — running
`key_out_green` on a real-alpha source is not a no-op here: every one of the
eighteen has RGB `(0,0,0)` under its transparent pixels, and the keyer's
greenness test (`g - max(r,b)`) reads pure black as fully-opaque background,
which would have turned the *entire* 256x256 canvas opaque on every one of
them (measured: 56%-637% inflation in opaque-pixel count, not a rounding
error). Confirmed only the two `-v2` files are truly green-screened
(opaque `#08D111`-ish corners) before choosing this path.

### Two files are unassigned, not wired to any relic

Eighteen of the twenty files matched a relic id once the `-v2` suffix was
dropped and `pointy_nail_on_a_stick` was read as
`pointy_nail_on_the_end_of_a_stick`. Two did not match anything by name, and
matching them to the two relics still left on `disc_metal.png` by
elimination (`loaded_dice`, `dancers_anklet`) does not hold up: the art is
wrong for both.

- `trickshot_token.png` is unambiguously a round coin/token with a crack down
  the middle — no dice anywhere in it.
- `dancers_compass.png` is unambiguously a compass (a ribboned dial with a
  star-burst needle) — no anklet in it.

Both pieces are well-drawn and on-style; they are simply illustrating their
own delivery names, not `loaded_dice` or `dancers_anklet`. They are **not**
copied into `Processed/` under either relic id and `relics.json` keeps both
relics on the `disc_metal.png` placeholder. The art stays here and in
`Processed/` under its own delivery name (`dancers_compass.png`,
`trickshot_token.png`) for a later decision: either relic gets renamed to
match the art it already has, or fresh art gets commissioned for
`dancers_anklet` and `loaded_dice` as written. Whoever owns the roster makes
that call — this is not a job for elimination-by-filename.

## Naming

One file in, one keyed file of the same name out — no grouping, no animation
frames (unlike the Hub buildings). Exact filenames expected (relic id, never
the delivery name a batch arrived under):

    dual_wield.png
    magical_shield.png
    bloodlust.png
    amassing_star.png
    berserkers_vest.png
    cursed_idol.png
    disgruntled_lackey.png
    essence_siphon.png
    ice_fingernail.png
    inconspicuous_key.png
    jar_of_bear_urine.png
    jo_suns_book_of_anatomy.png
    magic_marker.png
    monkey_kings_scepter.png
    phoenix_egg.png
    pointy_nail_on_the_end_of_a_stick.png
    rampaging_bulls_horn.png
    sparring_buckler.png
    sparring_saber.png
    vampire_dentures.png
    world_enders_crown.png

(One filename per relic id, exactly — `relics.json`'s `iconPath` is keyed off
the same names.) `dancers_anklet.png` and `loaded_dice.png` are deliberately
absent from this list and from `Processed/` — see "Two files are unassigned"
above.

## Why green, and not transparency

Image generators are unreliable at real alpha, and the obvious workaround —
generate on black and key the alpha from brightness — is wrong here too: some
relic concepts (Magical Shield especially) have deep shadow that has to stay
opaque. Green appears nowhere in this palette, so keying on hue removes the
backdrop and touches nothing else. See `RELIC_AND_TALENT_TREE_ART_PROMPTS.md`
at the repo root for the exact copy-paste prompts.

The keyer also forces each output PNG's importer to Sprite with
`alphaIsTransparency` on — required, or `LoadSprite` silently leaves the icon
slot blank with only a console warning to explain why. That importer fix-up
only touches a `.meta` that already exists; a brand-new PNG with no `.meta`
yet needs Unity to import it once first (see the 2026-09-05 batch note above —
none of its twenty files have a `.meta` in this worktree for exactly that
reason).

## Sizes

Generate square, subject centred with roughly 10% padding, then downscale to
**256×256** before keying — the image generator's native output is larger
(commonly 1024×1024) and every icon in the game is shown small enough that
the extra resolution just makes the alpha-key edges more expensive to clean
up. Unlike the Hub buildings, the keyer does not resample on its own: whatever
size the file is when it hits `key_green_screen.py` is the size that ships,
so downscale first, key second. The 2026-09-05 batch arrived already at
256×256 and was not resampled.

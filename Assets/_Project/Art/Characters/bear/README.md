# Bjorn — the bear veteran, six key stills

Delivered 2026-09-07. The third party member with battle art after Shawn and
Odette, replacing `placeholder_brawler`. STR/CON Tank, and the owner's
favourite bear design — see `output/bear-veteran-v4/prompt.txt` for the full
accepted prompt; this file only summarises it.

## Source

`sheet_poses.png` — 1536x1024, 3x2, copied verbatim from
`Assets/_Project/Art/Sheets/bear_veteran_sheet-v4.png` (the fourth generation;
v2/v3 beside it in `Art/Sheets/` are earlier attempts, left alone — the owner
decides what happens to those copies). The brief was a style-transfer pass:
redraw the owner's favourite bear design (image 1) in the flat, spare,
strong-outline rendering Odette and Shawn's sheets established (images 2 and
3), keeping the bear's own silhouette, copper pauldron/bracers, red scarf and
paw-emblem tabard, facial scar and paw-emblem hammer untouched. The prompt
also asked for "transparent background with real alpha, no painted
checkerboard" and got the opposite — the delivered file is RGB with no alpha
channel at all, a baked light checkerboard (~247-253 grey/white) standing in
for it, same as the owl's and the treant's sheets before it.

Cells, row-major: idle (standing, hammer down), attack (hammer swing), cast
(roaring, paw raised), hurt (flinch, arm up), defeated (lying down), victory
(hammer raised overhead) — the six stances `FightSession.Stances` drives for
a party member. The bear faces RIGHT in every cell (nose/attack direction
both point right — confirmed by eye against the sheet), matching
`battleSpriteFacing: "Right"`.

## Keying

`white_flood --pocket-max-area 4000`, same setting the owl and treant needed
and for the same reason: the checkerboard is baked into the RGB rather than
carried as alpha, and the cape, the gap between the raised arm and the body,
and the space under the hammer head each enclose a checkerboard pocket well
over the 200px default speck cap. Checked programmatically after slicing —
scanned every delivered still's opaque pixels for near-white
(RGB >= 235), near-neutral (channel spread <= 6) pockets that would mean a
checkerboard survivor: five of six stances came back with zero suspect
pixels, `defeated.png` with exactly one (a single antialiased highlight
pixel on the armor rim, not a pocket). `pocket-max-area` did not need
raising.

## Sizing

Sliced once at `--delivery-scale 1.0`: idle content height measured 446px.
Target is 1.10x Shawn's currently delivered idle bbox height — re-measured
directly rather than trusting the sheep README's stated 349px, which reads
**350px** off the committed `Resources/Characters/sheep/idle.png` alpha
bbox (a 1px gap from the README, immaterial to the target). Target = 1.10 x
350 = 385px. Re-sliced at `385/446 = 0.8632286995515696`; the written
`idle.png` now measures exactly 385px, a head taller than the sheep, as a
STR/CON frontliner should.

**Ceiling check.** `Domain/Stage/FightStageAnchors.cs`'s ceiling comment
names the golem as the tallest actor on record at 384px above its own
manifest ground line, with room to spare under the enemy plate (head lands
around y=72-90 against a plate edge at 96-108). The bear's 385px is 1px over
that reference point — inside the same margin the golem already clears by
12-36px, not a new binding constraint — so the delivery scale was not
dropped to 1.0. `FightScreenTests` and `PartyFormationCaptureTests` both
pass with the bear's art on disk (see the delivery commit's test run).

## Accepted metrics

Filled area (opaque pixel count) and fur px (brown-fur-hued opaque pixels,
isolated by hue/saturation from the copper armor, red cape and cream trim)
against idle, plus the slicer's own `sqrt(largest-component-mass)`:

| stance | filled area / idle | fur px / idle | sqrt(mass) |
|---|---:|---:|---:|
| idle | 1.00 | 1.00 | 285.3 |
| attack | 0.95 | 1.06 | 278.1 |
| cast | 1.21 | 1.26 | 302.1 |
| hurt | 0.94 | 1.05 | 277.1 |
| defeated | 0.77 | 0.86 | 249.7 |
| victory | 0.93 | 1.12 | 275.8 |

Cast sits over the usual 0.90-1.10 band on filled area — it is the roaring
pose's fully outstretched raised arm and open stance widening the canvas,
not a body redraw at a different scale; fur px rises with it in proportion,
which is what a genuine pose spread looks like rather than a resize. Defeated
legitimately loses mass, same as every other actor's collapsed pose.

## Provenance

**Reproducible — `recipe.json`, beside this file.** Replay it with:

```bash
python tools/slice_actor_sheet.py --recipe Assets/_Project/Art/Characters/bear/recipe.json
```

Verified 2026-09-07: the replay reproduces all six committed stills and
`Resources/StanceManifest.json`'s `Characters/bear` entry byte for byte
(sha256-compared before/after), which is what earns this actor
`groundLineSource: "slicer"`. `groundLine` is 74.

## Judgements made, not measured

- **`breath: 1.0`** in `StanceManifest.json`'s `Characters/bear` entry is
  copied, not tuned. Shawn's own entry (`Characters/sheep`) carries no
  `breath` override at all, which means the runtime default of 1.0 applies
  to him — this makes that same number explicit for the bear rather than
  authoring a new one. No `hover` block: the bear stands on the ground like
  Shawn, unlike the owl.
- **Display name "Bjorn"** is the owner's call and is provisional, same
  status Odette's stats/skill kit carried at her own delivery.
- **Portrait is borrowed.** `portraitPath` stays `Portraits/sheep` verbatim
  from `placeholder_brawler` — no bear portrait exists yet, so the dossier
  shows Shawn's neutral portrait for him too, exactly as it did for Odette
  before an owl portrait existed. First thing a design pass on this
  character should fix.
- **Skill ids kept as `placeholder_brawler_*`.** Renaming them would touch
  saves and tests for no visible gain today, the same call Odette's delivery
  made for her `placeholder_caster_*` skills. A design pass on stats, skill
  ids and a STR-scaled kit is a separate job from seating the bear in his
  own art.

## Delivery

```bash
python tools/slice_actor_sheet.py --sheet Assets/_Project/Art/Characters/bear/sheet_poses.png \
    --actor Characters/bear --stances idle,attack,cast,hurt,defeated,victory \
    --key white_flood --pocket-max-area 4000 --delivery-scale 0.8632286995515696 --prune
```

Canvas 486x467, `groundLine 74`, idle content height 385px against the sheep's
measured 350px (1.10x, by design).

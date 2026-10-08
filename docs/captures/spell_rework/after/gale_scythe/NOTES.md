# Gale Scythe: state record

Structure: `docs/captures/spell_rework/NOTES_TEMPLATE.md`. Plan S3, spell 3.4. Pre-workflow pass; owner gates were skipped, the calls below are the worker's.

## Status

Pass 2 (2026-10-04) wired the owner's painted blade (`gale_scythe_blade.png`, 2172x724): `92e06cba`; pass 1 used the 12-frame wind sheet only (`5915e34f`). Captured with `tools/gif.ps1 spell gale_scythe` and `tools/spell_feel.ps1`; GIFs untracked beside this file. Not yet through the workflow's steps 3-7.

## Identity

- **Verb:** cut across.
- **Signature beat:** a single long blade of wind crosses the whole line left to right in one stroke, then every enemy is marked by the same blade on the same frame.
- **Must read as:** "one cut across all of them".
- **Must NOT read as:** a rat-sized whirl rolling like a wheel; tornados (the pass-1 failure).

## Beat table

Release clock; cue 0.30 s = f18 at 1x, f12 at 1.5x. One payoff frame for all enemies: numbers, cut marks and hurt flashes open on the same frame on the full formation, two enemies, one enemy and the mixed rack (decision 8); nothing staggered.

| t | Beat | Layer | Art piece | Box / feel |
|---|---|---|---|---|
| 0.00-0.20 | quiet: a small whirl closing in Shawn's hand; nothing on the enemies | wind sheet f0-f3 | old sheet | n/a |
| 0.13-0.26 | sweep: blade laid along the line of struck bodies (`place formation`, `align span`, overhang 0.5), carried from the caster's side of the front body past the last (`drift` to 0.22), fading out at the cue; decorative | sweep | `Spells/gale_scythe_blade/f0` (one still, 6.3:1) | n/a |
| 0.30 | shared payoff on every enemy: the same blade drawn across each body, sound, all numbers, the push in the turn order, 16 leaves | per-body | same still | size 380, aspect 6.3, 0.12 s, fade 0.2, short drift through the body; punch 0.2, glow 1.2 |
| 0.30-0.65 | blades fade, leaves settle | | | blades gone about 0.3 s after the cue at 1x |

## Open items

1. The per-enemy payoff is a thin stroke at the waist, so the hit reads through the white hurt flash and the numbers more than the blade. **Owner call**: a short heavier slash mark needs a second painted piece.
2. Three-wide rack: the sweep tip runs past the right edge of the stage crop (the crop, not the stage). Trim `drift.toX` (0.22) or `overhang` (0.5) if it matters in a fight.
3. Treants: at the cue frame the blade shows a dark grey smear at the right edge for one frame (the sweep's last fading frame over the white hurt flash). Not investigated.
4. Push in the turn order not visible in these frames (HUD strip off the crop); unchanged.
5. Measured, no WARNs: payoff to clean 0.40 s (1x rat), 0.38 s (1x treant), 0.48 s (1.5x rat, the rat's own idle settling), 0.25 s (1.5x treant).

## Decisions

- 2026-10-04 (worker): keyed `"green-soft"` (new in `tools/slice_spell_sheet.py`) for the blade: the `green` key clamps green to min(red, blue), turning the pale sea-green blade lilac and the gold flecks red. `green-soft` keeps the keyer's spill suppression and skips the clamp.
- 2026-10-04 (worker): the sheet's three tornado whorls are gone.

## Art

| Piece | File | Recipe | Status |
|---|---|---|---|
| Blade | `gale_scythe_blade.png` 2172x724 (pale sea-green and white, gold flecks) | keyed `green-soft` | accepted (owner's painted piece) |
| Whirl f0-f3, leaves | old wind sheet | n/a | stand-in |

No step-4 prompts: this pass predates the art workflow.

## Round log

| Date | Round | What changed | Caught by | Commit |
|---|---|---|---|---|
| 2026-10-04 | pass 1 | 12-frame wind sheet only: a rat-sized whirl rolling like a wheel and three tornados | worker | `5915e34f` |
| 2026-10-04 | pass 2 | painted blade as sweep and per-body cut; `green-soft` key | worker | `92e06cba` |

## Measures

Not recorded (pre-workflow).

## Snapshot

- **Commit:** `92e06cba`.
- **Capture run id:** not recorded.
- **Binding checks / flash guard:** five casts at 1.5x on a three-rat formation (161 GIF frames): largest single-step brightening 7.9% of the stage crop (white hurt flash plus sweep), no 25% flash; casts about a second apart. Cut marks thin at the waist of each body; numbers (-7, -9, -9) sit above them on every frame, treants included.
- **Rubric:** not scored; identity test passes.
- **Still weak:** Open items 1-3.

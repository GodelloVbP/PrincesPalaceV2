# Censer of Embers: state record

Structure: `docs/captures/spell_rework/NOTES_TEMPLATE.md`. Plan `docs/PLAN_SPELL_ANIMATION.md` A1. The approved pilot: other spells compare against it. Pre-workflow; the owner judges, no verdict recorded.

## Status

Polish revision landed in `3ae5344e` (no light pool, additive afterimages, bigger sparks, fixed kick and green); polish items `23ea98e9`; released-from-left swing `3da1ad1e`. Captures 2026-10-05: `tools/gif.ps1 spell censer_of_embers -Vs treant|rat` (1x), `-Vs treant -Seconds 12`, `-Vs treant -Repeat 3 -Speed 1.5`; also `spell gale_scythe -Vs rat,treant,beetle` for the flash change. GIFs untracked (the 12 s one is 77 MB); treant 1x also at `../../showcase/censer_of_embers_pilot_treant.gif`. Frames viewed at 1920x1080 around the target, 60 fps.

## Identity

- **Verb:** swing, then break.
- **Signature beat:** a censer on a long chain, released from the left, swings through the bottom and breaks into embers on the cue; fire catches at the feet and settles into Burn.
- **Must read as:** "the censer breaks into embers", not a flame column.
- **Motion reference:** a pendulum on a 4.5-box-height chain, fastest at the bottom, slowing toward the ends.
- **Exceptions:** the break is the cue frame; the crack is 3 frames of the same drawing, brightened.

## Beat table

Release clock; hit cue **1.0 s** = f60 at 1x, f40 at 1.5x. Cast sound re-cut to 1.0 s (`tools/make_spell_cast_cues.py`, source window 0.63-1.63 s, rise -16.5 to -7.1 dB; loudest 50 ms ends in the last 0.15 s, `MeleeSoundOverrideTests`; gain +5.2 dB for that clip only). Burn badge, impact sound and skill shake 0.25 on the cue frame (badge first appears in the status strip at f60).

| t | Beat | Layer | Art piece | Box / feel |
|---|---|---|---|---|
| 0.00-1.00 | wind-up: censer faded in over 0.2 s held at the left extreme (`rotate.delay`), swings L to R (0.6 s) and R to L (1.0 s), cosine `phase` 90, `damping` -0.4 | `censer` (still, `above`, `rotate`, `fadeIn` 0.2) | `censer_hanging.png` | pivot 4.5 box heights above (1250 px to the lantern); angles -9.5, +11.1, -13.1 deg; stage gloom 0.20 over 0.4 s; `glowCurve.knee` 0.4 |
| 0.00-0.95 | smoke born at the lantern 10/s, hangs where born | `trail` (emitter on `layer:censer`) | art's own wisps | draws the swing's path |
| 0.00-1.00 | three ghost lanterns trail, strongest at the bottom | `echo` (additive, `_DstBlend` 1 in `UISpellGlow`) | n/a | alpha 0.55, full strength only 220 px from the lantern |
| ~0.2 and ~0.7 | sparks: 16 per turn in two layers; from the right rim at the release flung right (f14-f26), from the left rim at the right extreme flung left (f42-f50) | `sparks`, `sparks_back` | particles | 0.7-1.2 of a particle, glow 1.5 |
| 1.00 | **Break.** Censer's own still at the third extreme, exactly where the swinging layer is, white-hot 0.04 s, gone in 0.05 s | `crack` | `censer_hanging.png` break | no scale punch |
| 1.00 | stage kick: ground ring +48 px at f62, home by f75 (+44 at f41 at 1.5x) | `vfx.kick` | n/a | `unkicked` on `censer`, `trail`, `crack`, `burst`, `coals`, `sparks`; figures, ground, fire, smoke, ash kick |
| 1.00-2.20 | shower from the lantern's position at the third extreme: 38 embers (0.35-0.9 of a 64 px particle) at 180-560 px/s, gravity 760, life 0.6-1.2 s; 5 big coals (1.4-2.2) tumbling to 540 deg/s, gravity 900 | `burst`, `coals` | `censer_of_embers_particles` | embers bloom a little f66-f96 |
| 1.45-2.50 | fire loops at the feet (4 frames, 8 fps), catches 0.45 s after the cue, fades in 0.25 s, holds 0.5 s, fade 0.12 s into the Burn coal glow; coals float up from 1.45 s | `fire`, `rise` | `censer_flame_loop.png` | `breathe` on the slow Ember curve |
| ~1.6-2.9 | smoke born 120 px below the body's middle, rises slowly (f96-f152); one S wisp above the crown f182 | `smoke` | particles | faint, brown |
| ~2.0-2.8 | ash: 6 discs over face and chest (f118-f166) | `ash` | `glow_disc` (`ProceduralSpriteBaker.BakeGlowDisc`) | warm dark grey `#7A6E64` |

Hanging rule: a hanging layer (`place: above`, not travelling) keeps its lantern on the visible stage; only the top 0.28 of its box (the chain) rises above the stage top (`SpellFlight.StageTop` 300, `HangOverhang`); never over the enemy plates. Crownfall (travelling) keeps the 240 sky ceiling (`SpellFlightTests`). Engine fields added: `glowCurve` (with `knee`), `echo`, `stoke`, `emitter.burstTimes/burstEach`, `vfx.kick`, `glow`/`tint` on emitter particles (`docs/ART_PIPELINE.md`, `SpellPolishFieldsTests`). Particle pool: 102 thrown, 62 alive at most (of 64); effects renderers 5 of 12.

Swing reference (bronze-pixel centroid per frame, 1920x1080; treant [rat]): held at the left extreme f11-f13 at x 1141 [1095]; bottom f25-f26 at x 1355-1382 [1327-1356]; right extreme f36-f37 at x 1551 [1540]; bottom again f48 at x 1329 [1299]; left extreme at the break f59-f61 at x 1074-1076 [1020]. Travel 410 px L to R, 477 px R to L. dx per frame rises smoothly 0 to +27 at the bottom, falls to 0 at the right extreme, mirrors to -32 at f49, -8 at f59, +2 at f60 (crack), 0 at f61: no jump. The lantern rises about 11 px at the right extreme and 15 at the last (y 340, 329, 325).

## Open items

1. Rat: the lantern hangs about 120 px over its head and the shower falls onto it from there; embers fall 250 px and mostly die before reaching it.
2. Treant: the lantern sits on the crown and covers the right canopy during the swings (stage-room limit AUDIT records for Crownfall). The lantern is small in the GIF (about 65 px); larger covers more of the head.
3. Break is flat-art lantern into painterly particles; embers and coals do not match the cel style. No art of the lantern in pieces (crack is a brightened still).
4. Burn rim at the roots is heavy next to the thin rim elsewhere (`UIBurnGlow`, `BodyLight.BurnWeight`: 16% tint, shimmer along the edge, strongest at roots, half at crown); the rim-flash interior tint is visible on pale bodies.
5. Fire is about the treant's width and a little over the rat's; on the rat it reads as embers at the feet.
6. Afterimages over the treant's canopy add to the green and read as a lime glow (f24, f30, f44, f48): light but green-tinged. On the rat gold ghosts (f20, f26, f34, f50).
7. Spark flecks and ash are small: ash reads as dust specks, not flakes (no grey flake in the particle sheet).
8. Not measured: `spell_feel` WARN/accent numbers; no `-Darkness` capture; no pixel test of the shaders (the rule is pinned in `BodyLight`; shader property defaults are checked against it).
9. Flash guard: treant 1x largest step 49,036 px (2.4%) at f102 (the treant's own hit flash), 21,716 px at the crack (f61); `-Repeat 3 -Speed 1.5` 52,120 px (2.5%) at f68, none at 57,000 px or more.

## Decisions

- 2026-10-05 (owner): art is the owner's own: `censer_hanging.png` and `censer_flame_loop.png`.
- 2026-10-05 (worker): pilot's smoke column and flame-tongue emitter removed; the art carries its own wisps and tongues.
- 2026-10-05 (coordinator): after the watch, light pool removed, afterimages additive, kick no longer moves hanging layers, sparks bigger, green fixed.
- Spell and status flash (`UIRimFlash`, `BodyLight.FlashWeight`): a Burn tick lights the outline pale orange with a light peach interior (2 frames then 0.08 s); Gale Scythe flashes a pale cyan edge; the sheep's Nature staff swing still flashes the whole body flat green.

## Art

| Piece | File | Recipe | Status |
|---|---|---|---|
| Censer and break | `censer_hanging.png` | `Art/Sheets/recipes/censer_hanging.json`: `green-soft` key, no crop, one canvas so the chain top does not move | accepted (owner's) |
| Fire | `censer_flame_loop.png` | `Art/Sheets/recipes/censer_flame_loop.json`: same, coal line fixed | accepted (owner's) |
| Embers, coals | `censer_of_embers_particles` | n/a | stand-in, painterly |

## Round log

| Date | Round | What changed | Caught by | Commit |
|---|---|---|---|---|
| 2026-10-05 | pilot pass 2 | owner's art; released from the left, twice the sweep (193/262 to 410/477 px), no jump at the break (a scale about a pivot 1250 px away had moved the lantern 38 px); hanging-layer height rule; break from the lantern's centre; Burn coal glow; spell and status flash | owner | `3da1ad1e` |
| 2026-10-05 | polish | ten engine-made items: gloom 0.25 to 0.20, coal light breathing (`glowCurve.knee` 0.4: hot pixels 2273 at f12, crest 3892 at f20, trough 2152 at f38), afterimages, sparks, light pool, stage kick, glowing embers, smoke, ash, Burn glow with the fire (mean red 73 to 80 over f88-f101) | worker | `23ea98e9` |
| 2026-10-05 | revision | coordinator's watch: light pool removed (orange stain far below the lantern); afterimages additive; kick no longer moves hanging layers (rat lantern dx at the break -9.1, -5.3, +6.3, -5.3, -7.8, gone f63: no push); sparks 16 per turn; yellow-green from the flame's yellow core while fading (rat f130-f142, treant f138): fire fade 0.5 to 0.12 s, ash warm grey; yellow-green pixels over the feet f120-f200: rat 797 to none visible, treant 216 to 35; fire ends about 0.4 s sooner | coordinator | `3ae5344e` |

## Measures

Not recorded (pre-workflow).

## Snapshot

- **Commit:** `3ae5344e`.
- **Capture run id:** not recorded.
- **Gate:** cue tests green (`hitCueSeconds` unchanged); `SpellFlightTests`, `SpellPolishFieldsTests`, `MeleeSoundOverrideTests`.
- **Binding checks / rubric:** not scored.
- **Still weak:** Open items 1-5.

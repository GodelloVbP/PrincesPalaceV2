# Gilded Aegis: state record

Structure: `docs/captures/spell_rework/NOTES_TEMPLATE.md`. Plan S3, spell 3.8. Pre-workflow pass; owner gates were skipped, the calls below are the worker's.

## Status

Dim shards and mote, gold bloom ring on the cue (`11cefd82`), so the lock is the cast's peak and `spell_feel` is clean at 1x and 1.5x; capture runner fixed for the five-cast series (`0de173ac`); hand-off fix `d3c2c3b8`. Captured 2026-10-04 with `tools/gif.ps1 spell gilded_aegis -Party shawn,odette,bjorn -Target <ally>` (1x; Bjorn at `-Aspect 4:3`) and a series at 1.5x. GIFs untracked beside this file. Not yet through the workflow's steps 3-7.

## Identity

- **Verb:** ward.
- **Signature beat:** a finished gold shield with a crown locks around the ally while their face stays visible; the plate gains the ward.
- **Must read as:** "a golden shield on my ally"; a closed ward.
- **Must NOT read as:** a mirror, portal, or plate hiding the body.

## Beat table

Release clock; cue 0.30 s = f18 at 1x, f12 at 1.5x. Both layers are sized by the struck body (`fit: target`) and centred on the ally on Odette, Bjorn and Shawn.

| t | Beat | Layer | Art piece | Box / feel |
|---|---|---|---|---|
| 0.00-0.20 | white-gold mote flies from Shawn's middle to the ally's middle, shedding gilt leaves and glints | mote | procedural (`tools/make_spell_fx.py aegis_mote`, soft comet tail); leaf/glint particles unchanged | glow 0.8 |
| 0.10-0.30 | four shards close around the ally, last drawing ends on the cue, no fade | shards | old sheet f0-f4 | 25 fps, opacity 0.5, glow 0 |
| 0.30 | **Payoff.** Lock on the cue with sound (`gilded_aegis_impact`), ward bar, ward icon on the ally's plate, log line "Shawn wraps <ally> in the fleece."; leaf burst opens | lock | `gilded_aegis_lock.png` (closed ring of gold plates, crown, halo, centre open) | 440; punch 0.3, glow 1.7 |
| 0.30 | soft gold ring round the lock, clear centre | `flash` | `aegis_bloom` (`tools/make_spell_fx.py`) | 560; glow 3.0, 0.08 s then fade 0.12 |
| 0.30-0.70 | lock held 0.22 s, fades 0.18 s; last leaf clears at 0.4 s | lock | | |

## Open items

1. The painted lock is a different drawing from the shards that close into it, so the hand-off is a swap, not a close.
2. The mote is still a glow and a tail. On a self cast (Shawn) it has nowhere to go and shows as a static bright point at his staff side for 0.2 s, reading as a gather.
3. Lock frame covers the ally's upper body for about 0.1 s; the shield is a ring sized to the body, on the owl over head and chest with the wings outside. Faces visible through the lock on Odette 16:9 and Bjorn 4:3.
4. Bjorn recorded at 4:3 only (the 16:9 crop cuts him at the left edge, per the S0 notes).
5. `-Darkness` not looked at (the plan asks it only of Shackles and Viper).
6. Halo glow came back lime-yellow where painted over the green screen; `cap_green` on the recipe (`docs/ART_PIPELINE.md` 5b) pulls those pixels to gold.

## Decisions

- 2026-10-04 (worker): shards and mote dim (opacity 0.5, glow 0; mote glow 0.8) so the close is a build and the bloom ring is the one big rise on the cue (before: the mote arriving at f13, peak 3.0, outshone the lock at f18). Hold cut from 0.32 s to 0.22 s so payoff to clean stays 0.4 s.

## Art

| Piece | File | Recipe | Status |
|---|---|---|---|
| Lock | `gilded_aegis_lock.png` | `cap_green` on the recipe | accepted (painted) |
| Shards f0-f4 | old sheet | n/a | stand-in |
| Mote, bloom ring | procedural, `tools/make_spell_fx.py` | n/a | stand-in |
| Leaf/glint particles | old sheet | n/a | stand-in |

No step-4 prompts: this pass predates the art workflow.

## Round log

| Date | Round | What changed | Caught by | Commit |
|---|---|---|---|---|
| 2026-10-04 | runtime/tools | the ward was never a real cast in a capture (preview stage refused Ward): a Ward on a chosen ally or Shawn is now real, with ward bar, icon and log line on the cue frame | worker | `e4adbb39` |
| 2026-10-04 | runtime/tools | series with a squad stalled (a squadmate's turn waited for a hand; forced press refused on a kit without the spell): the squadmate casts their own first enemy-aimed spell | worker | n/a |
| 2026-10-04 | handoff | lock opens on the frame after the last shard drawing (cursor-compare bug left one empty frame; `../../README.md`) | worker | `d3c2c3b8` |
| 2026-10-04 | tuning | dim shards and mote, bloom ring | worker (`spell_feel`) | `11cefd82` |
| 2026-10-04 | capture runner | five-cast series died at cast 4: capture pacing treated Odette as armed from her previous turn (a stunned teammate's skipped turn sat between); a press is now per turn (`TurnPress` in `CaptureScenarioRunner`); recorded `gilded_aegis_on_odette_vs_forest_warden_full_x5_1.5x.gif` (1099 frames, `-Party shawn,odette,bjorn -Target odette -Vs forest_warden -Formation full -Repeat 5 -Speed 1.5`) | worker | `0de173ac` |

## Measures

Not recorded (pre-workflow). Timing: cast start to payoff 0.30 s (f18) at 1x, 0.20 s (f12) at 1.5x; payoff to clean 0.38 s at 1x, 0.25 s at 1.5x.

## Snapshot

- **Commit:** `11cefd82`.
- **Capture run id:** not recorded.
- **Binding checks:** `spell_feel` at 1x and 1.5x: no warnings, contact f18 / f12 and visual contact on the same frame; the lock frame is the cast's peak (3.07 over typical 2.02 at 1x; 4.45 over 3.40 at 1.5x). Flash guard, five casts at 1.5x (1099 frames, 18 s): the lock frame is a 1.2% step, five in 18 s (well inside 3 per second); largest steps anywhere 9.3%, 8.8%, 7.3% (frames 62, 487, 809) are the warden's and the squad's attacks.
- **Rubric:** not scored; identity test passes.
- **Still weak:** Open items 1-2.

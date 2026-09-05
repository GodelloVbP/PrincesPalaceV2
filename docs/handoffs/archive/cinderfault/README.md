# Cinderfault — implementation record

The specification this was built against is `HANDOVER.md` beside this file (the
5 September 2026 handover, moved here unchanged so the two can be read against
each other). `GAP_AUDIT.md` is the criterion-by-criterion verdict.

**Nothing in this folder has been run in Unity.** Every claim below is a claim
about source and about assets on disk, verified with Python where the assets
allow it. See `GAP_AUDIT.md` for what that leaves open.

---

## The masters, and why these two

Four distinct sheets were delivered, all 1536x1024 RGBA with authored alpha, all
2x3 grids of 512x512 cells. Two pairs were byte-identical duplicates:

| Delivered as | md5 | Verdict |
|---|---|---|
| `Art/Sheets/Spells/cinderfault_ground/cinderfault_ground_6frame_sheet.png` | `9197de7a…` | **SELECTED** (ground) |
| `Resources/Spells/cinderfault/cinderfault_ground_6frame_sheet_v2.png` | `9197de7a…` | identical bytes to the above — "v2" was a copy, not a revision |
| `Resources/Spells/cinderfault/cinderfault_target_local_6frame_sheet.png` | `09a573ea…` | **SELECTED** (eruption) |
| `Art/Sheets/Spells/cinderfault_plume/cinderfault_plume_6frame_sheet.png` | `7a15a69d…` | rejected |
| `Resources/Spells/cinderfault/cinderfault_target_local_6frame_sheet_v2.png` | `7a15a69d…` | identical bytes to the plume sheet |
| `Resources/Spells/cinderfault/cinderfault_6frame_sheet.png` | `9cdabcfa…` | rejected (ground alternative) |
| `Art/Sheets/Spells/cinderfault_eruption/cinderfault_eruption_plume_v2_6frame_sheet.png` | — | rejected, 2026-09-05 — registration, see below |

So "v2" named a duplicate in one case and a genuinely different drawing in the
other. Neither was approved by being called v2.

### A fifth delivery, rejected on the same registration test: `cinderfault_eruption_plume_v2_6frame_sheet.png`

Delivered untracked, 2026-09-05, alongside the current eruption master
(`cinderfault_eruption_6frame_sheet.png`, still selected). Measured the same
way the original eruption pair was measured above — `ground_row`/bbox-centre
per cell, six cells, 1536x1024, 2x3 grid of 512x512:

| | base row per cell | horizontal centre per cell |
|---|---|---|
| current master (unchanged) | 446, 446, 446, 446, 446, 445 | 256, 256, 256.5, 256, 256, 256 |
| `plume_v2` (rejected) | 459, 464, 465, 448, 446, 444 | 268, 242, 238.5, 304, 269.5, 248 |

Base row spans 21px (459-444) against the decision rule's 4px, and centre
spans 65.5px (304-238.5) against the rule's 6px — both fail by a wide margin,
same shape of problem as the original plume sheet's 66px/43px drift, and
visible by eye at a downscaled preview: cell f3 (the peak) is both far larger
than f0-f2 and leans right of the other five cells' shared centreline, with
its tallest spike nearly touching the cell's own right edge. The drawing
itself is reasonably on-brief — rock spikes with fire glowing through the
cracks and base, earth dominating the silhouette — so the rejection is
registration only, same as the first plume sheet. Master and slicer entry
unchanged; the file is left untracked on disk per instruction.

### Ground: `cinderfault_ground_6frame_sheet.png`

Chosen over `cinderfault_6frame_sheet.png` on two readings of the same pair of
frames:

- **The rupture is ONE connected seam.** The alternative's peak is three
  separate bright pools with dark gaps between them, which would fight the three
  per-target eruptions landing on top of it — six hot points where the brief
  asks for one fault with eruptions on it.
- **It cools further.** Measured mean luminance of the opaque pixels in the last
  drawing: 59 against the alternative's 45 *at a higher coverage* — the
  alternative keeps a visibly molten crack network to the end, this one goes to
  a dark seam with residual glow. "The fault cools away completely."

### Eruption: `cinderfault_target_local_6frame_sheet.png` (the *first* delivery)

Chosen over the plume/`_v2` sheet on registration, which is not a taste
judgement:

| | base row per cell | horizontal centre per cell |
|---|---|---|
| selected | 446, 446, 446, 446, 445, 445 | 256 in every cell |
| plume (rejected) | 472, 475, 472, 432, 433, 432 | 322, 256, 190, 316, 261, 193 |

The rejected sheet's burst walks 66px sideways and jumps 43px vertically
between drawings. Ground-anchored to an enemy's feet it would slide off them
mid-cast, and no authored impact point can correct a contact point that moves.
It is also the weaker drawing — smaller, dustier shards where the brief asks for
earth to dominate the silhouette — but the registration alone decided it.

---

## Preprocessing

**No keying.** Both sheets carry authored alpha (92.6% and 90.4% fully
transparent as delivered), so both slicer entries set `keyed: False`. Luminance
keying would have been actively destructive here rather than merely unnecessary:
the fault is black basalt with molten seams, so brightness and coverage disagree
across the whole width of the drawing and the rock would have vanished, leaving
the lava floating. No green-key preprocessing was needed or applied.

**One correction, on the ground sheet only: `base_align`.** Measured cell by
cell, the lowest substantially-opaque row of the ground sheet runs 441 / 458 /
458 across the top row and 294 / 290 / 285 across the bottom one. The frame the
fault ruptures is therefore also the frame the entire floor jumps 164px — a
third of the cell — up the screen. That is a delivery inconsistency, not
animation: a crack in the floor cannot move the floor. `slice_spell_sheet.py`
gained an opt-in `base_align` pass that slides every cell DOWN onto the lowest
floor any cell reaches, so no shift is ever negative and nothing is pushed off
its own cell. Shifts applied: +17, +1, 0, +165, +167, +174.

The eruption sheet declares no `base_align`, and the entry says so explicitly:
its cells already agree to within a pixel, so the pass would be a no-op. That is
the measurement that makes the ground sheet's need for it legible.

---

## Slicer entries and the composed sequences

`tools/slice_spell_sheet.py`, ids `cinderfault_ground` and
`cinderfault_eruption`. Both compose **nine frames from six drawings**, and both
put their peak at **index 4 — 1-based frame 5**. That shared index is the whole
timing contract: `skills.json` authors `impactFrame: 5` once and leaves
`groundImpactFrame`/`groundSeconds` unset, so the two layers cannot be given
different impact instants without a deliberate edit.

**Ground** — a gathering, a rupture held for two and grown 5% into the second
(frost_flare's own trick: a one-frame peak reads as a dropped frame), then a
cooling tail. Scaled steps pivot on (256, 458), the fault's own ground line, so
it spreads where it stands instead of sliding down the screen as it swells.

**Eruption** — the peak in this sheet is drawing **3**, not 4 (cell f2 carries
17.5% coverage against f3's 14.0%, and reaches 149px up the cell against f3's
186). The handover offered two ways to reconcile that with the ground's drawing
4: start the local anticipation earlier, or trim the local sequence to begin at
its peak. **Started earlier.** The anticipation gets four slots (f0 twice, f1
held twice) so the peak lands on index 4; trimming would have thrown away the
two drawings that make the eruption read as coming *out of* the ground rather
than appearing on top of it. Scaled steps pivot on (256, 446), its own base, so
the shards rise instead of the burst inflating about its middle.

Verified after slicing — every output frame's floor:

    ground     194 194 194 194 194 195 194 194 195   (512x207 canvas, see below)
    eruption   446 446 447 446 446 446 446 446 445

Which is what the authored contact points are read off:

| | authored | measured from |
|---|---|---|
| `vfx.impactY` (eruption) | `0.129` | (512 − 446) / 512 = 0.1289 |
| `vfx.groundImpactY` | `0.063` | (207 − 194) / 207 = 0.0628 |
| `vfx.impactX` (eruption) | `0.5` | centre 256 of 512, constant across all nine frames |

**GROUND CROPPED TO ITS OWN CONTENT, 2026-09-05 — the paragraph this replaces
was wrong.** It read "these frames are square with a wide drawing on them, so
the box is square... and the transparent top and bottom of the frame cost
nothing." They do not: `GroundBoxFor`'s fallback aspect is the sheet's own
CANVAS, and the ground sheet's uncropped 512x512 canvas held its crack in only
the bottom ~35%. Sized off a real three-enemy formation of wide monster art
(`Enemies/rat`, not the narrow placeholder the pre-visual-review capture
used), the span came out ~670 units and forced an equally tall box — half of
it above the crack's own content — so the fault rendered erupting around the
enemies' shoulders instead of at their feet
(`tools/screenshots/runtime/cinderfault/unlabelled/f6.png`, `f10.png`,
`f14.png`; `docs/handoffs/cinderfault/GAP_AUDIT.md` row 24). Fixed by adding
`content_crop` to `tools/slice_spell_sheet.py` and setting it on the
`cinderfault_ground` entry: every one of the nine composed frames is cropped
to the union of what any frame draws (one shared rect, so a growing rupture
does not slide around inside a shrinking canvas), padded 8px. The ground
sheet's canvas is now 512x207 — its own aspect (~2.47) IS the content's
aspect, so `GroundBoxFor`'s existing "0 means the sheet's own frame aspect"
fallback needed no code change, only honest input. `groundImpactY` was
re-measured against the cropped canvas (0.104 -> 0.063) for the same reason.
The eruption sheet is untouched: its own 512x512 canvas and content still
roughly agree (see its own peak/registration numbers above), which is why
only the ground entry declares `content_crop`.

`vfx.groundAspect` is still left unset (0) — the crop is what makes "the
sheet's own frame aspect" a true statement about this art instead of an
assumption that happened to go unnoticed for one release.

**Re-verified on a fresh capture, 2026-09-05 (after `189a679`).** The
unlabelled screenshots this fix was diagnosed against were timestamped 15:12,
before the 15:28 commit, so they could not be trusted to show the result.
`CinderfaultSpellCaptureTests` was extended to write a `stage` block into
`timing.json` — the ground box's own rect and every living slot's centre-x
and ground-y, read in the identical parent-local space `PlaySpellGroundVfx`
and `SpellVfxTests.TheFaultReachesEveryEnemyAndSitsNearTheGroundLine` both
already use — and re-run. Measured: `groundLeft 66.12`, `groundRight 737.17`,
`groundBottom -188.59`; slots at x `300.00`/`432.50`/`565.00`, ground-y
`-218.00`/`-171.50`/`-125.00` (average `-171.50`). The box's bottom sits
`17.09` units from that average — within the pinned test's 40-unit bound and
close to the commit message's own "~17" estimate — and its left/right edges
clear both outermost slot centres with well over 100 units to spare. A direct
pixel read of the same capture (`tools/screenshots/runtime/cinderfault/
after_189a679/`, ember-coloured pixels isolated by hue/brightness in the
enemy-formation band) shows one continuous glowing seam running from under
the front rat's front paws to the back rat's tail, present from the very
first sampled frame of the cast — not the disconnected, shoulder-height crack
`f6.png`/`f10.png`/`f14.png` showed before this commit. **The placement fix
holds; nothing further changed.** The original pre-fix frames could not be
diffed pixel-for-pixel against this capture: `tools/screenshots/` is
gitignored and untracked, and re-running the same fixture into its own
`unlabelled/` output overwrote them before a copy was taken — the before
numbers above are the commit message's and this row's own record, not a
fresh re-measurement of the old art.

**Reproduce:**

    python tools/slice_spell_sheet.py cinderfault_ground cinderfault_eruption

---

## Timing

    seconds        0.78     nine frames at ~87ms
    impactFrame    5        both layers
    rupture at     0.433s   0.78 x 5/9, via CombatBeat.ImpactFraction

0.78 rather than the ~0.52 the strike spells use: the pre-impact window at 0.52
is 0.289s, which is not enough room for a pressure cue to read as pressure. The
cooling tail is the remaining 0.347s. This is the number most likely to want
retuning in a full fight — the handover's own advice — and it is authored in
exactly one place (`skills.json`), with `tools/make_cinderfault_audio.py`
restating it at the top of the file as the contract the two clips are cut to.

None of this timing changed on 2026-09-05 — the ground-fault placement fix
above (a canvas crop and a re-measured `groundImpactY`) touches WHERE the
fault renders, not WHEN. `impactFrame`, `seconds` and the two audio clips are
exactly as this section already described.

---

## Audio — both clips are PLACEHOLDERS

`tools/make_cinderfault_audio.py`, in the idiom of `make_contact_fx.py --audio`:
numpy plus the stdlib `wave` module, 44.1 kHz 16-bit mono, envelopes starting at
zero so neither has a lead-in.

| Clip | Length | Shape | Measured RMS | Gain applied |
|---|---|---|---|---|
| `Audio/Sfx/cinderfault_pressure.wav` | 0.393s | sub sliding 36→66 Hz with grit gated into the second half, rising to a hard stop | −12.6 dB | −5.40 dB |
| `Audio/Sfx/cinderfault_impact.wav` | 0.347s | a 12ms crack, a boom falling 120→34 Hz under it, gravel over the tail | −18.2 dB | −0.15 dB |

Lengths are derived from the spell's timing, not chosen: the pressure clip is
the pre-rupture window less a 40ms gap, so the transient arrives out of silence
rather than on top of a rumble still going; the impact clip fits inside the
cooling tail so it is not still sounding when the next actor moves.

`Resources/Audio/audio_levels.json` was regenerated with
`tools/measure_audio_levels.py` (ffmpeg present). The diff is +16 lines: the two
new entries and nothing else — no existing clip's gain moved.

---

## What remains for visual review

Nothing here has been seen in the game. In rough order of how likely each is to
be wrong:

1. **`vfx.seconds: 0.78`** — set to buy the pressure cue room, not to look
   right. Tune in a full fight.
2. **`vfx.size`** — left unauthored, so the eruption is fitted into the same 380
   box every other spell uses. The eruption drawing fills roughly 55% of its
   frame's width, so it renders about 210 units wide. Against a Giant Rat that
   is probably right and against a boss it is probably small.
3. **The fault's width against a one-enemy formation.** It is measured off the
   slots' own edges, so a lone rat gets a fault as wide as the rat. That is the
   honest reading of "sized to the formation" and it may read as too small.
4. **The average ground line across ranks.** One flat drawing under figures
   standing at different depths is wrong by the same small amount at both ends;
   whether that amount is visible is a screenshot question.
5. **Both audio clips**, which are synthesised placeholders and are meant to be
   replaced by recordings.

`tools/slice_spell_sheet.py cinderfault_ground --preview` writes a GIF paced by
the content's own `seconds`, which is the cheapest way to look at the timing
without a scene.

---

## Brought into MAIN and seen in the game, 2026-09-05

The worktree's diff applied to `static-pilot` cleanly (`git apply --3way`, no
conflicts, even in `docs/CODE_MAP.md` and `ScreenRegistry.cs` — the only
intervening commit, `245761f`'s shop rebuild, never touched a file this work
touches). Everything this document's own "What remains for visual review"
section flagged as unseen has now been seen, or measured, or both. See
`GAP_AUDIT.md` for the row-by-row verdicts; this section is the two questions
that document left open.

### The impact-SFX timing deviation, closed by measurement

`GAP_AUDIT.md`'s own "Deviation from the brief" section asked for a measurement
of `frost_flare.mp3` and `lightning_strike.mp3` before deciding whether
`sfxPath` moving to the impact instant should apply everywhere or only where a
`castSfxPath` is absent. Both clips' loudest 50ms window sits in the first
quarter of the clip (frost_flare 22.8%, lightning_strike 12.5%, by window
start) — a fast transient with a long ring-out tail, not a swell. The rule the
brief set therefore resolves cleanly: **the move stands, unconditionally, for
every spell.** No branch on `castSfxPath` was added to `FightBeatPlayer`. Full
numbers and method in `GAP_AUDIT.md`.

### The full gate

    tools/test.ps1 Cinderfault,SpellVfx,FightScreen,FightBeatPacing,SpellCast,FightPlayable
    tools/run_tests_parallel.ps1 -BuildContent -BuildScenes

    EditMode -- Total: 2709  Passed: 2709  Failed: 0  Skipped: 0
    PlayMode -- Total: 759   Passed: 737   Failed: 0  Skipped: 22
    All tests passed.

(The final number, run again after `CinderfaultSpellCaptureTests` landed:
EditMode 2709/2709, PlayMode 759 total / 737 passed / 0 failed / 22 skipped —
one more skip than the first run, which is that capture's own
`CaptureCinderfaultAsAFrameSeries` self-skipping under the commit gate's
`-nographics`, same as every other pixel-reading test in this suite.)

`Resources/Content/Skills/cinderfault.asset` now exists (content rebuild), and
`Fight.unity` carries the pooled ground-layer node (scene rebuild). The four
deletions this document called for are done: `Resources/Spells/cinderfault/`
(all four sheets and its `.meta`) and `Art/Sheets/Spells/cinderfault_plume/`
(the rejected eruption sheet and its `.meta`).

### The cast, photographed

A new fixture, `CinderfaultSpellCaptureTests`
(`Assets/_Project/Scripts/Tests/PlayMode/CinderfaultSpellCaptureTests.cs`),
casts Cinderfault through the real menu — SKILL, its one row, any enemy plate —
with a party member carrying nothing else on their kit, against a full
three-enemy formation, and records a 42-frame series at 1/30s intervals (real
time, `BeatSpeedMultiplier` 1) from click to a fully cooled stage. Run with:

    powershell tools/graphics_tests.ps1 -Filter CinderfaultSpellCaptureTests
    python tools/capture_strip.py --root tools/screenshots/runtime/cinderfault

Strip: `tools/screenshots/runtime/cinderfault/unlabelled/strip.png` (frames also
under that folder as `f0.png`.."f41.png", plus `timing.json` and
`playback.gif`).

**What the strip shows.** At the peak frame (f10, 333ms after the click) the
three enemies' fire-and-basalt eruptions read as one continuous jagged
formation — the individual spike clusters overlap edge to edge rather than
sitting as three separate craters — with a shared red ground-glow visible
underneath them from several frames earlier (f7) than the eruption itself.
The three damage numbers (-22/-20/-19 against neutral resistance) appear on
that same frame, directly above each eruption, matching `GAP_AUDIT.md` #31's
claim that the numbers, the eruptions and the fault's own peak coincide. By
f21 (700ms, 367ms after impact) both layers are completely gone — no residual
glow, no lingering pool, three rats standing on plain ground — which is
`GAP_AUDIT.md` #18's "no final pool or sustained flame" seen in motion rather
than read off a coverage percentage.

One thing this capture does NOT settle: item 3 in "What remains for visual
review" above (the fault's width against a ONE-enemy formation) still needs
its own screenshot — this capture only exercises three.

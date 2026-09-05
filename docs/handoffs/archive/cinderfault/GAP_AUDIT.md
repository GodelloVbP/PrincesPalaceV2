# Cinderfault — gap audit

One row per acceptance criterion in `HANDOVER.md`. Verdicts:

- **built** — implemented and covered by a test that would fail if it regressed.
- **built-untested** — implemented, no test asserts it.
- **open** — not done, or done in a way that does not meet the criterion.

**One open still applies to every row below and is not repeated in each one:**

> **OPEN-B — the Intelligence/Wisdom weights are undefined.** See the last
> section.

**OPEN-A is closed, 2026-09-05.** The worktree's diff was applied to MAIN
(`static-pilot`, on top of `245761f`), the four sheets under
`Resources/Spells/cinderfault/` and `Art/Sheets/Spells/cinderfault_plume/`
were deleted as this document instructed, and the full gate ran twice:

    powershell tools/test.ps1 Cinderfault,SpellVfx,FightScreen,FightBeatPacing,SpellCast,FightPlayable
    powershell tools/run_tests_parallel.ps1 -BuildContent -BuildScenes

    EditMode -- Total: 2709  Passed: 2709  Failed: 0  Skipped: 0
    PlayMode -- Total: 757   Passed: 736   Failed: 0  Skipped: 21
    All tests passed.

Re-run once more after `CinderfaultSpellCaptureTests` (the capture fixture,
see the README's own capture section) landed:

    EditMode -- Total: 2709  Passed: 2709  Failed: 0  Skipped: 0
    PlayMode -- Total: 759   Passed: 737   Failed: 0  Skipped: 22
    All tests passed.

Every "built" verdict below has now compiled, run, and passed. The rows still
marked "built-untested" are untested for the reason each one states, not
because the suite never ran.

---

## Content and availability

| # | Criterion | Verdict | Where |
|---|---|---|---|
| 1 | id `cinderfault`, name Cinderfault | built | `Assets/_Project/ContentData/skills.json:123` |
| 2 | `bookOnly: true`, `bookTier: 2`, never innate | built | `skills.json:124` (`bookOnly`) and `skills.json:152` (`bookTier`); `CinderfaultSpellTests.TheEntryResolvesAsAGlobalBookSpellAimedAtEveryEnemy` pins `UnlockLevel == int.MaxValue`, which is what keeps it off the level ladder |
| 3 | Any character can learn/equip it | **built-untested** | Authored `"characterId": "sheep"`, following Frost Flare, Lightning Bolt, Mud Burst and Golden Fleece — every existing `bookOnly` entry. **Assumption stated:** the owner field on a book spell is the existing convention rather than an exclusivity gate, per the brief's "follow the existing owner-field convention without making it character-exclusive". Nothing in this change tests a second character equipping it, because no second character has a kit today. If `characterId` does gate the spellbook, every shipped book spell has the same problem and it is not Cinderfault's to fix. |
| 4 | Effect `DamageAll`, every living opponent | built | `skills.json:128`; `CinderfaultSpellTests.MixedWeaknessAndResistanceResolvePerEnemy` (three), `.ALoneEnemyIsHitExactlyOnce` (one) |
| 5 | Content rebuilds | built | `ContentBuilder` is untouched and copies the presentation whole via `SpellPresentation.Copy()`. `-BuildContent` has now run: `Assets/_Project/Resources/Content/Skills/cinderfault.asset` exists and is committed. |

## Damage

| # | Criterion | Verdict | Where |
|---|---|---|---|
| 6 | Two independent typed packets, 10 Fire + 10 Nature | built | `skills.json:131-140`; `CinderfaultSpellTests.TheContentEntryAuthorsTenFireAndTenNature` |
| 7 | Each packet gets its own weakness/resistance check | built | `FightSession.Skills.cs:466` — a new `HasFixedDamage` branch in `ResolveDamageAll` routing to `ResolveDamageInstances`, which checks per packet. `CinderfaultSpellTests.MixedWeaknessAndResistanceResolvePerEnemy` pins 20 / 25 / 10 across three matchups from one cast. **This branch did not exist**: a `DamageAll` skill with `damageInstances` previously fell through to the Attack-scaled formula, read Power and FlatAmount (both zero on a packet skill), and would have dealt the floor of 1 per enemy with no elemental check anywhere in it. |
| 8 | No extra `CombatMath.DamageScale` x10 | built | `CinderfaultSpellTests.TenFireAndTenNatureLandAsTwentyOnANeutralEnemy` — 20, pinned as a literal |
| 9 | Per-enemy displayed numbers are each correct | built | New `CombatBeat.Results` / `BeatTargetResult`, recorded in `FightSession.Skills.cs` and read by `FightBeatPlayer.ShowAmount` (`Core/FightBeatPlayer.cs`). `CinderfaultSpellTests.TheBeatCarriesEachEnemysOwnNumber` pins {20, 25, 10} on one beat. The beat's own `Amount` still holds the largest single hit, so every existing consumer (hit-stop weight, stage shake, `WantsContactFx`) is unchanged. The popup path itself is now covered too: `CinderfaultSpellCaptureTests.TheCastLandsOnAllThreeInOneInstantAndTheFaultCoolsAway` asserts exactly one rise in the popup pool's busy count for a three-enemy cast, and the capture strip (`tools/screenshots/runtime/cinderfault/unlabelled/`) shows three distinct figures (-22/-20/-19 against neutral resistance) over three slots on the same frame. |
| 10 | Dead enemies and empty slots are not targeted | built | `CinderfaultSpellTests.AnAlreadyDeadEnemyIsNotStruckAgain`, `.ALoneEnemyIsHitExactlyOnce`; PlayMode `ALoneEnemyGetsOneEruptionAndStillOneFault` for the art side |
| 11 | Hit reactions per enemy | **built-untested** | `FightBeatPlayer.Recoil` and `FightController.FlashCombatant` both walk `Results` when there is one, so an enemy that dodged does not flinch from a cast the two beside it took. Nothing asserts it. |

## Cost and cooldown

| # | Criterion | Verdict | Where |
|---|---|---|---|
| 12 | 15 mana, charged once per cast | built | `CinderfaultSpellTests.ManaIsChargedOncePerCastRatherThanOncePerEnemy` |
| 13 | No signature-resource cost | built | `.TheEntryResolvesAsAGlobalBookSpellAimedAtEveryEnemy` asserts `ResourceCost == 0` |
| 14 | Insufficient mana refuses | built | `.InsufficientManaRefusesTheCastAndSpendsNothing` |
| 15 | Cooldown 3 blocks own turns 2 and 3 | built | `.ACooldownOfThreeBlocksExactlyTurnsTwoAndThree` |
| 16 | Other actors' turns do not shorten it | built | `.EnemyTurnsBetweenTheCastersDoNotShortenTheWait` |

## Aftereffects

| # | Criterion | Verdict | Where |
|---|---|---|---|
| 17 | No burn, stun, status, DoT or terrain | built | `.TheContentEntryAuthorsTheGameplayContract` asserts the *absence* of `appliesStatus`/`statusDuration`, so one added later fails rather than shipping unremarked |
| 18 | No final pool or sustained flame in the art | built | The ground sequence's last frame is the cooled seam, coverage 4% against the rupture's 14%; the eruption's last frame is 3% against 19%. Now seen in motion: the capture strip shows both layers gone entirely by frame 21 (700ms after cast, ~367ms after impact) — no residual glow, no lingering pool, three rats standing on plain ground. |

## Presentation

| # | Criterion | Verdict | Where |
|---|---|---|---|
| 19 | Backward-compatible optional shared-ground layer on `SpellPresentation` | built | `Domain/Content/SpellPresentation.cs` — six new fields, all inert when `groundPath` is empty; `HasGroundLayer` is the single gate. `CinderfaultSpellTests.AnOldSpellWithNoGroundLayerIsUnchanged` and `.ThePresentationCopiesEveryNewFieldAcrossABoundary`; PlayMode `AnOrdinarySpellDrawsNoGroundLayerAtAll` |
| 20 | Copying, validation and serialization updated | built | `Copy()` covers all six (so `ContentBuilder` needed no change); `ArtPathConvention.Kinds` classifies `vfx.groundPath`/`vfx.castSfxPath` and BOTH `SkillEntryResolver` and `EnemyEntryResolver` call `Check` on them — `ArtPathConventionTests`'s reflection sweep would have failed the suite otherwise |
| 21 | No spell-ID branches in the combat controller | built | `grep -rn cinderfault Assets/_Project/Scripts` returns only test files. The controller asks `HasGroundLayer`. |
| 22 | ONE pooled ground renderer, behind the combatants, separate from the eruption pool | built | `FightScreen.BuildSpellGroundVfx` (a pool of one, added to `hud` *before* both stages so uGUI draws it behind them); wired in `ScreenRegistry.cs`. PlayMode `AFullFormationGetsThreeEruptionsAndExactlyOneFault` |
| 23 | Sized/positioned from the actually-occupied slots, in the renderer parent's coordinates | built | `FightController.SpellVfx.cs` `PlaySpellGroundVfx` — each target slot's `xMin`/`xMax` transformed through the shared parent, so a depth-scaled back-row slot contributes its real width. PlayMode `TheFaultIsWiderUnderThreeEnemiesThanUnderOne` and `TheFaultReachesEveryEnemyAndSitsNearTheGroundLine` |
| 24 | Preserve the authored aspect (a wide fault, not a square) | **the caveat below was wrong; fixed 2026-09-05** | Row 24 previously read this square box as harmless because "on this art the authored aspect happens to be 1:1." It was not harmless: `GroundBoxFor`'s fallback aspect is the sheet's own CANVAS (512x512, square), not its CONTENT (a wide, short crack occupying only the canvas's bottom ~35%). Against a real three-enemy formation of wide monster art (slots sized to `Enemies/rat`, not the narrow placeholder the earlier capture used) the span came out ~670 units, forcing an equally tall box — half of it above the crack's own content — which put the visible fault erupting around the enemies' shoulders instead of at their feet (`tools/screenshots/runtime/cinderfault/unlabelled/f6.png`, `f10.png`, `f14.png`, captured against the real click flow rather than `PlaySpellVfxForTest`). Fixed by cropping the ground sheet to its own content (`content_crop` in `tools/slice_spell_sheet.py`, applied to the `cinderfault_ground` entry) rather than by changing `GroundBoxFor`: the sheet's canvas is now 512x207, so its own aspect (~2.47) IS the content's aspect, and the existing fallback needed no change. `groundImpactY` re-measured against the cropped canvas and re-authored (0.104 -> 0.063). PlayMode `TheFaultReachesEveryEnemyAndSitsNearTheGroundLine` pins the box reaching every slot and landing within 40 units of the formation's own ground line. **Re-verified on a fresh capture, 2026-09-05 (after `189a679`).** `CinderfaultSpellCaptureTests` now writes the ground box's own rect and every living slot's centre-x/ground-y (`SlotForTest`, same parent-local space `PlaySpellGroundVfx` itself measures in) into `timing.json`'s new `stage` field, so the claim above is checked against the actual capture rather than trusted on the assert's word alone. Measured at cast (`tools/screenshots/runtime/cinderfault/after_189a679/timing.json`): `groundLeft 66.12` / `groundRight 737.17` / `groundBottom -188.59`; slots at x `300.00` / `432.50` / `565.00`, ground-y `-218.00` / `-171.50` / `-125.00` (average `-171.50`). Bottom edge is `17.09` units from the formation's average ground line (was 70+ before this commit; tolerance is 40) and the box's left/right edges (66.12, 737.17) both clear the outermost slot *centres* (300, 565) with over 170 units to spare on each side — the box is in fact sized to the slots' own edges (full sprite width), not merely to their centres, so this margin is expected. The rendered strip (`tools/screenshots/runtime/cinderfault/after_189a679/strip.png`) shows one continuous ember glow under all three rats' feet from the front rat's paws to the back rat's tail, present from the cast's very first sampled frame — no disconnected shoulder-height crack. **Placement confirmed correct; no further change made.** |
| 25 | Eruptions ground-anchored through slot/AimPoint and authored `impactX`/`impactY`, on the pre-damage snapshot | built | Unchanged existing path; the snapshot is `ResolveDamageAll`'s existing `struck` list, and `StruckBy` is now the single walk both layers use |
| 26 | All eruption instances share one timeline; no per-instance timing variation | built | Every instance is played with the same `beat.Vfx.seconds`; no scale or mirror variation was added |
| 27 | One authoritative impact time from `CombatBeat.ImpactFraction` | built | Both sequences are nine frames; content authors `impactFrame: 5` once and leaves `groundImpactFrame`/`groundSeconds` unset. `CinderfaultSpellTests.TheGroundLayerInheritsThePerTargetTiming` and `.AnUnsetGroundTimingFallsBackToThePerTargetSequence`; PlayMode `TheGroundAndTheEruptionRuptureTogether` measures the sliced sequences themselves |
| 28 | Anticipation never starts at impact | built | The eruption recipe gives its anticipation four slots so the peak lands on index 4; see README |
| 29 | Pressure audio starts with the cast, ends before impact | built | `castSfxPath` fires beside `PlayVfx` at beat open (`FightBeatPlayer.cs`); the clip is cut to the pre-rupture window less 40ms |
| 30 | ONE impact sound at rupture | built, **and this changed existing behaviour — see below** | `sfxPath` moved from beat-open to the impact instant |
| 31 | Numbers, reactions, stage impulse and sound coincide | built | All five now happen in the same `try` block at the impact instant. `CinderfaultSpellCaptureTests` confirms the visible half: the three numbers, the three eruptions and the ground fault's own peak all land on the same sampled frame (f10, 333ms). Sound is not asserted by any automated test — the audio measurement below is what decided its timing, not a pixel or a waveform captured mid-fight. |
| 32 | Both pools cleared on completion, interruption, reset and scene exit | built | `FightController.StopSpellVfx` stops the ground player too, and that is what `FightBeatPlayer.Flush` and `OnDisable` call. PlayMode `FlushReleasesTheGroundLayerAndEveryEruption` |
| 33 | Missing art/audio never blocks resolution | built | `SpellVfxPlayer.PlayAt` returns on no frames; `SoundController.PlayClip("")` is a no-op; `PlaySpellGroundVfx` returns on a null player, no ground path, no parent or no slotted target. Existing `AMissingSheetIsSilentRatherThanThrowing` covers the frames case. |

## Assets

| # | Criterion | Verdict | Where |
|---|---|---|---|
| 34 | Reproducible slicer entries, measured grid and contact point | built | `tools/slice_spell_sheet.py` ids `cinderfault_ground`/`cinderfault_eruption`; every number in the README is a measurement, printed by the tool or by a Pillow pass over its output |
| 35 | Authored alpha kept, black basalt not keyed away | built | Both entries set `keyed: False`; no green-key preprocessing was needed |
| 36 | Whole-sheet PNGs no longer runtime resources | built | The four sheets under `Assets/_Project/Resources/Spells/cinderfault/` (and its `.meta`) were deleted from MAIN, along with the rejected `Art/Sheets/Spells/cinderfault_plume/` sheet and its `.meta`. Only the sliced nine-frame sequences under `Resources/Spells/cinderfault_{ground,eruption}/` remain as runtime resources; the two selected masters live under `Art/Sheets/Spells/cinderfault_{ground,eruption}/`. |
| 37 | Frames import as Sprites | built | `Editor/StanceSpriteImporter.cs:37` already covers `/Resources/Spells/`; both output folders sit under it |

---

## OPEN-B — Intelligence and Wisdom weights

**Status: open, deliberately, and no number was invented.**

What is built: `"scalingAxis": "Spell"`, so both packets ride
`SpellScalingMultiplierFor(actor)` — the caster's own `SkillScaling` grades
against their actual ability scores, the same multiplier Frost Flare and
Lightning Bolt already take. Cinderfault therefore scales exactly like every
other packet spell in the game today, and a caster who invests in the spell axis
gets more out of it.

What is not built: the brief's "strong Intelligence, slight Wisdom" is a
statement about *relative weights between two attributes*, and
`ScalingAxis.Spell` cannot express it. The axis picks which SET of grades
applies; the grades themselves live on the CASTER (`CombatantState.SkillScaling`,
built from the character's kit), not on the spell. Every spell a given character
casts therefore scales identically for that character. There is no per-spell
INT/WIS weighting in this codebase to set.

**What I would propose, for the author to accept or reject** — a new optional
block on `RawSkillEntry`, in the shape `damageInstances` already established:

```json
"scaling": { "intelligence": "A", "wisdom": "C" }
```

- Letter GRADES rather than floats, matching whatever `ScalingSet` already uses
  for characters, so a designer picks from the same vocabulary in both places
  and the two cannot disagree about what "strong" means.
- Resolved into a `ScalingSet` on `ResolvedSkill` and, where present, used by
  `SpellScalingMultiplierFor` **instead of** the caster's own — the spell
  overriding the caster, not multiplying with them, because two multipliers
  stacking is the "does power scale on top of the fixed amount, or replace it"
  ambiguity the resolver already refuses for `damageInstances`.
- Absent means today's behaviour exactly, so no existing spell moves.

That is a design decision with balance consequences across every spell, not a
Cinderfault detail, and the brief explicitly declined to specify the numbers.
**Do not close this row by picking weights that look reasonable.**

---

## Deviation from the brief, stated plainly

**The impact SFX now plays at the impact instant for every spell, not just
Cinderfault.** `FightBeatPlayer` fired `beat.Vfx.sfxPath` beside `PlayVfx` at
the top of the beat, which put a spell's sound a whole impact delay ahead of the
blow it describes — about half a second early for Frost Flare, and the wrong
half second, because the number, the flash, the recoil and the stage kick all
happen at the impact instant. Cinderfault's "one impact sound at rupture" is not
satisfiable while that line sits where it was, and moving it only for a spell
that authors a cast cue would be a spell-shaped branch in shared playback.

So it moved for everybody. The comment that defended the old position gave one
reason — a spell with a sound but no frames should still be audible — and that
still holds: `ImpactDelayFor` returns 0 for a beat with no frames, so such a
cast reaches the new line on the same frame it used to.

**This is an audible change to Frost Flare, Lightning Bolt, Mud Burst and every
enemy ability with an `sfxPath`.** It is a fix by my reading and a regression by
the "existing single-layer spells retain timing" criterion, and it is the one
change in this batch most worth a second opinion. Reverting it is one line, at
the cost of Cinderfault's impact cue landing 0.43s before the rupture.

**Resolved by measurement, 2026-09-05: the move stands, for every spell.**

The brief that carried this deviation forward set a rule rather than asking for
a guess: measure `frost_flare.mp3` and `lightning_strike.mp3`'s loudest 50ms
window as a fraction of each clip's length; if BOTH sit in the first 25%, the
move is correct everywhere and stays; otherwise `sfxPath` needed to branch on
whether the presentation carries a `castSfxPath`.

    frost_flare.mp3       length 1.097s   loudest 50ms window starts at 0.250s (22.8%), centers at 25.1%
    lightning_strike.mp3  length 1.097s   loudest 50ms window starts at 0.137s (12.5%), centers at 14.8%

Both clips share the same shape: nine-tenths of a second of near-silence, a
sharp transient around 100-140ms, and a long decaying ring tail out past 700ms
(envelope sampled in 20 buckets, `python tools/measure_audio_levels.py`'s own
ffmpeg-decode approach, reused rather than reimplemented). That is an
impact-with-ring-out clip, not a cast-swell-then-strike clip — there is no
audio there BEFORE the transient for a cast cue to be cutting off early. Both
peaks land in the first 25% by the window-start reading (22.8% and 12.5%); the
window-centre reading puts frost_flare almost exactly on the boundary (25.1%)
and lightning_strike well inside it (14.8%). Read either way, neither clip is
"mostly a swell" — the rule's premise (a swell losing its build-up) does not
apply to either shipped clip.

**No code change follows.** `sfxPath` stays at the impact instant, unconditionally,
for every spell and every enemy ability. The conditional branch the brief
offered as a fallback (`sfxPath` at beat-open when `castSfxPath` is absent) was
not built, because the measurement did not call for it — building it anyway
would be exactly "picking weights that look reasonable" for a question the
data already answered. If a future clip is authored as a genuine swell-then-
strike (loudest window past 25%), that clip is the one to re-open this row for,
not the shared code.

# Cinderfault implementation handover

Prepared 5 September 2026. This is an implementation specification, not a claim that the spell is shipped. Paths below are repository-relative.

## Outcome

Implement a tier-2 global spell: one connected tectonic fault opens beneath the opposing formation, then compact basalt-and-lava eruptions strike every living enemy simultaneously. Earth dominates the silhouette; fire is revealed inside it. The fault cools away completely.

Design authority: `docs/SPELL_DESIGN_STANDARD.md` and `docs/CINDERFAULT_ASSET_PROMPT_WORKFLOW.md`. The latter supplies the art direction and intended mechanics; its reference to a “current implementation” is not evidence that a Cinderfault content entry exists in this checkout.

## Gameplay contract

| Property | Requirement |
|---|---|
| ID / name | `cinderfault` / Cinderfault |
| Availability | Global learnable spellbook, `bookOnly: true`, `bookTier: 2`; usable by any character |
| Effect | `DamageAll`; automatically selects living opponents, up to the current three-slot formation |
| Base damage | Two independent packets per enemy: 10 Fire and 10 Nature |
| Cost | 15 mana once per cast; no signature-resource cost |
| Cooldown | `cooldownTurns: 3`: cast on own turn 1, unavailable on own turns 2 and 3, available on own turn 4 |
| Scaling intent | Strong Intelligence contribution, slight Wisdom contribution; see scaling gap below |
| Aftereffects | No burn, stun, status, damage-over-time, or persistent terrain |

Use existing typed-damage resolution so each packet receives its own elemental checks. Packet amounts are already on the final health scale: do not multiply the authored 10s by `CombatMath.DamageScale`. Actual damage can vary with existing caster modifiers and target defenses/resistances.

## Verified starting point

- `Assets/_Project/ContentData/skills.json` has no Cinderfault entry. Add the global spell here, following existing `bookOnly` entries such as Frost Flare. `spells.json` is the shared spell power curve, not this spell catalogue.
- `FightSession.Skills.cs` already resolves `DamageAll`, captures primary/splash targets before damage, and records one cast beat. Preserve that snapshot so enemies killed by the cast still receive their impact visuals.
- `FightController.SpellVfx.cs` currently duplicates one `vfx.path` across primary and splash targets. It does not implement the required single shared formation-wide ground layer.
- `SpellPresentation.cs` currently describes one animation, a square size, one impact frame, authored contact coordinates, and one impact SFX path. It has no layered timeline or separate pressure cue.
- Typed-packet scaling currently uses the caster's `SkillScaling` through `SpellScalingMultiplierFor`. Merely setting `scalingAxis: Spell` does not establish Cinderfault-specific Intelligence/Wisdom weights. Define supported coefficients/grades and their content representation before claiming this requirement is complete; the brief does not specify numeric weights.
- The `DamageAll` implementation records the largest individual hit in the beat's aggregate amount. Audit the result-display path before reusing that amount for every enemy: different resistances must produce their own correct numbers.
- Source sheets exist, but no Cinderfault entry was found in the slicer's `VFX` manifest. The Resources folder contains whole sheets rather than the required named runtime frames.

## Asset inventory and preparation

Existing candidates:

- `Assets/_Project/Art/Sheets/Spells/cinderfault_ground/cinderfault_ground_6frame_sheet.png`
- `Assets/_Project/Art/Sheets/Spells/cinderfault_plume/cinderfault_plume_6frame_sheet.png`
- `Assets/_Project/Resources/Spells/cinderfault/cinderfault_ground_6frame_sheet_v2.png`
- `Assets/_Project/Resources/Spells/cinderfault/cinderfault_target_local_6frame_sheet_v2.png`
- The same Resources folder also contains earlier combined/target-local sheets.

These are candidates, not visually approved masters. Compare the ground and eruption variants at combat scale before selecting them; do not assume “v2” means approved. Preserve originals and record the selected source explicitly.

Recommended runtime destinations are `Spells/cinderfault_ground` and `Spells/cinderfault_eruption`, each containing `f0..fN.png`. Add reproducible entries to `tools/slice_spell_sheet.py`, measuring the actual grid and contact point. Keep authored alpha; if green-key preprocessing is necessary, record it. Do not luminance-key away black basalt. Import runtime PNGs as Sprites and retain Unity `.meta` files.

Produce one pressure/release cue and one shared impact cue using the asset workflow. Suggested Resources paths are `Audio/Sfx/cinderfault_pressure` and `Audio/Sfx/cinderfault_impact`; these are proposed destinations, not verified existing clips. Normalize through the existing audio workflow. Icon and caster accent are optional and should only be commissioned when their UI/readability need is established.

## Runtime implementation

1. Add the spell content and confirm spellbook acquisition, assignment, save/load, mana validation, and cooldown behavior through the existing global-spell path. Follow the existing owner-field convention without making the spell character-exclusive. Rebuild via **Prince's Palace > Build Default Content**.
2. Extend `SpellPresentation` with a backward-compatible optional shared-ground layer and separate cast cue. Keep new presentation data inside that value; update copying, validation, and serialization so existing spells retain their behavior. Avoid spell-ID branches scattered through the combat controller.
3. Add one pooled ground renderer behind the combatants, separate from the per-target eruption pool. Size and position it using actual stage slots in the renderer parent's coordinate system; preserve the authored aspect ratio instead of forcing a wide fault into a square. Handle partial formations without eruptions in empty or dead slots.
4. Ground-anchor each eruption through the existing slot/AimPoint placement logic and authored `impactX`/`impactY`. Use the pre-damage target snapshot. All target instances share the same timeline; secondary scale/mirroring variation must not change contact timing.
5. Coordinate through `FightBeatPlayer` and the existing impact event. Show damage results, hit reactions, stage impulse, and play one impact sound at the rupture. Keep domain damage resolution separate from delayed visual playback; never apply damage again from an animation callback.
6. Preserve correct per-target results. If the existing beat/view cannot express differing damage amounts, add a per-target result representation and carry it to the number/reaction display instead of duplicating the largest hit.
7. Clear both ground and eruption players on completion, interruption, fight reset, and scene exit. Missing art/audio must not prevent combat resolution.

Primary code touchpoints: `Assets/_Project/Scripts/Domain/Content/SpellPresentation.cs`, `Assets/_Project/Scripts/Domain/Combat/Session/FightSession.Skills.cs`, `Assets/_Project/Scripts/Domain/Combat/Session/FightSession.Beats.cs`, `Assets/_Project/Scripts/Core/FightController.SpellVfx.cs`, `Assets/_Project/Scripts/Core/FightBeatPlayer.cs`, and `Assets/_Project/Scripts/Core/SpellVfxPlayer.cs`. Trace content building and stage pool construction from these rather than editing generated assets as the source of truth.

## Timing contract

Use one authoritative impact time. Ground drawing 4 is the proposed rupture; local drawing 3 or 4 is the proposed eruption peak. Choose the actual contact drawing after preview and convert it to the final sliced playback frame index, which may differ if frames are repeated.

The art brief asks for eruptions to begin at shared impact, but their proposed first two drawings are anticipation. Resolve this by either starting local anticipation earlier so its peak coincides with ground rupture, or trimming the runtime local sequence to begin at peak. Do not start anticipation at impact and let visible contact arrive after damage.

Derive scheduling from the existing `CombatBeat.ImpactFraction` convention. Pressure audio starts with the cast and ends before impact; the impact transient plays once at contact. Tune total duration in a full fight rather than treating six source drawings as a fixed duration. Preserve a short cooling tail without delaying the next action unnecessarily.

## Acceptance and verification

- Content resolves and rebuilds; any character can learn/equip the book without gaining it as an innate skill.
- One-, two-, and three-enemy casts hit every living opponent exactly once, including visible lethal hits. Already-dead enemies and empty slots receive no eruption.
- Mixed Fire/Nature weaknesses and resistances resolve independently, with correct per-enemy HP changes and displayed numbers.
- Mana is charged once; insufficient mana prevents the cast; cooldown blocks exactly two subsequent own turns. Other actors' turns do not shorten it.
- Scaling tests establish the chosen Intelligence/Wisdom behavior, including a non-caster character; no accidental extra x10 multiplier.
- One connected ground fault appears behind actors; all occupied targets reach rupture together. Tall/wide enemies and partial formations remain readable.
- Damage display, reactions, visual peak, and one impact sound coincide. Existing single-layer spells retain placement and timing.
- No final pool, sustained flame, or repeated tick suggests a nonexistent status. Repeated casts do not leak effects or stack audio unexpectedly.
- Exit/reset mid-cast cleans up; missing assets degrade gracefully. Check opposite-side casting if exposed, plus reduced-shake/brightness settings where supported.

Use focused EditMode coverage for content, packets, scaling, and cooldown, and extend `Assets/_Project/Scripts/Tests/PlayMode/SpellVfxTests.cs` for placement, shared-layer count, synchronization, and cleanup. Capture representative in-game casts for visual review. Record selected masters, preprocessing, manifest entries, final frame timing, audio levels, and test/capture results in this folder. Add `GAP_AUDIT.md` when implementation is ready for comparison against this handover.

This handover was prepared from repository inspection. No gameplay changes, Unity tests, asset slicing, or visual asset approval were performed as part of writing it.

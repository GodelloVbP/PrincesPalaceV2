# Status-effect UI

Every combatant's active statuses, readable at a glance, with per-icon hover
detail. [STATUS_ICON_PROMPTS.md](STATUS_ICON_PROMPTS.md) is the art contract
and is frozen: 1024px masters, one untinted 24px glyph inside a nominal 36px
badge, no frames, backing or counters baked in, slugs exactly as that file's
headings spell them. Backing, frame, counter and tint are drawn by code.

---

## 1. The measurement that decides the design

Every audited canvas frame (`Domain/UiKit/UiFrames.cs:18`) is at least 1920
wide and 1080 tall: 21:9 adds width, 4:3 and 16:10 add height, nothing
narrows. Badge rows and the panels they must clear are all `Place.At` from
canvas centre, so **1920x1080 is the binding worst case in both axes and the
only frame worth measuring.**

Stage geometry, from `Domain/Stage/FightStageAnchors.cs:85` (Near 300,-218;
Far 565,-125), `SpriteScale` 0.76 and `StageLayout.FarScale` 0.74, at
`FightHudSpec.StageSlotsPerSide` = 3:

| Slot | Scale | Ground Y | Declared box X | Nameplate band Y |
|---|---|---|---|---|
| 0 (near) | 0.760 | -218 | +/- 121.6 of 300 | -254.5 .. -233.2 |
| 1 | 0.661 | -171.5 | +/- 105.8 of 432.5 | -203.2 .. -184.7 |
| 2 (far) | 0.562 | -125 | +/- 90.0 of 565 | -152.0 .. -136.3 |

A row of 4 badges at 36px plus a 36px overflow chip and four 4px gaps is
**196px wide and 36px tall**. Where it fits:

| Candidate band | Free box (canvas units) | Blocked by | 196x36 fits |
|---|---|---|---|
| Under party slot 0 | none: nameplate bottom -254.5 is already 6.5 below the verb column's top edge -248 (`FightScreen.cs:1190`, rows at `CommandBottom + 26 + i*62`, x -436..-136) | verb column | no |
| Under party slot 1 | nameplate bottom -203.2 against roster plate 0's top -185.7, x overlap -538..-468 | roster plate 0 | no |
| Under party slot 2 | nameplate bottom -152.0 against roster plate 1's top -135.7, x overlap -655..-468 | roster plate 1 | no |
| Under enemy slot 0/1/2 | clear from the nameplate down to the canvas floor; nothing always-visible sits at x 178..663, y -165..-300 (verb column is x -436..-136; submenu, detail column and target prompt are all `Inactive` until opened) | nothing | **yes** |
| Party plate, band y14..45 | 348 x 31 (x -138 portrait edge to +210.18 content inset; 238 wide above y45 where PartyClass starts at x100) | HP row below, name/class above | 36px too tall; **27px fits** |
| Roster mini-plate, band y-2..20 | 202 x 22 (x -66 name edge to +136 hpValue edge) | hp bar below | **20px fits** |
| Enemy plate status line | 142 x 12 (`FightScreen.cs:824`) | three rows in 64px | no |

**Placement decision: the status row hangs under the figure for enemies and
on the left-hand HUD plates for the party, because the party's front figure
provably stands over the verb column and the enemy plate is a 200x64 card
with a 12px text row and no space to convert.**

Which party members have a stage figure: **all of them, simultaneously.**
`FightController.StageVisuals.cs:114-124` shows one party slot per living
party member up to three, every refresh. So the roster row is a convenience,
not the only path: an off-turn PC is also reachable by hovering its figure.

The roster plate cannot grow to take a taller row. Raising `RosterPlateH`
from 44 (`FightScreen.cs:1046`) pushes roster plate 1's top edge past -135.72,
and party slot 2's foot band starts at -133 (`FightScreenTests.cs:418`,
FootBand 40 / RingDrop 8). The clearance is 2.28px. Do not touch it.

Sizes, therefore:

| Surface | Badge | Pitch | Slots | Overflow chip | Counter |
|---|---|---|---|---|---|
| Enemy, under figure | 36 | 40 | 4 | yes | yes |
| Party plate (acting PC) | 27 | 33 | 6 | slot 6 becomes "+N" at 7+ | yes |
| Roster mini-plate | 20 | 24 | 4 | yes | no, tooltip carries it |

The enemy row is a child of the **stage panel** at the slot's `SlotOffset`,
never of the slot node: the slot carries `WithScale(scale)`
(`FightScreen.cs:558`), so a row inside it would draw at 20px in the back
slot. Row centre Y is `offset.Y - 60`, unscaled; the nameplate reaches at
most 48 * 0.76 = 36.5 below the ground line, so 60 clears it by 23.5px at
slot 0 and 33px at slot 2. Row centres land at -278, -231.5 and -185, spans
40px apart, so the three rows never overlap even where their x ranges do.

Party plate: raise `PartyBuffSlots` 4 -> 6 (`FightScreen.cs:1103`), keeping
`PartyBuffIconSize` 27 / `PartyBuffPitch` 33 / `PartyBuffX0` -111. Six badges
end at x 67.5, inside the 100 where PartyClass begins. Nothing else moves.

---

## 2. The counter rule

One rule: **the counter is the status's `TurnsRemaining`, drawn whenever it is
below `StatusHud.SentinelTurns` (90). At or above 90 the badge draws no
counter and the tooltip reads "until it is used".**

`StatusEffects.Tick` (`StatusEffects.cs:399`) decrements every status except
Provoked and removes anything at zero, so everything really does tick and
"show the remaining ticks" is almost the whole rule. The exception is not a
second class of status; it is three sentinel constants already in the code:

| Constant | Value | Used by |
|---|---|---|
| `Marks.MarkDurationTurns` | 99 | Marked |
| `FightTuning.MagicalShieldDurationTurns` | 99 | Magical Shield, Sparring Buckler, Runic Ward |
| `FightSession.Talents.WardDurationTurns` | 999 | talent ward, Gift: Fury |

Every authored duration is small by comparison: content `statusDuration` tops
out at 3, `IceFingernailStackTurns` at 5, `PhoenixEggDurationTurns` at 3.
A threshold of 90 separates them with a factor of 18 to spare.

No new field on `ActiveStatus` or `BuffBadge`. The threshold is a public const
on the new `StatusHud` table (section 4), pinned by a test asserting every
sentinel constant is >= 90, every `FightTuning` duration constant is < 90, and
no content `statusDuration` is >= 90. That test is what stops a future
120-turn authored duration reading as "until used".

Provoked is authored at 1 turn and never ticked (`StatusEffects.cs:430`). It
shows "1", which is accurate: it lasts exactly one action.

---

## 3. Category colour: cut

Grep found no cleanse, dispel, purge or remove-status mechanic anywhere in
`Assets/_Project`. `PillCategory` / `CategoryOf`
(`FightHudModel.cs:671-694`) is read in exactly one place: as an `OrderBy`
sort key inside `EnemyStatusLine`. It is a code-internal partition with no
player-facing consequence.

**Cut category colour.** A badge encodes three things: frame shape and tint
for polarity, glyph for identity, counter for duration. Two tints, both
already present: `FightHudPalette.IntentHeal` for a benefit,
`FightHudPalette.HpBright` for a detriment (`FightController.Hud.cs:636`).
Polarity is holder-relative, so an enemy's Poison badge is a detriment badge
even though the player is pleased about it.

Frame shapes, drawn as UI geometry rather than generated art: a smooth
rounded outline for a benefit, clipped corners with a small top notch for a
detriment. Both must survive greyscale. `CategoryOf` stays as the ordering
key it always was.

---

## 4. One code and slug table

There are two code tables today and they disagree.
`FightHudModel.StatusBadge` (`FightHudModel.cs:557`) emits three letters for
ten statuses and two for Feared ("FR") and Marked ("MK"); `PillCode`
(`FightHudModel.cs:696`) emits two letters for all twelve. Merge them into one
public table, three letters everywhere, and delete `PillCode`.

| Entry | Code | Slug | Polarity | Counter |
|---|---|---|---|---|
| Poison | PSN | poison | detriment | ticks |
| Regen | RGN | regen | benefit | ticks |
| Protect | PRT | protect | benefit | ticks |
| Vulnerable | VLN | vulnerable | detriment | ticks |
| Stun | STN | stun | detriment | ticks |
| Shielded | SHD | shielded | benefit | none (sentinel) |
| Provoked | PRV | provoked | detriment | ticks (always 1) |
| Empowered | EMP | empowered | benefit | none (sentinel) |
| Chilled | CHL | chilled | detriment | ticks |
| Rooted | RTD | rooted | detriment | ticks |
| Marked | MRK | marked | detriment | none (sentinel) |
| Feared | FER | feared | detriment | ticks |
| Speed up | SP+ | speed | benefit | ticks, or none when TurnsLeft < 0 |
| Speed down | SP- | speed_down | detriment | ticks, or none when TurnsLeft < 0 |

`RTD` for Rooted, not `ROT`, because this game has a Poison status and `ROT`
reads as that.

The slug column needs no switch change for the twelve statuses.
`StatusBadgeIcons.Slug`'s default already returns `kind.ToString().
ToLowerInvariant()` (`FightHudModel.cs:651`), which is exactly these twelve
strings. What the switch cannot express is the two speed presentations, which
have no enum member. So the row model is keyed on a presentation id, not on
`StatusEffectType`, and speed contributes two rows to the table rather than a
thirteenth enum member. Do not add a Speed member.

---

## 5. What each badge says

Tooltips are keyword phrases, extending the shape Chilled and Rooted already
use: `NAME -- <keyword>, <duration>`. The keyword is wrapped in
`ItemStatLines.Coloured` with `GainHex` (#8FE07F) for a benefit and `LossHex`
(#E05A5A) for a detriment, by holder polarity. Duration reads `"N turns"`
below the sentinel and `"until it is used"` at or above it.

| Entry | Tooltip |
|---|---|
| Poison | `Poison -- N damage each turn start, K turns` |
| Regen | `Regen -- N healing each turn start, K turns` |
| Protect | `Protect -- N% less damage taken, K turns` |
| Vulnerable | `Vulnerable -- N% more damage taken, K turns` |
| Stun | `Stunned -- turn skipped, K turns` |
| Shielded | `Shielded -- next hit taken reduced N%, until it is used` |
| Provoked | `Provoked -- must attack its provoker, for N% less damage to them, 1 turn` |
| Empowered | `Empowered -- next attack deals N% more, until it is used` |
| Chilled | `Chilled -- -N% Speed, K turns` |
| Rooted | `Rooted -- Skill Only, K turns` |
| Marked | `Marked -- open to focused attacks, until it is used` |
| Feared | `Feared -- turn skipped and 25% more damage taken, K turns` |
| Speed up | `<source> -- +N Speed, K turns` / `for the rest of the fight` |
| Speed down | `<source> -- -N Speed, K turns` / `for the rest of the fight` |

Verified against the code, not the old copy:

- Feared is both halves. `HasStun` counts it as a stun and
  `DamageTakenMultiplier` adds its magnitude beside Vulnerable's
  (`StatusEffects.cs:219`), from the one authored `Fear.VulnerablePercent`
  = 25. Today's tooltip omits the 25% entirely.
- Marked has no damage effect. It is a token read by `Marks.IsMarked` and
  spent by `Marks.ConsumeMark`. Today's tooltip promises "increased damage
  from focused attacks", which overstates it.
- Shielded survives its own consumption under `WardsNeverExpire`
  (`StatusEffects.cs:308`). Unmentioned: the wearer cannot see the caster's
  talents, and the badge stays up either way.
- Speed entries are signed flat grants, not percentages, one per source,
  with `TurnsLeft < 0` meaning the rest of the fight. `ActiveStatus.Source`
  names a combatant, never a skill; a relic entry names the relic.
- Chilled appears once, as a status: `BuffBadgesFor` already skips a speed
  buff whose `Source is StatusEffectType` (`FightHudModel.cs:531`).

Ordering, both sides, one rule: action restrictions (Stun, Feared, Provoked,
Rooted), then immediate defense and offense (Shielded, Empowered), then the
rest by `CategoryOf` and a stable type/source key. Never a blanket harm-first
sort, which buries an enemy's Shielded.

---

## 6. Overflow

The balance bot does not record simultaneous status counts:
`docs/BOT_SUMMARY_SCHEMA.md` has no status field in `runs.jsonl` and
`RunTrace.cs` carries none. There is no number to fetch.

Estimated from content instead. One enemy carries `appliesStatus` (`rat`,
Poison 8/3) and three enemy skills carry one: `grapple` (Stun 1),
`spore_cloud` (Poison 3/3), `bog_mud_burst` (Vulnerable 25/2). Nothing an
enemy owns applies Chilled, Rooted, Marked, Feared or Provoked to a PC;
those all originate player-side and land on enemies.

| Holder | Realistic co-occurring set | Count |
|---|---|---|
| PC, floors 1-2, ward build | Poison, Vulnerable, Shielded, Regen, Protect, one speed entry | 6 |
| PC, worst case with Stun and Gift: Fury | the above plus Stun, Empowered | 8 |
| Enemy | Vulnerable, Marked, Chilled, Rooted | 4 |

**Realistic max is 6, on a party member.** The party plate's six slots need
no chip in practice; the enemy row's four cover the enemy case; the roster
row's four cover an off-turn PC's likely set.

**The scrollable, pinnable, keyboard-navigable inspector is cut.** The "+N"
chip is a badge like any other, and hovering it lists the hidden entries in
the same tooltip, one per line. That is the whole overflow feature.

---

## 7. Sludge, and the empty case

Worst case on screen is 3 enemies x 4 plus 6 on the party plate plus 2 x 4 on
the roster: 26 chips over a painterly stage. Three decisions hold it down.

1. **A row with no statuses renders nothing.** The row node is inactive and
   the backing is not drawn. That is the common case in most opening turns.
2. **One backing strip per stage row, not one per badge.** A single rounded
   strip at `#140A10CC` (80% alpha) behind the whole row, `AsDecor` so it
   takes no clicks. Five stickers become one label.
3. **Plate rows get no backing.** They already sit on painted panel art,
   which is the darkening the backing exists to provide.

---

## 8. Art risks worth a pilot

Three collision groups, all already differentiated in the prompts, all to be
checked at 24px and in greyscale before the remaining six are generated.

| Group | How they differ today |
|---|---|
| Protect / Vulnerable / Shielded | two kite shields (lavender whole, rust split) against one circle (periwinkle disc); the split gap must survive 24px, which the prompt's 2px-stroke rule already covers |
| Provoked / Feared | boar mask, crimson #CB7484, wide, tusks dominate; ghost mask, pale lilac #D6B9ED, tall, one large vertical mouth. Mitigated by shape, hue and value |
| Rooted / Speed down | boot with clamping roots against boot under a detached downward arrow; already called out in the prompt text |

Pilot set: those eight, not the prompts file's six.

---

## 9. Phases

### Phase 1: the feature (placeholder codes, no new art) -- 12h across 4 agents, ~5h wall

The shared badge component, both placements, per-icon hover, one merged
table, the completeness test. Ships working, with codes in every badge.

- Files: see the package table in section 11.
- Tests: `tools/test.ps1 ui,combat`, then `run_tests_parallel.ps1
  -BuildScenes` before the commit (a screen tree and a `[SerializeField]` set
  both change).
- Gate: UiAudit clean at four aspects with every row full; every
  `StatusEffectType` and both speed presentations resolve a unique 3-letter
  code and a tooltip containing no "?"; hovering any badge on any surface
  shows that badge's own text.

### Phase 2: art wiring and the keying route -- 3h

- Add a `status` kit to `tools/key_green_screen.py`'s `KITS`: source
  `Assets/_Project/Art/UI/Status/Raw`, output
  `Assets/_Project/Resources/Status`, `grouped: False`,
  `default_delivery_size: 256`. Non-grouped kits are still resized; only the
  re-crop is skipped. `force_sprite_import` fixes the `.meta`, and
  `Editor/StatusIconImportPostprocessor.cs` already enforces
  Sprite/Single/pivot/alpha/no-mipmaps for that folder.
- Key the master, inspect alpha, then resize. Overwrite `chilled.png` and
  `rooted.png` in place so their `.meta` GUIDs survive (CLAUDE.md gotcha 2).
  Copy every new `.png` and `.png.meta` back to main.
- Retire `tools/art/make_status_icons.py` only after checking its consumers;
  it is the current producer of chilled and rooted.
- Tests: `tools/test.ps1 art,combat` plus the PlayMode sprite test.
- Gate: `EveryStatusIconResolvesToArtworkThatActuallyLoaded` green over all
  fourteen.

### Phase 3: polish -- 4h

Last-tick counter emphasis (a static emphasised patch at 1 remaining, no
pulse), a restrained appearance pop with a reduced-motion path, and the
enemy plate's text status line. **Retire it:** two surfaces for one fact on a
200x64 card is the duplication that plate's own comment warns about.
`EnemyStatusLine` keeps only the BRK prefix, or goes entirely if BRK moves to
the row as a non-status prefix chip.

- Tests: `tools/test.ps1 ui,combat`, `run_tests_parallel.ps1 -BuildScenes`.

---

## 10. The completeness test

Extends `Tests/EditMode/Combat/StatusHudCoverageTests.cs`, which already
iterates `Enum.GetValues(typeof(StatusEffectType))`, with four assertions
over all twelve statuses plus the two speed presentations: the code is
exactly 3 characters, is not "?" or "??", and is unique across the fourteen;
the tooltip is non-empty and contains no "?"; the slug is lowercase, carries
no extension and yields a `Status/`-rooted path; and the sentinel threshold
holds (every sentinel >= 90, every `FightTuning` status duration < 90).

The **sprite-load half must be PlayMode**, extending
`Tests/PlayMode/Combat/StatusBadgeIconTests.cs`: `Resources.Load<Sprite>` is
what distinguishes a PNG imported as a Sprite from one imported as a plain
Texture, and this project already puts that assertion in PlayMode beside
`EnemyIntentIconTests`.

Interim handling. `StatusBadgeIconTests.EveryOtherStatusStillFallsBackToText-
WithNoSpriteFound` today asserts the other twelve statuses **must not** load
a sprite, which actively forbids the art this plan commissions. Delete it in
phase 2's first commit, in the same change that widens
`EveryStatusIconResolvesToArtworkThatActuallyLoaded` from two entries to
fourteen. That widened test then sits red until the art lands, and going
green is phase 2's gate: not skipped, not `[Ignore]`d, not written in phase 1.
The glyph fallback it used to protect is protected instead by the EditMode
code-completeness assertion above.

---

## 11. Work packages

Four packages, separate worktrees, phase 1.

| Package | Owns | Must not touch | Depends on |
|---|---|---|---|
| **A. Model and table** | `Domain/Combat/Session/FightHudModel.cs`, `Tests/EditMode/Combat/StatusHudCoverageTests.cs` | `FightScreen.cs`, `ScreenRegistry.cs`, `FightController.*` | nothing |
| **B. Screen tree and placement** | `Domain/UiKit/Screens/FightScreen.cs`, `Editor/SceneBuilder/ScreenRegistry.cs`, `Tests/EditMode/Ui/FightScreenTests.cs` | `FightHudModel.cs`, `FightController.*` | A's field names only (fixed below) |
| **C. Runtime refresh and hover** | `Core/FightController.Hud.cs`, `Core/FightController.StageVisuals.cs` | `FightScreen.cs`, `ScreenRegistry.cs`, `FightHudModel.cs` | A's row type, B's `NodeRef` names |
| **D. Keying route** | `tools/key_green_screen.py` | everything else | nothing |

`ScreenRegistry.cs` is the one file two packages both need: B declares the
new `NodeRef` lists and wires them there, C reads the resulting
`[SerializeField]` arrays. **B owns it.** C codes against the names below and
does not edit the registry; if one must change, B changes it and tells C.

Contract fixed up front so A, B and C can run concurrently:

- `FightHudModel.StatusRow`: a readonly struct with `Code`, `Slug`,
  `Tooltip`, `IsPositive`, `Counter` (int, -1 for none) and `SortKey`.
- `FightHudModel.StatusRowsFor(FightSession, CombatantState)` returns
  `List<StatusRow>` ordered by section 5's rule, replacing `BuffBadgesFor`.
  `BuffBadge` and `StatusBadge` go.
- `FightScreen` gains `List<NodeRef> EnemyStatusBadges` (3 x 5 flattened) and
  `RosterStatusBadges` (2 x 5), keeps `PartyBuffIcons` at six, and adds one
  shared `StatusTooltip` / `StatusTooltipText` pair repositioned through
  `Domain/UiKit/TooltipPlacement.cs`'s `Beside`.
- Hover uses `Core/HoverIndex.cs` with a flattened index, exactly as
  `WirePartyBuffIcons` does today (`FightController.Hud.cs:727`).

---

## 12. Out of scope

New statuses, gameplay changes, audio, figure VFX, gamepad navigation, Broken
badge art, and any change to `STATUS_ICON_PROMPTS.md`.

---

## Appendix: changes from the 2026-09-06 revision

- Measured the band. The party front figure stands over the verb column, so
  there is no under-figure row on the party side at any size; the enemy side
  is clear. Placement decided, on-plate versus under-figure, in one line.
- Fixed the party plate at 27px x 6 slots against the 348x31 band it sits in,
  rather than calling the 27px variant provisional.
- Replaced the two-class counter system with one threshold at 90, derived
  from the three sentinel constants (99, 99, 999) the code actually uses.
- Cut category colour. No cleanse, relic or talent reads `PillCategory`.
- Specified the completeness test, and found the existing PlayMode test that
  forbids the art this plan commissions.
- Sized overflow from content: realistic max 6, so the scrollable pinnable
  inspector is cut and the "+N" chip's hover lists the rest.
- `RTD` for Rooted, and one three-letter table replacing the two that
  disagreed (`FR`/`MK` against `PillCode`'s two-letter set).
- Sludge: rows render nothing when empty, one 80%-alpha strip per stage row,
  no backing on plate rows.
- Four concurrent packages with a fixed contract, and `ScreenRegistry.cs`
  named as the shared file with a single owner.

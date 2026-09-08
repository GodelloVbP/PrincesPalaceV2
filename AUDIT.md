# Prince's Palace — Architecture & Design Audit

**How this register works:** findings stay in place forever, they are never deleted.
A finding that gets fixed is struck through in place with ` — fixed in <short-sha>: <one
line>` appended, so the register stays a complete history of what this project has
actually been wrong about, not just what's currently wrong. New findings (including
open investigations that never got a firm root cause) get appended under whatever
section fits, or under "Open investigations" near the end if none does. A struck
finding's full write-up does not stay here: it moves, verbatim, to
`docs/AUDIT_STRUCK_ARCHIVE.md`, and this file keeps only the one-line struck heading
pointing at it — that is what keeps this register readable as it grows.

**This register begins at #37, and is native to this tree (v2).** Findings #1–36 —
written against v1, a codebase this repository never contained — plus the three dated
re-triage passes over them (2026-08-02, -03, -04), are archived verbatim at
`docs/AUDIT_V1_ARCHIVE.md`. Nothing was deleted, only relocated: that section's own
warning box already said none of it means anything without re-verifying against this
tree first, and it had grown to roughly 40% of this file's length. See the archive if
you're chasing a v1 finding's history; #37 onward below needs no translation.

---

## Findings from the debug-menu / Reckoning work, 2026-08-11

### 37. The economy is built around a voluntary-retreat flow the design does not have
`Domain/Economy/CurrencyType.cs:9-10` states the rule: run Gold is "banked onto the save on a
voluntary retreat", and "a wipe forfeits every unbanked coin". `:43` gives the framing — "Gold
is a wager until it is banked" — and `Wallet.BankFrom(source, from, to, rate)` (`Wallet.cs:96`,
`banked = floor(available * rate)`) is the partial-bank machinery that flow needs. Gold living
on **both** wallets (`RunState`'s at-risk copy and `SaveData`'s banked total) exists to serve
the same design.

**There is no retreat.** Confirmed with the author, 2026-08-11: retreating is not part of the
intended roguelike — you play until you die. So `BankFrom`'s `rate` parameter has no caller
that would ever pass anything but 1f, and the two-wallet split currently has one live purpose
(forfeit on wipe) rather than the two it was designed for.

**The wager itself is kept, and the author likes it.** The stated direction is to move banking
from an exit decision to an in-run one: checkpoints or banks encountered during a descent where
some capped amount of gold can be stored. That reuses `BankFrom` and the two-wallet split more
or less as they stand, so **nothing here should be deleted** — it is misfiled, not dead. The
debt is that the comments describe a mechanic that does not exist and no comment describes the
one that is planned, so the next person to read `CurrencyType` will implement the wrong thing.

Not fixed here because the checkpoint design is not settled and rewriting the comments to
describe an unbuilt mechanic would trade one wrong doc for another.

### ~~39. A tree node named `<button>Label` silently collides with the caption `UiEmitter` generates~~ — fixed in `4fc572b`: renamed the colliding `…Label` nodes and added `UiAudit`'s `CheckButtonLabelCollision`; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`

### ~~40. Relics and Embers are two currencies with one source, and one of them does nothing~~ — fixed in `f2ddde9`: Relics stopped being a currency and became a per-run draft with an authored rarity band instead; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`

### 41. `CurrencyType.Embers` and `Wallet.embers` survive with no live reader
Embers moved onto `Character.embers` (2026-08-11) so progression reflects who you actually
field. The wallet field and the enum value are both KEPT, deliberately: deleting the field
makes JsonUtility drop the value on load, which would silently rob every pre-v3 save of
everything it had banked, and `SaveData.MoveEmbersOntoTheRoster` is the only thing that reads
it. `Currencies.IsPersistent` still walks the enum, so removing the member would also change
what `WalletTests.ThereIsExactlyOneCurrencyARunCanCostYou` counts.

This is the exact shape of #40 — a currency member with no live use — and it is recorded
here rather than left to be rediscovered. The difference is that this one has a stated
expiry: once no save older than v3 can plausibly exist, both can go. `EmberOwnershipTests.
EmbersNeverLandOnTheSharedWalletAgain` is what stops anything starting to write to it in the
meantime, because that regression would silently re-share embers and nothing else would
notice.

### ~~42. No relic had ever fired in an actual fight~~ — fixed in `06f996b`: `FightEncounterAdapter.KitFor` now supplies the player's relic list, pinned by `RelicsReachCombatTests`; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`

### ~~38. A wipe does not actually forfeit anything yet~~ — fixed in `76a4dd1`/`2814cec`: the forfeit is enforced structurally — `RunSettlement.Settle` now runs before `EndRun` discards the snapshot; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`

---

## Findings from the Reckoning polish pass, 2026-08-12

### ~~43. `screenshot.ps1` reports success for a panel it never captured~~ — fixed in `846d820`: `-Panel` is validated against `ScreenRegistry` up front and success is checked against the specific expected file rather than whatever is already on disk; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`

### 44. The Reckoning's gain label sits on top of the bar it annotates
`ReckoningScreen.BuildRow`: the row is 64 tall, the name/level line occupies y 4..32, the track
is centred at -14, and the gain label (`ReckoningRow{i}Gain`, 200x22 at x 370, y -21) spans
y -32..-10 and x 270..470. The track spans x ±450, so the label overlaps the bar's right-hand end
by 20px vertically and across its whole width horizontally.

**A1 cannot see it**: both nodes are `.AsDecor()`, and the overlap check skips decoration on
purpose, because ambient art overlaps constantly by design.

It was already true before this pass and got marginally worse — the track grew 26 → 32 to stop
reading as a hole, which is finding 4 of the 2026-08-12 handover. Left alone rather than
half-solved: there is no vertical budget in the row, since a name line, a bar and a gain line want
84px in 64, and every alternative moves something the design settled deliberately. Either the row
pitch grows (which pushes three rows down toward the painted bottom border, and the top row into
the EXPERIENCE heading), or the gain moves onto the top line beside the level, or the overlap is
declared intentional and the label is styled to read as riding the bar. That is the author's call,
not a silent fix.

### 45. The detail column still covers the front enemy's feet while a submenu is open
`FightScreen.BuildDetailColumn` places a 340x300 panel at `(478, CommandBottom + 150)`, so it
spans y -486..-186 across x 308..648. The front enemy slot stands at x 470 with its ground line at
-228, which is inside that box — so whenever the player opens SKILL or ITEM, the nearest enemy's
contact ring, ground shadow and feet are behind the panel.

**Not fixed with the anchors, and the reason is arithmetic rather than preference.** Clearing the
detail column needs `Near.Y >= -166` (its top -186, plus the ring's 8, plus 12 of daylight). The
tallest actor needs 300 units above the ground line at the front slot's 0.78, so its head would
then reach 134, which pushes the enemy plate stack to `PlateFirstY` 430 and the ENEMIES heading to
within 24px of the canvas edge. That trades a conditional occlusion for a permanently cramped
top-right corner.

The rule applied instead is the one the handover states: clear every **always-visible** panel.
`FightScreenTests.NoAlwaysVisiblePanelStandsInFrontOfAFigureSFeet` enforces exactly that and
deliberately does not descend into subtrees that start inactive.

Three ways out, none of them free, all of them the author's call: shorten the detail column to 238
so its top clears the ground line (it currently holds a name, a kind, a 76px body, a divider and
four stat rows, so something has to give); move it out from over the stage, which the enemy plates
at x 520..920 leave no room for; or accept it and let the front enemy be occluded during selection.

---

## Findings from the register re-triage, 2026-08-17

The pass that established the v1/v2 split documented at the top. It struck #38 (built, never
struck), #29 and #19 (both obsoleted by the rebuild), and refiled the one piece of #19 that
genuinely carries across.

### 46. Twelve `Assert.Ignore`s skip on CONTENT shape, and three of them guard the regression #42 describes
v2 has 20 `Assert.Ignore` calls across 11 PlayMode files. **Eight are legitimate and should stay** —
they gate on `CanvasCapture.IsSupported` because `camera.Render()` is a no-op under `-nographics`
(`HitFlashPixelTests.cs:109`, `PostProcessingLegibilityTests.cs:178`/`:208`/`:243`,
`CharacterOverlayCaptureTests.cs:25`, `FightPlayableTests.cs:209`, `MapCaptureTests.cs:30`,
`RuntimeScreenshotTests.cs:51`). A pixel test with no pixels has nothing to assert.

The other **twelve skip because content or a generated map did not happen to contain what the test
wanted**, and those quietly stop testing as content drifts:

- `RelicsReachCombatTests.cs:106`, `:129`, `:135` — "content has only one character", "no relic in
  content carries a numeric modifier yet", "that relic does not touch attack"
- `CharacterOverlayTests.cs:230`, `:243`, `:265`, `:314` — "no wearable item in the starting kit",
  "the starting kit holds no consumable on the first page"
- `MapFlowTests.cs:228`, `:273` — "this leg offers no fight from the entry", "only fights"
- `EquipmentReachesCombatTests.cs:187`, `GlossaryTests.cs:167`, `FightPlayableTests.cs:172`

**`RelicsReachCombatTests` is the sharp end, and the reason this is filed rather than tidied.**
That file exists *because of #42*: relics were implemented, correct, and unreachable from actual
play for their entire life, and every relic test passed the whole time because each built its own
`PlayerKit` by hand. It is the guard against that recurring — and it can currently skip itself on
three separate content conditions without failing anything. A guard that silently declines to run
is indistinguishable from a guard that passes.

**This is NOT #19 wearing new clothes, and the difference decides the fix.** #19's skip was driven
by unseeded combat RNG, so seeding fixed it. These are driven by content shape and generated map
shape; seeding does not help, because the draw is not the problem — the *fixture* is. The remedy
is the one #24's fix already established as the house pattern: state the precondition rather than
hope for it. Either assert the content invariant loudly (`"content no longer has a relic with a
numeric modifier — this test has stopped covering #42"`), or build the fixture that guarantees it.
For the two `MapFlowTests` cases there is a third option the others lack: the map is seeded and
reproducible, so a seed known to offer both a fight and a quiet room can be pinned instead.

Cheap to fix, and worth doing before the next content change rather than after.

---

## Findings from the build-speed pass, 2026-08-19

### 47. The dynamic font atlas is tracked in git, so any run that rasterises a glyph dirties it

`Assets/_Project/Fonts/ChakraPetch-Regular SDF.asset` is a TMP font asset with a **dynamic**
atlas: glyphs are rasterised on demand and written back into the asset. It is committed, so its
`m_GlyphTable` is repo state that changes as a side effect of running the game or the tests.
Found on 2026-08-19 sitting at `-2133/+10` lines against `4d3dfc1` — the table had emptied — with
no commit in this session having touched it deliberately.

Nothing is broken by it: the atlas repopulates at runtime, and text renders correctly either way
(verified in the dossier captures, which are full of the glyphs the emptied table lacks). The
cost is noise. It shows up dirty in `git status` for work that never went near a font, which is
exactly the condition under which a real change gets waved through, and it is the kind of file
that `git add -A` would sweep up — which this project already has an incident about.

Left uncommitted rather than decided unilaterally, because the two fixes point in opposite
directions and the choice is the author's:

- **Commit it emptied and stop caring**, accepting that the table will churn again. Cheapest.
- **Untrack it and regenerate on build**, alongside the other generated assets. Consistent with
  rule 2 of `CLAUDE.md`, but the asset is referenced by GUID from every built scene, so it would
  have to be generated *and* synced back like `Resources/Content/` is, and gotcha #2 applies to
  its `.meta`.

Worth noting the same property is what made #48 below land: the atlas being sparse at load time
is not a defect, it is the design.

### ~~48. `EnemyIntentIconTests` asked an arbitrary font whether it could draw a glyph~~ — fixed in `c2f436a`: the test now reads the font off a live `TMP_Text` instead of an arbitrarily-ordered `Resources` lookup; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`

## The fight that never started, 2026-08-20

**#46 — A fight deadlocked whenever a monster won initiative. FIXED.**

Reported as "every game after the first elite, none of the buttons respond
anymore": monsters drawn and standing still, no intent icons, every verb dead,
nothing in the log.

`FightSession.Begin()` — whose own docstring reads *"Opens the fight: the first
actor gets its turn start, any monsters faster than the whole party take their
opening swings"* — **had 36 call sites and every one was in `Tests/`.** Nothing
in the game called it.

`AutoResolveEnemyTurns` has exactly two callers: `Begin`, and the path that runs
*after the player acts*. So enemies only ever moved in reply to a move.
`TurnOrder.Start` seeds charge from initiative and gives turn one to the
highest-initiative combatant — and when that was a monster, nothing existed to
resolve its turn. It held the turn forever.

Every symptom follows from that one fact, which is why nothing looked broken:
intents are telegraphed only on the player's turn, `CanAct` requires the
player's turn, no code path throws, and the stranded-turn watchdog added in
`bffe4c5` watches `_isBusy`, which is never set because no playback was ever
started.

**Why after the elite.** Floor 1's monsters are slower than the party, so the
player almost always opened and the deadlock could not occur. Leg 2's pool
admits faster ones, and the first fight a monster opened was the last fight that
worked.

**Why no test caught it.** Every fight test called `Begin()` by hand — including
the four `bffe4c5` added specifically for this report, which is why that commit
ends "WHAT I HAVE NOT DONE IS REPRODUCE IT. All four pass." The suite was
repairing the exact state the game left broken. `FightController.Bind` was doing
the same thing for the telegraph half, as a deliberately narrow fix, with a
comment that stated the missing caller outright and treated it as somebody
else's problem.

*Fixed:* `FightBootstrap` calls `Begin()` before `Bind`; `Begin` is idempotent so
the two doors cannot double-grant; `Bind` no longer commits intents, because a
`Bind` that quietly repairs half an opening is what let this go unnoticed. Three
tests: a monster opening and the fight coming back to the player, `Begin` being
idempotent, and `IsPlayerTurn || IsOver` through the real door — `CanAct`'s third
term, which `bffe4c5` named as the only one never asserted.

**Consequence worth knowing: every fight in the game has been missing its
turn-one `GrantTurnStart`** — opening-turn regen and status ticks never
happened. Restoring `Begin` restores them, so combat numbers move slightly in
every fight rather than only in the deadlocked ones. The suite is green either
way, which means nothing was tuned around the absence.

## Findings from the reward-track planning pass, 2026-08-21

### ~~49. `Character.cs` documented an invariant that was inverted, unimplemented, and guarded by a test that did not exist~~ — fixed in `457bce0`: both comments now say what is true and `RunManagerTests` gained the test that was claimed; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`

### 50. `SaveData.relicLoadout` is written by nothing and read by nothing

`RelicLoadout` is a 160-line class with a careful exclusivity rule ("assigning a
relic that is already in use takes it away from whoever had it"), a `Set` that
returns the displaced relic, `CharacterHolding` for greying out a card on an
assign screen, and eleven EditMode tests. **Nothing in the game calls any of
it.** `save.relicLoadout` appears in exactly two places outside its own file and
its own tests: the field declaration (`SaveData.cs:126`) and `Reconcile`
(`:491-492`), which validates entries that nothing ever creates.

Relics in play live on `RunSnapshot.relicIds` instead, and that file says why:
*"Drafted at the start of a descent and GONE when it ends -- which is why they
live here rather than on SaveData."* `RelicDraftController` writes there,
`FightBootstrap` reads there, `ReckoningController` and `RunStatsController` read
there. The per-character loadout is a v1 shape that survived the rebuild with its
tests attached, which is what kept it looking alive.

Same class as #41 (`CurrencyType.Embers` with no live reader), and the tests are
what make it expensive rather than merely untidy: eleven passing tests over a
type the game never invokes read, from the outside, as coverage.

**This one has already cost planning time.** `docs/archive/PLAN_PROGRESSION_TRACK.md`
priced the track's level-25 and level-45 "extra relic slot" rewards as a
widening of `RelicLoadout` from one entry per character to one per
(characterId, slotIndex) -- following that class's own header, which specifies
the widening as a stated future extension. The widening would have been correct,
tested, and reachable from nothing.

Not deleted yet: see the reward-track note below, because whether these
milestones become real is what decides whether the type has a future.

### 51. The track's "extra relic slot" rewards have nothing to unlock

`RunSnapshot.relicIds:108-111` states the design outright: *"A list rather than a
single id, deliberately. Only one is drafted today, but the design is 'infinite
slots per run' -- mid-run relic rewards from elites or bosses drop straight in
here with no shape change."*

There is no slot cap anywhere. A character can already hold any number of relics
in a run; only one is ever *offered*. So levels 25 and 45 grant capacity that is
already unlimited — the same shape as the two the handover already caught, where
"rest heals more" had nothing to improve because `RoomResolution` already heals
to full, and "honed offers" was a second dial on the Favor already driving
`RollTier` and `RollPlus`.

Three ways out, and it is a design call:

1. **Drop 25 and 45**, refill with filler, exactly as the gold nodes were held.
   Level 60 ("start every run with 2 relics") does not need them — it is drafting
   twice — and level 80 ("elites drop a relic") is `relicIds.Add`, which the
   comment above already anticipates.
2. **Introduce a cap** so the slots have something to lift. This makes the game
   more restrictive before it makes it more generous, and contradicts a design
   statement that is written down rather than assumed.
3. **Repurpose 25 and 45** to grant an extra *starting* relic each, so the relic
   line reads 25 → 2 at start, 45 → 3, 60 → the existing milestone folds in.

Recorded rather than chosen, per this file's standing posture on design
decisions that belong to the author.

### ~~53. Stat points cannot be spent -- `Character.Invest` has no production caller~~ — fixed in `a39a2c1`: the dossier's attribute cells gained a "+" that calls `Invest`; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`

### 54. `StatBlock.ScaledForElite` has no production caller -- elites get no stat boost at all

`ScaledForElite(multiplier, defenseMultiplier, attackMultiplier)` exists, is
commented at length, and names the playtesting it came out of: *"That
combination -- defense originally uncapped, attack still uncapped even after
defense was fixed -- is what made Elites 'completely clap you' in playtesting,
twice."* It closes by pointing at `FightController.EliteStatMultiplier /
EliteDefenseMultiplier / EliteAttackMultiplier` "for where this is actually
used".

**None of those three constants exists**, and nothing calls `ScaledForElite`.
`grep` returns the method, its tests, and one passing mention in `Rounding`'s
own comment.

So an elite room differs from a normal one in exactly two ways: it fields
`EncounterRoll.EliteEnemyCount` (2) enemies where a normal room rolls 1 or 2,
and it pays `VictoryRewards.EliteRewardMultiplier` (1.56x). Its monsters are
scaled by depth like everything else and by nothing else. Elites are
substantially WEAKER than the design describes, not stronger.

Found while diagnosing "floor 1 after the first elite it already becomes so
difficult" -- where it matters as the thing that is NOT the cause. The report
points at the elite; the elite is nearly a normal room.

Same class as #53, #42 and `DifficultyCurve`'s own "AND THEN IT WAS NEVER
CALLED": implemented, tested against inputs the test builds itself, unreachable
from play, and carrying a comment that describes it as live. Recorded rather
than wired, because switching it on would make the game harder and the reported
problem is that it is already too hard.

### ~~55. `ContentDatabase.MinSpentMet` enforces the talent gates and nothing calls it~~ — fixed in `e1ce29f`: the gate now lives in `TalentPage.Evaluate` as `Refusal.Gated`; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`

### ~~56. Every lit talent edge drew a second stray line, and the layout audit was exempted from seeing it~~ — fixed in `e1ce29f`: the edge's core is placed at `(0, 0)` unrotated instead of inheriting its parent transform twice; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`

## Findings from the combat-polish pass, 2026-08-25

Both raised by the author from play, both deferred on purpose rather than
folded into the polish pass that found them: one is a design decision that has
not been made yet, and the other is a content question wearing a bug's clothes.

### 57. A boss defeated for the first time should drop an Ember, and nothing does

Asked for directly, and explicitly as a note for later rather than as work:
"bosses that are defeated for the first time should drop an ember and it should
feel very rewarding receiving this."

Nothing is broken today -- this is a feature that does not exist. It is filed
here because it lands squarely on top of **#41**, which records that
`CurrencyType.Embers` and `Wallet.embers` survive with no live reader, and on
**#38**'s neighbourhood. Whoever builds this should read #41 first: the currency
already exists, so the work is a first-kill ledger, a drop moment, and the
presentation the request is actually about. "Feel very rewarding" is the whole
requirement and the only part of it that is not already plumbed.

The FIRST-TIME half needs somewhere durable to live -- a per-boss flag in
`SaveData`, alongside whatever #50's dead `relicLoadout` is eventually replaced
by -- and needs to survive a wipe, or the reward is farmable.

### 58. Shawn's skills have TWO unlock ladders, and only one of them is the tree

Reported from play: "Shawn still has all his skills without them being
unlocked. Woolgathering, shear, that should all be removed if it's not part of
the tree or be locked and hidden until you unlock it in the tree."

**Not a gating bug.** `ContentDatabase.cs:233-237` enforces the gate exactly as
written -- a skill appears when `unlockLevel <= character.level`, or when the
character was taught it, or when a talent granted it. Verified rather than
assumed. What the report is describing is the FIRST of those three routes doing
its job, and the objection is to the route existing at all.

Shawn's seventeen skills split cleanly in two, and the split is deliberate
design rather than drift:

| Route | Count | How it is authored |
|---|---|---|
| **Character level** | 8 | `unlockLevel` 1-8: shear (1), woolgathering (2), mud_burst (3), static_fleece (4), frost_flare (5), battering_ram (6), lightning_bolt (7), golden_fleece (8) |
| **Talent tree** | 9 | `unlockLevel: 999` -- deliberately unreachable -- plus a node naming them via `grantsSkillId`: provoke, headbutt, black_ram_mode, fleece_ward, shatter, wail, gift_mana, gift_fury, gift_haste |

Every `grantsSkillId` in `talents.json` resolves to a real sheep skill; there
are no orphans in either direction. The 999 sentinel is the mechanism that
keeps the two ladders from overlapping, and `ContentDatabase.cs:222-230`
documents it as the reason the reworked tree needed no parallel ability system.

**So the decision is the author's and it is a design one, not a fix.** Three
shapes, in rising order of work:

1. **Leave it.** Eight skills are a levelling reward and nine are a tree
   reward. Coherent, already built, already tested.
2. **Move the eight into the tree.** Author a node per skill with
   `grantsSkillId` and set each `unlockLevel` to 999. No code changes at all --
   this is entirely `talents.json` and `skills.json`, which is what the 999
   sentinel bought. The cost is eight new nodes' worth of tree layout and the
   balance question of what the early game hands out instead.
3. **Hide rather than remove.** The report offers this as the alternative
   ("or be locked and hidden until you unlock it"), but note that it is what
   already happens: an unreached skill is not in the list `SkillsFor` returns,
   so it is invisible rather than greyed out. If the intent was a visible
   locked entry the player can see and work towards, that is a fourth option
   and a UI change rather than a content one.

Recorded rather than actioned because 1 and 2 are different games and the
choice belongs to the author.

## Findings from the simplification pass, 2026-09-05

### 60. The content chain restates every field list four to five times

Eleven content types, 192 authored entries, and for each type the SAME field list is written
out in four or five places: `Raw*Entry`, the resolver, `Resolved*`, `*Definition`, and
`ContentBuilder.Build*`'s per-field copy -- plus, for skills, enemies and relics, a fifth in
`FightEncounterAdapter`'s copy back the other way. Skills were the worst of them: **1,155 lines
across five files to carry 34 fields**, of which the resolver's 571 are the only ones that
decide anything.

The cost is measured, not asserted. Adding `cooldownTurns` -- one integer, one behaviour --
touched **13 files and 41 mentions**, and **six of those files are chain files that decide
nothing**: `RawSkillEntry`, `SkillEntryResolver`, `ResolvedSkill`, `ContentBuilder`,
`SkillDefinition`, `FightEncounterAdapter`. Cross-checks from the same count: `meleeReach` 23
files, `bookTier` 19, `vfx.groundPath` only 8 -- lower because `SpellPresentation` had already
collapsed the middle for those six fields, which is the fix generalised here.

**The bug class is not hypothetical and it fired twice.** A mechanical 34-line copy is exactly
the shape where a missing line is invisible: `FightEncounterAdapter.Resolve(SkillDefinition)`
dropped `transform`, so every skill resolved for a fight arrived with a null grant and Black Ram
Mode did nothing at all (fixed, and pinned by `ContentRoundTripTests`). The same method then
dropped `bookOnly`/`bookTier` and nothing noticed -- harmless only because nothing yet reads
`ResolvedSkill.BookTier` at combat time. The file's own header had ended "This conversion is the
next candidate for the same treatment" since the first bug.

**Skills are collapsed in this commit.** `ResolvedSkill` is a `[Serializable]` class and
`SkillDefinition` is `{ ResolvedSkill data; string id; int SortOrder; }`, so the adapter returns
`definition.data` and there is no copy to drop a line from. Adding a field to `skills.json` now
touches three files: `RawSkillEntry`, `SkillEntryResolver`, `ResolvedSkill`. That is a **T2**
fix in §9's ladder -- one code path owns the field list, so there is nowhere else to get it
wrong.

Two things the pilot proved for the rest: `System.Serializable` on a Domain type keeps Domain
engine-free (the `noEngineReferences` asmdef still builds, and the dotnet domain suite is
unchanged), and no scene or prefab references a `Resources/Content` asset by GUID, so changing an
asset's serialized shape costs a content rebuild and nothing else.

**Eight of the eleven are now collapsed** -- skills (the pilot), then enemies, relics, talents,
characters, modifiers, spell tiers and achievements. Each `*Definition` is `data` + `id` +
`SortOrder` and whatever computed property its consumers actually call; each `ContentBuilder.
Build*` is `asset.data = resolved`; `FightEncounterAdapter`'s two remaining back-conversions
(enemy, relic) return `.data`. Adding a field to any of those eight now touches three files:
`Raw*Entry`, the resolver, `Resolved*`. Four Domain types had to become `[Serializable]` for
their owners to serialize -- `TalentEffect`, `ModifierEffect`, `RelicModifier` and
`EnemyAbilityRef` -- and each of those deleted a Content-side mirror carrier that existed only
because Unity would not serialize a readonly struct (`TalentEffectEntry`, `ModifierEffectEntry`,
`RelicModifierEntry`) along with the hand-written `Select` in each direction. Three nullable
fields became a value-plus-flag pair behind a same-named property, which is the trade
`ResolvedSkill.AppliesStatus` already made.

**Items, weapons and item sets do NOT fit, and were left alone.** `ItemDefinition` is not the
storage shape of one resolved record: `ContentBuilder` builds it from three
(`ResolvedItem`, `ResolvedWeapon`, `ResolvedSetPiece`), each contributing a different subset,
plus per-source sort offsets and a `ResolvedItemKind -> ItemKind` mapping across the Domain /
Content enum boundary. Collapsing it would mean inventing a FOURTH record that is the union of
the three, and the three copy blocks would survive verbatim -- copying into the record instead of
into the asset. That is a field list moved, not deleted, so the restatement count for items,
weapons and itemsets stands at 4 each and this finding stays open for them.

**Part two, the generic builder.** Eight of the eleven `Build*` methods were the same forty
lines -- `File.Exists` guard, `JsonUtility.FromJson`, `TryResolveAll`, the "no X were created"
wording, the `CreateInstance`/`CreateAsset` loop -- differing only in the per-field copy the
collapse above had already deleted. They are now four lambdas apiece on one
`ContentBuilder.Build<TRaw, TResolved, TDef>`; `ContentBuilder` went from 804 lines to ~550.
Relics reach it through a local function that closes over the achievement ids their resolver
takes as a second argument; enemies through an `include` predicate that keeps benched monsters
validated but unwritten. Items, weapons and item sets stay bespoke for the reason above.

### ~~61. `SpellVfxTests` flakes between runs on an identical tree — cause not found~~ — fixed in `c9afc22`: a frame-duration race, NOT the pooled ground node — the whole cast is 13ms of real time at 60x and the tests counted a frame later; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`

## Findings from the architecture review, 2026-09-06

One finding, fixed in the same pass that found it, and one question it surfaced
and deliberately did not answer.

### ~~62. A kill's two halves were typed by hand at five call sites, and one site had already lost one of them~~ -- fixed in `84eb5ed5`: `DealDamage` settles the death itself, behind a `KillCredit` argument with no default

When a combatant died, two things had to happen together: `_killedThisAction`
(`FightSession.Riders.cs:17`, read and reset once in `AdvanceAfterAction`, and
the sole gate on whether Trample or Bloodlust fire) and `RecordKill`. Both were
typed out by hand at five places -- `FightSession.cs:436`,
`FightSession.RelicMechanics.cs:419` and `:461`, `FightSession.Relics.cs:322`,
`FightSession.Talents.cs:332` -- each a copy of
`if (!x.IsAlive) { _killedThisAction = true; RecordKill(...); }` after its own
`DealDamage` call. **Nothing enforced the pairing.**

**It had already drifted twice, in both directions.** `FightSession.Skills.cs:578`
carried a comment recording that the pair had once been moved *into*
`ApplyFinalDamage` -- a note about where the truth currently lives is a note
that it has lived somewhere else. And `SplashOntoNeighbours`
(`FightSession.Talents.cs:543`) was a sixth kill path with only the `RecordKill`
half: a Black Ram transform splash that felled a bystander scored the kill in
the ledger and silently forfeited the rider. That is exactly the failure the
shape invites -- the kill is still a kill, the log still reads right, and no
test fails.

`FightSession.Relics.cs:300` had even seen the pattern and priced it: "worth
collapsing the day a fifth shows up and actually causes a gap the way the
damage-bonus duplication did, not before." The fifth had shown up and the gap
was already open.

**The shape chosen.** `DealDamage` -- the one funnel every damage figure in the
session already went through -- now settles the death itself via `SettleDeath`
(`FightSession.Ledger.cs`), which absorbed `RecordKill` outright. It measures
alive-before against dead-after, so one body settles exactly once even though
`ApplyFinalDamage`'s elemental and matching-type riders re-enter the funnel
after the main hit may already have felled the target. The five manual pairs
are deleted and `SettleDeath` is the only writer of `_killedThisAction` left.
**T2 in `docs/CODE_STANDARDS.md` §9's ladder**: one code path owns the concern,
so there is nowhere else to get it wrong. Not T1 -- the type system cannot make
"deal damage without settling the death" unrepresentable while `DealDamage`
returns a `DamageResult` a caller may ignore -- but the argument is the T1-shaped
part: `KillCredit` has **no default value**, so a sixth kill path does not
forget to decide, it fails to compile.

**The poison exception is now written down rather than omitted.** `TickStatuses`
(`FightSession.Riders.cs:355-363`) kills without crediting, on purpose: the
poison was applied turns ago by someone who may now be dead, and back-crediting
it would put points in a column the player cannot account for against any blow
they watched land. Left as a *missing* call that would be indistinguishable
from the bug above -- so the poison block calls `SettleDeath` with
`KillCredit.Nobody`, a no-op on that branch by design. What it buys is that one
grep finds every death decision in the file family, the exception included.
This is the half worth getting right: a deepening that only stopped silent
missed credits, while opening the door to a silent over-credit, would not be a
net gain.

`KillCreditTests` pins one test per former site, plus the transform splash whose
regression is the evidence above, plus the poison exception -- so a future
regression in any single path is named rather than merely counted.

### 63. A monster felling a party member records no kill row

Surfaced by #62 rather than caused by it: `FightSession.Enemies.cs:720` is the
enemy swing, and it has never paired anything. Post-#62 it says so out loud
(`KillCredit.Nobody`) instead of saying nothing, which is the only reason it is
legible enough to file.

The ledger is id-keyed and folds a fight's totals into a run's, so a monster's
`Kills` and a party member's `TimesDowned` are both columns that exist and stay
at zero. Whether that is wrong depends on what the end-of-fight and run screens
are meant to say, which is a design question.

**What must NOT change is the flag.** Raising `_killedThisAction` here would be
worse than the gap: an enemy turn resolves *inside* `AdvanceAfterAction`
(`AutoResolveEnemyTurns`), after that method has already read and reset the
flag, so the flag would survive to the player's next action and hand them a
Trample the monster earned. Any fix here credits the ledger only, and wants a
test that pins the flag staying down.

Left open because it is a balance and presentation decision, not a refactor's
to make.

### ~~64. `ContentDatabase.Initialize` and `FightSession.IsOnCooldown` had no live reader~~ — fixed in `ab4a0ba5`: both deleted

Same shape as #41/#50/#54: a member declared for a purpose that never grew a
caller. `ContentDatabase.Initialize` (`Core/Content/ContentDatabase.cs`) was a
seam meant to let tests inject content directly, skipping Resources — its own
comment said as much — but no test ever grew the adapter that would have
called it. Zero call sites in the tree. Tests substitute content one layer
down instead, where it is cheaper and engine-free: `ContentBuilder` writes
real assets and `Reset()` drops the cache (`CharacterPortraitTests`,
`ContentIsolationTests`, `TestGlobals`), or a test bypasses `ContentDatabase`
altogether and resolves straight from the source JSON (`EnemyContentPinTests`).
A seam with zero adapters is not a seam, it is dead code with a comment
explaining what it was for.

`FightSession.IsOnCooldown` (`FightSession.Cooldowns.cs`) was smaller but the
same story: a public one-line wrapper over `CooldownRemaining` that nothing
ever called. `CooldownRemaining` stays — it has the real callers.

### 65. A poison death records no `Ledger.WentDown`

Surfaced by the same `SettleDeath` (`FightSession.Ledger.cs`) that fixed #62.
`TickStatuses` (`FightSession.Riders.cs:381`) kills via `StatusEffects.Tick`
and calls `SettleDeath(actor: null, target: actor, credit: KillCredit.Nobody)`
on purpose — the poison was applied turns ago by someone who may now be dead,
and back-crediting the kill would put points in a column the player cannot
account for. But `SettleDeath` returns on `KillCredit.Nobody` *before*
`Ledger.WentDown(LedgerIdOf(target))` runs, so the victim's own down-count is
skipped along with the attacker's kill credit — a body that went down did go
down regardless of who is credited for it.

Arguably `WentDown` should fire on every `SettleDeath` call, `Nobody` included,
and only the kill-credit half should be gated. That changes ledger numbers a
run and an end-of-fight screen already read, so it is a decision and not a
drive-by fix.

## Findings from the restatement sweep, 2026-09-06

Searched for one fact typed by hand in two or more places with no single owner and
nothing that fires when the copies disagree, judged against `docs/CODE_STANDARDS.md`
§9's T1/T2/T3 ladder, in four read-only sweeps — Domain, Core/Editor, tools/docs/tests,
and the JSON↔code seam. Thirteen findings came out of it, twelve fixed the same day;
a fourteenth, `tools/test_areas.ps1`'s hand-maintained `$PathAreas` map, is already a
decision recorded in that file's own header (lines ~606-617) and gets no entry here.

### ~~66. A `DefeatSpecificBoss` achievement's target enemy was checked by nothing~~ — fixed in `24797a93`: `AchievementProgress.ValidateDefeatSpecificBossParameter` checks the parameter against the enemy catalogue, wired in from `ContentDatabase.ValidateContent`; full reasoning in the commit message

### ~~67. `KitFor(CharacterDefinition)` hand-rolled the skill-unlock filter `AvailableSkillsFor` owns~~ — fixed in `3d2a1de4`: the shared predicate moved into `ContentDatabase.SkillsUnlockedByLevel`, and both call sites go through it; full reasoning in the commit message

### ~~68. `ResistanceByType.WithMagical`/`IsEmpty` hand-listed the `DamageType` members with no completeness test~~ — fixed in `8ae0ee8f`: both now derive from `Enum.GetValues`, pinned by two new completeness tests; full reasoning in the commit message

### ~~69. `TryResolveStatus` was duplicated byte-for-byte across two content resolvers~~ — fixed in `1fcc621e`: moved into `StatusAuthoring.TryResolve`, shared by `SkillEntryResolver` and `EnemyEntryResolver`; full reasoning in the commit message

### ~~70. `tools/bot.ps1` retyped the archetype list `Archetypes.Names` owns~~ — fixed in `a846a972`: the script's `-Archetypes` default is empty and `-botArchetypes` is omitted from argv when empty, so `BalanceBotRunner`'s registry-derived default governs; full reasoning in the commit message

### ~~71. The spell-acquisition metric was computed, rendered nowhere, and absent from the schema doc that claims to be the contract~~ — fixed in `b03fa207` and, for the room-row half, `7975c5a1`: `docs/BOT_SUMMARY_SCHEMA.md` now documents both the `cells[].spellAcquisition` fields and the `RoomTrace` fields, `tools/bot_report.py` renders the metric, and `tools/bot_schema_test.py` checks doc against emitter for both halves; full reasoning in the commit messages

### ~~72. The dossier leader line's vertical anchor was its slot's plus 37, typed seven times~~ — fixed in `f40577dd`: `DossierLayout.SlotGeometry` is the one top/file table now, and `LeaderTop` derives from `SlotTop` plus a named `LeaderVerticalOffset`; full reasoning in the commit message

### ~~73. The stage capacity `3` was typed twice in `FightBootstrap` with comments naming `FightHudSpec.StageSlotsPerSide`, and `"lone"`/`"full"` were literals at six sites~~ — fixed in `cd4c4c2c`: both `FightBootstrap` literals now read `FightHudSpec.StageSlotsPerSide` directly, and `PreviewFight.FormationLone`/`FormationFull` are the one spelling referenced at all six sites; full reasoning in the commit message

### ~~74. `BotPhaseTimers.PhaseCount = 18` hand-counted the `BotPhase` enum~~ — fixed in `3262cc2d`: `PhaseCount` now derives from `Enum.GetValues(typeof(BotPhase)).Length`; full reasoning in the commit message

### ~~75. `CharacterVoice` keyed Shawn's lines on the literal `"sheep"` with no check against `characters.json`~~ — fixed in `8156a4ca`: a PlayMode test asserts every `CharacterVoice` key is a live id `ContentDatabase.GetCharacter` recognises; full reasoning in the commit message

### ~~76. `architecture_audit.md` §7 stated two partial-class line counts as precise numbers, both stale~~ — fixed in `1656372a`: the `FightController` and `ContentDatabase` rows now read approximate, sha-stamped counts, same phrasing as the `FightSession` row fixed the same morning; full reasoning in the commit message

### 77. `ContentDatabase.ValidateContent()`'s six pre-existing whole-catalogue rules have no test

Found while adding the seventh (`24797a93`): grep for `ValidateContent(` in
`Assets/_Project/Scripts/Tests/` returned nothing. The new rule is tested via a Domain
predicate (`AchievementProgress.ValidateDefeatSpecificBossParameter`) plus a PlayMode
check against real content, because Core's `InternalsVisibleTo` names only the Editor
assembly and no test assembly can construct a synthetic `AchievementDefinition` to
drive `ValidateContent` directly. The six rules beside it, in
`Core/Content/ContentDatabase.Validation.cs`, would pass a broken catalogue silently if
any regressed: the cross-catalogue id-uniqueness sweep (the shared `seen` set walking
Character/Talent/Upgrade/Relic/Modifier/Enemy/Item/Skill ids, line 84), a talent's
`GrantsSkillId` naming a real skill owned by the granting character (line 156), a
talent's `GrantsStartingItemId` naming a real item (line 192), a talent's `CharacterId`
naming a real character (line 201), a skill's `CharacterId` naming a real character or
enemy (line 432), and an enemy ability's `SkillId` naming a real skill (line 138). (The
function's own header comment names "a relic naming an achievement" as an example of
this class of check — that gate is not one of these six: it is `RelicEntryResolver`'s,
enforced per-file at authoring time against the known achievement ids `ContentBuilder`
hands it, not `ValidateContent`'s. The header is describing the class of problem, not
an accurate list of six live rules.)

The fix is the same shape #66 used: pull each comparison into a pure Domain predicate
over primitives, EditMode-test the predicate's own branches, and let one PlayMode test
prove `ValidateContent` actually wires the predicate to real content.

### 78. `docs/BOT_SUMMARY_SCHEMA.md` lists room fields in two places

The `traces.jsonl` `RoomTrace` block is now covered by `tools/bot_schema_test.py`
(finding #71), but the `runs.jsonl` `rooms[]` prose list — a second, independent
listing of the same fields inside the same doc — still omits
`learnedSpellCountAfterRoom`, `unassignedSpellBookCountAfterRoom`, and
`spellAssignments`. `7975c5a1`'s own commit message flags this exact gap as
deliberately out of scope. Two listings of one fact inside one doc is the finding; the
fix is one section pointing at the other, not a second test. ~10 min.

## Findings from the positions pass, 2026-09-07

### 79. `ContentTop` has the same crop-offset bug the ring measurement had

`FightController.StageVisuals.cs:1403-1420`. `ContentTop(sprite)` calls
`sprite.texture.GetPixels((int)rect.x, (int)rect.y, ...)` against
`sprite.textureRect` -- a Tight-mesh crop, offset from the authored canvas --
and then returns the first opaque row it finds (`y + 1`, line 1418) with no
`+ rect.y` added back. That return value is in CROP space; every caller that
compares it against a canvas-space measurement is reading a wrong number by
exactly the crop's own Y offset, the identical class of bug
`FootBandCentreFraction` had until `2026-09-07` (see `docs/INCIDENTS.md`,
"The ring at canvas centre"). `ContentTopForActor` feeds the intent badge's
vertical placement; it was flagged and deliberately left for its own pass
when the ring fix landed rather than folded in as a drive-by. Same fix
shape: map the returned row back into canvas space before anything divides
or compares it.

### 80. The six-theme container kit's LEFT pad is asymmetric on two ratios

`Assets/_Project/Scripts/Tests/EditMode/Ui/UiKitVisiblePadTests.cs:47` --
`KnownLeftAsymmetryBandPx = 7f` -- and lines 103, 111, where the ThreeByFour
and NineBySixteen `Container` groups both carry
`LeftEdgeKnownAsymmetric = true`. `tools/measure_ui_kit.py`'s per-edge scan
finds silver and violet spliced with less left padding than the other four
themes on exactly these two ratios (confirmed on raw alpha, not a threshold
artifact — the column jumps 0→255 at a different column per theme).

**The 2026-09-07 regeneration made it worse, not better.** The spread was
3-4px against the delivery this finding was first written for; it is now
**7-9px on `container_*_3x4` and 4-5px on `container_*_9x16`**, and the band
had to go from 4px to 7px to keep the test honest about what the art
actually is. The same regeneration added a second, smaller exemption:
`KnownTopAsymmetryBandPx = 2f` (line 56) covers two groups' TOP edge —
`banner_flag_*_3x4` (crimson and gold start 2px lower than the other four)
and `container_*_3x4` (blue starts 2px lower than crimson) — where 1px no
longer holds.

`tools/splice_ui_kit.py` has **no per-theme offset knob** to fix this with:
it finds six connected non-transparent blobs on the source sheet and crops
each one tight (`splice_ui_kit.py:8`, and the crop at :297), so a theme
whose painted border starts further out on the source sheet carries that
straight through to `Processed/`. The fix is in the source sheets, not in
the slicer. Every other edge of both groups, and every edge of the other
eight ratio groups, still agrees within 1px. Nothing in the emitted screens
currently depends on a disagreeing edge; the asymmetry itself is unfixed
art, flagged for whoever next touches the container kit's silver/violet 3x4
and 9x16 sheets.

### 81. The Skill submenu frame is locked to the container kit's 3x4 aspect, with 9-slice off

`FightSubmenuLayout.FrameHeight` (`Domain/UiKit/FightSubmenuLayout.cs:208-209`)
is **height-bound**: it grows the 500-tall inner box out by the 3:4
container's own top/bottom content insets (`ContainerHeight / (1 -
FrameInset.Top - FrameInset.Bottom)`), and `FrameWidth` (:211-212) then
follows from that height through `Ui.ContainerSizeForHeight(ThreeByFour,
FrameHeight)`. Height leads, width is whatever 3:4 makes of it — so the
frame's width is the free variable and the ratio is still the thing that
cannot be chosen. `UiEmitter.cs:353-357`
renders every container sprite `Image.Type.Simple`, not `Sliced` --
deliberately, per its own comment ("v1 shipped an invisible button on every
screen through Sliced and never found a fix") -- so there is no 9-slice
available to decouple the frame's height from its width even if a taller
ratio were picked. Height-bound, the cost lands on width instead: 3:4 makes
the 500-tall frame ~420 wide around a 316-wide inner box, ~23px of slack a
side (see `FrameWidth`'s own comment). A ~4:5 container variant added at the
next kit regeneration would take that back; working 9-slice would too and is
the larger, unscheduled fix. Neither is a `FrameHeight` rewrite against the
ratios the kit currently ships.

### 82. `ProtectTheFrontPolicy` loses to both greedy archetypes, not just fails to beat them

`reports/bot/20260907-114015` (gitignored — 200 runs/cell, `f2945c1b`,
`Fresh` profile, `WhenOffered` shop policy). `ProtectTheFrontPolicy`: 35
deaths, `doomedShare` 0.2188. `GreedyAggressivePolicy`: 30 deaths,
`doomedShare` 0.0714. `GreedyDefensivePolicy`: 13 deaths, `doomedShare`
0.0769. `ProtectTheFront` has more deaths than either greedy archetype and
roughly 3x `GreedyAggressive`'s doomed share (0.2188 / 0.0714 ≈ 3.06x; ≈2.85x
against `GreedyDefensive`). A policy built to protect the front rank is
currently the worst of the three greedy-family archetypes on the metric it
was meant to improve. Whether the policy's move heuristic needs rework or
this is a legitimate finding about the front-rank rule itself is a design
call, not something to fix by tuning the bot; recorded here per
`docs/WORKFLOW.md` §11 ("change what a balance batch writes, or what a
metric means" belongs in `docs/BOT_SUMMARY_SCHEMA.md` — this is the batch's
numbers changing, not its schema, so it lands here instead).

### 83. `bog_mud_burst`'s `reachSlots: [2, 3]` is a balance change awaiting the owner's call

`Assets/_Project/ContentData/skills.json:429`. Authored as demonstration
content for the Reach system (`f2945c1b`'s own commit message: "the one
authored ability in the roster whose fiction already says 'not the thing
right in front of me'"), exercising the resolver conversion, the
zero-weighting of an ability with nothing in reach, and the Bog Witch's
fall-back to her plain swing when the back rank is empty. It is a live
change to an existing enemy's kit, not a no-op demonstration: Mud Burst now
lands on ranks 2-3 only instead of anywhere, which changes what
`bog_witch` fights actually do. Content is generated, never hand-edited
(`CLAUDE.md` rule 2) — reverting or keeping this is a balance decision for
the owner, not something to resolve by editing `skills.json` further without
asking.

## Findings from the overnight bug hunt, 2026-09-08

Six Opus hunters (combat; run/dungeon/economy/progression; UiKit/stage/audio/party; the
Core→Domain seam; tools/; a speed-buff/reach follow-up) each found a defect, fixed it, and
also found a few things that are not bugs — a comment or a mechanism that quietly stopped
matching intent, left exactly as found because deciding differently is the owner's call, not
the hunter's. Full write-up: `docs/BUG_HUNT_2026-09-08.md`.

### 84. `FightHudModel.MoveRow` cannot tell "no room" from "rooted", though its own header promises a reason

`Domain/Combat/Session/FightHudModel.cs:196-206`. `MoveRow` asks one boolean —
`session.CanMove(actor, direction)` (`:199`) — and on failure always writes the literal
`"NO ROOM"` into the row's cost column (`:204`). `FightSession.CanMove` (`Session/FightSession.cs:383-388`)
returns false for two different reasons: no move partner exists in that direction, or either
combatant in the swap is Rooted. The header two rows above (`FightHudModel.cs:178-183`) states
the design intent directly — "an illegal one is dimmed with its reason in the cost column …
'you cannot go forward, you are already in front' is more use than a row that quietly is not
there" — but a Rooted refusal reads "NO ROOM" on screen, which is not why the move failed.
Unreachable today (nothing currently roots a combatant on the player's own move-eligible turn),
which is why this is filed rather than fixed blind.

**Why it is the owner's:** fixing it means deciding what a rooted party member's row should say
("ROOTED" is the obvious answer, but that is new player-facing copy this register does not get
to author) and whether that is worth doing before anything can actually reach the dead path.

### 85. `ModifierTable.MaxStep = 0.22` was tuned against a Favor ceiling that no longer exists

`Domain/Rewards/ModifierTable.cs:118`. The constant's own header (`:60-81`) already documents the
retraction: the affix-roll ceiling used to read "55 (cap)" on the claim that 55 was the highest
Favor a real save could carry, but `TrackReward` no longer has a Favor member at all — the
realistic ceiling today is Shawn's authored 4 plus one worn Fortunate's `.Best` (~10 typical, 41
absolute, per the same comment, `:67-73`). `MaxStep` itself — the value every encounter class's
3-affix odds converge toward, chosen at `:101-116` specifically so three rungs stay a genuine
jackpot at the *old* Favor range — was left at `0.22` when the ceiling that shaped it moved.
`:79-81` flags this directly: "Worth a designer's eye before the next retune of `MaxStep`: at 41
Boss and Elite still reach the cap, Normal … never does."

**Why it is the owner's:** `0.22` is a balance number tuned against a jackpot feel, not a
correctness bug — whether the new, lower realistic ceiling (~10) makes the top rung too hard to
ever see, or whether that is exactly the intended rarity, is a design call the doc fix (`3428bdc1`)
correctly declined to make unilaterally.

### 86. `RewardTrackEntryResolver` never validates a milestone's or filler row's `amount`

`Domain/Content/RewardTrackEntryResolver.cs:302-387` (`TryResolveEntry`). Every other field on a
track entry is checked — `reward` against the `TrackReward` enum (`:308`), one-shot-as-filler
(`:324`), `against` against `DamageType` (`:333`), the element-unlocked-at-level-1 rule (`:346`),
the signature-resource rule (`:357`), `skillId` against the skill catalogue (`:368`) — but `amount`
(the `int` parameter carried straight through to `ResolvedEntryCore` at `:384` with no comparison
anywhere above it) is never read by any `if`. A `SecondLife` milestone authored with no `amount`
key resolves cleanly and grants a squad zero extra lives — content that validates and then does
nothing, silently.

**Why it is the owner's:** the right rule depends on the reward kind (some grants are legitimately
amount-less, e.g. a pure unlock), so a blanket "amount > 0" check would need per-`TrackReward`
carve-outs only the design of `reward_tracks.json` can specify.

### 87. `RunSnapshot.bossEnemyId` and `RngStreams.Boss` are dead machinery, the same shape as #50

`Data/RunSnapshot.cs:58` declares `public string bossEnemyId = "";` and nothing in the tree ever
assigns it anything else — it is read at `Core/RunEncounter.cs:74` and
`Core/Bot/RunOrchestrator.cs:518`, always as its own default. `Domain/Rng/RngStreams.cs:30`
declares `public const uint Boss = 2;`; grepping every non-test file in
`Assets/_Project/Scripts` for `RngStreams.Boss` returns zero readers. Both are wired for a
declared-boss descent design (`Domain/Dungeon/EncounterRoll.cs:79`'s own comment names
`declaredBossId` as reading this exact field) that nothing populates yet — the identical
write-nothing-or-read-nothing pattern #50 already named for `SaveData.relicLoadout`.

**Why it is the owner's:** whether the declared-boss feature is still planned (keep both, wire the
write side when it lands) or was dropped (delete both, plus the `EncounterRoll` comment naming
them) is a roadmap question, not something inferable from the code.

### 88. `RewardTrackDefinition.UnlockedAmount` has no caller

`Domain/Progression/RewardTrackDefinition.cs:277-288`. The method's own comment states its
purpose precisely — "`SquadTrack.SecondLivesLeft`'s 'how many charges has this squad earned'
question" — but grepping the whole tree (tests included) for `.UnlockedAmount(` returns nothing.
Either `SquadTrack.SecondLivesLeft` was built to answer that question a different way and the
comment is stale, or it was never built and the question is still open.

**Why it is the owner's:** deleting a documented-but-uncalled method risks deleting the one piece
of a still-planned feature that already works; keeping it risks the same fate as #50 and #87 above
if nobody ever wires it. Needs the SecondLife/squad-charges design confirmed either way.

### 89. `StageSlotsPerSide` is restated as the literal `3` in three separate places

`Domain/Combat/Session/FightHudSpec.cs:51` (`public const int StageSlotsPerSide = 3;`, the
canonical source) is correctly *derived from* by `Domain/Combat/Reach.cs:86`
(`MaxRanks = Session.FightHudSpec.StageSlotsPerSide`). But `Domain/UiKit/PartyLayout.cs:95`
(`public const int SeatCount = 3;`) and `Domain/Party/PartySeat.cs:17`
(`public const int Count = 3;`) both restate the same fact as their own literal `3` — each with a
comment pointing at `StageSlotsPerSide` by name (`PartyLayout.cs:91-94`, `PartySeat.cs:14-16`)
rather than reading it. A change to how many party members fight at once has to be made in three
places to actually take, and only one of the three would fail to compile if missed.

**Why it is the owner's:** collapsing two of the three into `= Session.FightHudSpec.StageSlotsPerSide`
is mechanical and safe, but `Domain/UiKit` and `Domain/Party` referencing `Domain/Combat` at all
is an assembly-layering call this register does not get to make unasked.

### 90. `FightSubmenuLayout.FrameContentCentreY`'s comment states a kit-delivery-old inset split

`Domain/UiKit/FightSubmenuLayout.cs:240-246`. The comment reads "the top and bottom insets differ
(4.5% vs 4%)", but the insets it is describing — `FrameInset` at `:218-219`, resolved from
`Ui.ContainerContentInset(ContainerRatio.ThreeByFour)` — are documented three lines above as
`.052`/`.055` (`:195-199`, "500 / (1 - .052 - .055) = 559.9"): 5.2%/5.5%, not 4.5%/4%. The
container kit was re-spliced at least once between the two comments being written and the numbers
were never reconciled — the arithmetic on `:246` (`FrameHeight * (FrameInset.Bottom - FrameInset.Top) * 0.5f`)
is still correct, only the prose restating it is stale.

**Why it is the owner's:** a one-line comment fix, but it is the kind of drift `docs/AUDIT.md`'s own
`CODE_STANDARDS.md` §9 rule ("prefer a reference to a restatement in prose") argues should be
replaced with a live read of `FrameInset.Top`/`.Bottom` rather than re-typed numbers again — that's
a small design choice about this file's comment style, not just a typo fix.

### 91. `SystemMenuLayout.StripFits` cannot return false while uniform mode is active

`Domain/UiKit/SystemMenuLayout.cs:315-316`. In uniform mode (`IsUniformMode`, count ≤
`UniformModeMaxTabs` = 3, `:104,127`), `TabWidths` (`:148-152`) divides the row evenly —
`uniform = (RowWidth - UniformGap * (count-1)) / count` — regardless of what the labels actually
need, so every tab is forced into that width whether or not the text fits. `StripWidth`
(`:306-313`) then sums those forced widths and adds `MinimumGap * (count-1)` (`:115`, 24px) rather
than the `UniformGap` (`:107`, 130px) actually used to compute them, so the total it reports is
always `RowWidth - (UniformGap - MinimumGap) * (count-1)` — strictly less than `RowWidth` for any
count ≤ 3. `StripFits` (`:315-316`) checks that sum against `RowWidth`, so for three tabs or fewer
it is mathematically incapable of returning false: a label too wide for its uniform box is
silently squeezed (or overflows the box visually) rather than tripping the fits-check the design
comment (`:301-305`) says exists precisely to catch that.

**Why it is the owner's:** the fix depends on what "fits" should mean in uniform mode — measure the
label against its forced-uniform width instead of against the whole row, or accept that uniform
mode is exempt from the check by design because it never had a real per-label budget to overflow.
Both are legitimate readings of "ARITHMETIC, not a fixed count" (`:301`).

### 92. `StanceManifest`'s `_hovers` map can pair a stale hover with a later duplicate's ground line

`Domain/Stage/StanceManifest.cs:44-67`. The constructor's per-actor loop writes
`_groundLines[path]`, `_groundLineSources[path]` and `_breaths[path]` unconditionally on every
iteration (`:59-61`), so a duplicate `spritePath` across two authored actors always leaves the
*last* entry's ground line and breath in place — straightforward last-write-wins. `_hovers[path]`
(`:62-65`) is written only `if (actor.hover != null)`, so if the first of two duplicate entries
carries a hover block and the second does not, the dictionary ends up with the second entry's
ground line and breath paired against the *first* entry's hover — a record that never existed in
the authored data.

**Why it is the owner's:** the real fix is refusing a duplicate `spritePath` at content-build time
(the same shape `ContentDatabase.ValidateContent`'s cross-catalogue id check already takes,
finding #77), which is a validation-rule addition, not a `StanceManifest` bug fix — and whether a
duplicate `spritePath` is ever legitimately authored (two stances sharing art) is a content-authoring
question this register can't answer from the code alone.

### 93. `PartyController.Persist` compacts a benched seat's hole, promoting the next member unchosen

`Core/PartyController.cs:395`: `save.selectedCharacterIds = Formation.SeatIds.Where(id => id !=
null).ToList();` — every save writes the seat list with empty seats filtered out entirely, rather
than keeping their position. Benching the Front-seat member and reloading therefore promotes
whoever was in Middle into Front, silently, because the gap that used to separate them is gone
from the persisted list. `Tests/EditMode/Run/PartySeatGapRoundTripTests.cs` pins the round-trip
and is marked `[Ignore]`, added deliberately alongside the finding (`64dab014`, "Owner's call: an
empty seat is a state the model has and the save cannot").

**Why it is the owner's:** already labelled as such at the commit that added the failing-but-ignored
test — whether a benched hole should survive a save/load round trip (needs a nullable/sentinel
slot in `selectedCharacterIds`) or compaction-on-save is the intended behaviour is a save-format
design decision, not a bug fix.

### 94. `unity_lock.ps1`'s `Certain`/`Ambiguous` fields have no reader, so a guess and a real hold look identical

`tools/unity_lock.ps1:78-79` computes `Certain = $held` (a Unity.exe process actually matched to
this project path) and `Ambiguous = ($ambiguous -and -not $held)` (a Unity.exe process whose command
line could not be parsed or read — the file's own comment at `:49-51` names "a permissions quirk,
usually" as the cause) as two distinct diagnostic fields, but every caller
(`tools/build_content.ps1:33`, `tools/preview.ps1:163,274,404`) reads only `.Held`, which is
`($held -or $ambiguous)` — so an ambiguous case (a Hub window's own `Unity.exe`, which carries no
`-projectPath` at all) is indistinguishable downstream from a confirmed hold on this exact project.
`preview.ps1` responds to `.Held` by falling back to the slower Editor-attached route rather than
batchmode, which is the plausible cause of "preview.ps1 takes 180-900s" the tools hunt flagged as
tonight's best candidate for the preview stall.

**Why it is the owner's:** the mechanical fix (have callers branch on `.Certain` vs `.Ambiguous`, or
have `Get-UnityLockState` try harder to identify a Hub window specifically) changes what `preview.ps1`
does when it cannot be sure, which trades a slower-but-safe default for a faster-but-occasionally-wrong
one — a risk tolerance call, not a bug.

### 95. `test_areas.ps1` treats a changed `.meta` file as unable to affect test behaviour, contradicting gotcha 3

`tools/test_areas.ps1:560`: `$ChangedIgnore = '^(docs/|\.claude/|\.gitignore$|\.gitattributes$|
tools/githooks/|.*\.md$|.*\.meta$|tools/timings\.json$)'` (documented at `:541` as "cannot affect
test behaviour"), so `-Changed` never maps a modified `.meta` file to any test area. `CLAUDE.md`'s
gotcha 3 says the opposite about one specific class of `.meta` change: `LoadSprite()` silently
flips a texture importer from Default to Sprite, which changes runtime behaviour (what a sprite
IS) and is exactly the kind of thing `UiAudit`'s zero-sized-graphic check or an art-referencing
PlayMode test could catch or miss depending on whether it ran.

**Why it is the owner's:** most `.meta` churn genuinely is inert (a GUID stamp, an unrelated import
setting), so blanket-including `.meta` in `-Changed` would make the area-mapping noisy for no
benefit most of the time — narrowing the ignore to exclude only an importer-type change (the one
gotcha 3 warns about) needs a design for detecting that specific diff shape, not a one-line regex
edit.

### 96. `splice_ui_kit.py`'s `measure_inset` returns a 50% sentinel that is indistinguishable from real data

`tools/splice_ui_kit.py:233-237` (`scan`): when the border-vs-interior walk never finds an interior
pixel within half the cell's own dimension, it falls through to `return axis_len // 2` rather than
signalling "no border found". `:247-248` then divides that sentinel by the same `w`/`h` it came
from — `"left": left / w` — turning "the scan gave up" into an inset fraction of exactly `0.5`,
which reads as a plausible (if large) real measurement to anything downstream that consumes the
report, rather than as the "this cell could not be measured" case it actually is. The function's
own docstring (`:210-217`) already documents one prior false reading this exact shape of bug
produced (`.008`/`.003` "implausible" fractions before the alpha gate was added) — a hard-to-reach
interior on a differently-cropped source sheet reproduces the same class of problem with a
different, still-plausible-looking wrong number.

**Why it is the owner's:** the mechanical fix (return `None`/`NaN` and have the caller print
"unmeasured" instead of a number) is straightforward, but deciding what the *report* should do with
an unmeasured cell — fail the splice, flag it for manual review, fall back to a default inset — is
a tooling-workflow call about how strict `splice_ui_kit.py` should be, not a pure bug fix.

### 97. A summoned body pays its own full reward, with no cap on how many times

`FightSession.Outcome.cs:93` — `ResolveOutcome` pays `VictoryRewards.For(_enemyKits.Values,
IsEliteFight, DepthStep)`, and `_enemyKits` gets a summon's kit filed into it the moment it is
summoned (`ResolveSummon`) and never removes a dead one — so every rat the Forest Warden's Roar
calls in is worth its full authored experience and gold at payout, on top of the enemies actually
fielded. `SummonCap` (`FightSession.Skills.cs:316`, mirrored at `FightSession.Enemies.cs:136`)
counts only the LIVING against the cap, not the total ever summoned, so a player willing to stall
in that one room and let each rat die before the next is cast has an unbounded exp/gold faucet:
the cap bounds concurrent adds, not lifetime payout.

The rest of the kill funnel already answers this question the other way — `EssenceSiphonOnKill`
and `InconspicuousKeyOnKill` both bail on `victim.IsSummon`, on the stated reasoning that a
called-in body is not a body worth paying for. `ResolveOutcome`'s payout is the one place in the
funnel that never asks the same question.

**Why it is the owner's:** every available fix is a balance number, not a wiring one — exclude
summons from the payout entirely (drops reward for a boss room that is genuinely harder for
having adds), pay a summon once regardless of how many are cast, or pay a flat fraction of a
summon's authored reward. `FightOutcomeTests.ASummonedBodyDoesNotPayItsOwnReward` (`[Ignore]`,
`6cdd8678`) pins today's behaviour and turns red the moment one of those is picked.

### 98. `tools/measure_stage.py` is not wired into the gate, which is how it rotted silently

The eighth hunter (Core runtime relayout) found the tool itself dead (fixed, `cd6f6f67`; see
`docs/BUG_HUNT_2026-09-08.md`), but fixing the tool does not fix the fact that nothing runs it.
It answers the one clearance question `UiAudit` structurally cannot (a stage slot's declared
320x200 is a placeholder the runtime replaces with the real sprite canvas, so `UiAudit` measures
a box that never appears on screen) and it failed loud, exit 1, for however long the 2x1 plate
conversion has been in the tree -- loud is the one thing that went right, and still nobody heard
it because no script in `run_tests_parallel.ps1` calls it.

**Why it is the owner's:** wiring it into the gate needs Pillow available on whatever machine
runs the gate, which is an environment decision, not a code one.

### 99. `SystemMenuController.MeasuredLabelWidths` has no fallback for a zero-width live measurement

`SystemMenuController.cs:339-352`. The fallback to `defs[slot].LabelWidth` fires only when a
label is null or its text is empty (`:348-350`); it does not fire when `TMP_Text.GetPreferredValues`
legitimately returns 0 for a label that has text but has never been active. `ApplyContext()` is
called before `panel.SetShown(true)` on the open path (`:186-187`), so the first `ApplyContext`
after opening measures every tab label before any of them have been active in the hierarchy --
exactly the condition TMPro's own measurement is unreliable under. A 0 width collapses that
tab and its underline.

**Why it is the owner's:** the fix is either reordering `SetShown`/`ApplyContext` or adding a
second fallback branch, and both are cheap; filed because nothing in the tree currently proves
which labels are actually hit by this on a real first open.

**Cleared, 2026-09-08, by test rather than struck with a sha:** a live `TMP_Text.GetPreferredValues`
does not in fact return 0 for a label that has never been active in this tree --
`SystemMenuLabelWidthTests.TheUnderlineIsTheWidthOfTheWordItMarks` measures exactly that call
shape and would itself fail on a 0. The zero-width path this finding worried about is not
reachable as described. Kept rather than struck because the underlying claim ("TMPro's
measurement is unreliable before `SetShown(true)`") was never shown false in general, only in
this one call shape -- a struck-with-sha entry implies a fix landed, and none did.

### 100. `IdleBreathing` puts an opposing slot-0 pair in phase with each other

`FightController.StageVisuals.cs:963,966`. The enemy loop and the party loop both pass their own
loop index into `BreatheIdle(combatant, index)`, and `BreathCurve.PhaseFor(index)` is keyed only
on that index -- so enemy slot 0 and party slot 0 breathe in phase with each other, though
`BreathCurve`'s own header (`Domain/Stage/BreathCurve.cs`) argues against two figures on the SAME
side breathing in lockstep, not against the two sides mirroring each other. Cosmetic, and
plausibly nobody would notice at a glance; filed rather than fixed because it may be intended.

### 101. `CombatEncounter.UpcomingTurns` throws on a zero-length ask, and its one caller has no guard

`CombatEncounter.cs:193-197` throws `ArgumentOutOfRangeException` for `count <= 0`.
`RefreshInitiative` (`FightController.Hud.cs:1698`) always calls it with `initiativeIcons.Length`,
which is 6 today and therefore never zero -- unreachable in production, but the call site trusts
a `[SerializeField] Image[]` to never come back empty, and nothing states that assumption where
the call is made.

### 102. `SaveSystem.Save`'s in-memory backend stores the caller's live object, not a copy

`Core/SaveSystem.cs:149`. The in-memory branch is `Memory[slot] = data` -- the store IS the
caller's `SaveData`, not a snapshot of it. `Load` holds up the other half of the contract on
both backends (round-trips through `JsonUtility`, per the header's own stated reason: two
callers holding one `SaveData` is the shape of "my gold reset when I left the shop"), but
`Save` does not: every mutation the caller makes to `data` after the write is retroactively
saved in memory, while the same sequence against the disk backend loses those mutations,
because the file was serialized at the moment of the write. The two backends answer the same
call sequence differently, which is the one thing an in-memory mode is not allowed to do -- it
exists so BalanceBot can measure this game, and a bot run that silently keeps unsaved state is
measuring a different one.

**Why it is the owner's:** copying on write restores parity, at the cost of the exact
`JsonUtility.ToJson` this mode was added to skip, on a write path a bot run takes 28 times --
that cost is the finding `BotPhaseTimers` was instrumented to produce, and trading it away is a
tuning call, not a bug fix. `SaveSystemTests.OnDisk_AMutationAfterTheSaveIsNotInTheSave` pins
the disk half (passes today); `InMemory_AMutationAfterTheSaveIsNotInTheSaveEither` asserts the
same claim against the in-memory backend and is `[Ignore]`d pending this decision (`248e7f43`).

### 103. `deny_broad_staging.py` scans commit-message text too, and a refused `add` does not stop the `commit` chained after it

`tools/githooks/deny_broad_staging.py` matches its banned flags (`-a`, `-A`, `--all`, ...)
against the whole command line it is handed, not just the staging arguments -- so
`git add <paths> && git commit -F - <<'EOF'` was refused tonight purely because the commit
MESSAGE happened to contain the substring `-a`, nothing about the staging itself. That refusal
alone is a nuisance. The second half is the actual risk: because the `add` in that chain was
refused, nothing new got staged, and the bare `git commit` a hunter then tried as a workaround
had no `add` in the same command for the hook to refuse -- it went through and committed
whatever was already staged in this shared working tree, which was another agent's in-flight
docs changes. Caught before anything left the tree (`git reset --soft HEAD~1`, recommitted by
explicit pathspec); nothing lost. See `docs/BUG_HUNT_2026-09-08.md` (f) for the incident.

**Why it is the owner's:** two independent design calls -- whether the hook should scan commit
message text at all (a false positive costs a blocked commit; not scanning risks missing a
`-a` disguised some other way), and whether it should also refuse a bare `commit` whenever the
`add` immediately before it in the same chained command was the one just refused. Both are
policy questions about how paranoid this hook should be, not bugs in what it currently does.

## Open investigations

### ~~52. `SystemMenuExitsTests.OnePressOnAnExitDoesNothingButArmIt` flaked once, navigating to `"Hub"` — cause not found~~ — fixed in `58a7f69`: a leftover `HoldToConfirm` was bleeding its `Abandon` navigation into the next test; the fixture's `TearDown` now cancels every live hold; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`

### ~~24. `BloodlustRelic_GrantsAnImmediateExtraTurnAfterAKillingBlow` flakes on fresh content/scene builds — root cause not found~~ — fixed as a symptom of #13 (pre-v2 history, no sha in this tree): a message-buffer trim discarded the Bloodlust line before the assertion read it; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`

### ~~59. A flat-art CHARGE lands its blow before the charger has crossed, and its own travel floor is why~~ — fixed in `d0f9944`: `FightBeatPlayer.Charge` now returns the `outSeconds` `PlayBeats` waits out before firing impact, burst only (no slash arc) per `docs/ART_PIPELINE.md`'s Blunt row; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`

## Findings from the Prismatic Orb pass, 2026-09-08

### 104. The dossier's unassigned spell-book list reads as empty when it is not

Reported from play with a screenshot: the map's status line says `SPELL BOOKS
TO PLACE: 6`, the dossier's SPELL BOOKS panel shows three `EMPTY` slots and an
`UNASSIGNED` header with nothing under it, and "whatever I press nothing
happens". The owner's own read: books have no art, so an owned book looks like
nothing.

Verified so far, not yet diagnosed: `CharacterDossierController.RefreshSpells`
(`CharacterDossierController.cs:411-481`) paints one row per entry in
`run.unassignedSpellBooks`, setting the row's name label to the skill's
display name (`:470-474`) and the empty hint only when the list is empty
(`:466`). `CharacterDossierScreen.BuildSpellsPanel` declares five rows
(`CharacterDossierScreen.cs:629`, `:719-744`). Skills author no `iconPath`
(`ShopController.cs:36-39`), so a book row and a book card have no picture
anywhere in the game. Whether the six rows were painted invisibly (text
colour, row height, panel clipping) or not painted at all (wiring, a stale
generated scene) is the open question; neither the empty hint nor a row was
visible in the screenshot, which rules out "list was empty" and points at the
panel rather than the data.

Owner's call on the fix shape, recorded rather than actioned: at minimum a
book row must read as an object (an icon, or a plate with a title) and a
selected row must look selected; the three slot boxes should look like
boxes. Related: the shop's book cards fall back to the no-art slot art for
the same reason.

### 105. A provoked enemy stays provoked until it takes a plain swing, which can be never

Reported from play: "provoke doesn't resolve -- a taunted creature stays
taunted for ever."

Verified: Provoked is deliberately not counted down by the turn-start tick
(`StatusEffects.cs:441-468`, `IsSpentByTheTurn`) because a one-turn taunt
would expire before the turn it exists to redirect. It is consumed instead
by `StatusEffects.ConsumeProvoke` (`:218-221`), whose only two production
callers are inside the enemy's PLAIN-ATTACK resolution -- the miss branch
(`FightSession.Enemies.cs:877`) and the landed branch (`:936`). Every other
way an enemy's turn can end leaves the status in place: an ability drawn
from its pool (summon, heal, spell, a skill cast at the provoker), a skipped
turn (Stun, Fear), and, since `ApplyProvoke` authors `TurnsRemaining 1`
"only to satisfy the floor" (`FightSession.Talents.cs:658-661`), nothing else
ever removes it. A monster whose weighted pool keeps drawing abilities is
taunted for the rest of the fight, still redirected by `ForcedTargetFor`
(`:993`) and still blunted by `ProvokedDamageMultiplier` (`:890`) on the
plain swings it does take.

The fix shape, for whoever picks it up: consume the taunt when the provoked
combatant's turn ENDS having acted, whatever the action was -- one call at
the end of the enemy-turn resolution rather than two inside one branch of
it -- and pin it with an enemy whose pool is 100% an ability. Whether a
taunt should also survive a stunned turn (the holder did not act, so the
redirect was never spent) is a design call; the comment at `:664` reads as
"spent by the turn it redirected", which says no.

## Findings from the layered-spell pass, 2026-09-08

### 106. Loading a fight scene over a live one logs an error, because a status badge pops on a panel that is already inactive

Found by `SpellRuntimeCaptureTests` becoming the first fixture in the suite to
load `Fight` twice in one class: the second `LoadSceneAsync(..., Single)`
disables the outgoing scene, and Unity logs
`Coroutine couldn't be started because the the game object 'FightPanel' is
inactive!` before the new scene opens.

Verified, one call chain and no branch in it:
`FightBeatPlayer.OnDisable` (`FightBeatPlayer.cs:329`) calls `EndFight`
(`:321`), which calls `Flush` (`:303`), which fires `_onFinished` ->
`FightController.OnPlaybackFinished` (`FightController.Input.cs:820`) ->
`RefreshUi` (`FightController.Hud.cs:63`) -> `RefreshPartyPlate` (`:583`) ->
`RefreshPartyStatusRow` (`:1300`) -> `PaintStatusRow` (`:1107`) -> `PaintBadge`
(`:1196`) -> `BeginAppearancePop` (`FightController.Hud.cs:1218`), which calls
`StartCoroutine` on a `FightPanel` the engine has already deactivated.

It is cosmetic in the game -- the badge simply does not pop on a screen that is
being torn down -- and it is not cosmetic in a test host: Unity's test framework
fails any test that logs an unexpected error, so it turns an unrelated fixture
red. `SpellRuntimeCaptureTests` tolerates it across the scene swap only
(`LogAssert.ignoreFailingMessages`, lifted before the cast) and says so at the
line.

The fix shape: `BeginAppearancePop` should set the final scale directly rather
than starting a coroutine when the behaviour is not `isActiveAndEnabled` --
which is the same graceful-degradation rule the rest of the HUD follows, and
one guard rather than a caller-side check at each of the paint sites. Left
undone here because it belongs to fight-HUD teardown rather than to the spell
layers, and a change to `RefreshUi`'s path deserves its own gate.

### ~~107. `tools/preview.ps1 -Spell` cannot choose which element of a choice-skill it casts~~ — fixed in `d7d559ab`: `-Element <DamageType>` on `preview.ps1`, validated against the skill's own `elements[]` before Unity boots and refused by name listing what is offered; carried through `PreviewProtocol.element` and `FightBootstrap.DevForcedElement` to `PreviewFight.ForSpell`/`PreviewElementOf`, which now casts the requested element and falls back to the old first-that-draws rule only when none was asked. The forced press and the capture prefix both name it (`spell_prismatic_orb_wind_impact.png`), so four elements no longer overwrite each other or require reordering `elements[]` in `skills.json`; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`

### ~~108. `SpellEmitter` cannot weight which atlas cell a particle draws~~ — fixed in `895e11f9`: an optional `float[] weights` on `SpellEmitter`, one entry per cell in the folder's own file order, landed together with an array-aware `IsAuthored`/`FieldsEqual` so a `float[]` field defaulting to null never reads as reference-unequal to itself. `SpellLayerRules` refuses a negative, non-finite or all-zero array (the numbers-only half it can check without the disk); the length-equals-frame-count half lives beside the identical `startFrame` rule in `SpellVfxRecipeDriftTests`, because Domain cannot see the folder's frame count either way. `SpellEmitterSim.At` picks by cumulative weight over the same `hash(seed, index, 6)` an unweighted emitter always used, so a shipped emitter that authors no weights plays the identical field it always did. Earth's `shed` and `spray` emitters both ship `[15, 18, 8, 12, 12, 8, 15, 2]` over the 8-cell drops folder -- small/mid chunks dominant, the two heaviest chunks held down, grit present, the dust puff at 2.2%; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`

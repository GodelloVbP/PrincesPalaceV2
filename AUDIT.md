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

### ~~62. A kill's two halves were typed by hand at five call sites, and one site had already lost one of them~~ -- fixed in `84eb5ed5`: `DealDamage` settles the death itself, behind a `KillCredit` argument with no default; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`

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

### ~~64. `ContentDatabase.Initialize` and `FightSession.IsOnCooldown` had no live reader~~ — fixed in `ab4a0ba5`: both deleted; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`

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

### ~~93. `PartyController.Persist` compacts a benched seat's hole, promoting the next member unchosen~~ — fixed in `82385df6`, answered together with #118: the save carries the hole, and `PartySeatGapRoundTripTests` is un-`[Ignore]`d

**The answer, 2026-09-11.** Owner's call, of the two coherent readings the finding named: the SAVE learns to
carry a hole. An empty front rank is a formation the player can choose, so `selectedCharacterIds` is a seat list
and an empty seat is `SaveData.EmptySeat` in place.

The sentinel is the empty string rather than null, and that is forced rather than stylistic: `JsonUtility`
writes a null element of a `List<string>` as `""` and reads it back as `""`, so a null hole would not survive
its own round trip. `SaveData.IsEmptySeat` is what readers ask; `PartyController` translates once, at the one
seam between the save's `""` and `PartyFormation`'s null.

TRAILING holes are still dropped. A hole says something only when somebody sits behind it, and keeping the tail
would write three entries for a solo save's one member.

The transcription problem the ignored test always had is now covered from the other side:
`PartySeatGapRoundTripTests` still transcribes `PartyController.SeatList` and the `Refresh` loop because it is
engine-free Domain, but two PlayMode tests in `SystemMenuPartyTests` press the bench affordance and read the
file back, so a drifting transcription fails somewhere.

The original finding follows.

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

### ~~106. Loading a fight scene over a live one logs an error, because a status badge pops on a panel that is already inactive~~ — fixed in `16eeb5aa`: `BeginAppearancePop` sets the badge's final scale directly instead of starting a coroutine when the screen cannot host one. The guard is NOT the one proposed below and the difference is measured: at that teardown repaint `isActiveAndEnabled` still reads **true** — nothing called `SetActive`, the whole SCENE is unloading — and the scheduler refuses anyway, so the error still fired from the same line with the proposed guard in place (stack in the runner log). `gameObject.scene.isLoaded` is the flag that has already flipped. Asserted by `FightTeardownLifecycleTests.LoadingAFightOverALiveOneWithEverythingInFlightLogsNothing`, which abandons a real round (a popup mid-rise, a death fade mid-fade, a lunge mid-tween) with no `LogAssert.ignoreFailingMessages` anywhere in it, and was seen red with this exact message. The four Fight fixtures that tolerate it across a scene swap can now drop that line; none was touched here.

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

### ~~107. `tools/preview.ps1 -Spell` cannot choose which element of a choice-skill it casts~~ — fixed in `4c45692b`: `-Element <DamageType>` on `preview.ps1`, validated against the skill's own `elements[]` before Unity boots and refused by name listing what is offered; carried through `PreviewProtocol.element` and `FightBootstrap.DevForcedElement` to `PreviewFight.ForSpell`/`PreviewElementOf`, which now casts the requested element and falls back to the old first-that-draws rule only when none was asked. The forced press and the capture prefix both name it (`spell_prismatic_orb_wind_impact.png`), so four elements no longer overwrite each other or require reordering `elements[]` in `skills.json`; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`

### ~~108. `SpellEmitter` cannot weight which atlas cell a particle draws~~ — fixed in `44e05216`: an optional `float[] weights` on `SpellEmitter`, one entry per cell in the folder's own file order, landed together with an array-aware `IsAuthored`/`FieldsEqual` so a `float[]` field defaulting to null never reads as reference-unequal to itself. `SpellLayerRules` refuses a negative, non-finite or all-zero array (the numbers-only half it can check without the disk); the length-equals-frame-count half lives beside the identical `startFrame` rule in `SpellVfxRecipeDriftTests`, because Domain cannot see the folder's frame count either way. `SpellEmitterSim.At` picks by cumulative weight over the same `hash(seed, index, 6)` an unweighted emitter always used, so a shipped emitter that authors no weights plays the identical field it always did. Earth's `shed` and `spray` emitters both ship `[15, 18, 8, 12, 12, 8, 15, 2]` over the 8-cell drops folder -- small/mid chunks dominant, the two heaviest chunks held down, grit present, the dust puff at 2.2%; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`

### 109. The house contact effect is the one spell look still authored in code

Found by the simplify pass after the layered presentation shipped. Every
spell's look is content (`vfx.layers` in `skills.json`, resolved through
`SpellPresentation`), except the arc-and-burst a plain melee blow draws on
its target: `FightController.SpellVfx.cs` `BuildContactPresentation` (~`:463-527`)
hand-builds two `SpellPresentation` templates in C# from the `ContactCues`
constants, now cached as two static fields (`_contactWithArc`,
`_contactBurstOnly`) after the review found the per-beat allocation. The
caching fixed the cost and entrenched the shape: a third weapon-family
contact look, or a change to the arc's timing, is a code change, which is
the case `docs/CODE_STANDARDS.md` §10 names ("combinations of supported
behaviour should cost a data change").

Fix shape: author the contact effect as content, one presentation per
`Approach`/weapon family in a content file loaded once through
`ContentDatabase` and selected the way `PlayContactFx` selects today; delete
`BuildContactPresentation` and the two statics; the allocation win survives
because content assets are already built once. Needs a schema entry and
`ContentBuilder` support, so it is its own pass, not a cleanup.

### 110. `run_tests_parallel.ps1` reports "All tests passed" off a STALE results file

Found 2026-09-09 while running the gate against a shared TestRunner pair.
Unity refused to start (`Aborting batchmode due to fatal error: It looks
like another Unity instance is running with this project open` -- the other
session had taken `-TestRunner2` between the lock check and the launch), so
no PlayMode run happened at all. The script then read
`test-results-PlayMode.xml` left behind by an EARLIER, narrower run, printed
`PlayMode -- Total: 39  Passed: 39  Failed: 0`, and ended with **`All tests
passed.`** The real suite is 897.

That is the AUDIT #43 shape one level up: #43 was the screenshot tool
checking whether the output directory held ANY png rather than the one it
was asked for. Here it is the gate checking whether a results file parses
rather than whether THIS run wrote it. The exit code is deliberately ignored
(the script's own header says why, and that reasoning still holds), so the
results file is the only signal -- and a stale one is indistinguishable from
a fresh one.

Fix shape: delete both `test-results-*.xml` before launching, and refuse the
run if the file the platform was supposed to write is missing afterwards --
the same "check the expected artifact, by name" rule #43 landed for
`screenshot.ps1`. A count sanity floor would be a weaker version of the same
thing and would need maintaining.

Not blocking: the run was repeated until it got a clean slot and the real
suite was green. But a gate that can say "All tests passed" having run 4% of
the suite is the one kind of green nobody re-checks.

### 111. `absorbsDamage` is authorable on a pools.json row, resolved, copied onto the runtime pool -- and read by nothing

Found 2026-09-10 by the combat/resource-pool bug hunt and confirmed by a run.
Left as found because the two ways to close it are opposite decisions and both
are the owner's.

`RawPoolEntry.cs:128-129` documents the field to the schema:
`[ContentDoc("Whether this pool soaks incoming damage before health, the way a
signature resource can.")]`. `PoolEntryResolver.cs:213` carries it onto
`ResolvedPool.AbsorbsDamage` (`ResolvedPool.cs:52`), and `ResourcePool.cs:179`
copies it onto the runtime pool. Every step works. The last one does not exist:
`CombatMath.cs:555` is the only `Absorb` caller in the tree and it reads
`target.SignaturePool` alone --

```
int absorbed = target.SignaturePool != null ? target.SignaturePool.Absorb(amount) : 0;
```

-- so a PRIMARY pool that authors the switch soaks nothing. Confirmed with a
`Fixed(100)`, `Full`, `absorbsDamage: true` primary: `pool.AbsorbsDamage` reads
`True`, and `ApplyDamageDetailed(hero, 100)` returns `absorbed = 0`, health
`500 -> 400`, pool `100 -> 100`.

The failing test that would pin either fix, and its fixture already exists:
`Tests/EditMode/Combat/ResourcePoolTests.cs` (the `Row()` helper) --
`APrimaryPoolThatAuthorsAbsorbsDamageActuallySoaks`, asserting
`60 == CombatMath.ApplyDamageDetailed(hero, 60).Absorbed` and
`500 == hero.CurrentHealth` for that row. Today: `0` and `440`.

**Two honest options, and picking is a design call rather than a fixer's.**

1. **Make it real.** `ApplyDamageDetailed` asks both slots, signature first so
   the shipped Wool ordering is untouched, and `absorbPerPoint` is carried onto
   `ResolvedPool` so a soaking primary can be tuned the way the signature one
   already is. This is the reading the `[ContentDoc]` line promises, and it is
   the shape `ResourcePool.cs:112-117` says the soak machinery was deliberately
   KEPT for ("the obvious shape for a future resource that IS armour").
2. **Refuse it.** Delete `absorbsDamage` from `RawPoolEntry`/`ResolvedPool` and
   have the resolver refuse a row that authors it, leaving the soak
   signature-only and saying so. Cheaper, and honest in the other direction.

The current state -- an authored switch that validates, resolves, and silently
does nothing -- is the one shape neither comment claims. Nothing in content
authors it today (both `pools.json` rows say `false`), so this is an authoring
trap rather than a live bug.

**One correction to the hunt's own write-up, recorded so it is not carried
forward.** It reported a second half to this: that the content constructor
never sets `AbsorbPerPoint`, so a primary pool stays at the compat default `1`
while "the signature path passes `ContentDatabase.SignatureAbsorbPerPoint` (2,
`Effective.cs:327`)". The constant is **1**, not 2 -- `Effective.cs:44`, halved
from 2 in the same pass that halved `CombatMath.DamageScale`, with its own
comment explaining why. So there is no discrepancy between the two paths today;
option 1 would still want the field carried, but as tuning headroom rather than
as a mismatch to repair.

## Findings from the run-outside-combat bug hunt, 2026-09-10

Four Opus hunters (combat and the resource-pool model; the run outside combat; the fight
stage and its presentation; verification/content tooling plus UiKit) reported rather than
fixed, and four fixers worked from those reports — which is what let a finding be graded as
an owner's call before anyone spent a commit on it. Full write-up, including the deferred
work and the untuned Fury economy: `docs/BUG_HUNT_2026-09-10.md`. This hunt has TWO register
entries and they sit under different headers: #112 below, and #111 (`absorbsDamage`), which
was appended to the 2026-09-08 layered-spell section above rather than here. Left where it
is — this register does not renumber or relocate — but noted, because a reader looking for
this hunt's findings under this heading would find half of them.

### 112. `RunSnapshot.shopStockVersion` is written in four places and read in none

`Data/RunSnapshot.cs` declares it; `Core/Bot/RunOrchestrator.Shop.cs:129` sets it
to `ShopStock.StockVersion` on a roll and `:154` back to 0 on leaving;
`Data/SaveData.cs:851,865` zero it on both of `ReconcileShopStock`'s discard
paths. Nothing anywhere compares it against `ShopStock.StockVersion` (`ShopStock.cs:208`).

Same shape as #50 and #87: a field that is written, persisted and never
consulted. Ranked low deliberately -- the rule it appears to enforce does hold.
"An open shop from an older build is left exactly as it was rolled" is true
because the shelf is STORED and `EnsureShopStock` returns early while
`ShopIsOpen(run)`, whatever version produced it. The field buys nothing on top
of that.

The hazard is the header, and that half is fixed rather than filed: the comment
used to state the rule as though this field were the thing keeping it, which is
what a future `ReconcileShopStock` deciding to re-roll a stale shelf would read
as "already handled". It now says record-not-gate, and
`ShopMutationTests.AShelfRolledByAnOlderGeneratorIsLeftAsItWasRolled` pins the
rule against the storage instead.

What is left for an owner's call is the field itself. Three options, none of
them a fixer's to pick: read it (compare in `ReconcileShopStock` and re-roll a
stale shelf, which is the behaviour the old header described and NOT today's);
delete it and its four writers, as `grantedGold` was deleted in the same pass;
or keep it as the record it now says it is, on the argument that "which
generator made this shelf" is worth having on disk the day the shelf format
changes. Kept, unchanged in behaviour, pending that call.

Test: `ShopMutationTests.AShelfRolledByAnOlderGeneratorIsLeftAsItWasRolled`
(passes today, and pins the rule to something other than an unread field).
## Findings from the total bug hunt, 2026-09-11 (stage 3a, the four seams)

Four Opus finders read every call site of the four seams the plan named
(`ContentDatabase`, `RunManager`/`SaveData`/`RunSnapshot`, `RunOrchestrator`,
`FightSession`) against the callee's stated intent, then three fixers worked
only the findings whose intent evidence was two agreeing sources or better.
The twelve below are the ones a fixer could not take: each either picks a
behaviour the evidence does not settle, or changes a number that is a balance
decision. Full write-up, including what was fixed, what was disproven with
counter-evidence and what is still awaiting reproduction:
`docs/BUG_HUNT_2026-09-11.md`. The manifest row for each is in
`docs/hunt/MANIFEST.md`.

### ~~113. What an extra turn should re-pay: today it re-pays everything, and Black Ram Mode loses two of its three turns in one round~~ -- fixed in `c477205f`: the owner took option 1 -- a bonus action is the SAME turn and re-pays nothing, so `GrantTurnStart` split into `OpenTurnFor` (unchanged) and `ReopenTurnFor` (`_locks.ResetTurn`, `TickPrimaryPool`, and the two recomputes that read them), and the three `[Ignore]`d repro tests are green with a control beside them; full reasoning in the commit message

Found 2026-09-11 by the `FightSession` seam finder (F2) and confirmed by a run.
This is the lead `L1` (rider ordering) that two previous hunts deferred; the
ordering is now written out in full in `docs/BUG_HUNT_2026-09-11.md`, and this
is what it was hiding.

`FightSession.Riders.cs:79-80` runs `_encounter.AdvanceTurn(); GrantTurnStart();`
after `TryGrantTrample`/`TryGrantBloodlust` have already called
`_encounter.GrantExtraTurn(actor)`, so the SAME actor is `Current` again and
`GrantTurnStart` (`:218-261`) runs its whole thirteen-step block for them a
second time. Four of those steps are destructive rather than idempotent:
`TickStatuses` (`:233` -- the poison tick AND every non-`IsSpentByTheTurn`
duration countdown), `TickCooldowns` (`:234`), `TickTransform` (`:246`) and
`TickPhoenixEgg` (`:249`).

Clearance ledger row `K8` cleared exactly two of the thirteen (`_locks.ResetTurn`
`:229` and `TickPrimaryPool` `:231`) and argued they were intended. The other
eleven were never examined. `K8` has been narrowed in the manifest to say so.

**Contract, quoted.** `talents.json:169` (`sheep_ram_trample_3`, "Momentum"):
*"A kill does not cost you the turn. Once per turn -- he is heavy, not
infinite."* `Riders.cs:147-152`: *"Trample T3: a kill does not consume the
action."* And `Riders.cs:216-217`, which is the sentence the code breaks:
*"The next actor's turn opens: mana regenerates, statuses tick..."* -- on an
extra turn there is no next actor.

**Reachable on shipped content, and the pairing is forced.** `talents.json`
puts `sheep_ram_trample_3` (`ExtraAttackOnKill`) in the SAME path as
`sheep_ram_converge` (Black Ram Mode, *"7 wool for three turns of splash,
weight and speed"*) and lists it as a PREREQUISITE of it. Nobody can own Black
Ram Mode without owning Trample, and Black Ram Mode's splash makes a kill more
likely.

**The repro lives in the tree.** `Tests/EditMode/Combat/TurnRiderTests.cs`
carries three `[Ignore]`d tests (`:400`, `:421`, `:441`, all citing the same
`ExtraTurnDecision` reason string) added by `2c84a7a9` precisely so this is not
re-derived when the call is made. Their output today:

```
APoisonedTramplerIsPoisonedOncePerRoundNotOncePerKill
  Expected: 1   But was:  3
ATramplersStatusDurationsTickOncePerRound
  a 3-turn Shielded expired inside ONE of the hero's rounds
  Expected: True   But was:  False
ThreeTurnsOfBlackRamModeSurviveARoundInWhichHeTramples
  Expected: 2   But was:  1
```

**Two honest options.**

1. **Split the method.** `GrantTurnStart()` becomes `OpenTurnFor(actor)` (all
   thirteen steps, a genuinely new turn) and `ReopenTurnFor(actor)` (an extra
   action by the same actor). `K8`'s two steps stay in both. What else stays is
   the owner's line to draw, and each side of it is a balance number: not
   paying poison twice is a straight buff to a Trample build, not refunding
   cooldowns is a straight nerf. The call-site change is one line at
   `Riders.cs:79-80`, or a `_grantedExtraTurnTo` field the grant methods set
   and `GrantTurnStart` reads and clears.
2. **Leave it and fix the content row instead.** Say Black Ram Mode lasts
   "three actions" rather than three turns, and accept that a Trample build
   ages its own statuses faster as the cost of the extra swing.

The one part that is not a balance question either way is Black Ram Mode: a
three-turn form that reliably lasts two turns is a content row lying about
itself. Option 2 is the cheap way to stop it lying; option 1 is the one that
makes `Riders.cs:216-217` true again.

Callers a fix touches: `FightSession.Riders.cs` (both grant methods),
`FightSession.Enemies.cs:526` and `FightSession.cs:290` (both must keep the
full version). Tests: `TurnRiderTests` (un-`[Ignore]` the three),
`KillCreditTests`, `ChilledStatusTests`, `RelicMechanicsTests`,
`StatusEffectsTests`.

### ~~114. The reward roll draws from Equippables, not Offerable, so the six starting-kit items are offerable rewards~~ — fixed in `0ec7d8fc` (content) and `5fc4eb51` (code): the owner took neither filed option — the starting kit is DELETED, and `Candidates()` asks `ContentDatabase.Offerable` rather than re-typing a predicate, so there is no third universe left for the two filters to disagree about

**The answer, 2026-09-11.** Option 1 was "`Candidates()` reads `Offerable`"; option 2 was "keep the kit and
correct the header". The owner's answer was that a new profile should not open wearing anything: the six
`startingStock` rows are gone from `items.json`, along with their six generated assets and metas. `items.json` is
the two potions now, neither of them `IsEquippable`, so `Candidates()` and `ContentDatabase.Offerable` currently
select the identical set — and the green test `OnlyEquippablesAreOffered`, which a fixer could not have broken to
satisfy a header, did not have to be broken to get there. The `startingStock` FIELD survives, unauthored and still
pinned by `ItemEntryResolverTests`; `items.json`'s `_readme` records why the kit went. Stage 2's coverage
denominator and the roll's universe now agree, which they differed on by exactly these six ids.

The code half landed the same day in `5fc4eb51`, independently and from the other side: `Candidates()` now calls
`ContentDatabase.Offerable` rather than re-typing the `IsEquippable` predicate, so the two cannot drift apart
again the next time a hand-authored equippable is added, and `OnlyEquippablesAreOffered` was rewritten as
`OnlyOfferableItemsAreOffered` plus `TheStartingKitIsNeverOfferedAsAReward` — a rule that keeps saying something
the day a `startingStock` row is authored again. Either commit alone would have fixed the symptom; together they
close both the row and the seam.

The original finding follows.

Found 2026-09-11 by the `ContentDatabase` seam finder (F3). Filed rather than
fixed because the intent evidence CONFLICTS -- one of the two sources is a
green test asserting today's behaviour.

`Core/ItemOfferRoll.cs:36-42` (`Candidates()`) filters on `IsEquippable` alone
(`:39`).
`ContentDatabase.cs:199-208` (`Offerable`) filters on "was this generated with
a tier" and its header says why:

> That leaves out the hand-authored one-offs in items.json -- potions and the
> starting kit -- which have their own routes in and would otherwise turn up
> as a "reward" the player already owns six of.

`Offerable`'s only production caller is `Editor/Bot/BalanceBotRunner.cs:817`.
The reward screen, the shop's gear shelf (`RunOrchestrator.Shop.cs:396`) and
the Reckoning all draw from `Candidates()`.

**Repro (static).** Six `startingStock` items, all Equipment, tier 0;
`RarityTable.FloorTier(step) = step/16` is 0 on floors 1-2 and
`ItemOfferTable.Choose` opens at `TierSpread` 1, so all six sit in the early
band. Win the first fight of a run and be offered the `iron_helm` you are
wearing.

**Intent evidence, both directions.** FOR `Offerable`: the header above,
`docs/BOT_SUMMARY_SCHEMA.md:144`, `docs/PLAN_SHOP.md:962`. AGAINST:
`ItemOfferRoll.cs:30-35` reasons only about potions, and
`Tests/PlayMode/Content/ItemOfferRollTests.cs:37` `OnlyEquippablesAreOffered`
asserts the current behaviour by name. A fixer cannot break a named green test
to satisfy a header.

**Two options.**

1. **`Candidates()` reads `ContentDatabase.Offerable`.** The starting kit stops
   being a reward and stops stocking the shop's gear shelf. Touches
   `ItemOfferRoll.Roll`, `RunOrchestrator.Shop.cs:396`, `ReckoningController`,
   `ShopStock.RollGear`; `OnlyEquippablesAreOffered` is rewritten in the same
   commit to say what it now means. Failing test:
   `ItemOfferRollTests.TheStartingKitIsNeverOfferedAsAReward` -- `Candidates()`
   ids exclude every `StartingStock` id.
2. **Keep the kit offerable and correct the header.** The argument for it: six
   tier-0 items in the floor-1 band are the cheapest possible early reward, and
   a duplicate `iron_helm` is a sell, not a dead offer.

Side effect worth recording either way: stage 2's bot coverage denominator
("Offerable items offered, 491/638") and the roll's actual universe differ by
exactly these six ids, so the coverage figure is measured against a list the
roll does not use.

### ~~115. The shop screen produces refusals it may not guess at, and displays none of them~~ — fixed in `bbe23ff6`: option 1, one `PaintRefusal(ShopResult)` into the existing `detailLabel`, and `Reroll`/`SellRow` stopped discarding their results

**The answer, 2026-09-11.** Owner's call: show the refusal, minimal effort, "can't afford" is the important
one. So option 1 (reuse `detailLabel`) over option 2 (a dedicated line, a screen-tree change and a
`-BuildScenes` run), and the cost of that choice — a refusal replaces the selected card's description — is paid
by an explicit clearing rule: the line goes on the next selection, on opening or closing the pack, and on
opening the shop.

Three strings, not ten. Seven of `ShopRefusal`'s ten values describe a call arriving out of order and a player
cannot act on the difference, so they share "Can't do that"; `NotEnoughGold` gets "Not enough gold";
`AppliedNotPersisted` gets "Bought, but the save did not write", because it is the opposite news and a player
who reads it as "you cannot afford this" loses the run. `Reroll` and `SellRow` keep their results too — a reroll
refuses `NotEnoughGold` the same way a purchase does, and `SELL ALL` has no interactable gate against `NotInBag`
at all (#F5's asymmetry, still open).

`ShopScreenRefusalTests` drives the buttons rather than the orchestrator, because the mechanism was never in
doubt and only a press crosses the gap the finding is about. KNOWN AND LEFT: a sell's refusal is painted while
the pack modal is up, and the keeper panel it lands in may sit behind that modal; it is cleared on close, so it
cannot leak onto the shelf as a line about a row nothing is showing.

The original finding follows.

Found 2026-09-11 by the `RunOrchestrator` seam finder (F4). Confirmed for
`NotEnoughGold`; candidate for `AppliedNotPersisted`.

`ShopController.cs:21-25` states the design:

> only BUY can refuse, and it refuses through RunOrchestrator's own ShopResult
> rather than a client-side guess

and `ShopResult.cs:59-65` says *"The screen can say so."* It does not.
`Commit` (`ShopController.cs:176-200`) reads only `result.Applied` (`:192`);
`Reroll` (`:202-216`) and `SellRow` (`:269-289`) discard the `ShopResult`
entirely. The only production reader of `.Reason` anywhere in the tree is
`BotRunDriver.cs:530`, which writes it to a trace file.

So the UI CAN produce `NotEnoughGold` (there is no pre-check, by design),
`NotInBag`, and `AlreadyKnown`/`NotOwned`/`NoFreeSlot` from the dossier
(#116) -- and shows the player nothing at all. Combined with stage 2's finding
that no bot archetype can construct an illegal shop choice, seven of the ten
`ShopRefusal` values have no path from produced to seen except a test and the
bot's trace.

**Repro.** Gold one below a card's price, press Buy: nothing happens, no
message, gold unchanged.

**Failing test** (PlayMode):
`ShopScreenRefusalTests.BuyingACardYouCannotAffordSaysWhy` -- invoke
`buyButton.onClick`, assert the detail label carries the refusal string and
gold is unchanged. Red today.

**The mechanism is not in question; the wording and the placement are.** One
`PaintRefusal(ShopResult)` mapping `Reason` to a `UiStrings` line, called from
the three sites that currently discard the result.

**Two options.**

1. **Reuse `detailLabel`** (`ShopController.cs:31`), the line that already
   carries the selected card's description. Zero new UI, no scene change; the
   cost is that a refusal replaces the description and has to be cleared on the
   next selection.
2. **A dedicated refusal line** in the shop screen tree. Clearer, survives a
   re-selection, and costs a `Domain/UiKit/Screens/` change plus a
   `-BuildScenes` run and a `UiTextFitAudit` sample.

`AppliedNotPersisted` wants its own line under either option -- it means the
purchase happened and the save did not, which is not the same news as "you
cannot afford this".

### ~~116. The dossier swallows AlreadyKnown, which the plan says is where the player finds out~~ — fixed in `ed24933b`: option 3, "You already have this spell prepared" in the spell panel's existing status line, and the green would-fill preview is suppressed for a book the character already carries

**The answer, 2026-09-11.** Owner's wording and owner's placement. Option 3 (a message line) rather than an
OWNED marker on the slot chip or the row, and it is the only one of the three that needs no screen-tree change
and therefore no scene rebuild: the line borrows `DossierSpellsNoBooksLine`'s node, and the two can never be up
at once because a character who cannot hold a book cannot already have one.

Both halves of the finding are closed, not just the visible one. `PressSlot` reads `result.Reason` now, and
`RefreshSpells` asks ONCE per refresh — not per slot — whether the selected book is already in one of this
character's slots, because that is a fact about the book and the character and the slot the press lands on
cannot change it. That is what stops the screen promising a placement it then refuses.

The refusal belongs to one press: selecting another row, paging to another character, closing the panel and a
successful placement all clear it.

NOT MEASURED: the line is 36 characters against `DossierNoSpellBooks`' 33-character audit sample, in the same
14pt band at the same 381px width, so it fits with room — but `UiTextFitAudit` runs at scene build and this
change deliberately triggers none.

**The node it borrows is in the wrong place for this second reading, and that is filed as #146.** The
no-books line is centred in the three-slot band (`CharacterDossierScreen.cs:726`,
`slotTop - slotBandHeight * 0.5f`), which is correct for the case it was built for -- the slots are hidden
then, so the line stands in the empty band. For AlreadyKnown the slots are UP, so the refusal draws across
slot 1's name. `UiAudit` cannot see it: the line is `.AsDecor()`, and `CheckSiblingOverlap` skips any pair
with a decor side. The text-fit reasoning above is unaffected -- it fits; it is sitting on something.

The original finding follows.

Found 2026-09-11 by the `RunOrchestrator` seam finder (F3), confirmed by
inspection of both paths.

`CharacterDossierController.PressSlot` (`:434-452`) picks `ReplaceSpell`
(`:448`) or `LearnSpell` (`:449`) by slot occupancy, then:

```
if (result.Applied) _selectedUnassignedRow = -1;
Refresh();
```

`result.Reason` is never read. `RefreshSpells` paints no OWNED marker, and the
green "would fill" preview lights for ANY empty slot while a row is selected --
including one this press cannot fill.

**Contract.** `docs/PLAN_SHOP.md` 1d: *"Refuse a duplicate ... the assignment
panel reads OWNED for that character ... not a silent success either"*; 2d
(revision 2026-09-03): the player *"only finds out at assignment time, via 1d's
duplicate refusal"*. `Domain/Rewards/ShopResult.cs:5-9` exists as a reason enum
because *"the screen ... need[s]"* it.

**Reachability is anticipated, not hypothetical.** `AvailableBookOptions`
(`RunOrchestrator.Shop.cs:452-455`) excludes a book only when EVERY squad
member has learned it, `ShopController.BookFactLine` (`:540-546`) prints "N
unassigned copies", and `ReplaceSpell`'s own comment
(`RunOrchestrator.Spells.cs:137-140`) discusses buy-two-learn-one-replace.
Repro: buy X, assign to Shawn slot 0; buy X again; select it, press Shawn slot
1. Nothing happens and nothing is said.

**Restore in substance; the placement is the choice.** The refusal must become
visible -- that part has three agreeing sources and is not in doubt. Where:

1. **On the slot chip.** The slot the press would hit reads OWNED and the green
   fill preview is suppressed for it. Most local, tells the player before the
   press.
2. **On the row.** The unassigned-book row itself reads OWNED per character.
   Survives a slot-less glance; costs a per-character recompute on every
   refresh.
3. **A message line.** The refusal is said after the press, like #115's shop.
   Cheapest, and the only one that also covers `NoFreeSlot`.

Failing test (PlayMode, extending `DossierSpellSlotsTests`):
`PressingAnEmptySlotWithABookThisCharacterAlreadyKnowsSaysSo` --
`learnedSpells=[{shawn, mud_burst, 0}]`, `unassignedSpellBooks=["mud_burst"]`,
select the row, press slot 1; assert `learnedSpells.Count == 1` (passes today)
and the OWNED marker visible (red today). Touches
`CharacterDossierController` plus one `UiStrings` addition, so `-BuildScenes`.

### ~~117. The boot settle rewrites slot 0, so Continue points at the wrong slot~~ — fixed in `b8242045`: option 3, the `[RuntimeInitializeOnLoadMethod]` boot check is gone and `SaveSlotManager.EnterSlot` -> `SettleOnOpening` is the whole of the rule's enforcement

**The answer, 2026-09-11.** Owner's words: "if your last played save was 3, Continue should open save 3." Option
3 of the three, as the register recommended — it removes a mechanism rather than adding a special-cased write or
a save field and a migration for a fact the filesystem already keeps.

Nothing was lost with it. The boot check could only ever SEE slot 0 (that is what "before any scene" means), so
every slot it was written for was already covered by `SettleOnOpening`, which runs from `EnterSlot` AFTER
`CurrentSlot` is set and therefore reaches all five. `MostRecentSlot`'s mtime rule is untouched.

`SaveSlotFlowTests.SettlingARunLeftInSlotZeroDoesNotStealContinueFromTheSlotLastPlayed` reflects over
`RunManager`'s `[RuntimeInitializeOnLoadMethod]` members rather than naming the method that used to do this, so
what it pins is "nothing RunManager runs at boot may write a save" — a second boot hook added later under any
name is caught without the test being edited. `OpeningSlotZeroStillSettlesTheRunAPreviousSessionLeftInIt` is the
other half, and it is why removing the check did not make slot 0 the one slot a descent outlives the process in.

Worth keeping for the next fixture of this shape: the test ages slot 0's file by an hour rather than relying on
write ORDER. Two writes inside one system-clock tick carry the same mtime on Windows, and `MostRecentSlot`
breaks a tie toward the lower slot.

Related #123 turns on the same invariant and is still open; this answer does not settle it.

The original finding follows.

Found 2026-09-11 by the `RunManager`/`SaveData` seam finder (F4).

`RunManager.cs:69` is a `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` boot
check that runs with `CurrentSlot` still 0. When slot 0 holds a leftover run it
calls `EndRun`, and BOTH of `EndRun`'s live exits call `Persist()` --
`File.Replace` onto slot 0, whose mtime is then the newest on disk. `45be6e6a`
widened this: the discard arm persists too.

`SaveSystem.cs:224` states the rule the write breaks:

> KEYED OFF THE FILE'S OWN LAST-WRITE TIME, not a field on SaveData.
> CurrentSlot ... cannot answer "which slot did I play last time".

`MainMenuController.RefreshContinue` (`:72-74`) reads
`SaveSystem.MostRecentSlot()` (`:234`), which now returns 0.

**Repro.** Play slot 3. Leave a run in slot 1 (alt-F4 mid-descent). Relaunch:
the main menu offers "Continue (Slot 1)".

**Failing test.**
`SaveSlotFlowTests.SettlingARunLeftInSlotZeroDoesNotStealContinueFromTheSlotLastPlayed`
-- throwaway root, write slot 2 (newest), then slot 0 carrying a run under way,
invoke the boot settle, assert `MostRecentSlot() == 2`. Red today: 0.

**Three options; the third is recommended.**

1. **Preserve slot 0's mtime around the settle write.** Smallest diff, but it
   makes one write a special case and the next writer will not know.
2. **`MostRecentSlot` reads a `lastPlayedAtTicks` written only by `EnterSlot`.**
   Honest -- the question is "which slot did I play", and a field can answer it
   where a file timestamp only approximates it. Costs a save field and a
   migration.
3. **Drop the boot settle entirely** and rely on `SettleOnOpening`
   (`RunManager.cs:102`), which `SaveSlotManager.EnterSlot` already calls for
   every slot including slot 0, AFTER `CurrentSlot` is set (ledger row `R14`).
   Recommended: it removes a mechanism rather than adding one, and the boot
   check's stated job is already done by the other half.

Related: #123 below turns on the same invariant (a run never survives into
gameplay) and its answer should be decided with this one.

### ~~118. Benching a character is undone by the next load~~ — fixed in `82385df6`, together with #93: option 1, the save records the fact — `squadSizeSeen` for the cap, a hole in `selectedCharacterIds` for the seat

**The answer, 2026-09-11.** Owner's call: benching survives a reload. Option 1 (store the fact), not option 2
(benching is within-session and the screen says so) — an affordance weaker than it looks is not the thing to
ship.

Two facts, because the two questions are different and the register said so: a hole-carrying
`selectedCharacterIds` alone does NOT fix this, since the top-up counts ENTRIES against `effectiveMax`.
`squadSizeSeen` is the cap this profile last reconciled against, stamped at the bottom of `Reconcile`, and the
top-up fires only when `effectiveMax` exceeds it. It is 0 on every save written before the field existed —
JsonUtility keeps the initialiser for a missing key — which reads as "has never seen a cap" and so tops up
exactly once, exactly as before.

THE CASE THAT IS NOT THE PLAYER'S CHOICE KEEPS ITS OLD BEHAVIOUR, and this is the part worth remembering: an id
naming content that is gone still closes up and is still replaced, because nobody chose that hole and a squad
silently down to two would hide behind `ActiveSquad`'s whole-roster fallback. That is a second trigger on the
same loop, counted locally from the drop rather than stored, and it is what
`SaveReconcileRenamedCharacterTests` has always pinned.

`ReconcileTopsUpAShortSquadFromTheContentDefault` keeps passing with one added fixture line —
`squadSizeSeen = 1`, a profile written while the squad was still solo. That is the case the top-up was built
for and the only one it still fires in; the register was right that the two tests contradicted each other, and
stating the fixture is what resolves it rather than deleting either.

The original finding follows.

Found 2026-09-11 by the `RunManager`/`SaveData` seam finder (F7). Filed because
the fix CHOOSES between two readings of one field, and a green test asserts the
other one.

`SaveData.Reconcile`'s squad top-up (`:774-800`) tops `selectedCharacterIds` up
to `EffectiveMaxSquadSize()` from `TopUpOrder()` (`:809`). Its header says:

> EXISTING SAVES KEEP THEIR SQUAD, and this loop is why: it only ever ADDS,
> never reorders and never removes anything the player chose.

Adding back the character the player deliberately removed IS undoing what they
chose. `PartyController.SendToBench` (`:390-397`) is Camp-only and refuses only
at `FilledCount == 1`; the player benches one of three, `Persist` writes two
ids, `EffectiveMaxSquadSize()` is still 3, and the next `Reconcile` puts the
benched character back -- in the REAR seat, because that is where `TopUpOrder`
lands.

**Repro.** Hub -> Party -> pick a front-ranker -> Bench (squad shows 2). Quit.
Relaunch. Squad is 3, with the benched member in the rear.

**Failing test.**
`SaveDataSquadOfThreeTests.ADeliberatelyShortSquadIsNotToppedBackUp` -- set two
of three starters, `Reconcile`, assert `ActiveSquadIds().Count == 2`. Red
today: 3. It directly contradicts the green
`ReconcileTopsUpAShortSquadFromTheContentDefault`, which is why this is a
choose and not a restore.

**The question is whether `selectedCharacterIds.Count < effectiveMax` means
"the cap grew" or "the player benched somebody".** Today the code can only read
it the first way.

1. **Store the fact.** The save records that a seat is empty by choice (a
   `benchedCharacterIds` list, or a hole-carrying `selectedCharacterIds`), and
   the top-up fills only seats the cap opened. This also answers #93 -- and
   note that a hole-carrying fix for #93 alone will NOT fix this, because the
   top-up counts ENTRIES against `effectiveMax`, not seats.
2. **Benching is within-session and the screen says so.** The Bench affordance
   keeps working for the current session and the toast says the squad returns
   on next load. Cheapest, and it stops the save lying, but it makes a visible
   affordance weaker than it looks.

Answer this together with #93; they are the same field read two ways.

### ~~119. `SaveData.relicLoadout` is a serialized field with no writer and no reader~~ — fixed in `a7ebbf28`: option 1, the field, its prune, the `RelicLoadout` type and its unit tests are all deleted

**The answer, 2026-09-11.** Owner's call: delete. Option 2 (wire it into per-character relic assignment) is a
design decision with a screen behind it and is not on the roadmap.

No migration and no version bump. `JsonUtility` DROPS an unknown key on load, so an existing save's
`"relicLoadout": {}` is simply ignored — the same no-migration deletion `grantedGold` got in the pass that filed
#112, and it is only safe because the field was empty on every save ever written. A field with real data in it
is the case `SaveData`'s version comment covers instead.

Three comments named the type and would have pointed at nothing: `ContentDatabase.Relics` now says relics are
run-scoped and there is no per-character assignment, `RunSnapshot`'s in-band-discriminator note cites `Wallet`
alone, and `SaveReconcileRenamedCharacterTests`' list of what `Reconcile` prunes drops the entry. A comment
pointing at a deleted type is the drift the deletion was supposed to prevent.

What stands where the field did is a comment, because the hazard was never the field — it was a future reader
seeing a relic loadout on the save and concluding relics were already handled.

The original finding follows.

Found 2026-09-11 by the `RunManager`/`SaveData` seam finder (F8). Same family
as #50, #87 and #112, and stronger than all of them: those are written and
never read, this one is NEITHER written nor read.

`Data/SaveData.cs:187` declares it with a nine-line header explaining its
NAME. `:760-762` null-guards and prunes it on every `Reconcile`. Those are the
only two mentions in the tree outside its own type: zero hits for
`relicLoadout` anywhere else in `Assets/_Project/Scripts/`, and the
`RelicLoadout` type is referenced only by its own unit tests and two comments.

What actually carries a character's relic is `RunSnapshot.relicIds` plus
`FightEncounterAdapter.ResolveRelics`, which is run-scoped rather than
save-scoped -- and per-character assignment, which is what `RelicLoadout`'s
`(characterId, relicId)` shape is for, exists nowhere.

It is written as `{}` on every save, so dropping it is harmless: `JsonUtility`
keeps the initialiser for a missing key.

**Two options.**

1. **Delete it**, its prune and its type, the way `grantedGold` was deleted in
   the same pass that filed #112. One less field to read as "already handled".
2. **Wire it**: per-character relic assignment becomes a real feature and this
   is its storage. That is a design decision with a screen behind it, not a
   cleanup.

Deleting is recommended only if option 2 is not on the roadmap; the field is
inert either way, and the hazard is a future reader assuming it holds something.

### 120. The `Effective*` family has two different null contracts, and `EffectiveStats` has a dead guard

Found 2026-09-11 by the `ContentDatabase` seam finder (F11). No behaviour
changes under either option -- this is about which of two contracts the family
states, and the current state states both.

Fourteen members of the family are null-TOLERANT (`TalentsFor`,
`AvailableSkillsFor`, `TalentGrantedSkillsFor`, `TalentEffects`,
`ModifierEffects`, `ActiveLoadout`, `EffectiveAbilityScores`, `EquippedWeapon`,
`EquippedWeaponPower`, `OrbCost`, `SpentBy`, `EmbersLeftFor`,
`AllegianceRootOf`, `MeetsGates`). Nine THROW on null: `EffectiveStats`
(`.Effective.cs:158`), `BuildSignatureResource` (`:248`), `EffectiveMaxMana`
(`:709`), `MaxManaBonuses`, `EffectiveSkillManaCost` (`:818`),
`EffectiveSkillPowerMultiplier` (`:851`), `EffectiveSkillDisplayName` (`:935`),
`PrerequisitesMet` and `MinSpentMet`. Nothing says which family a member
belongs to; a caller has to read the body.

The dead guard is the sharp end: `EffectiveStats` dereferences
`character.definitionId` unguarded at `:162`, and then at `:220` asks
`if (character != null)` before folding in the reward track. The second test
can never be false. It reads as a null-tolerant method to anybody who scrolls
to it.

All 13 production call sites of `EffectiveStats` and all 3 of `ActiveLoadout`
null-guard before calling (`RunEncounter.cs:176,:214`; `EquipmentOps.cs:54,:79`;
`RewardTrackController.Input.cs:130`; `TalentOps.cs:75,:109`;
`BotRunDriver.cs:1243` and the rest), so nothing is broken today -- this is
recorded as clearance ledger row `C5`.

**Two options.**

1. **Make the nine null-tolerant** like their fourteen neighbours: an early
   return of `StatBlock.Zero` / the mana row / the empty answer, matching the
   house's graceful-degradation posture. Deletes the ambiguity and the dead
   guard together.
2. **Delete the dead guard and state the throw** in each of the nine headers.
   Keeps the throw as the contract -- defensible, because a null `Character`
   reaching `EffectiveStats` is a caller bug and a silent `Zero` hides it.

Whichever is picked, the nine and the fourteen should be labelled, because the
next caller will otherwise read the wrong one.

### 121. Shawn's Wool authors `signatureAbsorbsDamage: false`, and `characters.json`'s own `_readme` says wool eats damage first

> **STILL OPEN — but the file no longer contradicts itself (annotated in `90c41655`).** #127's fix rewrote the
> `_readme` paragraph and took the armour sentence with it. The prose now says plainly that wool does NOT absorb
> today, that `ResourcePool.Absorb` returns 0 while the flag is false, and that the sheep track's
> `SignatureAbsorbs` reward at level 60 is what switches the armour half on. That is option 2 as far as the
> DISAGREEMENT goes, and it deliberately does not touch option 1. What stays the owner's, and is the only reason
> this is still open: whether level 60 is the right place to hand the absorb over, or whether the base row should
> ship `true` and that milestone buy something else. Either way the F5 fix at `165c5746` — a poison tick the pool
> absorbs in full is now counted and said — is dead code until a Shawn actually reaches 60.

Found 2026-09-11 by the `FightSession` seam finder, as a note beside F5. It is
a content/doc disagreement in one file, which is why it is the owner's and not
a fixer's.

`characters.json:34` authors `"signatureAbsorbsDamage": false` on Shawn -- the
only signature pool on a real character today. The same file's `_readme`
(line 2) describes Wool as the balanced template and says:

> it is armour and ammunition out of one pool -- incoming damage eats it
> before his health and his abilities shear it off, so every turn is
> bank-or-spend and both answers are real

With the flag false, `ResourcePool.Absorb` returns 0 at `ResourcePool.cs:266-273`
and nothing is ever soaked: the "armour" half of "armour and ammunition" does
not happen. Every consequence the `_readme` draws from it -- the bank-or-spend
tension, both answers being real -- rests on the half that is switched off.

This is NOT #111. #111 is a PRIMARY pool authoring `absorbsDamage` and being
read by nothing (a code gap). This is a SIGNATURE pool, whose absorb path works
end to end, authoring `false` against its own documentation (a content/doc
disagreement).

**Two options.**

1. **Flip the row to `true`.** Wool becomes armour as written. This is a live
   balance change and it interacts with #124's third lead and with the F5 fix
   already committed at `165c5746` (a poison tick the pool absorbs in full is
   now counted and said -- which is dead code until this flag is true).
2. **Correct the `_readme`.** Wool is ammunition only; the armour sentence
   comes out, and with it the bank-or-spend framing that depends on it.

Whichever way, the two should agree: today the file's prose describes a
character the file's data does not author.

### 122. `ShopStock.SellHeadroom = 12` is provisional, and how many sales one visit should allow is the owner's number

Found 2026-09-11 by the `RunOrchestrator` seam finder (F1). The ARITHMETIC half
of F1 was a restore and shipped at `13b67842`; this is the half no arithmetic
can supply.

`ShopStock.cs:207-213` now derives a shop visit's decision bound from the shelf
itself -- `GearCount 4 + BookCount 3 + RelicCount 3` buys, `SectionCount 3`
rerolls, one Leave -- so the bound cannot drift from the shelf again the way
the old hand-written 12 did. `SellHeadroom` is the one term with no derivation
behind it, because selling is unbounded in principle: a bag can hold more rows
than a shelf has cards.

It was MEASURED rather than guessed. With the ceiling lifted out of the way,
2,050 shop visits across two archetypes over a 200-run batch peaked at 24
decisions in one visit, so 10 would be exactly the observed maximum and 12
leaves two above it. The tail is thin (three visits past 18 for the seller) and
a different seed moves it.

What the change bought, measured before and after at seed 20260911, 200 runs,
`-Shards 2`, GreedyDefensive + Lookahead2:

```
BEFORE   GreedyDefensive   994 visits, 202 capped, 20.4%
         Lookahead2       1054 visits, 331 capped, 31.4%
AFTER    GreedyDefensive   994 visits,   0 capped,  0.0%
         Lookahead2       1056 visits,   0 capped,  0.0%
```

**Two options.**

1. **Keep 12 and stop calling it provisional.** The loop exits on Leave, so a
   generous bound costs nothing on a visit that does not need it, and every
   measured visit fits inside it. The constant's header loses its "PROVISIONAL"
   paragraph.
2. **Set a deliberate sales-per-visit rule** and derive the headroom from it
   ("a visit may sell as many rows as the bag can hold", or "six"), which makes
   the number an answer rather than a bound. Costs one more constant and a line
   of design.

The source says which it is today (`ShopStock.cs:193-206`), so nothing is
hiding; this entry exists so the label comes off deliberately rather than by
being forgotten.

### 123. `Reconcile`'s run-scoped prunes are all unreachable, because a run never survives the process

Found 2026-09-11 by the `RunManager`/`SaveData` seam finder (F5). The intent
evidence CONFLICTS between two comments in the same subsystem, which is what
makes it the owner's.

`SaveData.cs:586-613` prunes run-scoped lists on every load and explains
itself: *"a run is not worth discarding over one dangling id"*.
`RunSnapshot.cs:119-122` says *"a run survives quitting to the main menu and
coming back"*. And `RunManager.cs:54` says the opposite, in capitals:

> A RUN DOES NOT SURVIVE THE PROCESS ... Deliberately not a resume.

The second is the one the code implements. `SaveSlotManager.EnterSlot` calls
`SettleOnOpening` (`RunManager.cs:102`) before the slot's first scene; the boot
check (`:69`) does the same for slot 0; every in-process route back to the main
menu calls `EndRun`. So the load order is Migrate -> Reconcile -> EndRun, and
`EndRun` destroys the run that was just reconciled. No run-scoped value
`Reconcile` prunes is ever read by gameplay.

**The one sharp edge has already been fixed.** `RunSettlement.Settle` reads
`run.bossesKilled` and `run.ledger` on its way to paying for them, and those
were the only two run lists `Reconcile` neither null-guarded nor pruned -- so a
boss id renamed between a crash and the next boot would pay an ember for a dead
id and append it permanently to `defeatedBossIds`. That landed as `adb2acb4`
(the guard and the prune, plus `RunSettlementTests` cover). This entry is about
the other nine prunes and the two comments.

**Two options.**

1. **Build resume.** `Reconcile`'s prunes become live, `RunSnapshot.cs:119-122`
   becomes true, and `RunManager.cs:54` is rewritten. This is a feature, with a
   descent-mid-flight save/restore behind it, and the prune plumbing is already
   in place for it -- `29908245` (the `relicIds` prune) is correct code whose
   stated repro simply cannot occur yet.
2. **Correct both comments** to say the prunes are plumbing kept ready rather
   than a live guard, and leave the code alone. Costs two comment edits, and
   the reachability guard test below goes in with them so the day resume is
   built, the comments are forced back into agreement.

Reachability guard, worth adding under either option:
`RunEndingTests.AnyRunFoundInASlotIsSettledBeforeAnythingCanReadIt` -- a slot
with a run at `step > 0`, `EnterSlot`, assert `HasRun` false. Green today; red
the day resume is built, which is exactly when somebody needs to be told.

Decide with #117: both turn on the same invariant.

### 124. Three questions about what the combat ledger's columns actually count

Found 2026-09-11 by the `FightSession` seam finder (F8, F9, F10). Filed as one
entry because all three are the same shape -- a column or a payout whose name
and whose contents disagree, each with a SINGLE source and an internal
counter-argument, which per the hunt's own rules makes them leads rather than
findings. None has a repro.

**(a) `Ledger.Took` counts overkill, and counts it against a corpse.**
`FightSession.Ledger.cs:135-136` computes `toHealth = amount - result.Absorbed`
BEFORE `CombatMath` clamps at zero health, so a 500-point blow on a 10-HP
target books `DamageTaken += 500`. `CombatLedger.cs:32-36` says the column is
*"What reached this combatant's HEALTH"*, and 490 of it reached nothing.
Separately, `DealDamage` does not refuse an already-dead target; its own
comment (`Ledger.cs:54-58`) accepts that riders can arrive after the body fell
but reasons only about the KILL row, while the ledger row and the pool grant
both still fire for a corpse. One-line fix if wanted:
`toHealth = Math.Min(toHealth, healthBefore)`. Against it: "damage dealt" as a
player-facing number arguably SHOULD count the whole swing. It compounds
`1139107d`'s territory -- these totals bank into `lifetimeDamageDealt` and the
`million_damage` achievement.

**(a) fixed in `25a0333c`**: the owner's call was no overkill in damage taken.
`healthBefore` is read immediately above `CombatMath.ApplyDamageDetailed` --
the only moment the answer exists -- and `Ledger.Took` is handed
`min(toHealth, healthBefore)`. `Ledger.Dealt` is untouched and still books the
whole swing, because "damage dealt" is a claim about the blow that was thrown.
Pinned by `CombatLedgerTests.OverkillIsNotCountedAsDamageTaken` (literal 10).
What that does NOT settle, and what stays open here beside (b) and (c): the
clamp is placed below Berserker's Vest's gate, so whether a rider arriving
after the body fell should fire at all -- the second half of (a)'s own
paragraph -- is still nobody's decision. Only the column moved.

**(b) The "Shielded" column counts signature-pool absorption, not the
`Shielded` status.** `Ledger.cs:159` passes `result.Absorbed`, which
`CombatMath.cs:555` sourced from `SignaturePool.Absorb`. The `Shielded` STATUS
is consumed earlier, inside `DamagePipeline`, and arrives here only as a
smaller `amount` -- so it is never counted in the column named after it.
`CombatLedger.Line.Shielded`'s header (*"What a ward ate"*) and
`RunStatsController`'s "Shielded" row therefore name a different mechanic from
the one they count. Two options: rename the column `Absorbed` (honest, and a
visible string change), or add the status's contribution to it (a second
measurement point inside the pipeline). Note that the one thing this column
SHOULD be able to see -- poison absorbed by the pool -- only became visible at
`165c5746`, and is dead until #121 is decided.

**(c) A kill with no credit pays no per-corpse relic.** `Ledger.cs:305`'s
`if (credit == KillCredit.Nobody) return;` sits ABOVE `RelicsOnEachKill`
(`:309`), so an enemy killed by a poison tick pays no Bounty Hunter Contract,
drops no Inconspicuous Key, and feeds no Amassing Star or Essence Siphon.
`Relics.cs:542-548` argues the opposite for the pairing: *"ONCE PER BODY, which
is a different moment... A bounty is paid for a corpse, so a splash that fells
two pays twice."* Against it: all four take the ACTOR as the holder, and a
poison tick has no actor, so three of the four would no-op anyway. The one that
genuinely differs is the Contract, whose payout is measured off the victim.

The victim's-row half of this same guard WAS a restore with two agreeing
sources and shipped at `f096823e` (`Ledger.WentDown` moved above the credit
gate, so "times downed" can report a party member going down). What is left
here is the relic payout, which is a balance question.


## Findings from the total bug hunt, 2026-09-11 (stage 4)

### 125. `itemsets.json`'s `styleWeights` has no sum rule; ten of eleven sets overspend the ability-score budget

`Assets/_Project/ContentData/itemsets.json`, every set but `bulwark`. `statProfile` is
enforced to sum to exactly 100 (`ItemSetEntryResolver.TryParseStatProfile`); its sibling
`styleWeights` is checked against no total at all (`ItemSetEntryResolver.DeriveScores:392-399`
divides each line by 100 and hands it straight to `GearScaling.AtBaseTier`/`AtTopTier`, which
apply no normalisation or cap). Total ability score granted is therefore linear in the **sum**
of the weights, and the sums shipped are not equal: `bulwark` 100, `court`/`harness`/`silk`/
`steel` 108-110, `wool`/`regalia`/`vellum` 110-120, `leather` 115, `brigandine`/`runeplate`
130-135.

**Measured** (PIL-equivalent arithmetic over the resolver's own formula, `Base 0.4`, slot
weights 1.25/1.05/0.95/0.90/0.85, `TopMultiplier(10) = 1.25^10`, away-from-zero rounding) — a
full five-piece set's tier-10 ability-score points, summed over the six scores:

| set | weights sum | tier-10 score points |
|---|---:|---:|
| `bulwark` | 100 | 19 |
| `court`, `harness`, `silk`, `steel` | 108-110 | 19 |
| `wool`, `regalia`, `vellum` | 110-120 | 21 |
| `leather` | 115 | 23 |
| `brigandine`, `runeplate` | 130-135 | 26 |

A full `runeplate` set is worth 7 more ability-score points than a full `bulwark` set at the
same tier, in the same slots — 37% more, against Shawn's whole authored spread of 66. At tier 0
every value rounds to 0-1, so the divergence is invisible in early play and opens with tier.

**Intent evidence, four agreeing.** `itemsets.json`'s own `_readme` ("a BUDGET the whole set
spends"; "that is exactly how same-tier sets drifted to unequal budgets", the stated reason
`baseStats`/`topStats` were removed; "a piece's own budget differs only by its slot's weight …
never by a different split"); `docs/CONTENT_SCHEMA.md`'s `styleWeights` entry ("this material's
per-score SHARE of the ability-score half of the budget"); the enforced sibling rule on
`statProfile` in the same resolver; `GearScaling.cs:243-245`'s own comment ("a pure style asks
10 of its one stat, and a dual asks 4 and 7 rather than 10 and 10" — written expecting the
weights to sum to ~1.0).

**Two options.**
1. Add a `styleWeights`-sums-to-100 check mirroring `TryParseStatProfile`'s, and re-author the
   ten sets onto 100. The rule itself restores established intent; *which* score each set gives
   up to get back to 100 is a balance number.
2. Leave the sets as shipped and correct the `_readme`/schema language to say the budget is a
   per-score share of a variable pool rather than a fixed one, accepting the current spread as
   intended variety between materials.

### ~~126. `enemies.json`'s golem authors `attackHoldsPosition` on a row where `attackWeight: 0` makes it unreachable~~ — fixed in `62094abf`: option 1, dropped the flag and rewrote the stale comment (no other enemy plain-attacks with a stationary pose yet, so the worked example was removed rather than relocated)

`Assets/_Project/ContentData/enemies.json`, row `golem`: `"attackHoldsPosition": true` beside
`"attackWeight": 0` and one ability (`boulder_slam`, weight 1).
`FightEncounterAdapter.cs:573` adds the plain swing to the draw pool only
`if (source.AttackWeight > 0f)`, so the golem never takes a plain attack.
`FightSession.Enemies.cs:853` reads the flag as
`if (!usingSkill && hasSource && source.AttackHoldsPosition)` — `usingSkill` is true on every
golem turn, so the branch is unreachable for this enemy. The contradiction sits in the code's
own comment four lines above the branch (`FightSession.Enemies.cs:849-852`): "The golem is the
case this exists for: its 'attack' stance is a byte-for-byte alias of its 'cast' stance" — the
one enemy the flag was written for is the one enemy that cannot reach it. No visible symptom
today: `boulder_slam` authors no `approach`, so it holds by the cast route instead.

**Intent evidence.** The `FightSession.Enemies.cs:849-852` comment, plus `RawEnemyEntry
.attackWeight`'s own doc ("0 removes plain attacks entirely") — the two agree on the facts and
disagree on the row.

**Two options.**
1. Drop `attackHoldsPosition` from the golem row and move the comment's worked example to an
   enemy that still plain-attacks.
2. Give the golem a non-zero `attackWeight` so the plain slam it has art for is drawn again (a
   balance change). Either way, `EnemyEntryResolver` could refuse `attackHoldsPosition`/
   `attackApproach` on a row with `attackWeight: 0`, the same "no meaning on that effect" rule
   `SkillEntryResolver` already applies five times (see #145).

### ~~127. `characters.json`'s `_readme` describes Wool as attack-led; the shipped row is per-turn-only~~ — fixed in `90c41655`: option 2, the prose. The owner's reasoning is that Wool is per-turn-led AT BASE and grows, through the reward track and the Black Ram talents

**The answer, 2026-09-11.** The row is right and the paragraph was describing an engine this tree has never had.
Wool builds per turn; capacity and income grow at later levels (`SignatureGainPerTurn` at 10 and 50,
`SignatureCapacity` at 25 and 100, `SignatureGainOnDamageTaken` at 40, twelve more capacity through the filler);
and talents add other income on top — sworn to the Black Ram, `WoolOnHitTaken` and `WoolPerTurnBelowHealth` at
67% and 33% health are what make a damage-taken engine reach him at all. So the base row is the floor of a curve
rather than the whole economy, and the paragraph now says that. `signatureGainOnAttack` stays authored at 0 and
stays sourceless — no `TrackReward` member and no `RawTalentEntry` field pays it — which the paragraph now states
outright instead of leaving to a reader to discover. The absorb sentence was rewritten in the same commit; see
#121 above, which stays open on the balance question alone.

The original finding follows.

`Assets/_Project/ContentData/characters.json`, row `sheep`: `signatureGainPerTurn: 1`,
`signatureGainOnAttack: 0`, `signatureGainOnDamageTaken: 0`. The file's own `_readme` says of
the same resource: "Wool (Shawn) is the balanced template: **attack-led** with a real per-turn
and damage-taken floor" … "His plain Attack pays the most because his Attack is 5 against enemy
Defense of up to 9, i.e. nearly worthless as damage; giving it the best Wool yield turns his
weakest action into a deliberate choice." Both halves are false of the shipped row: on-attack
yield is 0 and the damage-taken floor is 0. What Shawn actually has is per-turn-only — the shape
the same paragraph assigns to a *different* resource ("Insight (Owl) is per-turn-led and almost
nothing else"). Not #121 (`signatureAbsorbsDamage: false`, a different pair of fields).

`signatureGainOnAttack` has no source anywhere in shipped content: authored 0 on the only
character with a signature; `TrackReward` has `SignatureCapacity`/`SignatureGainPerTurn`/
`SignatureGainOnDamageTaken`/`SignatureAbsorbs` but no gain-on-attack member
(`Domain/Progression/RewardTrack.cs`); `RawTalentEntry` offers only `signatureCapacityBonus`/
`signaturePerTurnBonus`. The row has read `1 / 0 / 0 / false` unchanged since `1bd59995`, the
first commit of this tree — the prose was written against an intent the data never carried.

**Intent evidence.** The `_readme` paragraph is the only source (a comment alone is a lead, not
two agreeing sources) — filed as the owner's call on which side is wrong.

**Two options.**
1. Make Wool attack-led as written: author `signatureGainOnAttack` on the row and add a
   `SignatureGainOnAttack` track-reward member so it can grow.
2. Rewrite the `_readme` paragraph to describe Wool as per-turn-led, matching the shipped row.

### 128. `achievements.json:three_bosses` cannot be earned by the shipped roster

`{"id": "three_bosses", "condition": "DefeatDistinctBosses", "threshold": 3}`. `enemies.json`
carries two `isBoss` rows: `forest_warden` (`active: true`) and `hollow_choir`
(`active: false`). `ContentBuilder.cs:447` filters to `Active` when generating the assets
`ContentDatabase.Enemies` reads, so exactly **one** distinct boss can ever be defeated. Three
distinct is unreachable at any depth, on any save, for any number of runs. Nothing gates on it
today (no relic names `three_bosses` in `unlockedBy`).

**Blind spot.** `Tests/PlayMode/Content/AchievementBossValidationTests.cs` already asserts the
`DefeatSpecificBoss` half (that `first_forest_boss`'s `parameter` names an existing `isBoss`
row); there is no companion rule tying a `DefeatDistinctBosses` threshold to the count of
*active* boss rows — the identical question one step up.

**Two options.**
1. Lower `threshold` to 1 (matches what is currently earnable).
2. Leave it as a forward marker for bosses not yet shipped (the way `forest_wardens_tooth` is
   accepted as furniture) and add the companion resolver check so it fails loudly until a second
   and third boss ship, rather than silently sitting unearnable.

### 129. `Resources/Spells/prismatic_orb_water` is 15 frames of committed art nothing plays

All 31 folder/file references out of every `vfx` block in `skills.json` and `enemies.json` were
walked (top-level `path`/`groundPath`, `layers[].path`, `layers[].emitter.path`, each element's
own block, `transform.hit.vfx`) — every one resolves. In the reverse direction, one committed
folder is named by nothing: `Resources/Spells/prismatic_orb_water/` (15 PNGs + metas), with its
own recipe at `Art/Sheets/recipes/prismatic_orb_water.json`. The orb's Water element plays the
five-layer form (`prismatic_orb_water_{charge,core,wake,drops,contact}`) instead. Superseded art
still shipping in the build.

**Blind spot.** `SpellVfxRecipeDriftTests` checks "every folder a skill plays has a recorded
provenance" and "no skill times a beat to a frame its folder does not have," never the reverse —
a folder with a provenance that no skill plays.

**Two options.**
1. Delete the folder and its recipe.
2. Keep it as the single-sheet fallback and say so in the recipe's `_notes`.

### ~~130. Shawn is "he" in the Black Ram strand and "she" in the Fragile Lamb strand~~ — fixed in `44258f3e`: option 1, he. The four player-facing strings are swept; the two CODE comments are not, and that is recorded below rather than quietly left

**The answer, 2026-09-11:** he. `talents.json:sheep_lamb_ward_3` and `skills.json`'s `fleece_ward`, `shatter` and
`gift_mana` now read he/him/himself. Odette's pronouns were not touched — `characters.json` still says owl wears
Shawn's face "until her own portrait is drawn", which is about her.

**The code comments landed afterwards, and there were six of them, not two.** Swept in `b7ee8a40`. The two
this entry names were there -- `:325` ("a share of her Attack") and `:343` ("Her OWN ward") -- but a
whole-file `grep -w` for her/she/hers found four more the finding never censused: `:81` ("a round of hers"),
`:98-99` ("ending her income the moment she finishes her tree"), `:211` ("the thing she was already doing")
and `:455` ("her per-turn payout cap"). All six are the Lamb strand's caster, i.e. Shawn, and all six now
read he/his. The lesson is the census, not the sweep: the finding read the two lines its repro walked
through and stated a count, and the count was wrong by four.

The original finding follows.

`characters.json`'s `_readme` is consistent ("his abilities shear it off", "He starts every
fight at zero"), and the Black Ram talent strand agrees (`sheep_ram_trample_3` "he is heavy, not
infinite", `sheep_ram_stand_3` "leaves him standing on 1"). The Fragile Lamb strand does not,
across five player-facing strings for the same character: `talents.json:sheep_lamb_ward_3`
("**She** can cover **herself** AND do something with the day"), `skills.json:fleece_ward`
("finds wool before it finds **her**"), `skills.json:shatter` ("Every ward **she** has out…"),
`skills.json:gift_mana` ("**She** has more wool than **she** has turns"), and the code follows
it — `FightSession.Talents.cs:325-326,343` ("throws a share of **her** Attack", "**Her** OWN
ward is worth triple"). The strand boundary is too clean to be a typo — it reads as a design
where the Lamb strand was written for someone else.

**Intent evidence.** Single source (the pronoun split itself); the strand boundary's cleanliness
argues design rather than typo, but does not settle which pronoun is correct.

**Two options.**
1. Sweep the Fragile Lamb strand's five strings (and the two code comments) to "he"/"his"/
   "himself", matching the rest of the character.
2. Confirm the Lamb strand was deliberately written for a different character's voice and
   reassign it, leaving Shawn's own strand as Black Ram only.

### 131. `amulet_of_wisdom` is the one starting-kit item with no icon

`items.json` has six `startingStock: true` rows. Five carry an `iconPath` into
`Art/Items/<sheet>/level_1.png`; `amulet_of_wisdom` (Equipment, Necklace) has none. Graceful
degradation is the house style, so this renders, but five-of-six reads as an oversight rather
than a decision. The other two icon-less rows (`health_potion`, `mana_potion`) are consumables,
consistent with each other and not part of this finding.

**Two options.**
1. Commission icon art for `amulet_of_wisdom` to match its five siblings.
2. Leave it on the graceful-degradation path and note in the row why (a stated placeholder,
   rather than an unnoticed gap).

### ~~132. The bog witch is the only monster weak to the element it attacks with~~ — fixed in `8a4c32d6`: neither filed option. The owner re-authored the pair outright — weak to Wind and Arcane, resistant to Water and Earth

**The answer, 2026-09-11.** Not "was it a transposed pair", and not "keep the glass cannon and add a
justification rule". The four elements were chosen by hand on theme: Wind and Arcane cut through a bog, Water and
Earth are the bog. She is no longer weak to her own `attackType`, so the roster's seven-for-seven pattern holds
without her having to resist Poison.

`"weakness": "Wind, Arcane"` / `"resistance": "Water, Earth"` is also the first SHIPPED use of
`RawEnemyEntry`'s comma list, which until now had only ever been exercised by the resolver's own synthetic tests.
The two sets are disjoint, so the both-lists refusal has nothing to catch. `EnemyContentPinTests` gained the pin —
it had no weakness/resistance assertion of any kind before, which is part of why this sat unnoticed — and it also
asserts she is not weak to her own `attackType`, so the exact shape this finding describes fails loudly next time
rather than passing quietly.

**Blind spot B6 in #145 is NOT closed by this.** No resolver rule was added; the one live instance was authored
away. A future row may still name its own `attackType` as a weakness and validate clean.

The original finding follows.

`enemies.json:bog_witch`: `"attackType": "Poison"`, `"weakness": "Poison"`,
`"resistance": "Nature"`. Every other elementally-typed row resists its own attack type: `imp`
Fire/resists Fire, `ember_hound` Fire/Fire, `gloom_moth` Ice/Ice, `mire_lurker` Poison/Poison,
`crystal_bat` Arcane/Arcane, `sable_wisp` Arcane/Arcane, `hollow_choir` Arcane/Arcane — seven for
seven; the bog witch inverts it. Validates clean: `EnemyEntryResolver` only refuses an element
appearing in *both* `weakness` and `resistance`, which this row does not do. Fielded 81,183
times across stage 2's bot batches.

**Two options.**
1. Give the bog witch a resistance to Poison (or a different weakness) to match the roster's own
   pattern, if the inversion was a transposed pair.
2. Leave it as a deliberate glass-cannon caster and add a resolver rule that requires an explicit
   justification comment for a self-weak row, so the next one is a choice rather than a silent
   pass.

### 133. Four enemy stance PNGs nothing can play

Driven stances are the six on `FightSession.Beats.cs:423-430` (`idle`, `attack`, `cast`, `hurt`,
`defeated`, `victory`) plus whatever a skill names in `stance`/`approachStance`/`windupStance`.
Every stance string in `skills.json` resolves against its folder. The reverse sweep leaves four
files no name reaches: `Enemies/golem/guard.png`, `Enemies/rat/guard.png`,
`Enemies/rat/extra.png`, `Enemies/bog_witch/taunt.png`. Harmless (shared canvas, ground lines
agree with their actors'), but art shipping in the build with no route to the screen.

**Two options.**
1. Wire each into a skill's `stance`/`approachStance`/`windupStance` where one fits.
2. Prune the four files and their metas.

### 134. The roster's per-character content is uneven, and Bjorn cannot be given a reward track under the current `TrackReward` model

> **THE AUTHORING HALF IS ~~DONE~~ (`f432a366`). THE MODEL GAP IN THE HEADING STAYS OPEN.** The owner's answer to
> "should Bjorn have a track" was "why not?", so `reward_tracks.json` now authors a `bear` track: the same twelve
> fixed milestone levels, filler summing to 87, and the same Respec / StatPoint-10 / SecondLife spine sheep and owl
> have. It is authored AROUND the gap rather than through it, and every detour is a rule rather than a taste — no
> `Signature*` reward, because rule 5 refuses one on a character with no `signatureId`; no `MaxMana` and no
> `ManaRegen`, because both resolve CLEAN on him and then pay nothing (`fury`'s `capacityRule` is `Fixed` and
> `restoredByManaEffects` is false — blind spot B9 in #145, still open); and no `UnlockSkill` milestone at all,
> because all three `placeholder_brawler_*` skills author `unlockLevel: 1` and are his from level one, so there is
> no skill left for a level to hand him.
>
> **What is still missing is exactly the thing this heading names.** `TrackReward` has no member that pays into a
> primary pool other than mana, so nothing on his track can make his own resource bigger or faster the way sheep's
> `SignatureCapacity` rows do for wool. His track pays max health, stat points and Physical damage instead, and a
> hundred levels of it leave Fury exactly where level 1 found it. The numbers are first values and the row's
> `_comment` says so; the balance pass that authors what Fury BUYS (see `pools.json`'s `fury` `_comment`) is the
> pass that should retune them, and is the natural place to add the missing reward member. Odette's zero talents
> and the unauthored column 2 on every character are also untouched.

| character | skills | talents | reward track |
|---|---|---|---|
| Shawn (`sheep`) | 9 own + 6 book | 43, columns 0 and 1 | authored |
| Bjorn (`bear`) | 3, all `placeholder_brawler_*` | 1, `placeholder_brawler_ward_root` | none — `RewardTrackDefinition.Default` |
| Odette (`owl`) | 1 (`prismatic_orb`) | 0 | authored |

Bjorn's three skills are honest placeholders (`Art/Characters/bear/README.md` records the
decision to keep the ids: "Renaming them would touch saves and tests for no visible gain
today"), all free (`manaCost 0`, `resourceCost 0`), reaching the resolver only through the
zero-start carve-out (`SkillEntryResolver.cs:230-237`) — so nothing in the game spends Fury,
which `pools.json`'s own `_comment` already documents as an interim, not a defect.

**The model gap, not an authoring one.** Bjorn's resource is a pool (`fury`, `capacityRule:
"Fixed"`), not a signature. `TrackReward` has four `Signature*` members and `MaxMana`/
`ManaRegen`, and nothing that pays into a primary pool other than mana — a `SignatureCapacity`
row would be refused by rule (5) on a character with no signature, and `MaxMana` is inert
against a `Fixed`-capacity pool. A Bjorn track cannot be authored today without a new
`TrackReward` member. Odette has zero talents, and column 2 is unauthored for every character
against a skeleton the `_readme` calls "three paths" — recorded here as the same shape of gap,
not separately numbered.

**Two options.**
1. Add a `TrackReward` member that pays into a non-mana primary pool (e.g. `PrimaryPoolCapacity`
   keyed by pool id, or a `Fury`-specific reward), then author Bjorn's track.
2. Treat Bjorn's whole kit (skills, talents, track) as still pre-balance-pass and leave the model
   gap open until that pass, tracking it here rather than plumbing a track reward for a kit not
   yet designed.

### 135. `spells.json` ends at level 9; characters level to 100

`spells.json`'s `_readme` says "one row per character level" and "this is what makes Skill get
stronger and costlier as a character levels 1-9." Nine tiers ship; reward tracks run to level
100, and stage 2's bot observed `levelAtDeath` up to 87. Per the resolver's own rule ("the
highest tier not exceeding the caster's level is used"), the Skill action stops improving at
level 9 and is flat for the remaining 91 levels. Possibly deliberate — the `_readme`'s own
framing ("the curve deepens with level on purpose") reads as though 9 was the ceiling when it
was written, but nothing establishes what the intended top of the curve is.

**Two options.**
1. Author `spells.json` tiers past 9, deciding where the Skill curve should actually stop
   relative to the 100-level track.
2. Confirm 9 is the intended ceiling (Skill deliberately flattens for the rest of a run) and say
   so explicitly in the `_readme`, replacing the "curve deepens with level" framing.

### ~~136. `tools/run_tests.ps1` hardcodes v1 paths that no longer exist~~ — fixed in `5d46970d`: option 1, deleted as superseded by `test.ps1` and `run_tests_parallel.ps1`; `docs/CODE_MAP.md`'s entry went with it

Seven places still name it in PROSE and were left alone as outside that pass's scope: `tools/unity_path.ps1:3`,
`tools/bot.ps1:23` and `:161`, `tools/graphics_tests.ps1:3` and `:76`, `tools/run_tests_parallel.ps1:38`,
`docs/PLAN_BALANCE_BOT.md:164` and `docs/WORKFLOW.md:162`. None of them is a call; all describe a shape ("mirrors
run_tests.ps1", "dot-sourced by run_tests.ps1"). They now name a file that does not exist.

The original finding follows.

`tools/run_tests.ps1:12-13`:
```
$SourceProject = "C:\Games\Prince's Palace"
$TestProject = "C:\Games\Prince's Palace-TestRunner"
```
Every sibling script (`test.ps1`, `run_tests_parallel.ps1`, `bot.ps1`, `preview.ps1`,
`build_content.ps1`, `screenshot.ps1`) derives its project root dynamically via
`Split-Path $PSScriptRoot -Parent`; this is the only one that hardcodes an absolute v1 path.
Verified on disk: neither `C:\Games\Prince's Palace` nor `C:\Games\Prince's Palace-TestRunner`
exists. `git log` shows this file's only commit in this tree is `1bd59995` ("Start keeping the
rebuild's history") — carried into the v2 rebuild verbatim, paths untouched. As written it fails
loudly today (robocopy errors against a missing source, Unity fails to open a missing
`-projectPath`, "No results file produced", non-zero exit) — dead-but-noisy, not silent. But
robocopy's own exit code is never checked (line 19-21, piped to `Out-Null`), so if either path
were ever resurrected on disk (a stray v1 checkout, a restored backup, a second clone) this
script would silently sync from/to that tree and report results with zero indication they are
not about this project — the "tests the wrong tree and reports green" shape this hunt was
looking for. `docs/CODE_MAP.md:266` claimed this script "still works"; corrected in the stage 4
docs commit (see the entry above this section for the sha).

**Two options.**
1. Delete `tools/run_tests.ps1` as superseded by `test.ps1` + `run_tests_parallel.ps1`.
2. Repoint it to derive its project root dynamically like every sibling script, and add a
   robocopy exit-code gate, if it should stay as a documented fallback.

### 137. A balance-bot shard killed after `runs.jsonl` starts writing silently drops its requested-count share from the batch total

A shard that crashes or is killed **after** writing at least one run to `runs.jsonl` but
**before** `RunBatchCore`'s final `File.WriteAllText(... "batch.json" ...)`
(`Assets/_Project/Scripts/Editor/Bot/BalanceBotRunner.cs:339-340`) is merged as if it were a
normal shard, with its declared `runsPerCell` contribution missing from the batch total and no
warning anywhere in the chain.
- `BalanceBotRunner.cs:302-316`: each run's row is written and flushed incrementally as it
  completes, by design — `runs.jsonl` can legitimately be non-empty after a crash.
- `BalanceBotRunner.cs:339-340`: `content.json`/`batch.json` are written only after the whole
  nested loop finishes, after `runs.jsonl`/`traces.jsonl` are already closed — a crash in that
  window leaves `runs.jsonl` populated but `batch.json` entirely absent.
- `tools/bot.ps1:330-345` ("collect") judges a shard failed/succeeded solely on whether
  `runs.jsonl` exists; `batch.json`'s absence is never checked, so the shard is copied into the
  batch dir as a normal, successful shard.
- `tools/bot_merge.py:88-103` (`load_batch`): a missing `batch.json` becomes `{}`, and
  `if head: headers.append(head)` means the empty dict is never appended to `headers` — but the
  shard's runs ARE still appended to `runs` unconditionally, so `merge_headers`' `runsPerCell`
  sum (`bot_merge.py:909`) understates what was actually asked for, silently, by exactly the
  crashed shard's share.
- `docs/BOT_SUMMARY_SCHEMA.md`'s own "Partial batches" section (line 743-750) describes a
  different, pre-sharding mechanism (the runner itself flushing a partial `summary.json`,
  reading as e.g. "143/200") that predates `bot_merge.py` computing the summary — the doc was
  not updated for the sharded case, where a crash can drop a shard's contribution entirely
  rather than degrade it to a visible partial count.

Impact is low-to-moderate: every other `summary.json` number (medians, shares, `bugs[]`) is
computed directly from the runs that did arrive; only the batch's own self-description of how
much work it did (`batch.runsPerCell`, used in the report's subtitle and seed-range label) is
wrong.

**Two options.**
1. Have `bot.ps1`'s collect step warn by name when a shard's `batch.json` is missing (mirroring
   how it already prints a log tail when `runs.jsonl` itself is absent), and have
   `merge_headers` compute `runsPerCell` from each shard's own actual row count when its header
   is absent, so the aggregate self-corrects rather than silently understating.
2. Leave `bot.ps1`'s collect step as pass/fail on `runs.jsonl` alone (a genuinely partial shard's
   real numbers are not corrupted, only its self-description), and instead update
   `docs/BOT_SUMMARY_SCHEMA.md`'s "Partial batches" section to describe the sharded reality
   rather than the pre-sharding mechanism it currently documents.

### ~~138. `Domain/Combat/CombatAction.cs` is dead code with no intent evidence either way~~ — fixed in `5d46970d`: option 1, deleted. The grep was re-run over `.cs`, `.json` and `.md` first, and the only hits outside the file itself were this register and the hunt's own notes

The original finding follows.

`Assets/_Project/Scripts/Domain/Combat/CombatAction.cs` (whole file, 17 lines):
`public enum CombatAction { Attack, Skill, Item, Run, Default }`.
`grep -rn "CombatAction" --include=*.cs --include=*.json Assets/_Project` returns exactly one
line, the enum's own declaration — no caller, past or present, anywhere in the tree, including
Tests. The combat menu's actual action-kind type today is `FightActionKind`
(`Domain/Bot/FightAction.cs:16-25`: `Attack, Skill, Item, Move`), a differently-shaped enum (no
`Run`, no `Default`, has `Move` instead) used throughout the bot/FightSession seam. No comment,
doc, or commit references `CombatAction` outside its own file (repo history is squashed to one
commit, so no earlier trail is recoverable) — the single-source/none case the hunt's own
definitions call out: the file's header is the only description of intent, and nothing
corroborates or contradicts it.

**Two options.**
1. Delete the file — nothing in the tree references it and its own header gives no reason to
   keep it around.
2. Confirm it is scaffolding for a menu-level action distinct from `FightActionKind` (a `Run`/
   `Default` choice the bot-facing enum does not need) and give it a first caller.

### ~~139. `Domain/UiKit/OverlayAnchors.cs` is dead code whose replacement re-permits the exact defect it was built to fix~~ — fixed in `5d46970d`: option 1, the dead file deleted — but ONLY the dead-code half of it

> **THE DESIGN QUESTION IS NOT ANSWERED, AND IT DOES NOT GO AWAY WITH THE FILE.** `OverlayAnchors`'s header argued
> that slot cells sitting on the mannequin make the dossier read as "a stack of boxes with a purple shape behind
> it", and its flanking-column geometry cleared that overlap by construction. The live `DossierLayout` keeps the
> older mannequin-hugging numbers, and `CharacterDossierScreen.cs:883` carries an `AllowOverlap` exemption for
> exactly that overlap. Nobody has yet looked at the rendered screen with real equipped-item icons over the
> mannequin at its sub-10% alpha, which is what BOTH filed options said was needed before deciding. Deleting 194
> lines nothing called does not settle it — it only stops a dead file arguing one side of it.

`DebugMenuScreen.cs` cited `OverlayAnchors` as one of its two examples of an anchors sibling and now cites
`DossierLayout`, which is where the dossier's slot geometry actually lives.

The original finding follows.

`Assets/_Project/Scripts/Domain/UiKit/OverlayAnchors.cs` (194 lines) vs.
`Assets/_Project/Scripts/Domain/UiKit/DossierLayout.cs` (753 lines, live) and its caller
`Assets/_Project/Scripts/Domain/UiKit/Screens/CharacterDossierScreen.cs:833`
(`DossierLayout.SlotAt(slot)`) and `:883`
(`.AllowOverlap("a slot stands on the mannequin and its own leader line")`).

`OverlayAnchors.cs:44-59` describes a PRIOR arrangement that put slot cells directly on the
silhouette's centre line as a defect: "the paperdoll reads as a stack of boxes with a purple
shape behind it rather than as a body wearing things… This is the same defect the armour-stand
art brief already named — 'no internal detail competing with the slot cells' — arriving from the
other side" — the exact phrase MEMORY.md's recorded 2026-08-11 incident names (an armour-stand
generation that put pauldrons, tassets and joint seams where the slot cells land, praised as
"exactly it" before the reservation was walked back). `OverlayAnchors`'s fix: two columns
flanking the figure with 32px gutters, clearing the overlap "by construction rather than by
exemption."

`grep -rn "OverlayAnchors" --include=*.cs Assets/_Project/Scripts` returns exactly two lines: the
class's own declaration, and one illustrative comment in
`Domain/UiKit/Screens/DebugMenuScreen.cs:10` that is prose, not a call site.
`OverlayAnchors.PositionFor` — the method carrying the whole flanking-columns fix — has zero
callers. What is live instead: `CharacterDossierScreen.cs:833` positions each equipment slot via
`DossierLayout.SlotAt(slot)`, whose own header (line 20-24) states plainly that "the mannequin
slots and their leader hairlines are still the handover's own numbers, still positioned against
each other" — i.e. `DossierLayout` deliberately kept the older slot-against-mannequin numbers
`OverlayAnchors.cs`'s header describes replacing — and `CharacterDossierScreen.cs:883` carries a
live `AllowOverlap` exemption for the full slot cell (not just a hairline) on the mannequin,
exactly the category of overlap `OverlayAnchors.cs`'s header treats as the thing to eliminate.

Provenance: `OverlayAnchors.cs` was authored first (`40af5d16`), `DossierLayout.cs` second
(`c85af675`, the newer class) — `OverlayAnchors` kept receiving commits for a while, including
one titled `4be7ce55` ("Get the slot cells off the figure they are meant to describe" — the
exact fix its header narrates), before the screen's real wiring moved fully onto `DossierLayout`
and `OverlayAnchors` stopped being called at all.

**Two options, both requiring eyes on the rendered screen rather than a unilateral change.**
1. `OverlayAnchors.cs` is superseded and safe to delete outright — `DossierLayout`'s
   mannequin-hugging slot placement, with its `AllowOverlap` exemption, is an accepted design
   (low mannequin alpha and the item icon itself may not actually compete with painted detail in
   practice).
2. `OverlayAnchors.cs`'s flanking-column geometry is the better-considered design for exactly the
   reason its own header gives, and `DossierLayout`'s mannequin-hugging placement is a regression
   worth revisiting — check the actual screen with real equipped-item icons over the mannequin
   before deciding.

### ~~140. AUDIT.md's own archival rule was not followed for thirteen struck findings~~ — fixed in `5d46970d`: option 1. #62's and #64's inline write-ups moved verbatim to `docs/AUDIT_STRUCK_ARCHIVE.md`; #66-#76 are recorded there as one block, because they never had a write-up here to move

That distinction matters for whoever runs the cross-check next. #66-#76 were struck the day they were found and
their headings say "full reasoning in the commit message", so there was nothing verbatim to relocate, and
paraphrasing eleven commits into the archive would have created a second and worse copy of what git already holds.
They are listed in the archive under one heading that says exactly that — which is what stops the set-comparison
that produced this finding from reporting them as missing forever.

The original finding follows.

This register's own header states: "A struck finding's full write-up does not stay here: it
moves, verbatim, to `docs/AUDIT_STRUCK_ARCHIVE.md`, and this file keeps only the one-line struck
heading pointing at it." Cross-checked every struck (`~~N.~~`) finding number in this file
against every struck finding number in `docs/AUDIT_STRUCK_ARCHIVE.md`: this file has struck
findings {24, 38, 39, 40, 42, 43, 48, 49, 52, 53, 55, 56, 59, 61, 62, 64, 66, 67, 68, 69, 70, 71,
72, 73, 74, 75, 76, 107, 108} (29 total); the archive only contains {24, 38, 39, 40, 42, 43, 48,
49, 52, 53, 55, 56, 59, 61, 107, 108} (16 total). The 13 missing from the archive — #62, #64,
#66-76 — were spot-checked directly in this file (e.g. #62 at line 488, #64 at 566, #66 at 610,
#76 at 630 as of the revision this was found): several (#62) still carry a full multi-paragraph
write-up inline here, not just a one-line heading, directly contradicting the register's own
stated process. This is a process claim about the register itself, not a behavioural bug, and it
costs nothing to fix either way.

**Two options.**
1. Move #62, #64, #66-76's full write-ups to `docs/AUDIT_STRUCK_ARCHIVE.md` the same way #24 etc.
   already were, leaving one-line struck headings here.
2. Amend the header's stated process to describe what actually happens for this later range (if
   keeping some struck write-ups inline is now the intended behaviour).

### 141. A kindled Talent stone's interrupted settle rests on GameObject Update order between two components, not on TalentController

`TalentLifecycleTests.ASecondStoneKindledMidBeatStillSettlesTheFirst` (stage 3b lifecycle
scenario A18) passes, but for a reason nobody designed. `DriveKindling` writes
`orbs[index].localScale` every frame and writes it back to 1 in exactly one place: the branch
that fires when the beat reaches its end, for whichever stone the controller still owns —
nothing in `TalentController` settles the stone a second kindle abandons. What puts it back is
the orb's own `ButtonPressAnimator`: every orb is a `Ui.Button` (`TalentScreen` says so in
capitals), `UiEmitter.EmitButton` attaches a press animator to every non-hover button, and it
lerps `localScale` toward its base forever. So the property has three writers, and the one that
rescues the abandoned stone is the one `EmitButton`'s own comment two lines away is arguing
about when it refuses to attach two animators at once: "both drive localScale, and a node
carrying both would have them fight every frame." The outcome is correct today and rests on
Update order between two components on one GameObject — the shape `EscapeKey.cs`'s own header
calls "not a thing to build behaviour on." Settling the outgoing stone explicitly in
`BeginKindling` is a one-line fix, but the test is green today, so there is no red output to fix
against.

**Two options.**
1. Accept the current arrangement (it works, and rewriting a passing mechanism with no failing
   test is not itself a fix) and record it here as a known fragility if `ButtonPressAnimator`'s
   attach rule ever changes.
2. Settle the outgoing stone explicitly inside `BeginKindling` so the outcome no longer depends
   on which component's Update runs second, and add a test that would fail if that Update order
   ever inverted.

### 142. Whether time spent in the system menu counts toward playtime is undecided

`PlaytimeTracker` ticks `Time.unscaledDeltaTime`, so opening the system menu (which pauses
`Time.timeScale` but not unscaled time) currently counts as playtime. Nothing in the code, docs,
or git log decides this either way, and the tracker's own header — which explains at length what
it deliberately does NOT count — is silent about pauses specifically.
`GlobalStateLifecycleTests.TimeSpentInTheSystemMenuCountsAsPlaytime` (stage 3b lifecycle scenario
E9) is committed `[Ignore]`d, pinning current behaviour without asserting it is correct.

**Two options.**
1. Keep paused time as playtime (matches "time with the app open," which is the simpler
   definition) and un-ignore the test asserting exactly that.
2. Stop the tracker while the system menu (or any full pause) is open, and write the test the
   other way.

### 143. Fight HUD's tail-survives design contradicts one lifecycle scenario's expectation for a live spell effect

`docs/hunt/SCENARIOS.md` row A10 (`SpellVfxPlayer`, "`Play` while a previous effect is still
drawing" → "the previous effect is cleared, not layered") cannot be honestly asserted against the
shipped design: a `SpellVfxPlayer` draws one thing by construction, and "the previous effect is
cleared, not layered" is exactly the claim
`SpellRendererAndClockTests.ACinderfaultOverALiveTailSharesNoRendererWithIt` and the tail-survives
rule it protects refuse — a live tail is deliberately left to finish on its own renderer rather
than being cleared by the next cast. The scenario's expected-state column describes the opposite
of the shipped behaviour.

**Two options.**
1. Correct `SCENARIOS.md` row A10 to describe the tail-survives rule (a second `Play` gets its
   own renderer; the previous tail is not cleared), matching `SpellRendererAndClockTests`.
2. If "cleared, not layered" is actually the intended design for this specific interrupt case
   (as opposed to the general tail-survives rule), write the PlayMode test for it and reconcile
   it against `ACinderfaultOverALiveTailSharesNoRendererWithIt`, which currently asserts the
   opposite for a different case.

### ~~144. Which order the plate column keeps: a bug fix stopped the HUD column from reordering on a Move~~ -- fixed in `6740399e`: the owner took option 2 on 2026-09-11 ("move"). `_plateOccupants` stopped being scratch for one loop and became the painted-occupancy record itself: `RefreshPcPlates` walks the party in FORMATION order and writes which member it put on each card, `PaintVitals` reads that record instead of looking any index up. During a Move's playback the record says what the screen says (the cards have not been repainted yet, because `AfterResolution` deliberately repaints the menu chrome only); at `OnPlaybackFinished` the repaint moves the cards and rewrites the record in one pass. Option 1's cost was the whole finding -- a turn spent on nothing but position, readable only as two figures sliding past each other on the stage. Option 2 turned out smaller than this entry estimated: the record has no lifecycle of its own to keep synchronized against Move, death or revive, because it is rewritten whole by the one repaint that already handles all three. Pinned by `FightHudSnapshotLifecycleTests.AMoveReordersTheColumnToFollowTheField`, seen red (`Expected: "Beta" But was: "Alpha"`); `4c4bddc3`'s two tests are unaltered and stayed green throughout; full reasoning in the commit message

`4c4bddc3` fixed two real defects in `FightController.Hud.cs`'s `PaintVitals` (a beat painting a
stale maximum, and a Move-reordered party list landing two members' numbers on each other's
cards) by addressing both plates by slot rather than by list index — matching
`RefreshPcPlates`'s own header claim ("a plate belongs to a character for the whole fight") and
`DrawSide`'s stated convention (position walked by rank; drawing/nameplate/flash/fade by slot,
"which belongs to one combatant for the whole fight"). The commit's own message names the
consequence directly: "the HUD column no longer reorders itself when a Move reorders the field"
— a visible behaviour change that was a side effect of the correctness fix, not something the
fix's brief asked for. The commit's own reasoning for taking it anyway is sound (indexing one
half and not the other would fix the round containing the Move and break every round after it),
but whether the column should visually reorder to track live field position at all is a design
question the fix did not settle, it just answered which BROKEN option to pick between (slot for
both, or index for both — never a mix).

**Two options.**
1. Accept identity-stable plates (the shipped fix): the column never reorders after the first
   repaint of a round, and a Move is only visible as the two swapped plates' own content, not
   their position. Matches `DrawSide`'s existing slot-vs-rank convention.
2. Build a painted-occupancy record (a third piece of state recording which VISUAL slot is
   occupied by which combatant at each point in the beat sequence, independent of both rank and
   fight-long slot) so the column can re-order live to track field position while still painting
   the right numbers on the right card. Larger: a new record to keep synchronized against Move,
   death, and revive.

### 145. Eleven content-resolver blind spots found across the shipped `.json` files (B3-B11 plus two prior)

One entry for the resolver gaps the content pass found that let an authored value validate clean
while meaning nothing (or the wrong thing) downstream — recorded together since each is small on
its own and the pattern (a sibling field enforced, a related one not) repeats across resolvers:

- **B3.** `SkillEntryResolver` refuses `bookTier` only when negative; the schema's stated band is
  1-4, so `bookTier: 9` validates and lands in no price band.
- **B4.** `SkillEntryResolver` trims and stores `stance`/`approachStance`/`windupStance` with no
  existence check — only `vfx` paths go through `SpellPresentationPaths`. A typo costs the pose
  silently. (All twelve shipped strings do resolve today — measured.)
- **B5.** `EnemyEntryResolver` accepts `attackHoldsPosition`/`attackApproach` on a row with
  `attackWeight: 0`, where neither can be read (see #126, the golem).
- **B6.** `EnemyEntryResolver`'s `weakness` may name the row's own `attackType`; only
  weakness-intersect-resistance is refused (see #132, the bog witch).
- **B7.** Achievement validation checks `DefeatSpecificBoss`'s parameter against the enemy table
  but has no companion rule checking a `DefeatDistinctBosses` threshold against the count of
  active bosses (see #128, `three_bosses`).
- **B8.** `CharacterEntryResolver` leaves `attack`/`physicalDefense`/`magicalDefense`
  unvalidated (`maxHealth`/`speed` are refused at <= 0; these three are not, so a negative
  defense validates); `princesFavor < 0` is silently clamped to 0 rather than refused;
  `plateTheme` uniqueness is unchecked though the resolver's own comment argues every value is
  some other character's colour.
- **B9.** `RewardTrackEntryResolver` does not stop `MaxMana`/`ManaRegen` on a character whose
  pool is `capacityRule: "Fixed"` (where max-mana sources do not add) or
  `restoredByManaEffects: false`. Not live today (Bjorn has no track — see #134), but it is the
  same shape rule (5) already guards for signatures.
- **B10.** `SpellVfxRecipeDriftTests` (a) has no reverse sweep, so a committed folder no skill
  plays passes clean (see #129); (b) has no byte-identity replay, so a recipe's own `_notes`
  prose is the only record of a known pixel-level drift; (c) `groundImpactFrame` is not walked
  alongside `impactFrame`/`departFrame`/`startFrame` (inert today — nothing authors
  `groundPath`).
- **B11.** `StanceManifestValidationTests`' per-actor median check means a single-stance outlier
  is invisible by construction (benign today — the two outliers found are the documented staff/
  spike/hammer-head cases); nothing anywhere checks `castPoint` against the art at all (verified
  by hand this pass instead).
- **B1** (`ItemSetEntryResolver` not summing `styleWeights`) is filed separately as #125 — cross-
  referenced here, not double-counted. **B2** (`SkillEntryResolver` not checking `scalingAxis`
  against `damageInstances`) was already closed in this same hunt by the resolver refusal
  `d6a7ed0c` adds — cross-referenced here for completeness, not a remaining gap.

**Two options (applies to the group; each individual gap is small enough that a per-row decision
is not warranted).**
1. Work through B3-B11 as a batch of small resolver-hardening changes (each one mirrors an
   existing enforced sibling rule in the same resolver, so the shape of the fix is already
   established per row).
2. Leave them open and rely on the content pass's own measurement/replay evidence (recorded in
   `scratchpad/hunt2/stage4/content/notes.md`'s clearance ledger) as the standing check until a
   resolver-hardening pass is scheduled.

## Findings from the integration pass, 2026-09-11

### 146. The dossier's AlreadyKnown refusal draws on top of the slot it is refusing

Found 2026-09-11 by the #116 fixer and confirmed by the integrator against the screen tree.

#116's fix (`ed24933b`) puts "You already have this spell prepared" into
`DossierSpellsNoBooksLine`, the label the panel already owns. That node was placed for a
different reading: `CharacterDossierScreen.cs:723-727` centres it in the three-slot band
(`Place.At(cx, slotTop - slotBandHeight * 0.5f)`), which is exactly slot 1's row, because in
its original case -- a character whose primary pool refuses books -- the three slot buttons are
not drawn at all and the line stands alone in the space they would occupy. Its own comment says
so: *"WHAT REPLACES THE THREE SLOTS"*.

The AlreadyKnown reading is the opposite situation. The slots are up, the player is looking at
them, and the refusal is about one of them -- so the line lands across `DossierSpellSlot1`'s
name for as long as it shows.

**Nothing catches this.** The node is `.AsDecor()` and `UiAudit`'s `CheckSiblingOverlap` skips
any pair with a decor side, which is deliberate and correct for the case the node was built for
(it overlaps all three slot buttons by construction there, with nothing drawn underneath). The
exemption was earned by one reading and is now doing duty for two.

Not redesigned here. #116's placement was the owner's call and this is a consequence of it, not
a defect in it: option 3 was chosen precisely because it needed no screen-tree change, and every
fix below reverses that.

**Two options** (both named by the #116 fixer).
1. **The slot chip's own label.** The refusal is written into the slot the press would hit,
   which is where the player is already looking and which cannot collide with itself. Closest
   to filed option 1 of #116, and it leaves `NoFreeSlot` -- the other reason the line covers --
   without a home.
2. **A dedicated line of its own**, under the slot band rather than inside it, so the two
   readings stop sharing a node. Needs a screen-tree change and therefore `-BuildScenes`, and
   it re-opens the layout question the shared node sidestepped: the panel's geometry below the
   divider has to absorb the extra row, or the line has to be `Inactive()` decor in space the
   divider already owns.

Repro: `learnedSpells=[{shawn, mud_burst, 0}]`, `unassignedSpellBooks=["mud_burst"]`, select the
row, press slot 1. `DossierSpellSlotsTests` already builds this state -- but it writes no
picture of it, so the overlap was measured across the two captures that fixture DOES write:
in `spell_slots_present.png` the three slot names sit at y = 397, 461 and 525, centred at
x = 385 in a 1920x1080 capture, and in `spell_slots_refused.png` the borrowed line draws at
y = 461, x = 385. Same node, middle slot's row, to the pixel. A fixture that photographs the
AlreadyKnown state is the other thing missing here.

## Findings from the total bug hunt, 2026-09-11 (stage 3, the combat seam)

The rest of this hunt's combat findings were restores and are committed: F9 (`e071ad9a`, the
POWER row's stale attack bonus), F5 (`0aad2cec`, a heal announcing the request), F6
(`a6054cfd`, wool from any hit taken), F1 (`777701ff`, splash by living rank -- a CHOICE the
orchestrator made, flip-able, see its commit message), F4 (`15b1560d`, Magic Marker mana-first
-- likewise), F3 (`c0e72b3a`, the timed slow's missing refresh) and four small restores
(`310c4f43`). The three below need the owner.

### 147. "The Flock" wards exactly one ally, and which one is decided by the field formation

Found 2026-09-11 by the combat finder. `Domain/Combat/Session/FightSession.Talents.cs`,
`ApplyWard`: with `WardSpreadsToAllies` but not `WardSpreadsToWholeParty`, the loop wards the
first living non-caster in `_encounter.PlayerParty` order and `break`s.

Since the positions pass, **party list order IS the field formation**, and the player changes it
with Move. So which ally receives the Flock ward is decided by who happens to be standing
furthest forward -- which nothing states, nothing tests, and no player would guess. The talent's
own header (`:210-213`) explains only the STRENGTH of the spread ("a PERCENTAGE OF THE WARD'S
OWN strength ... one strand tunes the construct, the other decides how far it reaches"), never
who gets it.

There is no intent evidence either way, which is why this is filed rather than fixed. The
contrast that makes it worth filing is `GiftRecipient` in the same file: it had the identical
"the first ally is as good as any" answer and was given an explicit pick when the third party
slot landed, with its reasoning written out -- "the squad is one deep by default and two at
most, so 'an ally' is unambiguous today... It needs a real target picker the moment a third
party slot exists." That slot exists. The Flock is the other place that sentence applies and it
was not revisited.

**Two options.**
1. **Give it a pick, the way Gift: Mana got one.** The natural reading for a damage ward is
   "whoever most needs it" -- lowest current health, or lowest fraction of maximum -- with
   party order as the stable tiebreak, exactly the shape `GiftRecipient` uses. Costs one
   `OrderBy` and one test; makes the talent's value legible and stops a Move silently
   redirecting it.
2. **Say the rule out loud and keep it.** "The ward spreads to the ally standing nearest the
   front" is a defensible design -- it makes formation matter and rewards the player for
   putting the right character forward -- but it has to be in the talent's description, not
   only in a `break`. Costs a line of content and a comment.

Either way a test pins it; today nothing does, so the answer can change under a refactor without
anything going red.

### ~~148. The one conditional RNG draw in the enemy loop, on a branch the player's Root creates~~ — fixed in `44d94bc0`, beyond both filed options: the owner's call was that Root cancels the swing outright rather than redrawing into anything (legal skill or not), which drops the RNG draw entirely and changes real gameplay, not just seed comparability

Found 2026-09-11 by the combat finder. `Domain/Combat/Session/FightSession.Enemies.cs`, in
`ResolveEnemyAction`: a plain-swing commitment made before the enemy was Rooted is re-drawn with
`EnemyAbilityDraw.Pick(effective, _rng?.NextFloat() ?? 0f)`.

That draw happens ONLY when the player rooted the enemy after its intent was committed -- a
branch whose frequency is decided by how the player is playing. The file states the opposite
rule three separate times, most plainly at `PrepareEnemyIntents`: "THE ROLL STILL HAPPENS EITHER
WAY, and it has to: the draw's position in the RNG stream is what keeps a seeded run
reproducible, and a preview that skipped the draw would give the fight a different shape from
the one being previewed". The same file refused exactly this shape once already for the target
re-pick and took a forfeit instead.

**The impact is narrower than the rule sounds**, and that is why this is low rather than urgent:
a replay fed the same player actions still reproduces, because the same actions produce the same
branches. What breaks is seed-to-seed comparability between two runs that differ in whether a
Root landed -- which is the balance bot's determinism lens, not a player-visible bug.

**Two options.**
1. **Pass a literal `0f`, the way `RootedEnemyHasNoLegalAction` deliberately does** -- its own
   comment: "passing a literal 0f (not `_rng.NextFloat()`) costs nothing from the seeded stream
   -- this is a query, not a commitment". The redraw would then always take the first legal
   entry rather than a weighted one, which is a real behavioural narrowing for a monster with
   two legal skills and worth saying so.
2. **Write the exception into the header.** State that the Root redraw is the one draw whose
   frequency depends on play, and what that does and does not cost. Cheapest, and honest, but
   it leaves the invariant with a hole in it that the next reader has to re-derive.

### ~~149. Lucky Deck's red card says "a moment to recover" even when nothing recovered~~ — fixed in `62094abf`: option 2, the owner's call was to drop the line rather than measure it

Found 2026-09-11 by the combat finder as a suspected sixth instance of F5 (`0aad2cec`), checked
by the F5 fixer and found NOT to be one. `Domain/Combat/Session/FightSession.Relics.cs`,
`LuckyDeckHeal`: it heals a percentage of max health and restores a percentage of max mana, then
announces "{name}'s Lucky Deck turns up a red card - a moment to recover." The line carries **no
number**, so there is nothing for it to misreport and F5's fix does not reach it.

What remains is the weaker variant of the same question: a holder at full health with a full
primary pool draws the red card, nothing moves, and the log still says a moment to recover. The
precedent either way is in the same neighbourhood -- `RestorePartyMana` prints "finds nothing to
restore" when nothing landed, while plenty of flavour lines say something happened without
claiming a figure.

**Two options.**
1. **Measure it like everything else.** `HealAndCount` now returns what landed and
   `CombatMath.RestoreMana` always did, so the line can say "a moment to recover" or "and it is
   no use to him right now" on the same evidence the other announcements use. Two lines.
2. **Leave it.** It is a flavour line about drawing a card, not a claim about a number, and the
   card WAS drawn. Nothing is measurably wrong.

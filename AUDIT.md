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
the hunter's. Full write-up: `docs/archive/BUG_HUNT_2026-09-08.md`.

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

### ~~93. `PartyController.Persist` compacts a benched seat's hole, promoting the next member unchosen~~ — fixed in `82385df6`, answered together with #118: the save carries the hole, and `PartySeatGapRoundTripTests` is un-`[Ignore]`d; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`
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
`docs/archive/BUG_HUNT_2026-09-08.md`), but fixing the tool does not fix the fact that nothing runs it.
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
explicit pathspec); nothing lost. See `docs/archive/BUG_HUNT_2026-09-08.md` (f) for the incident.

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
(`StatusEffects.cs:441-468`, `DurationClock` -- renamed from `IsSpentByTheTurn`
in the spell expansion's milestone A, 2026-09-20) because a one-turn taunt
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

### ~~106. Loading a fight scene over a live one logs an error, because a status badge pops on a panel that is already inactive~~ — fixed in `16eeb5aa`: `BeginAppearancePop` sets the badge's final scale directly instead of starting a coroutine when the screen cannot host one. The guard is NOT the one proposed below and the difference is measured: at that teardown repaint `isActiveAndEnabled` still reads **true** — nothing called `SetActive`, the whole SCENE is unloading — and the scheduler refuses anyway, so the error still fired from the same line with the proposed guard in place (stack in the runner log). `gameObject.scene.isLoaded` is the flag that has already flipped. Asserted by `FightTeardownLifecycleTests.LoadingAFightOverALiveOneWithEverythingInFlightLogsNothing`, which abandons a real round (a popup mid-rise, a death fade mid-fade, a lunge mid-tween) with no `LogAssert.ignoreFailingMessages` anywhere in it, and was seen red with this exact message. The four Fight fixtures that tolerate it across a scene swap can now drop that line; none was touched here.; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`
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
work and the untuned Fury economy: `docs/archive/BUG_HUNT_2026-09-10.md`. This hunt has TWO register
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
`docs/archive/BUG_HUNT_2026-09-11.md`. The manifest row for each is in
`docs/hunt/MANIFEST.md`.

### ~~113. What an extra turn should re-pay: today it re-pays everything, and Black Ram Mode loses two of its three turns in one round~~ -- fixed in `c477205f`: the owner took option 1 -- a bonus action is the SAME turn and re-pays nothing, so `GrantTurnStart` split into `OpenTurnFor` (unchanged) and `ReopenTurnFor` (`_locks.ResetTurn`, `TickPrimaryPool`, and the two recomputes that read them), and the three `[Ignore]`d repro tests are green with a control beside them; full reasoning in the commit message; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`
### ~~114. The reward roll draws from Equippables, not Offerable, so the six starting-kit items are offerable rewards~~ — fixed in `0ec7d8fc` (content) and `5fc4eb51` (code): the owner took neither filed option — the starting kit is DELETED, and `Candidates()` asks `ContentDatabase.Offerable` rather than re-typing a predicate, so there is no third universe left for the two filters to disagree about; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`
### ~~115. The shop screen produces refusals it may not guess at, and displays none of them~~ — fixed in `bbe23ff6`: option 1, one `PaintRefusal(ShopResult)` into the existing `detailLabel`, and `Reroll`/`SellRow` stopped discarding their results; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`
### ~~116. The dossier swallows AlreadyKnown, which the plan says is where the player finds out~~ — fixed in `ed24933b`: option 3, "You already have this spell prepared" in the spell panel's existing status line, and the green would-fill preview is suppressed for a book the character already carries; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`
### ~~117. The boot settle rewrites slot 0, so Continue points at the wrong slot~~ — fixed in `b8242045`: option 3, the `[RuntimeInitializeOnLoadMethod]` boot check is gone and `SaveSlotManager.EnterSlot` -> `SettleOnOpening` is the whole of the rule's enforcement; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`
### ~~118. Benching a character is undone by the next load~~ — fixed in `82385df6`, together with #93: option 1, the save records the fact — `squadSizeSeen` for the cap, a hole in `selectedCharacterIds` for the seat; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`
### ~~119. `SaveData.relicLoadout` is a serialized field with no writer and no reader~~ — fixed in `a7ebbf28`: option 1, the field, its prune, the `RelicLoadout` type and its unit tests are all deleted; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`
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

### ~~126. `enemies.json`'s golem authors `attackHoldsPosition` on a row where `attackWeight: 0` makes it unreachable~~ — fixed in `62094abf`: option 1, dropped the flag and rewrote the stale comment (no other enemy plain-attacks with a stationary pose yet, so the worked example was removed rather than relocated); full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`
### ~~127. `characters.json`'s `_readme` describes Wool as attack-led; the shipped row is per-turn-only~~ — fixed in `90c41655`: option 2, the prose. The owner's reasoning is that Wool is per-turn-led AT BASE and grows, through the reward track and the Black Ram talents; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`
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

### ~~130. Shawn is "he" in the Black Ram strand and "she" in the Fragile Lamb strand~~ — fixed in `44258f3e`: option 1, he. The four player-facing strings are swept; the two CODE comments are not, and that is recorded below rather than quietly left; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`
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

### ~~132. The bog witch is the only monster weak to the element it attacks with~~ — fixed in `8a4c32d6`: neither filed option. The owner re-authored the pair outright — weak to Wind and Arcane, resistant to Water and Earth; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`
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

### ~~136. `tools/run_tests.ps1` hardcodes v1 paths that no longer exist~~ — fixed in `5d46970d`: option 1, deleted as superseded by `test.ps1` and `run_tests_parallel.ps1`; `docs/CODE_MAP.md`'s entry went with it; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`
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

### ~~138. `Domain/Combat/CombatAction.cs` is dead code with no intent evidence either way~~ — fixed in `5d46970d`: option 1, deleted. The grep was re-run over `.cs`, `.json` and `.md` first, and the only hits outside the file itself were this register and the hunt's own notes; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`
### ~~139. `Domain/UiKit/OverlayAnchors.cs` is dead code whose replacement re-permits the exact defect it was built to fix~~ — fixed in `5d46970d`: option 1, the dead file deleted — but ONLY the dead-code half of it; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`
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

### ~~144. Which order the plate column keeps: a bug fix stopped the HUD column from reordering on a Move~~ -- fixed in `6740399e`: the owner took option 2 on 2026-09-11 ("move"). `_plateOccupants` stopped being scratch for one loop and became the painted-occupancy record itself: `RefreshPcPlates` walks the party in FORMATION order and writes which member it put on each card, `PaintVitals` reads that record instead of looking any index up. During a Move's playback the record says what the screen says (the cards have not been repainted yet, because `AfterResolution` deliberately repaints the menu chrome only); at `OnPlaybackFinished` the repaint moves the cards and rewrites the record in one pass. Option 1's cost was the whole finding -- a turn spent on nothing but position, readable only as two figures sliding past each other on the stage. Option 2 turned out smaller than this entry estimated: the record has no lifecycle of its own to keep synchronized against Move, death or revive, because it is rewritten whole by the one repaint that already handles all three. Pinned by `FightHudSnapshotLifecycleTests.AMoveReordersTheColumnToFollowTheField`, seen red (`Expected: "Beta" But was: "Alpha"`); `4c4bddc3`'s two tests are unaltered and stayed green throughout; full reasoning in the commit message; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`
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

### ~~147. "The Flock" wards exactly one ally, and which one is decided by the field formation~~ - fixed in `ee0d7727`: neither filed option, and not a patch on this talent. The owner's call was that the engine deciding for the player is "just stupid", so nothing auto-picks any more: `SkillTargeting.SingleAlly` enters the same Target depth `SingleEnemy` does, on the party rack, and Ward plus all three Gifts go through it. Who may be picked is `Domain/Combat/AllyTargeting`, one predicate per effect, read by the plates, by `CastSkill`'s refusal and by the bot alike; `FightSession.EligibleAllies` is the filter over it, beside `EligibleTargets`; `CanReach` split into `CanReachEnemy`, `CanReachAlly` and a side-blind core, and the ally side takes no `Reach` at all because nothing stands between a caster and his own squad. The Flock's own rule (owner, 2026-09-15) is now: warding himself spreads nowhere, warding somebody else sends the share back to him -- isolated in `FlockSpread` so it is one edit to retune. `GiftRecipient` is gone; its two orderings and the ward's "his own back first" moved to `Domain/Bot/AllyTargetSelection`, the only caller left that must choose with no hand on the mouse. Full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`. **EXTENDED 2026-09-20, spell expansion milestone C:** the picker now takes a required pick COUNT rather than always exactly one -- `MenuDepth.Target` gains `RequiredPicks` and a pick list, `Back()` drops one pick at a time before leaving the depth (the owner's "Cancel steps back a level" rule applied to a level that did not exist then), and `FightSession.CastSkillOnPicks` is the two-pick door. How many picks is a property of the EFFECT (`SkillEffects.PicksRequired`), never an authored field: one more content number would let a row ask for three picks no resolution can use. Palace Passage is the one caller at count 2; every other command in the game is count 1 and takes literally the same path.

### ~~148. The one conditional RNG draw in the enemy loop, on a branch the player's Root creates~~ — fixed in `44d94bc0`, beyond both filed options: the owner's call was that Root cancels the swing outright rather than redrawing into anything (legal skill or not), which drops the RNG draw entirely and changes real gameplay, not just seed comparability; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`
### ~~149. Lucky Deck's red card says "a moment to recover" even when nothing recovered~~ — fixed in `62094abf`: option 2, the owner's call was to drop the line rather than measure it; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`
## Findings from the fleece-spell removal, 2026-09-15

### 150. Shawn's reward-track level 30 has no replacement for static_fleece

Found 2026-09-15 while removing `static_fleece` and `golden_fleece` from `skills.json` (two
book spells that failed `docs/SPELL_DESIGN_STANDARD.md`'s universality test -- both depend on
Shawn's own Wool, so no other character could ever equip them as a book, the same problem that
document already named at `:762-768`).

`reward_tracks.json`'s sheep track named `static_fleece` at its level-30 milestone
(`UnlockSkill`). Every one of a track's twelve milestone levels must carry exactly one entry
(`RewardTrackEntryResolver`'s rule 1), so the level could not simply be left empty without
failing the content build. It now carries a placeholder -- `StatPoint`, amount 2, marked with an
inline `_comment` -- that keeps the track valid and commits to nothing.

**This is the owner's call, not a design decision made here.** The previous milestone (level 25)
already grants `SignatureCapacity 5`; the sheep track's other `UnlockSkill` milestones sit at 30
and 70 (`cinderfault`). A real replacement could be: another `UnlockSkill` naming a different
sheep skill, a bigger `StatPoint`/`MaxHealth` grant matching the milestone's weight, or an
`ElementalDamagePercent` row (Shawn's level-1 element set is Nature, per `SkillDamageTypes.
AtLevel1` and the existing level-45 milestone). Whatever is chosen, `RewardTrackContentPinTests`
needs a new pin to replace `SheepLevel30UnlocksStaticFleece` (removed in this same change).

### ~~151. Provoke's "bellows at nothing in particular" line cannot be reached~~ - fixed in `399d6c1d`: the owner's call was that the line is dead copy; the `provoked == 0` branch is gone and the reach refusal is the one path; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`

### ~~152. Wards are a percent of the next hit with no timer; the owner may want absorb pools~~ - fixed in `339ce102`: the owner answered shields; a ward is a pool of shield points, stacking, on a one-turn clock (the stacking and the clock are the follow-up calls of the same day -- see 8f005266); full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`

### ~~153. A one-turn ward is never visible on its caster's own turn, and Shatter cannot reach one~~ - closed in `214e7ad7`: the owner's answer was to move the tick to the END of the wearer's turn, so a ward is visible on the turn it protects and Shatter can reach one cast the turn before; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`

### ~~154. The three relic wards stack on themselves every turn, on a whole-fight duration~~ - closed in `214e7ad7`: NOT a fix -- the owner chose unbounded stacking on purpose, so `RunicWardPointsCap` is deleted and no ceiling replaces it anywhere; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`

## Findings from gamepad-navigation phase 2, 2026-09-17

### ~~155. Map and Fight lost "Cancel opens the system menu" via keyboard/gamepad~~ - fixed in `4eab048b`: one shared open path (`SystemMenuController.OpenOnCancel`) called from the hub's, the map's and the fight's own Cancel handlers -- Fight hangs it on `MenuDepth.Root`, the only depth where its own `Back()` consumes nothing; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`

### ~~156. Party (gamepad-navigation phase 2, step D) was not attempted~~ - closed in `467770ef`: seats and roster cards are navigable Rails, Submit is the Button's own onClick (so pick-up and drop go through the same `ClickSeat`/`ClickCard` the mouse uses), and the missing per-pane Cancel seam is `INavCancelClaim` -- the active pane gets first refusal on the press, Party claims it while carrying, nothing else implements it; the visual capture found that Party has no mouse-hover treatment at all, which is an owner call recorded in the plan's status header; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`

## Findings from gamepad-navigation phase 3a, 2026-09-17

### 157. Two Combat/Stage tests fail on this tree, unrelated to phase 3a's own changes

Found while gating the Map screen commit (`7a16ce67`) of phase 3a's rollout:
`FightPlayableTests.AStillDrawingSwingCarriesTheFigureAndBringsItBack`
(`Tests/PlayMode/Combat/FightPlayableTests.cs:168`, "the attacker never left its mark, so a
single-drawing swing showed nothing at all", expected greater than 1.0f, got 0.0f) and
`StageFormationTests.AMoveCrossesBeforeTheEnemySwingsAndTheSwingFindsTheNewFront`
(`Tests/PlayMode/Combat/StageFormationTests.cs:191`, "the ogre never swung", expected >= 0, got
-1) both fail, consistently and identically, across four separate `tools/run_tests_parallel.ps1`/
`tools/test.ps1` invocations.

**Verified not caused by this plan's own changes**: `git stash`-ing every phase-3a file
(`MapController.cs`, the Map/CancelOpensSystemMenuTests test files) and re-running the same two
classes against the bare Main Menu commit (`d39d955b`) reproduces the identical two failures with
identical messages. Neither test touches Hub, Main Menu, Map, Talent or Shop; both are
Combat/Stage swing-timing tests, an area this plan does not read or write.

**Confirmed pre-existing on main, predates the branch entirely** (phase 3b job 0,
2026-09-17): the previous entry's "most likely trigger" (the Main Menu commit's incidental
`build_content.ps1` run) is ruled out. `git checkout f20d76de` -- the gamepad-nav branch's own
base commit, one commit before `ec81b29f` (phase 3a screen 1, the first commit this whole plan
made) -- and running `tools/test.ps1 FightPlayableTests,StageFormationTests` there reproduces
both failures verbatim:
```
FAILED: PrincesPalace.PlayModeTests.FightPlayableTests.AStillDrawingSwingCarriesTheFigureAndBringsItBack
  the attacker never left its mark, so a single-drawing swing showed nothing at all
  Expected: greater than 1.0f
  But was:  0.0f

FAILED: PrincesPalace.PlayModeTests.StageFormationTests.AMoveCrossesBeforeTheEnemySwingsAndTheSwingFindsTheNewFront
  the ogre never swung
  Expected: greater than or equal to 0
  But was:  -1
```
Same PlayMode totals both times (`Total: 11  Passed: 8  Failed: 2  Skipped: 1`). Since
`f20d76de` sits before any gamepad-navigation commit -- phase 2 and phase 3a's own content
rebuilds included -- these two tests were already failing on `main` before this plan touched
anything; no commit on `gamepad-nav` regenerated content or changed combat code in a way that
could have introduced this. Confirmed with `git stash push -u` / `git checkout f20d76de` /
`git checkout gamepad-nav` / `git stash pop` on the main tree (working-tree line count identical
before and after: 173). Left unfixed -- out of scope for the gamepad-navigation plan; `combat`
is the area to chase the actual swing-timing regression, and whether it's stale content or a
real formula bug is still open. Not this plan's call to make.

### ~~158. Three Hub-covering modals still do not push their own NavContext (RelicDraft, Glossary, Debug menu -- corrected from four)~~ -- fixed in `ccb1b08d`; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`
### 159. ~~Main Menu's Manage Saves and reset-confirm modals are mouse-only~~ -- fixed in `232f310b`; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`
## Findings from gamepad-navigation phase 3b items 1 and 2, 2026-09-17

### ~~160. Nothing on the dossier or the Reckoning shows WHERE the stick is standing~~ -- fixed in `65ad5325`, REOPENED BY HARDWARE AND CLOSED DIFFERENTLY in `ce405df9`; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`
### ~~161. The dossier's spell-books panel is mouse-only, and its nav rows stay Move-reachable underneath it~~ -- fixed in `7ea33bca`; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`
## Findings from gamepad-navigation phase 4, items 3 and 4, 2026-09-18

### ~~162. `JourneyToFirstFightMouseTests` (and once, `JourneyHubToTalentsMouseTests`) fails in a large batch, never alone~~ -- fixed in `bd6f80af`: `JourneyFixture.MoveMouseTo` aimed the scripted pointer at the target's PIVOT rather than its rect centre, which on the Hub's gate (pivot 0.5/0, rect y:0) is the rect's own inclusive bottom edge, and the hover scale-up the pointer's own arrival starts then shifts that edge a fraction of a pixel away from the frozen pointer before the MouseDown frame arrives.; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`
### ~~163. A click on a Navigation.Mode.None Selectable (a stepper button, a background click) drops the row's own selection to the context's Entry, not back to the row~~ -- fixed in this pass's own `NavigationInputModule.cs` change; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`
### 164. A scripted mouse cannot reliably reach a dot scrolled out of the reward track's own masked viewport

Found writing item 3's own mouse-only regression for phase 4 item 2 segment 8 (the reward track).
`RewardTrackController.Input.cs`'s `WireNodes` wires each dot's `HoverIndex` to call `ScrollTo`
on pointer-enter, the mouse's own equivalent of the pad's `SelectIndex`-driven reveal -- but
`RewardTrackLayout`'s own fly-in on open centres the rail on the character's CURRENT level, while
the Rail's own declared SELECTED entry stays `FirstLevel` regardless (a pad `Move` steps the
graph, not the viewport, so it needs no dot to be on screen at all). A scripted
`RectTransformUtility.WorldToScreenPoint` aimed at a dot far from the currently-shown region
computes a screen point outside `TrackViewport`'s own masked (`RectMask2D`) area, and the pointer
never reaches it -- `content.anchoredPosition.x` simply never moves from wherever the fly-in left
it, confirmed by trying both a click (`Press`) and a bare hover (`HoverIndex`) at the identical,
unchanged wrong value.

**Worked around, not fixed**: `JourneyVictoryToRewardScreensMouseTests.ReachingTheRewardTrack_...`
hovers a dot beside the character's own CURRENT level (inside the fly-in's own visible window)
instead of `FirstLevel + 1`, which is genuinely reachable by a real pointer and still proves
`ScrollTo` fires on hover. Left open rather than claimed as reachable: a scripted mouse in this
harness cannot yet drag the rail's own ribbon-seek (`BarSlider`-based, `RewardTrackController.
WireRibbon`) to bring an arbitrary far level into view first, which is what a real player would
do -- nothing production-facing is wrong here, this is a test-harness capability gap, matching
`PartyGamepadVisualCaptureTests`' own already-stated hedge on the same raycast technique
("whether a scripted pointer resolves against this scene's ScreenSpaceCamera canvas is not this
capture's own claim").

### 165. `FightTeardownLifecycleTests.TwoPopsOnOneBadgeLeaveItAtItsRestScale` can exit its own wait at a value its own assertion rejects

Seen once under a full `tools/run_tests_parallel.ps1` (2026-09-18) and not reproduced since,
including on an immediate re-run of the same gate. Not a behaviour bug and not related to whatever
change is in flight when it fires -- the numbers are an off-by-one-float inside the test itself:

```
the badge never reached its rest scale
Expected: 1.0d +/- 0.0010000000474974513d
But was:  1.0010000467300415d
```

The wait loop exits once `rect.localScale.x > 1.001f` is false, and `1.001f` widened to double is
`1.0010000467300415`; the assertion that follows allows `1.0 +/- 0.001d`, which is
`1.0010000000474975`. The float literal is the larger of the two by 4.7e-8, so there is a sliver of
values the loop treats as settled and the assertion treats as unsettled, and the pop's own lerp
lands in it whenever a loaded run's frame timing puts it there. Nothing about the badge, the pop or
the teardown is wrong when this fires.

The fix is to make the two agree -- one tolerance, read by both, rather than a `float` literal in
the loop and a `double` tolerance in the assertion. Not done here: this was found while gating an
unrelated change in `ui`/`run`, the class lives in `combat`, and changing a test's own arithmetic
deserves its own gated pass rather than a drive-by. Recorded so the next person who sees it does not
spend the afternoon looking for a real regression in the pop animation, which is where the message
points and is not where the problem is.

### 166. The D-pad bindings are correct for one controller by documentation, and unverified on any

Hardware round 1 item 4 bound the D-pad by adding a Joystick Axis entry on the 6th axis to
`Horizontal` and one on the 7th to `Vertical` in `ProjectSettings/InputManager.asset`, which is
the Xbox-on-Windows mapping for the hat. `GamepadAxisBindingTests` pins the entries, pins the
zero-based `axis:` reading against the left stick's own two entries, and pins that the dispatcher
reads those two axis names -- all of which are facts about this repository and all of which it can
check.

Three things it cannot, stated here rather than asserted around:

1. **That axes 6 and 7 really are the hat on the owner's own Xbox pad.** Unity's legacy input maps
   a controller's axes through the platform driver; a different pad, or a different driver, can
   number them differently. The failure mode is specific and worth recognising: if the entries land
   on the trigger axis instead, the stick will behave normally and a trigger pull will move the
   selection.
2. **That up on the hat reads positive.** The new `Vertical` entry carries `invert: 0` while the
   left stick's carries `invert: 1`, because the stick's raw Y is negative when pushed up and the
   hat is documented as the other way round. If up and down come out swapped on the pad, flipping
   `invert` on that one entry is the whole fix.
3. **The DualSense, at all.** It is documented as reporting the same two axes under Windows in
   Unity's legacy system, but nothing in this repository can confirm that and the test file says so
   in its own header rather than implying coverage it does not have. It needs the pad in hand.

Not a defect and not fixable from here -- it is the part of item 4 that only the owner's own
hardware can close, and it belongs beside section 12 item 4's standing hardware-acceptance call
rather than inside it.

**ADDENDUM, hardware round 2 (2026-09-19): three more bindings with the same shape.** Round 2
added a `SystemMenu` axis and two trigger axes to `ProjectSettings/InputManager.asset`, and every
one of them is documentation-correct and hardware-unverified for exactly the reason above.

1. **Start is `joystick button 7`**, with `escape` as the `positiveButton` and the pad on
   `altPositiveButton` (`InputManager.asset`, the `SystemMenu` entry). Button 7 is Xbox Start under
   Windows in Unity's legacy system. The failure mode is cleanly separable from every other one
   here: **Start does nothing while Escape still works** means the button NUMBER is wrong and
   nothing else is, because both drive the same axis and only one of them is a guess.
2. **The DualSense's Options button is deliberately NOT bound.** It is believed to report as
   `joystick button 9` under Windows, which on an Xbox pad is the right-stick click AND is already
   spoken for -- `Enable Debug Button 2`. Binding a button that means two different things on two
   pads, one of which opens the debug overlay, was judged worse than a DualSense owner having no
   Start until the pad is in hand. It needs the pad, same as item 3 above.
3. **The triggers are `TriggerLeft`/`TriggerRight` on the 9th and 10th joystick axes** (serialized
   as `axis: 8`/`axis: 9` -- `GamepadAxisBindingTests`' own documented zero-based convention, the
   same offset that file already pins for the left stick), feeding `INavSectionStrip`. DualSense L2
   and R2 are believed to be axes 4 and 5 and are not bound, for the reason above.
   The failure mode worth recognising is #166's own item 1 restated: **a trigger pull that moves
   the selection** means the axis numbers landed on the sticks.

### 167. A held stick no longer repeats at all, which is a real loss on a long list

Hardware round 1 item 3 (`f6672a26`) answered "the joystick only is wonky... it feels almost
random" by giving the ordinary dispatcher branch the same armed edge the Fight branch already had:
a press counts once and does not count again until the stick has been back below
`NavigationInputModule.MoveThreshold`. That commit states the cost rather than hiding it, and this
records it as a live owner call rather than leaving it in a commit message nobody greps.

What changed: before, a held stick auto-repeated, badly -- an off-centre rest position repeated at
ten selections a second because it never armed Unity's own 0.5s repeat DELAY, which is the whole of
what the owner reported. Now it does not repeat at all. On the reward track's rail and the dossier's
pack window that means one flick per step, and on a long rail that is a lot of flicks.

The fork, if the owner wants repeat back: hold-to-repeat can be rebuilt on top of the armed edge
rather than instead of it -- arm on the crossing, then re-fire on a real-time cadence the module
owns itself, rather than deferring to `StandaloneInputModule`'s machinery, whose repeat gate is what
misbehaved. That is a new mechanism with its own test cost (section 10's own note that
repeat-cadence testing has to wait real frames), not a flag, so it is not done speculatively.
Predictable-and-slower beat unpredictable in the owner's own report; whether it still does after
living with it is the owner's call.

## Findings from hardware round 1's visual pass, 2026-09-18

### 168. The pad-focus indicator was five indicators and three absences, and its size was a property of the control

The owner's play-test produced four complaints that read as four bugs on four screens -- "the
selector on the start descent is huge and looks weird", "the gold halo (e.g. in party screen) is
way too strong", "a player can't see where they're going in the character sheets screen: no obvious
selectors", and the talent tree having no visible focus at all. They are one defect.

Every one of those screens had grown its own answer to "where is the pad standing": Hub's five
building halos, Party's seat and card halos (plan section 8's own "candidate treatment, not a
verified solution"), the dossier's three per-group halos (#160 above), the Reckoning's rarity halo
brightened to double duty, and `ThemedButtonState`'s `_isSelected` arm for anything themed. Three
screens -- Talents, Options and Fight -- had nothing.

**Why no amount of tuning could have worked**, which is the part worth keeping: every halo was a
radial glow SIZED TO THE CONTROL it sat behind, so its apparent size was a property of the control
rather than of the indicator. On a 620x620 gate it is a 620-unit bloom; on a Party seat it is a
slab of gold; over a dossier cell's icon and name it is a tint. No pair of numbers is small on the
first and visible on the last. A marker of FIXED size beside the control is the same size
everywhere by construction, which is the property the halo could not have at any setting.

Fixed in `ce405df9` (`Core/FocusMarker.cs`, `Domain/UiKit/FocusMarkerPlacement.cs`, driven from
`NavigationInputModule.ShowFocusOn`). `Core/SelectHaloPainter.cs` had no callers left and is
deleted.

### ~~169. Fight's ATTACK verb wore the branch-is-open plate at rest, forever~~ -- fixed in `0995736b`; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`
### 170. The focus marker attaches to its target's own canvas, and "root canvas, last sibling" would have been wrong

Not a defect in shipped code -- it is a defect the capture pass caught in the marker itself before
it shipped, recorded because the reasoning is reusable. The first implementation parented the
marker to `canvas.rootCanvas` and made it the last sibling, on the ordinary uGUI rule that later
siblings draw on top. `FightScreen` wraps its whole HUD in `Ui.NestedCanvas("FightHud", 1000)` with
`overrideSorting`, so every root-canvas child draws UNDER all of it however late a sibling it is.

The symptom is the one worth recognising: the Reckoning's marker reported itself shown, at the
right coordinates, with the right target -- and was nowhere in the picture. No assertion in this
project could have caught that; the capture did. The rule is now "the target's own nearest canvas,
last sibling", which cannot be wrong for the reason that a canvas covering the marker covers the
control it points at too.

### ~~171. OWNER'S CALL: ATTACK still wears the Primary ring at rest, and that is a separate decision from #169~~ -- closed in `b12a1682`: the owner made the call ("the attack button in the fight menu is still glowing always") and the ring is gone from the verb column entirely; full write-up in `docs/AUDIT_STRUCK_ARCHIVE.md`
### 172. OWNER'S CALL: the marker's edge is derived from the control's aspect, not authored per control

`FocusMarkerPlacement.EdgeFor` puts the marker to the LEFT of anything wider than 1.8:1 and ABOVE
everything else. That is one rule for every screen, computed from the rect, and it cannot drift --
the alternative, an edge declared per control, would be roughly a hundred new declarations each of
which can be forgotten or contradict its neighbour.

The cost, visible in the captures: on Talents the arrow lands on top of the orb's own name label
(`marker_talents_orb.png`), and on the Reckoning it sits under the "CHOOSE ONE" heading
(`marker_reckoning_card.png`). Both are legible and neither is wrong, but a control whose label
hangs above it would be better served by a marker to its left. If the owner wants that, the
proportionate change is an opt-out on the kit node (a `UiNode.FocusEdge` hint the emitter carries
through), not a table of exceptions in `FocusMarkerPlacement`.

---

## Findings from hardware round 2, 2026-09-19

### 173. OWNER'S CALL: Start is absorbed by every modal, so it is not "open the menu from anywhere"

`NavContext.RaiseSystemMenu` (`Domain/UiKit/NavContext.cs:144-149`) returns false and does nothing
when the top context declares no `systemMenu` handler, and only four contexts in the tree declare
one: Hub (`HubController.cs:170`), Map (`MapController.cs:190`), the menu itself
(`SystemMenuController.cs:243-245`) and Fight (`FightController.Input.cs:1571`, through
`NavContext.ForFight`). Every other context absorbs the press --
`GlossaryController.cs:261`, `DebugMenuController.cs:277`, `RelicDraftController.cs:293` and
`MainMenuController.cs:138` all construct a `NavContext` with `cancel:` and no `systemMenu:`.

So while a hub-covering modal is up, Start is silently eaten. That is not a bug in any of those
four files: a context that is top is the thing the player is talking to, and a modal that lets a
button reach past it is the failure mode contexts exist to prevent.

But it means the shipped behaviour is "Start opens the menu from a ROOT", not "from anywhere",
which is the plainer reading of the owner's ask. Closing that gap is a real mechanism and not a
flag: every intervening context would have to be suspended (or popped and restored) while the menu
is up, and something has to own putting them back in the right order when it closes. That is a
design with a lifecycle, so it is recorded rather than improvised.

**One member of the list is not the same case and is called out so it is not "fixed" by mistake.**
The main menu's own modals absorb Start too, but the MainMenu scene carries no system menu at all
-- `ScreenRegistry.WireSystemMenu` is called from three places only (`ScreenRegistry.cs:125`, `:583`,
`:856`: Fight, Map, Hub). There is nothing there for Start to open, so nothing there is being
absorbed FROM. The same is true of Shop, Talents and the Reckoning.

### 174. OWNER'S CALL: Start over a Party carry puts the character back down rather than being ignored

`SystemMenuController.cs:245` wires the menu's own context `systemMenu:
CloseUnlessTheActivePaneClaimsIt`, and that method (`:413-419`) asks
`ActivePaneCancelClaim()` -- the same `INavCancelClaim` lookup Cancel goes through -- before
closing. `PartyController` is the only implementor, and it claims while a character is in hand.

So Start pressed over a Party carry spends itself undoing the carry, and a SECOND Start closes the
menu. The alternative would be for Start to ignore the claim and shut the menu on an open
transaction, leaving a picked-up character in an ambiguous state.

Recorded because it is a deliberate choice rather than a consequence, and because it is the one
place Start and Cancel are not independent. Reusing the identical check (rather than writing a
second one that could answer differently) is the reason this is a two-line method instead of a
policy.

### 175. OWNER'S CALL: `ThemedMenuState.Primary` now has exactly one production user, and "recommended" may be the wrong word for it

With ATTACK's ring dropped (#171 above), the only production caller passing
`ThemedMenuState.Primary` is `SaveSlotController.cs:108`:
`ThemedButtonState.ApplySelection(slotButtons[i], facts.Filled, ThemedMenuState.Primary)` -- so
EVERY FILLED SAVE SLOT wears the gold ring, up to three at once. Its own comment calls it "the gold
'recommended action' ring".

Three filled slots cannot all be the recommended action. Either the save screen is using a
recommended-default treatment to mean "this slot has a run in it" (a different fact, which deserves
a different visual), or `Primary` is misnamed and is really a generic "this control is live"
emphasis, in which case the name has been lying at every call site it ever had.

Not changed unasked: the enum value, `PrimaryGlowColor`/`PrimaryGlowAlpha` and
`ThemedButtonState.cs:285`'s `IsSelectedHalo` arm are all still wired, and picking between "rename
the state" and "restyle the save screen" is a design call. It is recorded now because a state with
one user is the moment to ask, not later when there are five again.

### 176. OWNER'S CALL: while the pad is inspecting, Up/Down walk the actor ring, so changing verb costs a press first

`FightController.Input.cs:1317-1333`: the explicit
`IFightNavigationTarget.MoveFocus` asks `IsInspecting` first and routes to `InspectStep(delta)`,
so BOTH axes walk the ring while the marker stands on a monster. The verb column is only reachable
again after a Left at the ring's head (`InspectMove` -> `InspectStep` -> `StepInspectRing` returns
-1 -> `LeaveInspect`, `:1795-1802` and `:1831-1853`) or a B
(`IFightNavigationTarget.OnBackPressed`, `:1349-1361`, which calls `LeaveInspect` BEFORE
`_menu.Back()` for exactly this reason).

The argument for it is in the code and is a good one: if the vertical axis quietly moved the verb
selection while the player was reading a monster, B would return them to a verb they never saw
themselves choose. The cost is that reading a monster and then picking a different verb is three
presses (Left/B, then Up or Down, then A) where it could be two.

The alternative is one branch in `MoveFocus`'s inspect arm -- vertical leaves inspect and moves the
column, horizontal walks the ring -- which is a smaller change than it sounds because both paths
already exist. It is not taken unasked because it trades the "B returns you where you were"
guarantee for a press, and which of those the owner wants is the owner's to say.

### 177. STATED DEVIATION: `ProcessFight` spends one axis per frame, so no diagonal can step the column and enter inspect in one gesture

`NavigationInputModule.ProcessFight` (`:552`) reads `Horizontal` and `Vertical` directly rather
than through `BaseInputModule.DetermineMoveDirection`, so it has to apply that method's
dominant-axis rule for itself, and it does (`:596-615`: `verticalLeads = |v| >= |h|` at `:604`, then one
armed branch or the other, never both). Vertical wins a tie.

This is not a defect -- without it a diagonal push would step the verb column AND step onto the
actor row on one gesture, which is two moves the player made one motion for. It is recorded as a
DEVIATION because it is a real behavioural limit that the Fight branch does not share with the
ordinary branch by accident but by a rule written out twice, and because the next person to wonder
why a diagonal "only did one thing" should find the answer here rather than in a stick's dead zone.

### 178. OWNER'S CALL: nothing on the Talents screen names the open character or which constellation is open

The owner's 2026-09-19 removal list took `TalentCharacterName` and `TalentPathName`
("CONSTELLATION x OF x - x KINDLED") off the panel, along with the fill bar, the "CHOOSE A STAR"
prompt and the violet container (`TalentScreen.BuildPanel`; `UiStrings.TalentPath` and
`UiStrings.TalentPickPrompt` are deleted outright). Carried out as given.

What is left to answer "who is this and where am I" is the tree silhouette itself and the path
pager's greyed state -- `TalentController.cs:518-519` sets
`prevPathButton.interactable`/`nextPathButton.interactable` from `ConstellationLayout.CanStep`, so
a dark left arrow means "this is the first constellation" and nothing else says so in words. The
character pager is not even that: `:504-513` hides both buttons outright while there is one
character, which is today's case for every character but Shawn.

Not re-added, because every one of those nodes was named in the owner's own cut list and adding
back a smaller version of a thing that was just removed is the worst of both. Recorded so that
"there is no way to tell which constellation you are in" is a known consequence of a decision
rather than a bug someone finds later.

### 179. Four nav-link asymmetries the round-2 audit found and did not fix

Found by walking every `RuntimeNavWiring.Link` call after the Map's own missing reverse was fixed
(`RuntimeNavWiring.LinkBoth` exists to make that class of omission impossible where a two-way edge
is meant). These four are the remaining candidates. None is fixed here: each is a one-way edge
that MIGHT be deliberate, and guessing wrong wires a step where the player never took one.

1. **`DebugMenuController.cs:248-249`** -- `nextPageButton -Down-> closeButton` with no reverse.
   `closeButton -Up-> prevPageButton` at `:249` is the only way back up, so Down-then-Up off the
   NEXT pager arrow lands on the PREV one.
2. **`GlossaryController.cs:234-235`** -- the identical pair, identically asymmetric. The two files
   share this block almost verbatim, which is why they are one finding.
3. **`RewardTrackController.Input.cs:96`** -- every dot links `-Down-> collectButton` and nothing
   links back up, so the collect button has no Up at all: reaching it is one-way, and the only way
   off it is whatever Unity's automatic navigation happens to find.
4. **`SystemMenuController.cs:366`** -- the selected tab links `-Down-> ActivePaneEntry()` with no
   Up back to the tab. This one is the most likely to be deliberate (a pane owns its own Up), and
   is listed so that "deliberate" is a decision somebody made rather than an omission nobody
   noticed.

**A fifth, listed separately because it is inconsistency rather than absence.**
`TalentController.cs:1178-1185` wires three doors from the right path-arrow into the panel --
`Up -> nextCharacterButton`, `Right -> investButton`, `Down -> respecButton` -- and the three
return by three different rules: Invest and Respec both come back `Left` (`:1184`, `:1185`), while
the character pager does not come back to the arrow at all, going `Down -> investButton` instead
(`:1182`, `:1183`). The block's own header explains the outbound directions ("each in the direction
that thing actually sits") and says nothing about the returns, so it is unclear whether the pager's
is a considered choice or the one that was forgotten.

### 180. OWNER'S CALL: the hub ring is derived from `HubAnchors` x, which reverses the owner's own literal wording

`HubController.cs:252-262` builds `hubRingOrder` by sorting the five staged controls on
`HubAnchors.PositionFor(...).X` (and `HubAnchors.Gate.X` for the gate, which is not a `Plot`), so
the ring is Principality (-673), Talents (-349), Gate (0), Relics (295), CharacterSheet (628).

That CONTRADICTS the owner's 2026-09-18 phrasing, which round 1 (`a9f89ebe`) hand-typed verbatim:
"from the gate, Left goes Principality then Talents". Those words and screen-x do not describe the
same ring, because Talents physically sits BETWEEN Principality and the gate. Round 1 resolved the
conflict in favour of the words and recorded the deviation; the owner's 2026-09-19 report -- "press
left twice you are left middle" -- is what that build actually feels like, which is the hand-typed
order overshooting the nearer building and landing back on it.

So round 2 reverses the call: the heuristic wins here because the two things the owner said
disagree with each other, and only one of them is a description of what they saw. The other stated
expectation ("Right off Relics reaches Character Sheet") was already the x-order answer and is
unchanged; only the left arm's two buildings swap.

Recorded rather than treated as settled for one reason: it is now possible for a future
`HubAnchors` `Depth`/`Lateral` edit to reorder the ring silently, which is the price of deriving it.
`HubRingAdjacencyTests` (EditMode, no scene) pins the resulting adjacency literally so that such a
move shows up as a failing test naming both buildings rather than as a play-test complaint three
weeks later.

## Findings from hardware round 3, 2026-09-19

### 181. Bloom is configured, tuned twice, and until this round could not touch a single pixel

`PipelineBuilder.cs:167` overrides `bloom.threshold` to `1.05f` on
`MenuVolumeProfile` (`:27`), and `:174` sets `intensity` to `0.285f`. Every other
precondition is met -- `supportsHDR` on the pipeline asset (`:98`), the camera's
post-processing pass, and `SceneBuilder` putting the canvas in `ScreenSpaceCamera`
rather than `ScreenSpaceOverlay` (`:134-138` says so in as many words, and calls
that the half of the URP migration that buys anything).

**A Canvas `Image` cannot produce a pixel above 1.0.** Sprite textures are LDR and
top out at white; a `CanvasRenderer`'s vertex colour is a `Color32`, so
`image.color` clamps at 1 before the shader ever runs. A threshold of 1.05 is
therefore above everything the UI can draw, and the bloom override has been inert
since it was written. `:153-158`'s comment -- "so the lantern glows and the palace
bloom while ordinary button faces and body text do not" -- describes an effect that
has never occurred: the lantern and the palace are Canvas Images like everything
else.

Nothing crossed the threshold until this round's `Resources/Shaders/UISpellGlow.shader`,
which multiplies AFTER the sample (`SpellVfxPlayer.cs:167`, `:179`) and is selected
per spell layer by the new `glow` field. So the first pixel that has ever bloomed in
this game is a spell layer authored with `glow`, and `cinderfault`'s two layers
(`glow: 1.0` and `1.3`) are the only ones today.

**Two owner's calls, stated separately because they pull opposite ways.**
(a) Whether to lower the threshold so ordinary bright art blooms again -- the number
was already 0.90 once and `:161-166` records why it was raised (bloom cannot tell
near from far, and the whole painted skyline bloomed as hard as the near lantern).
Lowering it re-opens a decision that was made on a picture.
(b) Whether `intensity` 0.285 is now too weak. That number was tuned by eye
(`:169-173`) against a frame in which NOTHING was blooming, so it was measuring
nothing. The first real look at it is a frame with a `glow` layer in it.

The third option, and the cheapest: leave both numbers alone and give the lantern,
the palace and any other art that is supposed to glow the same shader treatment the
spell layers now have. That keeps "what blooms" an authored property of the thing
rather than a threshold everything is measured against.

### 182. A formation spell layer whose beat struck nobody spends a pooled renderer on a zero-sized box

`SpellPerformance.cs:103` declares `public bool Placed = true;`, and
`SpellPerformancePlayer.cs:436` (`if (!instance.Placed) return;`) is what stops a
layer with nowhere to go from taking a pool member.

`FightController.SpellVfx.cs:203` (`PlaceOne`) handles that correctly for every
placement but one. Its `on == null` branch at `:232-235` sets `Placed = false` and
returns, with a five-line comment explaining exactly why. But the `formation` branch
at `:208-212` returns BEFORE that check is reached, and `PlaceOnFormation`'s own
nobody-to-stand-on exit (`:389`, `if (stood.Count == 0) return;`) returns without
touching `Placed`. Its comment -- "A fault of no width would be a zero-sized
graphic, so draw none" -- states the intent; the flag that implements it is the one
thing not set.

The result is a `Placed = true` instance with `Box`/`To`/`From` at their zero
default: one pooled renderer spent, for the layer's whole lifetime, on a
zero-sized box at the stage origin. Reachable whenever a cast-level formation layer
outlives the last body under it, or is aimed at an off-stage or synthetic target.

Not fixed here because the one-line fix (`instance.Placed = false;` before that
return) belongs with a test that covers the case, and this round's spell work was
already gated. Small, local, and the next spell commit should take it.

### 183. The reel is four times slower, and the two legs that moved are the two that are motion

The owner, 2026-09-19: "Reeling happens way too fast: it should happen 4x as slow."
`FightBeatPlayer.cs:1464` is the factor (`ReelSlowdown = 4f`), spread over the legs
of a recoil that are MOVEMENT rather than stillness:

| Leg | Was | Is | Why |
|---|---|---|---|
| Push out (`StageActorAnimator.LungeSeconds`) | 0.055s | 0.055s | unchanged -- it is what puts the body where the flash and the damage number already are, so stretching it slides the impact frame off them |
| Dwell (`RecoilDwellBeats`, `:1467`) | 0.2025s | 0.81s | the motionless beat at full extent |
| Spring back (`RecoilReturnBeats`, `:1473`) | 0.16s | 0.64s | the recovery, and the leg an earlier attempt left alone |

**Stated in BEATS, not seconds** (`:1467`, `:1473`), which is the other half of the
fix: the dwell was always a fraction of `BeatHoldSeconds`, and replacing it with a
flat number would have taken the reel out of the beat's unit system, so the speed
preset and the beat budget could move underneath it. The reel is 3.2 beats at every
speed. `RecoilReturnBeats` is written as `(0.16f / BeatHoldSeconds) * ReelSlowdown`
rather than as the literal 1.4222f so the number reads as "the old constant, in
beats, times four" -- `FightBeatPacingTests` pins the products literally (0.81,
0.64, 1.8, 1.4222, 1.505).

**`RecoilDistance` (`:1431`, 45f) is deliberately NOT 4x.** A body shoved four times
as far reads as a knockback, which is a different move from a flinch.

**The dwell is stepped by `Time.deltaTime`, not `WaitForSeconds`**
(`StageActorAnimator.PlayRoutine`). The tweens either side of it step by deltaTime,
so under a pinned `Time.captureDeltaTime` -- how every capture fixture in this
project records a strip -- they advance exactly one recorded frame per frame. A
`WaitForSeconds` is measured against the engine's own clock instead, so a hold long
enough to matter parks the figure mid-move for whole strips. At the old 0.2025s this
was invisible; at 0.81s the struck figure sat 45px off its mark for every frame of a
2.4s strip.

**And a later beat aimed at a reeling body now waits for it.**
`FightBeatPlayer.StillReeling` (`:1384`, with `_reeling` at `:1347` and `IsReeling`
at `:1391`) is the predicate; `PlayBeats` gates on it at `:566`, in the same `while`
as the formation walk. `TravelFor` measures the stand-off against both figures'
MARKS, which silently assumes the bodies are standing on them -- true at a 0.4175s
reel by the time the next beat opened, false at 1.505s, and the second and third
attacker of a round were landing blows in 45px of daylight.

### 184. OWNER'S CALL: the settle gate is narrow (this beat's two figures), and widening it costs about 4.4s a round

`StillReeling` (`FightBeatPlayer.cs:1384`) asks only about `beat.Actor` and
`beat.Target`, because those two are what `TravelFor` measures. A THIRD figure still
easing home from its own hit while this blow lands elsewhere is not gated -- that is
what a reel four times slower than the beat looks like, and it was asked for.

**Measured, not estimated** (`:1368-1372`): a beat's tail after the impact instant is
about 0.625s (hit-stop + `SettleAfter` + `BeatGapSeconds`), so a 1.505s reel has
about 0.88s left when the very next beat opens, 0.255s when the one after that does,
and nothing by the third. The narrow gate therefore pays **up to 0.88s, and only when
a blow is aimed at a body that was just hit** -- focused fire and sweeps, not every
beat.

Widening it to the whole stage is the stronger claim and pays that 0.88s on EVERY
damaging beat after the first: **about 4.4s on a six-beat round**, which more than
doubles it. `StillReeling` is the one place to change if that is wanted.

The alternative considered and rejected: cutting the spring-back short when the next
beat needs the mark. That would make the reel's length depend on what happens after
it, so the same blow would read differently in a duel and in a crowd. Waiting costs
pacing and says so; it does not cost the reel its shape.

### 185. "Playback finished" no longer implies "the stage is at rest", and five fixtures had to learn it

A reel is 1.505s and a beat is about 0.75s, so a figure struck on beat 1 is still
moving during beat 3 and past the end of the round. `FightBeatPlayer.IsPlaying`
answers about the beat QUEUE; `StageActorAnimator.IsPlaying` answers about the
BODY, and after this round they are different questions.

Four PlayMode fixtures were changed to wait on the animator rather than on the
player: `FightBeatPlayerFixtureTests.cs:95`, `FightBeatPlayerLifecycleTests.cs:146`,
`FightPlayableTests.cs:211` and `MeleeStandOffCaptureTests.cs:469`. (The brief for
this round said five; `StaticPilotStageCaptureTests` was changed for other reasons
and gained no such wait. Recorded rather than rounded.)

**The fifth piece of fallout was found by the gate, not by the fixers.**
`TransformFlashTests.TheRevertBeatFlashesTheActorsOwnSilhouette` failed with "the
transform never expired inside eight rounds, so there was no revert to look at" --
and the transform HAD expired. Its inner loop bounded one round of playback at a
literal 1200 frames; with the 4x reel and `StillReeling` a round now costs about
2350 (measured over three rounds: 1439 / 2349 / 1628). The bound was truncating
playback mid-round, the outer loop then clicked its next turn while the fight was
still busy, that click was dropped, and the revert beat was still QUEUED when the
fixture gave up on it. Fixed here by naming the number (`RoundFrameBudget`, 6000,
used by both loops in that file) and writing down what it measures.

**The general form, for the next fixture that trips on this:** a bound measured in
FRAMES pins how long playback takes, which is a number this project keeps moving on
purpose. Every other wait in these fixtures is a `Time.realtimeSinceStartup`
deadline, which does not. `TransformFlashTests` was the only frame-counted one left.

### 186. A typed hit is two pulses now, because one tinted pulse read as repainting the monster

`StageHitFlash.cs` draws the struck figure's own silhouette over itself. Before this
round a typed (elemental) hit was ONE pulse in the element's colour, and at the
alphas it was using the eye read it as the creature changing colour rather than as
something landing on it.

It is now white first, then the element:

| Pulse | Peak alpha | Hold | Fade |
|---|---|---|---|
| White | 1.0 | `HoldSeconds` 0.05s (`:26`) | `FadeSeconds` 0.16s (`:27`) |
| Element | `TypedPeakAlpha` 0.6 (`:47`) | `TypedHoldSeconds` 0.06s (`:48`) | `TypedFadeSeconds` 0.30s (`:49`) |

`DamageType.Physical` takes the white pulse alone (`:143`, `:170`) -- there is no
element to name, and a second pulse the same colour as the first is just a longer
first one.

`HitFlashPixelTests` samples the second pulse at 0.6 rather than at 1.0, which
leaves a luminance margin of roughly 0.45 over the fixture's background. That is a
real margin but a smaller one than the white pulse's, and it is the first thing to
suspect if that fixture ever goes intermittent over a lighter background.

### 187. `BeginStatusTickBeat` duplicates `BeginBeat`'s constructor to skip one line of it

`FightSession.Riders.cs:638` opens a beat for a status tick, and `:644-654` is a
copy of `FightSession.Beats.cs:22-32` -- the same `new CombatBeat { Actor, Target,
PreSnapshot, Approach }`, differing only in what it puts in the fields.

**The reason it is not one call is sound and is written down** (`:611-619`):
`BeginBeat`'s last line is `NotePoolActivity(actor, PoolActivity.Action)`, the seam
that tells a decaying pool "this turn was not idle". A tick is not an action its
holder took -- it is something done TO them at the top of a turn they have not spent
yet -- so routing it through `BeginBeat` would quietly stop wool decaying on any turn
its owner happened to be poisoned.

**The duplication is still duplication.** The fix is to extract the constructor half
of `BeginBeat` into `FightSession.Beats.cs` (a `NewBeat(...)` that builds and assigns
`_recordingBeat` and nothing else), and let `BeginBeat` be that plus
`NotePoolActivity` while `BeginStatusTickBeat` is that plus its own element and
stance work. Left undone because it is a pure refactor in a file the round was
already changing for behaviour, and the two should not land in one commit.

### ~~188. `TickReport` names poison specifically, so the second damage-over-time will not fit~~ — fixed in spell-expansion milestone E

`StatusEffects.cs:764` and `:774` declare `PoisonDamage` and `PoisonAbsorbed` by
name, `:789`'s "nothing happened" predicate walks them by name, and
`FightSession.Riders.cs:513`, `:528`, `:537`, `:539`, `:542` and `:554` all read
them by name. A burn or a bleed -- the obvious next two -- has nowhere to be
reported.

The shape this wants is per-element rather than per-status, and half of it already
exists: `StatusEffects.ElementOf` (`:745`) answers what element a status deals its
damage in, generically, and `BeginStatusTickBeat` (`FightSession.Riders.cs:661-663`)
already uses it to colour the tick's beat. So the beat layer is already generic and
only the REPORT is not.

Not fixed here: a second DoT is not authored yet, and the shape of the replacement
(a small list of `(element, dealt, absorbed)` versus a pair of dictionaries) is worth
deciding against a real second case rather than against an imagined one.

**Fixed, spell-expansion milestone E (plan D5).** `TickReport.Rows` is now
`IReadOnlyList<TickRow>`, one row per damaging status TYPE per tick
(`(StatusEffectType Status, DamageType Element, int ToHealth, int Absorbed)`),
built by the shared `StatusEffects.ApplyDotDamage` half both the turn-start
tick and Thorn Tithe's post-action retaliation call.
`FightSession.Riders.TickStatuses` loops the rows instead of reading
`PoisonDamage`/`PoisonAbsorbed` by name, and `RecordUnattributedDamage` is
called once per row, not once per tick — the real second case (Burn) landed
in the same milestone and is the one `StatusEffectsTests
.OneTick_CarryingBurnAndPoison_ReportsBothRowsSeparately` pins.

### 189. `b.Actor != null && !b.Actor.IsPlayerSide` stopped meaning "an enemy turn" the moment ticks became beats

A status tick now opens its own beat (`FightSession.Riders.cs:638`), and
`BeginStatusTickBeat` sets `Actor = isHealing ? victim : null` (`:644`). So a REGEN
tick on an enemy produces a beat whose `Actor` is that enemy -- and the idiom three
test helpers use to count enemy turns counts it as one. (A poison tick is safe: its
`Actor` is null by design, which is also what makes the victim flinch.)

Four call sites, not three:
`TurnRiderTests.cs:77` (`EnemyTurnsIn`), `EnemyAiTests.cs:70`,
`FightConsumableTests.cs:114` and `FightConsumableTests.cs:201`.

All four pass today because nothing in the shipped content gives an enemy a regen
status. That is the definition of a test that will break for a content reason, in a
file whose author will have no idea why. The fix is a predicate on the session side
that says "this beat is an action somebody took" rather than four copies of a null
check -- `CombatBeat` is where it belongs, beside `PaintActorDamageType`, which is
the other reader that had to learn the same lesson this round.

### 190. OWNER'S CALL: on the ally rack, Right means "nearer", and that needs a hand on a pad to confirm

The owner, 2026-09-19: "selecting different mobs with gamepad goes with up down,
but it should work with left right." `FightController.Input.cs:1926`
(`InspectMove`) is where the horizontal axis reaches target depth, and `:1951` is
the whole of the rule:

```
CycleTargetFromPad(_menu.Side == TargetSide.Allies ? -delta : delta);
```

**The two racks disagree, and only one of them is flipped.** The enemy rack runs
outward unmirrored (`FightStageAnchors`, Near.X 300 -> Far.X 660), so Right already
means "further right on stage, one slot deeper" and needs nothing. The party rack is
mirrored (`SlotOffset` negates X), so its Near.X 320 -> Far.X 810 becomes on-screen
-320 -> -810: the FARTHER ally sits FURTHER LEFT. For Right to keep pointing
rightward on screen there, it has to mean "toward the near end" -- a negative step in
depth ordering.

Vertical is not flipped per side at all, because Y is not mirrored: Up is deeper on
both racks (`IFightNavigationTarget.MoveFocus`).

So the shipped rule is: **Right = nearer on the party rack, deeper on the enemy
rack; Up = deeper on both.** That is screen-consistent by construction and is the
reading of the owner's words this round took. It is recorded as a call rather than as
settled because "left/right should pick a different ally" could equally have meant
list order regardless of mirroring, and nobody has held a pad and tried it.

### 191. Extends #173: Shop is wired for Start, and its own tree had been drawing OVER the menu

#173's closing paragraph listed Shop among the screens with no system menu for Start
to open. That is no longer true. `ShopController.cs:74` holds a `systemMenu` field,
`:674` declares the handler on its `NavContext`, `:718` is
`SystemMenuController.OpenFromRoot(systemMenu)`, and `ScreenRegistry.cs:625` assigns
it `map.systemMenu` -- **the Map's own instance, not a second one**. One overlay per
scene; the shop is nested in Map and has no scene of its own.

**A z-order bug fell out of it.** `MapScreen.cs` had `systemMenu.Root` before
`shop.Root` in `MapPanel`'s children, so the shop drew OVER the menu. That was
deliberate while `docs/PLAN_SHOP.md` 2c's "no dossier access from inside the shop"
held, and the owner's ask ("press Start in the shop to check on your chars'
equipment / skills") reverses it. Fixed this round at `MapScreen.cs:304`, with both
the field comment and the build comment rewritten to say why the order is what it is.

**Two contexts still absorb Start, and that is the open call.** The Reckoning (in
Fight) and the RelicDraft (in Hub) declare `cancel:` and no `systemMenu:`. Both trees
ALREADY order the menu above them -- `HubScreen.cs:216` (draft) before `:221`
(menu), `FightScreen.cs:412` (reckoning) before `:421` (menu) -- so wiring them is
one field each and no layout work. What is not decided is whether Start SHOULD open
over an offer or a choice that is waiting on the player, or whether those two are the
cases where absorbing it is correct. That is a design answer, not a wiring one.

### 192. Bjorn ships the stance-crop stopgap while a painted portrait sits unused on disk

`Assets/_Project/Resources/Portraits/bear.png` (1010x1250, unchanged since
2026-09-07) is the crop taken from his stance art as a placeholder. A painted
`Art/Portraits/Bear/Processed/Bjorn_neutral.png` exists at 1122x1379, keyed by the
same `tools/remove_portrait_backgrounds.py` pass that produced Odette's, and was
never copied to `Resources/Portraits/`.

Odette's was promoted this round -- `Resources/Portraits/owl.png` (1094x1366) and
`characters.json`'s `portraitPath: "Portraits/owl"` -- so the whole of the remaining
work for Bjorn is one copy, one `.meta`, and one `portraitPath` that is already
correct.

**Not done here, and the reason is ownership rather than effort.**
`Assets/_Project/Art/Portraits/Bear/` is another session's untracked work; staging it
from this round would sweep it into a commit its author has not finished. The owner's
call is whether to promote it as Odette's was, and whoever owns that folder should be
the one to stage it.


## Findings from spell-expansion milestone D, 2026-09-20

### ~~193. The turn-end status clock only ever ran on PLAYER turns~~ — fixed in `2c64a252`

`TickStatusesAtTurnEnd` had exactly one caller, inside `AdvanceAfterAction`
(`FightSession.Riders.cs`), and `AdvanceAfterAction` is reached only by the four
player commands — attack, cast, item, move. A monster's turn and every skipped
turn on either side advance through `StepToNextTurn`
(`FightSession.Enemies.cs`), and a sealed egg's through `AutoResolveEggTurns`
(`FightSession.RelicMechanics.cs`). Neither wound the clock.

**Invisible until milestone A, then live.** While wards were the only thing on
the turn-end clock the hole showed nothing: a monster rarely wears one. Plan D1
moved Protect, Vulnerable, Chilled, Rooted and Marked onto that clock, and from
that point **Winter's Rebuke's two-turn Chill never expired on an enemy**, nor
did `mud_burst`'s Vulnerable, nor `spore_cloud`'s Poison holder's Protect from
`shell_up`. Milestone D's root would have been permanent and self-sustaining on
top of it: a rooted monster with nothing legal forfeits, and a forfeited turn
was exactly the kind that aged nothing.

**Found by a test failing on the wrong turn**, not by reading —
`VelvetShacklesTests.TheSecondOfTwoShackledTurns_IsStillRestricted` expected the
third turn to be free and it was not.

Fixed by `FightSession.EndTurnStatusesForCurrent`, one seam sitting immediately
before all three `_encounter.AdvanceTurn()` sites, keeping the extra-action
exemption (#113) and adding a corpse guard so a combatant killed by its own
turn-start tick does not print "the shield around X fades" over a body.
`RootedStatusTests.RootedEnemyWithNoLegalSkill_ForfeitsItsTurn_TheSameWayStunDoes`
pinned 5 turns remaining after a forfeit and now pins 4, which is the one turn
that was actually spent.

### 194. OWNER'S CALL: Velvet Shackles denies a caster nothing, and heals a beetle

Measured, `docs/PLAN_SPELL_EXPANSION.md` section 5, milestone D. A shackled
enemy loses every action classified `physicalMove`. Against the thirteen rows in
`enemies.json` whose whole repertoire is physical — the eleven with no
`abilities` list plus `golem` (`boulder_slam`, `attackWeight: 0`) — that is the
turn gone: 0 actions out of 6 in the harness, against 6 unshackled. Against the
other three it is a substitution:

| enemy | what a shackled turn becomes |
|---|---|
| `bog_witch` | unchanged — its whole kit is `bog_mud_burst` |
| `forest_warden` (boss) | `roar` only, and it has no plain swing either |
| `treant` | `spore_cloud` only |
| `beetle` | `shell_up` only — **a heal** |

**Why it is the owner's:** two of the three substitutions are plainly a
downgrade for the monster, but `beetle`'s is not. A shackled beetle spends both
turns healing instead of rolling, which may be a better outcome for it than the
turn it lost. Nine mana for "the boss casts instead of slamming" is a different
product from nine mana for "the boss does nothing", and the card currently
promises the second ("It cannot strike, charge or move — only cast" is honest
about the mechanism and silent about the value).

The levers, in ascending cost: the mana price, the duration, or extending the
restriction to a named non-physical class as well. The third is a model change
and not a number — the classification is deliberately binary, and a second
category would need a second reason to exist (`docs/CODE_STANDARDS.md` §10).

Not tuned. `SpellExpansionBalanceTests.VelvetShacklesDeniesAWholeTurnOnlyFromAnEnemyWithNoCast`
pins every figure above, so a decision either way moves a literal.

### 195. Rooted gaining "no physical moves" silently retuned the Sylvan modifier

`modifiers.json`'s `Sylvan` carries `RootChancePercent: 10` — a 10% chance on a
landed hit to root the target for `FightTuning.RootOnHitTurns` (**1** turn; the
baseline's §8 says 2 and is stale). It is a weapon modifier, so it only ever
roots an enemy, and it is the ONLY authored source of Rooted in the game.

Before `2c64a252` that root cost a monster its plain swing. Three of the five
monsters with authored kits would rather have cast anyway, and two of them
(`golem`, `forest_warden`) author `attackWeight: 0` and had no swing to lose, so
the modifier's on-hit effect was close to nothing against exactly the enemies a
player would want it against. It now costs `golem` its entire turn and
`forest_warden` its two best abilities.

**Recorded rather than fixed** because it is not a defect: it is the rule change
working, and it lands on one item modifier that nobody edited. Flagged so a
later balance pass on `Sylvan` reads the right history — if the modifier looks
strong, this is when it became so, and the cause is in `CombatActions`, not in
`modifiers.json`.

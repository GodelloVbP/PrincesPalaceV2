# Prince's Palace — Architecture & Design Audit

**How this register works:** findings stay in place forever, they are never deleted.
A finding that gets fixed is struck through in place with ` — fixed in <short-sha>: <one
line>` appended, so the register stays a complete history of what this project has
actually been wrong about, not just what's currently wrong. New findings (including
open investigations that never got a firm root cause) get appended under whatever
section fits, or under "Open investigations" near the end if none does.

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

### ~~39. A tree node named `<button>Label` silently collides with the caption `UiEmitter` generates~~ — found and closed in the same pass, 2026-08-11
`UiEmitter.EmitButton` creates a button's caption as a GameObject named `node.Name + "Label"`. That
object **never exists in the tree**, so `UiAudit`'s A4 duplicate-name check — which only ever walks
the tree — was structurally incapable of seeing a collision with it. A screen declaring a child
called `MapNode3Label` under a button called `MapNode3` produced two GameObjects with one name under
one parent. The wiring bound the tree's node by `NodeRef` and painted it correctly, so it *looked*
fine; every lookup **by name** — PlayMode tests, `ScreenshotTool` — silently took the emitter's
empty one instead.

Three instances had shipped undetected: `MapNode{0..9}Label` (`MapScreen`, 27 audit errors once the
check existed), `InvestButtonLabel` (`TalentScreen`), and `DebugRow{i}Label` in the debug menu being
written when it was found. Renamed to `…Name` / `…Caption`. New check `CheckButtonLabelCollision`
(A4b) added to `UiAudit`, deliberately **before** A4's `Children.Count < 2` early return — a button
with exactly one child is the commonest shape and precisely the colliding one, so gating it behind
"has siblings" would have let the motivating case walk through. Non-vacuity pinned by
`DebugMenuScreenTests.TheCollisionCheckIsNotVacuous`, which builds a deliberate collision and asserts
the audit rejects it.

Worth keeping as a shape, not just a fix: **an audit that reads the source of truth cannot see what a
later stage synthesises.** The same blindness produced #40's sibling — the detail plate is `.AsDecor()`
and therefore exempt from the overlap check, which is why the boots-behind-the-plate collision needed
a direct assertion. Any exemption or any generated node is a hole the audit will not report.

### ~~40. Relics and Embers are two currencies with one source, and one of them does nothing~~ — **FIXED 2026-08-11.** Decided by the author the same day it was raised: Relics are not a resource at all. `CurrencyType.Relics`, `Wallet.relics` and `SaveData.Relics` are gone, the hub wallet line is two entries, and relics became a per-run draft with an authored rarity band instead. The original finding is kept below because the *reasoning* — two currencies sharing one source means one of them is dead — is what settled it
`SaveData.Relics`' own comment says *"Awarded only for clearing a floor's boss, so it measures how
deep a player has actually got."* `CurrencyType.cs:21-24` says the opposite: Relics are dormant,
*"nothing awards them and nothing sells for them"*, and a boss drop *"was the old model and Embers
replaced it."* Both comments are in the shipped codebase and they contradict each other.

The 2026-08-11 design decision — **Embers are paid per unique boss kill** — re-instates exactly what
`SaveData` claims Relics already are. So the game now displays three currencies on the hub plate, of
which one (Relics) has no source, no sink, and a comment describing a mechanic that belongs to a
different one.

Not resolved here because it is the author's call whether Relics get a distinct purpose or get
deleted. Whichever way it goes, `CurrencyType.IsSafe`, the hub currency line and `Wallet` all
already handle three, so the cost of leaving it is display noise rather than breakage.

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

### ~~42. No relic had ever fired in an actual fight~~ — fixed in this pass, 2026-08-11
`FightEncounterAdapter.KitFor` passed `null` for `PlayerKit`'s relic list, so `kit.Relics` was
always empty and `FightSession.RelicEffectFor` never matched anything. Dual Wield, Magical
Shield and Bloodlust were all implemented, all correct, and all unreachable from play.

**The reason it survived is worth more than the fix.** Every relic test in the suite —
including the flaky Bloodlust one that got four rounds of investigation under #24 — builds its
own `PlayerKit` by hand and hands it straight to a `FightSession`. That is the right shape for
testing a RULE, and it means the entire relic feature was covered by tests that could not
observe the one thing that was broken: nothing anywhere asserted that the ADAPTER supplies what
the session reads. #22's "untested critical paths" is exactly this category, and this is a
worked example of the failure mode — a path can be surrounded by passing tests on both sides
and still be severed in the middle.

Two further consequences of the same gap, both fixed here: `run.relicIds` was written by the
relic draft and read by nothing, so the draft screen was ceremony; and `RelicModifiers.Apply`
had zero callers, so the numeric modifier table was unreachable the day it was written.

Pinned by `RelicsReachCombatTests`, which was verified non-vacuous by reverting the null and
watching it fail.

### ~~38. A wipe does not actually forfeit anything yet~~ — **FIXED**, in `76a4dd1` and `2814cec`; struck 2026-08-17 on re-verification, having been built and never struck
The forfeit is enforced by **structure rather than by a clearing step**, which is why nothing
here looks like the "explicitly clears the at-risk Gold" this finding asked for. Run gold lives
only on `RunSnapshot`; `RunManager.EndRun` (`RunManager.cs:132`) replaces that snapshot with
`new RunSnapshot { hasRun = false }`, so the gold is destroyed with the run it was staked on.
There is no path that moves it to `SaveData`'s wallet, which is the invariant stated as "Gold is
a wager until it is banked".

`RunSettlement.Settle` runs **first**, and the ordering is load-bearing — its own header says so:
it reports `GoldLost` (`RunSettlement.cs:50`) and pays the ember/lifetime side while the evidence
still exists, because `EndRun` is about to throw it away. `76a4dd1` built that, plus
`DefeatController` and `DefeatScreen` — so the "returns the player to the hub with a defeat
animation" half landed too. `2814cec` then folded `Settle` INTO `EndRun`, because settling had
been the caller's job and only the defeat path did it: abandoning a descent from the map silently
binned every ember its bosses had earned, and discarding a snapshot looks identical whether or not
anyone read it first.

The test this finding explicitly asked for — one that fails if the forfeit stops happening —
exists twice over, and both assert the *negative* rather than just the report:
`RunEndingTests.TheRunsUnbankedGoldIsForfeited` (`RunEndingTests.cs:104`, "forfeited gold reached
the banked wallet") and `RunSettlementTests.LostGoldNeverReachesTheBankedWallet`
(`RunSettlementTests.cs:101`). `DefeatScreenWiringTests.cs:109` pins that the number reaches the
screen rather than dying in the settlement object.

Follows from #37, which stays open: the *comments* still describe a voluntary-retreat flow the
game does not have, and the checkpoint design that would replace them is unsettled. Enforcing the
forfeit did not resolve that, and should not be read as having done so.

**Original finding, kept:** Follows from #37 and is the more urgent half. `CurrencyType` documents the forfeit rule, but
the defeat path is unbuilt — the intended behaviour (2026-08-11) is that a defeat returns the
player to the hub with a defeat animation. Until something explicitly clears the at-risk Gold
on a wipe, the documented stake is not enforced by any code, and the "Gold is a wager"
invariant has no test. Whichever way the checkpoint design lands, the forfeit needs a test that
fails if it stops happening.

---

## Findings from the Reckoning polish pass, 2026-08-12

### ~~43. `screenshot.ps1` reports success for a panel it never captured~~ — **FIXED 2026-08-12**, both halves, in the commit that struck this
Asked for `-Panel ReckoningPanel`, the tool printed "Capturing ReckoningPanel to
…\ReckoningPanel.png", then listed the five PNGs already on disk from a previous run and exited
0. `ReckoningPanel.png` was never written and nothing said so.

The cause is stated in the script's own header and is reasonable on its face: "Success is
checked by whether the PNGs exist, **NOT** by `$proc.ExitCode`" — because that exit code came
back empty on runs that demonstrably succeeded. The check it fell back to is existence of *some*
output rather than of *the requested* output, so a stale file from any earlier run satisfies it.

`ReckoningPanel` could never have worked: `ScreenRegistry.All` holds five entries, all top-level
panels (`FightPanel`, `HubPanel`, `MainMenuPanel`, `MapPanel`, `TalentPanel`), and the Reckoning
is a sub-panel inside the fight. So the correct behaviour is to reject the name up front against
that list — the script already documents `ScreenRegistry` as the single source of truth for panel
names and deliberately keeps no second copy, which is right; it just never consults it.

Two separate defects, worth fixing separately: **an unknown panel name should fail loudly**, and
**success should be checked against the file that was asked for, and its timestamp**, not against
whatever is in the directory. The second is the one that generalises — it will silently pass on a
legitimate panel whose capture fails too.

~~Not fixed here because it is a tooling change sitting outside the Reckoning work, and because the
verification it was wanted for was done another way (`tools/measure_bar.py`).~~

**How it was fixed.** Both halves, because they are each other's backstop rather than two
independent tidy-ups.

*Loudly, and early*: `-Panel` is now validated before the robocopy and before Unity boots, against
names **parsed out of `ScreenRegistry.cs`** rather than restated in the script. That distinction is
the whole reason the list was absent in the first place, and it is preserved: deriving is not
copying, so adding a screen to the registry makes it valid to the tool on the same edit. Matching is
case-sensitive, because `ScreenshotTool` compares `PanelName` with `==` and `mainmenupanel` really
is not a screen. If the pattern ever matches nothing — a refactor, not an unknown panel — the check
**defers to Unity** instead of rejecting every name it can no longer recognise.

*And correctly, at the end*: the success check now asks whether **each expected file** exists, by
name. For a single panel that is the requested one. For `-All` it is every name in the registry —
clearing the directory first only rules out leftovers, and four files against five registered
screens is still a non-zero count, so the screen that quietly stopped rendering is exactly what the
old check could not see. On failure the log is filtered to `[ScreenshotTool]` lines, so its verdict
— which names the unknown panel and lists every valid one — is what gets printed rather than being
the sixteenth line of a 40-line Unity tail.

*And the same bug one layer down*: `CaptureAllTo` incremented its counter once per registered
screen regardless of outcome, so a `Capture` that bailed early (no graphics device, a scene with no
Canvas) still logged "wrote 5 screenshot(s)" having written four. It now counts what was written,
confirmed by `File.Exists` rather than by `RenderToFile` not throwing, and reports "wrote N of M".
A failed render exits 1, matching the unknown-panel path, so the exit code means one thing.

Ignoring `$proc.ExitCode` is **kept**. That was always right for the documented reason; checking a
different file than the one requested was never part of that trade.

Verified on all four paths: the original failing invocation now exits 1 without booting Unity, a
case-mismatched name is rejected, a valid panel still captures and now lists only the file it
actually wrote, and the end-of-run backstop reports the missing file plus ScreenshotTool's own
verdict.

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

### ~~48. `EnemyIntentIconTests` asked an arbitrary font whether it could draw a glyph~~ — **FIXED 2026-08-19** in `c2f436a`

Recorded because the *shape* recurs. The test called
`Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault()`, which is the same
incidental-order trap as gotcha #4 — `Resources.LoadAll` returning alphabetical order rather than
authoring order — reached through a different API and in a test rather than in content. Three
font assets are loaded, one of which is a nearly empty fallback atlas, and the day enumeration
order changed the test reported that the game's font could not draw the letter `F`.

Two lessons, both already paid for elsewhere in this register:

1. It had been passing for the wrong reason for its whole life. Any font with a Latin alphabet
   satisfies an all-ASCII fallback table, so it was testing LiberationSans and proving nothing
   about the font the game uses. A test that cannot fail is not evidence.
2. The one-argument `HasCharacter` asks whether a glyph is rasterised *right now*, which on a
   dynamic atlas is a question about what has been drawn so far. The three-argument form asks the
   typeface. The distinction is invisible until the atlas is sparse.

The fix reads the font off a live `TMP_Text` and pins its own premise: U+2694, the character the
intent badges first shipped with and which drew as nothing, must still come back false. If Chakra
Petch ever gains an emoji block, the guard fails and says the check has gone toothless.

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

### 49. `Character.cs` documented an invariant that was inverted, unimplemented, and guarded by a test that did not exist — **FIXED** in the commit that added this

`Character.cs:58` opened with *"PER-RUN progression, earned from combat and reset
by StartRun"* and closed with *"one rule that has to hold: StartRun resets all
four. There is a test whose whole job is to fail if it ever stops."*

Three separate claims, all false:

- **`RunManager.StartRun` never touched the roster.** It replaces `save.activeRun`,
  calls `RoomResolver.Reset()`, `Forget()` and `Persist()`. `save.roster` is not
  in the method.
- **Nothing anywhere reset them.** `grep` for `level = 1` / `exp = 0` /
  `unspentStatPoints = 0` across non-test code returns the field initialiser and
  a default parameter in `FightEncounterAdapter`. Nothing else.
- **There was no such test.** No test asserted anything about level across a run
  boundary in either assembly.

`RewardApplier.cs:12` states the true rule and always has — *"GOLD belongs to the
run and is lost with it, EXPERIENCE belongs to the characters and survives"* —
so the codebase carried both answers, in two comments, with the wrong one
attached to the field itself.

**Why this is worse than an ordinary stale comment.** It did not read as
description; it read as a load-bearing invariant with a named guard, in a file
whose house style is that comments carry the reasoning. It is the shape a
planner trusts most. It came within one session of costing a full reward-track
design: six of the track's ten milestones ("start every run with 2 relics",
"guaranteed rest before every boss", "second life") are statements about how a
run *begins*, and under the documented rule none of them can exist, because you
would never be holding the level at the moment the reward applies. Caught only by
reading `StartRun` rather than believing the field.

The same paragraph was also propping up a second decision one screen down:
`Invest`'s *"there is deliberately no matching Refund ... the run ending is
already a full reset, so nobody is stuck with a build forever."* The escape hatch
that argument leans on does not exist, so a placed stat point has always been
placed permanently. Refund is still deliberately absent, now for the half of the
argument that survives.

*Fixed:* both comments say what is true; `RunManagerTests` gains the test that
was claimed, inverted — `StartingARunKeepsEveryCharactersLevel` — plus
`StartingARunStillDiscardsTheRunBeforeIt` for the other half, so a future change
in either direction fails for its own reason. The class of bug (a comment
asserting a guard that does not exist) is not mechanically closable; the
narrowest available fix is that the guard now exists.

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

**This one has already cost planning time.** `docs/PLAN_PROGRESSION_TRACK.md`
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

### ~~53. Stat points cannot be spent -- `Character.Invest` has no production caller~~ -- **FIXED**: the dossier's attribute cells now carry a "+" that calls `Invest`, shown only when there are points to spend, with a "N POINTS TO SPEND" line beside the section heading so the mechanic is discoverable rather than a 22px glyph nobody looks at

`Character.Invest(AbilityScore)` is implemented, commented at length ("ONE point
at a time on purpose: the UI offers a '+' per score"), and covered by tests.
**Nothing in the game calls it.** `grep` for `Invest` across `Assets/` returns
this method, its tests, and otherwise only the TALENT tree's unrelated
`CanInvest`/`InvestButton`, which spends embers on orbs and never touches an
ability score.

`investedAbilityScores` is assigned in exactly one place -- inside `Invest`
itself (`Character.cs:191`). It is READ correctly, by
`ContentDatabase.Effective.cs:113`, which folds it into `EffectiveAbilityScores`
and out into attack/speed/health. So the pathway works end to end and has no
entrance.

The dossier is where the "+" per score was supposed to live. Its
`attributeCells` are wired to `HoverIndex` -> `OnAttributeHover` only: hovering
an attribute explains what it does, and there is no way to raise one.

**What this costs.** `unspentStatPoints` accumulates and is never spendable, so:

- One stat point per level was the ONLY thing levelling granted before the
  reward track, which means levelling has never granted anything a player could
  use.
- The track's filler is 30 stat points, and level 80 grants 10 more. That is 40
  of the track's rewards doing nothing -- and level 80 was chosen *because* it
  was the safe, already-implemented reward kind (it replaced "elites always drop
  a relic", cut as too strong).
- A respec's stat half refunds a block that is always zero.

Same class as #42 (relics that had never fired in a fight) and
`DifficultyCurve`'s own header ("AND THEN IT WAS NEVER CALLED"): implemented,
tested against inputs the test builds itself, and unreachable from play. The
tests are again what makes it look covered.

*Not fixed here.* The respec work that found it builds both halves so the refund
is correct the day the spend exists, but a "+" per score on the dossier is its
own change.

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

### ~~55. `ContentDatabase.MinSpentMet` enforces the talent gates and nothing calls it~~ -- **FIXED**: the rule now lives in `TalentPage.Evaluate` as `Refusal.Gated`

`MinSpentMet`/`SpentInPath`/`MeetsGates` (`ContentDatabase.cs:596-625`) implement
the tree's two point-gates -- 9 embers spent on a path before its convergence
opens, 20 before its capstone. `MeetsGates`' comment says "there are now four of
them and they have to agree between the talent screen's colouring pass, its
click handler and its tests". There were none: `grep` finds no caller outside
`ContentDatabase` itself.

`TalentPage` was written later to own "can this be kindled", carried
prerequisites and cost across, and did not carry the gate. Those two slots are
also the only orbs `OrbCost` prices at zero, so the gate WAS their price and it
was not being asked -- both were free the instant their parents lit.

Fixed by adding `Refusal.Gated`, ranked below `PrerequisiteMissing` (a player
told to spend more on a path they cannot climb yet will spend and come back to
the same refusal) and above `NotEnoughEmbers` (a stone behind a gate is not
expensive, it is shut). `TalentSlot` carries `MinSpent` so Domain can answer
without reaching across the layer boundary, and the collar the talent screen
draws around each gated stone reads the same number.

**Behaviourally this blocks nothing that used to succeed**, which is also why it
went unnoticed: a merge requires all three tiers beneath it, so the parent chain
is worth ten orbs by the time the 9-gate applies.
`TalentGateTests.TheAuthoredGatesArePaidByTheirOwnAncestry` pins that, and fails
the day a skeleton change makes the gate bite -- which is a balance decision and
should arrive as a failing test.

### ~~56. Every lit talent edge drew a second stray line, and the layout audit was exempted from seeing it~~ -- **FIXED**

`TalentScreen.BuildEdge` builds three nodes per connection: a dim limb, a lit
glow, and a core down the middle of the glow. The core is a CHILD of the glow
and was placed at the edge's absolute midpoint with the edge's absolute angle --
correct for the sibling it used to be. A child's transform composes with its
parent's, so every lit edge rendered its core at twice the offset and twice the
angle: a loose gold hairline elsewhere in the sky with no stone at either end.

`UiAudit`'s ChildContainment measures exactly this and was silenced by the
core's own `AllowOverflow("a rotated edge's axis-aligned box is wider than the
line inside it")` -- a true statement, and a blanket one, so it waived a 900px
escape as readily as the 4px it was written for.

Found in a runtime screenshot; nothing in the built scene or the suite said
anything was wrong. Fixed by placing the core at `(0, 0)` unrotated, and pinned
by `ConstellationScreenTests.EveryEdgesLitCoreSitsOnTheEdgeItLights`.

**The generalisable half is filed in `docs/INCIDENTS.md`:** an `AllowOverflow`
reason says why SOME overflow is expected, never how much, so a node carrying
one has no layout coverage until something asserts the property it still owes.

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

## Open investigations

### ~~52. `SystemMenuExitsTests.OnePressOnAnExitDoesNothingButArmIt` flaked once, navigating to `"Hub"` — cause not found~~ — **ROOT CAUSE FOUND AND FIXED, 2026-08-21.** A `HoldToConfirm` left running by an earlier test in the same fixture. `HoldToConfirm` cancels itself `OnDisable`, and its comment explains why — but between two tests nothing disables it: the scene stays loaded until the next `LoadSceneAsync`, so a test that begins a hold and neither completes nor releases it leaves `Update()` advancing that hold into whatever runs next. When it completed it called `Abandon`, which navigates to the hub, and the navigation landed in the *next* test's recorder. The fixture's `TearDown` now cancels every live hold. **What made it findable:** the reward-track panel added ~450 nodes to the Hub scene, frames got long enough that the leftover hold finished inside the very next test every time, and a one-in-four flake became 3-for-3 — including in isolation, which it had never done. The extra nodes did not cause it; they made it reproducible enough to get a stack trace, which named `HoldToConfirm.Update` with no test above it. Guessing had blamed process-wide `Navigation.LoadOverride` state, which was wrong

Seen **once in four full-suite runs**, during the reward track's starting-relic
work (2026-08-21). Passes in isolation (13/13), and passed in three consecutive
targeted runs paired with `RelicDraftTests`, and in the three full runs after it.

```
FAILED: SystemMenuExitsTests.OnePressOnAnExitDoesNothingButArmIt
  one press on an exit left the scene
  Expected: <empty>   But was: < "Hub" >
```

The recorded destination is the interesting part. The test arms `ExitTitle`,
whose second press goes to `Navigation.MainMenu` — so `"Hub"` is not this exit
firing early, it is a **different** navigation arriving inside this test's
recording window. `Navigation.LoadOverride` is a process-wide static that each
fixture installs in `SetUp` and clears in `TearDown`, which is exactly the shape
of thing `GlobalStateLintTests` exists to police, and exactly the shape that
produces a once-in-four failure rather than a consistent one.

**I do not know whether the starting-relic change introduced this.** That work
added four PlayMode tests that load the Hub scene and drive the draft to
completion, which changes both the ordering and the timing of every PlayMode
test after them — enough to surface a latent race without being its cause. It
was not observed before that change, but the change is also the first time this
suite had been run repeatedly in one sitting, so absence of prior sightings is
weak evidence either way. Recorded rather than guessed at.

Worth pairing with #24, the other flake in this register whose root cause was
eventually found to be a real bug (#13) rather than test noise.


### ~~24. `BloodlustRelic_GrantsAnImmediateExtraTurnAfterAKillingBlow` flakes on fresh content/scene builds — root cause not found~~ — **ROOT CAUSE FOUND, 2026-08-04.** It is a symptom of #13, and fixing that fixed this. Reproduced 2 times in 8 runs before, then 0 in 12 after

The investigation below is preserved because most of its ruling-out was correct — it just never
suspected the message *buffer*, only the message *not being emitted*. It was being emitted every
time.

**What was actually happening.** Bloodlust fires in `AdvanceAfterAction`, after `CommitBeat()` and
before `StartBeatPlayback()`. So its line went straight into `_recentMessages`, while the beat
messages for the very swing that earned it ("Shawn attacks…", "…is defeated!") were appended
*later*, during playback. The buffer trims from the front. On an eventful turn — poison ticks, a
Wool soak, two enemy replies — the newer beat lines pushed the Bloodlust line out before the
assertion read the label. Fewer preceding messages and it survived; that is the whole of the
intermittency, and why it correlated with nothing anyone tested.

**Why it looked so mysterious.** The failure output said only "no Bloodlust in the log", which is
consistent with a dozen explanations. Diagnostics were added to the assertion, and the first
reproduction settled it outright: `IsOver = False`, kill landed, `chain count = 1`, `chain actor =
Shawn`, `Current = Shawn`. The extra turn had been granted and recorded. **Only the message was
missing** — so the sole remaining suspect was the buffer. The register's own note that the sibling
`Assert.AreSame` "also failed" was mistaken; it passes, which is exactly what a granted-but-
untold extra turn looks like.

Two things were fixed alongside the strike. The test itself had a **second, independent** failure
mode never reached in practice: `AdvanceUntilItIsThisCharactersTurn` reaches the actor's turn by
attacking up to twenty times, and those attacks can finish the *second* enemy off first — after
which the pinned kill is the last one, `AdvanceAfterAction` takes its `_encounter.IsOver` early
return, and Bloodlust is genuinely never reached. The Elite room was chosen to prevent that but
does not, since it only guarantees two enemies *at the start*. The test now restores the
non-front enemies before the killing blow, stating the precondition rather than hoping for it. And
the diagnostics stay on the assertion permanently — they cost nothing on a green run and turned a
multi-session mystery into a single reproduction.

`Assets/_Project/Scripts/Tests/PlayMode/FightControllerTests.cs:1911`. Failed 4 times in one
night (2026-08-01), always the same way: a full combat sequence up to and including the killing
blow logs correctly, but the expected `"...'s Bloodlust surges — one more turn!"` message
(`FightController.cs:2043`) never appears and `Assert.AreSame(actorsTurn, encounter.Current, ...)`
/ `StringAssert.Contains("Bloodlust", ...)` fails. Every failure ran with a fresh
`-BuildContent -BuildScenes` (which regenerates and re-syncs the whole `Resources/Content` tree
and both scenes across two isolated Unity processes — see `tools/run_tests_parallel.ps1`); every
sync-less rerun of just this test passed. No other test has shown this pattern.

**Ruled out:**
- **Not a crash.** `test-run-PlayMode.log` for a failing run has zero `NullReferenceException`,
  zero `MissingReferenceException`, zero "missing script" warnings — nothing but the standard
  "Native extension not found" noise every batchmode run prints.
- **Not a stale-GUID sync issue**, or at least not one that survives to test time. Added
  `Assert-GuidsMatch` to `run_tests_parallel.ps1` — a hard, hash-independent comparison of every
  `.meta`'s literal `guid:` value between main and each secondary runner's copy, run immediately
  after the post-build re-sync, aborting the whole run loudly and naming the exact asset on any
  mismatch. It has fired zero times across the runs used to investigate this, including a run
  that then went on to fail with the exact Bloodlust symptom — so at the moment the test process
  launches, every asset file on disk genuinely agrees on GUIDs between main and the PlayMode
  runner. Whatever is wrong is not "the wrong file is on disk."
- **Not static/cross-test state leakage** in the mechanism itself. `_bloodlustChainActor` /
  `_bloodlustChainCount` (`FightController.cs:377-378`) and `_playerOwners`
  (`FightController.cs:766`) are all plain instance fields, rebuilt fresh per encounter/scene
  load; `_bloodlustChainCount`'s read at `:2036` already guards on `actor == _bloodlustChainActor`
  so a stale reference from a prior test's (different) actor object can't inflate the chain count.

**Both leading theories were wrong, and the way they were wrong is the useful part.** They were a
Unity asset-import timing race (`RelicEffectFor` resolving a relic against a not-yet-reimported
Library) and an actor-identity mismatch in `_playerOwners`. Both were plausible, both explained
intermittency, both were consistent with every observation — and both were about the relic lookup
FAILING, when in fact it succeeded every time. Four rounds of investigation narrowed the wrong
half of the problem, because the failure output ("no Bloodlust in the log") never distinguished
"the effect did not fire" from "the effect fired and the message was discarded", and nobody
thought to ask which. Adding four values to the assertion answered it on the first reproduction.

Two data points from those rounds are worth keeping and re-filing. The "only after a fresh
`-BuildContent -BuildScenes`" pattern never held up — later occurrences included plain no-flag
runs — which is expected once the cause is a message buffer and has nothing to do with builds.
And the Phase 6 observation of a *different* test, `ContinueAfterVictory…`, showing up as
**Skipped**, was never a Bloodlust data point at all: that test's only skip route is #19's
`Assert.Ignore` on a randomised defeat. It belongs to #19 and is recorded there.

The GUID-consistency assertion added while hardening against theory 1 is kept. It was aimed at
the wrong cause but is a reasonable check in its own right, and removing it now would be
churn.

### ~~59. A flat-art CHARGE lands its blow before the charger has crossed, and its own travel floor is why~~ — fixed in `d0f9944`: `FightBeatPlayer.Charge` now returns the `outSeconds` it computes, and `PlayBeats` waits out that exact value (`StaticSwing.Windup(seconds)`) before firing the impact, the same shape `StaticSwing` already gives a Lunge. The contact effects fire too, burst only (no slash arc) per `docs/STATIC_COMBAT_ART_DEEP_DIVE.md`'s Blunt row

`FightBeatPlayer.Charge` (`Core/FightBeatPlayer.cs:622-639`) times the rush so
the figure arrives on the impact frame: `outSeconds = Max(ChargeMinOutSeconds,
windup + impactDelay)` at `:634`. For an actor with a single-drawing stance the
wind-up is zero (`FrameStancePlayback.WindupSeconds` returns 0 for `FrameCount
<= 1`, `Core/StancePerformance.cs:99`), so the travel falls through to the
`ChargeMinOutSeconds` floor of 0.18s at `:747` -- deliberately, per that
constant's own comment, or "the figure would teleport into the target".

The IMPACT INSTANT is not moved to match. `PlayBeats` fires the flash, the
recoil, the squash and the damage number one frame after `playback.Windup()`
returns (`Core/FightBeatPlayer.cs:375-382`), and for a flat pose that is
immediate. So the blow lands roughly 0.18s before the charger arrives: the whole
point of the approach -- "the bump falls out of that timing", `StageApproach.cs`
-- does not happen, and what is on screen is a target flinching at nothing,
followed by a creature arriving at a target that has already reacted.

This is the same defect Phase 1 of the static-art pilot fixed for
`StageApproach.Lunge`, via `StaticStancePlayback` reporting anticipation plus
travel as its wind-up. Charge was deliberately left out of that wrap and the
reason is recorded at `PlaybackOf`'s gate: Charge derives its travel time FROM
`WindupSeconds`, so wrapping it would feed the rush a wind-up that already
contains the travel it is trying to fit inside, and it would then arrive late by
its own length. The fix is therefore not "wrap it too" -- it is a wrapper whose
reported wind-up IS the charge's own `outSeconds`, computed once and read by
both sides rather than each deriving it from the other.

Not urgent: no shipped enemy pairs flat art with a charge today (the Beetle's
Barrel Roll, the approach's own reason for existing, is a six-frame sheet). It
becomes visible the moment one does.

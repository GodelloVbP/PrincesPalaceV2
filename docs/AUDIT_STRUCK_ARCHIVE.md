# AUDIT.md — struck findings, full write-ups

`AUDIT.md` keeps a one-line entry for every struck (fixed) finding so the register stays
short to read. The full write-up — the reasoning, the reproduction, the fix — for each
one lives here instead, verbatim, in the same order it appeared in `AUDIT.md`. Nothing
here is current status; every finding below is closed. See `AUDIT.md` for the open
register and for how this whole scheme works.

Two edits were made against the original text when it moved here, both narrow: #59's
citation of `docs/STATIC_COMBAT_ART_DEEP_DIVE.md` now points at `docs/ART_PIPELINE.md`
(that doc archived, the cited rule relocated there), and #49's heading gained the
strike-through and fixing commit it was missing in place — its body is unchanged.

---

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

### ~~49. `Character.cs` documented an invariant that was inverted, unimplemented, and guarded by a test that did not exist~~ — fixed in `457bce0`

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

### ~~59. A flat-art CHARGE lands its blow before the charger has crossed, and its own travel floor is why~~ — fixed in `d0f9944`: `FightBeatPlayer.Charge` now returns the `outSeconds` it computes, and `PlayBeats` waits out that exact value (`StaticSwing.Windup(seconds)`) before firing the impact, the same shape `StaticSwing` already gives a Lunge. The contact effects fire too, burst only (no slash arc) per `docs/ART_PIPELINE.md`'s Blunt row

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

---

### ~~61. `SpellVfxTests` flakes between runs on an identical tree — cause not found~~ — fixed in `c9afc22`
Seen 2026-09-05 during the folder-per-area move (`4367eed`), on a tree with no
source change between runs: pass, then `ALoneEnemyGetsOneEruptionAndStillOneFault`
failing, then `AFullFormationGetsThreeEruptionsAndExactlyOneFault` failing, then
pass. A different test each time, both counting eruption/fault layers after a
Cinderfault cast. That shape (order-sensitive, count-off-by-one, PlayMode) matches
#52's leftover-state cause more than a timing race; the pooled ground node added
in `7504ea3` is the first suspect, since it is the one thing those two tests share
that the rest of `SpellVfxTests` does not. Not reproduced under a debugger; three
consecutive full gates after this pass were green. Filed so the next flake has a
starting point rather than a shrug.

**Closed 2026-09-06 in `c9afc22`.** It was a timing race after all, and the guess above
sent the wrong way: **the pooled ground node is not implicated, and neither is
leftover state.** `SpellVfxTests.PlayFast` sets `BeatSpeedMultiplier = 60`, so the
whole real-time budget of a 0.78s cinderfault is `FightBeatPlayer.Scaled(0.78)` =
**13ms**, and both tests cast, wait ONE frame, then count which layers still have
`Image.enabled`. A batchmode frame right after `LoadSceneAsync` was measured at
24ms and 57ms.

Instrumented `SpellVfxPlayer.PlayRoutine` and the two tests, five runs of the
fixture: **fail, pass, pass, fail, fail.** Every failing run logged `iters=1` with
`elapsed` past `total` (0.0568 / 0.0236 / 0.0135 against `total=0.0130`); both
passing runs logged `iters=2`, `elapsed` 0.0163 and 0.0179 — surviving by a single
tick. On the tightest failure the ground coroutine stamped `started` at 5.578244
and the test read its own clock at 5.595784: 17.5ms of the 13ms budget was already
spent *inside `PlaySpellVfx`*, loading the three eruption sheets after the ground
layer's clock had started.

That is also what produced the shape this finding could not explain. The fault and
the three eruptions are four independent coroutines losing that race separately, so
a slow frame killed the fault while the eruptions lived — "the fault was never
drawn" with `DrawnEruptions()` still returning 3. Different test each run, no source
change. Cross-fixture bleed of `BeatSpeedMultiplier` was ruled out by the same
probes: the fixture's own `[SetUp]` writes it unconditionally, and every reading was
`mult=60`, `total=0.013000`.

Fixed by making playback observable instead of raced: `SpellVfxPlayer.ClockOverride`
(null in the game) is held at the instant of the cast, applied to all six tests in
the fixture that read what is drawn a frame later — the four beyond the two that
failed race the same clock at 8.7ms. Advancing it past the end made the cleanup half
assertable for the first time, and
`EveryLayerIsDrawnAtTheImpactInstantAndNoneSurvivesTheBeat` is the only test in the
suite that fails when `PlayRoutine`'s `image.enabled = false` is deleted.

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

### ~~66-76 (eleven findings from the restatement sweep, 2026-09-06)~~ — no write-up ever lived in `AUDIT.md`

These eleven were struck the day they were found, and their one-line headings in
`AUDIT.md` each end "full reasoning in the commit message" rather than pointing here.
So there is no verbatim write-up to move: the reasoning is in the eleven commits named
below, and copying a paraphrase of it into this file would create a second, worse copy
of something git already holds. They are listed here anyway so this archive's set of
struck numbers matches `AUDIT.md`'s — which is the cross-check that found #140 in the
first place, and which would otherwise keep reporting these eleven as missing forever.

- ~~66. A `DefeatSpecificBoss` achievement's target enemy was checked by nothing~~ — fixed in `24797a93`: `AchievementProgress.ValidateDefeatSpecificBossParameter` checks the parameter against the enemy catalogue, wired in from `ContentDatabase.ValidateContent`; full reasoning in the commit message
- ~~67. `KitFor(CharacterDefinition)` hand-rolled the skill-unlock filter `AvailableSkillsFor` owns~~ — fixed in `3d2a1de4`: the shared predicate moved into `ContentDatabase.SkillsUnlockedByLevel`, and both call sites go through it; full reasoning in the commit message
- ~~68. `ResistanceByType.WithMagical`/`IsEmpty` hand-listed the `DamageType` members with no completeness test~~ — fixed in `8ae0ee8f`: both now derive from `Enum.GetValues`, pinned by two new completeness tests; full reasoning in the commit message
- ~~69. `TryResolveStatus` was duplicated byte-for-byte across two content resolvers~~ — fixed in `1fcc621e`: moved into `StatusAuthoring.TryResolve`, shared by `SkillEntryResolver` and `EnemyEntryResolver`; full reasoning in the commit message
- ~~70. `tools/bot.ps1` retyped the archetype list `Archetypes.Names` owns~~ — fixed in `a846a972`: the script's `-Archetypes` default is empty and `-botArchetypes` is omitted from argv when empty, so `BalanceBotRunner`'s registry-derived default governs; full reasoning in the commit message
- ~~71. The spell-acquisition metric was computed, rendered nowhere, and absent from the schema doc that claims to be the contract~~ — fixed in `b03fa207` and, for the room-row half, `7975c5a1`: `docs/BOT_SUMMARY_SCHEMA.md` now documents both the `cells[].spellAcquisition` fields and the `RoomTrace` fields, `tools/bot_report.py` renders the metric, and `tools/bot_schema_test.py` checks doc against emitter for both halves; full reasoning in the commit messages
- ~~72. The dossier leader line's vertical anchor was its slot's plus 37, typed seven times~~ — fixed in `f40577dd`: `DossierLayout.SlotGeometry` is the one top/file table now, and `LeaderTop` derives from `SlotTop` plus a named `LeaderVerticalOffset`; full reasoning in the commit message
- ~~73. The stage capacity `3` was typed twice in `FightBootstrap` with comments naming `FightHudSpec.StageSlotsPerSide`, and `"lone"`/`"full"` were literals at six sites~~ — fixed in `cd4c4c2c`: both `FightBootstrap` literals now read `FightHudSpec.StageSlotsPerSide` directly, and `PreviewFight.FormationLone`/`FormationFull` are the one spelling referenced at all six sites; full reasoning in the commit message
- ~~74. `BotPhaseTimers.PhaseCount = 18` hand-counted the `BotPhase` enum~~ — fixed in `3262cc2d`: `PhaseCount` now derives from `Enum.GetValues(typeof(BotPhase)).Length`; full reasoning in the commit message
- ~~75. `CharacterVoice` keyed Shawn's lines on the literal `"sheep"` with no check against `characters.json`~~ — fixed in `8156a4ca`: a PlayMode test asserts every `CharacterVoice` key is a live id `ContentDatabase.GetCharacter` recognises; full reasoning in the commit message
- ~~76. `architecture_audit.md` §7 stated two partial-class line counts as precise numbers, both stale~~ — fixed in `1656372a`: the `FightController` and `ContentDatabase` rows now read approximate, sha-stamped counts, same phrasing as the `FightSession` row fixed the same morning; full reasoning in the commit message

### ~~107. `tools/preview.ps1 -Spell` cannot choose which element of a choice-skill it casts~~ — fixed in `4c45692b`: `-Element <DamageType>` on `preview.ps1`, validated against the skill's own `elements[]` before Unity boots and refused by name listing what is offered; carried through `PreviewProtocol.element` and `FightBootstrap.DevForcedElement` to `PreviewFight.ForSpell`/`PreviewElementOf`, which now casts the requested element and falls back to the old first-that-draws rule only when none was asked. The forced press and the capture prefix both name it (`spell_prismatic_orb_wind_impact.png`), so four elements no longer overwrite each other or require reordering `elements[]` in `skills.json`

Found delivering Fire, Wind and Earth for `prismatic_orb`. `PreviewFight.PreviewElementOf`
(`Assets/_Project/Scripts/Core/PreviewFight.cs:87-98`) casts the first element in
authored order that has art, and `tools/preview.ps1` has no `-Element` parameter. While
only Water had art this was invisible; with four elements drawn, photographing any but
the first means reordering `elements[]` in `skills.json`, capturing, and restoring the
order -- which is what the delivery did. Fix shape: an `-Element <DamageType>` on
`preview.ps1`, handed through the same request the `-Spell` id travels in, validated
against the skill's `Offers`, refused by name when the element is not offered; the
capture fixture names the element in the log line it already prints.

### ~~108. `SpellEmitter` cannot weight which atlas cell a particle draws~~ — fixed in `44e05216`: an optional `float[] weights` on `SpellEmitter`, one entry per cell in the folder's own file order, landed together with an array-aware `IsAuthored`/`FieldsEqual` so a `float[]` field defaulting to null never reads as reference-unequal to itself. `SpellLayerRules` refuses a negative, non-finite or all-zero array (the numbers-only half it can check without the disk); the length-equals-frame-count half lives beside the identical `startFrame` rule in `SpellVfxRecipeDriftTests`, because Domain cannot see the folder's frame count either way. `SpellEmitterSim.At` picks by cumulative weight over the same `hash(seed, index, 6)` an unweighted emitter always used, so a shipped emitter that authors no weights plays the identical field it always did. Earth's `shed` and `spray` emitters over `Spells/prismatic_orb_earth_drops` (8 cells: six rock chunks, a grit cluster, a dust puff) both ship `[15, 18, 8, 12, 12, 8, 15, 2]` -- small/mid chunks dominant, the two heaviest chunks (index 2 and 5) held down, grit present, the dust puff at 2.2% of the total

Found delivering Earth. `SpellEmitterSim.At` (`Assets/_Project/Scripts/Domain/Combat/
Presentation/SpellEmitterSim.cs:156`) picks a still by `hash(seed, index) % frameCount`,
uniform across the folder, and `SpellEmitter` carries no weight field. Earth's delivered
atlas is uneven on purpose -- six rock chunks from 10% to 34% ink coverage, a grit
cluster and a dust puff -- and its README asks not to sample them uniformly. Content
cannot say "small chunks often, the puff rarely"; the delivery mitigated with a narrow
size range and a modest burst count. Fix shape: an optional `weights` array on
`SpellEmitter` (one float per cell, validated to the folder's count by the drift lint),
consumed by a cumulative-weight lookup in place of the modulus, seeded the same way so
previews stay repeatable; absent means uniform, so every shipped emitter is unchanged.
A resolver rule refuses a weights array whose length is not the folder's frame count.

### ~~147. "The Flock" wards exactly one ally, and which one is decided by the field formation~~ - fixed in `ee0d7727`; struck heading in `AUDIT.md`. The write-up below is verbatim as it stood, the owner's 2026-09-15 answer included -- that answer is what produced the ally picker rather than either option it filed.


**The owner's answer, 2026-09-15.** Neither filed option: silently deciding by formation is
"just stupid," but a real fix means the player picks the target, not the engine. There is no
target-picker anywhere in this game today -- every ally-facing talent (this one, and
`GiftRecipient`, which has the identical unsolved need per its own comment) auto-picks "the
first ally in line." Building one for The Flock alone would mean building the same feature
twice once `GiftRecipient` needs it too. Deferred on purpose: this wants a proper mid-combat
target-selection feature covering both talents, not a patch on one. Left on the "first ally"
auto-pick until that feature is scoped.

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

### ~~151. Provoke's "bellows at nothing in particular" line cannot be reached~~ - fixed in `399d6c1d`: the owner's call was that the line is dead copy; the `provoked == 0` branch is gone and the reach refusal is the one path

Found 2026-09-15 while shipping the ally picker (#147), by a test fixture that stopped guessing
targeting. `Assets/_Project/Scripts/Domain/Combat/Session/FightSession.Skills.cs`,
`ResolveCharacterSkillInner`'s `SkillEffect.Provoke` arm: `ApplyProvoke` returns a count, and 0
prints `"{actor.Name} bellows at nothing in particular."`

Nothing can produce that 0. Provoke is `SingleEnemy` (`SkillEntryResolver.DefaultTargetingFor`
falls through to it and `provoke`'s row authors no override), so `CastSkill`'s reach gate refuses
the cast unless the target is a living enemy -- and a living enemy is exactly what `ApplyProvoke`
then provokes, whether or not `ProvokeHitsEveryEnemy` widens it. The menu cannot reach the line
and neither can the bot, whose `LegalActions` offers a `SingleEnemy` skill only against
`EligibleTargets`.

**How it stayed invisible.** `FightTalentTests` built every synthetic skill with a hardcoded
`SkillTargeting.Self`, which skips the reach gate entirely -- so
`BellowingAtAnEmptyRoomSaysSoHonestly` cast at a corpse, resolved, and went green against content
`skills.json` could never produce. That is the exact failure mode `DefaultTargetingFor`'s own
header warns about ("a fixture that guessed SingleEnemy for a HealSelf was building content
skills.json could never produce"). The fixture asks the resolver now, and the test pins the
refusal instead (`BellowingAtACorpseIsRefusedRatherThanResolved`).

**Not fixed here, because which way it should go is a design question.** Either the line is dead
copy and should be deleted with the `provoked == 0` branch, or Provoke should not be
`SingleEnemy` at all once `ProvokeHitsEveryEnemy` is held -- it already ignores its target in
that case, so the front-rank rule is gating a cast that does not aim. The second reading is the
more interesting one: a taunt the player cannot open with because a bodyguard is in the way is
arguably wrong, and `SkillEffect.Provoke`'s own comment says the widening happens "at CAST time
when the caster's tree says so", which no targeting currently reflects.

### ~~152. Wards are a percent of the next hit with no timer; the owner may want absorb pools~~ - fixed in `339ce102`: the owner answered shields; a ward is a pool of shield points on a two-turn clock

Raised 2026-09-15 by phase 5 step 0's retune, from what phase 4 pinned. A ward in this game is
`StatusEffects.ConsumeWard`'s one line -- `damage - damage * Magnitude / 100` -- spent by the
first hit that lands, and applied with `FightSession.WardDurationTurns` = 999, so no ward has
ever expired on a clock. Every authored ward number is therefore a PERCENTAGE off one blow.

`docs/handoffs/progression_v2/PLAN_PROGRESSION_V2.md` §5 was written against a different model:
an absorb POOL in hit points, drained over "two of the wearer's turns". Step 0 retuned the
numbers to the model the code has (Tuck In 10% a Wool, Bulwark 50%, Prism Ward a flat 40%)
rather than building the pool, because replacing the ward model touches every ward in the game
-- the Lamb's whole talent branch, the Magical Shield relic, `placeholder_brawler_ward`,
`fleece_ward` -- plus the status HUD, which shows a ward as a state and not as a quantity.

**The open question is which model the game wants**, and it is a design call with real
consequences either way. A percentage is build-agnostic and never reads as wasted, but it cannot
be partly spent and makes a big hit the best thing to eat. A pool absorbs what it says it
absorbs, survives small hits, and gives the HUD a number to count down -- and would need a
duration, which nothing in the ward code has today. If absorb pools are wanted, they are a phase
of their own, not a number change.

### ~~153. A one-turn ward is never visible on its caster's own turn, and Shatter cannot reach one~~ - closed in `214e7ad7`: the owner's answer was to move the tick to the END of the wearer's turn, so a ward is visible on the turn it protects and Shatter can reach one cast the turn before

Found 2026-09-16 while building the owner's four shield-model decisions, by six test fixtures that
stopped asserting what they had always asserted.

`FightTuning.DefaultWardTurns` is 1, and `StatusEffects.Tick` counts a duration down at the
HOLDER'S turn start. So a ward applied during turn N reaches 0 at the start of turn N+1 and is
removed there. That is correct, and it is exactly what "one of the wearer's own turns" should mean
-- the ward covers the enemy phase between your turns, which is the phase it exists for. Two things
fall out of it that the decision may not have priced:

**(a) The player never sees the badge on their own turn.** `CastSkill` resolves, ends the turn, and
control returns at the caster's next turn start -- after the tick that took the ward. The SHD badge
is on screen only while the monsters are acting.

**(b) `SkillEffect.Shatter` cannot reach a ward cast on an earlier turn.** Its whole premise is
"detonate the wards you have out" (`FightSession.Talents.ResolveShatter`, gated by
`CanResolveSkill`'s `WardsCastBy(actor).Count == 0` refusal). A ward cast on turn N is gone by turn
N+1, so the Fragile Lamb's entire Shatter strand is unusable unless she also holds
`sheep_lamb_ward_3` (`WardIsFreeAction`), which keeps the ward and the detonation inside one turn.
Six fixtures -- `FightTalentTests`' Flock and Shatter tests, `SkillDispatchTests`,
`SkillEffectBehaviourTests`, `DodgeCoversEveryDamagePathTests` and `KillCreditTests` -- had to grant
that node to keep passing. That is the evidence, not the fix.

**(c) Stacking is barely observable for one caster.** Two presses of the same ward can never
coexist, because the first is ticked away before the second is cast. Stacking shows up between a
skill ward and a relic ward (which author the whole fight), between two casters inside one enemy
phase, or through The Flock's share -- and nowhere else.

**Not fixed here, because every way out is a design call.** Ticking a ward at the END of the
holder's turn rather than the start would make it visible and would let Shatter reach it, at the
cost of a ward that also covers the caster's own next turn. A default of 2 does the same more
bluntly and is what the owner has just moved away from. Leaving it and accepting that
`WardIsFreeAction` is load-bearing for the Shatter strand is also coherent -- it is one node, and
the strand it gates is hers.

### ~~154. The three relic wards stack on themselves every turn, on a whole-fight duration~~ - closed in `214e7ad7`: NOT a fix -- the owner chose unbounded stacking on purpose, so `RunicWardPointsCap` is deleted and no ceiling replaces it anywhere

Raised 2026-09-16 by the same pass. Wards stack now, and the three non-skill wards re-apply on a
schedule nothing spends: `FightSession.Riders.ApplyRunicWardConversion` fires at the start of every
one of the wearer's own turns, `FightSession.RelicMechanics`' Sparring Buckler fires once per turn
on any action that moves anybody, and `FightSession.Skills.RaiseMagicalShield` fires on a qualifying
cast. All three author `FightTuning.MagicalShieldDurationTurns` (99), so nothing they lay down
expires inside a fight.

The arithmetic: a Runic wearer who hoards mana banks `RunicWardPointsCap` (20) a turn for the whole
fight, unbounded. The cap that used to hold this held it because a new ward REPLACED the standing
one -- "the conversion tops the pool back up" was true while only one ward could stand. It is not
true now.

**Not fixed here, because it is the owner's number rather than a fault in the model.** The options
are a whole-fight ward that refuses to stack on itself (a per-source cap, which would be the first
special case in a model that deliberately has none), a short duration on the relic wards so the bank
drains as fast as it fills, or a ceiling on total shield points. Left standing so the call is made
rather than discovered.


## Findings from gamepad-navigation phase 2, 2026-09-17

### 155. Map and Fight lost "Cancel opens the system menu" via keyboard/gamepad - fixed in `4eab048b`

Phase 2 step B deleted `SystemMenuController`'s own raw `Input.GetKeyDown(KeyCode.Escape)`
poll (it independently opened the menu in every scene that carried it, racing each scene's
own Escape handling -- exactly the double-drive shape `docs/GAMEPAD_NAVIGATION_PLAN.md`
section 1 argues against) and replaced it with the hub's own `NavContext.Cancel` handler
(`HubController.HandleEscape`, now called once by `NavigationInputModule`). Only the hub
got that replacement wired. The map and the fight both carry a `SystemMenuController` too
(`ScreenRegistry.WireSystemMenu`'s three call sites), but neither registers a base
`NavContext` whose `Cancel` opens it the way the hub's does -- `MapController`'s new
`RegisterNavContext` passes `cancel: null`, and `FightController`'s own context (already
registered in phase 1) has never opened the system menu at all, since Fight's own Cancel
means `OnBackPressed` (`_menu.Back()`), a different verb entirely.

**Net effect**: a player on the map or mid-fight can still open the system menu by mouse
(if a HUD button calls `SystemMenuController.Open()` there) but not by pressing Cancel with
nothing else up, which they previously could (via the now-deleted poll, racing or not).
This is a stated, deliberate scope narrowing for step B, not an oversight -- see
`ScreenRegistry.WireSystemMenu`'s own comment -- but it is a real, player-visible
behaviour change and belongs here until it's closed. Closing it needs Map and Fight each
to decide what "nothing else owns Cancel" means for their own screen (Map: probably just
opens it, mirroring the hub; Fight: has to fit alongside `OnBackPressed`'s existing
submenu-depth semantics, which is the harder design question and the reason this was not
done inline).

**How it was closed.** `SystemMenuController.OpenOnCancel(menu)` is the one open path the hub's `HandleEscape`, the map's new `HandleCancel` and the fight's `OnBackPressed` all call. Fight's root case was the design question this finding named: `FightMenuState.Back()` returns false at `MenuDepth.Root` and only there, so a false is exactly "Cancel with nothing else up" and the previous `if (!_menu.Back()) return;` was spending that press on nothing. Submenu, element-list and target-pick Cancel behaviour is untouched.

### 156. Party (gamepad-navigation phase 2, step D) was not attempted - closed in `467770ef`

Steps A (nav declarations + Explicit-link generation), B (SystemMenu nested modal +
Options) and C (RewardTrack) landed; step D (Party's seats/cards as navigable groups,
selection-driven Carrying, the visual acceptance capture) did not, on a considered
call rather than running out of a mechanical budget.

**What was checked.** `PartyFormation.ClickSeat`/`ClickCard`
(`Domain/Party/PartyFormation.cs:197,282`) are already the SAME method for both halves
of the interaction -- called with nothing selected, they pick up; called with
something already selected, they resolve against whatever was clicked (swap, drop,
cancel-on-reclick) -- so Submit-on-a-selected-Button, which fires that Button's own
onClick for free once seats/cards are ordinary navigable Selectables, likely drives
the WHOLE Carrying interaction with no new dispatch code at all. `PartyController`'s
`OnEndDrag`-driven `IndexOfButton` resolution (`PartyController.cs:511-550`) the plan
names is the MOUSE-DRAG path specifically, a second, parallel interaction mode --
not the one a keyboard/gamepad Submit needs to go through.

**What still needs solving, and is not small.** Cancel. The plan asks for "Cancel
calls `Formation.Cancel()` then reselects the source" while Carrying, but an ordinary
Cancel press from ANY of Options/RewardTrack/Party's shared pane today reaches exactly
one handler -- `SystemMenuController`'s own `_navContext.Cancel`, fixed to `Close` at
push time (step B) -- because all three tabs share ONE `NavContext` (Options' own
Cancel-closes-the-whole-menu behaviour, proven by
`SystemMenuGamepadNavigationTests.CancelInsideOptions_PopsExactlyOneLayer`, is exactly
this). Party needs its Cancel to mean "cancel the carry" WHILE Carrying and only mean
"close the menu" once back in Browsing -- a per-pane Cancel override the current
one-Cancel-per-context design has no seam for. That seam (an active pane offered first
refusal on Cancel, falling through to the menu's own Close when it declines) is real,
new design work, not a mechanical extension of step B/C's pattern, and is why this was
not attempted inline. The visual acceptance capture (plan section 8: a live Unity
Editor session driving `tools/screenshot.ps1 -Runtime`, four simultaneous UI states,
someone actually looking at the picture) was not reached for the same reason -- there
was nothing built yet to capture.

**How it was closed.** The Cancel seam this finding called "real, new design work" is `INavCancelClaim`: the active pane is offered the press first (`NavContext.RaiseCancel`), Party claims it while carrying, and Options and RewardTrack decline by not implementing the interface. The finding's own reading of `ClickSeat`/`ClickCard` proved right and is what shipped -- Submit is the Button's own `onClick`, so no new dispatch code exists for the carry at all, and the plan's `IndexOfButton`-fed-by-selection route was deliberately NOT taken (it would have made Submit and a mouse click mean different things on a roster card). The visual capture ran and found the fourth state has nothing to draw: Party's slots carry no hover treatment of any kind.
## Findings from the overnight bug hunt, 2026-09-08


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


## Findings from the total bug hunt, 2026-09-11 (stage 3a, the four seams)


### ~~113. What an extra turn should re-pay: today it re-pays everything, and Black Ram Mode loses two of its three turns in one round~~ -- fixed in `c477205f`: the owner took option 1 -- a bonus action is the SAME turn and re-pays nothing, so `GrantTurnStart` split into `OpenTurnFor` (unchanged) and `ReopenTurnFor` (`_locks.ResetTurn`, `TickPrimaryPool`, and the two recomputes that read them), and the three `[Ignore]`d repro tests are green with a control beside them; full reasoning in the commit message

Found 2026-09-11 by the `FightSession` seam finder (F2) and confirmed by a run.
This is the lead `L1` (rider ordering) that two previous hunts deferred; the
ordering is now written out in full in `docs/archive/BUG_HUNT_2026-09-11.md`, and this
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


## Findings from the total bug hunt, 2026-09-11 (stage 4)


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


## Findings from the total bug hunt, 2026-09-11 (stage 3, the combat seam)


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


## Findings from gamepad-navigation phase 3a, 2026-09-17


### ~~158. Three Hub-covering modals still do not push their own NavContext (RelicDraft, Glossary, Debug menu -- corrected from four)~~ -- fixed in `ccb1b08d`

`HubController`'s own pre-existing comment (`Core/HubController.cs`, the `Update()` method's
gating check) already named this before phase 3a started: the debug menu, the glossary, the
relic draft and the character overlay all cover the hub without registering a context of their
own. Phase 3a wired Hub's own building/gate navigation (`ec81b29f`) but did not touch any of the
four modals -- while any one of them is open, the hub's own `NavContext` (buildings, gate,
`MainMenuButton`) is STILL top of stack, so a stick Move would walk the hub's buildings
underneath whichever modal is covering them. Mouse/click users are unaffected (the modals already
block the raycast). None of the five screens this plan's brief named (Hub, Main Menu, Map,
Talent, Shop) is one of these four, so this is out of scope for phase 3a rather than a gap in it
-- named here so it is not silently assumed to already work.

**Corrected, phase 3b, 2026-09-17: the count above is wrong.** `characterOverlayPanel`
(`Core/HubController.cs`) IS `screen.SystemMenu.Root` --
`Editor/SceneBuilder/ScreenRegistry.cs`'s `WireCharacterOverlay` assigns it directly, and its own
comment says so ("The character overlay: a Modal living inside the hub's own tree... The hub's
own NavContext.Cancel now opens this directly"). The "character overlay" is the same
`SystemMenuController` instance every other scene shares, which has carried its own `NavContext`
since phase 2 step B (`c8837374`) -- it is not a fourth un-navigable modal, it was already fixed
before this finding was written. Only RelicDraftController, GlossaryController and
DebugMenuController remain genuinely unwired. Not attempted in phase 3b (see
`docs/GAMEPAD_NAVIGATION_PLAN.md`'s status header for the sizing reasoning); still open.

**Fixed in `ccb1b08d`** (phase 3, item 1): each of the three pushes its own `NavContext` now,
rebuilt at the end of every repaint (`RelicDraftController.RefreshNavigation`, `GlossaryController.
RefreshNavigation`, `DebugMenuController.RefreshNavigation`) rather than declared once at open, so
paging, a new draft round or a filter change all keep the declared set matching what is on screen.
`HubController.HandleEscape` is simplified to just `SystemMenuController.OpenOnCancel(systemMenu)`
now that each modal's own context sits above the hub's while open -- the debug-menu/glossary/
relic-draft priority branches it used to run are dead code once nothing can reach them. RelicDraft's
Cancel is a deliberate no-op (`cancel: null`) rather than a close, matching the screen's own stated
design ("a draft you can navigate around is not a draft") over the brief's literal "Cancel closes
them" -- closing it would let a player leave without `RunOrchestrator.FinishDraft` ever running.

### 159. ~~Main Menu's Manage Saves and reset-confirm modals are mouse-only~~ -- fixed in `232f310b`

`MainMenuController`'s save-slot NavContext (`d39d955b`) covers the base Play/Continue/Exit list
and the save-slot picker, but deliberately stops there: `ManageSavesButton` (reached and pressed
via Submit like any other wired control) switches to `ManageSavesPanel`, and neither that panel
nor its own `ResetConfirmPanel`/hold-to-delete dialog declares any Selectables or a Cancel
handler. A controller-only player who presses Submit on Manage Saves can reach the panel but has
no stick-driven way back out of it (no Cancel, no Move target) -- mouse/`CloseManageSavesButton`
still works. Scoped out deliberately (the task brief for this screen named only "Main Menu and
its save slots", not the destructive delete flow behind it) rather than missed; a future pass
wiring it should follow the same Reconfigure-on-panel-swap shape `MainMenuController.
RefreshNavigation` already established for the save-slot toggle.

**Fixed in `232f310b`** (phase 3b, item 7): the same shared `NavContext` gained the manage list
and its confirm dialog as two more `Reconfigure` states (four total now), resolved topmost-first
since `ResetConfirmPanel` is a sibling drawn over `ManageSavesPanel`, not a child of it. Cancel
mirrors the same precedence through `ResetProgressController.Dismiss()`/`GoBack()`. The confirm
dialog's Yes/hold button still has no gamepad-Submit path of its own (`HoldToConfirm` is
pointer-only), recorded as a deliberate limitation in the commit message rather than chased --
teaching `NavigationInputModule` a "held button" concept used nowhere else would be out of
proportion for one destructive-confirm dialog.


## Findings from gamepad-navigation phase 3b items 1 and 2, 2026-09-17


### ~~160. Nothing on the dossier or the Reckoning shows WHERE the stick is standing~~ -- fixed in `65ad5325`, REOPENED BY HARDWARE AND CLOSED DIFFERENTLY in `ce405df9`

`65ad5325`'s halos were exactly what this entry asked for and the owner rejected them on a
real pad: "a player can't see where they're going in the character sheets screen: no obvious
selectors". A soft radial glow the same footprint as a cell, sitting under an icon and a
name, is a tint rather than a selector. The halos are gone along with every other one in the
project; `Core/FocusMarker.cs` is the one answer now, on this screen and on the other ten.
The original finding below is left standing because its reasoning about WHY these controls
had nothing is still correct -- it is the remedy that changed.

Both screens wired this pass (`914c249e`, `9c26de94`) are now fully operable on stick + Submit
+ Cancel, and on both of them the only feedback that a control has focus is the tooltip the
focus opens. The controls themselves carry no selected treatment at all: the dossier's slot
cells, ability-score cells and pack cells are `.NoChrome()` (two of them with `Hovers(1.02f)`,
which is a POINTER scale), and the Reckoning's offer cards are `.NoChrome()` with a deliberate
"NO PLATE" note in `Domain/UiKit/Screens/ReckoningScreen.cs` -- a themed plate behind each card
is recorded there as the mistake that "turned three treasures into three menu entries". So none
of them has a `ThemedButtonState`, and `ThemedButtonState.SelectedGlowAlpha`/`Scale` -- the ratio
every themed Button answers focus with -- reaches none of them.

Seen in the capture pass, not deduced: `tools/screenshots/gamepad_phase3/
Reckoning_selected_tooltip.png` has the MIDDLE card selected and nothing in the picture says so.
The four `Dossier_tooltip_*.png` shots have the same hole.

This is `docs/GAMEPAD_NAVIGATION_PLAN.md` section 12.2's open owner call (the selected-visual
treatment is "a candidate pending the visual capture, not approved") arriving on two more
screens, and the same shape Party's own capture already raised for its slots. `Core/
SelectHaloPainter.cs` exists for exactly this case -- an un-themed focusable borrowing the
themed ratio through a plain Image -- and Hub's four buildings and its gate already use it, so
the mechanism is one call per surface. Not applied here unasked: on the Reckoning it would put a
glow behind cards whose own design note argues against exactly that, which is a visual decision
belonging to the owner rather than a wiring gap. The pictures are the ask.

**Fixed in `65ad5325`** (phase 4, item 1). Each of the three genuinely `.NoChrome()` dossier
groups (pack cells, equipment slots, ability-score cells) gets its own `Core/SelectHaloPainter.cs`
halo, wired through the `SelectIndex` these cells already carry from `TooltipFocusRouter.Register`
(job 1) -- `+=` on `Changed`, not `=`, since that delegate was already claimed for the tooltip.
**Narrower than this finding's own "none of them has a ThemedButtonState" read**: re-checking
`UiEmitter.WireThemedButton` and `ThemedButtonState.UpdateGlow` while wiring these three found
that the pack's sort tabs and the pack/spells Close buttons (not named above, but named in the
phase 4 brief) are `.ThemedPlate()`/`.Themed()` and DO already carry a `ThemedButtonState`, which
already answers `ISelectHandler` focus with `SelectedGlowAlpha`/`Scale` for free -- so they were
left alone rather than given a redundant second halo. The Reckoning took the smaller fix this
finding itself anticipated: no new node, `PaintOffers` brightens the card's own existing rarity
halo to `Max(rarity alpha, SelectedGlowAlpha)` instead of a plate, honouring `BuildOffer`'s own
"NO PLATE" note. Re-captured via the same two capture test classes this finding cites
(`DossierTooltipCaptureTests`, `ReckoningTooltipCaptureTests` -- no new capture test needed, the
hole was in what the existing pictures showed) into `tools/screenshots/gamepad_phase4/`; the halo
and the brightened glow are visibly the selected control in every shot.

### ~~161. The dossier's spell-books panel is mouse-only, and its nav rows stay Move-reachable underneath it~~ -- fixed in `7ea33bca`

`CharacterDossierController.RefreshNavigation` (`914c249e`) declares two states -- pack open and
pack shut -- because `DossierPackPanel` is an opaque Image over the whole of column A and the
mouse cannot reach what it covers. `DossierSpellsPanel` is the same shape (same column, same
opaque ground, opened by `SpellsRow`) and got neither: its three spell slots and five unassigned
rows are in `SystemMenuController`'s declared Selectable set (so a mouse click on one sticks, and
the dispatcher will not fight it) but nothing links INTO them, so no Move can reach them --
and, the other way round, column A's own nav rows keep their links while the spells panel covers
them, so a stick can walk onto and Submit a row the mouse cannot click.

Deliberately out of scope rather than missed: this pass's brief named the equipment slots,
ability scores, pack items and pack sort tabs, and the spells panel is a third state of column A
with its own selection model (`_selectedUnassignedRow`, a slot/row pairing that PressSlot and
SelectUnassigned resolve between them) -- comparable in size to the pack itself. The fix is a
third `RefreshNavigation` branch keyed on `spellsPanel.activeSelf`, plus `SpellsCloseButton` in
the graph for the same reason `DossierPackClose` is in it (this pane does not claim Cancel).
`RewardTrackController`'s panel, the dossier's other column-A door, is NOT affected: its own
ribbon and collect button are already wired (phase 2 step C, `da205520`).

**Fixed in `7ea33bca`** (phase 3, item 4): `RefreshNavigation` is a third state exactly as this
finding predicted -- `DeclareSpells`, keyed on the new `IsSpellsShown`, mirrors `DeclarePack`
(the three spell slots and the unassigned-book rows as two Lists, `SpellsCloseButton` in the
graph). `RefreshSpells` now calls `RefreshNavigation()` at its own end, which is the literal bug
this finding named: opening or closing the panel never touched the graph at all before this,
regardless of which state `RefreshNavigation`'s own branch would otherwise have resolved to.
Found while wiring, not predicted here: neither `ShowPack` nor the old `ShowSpells` ever
explicitly reselected on open/close, and the dispatcher's own next-frame reselection rule cannot
paper over it -- `SystemMenuController.RefreshSelectables` declares every Selectable under the
whole panel regardless of visibility, so a row hidden behind a panel it just opened still reads
as "declared" and the rule stays silent. `ShowSpells` now selects explicitly on both edges;
`ShowPack`'s identical, older gap is left alone -- untested today, and fixing an unrequested
control is a separate change from wiring this one.


## Findings from gamepad-navigation phase 4, items 3 and 4, 2026-09-18


### ~~162. `JourneyToFirstFightMouseTests` (and once, `JourneyHubToTalentsMouseTests`) fails in a large batch, never alone~~ -- fixed in `bd6f80af`: `JourneyFixture.MoveMouseTo` aimed the scripted pointer at the target's PIVOT rather than its rect centre, which on the Hub's gate (pivot 0.5/0, rect y:0) is the rect's own inclusive bottom edge, and the hover scale-up the pointer's own arrival starts then shifts that edge a fraction of a pixel away from the frozen pointer before the MouseDown frame arrives.

`JourneyToFirstFightMouseTests.MainMenuToNewGame_ThroughTheHubGateAndTheDraft_ReachesTheFirstFight_MouseOnly`
("clicking the gate never opened the relic draft on a run that has not drafted one yet", expected
`True`, got `False`) failed in every one of four batched runs this pass tried against it (three
full `tools/run_tests_parallel.ps1` runs and one `tools/test.ps1 run`, 341 tests) --
`JourneyHubToTalentsMouseTests.ATier1Orb_BeforeItsRootIsUnlocked_IsRefused_BackReturnsToTheHub_MouseOnly`
("clicking TalentsBuilding should load the Talents scene", expected `"Talents"`, got `"Hub"`)
joined it in two of those four. Both are new files this pass added (item 3, the mouse-only
regression); nothing pre-existing is affected.

**Not reproducible in a small slice.** Both classes pass reliably, every time, run alone or as
part of the full ten-class new-test slice (`tools/test.ps1 <the ten new classes>`) -- confirmed
five separate times across this investigation. The failure needs a LARGE batch to appear at all,
but not a specific one: the fourth reproduction (`tools/test.ps1 run`, 341 tests, not the full
1240-test gate) is what disproves this investigation's own earlier, narrower theory --
`JourneyToFirstFightMouseTests` failed there with `JourneySystemMenuToMainMenuTests` (ending on
the Main Menu, no run, no draft) as its immediate predecessor, not `JourneyHubToShopTests`
(ending on the Map, mid-run) as in every earlier reproduction. Whatever this is, it is not tied to
one specific predecessor's own leftover state.

**Two mechanism-level fixes landed along the way, neither of which resolved this** (kept because
both are real, independently-verified fixes -- see `NavigationInputModule.cs`'s own comments and
segment 3/4/(a)/(e) of `JourneyMixedInputTests.cs`):
1. `NavigationInputModule.ReselectIfOutsideDeclaredSet` now prefers the selection captured before
   `base.Process()` runs, when the SAME top context still declares it, instead of always falling
   back to `NavContext.Entry` -- fixed a genuine, previously-unproven regression (a mouse click on
   a `Navigation.Mode.None` stepper button deselects the row behind it, per
   `PointerInputModule.DeselectIfSelectionChanged`'s own `ISelectHandler`-based check, which
   `Navigation.Mode.None` does nothing to prevent). See #163.
2. `JourneyFixture.RootCanvas()` now scopes its `Canvas` lookup to `SceneManager.GetActiveScene()`
   rather than a bare `isRootCanvas` check, ruling out a theorised stale-canvas read from another
   fixture's own hand-built, `Object.Destroy`-deferred canvas. Applied, verified harmless, did not
   change the failure.

**Ruled out, not merely suspected:**
- Not a settle-time-after-scene-load race in the simple sense: raising the post-`TakeOverInput`
  real-time wait from 0.2s to 0.5s, every mouse test, every scene load, made no difference across
  two full-gate runs -- if the true cause were "not enough real time before the click," more of it
  should have helped at least partially, and it changed nothing at all.
- Not `RunManager` state leaking across tests: `RunManager.ResetForTests()` only clears the
  cached map (`Forget()`); run/draft state lives on `SaveSlotManager.CurrentSave`, and every
  journey test (this one included) opens its own throwaway `SaveSystem.RootOverride` directory
  with no pre-existing save file, so a prior test's run cannot be read back by this one.
- Not `Navigation.LoadOverride` left stubbed by an earlier fixture: had that been true,
  `SaveSlotManager.EnterSlot(0)`'s own `Navigation.Go(Hub)` call would have been a no-op and this
  file's own `WaitForScene("Hub", ...)` would have timed out with a DIFFERENT message ("clicking
  an empty slot should enter it and load the Hub") before ever reaching the gate click -- every
  observed failure is the LATER assertion, so the Hub scene did load.
- Not a specific predecessor's leftover state (see above) -- the one candidate this investigation
  had most confidence in until the fourth reproduction contradicted it.

**A structural difference worth naming, not yet confirmed as the cause.**
`JourneyToFirstFightMouseTests` is the only one of the nine mouse files that reaches the Hub
through a click-triggered scene load rather than a top-level one: its Hub load happens as a side
effect of `SaveSlotController.Choose(0)`'s own `EnterSlot(0) -> Navigation.Go(Hub) ->
SceneManager.LoadScene(Hub, Single)` (SYNCHRONOUS, per `Navigation.Go`'s own implementation),
fired from inside the SAME `EventSystem.Update()` call this suite's own scripted `Click` on
Slot0Button drives -- every other mouse file calls `SceneManager.LoadSceneAsync` directly at its
own top level instead. `JourneyHubToTalentsMouseTests` does not share this shape (it loads Hub
directly), which is consistent with it failing less often (2 of 4) than
`JourneyToFirstFightMouseTests` (4 of 4) if this really is the mechanism, but that difference in
RATE is not proof by itself.

**Left open rather than forced.** Both tests assert real, correct production behaviour (verified
by their own reliable passes in every small-slice configuration tried), so weakening either
assertion to paper over an unreproduced-in-isolation symptom would hide a real claim behind a fake
pass -- the standing rule this project's own `docs/CODE_STANDARDS.md` states. Whoever picks this
up next: start from the synchronous-load-from-inside-a-click theory above (it is the one concrete,
falsifiable difference this investigation found and did not have time to test in isolation -- e.g.
by rewriting this one file's Hub transition to poll for scene readiness after the click rather
than assuming `WaitForScene` plus a flat settle is enough), and reproduce with
`tools/test.ps1 run` (341 tests, ~140s) rather than the full ~450s gate -- it reproduces there too
and is far cheaper to iterate against.

**RESOLVED, and the synchronous-load theory above was wrong.** Five instrumented reproductions of
`tools/test.ps1 run` (a temporary per-frame dump of `EventSystem.current`, the active scene, the
context stack, a fresh `RaycastAll` at the pointer, and the target's own rect geometry -- all
removed again) put the whole mechanism on the record:

```
frame N   localPt=(0.000, 0.000)  contains=True   hits=1 <StartRunGate>
frame N+1 localPt=(0.009,-0.009)  contains=False  hits=0      <- the MouseDown frame
frame N+2 localPt=(0.010,-0.010)  contains=False  hits=0      <- the MouseUp frame
```

`MoveMouseTo` aimed at `((RectTransform)node.transform).position`, which is the PIVOT and equals
the centre only at pivot (0.5, 0.5). `StartRunGate` is pivoted (0.5, 0) -- rect
(x:-310, y:0, w:620, h:620), pivot flat on the bottom edge, because a building is placed by the
ground it stands on -- so the pointer sat exactly on `yMin`, inside only because `Rect.Contains` is
inclusive there. The `MoveMouseTo` frame then fires `OnPointerEnter`, `ButtonPressAnimator` starts
lerping the button toward `HoverScale`, and the sub-pixel shift that puts in the pivot's screen
position (measured 78.320 -> 78.323 at a 0.333 canvas scale) moves the frozen pointer 0.009 canvas
units BELOW `yMin` on the very next frame -- the frame carrying `MouseButton0Down`.
`GraphicRaycaster` finds nothing, the press lands on no target, the release has no `pointerPress`
to match, and the Button's `onClick` never fires.

Batch size decided which way the coin fell because that first hover step is
`Time.deltaTime`-driven: a loaded run's longer frame moves the pivot a measurable fraction of a
pixel, the same test alone moves it too little to leave the edge. So "needs a large batch but not a
specific one" was the symptom of a knife edge, not of leaked state -- which is also why every
theory above about a predecessor, a settle time or a scene load from inside `Process()` could be
ruled out one after another without getting closer. Everything those theories suspected was
measured as sane at the failing frame: `EventSystem.current` was the Hub's own and focused, one
EventSystem alive, one context on the stack, the gate interactable, the module never deactivated.

Fixed at the aim point: `MoveMouseTo` now aims at `rect.TransformPoint(rect.rect.center)`, which is
what that fixture's own contract already claimed and what a mouse player aims at, and is robust by
construction -- a rect's centre is interior to it for every pivot. `WorldPointAtFraction`'s local y
gets the same correction for the same reason (it was 0, the pivot's row). Identical for anything
already pivoted (0.5, 0.5), so the other eight mouse files and the mixed-input file are
bit-identical. Three consecutive `tools/test.ps1 run` runs green plus a full
`tools/run_tests_parallel.ps1`.

### ~~163. A click on a Navigation.Mode.None Selectable (a stepper button, a background click) drops the row's own selection to the context's Entry, not back to the row~~ -- fixed in this pass's own `NavigationInputModule.cs` change

Found writing item 4's own mixed-input pass, rule (e): `OptionsController.cs`'s own comment on
`stepPrev`/`stepNext` states "does nothing to the row's own remembered focus, since
`OnPointerDown` never calls `SetSelectedGameObject` for a `None`-mode Selectable" -- true as far
as it goes, and incomplete. `Navigation.Mode.None` stops the CLICKED button from being reselected;
it does nothing to stop `PointerInputModule.DeselectIfSelectionChanged` from nulling whatever WAS
selected, since that check walks up from the clicked object looking for any `ISelectHandler`
ancestor (a bare `Selectable`/`Button` implements it regardless of its own `Navigation.Mode`) and
compares that against the current selection -- a stepper button, or a plain background click that
hits nothing declared at all, both null the row/building that was selected a moment ago. Before
this pass, `NavigationInputModule.ReselectIfOutsideDeclaredSet`'s only answer to a null selection
was `NavContext.ResolveSelection()` (`RememberedId ?? Entry`), and grepped, `NavContext.Remember`
is called nowhere in this project -- so the fallback was always `Entry`, silently, meaning a
stepper click (or a background click) drops focus to the screen's declared entry rather than
leaving it where it was.

**Not `NavContext.RememberedId`'s job either.** That field is the CROSS-VISIT case (a modal
reopened later restoring what it last had selected, which nothing in this project wires up yet --
`DebugMenuGamepadNavigationTests`' own pinned claim, "HubController never calls
`NavContext.Remember`, so there is no per-node memory to restore, only Entry," is about exactly
that case, a Cancel that POPS a context, and stays true and unaffected by this fix). This bug is
the SAME-VISIT case: the top context never changed, only a click's own deselect-without-replace
ran through it.

**Fixed**: `NavigationInputModule.Process` now captures `EventSystem.current.currentSelectedGameObject`
BEFORE `base.Process()` runs, and `ReselectIfOutsideDeclaredSet` prefers that captured value over
`Entry` whenever the SAME top context still declares it as one of its own Selectables -- which is
true only for the same-context click case, never for a context that was just pushed or popped
(the previous selection belongs to a DIFFERENT context in both of those, so `ContainsSelectable`
answers false and `Entry` still runs, matching `DebugMenuGamepadNavigationTests`' own claim).
Proven at the journey level by `JourneyMixedInputTests.cs` rules (a), (b) and (e) -- a background
click on the Hub, a click through a System Menu modal, and a mouse click on an Options stepper
button, each restoring the pre-click selection in the same frame rather than falling back to
Entry.

**THE CROSS-VISIT HALF, the one the paragraph above deliberately left standing, is fixed in
`fd7c8984`**: `NavContext.Remember` is now called -- by `NavigationInputModule.Process`, once, for
every context, after the post-dispatch reselection has settled the frame -- so a context popped or
re-entered lands where the player left it rather than on its entry. Three things needed fixing
before "remembered ?? entry" could mean anything, each a model problem rather than a missing line:

1. **A context destroyed on close can never satisfy "Push selects remembered".** SystemMenu, the
   debug menu, the glossary and the shop each built a fresh `NavContext` on every open and nulled
   the field on every close. Their contexts now outlive their time ON THE STACK (created once, put
   back with the new `NavContextStack.PushIfAbsent`, removed but not discarded on close). The other
   seven are one-per-scene or one-per-fight and are untouched -- nothing re-enters them without a
   scene load, which resets the stack anyway.
2. **"Still valid" had to mean usable, not declared.** A controller declares what it owns, not what
   is on screen, so a hidden or destroyed node is still a member of its context's set. The whole
   rule now lives once in `NavigationInputModule.SelectionFor` (remembered if shown and alive, else
   entry); `NavContext` keeps only the half it can answer engine-free (`RememberedSelectable`), and
   `NavContext.ResolveSelection` is gone rather than left beside it.
3. **The same gap on the CURRENT selection**, which plan section 6 already specified and nothing
   implemented ("if the focused node vanishes mid-session... the entry if none remain"):
   `ReselectIfOutsideDeclaredSet`'s early-out tested non-null and declared, so hiding the control
   that held the focus left the focus on something the player can neither see nor move off.

Proven by `FocusMemoryGamepadNavigationTests` (the Hub's pop case, the System Menu's cross-visit
case, and a remembered node hidden after the fact falling back to entry), all three through the
real dispatcher. Two existing tests were adapted rather than relaxed, both because they encoded the
absence of memory -- `DebugMenuGamepadNavigationTests`' own pinned claim passes unchanged and only
its reason was stale (nothing in it moves off the gate, so memory and entry agree, and both
readings hold), while `DossierGamepadNavigationTests`' tooltip-on-close test genuinely changed
behaviour and is renamed to say so: a reopened menu now restores the remembered cell, and a box
describing the selected cell is section 7's tooltip following focus rather than a stale flag
surviving.

**One limitation stated rather than hidden**: `SystemMenuController` keys its declared set by
position (`s0`, `s1`, ... off a hierarchy walk) rather than by name the way the debug menu and the
glossary do. That is stable across a close and a reopen, so the memory above is correct there, but
a pane that rebuilt its rows between visits would restore focus to the same POSITION rather than
the same control. Left alone deliberately -- a name-keyed set is a change to what that screen
declares, with its own duplicate-name risk for runtime-instantiated rows.


## Findings from hardware round 1's visual pass, 2026-09-18


### ~~169. Fight's ATTACK verb wore the branch-is-open plate at rest, forever~~ -- fixed in `0995736b`

"Attack is always seeming to be hovered over (not a gamepad bug) and makes it difficult to notice
if you hover/select it." `FightController.Hud.RefreshVerbs` computed
`highlighted = i == active || (active < 0 && i == _focusedVerb)` and painted `ThemedMenuState.Open`
for anything highlighted. `_focusedVerb` is 0 from the moment the controller wakes and is never -1
-- Fight's model keeps a focused verb at all times because Submit has to have something to press --
so with no branch open the second clause was unconditionally true for index 0.

Worth recording as a class rather than an instance: the code's own comment asserted the two states
were "mutually exclusive in practice", and `FightFlowTests` had written the SAME wrong belief into
an assertion with a comment explaining it ("`highlighted` is already true for ATTACK before
anything is ever pressed"). A green suite pinned the defect. What caught it was a person holding a
controller.

Drawing only; `_focusedVerb`, `MoveFocus` and `ConfirmFocus` are untouched.

### ~~171. OWNER'S CALL: ATTACK still wears the Primary ring at rest, and that is a separate decision from #169~~ -- closed in `b12a1682`: the owner made the call ("the attack button in the fight menu is still glowing always") and the ring is gone from the verb column entirely

#169 removed the open-branch plate from the resting verb column. What remains on ATTACK is
`ThemedMenuState.Primary` -- the recommended-default gold ring it has always had, from
`i == 0` and nothing else -- which is visible in `tools/screenshots/gamepad_visuals/
marker_fight_verb.png` as a warm glow the other three verbs do not have.

That is the intended design of the verb column and it was not touched by this pass. But the owner's
complaint was about what ATTACK LOOKS LIKE at rest, and half of what it looks like is still there.
If the ring is what read as "hovered" rather than the plate, the fix is to drop Primary from the
verb column entirely and let the hotkey number carry "this is the default" -- a one-line change in
`RefreshVerbs`, not a mechanism. Not done unasked: a recommended action is a real thing to signal
and removing it is a design decision, not a bug fix.

**Closed in `b12a1682`, by the owner's answer rather than by a new argument.** The ring WAS what
read as hovered -- the complaint survived #169's fix unchanged. `RefreshVerbs` now computes
`i == active ? Open : Idle` and nothing else, so the resting column carries no ring on any verb,
and "ATTACK is the default" is left to the hotkey number beside it: already on screen, costing no
glow, and impossible to mistake for focus or hover. The one-line change the finding predicted, at
the cost the finding named. `ThemedMenuState.Primary` itself survives with one production user
left, which is #175 below.

### ~~182. A formation spell layer whose beat struck nobody spends a pooled renderer on a zero-sized box~~ — fixed in `818a00eb`: `PlaceOnFormation`'s `stood.Count == 0` branch now sets `instance.Placed = false` before returning, matching `PlaceOne`'s `on == null` branch

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

**How it was fixed.** The one-line fix predicted above: `PlaceOnFormation`'s
`stood.Count == 0` branch (current file: `FightController.SpellVfx.cs:642`) now sets
`instance.Placed = false` before returning, the same shape as `PlaceOne`'s
`on == null` branch (current file: `:380-384`).

### ~~209. Clearing `VfxPaddingCache` per fight breaks a travelling-effect placement test -- cause not chased down~~ — fixed in `756abfbf`: a cold scan was a 35ms hitch that outlasted the fixture's 11ms 60x flight, so the test read a released renderer; scan is now row-wise from the bottom, cache cleared per fight and keyed by (path, first frame)


`FightController.SpellVfx.cs:190`'s `VfxPaddingCache` has the identical
stale-measurement hazard as its three siblings `ContentCentreCache`,
`ContentTopCache`, `OpaqueBoxCache` (cleared in `ResetStagePresentation`,
`FightController.StageVisuals.cs:~1303-1321` -- an asset-only re-slice never
invalidates any of the four, and the other three are cleared per fight for
exactly that reason). Adding the same `.Clear()` for `VfxPaddingCache` looked
like the obvious missing line and is what a straight read of the class asks
for.

It breaks `SpellVfxTests.ATravellingEffectStartsOnTheCasterAndEndsOnTheTarget`
(fails in isolation and as part of the class; confirmed by bisection --
reverting only this one `.Clear()` call, with the other two fixes from the
same pass left in, turns the whole 45-test class green again). The symptom:
`_player.Image.rectTransform.anchoredPosition.x` reads the CASTER's position
(-320) once the flight finishes, instead of the TARGET's (300) -- i.e. the
travelling box never advances past its launch point, though `IsPlaying`
correctly goes false.

**Not chased to a root cause.** The suspect is `VfxContentPaddingFraction`
(`FightController.SpellVfx.cs:~1239`), which now does a real, uncached pixel
scan of `Spells/mud_burst`'s frames inside this specific test's `PlaceCast`
call rather than reusing a value an earlier test in the fixture had already
warmed permanently into the (previously never-cleared) static dictionary.
Only `VfxDeadSpaceBelow`'s Y correction reads that value on this code path
(`to.y`, not `to.x`), and the caster-side launch X (`CasterCastPoint`'s
fallback) is `casterX` regardless of it -- so how a Y-only measurement stalls
the box at its launch X specifically was not established. Left as filed
rather than fixed blind, per this project's own "an audit that reads the
source of truth cannot see what a later stage synthesises" lesson (#39,
`docs/AUDIT_STRUCK_ARCHIVE.md`) -- there is a real seam here nobody has
looked at closely enough yet.

`VfxPaddingCache` is therefore left **uncleared** for now, same as before
this pass (`FightController.StageVisuals.cs`'s `ResetStagePresentation` has a
comment at the clear-caches block saying why, pointing here). The bug this
was meant to fix (a long Editor session carrying a stale padding measurement
across an asset-only re-slice, same as the other three caches) is still open.

**How it was fixed.** The mechanism was time, not position. `BottomPaddingFraction`'s
whole-frame `GetPixels` over mud_burst's 14 compressed 512x512 frames took 18-35ms inside the
cast's frame. At the fixture's 60x the whole 0.65s flight is 11ms, so the next frame's `Tick` was
already past the cast's end: `Advance` closed the layer and released the cast with no `Paint` in
between, and the test (wait for `IsPlaying` false, then read the renderer) read the last position
ever painted -- `Begin`'s paint on the caster. To/From were identical cold and warm. Players never
saw a stall (a 35ms frame does not swallow a 0.65s flight at 1x); they got the hitch on a sheet's
first standing/travelling cast. Fix: the scan reads row by row from the bottom and stops at the first
opaque row (mud_burst 8-9ms, frost_flare under 1ms, same answers); the cache is cleared in
`ResetStagePresentation` with its siblings and keyed by (path, first frame scanned), since the
result depends on `impactFrame`; the travelling test holds the clock and reads at 0.5s, and
`AColdPaddingScanLandsATravellingEffectWhereAWarmOneDoes` pins cold and warm casts to -320 -> 300.

### ~~90. `FightSubmenuLayout.FrameContentCentreY`'s comment states a kit-delivery-old inset split~~ -- obsolete: code removed in `47358962`

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

**Obsolete:** `FrameContentCentreY` and `FrameInset` were both removed from `FightSubmenuLayout.cs`
in `47358962` ("Playtest batch: hover box, gamepad maps, talent kindling, fight HUD card",
2026-09-23) — the flat-fill rework replaced the inset-frame layer with `FrameWidth`/`FrameHeight`
reading straight off `ContainerWidth`/`ContainerHeight(For)`, so there is no longer a comment or an
inset split to reconcile.

### ~~46. Twelve `Assert.Ignore`s skip on CONTENT shape, and three of them guard the regression #42 describes~~ — fixed in `794f2278`: the twelve named sites were already converted in `0625b823`; sixteen newer content-shape skips turned into hard failures or fixtures

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

### ~~50. `SaveData.relicLoadout` is written by nothing and read by nothing~~ — superseded: its subject (`SaveData.relicLoadout`) was deleted in `a7ebbf28` under #119

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

### ~~77. `ContentDatabase.ValidateContent()`'s six pre-existing whole-catalogue rules have no test~~ — fixed in `51a3dba6`: the six rules became pure predicates in `Domain/Content/CatalogueCrossChecks.cs` with one fixture test per rule

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

### ~~78. `docs/BOT_SUMMARY_SCHEMA.md` lists room fields in two places~~ — fixed in `2fffa3d5`: the `runs.jsonl` `rooms[]` section points at the `RoomTrace` block

The `traces.jsonl` `RoomTrace` block is now covered by `tools/bot_schema_test.py`
(finding #71), but the `runs.jsonl` `rooms[]` prose list — a second, independent
listing of the same fields inside the same doc — still omits
`learnedSpellCountAfterRoom`, `unassignedSpellBookCountAfterRoom`, and
`spellAssignments`. `7975c5a1`'s own commit message flags this exact gap as
deliberately out of scope. Two listings of one fact inside one doc is the finding; the
fix is one section pointing at the other, not a second test. ~10 min.

### ~~79. `ContentTop` has the same crop-offset bug the ring measurement had~~ — fixed in `6e8c71d5`: `ContentTopForActor` reads `OpaqueBoxForActor`'s measured box; `FootBandCentreFraction` uses `textureRectOffset`; `IntentBadgeContentTopTests` pins the rat idle. Note: intent badges on trimmed idles now sit higher, and this has not been looked at on screen yet (see #211)

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

### ~~99. `SystemMenuController.MeasuredLabelWidths` has no fallback for a zero-width live measurement~~ — resolved: cleared by test on 2026-09-08 per its own entry

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

### ~~101. `CombatEncounter.UpcomingTurns` throws on a zero-length ask, and its one caller has no guard~~ — fixed in `27034b2b`: `FightSession` refuses `initiativeSlots <= 0` at construction; `RefreshInitiative` returns early on an empty icon array

`CombatEncounter.cs:193-197` throws `ArgumentOutOfRangeException` for `count <= 0`.
`RefreshInitiative` (`FightController.Hud.cs:1698`) always calls it with `initiativeIcons.Length`,
which is 6 today and therefore never zero -- unreachable in production, but the call site trusts
a `[SerializeField] Image[]` to never come back empty, and nothing states that assumption where
the call is made.

### ~~110. `run_tests_parallel.ps1` reports "All tests passed" off a STALE results file~~ — fixed in `44473ae6`: per-run results file names, a failed delete refuses the run (helpers in `tools/unity_lock.ps1`)

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

### ~~157. Two Combat/Stage tests fail on this tree, unrelated to phase 3a's own changes~~ — resolved with no change: both named tests pass on this tree (`tools/test.ps1 FightPlayableTests,StageFormationTests`: 10 passed, 1 legitimate graphics skip)

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

### ~~165. `FightTeardownLifecycleTests.TwoPopsOnOneBadgeLeaveItAtItsRestScale` can exit its own wait at a value its own assertion rejects~~ — fixed in `b1adf41b`: the wait, assertion and hold check share one `AtRestScale` predicate

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

### ~~170. The focus marker attaches to its target's own canvas, and "root canvas, last sibling" would have been wrong~~ — recorded, not a defect: caught before ship

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

### ~~183. The reel is four times slower, and the two legs that moved are the two that are motion~~ — recorded, not a defect: landed change, recorded for history

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

### ~~185. "Playback finished" no longer implies "the stage is at rest", and five fixtures had to learn it~~ — recorded, not a defect: fixed as fallout in the same pass

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

### ~~187. `BeginStatusTickBeat` duplicates `BeginBeat`'s constructor to skip one line of it~~ — fixed in `efbce0fa`: one `NewBeat` constructor

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

### ~~189. `b.Actor != null && !b.Actor.IsPlayerSide` stopped meaning "an enemy turn" the moment ticks became beats~~ — fixed in `559f3069`: `CombatBeat.IsAction` predicate across 13 sites; later refined by `fb0a06ab` (`BeatCause`)

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

### ~~195. Rooted gaining "no physical moves" silently retuned the Sylvan modifier~~ — recorded, not a defect: the rule change working as intended

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

### ~~172. OWNER'S CALL: the marker's edge is derived from the control's aspect, not authored per control~~ — resolved in `86abfe67` + `7a2ebe3e`: one general rule (preferred edge by shape, next clear edge if the marker would overlap another control or text; scrollbars stepped past), no per-control overrides

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

### ~~202. OWNER'S CALL: the dossier item tooltip's reused comparison panel crosses the Legs-slot mannequin art~~ — fixed in `0660b1bf`: tooltips place around keep-out rects (mannequin, all eight slots, Carried row) and size to content; plate opaque in `f15a5620`

The dossier tooltip now reuses `ItemComparisonPanel` (420x420, font 15) with
its title left-aligned as the shop's is. Beside the Legs slot the panel
crosses the mannequin art — the existing 300x480 tooltip already had the same
overlap there. Owner: accept it, add a title-alignment parameter, or move the
anchor rule so Legs clears the art.

### ~~203. Hands-on QA still pending for nine landed systems; no runtime captures taken~~ — QA capture pass done 2026-09-26, report at `docs/captures/qa-2026-09-26/REPORT.md`; defects it found fixed in `27e8f67f`, `8ddb135f`, `bee93472`, `8fb2dc9c`, `5b818279`, `8852f19d`, `f3918a88`, `fc5bbd72`, `7bf45849`, `e1328743`, `0660b1bf`, `86abfe67`, `6e6d63f5`, `ce1db1d2`, `d418a31f`, `f15a5620`, `7a2ebe3e`, `29ca1c97`, `f1b49295`, `36c4aca3`, `7a53c8ec`

Attributes panel with pad Left/Right/Submit; fight detail card (compact rows,
damage-type tag on POWER); five-row skills list; shop comparison panel and
LB/RB character picker; enemy plates (head-zone icons, ward segment,
hit-strength recoil tiers); Blackglass Spear / Winter's Rebuke spear layers;
Thorn Tithe `fit: target`; treant slam and spores. No captures were taken
because graphics-mode launches steal the owner's focus. Owner: schedule a
focus-safe QA pass.

**2026-09-26:** a focus-safe QA capture pass is scheduled next in this sweep.

### ~~204. Graphics-mode tooling still steals focus; the hidden-desktop fix is an untested candidate~~ — fixed in `33171e6b`: graphics launches run on the hidden desktop PPHeadless by default (real GPU, Direct3D 12), opt-out `PP_GRAPHICS_DESKTOP=visible`; `focus_check` proof zero foreground changes. Captures now render at 1920x1080 (`d44b0695`). Runner copies claimed, never cleared blind (`46ffce90`, `bf88a7dc`)

`tools/screenshot.ps1 -Runtime`, `graphics_tests.ps1`, and `preview.ps1
-Launch` all open a focus-stealing window; only `-nographics` launches use the
hidden desktop (`docs/INCIDENTS.md`, "Batchmode Unity steals focus").
Candidate fix: route graphics launches through the hidden desktop too, proven
with `tools/focus_check.ps1`. That proof run itself pops windows, so it needs
scheduling for when the owner is away.

### ~~205. OWNER'S CALL: a sky strike at a tall target starts under the combat log~~ — resolved by owner rule, fixed in `fc5bbd72`: layers that would reach the log band are shortened about their anchor; the log never overlaps

`place: sky` sits 110 above the taller body, capped at 240. An Elder Treant's
head reaches ~240 in the front rank, so Winter's Rebuke / Blackglass Spear /
Crownfall amass at the cap and the spear's upper end crosses the bottom lines
of the combat-log bark (capture: `spell_winters_rebuke_vs_treant_after.png`).
Owner: lower `SpellFlight.SkyCeiling`, or accept the overlap for tall mobs.

### ~~207. RECORDED: spell previews anchor sample 0 at the press, so the file labels run early~~ — fixed in `ce1db1d2`: preview samples taken from the cast's own clock

`PreviewCaptureTests.CaptureSpellCast` reports "sample 0 = the frame the cast
first drew (0 frames after the press)" for every sheep spell tried: something
is already drawn at the press, so the release anchor never waits. The damage
popup lands ~8 samples after the scheduled `_impact`, so `_impact.png` shows
the moment before the blow and `_tail.png` shows the blow. Unfixed.

### ~~211. OWNER'S CALL: intent badge height moved on trimmed idles (from #79) and is unreviewed on screen~~ — reviewed on screen 2026-09-26: rat and beetle badges sit ~50-60px above the body, same gap; treant ~85px (REPORT.md §3). Resolved

#79's fix (`ContentTopForActor` reading `OpaqueBoxForActor`'s measured box)
moves the intent badge higher on trimmed idles than it sat before. Correct
by the new measurement, but nobody has looked at it in the running game yet.
Pending the runtime QA capture pass (#203).

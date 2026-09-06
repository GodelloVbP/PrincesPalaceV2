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

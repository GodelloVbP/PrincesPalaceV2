# Repo-context review of PLAN_RUN_MAP.md rev 3

Reviewer: Sonnet 5.5 (`implementer` agent, read-only), checked against `main`
at `1914a617`, 2026-10-13. Verbatim report below. Findings are unapplied. The
orchestrator triages them (see `README.md`). Finding 1 was spot-checked by the
handing-over session and holds: `CombatEncounter.cs:342` refuses on
`_enemies.Count >= maxSlots`, and `FightSession.Enemies.cs:171,181` says the
list never shrinks and a slot "cannot re-open today".

## Verdict
Not ready. §6's reinforcement queue cannot work on the current encounter model: the enemy list never shrinks, so a freed slot never reopens. §2/§4 also contradict each other on whether strength is a generator input or derived from enemies. Several rev-3 mechanics overlap code that already pays rewards. With those fixed, the rest is ready after fixes.

## Findings

1. **[blocker]** §6 "The rest wait their turn... When an enemy slot frees, the next one steps in through `TryAddEnemy`" — `TryAddEnemy` counts the append-only enemy list, corpses included, so no slot ever frees.
   - Evidence: `Domain/Combat/CombatEncounter.cs:342` `if (enemy == null || _enemies.Count >= maxSlots) return false;`. `FightSession.Enemies.cs:~165` says the list "NEVER SHRINKS ... a cap on total ever fielded, corpses included ... A slot cannot re-open today". `Core/FightController.cs:~866` says `SlotIndexOf` equals the list index, so an appended enemy at index 3 or higher has no stage position and is invisible.
   - Failure: boss plus two anchors fills the 3 slots. Every queued anchor is refused for the whole fight, and the "queue as a clock" mechanic does not exist.
   - Fix: add to §6's "check before sizing" list that slot semantics need a contract change. Options are a reseat contract (a new enemy takes a dead enemy's list index and slot, with changes in `CombatEncounter`, `FightController._slotOf`, `LivingRankOf` and the turn order) or dropping the queue. That makes P5 `Escalation: architecture` for a second reason, not only the leader rule.

2. **[major]** §2/§4 vs §5 — strength is both a generator input and derived from rolled enemies.
   - §4 profile has a "fight-strength mix", the validator and §2 table require "a strength-3 fight" in some pockets, and the fuzz test pins counts. But §2/§5 derive strength from `EncounterRoll.Roll` enemies, which needs `ContentDatabase` (`RunEncounter.cs:120-123`, `Pool()`), which the generator must not read.
   - Failure: the generator cannot place "strength-3" or satisfy "every pocket holds a prize" using strength it cannot compute. An implementer must guess one of two designs.
   - Fix: pick one. Either strength is rolled as an enum by the generator and the enemy roll is constrained to it (changes `EncounterRoll`), or strength is derived after generation. In the second case, remove strength from the profile and the generator validator, and add a post-pass in P2.

3. **[major]** §6 "boss takes one slot, the two strongest anchors take the other two" vs an anchor being an elite node. An elite rolls 2 enemies (`Domain/Dungeon/EncounterRoll.cs:61` `EliteEnemyCount = 2`), and the §6 "Fight build" bullet says the roster takes "the two strongest living anchors' rolled enemies". That is 4 enemies plus the boss against 3 slots (`FightHudSpec.cs:51`).
   - `EnemyCandidate.SlotSpan` also exists (`EncounterRoll.cs:28`, `EncounterRoll.cs:197` budget). `TryAddEnemy` ignores span and counts enemies.
   - Failure: unbuildable as written. The text does not say whether one anchor means one enemy of the pair, the whole pair, or a re-roll.
   - Fix: define an anchor as exactly one fielded enemy (name which one, e.g. the strongest of the pair). State that its span must fit, and how the boss's own `SlotSpan` is handled.

4. **[major]** §6 "Check before sizing" — the plan leaves open questions the code already answers, and the answers are bad news.
   - Elite scaling is per roster, not per enemy: `Core/FightEncounterAdapter.cs:538-539` passes one `isElite` to `ToCombatant` and `EnemyKitFor`, and `:566` gives `FightSession` one `IsEliteFight`. A boss fight (`isElite:false`, `EncounterRoll.cs:147`) cannot field elite-scaled anchors without a per-enemy flag.
   - `VictoryRewards.For(..., IsEliteFight, ...)` (`FightSession.Outcome.cs:132`) applies one elite multiplier to all kills, so an anchor killed at the heart pays boss-fight base exp and gold, not elite.
   - The summon factory closes over that build's `isElite` and `depthStep`.
   - `IsSummon` is set by the caller after `TryAddEnemy` (`FightSession.Skills.cs:823`), so a reinforcement path can simply not set it. It does need a new `FightSession` method to register `_enemyKits[state]`, which is private.
   - Code assumes one boss: `FightController.cs:549-553` ("A BOSS ROOM FIELDS EXACTLY THE ONE DECLARED BOSS... the front slot is always it"), `EncounterRoll.cs:145-148` returns exactly one id, and `BossIdOf` (`RunOrchestrator.cs:708`).
   - Fix: record these as findings, not open checks. State the per-enemy elite flag and payout rule, and list the code that assumes boss-only.

5. **[major]** §2 "Rewards" — items, spell books and gold already pay on every fight, and the plan does not account for them.
   - Evidence: `FightController.Input.cs:1246` builds `ReckoningRequest.Full(settled.Reward, RollOffers(), ...)`, so every won fight offers 3 items via `ItemOfferRoll.Roll` (`RunOrchestrator.cs:750-762`). `RunOrchestrator.cs:684-686` rolls a spell drop per fight (`SpellDropChanceNormal` 0.10, elite 0.20, boss 0.35). `VictoryRewards.RollConsumableDrops` has no live caller, only tests (`FightRewardsTests.cs`).
   - Failure: an "item" chip on top of an item offer is a double payout. A "spell book" chip stacks on the random roll. A "consumable" chip has no existing payout path.
   - Fix: say what each chip replaces or adds to. Likely the chip sets the offer's tier floor (`RarityTable.TierFloorFor`, `RarityTable.cs:62`) and replaces the random spell roll. State that the consumable path is new.

6. **[major]** §8 SaveData v8->v9 — a party at offset 8 would be "counted as cleared", so a mid-leg save standing in the boss room skips the boss fight.
   - With the boss node cleared, `LegIsOver` (`RunManager.cs:~392`, `Choices().Count == 0`) triggers `AdvanceLeg` with no fight and no `RecordBossKill` (`RunOrchestrator.cs:564`).
   - Also unspecified: old saves from before the absolute-step fix can have `step < legStartStep` (comment at `RunManager.cs:333-343`), so the offset is negative.
   - Fix: clamp the offset to 0..8. If the old node was the boss, or its room was unresolved, place the party on spine offset-1 with that node uncleared. Add tests for offset 8 and for a negative offset.

7. **[major]** §8 P3 gate — "anchors left alive must be reported with the boss-fight win rate they produce" cannot be measured at P3. §6 says anchors are plain elites until P5, so no anchor reaches the boss fight. P3 also gates only on a "bot report", but TESTING.md (`docs/TESTING.md:15`) requires `run_tests_parallel.ps1 -Changed` for any Domain/Core/Bot code, and a balance report is explicitly "not a gate" (`docs/TESTING.md:74`).
   - Fix: move the anchor win-rate item to P5, which already re-runs the gate. Make P3's gate `-Changed` plus the bot report.

8. **[major]** §2 point 1 / §11 — the +8% skip heal is specified (§2, §11, P3 gate) but no phase builds it.
   - P2's list has nothing for it. "Walking the spine past a pocket's mouth" is undefined: arrival on spine node b from spine node a where a has a pocket exit?
   - It also has to write HP through `currentHealth`/`RunEncounter` (`HealPartyToFull` is at `RunEncounter.cs:244`) and be excluded in the migration.
   - Fix: add it to P2 with an exact trigger, where it is applied (`RunManager.MoveTo`), and a pin test.

9. **[minor]** §4 "at most one per interior spine stretch", cap 6 — "stretch" is undefined. Six equals the edges between interior nodes 1..7. §3's formula also admits pockets with b-a > 1 that bypass spine nodes, and the validator "none bypassing offset 0 or 8" does not say whether edges 0-1 and 7-8 may be doubled.
   - Failure: the 6 cap and the fuzz counts depend on this reading.
   - Fix: define stretch as an edge between consecutive spine nodes with both endpoints interior, or say otherwise.

10. **[minor]** §4 table, floor 100 "Longest ~24–26". 6 loops at the +4 ceiling plus an 8-room spine is 32. The table must mean a mean, or the ceiling is wrong. The art template height and "bounds the map time" are sized off this number.

11. **[minor]** §2 table vs "Rest... at most one per realm" — "offset 7 on the spine when `restBeforeBoss`" plus "one on a fork branch" can be two rests. State whether the spine rest counts toward the one.

12. **[minor]** §6 "Open consequence" (anchors guarding the boss) — resolved by roster order, which the plan never fixes. `CombatEncounter.LivingRankOf` (`:146`) is list order, and melee `Reach` is rank 0 only. Specify the boss at index 0. Also note `AvoidsFrontSlot` (`EncounterRoll.cs`/`RunEncounter.cs:152`) for anchors.

13. **[minor]** §6 leader rule — the plan frames it as a new outcome contract. A precedent already exists: `CombatEncounter.EndBySurvival` (`:367`, sets `EndedBySurvival`, makes `IsOver` and `PlayerWon` true with enemies alive). `FightSession.Rounds.cs:65,96` is the round-limit caller, with `FightEndReason.Survived`. `Outcome.cs:132` already pays only dead enemies. Say whether the leader rule reuses it (add a leader-fell caller and end reason) or why not. The boss-death rule alone then looks like an `implementer` change; the queue (finding 1) is what justifies `senior`.

14. **[minor]** §8 P2 test-rewrite list is incomplete. About 30 test files reference `DescentMap`, `DescentNode`, `GenerateLegFor` or `ForcedTypeAt`. Unlisted examples: `DescentMapTests`, `MapLayoutTests`, `MapNodeCardTests`, `MapLifecycleTests`, `MapRoutePreviewTests`, `BossEncounterClassTests`, `FightSettlementTests`, `ProgressionEncounterRangeTests`, `DifficultyCurveTests`, and the `Journey*` PlayMode set. `MapNodePreview`, `RunEncounter.Preview` and `DebugMenuController.JumpToBoss` consume `Depth` and are not named as P2 work. P2's `Escalation: cross-layer` also names no Editor/scene-generation part, which CLAUDE.md's criterion requires, unless the column projection touches `ScreenRegistry`.

15. **[minor]** §7/§8 P4 and P6 gates — P6 (shader plus screenshot) needs `-Runtime`. CLAUDE.md requires asking at session start and passing the answer into every brief; the plan's gates do not. P7 adds a `RoomType` (needs `RoomResolution`, `MapNodeCard.Caption` and medallion work) but is gated `-Changed` only.

16. **[minor]** §1 is stale on main. "A node is a tree, an icon and a caption. There is no reward, no strength and no detail card" is wrong. `MapNodeCard`, `MapNodePreview.For` and `RunEncounter.Preview` already show enemy names, exp, gold and treasure stash on a detail card (`MapController.cs:803-804`). §5's preview is already built; the new parts are strength and the reward chip. The plan should say it extends these.

17. **[minor]** Save format — `JsonUtility` cannot write dictionaries or polymorphic data (`RunSnapshot.cs:~287` says so). "nodeStrength", the profile's weight tables and `slainAnchorIds` need list-of-struct shapes. `RealmProfile` saved into the snapshot is otherwise consistent with the RunSnapshot comment at `:27-33`, since it is data, not content. `AdvanceLeg` (`RunManager.cs:399-440`) and `StartRun` can resolve the profile at realm open without a content read.

## Citations checked
- `DescentMap.cs:124-144` (every leg 9 columns, 8 rooms) → OK (`DefaultLegLength` at `:124`, `MaxColumnWidth` at `:144`).
- `DescentMap.cs:171-184` (~55% fights) → OK.
- `DescentMap.cs:216-252` (`ForcedTypeAt`) → OK.
- `DescentMap.cs:272-277` (Leg stream) → OK (`GenerateLegFor` at `:272`).
- `MapController.cs:670-731` (node = tree/icon/caption, Shop no icon) → moved to ~`:723`; the "no detail card" claim is wrong (finding 16).
- `MapController.cs:319` (label by step) → moved to `:375` (`depthLabel.Set(UiStrings.MapDepth, run.step, run.floor)`).
- `SystemMenuController.cs:590` ("ROOM {step}") → moved to `:761` (`UiStrings.SystemFloorRoom, run.floor, run.step`).
- `RunManager.cs:339` (`step = legStartStep + Depth`) → moved to `:342`.
- `RunManager.cs:400` (`legStartStep` += 8) → moved to `:403`.
- `RunManager.cs:412-437` (full heal after boss) → moved to `:419-440` (`HealPartyToFull` at `:440`).
- `RunManager.cs:123-124` (`_map` cache key) → moved to `:126-127`.
- `RunEncounter.cs:83-116` (`For`) → OK roughly; the stream is now at `:120`, `Preview` at `:~130`, `RollRoom` at `:122`.
- `RunEncounter.cs:84` (key step, nodeId) → OK in effect; `FightStream` is at `:120`.
- `RoomResolver.cs:57` → moved to `:38`.
- `RunOrchestrator.cs:683` → `:684` OK. `.Event.cs:169` → `:170` OK. `.Shop.cs:418` → `:416`. `.Shelf.cs:122` → OK.
- `RngStreams.cs:97-103` (`Derive`) → OK.
- `RunSnapshot.cs:27-33` (map-absent invariant) → OK (`:28-33`).
- `RunSnapshot.cs:72` (`DescentIsUnderWay` reads `step > 0`) → OK.
- `RunSnapshot.cs:387-391` (`eventBuffs`/leg buff) → moved to `:378-382`.
- `SaveData.cs:546-548` (`version < 8` block) → OK (`:546-549`).
- `SaveData.cs:66` (`CurrentVersion`) → OK (value 8).
- `CombatEncounter.cs:340` (`TryAddEnemy`) → OK.
- `CombatantState.cs:273` (`IsSummon`) → OK.
- `FightHudSpec.cs:51` (3 slots) → OK (`StageSlotsPerSide = 3`).
- `FightEncounterAdapter.cs:85` (`ScaledForElite`) → OK (per roster, see finding 4).
- `VictoryRewards.cs:51` (`For`) → OK. "Pays only the defeated" → OK (`FightSession.Outcome.cs:132`).
- `FightSession.Enemies.cs:751` (`SettleIfOver`) → moved to `:765`.
- `CombatEncounter.IsOver` → OK (`:375`).
- `DebugMenuController.cs:559-573` (`JumpToBoss`) → OK (uses `map.Nodes` and `before.Depth`).
- `IRunPolicy.cs:19` (`ChooseNode`) → OK.
- `RunSettlement.cs:50` (gold lost on death) → OK.
- `OWNER_DECISIONS_2026-09-26.md:133` (G2) → OK.
- `DIRECTION_BIBLE.md:38` (deeper means closer to the trauma) → OK. `:57` (dens in a painted place ruled out) → OK.
- `RUN_MAP_DECISION_SPEC.md:80` (control option) → OK.
- `RunSnapshot.bossEnemyId` has readers and no writer → OK (reads at `RunOrchestrator.cs:708` and `RunEncounter.cs:123`, no assignment). `RngStreams.Boss` unused outside tests → OK.
- `CONTENT_SCHEMA.md:79-80, 260-261, 409` (enemy schema lacks passive/per-turn hooks) → wrong on main: those lines are skill/ability field rows, not the claim. Not verified (see Unverifiable).
- "`RunManager.cs:412-437` stays: fixes a measured attrition complaint" → OK in content (comment block).

## Unverifiable
- `RUN_MAP_ART_BRIEF.md` line cites (`:11, 44, 50, 78, 90`), the two artifact links in §10, and the five storyboard frames: not read, since the artifacts are off-repo and the art brief is outside the requested checks.
- "The roughly 25 step consumers work unchanged" (§3): I confirmed the Fight, Treasure, Event, Shop, Shelf and SpellDrop streams key on `(step, nodeId)`, but did not enumerate every `step` consumer (`DifficultyCurve`, `RarityTable.StepsPerTier`, `LevelCurve`, achievements and the bot).
- Whether `ContentDatabase` is loaded when `SaveData.Migrate` runs. `SaveData.cs` already references it 33 times, so it probably is, but the load order was not traced.
- The CONTENT_SCHEMA claim about missing passive/per-turn enemy hooks was not located at any line.
- Balance claims (EV, pocket value, 8% heal, strength multipliers): out of scope for a read-only review.

# Plan — the run map: "the map builds the boss"

Status: revision 5, ready to execute P0–P3. P4 waits on owner decisions §9.1–2
and P7 on §9.3. Rev 1 was reviewed twice (one reviewer with repo context, one
blind) and checked against acclaimed games (§10). Revs 2–3 applied the
owner's feedback. Rev 3 was reviewed by two Sonnet 5.5 reviewers, one with
repo context checked against `main` at `1914a617` and one blind
(`docs/handoffs/run_map/REVIEW_*.md`). Rev 4 applies those findings and the
owner's answers (§11). Rev 5 applies the item redesign's amendments
(`PLAN_ITEMS.md` §5, §11). Citations are to `main` at `1914a617`. Owner rules
this plan answers to: `DIRECTION_BIBLE.md` §5–7 and
`RUN_MAP_DECISION_SPEC.md` §2–3. It supersedes `RUN_MAP_ART_BRIEF.md` (§7).

## 1. Why the current map fails

- Every leg is 9 columns, 2–3 wide, and every path is exactly 8 rooms
  (`DescentMap.cs:124-144`). Legs never grow.
- About 55% of rooms are plain fights (`DescentMap.cs:171-184`), so most picks
  are "fight or fight".
- The elite is a forced width-1 column at step 4 and the boss is at step 8
  (`ForcedTypeAt`, `DescentMap.cs:216-252`). No choice touches either.
- A detail card already shows a node's enemies, exp, gold and treasure stash
  (`MapNodeCard`, `MapNodePreview.For`, `RunEncounter.Preview`;
  `MapController.cs:803-804`). It shows no strength and no prize, and Shop has
  no icon (`MapController.cs:~723`). This plan extends that card; it does not
  build a new one.
- Clearing changes a tint and nothing else.

The rules that stand: whole realm visible, no "?", rewards shown before
choosing, no time pressure, legs get longer, reads as a strange fantasy realm
full of monsters, the world eases when cleared, never a mood or progress meter.

## 2. Concept

A realm is a road down to its heart, the boss. Three things make it a
decision instead of a path-pick:

1. **Routes of unequal length.** An 8-step spine runs to the heart. Loops
   ("pockets") branch off it and rejoin it, costing extra rooms. They grow with
   depth up to what the spine can hold (6). Past that, the realm keeps changing
   through loop length, loop contents and difficulty (§4). HP carries from
   room to room and refills only after the boss: the full heal at
   `RunManager.cs:419-440` stays, because it fixes a measured attrition
   complaint. Every pocket is therefore HP you carry into the boss fight, paid
   for a prize the spine doesn't have. Skipping pays a little: the skip heal
   (+8%, §3). Skipping also costs: the anchors you skip wait at the heart, and
   a skipped shop is a realm without a shop.
2. **Visible encounters, visible prizes.** A fight node shows its enemies, a
   strength from 1 to 3, and a prize chip. The prize's *kind* varies per node
   and is not tied to strength. Strength scales *how much*: strength-3 gold is
   far more gold than strength-1 gold. Inspecting never commits.
3. **Anchors stand with the heart.** The heart is drawn as the boss with its
   living anchors. Each anchor is one elite enemy on the map, joined to the
   heart by a painted root. Beat it on the map and it is gone; leave it alive
   and it fights in the boss fight, in front of the boss (§6). Anchors are
   optional. No count or percentage is ever shown: you see who will be
   standing there.

Then, as polish (P6): **the realm eases where you've been.** Two aligned
paintings, and a mask reveals the eased one around cleared nodes and around
the heart as anchors fall. It is never labelled (`RUN_MAP_DECISION_SPEC.md`
§3).

**Honest framing for the owner.** On screen this is the spec's "control"
option: a graph drawn into a painted place (`RUN_MAP_DECISION_SPEC.md:80`).
The bible rules out "dens in a painted place" (`DIRECTION_BIBLE.md:57`). The
difference is in the decision structure (unequal routes, a boss shaped by the
route), not in the picture, so it needs explicit sign-off against that line
(§9.1).

### Strength

- **Derived, not generated.** The generator reads no content (§4), so it
  cannot know strength. Strength is computed in Core right after the realm is
  generated (P2), from each fight node's preview roll (§5) against the party.
- **Formula:**
  - `threat` = the sum, over the node's rolled enemies, of scaled max HP ×
    scaled attack at the node's step.
  - `party` = the sum, over the fielded party at realm open, of max HP ×
    attack.
  - `r = threat / party`.
  - Strength is 1 if `r < T1`, 2 if `r < T2`, otherwise 3.
  P2 names the exact stat fields.
- **Thresholds:** `T1` and `T2` are literals set on the P0 EV sheet so that the
  default profile's fights land near 40% / 40% / 20% on floors 1–10. Enemies
  scale with step and the party with level, so the ratio is roughly
  floor-independent. The P2 test pins the mix on fixed seeds, within a
  tolerance.
- **Frozen per realm** (owner): computed once at realm open and saved, so
  levelling mid-realm never re-labels a node you already judged.

### Prizes

- **Additive.** Every fight still pays its enemies' exp and gold
  (`VictoryRewards.For`, `VictoryRewards.cs:51`). The chip is the prize on top.
- **Kind:** assigned per fight node by the generator from the profile's
  prize weights. It is an enum, so it needs no content.
- **Kinds today:**
  - gold, scaled by strength;
  - a consumable, by count. This is a new payout path:
    `VictoryRewards.RollConsumableDrops` has no live caller today.
  - `Item` (shown as "Gear"), `Book` and `Relic` are enum values at weight 0
    until item phase I3. What each pays, and how a chip meets the item offer
    and spell drop, lives in `PLAN_ITEMS.md` §1.1 and §1.2.
- **Strength multipliers:** one literal table per kind, set on the P0 EV sheet,
  steep (each pip worth more than the last).

### Point types

| Type | Where it may sit | Role |
|---|---|---|
| Fight | anywhere | strength 1–3, prize chip |
| Anchor (elite) | pockets and fork siblings, never on a node you must walk | one elite enemy; beaten on the map or met at the heart (§6) |
| Event | anywhere but next to another event | the card shows the event's title, never "?" |
| Shop | 0 or 1 per realm (profile's chance), always inside a pocket | skipping the pocket means no shop this realm |
| Rest | at most one per realm: the spine node at offset 7 when `restBeforeBoss`, otherwise a fork sibling | reachable without paying a pocket |
| Treasure | pockets only | a pocket prize |
| Cache | **new, if signed (§9.3)**: at most one per realm, in a pocket | banks gold, safe from death (owner ask G2, `OWNER_DECISIONS_2026-09-26.md:133`; gold is lost on death per `RunSettlement.cs:50`) |

Forks decide *which* node you get. Pockets decide *how much more* you take on.
Every pocket holds at least one of: an anchor, the shop, Treasure, or Cache.

### Orientation

Open (§9.2), recommended top to bottom: you enter at the top and the heart
lies at the bottom, so scrolling down means going deeper. Deeper realms are
closer to the trauma (`DIRECTION_BIBLE.md:38`); bottom-to-top reads as a
climb. Depth is shown by composition, not a colour gradient, so each realm
keeps one dominant colour (`DIRECTION_BIBLE.md` §6). Orientation is one
layout constant in `RealmLayout`, defaulting to top to bottom; switching
later costs art, not code.

## 3. The step contract and the skip heal

`step` is the difficulty clock. `DifficultyCurve`, `RarityTable.StepsPerTier`,
`LevelCurve`, every rng stream, achievements, settlement and the bot read it.
Its meaning stays the same:

- `legStartStep` still advances by 8 per realm (`RunManager.cs:403`).
  `RunDepth.FloorFor` is unchanged.
- Each node has `Offset` 0..8. The spine is the entry (0), 7 rooms (1–7) and
  the heart (8). A spine node's offset is its index, as `Depth` is today.
- **A fork** at offset *k* (1–7) is two sibling nodes at offset *k* with
  identical in- and out-edges. You take one.
- **A stretch** is the edge between consecutive interior spine offsets *k* and
  *k+1*, for *k* = 1..6. That gives 6 stretches, hence at most 6 pockets.
- **A pocket** on stretch *k* leaves from offset *k* (from every sibling, if
  forked) and rejoins at offset *k+1*. It holds *L* rooms in a chain, all at
  offset *k+1*. So a detour adds *L* rooms at the difficulty of its rejoin
  node and never raises later difficulty. The flat difficulty inside a
  detour is intended; the P0 sheet measures the power it gives.
- On arrival, `step = legStartStep + node.Offset`, replacing `+ node.Depth` at
  `RunManager.cs:342`.
- Node ids are unique within a realm and stable under regeneration. Rng
  streams key on `(step, nodeId)` (`RunEncounter.cs:120`, `RoomResolver.cs:38`,
  `RunOrchestrator.cs:684`, `.Event.cs:170`, `.Shop.cs:416`, `.Shelf.cs:122`;
  `RngStreams.Derive`, `RngStreams.cs:97-103`), so pocket rooms that share a
  step still roll differently.
- New saved stat `roomsWalked`, the run's total rooms entered. Labels that
  count rooms use it, not step: `SystemMenuController.cs:761` and
  `MapController.cs:375`.

**Skip heal** (owner: flat 8%):

- **Trigger:** in `RunManager.MoveTo`, when the party moves from offset *k*
  to a spine node at offset *k+1* along the spine edge, and stretch *k* has a
  pocket.
- **Effect:** every living party member heals 8% of max HP, capped at max.
- **Timing:** once per pocket per realm, never retroactively (§8 migration).
- **Shown:** on the card of the spine node it leads to, as part of that
  choice.
- Built in P2, with a pin test.

The shortest route is today's 8 rooms on every floor, so balance stays
calibrated. Difficulty rises 8 steps per realm, forever.

## 4. Generator: `RealmMap` and realm profiles

Lives in `Domain/Dungeon/`. It replaces `DescentMap`, `DescentNode`,
`DescentMapGenerator` and `ForcedTypeAt`. `RealmMap` keeps `Entry`, `Boss`,
`Node(id)`, `ReachableFrom`, `CanMove` and `ReachableFromEntry`, because
`MapController`, the debug menu (`DebugMenuController.cs:559-573`), the bot and
tests call them.

### Realm profiles

There is no realm content type today, and many realms are still to be
designed. So the generator takes its shape from a **`RealmProfile`**:

- **Fields:** pocket count, pocket length range, fork count, anchor count,
  shop chance, waypoint weights (fight / event / treasure / rest / cache),
  prize-kind weights, anchor prize-kind weights, boss pool, and
  `restBeforeBoss`.
- **`anchorPrizeWeights`** is a `{kind, weight}` list like `prizeWeights`. The
  default is all weight 0: an anchor pays exp and gold only until item phase
  I3 sets it (`PLAN_ITEMS.md` §1.1).
- **Designed realms** author their own profile later, as content (P8).
- **The generator reads no content.** The profile is resolved once when the
  realm opens (`StartRun`, `AdvanceLeg`), from the default table or (after
  P8) authored content. It is written into the save beside `bossEnemyId`,
  and `RealmMap` reads it from the snapshot. The invariant at
  `RunSnapshot.cs:28-33` stays, because a saved profile is data, not
  content.
- **Save shape:** `JsonUtility` cannot write dictionaries
  (`RunSnapshot.cs:~287`). Weight tables are lists of `{kind, weight}`
  structs.

### The default profile

It covers every floor without a designed realm, floor 100 included. Shape is
bounded by the spine; depth shows through what grows below.

| Grows | Default | Ceiling |
|---|---|---|
| Pockets | `1 + floor/2` | 6, at floor 10 |
| Pocket length *L* | 1–2 rooms; upper bound +1 per 6 floors after floor 2 | 1–4, at floor 14 |
| Forks | `floor/4`, on offsets whose stretches have no anchor yet | 3 |
| Anchors | `1 + floor/3` | 6 |
| Shop chance | 0.75 | profile |
| Difficulty | +8 steps per realm | none |

| Floor | Pockets | Shortest | Longest (mean / max) |
|---|---|---|---|
| 1 | 1 | 8 | 9.5 / 10 |
| 10 | 6 | 8 | ~19 / 26 |
| 100 | 6 | 8 | ~23 / 32 |

The art template and the map time budget are sized for the 32-room maximum.
The skip heal is bounded at 6 × 8%.

### Shape and rules

- **Inputs:** `runSeed`, `legStartStep`, and the saved profile, opened on
  `RngStreams.Leg` as today (`DescentMap.cs:272-277`).
- **Shape:** a forward-only DAG made of the spine, the forks (§3) and the
  pockets (§3). Positions come from `RealmLayout` (Domain, normalised 0..1),
  and the realm's height grows with its longest route.
- **Anchors:** placed on pocket rooms and fork siblings, at most one per
  pocket or fork, and at least one reachable on every floor. `ForcedTypeAt`'s
  forced elite is removed.
- **Rolled types and prizes:** from the profile's weights, using §2's
  placement rules. No two events adjacent. Every fight node gets a prize
  kind.
- **Validator** (`RealmMapRules`):
  - Every node is reachable, including the boss.
  - No anchor sits on a node every route must walk.
  - Shop, rest and cache placement as in §2's table. Every pocket holds a
    prize.
  - Ids are unique. Offsets never decrease along an edge, and the entry's
    successors have offset ≥ 1 (`DescentIsUnderWay` reads `step > 0`,
    `RunSnapshot.cs:72`).
- **Relax order** when rules conflict: fork rest → spine rest; fork count;
  then fill a prize-less pocket with Treasure. Never fewer than one pocket.
- **Fuzz test:** 10k seeds across the default profile, floors 1–100, plus
  hand-written extreme profiles. It pins literal expected counts per floor
  class (gotcha 5): pockets, forks, anchors, shops, and the longest-minus-
  shortest route.

## 5. Encounter preview: one source of truth

The map preview already exists (`RunEncounter.Preview`, `MapNodePreview.For`).
It must call `EncounterRoll.Roll` on a fresh `RngStreams.Open(Fight, step,
nodeId)` with the same `RunEncounter.Pool()` and floor band, as the fight does
(`RunEncounter.cs:~83-123`). That means no new stream, no stored roll and no
change to Fight-stream outputs, so bot determinism holds. Strength (§2) is
computed from this roll. Test: for every node on 200 seeds, the preview's
enemy ids equal those from `RunEncounter.For(...)`.

**Anchor roll:** an anchor is one enemy. Today an elite rolls 2
(`EncounterRoll.cs:61`, `EliteEnemyCount = 2`). Anchors get their own roll:
one enemy from the elite band, `SlotSpan` 1 only (`EncounterRoll.cs:28`), on
the same Fight stream key. That same enemy is fought on the map and stands at
the heart. P1 adds the roll; P2 wires it.

## 6. Anchors in the boss fight

### The rules

- **Formation:** the boss stands in the rear. Living anchors stand in front of
  it, the two strongest first (by `threat`, §2; ties by map order). The stage
  has 3 slots (`FightHudSpec.cs:51`). Rank is list order
  (`CombatEncounter.LivingRankOf`, `:146`) and melee reaches rank 0 only, so
  **anchors guard the boss**: melee can't touch it while an anchor stands in
  front, and spells and ranged attacks can. This is the owner-approved answer
  to "just kill the boss" (§11). It needs no new ability system. With no
  living anchors the boss is rank 0, as today.
- **Reinforcements:** every other living anchor queues. When an anchor dies,
  the next in the queue takes its slot and its place in front of the boss.
- **The boss's death ends the fight.** Anchors still standing or queued leave
  with it.
- **Rewards:** an anchor pays its elite exp and gold, plus its prize chip, only
  if it dies, on the map or at the heart. Anchors that die with the boss
  (the leader rule, every boss fight) pay nothing (`FightSession.Outcome.cs:132` pays only the dead). Chips of
  anchors that die at the heart join the boss settlement in map order, before
  pages merge (`PLAN_ITEMS.md` §1.2).
- **Tuning:** an anchor joins the boss fight at a fraction of its HP, a literal
  set by the bot (P5). Not shown to the player.
- **Boss choice:** from the profile's boss pool, written once at realm open
  from the unused `RngStreams.Boss` stream into `RunSnapshot.bossEnemyId`,
  which has readers (`RunOrchestrator.cs:708`, `RunEncounter.cs:123`) and no
  writer today. Must have `SlotSpan` 1.
- **Saved:** `slainAnchorIds` (a string list).

### What the code needs (P5, `senior`, `Escalation: architecture`)

- **Reseat contract.** `TryAddEnemy` refuses on `_enemies.Count >= maxSlots`
  (`CombatEncounter.cs:342`), and the enemy list never shrinks
  (`FightSession.Enemies.cs:171,181`), so no slot ever reopens today. A
  reinforcement must take a dead enemy's list index and stage slot. That
  touches `CombatEncounter`, `FightController._slotOf` / `SlotIndexOf`
  (`FightController.cs:~866`), `LivingRankOf`, turn order, kill credit and
  intents. Owner-approved (§11).
- **Per-enemy elite flag.** Elite scaling and the elite payout are per roster
  today: one `isElite` in `FightEncounterAdapter.cs:538-539,566`, and one
  `IsEliteFight` in `VictoryRewards.For` (`FightSession.Outcome.cs:132`).
  Anchors at the heart need elite stats and elite pay inside a boss fight.
- **One-boss assumptions to change:** the front slot is the boss
  (`FightController.cs:549-553`), and the boss roster is exactly one id
  (`EncounterRoll.cs:145-148`, `BossIdOf` at `RunOrchestrator.cs:708`).
- **Kill credit:** reinforcements are registered through a new `FightSession`
  method (`_enemyKits` is private) and are never marked `IsSummon`
  (`CombatantState.cs:273`, set by the caller at
  `FightSession.Skills.cs:823`), so they count as kills.
- **Leader rule:** reuse the survival ending. `CombatEncounter.EndBySurvival`
  (`:367`) already ends a won fight with enemies alive
  (`FightSession.Rounds.cs:65,96`, `FightEndReason.Survived`). Add a
  `LeaderFell` reason and caller. Owner decision 2026-10-14: the rule holds in
  every boss fight, and the followers die on stage (`CombatEncounter.DismissFollowers`,
  the ordinary death fade in `FightController.BeginLeaderFallEnding`), not
  vanish or stand until the Reckoning. Queued anchors need no presentation.

Until P5, anchors are plain optional single-elite fights on the map with a
visible prize, so the pocket decision is proven first.

## 7. Screen

1920×1080, scrolling vertically with the camera following the party. Built
only through `Domain/UiKit/Screens/MapScreen.cs` and `ScreenRegistry.cs`.

- **Top band:** realm name, floor, gold, and the cache balance once P7 lands.
- **Bottom detail card** (the existing `MapNodeCard`, extended): type,
  enemies, strength, prize, the skip heal on a spine choice, and (for an
  anchor) that it will guard the boss if left alive. When two choices are
  open, it compares them side by side.
- **Fixed node grammar:** medallion = type, pips = strength, one prize chip
  whose size follows strength.
- **Closed-off routes:** focusing a choice veils, under painted fog, the routes
  it would close off, and only when something does close.
- **Heart:** the boss in the rear, the two front anchors, and the queue as a
  row of smaller portraits behind (at most 4 at the 6-anchor ceiling). Painted
  roots run to each anchor on the map and wither when it falls.
- **Opening:** the camera starts on the heart and moves to the party (2.5 s at
  most, skippable).
- **Gamepad:** Left and Right step through the current choices in screen
  order, and Submit walks. Y toggles a zoomed-out overview where the d-pad
  inspects every node read-only. Mouse hover is focus. No free cursor.
- **Walk:** `MapWalk` and `MapController.Walk` are reused, along vertical
  curves.
- **Art brief v2:** replaces `RUN_MAP_ART_BRIEF.md`, which is horizontal. It
  is a template per realm, sized for the 32-room maximum. It covers:
  - vertical found/eased paintings with the same safe-area method;
  - medallions, adding shop, cache and anchor;
  - the strength pip and one chip per prize kind;
  - the root stroke, living and withered;
  - a vertical trail.
  Greyboxes stand in until the art arrives.

## 8. Phases

| # | Work | Owner | Gate |
|---|---|---|---|
| P0 | EV sheet: `T1`/`T2` for the 40/40/20 mix; strength multipliers per prize kind (gold-chip multipliers and detour gold set against the realm gold target, `PLAN_ITEMS.md` §1.6, and redone once item phase I5 sets `GoldIncomePermille`); pocket value at full HP and 40% HP; skip-heal value; detour exp/gold at floors 1, 10 and 100. Art brief v2. Storyboards redrawn for rev 4, only if the owner asks. | orchestrator | the owner reads the sheet |
| P1 | `RealmProfile` and the default table, `RealmMap`, `RealmLayout` (orientation constant), `RealmMapRules`, the anchor roll in `EncounterRoll`, and the fuzz test. All pure Domain. | implementer | `tools/test.ps1 run`, then `run_tests_parallel.ps1 -Changed` |
| P2 | Integration: profile resolved and saved at realm open; step assignment and `roomsWalked`; the skip heal in `RunManager.MoveTo`; strength computed and saved at realm open; the preview on the Fight stream (§5); the consumable and gold prize payout, with `Item`, `Book` and `Relic` chips handed to the item pipeline (a no-op until item phase I3) and the node card reading the relic and book pools live for "empty pool → gold" (`PLAN_ITEMS.md` §1.1); `restBeforeBoss` and the `_map` cache key (`RunManager.cs:126-127`); debug jump (`DebugMenuController.JumpToBoss`); `MapNodePreview` and `RunEncounter.Preview` moved off `Depth`; SaveData v8→v9 (below); a column projection so the current screen stays playable (below); rewrite every test referencing `DescentMap`, `DescentNode`, `GenerateLegFor`, `ForcedTypeAt` or `Depth` (about 30 files, found by Grep, including the `Journey*` PlayMode set); update `CODE_MAP.md` and `ARCHITECTURE.md`. | senior, `Escalation: architecture` (replaces the map, step and save contracts) | `run_tests_parallel.ps1 -Changed -BuildScenes` |
| P3 | Bot: `MapView` on `RunView` (route to the heart, pockets, anchors, strength, prizes) and path-aware choices in the four policies (`IRunPolicy.cs:19`). `MapView` includes Gear and Relic chips and anchor prizes. | implementer, then verifier | `-Changed`, plus a bot report at floors 1, 10 and 30: the pocket-take rate and the strength-3 take rate must each differ by HP band (not ~0% or ~100% in every band) |
| P4 | Screen: the graph, node grammar, the extended detail card and comparison, walk, gamepad, overview. Needs §9.1–2. | implementer | `-Changed -BuildScenes`, diff `Art/`; `-Runtime` captures only if the owner allows them that session |
| P5 | Anchors at the heart (§6): reseat contract, per-enemy elite flag, formation, reinforcement queue, leader rule, `slainAnchorIds`, anchor HP fraction, heart drawing. Combat tests with zero, one, two and four anchors alive; the boss killed with anchors queued; melee refused on a guarded boss; one test with a heart anchor carrying a Gear chip (`PLAN_ITEMS.md` §5.4). | senior, `Escalation: architecture` | `-Changed -BuildScenes`, plus the P3 bot report re-run with the boss-fight win rate by anchors left alive. Landed: core `a929520c`, remainder after it; the heart drawing moved to P4. Bot table: `handoffs/run_map/P3_BOT_REPORT.md` |
| P6 | Easing mask: a one-day spike (hand-written UI shader keeping stencil/clip, content-space UVs, rebuilt from `clearedNodeIds` on load), then the build and the art swap. | implementer | headless mask test; `-Runtime` capture if allowed |
| P7 | Cache, if signed (§9.3): a new `RoomType`, `RoomResolution`, `MapNodeCard.Caption`, medallion, deposit, and the `RunSettlement` exemption. | implementer | `-Changed -BuildScenes` |
| P8 | Designed realms as content: a `realms` list in `ContentData` (`IOrderedContent`, `ContentBuilder`, `CONTENT_SCHEMA.md` rows) mapping a floor to its profile. It starts with the first designed realm. | implementer | `-Changed -BuildContent` |

If the P3 report fails, tune before P4. The levers are the prize multipliers,
pocket length, shop chance and anchor count.

### Column projection (P2)

The current screen draws columns by `Depth`. Until P4 replaces it, P2 feeds it
offset as the column and gives forks and pocket rooms rows in that column.
Then every node is visible and clickable, and anchors show as elites. It is a
throwaway adapter, deleted in P4.

### SaveData v8 → v9

- Add a `version < 9` step after the `version < 8` block (`SaveData.cs:546-549`).
  Bump `CurrentVersion` (`:66`) and extend the ladder comment.
- Resolve the default profile, generate the new realm, and compute strength.
- Let `o = clamp(step - legStartStep, 0, 8)`. Saves from before the
  absolute-step fix can have `step < legStartStep` (`RunManager.cs:333-343`),
  hence the clamp.
- **If the old room was resolved** (`currentNodeId` is in `clearedNodeIds`) and
  `o < 8`: place the party on the spine node at `o`, cleared.
- **Otherwise** (an open fight, event or shop, or the boss room): place the
  party on the spine node at `o - 1` (minimum 0), cleared, so the room at `o`
  is the next choice. The boss is never skipped.
- Mark every spine node before the party passed. Pockets on stretches before
  it count as walked past, with no skip heal. Anchors on those stretches and
  forks go into `slainAnchorIds` and pay nothing, so they never join the boss
  unearned.
- Clear `eventId`, `eventNodeId`, `eventPageId`, `pendingFight`,
  `pendingShelf`, `eventResult`, `shopStock` and `shopNodeId`.
- `step` becomes `legStartStep + offset of the placed node`. Leg-scoped
  `eventBuffs` stay (`RunSnapshot.cs:378-382`), and HP is kept as saved.
  `roomsWalked = step`.
- **New fields:** `realmProfile`, `nodeStrength` (list of `{nodeId, strength}`),
  `roomsWalked`, `slainAnchorIds`.
- **Merge order:** P2 and item phase I2a both edit `RunSnapshot`; P2 merges
  first. Item fields need no v9 entry (`PLAN_ITEMS.md` §2.5).
- **Tests:** v8 saves at offset 5 resolved; offset 5 with an open fight, event
  and shop; offset 8 (the boss room, which must not be skipped); a negative
  offset; offset 0.

## 9. Owner decisions

1. **The screen:** accept the "graph in a painted place" presentation against
   `DIRECTION_BIBLE.md:57`. Blocks P4.
2. **Orientation:** top to bottom (recommended, §2) or bottom to top. Blocks
   P4's art, not the code.
3. **Cache:** build it (G2) or drop it. Blocks P7 only.

## 10. Checked against acclaimed games

The comparison and the rev-1 storyboards: `docs/handoffs/run_map/storyboards.html`
(and https://claude.ai/artifact/AWhthzsNhso4dzaEcjBTSx; advisory at
https://claude.ai/artifact/Fe1AMzHFEjmjSmYYjPKgvL). They predate revs 2–4. What
it changed in this plan:

- Anchors join the boss instead of granting abstract wards (Shadow of Mordor's
  bodyguards).
- Skipping a pocket heals a little (Balatro pays for skipping a blind).
- The visible boss is not sold as new (Slay the Spire and Balatro show it).
  What's new is that the boss you face is the one your route left.
- A time budget for the map (Darkest Dungeon II's slow travel is the warning).

## 11. Owner decisions log

**Rev 2:**
- Realm profiles, because most floors are still to be designed.
- The orientation recommendation.
- The endless descent is covered by the default profile.

**Rev 3:**
- Every fight shows its prize; strength scales the amount, not the kind.
- Prince's favor is not the lever.
- Strength is frozen per realm.
- Shops sit only inside pockets.
- Pockets cap at 6, while length, contents and difficulty keep growing.
- The skip heal is a flat 8%.
- Anchors are uncapped per realm, with a reinforcement queue.
- Killing the boss ends the fight.
- A migrated save resumes at its last step.

**Rev 4,** from the reviews and the owner's answers (2026-10-13):
- **Owner:** keep the queue and build the reseat contract.
- **Owner:** guarding or buffing are acceptable examples of anti-focus.
  Chosen: anchors guard the boss through rank and Reach (§6). It reuses
  existing rules, while buffs would need hooks the enemy schema lacks.
- **Owner:** the skip heal is not open. It now has a trigger and a phase.
- **Owner:** shops stay inside pockets; skippers go without.
- **Owner:** item and spell-book prizes wait on the item redesign.
- **Fixed:**
  - strength is derived in Core, not generated (it was contradictory);
  - an anchor is one enemy with its own roll;
  - stretches, forks and pocket offsets are defined;
  - the skip heal trigger;
  - the migration no longer skips the boss, and handles offsets below 0;
  - the route numbers (32 maximum);
  - the rest count;
  - per-enemy elite scaling;
  - P3/P5 gates per `docs/TESTING.md`;
  - the P2 escalation criterion;
  - the stale §1;
  - moved citations;
  - the save shapes.

**Rev 5,** from the item redesign (`PLAN_ITEMS.md` §5, owner-answered
2026-10-13): `Relic` prize kind, `anchorPrizeWeights`, item-pipeline payout in
P2, heart-anchor chips in P5, Gear and Relic chips in the P3 `MapView`, P2
before I2a on `RunSnapshot`, gold-chip multipliers against the realm gold
target in P0.

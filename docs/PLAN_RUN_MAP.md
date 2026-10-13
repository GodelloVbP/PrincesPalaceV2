# Plan — the run map: "the map builds the boss"

Status: proposal, awaiting owner sign-off (§9). Written against `2565b5a`.
Reviewed in two rounds, each by one reviewer with repo context and one blind
reviewer, then challenged against acclaimed games (§10); every file:line
below was read, not remembered. Owner rules this
plan answers to: `DIRECTION_BIBLE.md` §5–7, `RUN_MAP_DECISION_SPEC.md` §2–3.
It supersedes `RUN_MAP_ART_BRIEF.md` once signed (§7).

## 1. Why the current map fails

- Every leg is 9 columns, 2–3 wide, and every path is exactly 8 rooms
  (`DescentMap.cs:124-144`). Legs never grow.
- About 55% of rooms are plain fights (`DescentMap.cs:171-184`), so most picks
  are "fight or fight".
- The elite is a forced width-1 column at step 4 and the boss is at step 8
  (`ForcedTypeAt`, `DescentMap.cs:216-252`). No choice touches either.
- A node is a tree, an icon and a caption. There is no reward, no strength and
  no detail card, and Shop has no icon (`MapController.cs:670-731`).
- Clearing changes a tint and nothing else.

The rules that stand: whole realm visible, no "?", rewards shown before
choosing, no time pressure, legs get longer, reads as a strange fantasy realm
full of monsters, the world eases when cleared, never a mood or progress meter.

## 2. Concept

A realm is a road down to its heart, the boss. Three things make it a
decision instead of a path-pick:

1. **Routes of unequal length.** An 8-step spine runs to the heart. Pockets
   branch off it and rejoin it, costing 1–2 extra rooms, and they grow in
   number with depth. HP carries from room to room and refills only after the
   boss (the full heal at `RunManager.cs:412-437` stays: it fixes a measured
   attrition complaint). Every pocket is therefore HP you carry into the boss
   fight, paid for a prize the spine doesn't have. Restraint pays too: walking
   the spine past a pocket's mouth lets the party catch its breath (+8% HP),
   so bypassing is a choice with its own reward, not only an absence.
2. **Visible encounters.** A fight node shows the enemies it will spawn, a
   strength from 1 to 3, and one reward chip. Strength changes the *kind* of
   reward, not only its size: 1 pays gold, 2 pays gold and a consumable, 3 pays
   a spell book. Inspecting never commits.
3. **Anchors stand with the heart.** The heart is drawn as the boss itself,
   with its anchors standing at its side. Each elite on the map is an anchor,
   joined to the heart by a painted root. Beat it on the map and it is gone;
   leave it alive and it fights beside the boss. Elites are optional, and the
   boss's drop grows by one reward per anchor still standing, so leaving one
   alive is a bet, not only a mistake. No count or percentage is ever shown:
   you see who will be standing there.

Then, as polish (P6): **the realm eases where you've been.** Two aligned
paintings, and a mask reveals the eased one around cleared nodes and around
the heart as anchors fall. It is never labelled. The spec asks for exactly this
response (`RUN_MAP_DECISION_SPEC.md` §3).

**Honest framing for the owner.** On screen this is the spec's "control"
option: a graph drawn into a painted place (`RUN_MAP_DECISION_SPEC.md:80`).
The bible rules out "dens in a painted place" (`DIRECTION_BIBLE.md:57`). The
difference is in the decision structure (unequal routes, a boss shaped by the
route) rather than in the picture, so it needs explicit sign-off against that
line, with storyboard frames as the evidence (P0).

### Point types

| Type | Status | Where it may sit | Role |
|---|---|---|---|
| Fight | kept | anywhere | strength 1–3, reward kind by strength |
| Elite (anchor) | kept, reworked | pockets and forks, never the spine | beaten on the map, or it joins the boss fight |
| Event | kept | anywhere but next to another event | the card shows the event's title, never "?" |
| Shop | kept | one per realm, on a fork branch | costs the other branch, not extra rooms |
| Rest | kept | at most one per realm, on a fork branch; offset 7 on the spine when `restBeforeBoss` | reachable without paying a pocket |
| Treasure | kept | pockets only | a pocket prize |
| Cache | **new, optional** | at most one per realm, in a pocket | banks gold, safe from death (owner ask G2, `OWNER_DECISIONS_2026-09-26.md:133`; gold is lost on death per `RunSettlement.cs:50`) |

Forks decide *which* waypoint you get. Pockets decide *how much more* you
take on. Every pocket holds at least one prize the spine stretch it bypasses
lacks (an anchor, Treasure, Cache, or a strength-3 fight).

### Orientation

Top to bottom: you enter at the top and the heart lies at the bottom.
Scrolling down means going deeper. Depth is shown by composition (the ground
falling away, the heart below), not by a colour gradient, so each realm keeps
its one dominant colour (`DIRECTION_BIBLE.md` §6).

## 3. The step contract

`step` is the difficulty clock. `DifficultyCurve`, `RarityTable.StepsPerTier`,
`LevelCurve`, every rng stream, achievements, settlement and the bot read it.
Its meaning stays the same:

- `legStartStep` still advances by 8 per realm (`RunManager.cs:400`).
  `RunDepth.FloorFor` is unchanged.
- Each node has `Offset` 0..8. The spine is entry + 7 rooms + heart, so a
  spine node's offset is its index, as `Depth` is today.
- Pocket room *j* (0-based) of *n*, between spine nodes *a* and *b*, takes
  `offset(a) + floor((j+1) * (offset(b) - offset(a)) / (n+1))`, clamped to at
  least `offset(a) + 1`. So a detour never raises later difficulty.
- On arrival, `step = legStartStep + node.Offset`, replacing `+ node.Depth` at
  `RunManager.cs:339`.
- Node ids are unique within a realm and stable under regeneration. Rng
  streams key on `(step, nodeId)` (`RunEncounter.cs:84`, `RoomResolver.cs:57`,
  `RunOrchestrator.cs:683`, `.Event.cs:169`, `.Shop.cs:418`, `.Shelf.cs:122`;
  `RngStreams.Derive`, `RngStreams.cs:97-103`), so two rooms that share a step
  still roll differently.
- New saved stat `roomsWalked`. Display labels that count rooms use it, not
  step: `SystemMenuController.cs:590` ("ROOM {step}") and `MapController.cs:319`.

Result: the shortest route is today's 8 rooms on every floor, so balance stays
calibrated. The longest route grows from about 10 rooms at floor 1 to about 16.
The roughly 25 step consumers work unchanged. Difficulty still rises 8 steps
per realm.

## 4. Generator: `RealmMap`

Lives in `Domain/Dungeon/`. It replaces `DescentMap`, `DescentNode`,
`DescentMapGenerator` and `ForcedTypeAt`. `RealmMap` keeps `Entry`, `Boss`,
`Node(id)`, `ReachableFrom`, `CanMove` and `ReachableFromEntry`, because
`MapController`, the debug menu (`DebugMenuController.cs:559-573`), the bot and
tests call them.

- **Inputs:** `runSeed`, `legStartStep`, `floor`, `restBeforeBoss`, opened on
  `RngStreams.Leg` as today (`DescentMap.cs:272-277`). **It reads no content.**
  That invariant is stated at `RunSnapshot.cs:27-33` and it stays, so a
  content update can't corrupt a run in progress.
- **Shape:** a forward-only DAG. The spine has 9 nodes at offsets 0..8.
  - Pockets: `1 + floor/2`, capped at 4. Each is 1–2 rooms longer than the
    stretch it bypasses, and none bypasses offset 0 or 8.
  - Forks: 1–2 equal-length branches.
  - Positions come from `RealmLayout` (Domain, normalised 0..1).
- **Anchors:** `min(1 + floor/3, 2)` elites (the enemy stage has 3 slots,
  `FightHudSpec.cs:51`: the boss plus two anchors), on pockets and forks, at most one
  behind any single choice. At least one is reachable on every floor.
  `ForcedTypeAt`'s forced elite is removed.
- **Rolled types:** fights about 50%, using the waypoint rules in §2's table.
  No two events adjacent.
- **Validator** (`RealmMapRules`):
  - Every node is reachable and the boss is reachable.
  - The spine carries no anchor.
  - One shop, on a fork. Rest placement as in §2.
  - Ids are unique. Offsets never decrease along an edge, and the entry's
    successors have offset ≥ 1 (`DescentIsUnderWay` reads `step > 0`,
    `RunSnapshot.cs:72`).
- **Relax order** when rules conflict: rest placement, then shop position,
  then fork count. Never fewer than one pocket.
- **Fuzz test:** 10k seeds across floors 1–12. It pins literal expected
  counts per floor class (gotcha 5), plus decision metrics: pocket count,
  anchors behind distinct choices, and longest minus shortest route.

## 5. Encounter preview: one source of truth

The preview calls `EncounterRoll.Roll` on a fresh
`RngStreams.Open(Fight, step, nodeId)` with the same `RunEncounter.Pool()` and
floor band. That is the call `RunEncounter.For` makes (`RunEncounter.cs:83-116`).
There is no new stream, no stored roll, and no change to Fight-stream outputs,
so bot determinism holds. Strength is derived from the rolled enemies' scaled
stats against the party. Test: for every node on 200 seeds, the preview's enemy
ids equal those from `RunEncounter.For(...)`.

## 6. Anchors in the boss fight (their own phase)

A living anchor joins the boss fight as an extra enemy. That reuses what combat
already does (multi-enemy fights, elite scaling via
`FightEncounterAdapter.cs:85`), so it needs no new ability system. Bespoke
boss modifiers would need passive and per-turn hooks the enemy schema lacks
(`CONTENT_SCHEMA.md:79-80, 260-261, 409`).

- **Boss choice:** written once when the realm opens (`StartRun`,
  `AdvanceLeg`), from the unused `RngStreams.Boss` stream, into the existing
  `RunSnapshot.bossEnemyId`. That field has readers but no writer today.
- **Saved:** `RunSnapshot.slainAnchorIds`.
- **Fight build:** the boss roster appends each living anchor's rolled enemy
  (the same `EncounterRoll` call as its map preview, §5). Check before sizing:
  - whether `IsElite` scaling is per roster or per enemy;
  - whether a boss roster accepts extra enemies.
  If either needs a contract change, that is `Escalation: architecture`.
- **Tuning:** an anchor joining at full elite stats may be too much next to a
  boss. It joins at a fraction of its HP, set by the bot (P5).
- **Until P5:** anchors are plain optional elites with a visible reward. The
  pocket decision is proven first.

## 7. Screen

1920×1080, scrolling vertically with the camera following the party. Built
only through `Domain/UiKit/Screens/MapScreen.cs` and `ScreenRegistry.cs`; never
hand-edit the scene.

- **Top band:** realm name, floor, gold, and the cache balance once P7 lands.
- **Bottom detail card:** for the focused node, its type, enemies, reward and
  (for an anchor) that it will stand with the boss if left alive. When two choices are open, the card
  compares them side by side.
- **Fixed node grammar:** medallion = type, pips = strength, at most one
  reward chip. A node looks the same no matter what else is on screen.
- **Closed-off routes:** focusing a choice veils, under painted fog, the routes
  it would close off, and only when something does close.
- **Heart:** the boss sprite with its living anchors beside it. Painted roots
  run from the heart to each anchor on the map and wither when it falls.
- **Opening:** on entering a realm the camera starts on the heart and rises
  to the party (2.5 s at most, skippable). After that the map stays out of the
  way, with walks capped as today.
- **Gamepad:** Left and Right step through the current choices in screen
  order, and Submit walks. Y toggles a zoomed-out overview where the d-pad
  inspects every node read-only. Mouse hover is focus. There is no free
  cursor.
- **Walk:** `MapWalk` and `MapController.Walk` are reused, along vertical
  curves.
- **Art brief v2:** replaces `RUN_MAP_ART_BRIEF.md`, which is horizontal
  (`:11, 44, 50, 78, 90`). It covers:
  - vertical found/eased paintings with the same safe-area method;
  - medallions, adding shop, cache and anchor;
  - one chip per reward kind and the strength pip;
  - the root stroke, living and withered;
  - a vertical trail.
  Greyboxes stand in until the art arrives.

## 8. Phases

| # | Work | Owner | Gate |
|---|---|---|---|
| P0 | Owner sign-off on §9. Art brief v2. Five storyboard frames: arrive, choose a pocket, an anchor falls, the heart, a later run. EV sheet for a pocket at full HP and at 40% HP, plus the floor-1 exp/loot budget against today. | orchestrator + owner | written sign-off |
| P1 | `RealmMap`, `RealmLayout`, `RealmMapRules`, fuzz test, all pure Domain. | implementer | `test.ps1 run`, then `run_tests_parallel.ps1 -Changed` |
| P2 | Integration: step assignment and `roomsWalked`; the preview (§5); `restBeforeBoss` input and `_map` cache key (`RunManager.cs:123-124`); debug jump; SaveData v8→v9 (below); a column projection so the current screen stays playable; rewrite the broken tests (`DescentLeg*`, `DescentRoadVariation`, `RunManagerTests`, `FightAfterTheElite`, `EventRunBuff`, `RunEnding`, `RarityTableTests` and `EnemyBandTests` if touched); update `CODE_MAP.md` and `ARCHITECTURE.md`. | senior, `Escalation: cross-layer` (Domain generator + Core RunManager/RunEncounter + SaveData ladder land atomically) | `-Changed -BuildScenes` |
| P3 | Bot: `MapView` on `RunView` (path to heart, pockets, anchors) and path-aware choices in the four policies (`IRunPolicy.cs:19`). **Gate:** the pocket-take rate must differ by HP band (it can't be ~0% or ~100% in every band), and anchors taken by strength band must be reported. | implementer, then verifier | bot report |
| P4 | Screen: the graph, node grammar, detail card and comparison, walk, gamepad, overview. | implementer | `-Changed -BuildScenes`, diff `Art/` |
| P5 | Anchors in the boss fight (§6): `slainAnchorIds`, boss roster build, anchor HP fraction, heart drawing, combat tests (one and two anchors alive). The bot re-runs the P3 gate. | implementer, or senior with `Escalation: architecture` if the roster contract must change | `-Changed -BuildScenes` |
| P6 | Easing mask: a one-day spike (hand-written UI shader keeping stencil/clip, content-space UVs, rebuilt from `clearedNodeIds` on load), then the build and the art swap. | implementer | headless mask test + screenshot |
| P7 | Cache waypoint, if signed: deposit, `RunSettlement` exemption, at most one per realm. | implementer | `-Changed` |

If P3 fails, tune before building the screen. The levers are the size of the
pocket prize, fork-vs-pocket placement of rest and treasure, and the strength
mix.

### SaveData v8 → v9

- Add a `version < 9` step after the `version < 8` block
  (`SaveData.cs:546-548`). Bump `CurrentVersion` (`:66`) and extend the
  ladder comment.
- A run that is mid-realm restarts the realm at its entry:
  - `currentNodeId` is set to the new entry; `clearedNodeIds` is cleared;
    `step` is set to `legStartStep`.
  - Clear `eventId`, `eventNodeId`, `eventPageId`, `pendingFight`,
    `pendingShelf`, `eventResult`, `shopStock` and `shopNodeId`.
  - Leg-scoped `eventBuffs` are dropped explicitly. They lapse only when
    `legStartStep` moves (`RunSnapshot.cs:387-391`).
  - HP is kept as saved, with no refill.
- New fields: `roomsWalked`, `slainAnchorIds`.
- Test: a v8 save taken mid-realm, with an open shop, an open event and a
  pending fight, loads clean.

## 9. Owner decisions

1. **The screen:** accept the "graph in a painted place" presentation against
   `DIRECTION_BIBLE.md:57`, on the strength of the storyboard.
2. **Orientation:** top to bottom (recommended) or keep left to right.
3. **Elites:** optional anchors that join the boss when left alive resolve the open "forced or avoidable"
   (`DIRECTION_BIBLE.md:56`) as avoidable.
4. **Cache:** build it (G2) or drop it.
5. **Endless descent:** it stays. Pocket growth stops at floor 8; past that,
   floors differ by difficulty and content only.

## 10. Checked against acclaimed games

The comparison and the five P0 storyboard frames:
https://claude.ai/artifact/AWhthzsNhso4dzaEcjBTSx (advisory summary:
https://claude.ai/artifact/Fe1AMzHFEjmjSmYYjPKgvL). What it changed in this
plan:

- Anchors join the boss instead of granting abstract wards (Shadow of
  Mordor's bodyguards). It is easier to read and needs no new combat system.
- Bypassing a pocket heals a little (Balatro pays for skipping a blind), so
  restraint has a reward of its own.
- The visible boss is not sold as new: Slay the Spire and Balatro already show
  it. What is new is that the boss you face is the one your route left.
- A time budget for the map (Darkest Dungeon II's slow travel is the warning).

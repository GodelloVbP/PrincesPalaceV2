# Balance bot output schema

The contract between `PrincesPalace.Editor.Bot.BalanceBotRunner.RunFromCommandLine`
(writer, not yet built) and `tools/bot_report.py` (reader, this doc's other
half). Written against `Assets/_Project/Scripts/Domain/Bot/RunTrace.cs` as it
stands on `balance-bot` — the field names below are read from that file, not
guessed. If that file's shape changes, this doc and `bot_report.py` both need
a pass; nothing here should be inferred by convention.

A batch directory (`reports/bot/<yyyyMMdd-HHmmss>/`, made by `tools/bot.ps1`)
holds exactly two files:

- `traces.jsonl` — one full `RunTrace` per line, for manually digging into a
  specific seed a bug row names. Nothing in `bot_report.py` parses it; it is
  archival.
- `summary.json` — one aggregate object. Everything the HTML report renders,
  including every metric in plan §2 and every check in plan §3, is computed
  **inside the runner**, in-process, while it still holds the live
  `FightSession`/`Character` objects. `bot_report.py` never recomputes a
  metric from raw traces — it only renders `summary.json` and diffs it
  against a previous batch's `summary.json`.

That split matters for one metric family in particular: "doomed" and "swing"
(below) are defined in terms of the party's *max* HP, which is not a trace
field (max HP moves with level, gear and relics, and recording it per turn
would bloat every trace for a number the runner already has on hand at
computation time). The runner reads it straight off the live party. No schema
change is needed to support this — do not add a `PartyHpMax` field to
`RunTrace.cs` to chase it.

## `traces.jsonl`

One JSON object per line, one line per run, in the order runs completed. Keys
match the C# field names in `RunTrace.cs` verbatim (`PascalCase`), because the
intended writer is `JsonUtility.ToJson` over those types — do not rename on
the way out. A consumer that wants a specific run finds it by scanning for
`"Seed":<n>` together with the right `Archetype`/`Profile` (there is no
index; batches are hundreds to low thousands of lines, and `grep` is enough).

```
RunTrace
  Seed          uint64, JSON number (Python's json module is arbitrary-
                precision int and loses nothing; a non-Python reader must
                not round-trip this through a 64-bit double)
  Archetype     string, e.g. "RandomLegal"
  Profile       string, e.g. "Fresh"
  Fights        FightTrace[]
  Rooms         RoomTrace[]
  DeathStep     int  -- the Step of the fight or room the run ended on;
                        meaningless (0) when Capped is true
  DeathCause    string -- free text, human-readable, e.g. "killed by
                           forest_troll in a Fight room on floor 3";
                           not machine-parsed anywhere, see deathCauses[]
                           below for the structured version
  Capped        bool -- true if the run reached DepthCapSteps alive

FightTrace
  Step          int  -- position in the run's overall step sequence (fights
                         and non-fight rooms share one counter)
  Floor         int
  RoomType      string
  EnemyIds      string[]
  Turns         int
  PartyHpIn     int  -- party HP total entering the fight
  PartyHpOut    int  -- party HP total leaving it (0 on a loss)
  DamageDealt   int  -- total across all player actions this fight
  DamageTaken   int  -- total across all enemy actions this fight
  TurnTraces    TurnTrace[]
  Won           bool
  PayoutGold    int  -- 0 when Won is false
  PayoutExp     int  -- 0 when Won is false

TurnTrace
  ActorId       string -- combatant id issuing this command
  Action        string -- "Attack" / "Skill:<name>" / "Item:<name>" /
                          "HoldBack"
  TargetId      string -- "" for HoldBack
  PartyHpAfter  int    -- party HP total immediately after this command
                          resolved (including the enemy reply, per F6: one
                          command call resolves the whole exchange)
  EnemyHpAfter  int    -- sum of living enemies' HP immediately after

RoomTrace
  Step          int
  NodeId        int
  RoomType      string
  OfferItemIds  string[] -- empty when this room made no item offer
  PickedIndex   int      -- -1 when nothing was offered or nothing taken
```

## `summary.json`

```jsonc
{
  "batch": {
    "timestamp": "2026-09-01T14:03:00Z",   // ISO 8601 UTC, batch start
    "commitSha": "d051b03",                // git rev-parse HEAD of main, short
    "runsPerCell": 200,
    "firstSeed": 1,                        // seeds run are firstSeed .. firstSeed+runsPerCell-1
    "archetypes": ["RandomLegal", "GreedyAggressive"],
    "profiles": ["Fresh"],
    "depthCapSteps": 40,                   // 5 legs x 8 steps/leg, see plan F4
    "elapsedSeconds": 47.2
  },
  "cells": [
    {
      "archetype": "RandomLegal",
      "profile": "Fresh",
      "runs": 200,
      "depth": { "median": 12, "p10": 4, "p90": 22, "mean": 12.8, "cappedShare": 0.0 },
      "deathCauses": [
        { "enemyId": "forest_troll", "roomType": "Fight", "floor": 3, "count": 34 }
      ],
      "doomedShare": 0.41,
      "swingShare": 0.22,
      "fightLength": {
        "byFloor": { "1": 3.1, "2": 3.4 },
        "byRoomType": { "Fight": 3.3, "Elite": 5.1 }
      },
      "turnOneShare": 0.08,
      "steamrollByFloor": { "1": 0.35, "2": 0.19 },
      "consumableUseShare": 0.44,
      "potionsWastedMean": 1.2,
      "buildDiversity": { "distinctRelicSets": 41, "distinctTalentSets": 3 },
      "itemPickRate": { "healing_draught": 0.6, "iron_ration": 0.15 },
      "relicPickRate": { "bloodlust": 0.3 }
    }
  ],
  "decisionPressure": { "nodes": 0.31, "offers": 0.44, "relics": 0.52 },
  "coverage": {
    "enemiesNeverSeen": [],
    "relicsNeverOffered": ["ashbound_locket"],
    "relicsNeverPicked": ["ashbound_locket", "tin_whistle"],
    "itemsNeverOffered": [],
    "itemsNeverPicked": ["stale_bread"],
    "skillsNeverUsed": []
  },
  "archetypeGap": [
    {
      "profile": "Fresh",
      "archetypes": [
        { "archetype": "RandomLegal", "medianDepth": 12 },
        { "archetype": "GreedyAggressive", "medianDepth": 19 }
      ]
    }
  ],
  "bugs": [
    {
      "invariant": "HpOutOfRange",
      "seed": 17,
      "archetype": "RandomLegal",
      "profile": "Fresh",
      "step": 9,
      "nodeId": 4,
      "detail": "party HP 130 exceeds max 120 after ExecuteAttack",
      "lastActions": ["Attack:goblin_1", "Skill:0:goblin_2"],
      "stack": null
    }
  ],
  "determinism": {
    "checked": 400,
    "mismatches": [
      { "seed": 17, "archetype": "RandomLegal", "profile": "Fresh" }
    ]
  }
}
```

### Field definitions

**`batch`** — one row of facts about the whole run, not per cell.

- `timestamp`: when the batch started, ISO 8601, UTC (`Z` suffix).
- `commitSha`: short SHA of `main`'s `HEAD` at batch time, so a report can be
  matched back to the code it measured.
- `runsPerCell`: runs per archetype x profile combination; every cell has the
  same count (a partial cell from a crash is still written, with `runs` set
  to however many completed — see "Partial batches" below).
- `firstSeed`: seeds for every cell are the contiguous range
  `[firstSeed, firstSeed + runsPerCell)`. The same seed range is reused
  across every archetype x profile combination, which is what makes
  `archetypeGap` and `decisionPressure` well-defined without a separate
  "shared seeds" list.
- `archetypes`, `profiles`: the full cross product is `cells`.
- `depthCapSteps`: the run-length cap in steps (plan F4; steps count fights
  and non-fight rooms together, so this is not simply legs x rooms/leg
  without knowing the leg's room count, but 5 legs x 8 steps/leg = 40 is the
  assumed default per plan §5).
- `elapsedSeconds`: wall-clock time for the whole batch, all cells.

**`cells[]`** — one entry per `(archetype, profile)` pair.

- `runs`: number of runs actually completed in this cell (see partial
  batches).
- `depth`: distribution of `RunTrace.DeathStep` over the cell's runs.
  `median`/`p10`/`p90`/`mean` are over that distribution; a capped run
  contributes `depthCapSteps` as its depth (it did not die, but it did stop
  there). `cappedShare` = capped runs / `runs`.
- `deathCauses[]`: for every non-capped run, take the `FightTrace` whose
  `Step` equals `DeathStep` (the fight the run ended in — a death in a
  non-fight room does not happen today per F6/§0, so this list is empty for
  those). Emit one row per distinct `(enemyId, roomType, floor)` where
  `enemyId` ranges over that fight's `EnemyIds` — a multi-enemy fight
  contributes one row per enemy present, not one for whichever landed the
  killing blow (the trace does not record that). `count` is the number of
  runs contributing to the row. Sorted by `count` descending; the report
  shows the top rows and folds the rest into an "other" line if there are
  more than 15.
- `doomedShare`: of the cell's non-capped runs, the share where the party's
  HP fraction (current / max) was never above 0.5 at the end of any of the
  last 3 fights before death (fewer than 3 if the run had fewer fights).
  Max HP is read from the live party at computation time, not from the
  trace. Runs with fewer than 1 fight (died in room 1, if that's ever
  possible) are excluded from the denominator.
- `swingShare`: of all `TurnTrace` entries across the cell's fights, the
  share where `|PartyHpAfter - PartyHpBefore| / partyMaxHp > 0.25`, where
  `PartyHpBefore` is the previous turn's `PartyHpAfter` (or `PartyHpIn` for
  the fight's first turn) and `partyMaxHp` is read live, at the time of that
  turn, from the same party.
- `fightLength.byFloor` / `.byRoomType`: mean `Turns` per fight, grouped by
  `Floor` / `RoomType`. Keys are stringified floor numbers / room type names.
- `turnOneShare`: share of fights with `Turns == 1`.
- `steamrollByFloor`: share of fights per floor with `DamageTaken == 0`.
- `consumableUseShare`: share of fights containing at least one
  `TurnTrace` whose `Action` starts with `"Item:"`.
- `potionsWastedMean`: mean, across the cell's runs, of consumables left in
  the stockpile unused when the run ends (death or cap). "Potion" here means
  any consumable item, not a specific item id.
- `buildDiversity`: `distinctRelicSets` / `distinctTalentSets` = count of
  distinct (order-independent) relic-id sets / talent-id sets held at death
  or cap, among the cell's deepest 10% of runs by `DeathStep` (at least 1
  run; round up).
- `itemPickRate` / `relicPickRate`: for each item/relic id that was ever
  offered in this cell, picks / offers (a `RoomTrace.PickedIndex >= 0`
  pointing at that id counts as a pick). An id never offered is omitted here
  (it shows up in `coverage` instead, not as a `0/0` row).

**`decisionPressure`** — batch-wide, not per cell (it is specifically about
archetypes disagreeing with each other, so it needs more than one archetype
to mean anything; with a single archetype in the batch, all three values are
`null`).

- `nodes`: share of `(seed, profile, step)` positions where at least two
  archetypes reached that step and chose a different next node.
- `offers`: same, for item-offer `PickedIndex` at matching
  `(seed, profile, step, NodeId)` offers.
- `relics`: same, for the relic draft pick at matching `(seed, profile)`
  (one draft per run, at the start).

Denominator for each is the count of positions reached by 2+ archetypes on
the same seed/profile; a position only one archetype ever reaches (because an
earlier disagreement diverged the run) does not count either way.

**`coverage`** — batch-wide (offering/pick behavior does not usually depend
on archetype in a way worth splitting; if it later does, revisit). Each list
is content ids that never appeared in the stated role across every cell:
`enemiesNeverSeen` (never in any `FightTrace.EnemyIds`), `relicsNeverOffered`
/ `relicsNeverPicked`, `itemsNeverOffered` / `itemsNeverPicked`,
`skillsNeverUsed` (never the target of a `TurnTrace.Action` of the form
`"Skill:..."`). Sourced against the full content database, not just what
showed up in traces, so a relic id that exists in content but was never
drawn is caught rather than silently absent.

**`archetypeGap[]`** — one entry per profile, holding each archetype's
`depth.median` for that profile, side by side, for the report's headline
chart. This is a projection of `cells[].depth.median` (same numbers), kept as
its own array because it is grouped by profile-then-archetype rather than
by cell, and because a future batch that runs archetypes over different seed
ranges per profile would need this field to state which seeds the median was
actually taken over — out of scope for v1 (all archetypes in a profile share
`firstSeed`/`runsPerCell`), but the field exists now so that day is not a
schema break.

**`bugs[]`** — one row per invariant hit (plan §3), not deduplicated by
seed; a run that trips the same invariant twice produces two rows.

- `invariant`: short PascalCase name (`HpOutOfRange`, `FightNotOver`,
  `NoLegalAction`, `PayoutMismatch`, `StuckAfterWin`, `EmptyOffer`,
  `OfferPickNotStockpiled`, `RewardsDecreased`, `Exception`). Names are
  chosen by the runner; `bot_report.py` does not special-case them, it just
  renders whatever string is here — so a new invariant needs no report
  change.
- `seed`, `archetype`, `profile`, `step`: where it happened.
- `nodeId`: the map node in play at the time, `null` if not applicable
  (e.g. mid-fight, where node doesn't apply — use the fight's containing
  `RoomTrace.NodeId`).
- `detail`: one line, human-readable, with the actual numbers involved.
- `lastActions`: up to the 10 most recent `TurnTrace.Action` strings
  (oldest first) leading up to the hit, for repro without opening
  `traces.jsonl`.
- `stack`: exception stack trace as a string, `null` for a value-invariant
  hit (not an exception).

**`determinism`** — every run in the batch is executed twice back to back
(this is what plan §1 calls "a test the bot gets for free"; a fight is
milliseconds, so doubling run count is cheap next to one Unity boot).

- `checked`: total number of runs verified this way (= sum of `cells[].runs`
  for a complete batch).
- `mismatches[]`: `(seed, archetype, profile)` triples where the two
  `RunTrace.Hash()` values differed. Reported once per triple, not once per
  differing field — the whole point is "this seed is not reproducible",
  which is the finding regardless of where in the trace it first diverges.

### Partial batches

A batch that stops early (crash, timeout, killed process) still gets whatever
`summary.json` the runner had accumulated flushed to disk, with `cells[].runs`
below `batch.runsPerCell` for the unfinished cell(s) and no entry at all for
cells not yet started. `bot_report.py` renders whatever is there and does not
treat a short `runs` count as an error — the HTML header shows
`runs: 143/200` for a short cell so it reads as partial rather than wrong.

### "Better/worse" for the delta column

`tools/bot_report.py` colors a cell's delta against the previous batch only
where "better" has a stated direction:

- `depth.median` (and `p10`/`p90`/`mean`): higher is better (deeper runs).
- `bugs` count (batch-wide, and per-invariant): lower is better.
- `determinism.mismatches` count: lower is better (zero is the only healthy
  value).

Every other numeric field (shares, rates, diversity counts, fight length) is
shown with a plain delta and no color — a change is a finding to read, not a
verdict; a rising `steamrollShare` might be a buff to celebrate or a fight
that stopped mattering, and the bot cannot tell which.

# Balance bot output schema

The contract between `PrincesPalace.Editor.Bot.BalanceBotRunner.RunFromCommandLine`
(writer, not yet built) and `tools/bot_report.py` (reader, this doc's other
half). Written against `Assets/_Project/Scripts/Domain/Bot/RunTrace.cs` as it
stands on `balance-bot` — the field names below are read from that file, not
guessed. If that file's shape changes, this doc and `bot_report.py` both need
a pass; nothing here should be inferred by convention.

A batch directory (`reports/bot/<yyyyMMdd-HHmmss>/`, made by `tools/bot.ps1`)
holds one `summary.json` plus one set of shard files per Unity process that
produced it. With `-Shards 1` the shard files sit in the batch directory
itself; with `-Shards N` they sit in `shard-1/` ... `shard-N/`.

Per shard:

- `traces.jsonl` — one full `RunTrace` per line, for manually digging into a
  specific seed a bug row names. Nothing in `bot_report.py` or `bot_merge.py`
  parses it; it is archival.
- `runs.jsonl` — one **flat row per run**, carrying everything every metric
  below is computed from. This is the file the merger reads.
- `content.json` — the id lists `coverage` is differenced against, read out
  of `ContentDatabase` at batch time.
- `batch.json` — what that shard was asked to do (see `batch` below).

Per batch:

- `summary.json` — one aggregate object, written by **`tools/bot_merge.py`**
  over every shard's `runs.jsonl` + `content.json` + `batch.json`.
  `bot_report.py` never recomputes a metric from raw traces — it only renders
  `summary.json` and diffs it against a previous batch's.

**Where the aggregates are computed, and why that moved.** They used to be
computed inside the runner, in-process, while it still held the live
`FightSession`/`Character` objects. Sharding ended that: a batch is now N
Unity processes over disjoint seed ranges, and almost nothing in
`summary.json` can be merged from N per-shard summaries. A median of medians
is not a median; `decisionPressure`'s denominator counts positions reached by
two or more archetypes on the same seed, which is only knowable once every
archetype's runs are in one place; "this relic was never offered" is only
true if it was never offered in *any* shard. So the runner emits facts and
`bot_merge.py` computes every aggregate exactly once, over all of them. There
is deliberately only ONE implementation of each metric, and it is the Python.

The reason the split existed at all survives intact, in a narrower form:
"doomed" and "swing" (below) are defined in terms of the party's *max* HP,
which is not a trace field (max HP moves with level, gear and relics, and
recording it per turn would bloat every trace). The runner reads it straight
off the live party and writes it into `runs.jsonl` — `fights[].partyMaxHp`,
plus a pre-reduced `swingTurns`/`swingDenomTurns` pair. Do **not** add a
`PartyHpMax` field to `RunTrace.cs` to chase it.

## `runs.jsonl`

One JSON object per line, one line per run, in the order runs completed.
`camelCase` keys, unlike `traces.jsonl` (which mirrors C# field names because
it mirrors a C# type). Written as runs finish, so a batch killed halfway
still leaves every completed run on disk.

```
seed              uint64, JSON number, same precision note as traces.jsonl
archetype         string
profile           string
capped            bool
deathStep         int   -- 0 and meaningless when capped
consumablesLeft   int   -- feeds potionsWastedMean
replayed          bool  -- was this run played twice (see -botReplayShare)
hashMatched       bool  -- true when not replayed; only meaningful with it
swingTurns        int   -- commands that moved party HP by >25% of max
swingDenomTurns   int   -- commands that were eligible to (max HP known)
relicIds          string[] -- held when the run ended
talentIds         string[] -- ditto
gearIds           string[] -- worn across the fielded squad when the run
                              ended, ordinal-sorted; the third build axis,
                              feeds buildDiversity.distinctGearSets
levelAtDeath      int   -- fielded squad's total level when the run ended
skillsUsed        string[] -- distinct ids behind "Skill:" turn labels
fights[]          step, floor, roomType, enemyIds, turns, damageTaken,
                  partyHpOut, partyMaxHp, usedItem, won, payoutGold
                  -- won/payoutGold exist for one number: "gold forgone", the
                  median WON-fight payout at the step band a shop was taken
                  at, which is what taking that node cost (PLAN_SHOP.md
                  SS7.1 point 1). A lost fight pays zero, so the median has
                  to be able to exclude it.
rooms[]           step, floor, nodeId, roomType, offerItemIds, pickedIndex,
                  favor, encounterClass, offers[], equippedItemIds,
                  goldOnArrival, goldSpent, goldOnLeave,
                  purchasesBySection[], rerollsBySection[], shopOffers[],
                  shopChoices[]
                  equippedItemIds is what
                  the equip pass after this room actually put on; feeds
                  itemEquipRate.
                  favor is ItemOfferRoll.CurrentSquadFavor() at the moment
                  this room's offer was rolled (0 / "" for a room that made
                  no offer). encounterClass is "Normal" or "Elite", read off
                  the exact same expression RunOrchestrator.RollOffers uses
                  (session.IsEliteFight ? Elite : Normal,
                  Assets/_Project/Scripts/Core/Bot/RunOrchestrator.cs:472-474)
                  -- IsBossFight is never consulted there, so a boss room's
                  offer rolls as "Normal" (or "Elite" if it also happens to
                  be flagged elite) today; encounterClass records what the
                  roll actually saw, not what a boss room arguably deserves.
                  offers[] is one entry per offerItemIds entry, index-aligned:
                  {itemId, tier, plus, riftTier, modifierCount} -- the axes
                  RarityTable/LootLadder/ModifierTable actually rolled for
                  that copy (riftTier is the plain int backing
                  Domain.Content.RiftTier, 0..3).
                  goldOnArrival is recorded for EVERY room, not only for
                  shops, and read BEFORE the room resolves -- so a treasure
                  room's stash is not already in it. It is what a player
                  HOLDS when a door opens, which is neither their lifetime
                  winnings nor a median over fights they won, and it is what
                  the arrival-gold-by-step table is computed from.
                  goldSpent / goldOnLeave are 0 for every room that is not a
                  shop. Kept as two numbers rather than one difference so a
                  sale, which moves gold the other way, cannot hide inside a
                  subtraction.
                  purchasesBySection[] / rerollsBySection[] are indexed by
                  ShopStock's section constants (gear 0, books 1, relics 2).
                  shopOffers[] is the shelf as it stood when the visit ENDED:
                  {kind, contentId, price, sold}, NO OFFER placeholders
                  excluded. Affordability "on arrival" is this price against
                  goldOnArrival.
                  shopChoices[] is every ChooseShop answer IN ORDER, refusals
                  and the closing leave included:
                  {kind, section, index, goldDelta, outcome, refusal}.
                  `kind` is ShopChoiceKind's name, plus two the policy never
                  says: "Leave" for the answer that ended the visit and
                  "Capped" for a visit cut off at BotRunDriver's twelve-choice
                  ceiling. The counters above cannot express ORDER, and order
                  is the difference between "sold the duplicate, then bought
                  what beat it" and the reverse; they cannot express a
                  REFUSAL at all.
relicRounds[]     offerIds, pickedIndex   -- -1 for "took nothing"
bugs[]            invariant, step, nodeId, detail, lastActions, stack
                  (the merger adds seed/archetype/profile on the way out)
```

## `content.json`

```
enemyIds           string[]
relicIds           string[]
offerableItemIds   string[] -- ContentDatabase.Offerable, not every item
skillIds           string[] -- skills.json, which holds BOTH player skills
                              and enemy abilities
enemyAbilityIds    string[] -- the subset of skillIds an enemy owns, via
                              RawEnemyAbility.skillId
```

Identical across shards of one batch. The merger unions them anyway, so a
mismatched shard degrades to "everything any shard believed exists" — which
over-reports a coverage gap rather than hiding one.

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
  EquippedAtStart EquipTrace[] -- the one equip pass that happens before any
                        room does: the party dressing itself out of the
                        profile's starting stock. Not a RoomTrace because
                        there is no room yet.
  WornAtDeath   string[] -- item ids across the fielded squad when the run
                        ended, ordinal-sorted. On the trace and not computed
                        in the runner because it is the only place it can be
                        read at all: RunManager.EndRun strips the paperdoll,
                        so anything asked afterwards reports empty for every
                        run, which is every run.
  LevelAtDeath  int  -- fielded squad's total level. Summed rather than per
                        character: only Shawn is really fielded today, and a
                        sum degrades correctly while a first-member read
                        would start lying the day a second character is real.
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
  Floor         int
  NodeId        int
  RoomType      string
  OfferItemIds  string[] -- empty when this room made no item offer
  PickedIndex   int      -- -1 when nothing was offered or nothing taken
  Offers        OfferEntry[] -- index-aligned with OfferItemIds; empty when
                        this room made no offer
  Favor         int    -- ItemOfferRoll.CurrentSquadFavor() at roll time; 0
                        when this room made no offer
  EncounterClass string -- "Normal" or "Elite", exactly as
                        RunOrchestrator.RollOffers computed it for this
                        roll (session.IsEliteFight ? Elite : Normal); ""
                        when this room made no offer. IsBossFight is not
                        consulted there, so a boss room's roll reads
                        "Normal" today -- see BalanceBotRunner.RunRowJson's
                        note on the same field.
  Equipped      EquipTrace[] -- what the equip pass after this room put on.
                        Empty for most rooms, which is the honest answer: a
                        player does not re-dress after every fight either.
  LearnedSpellCountAfterRoom int -- fielded characters that have learned a
                        first spell, read AFTER this room's pending spell
                        assignments resolve (docs/PLAN_SHOP.md 1g/2g, gate
                        3). Recorded for EVERY room, not only ones with a
                        book offer, for the same reason GoldOnArrival is:
                        gate 3's numbers ask about a specific step, and a
                        single end-of-run reading cannot answer that.
  UnassignedSpellBookCountAfterRoom int -- pending books still unassigned
                        at the same point.
  SpellAssignments SpellAssignmentTrace[] -- every ChooseSpellAssignment
                        answer this room, in order -- the acquisition-loop
                        analogue of RoomTrace.ShopChoices.

OfferEntry
  ItemId        string
  Tier          int
  Plus          int
  RiftTier      int    -- (int)Domain.Content.RiftTier, 0..3
  ModifierCount int

SpellAssignmentTrace
  SkillId       string
  Assigned      bool
  CharacterId   string -- "" when Assigned is false
  Slot          int    -- -1 when Assigned is false
  Outcome       string -- ShopResult.Outcome's name when Assigned is true;
                          "Skip" otherwise -- a policy declining is not a
                          refusal, so it gets its own word rather than
                          borrowing ShopOutcome.Refused for a choice nothing
                          refused.

EquipTrace
  CharacterId   string -- CharacterDefinition.id it was worn by
  ItemId        string
  Slot          string -- EquipmentSlot name, e.g. "Weapon1"
  Plus          int    -- which copy; the bag keys stacks on it
```

### `RunTrace.Hash()` and this change

`Hash()` folds every field named above into one FNV-1a string (see
`RunTrace.cs`'s own comment on why FNV-1a rather than
`System.Security.Cryptography`). Adding `RoomTrace.Floor`/`Favor`/
`EncounterClass`/`Offers` to the fields the hash loop appends means the hash
of a run traced before this change and the identical run traced after it
will differ -- not because anything about how the run plays changed, but
because the hash now covers strictly more of the trace than it did. This is
expected and not a `determinism` mismatch: the determinism check
(`bot_merge.py`'s `determinism.mismatches`) compares two hashes taken with
the SAME build replaying the SAME seed back to back within one batch, so
both sides of that comparison always include the new fields identically.
Nothing about the existing fields' contribution to the hash changed -- the
new `Append` calls are additions to the string being hashed, not edits to
how the old fields are appended.

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
    "elapsedSeconds": 47.2,
    "shards": 1,
    "shopPolicies": ["WhenOffered"]        // a LIST: merging two batches to
                                           // compare them puts both here
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
      "buildDiversity": { "distinctRelicSets": 41, "distinctTalentSets": 3, "distinctGearSets": 28 },
      "itemPickRate": { "healing_draught": 0.6, "iron_ration": 0.15 },
      "itemEquipRate": { "healing_draught": 0.0, "iron_ration": 0.11 },
      "relicPickRate": { "bloodlust": 0.3 },
      "shop": {
        "visits": 340,
        "arrivalGold": { "p10": 12, "p25": 24, "median": 51, "p75": 96 },
        "belowCheapestShare": 0.22,
        "goldForgone": { "1-8": 16, "9-16": 22, "17-24": 20, "25-40": 14 },
        "purchasesPerVisit": 0.61,
        "zeroPurchaseShare": 0.55,
        "affordableShareBySection": { "gear": 0.44, "books": null, "relics": 0.07 },
        "rerollsPerVisitBySection": { "gear": 0.09, "books": 0.0, "relics": 0.02 },
        "rerollThenNoPurchaseShare": { "gear": 0.05, "books": null, "relics": null },
        "spendShareBySection": { "gear": 0.72, "books": null, "relics": 0.28 },
        "goldOnLeaveMedian": 20,
        "goldAtDeathMedian": 44,
        "arrivalGoldByStep": {
          "4": { "p10": 8, "p25": 16, "median": 33, "n": 3812 },
          "8": null
        }
      },
      "spellAcquisition": {
        "learnedFirstSpellByStep": {
          "8": { "share": 0.12, "n": 190 },
          "16": null,
          "24": null,
          "32": null
        },
        "slotsFilledPerLegMean": 0.31,
        "depthZeroBooksAtLeg2": 9,
        "depthSomeBooksAtLeg2": 14,
        "shopVisitsShowingUnaffordableBookShare": 0.18
      }
    }
  ],
  "decisionPressure": { "nodes": 0.31, "offers": 0.44, "relics": 0.52 },
  "shopVsNoShop": {
    "pairs": 3000,
    "pairsWithAShopVisit": 1840,
    "medianDepth": { "WhenOffered": 12, "Never": 13, "delta": -1 },
    "survivalToNextBossShare": { "WhenOffered": 0.41, "Never": 0.44 },
    "depthVariance": { "WhenOffered": 61.2, "Never": 58.9 }
  },
  "coverage": {
    "enemiesNeverSeen": [],
    "relicsNeverOffered": ["ashbound_locket"],
    "relicsNeverPicked": ["ashbound_locket", "tin_whistle"],
    "itemsNeverOffered": [],
    "itemsNeverPicked": ["stale_bread"],
    "skillsNeverUsed": [],
    "enemyAbilityIds": ["hex", "roar"]
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
- `elapsedSeconds`: wall-clock time for the whole batch, all cells. With
  several shards this is the **slowest shard's** wall clock, not the sum:
  they ran at the same time, and summing would report a batch four times
  longer than the user watched it take.
- `shards`: how many Unity processes produced the batch. `runsPerCell` is the
  sum across them and `firstSeed` the lowest, so the seed range still reads
  as one contiguous block.

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
- `buildDiversity`: `distinctRelicSets` / `distinctTalentSets` /
  `distinctGearSets` = count of distinct (order-independent) relic-id,
  talent-id and worn-item-id sets held at death or cap, among the cell's
  deepest 10% of runs by `DeathStep` (at least 1 run; round up). Gear is the
  third axis and the one where most of a build's variation actually lives:
  relics and talents are picked from a handful of options, the paperdoll is
  eight slots filled out of everything the run was offered.
- `itemPickRate` / `relicPickRate`: for each item/relic id that was ever
  offered in this cell, picks / offers (a `RoomTrace.PickedIndex >= 0`
  pointing at that id counts as a pick). An id never offered is omitted here
  (it shows up in `coverage` instead, not as a `0/0` row).
- `itemEquipRate`: for each item id that was ever OFFERED in this cell,
  how many copies the equip pass actually put on somebody, over offers.
  Denominator is offers rather than picks deliberately, so this and
  `itemPickRate` are read against the same base -- a per-pick rate would
  divide by a number that is itself a policy decision. The pair is the point:
  `itemPickRate` says "when this is on the table, how often is it taken",
  this says "how often does it end up on anybody". A high pick rate with a
  zero equip rate is a trap item -- it looks like the best thing in the offer
  and is never worth wearing -- and neither number can show that alone.

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

`enemyAbilityIds` is the odd one out and is **not** a "never" list: it names
which of the ids in `skillsNeverUsed` are an enemy's rather than a player's.
Enemy abilities are authored in `skills.json` like any other skill (a
monster's skill obeys identical rules; `RawEnemyAbility.skillId` is what ties
it to its owner), and `FightRunner` only ever traces the PLAYER's commands —
so every enemy ability appears in `skillsNeverUsed` on every batch that has
ever run. That is noise, and it was burying the player-side gaps that are the
actual finding. `bot_report.py` splits the list against this one.

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

**`determinism`** — a sampled share of the batch's runs is executed twice
back to back (this is what plan §1 calls "a test the bot gets for free").

- `checked`: number of runs actually verified this way. Since
  `-botReplayShare` (default 0.1) the replay is **sampled**, so this is a
  fraction of `sum(cells[].runs)` rather than equal to it — what the check
  looks for is something unseeded leaking into the loop, which is a property
  of the machinery rather than of a seed, so it leaks into every run and one
  run in ten finds it as surely as ten in ten. The sample is every Nth run,
  not a random tenth, so "this batch found no mismatch" stays reproducible.
  `-botReplayShare 1` restores replaying everything.
- `mismatches[]`: `(seed, archetype, profile)` triples where the two
  `RunTrace.Hash()` values differed. Reported once per triple, not once per
  differing field — the whole point is "this seed is not reproducible",
  which is the finding regardless of where in the trace it first diverges.

**`cells[].shop`** — the gate-1 shop numbers (`PLAN_SHOP.md` §7.3). Every
share here is per VISIT unless it says otherwise, and every "per section"
map is keyed `gear` / `books` / `relics` in `ShopStock`'s own order.

- `visits`: shop rooms entered by this cell's runs. A cell with none reports
  `visits: 0` and `null` for the numbers a visit would have produced — which
  is what a `-ShopPolicy Never` batch looks like, and a block of zeroes there
  would read as "every visit bought nothing" rather than "no visits".
- `arrivalGold`: p10 / p25 / median / p75 of `goldOnArrival` at shop nodes.
  **This is what the shop is priced against** (§7.1 point 1), not §2a's
  medians over won fights.
- `belowCheapestShare`: visits arriving with less gold than the cheapest card
  on the shelf. Nothing is sold on arrival, so "cheapest card" and "cheapest
  unsold card" are the same number at the moment this is measured.
- `goldForgone`: the median **won**-fight `payoutGold` in each step band
  (1-8 / 9-16 / 17-24 / 25-40), measured inside the same cell. What taking a
  shop node cost, in the currency the shop charges. `null` for a band the
  cell never won a fight in.
- `purchasesPerVisit`, `zeroPurchaseShare`: cards bought per visit, and the
  share of visits that bought none.
- `affordableShareBySection`: of the cards actually SHOWN in a section, the
  share priced at or below `goldOnArrival`. `null` for a section that showed
  no cards at all — the book shelf rolls `NO OFFER` until gate 3, and a zero
  there would read as "never affordable".
- `rerollsPerVisitBySection`, `rerollThenNoPurchaseShare`: how often a
  section is rerolled, and the share of visits that paid to look again at a
  section and then bought nothing in it — §7.1 point 2's *reroll or walk*
  telemetry. Counted per VISIT, not per reroll: a section rerolled twice and
  then bought from is one satisfied decision.
- `spendShareBySection`: of the gold that left the purse on CARDS, the share
  that went to each section. `null` throughout when nothing was bought.
- `goldOnLeaveMedian`: what was still in the purse walking out.
- `goldAtDeathMedian`: the last room's `goldOnArrival`. The run snapshot is
  gone by the time anything could ask — `RunManager.EndRun` replaces it — so
  this is the closest honest reading: gold unspent at the moment it stopped
  mattering.
- `arrivalGoldByStep`: p10 / p25 / median of `goldOnArrival` at steps 4, 8,
  12, 16, 24, 32 and 40, over EVERY room rather than only shop nodes, plus
  `n` (rooms measured). **This replaces §2a's cumulative-won-gold table.**
  `null` for a step nobody in the cell reached — "never got there" and "got
  there broke" are different findings.

**`cells[].spellAcquisition`** — gate 3's own exit numbers (`PLAN_SHOP.md`
§7.3, Phase D 1-3): whether the acquisition loop is putting first spells on
fielded characters at the depths gate 3 asks about, computed from
`RoomTrace.LearnedSpellCountAfterRoom` / `.UnassignedSpellBookCountAfterRoom`
(both bookOnly-inert during Phase A — this measures ACQUISITION, not combat
impact, which is the point of shipping the loop before the flip).

- `learnedFirstSpellByStep`: keyed `"8"` / `"16"` / `"24"` / `"32"` (one
  entry per boss). Each value is `null` when no run in the cell reached that
  step (**reached** means the run's own depth got there, not merely "has a
  room recorded before this step" — a run that died at step 5 still has a
  step-4 room, and counting it toward "reached step 8" would read as the
  depth-of-8 share reading as depth-of-4), otherwise
  `{ "share": <n>, "n": <reached> }`: `share` is the fraction of runs that
  reached the step whose deepest room at or before it already shows
  `LearnedSpellCountAfterRoom > 0`.
- `slotsFilledPerLegMean`: mean, across the cell's runs, of that run's final
  `LearnedSpellCountAfterRoom` divided by `depth / 8` legs (floored at one
  leg, so a run that died on step 3 is not divided by a fraction). `null`
  when the cell has no runs with a recorded room.
- `depthZeroBooksAtLeg2` / `depthSomeBooksAtLeg2`: median `DeathStep`/cap
  depth of runs split by whether, at leg 2 (step 8), the deepest room
  reached by then shows zero or at least one book ACQUIRED — learned or
  still unassigned either counts, since a book sitting unassigned still
  says the loop found something. `null` for whichever side has no runs; a
  run that recorded no room by step 8 counts toward neither.
- `shopVisitsShowingUnaffordableBookShare`: of every shop visit in the cell,
  the share whose shelf showed at least one `Book` card priced above
  `goldOnArrival` — the shelf's own roll already excludes a book every
  fielded character knows, so a shown-but-unaffordable book is the honest
  "wanted it, could not pay" reading without replaying per-character
  eligibility here. `null` when the cell had no shop visits.

**`shopVsNoShop`** — the matched-seed comparison (§7.1 point 1), or `null`.

Present only when the runs come from both `-ShopPolicy` modes: one batch dir
holding shards of each, or two batch dirs merged with
`bot_merge.py <a> --compare <b>`. Runs are paired by
`(seed, archetype, profile)`, never by position — a run present in one mode
and missing from the other would otherwise shift every comparison after it.

The **cells are computed over the first batch's mode only**. Every cell
metric is a claim about one mode, and pooling two into one median answers a
question nobody asked; `batch.shopPolicy` names which mode the cells
describe, and `batch.shopPolicies` lists every mode present.

- `pairs`: seeds present in both modes.
- `pairsWithAShopVisit`: of those, the ones where the shop-taking side
  actually entered a shop. A pair with no visit is the same run twice and
  counts in neither numerator nor denominator of the survival share.
- `medianDepth`: per mode, and their difference. Negative `delta` means
  taking shops cost depth.
- `survivalToNextBossShare`: per mode, the share of pairs whose run went on
  to WIN a boss fight at a step later than the first shop visit's. Both sides
  are measured from the same step — the shop-taking side's visit — so the
  comparison is against the same point in the run.
- `depthVariance`: population variance of depth per mode. A shop that does
  not move the median but widens the spread is a shop that is swinging runs.

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

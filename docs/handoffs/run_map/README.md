# Handover: run map redesign

State at `2c197bf` on branch `claude/sweet-goodall-g4yi0n`. **Planning only;
nothing is built.** No game code, scene or content has changed. Written by a
cloud session that had no Unity, so no Unity gate has run.

## Where things are

| What | Where |
|---|---|
| The plan (single source) | `docs/PLAN_RUN_MAP.md` |
| Advisory summary with a map mock | `advisory.html` here, or https://claude.ai/artifact/Fe1AMzHFEjmjSmYYjPKgvL |
| Games comparison + the five P0 storyboards | `storyboards.html` here, or https://claude.ai/artifact/AWhthzsNhso4dzaEcjBTSx |
| Storyboard generator | `storyboards/gen.py` (writes the frames), `storyboards/page.py` (builds the page); run `python3 gen.py frames.svgs` then `python3 page.py out.html` in that folder |
| Owner rules the plan answers to | `docs/DIRECTION_BIBLE.md` §5–7, `docs/RUN_MAP_DECISION_SPEC.md` §2–3 |

The two `.html` files open in any browser. The claude.ai links are private to
the owner.

## The design in one paragraph

A realm is a road down to its boss, drawn top to bottom. The spine is always 8
steps, so balance and the `step` difficulty clock stay as they are. Optional
pockets branch off and rejoin, cost 1–2 extra rooms, and hold a prize the spine
lacks. Walking past a pocket heals 8%. HP refills only after the boss. Fight
nodes preview their real enemies, a strength from 1 to 3, and one reward chip.
Elites are optional "anchors" tied to the boss by a painted root: beat one on
the map and it's gone, leave it alive and it fights beside the boss (max 2,
because the enemy stage has 3 slots, `FightHudSpec.cs:51`). Mob, elite, event
and shop stay; rest and treasure stay; Cache (bank gold) is an optional
addition.

## How it got here

1. Plan v1 → two review rounds. Each round had a Sonnet `reader` with repo
   context and a blind Sonnet `reader` who got only the plan text.
2. Then a challenge against Slay the Spire, Hades, Shadow of Mordor, Balatro,
   FTL, Into the Breach, Darkest Dungeon II, Monster Train and Loop Hero. That
   changed three things: anchors join the boss (instead of abstract wards), the
   bypass heal, and a map time budget. Details: `PLAN_RUN_MAP.md` §10 and
   `storyboards.html`.

Decisions the reviews settled, so don't reopen them without a reason:

- **The full heal after each boss stays.** It fixes a measured attrition
  complaint (`RunManager.cs:412-437`).
- **The preview reuses the Fight stream roll.** It calls `EncounterRoll.Roll`
  on `RngStreams.Open(Fight, step, nodeId)`, not a new stream, so it can't
  disagree with the fight.
- **The generator reads no content** (`RunSnapshot.cs:27-33`).
- **The save goes through the SaveData ladder,** v8 → v9.

## Waiting on the owner (plan §9)

1. Accept "a graph in a painted place". It sits close to the ruled-out "dens in
   a painted place" (`DIRECTION_BIBLE.md:57`); judge it on the storyboards.
2. Top-to-bottom or left-to-right.
3. Optional elites that join the boss if left alive.
4. Cache: build or drop.
5. Endless descent stays.

**Do not start P1 until these are answered.** The open risk: the greybox frames
can't pass the spec's surface test ("a strange realm full of monsters, not a
board game"). That test needs painted art and monster portraits.

## Next steps once signed

1. **Art brief v2:** vertical, found/eased, medallions including shop, cache
   and anchor, the root stroke. It replaces `docs/RUN_MAP_ART_BRIEF.md`.
2. **P0 EV sheet:** pocket value at full HP and at 40% HP, plus the floor-1
   exp/loot budget against today.
3. **P1:** brief an `implementer` with `RealmMap`, `RealmLayout`,
   `RealmMapRules` and the 10k-seed fuzz test, all pure Domain. The phase
   table, owners and gates are in plan §8. P2 is `senior` with
   `Escalation: cross-layer`.

## Unchecked facts the next session must verify

- Whether `IsElite` scaling is per roster or per enemy, and whether a boss
  roster accepts extra enemies (plan §6). This decides whether P5 needs
  `Escalation: architecture`.
- The bot's four policies only see next-hop nodes (`IRunPolicy.cs:19`). P3
  must add a path-aware `MapView` before the pocket-rate gate means anything.

## Routing note

`tools/githooks/route_agents.py` refuses Haiku. The owner asked for "Haiku for
reading" in chat; under the hook today, `reader` runs on Sonnet 5.5. Raise it
with the owner if that should change.

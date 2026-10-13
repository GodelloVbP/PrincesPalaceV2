# Handover: run map redesign — execution (for the orchestrator)

The plan is final at rev 4: `docs/PLAN_RUN_MAP.md`. Your job is to execute
it, phase by phase, per its §8. Nothing is built yet.

## Where things are

Everything is on local `main`. Local `main` has about 293 commits that were
never pushed to GitHub, whose `main` is still `2565b5a`. Rev 1 was written by
a cloud session against that old tree; revs 2–4 were checked against local
`main` at `1914a617`. Work on local `main` and don't base anything on
`origin/main`.

| What | Where (repo root) |
|---|---|
| The plan, rev 4 (single source) | `docs/PLAN_RUN_MAP.md` |
| Rev-3 reviews (applied in rev 4, kept for the record) | `docs/handoffs/run_map/REVIEW_CONTEXT.md`, `REVIEW_BLIND.md` |
| Rev-1 storyboards and advisory (out of date) | `docs/handoffs/run_map/storyboards.html`, `advisory.html`, `storyboards/` |
| Owner rules the plan answers to | `docs/DIRECTION_BIBLE.md` §5–7, `docs/RUN_MAP_DECISION_SPEC.md` §2–3 |

## What to do

1. **P0 first:** the EV sheet (plan §8) sets the literals that P2 needs:
   `T1`/`T2`, the strength multipliers, pocket and skip-heal value. Show it to
   the owner. Write art brief v2. Redraw the storyboards only if the owner
   asks.
2. **P1 → P2 → P3** can run now. They don't depend on the open decisions.
   P1 goes to `implementer`, P2 to `senior` with `Escalation: architecture`,
   P3 to `implementer` then `verifier`. The gates are in §8.
3. **P4 waits** on owner decisions §9.1 (screen presentation) and §9.2
   (orientation). **P7 waits** on §9.3 (Cache). Ask the owner when you get
   there; until then, keep going on what isn't blocked.
4. **P5** is `senior` with `Escalation: architecture`: the reseat contract,
   per-enemy elite flag, formation and leader rule (plan §6).
5. Before briefing, read `docs/WORKFLOW.md` and `docs/TESTING.md`. Brief by
   behavioural contract, not by file list. Commit only with explicit paths
   (`git commit --only -- <paths>`), because sessions share one tree.

## Do not reopen without a reason

Every owner decision is logged in plan §11. The short list: prizes vary per
node and strength scales them; item and spell-book prizes wait on the item
redesign; strength is frozen per realm; shops sit only inside pockets;
pockets cap at 6 while depth keeps growing; the skip heal is a flat 8%;
anchors are uncapped, guard the boss and reinforce; killing the boss ends the
fight; a migrated save resumes at its last step (never skipping the boss);
the full heal after each boss stays.

## Routing and runtime

Use `CLAUDE.md`'s routing table. Runtime screenshots: ask the owner at the
start of your session and pass the answer into every brief.

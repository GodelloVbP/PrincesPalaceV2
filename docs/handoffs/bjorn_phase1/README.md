# Phase 1 part B (crit authoring) — parked patch

`partB.patch` holds the crit-stat authoring work: `StatType`/`StatBlock` crit
fields, `CritRules` party helpers, the `TalentEffect` comment update,
`RawEnemyEntry`/`EnemyEntryResolver`/`ResolvedEnemy` crit plumbing,
`CombatantKit` wiring, `docs/CONTENT_SCHEMA.md`, and the new
`Tests/EditMode/Content/CritAuthoringTests.cs`.

It is not committed as code because `ContentFreshnessTests` checks
`ContentInputHash` against the generated content tree, and that hash only
updates when Unity runs `-BuildContent`. This session has no Unity Editor,
so it cannot regenerate `Assets/_Project/Resources/Content/` to match the
new `Raw*Entry` fields, and committing the code alone would leave that gate
red.

## Applying in the local Unity session

1. `git apply docs/handoffs/bjorn_phase1/partB.patch`
2. Run `-BuildContent` in Unity.
3. Work through the checklist in `docs/PLAN_BJORN_CONSTELLATIONS.md`,
   section "Phase 1 — Unity-side handoff".
4. `tools/run_tests_parallel.ps1 -Changed -BuildContent`
5. Commit the code and the rebuilt content together.
6. Delete this patch (`docs/handoffs/bjorn_phase1/partB.patch`) and this
   README once the content is committed.

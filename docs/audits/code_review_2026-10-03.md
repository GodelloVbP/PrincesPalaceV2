# Code review, 2026-10-03

`/code-review high 6874cc4e..HEAD` over this session's 68 commits (128 code
files). Not verified; none fixed yet. Ranked most severe first.

1. **Status cue reads live state, not the beat** (`Core/StatusCues.cs:65`).
   `Observe` diffs `combatant.Statuses` (final session state), so the ward or
   regen chime plays on the first beat of a batch rather than the beat that
   applies the status, while badges now paint per beat. Statuses present at
   first sight also chime (relic wards at fight start). Fix: diff
   `beat.Snapshot[c].Statuses`.
2. **Multi-packet accent replays the whole impact clip**
   (`Core/FightBeatPlayer.cs:1223`). Frost Flare, Cinderfault and Ashen play
   the same wav twice 50 ms apart: a smeared double transient. Needs an
   authored short accent, or no replay.
3. **Every beat snapshot builds HUD status rows**
   (`Domain/Combat/Session/FightSession.Beats.cs:523`). Costs headless bot and
   balance runs; couples Domain to HUD formatting. Record raw statuses, map in
   Core.
4. **Over-art backing refits every frame** (`Core/OverArtEdge.cs:93`). Forces
   HUD canvas rebuilds at 60/s for static text; refit on change only.
5. **`place: above` shrinks on tall targets** (`Core/FightController.SpellVfx.cs`).
   SkyCeiling clamp plus `KeepUnderTheLog` scale Crownfall to about a third on
   the treant; fix in the placement rule, not per spell.
6. **History comments** (CLAUDE.md: history and owner quotes belong in commit
   messages): `FightScreen.cs:2951`, `FightHudModel.cs:761`,
   `MeleeSoundOverrideTests.cs:347`, `FightSkillDetailElementIconTests.cs:138`,
   `DossierLoadoutTests.cs:447`.

Also from captures (`docs/captures/spell_feel/INDEX.md`): Spore Cloud still
shows no cloud around the target on the treant; Boulder Slam's recorded hit
cue (f9) disagrees with the authored f14; Censer and Dark Chains WARN a 0.73 s
residue tail.

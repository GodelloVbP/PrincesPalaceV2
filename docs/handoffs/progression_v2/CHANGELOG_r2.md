# Progression v2, revision 2: change log against revision 1

Written 2026-09-15 for the docs copy. The full revision 2 text is in the
session transcript and must be committed verbatim as PLAN_PROGRESSION_V2.md
by the docs agent from the file the orchestrator hands over.

Corrections:
- Level 3 cost lowered so six average fights clear it (160 vs 174 earned);
  guarantee restated: by the leg-1 boss for a standing character (forced
  rooms pay 189).
- Fury breakpoints recomputed from the kit at each level: 15/20/25 per
  attack reach 50 in 4/3/2 attacks; the third gain bump was dead and is
  replaced by Slam +5 flat at 12 and 24; fury-on-hit bumps removed; level 26
  is now "Fury opens at 50".
- The experience rate comparison was backwards: 4% per room gives a deep
  run 6.0x a leg-5 death, 2.5% gives 3.8x.
- Ability four: run 14 everywhere.
- Second Life is combat power and moves to level 25; 31-40 is identity only.
- Promise reworded: every combat node offers an immediately useful option
  when collected and applied.
- Odette: per-element bumps replaced by all-spell +5%; the level-15 example
  uses the 36-mana pool held at that level (4 casts to 5).
- Shawn: capacity bumps replaced by Shear 3->2 and Battering Ram 6->4 Wool;
  the absorb fallback (Tuck In) is a separate spec with its own measurement.
- Migration replaced by a version-6 reset of track progression (dev saves).
- Bot gates: paired seeds, 30 runs per arm, 95% interval, signals not
  thresholds; deterministic career pins exact levels and remainders;
  knockout and stuck-player trajectories reported separately.
- Skill inventory corrected: five new skills plus Shawn's V4; HealSingle is
  a new effect kind; seven new reward types listed.
- Level 30 is the completion point; 31-40 optional prestige with cap-at-30
  fallback.

Addendum r2.1 (2026-09-15, corrections from phase 1 and phase 2 reports):
- Section 3 career table, run 2 dies leg 3: remainder corrected from 246 of 450 to 321 of 450.
- Section 3 knockout paragraph: downed-at-every-boss trajectory corrected to reach level 5 on run 1 at 576 against 560, and level 30 on run 17 (was 562/run 15).
- Section 5 Second Life bullet rewritten to the code as phase 2 pinned it: whole-party-wipe revive at half max health, one charge per collected node per run, pooled across the fielded squad.
- Section 6 and section 1 contract 4 reworded: pay depends on whether a character was out for the whole fight (half) versus fell during a fight the party won (full), not on victory-vs-downed timing.
- Section 4 table and section 5 Shawn bullet: level 15/26 Wool-absorb nodes replaced by Tuck In, the skill phase 1 selected after the automatic absorb was rejected for Shear starvation.
- Section 5 reward types: noted SignatureAbsorbPerPoint is built but unused by the shipped tracks after phase 1.

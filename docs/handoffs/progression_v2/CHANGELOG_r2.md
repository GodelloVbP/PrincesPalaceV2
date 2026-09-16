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

Addendum r2.2 (2026-09-15, phase 5 step 0 -- the ward retune):
- Section 5's three ward bullets rewritten to the PERCENT model phase 4 found
  in the code (StatusEffects.ConsumeWard: `damage - damage * Magnitude / 100`,
  spent by the hit, applied at 999 turns so nothing expires). Revision 2 had
  written them as an absorb pool measured in hit points and drained over two
  of the wearer's turns; no such mechanism exists.
- The numbers moved to match, because the old ones had been priced for the
  pool and read as almost nothing as percentages: Tuck In 2 -> 10 per Wool
  (40% off the next hit at a full four Wool, against 8%); Shawn's level-26
  SkillPowerDelta 1 -> 5, so 15% per Wool and 60% at four; Bulwark 30 -> 50.
- Prism Ward becomes a FLAT 40 and drops its spell-attack term. A percentage
  that scales with a stat which routinely exceeds 100 would ward for more than
  the hit. Mend is untouched -- 20 plus spell attack, in hit points.
- Status line bumped to Revision 2.2.
- Whether wards should become absorb pools at all is the owner's call and is
  filed as AUDIT #152 rather than decided here.

Addendum r2.3 (2026-09-16, the owner's call on AUDIT #152 -- a ward IS a shield):
- The ward MODEL is replaced, not retuned. StatusEffectType.Shielded's Magnitude
  is a pool of shield points: damage comes off the pool first and the remainder
  off health, a hit bigger than the pool carries the rest through, and a hit
  smaller than it leaves the pool standing with less in it. This is what
  revision 2 described all along and what the code had never had.
- Section 5's ward bullets are rewritten to it, and the full ward rules now sit
  in one place under Bulwark: the absorb order (after dodge and defences,
  before health, before Wool), one ward per character with the bigger pool
  winning and the smaller refused out loud, and a real duration of two of the
  wearer's own turns.
- The numbers are shield points, sized against phase 1's measured leg-2 enemy
  hits of 19-23: Brace 60, Fleece Ward 50, Bulwark 30% of the caster's max
  health (78 on Bjorn's authored 260), Prism Ward 20 plus her spell attack,
  Tuck In 5 a Wool capped at four (20), with level 26's SkillPowerDelta cut
  from +5 to +3 so the ceiling reads 32.
- Prism Ward's spell-attack term comes BACK. r2.2 cut it because a percentage
  scaling past 100 is an immunity; a 110-point pool is just a big shield.
- The Golden Fleece is reinterpreted rather than retuned: "wards never expire"
  meant "wards never break" while a ward was one hit's worth of percentage, and
  against a pool that is literal immunity. It is the clock that is permanent
  now; the pool drains like anybody else's. The node's own text ("They last the
  fight") already read that way.
- WardReductionPercent keeps its effect id and becomes "+N% shield amount", a
  multiplier over whatever the skill row authored, rather than a rival figure
  the larger of which won. Two rows had to start authoring a size at all --
  fleece_ward and placeholder_brawler_ward carried only their cost, because the
  talent used to BE the ward.
- Status line bumped to Revision 2.3.

Addendum r2.4 (2026-09-16, the owner's four calls on the shield model as built):
- WARDS STACK. A new ward is a new entry, the shield total is the sum of the
  live ones, and nothing replaces, refreshes or refuses anything. The
  one-ward-per-character rule r2.3 shipped lasted hours: it made The Flock's
  half-strength share bounce off anybody already covered and turned every free
  relic ward into a coin flip against what the player had spent a turn on.
- Damage drains the entry that EXPIRES SOONEST first, then the next; ties break
  by age, oldest first; an entry that never expires sorts last. Soonest-first
  is the only order that does not waste shield.
- The default duration is ONE of the wearer's own turns, not two
  (FightTuning.DefaultWardTurns). None of the five ward skills authors a
  `wardTurns`, so all five take it. The three relic wards keep their authored
  whole-fight duration. One rather than two is what keeps stacking a decision
  instead of an accumulator.
- The Golden Fleece belongs to the CASTER. Expiry is skipped for ward entries
  whose source holds the capstone, read live at every tick rather than baked
  into the duration when the ward went up -- so a Flock share Shawn spreads
  onto an ally never expires, and a ward Odette puts on Shawn does.
- Brace (placeholder_brawler_ward) is 20% of the caster's max health instead of
  a flat 60 -- 52 on Bjorn's authored 260 -- and gains a cooldown of 2, which
  is the value that means "cast on turn N, refused on N+1, allowed on N+2".
  Bulwark and Fleece Ward are unchanged.
- Section 5's ward-rules block is rewritten around all of the above, and the
  consequence the one-turn clock has for Shatter and for the badge is recorded
  there and filed as AUDIT #153.
- Status line bumped to Revision 2.4.

# Plan: Bjorn's three constellations (Sentinel, Einherjar, Juggernaut)

Status: DESIGN + PLAN, all decisions closed 2026-09-28. No code yet. Next
step: Phase 1 (crit system).
Source: owner design session, 2026-09-28. Every number below is a FIRST
VALUE for the balance bot (`tools/bot.ps1`), not a tuned one.

Three lines, per `CLAUDE.md` conventions:

- **Change:** replace Bjorn's placeholder kit/talent with three authored
  constellations on the existing 21-slot skeleton, each with its own Fury
  engine; add a game-wide critical-hit system first.
- **Don't touch:** the skeleton shape, the ember economy (cap 30, gates 9/20),
  Shawn's content, the engine-exclusivity rule. They already fit this design.
- **Done when:** all three constellations are authored, rendered in their own
  silhouettes, fight-tested by the bot, and the old flat Fury gains are gone.

---

## 0. What already exists (no new model needed)

Checked against the tree on 2026-09-28. The owner's shape is the shape the
talent system already has:

| Owner's words | Existing system |
|---|---|
| Root / "engine" | Slot 0 (`TalentSkeleton`), costs 0 embers, exclusive across the three paths (`TalentPage.Refusal.AllegianceSworn`). "The engine is exclusive, the tree is not": points can still go into the other two paths. |
| Three bottom trees | Slots 1-9, a 3x3 grid; left branch = 1/4/7, centre = 2/5/8, right = 3/6/9. Cost 1/2/3 by tier. |
| Convergence | Slot 10, free, gated at 9 embers spent in that path. |
| Three top trees | Slots 11-19; left = 11/14/17, centre = 12/15/18, right = 13/16/19. Cost 2/3/4 by tier. |
| Ultimate | Slot 20 (capstone), free, gated at 20 spent in that path. |
| Point budget | `ContentDatabase.EmberSpendCap = 30`, so one capstone per character. 20 in one path + 10 elsewhere reaches a second convergence but never a second capstone. |

So "the default engine can be turned on without an ember" already holds:
the root is free. What is NOT there yet:

1. Critical hits: none anywhere (`CombatMath.cs:227`, `TalentEffect.cs:152`).
2. Per-character silhouettes: `ConstellationLayout.Plots` is indexed by path
   only (`BlackRam, FragileLamb, Unwritten`), so Bjorn would draw in Shawn's
   shapes.
3. Fury is earned from flat pool numbers (`pools.json` `fury`:
   `gainOnAttack 15`, `gainOnDamageTaken 10`, `decayPerIdleTurn 10`), not
   from an engine.
4. The combat mechanics listed in section 5.

---

## 1. Rules shared by all three

- **One Fury rule: measured against Bjorn, never against the enemy.** Bosses
  and trash pay the same per hit; nothing starves in a boss fight.
- **Engines replace the pool's flat gains.** `fury` goes to
  `gainOnAttack 0`, `gainOnDamageTaken 0`. With no root chosen, Bjorn earns
  no Fury. The root is free, so the player picks an engine from the start.
- **Decay is per engine** (see each root). The pool's `decayPerIdleTurn`
  stays only as the default for an engine that does not override it.
- **Reward track** (`reward_tracks.json` bear rows): `FuryGainOnAttack`
  (levels 5, 19) contradicts "Juggernaut gets Fury from no other source" and
  double-counts on Einherjar. Owner decision (2026-09-28, second pass): leave
  those two rows EMPTY for now rather than rework them; a replacement reward
  is a later decision.
- **Roots grant a skill** (owner, 2026-09-28, second pass): the mechanism
  already exists — a `skills.json` row gets `unlockLevel: 999`, and the root
  talent names it via `grantsSkillId`. Sentinel's root also grants Brace
  (existing `placeholder_brawler_ward`, today `unlockLevel 1`); Juggernaut's
  root also grants Second Wind (existing `second_wind`); Einherjar's root
  also grants a new skill, placeholder name **Hack** (see section 3).
- **Starting kit**: base kit = Slam only, plus the basic attack. Brace,
  Second Wind and Hack come from whichever root the player picks, not from
  the base kit. Einherjar's bottom-left branch upgrades Slam.
  - Rampage: OUT of the base kit, Einherjar convergence.
  - Bulwark (`bear_bulwark`): removed (owner, 2026-09-28, third pass).
  - `placeholder_brawler_ward_root` (the one current bear talent): removed.
- **Engine pick is mandatory** (owner, 2026-09-28, third pass): Bjorn cannot
  enter a fight without a chosen root; the Talents screen (or first hub
  visit) forces the choice.

---

## 2. Sentinel (tank) — shield silhouette

Point of the shield at the bottom (root), the boss of the shield at the
convergence, the ultimate on the top edge.

**Root — engine (slot 0).** Fury per hit taken =
`raw incoming damage / Bjorn max HP x 150`, clamped 3-25 per hit. RAW means
before his defenses reduce it, so resistance investment never cuts his
income. Damage to his planted shield or to Shieldwall counts. Decay: pool
default. The root also grants **Brace** (existing `placeholder_brawler_ward`,
today `unlockLevel 1`), via `grantsSkillId` at `unlockLevel: 999`.

### Bottom

| Branch | Tier 1 (1) | Tier 2 (2) | Tier 3 (3) |
|---|---|---|---|
| **Iron Retort** (L: 1/4/7) — defense into damage | His physical hits gain flat damage = 10% of (Defense + Magical Defense above his base stats). Elemental resistances do not count. | 20% | 30% |
| **Hold the Line** (C: 2/5/8) — active party buff | Unlocks the skill: 40 Fury, every ally gains +Defense and +Magical Defense for 2 turns. | 3 turns; Bjorn gains 5 Fury each time an ally is hit while it is active. | Cast also cleanses one debuff from each ally. |
| **Bellow** (R: 3/6/9) | Unlocks Bellow (existing `Provoke` effect), 0 Fury, 3-turn cooldown (owner, 2026-09-28, third pass). | Passive: enemies prefer Bjorn as a target even without Bellow. | Bellow grants 10 Fury per enemy provoked. |

"Above his base stats" (40 / 12 today) means only gear, relics and
talents add damage, so a fresh Bjorn is not a damage dealer by default.

### Convergence — Plant the Shield (slot 10)

Upgrades the root-granted Brace into the planted shield; same button
(owner, 2026-09-28, third pass). Bjorn sets his shield down in front of
himself (needs art: planted sprite + cracked/broken state).

- Shield HP = `2 x (Defense + Magical Defense) + 10% max HP` (about 130 on
  today's Bjorn).
- Takes hits aimed at Bjorn before his health; uses his resistances, so the
  top branches apply to it.
- Lasts until broken or 3 turns; 3-turn wait before it can be placed again.
- On break: shards deal damage to the attacker that broke it.
- Cost: 30 Fury (first value).

### Top

| Branch | Tier 1 (2) | Tier 2 (3) | Tier 3 (4) |
|---|---|---|---|
| **Spellbreaker** (L: 11/14/17) — anti-magic | Reflect 15% of magic damage taken (by him or the shield) back to the caster. | A spell that hits him or the shield silences its caster for 1 turn (3-turn cooldown per enemy). | Reflected damage also gives Fury equal to half of it (subject to the per-hit clamp). |
| **Shield Bash** (C: 12/15/18) — new skill | Unlocks the skill: strike with the planted shield for base damage + 30% of what the shield absorbed this placement, stun 1 turn. Consumes the shield (starts the re-place wait). | Wait after a Bash is 1 turn instead of 3. | Also hits the enemies adjacent to the target. |
| **Thornwall** (R: 13/16/19) — anti-physical | Melee physical hits on him or the shield return 15% of the damage. | The attacker is slowed for 1 turn (existing `Chilled` if it fits, else a new status). | When a physical hit breaks the shield, the attacker is disarmed: -30% attack for 2 turns. |

Shield Bash sits in the TOP tier on purpose: it needs the planted shield,
which only exists from the convergence up.

### Ultimate — Shieldwall (slot 20)

Plant the Shield covers the whole party instead of only Bjorn.

- One pool = 2x the planted shield's HP.
- Absorbs all damage to any ally before their health.
- An area attack drains it once per ally hit (it soaks all of it).
- Bjorn gains Fury from damage it absorbs, capped at 40 Fury per turn.
- Art: one wall across the party (or a shield in front of each ally).

---

## 3. Einherjar (DPS, crit-based) — axe silhouette

Double-bitted axe: root at the foot of the handle, the three bottom
branches climbing the handle side by side, Rampage where handle meets head,
the three top branches across the head (left blade, top spike, right
blade), the ultimate on the blade edge.

Identity: main damage dealer, sturdy-ish but not tanky. Sturdiness comes
from spending Fury on defense (Battle Trance), not from healing. Lifesteal
relics are his sustain; he gets no healing tree.

**Root — engine (slot 0).** Fury per damaging action =
`damage dealt / Bjorn's Attack x 10`, clamped 5-30. Once per action; an
area hit counts its single largest hit, so Rampage cannot refill itself.
Damage is the RAW, pre-mitigation amount — verified in code (2026-09-28):
`FightSession.Ledger.cs` `ApplyAndCountDamage` pays Fury pools on the raw
hit ("the pools hear the blow first"), not the corrected "after mitigation"
this section previously said. Because raw damage scales with Attack in
practice, `damage / Attack x 10` works out to "10 Fury x the hit's damage
multiplier" (basic attack ~10, a 2x Slam ~20, crits more): enemy defense and
Bjorn's own Attack gear do not move that multiplier, but damage bonuses that
are not Attack (Wrath, a crit) do raise it. A crit deals more, so it pays
more with no extra rule. Decay: pool default (idle turns drain). The root
also grants a new skill, placeholder name **Hack**: hits one target twice,
costs no Fury, 3-turn cooldown (owner said 2-3; starting at 3). Each of its
two hits pays the Fury engine separately — the one exception to "once per
action" — so Hack is the Fury builder.

### Bottom

| Branch | Tier 1 (1) | Tier 2 (2) | Tier 3 (3) |
|---|---|---|---|
| **Slam** (L: 1/4/7) — upgrades the base-kit Slam | A Slam crit restores 10 Fury. | Slam at 50+ Fury gets +15% crit chance. | Slam at 100 Fury ignores 30% of the target's defense. |
| **Bloodfire** (C: 2/5/8) — Fury generation | A kill refunds 15 Fury. | His first idle turn each fight does not drain Fury. | Crits give +50% Fury on top of the engine. |
| **Momentum** (R: 3/6/9) — crit chance | Each consecutive turn he deals damage: +5% crit chance per stack, max 5. A turn without dealing damage resets it. A hit taken removes ONE stack (not all: in a party he is hit most turns). | +5% crit damage per stack; max 8 stacks. | Hits under 10% of his max HP no longer remove a stack. |

### Convergence — Rampage (slot 10)

The existing `rampage` skill moves here unchanged: 70% of a Slam against
every enemy; under 50 Fury a normal sweep, 50+ spends half for 2x, 100
spends all for 4x.

### Top

| Branch | Tier 1 (2) | Tier 2 (3) | Tier 3 (4) |
|---|---|---|---|
| **Headsplitter** (L: 11/14/17) — finisher | Unlocks the skill: spends all Fury (min 30). Damage scales with Fury spent and the target's missing health. A kill refunds half the Fury spent. | +25% crit chance against targets under 30% health. | A kill fills Momentum to max stacks. |
| **Berserk** (C: 12/15/18) — transform (reuse the existing `Transformation`/`TransformGrant` seam from Black Ram Mode) | Unlocks the skill: 50 Fury to enter. 50% of his Defense and Magical Defense converts into Attack. Fury no longer drains when idle, instead drains 10 per turn; the form ends at 0 Fury. Cannot use Bellow, Hold the Line or shields while transformed. Needs a transformed battle sprite. | +10% crit chance while transformed. | Each kill while transformed adds 20 Fury. |
| **Battle Trance** (R: 13/16/19) — Fury soaks damage | Above 50 Fury, 20% of incoming damage is paid with Fury instead of health (1 Fury soaks 1% of his max HP). | 30%; doubled while in Berserk. | When soaking empties his Fury, he gains `Protect` for 1 turn. |

Battle Trance is the sturdy-not-tanky piece and a deliberate tension:
every Fury point spent soaking is one not spent on Rampage or Headsplitter.

### Ultimate — Twin Rampage (slot 20)

Not a separate button: casting Rampage at 100 Fury while it is off
cooldown triggers it on top.

- First sweep: normal 100-Fury Rampage (spends all, 4x).
- Second sweep: 1x, plus a 1-turn stun on every enemy it hits. Bosses are
  stunned 1 turn unless that boss is authored stun-immune.
- The first sweep's Fury gain does not refill the bar before the second.
- 5-turn cooldown on the empowered version only; during it a 100-Fury
  Rampage is a normal Rampage.

Requires the crit system (section 5, phase 1) for Slam, Bloodfire,
Momentum, Headsplitter and Berserk tiers.

---

## 4. Juggernaut (low-health bruiser) — bear silhouette

Silhouette decided (owner, 2026-09-28, third pass): a paw print. Root at
the heel of the main pad, bottom branches inside the pad, convergence at
the top of the pad, the three top branches running up into three toes, the
ultimate as the largest centre toe.

**Root — engine (slot 0).** Fury per turn, with no hit-based income:
`15 x (1 + 3 x t^2)`, `t = clamp((1 - HP%) / 0.75, 0, 1)`. Fury-granting
nodes bought in other trees (Bellow T3, Hold the Line T2, Bloodfire) still
pay — the rule only rules out earning Fury from taking or dealing hits
(owner, 2026-09-28, third pass).

| HP | Fury / turn |
|---|---|
| 100% | 15 |
| 75% | 20 |
| 50% | 35 |
| 25% or less | 60 |

Curved, not linear: modest above half health, steep below. Idle decay is
OFF for this engine; the per-turn income is the whole economy. The root
also grants **Second Wind** (existing `second_wind`), via `grantsSkillId` at
`unlockLevel: 999`. When granted by the Juggernaut root, Second Wind has a
**4-turn cooldown** (owner-approved, self-review 2026-09-28): at 60 free Fury
per turn at low health it would otherwise chain near-full heals every turn.

### Bottom

| Branch | Tier 1 (1) | Tier 2 (2) | Tier 3 (3) |
|---|---|---|---|
| **Thick Blood** (L: 1/4/7) — max HP + regen | +10% max HP. | Regen 2% max HP per turn at full health, rising to 6% at 25% health (same curve shape as the root). | Another +10% max HP; regen ceiling 8%. |
| **Wrath** (C: 2/5/8) — damage from missing health | +0.5% damage per 1% health missing (+37.5% at 25%). | 0.75% | 1.0% (+75% at 25%) |
| **Gorge** (R: 3/6/9) — vampiric skill | Unlocks the skill: heavy physical hit on one target, heals 30% of damage dealt. 30 Fury. | Heals 40%. | +25% damage while he is under 50% health. |

Regen pulling him out of low health is the intended tension: he should
settle near the edge. Regen must not out-heal enemy damage outside the
convergence window, or Wrath is dead.

### Convergence — Unbroken (slot 10)

Skill, 50 Fury: very high regen (15% max HP per turn) for 2 turns and
Unstoppable for 2 turns (cannot be affected by crowd control). It is the
"reset" button after riding low health.

### Top

| Branch | Tier 1 (2) | Tier 2 (3) | Tier 3 (4) |
|---|---|---|---|
| **Unyielding** (L: 11/14/17) — CC passive | When crowd control is attempted on him, he negates it and gains +speed and +25% damage for 2 turns. 4-turn cooldown. | Cooldown 3 turns. | The trigger also grants 20 Fury. |
| **Ignore Pain** (C: 12/15/18) — delayed damage | 20% of every hit is not taken now but spread over the next 3 turns as damage over time. | 30%. | Healing reduces the pending delayed damage before it restores health. |
| **Blood Price** (R: 13/16/19) — control over his own health | When short on Fury, skills can be paid with health instead: 1 Fury = 0.5% max HP. Health payment cannot take him below 1 HP, and self-payment never triggers cheat death. | Health-paid skills deal +15% damage. | Cheat death, once per fight: lethal damage leaves him at 1 HP and fills Fury to 100. Reuses the existing `CheatDeathOncePerFight` `TalentEffect` — no new kind needed. |

Unyielding vs Unstoppable (owner decision): while Unstoppable is active,
Unstoppable blocks the crowd control and Unyielding neither fires nor
starts its cooldown. It stays loaded for after Unstoppable ends.

Blood Price exists because every other branch assumes low health while
three of them push his health back up; it gives the player a deliberate
way down, and the cheat death makes Cursed Blood survivable.

### Ultimate — Cursed Blood (slot 20)

Skill. Cost 50 Fury, once per fight. For 2 turns Bjorn cannot be healed.
Every heal that would land on him — his regen (Thick Blood), Gorge,
**Unbroken's regen included**, an ally's heal, his share of a party heal —
is dealt as damage to every enemy instead, damage type **Void** (exists in
`DamageType`, rarely resisted). ALL healing and regen on Bjorn converts,
with no exceptions (owner, 2026-09-28, third pass): Unbroken stacking with
Cursed Blood is the intended combo Cursed Blood is tuned around, not an
edge case to exclude.

- Converts the EFFECTIVE heal: capped by his missing health at that
  moment. Since he cannot heal during it, his missing health stays put and
  each heal converts in full. A raw-heal version would reward being at
  full health, the opposite of the tree.
- Second Wind becomes a nuke under this (Second Wind is granted by the
  Juggernaut root); the missing-health cap bounds it.
- Ignore Pain T3 vs Cursed Blood (owner-delegated, 2026-09-28): under
  Cursed Blood, heals convert first and do not clear delayed damage —
  Ignore Pain T3's "healing reduces the pending delayed damage" does not
  apply to a heal that Cursed Blood has converted to enemy damage.

---

## 5. Implementation plan

**Routing (owner, 2026-09-28, second pass) — supersedes `CLAUDE.md`'s
routing table for this plan only:** the main session orchestrates on
Opus 5.5; all reading goes to Sonnet `reader`; any implementation Sonnet can
do goes to Sonnet `fixer`; Opus 5.5 medium (`implementer`/`senior`) is used
only when the extra reasoning is actually needed. Triage each phase at
launch against that bar, not against `CLAUDE.md`'s trivial/doable/deep
table. Every phase below is a separate issue with one owner. Gates per
`docs/TESTING.md`; formula tests pin literal values (gotcha 5).

### Phase 1 — Critical-hit system (game-wide, first; owner decision)

- Stats: crit chance, crit damage on `StatBlock`/`StatType` (APPEND enum
  members, never insert; content stores ordinals).
- Roll in `CombatMath` / `DamagePipeline` through the existing RNG seam so
  the bot stays deterministic.
- Beat/popup + combat log line for a crit.
- Sources: gear/relic affix hooks (authoring later).
- Revisit the "cannot be critically hit" note in `TalentEffect.cs:152`.
- Owner decision (2026-09-28, second pass): enemies can crit ONLY when an
  ability is authored to crit, never from a random baseline chance. An
  authored crit is guaranteed, not a percent roll, and `EnemyIntent` (which
  already carries `ExpectedDamage`) shows it in the telegraph — so the
  owner's "no random spikes" rule still holds even though enemies can crit.
- Routing (owner, 2026-09-28, second pass, supersedes `CLAUDE.md`'s table for
  this plan; see section 5 header): `implementer` (Opus 5.5 medium). Gate:
  `run_tests_parallel.ps1 -Changed`, plus `-BuildScenes` / `-BuildContent`
  if touched.

#### Phase 1 — Domain design as built (2026-09-28)

- **Rule** lives in `Domain/Combat/CritRules.cs`: `BaseChancePercent = 5`,
  `BaseDamagePercent = 150` (a crit is x1.5), `MinDamagePercent = 100`.
  `CombatantState.CritChancePercent` / `CritDamagePercent` are the fight
  totals; the constructor starts every player-side combatant at 5% and every
  enemy at 0, damage at 150 for both.
- **Where it applies:** inside `DamagePipeline.AfterDefences` (both
  overloads), right after the dodge roll and before effectiveness, defense,
  variance and the ward — the multiplier rides the OUTGOING amount. A miss
  cannot crit and spends no crit draw. Heals never reach the pipeline, so
  heals never crit. Splash, Shatter, DoT ticks and relic packets with
  `rng: null` (Toll of the Flock) cannot crit either.
- **The roll** is `DamagePipeline.RollCrit` -> `RandomOps.RollPercent` on
  the fight's own `_rng`; `rng == null` (every preview) never rolls. Enemies
  answer 0 from `CritRules.ChanceFor` and never consume a draw.
- **Per hit, not per action:** each target of a sweep rolls its own crit;
  a multi-packet spell rolls ONE crit for the whole cast (same shape as its
  single dodge) via `ResolveDamageInstances`. The elemental on-hit rider
  never crits; Ashen Reckoning's detonation (a returned snapshot) never
  crits.
- **Authored enemy crits:** `EnemyAbility.Crits` (plain swing, legacy
  attack or real skill). `ResolveEnemyAction` arms
  `FightSession._authoredCritActor` for that one action (cleared in a
  `finally` by `AutoResolveEnemyTurns`, before retaliation); every damage
  call site passes `crit: CritCallFor(actor)`. Guaranteed, no draw.
  `EnemyIntent.WillCrit` is set and `ExpectedDamage` (and the per-seat
  table) includes the multiplier. The legacy `skillName/skillPower/
  skillChance` trio has no crit field — frozen legacy authoring.
- **Reporting:** `DamagePipeline.Outcome.IsCrit`; `CombatBeat.Crit` for a
  single-target blow; `BeatTargetResult.Crit` per target of a sweep. Log:
  `FightSession.CritLine` ("A critical hit!") ahead of the damage line;
  sweeps append " (critical)" per target in the summary line.
- **Bot:** `FightAction.PreviewAttackDamage` and `FightAction.PreviewDamage`
  apply `CritRules.ExpectedDamage` (damage x (1 + chance x bonus)); all three
  policies use them. `PreviewSkillPower` itself (the HUD POWER row) stays
  exact. Enemy authored crits reach the bot exactly through
  `EnemyIntent.ExpectedDamage`.
- **Part B — written but held back from the Domain commit** (the next two
  bullets and the `TalentEffect.cs` comment). Every file it touches is in
  `ContentInputHash`, so committing it without a content rebuild turns
  `ContentFreshnessTests` red; it lands atomically with the first
  `-BuildContent` (handoff item 1). Files: `Domain/Stats/StatType.cs`,
  `Domain/Stats/StatBlock.cs`, `Domain/Combat/CritRules.cs` (the two
  `Party*` helpers), `Domain/Combat/TalentEffect.cs` (comment only),
  `Domain/Content/RawEnemyEntry.cs`, `Domain/Content/EnemyEntryResolver.cs`,
  `Domain/Content/ResolvedEnemy.cs`, `Domain/Combat/Session/CombatantKit.cs`,
  `docs/CONTENT_SCHEMA.md`, `Tests/EditMode/Content/CritAuthoringTests.cs`.
- **Stats (part B):** `StatType.CritChance`, `StatType.CritDamage` APPENDED; the
  `StatBlock` fields `critChance` / `critDamage` are BONUS percent points on
  top of the baseline (0 = baseline). `CritRules.PartyChancePercent(stats)`
  / `PartyDamagePercent(stats)` turn effective stats into the combat totals.
  `Scaled` / `ScaledForElite` pass them through unscaled.
- **Authoring (part B):** `RawEnemyEntry.attackCrits` (refused with `attackWeight 0`)
  -> `ResolvedEnemy.AttackCrits`; `RawEnemyAbility.crits` ->
  `EnemyAbilityRef.Crits`. `docs/CONTENT_SCHEMA.md` regenerated with
  `CONTENT_SCHEMA_WRITE=1` (both rows present).
- Existing talents unchanged; Last Stand's spike cap
  (`DamageCapPercentBelowHealth`) still stands in for "cannot be critically
  hit".

#### Phase 1 — Unity-side handoff

Domain is done. Every item names the file, the change and why. Status per
item is marked in place (DONE / NOT DONE).

1. **DONE. Land part B with `-BuildContent` (blocking).** `StatBlock.cs`,
   `TalentEffect.cs` and `Domain/Content/*.cs` are in `ContentInputHash`, so
   with part B applied
   `ContentFreshnessTests.TheInputsAreTheOnesTheTreeWasBuiltFrom` is red
   until the content tree is rebuilt — commit part B and the rebuilt tree
   together. `StatBlock` gained two serialized
   fields and `ResolvedEnemy` / `EnemyAbilityRef` one each; the rebuild
   writes them into every asset and the new stamp. Sync
   `Resources/Content/` back to main (gotcha 1). No `ContentBuilder` mapping
   code should be needed — `EnemyDefinition` stores the resolver's
   `ResolvedEnemy` whole — but confirm a built enemy asset carries
   `AttackCrits` / `Abilities[i].Crits`.
2. **DONE. `Core/FightEncounterAdapter.cs`, party kit builds (~L221 and ~L348,
   beside `ArmorPenetration`):** set
   `state.CritChancePercent = CritRules.PartyChancePercent(stats);` and
   `state.CritDamagePercent = CritRules.PartyDamagePercent(stats);` (part
   B helpers). Without
   it every party member still crits at the 5%/150% baseline (constructor
   default) but a `critChance` / `critDamage` bonus on gear or talents is
   ignored. Use the effective stats (`ContentDatabase.EffectiveStats` on the
   save-backed path) so item `statBonus` crit stats count.
3. **DONE. `Core/FightEncounterAdapter.cs` `EnemyKitFor` (L600, L614, L621):**
   `EnemyAbility.LegacyAttack(FightSession.IntentAttack, 1f,
   source.AttackWeight, crits: source.AttackCrits)`;
   `EnemyAbility.Of(Resolve(skill), reference.Weight, reference.Crits)`; and
   the empty-pool fallback at L621 with `crits: source.AttackCrits`. Without
   this an authored enemy crit resolves to a plain hit in real fights (the
   Domain legacy pool in `CombatantKit` reads `AttackCrits` with part B).
4. **PARTLY DONE. Crit sources (authoring later, hooks only):** gear/relic crit.
   Gear crit flows through item 2; relic crit is hooked (`RelicStat.CritChance`
   / `CritDamage` and `RelicModifierType.CritChanceFlat` / `CritDamageFlat`,
   appended, read at the item-2 lines). NOT DONE: the rolled-affix
   `ModifierEffectType` members and their read. Item
   `statBonus` is a `StatBlock`, so crit stats flow once item 2 lands. A
   relic source needs `RelicStat.CritChance` / `CritDamage` APPENDED and
   `RelicModifiers.Apply` at the item-2 lines; a rolled affix needs
   `ModifierEffectType` members APPENDED plus a read at the same lines.
   Enum rule: append only.
5. **DONE (popup); hit-stop/shake NOT DONE. View — popup and beat (`Core/FightBeatPlayer.cs` ~L1159-1186
   `PopNumber` / single-amount path, `DamagePopup`):** read `CombatBeat.Crit`
   (single target) and `BeatTargetResult.Crit` (sweeps) and play a crit
   popup style (larger, distinct colour, e.g. "36!"); optionally a
   shake / hit-stop floor via the existing `RecordFormHitCue` floor pattern.
   The log line already arrives in `beat.Messages`.
6. **DONE. View — telegraph (`Core/FightController.StageVisuals.cs` ~L571 and
   ~L723, intent badge/tooltip):** show `EnemyIntent.WillCrit` (crit marker
   on the badge, "Critical" in the tooltip). `ExpectedDamage` already
   includes the multiplier.
7. **NOT DONE (optional). Character sheet:** a Crit / Crit Dmg row in
   `Domain/UiKit/SheetStats.cs` / `DossierLayout` reading
   `CritRules.PartyChancePercent(stats)`; a screen-tree change means
   `-BuildScenes`.
8. **DONE. [U] tests:** adapter test (party state gets 5/150 and a stat bonus
   moves it; enemy kit carries `Crits`); PlayMode popup test for a crit
   beat and a sweep with per-target crits; intent badge test for
   `WillCrit`.
9. **DONE (nothing to re-pin). Balance bot:** party damage EV rises ~2.5% from the baseline; re-run
   and re-pin any Core/Bot balance baselines.
10. **DONE. Gate:** `run_tests_parallel.ps1 -Changed -BuildContent` (plus
    `-BuildScenes` if item 5-7 touch a screen tree or `[SerializeField]`).

### Phase 2 — Engine-driven Fury

- Zero the pool's flat gains; move Fury income into the three root
  `TalentEffect`s with the formulas in sections 2-4.
- Per-engine decay (Juggernaut: off).
- Reward-track bear rows left empty (section 1).
- `implementer` (Opus 5.5 medium).

**Status (2026-09-28): the engine code is built** as a Domain seam
(`CombatantState.FuryEngine`, session half `FightSession.FuryEngines.cs`,
tests `FuryEngineTests` `[D]`); see "Phase 2 engines and the Einherjar seams"
under the Phase 4 engine-seams subsection for the exposed state and the exact
wiring. With the engine `None` the flat gains pay exactly as before. What
remains is Unity-side, `-BuildContent`: the three root `TalentEffectType`
members that set the engine, the `ArmEngineSeams` line that reads them,
`pools.json` `fury` `gainOnAttack`/`gainOnDamageTaken` to 0, and the empty
reward-track rows.

### Phase 3 — Per-character constellation silhouettes

- `ConstellationLayout.Plots` keyed by (character, path) instead of path;
  graceful fallback stays the spire.
- Three plots: shield, double-bitted axe, paw print. Background art for
  each.
- Screen tree in `Domain/UiKit/Screens/`, wiring in `ScreenRegistry.cs`;
  `-BuildScenes`; `UiAudit` must pass at all four aspects.
- Routing: `fixer` for the plots data (the `Plots` keying and the three
  layout entries); `implementer` if the layout contract itself changes.

#### Phase 3 — Domain part as built (2026-09-28)

- **Path order for Bjorn (`bear`): path 0 Sentinel, 1 Einherjar,
  2 Juggernaut.** Phase 5 content must author Bjorn's `talents.json`
  columns in this order, or the Sentinel talents draw on the axe.
- `ConstellationLayout` now selects a figure by (character id, path):
  `StarX/StarY(string characterId, int path, int slot)`,
  `PlotId(characterId, path)` (`ram`/`lamb`/`spire` for `sheep`,
  `shield`/`axe`/`paw` for `bear`), `PlotCountFor(characterId)`,
  `PlottedCharacters`. The key is `Character.definitionId`. Anything
  unplotted (unknown id, null, `owl`, out-of-range path) draws the spire.
- The path-only `StarX/StarY(path, slot)` and `PlotCount` are kept and
  delegate to Shawn (`sheep`), so every current caller draws exactly what it
  drew before. Nothing on screen changes until the call sites below move.
- The three plots keep every existing invariant (shared spine ladder, 112px
  separation floor, 130px side clearance, sky bounds, `TreeWidth` still the
  widest figure) and a new one: no edge's lit glow runs through a stone it
  does not join. Tests walk every registered (character, path), so a future
  character's plots are held to them on registration.
- **One deviation from section 3:** the axe's capstone is not "on the blade
  edge". The capstone is a spine slot, and the spine ladder pins it to
  (0, -924). It sits on the head's top edge where both blades' upper edges
  meet; the cutting edges are the strokes 14-17 and 16-19. Putting the
  capstone on a blade would be a layout-contract change (spine off x = 0),
  not a plot.

#### Phase 3 — Unity-side handoff

**The screen bakes star positions for one fixed character.** Verified:
`TalentScreen.Build()` (`Domain/UiKit/Screens/TalentScreen.cs:168`) takes
no character; `BuildPage(path)` (`:548`) and `PositionOf(path, slot)`
(`:578-580`, path-only `StarX/StarY`) place every orb with `Place.At` at
scene-build time, and `BuildEdge` (`:728-816`) bakes each edge's midpoint,
length and rotation from the same positions, plus the spark's start at
`-length/2` (`:802`) and `EdgeLengths` (`:816`). `ScreenRegistry.cs:958-961`
(Editor) bakes those lengths into `TalentEdgeSpark.SetLength` as a
`[SerializeField]`. The character pager switches characters at RUNTIME
(`Core/TalentController.cs:242`, `StepCharacter`), inside one scene. So a
per-character figure cannot be baked: it needs a **runtime reposition** on
every character switch (and on first paint):

1. `Core/TalentController.cs` — add a `Relayout()` called from `Start` and
   from `StepCharacter` (`:242`, next to the `_selectedSlot = -1` reset),
   keyed by `Current?.definitionId`. For each path and slot, set the orb
   rect's `anchoredPosition` to `TreeOriginX + StarX(id, path, slot)`,
   `TreeOriginY + StarY(id, path, slot)`. Orb children (aura, glow, core,
   ring, collar, label, price) are children and follow.
2. Same method, per edge (`edgeParentSlots`/`edgeChildSlots` already map
   each edge): recompute midpoint, length and angle exactly as
   `TalentScreen.BuildEdge` does, and apply to the dim edge rect (position,
   `sizeDelta.x`, `localEulerAngles.z`) and the glow sibling (same), then the
   core's width and `TalentEdgeSpark.SetLength(length)`. Extract the
   midpoint/length/angle arithmetic into `ConstellationLayout` (Domain) and
   have both `BuildEdge` and `Relayout` call it, so build and runtime cannot
   drift — the reason `ConstellationLayout` exists.
3. `Core/TalentController.Motion.cs:61` (`AimPushIn`) — switch
   `StarX(_path, _selectedSlot)` to `StarX(Current?.definitionId, _path,
   _selectedSlot)`.
4. `TalentScreen.cs:579-580` may stay on the path-only API (it is the
   pre-relayout build-time placement, and Shawn's is a fine default for a
   scene opened with nothing loaded) — or take `ShawnId` explicitly. Either
   way the runtime `Relayout` is the source of truth.
5. PlayMode test: switch character sheep -> bear and assert orb 0/10/20
   positions match `StarX/StarY("bear", ...)`, and one edge's length and
   angle match its endpoints; switch back and assert Shawn's.

Gamepad navigation is slot-topology based (`TalentController.cs:~1090`,
DxSlot/Parents), not positional, so it needs no change.

**Unity-side status: items 1-5 DONE.** `TalentController.Relayout()` runs from
`Start` and `StepCharacter` (new `edges` field auto-bound to the dim edge
rects); edge arithmetic is `ConstellationLayout.SegmentBetween`, called by
`BuildEdge` and `Relayout`; `AimPushIn` takes the character overload;
`TalentScreen.PositionOf` names `ShawnId` explicitly; `TalentRelayoutTests`
covers sheep -> bear -> sheep. Per-silhouette background swap NOT done: it
needs the Phase 7 sprites, and no keyed lookup exists to hang it on yet.

**Gate:** `tools/run_tests_parallel.ps1 -Changed -BuildScenes` (a
`[SerializeField]` and the screen's build output move), and `UiAudit` must
pass at all four canvas aspects. The page panels already carry
`AllowOverlap`/`AllowOverflow` with reasons; the audit sees only the
build-time (Shawn) positions, so the runtime figures' fit is what the [D]
invariant tests above guarantee.

**Background art per silhouette** (today one nebula for all,
`TalentScreen.BackgroundKey`, `:52`, built at `:232`): a per-(character,
path) backdrop keyed on `ConstellationLayout.PlotId`, swapped at runtime in
the same `Relayout`/page change. Briefs for Phase 7:
- `shield` — a cold iron-blue nebula with a faint heater-shield glow
  behind the boss; stone-wall dust at the bottom.
- `axe` — a red-gold storm sky, a faint double-bit axe head in the clouds
  above the convergence; embers rising along the haft.
- `paw` — a deep forest-green/brown night sky, a faint paw-print
  impression in the clouds, blood-red mist at the edges.
Each is a sprite under `Art/Backgrounds/` with the same `BackgroundTint`
multiply, so the stones stay the brightest thing on screen.

### Phase 4 — Combat mechanics (mechanics reference, 4a-4h)

Every issue that adds a utility skill also adds its bot valuation for it
(closes review finding 2: non-damage skills otherwise score 0 and Phase 6
cannot evaluate Sentinel or Juggernaut).

Routing 4a-4h: triage at launch; default `fixer` for content-on-existing-
effects, `implementer` for new mechanics. This table is the mechanics
reference; build order (which rows land when) is the vertical-slice plan
below, not top-to-bottom through this table.

| # | Mechanic | Used by | Notes |
|---|---|---|---|
| 4a | Plant the Shield as a Ward + a single shared party Ward pool | Plant the Shield, Shield Bash, Shieldwall | Verified in code (2026-09-28): there is no damage-redirect hook, and `Summon` only adds combatants to the ENEMY side (`TryAddEnemy`) — a planted shield cannot be a party-seat entity. But Wards already absorb last in `DamagePipeline.AfterDefences`, and Fury pools hear damage before Wards do, so "the shield takes the hit and Bjorn still gains Fury" is already how a Ward works. Plant the Shield = a Ward with its own HP formula, a planted visual, and break effects. Shieldwall = ONE Ward pool shared by every ally, which is new work (`WardSpreadsToWholeParty` today gives each ally their own separate pool). Downgraded from "likely `senior`, architecture" to `implementer`. |
| 4b | Route the Regen tick through the existing heal funnel, add a conversion hook | Cursed Blood, Ignore Pain T3 | Verified in code (2026-09-28): heals already share one funnel, `FightSession.HealAndCount` (`Ledger.cs:381`) — skills, potions, relics all pass through it. Unverified: whether the Regen status tick uses it. Scope is narrower than first thought: add the conversion hook in `HealAndCount`, and route the Regen tick through it if it does not already. |
| 4c | Delayed damage pool | Ignore Pain | |
| 4d | Health as skill cost + cheat death | Blood Price | Blood Price T3 reuses the existing `CheatDeathOncePerFight` `TalentEffect` — no new kind needed. |
| 4e | Define "crowd control" as one predicate; block at status application | Unbroken, Unyielding | Verified in code (2026-09-28): crowd control has no single existing definition — Stun and Feared share one gate (`StatusEffects.HasStun`), Rooted uses another (`CombatActions.IsLegalFor`), and Chilled only slows. 4e must first define "crowd control" as one predicate (proposal: Stun, Feared, Rooted, Chilled) and block it at status APPLICATION (the `ApplyStatusTo` seam), which is also where Unyielding's "negated an attempt" trigger fires. |
| 4f | Reflect, silence, thorns, slow, disarm | Spellbreaker, Thornwall | Silence may need a new status. |
| 4g | Momentum stacks, Fury soak, Berserk (reuse `Transformation`), Headsplitter, Twin Rampage | Einherjar | After phase 1. |
| 4h | Hold the Line, Bellow upgrades, Gorge, Unbroken skills | Sentinel, Juggernaut | Mostly content on existing effects. |

#### Phase 4 engine seams — built in cloud, wiring handoff (2026-09-28)

Rows 4b, 4c, 4d and 4e are built as Domain seams, commit `601be17`. Row 4a followed the same way; it is described at the end of this subsection. Each one
is switched per combatant by state on `CombatantState`, and each is off for
everyone until something sets it. Nothing sets any of them yet. The
`TalentEffectType`/`SkillEffect` members that will set them are
content-hashed (`ContentInputHash`), so they land in the Unity session with
`-BuildContent`. The session half of all four is in
`Domain/Combat/Session/FightSession.EngineSeams.cs`. The tests are
`HealConversionTests`, `DelayedDamagePoolTests`, `BloodPriceTests` and
`CrowdControlTests`, all `[D]`.

**Shared: `TurnWindow`** (`Domain/Combat/TurnWindow.cs`). It counts a span
of the holder's own turns. It ages at the holder's turn END
(`TickStatusesAtTurnEnd` → `AgeEngineWindows`), and the end of the turn it
was opened on is skipped, the same rule as the `AtTurnEnd` status family.
So "for 2 turns" means two full turns of the holder, whichever side opened
it. Opening it again never shortens it.

**4b Cursed Blood — `CombatantState.HealConversion`** (`HealConversion.cs`).

- How it works: while `HealConversion.Window` is open,
  `FightSession.HealAndCount` restores nothing. It deals the EFFECTIVE heal
  (the heal capped by missing health) as a typed `Void` hit to every living
  enemy, through `DamagePipeline.AfterDefences` and then `DealDamage`.
  Resistance and wards still apply; there is no variance, dodge or crit. The
  holder gets kill credit only on his own turn.
- It is the first thing the funnel does, so Ignore Pain T3 never sees a heal
  that has been converted.
- Regen ticks, lifesteal and Mending Fleece's ward-break heal were outside
  the funnel. They are now routed into it through `StatusEffects.HealSink`.
  All in-fight heals now convert. Revives and a transformation's temporary
  health are not heals.
- One-liner: `session.OpenCursedBlood(holder, turns)`.
- Wiring:
  1. Append `SkillEffect.CursedBlood` (or a generic `OpenHealConversion`)
     to `SkillEffect.cs`.
  2. Add its case to `FightSession.Skills` resolution, calling
     `OpenCursedBlood(actor, skill.StatusDuration)` (2).
  3. Add the `skills.json` row `bjorn_cursed_blood`: 50 Fury, once per
     fight, `grantsSkillId` from the ultimate talent node (slot 20).
  4. Add a bot valuation for it (Phase 4 intro rule).
- Open presentation point: a converted Regen tick deals its enemy damage
  inside the Regen tick's own beat. The lines are correct, but the view shows
  a heal beat on Bjorn. A dedicated beat is the view session's call.

**4c Ignore Pain — `CombatantState.DelayedDamage`** (`DelayedDamagePool.cs`,
null = off).

- Where it applies (decided): after the ward and every defence, before
  health. It takes effect in `FightSession.LandPacket`, after Kinship and a
  hatched shell, and before the Phoenix Egg's lethal check.
- Only hits are deferred. Status ticks and the pool's own payments go
  through `DealStatusTickPacket` and are never deferred again.
- Deferral is `floor(hit × Percent / 100)`.
- Each hit is its own tranche. A tranche pays `ceil(remaining / turnsLeft)`
  at each of the holder's turn starts (20 over 3 turns is 7, 7, 6).
  Payment happens in `TickStatuses`, after the DoT rows and before Regen,
  with its own beat and line. Payments are damage, so cheat death still
  answers them.
- `HealsReducePool` (T3) makes a heal pay down the pool, oldest tranche
  first, before it restores health. It never does this under Cursed Blood.
- One-liner, set at fight start:
  `actor.DelayedDamage = new DelayedDamagePool(percent, 3, healsReducePool: t3)`.
- Wiring:
  1. Append `TalentEffectType.DelayedDamagePercent` (Magnitude = percent,
     Threshold = turns) and `TalentEffectType.HealReducesDelayedDamage` to
     `TalentEffect.cs`.
  2. Add a fight-start pass (e.g. `ArmEngineSeams(actor)` called from
     `FightSession.Begin` for each party member) that sets the pool from
     `Talents.Best(...)`.
  3. Talent rows: Ignore Pain T1 = 20, T2 = 30, T3 = the flag.

**4d Blood Price — `CombatantState.ShortfallHealthPermille`**
(`BloodPrice.cs`, 0 = off, 5 = 0.5% max HP per point).

- `SkillResolution.CanAfford` is the one affordability check. The menu's
  `Affordable`, every bot, `CanCastToSeat` and the cast refusal all call it.
  It counts the skill's own health cost plus the shortfall against a single
  1 HP floor: a cast that would go below it is refused, not clamped.
- Rounding: `ceil(shortfall × maxHP × permille / 1000)`.
- `CastCore` charges the pool what it holds and writes the rest straight to
  health. That write never reaches `ApplyDamageDetailed`, so cheat death,
  wards, deferral and the ledger never see it. Cheat death stays loaded.
- One-liner, set at fight start: `actor.ShortfallHealthPermille = 5`.
- Wiring:
  1. Append `TalentEffectType.ShortfallPaidInHealthPermille` (Magnitude 5).
  2. Set the field from it in the same `ArmEngineSeams` pass.
  3. Blood Price T3 reuses `CheatDeathOncePerFight`; its "fills Fury to 100"
     half is new work in `CombatMath.ApplyDamageDetailed`'s cheat-death
     branch (session side).
- Not built:
  - T2 "health-paid skills deal +15%". This needs the cast to remember that
    it was blood-paid. `CastCore` has `bloodPaid` in scope, which is the
    place to hook it.
  - The HUD cost label still shows only the Fury cost. Showing the health
    price when short is a view decision.

**4e Crowd control — `CrowdControl.IsCrowdControl` and
`CombatantState.CrowdControl` (`CrowdControlGuard`)** (`CrowdControl.cs`).

- The CC statuses are Stun, Feared, Rooted and Chilled.
- The block is in `FightSession.RecordStatus`, which every status reaches:
  `ApplyStatusTo`, `ApplyChilled`'s direct callers, and Court of Whispers'
  fear, which is now rerouted through it. It runs after hard-control
  recovery.
- `ApplyStatusTo` now returns whether the status landed. The four call sites
  that announced a status unconditionally now announce it only when it
  landed.
- Unstoppable blocks the attempt first, so Unyielding neither fires nor
  starts its cooldown.
- Unyielding negates the attempt. It then:
  - opens `UnyieldingSurge`, which gives +`SpeedPercent` speed under the
    speed-buff key `"Unyielding"` and +`DamagePercent` through
    `AttackBonusFor`;
  - starts `UnyieldingCooldown`;
  - adds `FuryGain` to the primary pool.
- One-liners:
  - `session.OpenUnstoppable(holder, 2)` from Unbroken's resolution.
  - At fight start:
    `actor.CrowdControl.Unyielding = new UnyieldingRule(cooldownTurns: 4 or 3, speedPercent: 20, damagePercent: 25, furyGain: 0 or 20)`.
- The speed percent is a placeholder (20). The plan says "+speed" with no
  number.
- Wiring:
  1. Append `SkillEffect.Unbroken` (it opens `Regen` 15% for 2 turns
     through `ApplyStatusTo`, then calls `OpenUnstoppable`, 2 turns).
  2. Append `TalentEffectType.UnyieldingCooldownTurns` (T1 = 4, T2 = 3)
     and `TalentEffectType.UnyieldingFuryGain` (T3 = 20).
  3. Set the rule in `ArmEngineSeams`.
  4. Add a bot valuation for Unbroken.
- Enemy-roster track reminder (review finding 10): CC-applying enemies are
  what make 4e testable in a real fight.

**4a Plant the Shield / Shieldwall — `CombatantState.PlantedShield`**
(`PlantedShield.cs`; session half `FightSession.PlantedShield.cs`; tests
`PlantedShieldTests`, `[D]`).

- The model: the planted shield IS a ward. `PlantShield` raises one
  `Shielded` entry through `RaiseWard` (sourced to the holder, turns =
  `LifetimeTurns`) and `PlantedShield.Ward` keeps a handle on that entry.
  The badge, drain order, Shatter and HUD see an ordinary ward. No parallel
  shield type.
- Shield HP = `2 x (PhysicalDefense + MagicalDefense) + floor(MaxHealth / 10)`,
  read live at placement. A negative defence sum reads as 0 and the minimum
  is 1. Today's Bjorn (40 / 12 / 260) gets 130.
- Lifetime: `Lifetime` (a `TurnWindow`), 3 of the holder's turns, the same
  clock as every window. The ward entry expires on the same turn end, and
  `AgePlantedShield` lifts it first. Re-place wait: `ReplaceWait` opens when
  the placement ends (broken, expired, bashed or removed by something
  else) for `ReplaceWaitTurns` (3), or for `BashWaitTurns` (1) after a Bash
  when `ShortWaitAfterBash` is set. `CanPlace` is the menu's question;
  `PlantShield` refuses when it is false.
- `AbsorbedThisPlacement`: everything this placement soaked, for the holder
  and for every ally the wall covered. Reset at each placement.
  `BashPlantedShield(holder)` consumes the shield and returns it. Shield Bash
  deals `base + PlantedShield.BashBonus(absorbed, 30)` (floored).
- Break: when `BreakShards` is on, `BreakShardDamage` =
  `max(1, floor(PlacedPoints x BreakShardPercent / 100))` (default 20%,
  decided 2026-09-28: 26 for today's 130 shield, 52 for a 260 wall) is dealt
  as Physical to the attacker whose hit emptied the shield. Only a break by a hit counts. An entry that
  something else removed ends quietly.
- The ward step learns who struck and with what.
  `DamagePipeline.resolveWard` is now a `WardResolver(target, damage,
  attacker, type)`. `FightSession.ResolveWard` runs the status wards
  (`ResolveStatusWards`, the old body), then the planted-shield bookkeeping,
  then Shieldwall.
- Fury, corrected finding: the pools do NOT hear a hit before the wards.
  They hear what `DealDamage` is handed, which is the post-ward remainder,
  so a hit a ward eats whole has always paid the victim's pool nothing
  (`NoteDamageForPools` returns on 0). The planted shield fixes this for
  itself only: when it absorbed and nothing was left, the holder's pools
  hear the blow once. Ordinary wards are unchanged, which is pinned by
  `AnOrdinaryWard_ThatEatsTheWholeHit_StillPaysNoFury`.
- Shieldwall: set `CoversParty` before placing. `PlantShield` then raises
  ONE entry of `2 x` the planted HP on the holder. Any other living ally's
  hit drains it after that ally's own wards (soonest-to-lapse first, the
  rule every ward follows) and before their health. An area attack reaches
  it once per ally hit, each in full. The holder's own hits drain it as his
  own ward.
  - Coverage is decided per hit from the side's living members. An ally who
    joins or is revived mid-fight is covered from their first hit, and a
    fallen ally is simply never asked about.
  - If the holder is dead, the wall ends the next time anyone asks for it.
- Shieldwall Fury: per ally hit the wall absorbs,
  `clamp(floor(absorbed x 150 / holder MaxHealth), 3, 25)` (the root
  engine's formula, `FuryForAbsorbedHit`), then capped at
  `PartyFuryCapPerTurn` (40). The cap resets at the holder's turn end. This
  is the only place the root's per-hit clamp exists until Phase 2 builds it
  for his own hits; Phase 2 should call the same helper.
- Reactive hooks (all off): `ThornsPercent` (Thornwall), `ReflectMagicPercent`
  (Spellbreaker; "magic" means any type other than Physical, reflected as
  that type), and `SilenceCasterOnSpellHit`, which raises
  `FightSession.SpellHitShieldHolder(holder, caster)` once per caster per
  action.
  - "Him or the shield": each hook applies to the whole post-defence hit on
    the holder, whether or not a shield is down, and to the share of an
    ally's hit that his wall ate.
  - Answers are queued during the swing and paid by `SettleShieldReactions`
    at the two post-action seams, beside the Thorned retaliation. They are
    never dealt mid-swing, so an AOE never keeps swinging with a dead
    attacker.
  - Thorns answer only when the action was a physical MOVE
    (`CombatActions.IsPhysicalMove`, the nearest thing to "melee" here). A
    physical-typed arrow or spell is not returned. Decided (2026-09-28):
    "melee" = physical move.
  - Each answer is a hit through `DamagePipeline` (no dodge, crit or
    variance) and then `DealDamage`, with the holder as the source and
    `actorActed: false`. Kill credit counts only on the holder's own turn.
    An answer is never itself answered.
- Wiring (Unity session, `-BuildContent`):
  1. Append `SkillEffect.PlantShield` (the cast calls
     `session.PlantShield(actor)`; Brace's button is upgraded by the
     convergence node) and `SkillEffect.ShieldBash` (calls
     `BashPlantedShield`, deals `base + BashBonus(absorbed, 30)`, stuns 1;
     T3 also hits the adjacent enemies). Add both to
     `FightSession.Skills` resolution and gate the menu on `CanPlace` or
     `IsPlaced`.
  2. Append `TalentEffectType` members: `PlantedShieldBreakShards`
     (a flag: sets `PlantedShield.BreakShards`; the value is decided, 20%
     of the placement's size, already the default `BreakShardPercent`),
     `ShieldBashShortWait`, `ShieldwallCoversParty`, `ThornsPercent`
     (15), `ReflectMagicPercent` (15) and `SilenceCasterOnSpellHit`. Set
     them on `actor.PlantedShield` in the `ArmEngineSeams` pass.
  3. Append `StatusEffectType.Silence` to `StatusEffect.cs` (its
     DurationClock, HUD slug and glossary entry, and the block on casting).
     Subscribe to `SpellHitShieldHolder` to apply it for 1 turn, with the
     3-turn per-enemy cooldown.
  4. Content rows: the Plant the Shield skill (30 Fury,
     `placeholder_brawler_ward` upgraded by the slot-10 node), Shield Bash
     (slot 12), the Spellbreaker, Thornwall and Shieldwall nodes, and bot
     valuations for Plant the Shield and Shield Bash.
  5. Art: a planted-shield sprite with cracked and broken states (read
     `IsPlaced` and `Ward.Magnitude` against the placed size), and the
     Shieldwall visual (one wall across the party, read `IsShieldwall`).
- Not built: Spellbreaker T3 (reflected damage grants half as Fury) and
  Thornwall T2/T3 (slow; disarm on a physical break). The root's own per-hit
  Fury engine is built now (Phase 2, below).

**Phase 2 engines and the Einherjar seams — `CombatantState.FuryEngine`,
`Momentum`, `BattleTrance`, `TwinRampage`, `CooldownOverrides`**
(`FuryEngine.cs`, `EinherjarSeams.cs`; session half
`FightSession.FuryEngines.cs`; tests `FuryEngineTests`,
`EinherjarSeamsTests`, `[D]`). All off by default; nothing sets them yet.

- The engine: `actor.FuryEngine.Kind = FuryEngineKind.Sentinel |
  Einherjar | Juggernaut` (None = today). Any engine REPLACES the pool's
  flat `gainOnAttack`/`gainOnDamageTaken` for that combatant, in
  `NoteDamageForPools` (the one pool seam). Fury that talent riders grant
  through `PrimaryPool.Gain` (Unyielding T3, Bellow T3, Hold the Line T2,
  Bloodfire) is untouched and still pays under every engine.
- Sentinel, per HIT taken: `FuryEngine.SentinelFury(raw, maxHP)` =
  `clamp(floor(raw x 150 / maxHP), 3, 25)` — the same helper Shieldwall
  uses (`PlantedShield.FuryForAbsorbedHit`). RAW = the blow before his
  defences: `DamagePipeline.WardResolver` now carries a fifth argument,
  `incoming` (the outgoing figure after the attacker's crit and execute
  bonus, before effectiveness, Protect/Vulnerable, resistance, variance and
  plating); `ResolveWard` holds it for the one pool hearing that follows.
  Consistent with 4a: a hit an ordinary ward eats whole is not heard, so
  pays nothing (the held figure is dropped); the planted shield's own
  hearing pays once on the raw figure; a hit that breaks through pays once
  (pinned). Status ticks and Ignore Pain installments keep his turn from
  reading idle but are not hits and pay nothing (decided here: "per hit
  taken"). Blows with no ward step (splash, relic packets, riders) pay on
  what they dealt. Decay: the pool's default.
- Einherjar, per damaging ACTION: `FuryEngine.EinherjarFury(damage, Attack)`
  = `clamp(floor(damage x 10 / Attack), 5, 30)` on the amount the pools
  hear (post-defence, post-ward). Once per action for its single largest
  hit: paid as a top-up on the beat of each hit (a bigger later hit pays
  the difference), so an area sweep pays exactly its largest hit. The action
  boundary is the flat gain's own (`OpenTurnFor`/`ReopenTurnFor`, where the
  OncePerTurn locks reset). Hack: `session.BeginPerHitEngineAction(actor)`
  before resolving makes every hit of that action pay separately
  (`FuryEngine.PaysPerHit`, cleared at the post-action seams). Riders inside
  a Hack action also pay per hit. Nothing for being hit. Decay: default.
- Juggernaut, per turn start: `FuryEngine.JuggernautFury(hp, maxHP)` =
  `15 x (1 + 3 t^2)` in integers (100% 15, 75% 20, 50% 35, <=25% 60,
  floored between; pinned). Paid in `TickPrimaryPool` after the pool's own
  tick, whose idle decay is skipped (`ResourcePool.TickTurnStart(allowDecay)`).
  Once per his OWN turn (decided 2026-09-28): an extra action that reopens
  the turn (Trample, Bloodlust) re-runs the pool's own tick, as `gainPerTurn`
  always has, but does not pay the Juggernaut income again
  (`TickPrimaryPool(actor, reopened: true)`; pinned). No hit-based income.
- Momentum (`actor.Momentum`): `Enabled` (T1), `ExtendedStackCap` (8
  instead of 5) and `CritDamagePerStackBonus` (+5% crit damage per stack)
  for T2, `IgnoresSmallHits` (T3: a hit under 10% max HP keeps the stack).
  A turn that dealt damage (on his own turn, as the pools heard it) adds a
  stack at his turn end; a turn that dealt none resets to 0; each HIT taken
  removes one. `CritRules.ChanceFor`/`DamagePercentFor` add
  `CritChanceBonus`/`CritDamageBonus`, so rolls, previews and the bot all
  see it. `FillToCap()` is Headsplitter T3's hook.
- Battle Trance (`actor.BattleTrance = new BattleTrance(20 or 30)`):
  `DoublesWhileTransformed` (T2; read off `CombatantState.Transformation`,
  Bjorn's only form being Berserk), `ProtectWhenTranceBreaks` (T3,
  `ProtectPercent` 50, `ProtectTurns` 1, through `ApplyStatusTo` — no
  hashed file touched). In `LandPacket`, after Ignore Pain's deferral: at or
  above `ThresholdFury` (50; "above 50" read as the 50+ used everywhere
  else), `floor(hit x pct / 100)` is paid at 1 Fury per 1% max HP rounded
  up; if the pool cannot cover it, the soak is what the whole pool covers
  and the pool empties. Hits only. T3 and `session.BattleTranceBroke` fire
  when a soak takes Fury from at-or-above the threshold to below it, the
  moment soaking stops (decided 2026-09-28; the literal "when soaking empties
  his Fury" would need one hit of about 83% max HP at 60% in Berserk from 50
  Fury, so T3 would almost never fire).
- Twin Rampage (`actor.TwinRampage = new TwinRampageRule("rampage", 1f, 1,
  5)`): a DamageAll cast of that skill id at a full-pool tier (spend 100%)
  with `Cooldown` closed runs `ResolveDamageAllWithTwin`: the Einherjar
  engine holds (hits tallied, nothing paid), sweep 1 as cast (own beat),
  sweep 2 at `SecondSweepMultiplier` (own beat), `StunTurns` of Stun on
  every living enemy sweep 2 landed on through `ApplyStatusTo` (the CC
  guard and existing stun rules apply), `Cooldown` opens for 5 of his
  turns, then the held tally pays once for the action's largest hit. No
  living enemy after sweep 1 = no second sweep and no cooldown.
- Per-holder cooldowns: `actor.CooldownOverrides["second_wind"] = 4`
  replaces the row's `cooldownTurns` for that holder
  (`FightSession.CooldownTurnsFor`, read by `BeginCooldown` and the HUD's
  COOLDOWN row and icon).
- Wiring (Unity session, `-BuildContent`):
  1. `pools.json` `fury`: `gainOnAttack` 0, `gainOnDamageTaken` 0 (with an
     engine set they are ignored anyway; zeroing them is what makes "no
     root, no Fury" true).
  2. Append `TalentEffectType.FuryEngineSentinel`, `FuryEngineEinherjar`,
     `FuryEngineJuggernaut` (or one member whose Magnitude is the kind) and
     set `actor.FuryEngine.Kind` in `ArmEngineSeams` from the root node.
     The Juggernaut root also sets `CooldownOverrides["second_wind"] = 4`.
     Roots grant Brace / Hack / Second Wind via `grantsSkillId`.
  3. Hack: a `skills.json` row (DamageSingle, two hits, 0 Fury, 3-turn
     cooldown) and a `SkillEffect` member or flag whose resolution calls
     `BeginPerHitEngineAction(actor)` and then deals the two hits (the
     two-hit resolution itself is new Unity-side work).
  4. Momentum rows: T1 `Enabled`, T2 `ExtendedStackCap` +
     `CritDamagePerStackBonus`, T3 `IgnoresSmallHits`
     (`TalentEffectType` members, set in `ArmEngineSeams`).
  5. Battle Trance rows: T1 `new BattleTrance(20)`, T2 Percent 30 +
     `DoublesWhileTransformed`, T3 `ProtectWhenTranceBreaks`.
  6. Twin Rampage: the slot-20 node sets `actor.TwinRampage = new
     TwinRampageRule()`; Rampage moves to the slot-10 convergence
     (`grantsSkillId`).
  7. Headsplitter (skill: spends all Fury, min 30, scales with Fury spent and
     missing health, kill refunds half) and Berserk (a `Transformation` via
     the existing `TransformGrant` seam, 50 Fury, 50% of both defences into
     Attack, 10 Fury drain per turn, ends at 0; its "no idle decay" reads
     naturally off the engine only if the Unity wiring switches the pool's
     decay for the form) are NOT built here: both are skill/transform rows
     plus new resolution in hashed `SkillEffect`/`Transformation` files.
  8. Bot valuations for Hack, Headsplitter and Berserk.

### Build order — vertical slices per constellation (owner-delegated, 2026-09-28)

Closes review finding 8: not all mechanics, then all content, then
balance. After Phases 1-3, build one constellation fully — its mechanics,
its content, its bot pass — before starting the next. Order: Einherjar
(Slice A), then Juggernaut (Slice B), then Sentinel (Slice C, since the
shared Ward pool in 4a is the newest mechanic).

Each slice is: its Phase 4 mechanics rows (below) + Phase 5 content for
that constellation + a Phase 6 bot pass for it, before the next slice's
mechanics start.

- **Slice A — Einherjar**: mechanics 4g, plus the Einherjar-relevant parts
  of 4h. Content: Einherjar's 21 `talents.json` rows plus Hack. Bot pass:
  Einherjar vs the reworked reward track.
- **Slice B — Juggernaut**: mechanics 4b, 4c, 4d, 4e, plus the
  Juggernaut-relevant parts of 4h (Gorge, Unbroken skills). Content:
  Juggernaut's 21 `talents.json` rows plus Second Wind's `grantsSkillId`
  wiring. Bot pass: Juggernaut vs the reworked reward track.
- **Slice C — Sentinel**: mechanics 4a, 4f, plus the Sentinel-relevant
  parts of 4h (Hold the Line, Bellow upgrades). Content: Sentinel's 21
  `talents.json` rows plus Brace/Plant the Shield's `grantsSkillId` wiring.
  Bot pass: Sentinel vs the reworked reward track.

`talents.json` (63 rows total across the three slices) and `skills.json`;
remove the placeholder kit rows that the trees replace, and the
`bear_bulwark` skill row (Bulwark is dropped, owner, 2026-09-28, third
pass); `-BuildContent`. Routing: `fixer`.

Save compatibility, verified in code (2026-09-28): removed talent ids are
dropped silently on load (`SaveData.Reconcile`), and spent embers are
summed from unlocked ids, so removing `placeholder_brawler_ward_root`
effectively refunds those embers on next load — no migration step needed.

A reuse pass runs before content authoring for each slice (closes review
finding 1): map each node to an existing `TalentEffect` kind where it fits
(`CheatDeathOncePerFight`, `IgnoreDefensePercent` for Slam T3,
`ExecuteDamageBonusPercent` for Headsplitter T2, `TransformExtendOnKill`,
`ProvokedDamageReductionPercent`), and trim nodes that only exist to
differ from an existing one.

`tools/bot.ps1` per slice's constellation, and each against the reworked
reward track once its content is in. Every number in this doc is up for
change here. Every issue that adds a utility skill also adds its bot
valuation for it (same rule as Phase 4's intro).

### Phase 7 — Art

Planted shield (intact + broken), Shieldwall, Berserk battle sprite,
Cursed Blood VFX, three constellation backgrounds. Pipeline:
`docs/ART_PIPELINE.md`. Runs alongside the slice that needs it, not as a
separate final phase; placeholders are acceptable meanwhile (graceful
degradation, per `CLAUDE.md` conventions).

### Parallel track — enemy roster

Owner confirmed the roster will grow to support these trees. Today:
1 enemy crowd-control ability (Forest Warden `grapple`), 1 enemy area
attack (Treant `spore_cloud`), poison as the only damage over time. This
track must land its crowd-control and caster additions before the Slice B
(Juggernaut) and Slice C (Sentinel) balance passes, or the bot reads
Spellbreaker's silence, Unyielding, Unbroken's Unstoppable and Ignore Pain
as worthless. Separate plan.

---

## 6. Decisions taken in the session

- Engines exclusive, trees open (matches Shawn's wool engine).
- Juggernaut Fury: curved per-turn income, no other source.
- Einherjar Fury: measured against Bjorn's Attack (owner accepted the
  self-referenced version over "0.67 per 1% of target max HP").
- Einherjar is crit-based; Momentum is the crit engine; no healing tree.
- Twin Rampage: triggers from Rampage at 100 Fury, second sweep weaker,
  1-turn stun, no refill, 5-turn cooldown.
- Sentinel: only Defense and Magical Defense convert to damage; resistance
  branch replaced by Hold the Line; Bellow moves into Sentinel; Shieldwall
  drains once per ally on area attacks.
- Juggernaut: Ignore Pain delays damage; Unyielding kept as designed;
  Blood Price chosen for the open branch; Unyielding does not fire under
  Unstoppable.
- Sentinel turn-1 Fury (owner, 2026-09-28, third pass): Bellow costs 0
  Fury, 3-turn cooldown.
- Brace vs Plant the Shield (owner, 2026-09-28, third pass): the
  convergence upgrades Brace into Plant the Shield; no second button.
- "Juggernaut gets Fury from no other source" (owner, 2026-09-28, third
  pass): confirmed to mean no hit-based income only — Fury-granting nodes
  bought in other trees (Bellow T3, Hold the Line T2, Bloodfire) still pay.
- Engine pick is mandatory (owner, 2026-09-28, third pass): Bjorn cannot
  enter a fight without a chosen root; the Talents screen (or first hub
  visit) forces the choice.
- `TalentEffect` reuse (owner-delegated, 2026-09-28, review finding 1): a
  reuse pass runs before content authoring for each slice, mapping each
  node to an existing kind where it fits and trimming nodes that only
  exist to differ; see the Phase 5/build-order section.
- Bot valuations for utility skills (owner-delegated, 2026-09-28, review
  finding 2): every issue that adds a utility skill also adds its bot
  valuation for it, so Phase 6 can evaluate Sentinel and Juggernaut.
- Blood Price near-zero health (owner-delegated, 2026-09-28, review
  finding 6): health payment cannot take him below 1 HP; self-payment
  never triggers cheat death.
- Ignore Pain T3 vs Cursed Blood (owner-delegated, 2026-09-28, review
  finding 7): under Cursed Blood, heals convert first and do not clear
  delayed damage.
- Build order (owner-delegated, 2026-09-28, review finding 8): vertical
  slices per constellation after Phases 1-3 — Einherjar, then Juggernaut,
  then Sentinel — each slice bundling its Phase 4 mechanics, Phase 5
  content and a Phase 6 bot pass.
- Enemy-roster track sequencing (owner-delegated, 2026-09-28, review
  finding 10): the crowd-control and caster additions land before the
  Slice B and Slice C balance passes.
- Twin Rampage second sweep (review finding 11): no work needed — verified
  in code that party-applied Stun on enemies already uses `HasStun` at
  enemy turn.
- Second Wind cooldown under Juggernaut (owner-approved, self-review
  2026-09-28): when granted by the Juggernaut root, Second Wind has a 4-turn
  cooldown — 60 free Fury per turn at low health would otherwise chain
  near-full heals every turn.
- Planted-shield break shards (2026-09-28): 20% of the shield's max HP (the
  placement's size), dealt to the breaking attacker; wired as the default
  `PlantedShield.BreakShardPercent`.
- Battle Trance T3 (owner-delegated, 2026-09-28): Protect for 1 turn fires
  when a soak takes Fury from at-or-above the threshold to below it (the
  moment soaking stops), not when Fury reaches 0.
- Juggernaut income (owner-delegated, 2026-09-28): once per his own turn; an
  extra action that reopens the turn (Trample etc.) does not pay again.
- Thornwall "melee" (2026-09-28): a physical MOVE
  (`CombatActions.IsPhysicalMove`), as 4a built it.

## 7. Open questions

None. Working names are adopted; renaming is cosmetic.

---

## 8. Review findings (2026-09-28), all decided

| # | Finding | Recommendation |
|---|---|---|
| 1 | `TalentEffect` is a CLOSED enum (Type/Magnitude/Threshold only, ~35 kinds, each wired inline at its mechanic). The 60 non-root nodes here need roughly 40+ new kinds; the plan did not budget this. | A reuse pass before Phase 5 mapping each node to an existing kind where it fits (`CheatDeathOncePerFight`, `IgnoreDefensePercent` for Slam T3, `ExecuteDamageBonusPercent` for Headsplitter T2, `TransformExtendOnKill`, `ProvokedDamageReductionPercent`), and trim nodes that only exist to be different. DECIDED (Claude recommendation, owner-delegated): a reuse pass runs before Phase 5, mapping each node to an existing kind where it fits and trimming nodes that only exist to differ. Moved to section 6; added as an explicit step at the start of the Phase 5/build-order section. |
| 2 | The balance bot scores actions by previewed damage; non-damage skills score 0 (`SkillResolution` returns 0 for Provoke, Transform, Ward-type utility). Brace, Bellow, Hold the Line, Plant the Shield, Berserk, Unbroken, Cursed Blood would be ignored, so Phase 6 cannot evaluate Sentinel or Juggernaut. | Add bot valuations for each new utility skill in the same issue that adds the skill. DECIDED (Claude recommendation, owner-delegated): every issue that adds a utility skill also adds its bot valuation for it. Moved to section 6; added to Phase 4's intro and to Phase 6. |
| 3 | Sentinel cannot act as a tank on turn 1: Fury starts at 0 and only comes from being hit, while Bellow (20), Plant the Shield (30) and Hold the Line (40) all cost Fury. | Bellow costs 0 with a 3-turn cooldown. DECIDED (owner): Bellow costs 0 Fury, 3-turn cooldown. Moved to section 6. |
| 4 | Brace (root) and Plant the Shield (convergence) are two Ward buttons. | The convergence upgrades Brace into Plant the Shield instead of adding a second button. DECIDED (owner): confirmed as recommended; no second button. Moved to section 6. |
| 5 | "Juggernaut gets Fury from no other source" conflicts with Fury-granting nodes in other trees he can still buy (Bellow T3, Hold the Line T2, Bloodfire). | Those still pay; the rule means "no hit-based income", nothing more. DECIDED (owner): confirmed as recommended. Moved to section 6. |
| 6 | Blood Price health cost: no rule for paying near 0 health. | Cannot pay below 1 HP; self-payment never triggers cheat death. DECIDED (Claude recommendation, owner-delegated): health payment cannot take him below 1 HP; self-payment never triggers cheat death. Moved to section 6; added to the Blood Price row in section 4. |
| 7 | Ignore Pain T3 vs Cursed Blood: order undefined. | Under Cursed Blood, heals convert first and do not clear delayed damage. DECIDED (Claude recommendation, owner-delegated): under Cursed Blood, heals convert first and do not clear delayed damage. Moved to section 6; added to the Cursed Blood section. |
| 8 | Phase order builds all mechanics, then all content, then balance. | Vertical slices per constellation after Phases 1-3: Einherjar first (exercises the crit system, mostly existing seams), then Juggernaut, then Sentinel (shared Ward pool is the newest mechanic). DECIDED (Claude recommendation, owner-delegated): vertical slices after Phases 1-3 — Einherjar, then Juggernaut, then Sentinel — each slice bundling its Phase 4 mechanics, Phase 5 content and a Phase 6 bot pass. Moved to section 6; section 5 restructured accordingly. |
| 9 | Existing saves / new players: a Bjorn with no root has no Fury and only Slam. | The Talents screen (or first hub visit) forces the engine pick; until then Bjorn is flagged in the party UI. DECIDED (owner): the engine pick is MANDATORY — Bjorn cannot enter a fight without a chosen root. Moved to section 6. |
| 10 | Several branches react to enemy behaviour the roster barely has (crowd control, magic casts). | The enemy-roster track lands before Sentinel/Juggernaut balance, or the bot reads those branches as worthless. DECIDED (Claude recommendation, owner-delegated): the enemy-roster track lands its crowd-control and caster additions before the Slice B and Slice C balance passes. Moved to section 6; the "Parallel track" paragraph updated. |
| 11 | Twin Rampage second sweep: keep 1x + 1-turn stun. | Verify party-applied Stun on enemies uses `HasStun` at enemy turn (it does per the 2026-09-28 read), so no new work there. DECIDED (Claude recommendation, owner-delegated): no work needed — already verified. Moved to section 6. |

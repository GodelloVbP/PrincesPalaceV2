# Plan: Bjorn's three constellations (Sentinel, Einherjar, Juggernaut)

Status: DESIGN + PLAN ONLY. No code has been written against this.
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
  double-counts on Einherjar. Rework them into engine-neutral rewards
  (e.g. Fury capacity, `FuryStartOfFight`). Owner to confirm.
- **Starting kit** (deferred by owner, partially settled):
  - Slam: in the base kit. Einherjar's bottom-left branch upgrades it.
  - Bellow: OUT of the base kit, unlocked by Sentinel.
  - Rampage: OUT of the base kit, Einherjar convergence.
  - Brace, Bulwark, Second Wind: open, see section 7.
  - `placeholder_brawler_ward_root` (the one current bear talent): removed.

---

## 2. Sentinel (tank) — shield silhouette

Point of the shield at the bottom (root), the boss of the shield at the
convergence, the ultimate on the top edge.

**Root — engine (slot 0).** Fury per hit taken =
`raw incoming damage / Bjorn max HP x 150`, clamped 3-25 per hit. RAW means
before his defenses reduce it, so resistance investment never cuts his
income. Damage to his planted shield or to Shieldwall counts. Decay: pool
default.

### Bottom

| Branch | Tier 1 (1) | Tier 2 (2) | Tier 3 (3) |
|---|---|---|---|
| **Iron Retort** (L: 1/4/7) — defense into damage | His physical hits gain flat damage = 10% of (Defense + Magical Defense above his base stats). Elemental resistances do not count. | 20% | 30% |
| **Hold the Line** (C: 2/5/8) — active party buff | Unlocks the skill: 40 Fury, every ally gains +Defense and +Magical Defense for 2 turns. | 3 turns; Bjorn gains 5 Fury each time an ally is hit while it is active. | Cast also cleanses one debuff from each ally. |
| **Bellow** (R: 3/6/9) | Unlocks Bellow (existing `Provoke` effect), 20 Fury. | Passive: enemies prefer Bjorn as a target even without Bellow. | Bellow grants 10 Fury per enemy provoked. |

"Above his base stats" (40 / 12 today) means only gear, relics and
talents add damage, so a fresh Bjorn is not a damage dealer by default.

### Convergence — Plant the Shield (slot 10)

Replaces Brace for a Sentinel. Bjorn sets his shield down in front of
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
Damage after mitigation. A crit deals more, so it pays more with no extra
rule. Decay: pool default (idle turns drain).

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

Owner: "maybe a bear". Proposal: a paw print. Root at the heel of the main
pad, bottom branches inside the pad, convergence at the top of the pad,
the three top branches running up into three toes, the ultimate as the
largest centre toe. Owner to confirm or pick a bear head instead.

**Root — engine (slot 0).** Fury per turn, from nothing else:
`15 x (1 + 3 x t^2)`, `t = clamp((1 - HP%) / 0.75, 0, 1)`.

| HP | Fury / turn |
|---|---|
| 100% | 15 |
| 75% | 20 |
| 50% | 35 |
| 25% or less | 60 |

Curved, not linear: modest above half health, steep below. Idle decay is
OFF for this engine; the per-turn income is the whole economy.

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
| **Blood Price** (R: 13/16/19) — control over his own health | When short on Fury, skills can be paid with health instead: 1 Fury = 0.5% max HP. | Health-paid skills deal +15% damage. | Cheat death, once per fight: lethal damage leaves him at 1 HP and fills Fury to 100. |

Unyielding vs Unstoppable (owner decision): while Unstoppable is active,
Unstoppable blocks the crowd control and Unyielding neither fires nor
starts its cooldown. It stays loaded for after Unstoppable ends.

Blood Price exists because every other branch assumes low health while
three of them push his health back up; it gives the player a deliberate
way down, and the cheat death makes Cursed Blood survivable.

### Ultimate — Cursed Blood (slot 20)

Skill. For 2 turns Bjorn cannot be healed. Every heal that would land on
him (his regen, Gorge, Unbroken, an ally's heal, his share of a party heal)
is dealt as damage to every enemy instead.

- Converts the EFFECTIVE heal: capped by his missing health at that
  moment. Since he cannot heal during it, his missing health stays put and
  each heal converts in full. A raw-heal version would reward being at
  full health, the opposite of the tree.
- Second Wind (if kept, section 7) becomes a nuke under this; the
  missing-health cap bounds it.

---

## 5. Implementation plan

Routing per `CLAUDE.md`: triage each phase at launch. Every phase below is a
separate issue with one owner. Gates per `docs/TESTING.md`; formula tests
pin literal values (gotcha 5).

### Phase 1 — Critical-hit system (game-wide, first; owner decision)

- Stats: crit chance, crit damage on `StatBlock`/`StatType` (APPEND enum
  members, never insert; content stores ordinals).
- Roll in `CombatMath` / `DamagePipeline` through the existing RNG seam so
  the bot stays deterministic.
- Beat/popup + combat log line for a crit.
- Sources: gear/relic affix hooks (authoring later).
- Revisit the "cannot be critically hit" note in `TalentEffect.cs:152`.
- Decision needed: can enemies crit? Recommendation: no in this phase.
- Likely `senior` with `Escalation: cross-layer` (serialized stat enum +
  Core damage path + Editor/scene popup must land together). Gate:
  `run_tests_parallel.ps1 -Changed`, plus `-BuildScenes` / `-BuildContent`
  if touched.

### Phase 2 — Engine-driven Fury

- Zero the pool's flat gains; move Fury income into the three root
  `TalentEffect`s with the formulas in sections 2-4.
- Per-engine decay (Juggernaut: off).
- Reward-track bear rows reworked (section 1).
- `implementer`.

### Phase 3 — Per-character constellation silhouettes

- `ConstellationLayout.Plots` keyed by (character, path) instead of path;
  graceful fallback stays the spire.
- Three plots: shield, double-bitted axe, paw print. Background art for
  each.
- Screen tree in `Domain/UiKit/Screens/`, wiring in `ScreenRegistry.cs`;
  `-BuildScenes`; `UiAudit` must pass at all four aspects.
- `implementer`.

### Phase 4 — Combat mechanics (one issue each, in this order)

| # | Mechanic | Used by | Notes |
|---|---|---|---|
| 4a | Damage redirect to a shield entity + a shared party pool | Plant the Shield, Shield Bash, Shieldwall | New damage-routing lifecycle; likely `senior`, `Escalation: architecture`. Decide: status on Bjorn vs. a `Summon`-style field entity. |
| 4b | One heal funnel with a conversion hook | Cursed Blood, Ignore Pain T3 | Every heal path must pass one seam or conversion leaks. |
| 4c | Delayed damage pool | Ignore Pain | |
| 4d | Health as skill cost + cheat death | Blood Price | |
| 4e | CC immunity + CC-negate trigger | Unbroken, Unyielding | |
| 4f | Reflect, silence, thorns, slow, disarm | Spellbreaker, Thornwall | Silence may need a new status. |
| 4g | Momentum stacks, Fury soak, Berserk (reuse `Transformation`), Headsplitter, Twin Rampage | Einherjar | After phase 1. |
| 4h | Hold the Line, Bellow upgrades, Gorge, Unbroken skills | Sentinel, Juggernaut | Mostly content on existing effects. |

### Phase 5 — Content

`talents.json` (63 rows, slots per the tables above) and `skills.json`;
remove the placeholder kit rows that the trees replace; `-BuildContent`.

### Phase 6 — Balance

`tools/bot.ps1` per constellation, and each against the reworked reward
track. Every number in this doc is up for change here.

### Phase 7 — Art

Planted shield (intact + broken), Shieldwall, Berserk battle sprite,
Cursed Blood VFX, three constellation backgrounds. Pipeline:
`docs/ART_PIPELINE.md`.

### Parallel track — enemy roster

Owner confirmed the roster will grow to support these trees. Today:
1 enemy crowd-control ability (Forest Warden `grapple`), 1 enemy area
attack (Treant `spore_cloud`), poison as the only damage over time. Until
that grows, Spellbreaker's silence, Unyielding, Unbroken's Unstoppable and
Ignore Pain see little use. Separate plan.

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

## 7. Open questions

1. Juggernaut silhouette: paw print (proposed) or bear head?
2. Brace: gone entirely, or base-kit for non-Sentinel builds?
3. Bulwark (ally shield) and Second Wind (Fury to heal): keep, fold into a
   tree (Hold the Line / Juggernaut), or drop?
4. Does Unbroken's regen convert under Cursed Blood? (The obvious combo;
   either tune Cursed Blood around it or exclude it.)
5. Cursed Blood cost, cooldown and damage type (physical, true, or a type
   enemies can resist).
6. Can enemies crit (phase 1)?
7. Reward-track `FuryGainOnAttack` rows: what replaces them?
8. Names still placeholders: Iron Retort, Thornwall, Spellbreaker, Plant
   the Shield, Bloodfire, Battle Trance, Twin Rampage, Thick Blood, Wrath,
   Gorge, Unbroken, Unyielding.

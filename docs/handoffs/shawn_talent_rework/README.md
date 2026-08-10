# Handoff: Shawn's Talent Rework — Wool as a spend resource

Design session 2026-08-07. This is a **mechanics** handoff, not a screen
handoff — the talent tree's layout, art kit, and interaction model from
`docs/handoffs/talent_tree_v2/` all survive unchanged. What changes is what
the nodes *do*, what they cost, and what the resource underneath them is for.

Status markers used throughout:
**[LOCKED]** decided in session · **[PROPOSED]** suggested, not yet confirmed ·
**[OPEN]** genuinely undecided, needs a call before building.

---

## 1. Overview

Shawn's 21-node-per-path talent tree currently hands out flat additive
stats (`+15 maxHealth`, `+3 defense`, `+1 strength`) across 63 nodes. Every
node is commensurable with every other node, so there is no build to make —
only a stat total to accumulate.

This rework keeps the tree's *shape* (root → 3×3 grid → convergence →
3 limbs → capstone) and replaces its *payload*. Nodes now grant triggered
effects, conditional modifiers, and rule-changes. Wool stops being a damage
sponge and becomes the resource those effects spend.

**What it replaces:** the payload vocabulary of all 63 Shawn talents in
`Assets/_Project/ContentData/talents.json`, the cost derivation in
`ContentDatabase.OrbCost`, and the `minSpent` gate values.

---

## 2. Wool

| Property | Value | Status |
|---|---|---|
| Max wool | **10** | [LOCKED] |
| Baseline generation | **+1 per turn**, all paths | [LOCKED] |
| Damage soak | **Removed.** Wool no longer absorbs damage by default | [LOCKED] |
| Purpose | Spent on abilities unlocked in the talent tree | [LOCKED] |
| Display | Sheep above Shawn's head; "awake" sheep = current wool, dormant = empty | [LOCKED] |

Abilities bought with wool get their own bespoke animations. [LOCKED]

**[OPEN] — pip art at 10.** Ten sheep above one head is a lot of sprites in
a small space. Either ten small sheep, or five sheep with two states each.
Not resolved. Current HUD code builds a generic pip row
(`FightController.Hud.cs:376`) whose mock shows `3/16`, so both the max and
the pip treatment need revisiting.

**[PROPOSED] — Woolgathering.** There is already a stub ability named
`Woolgathering` in the HUD mock (`FightController.Hud.cs:210`, tagged
`SUPPORT · SELF · FREE`). Suggested: an action that gains 2-3 wool but
consumes your whole turn — so wool can be *taken* at a tempo cost, not just
waited for. Discussed, never confirmed.

---

## 3. The engine (allegiance)

The root orb of each path is that path's **generation rule**. It is:

- **Free** — costs 0 embers. [LOCKED]
- **Mutually exclusive across all three paths** — exactly one root may ever
  be lit. [LOCKED]
- **Swappable via Respec All**, which now also clears allegiance. [LOCKED]

This is the load-bearing rule of the whole design. Three generation rules
stacking would produce ~+4-5 wool/turn against abilities priced at 7-8,
collapsing the economy. Locking the *engine* rather than the whole tree is
what lets you own nodes from all three paths without breaking anything.

**[PROPOSED] — respec cost.** A fusion build (§7) spends 29 of 30 embers
with no slack, so one mistimed purchase can end a build. Respec should be
free, or trivially cheap. Not confirmed.

---

## 4. The three paths

### Path 0 — The Black Ram (aggression / juggernaut) [LOCKED]

Fully specced in §6.

- **Engine:** gains wool from being hit, and more per turn the lower his
  health falls.
- **Convergence:** Black Ram Mode.

### Path 1 — The Fragile Lamb (debuff / buff / utility) [LOCKED as concept]

Fragile. Debuffs enemies, buffs allies, lets others tank.

- **Engine:** gain **+2 wool when an ally you have shielded or buffed takes
  a hit.** [LOCKED]

  > This replaced the original "gain +1 whenever any party member is hit."
  > That version had no agency — enemy targeting decided your income, and
  > it made the Lamb mechanically identical to the Ram (both engines run on
  > incoming damage). The setup step is what makes it a different tree:
  > you ward the tank, the tank eats a hit, you get paid.

- **Convergence — Wail:** all enemies debuffed, dealing **50% less damage
  for 2 turns**. Cost **8 wool**. [LOCKED]

  > **[OPEN]** — Wail is party-wide *and* enemy-wide mitigation, which is
  > arguably bigger than the Ram's personal 3-turn buff at 7. Suggested
  > reprice to 10 wool, or drop to 1 turn. Not resolved.

- Six strands: **not designed.**

### Path 2 — The mage sheep (DoT / utility caster) [name OPEN]

- **[OPEN] — name.** Candidates, keeping the adjective+animal parallel with
  Black Ram / Fragile Lamb: **The Golden Ewe** (mythic, parallel),
  **The Weaver** (breaks the pattern but explains the mechanics — wool,
  thread, fate-spinning, and thread as a physical weapon), **The Hollow
  Ewe** (if the magic should feel unsettling). No pick made.

- **Engine [PROPOSED, tacitly accepted]:** at the start of your turn, gain
  **+1 wool per enemy suffering a status effect you applied.**

  > This is the third distinct axis: the Ram runs on damage-in-to-self, the
  > Lamb on damage-in-to-allies, and this on **board state** — you're paid
  > for how much of the battlefield you've touched. Play pattern is "spread
  > wide early, cash in late."
  >
  > With a stated cap of **3 enemies per fight**, this runs at +1 to +3 per
  > turn, in line with the other two engines. No cap needed.

- **Convergence — wool explosion:** AoE, dealing **physical** damage (so
  the caster path has an answer to magic-resistant enemies), and leaving
  enemies coated in wool at **-50% speed for 2 turns**. [LOCKED]

  > The explosion applies a status, which pays the engine next turn. The
  > ultimate refuels the engine that built it.
  >
  > On the `wip/intent-speed` branch a 50% speed cut visibly reorders the
  > turn queue — a very legible payoff.

  **[OPEN] — wool cost.** Never set. 8 is the working assumption.

- Six strands: **not designed.**

---

## 5. Strand structure

Each path's 21 slots decompose into **6 strands of 3 nodes**, plus a free
root, a convergence, and an ultimate.

- **A strand is one idea, developed three times.** Rhythm: **number,
  number, rule-change.** T1 and T2 tune a knob; T3 changes how the game
  works. The prereq chain already forces you through T1 and T2 to reach T3,
  so the escalation is structurally free.
- **Three strands below the convergence, three above.** Below = *get you to
  the transform*. Above = *make it count*.
- This cuts authoring from 63 unique effects to **18 ideas** across Shawn.

The player picks strands, not nodes. "I went Blood Price and Pact" is a
sentence a player can say out loud; sixty-three stat bumps never produces
one.

---

## 6. The Black Ram — full spec

### 6.1 Engine (root, free, exclusive)

| Effect | Value | Status |
|---|---|---|
| Wool on being hit | **+1 per hit taken** | [PROPOSED] — never pinned |
| Wool per turn below 67% max HP | **+1** | [LOCKED] |
| Wool per turn below 33% max HP | **+2** | [LOCKED] |

**[OPEN] — do the HP tiers stack with the baseline, or replace it?** Asked,
never answered. Two readings:

- *Replace:* full HP +1/turn · below 67% +2/turn · below 33% +3/turn.
- *Stack:* full HP +1/turn · below 67% +2/turn · below 33% +4/turn.

Recommend **replace** — it keeps the ceiling at +3/turn plus on-hit gains,
which is what the ability prices below were sanity-checked against.

### 6.2 Below the convergence — three strands, 1 / 2 / 3 embers

**Sharp Horns** — armor penetration (damage)

| Tier | Cost | Effect |
|---|---|---|
| T1 | 1 | Attacks ignore **25%** of the target's defense. |
| T2 | 2 | Ignore **50%**. |
| T3 | 3 | **Rule-change:** enemies you hit lose **2 defense permanently** for the rest of the fight, stacking. |

> T3 turns a selfish stat into a party contribution — you soften targets and
> everyone benefits. Armor is the stat the player identified as "necking you
> every run," so this strand is the direct answer to it.

**Provoke** — utility + defense (and the income engine)

| Tier | Cost | Effect |
|---|---|---|
| T1 | 1 | Unlocks **Provoke**: force one enemy to target Shawn on its next turn. |
| T2 | 2 | Provoked enemies deal **20% less damage** to Shawn. |
| T3 | 3 | **Rule-change:** Provoke affects **all** enemies, and each enemy targeting Shawn grants **+1 wool per turn**. |

> Deliberate placement: T3 brings the income engine fully online at roughly
> the same moment the convergence unlocks.

**Trample** — execute + splash (damage)

| Tier | Cost | Effect |
|---|---|---|
| T1 | 1 | **+25% damage** to enemies below **30%** max HP. |
| T2 | 2 | Killing an enemy deals **50% of Shawn's attack** as splash to adjacent enemies. |
| T3 | 3 | **Rule-change:** a kill does not consume the action — Shawn may attack again. **Cap at one extra attack per turn** to avoid unbounded chains. |

### 6.3 Above the convergence — three strands, 2 / 3 / 4 embers

**Wrath** — juggernaut (damage + defense)

| Tier | Cost | Effect |
|---|---|---|
| T1 | 2 | Black Ram Mode lasts **4 turns** instead of 3. |
| T2 | 3 | A kill during Black Ram Mode **extends it by 1 turn**. Cap the total extension at +3. |
| T3 | 4 | **Rule-change:** Black Ram Mode **does not expire** while Shawn is below **33%** max HP. |

> T3 is why this is the juggernaut limb: played correctly, the transform
> stops being a timer.

**Last Stand** — defense

| Tier | Cost | Effect |
|---|---|---|
| T1 | 2 | **+25% defense** while below **50%** max HP. |
| T2 | 3 | **Cannot be critically hit** while below **25%** max HP. |
| T3 | 4 | **Rule-change:** once per fight, a hit that would reduce Shawn to 0 leaves him at **1 HP** instead. |

> A tree built on being wounded needs a safety net or nobody commits to it.
> This node is what makes the low-health playstyle playable rather than
> merely theoretically strong. It sits *above* the convergence deliberately —
> a 1-ember slot would have undersold it.

**Charge** — utility + speed

| Tier | Cost | Effect |
|---|---|---|
| T1 | 2 | Unlocks **Headbutt**: an attack that also pushes the target back in the turn queue. |
| T2 | 3 | Headbutt's push also **cancels the target's telegraphed intent** — they lose the action outright. |
| T3 | 4 | **Rule-change:** entering Black Ram Mode pushes **every** enemy back in the queue. |

> **[OPEN] — push magnitude.** Needs pinning against the `wip/intent-speed`
> system's actual units. "One slot later in the queue" is the intended feel;
> whether that's expressed as a flat speed penalty or a queue-index shift
> depends on how intents are ordered.
>
> Note the interaction: **Charge T3 fires on transform, so it also fires
> inside a fusion window.** Ram + Explosion with Charge maxed = shed your
> armor, detonate, and knock the entire enemy team to the back of the queue
> while at massive speed.

### 6.4 Convergence — Black Ram Mode

**Free.** Gated at **9 embers spent in this path**. Costs **7 wool** to
activate. Duration **3 turns** (before Wrath).

| Grant | Value | Status |
|---|---|---|
| Splash damage on attacks | — | [LOCKED] |
| Bonus attack | **+50%** | [PROPOSED] — never pinned |
| Bonus speed | **+30%** | [PROPOSED] — never pinned |
| Temporary HP | **25% of max HP**, expires with the mode | [LOCKED] |

> The temp HP must be **temporary**, not permanent max HP. It saves him
> during the 3 turns and then drops him straight back into the wounded band
> where his engine runs hot. Permanent max health would fight his own
> generator — which is why there are **no max-HP nodes anywhere in this
> tree**.

### 6.5 Ultimate — **[OPEN, not designed]**

**Free.** Gated at **20 embers spent in this path**.

The Black Ram's capstone was never designed. Two candidates raised:

- **Permanent Ram Mode below 25% HP** — the transformation stops being a
  button and becomes who he is.
- **Die once per fight and rise as the ram at full wool** — note this
  overlaps heavily with Last Stand T3 and would need differentiating.

---

## 7. Economy

| Item | Cost | Status |
|---|---|---|
| Root / engine | **0** (choose exactly one across all paths) | [LOCKED] |
| Bottom strand nodes | **1 / 2 / 3** (6 per strand, 18 for all three) | [LOCKED] |
| Top strand nodes | **2 / 3 / 4** (9 per strand, 27 for all three) | [LOCKED] |
| Convergence | **0**, gated at **9** embers spent in that path | [LOCKED] |
| Ultimate | **0**, gated at **20** embers spent in that path | [LOCKED] |
| **Lifetime spendable embers** | **30**, across all three paths | [LOCKED] |

A full path costs 45 embers of nodes. A 30 budget therefore buys about
two-thirds of one path — the tree is a genuine choice, not a checklist.

### The three archetypes

| Build | Spend | What you get |
|---|---|---|
| **Deep** | 30 in one path | Full bottom (18) + 12 into top strands, plus convergence and ultimate. No fusion. |
| **Wide** | 9+9+9 = **27** | All three convergence abilities. No ultimate, so **no fusion**. 3 embers spare. |
| **Fusion** | 20 + 9 = **29** | One ultimate + one other path's convergence. 1 ember spare. |

### Why the ultimate gate is 20 and not 21

At 21, the most intuitive purchase order — buy all the cheap bottom nodes
first — could never hit the gate exactly. Bottom strands cost 1/2/3
chained, so a strand contributes only 0, 1, 3, or 6, and all three max at
**18**. No top node costs 3 as a first purchase (they are 2, then 3), so
that player lands on 20 or 22, never 21. At 22 spent, fusion costs 31 and
**they miss it by one ember for playing the most natural way possible.**

At 20: `18 + 2 = 20` qualifies. Other exact routes still exist (bottom 9 +
top 11, bottom 10 + top 10, bottom 13 + top 7, bottom 15 + top 5), so
optimisation is rewarded but no longer mandatory.

Two properties worth preserving:

- **20 is the tightest gate that still forces a top-strand purchase.**
  Bottom maxes at 18, so the ultimate can never be bought on foundations
  alone. At 18 or below that hole opens.
- **The spare ember has exactly one legal use.** Nothing in a top strand
  costs 1, so it can only open a fresh bottom strand's T1.

**[OPEN] — what happens to Embers after 30 are spent?** If 30 is a hard
lifetime cap, the currency becomes worthless mid-playthrough and every
reward granting it stops meaning anything. Either the cap grows over a
longer meta arc (which would also make fusion something to *reach for*), or
Embers need a second sink.

---

## 8. Fusion — **[PARKED, late-game]**

Explicitly deferred in session, but recorded so it isn't lost.

**Rule:** each convergence ability leaves a 2-3 turn window. Casting a
*different* signature while a window is open produces a **fused** ability
instead of the normal one.

**Requirement:** an **ultimate** (top of one tree) plus another tree's
**convergence**. This is why it costs exactly 29 of 30 embers — a fusion
build is your entire life savings, touches two of three paths, and leaves
the third untouched. You can only ever have **one** fusion pair, so the
pair you choose *is* your identity.

| Pair | Fused ability | Status |
|---|---|---|
| Ram + Wail | **Piercing Roar** | [LOCKED] name, effect undefined |
| Ram + Explosion | Shed the black wool: **lose defense, gain massive speed**, enemies stuck in thick wool and **vulnerable** | [LOCKED] concept, unnamed |
| Wail + Explosion | **[OPEN]** — suggested **Smother**: the wail rides the wool, enemies wrapped and taking damage each turn, **intents scrambled or hidden**. Devastating on `wip/intent-speed`, where reading intents *is* the skill loop. | [PROPOSED] |

**[OPEN] — order independence.** Is Ram→Explosion the same ability as
Explosion→Ram? Same = 3 fused abilities to author and animate.
Order-dependent = 6. Recommend same; the pair is the identity, not the
sequence.

**[OPEN] — triple fusion.** All three signatures active at once is roughly
22 wool across one fight and unaffordable under the 30-ember cap, but worth
leaving the door open as a secret.

---

## 9. For the engine

What this rework actually breaks in the existing code:

1. **The payload vocabulary is the blocker.** `RawTalentEntry` /
   `TalentDefinition` currently expose only additive fields: `statBonus`,
   `abilityScoreBonus`, `maxManaBonus`, `skillManaCostReduction`,
   `signatureCapacityBonus`, `signaturePerTurnBonus`,
   `grantsStartingItemId`. **Not one of the 18 Black Ram talents above fits
   that vocabulary.** A triggered-effect / conditional-modifier system is a
   prerequisite for all of this, not a follow-up.

2. **`ContentDatabase.OrbCost` derives cost from slot kind** — currently
   1 shallow / 2 deep / 4 signature / 8 ultimate
   (`ContentDatabase.cs:392`). The new scheme is 1/2/3 below, 2/3/4 above,
   **0** for both convergence and ultimate. It must key off position within
   a strand, not just `TalentSkeleton.Kind[slot]`.

3. **`minSpent` gates change** from 8 → **9** (convergence) and stay at
   **20** (ultimate). `SpentInPath` already scopes correctly by
   `characterId` + `column` (`ContentDatabase.cs:423`).

4. **Root exclusivity across columns is a new rule.** Nothing today
   prevents lighting all three roots — the prereq system only checks
   *upward* within a path.

5. **The 30-ember lifetime spend cap is new.** The wallet
   (`CurrencyType.Embers`) is currently uncapped.

6. **Wool max drops to 10.** The HUD mock shows `3/16`
   (`FightController.Hud.cs:190`) and the existing
   `signatureCapacityBonus` talents raise capacity — those nodes have no
   place in the new design and need removing or repurposing.

7. **Wool no longer soaks damage.** Wherever that currently happens, it
   comes out.

8. **Content regeneration is destructive** — `ContentBuilder` deletes
   `Resources/Content/` wholesale before rebuilding. All 63 Shawn talents
   are re-authored in `talents.json`, never by hand in the asset folder.

---

## 10. Out of scope

- **The Fragile Lamb's and mage path's six strands each.** Only their
  engines and convergence abilities exist. Twelve strands still to design.
- **The other four characters** (fly, dog, owl, turtle) — 189 talents,
  entirely untouched by this session. The strand structure in §5 should
  apply to them, but none of it is designed.
- **Screen layout, art, and interaction model** — unchanged from
  `docs/handoffs/talent_tree_v2/`. This handoff moves no pixels.
- **Fusion implementation** — parked, §8.
- **Balance validation.** Every number above is first-pass. Nothing has
  been played.

---

## 11. Open decisions, collected

Ordered roughly by how much downstream work each one blocks.

1. **Black Ram's ultimate** — undesigned (§6.5). Blocks the top of the tree.
2. **Do the Ram's HP tiers stack or replace?** (§6.1) Blocks engine
   implementation.
3. **Wool per hit taken** — exact value (§6.1).
4. **Black Ram Mode's attack and speed numbers** (§6.4).
5. **Turn-queue push magnitude**, pending `wip/intent-speed` units (§6.3).
6. **What Embers do after 30 spent** (§7).
7. **Mage path name** (§4).
8. **Explosion wool cost**; **Wail reprice** (§4).
9. **Sheep pip treatment at max 10** (§2).
10. **Respec cost** (§3).
11. **Fusion order-independence; triple fusion; Wail+Explosion effect** (§8).

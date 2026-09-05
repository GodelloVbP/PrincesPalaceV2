# Gap audit — Shawn's talent rework, handoff vs. built

Audited 2026-08-07, immediately after the implementation pass, against the
handoff `README.md` in this folder. Format per `docs/HANDOFF_TEMPLATE.md`.
Verdict ∈ **match** / **partial** / **missing** / **deliberate-deviation**.

Overall: the **systems are complete and the Black Ram is complete**. The
payload vocabulary the handoff names as "the blocker" (§9.1) exists, the
economy is repriced and gated, wool is a spend-only resource, and all 21 of
the Black Ram's slots are authored and working — including the ultimate the
handoff left undesigned.

What is **not** built is the content the handoff itself put out of scope: the
Fragile Lamb's and the mage path's six strands each (§10). That has one
consequence the handoff does not spell out and which is worth reading before
anything else here — see **The two unbuilt paths** below.

---

## Systems (§9, "For the engine")

| # | Handoff item | Spec'd | Built | Verdict |
|---|---|---|---|---|
| 1 | Payload vocabulary (§9.1) | A triggered-effect / conditional-modifier system; "not one of the 18 Black Ram talents fits" the additive fields | `TalentEffectType` — a closed enum of 21 rules — plus `TalentEffect`, `TalentEffectSet`, and an `effects[]` array on every talent, validated at authoring time by `TalentEntryResolver` | **match** |
| 2 | `OrbCost` keys off strand position (§9.2) | 1/2/3 below, 2/3/4 above, 0 for root/convergence/ultimate | `ContentDatabase.OrbCost`, derived from `TalentSkeleton.Depth`. A full path is 45 | **match** |
| 3 | `minSpent` gates (§9.3) | 8 → 9 at the convergence, 20 at the ultimate | Both, and applied to **all five characters** rather than only Shawn — the reprice is structural, so leaving the other four gated at 8 would mean two gates for one identical skeleton | **match** |
| 4 | Root exclusivity across columns (§9.4) | Exactly one root may ever be lit; a new rule the prereq system cannot express | `ContentDatabase.AllegianceRootOf` / `IsBlockedByAllegiance`, folded into the single `MeetsGates` the screen's colouring pass and its click handler both read | **match** |
| 5 | 30-ember lifetime cap (§9.5) | New; the wallet is uncapped | `EmberSpendCap`, enforced **per character**. The wallet itself stays shared and uncapped — see the note below on §7's open question | **match** |
| 6 | Wool max drops to 10 (§9.6) | And the `signatureCapacityBonus` nodes have no place in the new design | Capacity 10, and no Shawn node touches capacity or the flat rate at all. The two fields survive for the other four characters' 252 nodes, with a test pinning that they never come back on Shawn | **match** |
| 7 | Wool no longer soaks damage (§9.7) | Comes out wherever it happens | `SignatureResource.AbsorbsDamage`, false for Wool. The soak machinery is **kept, not deleted**: it is tuned (see `ContentDatabase.SignatureAbsorbPerPoint`'s own comment on why 2 and not 10) and is the obvious shape for a future resource that IS armour | **deliberate-deviation** (kept behind an off switch rather than removed) |
| 8 | Content regenerated, never hand-edited (§9.8) | — | All 21 Shawn nodes re-authored in `talents.json`; three abilities added to `skills.json`; Shawn's wool block rewritten in `characters.json` | **match** |

## Wool (§2)

| # | Item | Verdict | Note |
|---|---|---|---|
| 9 | Max 10 | **match** | |
| 10 | +1 per turn, all paths | **match** | And the engines *replace* it rather than adding — see decision D2 |
| 11 | Damage soak removed | **match** | |
| 12 | Spent on tree abilities | **match** | Provoke (free), Headbutt (2), Black Ram Mode (7) |
| 13 | Sheep pips above his head | **missing** | The HUD still draws a generic pip row on the party plate. It now correctly shows ten of the sixteen baked pips instead of all sixteen, but the *art* question (§2's [OPEN]: ten small sheep, or five with two states) is untouched — this pass moved no pixels |
| 14 | Woolgathering as a wool-gaining action | **missing** | [PROPOSED], never confirmed. `woolgathering` is still the level-2 self-heal it already was |

## The Black Ram (§6)

All 18 strand nodes, the convergence and the ultimate are authored and
working. Every number is as the handoff states it, except where a row says
otherwise.

| # | Node | Verdict | Note |
|---|---|---|---|
| 15 | Engine: +1 wool per hit taken | **match** | Was [PROPOSED] at "never pinned"; taken as written |
| 16 | Engine: +2/+3 per turn below 67%/33% | **match** | Resolved as **replace**, not stack — decision D2 |
| 17 | Sharp Horns T1/T2 — ignore 25%/50% defense | **match** | Applies to a plain swing AND to a character skill, through the one `EffectiveDefense` overload both read |
| 18 | Sharp Horns T3 — 2 permanent defense per hit, stacking | **match** | The only rule in the vocabulary that deliberately compounds |
| 19 | Provoke T1 — force one enemy onto Shawn | **match** | A `StatusEffectType.Provoked` carrying its source. Spent by the turn it redirects, not counted down — see the note in `StatusEffects.Tick` |
| 20 | Provoke T2 — provoked enemies deal 20% less to Shawn | **match** | A property of the PAIR, so it blunts what is aimed at him and nothing else |
| 21 | Provoke T3 — all enemies, +1 wool each per turn | **match** | The skill widens at cast time rather than being authored twice |
| 22 | Trample T1 — +25% below 30% target HP | **match** | |
| 23 | Trample T2 — kill splashes 50% of Attack to adjacent | **match** | "Adjacent" is index adjacency in the enemy list, which is the order the stage already draws front-to-back |
| 24 | Trample T3 — a kill does not consume the action, cap 1 | **match** | Short-circuits Bloodlust, so a Ram wearing that relic gets one extra action per kill and not two |
| 25 | Wrath T1 — 4 turns instead of 3 | **match** | |
| 26 | Wrath T2 — a kill extends by 1, cap +3 | **match** | |
| 27 | Wrath T3 — does not expire below 33% | **match** | |
| 28 | Last Stand T1 — +25% defense below 50% | **match** | |
| 29 | Last Stand T2 — cannot be critically hit below 25% | **deliberate-deviation** | **This game has no critical hits, in any file.** Built as a spike cap instead: no single hit may exceed 25% of max health while below 25% health. Same intent (the burst that ends the run cannot happen) on a hook that exists. Decision D6 |
| 30 | Last Stand T3 — survive one lethal hit at 1 HP | **match** | Checked before health is written, so nothing downstream ever sees a dead-then-undead combatant |
| 31 | Charge T1 — Headbutt, pushes the target back | **match** | Push magnitude resolved as one queue SLOT — decision D5 |
| 32 | Charge T2 — the push cancels the telegraphed intent | **match** | |
| 33 | Charge T3 — transforming pushes every enemy back | **match** | |
| 34 | Convergence — Black Ram Mode, 7 wool, 3 turns, gated at 9 | **match** | Splash, +50% attack, +30% speed, 25% temporary health. The two [PROPOSED] percentages taken as written |
| 35 | Temporary HP must be temporary | **match** | And exact: `Transformation` stores the deltas it applied rather than recomputing an inverse that does not round-trip through integer arithmetic |
| 36 | Ultimate (§6.5, [OPEN, not designed]) | **deliberate-deviation** | Built as the first of the two candidates: below 25% health the transform is permanent. Decision D1 |

## Out of scope, and stayed out

| # | Item | Verdict |
|---|---|---|
| 37 | The Fragile Lamb's six strands (§10) | **missing** — by design. See below |
| 38 | The mage path's six strands (§10) | **missing** — by design. See below |
| 39 | The other four characters' 189 talents (§10) | **missing** — untouched, still the additive vocabulary |
| 40 | Fusion (§8) | **missing** — parked, as stated |
| 41 | Screen layout, art, interaction model (§10) | **match** — this pass moved no pixels |
| 42 | Balance validation (§10) | **missing** — nothing has been played |

---

## The two unbuilt paths

The handoff puts the Lamb's and the mage path's strands out of scope but
still specifies their engines and their convergence abilities as [LOCKED].
Those were **not** authored, and the reason is worth recording because it is
a decision rather than an omission.

Both engines are paid for the player's own SETUP: the Lamb gains wool when an
ally it has warded is hit, and the mage gains wool per enemy carrying a
status it applied. **Shawn's kit contains no way to do either.** No skill of
his applies a status to an ally or to an enemy — the tools that would live in
those twelve undesigned strands are exactly what is missing. So an authored
Lamb root would generate nothing, and root exclusivity means choosing it
would *lock out* the Black Ram's engine until a respec. A free, permanent
choice that does nothing while removing the one that works is a trap, not an
unfinished feature.

Both engine rules **are implemented and unit-tested**
(`WoolWhenWardedAllyHit`, `WoolPerStatusedEnemy`, and the
`ActiveStatus.Source` attribution both depend on). Authoring the roots is one
JSON entry each on the day their strands land.

The same reasoning applies to Wail and the wool explosion. Both would also
need status kinds that do not exist — an outgoing-damage debuff, and Slow,
which `StatusEffects`' own header explicitly rules out as needing its own
pass against `SpeedScale`'s deliberately delicate curve.

---

## Open decisions (§11), and what was done with each

Numbered as the handoff numbers them.

**D1 — Black Ram's ultimate.** Built as *permanent Ram Mode below 25% HP*,
the first of the two candidates. The second ("die once per fight and rise as
the ram") overlaps Last Stand T3, which the handoff itself flags; two
once-per-fight death mechanics in one tree read as one mechanic bought twice.
**Reversible**: it is one node's `effects` entry.

**D2 — do the HP tiers stack or replace?** **Replace**, as the handoff
recommends. Full HP +1, below 67% +2, below 33% +3. Stacking takes the
ceiling to +4 plus on-hit gains, against a transform priced at 7 — not the
economy those prices were checked against. `TalentEffectSet.BestBelowHealth`
takes the strongest *satisfied* tier, and there is a test on the boundary
case specifically.

**D3 — wool per hit taken.** Taken as the handoff's [PROPOSED] **+1**.

**D4 — Black Ram Mode's attack and speed.** Taken as the [PROPOSED] **+50%**
and **+30%**.

**D5 — turn-queue push magnitude.** Resolved as **one queue SLOT**, not a
charge figure and not a speed penalty. That is the stated intended feel and
the only phrasing that needs no calibration against the scheduler's arbitrary
units: a flat charge penalty means something different to a fast combatant
than to a slow one, and a speed penalty would change how often they act
forever rather than shuffling one turn. `TurnOrder.PushBack` drops the actor
to just under whoever is charged immediately below them — the exact inverse
of what `DelayCurrent` already does for a voluntary delay.

**D6 — Last Stand T2, "cannot be critically hit".** Substituted, because
there are no critical hits anywhere in this codebase, so the node as written
would have been inert content the resolver could not tell from a typo. Built
as a spike cap: no single hit exceeds 25% of max health while below 25%
health. **If crits ever land, this is the node to revisit** — the substitution
is recorded in `TalentEffectType.DamageCapPercentBelowHealth`' own comment so
a content author reads it too.

**D7 — what Embers do after 30 are spent.** Answered in passing rather than
head-on: the cap is **per character**, so the 31st ember goes to somebody
else's tree. The wallet stays shared and uncapped. That keeps every reward
meaningful across a five-hero roster without inventing a second sink or a
meta arc. **If the intent was a save-wide 30, this is a one-line change** and
the question is genuinely still open.

**D8 — mage path name.** Not needed, since the path was not authored. If it
had been: **The Golden Ewe** keeps the adjective+animal parallel with Black
Ram and Fragile Lamb.

**D9 — explosion wool cost; Wail reprice.** Untouched — neither ability was
built.

**D10 — sheep pip treatment at max 10.** Untouched. The row now correctly
shows ten pips rather than sixteen, which is a behaviour fix, not the art
decision.

**D11 — respec cost.** Respec is **free** and refunds in full, which is what
the handoff's [PROPOSED] asks for. It clears allegiance along with everything
else, since it clears every unlocked node.

**D12 — fusion order-independence, triple fusion, Wail+Explosion.**
Untouched; fusion is parked.

---

## One number the handoff never set

**Headbutt's cost: 2 wool.** First-pass, and stated here because nothing in
the handoff pins it. It cannot be free — an attack that also shoves would
strictly dominate the plain Attack every turn. Provoke, by contrast, *is*
free on purpose: it deals nothing, heals nobody, and spends the whole turn
buying the right to choose who swings at you.

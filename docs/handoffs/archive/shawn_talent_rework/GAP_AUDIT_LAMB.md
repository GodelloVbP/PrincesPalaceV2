# Gap audit — The Fragile Lamb, handoff vs. built

Audited 2026-08-08, immediately after the implementation pass, against
`LAMB.md` in this folder. Companion to `GAP_AUDIT.md`, which covers the
Black Ram. Format per `docs/HANDOFF_TEMPLATE.md`.
Verdict ∈ **match** / **partial** / **missing** / **deliberate-deviation**.

Overall: **the path is complete.** All 21 nodes, all six strands, Wail, the
proposed capstone, and all six abilities are built and tested. Every open
decision in §11 is answered — two of them by the code refusing to let them
stay open.

Shawn now has two of three paths. The mage remains, per §12.

---

## Systems (§0 and §8)

| # | Handoff item | Built | Verdict |
|---|---|---|---|
| 1 | Authored the same way the Ram was, not a parallel system | Same `TalentEffectType` enum, same `effects[]` by member name, same `grantsSkillId` route for abilities | **match** |
| 2 | Fourteen (in fact fifteen) new enum members | **Sixteen**, because The Flock's spread split into two — see D8 below | **deliberate-deviation** |
| 3 | Ward damage bonuses fold into `ScaledAttack` before defense | Via `CombatantState.BonusAttackPercent`, and pinned by a test that checks the pre-defense figure specifically | **match** |
| 4 | `WardAlsoAppliesRegen` reads Threshold as turns | Accepted as the second exception, and it is still only the second — see D8 | **match** |
| 5 | Call the construct a **Ward**, everywhere | Content, code, log lines and the talent panel. `StatusEffectType.Shielded` is what implements it and is never the word shown | **match** |

## The engine (§3) and Ward mechanics (§4)

| # | Item | Verdict | Note |
|---|---|---|---|
| 6 | `WoolWhenWardedAllyHit`, +2 | **match** | |
| 7 | Narrowed to `Shielded` specifically | **match** | §11's highest priority. Without it Wail would have paid up to +6/turn for two turns on top of the wards |
| 8 | Once per ally, per turn | **match** | Cleared when the WARDER's turn begins, so "per turn" means a round of hers |
| 9 | A self-Ward pays | **match** | Assumed yes, built yes |
| 10 | Ward = `Shielded`, source Shawn, 2 wool | **match** | |
| 11 | Pops on the first hit | **match** | The second belt on the same infinite-loop problem, and tested from both ends |

## The six strands (§5)

Every tier is built. Numbers as authored unless a row says otherwise.

| # | Node | Verdict | Note |
|---|---|---|---|
| 12 | Fleece Ward T1/T2 — 40% / 60% | **match** | |
| 13 | Fleece Ward T3 — warding costs no action | **match** | The beat is still committed and played; only the turn does not advance |
| 14 | Mending Fleece T1/T2 — regen rider | **match** | Rides the ward, so the strand costs no extra action and no extra wool |
| 15 | Mending Fleece T3 — heal on spend | **match** | Healed by the CASTER's talent, on the WEARER's max health |
| 16 | Weight of Wool T1-T3 | **match** | Ceiling is party-size dependent — see "One number that moves" |
| 17 | Weight of Wool T3 — one turn of grace | **match** | Only a SELF-ward starts it; an ally's ward popping never earned 3x |
| 18 | The Flock T1 — one ally, half strength | **match** | |
| 19 | The Flock T2/T3 — whole party, half then full | **match** | Split into a rule plus a flag rather than a Threshold count — D8 |
| 20 | Wool Gift T1/T2/T3 | **match** | Gift: Haste lands on `TurnOrder.PullToFront`, the mirror of Headbutt's shove |
| 21 | Shatter T1/T2 — 100% / 175% of Attack | **match** | |
| 22 | Shatter T3 — triple on your own, plus Vulnerable | **match** | Undiluted, as instructed. No compensating rider, no smaller ward handed back |

## Convergence and capstone

| # | Item | Verdict | Note |
|---|---|---|---|
| 23 | Wail — 50% less damage, 2 turns, gated at 9 | **match** | |
| 24 | Wail repriced to 10 wool | **match** | On a 10-wool cap it empties the meter, which is the shape a panic button should have |
| 25 | Wail as party-wide `Protect`, no new status | **match** | Reads as an ally buff rather than an enemy debuff, which is worth knowing for the VFX brief |
| 26 | The Golden Fleece — wards never pop | **match** | And its balance note is already handled — see below |

## Tests (§10)

Every one the handoff asks for exists.

| # | Asked for | Built |
|---|---|---|
| 27 | A ward pays +2 exactly once even if struck twice in a turn | Yes |
| 28 | A `Protect` or `Regen` sourced by Shawn pays nothing | Yes — the test that stops Wail doubling as an income engine |
| 29 | Weight of Wool composes to its ceiling, and to 1x with none | Yes, driven through the real `RefreshAttackBonus` |
| 30 | The self bonus survives exactly one turn, then stops | Yes |
| 31 | Shatter with no wards deals nothing and spends nothing | Yes, driven through the real button |
| 32 | The Flock T3 wards everyone for the same 2 wool | Yes |
| 33 | Literal expected values, never a recomputed formula | Yes — e.g. Shatter's 520 is spelled out as 175% of 10 Attack, tripled, on the x10 scale |

Two additions worth naming. **The default squad is ONE character**, so every
ally-facing rule would have passed vacuously; the ally tests field a second
recruit deliberately. And the capstone's never-expiring ward is tested for
still reporting `WardedBy`, which is what keeps her income alive after it.

---

## Open decisions (§11), and what was done with each

**D1 — narrow the engine to `Shielded`.** Done. The enum member's own comment
now carries the reasoning, so the next person to widen it has to read why it
was narrowed first.

**D2 — Wail at 10 wool.** Confirmed and built.

**D3 — the ultimate.** Built as proposed: The Golden Fleece, wards never pop.

The handoff's balance note — "this removes the engine's income trigger, since
a ward that never pops never pays" — **does not apply to what was built**, and
that is not luck. The engine pays for CARRYING a ward when struck, not for one
breaking, because the once-per-turn cap already made "pays on every absorbed
hit" impossible. So a persistent ward pays once per combatant per turn, which
is exactly the fallback the handoff proposed if income collapsed. It cost
nothing to build it that way from the start.

**D4 — does a self-Ward pay?** Yes, as assumed.

**D5 — party size.** See below. This one moved.

**D6 — Ward base cost 2 wool.** Taken as proposed.

**D7 — how abilities are authored.** Answered by the Ram's build, which
happened after this question was written: `skills.json`, at an unlock level no
character can reach, named by `grantsSkillId` on the node that hands it over.
No new file, no new system.

**D8 — `WardAlsoAppliesRegen`'s Threshold-as-turns.** The handoff's rule is
"accept the second exception, or split the field if a third appears." **A
third appeared**: The Flock's spread wanted Threshold as *a count of allies,
0 = all*, which is neither a health percent nor a turn count.

Rather than splitting the FIELD — which would have meant renaming `Threshold`
across the whole vocabulary and re-authoring the Black Ram's seven health
gates — the Flock's **rule** was split, per the handoff's own convention #1:
`WardSpreadsToAllies` carries the strength percent, and a separate
`WardSpreadsToWholeParty` flag carries the reach. Two members, no third
meaning, and a two-valued thing expressed as the flag it always was. That is
why the count is sixteen new members rather than fifteen.

`WardAlsoAppliesRegen` therefore remains the second and last exception.

---

## Two things that moved

**One number: Weight of Wool's ceiling.** §5 assumes a party of three and a
4x cap. **The game's squad is one by default and two at most** —
`SaveData.BaseMaxSquadSize` is 1, and `extra_recruit_slot` adds one. So the
reachable ceiling today is **3.5x** (self 200% plus one ally at 50%), not 4x.
Nothing was changed to compensate: the rule is authored per ally and the cap
follows party size on its own, so a third slot would make 4x real without any
content edit. There is a test stating the squad cap so this is noticed rather
than rediscovered.

**One target: who a Gift lands on.** §9 leaves targeting unstated and §12 puts
screen layout out of scope — and the fight screen has **no ally-targeting mode
at all** (the party stage is hover-only, non-interactive). With a squad of at
most two, "an ally" is unambiguous, so a Gift goes to the first living party
member who is not the caster. **This needs a real picker the moment a third
party slot exists**, and it is the one place this path will not scale on its
own.

The same constraint shaped Ward, but there it improved things: Ward targets
**self**, which is the reading the Flock's own wording already implies ("also
applies to one ADDITIONAL ally") and the one Weight of Wool pays double for.
No picker needed, and no design invented to avoid one.

---

## A bug this found in the previous commit

Statuses applied by a skill were never attributed to their caster —
`ApplySkillStatus` passed no source. Harmless for the Ram, whose engine reads
damage rather than statuses, and **silently fatal** for the mage path
(`WoolPerStatusedEnemy`), which would have read zero forever with nothing
anywhere explaining why. Every call site hands over the actor now.

Separately, the Lamb's ward ability was first authored as `ward` — an id the
**owl already owned**. The runtime owner check refused the cross-character
grant correctly, so the node simply granted nothing and the ability never
appeared. Content validation now fails the build on that collision instead of
quietly deleting a talent's whole payload.

---

## Out of scope, and stayed out

The mage path's six strands and its name; the Black Ram's ultimate is now
built (see `GAP_AUDIT.md`) so that §12 line is stale; fusion; the other four
characters; screen layout and art; balance validation. Nothing here has been
played.

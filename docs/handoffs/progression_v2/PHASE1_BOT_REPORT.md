# Phase 1 bot report — three combat changes

Written against `e20f5ae1` (main, branch `prismatic-orb`). Answers
`PLAN_PROGRESSION_V2.md` §7 phase 1, (a)-(d). All four measurements are
pooled across the balance bot's five archetypes (`RandomLegal`,
`GreedyAggressive`, `GreedyDefensive`, `Lookahead2`, `ProtectTheFront`) on
the **Fresh** profile, 50 runs/cell -> 250 runs per arm, seeds 1-50 reused
identically across every archetype and every arm (`-Seed 1 -Runs 50`, script
default `DepthCap 40`). Every batch: **0 bugs, 0 determinism mismatches**
(`summary.json.bugs` / `.determinism.mismatches`).

Every command below is the exact one run, in order. `tools/build_content.ps1`
regenerates `Assets/_Project/Resources/Content` from the edited JSON in the
main tree (destructive: deletes the tree first) — required before a content
edit reaches the bot, since `tools/bot.ps1` robocopies the main tree's
`Assets/` (including the built SOs) into the shard copies, not the JSON
directly. After every run the edited JSON was restored from a byte-for-byte
backup (verified with `cmp`) and `git checkout -- Assets/_Project/Resources/Content/`
discarded the regenerated assets, since content rebuilds are not
byte-deterministic even with unchanged JSON (~68 files touch on a no-op
rebuild — GUIDs/timestamps, not content). `git status --short` is clean of
both at the end of this report (see §6).

**Instrumentation.** Three of the four questions need to know what a
character's resource pool held at the moment they acted, which nothing in
`RunTrace`/`BOT_SUMMARY_SCHEMA.md` records. Three small, additive fields were
added to `Domain/Bot/RunTrace.cs` (`TurnTrace.PrimaryPoolAfter`,
`.SignaturePoolAfter`, `.PoolTierFired`), populated in
`Domain/Bot/FightRunner.cs`, and serialized in
`Editor/Bot/BalanceBotRunner.cs`'s hand-written JSON writer — all three files
reverted to `HEAD` after the last run (see §6); nothing here was committed
except this report. Detail on why each was needed is in (b) and (c) below.

---

## (a) Bjorn's opening kit — Bellow moved off level 1

**Arm A (today):** Slam, Bellow (`placeholder_brawler_provoke`), Brace
(`placeholder_brawler_ward`) all `unlockLevel: 1` in
`Assets/_Project/ContentData/skills.json`.

**Arm B:** Bellow's `unlockLevel` set to `999`; Slam and Brace untouched.

```diff
   "id": "placeholder_brawler_provoke",
   "displayName": "Bellow",
   "characterId": "bear",
-  "unlockLevel": 1,
+  "unlockLevel": 999,
   "effect": "Provoke",
```

**Commands:**
```bash
# Arm A -- no content edit, already today's state
powershell -NoProfile -ExecutionPolicy Bypass -File tools/bot.ps1 -Runs 50 -Seed 1 -Profiles Fresh
# -> reports/bot/20260915-194852 (the canonical baseline batch this report reuses for a/b/c/d)

# Arm B
# (edit skills.json as above)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build_content.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/bot.ps1 -Runs 50 -Seed 1 -Profiles Fresh
# -> reports/bot/20260915-193004
```

| Metric | Arm A (today) | Arm B (Bellow @ 999) | Diff (B-A) |
|---|---|---|---|
| First-leg clear rate (leg-1 attempts = 250) | 90.8% [87.2, 94.4] | 91.2% [87.7, 94.7] | +0.4 pt |
| Avg party HP at end of leg 1 | 512.7 [485.6, 539.8] | 515.9 [489.0, 542.9] | +3.2 |
| Party deaths per 100 fights (proxy, see note) | 4.11 [3.52, 4.70] | 4.12 [3.53, 4.71] | +0.01 |
| Mean depth reached (steps, cap 40) | 30.03 [28.65, 31.41] | 29.96 [28.57, 31.35] | -0.07 |

Intervals are 95% Wilson (proportions) or normal-approximation mean±1.96·SE
(continuous), n=250 runs / n=4,403-4,393 fights as noted per row.

**"Bjorn's deaths per 100 fights" is not directly measurable.** Nothing in
`RunTrace` records per-character HP or per-character downed state — only
party-aggregate HP (`PartyHpIn/Out`, `PartyHpAfter`). The proxy used above is
the **party wipe rate per 100 fights** (a run ending in death, not capped,
scaled by total fights played), which is the closest available reading and
almost certainly undercounts true "Bjorn down" events, since a party can
carry a downed Bjorn through several more fights before the run actually
ends. Adding true per-character death tracking would mean carrying
per-combatant HP into `RoomTrace`/`FightTrace`, a materially bigger change
than the three additive pool fields used elsewhere in this report — flagged
for a follow-up, not attempted here.

**Trip-wire (plan: a drop of more than 3 points in first-leg clear rate):
not triggered.** The measured direction is a *slight increase*, not a drop,
and the change is within noise (CIs overlap almost completely on every
metric). Moving Bellow off level 1 reads as **near-zero measured impact** on
early-game survival at bot-population scale. This is consistent with what
Bellow actually is: `Provoke` deals no damage and heals nothing (the skill
schema's own "whole cost is the turn" carve-out), so an archetype that
already has Slam and Brace loses a redirect-aggro option, not stopping
power — and the bot policies evidently do not lean on it heavily enough for
its removal to move survival or depth at this sample size.

---

## (b) Shawn's Wool absorb (level-60 track node, tested early)

**Arm A (today):** `Assets/_Project/ContentData/characters.json`, sheep row,
`signatureAbsorbsDamage: false`.

**Arm B:** `signatureAbsorbsDamage: true`. Confirmed via
`Core/Content/ContentDatabase.Effective.cs:301-328` and
`Domain/Combat/ResourcePool.cs:91-117,256-276` that this flag is the whole
switch (OR'd with the track's own `SignatureAbsorbs` unlock, never replaced),
`AbsorbPerPoint` is 1 (1 damage per Wool banked), and `Absorb()` fires
automatically inside `TakeDamage`, ahead of health, on every hit — no player
action involved, matching the brief exactly.

```diff
   "signatureGainOnDamageTaken": 0,
-  "signatureAbsorbsDamage": false
+  "signatureAbsorbsDamage": true
```

**Commands:** same shape as (a); Arm A reuses `20260915-194852`.
```bash
# (edit characters.json as above)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build_content.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/bot.ps1 -Runs 50 -Seed 1 -Profiles Fresh
# -> reports/bot/20260915-194157
```

**Why the instrumentation was necessary.** "Share of Shawn's turns where
Shear was unaffordable" needs to know his Wool bank at decision time, which
no existing trace field carries. `TurnTrace.SignaturePoolAfter` (Wool, after
this command) was added for it. Wool has no idle decay authored on the sheep
row, only `signatureGainPerTurn: 1`, ticking at the **start** of Shawn's own
turn, before his action — confirmed against a concrete trace (seed 1,
RandomLegal, fight step 3: recorded Wool 1,2,3,4,5, then a Shear cast records
3 (5+1-3), the next Shear records 1 (3+1-3)). So "Wool available this turn"
= previous turn's recorded value + 1, every turn including the first (which
starts from 0). A turn that actually cast Shear is additionally treated as
proof affordability was fine that turn regardless of the computed value, to
remove the one-sided error that would otherwise call an affordable turn
"starved"; a small residual noise the other way (24/13,078 and 7/15,693
`Skill:woolgathering` casts still landing in the "starved" bucket after that
correction, ~0.05-0.2%) is not correctable from the trace and is negligible
against the effect size below.

| Metric | Arm A (today) | Arm B (absorb on) | Diff (B-A) |
|---|---|---|---|
| Party deaths per 100 fights (proxy) | 4.11 [3.52, 4.70] | 4.44 [3.82, 5.07] | +0.33, CIs overlap |
| Avg **party** HP at fight end (proxy, see note) | 802.0 [791.5, 812.4] | 789.7 [779.2, 800.3] | -12.2, CIs barely overlap |
| **Shawn's Shear-starved turn share** | 41.2% [40.7, 41.8] (n=31,708 Shawn turns) | 55.3% [54.8, 55.9] (n=28,358) | **+14.1 pt** |
| Win rate (fight-level) | 95.9% [95.3, 96.5] | 95.6% [94.9, 96.2] | -0.3 pt, CIs overlap |
| Shawn's damage share | 28.2% (635,179 / 2,251,628) | 28.0% (584,760 / 2,087,026) | -0.2 pt |

**"Shawn's deaths per 100 fights" and "average Shawn health at fight end"
are not directly measurable**, for the same reason as (a): no per-character
HP in the trace. The rows above are the **party-level** proxies (party wipe
rate, party HP at fight end), not Shawn-specific — flagged, not silently
substituted.

**Trip-wire (plan: starvation share over a fifth of his turns, or deaths
rising): fires, and it already fires at baseline.** The 20% threshold is
blown through by both arms — Shawn is Shear-starved on **41%** of his turns
*today*, before this change touches anything. Turning absorb on pushes that
to **55%** (+14 points, a 6-sigma move against the CI widths — not noise),
because every point Absorb spends out of the bank is a point Shear cannot
spend afterward, and the two mechanics compete for the same resource by
construction (the row's own comment already names this: "the bank-or-spend
tension is real for the rest of the character's life"). Deaths trend up
(+0.33/100 fights) and party HP at fight end trends down (-12), but neither
is distinguishable from noise at n=250 runs/arm — a larger arm (500+
runs/arm) would be needed to confirm or rule out the death/HP-loss reading;
the starvation share does not need a larger sample, its interval is already
tight and the two arms plainly do not overlap.

Per the plan's own reject rule, this reading argues for evaluating the
**Tuck In fallback** (a 0-cost skill converting up to 4 banked Wool into a
ward, not automatic drain) rather than shipping automatic absorb as
specified — though the fact that the *current, absorb-off* game already
starves Shear on 41% of Shawn's turns is arguably the bigger, pre-existing
finding here and worth its own look independent of this track node.

---

## (c) Bjorn's Fury opening and gain

**Arm A (today):** `Assets/_Project/ContentData/pools.json`, fury row,
`gainOnAttack: 15`, `startRule: "Zero"`, `startValue: 0`.

**Arm B, as specified, is not reachable without a second, larger change.**
`startRule: "Value"` with `startValue: 50` (0-100 capacity, so 50 is a valid
`Value` reading per `PoolEntryResolver.cs:150-162`) is schema-legal on its
own, but pools.json's own comment on the fury row says why it cannot be
changed alone: *"a Zero-start pool means he opens every fight unable to pay
for anything... his slam and his brace are manaCost 0 today"*.
`SkillEntryResolver.cs:234-263` enforces this as a build-time rule: a
`playerSelectable` skill that costs nothing is refused UNLESS its owner is in
`zeroStartPoolOwnerIds` (characters whose primary pool's `StartRule ==
Zero`). Moving fury off `Zero` removes Bjorn from that set, and
`ContentBuilder` refuses the build — confirmed twice, reproducibly, by
running `tools/build_content.ps1` with only the `startRule`/`startValue`
edit changed (`gainOnAttack` left alone): both times it failed with
`ContentBuilder generated 18 invalid content item(s)`, the first logged error
being every enemy/talent reference to a skill downstream of Bjorn's own
failed entries reading as "unknown skill id" (Slam/Brace fail their own
"costs nothing" check first, and nothing after that point in the skill list
ends up in the resolved catalogue). Restoring `startRule` to `Zero` alone
(keeping `gainOnAttack` however it was) rebuilds cleanly every time — isolated
and reproduced this precisely.

So per the plan's own fallback clause, Arm B actually run is **`gainOnAttack:
25` alone**, `startRule`/`startValue` left at `Zero`/`0`:

```diff
   "gainPerTurn": 0,
-  "gainOnAttack": 15,
+  "gainOnAttack": 25,
   "gainOnDamageTaken": 10,
```

**Commands:**
```bash
# (edit pools.json as above)
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build_content.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/bot.ps1 -Runs 50 -Seed 1 -Profiles Fresh
# -> reports/bot/20260915-195031
```

**Why the instrumentation was necessary, and why it needed a second field.**
The first attempt used `PrimaryPoolAfter` (Fury) the same way as Wool above
(previous turn's value + known deltas), but Fury has
`decayPerIdleTurn: 10, decayUnless: "Damage"` — the previous turn's recorded
value is *not* "what was available at this cast" whenever something drains
the pool between two of Bjorn's own turns without him spending it, and that
cannot be reconstructed from a single per-turn snapshot. Rather than publish
an approximate split, `TurnTrace.PoolTierFired` was added, reading
`CombatBeat.PoolTierDamageMultiplier` directly off the engine's own tier
decision (`FightSession.Beats.RecordPoolTier`) via the previously-unclaimed
`FightSession.DrainBeats()` (used today only by `FightController.Input.cs`
and EditMode tests — no conflict with the live game). This is exact, not
inferred.

| Metric | Arm A (today) | Arm B (gain 25) | Diff (B-A) |
|---|---|---|---|
| Share of Bjorn's turns firing the x4 Slam tier | 0.38% [0.31, 0.46] (112/29,135) | 0.59% [0.50, 0.67] (170/29,049) | +0.21 pt |
| Share of Bjorn's turns firing the x2 Slam tier | 17.7% [17.3, 18.2] (5,164/29,135) | 29.9% [29.4, 30.4] (8,689/29,049) | **+12.2 pt** |
| Win rate (fight-level) | 95.9% [95.3, 96.5] | 96.0% [95.5, 96.6] | +0.1 pt, CIs overlap |
| Bjorn's damage share | 24.2% (544,171 / 2,251,628) | 26.1% (603,251 / 2,313,870) | +1.9 pt |

**Trip-wire (plan: x4 share over a third): not triggered, by a wide
margin.** x4 stays under 1% of Bjorn's turns in both arms — the tier that
matters for the trip-wire is barely reachable at all with `gainOnAttack`
alone, because `Zero`-start plus a 10/idle-turn decay plus a 100-Fury
threshold means reaching the cap needs several consecutive attack turns with
no idle gaps, which the bot archetypes rarely sustain. **This is itself the
notable finding**: the specified change (`gainOnAttack: 25` **and**
`startRule: Value 50`) was explicitly meant to make the x4 tier and the
opening turn meaningfully more reachable, and the opening-value half of that
— almost certainly the larger lever of the two, since it removes the empty
opening turn entirely — could not be tested without also pricing Slam/Brace
away from `manaCost: 0` (a genuine design decision, not a one-line content
tweak). The x2 tier, reachable purely through faster accumulation, **does**
move a lot (+12 points) and is worth reading on its own even though it
carries no stated trip-wire.

**Recommendation for the owner:** deciding the Fury opening value needs a
companion decision on what Slam/Brace cost once Bjorn no longer opens every
fight empty (the pools.json comment already flags this as the next step of
the same balance pass). A second, narrower phase-1b — author a small nonzero
Fury cost for Slam/Brace alongside `startRule: Value 50` — would let the x4
share and the opening-turn question actually be measured together, the way
the row's own comment says they were always meant to be.

---

## (d) Average enemy hit per leg, vs. the "+40 health = one hit at leg 2" claim

Computed from `reports/bot/20260915-194852` (Arm A / today, all 250 runs, no
content change) by walking every `TurnTrace` in every `FightTrace` at
`Floor` 1-5 and taking the positive `PartyHp` delta between consecutive
commands (`PartyHpIn` for a fight's first command) as one enemy exchange's
damage. This is "damage taken from the enemy reply this command resolved,"
which equals a single enemy's hit only in a 1-enemy fight; a 2-enemy fight
(Elite rooms, ~1.5 enemies/fight mean per the plan) can fold two hits into
one measured delta. Both readings are given: **pooled** (every fight) and
**solo-only** (`EnemyIds` has exactly one entry), the latter being the honest
"one hit" number.

| Leg (floor) | n (pooled) | Mean, pooled | Median, pooled | n (solo) | Mean, solo | Median, solo |
|---|---|---|---|---|---|---|
| 1 | 5,338 | 15.0 | 8 | 1,558 | 22.9 | 15 |
| 2 | 6,696 | 18.7 | 14 | 2,271 | 23.2 | 15 |
| 3 | 4,971 | 23.9 | 17 | 1,894 | 29.5 | 17 |
| 4 | 8,273 | 25.9 | 17 | 3,685 | 29.9 | 16 |
| 5 | 6,259 | 28.3 | 18 | 2,894 | 31.4 | 17 |

**The plan's claim needs a leg-2 hit of about 20.** Measured leg-2: pooled
mean **18.7** (close), pooled median **14** (under), solo mean **23.2**
(over), solo median **15** (under). The claim sits inside the spread across
these four readings rather than being clearly right or wrong — the pooled
mean is the closest single match to "about 20," but the solo-only mean (the
cleaner single-hit reading) runs about 16% hotter than the claim assumes.
Read plainly: **+40 health is not quite a guaranteed extra hit at leg 2** in
the solo case (a hit that size eats most but not all of the 40), and is
closer to exactly one extra hit if the player is regularly fighting two
enemies at once by then (which the pooled numbers, dominated by Elite rooms'
double hits, partly reflect). The claim should be read as "roughly," not
"about 20" taken literally — retracting it outright would overstate the
gap, since three of the four readings land within about 25% of 20.

---

## What could not be measured, in one place

- **Per-character deaths and per-character HP at fight end** (asked for
  Bjorn in (a) and Shawn in (b)): unavailable from the trace as it exists
  today; only party-aggregate HP is recorded. Proxied by the party wipe rate
  and party HP at fight end throughout, both clearly labeled where used.
  Adding true per-combatant HP would be a materially bigger trace change
  than the three additive fields used here (RoomTrace/FightTrace would need
  a new per-character breakdown, not one more int field) and was not
  attempted.
- **Arm B for (c) as literally specified** (`gainOnAttack 25` *and*
  `startRule Value 50` together): breaks content validation for an unrelated
  reason (Bjorn's zero-cost Slam/Brace carve-out is keyed off Fury's
  `startRule == Zero`). Ran `gainOnAttack: 25` alone per the plan's own
  fallback clause; see (c) above for the full mechanism and a suggested
  follow-up.

## Instrumentation reverted

`git diff --stat` against `HEAD` for
`Assets/_Project/Scripts/Domain/Bot/RunTrace.cs`,
`Assets/_Project/Scripts/Domain/Bot/FightRunner.cs`, and
`Assets/_Project/Scripts/Editor/Bot/BalanceBotRunner.cs` is empty as of this
report; all three were `git checkout`'d back to `HEAD` after the last bot run
in this session. `Assets/_Project/ContentData/{skills,characters,pools}.json`
are byte-identical to their pre-session state (verified with `cmp` after
every restore) and `Assets/_Project/Resources/Content/` is clean against
`HEAD` (`git checkout`'d after every content rebuild). `git status --short`
at the end of this session shows the same pre-existing Art/Fonts/
`FightController.StageVisuals.cs`/`tools/remove_portrait_backgrounds.py`/
untracked-folder diff the session started with, plus this report — nothing
else.

# Per-character reward tracks

One track per character, authored as content, every reward derived from what the
character has **collected** rather than from what the squad has **reached**.
Nothing on the track is an over-arching modifier any more.

> **2026-09-16 addendum, not a correction to the plan below.** `TrackReward.
> SignatureAbsorbs` (the old boolean this plan's own P3 sections describe
> being replaced by `SignatureAbsorbPerPoint`) and its resolver-side alias
> are deleted as of this date -- `reward_tracks.json` never authored the old
> name, so nothing depended on the alias staying parseable. Every mention of
> `SignatureAbsorbs` below is left as written, describing the plan as it was
> designed and built.

---

## 1. The measurement that decides the design

The screen bakes milestone-ness into the tree at build time, at nine separate
places per node:

| Baked at build time | Where | What it keys off |
|---|---|---|
| disc diameter 26 vs 44 | `RewardTrackScreen.cs:608`, `RewardTrackLayout.cs:72-73` | `IsMilestone(level)` |
| ring sprite `proc:ring_hairline` vs `proc:milestone_ring`, and its diameter | `RewardTrackScreen.cs:658-666` | same |
| the breathing aura node exists at all | `RewardTrackScreen.cs:617-627` | same |
| caption font 11 vs 16 | `RewardTrackScreen.cs:773` | same |
| level-number font 15 vs 22 | `RewardTrackScreen.cs:786-788` | same |
| seal-pip offset | `RewardTrackLayout.cs:200-201` | `DiameterOf(level)` |
| a ribbon **tick** is emitted only for a non-milestone level | `RewardTrackScreen.cs:1077-1088` | `IsMilestone(level)` |
| a ribbon **dot + ring + number** only for a milestone level | `RewardTrackScreen.cs:1100-1130` | `MilestoneLevels()` |
| the card's art sprite, one per level | `ScreenRegistry.cs:925-929` (`cardArtByLevel`, 99 entries) | `CardArtFor(level)` |

A scene is emitted once and shared by all three copies of the system menu
(`ScreenRegistry.cs:121-122`, `:514-515`, `:772-773`). **If milestone LEVELS
varied per character, every one of those nine would have to become a runtime
decision** — sprite swaps, font-size writes, node activation, and two
differently-shaped ribbon collections — and `UiAudit` would be measuring a
layout no character actually sees.

**Decision: the milestone CADENCE is shared and fixed; the milestone CONTENT is
per-character.** The twelve levels stay exactly the twelve
`RewardTrack.cs:116-155` already uses:

> **10, 20, 25, 30, 40, 45, 50, 60, 70, 80, 90, 100**

Consequences, all of them good:

- Not one geometry constant in `RewardTrackLayout.cs` moves.
- `RewardTrackLayoutTests.ThereAreTwelveLandmarks`, `AMilestoneLevelDrawsLarge`,
  `FillerDoesNotDrawLarge`, `EveryLevelFitsOnTheRibbon`,
  `TheSealPipSitsOnTheRimAtBothSizes` are all unchanged and stay green.
- 99 nodes, 12 milestone, 87 filler — measured, not assumed:
  `FirstLevel = StartingLevel + 1 = 2` (`RewardTrackLayout.cs:28`),
  `NodeCount = MaxLevel - FirstLevel + 1 = 99` (`:29`). Decision 2 in the brief
  is correct as written.
- Authoring becomes a fill-in-the-blanks exercise: twelve slots that must each
  hold something, plus a filler mix that must sum to 87.

The three things that DO become runtime, and they are cheap:

| Was | Becomes |
|---|---|
| `TrackIcon{level}` sprite keyed at build (`RewardTrackScreen.cs:723-730`) | controller writes `icons[i].sprite` from a per-**kind** array |
| `cardArtByLevel` — 99 sprites by level (`ScreenRegistry.cs:925-929`) | `cardArtByReward` — one sprite per `TrackReward` |
| `MatTintFor(level)` | `MatTintFor(TrackReward)` — already painted at runtime (`RewardTrackController.cs:292-296, 529`) |

### The second measurement: no reward name grows

`NodePitch = 190` was sized from the longest name the track can say — `"YOUR
SECOND LIFE RETURNS AT EVERY BOSS"`, 38 characters (`RewardTrackLayout.cs:50-56`),
and `NextRewardWidth = 420` from the same string measuring ~320px
(`:405-412`). Every new name is shorter: `"A SECOND LIFE, ONCE PER RUN"` (27) is
the longest survivor, and the new kinds top out at `"+20% LIGHTNING DAMAGE"`
(21), `"LEARN LIGHTNING BOLT"` (20) and `"WOOL ABSORBS DAMAGE"` (19) — the
twelve templates are written out in §5's caption table. **No caption, pitch or
summary constant moves, and captions get easier rather than harder.**

### The third measurement: no new art

> **Superseded by progression v2 phase 5, 2026-09-15.** The table below is
> kept as the record of what P3 planned. What shipped is different in kind: a
> card medallion now exists only where the talent tree's painted set contains
> one that HONESTLY means the reward (nine of twenty-one), and the other
> twelve draw a short word in the kit's own type instead. The "PLACEMENTS,
> not decisions" posture this section leans on is what phase 4 then used to
> borrow seven flat-cel status icons onto a card of painted gold medallions,
> four of them admittedly arbitrary -- so phase 5 replaced the rule that
> forced it (a distinct SPRITE per kind) with a distinct VISUAL per kind.
> Live mapping and the reasoning per medallion: `RewardTrackLayout.
> CardArtKeyFor` / `CardGlyphFor`.

Twelve reward kinds after the change, and exactly twelve card-art keys already
declared at `RewardTrackScreen.cs:191-202`. The mapping is a re-point of
constants, not a commission (and that block's own header already says these are
"PLACEMENTS, not decisions"):

| New kind | Key constant to reuse | freed from |
|---|---|---|
| StatPoint | `StatArtKey` | — |
| MaxHealth | `HealthArtKey` | — |
| Respec | `RespecArtKey` | — |
| SecondLife | `SecondLifeArtKey` | — |
| SignatureCapacity | `RelicArtKey` → rename `SignatureCapacityArtKey` | StartingRelics |
| SignatureGainPerTurn | `RestArtKey` → `SignatureGainArtKey` | RestBeforeBoss |
| SignatureGainOnDamageTaken | `ChosenRelicArtKey` → `SignatureHurtArtKey` | ChosenStartingRelics |
| SignatureAbsorbs | `OfferArtKey` → `SignatureAbsorbArtKey` | WiderOffer |
| ElementalDamagePercent | `ExpArtKey` → `ElementalArtKey` | ExpFind |
| MaxMana | `FavorArtKey` → `MaxManaArtKey` | Favor |
| ManaRegen | `RerollArtKey` → `ManaRegenArtKey` | OfferReroll |
| UnlockSkill | `SecondLifeRefreshArtKey` → `UnlockSkillArtKey` | SecondLifeRefresh |

Rail marks likewise reuse the four existing procedural bakes plus the ring
default (`RewardTrackLayout.IconFor`, `:935-946`): `proc:track_stat`
(StatPoint), `proc:track_health` (MaxHealth), `proc:track_exp`
(ElementalDamagePercent), `proc:track_favor` (MaxMana, ManaRegen),
`proc:ring_outline` (everything else). **No `ProceduralSpriteBaker` change and
no new PNG.** `MatTintFor` (`:986-996`) has the same four-plus-default shape and
takes the same treatment: StatPoint/MaxHealth keep their hexes, ExpFind's
`#C8B4DE2E` becomes ElementalDamagePercent's and Favor's `#F2DB9E2E` becomes
MaxMana/ManaRegen's, everything else falls to the default `#C8B4DE1F`.

---

## 2. The model: one grant, everything else derived

Today the track has four grants writing four save fields
(`Character.cs:106, 133, 153, 164`) and eight unlocks derived from `level`.
The new model has **one** grant.

> **`StatPoint` is the only grant. Every other reward is a total summed over
> the entries at levels `<= claimedTrackLevel`, read at its own read site.**

Stat points must be stored because the player *spends* them (and, after
package 7, refunds them). Nothing else is spent, so nothing else needs storage
— and storing it is strictly worse, because a stored copy can disagree with the
definition after a retune.

What this buys, in order of value:

1. **Decision 4 falls out for free.** Respec and SecondLife derive from
   `claimedTrackLevel` because *everything* does. There is no second rule to
   forget.
2. **Three save fields die**: `earnedFavor` (`Character.cs:133`),
   `bonusExpPermille` (`:164`), `bonusMaxHealth` (`:153`). Max health becomes a
   term in `ContentDatabase.EffectiveStats`, which is where every other
   contribution to max health already lives.
3. **One summation rule.** Today `RerollsPerRun` accumulates
   (`RewardTrack.cs:375-376`) and `StartingRelics` replaces (`:416-417`), with a
   comment admitting the pair exists because getting it the wrong way round is
   silent. Every new kind **accumulates**. Authored capacity steps are
   therefore written `+5`, `+10`, never `15`, `20`.
4. **`unlockedSkillIds` gains no second writer.** `UnlockSkill` is answered by
   `AvailableSkillsFor` asking the track, so the hazard
   `Character.cs:26-33` warns about (a respec stripping an event-taught spell)
   never comes up.

`Character.ClaimTrackRewards` shrinks to:

```
unspentStatPoints += track.GrantedBetween(StatPoint, claimedTrackLevel, throughLevel);
claimedTrackLevel = throughLevel;
```

`RewardTrack.IsGrant` stays, with one member, and its comment says why: the
grant/unlock split is what makes the watermark mean anything, and a second
grant added without noticing would be paid twice.

### Where each reward is read

| Kind | Read site | How |
|---|---|---|
| StatPoint | `Character.ClaimTrackRewards` (`Character.cs:374-405`) | the one grant |
| MaxHealth | `ContentDatabase.EffectiveStats` (`ContentDatabase.Effective.cs:157-228`) | replaces `total.maxHealth += character.bonusMaxHealth` at `:213` |
| Respec | `TalentController.cs:340, 379, 414` | `track.CollectedTotal(Respec, claimed) > 0` |
| SecondLife | `RunOrchestrator.cs:317` | sum over the fielded squad (see §6) |
| SignatureCapacity / GainPerTurn / GainOnDamageTaken | `ContentDatabase.BuildSignatureResource` (`ContentDatabase.Effective.cs:236-285`) | added beside the talent terms at `:264-272` |
| SignatureAbsorbs | same, at the constructor's last argument, `:284` | `authored \|\| collected > 0` |
| ElementalDamagePercent | `ContentDatabase.ModifierEffects` (`ContentDatabase.Effective.cs:340-360`) | append ONE effect per element, **routed by the character's own `attackType`** — see "the routing rule" below |
| MaxMana | `ContentDatabase.EffectiveMaxMana` (`:541-559`) | `+ collected`; reaches the fight at `FightEncounterAdapter.cs:171` |
| ManaRegen | `EffectiveStats.manaRegen` (base is `AbilityDerivation.ManaRegenBonus`, WIS/4, `AbilityDerivation.cs:130-135`) | `+ collected`; reaches the fight at `FightEncounterAdapter.cs:175` |
| UnlockSkill | `ContentDatabase.AvailableSkillsFor` (`ContentDatabase.cs:236-322`) | a **fifth** route beside `LearnedThisRun`, which the code already calls the fourth (`:272`) |

**Every one of those six `ContentDatabase` methods takes a `Character`, not a
`CharacterDefinition` and not an id** — checked signature by signature:
`EffectiveStats(Character)` (`Effective.cs:157`), `BuildSignatureResource(Character)`
(`:236`), `ModifierEffects(Character)` (`:340`), `EffectiveAbilityScores(Character)`
(`:532`), `EffectiveMaxMana(Character)` (`:541`), `AvailableSkillsFor(Character)`
(`ContentDatabase.cs:236`). So `claimedTrackLevel` is in hand at every read site
and nothing has to be threaded in. The one definition-only skill route,
`SkillsUnlockedByLevel(definition.id, level)`, is `FightEncounterAdapter.cs:614`'s
no-Character overload — it stays a pure level ladder and gains no track branch,
which is correct: it has no watermark to read and no character to read it off.

`ElementalDamagePercent` is the seam with the most detail behind it, and one
part of the brief's reading of it is wrong.

- `ModifierEffects` reaches combat at exactly one place,
  `FightEncounterAdapter.cs:233` (`state.ModifierEffects = ...`). It has a
  second production caller, `ItemOfferRoll.cs:105`, which asks only
  `.Best(FortunateFavorBonusFlat)` — a track-appended elemental effect is a
  different `Type` and cannot disturb it.
- `FightSession.ApplyModifierOnHitRiders` (`FightSession.cs:464-560`) walks
  `.All` (`:484`), not `.Best`, so a track-granted +10% Fire **stacks with** a
  Fiery affix instead of collapsing against `ModifierEffectSet`'s MAX-not-SUM
  rule (`ModifierEffectSet.cs:20-25, 86-98`).
- **But the rider has two branches and only one of them is a percentage of the
  hit.** `hitType` is `AttackTypeOf(actor)` — the character's own authored
  `attackType` (`FightSession.Ledger.cs:213-214`), which for a skill cast is
  still the caster's type and never the spell's element
  (`FightSession.Skills.cs:558-563` says so explicitly). When the reward's
  element EQUALS that type, `:510-521` applies `Magnitude%` to `damage`, the
  hit's own landed figure — "this hit should have been 10% bigger", which is
  what the reward means. When it does NOT, `:535` applies `Magnitude%` of the
  actor's **Attack**, floored at 1, as a separate typed tick.

That distinction decides which characters an elemental node is worth anything
to **through the existing rider**. Shawn's `attackType` is `Nature` and
Odette's is `Arcane` (`characters.json`), so a Nature line on Shawn and an
Arcane line on Odette are same-element and scale with everything they do
through `ElementalDamageOnHitPercent`. **A Fire, Ice or Lightning line on
Odette is not** — it takes the foreign branch on every landed hit regardless
of what she cast, and pays `Attack × n%` — a flat few points that has nothing
to do with her Firebolt, if that rider were the only seam available.

**Round 2, Part A: it is not the only seam.** The owner asked for elemental
damage mods on Odette the elementalist, and the hook that pays her spells
their own element exists a few lines below the rider above, inside the same
file — see the new package **P4b** and the redesigned §5. `ElementalDamagePercent`
(new `ModifierEffectType` member, distinct from `ElementalDamageOnHitPercent`)
is read inside `ResolveDamageInstances` (`FightSession.Skills.cs:619-649`)
against `instance.type` — the packet's own element, not the caster's
`attackType` — so a Fire node now scales a Fire packet regardless of whose
`attackType` is what. §5 restores Fire/Ice/Lightning/Arcane accordingly.

### The routing rule (round 3)

**Round 3: the packet hook alone is not enough either, and using it for every
element would have made Shawn's whole damage line invisible.**
`ResolveDamageInstances` runs only for a skill that authors `damageInstances`
(`FightSession.Skills.cs:350`, `:481` — the branch is on the skill, not the
caster). Shawn's entire kit authors exactly ONE: `cinderfault` (`Fire 10 +
Nature 10`), and his track does not hand it over until level 70. `shear`,
`battering_ram`, `mud_burst`, `static_fleece`, `golden_fleece`, every talent
ability and every plain swing carry no instances at all and never reach that
method. A Nature line paid only through the packet hook would therefore have
been worth nothing to him for 68 levels and then half of one spell — which is
what the round-2 draft shipped, unnoticed, because nobody computed where the
filler landed.

> **An elemental reward is appended as `ElementalDamageOnHitPercent` when its
> element EQUALS the character's authored `attackType`, and as
> `ElementalDamagePercent` otherwise. Exactly one effect per element per
> character; never both.**

That is the whole rule, and it falls out of what the two hooks can see:

- **Same element as `attackType`** — this is the character's every landed hit,
  swing and cast alike, and a swing has no damage instances for the packet hook
  to multiply. The rider's same-element branch (`FightSession.cs:510-521`)
  applies `Magnitude%` to `damage`, the hit's own landed figure, which is
  exactly "this hit should have been n% bigger". Shawn's Nature and Odette's
  Arcane take this branch.
- **Any other element** — the only place a foreign element exists at all is a
  spell packet, and that is precisely what P4b's hook reads. Odette's Fire, Ice
  and Lightning take this branch.

Read at `ContentDatabase.ModifierEffects` off
`GetCharacter(character.definitionId)?.Data.AttackType` (`ContentDatabase.cs:345`,
the accessor `FightEncounterAdapter.cs:619, 665` already uses); a null
definition falls through to the packet hook, which is the graceful-degradation
direction (a foreign-element effect on a character we cannot identify is inert,
not doubled).

**One consequence to say out loud rather than let a player discover:** because
the rider fires on every landed hit with `hitType = AttackTypeOf(actor)`,
Odette's Arcane line pays on her Fire casts too, while her Fire line pays only
on Fire packets. Her four lines are not four equal things — Arcane is broad and
the other three are specialisations. That is the engine's existing model (an
`Astral` affix behaves the same way today, and `FightSession.cs:491-509`'s own
bug note is where it was settled), not something this plan introduces, and the
track is authored rather than chosen, so there is no investment decision it
distorts.

---

## 3. Collisions with the brief, and how each resolves

The brief's survey is right about most things. Five places where it is not:

**a. `RestBeforeBoss` is not name-only.** It is written into the run at
`RunManager.cs:160` and threaded through the whole map generator —
`DescentMap.cs:218, 249, 267, 298, 318-320, 346, 367, 484-486, 503, 558-570,
596-636` — with its own test surface (`DescentLegTests`,
`DescentRoadVariationTests`).
**Resolution:** delete the `TrackReward` member and the assignment at
`RunManager.cs:160` only. `RunSnapshot.restBeforeBoss` (`:157`) and the entire
generator parameter stay, defaulting false, with a one-line comment at the
field saying nothing grants it today. Ripping the parameter out of the
generator is a separate change with a separate test sweep and no reward-track
value.

Three tests in `RunManagerTests` sit on this rule, not one:
`ALevelThirtySquadDescendsWithTheRestGuaranteed` (`:454-466`) is deleted whole
— the plan previously named only its assertion line;
`AnUnlevelledSquadGetsNoGuaranteedRest` (`:445-452`) stays but becomes a
**vacuity risk**, since nothing can now set the flag, so its comment must say
it is pinning that nothing grants it rather than that level 1 has not earned
it; and `LevellingMidDescentDoesNotReshapeTheLegUnderThePlayer` (`:475-489`)
stays with its `LevelTheSquadTo(30)` no longer able to change the input — it
still pins the snapshot mechanism, and its header comment must stop describing
the level-30 rule as live.

**b. `SecondLifeRefresh` is not name-only either.** `RunManager.cs:294-296`
clears `run.secondLivesUsed` on entering a boss room. That block goes with the
member.

**c. `OfferReroll` is a shipped UI control, not just a number.** Seven sites,
not four: `ReckoningController` fields at `:66-68`, the listener at `:216`,
`Reroll` (`:467-491`), `PaintReroll` (`:494-510`), the repaint call at `:630`,
`RerollSource`'s assignment at `FightController.Input.cs:497`, the tree node at
`ReckoningScreen.cs:162, 274-280`, and its wiring at `ScreenRegistry.cs:1016`.
The button is already hidden when the allowance is zero (`:505`), so leaving it
would be dead-but-invisible; that is worse than deleting it, because it is a
control nothing can ever reach.
**Resolution:** delete the control and its tree node in the same package.
`RunSnapshot.offerRerollsUsed` stays as a field (JsonUtility ignores it) —
removing it would drop the value from old in-flight runs for no gain.
**`ShopController`'s reroll (`:321, 369-372`) is a different, gold-priced
control and must not be touched** — a grep for "reroll" reaches it.

**d. `WiderOffer` changes the Reckoning's tree width.**
`OfferRowLayout.MaxCards` (`:72`) is "the widest row the track can ask for" and
the tree emits that many cards. Removing the reward drops it 4 → 3
(`ItemOfferTable.OfferCount`, `ItemOffer.cs:69`). **That is a screen-tree
change and forces `-BuildScenes` on package 1.**

**e. There is no gold modifier on the track.** Grepped: the only gold reference
in `RewardTrack.cs` is the comment at `:182-185` explaining why gold was
*rejected* as a filler kind. Nothing to remove.

**e2. `StartingRelics` has a live fallback that must survive it.**
`SquadTrack.StartingRelics()` has one caller, `RunOrchestrator.cs:170`
(`DraftHasAnotherRound`), and `RunOrchestrator` is production, not bot-only —
its own header says "THE RULES OF A RUN, IN ONE PLACE, WITH TWO CALLERS", and
`HubController.cs:433` is the first of them. It answers `UnlockedAmount(...,
fallback: RewardTrack.BaseStartingRelics)`, i.e. **1** below level 25. Deleting
`BaseStartingRelics` with nothing in its place would leave the draft count
undefined, and replacing it with `RelicPool.OfferCount` — which is how many
CARDS one round shows, not how many ROUNDS there are — would silently make
every descent start with three relics.
**Resolution:** move the number, do not delete it. A
`public const int StartingRelicsPerDescent = 1;` on `RelicPool` beside
`OfferCount` (`RelicPool.cs:43`), read by `DraftHasAnotherRound`. Shipped
behaviour is unchanged for a character below level 25, which is every character
on a migrated save (§9). `RunOrchestrator.cs:86`'s `ChosenStartingRelics` branch
goes with the member, so the draft is always the weighted three rather than the
whole catalogue — state that as the behaviour change it is.

Two more collisions, from the new design:

**f. `unlockedSkillIds` is gated on skill authorship.**
`ContentDatabase.cs:316` requires `s.Data.CharacterId == character.definitionId`.
Every book-only skill in the game is authored `characterId: "sheep"` — **six**,
not four: `static_fleece`, `golden_fleece`, `mud_burst`, `frost_flare`,
`cinderfault`, `lightning_bolt` — so Odette's track could not unlock any of
them. The comment immediately above, at `:280-297`, already argues the general
case in exactly those terms (*"every one of the six book spells carries
characterId 'sheep'"*, *"Who it was AUTHORED against is an artefact of where it
was first written down"*).
**Resolution:** the track's skill branch carries **no** ownership test. It
doesn't need one — the track definition is per-character, so it has already
said whose skill this is. The `CharacterId` gate stays on the
`unlockedSkillIds`/`granted` branch untouched.

**g. Odette has no signature resource.** `characters.json` authors
`signatureId` only on `sheep` (capacity 10, 1/turn, 0 on-attack, 0 on-hurt, no
absorb). Her three skills are all `placeholder_caster_*` and authored
`characterId: "owl"`. **Resolution:** her track spends its flavour on elemental
damage, mana and two real book spells; nothing on it touches a placeholder
skill or invents a signature resource for her.

**h. The talent system enforces the OPPOSITE ownership rule, and the new
validator must not copy it.** `ContentDatabase.Validation.cs:154-176` refuses a
talent whose `grantsSkillId` belongs to another character — *"A character
cannot hand out another character's kit."* The track's cross-catalogue check
(below, §4) lives beside that arm and takes only its first half: **the id
resolves to a real skill, and nothing about whose it is.** Say so in the arm
itself, or the next reader will restore the symmetry and break §3f.

**i. The fight submenu has room.** `FightCapacityPinTests` bounds
level-unlocked-plus-one-talent-root skills per character against
`FightSubmenuLayout.PoolSize = 24`. It counts by AUTHORED owner, so it does not
see the track's cross-owner grants (nor the shop's), and needs no change:
Odette's real worst case becomes 3 authored + 2 track = 5, and Shawn's two
track spells are already inside his 18. Nothing is added to the pin; this is
recorded because the check was made.

**j. There is no skill-equip step to be out of sync with.** Grepped for
`skillSlot` / `SkillSlot` / `selectedSkills` / `equippedSkills` across
`Scripts/`: nothing. `FightEncounterAdapter.KitFor(Character, ...)` (`:657-666`)
takes `AvailableSkillsFor` whole, so a track-unlocked skill is in the kit the
moment it is collected and there is no assignment UI that could fail to offer
it. It also means `AvailableSkillsFor` is called with the live save's own
`Character` at fight-build time, so `claimedTrackLevel` is current by
construction.

**k. `SkillUnlockFilterTests` pins the two skill routes against each other and
survives.** `TheSharedFunctionAgreesWithAvailableSkillsForAFreshLevelOneCharacter`
(`:62-83`) compares `SkillsUnlockedByLevel(id, 1)` with
`AvailableSkillsFor(fresh)`. A fresh `Character` has `claimedTrackLevel = 0`,
the track's first node is level 2 and its first `UnlockSkill` is level 10, so
the new branch contributes nothing there. It stays green, and P4 must keep it
that way — `SkillsCollected(0)` returning empty is the invariant it rests on.

---

## 4. Where the tracks are authored

**A new `Assets/_Project/ContentData/reward_tracks.json`,** not a block inside
`characters.json`. Three reasons, in order:

1. **"No authored track" has to be expressible.** A missing *file entry* is
   unambiguous; an empty nested array inside a character row is
   indistinguishable from an author who started and stopped.
2. **Different authoring cadence.** A character's base stats change twice a
   year; a track is retuned every balance pass. Keeping them apart keeps the
   diffs readable.
3. **The `_readme` for a track is a different document** from the one for a
   roster, and `characters.json`'s is already 3,000 characters long.

Described-then-derived, exactly as `RewardTrack.cs:65-72` argues today: an
author writes twelve milestones and a filler mix, and the 87 filler placements
are computed by the existing `InterleaveMix`/`Spread` pair
(`RewardTrack.cs:497-580`), moved onto the definition unchanged.

> **Superseded 2026-09-15 by progression v2 phase 4.** The format below —
> `milestones` plus a `filler` mix of `(reward, amount, count)` rows — is gone,
> and with it `InterleaveMix`/`Spread` and validation rules 2, 3 and 4. A track
> is now **one `levels` array with an explicit entry for every level from 2 to
> `RewardTrack.MaxLevel`** (`RawTrackLevel`: `level`, `reward`, `amount`, plus
> the optional `against` / `skillId` / `resource` / `identityKind` / `value`
> selectors). The 40-level table in
> `docs/handoffs/progression_v2/PLAN_PROGRESSION_V2.md` §4 places every node
> deliberately — "+5% Nature damage at 7, 17 and 28", "Slam +5 at 12 and 24" —
> and a computed placement cannot express a placement. Three rules remain:
> every level from 2 to `MaxLevel` carries exactly one entry and no entry names
> a level outside that span; a one-shot capability appears at most once; a
> signature reward needs a signature resource. Rules 3 ("a one-shot kind may
> not be filler") and 4 ("a filler element must be one the character can
> already deal at level 1") were both policing computed placement — a milestone
> was always exempt from rule 4 for exactly that reason — so both retired with
> the mechanism. The rest of this section is kept for the reasoning behind the
> separate file and the twelve captions, which is unchanged; the placement
> arithmetic below it describes code that no longer exists.

### Where a filler node actually lands, so an author can predict it

**The mix's order IS the level order, one node per free level, from level 2
upward.** `Spread` (`:534-576`) is a Bresenham that hands slot `i` a reward when
`i * mixed.Length / fillerCount` crosses an integer — and because the mix must
sum to exactly 87 while there are exactly 87 free levels, `mixed.Length ==
fillerCount` and that expression is just `i`. Every free level takes the next
reward in the sequence, in order, with nothing skipped and nothing doubled.
`InterleaveMix` (`:489-527`) builds that sequence by largest deficit: at step
`k` (0-based) it hands the slot to whichever mix row maximises `Count * (k+1) -
Emitted * 87`, ties going to declaration order. In practice that means each row
appears at its own even cadence — a row with count 40 lands roughly every other
free level, a row with count 9 roughly every tenth — and **the biggest count in
the mix always takes level 2.**

Two consequences an author has to design around, both of which caught a real
defect in the round-2 draft:

1. **Filler starts at level 2, so a filler kind must be worth something at
   level 2.** A percentage that only scales a spell the character does not own
   yet is a dead node until they do. This is validation rule 4 below.
2. **Milestone amounts are what an author can place precisely.** Anything paid
   by filler drifts in small steps across the whole track; if a specific total
   has to be reached at a specific level, it has to be a milestone.

```json
{
  "_readme": "...",
  "tracks": [
    {
      "characterId": "sheep",
      "milestones": [
        { "level": 10,  "reward": "SignatureGainPerTurn",       "amount": 1 },
        { "level": 20,  "reward": "Respec" },
        { "level": 25,  "reward": "SignatureCapacity",          "amount": 5 },
        { "level": 30,  "reward": "UnlockSkill", "skillId": "static_fleece" },
        { "level": 40,  "reward": "SignatureGainOnDamageTaken", "amount": 2 },
        { "level": 45,  "reward": "ElementalDamagePercent", "against": "Nature", "amount": 10 },
        { "level": 50,  "reward": "SignatureGainPerTurn",       "amount": 1 },
        { "level": 60,  "reward": "SignatureAbsorbs" },
        { "level": 70,  "reward": "UnlockSkill", "skillId": "cinderfault" },
        { "level": 80,  "reward": "StatPoint",                  "amount": 10 },
        { "level": 90,  "reward": "SecondLife",                 "amount": 1 },
        { "level": 100, "reward": "SignatureCapacity",          "amount": 10 }
      ],
      "filler": [
        { "reward": "StatPoint",                  "amount": 1,  "count": 40 },
        { "reward": "MaxHealth",                  "amount": 10, "count": 15 },
        { "reward": "SignatureCapacity",          "amount": 1,  "count": 12 },
        { "reward": "ElementalDamagePercent", "against": "Nature", "amount": 1, "count": 20 }
      ]
    }
  ]
}
```

`reward` and `against` are authored as **enum member names**, matched
case-insensitively, exactly as `RawSkillEntry.effect` and
`RawEnemyEntry.attackType` are, and for the same reason: JsonUtility can only
write an enum as its ordinal, so reordering the enum would silently repoint
every authored row (`ContentSchema.cs:47-56`, `RawSkillEntry.cs:200-203`).

`TrackEntry` (`RewardTrack.cs:47-63`) is `(Reward, Amount)` today and has to
grow to carry the two selectors an entry can now hold plus the two baked
captions: `DamageType? Against`, `string SkillId`, `string SkillDisplayName`,
`string ResourceDisplayName`. `DamageType` is
`PrincesPalace.Domain.Stats` and engine-free, so this stays inside Domain with
no reference added.

### The five validation rules

Collected in one pass, `List<string>` of problems, per
`docs/CODE_STANDARDS.md` §5 and the `SkillEntryResolver.TryResolveAll`
precedent (`SkillEntryResolver.cs:28-31`):

1. **Every one of the twelve milestone levels carries exactly one entry, and
   no milestone entry names a level that is not a milestone.** A landmark the
   screen draws large with nothing on it is the failure this stops.
2. **The filler counts sum to exactly 87.** Restated from
   `RewardTrack.MaxLevel - RewardTrack.MilestoneLevels.Length - 1`, never typed.
3. **No one-shot capability appears as filler** — `Respec`, `SecondLife`,
   `SignatureAbsorbs`, `UnlockSkill`. This **replaces** today's
   `EarliestFillerLevel`/`TooEarlyFor` placement hack
   (`RewardTrack.cs:477-487, 560-583`), which existed to stop a filler reroll
   landing before the milestone that introduces rerolls. Under the new model
   every non-`StatPoint` kind is an "unlock", so keeping the old gate would
   push all of a character's flavour above level 45 and leave levels 2-24 as
   nothing but stat points and health. A refusal at author time is the honest
   version of the same rule, and the swap loop in `Spread` goes with it.
4. **A FILLER `ElementalDamagePercent` entry's element must be one the
   character can already deal at level 1** — either their authored `attackType`,
   or the `type` of a `damageInstances` entry on a skill authored to them with
   `unlockLevel <= 1`. Milestones are exempt: their level is authored and
   visible, so an author can deliberately place one after the milestone that
   unlocks the spell it scales, which is exactly what Odette's Ice (45, after
   Frost Flare at 10) and Lightning (70, after Lightning Bolt at 60) do. Filler
   is not exempt, because its placement is computed and always begins at level
   2 — this is the rule the round-2 draft broke, putting `+2% Lightning` at
   levels 16 and 42 on a character whose only Lightning source arrives at 60.
5. **A signature reward requires a signature resource.** `SignatureCapacity`,
   `SignatureGainPerTurn`, `SignatureGainOnDamageTaken` and `SignatureAbsorbs`
   are refused on a character whose `CharacterDefinition` authors no
   `signatureId` — `BuildSignatureResource` returns null for them
   (`ContentDatabase.Effective.cs:236-285`), so the reward would be collected,
   captioned and paid into nothing. Today that means those four are sheep-only,
   and the refusal is what makes that a rule rather than a coincidence.

Rules 4 and 5 are **cross-catalogue**, and belong in
`ContentDatabase.Validation.ValidateContent` beside the `GrantsSkillId` check
at `:154-176`, together with: **every `skillId` on a track resolves to a real
skill — and nothing about whose skill it is** (§3h). Rules 1-3 need only the
track file itself and stay in the resolver.

### The two display names the captions need

`RewardTrackNames` is in Domain and cannot reach the skill or character
catalogues, and splitting "what rewards exist" from "what they are called"
across assemblies is exactly what that file's header (`RewardTrackNames.cs:5-9`)
forbids. Two of the twelve captions need a lookup it cannot do:

- **the skill's name** — `"LEARN CINDERFAULT"`, not `"LEARN cinderfault"`;
- **the signature resource's name** — `"+5 WOOL CAPACITY"`, not
  `"+5 SIGNATURE CAPACITY"`. The four signature kinds are generic by design
  (`SignatureResource.cs:5-9`: "the type is deliberately generic so the second
  one is content and wiring rather than a parallel system"), so the word "Wool"
  is content, not an enum name. It is authored already, as
  `characters.json`'s `signatureDisplayName`.

**Resolution:** the resolver takes both as extra arguments and bakes the
resolved strings onto the entry — the identical shape
`RelicEntryResolver.TryResolveAll(entries, achievementIds, ...)` already uses
(`ContentBuilder.cs:284-297`). `RewardTrackNames.Of` then reads
`entry.SkillDisplayName` / `entry.ResourceDisplayName` with no lookup. The
signature name is one string per TRACK, not a map — a track names one character
— so the argument is `IReadOnlyDictionary<string, string>` keyed by character
id, resolved per track before its entries are.

`BuildCharacters()` therefore has to return an id→`signatureDisplayName` map
the same way `BuildSkills()` has to return an id→`displayName` one; both return
void today (`ContentBuilder.cs:117, 123`). `BuildCharacters()` already runs
first in the block, so no reordering is needed for it — only `BuildSkills()`
constrains where `BuildRewardTracks` can sit.

A character with a signature resource but a blank `signatureDisplayName` falls
back to the literal `"SIGNATURE"` rather than emitting `"+5  CAPACITY"` with a
hole in it — graceful degradation, and it cannot happen today because
`characters.json` authors the name beside the id.

### Content-chain touch points

| File | Change |
|---|---|
| `Domain/Content/RawRewardTrackEntry.cs` | new: `RawRewardTrackEntry`, `RawTrackMilestone`, `RawTrackFiller`, `RawRewardTrackFile`, all `[ContentDoc]`-annotated |
| `Domain/Content/ResolvedRewardTrack.cs` | new |
| `Domain/Content/RewardTrackEntryResolver.cs` | new |
| `Core/Content/RewardTrackDefinitionAsset.cs` | new `ScriptableObject, IOrderedContent` (gotcha 4); `SortOrder` = the character's own roster order |
| `Editor/ContentBuilder.cs:17-26` | `RewardTracksPath = ContentRoot + "/RewardTracks"` |
| `Editor/ContentBuilder.cs:81-90` | `EnsureFolder(RewardTracksPath)` — every other path has a line here and a missing one means `CreateAsset` writes into nothing |
| `Editor/ContentBuilder.cs:117-131` | **`BuildRewardTracks(skillNames, signatureNames)` goes AFTER `BuildSkills()`**, for the reason stated at `:126-129` about achievements preceding relics: a resolver validated against nothing lets a typo through. `BuildSkills()` returns void today (`:123`) and must return its id→displayName map, the way `BuildAchievements()` already returns its ids (`:130`); **`BuildCharacters()` (`:117`) likewise returns id→`signatureDisplayName`** for the caption lookup above. It already runs first, so only `BuildSkills` constrains the ordering |
| `Editor/ContentBuilder.cs:283-298` | the local-function shape `BuildRelics` uses to close over a second resolver argument — copy it verbatim rather than adding a bespoke `Build` path |
| `Core/Content/ContentDatabase.cs:705-722` | `_rewardTracks = LoadOrdered<RewardTrackDefinitionAsset>(...)` inside `EnsureLoaded`, **not lazily elsewhere**: `AvailableSkillsFor` calls `EnsureLoaded()` at `:238` and reads the track at `:312`, so the field is assigned by then; a lazy load would re-enter `EnsureLoaded`, find `_characters` non-null at `:707` and return without it |
| `Core/Content/ContentDatabase.cs:327-336` | add `_rewardTracks = null` to `Reset()`, **and `RewardTracks.Reset()` beside `CharacterPortraits.Reset()` at `:342`** — for the identical reason that comment gives: a test that installs its own roster would otherwise see the last one's tracks |
| `Domain/Content/ContentSchema.cs:31-45` | a `("reward_tracks.json", typeof(RawRewardTrackEntry))` row — the table is **alphabetical by filename** (`:26-27`), so it sits between `relics.json` and `skills.json`. **The generator; regenerate with `tools/content_schema.ps1`** |
| `Domain/Content/ContentSchema.cs:57-85` | `EnumBackedFields` rows for `reward` → `TrackReward` and `against` → `DamageType` |
| `Domain/Content/ContentInputHash.cs:56-61` | `ContentData/*.json` is already non-recursive glob (`:58`) and `Domain/Content/*.cs` is recursive (`:59`) — the new file and every new `Raw*`/`Resolved*`/resolver are hashed for free. **But** add `("…/Domain/Progression/RewardTrack.cs", null, false)` **and `RewardTrackDefinition.cs`**, both of which declare types a content record reaches; `ContentInputCoverageTests` prints the exact lines if either is missed |
| `Tests/EditMode/Content/ContentStampIdsTests.cs:49, 178-224, 287` | an eleventh mirrored folder — **round 2 correction: `:45` (the `Fix` constant) does not say "ten" anywhere and needs no edit; it is a run-command string, not a count.** The three that actually need touching: `:49`'s "Ten folders today" comment, a new `["RewardTracks"] = Resolve<...>` entry inside the `ResolvedByFolder()` dictionary body (`:178-224`), and `:287`'s "Ten folders are mirrored above" comment |

---

## 5. The two authored tracks

Both fill the same twelve milestone slots and an 87-node filler mix. Three
milestone levels are the **spine** every track keeps, authored or generated —
20 Respec, 80 StatPoint 10, 90 SecondLife 1 — because "when can I rebuild" and
"when do I stop dying at the boss" should not be per-character trivia.

**Measured against today, before proposing anything:** today's track pays 40
filler stat points plus a 10-point block at level 80 = **50 stat points**, not
40 (`RewardTrack.cs:152, 209`), and 15 × 10 = **150 max health** (`:211`).
Both tracks below hold those two totals exactly.

### The twelve captions

`RewardTrackNames.Of` writes the amount into the string itself
(`RewardTrackNames.cs:11-14`), so the templates are part of the design rather
than left to a caller. `{R}` is the entry's baked `ResourceDisplayName`
("WOOL"), `{S}` its `SkillDisplayName`, `{E}` its `Against`:

| Kind | Caption | Longest instance |
|---|---|---|
| StatPoint | `A STAT POINT` / `{n} STAT POINTS` | `10 STAT POINTS` (14) |
| MaxHealth | `+{n} MAX HEALTH` | `+15 MAX HEALTH` (14) |
| Respec | `FREE RESPEC` | (11) |
| SecondLife | `A SECOND LIFE, ONCE PER RUN` | (27) |
| SignatureCapacity | `+{n} {R} CAPACITY` | `+10 WOOL CAPACITY` (17) |
| SignatureGainPerTurn | `+{n} {R} PER TURN` | `+1 WOOL PER TURN` (16) |
| SignatureGainOnDamageTaken | `+{n} {R} WHEN HURT` | `+2 WOOL WHEN HURT` (17) |
| SignatureAbsorbs | `{R} ABSORBS DAMAGE` | `WOOL ABSORBS DAMAGE` (19) |
| ElementalDamagePercent | `+{n}% {E} DAMAGE` | `+20% LIGHTNING DAMAGE` (21) |
| MaxMana | `+{n} MAX MANA` | `+20 MAX MANA` (12) |
| ManaRegen | `+{n} MANA A TURN` | `+2 MANA A TURN` (14) |
| UnlockSkill | `LEARN {S}` | `LEARN LIGHTNING BOLT` (20) |

Every one is inside the 38 characters §1 measured, and `A SECOND LIFE, ONCE PER
RUN` is still the longest thing the track can say.

### Shawn (`sheep`) — the wool economy

His base line, from `characters.json`: capacity 10, +1/turn, 0 on-attack, 0
on-hurt, no absorb; `attackType` Nature; 200 max health, WIS 14 (mana pool 38,
regen 3).

| Level | Reward | Caption | Amount |
|---|---|---|---|
| 10 | Wool per turn | `+1 WOOL PER TURN` | +1 (→ 2) |
| 20 | Free respec | `FREE RESPEC` | — |
| 25 | Wool capacity | `+5 WOOL CAPACITY` | +5 |
| 30 | Learn Static Fleece | `LEARN STATIC FLEECE` | — |
| 40 | Wool when hurt | `+2 WOOL WHEN HURT` | +2 |
| 45 | Nature damage | `+10% NATURE DAMAGE` | +10% |
| 50 | Wool per turn | `+1 WOOL PER TURN` | +1 (→ 3) |
| 60 | Wool absorbs damage | `WOOL ABSORBS DAMAGE` | — |
| 70 | Learn Cinderfault | `LEARN CINDERFAULT` | — |
| 80 | Stat points | `10 STAT POINTS` | 10 |
| 90 | A second life | `A SECOND LIFE, ONCE PER RUN` | 1 |
| 100 | Wool capacity | `+10 WOOL CAPACITY` | +10 |

Filler, 87 nodes: StatPoint 1 ×40, MaxHealth 10 ×15, SignatureCapacity 1 ×12,
ElementalDamagePercent(Nature) 1 ×20 — 40+15+12+20 = 87.

Fully collected at 100: **wool capacity 37, +3/turn, +2 when hurt, absorbs,
+30% Nature, +150 max health, 50 stat points, two book spells, a respec, a
second life.** The shape is deliberate — he starts every fight at zero
(`characters.json` `_readme`), so a hundred levels turn his worst opening turn
into a bank he can actually fill.

**Why each node is his.** Every wool node answers to the one resource nobody
else has. Nature is the element he swings (`attackType: Nature`), so under §2's
routing rule it rides the on-hit rider and pays on every landed hit rather than
on a spell he does not own. `static_fleece` is the only book spell in the game
that SPENDS wool ("all that wool has to discharge somewhere") — it is what makes
the first twenty-nine levels of capacity and gain nodes cash out, and it is why
it displaced `mud_burst`, which is a bog spell the Bog Witch already casts
(`bog_mud_burst`) with no connection to the fleece. `cinderfault` stays at 70 as
the showpiece: Fire + Nature, AoE, with finished VFX.

**Both are authored `characterId: "sheep"`, `bookOnly: true`. No new skill
content.** One gap to state rather than let a player find: `static_fleece` has
no `vfx` block in `skills.json` (`mud_burst`, `frost_flare`, `cinderfault` and
`lightning_bolt` all do), so it will cast without a spell animation until art
exists. Graceful degradation is the house style, but it is a real difference
from the spell it replaced — see §12.

**The first twenty-five levels, node by node** (computed, not guessed — the
placement rule is §4's; `*` marks a milestone):

```
 2 A STAT POINT        10 +1 WOOL PER TURN *  18 +1% NATURE DAMAGE
 3 +1% NATURE DAMAGE   11 +10 MAX HEALTH      19 A STAT POINT
 4 +10 MAX HEALTH      12 A STAT POINT        20 FREE RESPEC *
 5 A STAT POINT        13 +1% NATURE DAMAGE   21 +1 WOOL CAPACITY
 6 +1 WOOL CAPACITY    14 +1 WOOL CAPACITY    22 A STAT POINT
 7 A STAT POINT        15 A STAT POINT        23 +1% NATURE DAMAGE
 8 +1% NATURE DAMAGE   16 A STAT POINT        24 A STAT POINT
 9 A STAT POINT        17 +10 MAX HEALTH      25 +5 WOOL CAPACITY *
```

He is recognisably himself from level 3 (Nature) and level 6 (wool), long
before the first milestone. The default track's level 2 is `+2 MAX HEALTH` and
his is `A STAT POINT`, so even the very first node differs.

**Wool capacity first reaches 20 at level 39** (10 base, +1 filler at 6/14/21,
+5 at 25, +1 at 31, +1 at 39), and 25 at level 81. That is the owner's "wool
capacity upgraded to 20" example, mid-track and reachable, and it is written
down here because it is arithmetic nobody can read off the table.

**Magnitudes, checked against the numbers the game actually uses:**

- **+3 wool a turn** against wool costs of 2 (Ward, Gift: Mana), 3 (Shear,
  Static Fleece, Shatter, Gift: Fury), 5 (Gift: Haste), 6 (Battering Ram), 7
  (Black Ram Mode), 8 (Golden Fleece) and 10 (Wail). At base 1/turn a six-turn
  fight generates six wool — one Ward and change. At 3/turn it generates
  eighteen: a Wail plus a Ward, or six turns of Shear. Roughly a 3× economy at
  level 100, which is the arc the resource is for.
- **+2 when hurt, down from the round-2 draft's +5.** The Black Ram's ROOT
  talent — the whole entry fee to that path — grants `WoolOnHitTaken 1`
  (`talents.json`). A track paying +5 per damage event is five times the root
  talent by itself, and on a front-liner taking two or three hits a round it
  adds 10-15 wool a turn on top of the per-turn gain, filling a 37-cap by turn
  three and overflowing thereafter. That deletes the arc
  `SignatureResource.cs:10-14` exists to create ("weakest on turn one and
  strongest at the end") and makes every capacity node on the track moot. **+2
  is double the root talent and still leaves the meter something to climb.** The
  three filler `SignatureGainOnDamageTaken 1` nodes are dropped with it — the
  kind survives as a single authored milestone, which is also what makes it
  legible.
- **Capacity 37 is kept**, and it is not the trivialising number the on-hurt
  rate was. Capacity is a ceiling, not a rate: at +3/turn an empty meter takes
  twelve turns to reach 37, so the top of it only matters to a Shawn who is also
  being hit or running the Ram's below-health gains. It is also directly
  load-bearing — `SkillResolution.Damage` is `scaledAttack + flatAmount + power
  * resourceSpent` (`:170`), and `golden_fleece` is `power 3,
  spendsAllResource`, so a full meter is the difference between +30 and +111 raw
  on that one cast. A player who banks gets paid for banking, which is the
  point.
- **+30% Nature, down from +44%.** A `Sylvan` gear affix is
  `ElementalDamageOnHitPercent 10` at Ordinary/tier-0 (`modifiers.json`), and
  the same-element branch pays it unmitigated on top of an already-mitigated hit
  (`FightSession.cs:510-521`). +44% was four and a half affixes' worth of
  permanent, unmitigated bonus on every swing; +30% is three, spread over 21
  nodes and a hundred levels. Neither invisible nor dwarfing gear, which is the
  band this has to sit in.
- The 20 filler nodes read `+1% NATURE DAMAGE` each. A one-point node is small,
  deliberately: it is the beat that makes his track feel like his in the gaps
  between milestones, and 20 of them is the count that lands the total at 30
  once the level-45 milestone is added.

### Odette (`owl`) — the elementalist

Her base line: `attackType` Arcane, 140 max health, WIS 20, INT 16. Mana pool is
`GameplayConstants.DefaultMaxMana` 30 + `AbilityDerivation.MaxManaBonus` (WIS 20
→ +20) = **50**; regen is `ManaRegenBonus` = WIS/4 = **5 a turn**; a cast costs
the level-9 spell tier's **28** (`spells.json`, and every character is at tier 9
from level 9 — `GetSpellTierForLevel` takes the highest tier at or below their
level). So she opens a fight with fewer than two casts in the tank and waits six
turns for the next one. Mana is what actually gates her, which is why her track
spends a third of its slots on it.

**Round 1 replaced her Fire/Ice/Lightning lines with Arcane-only, because the
only seam then in scope (`ElementalDamageOnHitPercent`, comparing a reward's
element against `AttackTypeOf(actor)` — Odette's authored `attackType`, always
`Arcane`, even on a Fire cast, `FightSession.Skills.cs:558-563`) pays a foreign
element `Attack × 10%` floored at 1: one or two points, nothing a design round
should ship. Round 2, Part A overrules that**, per the owner's explicit ask for
elemental damage mods on the elementalist, by building the seam that pays the
SPELL's own element instead of the caster's swing — package **P4b**, a new
`ModifierEffectType.ElementalDamagePercent` read inside `ResolveDamageInstances`
(`FightSession.Skills.cs:619-649`) against `instance.type`, the packet's real
element, multiplying per packet at `:633` alongside the existing
`SkillPowerMultiplierFor`/`SpellScalingMultiplierFor` terms. `Frost Flare` casts
a Fire packet and an Ice packet in one spell (`Fire 7 + Frost 7`, i.e. `Fire 7 +
Ice 7`), `Lightning Bolt` a Lightning packet, and her authored
`placeholder_caster_firebolt` a Fire packet, so Fire/Ice/Lightning nodes scale
real, distinct packets. Her Arcane line takes the OTHER branch of §2's routing
rule — Arcane IS her `attackType` — and pays on every landed hit.

**Which elements may be filler and which may not.** Fire and Arcane are hers at
level 1 (`placeholder_caster_firebolt` Fire 20, `placeholder_caster_bolt` Arcane
18, both `unlockLevel: 1`), so both may spread across the whole track. Ice
arrives with Frost Flare and Lightning with Lightning Bolt, so both are
**milestone-only** — validation rule 4. The round-2 draft gave Lightning four
filler nodes; the placement rule puts them at levels 16, 42 and 64, two of which
sit forty-odd levels before the spell they scale. That is the defect the rule
exists to stop.

| Level | Reward | Caption | Amount |
|---|---|---|---|
| 10 | Learn Frost Flare | `LEARN FROST FLARE` | — |
| 20 | Free respec | `FREE RESPEC` | — |
| 25 | Max mana | `+20 MAX MANA` | +20 |
| 30 | Mana regen | `+1 MANA A TURN` | +1 |
| 40 | Fire damage | `+10% FIRE DAMAGE` | +10% |
| 45 | Ice damage | `+15% ICE DAMAGE` | +15% |
| 50 | Arcane damage | `+10% ARCANE DAMAGE` | +10% |
| 60 | Learn Lightning Bolt | `LEARN LIGHTNING BOLT` | — |
| 70 | Lightning damage | `+20% LIGHTNING DAMAGE` | +20% |
| 80 | Stat points | `10 STAT POINTS` | 10 |
| 90 | A second life | `A SECOND LIFE, ONCE PER RUN` | 1 |
| 100 | Mana regen | `+2 MANA A TURN` | +2 |

Filler, 87 nodes: StatPoint 1 ×40, MaxHealth 10 ×15, MaxMana 2 ×15,
ElementalDamagePercent(Fire) 2 ×9, ElementalDamagePercent(Arcane) 2 ×8 —
40+15+15+9+8 = 87.

Fully collected at 100: **Fire +28%, Arcane +26%, Lightning +20%, Ice +15%,
+50 max mana, +3 mana regen, two spells, +150 max health, 50 stat points, a
respec, a second life.**

**The first twenty-five levels, node by node:**

```
 2 A STAT POINT        10 LEARN FROST FLARE * 18 +2 MAX MANA
 3 +10 MAX HEALTH      11 +10 MAX HEALTH      19 A STAT POINT
 4 +2 MAX MANA         12 +2 MAX MANA         20 FREE RESPEC *
 5 A STAT POINT        13 A STAT POINT        21 +2% ARCANE DAMAGE
 6 +2% FIRE DAMAGE     14 A STAT POINT        22 A STAT POINT
 7 A STAT POINT        15 +2% FIRE DAMAGE     23 +10 MAX HEALTH
 8 +2% ARCANE DAMAGE   16 A STAT POINT        24 A STAT POINT
 9 A STAT POINT        17 +10 MAX HEALTH      25 +20 MAX MANA *
```

Mana at 4 and Fire at 6 make her unmistakably not Shawn from level 4 on, and her
level-10 milestone is a spell where his is a resource tick.

**The extra spell, and why level 10.** The owner asked that Odette "start with
an extra spell". The earliest a track can pay one is level 10: nodes begin at
level 2, `UnlockSkill` is refused as filler (rule 3 — a one-shot capability
whose placement would otherwise be computed), and 10 is the first milestone the
shared cadence has. So Frost Flare is the very first thing her track does,
nothing competes for that slot, and level 10 is inside the first session. The
alternative — authoring a second starting skill onto her in `characters.json` —
is a roster change outside this plan, and it would hand her the spell at level 1
with nothing on the track to show for it.

**Magnitudes, checked against her actual mana economy:**

- **+50 max mana and +3 regen, down from the round-2 draft's +100 and +5.** Base
  pool 50, base regen 5, cast 28. Over an eight-turn fight she has
  `50 + 5×8 = 90` mana, i.e. **3.2 casts**. The round-2 draft's +100/+5 gives
  `150 + 10×8 = 230`, i.e. **8.2 casts in eight turns** — she casts every single
  turn and mana stops being a decision at all, which is the one thing
  `SignatureResource.cs:10-14` distinguishes mana from a signature resource for
  ("it is a budget"). +50/+3 gives `100 + 8×8 = 164`, i.e. **5.8 casts**: 1.8×
  her base, three and a half casts banked at the opening bell instead of under
  two, and still a real gap between casts. That is a level-100 payoff, not an
  off switch.
- **Fire +28% / Arcane +26% / Lightning +20% / Ice +15%** against a gear affix's
  10% (`modifiers.json`: `fiery`, `astral`, `frosty` are all
  `ElementalDamageOnHitPercent 10`). Two to three affixes' worth per element,
  the same band Shawn's +30% Nature sits in — the difference is that his is one
  broad line and hers is four narrower ones, which is what "she is the
  elementalist" ought to mean. Ice and Lightning are smaller totals because each
  is a single milestone; they are bigger single steps (15 and 20) because a lone
  node has to feel like an arrival.
- The Fire packets themselves are 20 (`firebolt`) and 7 (`frost_flare`'s Fire
  half), so +28% is +5.6 and +2 on the packet — before the spell tier's 4.2×
  multiplier and her `SkillScaling` term, both of which multiply it, since
  `ResolveDamageInstances` folds the elemental term into the same product at
  `:629-633`. It scales with her rather than being a flat tail.

**Authoring hazard, sharper than §13's general note.** `frost_flare`'s own
`skills.json` entry authors its damage type as `"Frost"`, an alias only
`SkillEntryResolver.TryParseDamageType` accepts (§13); `reward_tracks.json`'s
resolver does not. An author who just wrote Odette's level-45 Ice node right
after looking at Frost Flare's own JSON is exactly the person who copies
`"against": "Frost"` onto it — the correct authored value is `"Ice"`. P6's pin
test asserts this explicitly.

`frost_flare` (Fire 7 + Ice 7) and `lightning_bolt` (Lightning 20) are authored
`characterId: "sheep"` but `bookOnly: true`, and the track's skill branch
carries no ownership test (§3f). **No new skill content.**

### `placeholder_brawler` — the generated default

No authored entry, so `RewardTrackDefinition.Default(id)` supplies: spine at
20/80/90; the other nine milestones MaxHealth 15; filler StatPoint 1 ×40 +
MaxHealth 2 ×47. Totals: **50 stat points, +229 max health, a respec, a second
life.**

**Every caption it can show is a real one**, which is what keeps it from
embarrassing the screen: `+2 MAX HEALTH` and `A STAT POINT` on filler, `+15 MAX
HEALTH` on nine milestones, `FREE RESPEC` at 20, `10 STAT POINTS` at 80, `A
SECOND LIFE, ONCE PER RUN` at 90. Nothing is blank, nothing says "placeholder",
nothing reads as missing — it reads as a plain, honest track for a plain,
honest fighter. Its first twenty-five levels alternate `+2 MAX HEALTH` / `A STAT
POINT` from level 2 (max health takes level 2, since 47 is the biggest count in
the mix), with `+15 MAX HEALTH` at 10 and 25 and `FREE RESPEC` at 20.

The extra 79 health over an authored track is the price of having no flavour,
and it is stated rather than tuned: the default is graceful degradation for a
character nobody has designed yet, not a balanced alternative. **Nothing is
authored onto the placeholder.**

### The spine stays identical on all three

20 Respec, 80 StatPoint 10, 90 SecondLife 1, on every track, authored or
generated. Confirmed rather than moved, for two reasons that are not the same
reason:

- **Respec at 20 is a UI contract, not a reward.** `TalentController.cs:340,
  379, 414` gates three separate controls on it, and "can this character
  rebuild" varying per character means a player learns the answer three times
  and a tooltip has to explain which. It is also the mechanic the dossier's new
  minus partially replaces (§7) — one is a full rebuild, one is a single point —
  so the level it arrives at is a statement about the whole progression, not
  about Shawn.
- **Second life at 90 is squad arithmetic.** §6 makes the SOURCE per-character
  and the SPEND squad-wide, so a squad's charge count is the number of members
  past 90. Moving it to 85 on one and 95 on another turns "how many charges do
  we have" into a lookup nobody can do in their head at the moment it matters,
  and the charge would still be spent by whoever wipes first.

Everything above the spine is per-character, which is where the ask's "unique
and good and different" is answered. The spine is the part that should NOT be
trivia.

---

## 6. SecondLife: per-character source, squad-wide spend

`SecondLifeCharges` enters a fight at exactly one place —
`RunOrchestrator.cs:317`, and `FightBootstrap.cs:308` routes the real game
through the same `BuildFight`, so there is one seam and not two. (Grepped:
the only other writes are three test fixtures setting it directly.)

`SquadTrack.SecondLivesLeft` becomes:

```
sum over save.ActiveSquad() of track(c).CollectedTotal(SecondLife, c.claimedTrackLevel)
  - run.secondLivesUsed
```

`run.secondLivesUsed` stays one integer. The argument for not splitting it is
in `FightSession.Outcome.cs:114-121`, written before this question came up:
`TrySecondLife` fires **only when the party would otherwise be wiped**, and
raises everyone who is down for one charge, because reviving one and losing
anyway "would spend the charge and change nothing". Making the charge
owner-only would re-introduce exactly that outcome. So: **the SOURCE is
per-character — a squad of three who have each collected level 90 gets three
charges, one who has collected none contributes none — and the SPEND stays
squad-wide.** Listed in §12 as the one thing worth overruling.

---

## 7. The dossier's minus

Measured, in cell-local coordinates. `ContentCWidth = 340`
(`DossierLayout.cs:632`), `AttributeCellWidth = 340/3 = 113.33` (`:646`),
`AttributeCellHeight = 86` (`:643`); the cell button is emitted at
`width - 2` × `height - 2` = **111.33 × 84** (`CharacterDossierScreen.cs:877`),
so half-extents are 55.67 × 42. The "+" is 22px at inset 13 (`:640-641`),
placed at `(halfWidth - 13, halfHeight - 13) = (+42.67, +29)` (`:886-889`),
spanning **x 31.67…53.67, y 18…40**.

**The minus mirrors it at `(-42.67, +29)`, spanning x -53.67…-31.67, y 18…40.**

- 63.3px of clear air between the two buttons — no A1 pair.
- 2px of clearance from the cell's own top edge, identical to the plus — no
  escaping-child failure.
- It overlaps `DossierAttrValue{i}` (90×36 at `(0,10)`, x ±45), and that costs
  nothing: `UiAudit.CheckSiblingOverlap` skips any pair where either side is
  decor (`UiAudit.cs:346`), and the value label is `.AsDecor()`
  (`CharacterDossierScreen.cs:868`). The plus already overlaps it on the other
  side and needs no exemption. **No `AllowOverlap` string is added.**

Wiring, mirroring `attributePluses` exactly:

- `CharacterDossierScreen.AttributeMinuses`, a `List<NodeRef>` beside
  `AttributePluses` (`:131`), `.NoChrome().Inactive()`.
- `CharacterDossierController.attributeMinuses`, a `Button[]` with
  `[SerializeField] internal`, beside `attributePluses` (`:85`), bound by
  `UiAutoBind` off the field's own identifier —
  `attributeMinuses` → `AttributeMinuses` under `UiBindingNames.ScreenMemberFor`
  (`UiBindingNames.cs:19-27`), which only re-cases the first letter and strips
  nothing.
- `Refund(int index)` beside `Spend` (`:836-867`), resolving through
  `_cellOrder` at click time for the reason `Spend`'s header gives, and calling
  `SaveSlotManager.SaveCurrent()` then `Refresh()` exactly as `Spend` does at
  `:865-866`. Its click listener is registered beside the plus's at `:239-247`,
  which already sits below the `lockedForFight` guard at `:225`. **Its first
  line is `if (lockedForFight || inDescent) return;`** — `Spend` guards inside
  the method as well as through the button (`:850`), and a control that is only
  hidden is still reachable through the EventSystem.
- `PaintStatSpending` (`:869-900`) gains the visibility rule below.

**The minus is gated PER CELL, where the plus is gated globally, and that
asymmetry is the whole reason a copy-paste of `PaintStatSpending`'s loop is
wrong.** An unspent point can go into any of the six scores, so one condition
(`points > 0`) answers all six pluses at `:894-899`. A refund cannot: it can
only come back out of a score that has something invested in it. So the minus on
cell `i` is active when

```
!lockedForFight && !inDescent
  && i < _cellOrder.Count
  && character.investedAbilityScores[_cellOrder[i]] > 0
```

whose last term is exactly what makes `Character.Refund` return true, so the
button and the model cannot disagree about any individual cell. Six greyed
minuses on a character who has invested nothing would be the same six pieces of
furniture `PaintStatSpending`'s own header rejects for the plus; hidden, not
disabled, for the same reason.

**Nothing can be stranded after a full refund and re-spend.** `_cellOrder` holds
all six scores (`AbilityScores.All`, `:962-965`) and is frozen per character by
`_cellOrderFor`, so every invested score always has a cell with a minus on it,
and refunding never reshuffles the grid under the cursor — the hazard
`RefreshAttributes`' own comment (`:940-957`) describes for spending applies
identically here and is already solved. Refunding to zero and re-spending walks
the same `Invest` path a fresh level-up does; `unspentStatPoints` and
`investedAbilityScores` are the only two fields involved and they move in
opposite directions by one, so their sum is invariant. The one visible
side effect is the gear-requirement floor (below), which is recoverable by
re-spending and is painted while it lasts.

**Only the hub copy shows it.** `inDescent` already reaches the dossier's
sibling `SystemMenuController` (`ScreenRegistry.cs:808`); `WireDossier`
(`:954-955`) takes only `lockedForFight`, which it assigns at `:973`. Add
`internal bool inDescent` to `CharacterDossierController` and assign it at the
call site rather than widening the signature — two adjacent bools in one
parameter list are transposable and `CODE_STANDARDS.md` §5 says so. `:810`
already discards `WireDossier`'s return; capture it into a local declared above
the `if`, and write `dossierController.inDescent = inDescent;` beside the
`trackPanel` line. `inDescent` is in scope there: it is `WireSystemMenu`'s own
parameter, passed by the three call sites at `ScreenRegistry.cs:121-122`,
`:514-515`, `:772-773`. The per-cell activation rule is the one written out
above; `inDescent` is its second term.

`Character.Refund(AbilityScore score)`, beside `Invest` (`Character.cs:214-224`):
returns false and changes nothing when `investedAbilityScores[score] <= 0`, so
a caller can drive a button off it; otherwise `unspentStatPoints++` and the
score drops by one. It can never go below the authored base because
`investedAbilityScores` holds only what was invested — the authored base is a
separate summand in `ActiveLoadout`'s floor
(`ContentDatabase.Effective.cs:98, 111-123`), which is what
`EffectiveAbilityScores` (`:532-535`) is a one-line delegate onto.

**A refund can flip a worn item inert, and that is not a bug.** The same block
says so at `:111-119`: invested points are part of the requirement FLOOR, so
"levelling into Strength should let you lift the sword you could not lift at
level one" — and taking the point back puts the sword down again.
`Character.Respec` already has this property; the minus only makes it
one point at a time. It is not silent: `Refund` ends in `Refresh()`, which
repaints the paperdoll and its `SlotBlockedCaptions`
(`CharacterDossierScreen.cs:124`) in the same frame. Nothing to guard, but the
`Refund` header should say it rather than leave the next reader to discover it
from a greyed slot.

**The comment at `Character.cs:202-213` is overruled and must be rewritten.**
Its surviving half — "a point you can take back is a slider, and levelling
should be a decision" — is answered by *where* the button lives: a refund
outside a run is a decision you can revise between descents, and there is no
undo inside one. The new comment should say that, and should not read as a
changelog (§9 of `CODE_STANDARDS.md`).

One hazard, from `RefreshAttributes`' own comment (`:940-957`): the cell order
is frozen per character precisely so that spending does not reshuffle the grid
under the cursor. Refunding has the same property and is already covered —
`_cellOrderFor` does not change, so nothing moves.

---

## 8. The screen shows the selected character

The dossier already has a character selector: `_index`,
`prevCharacterButton`/`nextCharacterButton` (`CharacterDossierController.cs:31-32,
167-168, 448-453`), over `Squad()` (`:455-458`). The track is opened from the
dossier and nowhere else — `ShowTrack()` (`:825-834`) sets
`trackPanel.SetActive(true)`, and `trackPanel` is bound at
`ScreenRegistry.cs:816-825`.

So:

- Bind a typed `internal RewardTrackController trackScreen` on the dossier in
  that same block, by direct field assignment — compile-checked, per
  `CODE_STANDARDS.md` §4a. **Both wiring calls already return the controller
  and both returns are discarded**: `WireDossier` at `:810` and
  `WireRewardTrack` at `:813`. Capture them into locals and the
  `result.Go(...).GetComponent<CharacterDossierController>()` at `:820` goes
  away entirely rather than being joined by a second one.
- `ShowTrack()` calls `trackScreen.ShowFor(character.definitionId)` **before**
  `SetActive(true)`, so `OnEnable`'s `Refresh()` (`RewardTrackController.cs:142-147`)
  already has the id.
- The controller stores the **id**, never the `Character` object, and resolves
  it from the save on every `Refresh` — the save is replaced by a slot load.
- No id set (a screenshot fixture, a test calling `SetActive` directly) falls
  back to the first squad member. Graceful degradation, and it keeps
  `SystemMenuCaptureTests` working with no fixture change.

`RewardTrackController.Refresh` (`:178-206`) then reads `_level` and `_claimed`
off that one character, replacing `:184-185`, and three methods go — they live
in three different files, which is part of why they were hard to keep in step:
**`SquadTrack.BestLevel()` (`SquadTrack.cs:30-43`),
`RewardTrackController.ClaimedLevel()` (`RewardTrackController.cs:220-239`) and
`RewardTrackController.BestCharacter()`
(`RewardTrackController.Input.cs:173-202`)**, along with the two long tie-rule
comments that exist only to keep them agreeing with each other. `SquadTrack` is
left holding `SecondLivesLeft` alone and its header shrinks to that — including
the "THE HIGHEST LEVEL IN THE SQUAD, NEVER THE SUM" argument at `:7-18`, which
§6 deliberately reverses for this one reward and which must not be left standing
above a method that now sums.

---

## 9. Saves

`JsonUtility`, confirmed — `SaveData.cs:17, 111, 194`, `Character.cs:43, 99,
131`. Removed fields on an old save are silently dropped on load, so removal
alone needs no version bump.

But `claimedTrackLevel` (`Character.cs:183`) survives and would be
**reinterpreted**: a watermark of 40 against Shawn's new table would read as
"already collected +12% Nature and two wool capacity" that were never applied,
while `unspentStatPoints` and `bonusMaxHealth` still hold what the old table
paid. That is a plausible wrong answer, which `CODE_STANDARDS.md` §5 names as
the line neither posture may cross.

**`SaveData.CurrentVersion` 4 → 5**, with a `if (version < 5)` step in
`Migrate()` (`SaveData.cs:335-388`, ordered before the stamp at `:385` for the
reason `:369-371` gives): for every roster character, `claimedTrackLevel = 0`,
`unspentStatPoints = 0`, `investedAbilityScores = default`. `level`, `exp`,
`embers`, `unlockedTalentIds` and equipment are untouched. `earnedFavor`,
`bonusExpPermille` and `bonusMaxHealth` need no clearing — the fields are gone
and the loader drops them.

The player's experience of this: every character's whole track reads as waiting,
the collect button says how many, and their stat points come back unspent. That
is a full free respec of the half the track paid for, which is the correct
answer to "the track changed underneath you", and it exercises the new claim
path from level 1 on the first open.

**One consequence to state rather than discover.** Invested points are part of
the equipment requirement floor (`ContentDatabase.Effective.cs:111-123`), so a
character whose weapon was liftable only because of them opens the migrated
save with that item **inert** — worn, on the paperdoll, contributing nothing —
until the points are re-spent. It is visible (the dossier paints
`SlotBlockedCaptions`) and it is one click of "collect" plus re-spending away,
which is the same state the existing `Character.Respec` already produces on
purpose. Nothing in the migration should try to be clever about it; the note
belongs in the migration step's own comment.

The bot does not reach this path at all: `BotRunDriver.cs:1041` and
`ProfilePresets.cs:146` call `ClaimTrackRewards()` on characters in a `SaveData`
constructed at `CurrentVersion`, so `Migrate()` short-circuits at
`SaveData.cs:337-341` and the v5 step never runs. Their `claimedTrackLevel` is 0
from construction, which is the same state the migration produces, so the two
agree by accident rather than by test — the new
`AVersionFourSaveComesBackWithEverythingWaiting` in P4 is what makes it a claim.
`ProfilePresets.cs:142-145`'s comment names "exp-find" as a thing a level-60
character has and must be rewritten with the field.

---

## 10. Packages

Each compiles on its own. `[D]` = dotnet EditMode, runnable from a parallel
worktree; `[U]` = Unity PlayMode, main tree only.

### P1 — Remove the eight over-arching rewards
**Main tree. `run_tests_parallel.ps1 -BuildScenes` (the Reckoning row narrows).**

Touch:
- `Domain/Progression/RewardTrack.cs` — delete `Favor`, `ExpFind`,
  `StartingRelics`, `RestBeforeBoss`, `OfferReroll`, `WiderOffer`,
  `ChosenStartingRelics`, `SecondLifeRefresh` from the enum (`:9-43`); drop
  their eight `Milestones` rows (`:116-155`) and fill the nine now-empty
  milestone levels with `MaxHealth 15`; `FillerMix` (`:207-214`) becomes
  **`StatPoint 1×40`, `MaxHealth 2×47`** (sum 87); move
  `BaseStartingRelics` (`:160`) to `RelicPool` per §3e2; delete `RerollsPerRun`
  (`:375-376`), `StartingRelics` (`:416-417`),
  `EarliestFillerLevel`/`TooEarlyFor` (`:477-487, 582-583`) and the swap block
  in `Spread` (`:554-576`).
  *This interim table is deliberately the eventual generated default track —
  P4 lifts it verbatim into `RewardTrackDefinition.Default`, so it must equal
  §5's default exactly: 9 × MaxHealth 15 + 47 × MaxHealth 2 = **229 max
  health**, 40 + 10 = **50 stat points**.* An earlier draft of this line said
  `MaxHealth 10×15` + `MaxHealth 2×32`, which summed to 87 but paid 349 health
  and did not match §5; two `MaxHealth` rows in one mix would also have made
  `TheTrackHandsOutTheMixItDescribes`'s per-kind grouping ambiguous.
- `Domain/Progression/RewardTrackNames.cs` — eight cases go: `:26-27` (Favor),
  `:32-33` (ExpFind), `:38-39` (StartingRelics), `:41-42` (RestBeforeBoss),
  `:44-45` (OfferReroll), `:47-48` (WiderOffer), `:50-51`
  (ChosenStartingRelics), `:56-57` (SecondLifeRefresh). `:53-54` (SecondLife)
  stays.
- `Domain/UiKit/RewardTrackLayout.cs` — `IconFor`: `:941` (Favor), `:943`
  (ExpFind). `CardArtFor`: `:964-965`, `:967-970`, `:971-972`, `:974-975`.
  `MatTintFor`: `:991` (ExpFind), `:993` (Favor).
- `Domain/UiKit/OfferRowLayout.cs:62-76` — `CardsFor`/`MaxCards` →
  `ItemOfferTable.OfferCount`, and the six-line comment above `MaxCards`
  explaining the track link goes with them.
- `Core/SquadTrack.cs:59-66` — `StartingRelics`, `OfferWidth`, `RerollsPerRun`
  go, with the `:49-57` block header that gathers them.
- `Core/ItemOfferRoll.cs:86, 102` — the `earned` term.
- `Core/RewardApplier.cs:57-66` — `payout.Experience` direct.
- `Core/ReckoningController.cs:66-68` (fields), `:216` (listener),
  `:467-491` (`Reroll`), `:494-510` (`PaintReroll`), `:630` (the repaint call),
  `RerollSource` and its assignment at `Core/FightController.Input.cs:497`,
  `Domain/UiKit/Screens/ReckoningScreen.cs:162, 274-280`, and its wiring at
  `Editor/SceneBuilder/ScreenRegistry.cs:1016`.
- `Core/RunManager.cs:160` (delete), `:294-297` (delete).
- `Core/Bot/RunOrchestrator.cs:86` (the `ChosenStartingRelics` branch, so the
  draft is always the weighted three), `:170` (→
  `RelicPool.StartingRelicsPerDescent`, **not** `RelicPool.OfferCount` — §3e2),
  `:578` (`OfferWidth()` → `ItemOfferTable.OfferCount`). This file is
  production with two callers, not bot-only; `HubController.cs:433` reaches it.
- `Data/Character.cs:118-133` (`earnedFavor`), `:155-164` (`bonusExpPermille`),
  `:242-248` (`ExperienceWorthOf`), `:394, 396, 399, 401, 404`.

Delete or rewrite, in `RewardTrackTests`: the `[TestCase]` rows at `:14-24`
for the removed kinds; `:60-78` (`TheTrackHandsOutTheMixItDescribes` — its
cases name Favor/ExpFind/OfferReroll); `:79-113`
(`AFullTrackTakesSheepToExactlyTheFavorCap`,
`AFullTrackIsWorthTwentyOnePercentExperience`); `:130-155`
(`TheFillerKindsInterleaveRatherThanClumping` — it asserts the old mix's
interleave); `:156-161` and `:203-219` (`[TestCase]` rows over removed kinds);
`:220-275` (relic line); `:307-352` (rerolls); `:353-377`
(`NoUnlockIsHandedOutBeforeItsMilestone`); **`:476-487`
(`TheTrackHandsOutFifteenMaxHealthNodes`) and `:488-496`
(`AFullTrackIsWorthOneHundredAndFiftyMaxHealth`), both of which the new interim
mix falsifies.** Then `RunManagerTests.cs:454-466` (whole test, §3a) and the
`WiderOffer` cases in `OfferRowLayoutTests`.

Also in P1, not previously listed: `ItemOfferFavorTests.cs:64, 93, 115,
136-138` (`earnedFavor` fixtures), `:149, 157, 164, 186, 203`
(`SquadTrack.OfferWidth`/`RerollsPerRun` assertions and
`TheWiderOfferActuallyRollsAFourthItem`); `RewardApplierTests.cs:218, 248, 256`
(`earnedFavor`) and `:309, 323, 338, 350, 356` (`bonusExpPermille`);
`RewardTrackClaimTests.cs:76, 78, 168, 184, 195` (`earnedFavor`). Stale comment
references to correct rather than delete: `SpeedAndBountyRelicTests.cs:276`,
`RunSnapshot.cs:124`, `ProfilePresets.cs:142-145`.

Run: `[D]` `RewardTrackTests`, `RewardTrackStateTests`, `RewardTrackLayoutTests`,
`OfferRowLayoutTests`, `ReckoningScreenTests`; `[U]` `RewardTrackClaimTests`,
`RewardApplierTests`, `ReckoningTests`, `ReckoningTabTests`, `RunManagerTests`,
`ItemOfferFavorTests`, `FightSettlementTests`.

Must not change: `DescentMap.cs` (the `restBeforeBoss` parameter stays),
`RunSnapshot.restBeforeBoss`/`offerRerollsUsed`, `ItemOfferRoll.FavorOf`'s
authored and Fortunate terms, `ShopController`'s gold-priced reroll
(`:321, 369-372`).

New test, `Tests/EditMode/Run/RewardTrackTests.cs`, literal values:
`TheTrackStillPaysFiftyStatPointsAndTwoHundredTwentyNineMaxHealth` — 40 filler
singles + 10 at level 80; 9 milestone nodes × 15 + 47 filler nodes × 2.

---

### P2 — The content type (nothing reads it yet)
**Parallel worktree, `[D]`-verifiable.**

Everything in §4's touch-point table, plus a `reward_tracks.json` containing
only its `_readme` and an empty `tracks` array. `ContentBuilder` writes zero
assets and logs it. No behaviour changes anywhere.

Run: `[D]` `ContentSchemaTests`, `ContentStampIdsTests`,
`ContentInputCoverageTests`, `ContentLoadingLintTests`; then
`tools/build_content.ps1` and `tools/content_schema.ps1` in the main tree.

New tests, `Tests/EditMode/Content/RewardTrackEntryResolverTests.cs`, all with
pinned literals: a track missing level 45 is refused naming that level; a track
with an entry at level 44 is refused naming it as not a milestone; filler
counts of 86 and 88 are both refused naming 87; `Respec` as filler is refused;
a valid two-track file resolves to two records with `Milestones.Length == 12`.

---

### P3 — The definition replaces the static table (Domain)
**Parallel worktree, `[D]`-verifiable. Depends on P1 and P2.**

- New `Domain/Progression/RewardTrackDefinition.cs`: `At(level)`,
  `NextRewardLevel(level)`, `GrantedBetween(reward, after, through)`,
  `CollectedTotal(reward, claimedLevel)`,
  `CollectedTotal(reward, DamageType, claimedLevel)`,
  `SkillsCollected(claimedLevel)`, `static Default(characterId)`. The
  `InterleaveMix`/`Spread` construction moves here **verbatim** (no renames —
  `CODE_STANDARDS.md` §4).
- `RewardTrack.cs` keeps only what is character-independent: `StartingLevel`,
  `MaxLevel`, a public `MilestoneLevels` array (the twelve), `IsMilestone`,
  `StateOf`, `IsWaiting`, `UnclaimedCount`, `IsGrant`/`IsUnlock`. Class name
  unchanged. `TrackEntry` (`:47-63`) grows the two selectors from §4 —
  `DamageType? Against`, `string SkillId`, `string SkillDisplayName` — and
  `UnlockLevel`/`HasUnlocked`/`UnlockedTotal`/`UnlockedAmount`/`SumOver`/`At`/
  `NextRewardLevel`/`GrantedBetween` (`:257-434`) all move onto the definition
  or go, since every one of them reads the now-deleted static `Entries` table.
- `RewardTrackLayout.cs`: `IconFor`, `CardArtKeyFor`, `MatTintFor` take a
  `TrackReward`, not a level. `IsMilestone(level)` is unchanged.
- `RewardTrackNames.cs`: the twelve new cases, verbatim from §5's caption table
  — including the reads off `entry.ResourceDisplayName`/`entry.SkillDisplayName`
  and the `"SIGNATURE"` fallback for a blank resource name.
- `RewardTrackScreen.cs:191-202`: the twelve key constants renamed per §1.

Delete in P3: `RewardTrackTests.EveryRewardKindTheEnumKnowsAboutIsActuallyGranted`
(`:462-475`). It walks `Enum.GetValues(typeof(TrackReward))` against the static
table, and P3 deletes the static table while adding twelve kinds that only
authored content grants. Leaving it would fail; weakening it to "the default
track grants everything" would be false. P6 replaces it with the honest
version, asserted over the shipped tracks.

Run: `[D]` `RewardTrackTests`, `RewardTrackStateTests`, `RewardTrackLayoutTests`.

New tests, `Tests/EditMode/Run/RewardTrackDefinitionTests.cs`:
`TheDefaultTrackPaysFiftyStatPointsAndTwoHundredTwentyNineMaxHealth`;
`EveryMilestoneLevelCarriesSomething` over all twelve;
`CollectedTotalSumsRatherThanReplacing` — two `SignatureCapacity 5` entries at
levels 25 and 100 give `CollectedTotal(…, 100) == 10` and
`CollectedTotal(…, 99) == 5`;
`NothingIsCollectedAboveTheWatermark`;
`EveryRewardKindResolvesAnArtKeyAndAName` over `Enum.GetValues(typeof(TrackReward))`
minus `None`, asserting a non-empty key and a non-empty name.

---

### P4 — The read sites (Core), and the save bump
**Main tree. `run_tests_parallel.ps1 -BuildContent`. Depends on P3 and P4b.**

- New `Core/RewardTracks.cs`: `For(string characterId)` → the authored
  definition or `RewardTrackDefinition.Default(id)`; `For(Character)`; a
  memo over both, and a `Reset()` seam **called from
  `ContentDatabase.Reset()` beside `CharacterPortraits.Reset()`
  (`:342`)**, not merely declared next to it (`CODE_STANDARDS.md` §7).
- `Data/Character.cs:374-408` — `ClaimTrackRewards(RewardTrackDefinition, int)`,
  one grant; `bonusMaxHealth` (`:135-153`) deleted. **This collapses the two
  overloads that exist today** (`ClaimTrackRewards(int throughLevel)` at
  `:374` and the parameterless `ClaimTrackRewards() => ClaimTrackRewards(level)`
  at `:408`) **into one that also takes the definition** — so both of the
  bot's zero-arg call sites below must change to
  `character.ClaimTrackRewards(RewardTracks.For(character), character.level)`,
  not merely "likewise"; there is no compile-preserving shortcut, because
  `Character` (in `Data/`, same assembly as `Core/` — round 1's assembly note)
  cannot resolve its own track without being handed the lookup, and adding a
  convenience overload that calls `RewardTracks.For(this)` internally was
  considered and rejected: it would be the only method on `Character` reaching
  into `Core.RewardTracks` on its own rather than being handed what it needs,
  which is the same "an inert item is inert everywhere at once"-style
  discipline this codebase already keeps elsewhere.
- `Core/Content/ContentDatabase.Effective.cs:236-285` — the three signature
  terms beside the talent terms at `:264-272`, and the absorb flag OR'd into
  the constructor's last argument at `:284`.
- `Core/Content/ContentDatabase.Effective.cs:349-357` (inside `ModifierEffects`,
  between the gear loop and the `return` at `:359`) — read the character's own
  attack type once, `var own = GetCharacter(character.definitionId)?.Data.AttackType;`
  (`ContentDatabase.cs:345`), then for every `DamageType` in
  `Enum.GetValues(typeof(DamageType))`, `int n =
  RewardTracks.For(character).CollectedTotal(TrackReward.ElementalDamagePercent,
  type, character.claimedTrackLevel)`, and when `n > 0` append **one** effect to
  `found` per §2's routing rule:
  `new ModifierEffect(own == type ? ModifierEffectType.ElementalDamageOnHitPercent
  : ModifierEffectType.ElementalDamagePercent, n, against: type)`.
  Never both, and a null `own` (no such definition) takes the
  `ElementalDamagePercent` side, which is the inert direction. **Depends on P4b**
  for the second enum member to exist. The method's own
  header comment (`:320-339`, "Every rule this character's equipped items'
  ROLLED MODIFIERS contribute") is now half wrong — it also carries a
  reward-track term that is not gear at all — and must say so, the same
  "rewrite the comment, don't leave it standing above code it no longer
  describes" rule P5 already applies to `RewardTrackScreen.cs:719-722`.
- `ContentDatabase.EffectiveStats` — `+ CollectedTotal(MaxHealth, …)` replacing
  `:213`, and `+ CollectedTotal(ManaRegen, …)` on `total.manaRegen` in the same
  place; `EffectiveMaxMana` (`:541-559`) — `+ CollectedTotal(MaxMana, …)` before
  the `Mathf.Max(0, …)` at `:558`.
- `Core/Content/ContentDatabase.cs:305-322` — the fifth `AvailableSkillsFor`
  route, no ownership test, with the reason stated (§3f) and the neighbouring
  comment's "fourth route" numbering (`:272`) corrected with it.
- `Core/Content/ContentDatabase.Validation.cs` — the three cross-catalogue arms
  beside the `GrantsSkillId` check at `:154-176`: the skill-id arm (§3h — the id
  resolves, and nothing about whose it is), validation rule 4 (a FILLER
  `ElementalDamagePercent` element must be the character's `attackType` or the
  type of a `damageInstances` entry on a skill authored to them with
  `unlockLevel <= 1`; milestones exempt) and validation rule 5 (a signature
  reward requires the character to author a `signatureId`). All three need the
  characters and skills catalogues, which is why they cannot live in the
  resolver with rules 1-3.
- `Core/TalentController.cs:340, 379, 414` — respec through the track.
- `Core/SquadTrack.cs:76-82` — §6.
- `Core/RewardTrackController.Input.cs:151` — pass the definition. **`:149`'s
  `maxBefore` read and `:153`'s `RunEncounter.ScaleCarriedHealth` are unchanged
  and still correct**: `maxBefore` comes from `EffectiveStats`, which after
  this package reads `CollectedTotal(MaxHealth, claimedTrackLevel)`, and the
  watermark moves at `:151` between the two reads — the same delta the deleted
  `bonusMaxHealth` used to produce. The ordering is what makes it work, so say
  so at the call rather than leaving it to look incidental.
- `Core/Bot/BotRunDriver.cs:1041` — `if (character.ClaimTrackRewards()) changed = true;`
  becomes `if (character.ClaimTrackRewards(RewardTracks.For(character), character.level))
  changed = true;`. `Core/Bot/ProfilePresets.cs:146` — `character.ClaimTrackRewards();`
  becomes `character.ClaimTrackRewards(RewardTracks.For(character), character.level);`.
  Both are the exact rewrite, not "likewise" left for the implementer to invent.
- `Data/SaveData.cs:42, 335-388` — version 5 and the step, inserted after the
  `version < 4` arm at `:381-384` and before the stamp at `:385` (§9).

Run: `[U]` `RewardTrackClaimTests`, `RewardApplierTests`, `SaveSystemTests`,
`SaveSlotFlowTests`, `SaveDataSquadOfThreeTests`, `TalentInvestmentTests`,
`FightSettlementTests`, `FightAfterTheEliteTests`, `EmberOwnershipTests`,
`SkillUnlockFilterTests` (§3k), `SpellBooksTests`;
`[D]` `SignatureResourceTests`, `SignatureAbsorptionTests`, `CharacterSheetStatsTests`.
**Round 2 correction: `SignatureAbsorptionTests` DOES exist** — round 1's "no
such test exists" was wrong. `tools/test.ps1 -List` discovers it as its own
`[D]` `combat` class because `test_areas.ps1`'s discovery is per-CLASS, not
per-file; the class itself is declared at
`Tests/EditMode/Combat/SignatureResourceTests.cs:105`, inside the same file as
`SignatureResourceTests`, not a file of its own. Both names run under
`tools/test.ps1 SignatureAbsorptionTests` regardless. Note that all three
`[D]` entries are EditMode and therefore cannot reach `ContentDatabase` at all
(`CODE_STANDARDS.md` §1): they pin the Domain types this package feeds, not
the feeding. The new PlayMode tests below are the ones that actually cover P4.

New tests, `Tests/PlayMode/Run/RewardTrackClaimTests.cs`. **Round 2 correction:
these are NOT pinned against a fixture track** — the literals below (Shawn's
wool capacity, Odette's Frost Flare unlock level) are the SHIPPED sheep/owl
numbers from §5, matching P6 exactly, and `RewardTracks.For("sheep"/"owl")`
resolves to `RewardTrackDefinition.Default(id)` — not the authored elemental/
wool-economy track — until P6's `reward_tracks.json` content actually lands.
There is no content-injection seam in this codebase today (grepped
`Core/Content/*.cs` for `Install`/`TestOverride`/`SetForTests`: nothing) for a
PlayMode test to substitute a fixture track while still going through
`ContentDatabase`/`RewardTracks.For` the way `OpenTheTrack`'s existing
Hub-scene-load path does. **So this package's new tests genuinely depend on
P6's authored JSON, not merely on P3's types** — see the new Execution order
subsection under §10 for the sequencing fix (P6's JSON lands before this
package's gate runs, even though the code in P4 only needs P3 to compile). A
retune in P6 later will move these numbers and these tests together, which is
the honest trade for pinning literals a person can read instead of recomputed
constants (CLAUDE.md gotcha 5) — not a defect, just not "fixture-immune" as
originally written.
- `AWoolCapacityNodeCollectedRaisesTheFightsCapacityAndAnUncollectedOneDoesNot` —
  Shawn at level 26 with `claimedTrackLevel` 25 builds a `SignatureResource`
  with `Max == 18`; the same character at `claimedTrackLevel` 24 gets
  `Max == 13`. **Round 3 correction: the previous literals (15 and 10) matched
  no version of the sheep mix.** 10 base, plus the three filler
  `SignatureCapacity 1` nodes at levels 6, 14 and 21, plus the level-25
  milestone's +5 — the filler capacity before level 25 was simply not counted.
  Both numbers come straight off §5's table and are recomputed there.
- `AnElementalNodeReachesTheCombatantAsAModifierEffect` — a level-71 Odette with
  70 collected has exactly one `ModifierEffectType.ElementalDamagePercent` in
  her `CombatantState.ModifierEffects` with `Magnitude == 20` and
  `Against == DamageType.Lightning`; at 69 collected she has none.
  **Lightning, not Fire**: Lightning is milestone-only on her track (§5,
  validation rule 4), so it is the one element for which "has none / has
  exactly one" is a true statement — Fire has filler nodes from level 6 and is
  already at +6 by level 39, which is what made the round-2 draft's "at 39
  collected she has none" false. The NEW enum member P4b adds, not
  `ElementalDamageOnHitPercent`, because Lightning is not her `attackType`.
- `TheCharactersOwnElementRidesTheOnHitRiderInstead` — the other half of §2's
  routing rule, and the pin that stops an implementer collapsing it back to one
  effect type: a level-46 Shawn with 45 collected has exactly one
  `ModifierEffectType.ElementalDamageOnHitPercent` with `Magnitude == 19`,
  `Against == DamageType.Nature`, and **no** `ElementalDamagePercent` at all;
  a level-51 Odette with 50 collected has exactly one
  `ElementalDamageOnHitPercent` with `Against == DamageType.Arcane` and
  `Magnitude == 18` (8 from filler through level 49, plus the level-50
  milestone's 10), alongside her Fire effect, which is an
  `ElementalDamagePercent`. Every literal here is read off §5's tables rather
  than re-derived (CLAUDE.md gotcha 5).
- `ASameElementNodeScalesTheHitAndAForeignOneDoesNot` — the pin that keeps §2's
  finding about the EXISTING rider from being rediscovered: an actor whose
  `attackType` is Arcane, carrying `ElementalDamageOnHitPercent(Arcane, 50)`,
  deals a landed hit whose bonus tick is 50% of the hit; the same actor
  carrying `ElementalDamageOnHitPercent(Fire, 50)` deals a tick of
  `Attack × 50%` instead, independent of the hit's size. Literals, driven
  through `FightSession` directly — this is a Domain fact about the OLD rider
  and belongs in `Tests/EditMode/Combat/ItemModifierCombatHookTests.cs`, not
  here; P4b adds its own, separate pair for the NEW `ElementalDamagePercent`
  hook in the same file (see P4b).
- `ASkillNodeCollectedPutsTheSpellInTheKit` — `AvailableSkillsFor` contains
  `frost_flare` for a level-11 Odette with 10 collected and not with 9.
- `ASecondLifeIsCountedPerCollectingCharacter` — three squad members at level
  90 with 90/90/0 collected give `SecondLifeCharges == 2`.
- `AVersionFourSaveComesBackWithEverythingWaiting` — **exact recipe, matching
  the established idiom for a same-shape migration**
  (`Tests/PlayMode/Run/EmberOwnershipTests.cs:136-140`, direct field
  assignment, not `ItemModifierSaveCompatTests.cs`'s JSON-stripping — that
  technique is for a FIELD REMOVED from the shape, and `claimedTrackLevel`/
  `unspentStatPoints` are neither removed nor renamed, only reinterpreted):
  `var save = SaveData.CreateNew(); save.version = 4;` set
  `claimedTrackLevel = 40, unspentStatPoints = 7` directly on a roster
  character; `save.Migrate()`; assert `claimedTrackLevel == 0`,
  `unspentStatPoints == 0`, `level` unchanged. No `JsonUtility` round-trip
  needed.

---

### P4b — The per-element combat hook
**Parallel worktree, `[D]`-verifiable. Depends on nothing in this plan** — pure
`Domain/Combat` engine work, touches no `Progression`/`Content` file, and can
run concurrently with every other package including P1. **P4 depends on it**
(for the enum member `ContentDatabase.ModifierEffects` appends).

Round 1 found the seam and left it as open question 5 ("a Fire node is a flat
`Attack × n%` tick that has nothing to do with her Firebolt... one new
`ModifierEffectType` member... a genuinely new combat hook"). The owner asked
for elemental damage mods on the elementalist. This package builds the hook;
§5 spends it.

**Why option (a) over option (b).** The brief offered two shapes: (a) a new
`ModifierEffectType` read inside `ResolveDamageInstances`, matching
`instance.type`; (b) a per-`DamageType` percent table (`int[]`) on
`CombatantState`, populated by `FightEncounterAdapter`. (a) wins on touch
points: it reuses the `CombatantState.ModifierEffects`/`ModifierEffectSet`
carrier that already exists and that `FightEncounterAdapter.cs:233` already
assigns unchanged — no new field on `CombatantState`, no new adapter-side
assembly step beside the one `ContentDatabase.ModifierEffects` already builds.
(b) would need a new field, a new adapter loop mirroring the existing
`TypedResistanceFlat` one at `FightEncounterAdapter.cs:262-270`, and a second
place (`CombatantState` itself) that has to agree with `ModifierEffectSet`
about what "the same rule" means. Neither option touches
`ApplyModifierOnHitRiders` (`FightSession.cs:464-560`) or
`ElementalDamageOnHitPercent`'s existing behaviour at all — the two hooks read
different `CombatantState` state at different call sites (rider block vs.
`ResolveDamageInstances`), so nothing about the attackType-matching rider
changes.

Touch:
- `Domain/Combat/ModifierEffect.cs` — new member `ElementalDamagePercent`,
  inserted immediately after `ElementalDamageOnHitPercent` (`:77`), with a
  comment stating the distinction from its neighbour: read inside
  `ResolveDamageInstances` against the PACKET's own `instance.type`, not
  `AttackTypeOf(actor)` — so it fires on every damage instance a cast deals,
  including a foreign-element one, unlike the attackType-gated rider above it.
  Satisfies the enum's own closed-enum rule (`:31-34`): a line here AND a hook.
- `Domain/Combat/Session/FightSession.Skills.cs:619-649` — a new private
  `static` (well, non-static — reads `actor.ModifierEffects`, an instance
  member, so it does not need `this`) helper,
  `ElementalDamagePercentFor(CombatantState actor, DamageType type)`: sums
  `effect.Magnitude` over `actor.ModifierEffects.All` where
  `effect.Type == ModifierEffectType.ElementalDamagePercent && effect.Against == type`
  — SUMS rather than `Best()`'s max-not-sum, mirroring
  `FightEncounterAdapter.cs:262-270`'s own `TypedResistanceFlat` precedent
  ("the two sources stack, same as PhysicalDefense/MagicalDefense already do
  for relics vs gear stats") rather than `ModifierEffectSet.cs`'s general
  max-not-sum default, which is about collapsing REPEATED copies of one rule,
  not about a typed rider meant to stack across elements. At `:629-633`, change
  `int scaled = System.Math.Max(1, Rounding.AwayFromZero(instance.amount * multiplier));`
  to fold in `float elementalMultiplier = 1f + ElementalDamagePercentFor(actor, instance.type) / 100f;`
  and multiply `instance.amount * multiplier * elementalMultiplier` — per
  packet, exactly where round 1 already pointed (`:633`).
- `Domain/Rewards/ModifierTable.cs:177-186` (`OffensiveEffects`) — add
  `ModifierEffectType.ElementalDamagePercent`, classified offensive beside
  `ElementalDamageOnHitPercent` for the identical reason ("something a landed
  hit... triggers"). **Never actually authored via `modifiers.json`** (only
  the reward track appends it, in P4), so `IsOffensiveModifier` is never
  called on it in production today — but the classification exists so the
  vocabulary stays honest if a future modifier ever wants the same rider, and
  because the test below requires SOME answer.
- `Tests/EditMode/Content/ModifierTableTests.cs:361-378` (the `covered`
  HashSet) — add `ModifierEffectType.ElementalDamagePercent`, or
  **`ModifierTableTests` fails immediately** the moment the enum member is
  added: it walks `Enum.GetValues(typeof(ModifierEffectType))` and refuses any
  member absent from `covered` (`:380-388`). Missed entirely by round 1's
  §2/§4 read-site table, which cited only the append site and the combat hook
  — this exhaustiveness guard is a real, breaking touch point a Sonnet
  implementer would only discover by running the suite and reading the
  failure.

Run: `[D]` `ModifierEffectTests`, `ModifierTableTests`, `ItemModifierCombatHookTests`,
`FightSessionTests`.

New tests, `Tests/EditMode/Combat/ItemModifierCombatHookTests.cs` (beside the
existing `ElementalDamageOnHitPercent` tests, same `Fighter`/`Session`/`Give`
helpers already in the file), exact literals:
```csharp
private static ResolvedSkill FireBolt() =>
    new ResolvedSkill("firebolt_fixture", "Firebolt", "", "hero", 1, SkillEffect.DamageSingle,
        SkillTargeting.SingleEnemy, 0, 0, false, 0, 0, false,
        new[] { new DamageInstance(DamageType.Fire, 20) },
        SpellPresentation.None, 0);

[Test]
public void AFireSpellWithTenPercentFireOnTheCasterDealsTwentyTwoNotTwenty()
{
    var hero = Fighter("Hero", true, attack: 20, speed: 10);
    var foe = Fighter("Foe", false, maxHealth: 100, speed: 1);
    Give(hero, new ModifierEffect(ModifierEffectType.ElementalDamagePercent, 10, against: DamageType.Fire));

    var session = Session(new CombatEncounter(new[] { hero }, new[] { foe }));
    Assert.IsTrue(session.CastSkill(FireBolt(), foe));

    // 20 base * (1.0 skill/scaling multiplier) * 1.10 Fire bonus = 22,
    // Rounding.AwayFromZero, unmitigated against a target with no Fire
    // resistance/defense and DamageVarianceRange 0.
    Assert.AreEqual(100 - 22, foe.CurrentHealth);
}

[Test]
public void AFireSpellWithTenPercentIceDealsTheUnmodifiedTwenty()
{
    var hero = Fighter("Hero", true, attack: 20, speed: 10);
    var foe = Fighter("Foe", false, maxHealth: 100, speed: 1);
    Give(hero, new ModifierEffect(ModifierEffectType.ElementalDamagePercent, 10, against: DamageType.Ice));

    var session = Session(new CombatEncounter(new[] { hero }, new[] { foe }));
    Assert.IsTrue(session.CastSkill(FireBolt(), foe));

    // the Ice bonus does not apply to a Fire packet -- unmodified 20.
    Assert.AreEqual(100 - 20, foe.CurrentHealth);
}
```
Both rely on the same defaults `ItemModifierCombatHookTests` already
establishes: `KitFor(actor) == null` so `SkillPowerMultiplierFor` is `1f`, and
`actor.SkillScaling` unassigned so `SpellScalingMultiplierFor` is `1f` — the
file's own header already explains both short-circuits, so the 22/20 literals
are the whole computation, not a partial one.

---

### P5 — The screen and the controller
**Main tree. `run_tests_parallel.ps1 -BuildScenes`. Depends on P3, P4.**

- `Editor/SceneBuilder/ScreenRegistry.cs:925-929` — `cardArtByLevel` becomes
  `cardArtByReward` and a new `markByReward`, both
  `Enum.GetValues(typeof(TrackReward))`-sized, `LoadSpriteByKey` per kind.
  `LoadSpriteByKey` resolves `proc:` keys off `ProceduralSpriteBaker.GeneratedDir`
  (`SceneBuilder.cs:252-258`), so the rail marks bind exactly as the card art does.
- `Editor/SceneBuilder/ScreenRegistry.cs:810-826` — capture both wiring returns
  and assign `dossierController.trackScreen` and `.inDescent`, deleting the
  `GetComponent` at `:820` (§7, §8).
- `Domain/UiKit/Screens/RewardTrackScreen.cs:723-730` — `TrackIcon{level}` is
  emitted with the neutral `proc:ring_outline` and no per-level lookup;
  `:712-715` the mat is emitted untinted. **The comment at `:719-722` — "Keyed
  at BUILD time because the track is static — level 40 is an offer reroll in
  every save there will ever be, so there is nothing here for a controller to
  decide" — is the exact claim this package falsifies and must be rewritten,
  not left standing above the changed code.**
- `Core/RewardTrackController.cs:66` — the two arrays; `:184-185` and the
  deleted `ClaimedLevel()` at `:220-239` — the selected character; `:262-332` —
  `PaintNode` writes `icons[i].sprite` and `mats[i].color` from the resolved
  entry (`MatTintFor` at `:295`); `:467-531` — `PaintCard` subscripts by kind
  (`cardArtByLevel` at `:488-489`, `MatTintFor` at `:529`); `:323, 417, 568` —
  names off the character's own definition. Also `:394` (`NextRewardLevel`),
  `:461` (`MaxLevel` clamp) and `:471` (`At`), which the plan previously
  missed and which read the deleted static table.
- `Core/CharacterDossierController.cs:825-834, 914-939` — `ShowFor` and
  `PaintTrackLine` (`:918, 927`) through the character's own track.
- `Core/SquadTrack.cs` — `BestLevel` (`:30-43`) and `HasUnlocked` (`:46-47`)
  deleted; header shrinks, including the max-not-sum argument §6 reverses.

Run: `[D]` `RewardTrackLayoutTests`, `UiKitAuditTests`, `SystemMenuScreenTests`,
`SystemMenuPaneTests`, `UiBindingContractTests`, `UiWiringSweep` via the build;
`[U]` `SystemMenuCaptureTests`, `ScreenWiringTests`, `RuntimeScreenshotTests`,
`RewardTrackClaimTests`.

**None of the `[D]` classes above actually calls `UiAudit` against a real
`SystemMenuScreen.Build()`/`RewardTrackScreen` tree** — verified by grepping
every `UiAudit.Run`/`RunAllFrames` call site under `Tests/EditMode/Ui/`.
Reckoning and the Dossier both have a standalone `[D]` test that builds the
real screen and audits it (`ReckoningScreenTests.TheScreenAuditsCleanAtEveryFrame`
building `ReckoningScreen.Build().Root`; `CharacterDossierScreenTests.TheScreenAuditsCleanAtEveryFrame`
building `CharacterDossierScreen.Build().Root`); `SystemMenuScreenTests` and
`SystemMenuPaneTests` only check arithmetic and node counts against the tree
(their own file headers say why: the runtime three-tab narrowing "cannot be
seen" by `UiAudit`, so they stand in for it with arithmetic — but that
explains why the RUNTIME shape isn't audited, not why the BUILD-TIME shape
(the RewardTrack panel included) isn't either). **The only thing that actually
audits `RewardTrackScreen`'s layout is the real scene build** — `UiEmitter.Emit`
(`Editor/SceneBuilder/UiEmitter.cs:26`) calls `UiAudit.RunAllFrames` for every
screen inside `SceneBuilder.BuildScene`, which only runs under `-BuildScenes`.
So a worktree agent has no fast, `[D]`-only way to catch a RewardTrack-panel
layout regression before the scene build; P5 is correctly main-tree already,
but its own `Run:` line should not be read as pre-build layout coverage for
the panel it changes — only `-BuildScenes` itself is. Not a defect to fix in
this plan (adding a standalone `[D]` audit test for `SystemMenuScreen` is a
real, separate improvement, worth an `AUDIT.md` entry rather than scope creep
here).

**`-BuildScenes` runs four checks per screen, not "the binding contract
tests"**: `UiTextFitAudit.Run`, `UiCountAudit.Run`, `UiWiringSweep.Run` and
`UiBindingAudit.Run` (`SceneBuilder.cs:118-121`), plus `UiAudit.RunAllFrames`
one level down inside `UiEmitter.Emit` (`:26`) — five checks in total.
`UiBindingContractTests` (the `[D]` EditMode class) is a DIFFERENT thing: it
pins the wrong-binding MESSAGE TEXT (`UiBindingContract.WrongNode`'s wording),
reachable with no scene at all (`Tests/EditMode/Ui/UiBindingContractTests.cs`'s
own header: "the comparison itself lives in Editor's UiBindingAudit... this
file covers the half that can be reached from here"). It runs in the ordinary
`[D]` suite regardless of whether `-BuildScenes` ever runs, and passing it
proves nothing about whether the real `UiBindingAudit` accepted this
package's actual wiring.

New test, `Tests/EditMode/Ui/RewardTrackScreenTests.cs`:
`EveryRewardKindBindsAMarkAndACardSprite` — over `Enum.GetValues`, every
non-`None` kind yields a non-empty key from both `IconFor` and `CardArtKeyFor`,
and the twelve card keys are distinct.

---

### P6 — Author sheep and owl
**Parallel worktree for authoring + `[D]`; `tools/build_content.ps1` in the main
tree. Depends on P2, P3, P4b** (`ElementalDamagePercent` must exist as a
`ModifierEffectType` member and pass `ModifierEntryResolver`'s enum-name check
before `"against": "Fire"` etc. can resolve — though the JSON itself never
authors `ModifierEffectType`, only `TrackReward`, the pin test's assertions
about what the track ultimately grants read through to the same enum).
**Its own JSON content must land in the main tree before P4's new
`RewardTrackClaimTests` literals can pass** — see the Execution order
subsection.

`reward_tracks.json` gains the two tracks from §5 (Odette's now Fire/Ice/
Lightning/Arcane, restored from round 1's Arcane-only draft — Round 2, Part A —
with Ice and Lightning milestone-only per validation rule 4, and every magnitude
retuned in round 3). **Author from §5's tables, not from this section's
summary** — §5 is the single place both the milestone rows and the filler counts
are written out, and it is what P4's and P6's pinned literals were computed
from.

New test, `Tests/EditMode/Content/RewardTrackContentPinTests.cs` — reads the
real JSON through the real resolver (the `ContentStampIdsTests` two-parser
pattern), literal expectations:
- sheep level 25 is `SignatureCapacity 5`; level 30 is `UnlockSkill
  static_fleece`; level 60 is `SignatureAbsorbs`; level 100 is
  `SignatureCapacity 10`.
- owl level 10 is `UnlockSkill frost_flare`; level 40 is
  `ElementalDamagePercent Fire 10`; level 45 is `ElementalDamagePercent Ice 15`;
  level 70 is `ElementalDamagePercent Lightning 20`.
- sheep's fully-collected totals: capacity `+27` (→ `Max` 37), per-turn `+2`,
  on-hurt `+2`, Nature `+30`, max health `+150`, stat points `50`.
- owl's fully-collected totals: Fire `+28`, Arcane `+26`, Lightning `+20`,
  Ice `+15`, max mana `+50`, mana regen `+3`, max health `+150`,
  stat points `50`.
- **`NoFillerElementLandsBeforeItsSource`** — the mechanised half of validation
  rule 4, asserted over the shipped tracks rather than only refused at author
  time: neither track has a filler `ElementalDamagePercent` node whose element
  is absent from the character's level-1 damage sources. Owl's Ice and Lightning
  are milestone-only and sheep's Nature is his `attackType`, so it passes; the
  round-2 draft's Lightning filler at level 16 is what it would have caught.
- **`AnElementRoutesToExactlyOneEffectType`** — over both shipped tracks and
  every `DamageType`: a collected elemental total produces exactly one
  `ModifierEffect`, `ElementalDamageOnHitPercent` when the element is the
  character's `attackType` and `ElementalDamagePercent` otherwise, never both
  and never neither (§2's routing rule).
- both: level 20 `Respec`, level 80 `StatPoint 10`, level 90 `SecondLife 1`.
- `placeholder_brawler` has no authored track and `RewardTracks.For` returns a
  default whose totals are `50` and `+229`.
- `EveryRewardKindIsGrantedBySomeShippedTrack`, replacing P3's deleted
  `EveryRewardKindTheEnumKnowsAboutIsActuallyGranted`: over
  `Enum.GetValues(typeof(TrackReward))` minus `None`, every kind appears
  somewhere in sheep ∪ owl ∪ the default. All twelve do — StatPoint, MaxHealth,
  Respec and SecondLife from the spine and the default;
  SignatureCapacity/GainPerTurn/GainOnDamageTaken/Absorbs from sheep;
  MaxMana and ManaRegen from owl; ElementalDamagePercent and UnlockSkill from
  both. The vacuity guard is asserting the count is 12, so a kind added without
  content fails here rather than shipping unreachable.
- **`against` is authored `"Fire"`, `"Ice"`, `"Lightning"`, `"Arcane"` (owl) and
  `"Nature"` (sheep) — never `"Frost"`** — see §13, and see §5's own
  authoring-hazard note: Odette's level-45 Ice node sits right after Frost
  Flare's own `skills.json` entry, which authors `"Frost"` as an alias
  `SkillEntryResolver.TryParseDamageType` accepts and `reward_tracks.json`'s
  resolver does not. A track authoring `"Frost"` is refused where a skill
  authoring it is accepted, and the refusal is the intended behaviour, not a
  bug to work around in the JSON. This test pins the correct spelling
  (`"Ice"`) specifically to catch that copy-paste before it ships.

---

### P7 — The dossier minus
**Main tree. `run_tests_parallel.ps1 -BuildScenes`. Depends on P4 (`Character.cs`).**

Everything in §7.

Run: `[D]` `CharacterDossierScreenTests`, `UiKitAuditTests`;
`[U]` `DossierEquipTests`, `DossierPackCaptureTests`, `DossierXpBarTests`,
`SystemMenuCaptureTests`.

New tests:
- `[D]` `CharacterDossierScreenTests.TheMinusSitsOppositeThePlusInsideItsCell` —
  solved rects: minus centre `(-42.67, 29)`, size `22`; plus centre
  `(42.67, 29)`; both inside the `111.33 × 84` cell; the gap between them is
  `63.33`. Literals, not recomputed from `DossierLayout`.
- `[D]` a `RefundTests` pair in `Tests/EditMode/Hub/`: investing 3 into STR then
  refunding twice leaves `investedAbilityScores.strength == 1` and
  `unspentStatPoints` back up by 2; refunding a score with 0 invested returns
  false and changes nothing.
- `[U]` `DossierRefundTests` in `Tests/PlayMode/Ui/`, beside `DossierEquipTests`
  — the hub copy shows the minus, the map copy (`inDescent: true`) does not,
  the fight copy (`lockedForFight: true`) does not.

---

### Parallelism

| Can run concurrently | Why |
|---|---|
| P2 and P3 after P1 | disjoint files; P3 touches only `Domain/Progression` + `Domain/UiKit`, P2 only `Domain/Content` + `Editor` + `Core/Content` load |
| P4b alongside everything, including P1 | touches only `Domain/Combat` + `Domain/Rewards` + their tests; no dependency in either direction on `Progression`/`Content` |
| P6 alongside P5 | content JSON and tests only |

**P4 and P7 both edit `Data/Character.cs` and must not share a worktree.**
P1, P4, P5 and P7 all need the main tree (scene or content build). **P4b is
worktree-only** (`[D]`-verifiable, no `[U]` class and no content/scene build in
its own Run: line) despite feeding a main-tree package.

**P4's new PlayMode pin tests need P6's content, not merely P3's types** (see
P4's own note above) — this is a real dependency the package headers do not
state, because it is a CONTENT dependency (what `reward_tracks.json` says) on
top of the stated CODE dependency (what compiles against). The Execution order
subsection below sequences around it.

### Execution order

Five worktree lanes, one main-tree lane, in this order:

1. **P1** (main tree) lands first and alone — every other package depends on
   it directly or through P3. Commit, gate `run_tests_parallel.ps1 -BuildScenes`
   (the Reckoning row narrows).
2. **P2, P3, P4b** run concurrently, each in its own worktree, each verified
   by its own `Run:` class list only (`tools/test.ps1 <ClassA,ClassB,...>` —
   explicit class names, never an area name or `-Changed` or a bare run: any
   of those three from a worktree tries to build a second Unity TestRunner
   copy beside the real one). None of the three touches a file either of the
   others does (§ table above), so integration back to main is three
   sequential `git` merges (or three sequential patches applied to main) in
   any order, each followed by its own commit — not a three-way merge that
   has to reconcile anything.
3. **P6** (worktree for authoring + `[D]`) starts as soon as P2 and P3 are on
   main (it depends on both, plus P4b for the enum name to resolve through
   `ModifierEntryResolver`'s validation, though nothing in `reward_tracks.json`
   authors `ModifierEffectType` directly). **P6's `reward_tracks.json` must be
   integrated to main, and `tools/build_content.ps1` run against it, BEFORE
   P4's verification gate runs** — P4's new `RewardTrackClaimTests` literals
   are pinned to the shipped sheep/owl numbers (see P4's own note), so P4's
   `-BuildContent` gate will fail on those five tests if it runs against an
   empty `tracks: []` file. This is the one place the plan's package numbering
   (P4 before P6) and its actual build order (P6's content before P4's gate)
   diverge — sequence by content readiness, not by package number.
4. **P4** (main tree) lands after P3, P4b (code) and P6 (content) are all on
   main. Commit, gate `run_tests_parallel.ps1 -BuildContent`.
5. **P5** (main tree, depends on P3 + P4) and **P7** (main tree, depends on
   P4) land one at a time — **not concurrently**, because both are main-tree
   and P4/P7 already share `Data/Character.cs`-adjacent surface closely enough
   that the plan calls out not sharing a worktree; on a single main tree
   "not concurrently" means one full commit-and-gate cycle before the next
   starts, not a parallelism question. Each: commit, gate
   `run_tests_parallel.ps1 -BuildScenes`.

**Every gate — `[D]` or main-tree — runs in ONE foreground `Bash`/`PowerShell`
call with a 600000 ms (10-minute) timeout**, never backgrounded: a subagent
that backgrounds a gate and waits on it never wakes (session memory,
"Subagents must block on gates in foreground"). **Before any gate that touches
the TestRunner path** (anything with `-BuildContent`/`-BuildScenes`, or a bare
`run_tests_parallel.ps1`), check for a running `Unity.exe` on
`C:\Games\Prince's Palace-v2-TestRunner`/`-TestRunner2` first (e.g.
`Get-Process Unity -ErrorAction SilentlyContinue | Where-Object Path -like
"*TestRunner*"`) — a leftover batchmode or open-Editor instance on that path
collides with a fresh run rather than queuing behind it.

**Three other interactive sessions are open on this repo today** (git status
snapshot at the top of this conversation). Stage by explicit path for every
commit in every step above — never `git add -A`/`git add .`/`git add -u`/
`git commit -a` (CLAUDE.md, `tools/githooks/deny_broad_staging.py`) — a broad
stage on a shared working tree sweeps another session's uncommitted work into
this plan's commit.

---

## 11. Verification

```
powershell -NoProfile -ExecutionPolicy Bypass -File tools/test.ps1 run,ui,content
powershell -NoProfile -ExecutionPolicy Bypass -File tools/build_content.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/content_schema.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File tools/run_tests_parallel.ps1 -BuildContent -BuildScenes
powershell -NoProfile -ExecutionPolicy Bypass -File tools/preview.ps1 -Character owl
```

Sync `Scenes/*.unity`, `Resources/Content/`, `Art/` and every new `.meta` back to
main in the same pass (CLAUDE.md gotchas 1-3).

### How we know it worked

- A level-71 Odette with 70 collected has one
  `ModifierEffectType.ElementalDamagePercent` effect on her fight combatant,
  `Magnitude 20`, `Against Lightning`; the same Odette with 69 collected has
  none. Cast Lightning Bolt and it hits 20% harder than an otherwise-identical
  Odette with the node uncollected — that is the thing to look at in a real
  fight, not just the effect's presence on the combatant, and it is true
  regardless of what her `attackType` is (P4b's whole point).
- The same Odette's Arcane line is an `ElementalDamageOnHitPercent`, not an
  `ElementalDamagePercent`, and Shawn's Nature line likewise — one effect per
  element, routed on `attackType` (§2). Shawn's plain swing hits ~30% harder at
  level 100 than at level 1 for that reason alone; a Shawn whose Nature line
  had been routed to the packet hook instead would swing for exactly the same
  as an unlevelled one, which is the regression this bullet exists to catch.
- A level-26 Shawn with 25 collected walks into a fight with a wool meter whose
  `Max` is 18; at 24 collected it is 13.
- Shawn's wool meter reads 20 at level 39 and 37 at level 100; Odette's mana
  pool reads 100 with 8 a turn at level 100, against 50 and 5 at level 1.
- Every level from 2 to 100 shows something on every character's track: no
  empty node, no blank caption, and the twelve landmarks are the same twelve on
  all three (`EveryMilestoneLevelCarriesSomething`, plus reading the screen).
- The level-80 node still pays ten stat points and the filler still pays forty:
  a character who has collected the whole track has 50 to spend, spends them
  from the dossier, and takes them back one at a time with the minus.
- A squad member past level 90 still contributes a second life, and three past
  90 contribute three (§6) — the reward the ask says to keep is kept, and it is
  now sourced per character rather than off the squad's best level.
- A level-11 Odette with 10 collected has Frost Flare in her fight kit; at 9
  collected she does not.
- Opening the reward track from Shawn's dossier page and then stepping to
  Odette's and reopening it shows two different sets of captions on the same
  ninety-nine nodes, at the same twelve landmarks — and the difference is
  visible before the first landmark: level 3 reads `+1% NATURE DAMAGE` on him
  and `+10 MAX HEALTH` on her, level 4 `+10 MAX HEALTH` and `+2 MAX MANA`,
  level 6 `+1 WOOL CAPACITY` and `+2% FIRE DAMAGE`. Nine of their twelve
  milestones differ.
- `placeholder_brawler`'s track opens, reads plainly (health and stat points),
  and collects — no exception, no blank captions.
- A save written before the change opens with every level from 2 to the
  character's own showing as waiting, the collect button counting them, and
  zero unspent stat points until it is pressed.
- The hub dossier shows a "-" beside every attribute with a point in it; the
  map and fight dossiers show none; `UiAudit` passes at four aspects with no new
  `AllowOverlap`.
- Nothing in the game grants Favor, extra experience, extra starting relics, a
  wider offer, a reroll or a guaranteed rest: `grep -rn "TrackReward\." Assets`
  returns only the twelve surviving kinds.
- A descent still begins with exactly one relic to draft, as it did before
  (§3e2), and the shop's own gold-priced reroll still works.
- A migrated save's characters whose gear leaned on invested points show those
  slots greyed until the points are re-spent, and the dossier says which (§9).

---

## 12. Open questions for the owner

1. **Should a second life revive only the character who collected it?**
   Default: no — the source is per-character, the spend stays squad-wide
   (§6), because `TrySecondLife` only fires on a would-be wipe and an
   owner-only revive would spend the charge and still lose the fight.
2. **Shawn's wool absorbing damage again (level 60).** The talent rework
   deliberately turned this off (`SignatureResource.cs:40-54`: "banking for
   armour and banking to spend are the same decision made twice"). Default:
   keep it as authored above, as a late-track reward rather than a baseline —
   at level 60 the tree is long since bought, so the tension the rework removed
   is a reward rather than a confusion. Say the word and it becomes another
   capacity step.
3. **50 stat points, not 40.** Today's track pays 50 (40 filler singles plus
   the level-80 block of ten); the brief says 40. Default: kept at 50, matching
   what ships.
4. **The generated default's +229 max health** against an authored track's +150
   (§5). Default: left as is, and stated as the price of having no flavour.
5. **A retune after v5 reinterprets `claimedTrackLevel` all over again.** The
   version bump in §9 fixes the one-time v4 → v5 case; it does not make future
   edits to `reward_tracks.json` safe, because a watermark of 40 against a
   retuned table still means something different. Default: **accept it, and
   bump `CurrentVersion` with any retune that moves a milestone's kind**, which
   is a line in `Migrate()` and a one-line note in the JSON's `_readme`. The
   alternative — storing what was collected rather than deriving it — is the
   thing §2 argues against, so this is the price of that argument and should be
   written down beside it rather than left to be rediscovered.
6. **Static Fleece at Shawn's level 30 has no VFX.** It is the only book spell
   that spends wool, which is why round 3 put it there in place of `mud_burst`
   (a bog spell the Bog Witch already casts, with no connection to the fleece) —
   but `mud_burst`, `frost_flare`, `cinderfault` and `lightning_bolt` all carry
   a finished `vfx` block in `skills.json` and `static_fleece` does not, so it
   will cast without a spell animation. Default: **ship it and commission the
   VFX separately** — the thematic fit is the point of the node and a missing
   animation degrades gracefully. Say the word and level 30 goes back to
   `mud_burst`, which changes nothing else on the track.
7. **Odette's four elemental lines are not four equal things.** Her Arcane line
   is her `attackType`, so under §2's routing rule it pays on every landed hit,
   including her Fire casts; Fire, Ice and Lightning pay only on their own
   packets. Default: **accept it** — it is the engine's existing model (an
   `Astral` affix behaves identically today), the track is authored rather than
   chosen so there is no investment decision it distorts, and "the arcane caster
   who has specialised into fire" is a readable fantasy. The alternative is
   dropping her Arcane line for a fourth narrow element, which would cost her
   the one line that scales her basic bolt.

---

## 13. Noticed in passing, out of scope

`SkillEntryResolver.TryParseDamageType` (`:483-500`) accepts `"Frost"` as `Ice`;
`ModifierEntryResolver.cs:224`, `RelicEntryResolver.cs:159` and
`EnemyEntryResolver.cs:126` each parse `DamageType` by name without it. A track
entry authoring `"against": "Frost"` would therefore be refused where a skill
authoring `"type": "Frost"` is accepted. The new resolver follows the majority
(plain `Enum.TryParse`, no alias) rather than adding a fourth copy of the rule.
Worth an `AUDIT.md` entry and a shared `DamageTypeNames.TryParse`, separately.
This is live for `frost_flare` specifically, which authors `Fire 7 + Frost 7`
and resolves to `Fire + Ice` — an author copying that word onto a track entry
gets a refusal, which is why P6 pins the spelling.

A second one, found verifying §2: `ModifierEffectSet.cs:17-18` cites
`ModifierEffectsAreEmptyForRealCharactersTests` as the test proving real
combatants carry `Empty`. **No such test exists** — grepped across
`Scripts/`, the only occurrence is that comment. It is a dangling reference,
not a coverage gap this plan opens, but the sentence it appears in ("proves
that with a real test rather than an assertion") is untrue today and stops
being true in principle once P4 appends track effects to that set. Worth an
`AUDIT.md` entry: either write the test or delete the claim.

---

## Revision log

### Round 1 — architecture

**Verified as written.**

- Assembly layout: five asmdefs, `Domain` with `noEngineReferences: true` and no
  references; `Core` at `Scripts/` root covering `Core/` and `Data/`; `Editor`
  and both test assemblies as `CODE_STANDARDS.md` §1 states. `DamageType` is
  `Domain/Stats`, so `TrackEntry` can carry it with no reference added.
- **Every read site in §2 takes a `Character`, not a definition and not an id** —
  `EffectiveStats` (`ContentDatabase.Effective.cs:157`), `BuildSignatureResource`
  (`:236`), `ModifierEffects` (`:340`), `EffectiveAbilityScores` (`:532`),
  `EffectiveMaxMana` (`:541`), `AvailableSkillsFor` (`ContentDatabase.cs:236`).
  Nothing has to be threaded in for `claimedTrackLevel` to reach any of them.
- `ModifierEffect`'s constructor is
  `(ModifierEffectType, int magnitude, int threshold = 0, DamageType? against = null, bool againstMagical = false)`
  (`ModifierEffect.cs:370-379`), so `new ModifierEffect(…, n, against: type)`
  compiles.
- Riders stack rather than collapsing: `ApplyModifierOnHitRiders`
  (`FightSession.cs:464`) walks `effects.All` at `:484`, not `.Best`.
- `SecondLifeCharges` has exactly one production write, `RunOrchestrator.cs:317`,
  reached by `FightBootstrap.cs:308`. `SquadTrack.SecondLivesLeft` is at `:76-82`.
- `ModifierEffects` reaches combat at exactly one place,
  `FightEncounterAdapter.cs:233`.
- MaxMana and ManaRegen both have real seams: `StatBlock.manaRegen` (`:32`),
  `AbilityDerivation.ManaRegenBonus` (`:130-135`) folded in at
  `Effective.cs:205`, `state.ManaRegen = stats.manaRegen`
  (`FightEncounterAdapter.cs:175`), `EffectiveMaxMana` reaching
  `CombatantState.MaxMana` at `:171`.
- `TalentController.cs:340, 379, 414` are exactly the three `Respec` gates.
- `SaveData.CurrentVersion = 4` (`:42`), `Migrate()` at `:335-388`, stamp at
  `:385`; the `if (version < N)` chain is the shape §9 describes.
- `Character.cs` citations `:26-33, 106, 133, 153, 164, 183, 202-213, 214-224,
  242-248, 374-405`, and the JsonUtility notes at `:43, 99, 131`.
- The §7 dossier geometry, recomputed from source: `ContentCWidth = 340`
  (`DossierLayout.cs:632`), `AttributeCellWidth = 340/3` (`:646`),
  `AttributeCellHeight = 86` (`:643`), plus 22 at inset 13 (`:640-641`), cell
  emitted at `width-2 × height-2` (`CharacterDossierScreen.cs:877`), plus placed
  at `(halfWidth-13, halfHeight-13)` (`:886-889`). Half-extents 55.67 × 42, plus
  centre `(42.67, 29)`, 2px clearance, mirrored minus at `(-42.67, 29)`, 63.33px
  gap. `DossierAttrValue{i}` is 90×36 at `(0,10)` and `.AsDecor()` (`:867-868`),
  and `UiAudit.CheckSiblingOverlap` skips any decor pair at `UiAudit.cs:346`, so
  no `AllowOverlap` is needed. **All of §7's arithmetic checks out.**
- `UiBindingNames.ScreenMemberFor` (`:19-27`) only re-cases the first letter, so
  `attributeMinuses` maps to `AttributeMinuses` and nothing fuzzy is involved.
- `controller.inDescent = inDescent` at `ScreenRegistry.cs:808`;
  `ShowTrack()` at `CharacterDossierController.cs:825-834`; `trackPanel` bound at
  `:811-826`; `cardArtByLevel` (99 entries) at `ScreenRegistry.cs:925-928`.
- §1's nine bake points, all nine: `RewardTrackScreen.cs:608, 617-627, 658-666,
  723-730, 773, 786-788, 1077-1088, 1100-1130`, `RewardTrackLayout.cs:200-201`,
  `ScreenRegistry.cs:925-929`. `FirstLevel`/`NodeCount` at
  `RewardTrackLayout.cs:28-29` give 99 nodes, 12 milestone, 87 filler.
- `NodePitch = 190` (`:50-56`) and `NextRewardWidth = 420` (`:405-412`) are both
  sized off the 38-character `SecondLifeRefresh` name, and every surviving name
  is shorter.
- Exactly twelve card-art key constants at `RewardTrackScreen.cs:191-202`, under a
  header that already calls them placements (`:185-188`).
- `ContentInputHash.Sources` (`:56-61`) globs `ContentData/*.json`
  non-recursively (`:58`) and `Domain/Content/*.cs` recursively (`:59`), so the
  new JSON and every new `Raw*`/`Resolved*`/resolver file are hashed for free.
- Test-class names and `[D]`/`[U]` labels: all correct (EditMode is `[D]`,
  PlayMode is `[U]`) except the one below.

**Citations corrected.**

- `ContentDatabase.cs:317` to `:316` for the `CharacterId` gate (§3f); the
  surrounding comment is `:280-297`, not `:280-296`.
- `SignatureAbsorbs` seam `Effective.cs:282` to `:284`; `BuildSignatureResource`
  `:236-284` to `:236-285`; talent terms `:264-271` to `:264-272`.
- `FightSession.cs:485-520`: the rider loop is `:484` onward inside
  `ApplyModifierOnHitRiders` at `:464-560`; the same-element branch `:511-518` to
  `:510-521`.
- `FightSession.Outcome.cs:117-121` to `:114-121`.
- `SkillEntryResolver.cs:26` to `:28-31` (`:26` is `DefaultFlatAmount`).
- `RawSkillEntry.cs:317-320` is out of range, the file is 293 lines; the
  enum-as-string argument is `RawSkillEntry.cs:200-203` and `ContentSchema.cs:47-56`.
- `ContentSchema.cs:30-43` to `:31-45`; `:59-70` to `:57-85`. Added that `FileMap`
  is alphabetical by filename (`:26-27`), so the new row sits between
  `relics.json` and `skills.json`.
- `ContentDatabase.cs:333`: `Reset()` is `:325-343`; the addition goes at
  `:327-336` and the `RewardTracks.Reset()` call beside `:342`.
- `ContentDatabase.Validation.cs:154-174` to `:154-176`.
- `AbilityDerivation.cs:130-134` to `:130-135`.
- `OfferRowLayout.MaxCards` `:73` to `:72`; P1's `:71-77` to `:62-76`.
- `RewardTrack.cs:181-186` to `:182-185` for the gold comment; the `Spread` swap
  block `:560-576` to `:554-576`.
- `RewardTrackLayout.cs:942-943, 962-978, 986-996`: the actual deletions are
  `IconFor` `:941, 943`; `CardArtFor` `:964-965, 967-972, 974-975`; `MatTintFor`
  `:991, 993`.
- The three system-menu call sites `:120-121, :513-514, :771-772` become
  `:121-122, :514-515, :772-773`.
- §7's "the dossier itself takes only `lockedForFight` (`:868`)": `:868` is
  `controller.exitLabels`; `WireDossier`'s signature is `:954-955` and its
  assignment `:973`.
- §8 attributed all three deleted methods to `Input.cs`. `BestLevel()` is
  `SquadTrack.cs:30-43`, `ClaimedLevel()` is `RewardTrackController.cs:220-239`,
  and only `BestCharacter()` is `RewardTrackController.Input.cs:173-202`.
- `ReckoningController.Reroll` `:467-485` to `:467-491`; `PaintReroll`
  `:493-509` to `:494-510`; the hidden-when-zero line `:504` to `:505`.
- `RunManager.cs:294-296` to `:294-297`.
- `Spend` `:846-862` to `CharacterDossierController.cs:836-867`;
  `PaintStatSpending` `:870-901` to `:869-900`; `PaintTrackLine` `:914-938` to
  `:914-939`.
- `SignatureResource.cs:40-54` to `:39-54`.
- P4's `[D] SignatureAbsorptionTests` **does not exist**; the absorb coverage is
  inside `SignatureResourceTests` (`Tests/EditMode/Combat/`) and
  `RelicMechanicsTests`. Removed from the list.

**Design changes forced by the code.**

- **Odette's Fire/Ice/Lightning milestones are replaced by Arcane, mana and
  spells (§5).** `ElementalDamageOnHitPercent` compares its element against
  `AttackTypeOf(actor)` (`FightSession.Ledger.cs:213-214`), which for a skill
  cast is still the caster's own authored `attackType` and never the spell's;
  `FightSession.Skills.cs:558-563` states this outright. Odette's `attackType` is
  `Arcane` (`characters.json`), so a Fire node would take the foreign branch at
  `FightSession.cs:535` and pay `Attack × 10%` floored at 1, on every landed hit,
  regardless of what she cast. Three of her twelve milestones would have been
  worth one or two points. Arcane, her own type, takes the same-element branch at
  `:510-521` and scales with everything she does. The per-element hook that
  *would* work is a new `ModifierEffectType` read inside `ResolveDamageInstances`
  (`FightSession.Skills.cs:619-649`, where `instance.type` is the real element and
  the multiplier applies per packet at `:633`) — a new combat hook, deliberately
  expensive per `ModifierEffect.cs:31-34`, and now open question 5.
- **P1's interim `FillerMix` changed from `StatPoint 1x40, MaxHealth 10x15,
  MaxHealth 2x32` to `StatPoint 1x40, MaxHealth 2x47`.** Both sum to 87, but the
  first paid 349 max health while §5's default and P3's
  `TheDefaultTrackPaysFiftyStatPointsAndTwoHundredTwentyNineMaxHealth` both say
  229 — and P1 claims the interim table *is* the eventual default. Two `MaxHealth`
  rows in one mix would also have made
  `TheTrackHandsOutTheMixItDescribes`'s per-kind grouping ambiguous. P1's new
  pinned test is renamed to match.
- **`RewardTrack.BaseStartingRelics` is moved to `RelicPool`, not deleted (§3e2).**
  `SquadTrack.StartingRelics()` has one caller, `RunOrchestrator.cs:170`
  (`DraftHasAnotherRound`), and `RunOrchestrator` is production — its own header
  says "THE RULES OF A RUN, IN ONE PLACE, WITH TWO CALLERS", and
  `HubController.cs:433` is the first. The plan's replacement,
  `RelicPool.OfferCount` (`:43`), is how many CARDS a round shows, not how many
  ROUNDS there are: it would silently make every descent start with three relics
  instead of one. A `RelicPool.StartingRelicsPerDescent = 1` preserves shipped
  behaviour with one home for the number.
- **`ContentBuilder.BuildSkills()` must return an id-to-displayName map and
  `BuildRewardTracks` must run after it**, for the reason `:126-129` gives about
  achievements preceding relics. `BuildSkills()` returns void today (`:123`), and
  `EnsureFolder(RewardTracksPath)` needs a line at `:81-90`. None of this was in
  §4's touch-point table.
- **`TrackEntry` has to grow** `Against`, `SkillId` and `SkillDisplayName`; the
  plan described the resolver baking a display name onto the entry without saying
  the Domain struct changes.
- **`RewardTracks.Reset()` must be *called from* `ContentDatabase.Reset()`**, not
  merely declared beside it. `Reset()` (`:325-343`) already calls
  `CharacterPortraits.Reset()` at `:342` for exactly this reason: "a test that
  installs its own characters would otherwise see the last one's face."
- **`_rewardTracks` must load inside `EnsureLoaded` (`:705-722`), not lazily.**
  `EnsureLoaded` short-circuits on `_characters != null` at `:707`, so a lazy
  load re-entered from `AvailableSkillsFor` would find the guard already true and
  return without it. Loading in the block is safe: `AvailableSkillsFor` calls
  `EnsureLoaded()` at `:238` and reads the track at `:312`.
- **The `AvailableSkillsFor` addition is the fifth route, not the fourth** — the
  code already calls `LearnedThisRun` the fourth at `:272`, and that comment has
  to be renumbered with it.
- **§8's typed `trackScreen` binding is an addition to a `GetComponent`, not a
  replacement — unless both discarded returns are captured.** `WireDossier`
  (`:810`) and `WireRewardTrack` (`:813`) both already return their controller
  and both returns are thrown away, which is why `:820` re-finds the dossier with
  `GetComponent`. Capturing them removes that lookup instead of adding a second.
  The same locals carry `inDescent` without widening `WireDossier`'s signature to
  two adjacent transposable bools (`CODE_STANDARDS.md` §5).

**Collisions found, and how the plan now resolves them.**

- **§3a understated the `RestBeforeBoss` test surface.** Three tests sit on it,
  not one: `ALevelThirtySquadDescendsWithTheRestGuaranteed` (`:454-466`) is
  deleted whole; `AnUnlevelledSquadGetsNoGuaranteedRest` (`:445-452`) becomes a
  vacuity risk under `CODE_STANDARDS.md` §8 and needs its comment rewritten;
  `LevellingMidDescentDoesNotReshapeTheLegUnderThePlayer` (`:475-489`) keeps
  running with an input that can no longer vary.
- **§3c understated the `OfferReroll` surface.** Seven sites, including the
  listener at `ReckoningController.cs:216`, the repaint at `:630`, the
  `RerollSource` assignment at `FightController.Input.cs:497` and the wiring at
  `ScreenRegistry.cs:1016`. Added a "must not change" for `ShopController`'s
  separate gold-priced reroll (`:321, 369-372`), which a grep for "reroll" reaches.
- **Six book-only skills carry `characterId: "sheep"`, not four** — `static_fleece`
  and `golden_fleece` as well as the four §3f named. The code's own comment at
  `ContentDatabase.cs:285` says six.
- **The talent system enforces the opposite ownership rule** to §3f:
  `ContentDatabase.Validation.cs:154-176` refuses a talent granting another
  character's skill ("A character cannot hand out another character's kit"). The
  new cross-catalogue arm sits right beside it and must take only its first half.
  Added as §3h so the next reader does not restore the symmetry.
- **P1's test deletions were incomplete.** Six more `RewardTrackTests` methods
  break on the removals (`:60-78, 130-155, 156-161, 203-219, 476-487, 488-496`),
  and three test files were listed only under "Run", never under "Delete":
  `ItemOfferFavorTests` (10 sites, including
  `TheWiderOfferActuallyRollsAFourthItem`), `RewardApplierTests` (8 sites on
  `earnedFavor`/`bonusExpPermille`), `RewardTrackClaimTests` (5 sites on
  `earnedFavor`). Plus three stale comments:
  `SpeedAndBountyRelicTests.cs:276`, `RunSnapshot.cs:124`,
  `ProfilePresets.cs:142-145`.
- **`RewardTrackTests.EveryRewardKindTheEnumKnowsAboutIsActuallyGranted`
  (`:462-475`) breaks at P3, not P1**, because P3 deletes the static table it
  walks while adding twelve kinds only content grants. P3 now deletes it and P6
  replaces it with `EveryRewardKindIsGrantedBySomeShippedTrack` over sheep, owl
  and the default — all twelve are covered, checked kind by kind.
- **P5's `RewardTrackController` list missed three reads of the deleted static
  table**: `:394` (`NextRewardLevel`), `:461` (`MaxLevel` clamp), `:471` (`At`).
- **`RewardTrackScreen.cs:719-722`'s comment is the exact claim P5 falsifies**
  ("Keyed at BUILD time because the track is static — level 40 is an offer reroll
  in every save there will ever be"). Added to P5 explicitly.
- **The v5 migration makes gear inert.** Invested points are part of the
  equipment requirement floor (`ContentDatabase.Effective.cs:111-123`), so zeroing
  `investedAbilityScores` puts down any weapon that was liftable only because of
  them. Recoverable, visible (`SlotBlockedCaptions`), and the same state
  `Character.Respec` already produces — but it belongs in the migration step's own
  comment rather than in a bug report. The same applies per-click to §7's refund.
- **`ScaleCarriedHealth` at `RewardTrackController.Input.cs:149-153` is NOT broken
  by removing `bonusMaxHealth`** — `maxBefore` reads `EffectiveStats`, which after
  P4 reads `CollectedTotal(MaxHealth, claimedTrackLevel)`, and the watermark moves
  at `:151` between the two reads. The read-order is load-bearing and is now
  stated at the call.
- **No skill-equip UI exists** (grepped `skillSlot`, `SkillSlot`, `selectedSkills`,
  `equippedSkills`: nothing), so a track-unlocked skill cannot land in a kit the
  dossier cannot equip. `FightEncounterAdapter.KitFor(Character, …)` (`:657-666`)
  takes `AvailableSkillsFor` whole with the live save's `Character`, so
  `claimedTrackLevel` is current at fight-build time. Recorded as §3j.
- **`FightCapacityPinTests` needs no change but is worth knowing about**: it bounds
  skills per character against `FightSubmenuLayout.PoolSize = 24`, counting by
  authored owner, so it does not model the track's cross-owner grants. Odette's
  real worst case is 5. Recorded as §3i.
- **`SkillUnlockFilterTests` (`:62-83`) pins `SkillsUnlockedByLevel` against
  `AvailableSkillsFor` for a fresh level-1 character** and stays green only
  because `SkillsCollected(0)` is empty. Made an explicit P4 invariant (§3k).
- **`RunOrchestrator` is production, not bot-only**, and the plan filed three of
  its edits under a bot heading. Corrected in P1.
- **`ModifierEffects` has two production callers, not one** —
  `FightEncounterAdapter.cs:233` and `ItemOfferRoll.cs:105`. The second asks
  `.Best(FortunateFavorBonusFlat)` and cannot be disturbed by an appended
  elemental effect; stated rather than left implicit.
- **A future retune reinterprets `claimedTrackLevel` all over again.** The v5 bump
  fixes the one-time case, not the recurring one. Added as open question 6 with
  the default (bump the version with any retune that moves a milestone's kind).

**Could not verify.**

- `ModifierEffectSet.cs:17-18` names `ModifierEffectsAreEmptyForRealCharactersTests`
  as proving real combatants carry `Empty`. That test does not exist anywhere in
  `Scripts/` — the only occurrence of the name is the comment itself. Recorded in
  §13; it is a pre-existing dangling reference, not something this plan opens.
- Nothing here was run. No test suite, no content build, no scene build — the
  package-level `Run:` lists are unexecuted claims, which is round 2's ground.
- The magnitudes in §5 (Odette's +69% Arcane, +125 max mana) are arithmetic
  consistent with the filler counts and are **not** a balance judgement; round 3
  should expect to move them.

### Round 2 — verification and tooling

**(a) Part A — the design correction and the hook chosen.**

The owner asked for elemental damage mods on Odette the elementalist; round 1's
Arcane-only draft was a workaround for a seam that could not pay a foreign
element, not an answer to the ask. Read `FightSession.Skills.cs:619-649`
(`ResolveDamageInstances`), `ModifierEffect.cs` (`:31-34`'s closed-enum
caution and the constructor at `:370-379`), `ModifierEffectSet.cs` (Best's
max-not-sum header, `:20-25, 86-98`), `CombatantState.cs` (confirmed: no
per-element field exists, `ModifierEffects` at `:84` is the only carrier), and
`FightEncounterAdapter.cs:170-300` (confirmed `state.ModifierEffects =
ContentDatabase.ModifierEffects(character)` at exactly `:233`, and the
`TypedResistanceFlat` precedent at `:255-270` for summing a typed rider across
sources rather than colliding into `Best()`'s max).

**Chose option (a)** — a new `ModifierEffectType.ElementalDamagePercent`
member read inside `ResolveDamageInstances` against `instance.type` — over
option (b) (a per-`DamageType` array on `CombatantState`). Fewer touch points:
(a) reuses the `CombatantState.ModifierEffects`/`ModifierEffectSet` carrier
and the one adapter assignment that already exist; (b) would need a new field
plus a new adapter loop mirroring `TypedResistanceFlat`'s. Neither option
changes `ApplyModifierOnHitRiders` or `ElementalDamageOnHitPercent`'s existing
behaviour — confirmed by reading both call sites: they read different
`CombatantState` state at different points in the fight (the rider block vs.
`ResolveDamageInstances`), so the two hooks cannot interfere. Built as new
package **P4b**, `[D]`-verifiable, dependent on nothing else in this plan
(pure `Domain/Combat` + `Domain/Rewards`), with P4 depending on it for the
enum member. Full touch-point list, two pinned tests
(`AFireSpellWithTenPercentFireOnTheCasterDealsTwentyTwoNotTwenty` = 22,
`AFireSpellWithTenPercentIceDealsTheUnmodifiedTwenty` = 20, both literal and
hand-derived from `Rounding.AwayFromZero(20 * 1.0 * 1.10)`), and the
previously-unlisted `ModifierTableTests.cs:361-388` exhaustiveness guard (walks
`Enum.GetValues(typeof(ModifierEffectType))`, so the new member breaks the
suite immediately unless added to `ModifierTable.OffensiveEffects` and the
test's own `covered` set) are all in the new §10 P4b section. §2, §5, P4, P6
and §12 (open question 5 deleted, overruled by this package) were rewritten to
match — Odette's track is Fire/Ice/Lightning/Arcane again, ordered so no
elemental node predates the spell it scales (Fire/Ice at 40/45 after Frost
Flare unlocks at 10, Arcane at 50 for her level-1 basic bolt, Lightning at 70
right after Lightning Bolt unlocks at 60), with recomputed totals (Fire +18%,
Ice +18%, Arcane +20%, Lightning +23%, +100 max mana — down from the
arcane-only draft's +125 because a max-mana milestone slot moved to make room
for a fourth elemental node, stated rather than hidden) and P6's pin tests
rewritten to match.

**(b) Test names and areas corrected.**

- **`SignatureAbsorptionTests` exists — round 1's "no such test exists" was
  wrong.** `tools/test.ps1 -List` discovers it as its own `[D]` `combat` class
  (`test_areas.ps1` discovers per class, not per file); it is declared at
  `Tests/EditMode/Combat/SignatureResourceTests.cs:105`, inside the same file
  as `SignatureResourceTests` rather than a file of its own. Re-added to P4's
  `Run:` line.
- Every other test class named in a `Run:`/`Delete:` line across P1-P7 was
  checked against `tools/test.ps1 -List`'s full class/area/tag table (196
  EditMode + 116 PlayMode classes) — all others match the stated `[D]`/`[U]`
  tag and, where the plan implies one, the area. No other misnames found.
- Every NEW test class the plan proposes (`RewardTrackEntryResolverTests`,
  `RewardTrackDefinitionTests`, `RewardTrackScreenTests`,
  `RewardTrackContentPinTests`, `RefundTests`, `DossierRefundTests`) is absent
  from the current tree (checked by name against the full `-List` output, so
  none collides with `run_tests_parallel.ps1`'s duplicate-class-name refusal),
  and each sits in an area folder that already exists with a precedent of the
  same shape: `RewardTrackEntryResolverTests`/`RewardTrackContentPinTests`
  beside `SkillEntryResolverTests`/`RelicEntryResolverTests` under
  `Tests/EditMode/Content/`; `RewardTrackDefinitionTests` beside
  `RewardTrackTests`/`RewardTrackLayoutTests` under `Tests/EditMode/Run/`
  (`Domain/Progression/` maps to `run` in `test_areas.ps1`'s changed-file
  table); `RewardTrackScreenTests` under `Tests/EditMode/Ui/` (`Domain/UiKit/`'s
  catch-all, since `RewardTrackScreen.cs` is not one of the two files the table
  special-cases to `ui+run`); `RefundTests` under `Tests/EditMode/Hub/` —
  technically `Data/Character.cs` maps to `run` in the changed-file table, but
  the one existing precedent for testing `Character.Invest`/ability-score
  investment, `TalentInvestmentTests`, already lives in `Tests/PlayMode/Hub/`,
  so `Hub` (not `Run`) is the established home for this kind of test and the
  plan's placement is correct, not a misfile; `DossierRefundTests` beside
  `DossierEquipTests` under `Tests/PlayMode/Ui/`.

**(c) Chain constraints added.**

- Content chain: `tools/content_schema.ps1` exists and does what §11/§4
  already claim. `ContentSchema.FileMap` (`:31-45`, alphabetical by filename)
  and `EnumBackedFields` (`:57-85`) citations both check out exactly as the
  plan states. `Resources.LoadAll<T>` on a folder with zero assets returns an
  empty array (no throw) and `ContentDatabase.Ordered` is explicitly
  null-tolerant (`:747-748`), so P2's "`ContentBuilder` writes zero assets and
  logs it, no behaviour changes" is confirmed rather than assumed.
  `ContentLoadingLintTests.OnlyContentDatabaseLoadsContentFromResources` scans
  generically for `Resources.LoadAll<` anywhere outside `ContentDatabase.cs` —
  it needs no per-type update, and P2's `Run:` line already includes it
  correctly.
- **`ContentStampIdsTests.cs:45` (the `Fix` constant) does not say "ten"
  anywhere** — it is a run-command string with no folder count, unlike `:49`
  and `:287`, which do. §4's touch-point row corrected to name the three real
  touch points (`:49`, the `ResolvedByFolder()` dictionary body at
  `:178-224`, and `:287`), dropping the `:45` miscitation.
- **`ModifierTableTests.cs:361-388`'s exhaustiveness guard is a new,
  previously-unlisted touch point for P4b** (see (a) above) — the kind of gap
  this task exists to catch: a plan section that names the enum and the read
  site but not the test that walks `Enum.GetValues` over every member of it.

**(d) Worktree/main-tree corrections.**

- P2, P3 and P6 are correctly `[D]`-only for their worktree portion; each
  explicitly defers its `tools/build_content.ps1`/`tools/content_schema.ps1`
  step to "the main tree" in its own text rather than implying the worktree
  runs it — verified, no fix needed.
- Every package's `Run:` line invokes explicit class names (never an area name
  or `-Changed`), which is the safe worktree form per this session's own
  standing rule ("an area or full run from a worktree tries to build a new
  Unity TestRunner copy beside it") — confirmed consistent throughout, no
  violations found.
- **New package P4b is worktree-only and depends on nothing** — added to the
  Parallelism table as running alongside everything, including P1.
- **A real content-vs-code dependency was missing**: P4's new
  `RewardTrackClaimTests` literals (Shawn's wool capacity at specific levels,
  Odette's Frost Flare unlock level, both matching §5/P6 exactly) are pinned
  to the SHIPPED sheep/owl tracks, not to a separate fixture, despite the
  plan's own header claiming "a fixture track... so a retune does not break
  them." Grepped `Core/Content/*.cs` for `Install`/`TestOverride`/
  `SetForTests`: no content-injection seam exists for a PlayMode test to
  substitute a fixture track while still going through
  `ContentDatabase`/`RewardTracks.For` the way the file's own `OpenTheTrack`
  helper (loading the real Hub scene) does. So P4's new PlayMode tests
  genuinely require P6's JSON to be in the main tree before they can pass,
  even though P4's package header states only "Depends on P3." Corrected in
  P4's own text, the Parallelism table, and the new Execution order
  subsection, which sequences P6's content landing before P4's verification
  gate despite the lower package number.
- Added a full `### Execution order` subsection under §10: five lanes (P1
  alone; P2+P3+P4b concurrent worktrees; P6 after P2/P3/P4b; P4 after P3/P4b
  (code) and P6 (content); P5 then P7 sequential on main), one commit and one
  gate per step, the foreground-only/600000ms-timeout rule for every gate, a
  check for a running `Unity.exe` on the TestRunner path before any
  `-BuildContent`/`-BuildScenes` gate, and the standing "stage by explicit
  path, never `git add -A`" rule given the three other interactive sessions
  the git-status snapshot shows open on this repo today.

**(e) Executability gaps filled.**

- **`Character.ClaimTrackRewards`'s bot call sites had no exact rewrite.** The
  plan's "likewise" at `BotRunDriver.cs:1041`/`ProfilePresets.cs:146` did not
  say what the new call looks like. Both are two-overload today
  (`ClaimTrackRewards(int)` and a parameterless `ClaimTrackRewards() =>
  ClaimTrackRewards(level)`, confirmed at `Character.cs:374, 408`) collapsing
  to one `(RewardTrackDefinition, int)` signature — filled in as
  `character.ClaimTrackRewards(RewardTracks.For(character), character.level)`
  at both sites, with the "why no convenience zero-arg overload" reasoning
  stated (an `Character`-internal call into `Core.RewardTracks` would be the
  only such self-resolving call on the type).
- **`ContentDatabase.ModifierEffects`'s append site had no exact code.** Filled
  in: loop `Enum.GetValues(typeof(DamageType))`, read
  `RewardTracks.For(character).CollectedTotal(TrackReward.ElementalDamagePercent,
  type, character.claimedTrackLevel)`, append when `> 0`. Flagged that the
  method's own header comment (`ContentDatabase.Effective.cs:320-339`,
  currently "every rule this character's equipped items' ROLLED MODIFIERS
  contribute") is now half wrong and must be rewritten, the same standing rule
  P5 already applies to `RewardTrackScreen.cs:719-722`.
- **The save-migration test had a technique but not a recipe.** Found the two
  existing precedents: `Tests/PlayMode/Run/EmberOwnershipTests.cs:136-172`
  (direct `save.version = N` field assignment, for a migration that changes
  values but not field shape) and `Tests/PlayMode/Run/ItemModifierSaveCompatTests.cs`
  (build-then-strip-JSON-tokens, for a migration where a field is REMOVED from
  the shape). `claimedTrackLevel`/`unspentStatPoints` are neither removed nor
  renamed by the v5 step, only reinterpreted, so `AVersionFourSaveComesBackWithEverythingWaiting`
  should use the `EmberOwnershipTests` idiom — `save.version = 4` plus direct
  field sets, no `JsonUtility` round-trip — and the plan now says so exactly
  rather than "hand-built."
- **P5's `Run:` line implied layout coverage that does not exist.** Detailed
  in the P5 section itself (see the note added there): no `[D]` test audits
  the real `SystemMenuScreen`/`RewardTrackScreen` tree; only `-BuildScenes`
  does, via `UiEmitter.Emit` calling `UiAudit.RunAllFrames` inside
  `SceneBuilder.BuildScene` (`SceneBuilder.cs:118-121` for the other three
  audits, `UiEmitter.cs:26` for `UiAudit` itself). `UiBindingContractTests`
  pins message TEXT only and proves nothing about the real audit's verdict on
  this package's actual wiring.
- **The bot's read of the reward track was verified line-by-line**:
  `BotRunDriver.cs:1041`, `ProfilePresets.cs:146` (`ClaimTrackRewards`),
  `RunOrchestrator.cs:86, 170, 317, 578` (`ChosenStartingRelics`,
  `StartingRelics`, `SecondLifeCharges`, `OfferWidth`) — all four
  `RunOrchestrator` citations match the plan's existing P1/§3e2/§6 line
  numbers exactly; no correction needed there, only the ClaimTrackRewards
  call-site rewrite above. The plan already states a bot smoke run is not
  separately gated (`BalanceBotSmokeTests` is `[U]` `combat`, already covered
  by the main-tree `[U]` run implicitly whenever `run_tests_parallel.ps1` runs
  without a filter) — confirmed sufficient; no new bot-specific verification
  step is needed beyond what P1/P4's existing `Run:` lines already cover.

**(f) Could not verify / out of scope for this pass.**

- A second dangling test-name reference, distinct from §13's
  `ModifierEffectsAreEmptyForRealCharactersTests`: `ContentDatabase.Effective.cs:327`
  cites `ModifierEffectSetAlwaysEmptyForRealCharactersTests` — a DIFFERENTLY
  SPELLED name, also absent from `Scripts/` (grepped both spellings; each
  appears exactly once, in its own comment, nowhere else). Two dangling
  references to two different imagined test names for the same claim, not
  one. Worth folding into the same `AUDIT.md` entry §13 already asks for.
- Nothing in this pass was executed either — `tools/test.ps1 -List` and
  read-only `git`/`grep` were the only commands run, per this round's scope.
  Every `[D]`/`[U]` class-existence and area-folder claim above was checked
  against the real tree; no test was actually run, no content or scene build
  was performed, and the new pinned literals (22/20 for P4b, the recomputed
  Odette totals) are hand-derived, not machine-verified. Round 3 or the
  eventual implementer is the first to actually run any of this.
- Odette's redesigned magnitudes (Fire/Ice +18%, Arcane +20%, Lightning +23%,
  +100 max mana) are arithmetic consistent with the filler/milestone counts,
  exactly as round 1 flagged for the arcane-only draft — still not a balance
  judgement, and round 3 should expect to move them again now that they are
  split four ways instead of one.

### Round 3 — game design and final pass

Nothing here was run either: `tools/test.ps1 -List` was not needed, no suite, no
content build, no scene build. What WAS executed is the track construction
itself — `InterleaveMix`/`Spread` reimplemented from `RewardTrack.cs:489-576` and
run over every candidate mix, which is how three of the findings below were
found and is why every level number in §5 is computed rather than asserted.

**(a) What the first twenty-five levels now look like, and why.**

The construction turned out to be simpler than either earlier round assumed, and
§4 now states it so an author can predict placement: the mix must sum to 87 and
there are exactly 87 free levels, so `Spread`'s Bresenham degenerates to
one-node-per-level and **the interleave's order IS the level order, starting at
level 2**. The biggest count in the mix always takes level 2.

- **Shawn**: stat point at 2, `+1% NATURE DAMAGE` at 3, `+10 MAX HEALTH` at 4,
  `+1 WOOL CAPACITY` at 6, then the same four beats until `+1 WOOL PER TURN` at
  10 and `+5 WOOL CAPACITY` at 25.
- **Odette**: stat point at 2, health at 3, `+2 MAX MANA` at 4, `+2% FIRE
  DAMAGE` at 6, `+2% ARCANE DAMAGE` at 8, `LEARN FROST FLARE` at 10,
  `+20 MAX MANA` at 25.
- **The default**: `+2 MAX HEALTH` at 2 (47 beats 40), alternating with stat
  points, `+15 MAX HEALTH` at 10 and 25.

So the answer to "is it stat points and health until the first milestone" is no,
and it already was no in the round-2 draft — flavour reaches level 3 on Shawn
and level 4 on Odette, all three tracks differ at level 2, and nine of the two
authored tracks' twelve milestones differ from each other. Nothing was moved
earlier; both node-by-node listings were added to §5 because the claim was
unverifiable without them.

**(b) Retunes, with before/after and the reason.**

Four numbers moved, and one design decision was reversed. Every one of them is
the same failure: a magnitude that was arithmetically consistent with the filler
counts and had never been compared to anything in the game.

1. **The elemental routing rule (new, §2) — the reversal.**
   `ResolveDamageInstances` runs only for a skill that authors
   `damageInstances` (`FightSession.Skills.cs:350, 481`). Shawn's entire kit
   authors exactly one, `cinderfault`, and his track does not hand it over until
   level 70; `shear`, `battering_ram`, `mud_burst`, `static_fleece`,
   `golden_fleece`, every talent ability and every plain swing carry none. So
   the round-2 draft's `+44% Nature`, paid through P4b's packet hook, was worth
   **nothing at all for 68 levels and then half of one spell** — 18 of his 99
   nodes were dead. Fixed by routing on the character's own `attackType`: same
   element rides the existing `ElementalDamageOnHitPercent` (every landed hit,
   `FightSession.cs:510-521`), any other element rides P4b's packet hook. One
   effect per element, never both. Shawn's Nature and Odette's Arcane take the
   rider; her Fire, Ice and Lightning take the packet hook. ~8 lines in
   `ContentDatabase.ModifierEffects`, two new pins, and P4b is unchanged and
   still needed.
2. **Odette's mana: +100 pool / +5 regen → +50 / +3.** Her base pool is 50
   (`DefaultMaxMana` 30 + WIS 20 × 2), her regen 5 (WIS/4), a cast 28 (spell
   tier 9, which she is at from level 9). Over eight turns the draft gave her
   `150 + 80 = 230` mana against a 28 cost — **8.2 casts in 8 turns**, i.e. she
   casts every turn and mana stops being a budget. +50/+3 gives `100 + 64 =
   164`, **5.8 casts**, against a base of 3.2. 1.8×, with the gap between casts
   intact.
3. **Shawn's wool-when-hurt: +5 → +2.** `SignatureGainOnDamageTaken` fires per
   damage event. The Black Ram's ROOT talent — the entry fee to a whole path —
   grants `WoolOnHitTaken 1`. +5 is five times that from the track alone; on a
   front-liner taking two or three hits a round it adds 10-15 wool a turn on top
   of the per-turn gain, fills a 37-cap by turn three and overflows thereafter,
   which deletes the arc `SignatureResource.cs:10-14` exists to create and makes
   every capacity node moot. +2 is double the root talent. The three filler
   nodes of it were dropped with the retune; it is one authored milestone now.
4. **Shawn's Nature: +44% → +30%.** A `Sylvan` affix is
   `ElementalDamageOnHitPercent 10` at tier 0 (`modifiers.json`) and the
   same-element branch pays it unmitigated on top of an already-mitigated hit.
   +44% is four and a half affixes permanently on every swing; +30% is three.
   Delivered as 20 filler nodes of `+1%` plus the level-45 milestone's 10.
5. **Odette's elements: +18/+18/+20/+23 (Fire/Ice/Arcane/Lightning) →
   +28/+15/+26/+20.** Not a rebalance so much as a consequence of (6) below:
   Ice and Lightning lost their filler and became single, larger milestones (15
   and 20), and their 8 freed slots went to Fire and Arcane. Every one is two to
   three affixes' worth, the same band Shawn now sits in.

**Kept, having been checked rather than assumed:** wool capacity 37 (capacity is
a ceiling, not a rate — twelve turns to fill at +3/turn — and it is directly
load-bearing on `golden_fleece`, whose damage is `power 3 × resourceSpent`,
`SkillResolution.cs:170`); +3 wool a turn (against costs of 2-10, a ~3× economy);
50 stat points and +150 max health on both tracks; the default's +229; the spine
at 20/80/90 on all three, with the argument for each half written into §5 —
respec is a UI contract three `TalentController` gates read, and the second life
is squad arithmetic under §6.

**(c) A defect the retune came out of, and one the numbers came out of.**

- **Odette's Lightning filler landed at levels 16, 42 and 64.** Lightning Bolt
  unlocks on her track at 60. §5 claimed her elemental milestones were "ordered
  so a node is never granted before the spell it scales exists" — true of the
  milestones, false of the filler, because nobody had computed where the filler
  goes. Fixed twice: Ice and Lightning are milestone-only (Ice at 45, Lightning
  at 70, both after their spells), and **validation rule 4** now refuses a
  filler `ElementalDamagePercent` whose element the character cannot already
  deal at level 1. Rule 5 was added beside it — a signature reward on a
  character with no `signatureId` would be collected, captioned, and paid into
  the null `BuildSignatureResource` returns.
- **P4's `AWoolCapacityNode…` pinned `Max == 15` and `Max == 10`.** Neither
  number matches any version of the sheep mix: capacity at claimed 25 is 18
  (10 base + three filler nodes at 6/14/21 + the milestone's 5) and at claimed
  24 is 13. The filler capacity before level 25 had simply not been counted.
  Corrected, along with `AnElementalNodeReachesTheCombatantAsAModifierEffect`,
  which asserted "at 39 collected she has none" of an element that has filler
  nodes from level 6 — it now pins Lightning, the one element that is
  milestone-only and so genuinely goes 0 → 20 in one step.

**Owner's named examples, and where they land.**

| Ask | Where |
|---|---|
| "+1 wool per turn" | level 10, and again at 50 (→ 3/turn) |
| "wool capacity upgraded to 20" | first reaches 20 at **level 39**; 15 at 25, 25 at 81, 37 at 100 |
| Odette "starts with an extra spell" | **level 10**, Frost Flare — the earliest a track can pay one, since nodes start at 2 and `UnlockSkill` is refused as filler (rule 3). Justified in §5 rather than left as a near-miss |
| "elemental dmg mods" | Fire 40, Ice 45, Arcane 50, Lightning 70, plus Fire/Arcane filler throughout |
| "extra life can stay" | level 90 on every track, sourced per character (§6) |
| "stat points can stay" | 40 filler singles + 10 at level 80 = 50, on every track |
| the minus | §7, hub only, one point at a time |

One node changed owner for a design reason rather than a numeric one:
**Shawn's level 30 is `static_fleece`, not `mud_burst`.** Static Fleece is the
only book spell in the game that SPENDS wool, so it is what makes the first
twenty-nine levels of capacity and gain nodes cash out; Mud Burst is a bog spell
the Bog Witch already casts (`bog_mud_burst`) with nothing to do with the
fleece. The cost is that `static_fleece` has no `vfx` block where `mud_burst`
does — open question 6.

**(d) Consistency fixes.**

- Twelve caption templates written out in §5 as a table. They did not exist
  anywhere, and `RewardTrackNames.Of` folds the amount into the string
  (`:11-14`), so an implementer would have invented twelve of them. Longest is
  `+20% LIGHTNING DAMAGE` at 21 characters; §1's "top out at 18/20" corrected.
- **Two of those captions need a lookup Domain cannot do.** The skill name was
  already handled; the RESOURCE name was not — `+5 SIGNATURE CAPACITY` is what
  the plan as written would have shipped, because "Wool" is content
  (`signatureDisplayName`), deliberately, per `SignatureResource.cs:5-9`. So
  `TrackEntry` grows a fourth field (`ResourceDisplayName`), the resolver takes
  a second lookup, and **`BuildCharacters()` has to return an id→name map the
  same way `BuildSkills()` does** — a `ContentBuilder` touch point §4 did not
  have. Blank name falls back to `"SIGNATURE"`.
- §7's minus: the plan gated it on `character.InvestedPointTotal > 0`, which is
  a GLOBAL condition on a PER-CELL control — it would have shown a minus on all
  six cells whenever any one score had a point in it, five of which do nothing
  when clicked. The rule is now per-cell
  (`investedAbilityScores[_cellOrder[i]] > 0`), which is also exactly what makes
  `Character.Refund` return true. Added: `Refund`'s own
  `if (lockedForFight || inDescent) return;` (a hidden button is still
  EventSystem-reachable, and `Spend` guards inside the method too), and the
  stranding argument — `_cellOrder` holds all six scores and is frozen by
  `_cellOrderFor`, so every invested score always has a minus and nothing
  reshuffles.
- §11's "How we know it worked" was missing four of the seven things the ask
  names: level 1-100 coverage, stat points kept and spendable, the second life
  kept, and any statement of what makes the two tracks different. All four
  added, with the level-3/4/6 caption differences as the concrete test.
- §5's `placeholder_brawler` section now lists the six captions its track can
  actually show, which is what answers "does it embarrass the screen" — nothing
  blank, nothing saying "placeholder".
- Numbers checked in both directions: 87 (four mixes, all summing), 229 and 50
  (default, P1 interim, P3 test, P6 pin), 150 (both tracks), and every P4/P6
  pinned literal recomputed against §5's tables. §12 gained questions 6 and 7;
  every question still has a default and none reopens a settled decision.

**(e) Left for the owner.**

- **Static Fleece has no VFX** (open question 6). Ship it and commission the
  animation, or put `mud_burst` back at level 30 — nothing else on the track
  moves either way.
- **Odette's four elemental lines are not four equal things** (open question 7).
  Arcane is her `attackType`, so it pays on everything she does, including her
  Fire casts; the other three pay only on their own packets. That is the
  engine's existing model, not something this plan invents, and the track is
  authored rather than chosen — but it is the kind of thing that reads as a bug
  if nobody wrote it down.
- **Wool absorbing damage at level 60** is still open question 2, unchanged and
  unresolved by this round.
- Nothing in this plan has been executed by any of the three rounds. The first
  time any of it runs is P1's gate.

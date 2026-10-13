Context review of PLAN_ITEMS.md rev 2. Reviewer: Sonnet 5.5 (`implementer`, read-only), 2026-10-13.

## Verdict

**Ready after fixes.** The round-1 blockers landed correctly: the all-Common arithmetic (21.6% / 12.5% / 0%) is exact, the Reward stream (14) is free, and the pendingPicks idea is sound. But the headline new mechanism, honing on the slot, is impossible as written against `EquipmentLoadout`, the shop floors cannot hold under the new price rule, and the deep-power claim behind Q9 is built on a partial calculation. Several gaps in ordering and in the saved pick shape need a sentence each before I2.

## Round-1 triage check

- **Applied items verified:** C1 (quality table arithmetic, `p^s` ladder), C2 (key and pages), C4 (stat readers), C9 (37% was right for the old design) and B1/B4/B5 landed in the text. Two partly: B2 (see 1) and C9 (see 6) fix the symptom and leave a spec hole.
- **B3 rejection:** sound on the cap, with a wrong citation. `RarityTable.FloorTier` (`RarityTable.cs:50`) is `depthStep / 16`, uncapped. The cap at 10 is the content `maxTier` passed in (`RarityTable.cs:100,142`; `ShopStock.cs:352`). The plan's `min(step/16, 10)` is a new rule (step 6) cited as if it were existing code. Its "Q9: as the code intends today" is only half true (see 4).
- **B14 rejection:** weak. B14 asked for an invariant test on skip income; the plan only measures it in I5. The per-pick skip price stays under the 60g relic (0.3 x 177 = 53), but per-realm skip income is a sum over up to 6 anchors plus a boss. The plan's own Risks bullet concedes the budget does not hold deep (see 5).
- **C3 partial rejection:** sound. The `Tests/EditMode` and `Tests/PlayMode` folders are exactly `Art Combat Content Hub Rng Run Ui`, plus `Shared`.

## Findings

1. **[blocker] Honing "on the slot" cannot survive an empty slot in `EquipmentLoadout`.**
   - Quote: 1.5 "`EquipmentSlotEntry.plus` ... keeps its value when the item in it changes or the slot is emptied."
   - Evidence: `EquipmentLoadout.cs:167-181`: `Put(slot, null)` does `slots.Remove(entry)`. The class header states "an entry with a null/empty itemId is dropped rather than kept as a tombstone, so 'is this slot free' has exactly one representation". `GetPlus` (`:129`) returns 0 for an empty `itemId`. `EquipmentSlotEntry.Write` (`:67-75`) overwrites `plus` from the instance. `EquipMove.TryUnequip` (`EquipMove.cs:94`) goes through `Put(slot, null)`.
   - Failure: hone Weapon1 to +4, unequip it in the dossier (allowed in any menu context), and the +4 is gone. The same happens on any slot clear.
   - Fix: store honing outside the item entry: a `slotPlus` list keyed by (character, slot) on the loadout or the run, with an invariant that it is independent of `itemId`.
   - Also state: Weapon1/Weapon2 are separate slots (a sword moved between hands changes plus); what happens to a character who leaves the squad; and the clear at `EndRun`. Rewrite the sentence "EquipmentSlotEntry.plus changes meaning" in 2.5 accordingly.

2. **[major] The shop floors cannot hold under the new price rule (I2a gate says "floors hold").**
   - Quote: 2.3 "`floorNoBare` = at least Uncommon; `floorCheapestAtMost` keeps 28g"; I2a gate.
   - Evidence: `GearPrice(tier, quality) = BaseFor(tier) x (1 + 0.5 x affixes)`; `ShopPricing.cs:41-58`: GearBase 21, so the cheapest legal piece is tier 0 Uncommon, 21 x 1.5 = 31.5, rounded to-even = 32 (calc). `ShopPricing.cs:176-197` documents the same trap ("a guarantee that never fires reads as one that does"). Today 28 is `GearPrice(0,1,0)` via `ShopStock.cs:420-428` (`WithPlus(1)`), which the plan deletes. A necklace at "3" prices at 52.
   - Failure: `floorCheapestAtMost` re-draws exhaust every time at floors 1-2 and the pinned stock literals drift.
   - Fix: move the anchor to 32 (and re-derive "a normal fight's payout 16-24 buys nothing at floor 1"), or let the floor accept a Common at tier 0. Name the chosen value in 2.3 and I2a.

3. **[major] Step 5 "bases whose `requirements` the owner meets" is undefined and can empty the pool.**
   - Evidence: `GearScaling.cs:271-278`: requirements grow with tier and "refuse wearing one piece of a material you have not otherwise committed to"; at tier 10 a pure style asks 23 of its own stat. Requirements resolve by a fixpoint across the whole loadout (`ContentDatabase.Effective.cs:128-148`), with slot plus feeding `AbilityScoreBonusAt`, not by a per-piece test.
   - Failure: read as "owner's current stats alone", deep cards are filtered to the owner's existing material, contradicting "not biased toward the owner's build: material is the off-build surprise" and the T11 score. Read as "with the card equipped" it is a loadout-wide fixpoint per candidate base, which is costly and unspecified. The "no card at all → pays skip value" path then fires at depth.
   - Fix: pick one reading and test it. Simplest: the card is checked as if worn on the owner's current loadout through the fixpoint, and an unmet requirement is allowed but shown (the dossier already handles worn-but-unmet items, `ContentDatabase.Effective.cs:397`).

4. **[major] "Gear is a small share of deep power" (1.6, Q9) rests on raw budget multipliers only, and the claim as written cannot be settled from the cited code.**
   - Arithmetic, verified (calc): 1.075^160 = 1.06e5, 1.075^80 = 326, 1.075^16 = 3.18, 1.075^200 = 1.91e6; 1.038^160 = 390, 1.038^80 = 19.8; 1.25^10 = 9.31, 1.35^10 = 20.1. The table is right.
   - But `DifficultyCurve.cs:19-31` says enemy health tracks the player's damage and enemy attack tracks the player's health, and that "GearScaling and AbilityDerivation compound a geared character's power by two-plus orders of magnitude across the ladder". `GearScaling.cs:40-45` contradicts that ("a minor drip", about 18 score points at tier 10). The two comments disagree and the plan compares neither to the real stat path (`FightEncounterAdapter.ToCombatant`).
   - What it means if the curve is really exponential at 7.5% a step: past step ~80 no linear or capped power source keeps pace, so deep survival comes from what compounds (levels at 2.5% a step, spells, ability derivation) and from mechanics (traits, relics), not gear stats. Item stats matter only to about realm 8-10; the item system's lasting value past that is mechanics.
   - And at `MaxScaledStep = 200` (`DifficultyCurve.cs:206`) enemy power stops growing (the plan's row 320 is the cap), while levels and spells keep growing, so "the descent ends runs" fails past step 200. The plan lists the cap and draws no conclusion.
   - Fix: change Q9's recommendation from "yes for now" to "no decision until I5 measures gear's share through `ToCombatant`, not budget multipliers". State that I5's deliverable is a deep-power split (gear / levels / spells / mechanics) at steps 40, 80, 160. Correct the stale "tier 10 = step 80" comment (already queued in I0), and drop "as the code intends today" from Q9.

5. **[major] Every gold mechanism is meaningless past about realm 5, and the score counts them.**
   - Evidence: fight gold rides `ScaleReward`, the 7.5% curve (`DifficultyCurve.cs:143`; the code comment itself says a step-80 fight pays about 325x a step-0 one). Gear prices stop at `BaseFor(10)` = 71, so a tier-10 sale is 21g (0.3 x 71) at best, a relic skip is 18g, and honing to +10 at tier 10 totals about 1.8k (71 x (10 + 0.35 x 45), calc), against thousands of gold per fight at step 80 (calc).
   - Failure: skip pays nothing that registers, so T1 "skip is a real option" and T3 "a real cost to each option" hold only early; honing "sinks gold" (T14) only early; the rule "selling the whole bag never funds a relic" is trivially true.
   - Fix: either scale skip and honing prices by the same step curve as fight gold (`ScaleReward`, accepting that the rule at `ShopPricing.cs:123-127` becomes a ratio), or state that gold mechanisms are an early and mid-game system and score T1/T3/T14 as 1. Add B14's invariant as a pinned test: skip gold per realm (a sum over its picks) < cheapest relic price at floors 1, 10 and 30.

6. **[major] Necklace odds and pity are under-specified, and the effective rate is not the stated one.**
   - Arithmetic (calc, renewal over the pity counter): base 0.05 + 0.05 x pity gives a necklace every 5.29 normal picks, about 18.9% per pick, not 5%. Anchor 0.20 gives 29.2%; boss 0.35 gives 41.0%. If pity is shared across sources (one `necklacePity`) the rate is higher still.
   - Failure: it is "rare" only in the sense that 19% is half of the 37% round 1 rejected. A run with 30 gear picks sees about 6 necklaces for 3 neck slots, so most are replacements the player must read. The `rewards.json` row says 0.05.
   - Unspecified: does pity reset on a card shown or only on a card Taken? A skipped necklace under "skip the rest"? "No usable trait → no necklace card" (no reset or increment)? The plan says pity is "read once at settlement" while `lastPickBestCommon` must be sequential inside a settlement (pick 2's floor depends on pick 1's rolled best card).
   - Fix: state the effective rate in 1.3 or step 2; decide reset-on-roll-success; define the intra-settlement order; and make I5 (or a pin) report necklaces per run.

7. **[major] `pendingPicks` stored "as `ShopStockEntry`" cannot hold a pick.**
   - Evidence: `ShopStock.cs:41-75` has `kind, contentId, noOffer, plus, modifiers, riftTier, price, sold, section, index, lot, fake`. It has no recipient, no source class (boss/anchor/normal, needed for the skip value and for step 8 floors), no pick page, no tier (2.5 adds it), no necklace flag, and a `price` meant as a quote.
   - Failure: re-entering the screen cannot reproduce "Pick 2 of 4", the card's recipient, or the skip payout; a resolved pick is undistinguishable from an unresolved one.
   - Fix: define a `PendingPick { sourceClass, nodeId, pickIndex, resolved, cards[] { entry, recipientId } }` in 2.5. Also: if `nodeId` is a string it must go through the `Fnv1a` helper in `RngStreams` (it takes ints in `a, b, c`); say which of the three slots it occupies.

8. **[major] Phase and ordering holes.**
   - (a) The `Needs` column omits dependencies the text uses. I2b relic cards use `EffectSet` ("excluded when every member it reaches already gets its mechanic", from I1); I2c traits use `EffectKind`/`EffectCatalog` and `requires` (I1); I2b needs I2a's `Quality`; I2c needs I2a/I2b. Add I1 and I2a/I2b to `Needs`.
   - (b) I3 pays "anchor dies ... at the heart". Heart anchors do not exist until run map P5 (`slainAnchorIds`, `PLAN_RUN_MAP.md:297-322`; until P5 anchors are plain optional single-elite fights). No item phase depends on P5 and no gate proves the heart path.
   - (c) 2.4 says run map P1/P2 "should" carry the `Relic` prize value and the anchor weight list. `PLAN_RUN_MAP.md` rev 4 does not say so (item and book "reserved with weight 0", `:99-101`; P2 already owns the v8→v9 bump). Neither plan gates it, so I3 can start without it. Amend the run map or add an I3 precondition.
   - (d) Between I2a/I2b (plus roll deleted) and I4 (hone source), gear cannot hone, so the bot balance between those phases is unmeasured. Say so or put the honing service in I2a.
   - (e) I2a and run map P2 both edit `RunSnapshot`/`ShopStockEntry` and save shape. Order them, or put the item fields under P2's v9 bump (the bump the 2.5 "no bump" bet then costs nothing to share).

9. **[major] The shop quality distribution is unspecified, and Favor loses headroom.**
   - Evidence: the table in step 7 gives p only for Normal/Anchor/Boss. The shop (and the shelf) use `ItemOfferRoll.RollOne` (2.2). If they take p = 0.40, an affixed copy is 40% against about 12% today (`ModifierTable` 0.12/0.15/0.18) and affix counts and prices rise accordingly; if they keep the old affix ladder, shop and fight use two quality scales.
   - Cap arithmetic: `LootLadder.MaxStep` = 0.55 (`LootLadder.cs:50`): anchor p 0.50 has 0.05 of headroom, so 8 Favor points (0.006 each) hit the cap; boss 0.40 has 25 points; normal 0.40 the same. Favor stops mattering for anchors quickly. `ModifierTable`'s header says its dials are deliberately different from `LootLadder`'s; the plan removes that without saying so.
   - Fix: add a `shop` and a `shelf` row to the p table, and state whether the cap moves (raise to 0.65, or move the base down).

10. **[minor] Hone price is gameable by swapping in a cheap item.** Price = `BaseFor(tier of the slot's item)` x (1 + 0.35 x plus). Equip a tier-0 item (21g base), hone to +10 (about 540g in total against 1.8k at tier 10), swap back; slot honing makes it free to do. Price on the best tier ever worn in that slot, or on `max(slot tier, floor-tier(step))`. Pointless to fix if finding 5 stands (gold is irrelevant), so decide that first.

11. **[minor] The 36% necklace-in-lottery figure in section 6 is off.** `EquipmentSlot` has 8 slots (Head, Necklace, Torso, Legs, Shoes, Gloves, Weapon1, Weapon2), so seven non-necklace plus the necklace; 1 − (7/8)^3 = 33%, not 1 − (6/7)^3 = 37% with "7 empty slots". The conclusion (not rare) stands; fix the number.

12. **[minor] Weighting steps 2-4.** Step 3 turns "one non-necklace card" into the weapon for the owner with the lowest Weapon1, and step 4 then weights empty slots at 3: at squad start every slot is empty, so over an opening pick 3 each of 7 slots is drawn at 3/21 and the Weapon2 case needs a statement ("empty Weapon1 → weapon" covers only Weapon1, but both weapon slots are eligible). Name whether a card for Weapon2 can be offered before Weapon1 is filled (it cannot by step 4) and whether a second weapon is ever a "weapon card".

13. **[minor] Theory score: 25 is not credible; about 22-23.**
    - T3 and T14 drop to 1 deep (finding 5). T8: pity covers the necklace and a Common-streak floor, but a quit ends the run, so "quit-safe" is vacuous, not designed; 1-2. T1 stays 2 early. T9 at 1 and T4 at 1 are stated correctly.
    - T11 at 2 ("every base at every depth") conflicts with finding 3 (requirements filter the pool) until that is settled; T15 stays 2 only if finding 9's p table separates the classes, which it does.
    - Restate the total as "25 at floors 1-8, about 22 deeper" or rescore, and keep the lever column.

14. **[minor] Gates.** I1's byte-identical `RunTrace` is valid only if I0 has already moved offers onto the Reward stream; say so in I1's gate. I2b's UI gate (`-BuildScenes`) proves the scene builds, not the multi-page flow; add a PlayMode test: settle a boss plus two anchors, take pick 1, re-enter the screen, "skip the rest", and assert the gold and the stored picks. `OfferRowLayout` relic cards have no named test.

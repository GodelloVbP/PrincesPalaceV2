# Round 1 triage (rev 1 → rev 2), by the plan author (`senior`), 2026-10-13

C = `REVIEW_R1_CONTEXT.md`, B = `REVIEW_R1_BLIND.md`. Reviewer numbers were re-checked before adoption.

| # | Result | Note |
|---|---|---|
| C1 bare picks | applied | 0.88³ = 68% confirmed; per-class quality ladder (0.40/0.50/boss 0.40 from Uncommon) → 21.6%/12.5%/0% all-Common; boss floor + Rare weapon; streak floor after an all-Common pick |
| C2 multi-pick key | applied | key (step, nodeId, pickIndex); `pendingPicks` on the snapshot; Reckoning pages incl. relic cards and "skip the rest" |
| C3 shop/shelf/SameStack | applied | partly rejected: gates name the real test areas (art, combat, content, hub, rng, run, ui) |
| C4 stat readers | applied | 29 files; one copy-taking accessor; base loses stat fields so a missed reader fails to compile |
| C5 D9/C1 dropped | applied | boss relic Uncommon+, per-source `relicWeights`; `ItemKind` split rejected with reason |
| C6 score | applied | rescored to 25/30 |
| C7 precedence/literals | applied | 9-step card algorithm; skip values defined; chips read the pool live |
| C8 Rest | applied | honing reach per realm stated; Rest is a pick on the shared screen |
| C9 necklace rate | applied | 37% confirmed; own roll per pick 0.05/0.20/0.35 + pity; label "Trait" |
| C10 phases/gates | applied | I2 → I2a/b/c; byte-identical `RunTrace` for I1; Relic kind + anchor weights into run map P1/P2 |
| C11 savings overstated | applied | relic mechanics still 3 files; source-scan test named |
| C12 save caveats | applied | no version bump stated as a bet on the no-resume rule (`RunManager.SettleOnOpening`) |
| C13 research | applied | [M] StS boss-relic precedent no longer cited |
| C14 slips | applied | |
| B1 skip dominated | applied | skip pays the best card's sale price, now; inequality stated |
| B2 honing erased | applied | plus lives on the slot (`EquipmentSlotEntry.plus`) |
| B3 endless scaling | partly rejected | tier/plus capped at 10 (`DifficultyCurve.cs:200-205`); real problem is the reverse (enemy HP 1.06e5× at step 160 vs gear 9-20×) → Q9 |
| B4 rarity collision | applied | `Rarity` bands → `Quality`; Mythic goes |
| B5 I2 too big | applied | split |
| B6 meaningless cards | applied | `requires` on traits/relics, fallbacks |
| B7 precedence | applied | elite weapon guarantee kept for anchors |
| B8 relic flow | applied | rejected merging picks |
| B9 book chip | applied | |
| B10 I1 gate | applied | |
| B11 duplicate relic | applied | |
| B12 old-id load | applied | |
| B13 re-derive | applied | |
| B14 gold budget | partly rejected | fight gold grows 7.5%/step vs flat relic prices; 30% cap kept, I5 measures |
| B15 dangling refs | applied | |

Unraised corrections: affix count is 14, not 15; `DifficultyCurve.cs:203` comment says tier 10 = step 80, it is 160 (queued in I0).

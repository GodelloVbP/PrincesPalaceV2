# Reward cache rates (A9)

Question: does the cache stay rare? R4 gate: top-tier beat (Legendary live
upgrade) on at most one gear page in five at mid-run Favor
(`docs/PLAN_REWARD_SCREENS.md` C3, section 6).

## Method

- Trace: `BotRunDriver.Offer` records each gear card's quality, its
  `LiveUpgrade.Of` result and `LiveUpgrade.WornTier` before the first choice.
  `BalanceBotRunner` builds `RevealCard`s from them and calls
  `RevealSchedule.For` (page class from the settlement) to get cache, beat
  and `ChoosableAt` at 1x; it writes those to `pages.jsonl`, a file of its
  own. Summary: `python tools/reward_cache_rates.py <batch-dir>`.
- Act = `step / 8 + 1` (a realm is 8 steps, `docs/ARCHITECTURE.md`).
- **old** = the first rule: a live Rare+ card is a cache, a live Legendary a
  beat, wall time from the C4 table arithmetic. **new** = the shipped rule
  (ff122e6e): the cache needs a live Rare+ card above the worn tier (empty
  slot 0), boss pages always cache, the beat needs a live Legendary over a
  worn tier below 4. Same pages, same seeds; only the rule differs.
- Gear pages only; relic pages (about 30% of pages) are not counted.
- Neutrality: `-Runs 20 -Seed 1` with and without the trace gave
  byte-identical `traces.jsonl` and `runs.jsonl` (`cmp`); the 1000-run sweep
  with WornTier and RevealSchedule is byte-identical in both files to the
  sweep before them.

## Fresh profile (`-Runs 250 -Seed 1000 -Shards 1`, 4 archetypes, 1000 runs)

| act | gear pages | cache old | cache new | beat old | beat new | wall old | wall new | median Favor |
|---|---|---|---|---|---|---|---|---|
| act 1 | 1822 | 39.4% | 39.3% | 15.6% | 15.6% | 0.85 s | 0.85 s | 4 |
| act 2 | 2422 | 77.1% | 77.0% | 32.7% | 32.7% | 1.11 s | 1.12 s | 4 |
| act 3 | 2274 | 85.5% | 84.9% | 36.1% | 36.1% | 1.17 s | 1.18 s | 4 |
| act 4 | 2185 | 88.6% | 87.2% | 33.1% | 33.1% | 1.19 s | 1.19 s | 4 |
| act 5 | 1964 | 90.9% | 88.0% | 32.8% | 32.5% | 1.20 s | 1.19 s | 4 |
| act 6 | 644 | 99.8% | 100.0% | 61.5% | 60.2% | 1.35 s | 1.37 s | 4 |
| all | 11311 | 78.6% | 77.7% | 32.4% | 32.2% | 1.13 s | 1.13 s | 4 |
| non-boss | 7242 | 66.7% | 65.2% | 16.3% | 16.3% | 1.00 s | 1.00 s | 4 |

| encounter | gear pages | cache old | cache new | beat old | beat new | wall old | wall new | median Favor |
|---|---|---|---|---|---|---|---|---|
| Boss | 4069 | 99.9% | 100.0% | 61.0% | 60.7% | 1.34 s | 1.37 s | 4 |
| Elite | 1741 | 72.4% | 70.8% | 21.1% | 21.1% | 1.05 s | 1.04 s | 4 |
| Normal | 5501 | 64.8% | 63.4% | 14.8% | 14.7% | 0.99 s | 0.98 s | 4 |

## Mid and Late profiles (`-Runs 150 -Seed 1000 -Profiles Mid,Late`)

| act | gear pages | cache old | cache new | beat old | beat new | wall old | wall new | median Favor |
|---|---|---|---|---|---|---|---|---|
| act 1 | 2306 | 38.9% | 38.8% | 18.6% | 18.6% | 0.85 s | 0.86 s | 4 |
| act 2 | 3816 | 57.1% | 57.1% | 31.2% | 31.2% | 1.00 s | 1.00 s | 4 |
| act 3 | 4043 | 69.1% | 68.8% | 33.4% | 33.1% | 1.07 s | 1.08 s | 4 |
| act 4 | 3964 | 80.7% | 79.3% | 32.0% | 31.9% | 1.14 s | 1.14 s | 4 |
| act 5 | 3875 | 90.3% | 88.2% | 34.4% | 33.8% | 1.20 s | 1.20 s | 4 |
| act 6 | 1425 | 100.0% | 100.0% | 60.5% | 59.5% | 1.34 s | 1.37 s | 4 |
| all | 19429 | 72.0% | 71.2% | 33.1% | 32.8% | 1.09 s | 1.10 s | 4 |
| non-boss | 12032 | 54.9% | 53.5% | 16.5% | 16.4% | 0.94 s | 0.93 s | 4 |

| encounter | gear pages | cache old | cache new | beat old | beat new | wall old | wall new | median Favor |
|---|---|---|---|---|---|---|---|---|
| Boss | 7397 | 99.9% | 100.0% | 60.1% | 59.6% | 1.34 s | 1.36 s | 4 |
| Elite | 3321 | 63.9% | 61.9% | 22.0% | 21.9% | 1.00 s | 1.00 s | 4 |
| Normal | 8711 | 51.4% | 50.3% | 14.5% | 14.3% | 0.91 s | 0.91 s | 4 |

## Verdict against 1 in 5

- **All pages: fails.** Top-tier beat 32.2% (Fresh) and 32.8% (Mid/Late). The
  new rule moves it by 0.2 to 0.3 points.
- **Non-boss pages: passes.** 16.3% and 16.4%. Normal 14.7% and 14.3%; Elite
  is at the line (21.1% and 21.9%).
- **Boss pages: fail.** 60.7% beat, 100% cache by rule. They are 36% of the
  gear pages in the sweep (every realm ends in one), so they set the
  all-page rate.
- The cache is no rarer under the new rule: 77.7% of all pages (was 78.6%),
  65.2% of non-boss pages (was 66.7%). Mean wall time to choosable is 1.13 s
  at 1x, inside the 1.6 s cap; boss pages average 1.37 s.

## Limits

- **Most cards face an empty slot (87%), and that is how the roll plays, not
  a trace bug.** The bot equips what it takes (`RewardPages.Take` ->
  `EquipmentOps.Equip`) and `LiveUpgrade.WornTier` reads the same recipient
  and slot (`WornTier_ReadsTheRecipientsOwnSlot_EmptyIsZero`). There is no
  starting gear; a gear page comes about one room in four and a card fills
  one of 24 slots (3 members x 8), so about 6.5 slots are filled by the time
  a run ends; `GearPage.DrawSlot` weights an empty slot 3:1 and weapon cards
  target an empty Weapon1, then Weapon2, so cards keep landing on empty
  slots. A uniform model over the traced takes gives 90% empty in act 2 and
  66% in act 5 (observed 98% and 71%). A human faces the same bias, so the
  worn-tier test thins the cache late in a run, not early.
- **One quality scale.** The first sweep built reveal tiers from `Rolled`
  while `WornTier` used `QualityOf`; a Legendary with no trait left kept
  `Rolled = Legendary`. db515a0b made `Rolled` agree. Re-run on top of it, the
  Fresh sweep is byte-identical in `pages.jsonl` and `runs.jsonl` to the one
  before: the bot never hit the no-trait fallback. The Mid/Late table was not
  re-run (the same logic applies).
- Favor is flat at 4 in every act (`ItemOfferRoll.CurrentSquadFavor` is authored per
  character plus event buffs); the bot never reaches the plan's Favor 14, so
  this sweep cannot judge the mid-run Favor gate. Higher Favor only raises these rates.
- The bot's gearing is a floor on a human's.

## Open call

Read the gate as non-boss pages (passes), give boss pages their own rule, or
retune the ladder. This report does not pick.

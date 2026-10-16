# Reward cache rates (A9)

Question: does the cache stay rare? R4 gate: top-tier beat (Legendary live
upgrade) on at most one gear page in five at mid-run Favor
(`docs/PLAN_REWARD_SCREENS.md` C3, section 6).

## Method

- Batch: `tools/bot.ps1 -Runs 250 -Seed 1000 -Shards 1` (Fresh profile, all
  four archetypes, 1000 runs, 11,311 gear pages; 4,794 relic pages are not
  counted). Base commit `447046fd` plus the trace change.
- Trace: `BotRunDriver.Offer` records each card's quality and
  `LiveUpgrade.Of(save, card)` before the first choice; `BalanceBotRunner`
  writes them to `pages.jsonl` (a file of its own). Summary:
  `python tools/reward_cache_rates.py <batch-dir>`.
- Act = `step / 8 + 1` (a realm is 8 steps, `docs/ARCHITECTURE.md`).
- Cache = a Rare+ live upgrade on the page; beat = a Legendary live upgrade.
- Wall time: the C4 table arithmetic in the script (no cache 0.59 s; cache of
  3 cards 1.15 s, +0.25 s for a beat). Its constants match `RevealSchedule`'s,
  which landed after the sweep; the script does not call it.
- Neutrality: `-Runs 20 -Seed 1` with and without the trace gave
  byte-identical `traces.jsonl` and `runs.jsonl` (`cmp`).

## Results

Per act:

| act | gear pages | Rare+ / Legendary on page (live or not) | cache | top-tier beat | mean wall | Favor min/med/max |
|---|---|---|---|---|---|---|
| act 1 | 1822 | 43.9% / 17.9% | 39.4% | 15.6% | 0.85 s | 4 / 4 / 4 |
| act 2 | 2422 | 80.8% / 33.6% | 77.1% | 32.7% | 1.11 s | 4 / 4 / 4 |
| act 3 | 2274 | 88.0% / 36.8% | 85.5% | 36.1% | 1.17 s | 4 / 4 / 4 |
| act 4 | 2185 | 95.5% / 34.7% | 88.6% | 33.1% | 1.19 s | 4 / 4 / 4 |
| act 5 | 1964 | 99.8% / 34.9% | 90.9% | 32.8% | 1.20 s | 4 / 4 / 4 |
| act 6 | 644 | 100.0% / 64.4% | 99.8% | 61.5% | 1.35 s | 4 / 4 / 4 |
| all | 11311 | 83.5% / 33.9% | 78.6% | 32.4% | 1.13 s | 4 / 4 / 4 |

By Favor band (see Limits):

| Favor | gear pages | Rare+ / Legendary on page (live or not) | cache | top-tier beat | mean wall | Favor min/med/max |
|---|---|---|---|---|---|---|
| 0-7 | 11311 | 83.5% / 33.9% | 78.6% | 32.4% | 1.13 s | 4 / 4 / 4 |
| 8-13 | 0 | - | - | - | - | - |
| 14-19 | 0 | - | - | - | - | - |
| 20-up | 0 | - | - | - | - | - |

By encounter class:

| encounter | gear pages | Rare+ / Legendary on page (live or not) | cache | top-tier beat | mean wall | Favor min/med/max |
|---|---|---|---|---|---|---|
| Boss | 4069 | 100.0% / 62.1% | 99.9% | 61.0% | 1.34 s | 4 / 4 / 4 |
| Elite | 1741 | 78.2% / 23.3% | 72.4% | 21.1% | 1.05 s | 4 / 4 / 4 |
| Normal | 5501 | 73.1% / 16.5% | 64.8% | 14.8% | 0.99 s | 4 / 4 / 4 |

## Verdict

**Fails the 1-in-5 target.** Over all gear pages the top-tier beat is 32.4%
(1 in 3) and the cache 78.6%. Acts 2-5 sit at 33-36% beat.

- Normal pages pass: 14.8% beat. Elite pages are at the line (21.1%).
- Boss pages fail by a wide margin: 61.0% beat, 99.9% cache. They are 36% of
  the gear pages in the sweep (every realm ends in one), so they set the
  all-page rate. Read as non-boss pages, the gate holds.
- The live filter barely thins the cache: Legendary is on 33.9% of pages at
  all and 32.4% are live upgrades. The plan expected live-keying to thin the
  ceremony as the party gears up; it does not here, because the quality
  ladder climbs with depth as fast as the bot's gear.
- Act 6 is the depth cap's tail (644 pages), mostly boss pages.

## Limits

- Favor is flat at 4. `ItemOfferRoll.CurrentSquadFavor` is authored per
  character plus run event buffs; the bot never reaches the plan's Favor 14,
  so the mid-run Favor bands are empty. Higher Favor only raises these rates.
- Fresh profile only. A Mid/Late run failed to compile when another session
  overwrote the shared TestRunner2 copy mid-run; not retried.
- The bot's gearing is a floor on a human's; a human who keeps better gear
  sees fewer live upgrades.

## Open call

Read the gate as non-boss pages (passes), give boss pages their own rule, or
retune the ladder. This report does not pick.

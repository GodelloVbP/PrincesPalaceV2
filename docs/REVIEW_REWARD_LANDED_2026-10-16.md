# Review: landed reward-screen work vs PLAN_REWARD_SCREENS rev 5

2026-10-16. Read-only review of committed work only (`git show HEAD:`), not the
R3 working tree. Commits read: 4362c4df, 4ecf8618 (R0), f712ad4e (R1),
da763eae + ff8f12cc (RU), 529a14bc, 575b9264, 05dd7dcf, 7b98ceb0, ff122e6e
(R4a), 9b4d059f (R2), ebcaa92f (Sound), 43047cb8 + 2fa1c400 (A9), 76522bbf
(art, not wired). No Unity run; compile was checked only by reading usings
and referenced symbols.

State of wiring: `RevealSchedule`, `ReckoningTime`, `LiveUpgrade`,
`PreviewLeave`'s face, `HoningPreview`'s tooltip, `Prizes`/`GoldBefore` have
no runtime consumer yet except the bot and `Leave`. Most findings below are
latent: they bite when R3-R5 wire them.

## High

### H1. Two quality sources for one card: the reveal keys on `Rolled`, the face and `WornTier` on the affix-derived quality

- `GearPage.cs:243-272`: a Legendary roll with no trait left for its owner
  gets a third affix instead and keeps `Rolled = Legendary`.
  `ContentDatabase.QualityOf` (`ContentDatabase.Gear.cs:91-97`,
  `ItemQuality.Of`) derives quality from affix and trait counts, so that copy
  is **Rare** everywhere it is drawn (`RewardCardFace.OfGear` plinth, name
  colour) and everywhere it is worn later.
- The A9 trace builds `RevealCard` tiers from `c.Rolled`
  (`BotRunDriver.cs:1956`, `BalanceBotRunner.cs:661`), while
  `LiveUpgrade.WornTier` (`LiveUpgrade.cs:30-36`) uses `QualityOf`. Card tier
  and worn tier are measured on different scales.
- Trait supply shrinks as a member gears up (`TraitsFor` drops kinds already
  gotten, `RewardPages.cs:470-489`), so this gets more frequent late in a
  run, which is where A9 measures the beat.

Failure: when R4 wires `RevealCard.Gear(card.Rolled, …)`, a card whose plinth
says Rare gets the hold, the burst, the flare and the 1.2 s linger (breaks
C4.8 "a card never shows a higher quality than it has", and M1's point). A9's
32% beat rate counts these cards as top tier. Adjacent, not in this work:
`GearPage.cs:236, 279` reset Legendary pity on such a card, so the player's
pity resets on a Rare.

Fix: one quality per card. Either set `Rolled = Rare` in the trait fallback
(fixes pity too), or build `RevealCard` from `ContentDatabase.QualityOf(card.Copy)`
and stop tracing `Rolled`. Then re-run A9. Unmeasured: how many beats this
removes; the trace has no field for it (add `derived` beside `q`).

## Medium

### M1. The RU rework reverted "ReckoningTime owns the speed rule"

529a14bc made `RunUpSchedule.TimeScale` call `ReckoningTime.Scale`;
ff8f12cc (later) rewrote the file and put the formula back
(`RunUpSchedule.cs:87-90`). `ReckoningTime`'s header still claims to be the
one home. `ReckoningTimingTests.RunUpSchedule_ReadsTheSameRule` (lines 52-55)
compares the two functions, so it passes against a copy and could not catch
this. `RunUpSchedule` also ignores the `SpeedMultiplier` seam.

Failure: the next change to the rule (a seam, a preset table) moves the
Reckoning and not the run-up. Lost-update sign on a commit made without a
clean gate; I checked the other files ff8f12cc touched against 529a14bc and
found no other overlap.

Fix: restore the delegation (or delete `TimeScale` and call
`ReckoningTime.Scale` from `Build`). Turn the test into a literal pin on
`RunUpSchedule` at 2x (e.g. `WashSecondsWall == 0.25`).

### M2. A paying event fight carries no settlement record (`GoldBefore = 0`, no fight-gold prize)

`SettleEventFight` returns `PayOut(...)` directly
(`RunOrchestrator.Event.cs:577`); only the room path fills `Prizes` and
`GoldBefore` (`RunOrchestrator.cs:633-643`). C2 says fight gold is recorded
with `Shown = None` and the count runs from `GoldBefore`.

Failure: R5's gold stack after an event fight counts up from 0 (or shows the
wrong total) with a run holding, say, 240 gold. `RewardDataLayerTests:72`
pins `GoldBefore == 0` on a run whose gold is 0, so "set" and "never set"
look the same.

Fix: record in `PayOut` (shared by both paths): take `goldBefore` before the
first pay of either path and add the `None -> Gold` prize there. Pin a test
with non-zero starting gold, for a room fight and an event fight.

### M3. The reveal's skip has no guard, and its gate predicates keep the unskipped clock

`IsRunningAt`/`GuardsAt` (`RevealSchedule.cs:317-321`) compare against the
unskipped `ChoosableAt`; `SkippedAt` (`:327-332`) returns a new
`ChoosableAt` but no guard window.

Failure, either way R4 wires it: if the controller keeps asking the schedule,
presses after a skip at 0.5 s are swallowed until 1.15-1.6 s (dead input over
face-up cards), then the guard fires on a later press; if it tracks a skip
flag, there is no guard after the skip, so a double-tap or key repeat takes the
focused card one frame after it turned face-up (the case the C10 guard
exists for).

Fix: make the skip return a schedule like `RunUpSchedule.SkippedAt` does
(`Skipped`, `ChoosableAt = now`, `GuardUntil = now + W(Guard)`), with
`IsRunningAt`/`GuardsAt` reading it. Pin: skip at 0.5, `GuardsAt(0.6)` true,
`GuardsAt(0.66)` false. Drop the constant fields `LingerSeconds`/`AllFaceUp`
or give them meaning.

### M4. A rising chevron note for a card that shows a flat bar

`RevealSchedule.For` emits a `Chevron` beat (a step of the rising ladder) for
every `LiveUpgrade` card (`RevealSchedule.cs:240`). `LiveUpgrade` counts an
affix gain with no stat change as live (`LiveUpgrade.cs:47-54`), while
`RewardCardFace.SetDelta` gives that card `DeltaSign = 0`, the grey flat bar
(`RewardCardFace.cs:222-225`). The plan says both (C3/A1 "every live upgrade
pops its up-chevron"; C5 "no stat moves but affixes change: the flat bar").

Failure: the player hears good news on a card whose face says "no change".
Plan contradiction carried into code; owner or plan call.

Fix: either the face shows the up chevron (plus the affix icon) for an
affix-gain live card, or the `Chevron` beat keys on `DeltaSign > 0` and the
cache keeps `LiveUpgrade`. Pick one in C3/C5.

### M5. The sound commit contradicts C11, and two placeholders are the sounds C11 bans

- C11, M5 and §8 say "no engine change; pre-pitched clips"; ebcaa92f adds a
  pitched `AudioSource` pool and `Play(Sound, pitch)`. The plan was not
  updated, so the plan and the code now disagree. The pool is a reasonable
  answer to M5 (it does not retune the main source); record it in C11 or
  revert it.
- `SoundController.cs:264-265`: "the oldest shot is cut" is a round-robin
  index, not the oldest; it can cut a shot that just started.
- `Sound.cs:102` `CoinTick -> "button click"` and `:110` `Fell -> click_fail`.
  R6's gate ("no `ButtonClick` or `ClickFail` from a Reckoning control")
  checks enum values, so it passes while the player hears a button click per
  coin and the refusal buzz on a Fell.
- Missing cue names: ember lights (breath), chevron ladder, boss peak.

### M6. Run-up drift at the plan's precedence rules

- **Endured plays the sting.** C0: "An Endured fight skips steps 1-3 and the
  sting." `BeginRunUp` excludes `Survived` correctly, but `OnPlaybackFinished`
  still plays `VictorySting` and the voices for any `PlayerWon`
  (`FightController.Input.cs:1492-1502`).
- **Leader-fall timing changed.** `MarkLatestBeatEndsFight` runs for every
  non-Survived win (`FightSession.Riders.cs:283`), so a leader-fall's last
  beat gets the final-blow stop (`FightBeatPlayer.cs:1197`) and loses its
  settle (`:1258-1267`) before `BeginLeaderFallEnding`. C0: "the existing
  endings stay first and keep their timing." Not verified by running it.
- **Elite event fights.** C0: "Event fights follow the same rules with the
  normal-class final-blow stop." `RunEncounter.cs:100` sets
  `IsElite = EventFight.Elite`, so `session.EncounterClass` is Elite and the
  run-up uses the elite stop, wash and sag.

Fix: gate the sting on `EndReason != Survived`; mark `EndsFight` only for
`Defeated`, or keep leader-fall's weighted stop; pass a class to the run-up
that is Normal for event fights.

## Low

- **L1. A second fight-class ranking.** `FightSession.EncounterClass` says it
  is the one ranking (`FightSession.cs:52-61`); `SettleFight` builds another
  (`RunOrchestrator.cs:642-643`: boss from the session, else
  `RealmPrizes.ClassOf(room)`), and `EncounterRequest.cs:64` a third. They
  agree today only because `IsAnchor == EliteFight`. Use
  `session.EncounterClass` when a room was fought.
- **L2. NRE.** `int goldBefore = run.gold` (`RunOrchestrator.cs:633`) in a
  method that elsewhere treats `run` as nullable (`:580`). `run?.gold ?? 0`.
- **L3. The record can claim what was not paid.** `RealmPrizes.cs:64`
  records gold even when `CreditPayout` returns false; `:69` records
  `chip.Amount` potions even when none were drawn (`save == null` or an empty
  pool). Record `ids.Count`, and gold only on a credit.
- **L4.** `RunUpSchedule.SkippedAt` adds unscaled `VoiceDelaySeconds`
  (`RunUpSchedule.cs:177`): at 2x, a skipped run-up's voices come 0.2 s after
  the sting instead of 0.1 s.
- **L5. RevealSchedule details.** Comments at `RevealSchedule.cs:140-141` and
  `:270-271` say only a page past five cards can pass the cap; the plan (C4)
  and the code's own test show 4 cards with two holds at 1.71 s. A boss page
  with zero cards indexes `landAt[-1]` (`:232`). No haul-tick beats, which
  C10 lists.
- **L6. Settlement now waits for the run-up.** `RouteFightEnd` runs at the
  end of `OnPlaybackFinished`, after the run-up, so `SettleFight` happens up
  to 1.3 s after the kill. A quit in that window leaves the fight
  unsettled. Darkness already had the same window; unverified what a reload
  does there.
- **L7. Plinth width, not outline.** C5 makes the plinth outline (four
  silhouettes) the greyscale quality channel; R2 steps width
  (`OfferRowLayout.PlinthWidth`). Fine as a placeholder; A2's art has to carry
  the outline or C5 changes.

## Tests that recompute production values

- `OfferRowLayoutTests:195`: expected `CardWidth(3) * fraction` restates
  `PlinthWidth`'s formula. Pin the widths (e.g. 238.13 / 272.15 / 306.17 /
  340.19).
- `RewardCardFaceTests:114` (`ContentDatabase.NameOf(copy)`), `:142`
  (`ItemIcons.KeyOf(...)`): production calls as the expected value. Pin the
  name and key strings.
- `RewardCardFaceTests:175, 180`: `Contains("+")`/`Contains("-")` do not pin
  the delta. Pin the number.
- `FightRunUpTests:196`: expects `RunUpSchedule.EnemyDim`. Pin 0.10.
- `ReckoningTimingTests:52-55`: two production functions compared (M1).
- R1's gate asked for a bot-neutrality test pinning the save JSON and notice
  list of one settlement before and after; there is none. The commit message
  cites a manual 40-run batch instead.

`ReckoningTimingTests` and `RunUpScheduleTests` otherwise pin literals well.

## Contracts that hold

- B2: the slot band shows `HoningOf(recipient, slot)`, the name has no `+N`,
  the ghost draws no honing, and the band stays lit on a taken card
  (`RewardCardFace.cs`, `RewardCardFaceTests`).
- C13: `Leave` consumes `PreviewLeave`; the target rule matches the old
  `CanHone` loop (`SlotHoning.cs:41-42`), including spill to the next slot at
  `MaxPlus`. `HoningPreview` sits at the one seam (`HoningOf`), clamps, and
  unwinds when the body throws.
- C12 room path: pay order, `Shown = None` fight gold, overflow and exhausted
  pool recorded as gold, class captured before `AdvanceLeg`.
- C4 timing: the table, the 1.6 s cap, the shortening order (wait, stagger to
  0.02, hold to 0.10, then to 0) and `min(1, Default/preset)` match the plan;
  I hand-checked a 10-card, 5-hold page and it lands at 1.60 s.
- C3 rules as shipped (ff122e6e): gear cache needs live, Rare+ and above the
  worn tier; boss always; relic Rare+ live; hold only for a live Legendary
  over a worn tier below 4. A hold implies a cache, so no hold ever runs on a
  deal-only page.
- B1 is not exposed in committed code: nothing plays a reveal yet, so the door
  and whetstones cannot fire mid-beat. It stays R3/R4's to close.

## A9: is `WornTier` or the bot wrong about 87% empty slots?

From the code alone, neither. `WornTier` reads the same roster equipment the
page roll reads (`MemberOf` → `PageMember`), guards empty slots, and
`GetInstance` is null only for an id-less entry. The empty share comes from
the roll:

- `GearPage.DrawSlot` weights an empty slot 3 to a filled one's 1
  (`GearPage.cs:170, 357-373`).
- A member with an empty `Weapon1` always gets a weapon card there, and
  `WeaponTarget` sends a weapon card to an empty `Weapon2` whenever one
  exists (`:197-209, 346-352`).
- With fewer than three members, owners start at the member with the most
  empty slots (`:288-316`).
- Gear is run-scoped and characters start bare (no starting-gear field in
  `characters.json`); a party has 3 x 8 = 24 slots, and the sweep shows about
  11 gear pages per run, each filling about one slot (less when the bot
  hones instead).

Rough check: with about 6-7 of 24 slots filled on average across a run,
P(card faces a filled slot) = F / (F + 3E) is about 6.5 / (6.5 + 3 x 17.5),
about 11%, so about 89% empty. The 87% is what the roll produces at the
bot's fill rate, not a lookup bug.

Two corrections to the report's reading:

- "A human who fills slots will see the new rule bite more" is true only
  through the fill rate (shop, events). The 3:1 empty-slot bias applies to a
  human too, so the worn-tier rule will stay weak for anyone until mid-run.
- The real defect in the measurement is H1: card tier from `Rolled`, worn
  tier from `QualityOf`. Also, Favor is flat at 4 in every act, so the R4
  gate ("at mid-run Favor") cannot be evaluated from this sweep at all.

## Open questions

1. H1: fix at the roll (`Rolled = Rare` on the trait fallback, which also
   moves pity) or at the reveal (`QualityOf`)? The roll fix changes a bot
   number (pity); the reveal fix does not.
2. M4: which side is right, face or ladder?
3. M5: keep the pitched pool and amend C11, or go back to pre-pitched clips?

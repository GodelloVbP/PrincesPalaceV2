# Senior review: PLAN_REWARD_SCREENS.md rev 4

Target: `docs/PLAN_REWARD_SCREENS.md` at dcb54aa9 (rev 4). Read against the
code at that commit. Review only; nothing in the plan or code is changed.
Rev 4 changed §2, §11, C4.4 (Legendary linger), C4.6 (no near miss), a
quality-versus-delta note in C5, O3's evidence and two rejections. Findings
that rev 4 already answers are dropped; where rev 4 answers part of one, the
item says what is left.

Counts: 2 blockers, 10 major, 9 additions, 4 owner-call rulings, 10 minor.

## Blockers

### B1. Leave and hone are not gated during the reveal (C10, C6)

**Claim.** C10 consumes presses during a beat in two places only: the
full-frame catcher and `Take(i)` checking `_revealing`. The door and the
whetstones are real Buttons on screen during the reveal, wired straight to
`LeaveOrAdvance` and `HoneInstead` (`ReckoningController.cs:264-270`). A click
on the door while the cards are face-down calls `RewardPages.Leave`, which
pays and closes the page (`RewardPages.cs:250-281`), and `AfterChoice` saves
it (`ReckoningController.cs:1043-1045`). The player loses the page without
seeing it, and it cannot be undone.

**Fix.** One gate, not three. Route take, hone and leave through one
`Act(kind, index)` that refuses while a beat runs and plays the nudge, or keep
the whetstones and door out of the tree until the row is choosable. The R4
gate becomes "a press during a beat never calls `Take`, `HoneInstead` or
`Leave`", driven from each control by both mouse and Submit.

### B2. The ghost shows honing as belonging to the item; the code says it belongs to the slot (C5, C6, C13)

**Claim.** "Honing belongs to the slot, not to what it holds"
(`ContentDatabase.Gear.cs:28-33`; `SlotHoning` is keyed by character and
slot). A taken item inherits the slot's +N, and today's card name already
carries it (`RewardCardFace.cs:50-53`). The plan draws honing on the worn
item: the hone spark goes "into the ghost", "the ghost lifts" on hone focus,
leave sparks "wait on the card's ghost". That picture is false, and it misleads
the player about the decision: someone who believes taking a card throws away
their +4 will take less often. Under "show, don't tell", a false picture is
worse than a sentence.

It also breaks the leave preview. `Leave` targets the first card whose slot
can still be honed and ignores the card's outcome (`RewardPages.cs:265`), so
after a take it often hones the slot just filled. That card has no ghost (an
empty plinth), and several cards can share one slot. "Lights on its card's
ghost" has no defined target in either case.

**Fix.** Put honing on the slot. Engrave `+N` on the wearer's slot pip, or on
the plinth's front band beside the tier, and keep it there while the items
swap. On a take, the new item settles onto the band and the `+N` stays lit.
On a hone, the band sparks, not the ghost. Leave sparks go to the wearer
medallion's slot pip, so the target exists whatever the cards' outcomes and
however many cards share the slot. The ghost stays, as "what you wear now",
with no honing drawn on it.

## Major

### M1. Ceremony keyed on absolute quality will be routine by mid-run, and it can promise a downgrade (C3, C4; rev 4 C5 note)

**Claim.** The cache appears whenever a page holds a Rare or better. The roll
is geometric: `LootLadder.Climb` (`Domain/Rewards/LootLadder.cs:69-93`) steps at `ladderStep +
0.006 x Favor`, capped at 0.55 (`LootLadder.cs:43-53`). For a three-card normal
page (`rewards.json` normal_gear, ladderStep 0.35), at the Favor 14 the plan's
own audit shows (step 0.434):

- P(page has a Rare or better) ≈ 0.47; P(page has a Legendary) ≈ 0.23.
- At the cap (0.55): ≈ 0.66 and ≈ 0.42.

On top of that, Legendary pity forces one at least every 7 gear pages
(`GearPage.cs:147, 236-240`), a strength-3 node floors the best card at Rare
(`GearPage.cs:431-437`), and every boss page has a Rare weapon floor. By fight
50, most pages open with the cache and a third of them run the Legendary beat
plus the 1.2 s linger. The plan's own F7 (Diablo IV overload) predicts what
follows, and so does Gearbox's stated reason for reworking Borderlands 4 loot:
"making rarity matter again" after Borderlands 3 dropped too many legendaries
(https://www.pcgamer.com/games/fps/gearbox-is-making-rarity-matter-again-in-borderlands-4-after-dropping-too-many-legendaries-in-borderlands-3/;
headline-level, the body did not fetch).

Rev 4 accepts that quality and delta disagree, but the cache is a promise
made before the cards are seen, and it is made on quality alone. An inert or
lower-tier Legendary is a ceremony that ends in a downgrade: the Diablo III
"blues beat legendaries" confusion that F6 cites, staged as a fanfare.

**Fix.** Key the ceremony on what the player can see on the card, which is
not a hidden counter. The cache appears when the page holds a Rare or better
card that is live and not a downgrade for its wearer (a positive
`GearEvaluator.LargestDelta`, or an affix or trait gain). The held beat and linger fire only for such a
Legendary. Late in a run, when the party is well geared, upgrades get
scarcer and the ceremony thins out on its own, with no counter. A face-down
Legendary that is a downgrade flips like any card, and its plinth still says
Legendary. Pin the frequency with a bot-seed sweep (`tools/bot.ps1`) reporting
cache rate and Legendary-beat rate per act. Gate: the Legendary beat fires on
at most about one gear page in five at mid-run Favor.

### M2. C13's honing override sits on the wrong seam (C13)

**Claim.** The plan wants "an override parameter on that path" inside
`EffectiveStats`. Honing is read in at least three places on that path:
`ActiveLoadout` (`ContentDatabase.Effective.cs:144`), `EffectiveStats` (182)
and weapon power (657). `ItemDescription.Compare` also reads it through
`SimulateEquip`. Threading a parameter through all of them means rewiring
every caller. Gear-level numbers need no override at all:
`ContentDatabase.Effective(copy, honing)` already takes the honing value
(`ContentDatabase.Gear.cs:20-26`).

**Fix.** Override once, at `HoningOf` (`ContentDatabase.Gear.cs:32`): a
scoped, disposable preview that `HoningOf` consults first, for example
`using (HoningPreview.Add(characterId, slot, +1)) { … Compare … }`. Then
every reader agrees with no new parameters. The test pins two things:
`slotHoning` is unchanged after dispose, including when the body throws;
and a nested preview stacks correctly. This is the standing "swap the seam,
not every caller" rule.

### M3. C12's record belongs on `CombatReward`, not on the `FightSettlement` struct (C12)

**Claim.** `ReckoningRequest.Full` is built from `settled.Reward`
(`FightController.Input.cs:1245-1246`). `CombatReward` is a mutable class
that already carries the notices `WithNotices` appends to
(`CombatReward.cs:134-161`; `RunOrchestrator.cs:487-492`), and it has a
`TotalGold` field nothing writes. Adding three fields to the readonly struct
creates the five-construction-site copy hazard that the plan then has to
police.

Two smaller errors. First, "would drop them on every room fight" is
overstated: `WithNotices` returns `this` when there are no notices
(`RunOrchestrator.cs:489`). Second, the class has to be captured before
`AdvanceLeg`, not before `ClearCurrentRoom`. `ClearCurrentRoom` does not move
the node (`RunManager.cs:380-392`); `AdvanceLeg` sets `currentNodeId = -1`
and moves the step (`RunManager.cs:409-418`), and that is what loses the
boss's node.

**Fix.** Put `Prizes`, `EncounterClass` and `GoldBefore` on `CombatReward`,
filled where `room` and `chips` are built (`RunOrchestrator.cs:624-631`).
`ReckoningRequest` reads them off `Reward`. The outcome form needs none of
them: nothing was paid, and its medallion state comes from `Ending`. Bot
neutrality holds the same way. Also say what the fight's own gold is
(`GoldGained`, which no map chip showed): a `PaidPrize` with `Shown = None`,
or counted separately.

### M4. The board's downed and fell states contradict the experience rule (C8)

**Claim.** "Did not fight / fell: portrait tinted grey, ring still" puts two
cases in one state, and both are wrong:

- A member who fell **during** the fight was fielded and gets full
  experience.
- A member left out because they were at 0 HP (`IsDowned`, meaning not
  fielded) gets **half**, rounded up (`VictoryRewards.cs:184-190`;
  `RewardApplier.cs:66-87`).

Today's screen already hides that half: it sets before = after
(`ReckoningController.cs:587-613`), and `StartBars` skips downed rows (1348).
"Ring still" would carry that bug into the rebuild.

**Fix.** Three states. Fielded: full fill. Fell in the fight: full fill, plus
a mark from `Ledger.TimesDowned > 0` (`CombatLedger.cs:42`). Not fielded:
grey portrait, half fill shown in the amber "this fight" tint. Fix today's
hidden half-gain in R5 as a bug, with the test pinned to a literal.

### M5. The sound plan does not fit `SoundController` (C11)

**Claim.**

- **Pitch.** `Play` is one `AudioSource` with `PlayOneShot`
  (`SoundController.cs:12-15, 119`). Pitch is a property of the source, not
  of the shot, so a "pitch overload" retunes every clip still ringing on it,
  including the victory sting and earlier flips.
- **Double sounds.** The offer cards are `.NoChrome()` but not `.Quiet()`
  (`ReckoningScreen.cs:1020-1021`), so `ButtonPressAnimator` plays
  `ButtonClick` on every take, on top of the new clink. The cache button and
  the catcher would click as well.
- **Refusal sound.** A press on a non-interactable button plays `ClickFail`
  (`ButtonPressAnimator.cs:55-58`). Disabling cards during a beat would make
  every consumed press buzz.

**Fix.**

- Pitch: either a small pool of pitched UI sources (round-robin, 4 is
  enough) behind `Play(Sound, float pitch)`, or pre-render the flip at its
  four pitches as four clips, which needs no engine change.
- Mark every Reckoning control `.Quiet()` (cards, cache, catcher, whetstones,
  door) and let the controller own the sound.
- Gate with `EveryButtonInTheScene_HasAClickSound`, which already exists:
  extend its exemptions to the Reckoning's quiet controls.

### M6. Relic pages are underspecified (C3, C4, C5)

**Claim.** Relics use `RelicRarity`, which has six tiers (Common to Godlike,
`RelicRarity.cs:15-23`), not the four gear qualities. The plan does not say:

- what "Rare or better" means for a relic page;
- which of the four plinths a Mythic or a Godlike relic sits on;
- how "a step in pitch per quality" maps onto six tiers.

Relic focus also has a third state the plan drops. A relic can reach a member
who already gets its mechanic (`PendingCard.AddsNothingFor`,
`RewardPages.cs:129-144`, today's "adds nothing for" line). Drawing reached
members "in colour" shows that member as a gain when the relic adds nothing
for them.

**Fix.**

- Write the relic mapping table: rarity to plinth, rarity to pitch step, and
  the cache threshold. Rare and up is about 16% per card on anchor weights
  `[100,45,18,6,2,1]`, so roughly 40% of three-card pages.
- Portraits on relic focus get three states: gains (colour), reached but
  redundant (colour with a hollow ring), and not reached (grey).

### M7. The haul splits attention and sound with the reveal (C2, C3)

**Claim.** C2 runs "alongside" C3/C4. Coins spill into the top-right with up
to 12 ticks, flasks fly bottom-left and a book drops, all while the cache
seals, cards slide and flips climb in pitch. That is several focal points and
several sound layers at once, which goes against the plan's own principle 1
(one bit at a time) and against F10 (keep routine effects small). On one
`PlayOneShot` source it turns to mud.

**Fix.** Sequence it, inside the same wall-time cap. The haul lands during
the 0.34 s wipe and the open wait. The Gear or Relic chip the map showed is
the object that becomes the cache (addition A2), so the haul is the cache's
arrival, not a parallel track. Gold ticks finish before the first flip. With
no page, the haul plays into the board as planned.

### M8. Following the battle speed at 0.5x breaks the plan's own cap (C4, C10, O3)

**Claim.** The presets are 0.5, 1, 1.5 and 2, with the default at 1
(`BattleSpeed.cs:36-70`). The C10 formula doubles every duration at 0.5x: the
1.52 s Legendary page becomes 3.04 s. The 1.6 s cap then has to cut the held
beat and the open wait, so the slow-preset player gets the most truncated
ceremony. That is backwards, and it means the slowest preset needs its own
special-case path. See ruling O3.

**Fix.** `factor = min(1, DefaultDisplay / preset.Display)`. The Reckoning
speeds up with 1.5x and 2x and never slows below its authored times. The cap
then holds by construction, and the R4 gate "within the cap at the slowest
preset" becomes a one-line EditMode assertion.

### M9. The phase gates cannot catch the failure this plan exists to prevent (§6)

**Claim.** Every gate is structural: trees, test names, pinned saves. The
owner's ask is feel ("flat, slow, repetitive"), and §6 accepts "tests green,
not seen" as a phase result. Two of the planned gates will also be weak:

- **Timing.** PlayMode wall-time assertions on `Time.unscaledDeltaTime`
  loops are flaky on a loaded machine.
- **Ownership.** C10's `ReckoningTime` is listed in both R4 ("C10") and R6
  ("the timing helper across every wait"). R4's cap gate needs the preset
  scaling, which R6 delivers.

**Fix.**

- (a) Make the reveal a pure Domain model, `RevealSchedule.For(page cards,
  row order, preset)`, returning timed events (deal, seal, flip i, hold,
  land, linger end, choosable). The controller only plays it back. The cap,
  the flip order, the Legendary hold, pitch steps and skip-to-end become
  EditMode tests on literals, and PlayMode checks only that the controller
  consumes the schedule. This is "build the model".
- (b) Move `ReckoningTime` wholly into R4.
- (c) Add a stop/go gate after R4: a `tools/gif.ps1` capture of the three
  fixtures from R0 (no cache, cache with a Legendary, five cards). Watch each
  GIF before relaying a verdict, and get the owner's yes before R5 builds on
  that pace.
- (d) Bring a placeholder sound layer forward into R4 (the existing
  `OpenPack` plus one flip clip), so the reveal is never judged silent.

### M10. R5 rewrites four test classes in the same commit as the screen they guard (§6 R5)

**Claim.** `ReckoningTabTests`, `ReckoningChipNavigationTests`,
`ReckoningGamepadNavigationTests` and `ReckoningScreenTests` are all
rewritten in the phase that deletes the tabs. A test suite rewritten together
with the screen it checks proves only that the two agree with each other.

**Fix.**

- Write the new board navigation graph as an EditMode test first, against
  `RuntimeNavWiring` groups and links (Continue entry; Up to the haul rail;
  Up to the portraits; clamp; Cancel = Continue). Commit it red-to-green
  before the tabs go.
- Keep `ReckoningGamepadNavigationTests`' choice-phase cases untouched
  through R5.
- `ReckoningPhaseTests` pins `ReckoningContinueGlow`
  (`ReckoningScreen.cs:440-447`). Removing the glow in C8 must update that
  test in the same commit.

## Additions

Sourced where a source exists; the rest is marked opinion.

### A1. Pay off the delta, not only the rarity (C4, C5)

Every page, including the no-cache majority, gets one payoff that is about
*this party*: when a card lands face-up with an up-chevron, the chevron pops
with a small rising note, one step higher per up-card in row order. A
downgrade lands silent. Hades makes reward identity audible: since patch 032,
boon choices "sound" by god and upgrade type (fan analysis,
https://teemo.dev/game-design/hades/systems/audio-design/). Diablo III's
answer to fast evaluation was comparison arrows (plan F6). A common page
that holds an upgrade then sounds like good news, which is what makes a
player look forward to the screen. Without it, no cache reads as "nothing
here" before the cards are even read. [Opinion on the mechanism; sources for
the two channels.]

### A2. The map's chip becomes the cache (C2, C3)

The Gear chip the player chose on the map flies from the gold stack's corner
to the row centre, and *unfolds* into the cache. With no Rare inside, it
unfolds straight into the dealt cards. The promise made before the fight
(F9, Hades door symbols) is then visibly the reward arriving. Destiny's engram
works the same way: the object you carried is the one that decodes (F2). This
also resolves M7. No new art: the chip art exists
(`map_chip_gear.png`).

### A3. The take answers the reveal (C6)

Make the flips' rising pitches an unresolved phrase and the take its
resolution. The take clip lands on the root of the ladder the flips climbed,
so choosing resolves a musical tension the reveal built up. The plan already
has "rising pitch, held beat" (rev 3's principle 8); this extends it to the
moment of choosing, which §2 calls the climax. [Opinion.]

### A4. Let the soul respond (C6, C8)

The premise is Prince giving gifts to souls who fight out of love. Repeated
rewards earned for others lose their neural response more slowly than rewards
earned for oneself (Luo et al. 2022, *Psychophysiology*, "eudaimonic" versus
"hedonic" rewards, https://pubmed.ncbi.nlm.nih.gov/34846754/). That suggests
framing the take as a gift received. On a take, the wearer's medallion warms,
and on a Legendary take only the wearer's own voice bark plays. It is rare,
so it is not repetitive, and `CharacterVoice` exists. The study measured
money, not games, so the application is opinion; the barks are already an
open item in the sound pass.

### A5. One peak above the Legendary (C4, C11)

Keep the top ceremony for the rarest event, not the rarest quality: a boss
page that holds a Legendary upgrade gets one short musical sting under the
bell. Peggle's Extreme Fever pairs its rarest moment with Ode to Joy, and
PopCap called it the thing that "will never change" (fan wiki making-of,
https://peggle.fandom.com/wiki/Peggle_Deluxe/Making_of_Peggle). Scale the
effect to the event (F10). Everything below it stays as the plan has it.

### A6. Level-up points at what is now waiting (C8)

A level-up today is a bell and a numeral, but its real payoff is the reward
track, and collection there is manual by the owner's decision (memory:
manual reward collection is decided; `RewardApplier.cs:90-114`). On a
level-up, the badge shows the track's waiting pulse (the same `BeaconPulse`
the track uses). It shows that something is waiting without claiming it.
[Opinion; respects the decision.]

### A7. The stage shows the ending, so the plaque word is not needed (C1.3, O2)

The overlay sits on the live fight (`ReckoningScreen.cs:6-10`). Lower the
gloom over the stage for the outcome form's first 0.4 s: standing enemies say
Endured, an empty field says Victory, and a downed party says Fell. The board
and medallion then confirm it. This answers O2's "Endured and a paid-nothing
Victory look alike". [Opinion.]

### A8. Cut list, if scope has to give (§5, §9)

Ranked, cheapest loss first:

1. Merge satchel, book pile and bag into one haul strip at the frame's foot.
   Four destinations on the choice phase compete with the cards.
2. Two caches (cloth for gear, tin for relics) with the class shown by
   binding alone. The medallion already carries the class.
3. Drop the outcome-form medallion states if A7 lands.
4. Halve the eleven new sound clips: deal, flip, take, bell, coin and hone
   carry most of it.

### A9. A bot-seed frequency report as a standing check (M1)

A one-off run of `tools/bot.ps1` over N seeds that prints, per act: cache
rate, Legendary-beat rate, mean wall time to choosable. This is the only
evidence that "fight 50 is not a tax" will stay true when the item plan
retunes the ladder. Keep it beside the plan; it is not a commit gate.

## Owner-call rulings

1. **Favor (O1): (a)**, a squad-Favor row on the RUN pane fed from
   `CurrentSquadFavor` (`ItemOfferRoll.cs:118-133`). Favor is a stat the
   player builds; hiding it entirely removes their view of their own
   investment. Odds on the reward screen are the "telling" the plan is
   removing.
2. **Ending words (O2): drop all three.** Endured is a survive-N fight
   (`FightEndRouting.cs:64`), and the stage behind the overlay shows it (A7).
   A word kept for one of three states makes the other two read as missing a
   word.
3. **Battle speed (O3): follow faster presets only** (`min(1, …)`, M8). F5's
   complaint is about length, and 2x players get the shorter screen. A 0.5x
   player chose a slower *fight*, not a longer reward screen, and the full
   follow breaks the 1.6 s cap.
4. **Relics and tally (O4): agree with the plan.** Drop the held-relic list
   (same `RelicListPresenter` on the RUN pane) and fold the tally into deeds.
   Add `Shielded` (`CombatLedger.cs:38`) to the portrait's focus tooltip
   beside the physical/other split, since a tank's fight is invisible
   without it.

## Rejected objections: verdicts

- **"Collapse the ceremony after N fights": rejection right, reason wrong.**
  No hidden counter is correct. "Value-proportional ceremony and the cap keep
  fight 200 short" is false on the numbers in M1: most mid-run pages get the
  cache. Replace the reason with M1's fix (key the ceremony on a visible
  upgrade, so it thins out as the party gears up).
- **Player-driven flips: right.** F5 applies to every page of every fight.
- **Slot-machine reel: right**, and rev 4's corrected reason holds.
- **Drops on the fight stage: right on cost.** A2 and A7 take the useful part
  (the promise kept; the stage as witness) without touching
  `FightController`.
- **Focus on best; ascending-quality flips; medallion-rim chip; household
  plinths; coin badge on whetstone; drop haul row; first-time ghost; card
  frames; hover tilt: all right.** One caveat on the medallion-rim chip:
  rejecting it for *the worn item* was right. Its 24 px slot pip is still the
  right home for *slot honing* (B2).

## Minor

1. **C4 timing table** omits the most common page: three cards, k = 1. With
   a cache that is 0.34 + 0.2 + 0.12 + 0.25 + (2 x 0.06 + 0.12) = 1.15 s.
   Add the row; it is the one the player sees most.
2. **Rev 4 C4.4** says "a take or a press ends the linger at once". C10 says
   "Moving focus never skips". State that Left/Right does not end the
   linger.
3. **C10 entry handoff**: rely on NavContext's "remembered ?? entry"
   (`NavContext.cs:231-250`). A player who moved to a face-down card keeps
   it. Declare face-down cards as selectables from the slide onward, or the
   remembered id is discarded. Consider `NavContext.LazyEntry`
   (`NavContext.cs:35`, 675e25f9) for the wipe frames before the cache lands.
4. **C6 door**: it sits at the row's right end but is reached only by Down.
   Add Right from the last card to the door, so the spatial and the logical
   links agree.
5. **C5 name**: say whether the item name keeps today's `+N` suffix
   (`RewardCardFace.cs:53`). With B2 it should go, since the slot band shows
   it.
6. **C2**: for the fight's own gold there is no `Shown` chip. See M3.
7. **C12 test**: "a won room fight's `Prizes` is non-empty" does not hold for
   an Item-only chip with no notices if `Prizes` records only gold. Pin what a
   Gear chip records.
8. **C3**: the cache's class binding and the crest medallion both say the
   class, so the class is shown twice. Fine to keep, but A8.2 is the cheap
   cut.
9. **§4.12**: when a menu opens over the Reckoning, `SettleImmediately` should
   also settle the gold count-up and any running fill, not only the reveal.
10. **§9**: add `Core/ButtonPressAnimator.cs` / UiNode `.Quiet()` call sites
    (M5) and `ContentDatabase.Gear.cs` (`HoningOf` preview, M2) to the file
    list. `Core/Content/ContentDatabase.*` is too broad for a reviewer to
    check against.

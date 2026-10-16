# Plan: the gear reward and the Reckoning

Status: rev 5, 2026-10-16; owner calls O1-O4 decided 2026-10-16. Plan only; nothing here is built. Rev 5 folds in
the senior adversarial review of rev 4
(`docs/PLAN_REWARD_SCREENS_SENIOR_REVIEW.md`, 3055bed1). Earlier: two review
rounds, each with one reviewer reading the code and one reading only the
plan, and a sourced research round (§2, §10).

**Reading of the ask.** "Gear reward screen" is the Reckoning's choice
phase (the gear page and the relic page, `ReckoningScreen.OfferPhase`);
"Reckoning screen" is its summary phase (`SummaryPhase`). Both live in one
overlay on the fight (`ReckoningScreen.cs:6-16`). This plan keeps the
overlay and reworks both acts. If the owner meant two separate screens, only
the act boundary (C7) moves.

**Governing rule: show, don't tell.** A fact the player can see on an object
(shape, light, motion, sound, a number next to a glyph) is never also
written as a sentence. Words survive only where no picture carries the fact:
an item's name, an affix's or relic's rules text, and the compare detail on
focus. No tutorial either: nothing plays "the first time" only. A picture
that is false is worse than a sentence, so every picture here draws a fact
where the code keeps it (honing on the slot, not the item: C6).

**What does not change.** The decision model: `RewardPages` (take, hone
instead, leave), `PendingPage`, the page roll on `RngStreams.Reward`, the
save after each action, the bot's `SettleRewardPages` / `BotRunDriver.Offer`
(`docs/PLAN_ITEMS.md` 1.2). No RNG draw, save field or content row changes.

**What changes below the screen (cross-layer, additive).** By the time the
Reckoning opens, `RunOrchestrator.SettleFight` has paid the map prizes and
`AdvanceLeg` may have moved the node (`RunOrchestrator.cs:624-651`,
`RunManager.cs:409-418`), and the request carries only notice strings about
them. R1 adds a read-only record of what was paid on `CombatReward` (C12), a
pure preview of what leaving a page pays, and a scoped honing preview at the
one seam that reads honing (C13). Notice strings stay for the bot and tests.

## 1. Audit: what the two screens tell today

Read from `Domain/UiKit/Screens/ReckoningScreen.cs`,
`Core/ReckoningController.cs`, `Core/RewardCardFace.cs`,
`Domain/UiKit/UiStrings.cs:584-718`, `Core/RealmPrizes.cs`.

**Choice phase (gear and relic pages)**

| Element | Today | Why it tells |
|---|---|---|
| Heading | "BOSS GEAR PAGE - 2 CHOICES LEFT" | Class and remaining choices as prose. The class is the fight the player chose on the map. |
| Favor line | "PRINCE'S FAVOR 14 - TIER CLIMB +8% - QUALITY CLIMB +8%" | A probability readout on a reward screen. |
| Meta line | "Rare - TIER 3" | Quality written although halo, burst and name colour carry it. |
| Recipient line | "FOR SHAWN - Weapon 1" | The art shows the slot; who wears it is a face, not a word. |
| Delta line | "+12 DMG", "NO STAT CHANGE" | The one-glance rank is a word-suffixed number with no up/down shape. |
| Feature line | strongest affix, 13 pt | Legitimate rules text; on every card at once the row reads as a text grid. |
| Loses line | "loses: Lifestealing Gale" | A loss as a sentence. |
| Action caption | "TAKE", then "TAKEN" / "HONED" / "SOLD +40" | The card is the button; outcomes are words, not motion. |
| Hone button | "HONE INSTEAD" / "SELL FOR 40" | What honing does to the worn item is named, never shown. |
| Ledger | "Now in your bag: X - Honed +1: Shawn Weapon 1 - Left unused: honed 2" | A paragraph narrating what the player just did. |
| Leave button | "LEAVE PAGE" | What leaving pays is hidden until after the press, then narrated. |
| Reveal | none | Cards are there when the wipe opens: no anticipation, no order, no sound per quality. |
| Sound | `TabSwipe` on tabs only | A Legendary sounds like a Common. |

**Summary phase (the Reckoning)**

| Element | Today | Why it tells |
|---|---|---|
| Title plaque | "THE RECKONING" / "VICTORY" / "ENDURED" / "FELL" | A word for something the player watched. |
| Tabs | SPOILS / RELICS / TALLY | Three lists behind word tabs: the summary is a menu. |
| Spoils | "+120 GOLD", "EXPERIENCE", "LEVEL 4 - UP!", "+85 EXP", "DID NOT FIGHT" | Bars fill (good) but every bar is captioned; no faces. |
| Relics tab | "HELD FOR THIS DESCENT" | The unchanged held list, after every fight. The RUN pane shows the same list (`RelicListPresenter` is shared). |
| Tally tab | "812 dealt (600 phys / 212 other) 140 taken 0 healed" at 14 pt | A spreadsheet line. |
| Notices | "+40 gold", a book's name, potions | Prizes the map showed as icons arrive as sentences (`RealmPrizes.Pay`). |
| Gained chips | "Gained" caption + event relic chips | Good object, captioned. |
| Continue | plate + an ember glow tuned to an arrow it no longer has (`ReckoningScreen.cs:441-448`) | Stale glow. |

**Stays:** the overlay over the live fight; the horizontal wipe; quality
colour on halo and burst; the two-tint experience fill; spent cards staying
on screen; the focus arrow as the only selector; no hover scaling
(`docs/DIRECTION_BIBLE.md` §6).

## 2. What the research says

Second research round (2026-10-16), sourced claims only. Every rev-3
"from memory" observation was either sourced below or dropped (§11). Where a
source is a fan write-up or commentary rather than the developer, it says
so. Search found no GDC talk about the moment of reveal itself; the closest
primary material is a postmortem on loot (Diablo III) and a peer-reviewed
study of rarity and arousal.

**Findings**

- **F1. Not knowing yet prolongs the pleasure.** People told they had won
  one of two prizes stayed in a good mood longer than people told which
  prize, or told they had won both; forecasters predicted the opposite
  (Kurtz, Wilson and Gilbert 2007, *J. Exp. Soc. Psych.* 43(6),
  https://dtg.sites.fas.harvard.edu/KURTZWILSON&GILBERT(2007).pdf). Applied
  to Destiny's engrams by Jamie Madigan
  (https://www.gamedeveloper.com/design/psychology-and-destiny-s-loot-system).
- **F2. Show the kind and the tier, hold back the identity.** Destiny
  engrams show rarity colour and item type at the drop, and the specific
  item only on decoding (https://www.destinypedia.com/Engram). Hearthstone
  deals five cards face-down; hovering one shows its rarity border before
  the flip, and the player flips each card
  (https://hearthstone.wiki.gg/wiki/Card_Pack). Genshin's wish shows the
  star's colour before the character (commentary,
  https://hostedgg.com/blog/gacha-pull-reveal-color-culture).
- **F3. Rarer rewards produce larger arousal and longer pauses after.**
  Rarer loot box items drew larger skin-conductance and press-force
  responses, longer post-reinforcement pauses, and a stronger urge to open
  more (Larche et al. 2021, *J. Gambling Studies* 37:141-163,
  https://link.springer.com/content/pdf/10.1007/s10899-019-09913-5.pdf;
  vicarious viewing; a 2024 study and Brady and Prentice 2021 found weaker
  or no effects:
  https://www.sciencedirect.com/science/article/pii/S2451958824001787).
  The pause is the player taking it in, so the rare moment should not be
  hurried away; the urge is the reason this screen never sells, re-rolls or
  fakes a near miss.
- **F4. A rare ceremony that runs a few seconds can be the best part.**
  Vampire Survivors' chest pauses play while rewards cycle and a gold
  counter ticks under rising music; three- and five-item jackpots are rare
  and louder; a player told it could be skipped asked why anyone would
  (https://mechanicsofmagic.com/2024/05/22/critical-play-vampire-survivors/).
  Galante built it around a slot game's chest jingle whose animation did
  not match its sound: "a super rewarding and over-the-top treasure chest
  animation that matches a catchy little sound"
  (https://www.androidpolice.com/vampire-survivors-developer-interview/;
  https://www.thexboxhub.com/the-treasure-chest-trick-behind-vampire-survivors/).
- **F5. A ceremony on every reward becomes a tax.** Players ask to skip or
  batch repeated reward animations (Rocket League,
  https://steamcommunity.com/app/252950/discussions/0/4521135641322138209/;
  Tiny Rogues, cute the first time and unwanted after every boss,
  https://steamcommunity.com/app/2088570/discussions/0/4030223299347013580;
  RPG victory screens, https://www.resetera.com/threads/victory-results-screens-in-rpgs-are-annoying-please-let-players-completely-disable-them-to-speed-things-up-devs.556687/).
  One project's spec answers with tap-anywhere-to-end-state for anything over
  320 ms and a Calm/Brisk speed toggle
  (https://github.com/jarrodjohnson-gif/emberisle/issues/539; a project
  document, not a study).
- **F6. Drop fewer, drop better; quality must mean power; compare at a
  glance.** Diablo III's postmortem (Mosqueira, GDC 2015, as reported by a
  fan write-up): launch players were confused when blues beat rares and
  rares beat legendaries; the fix was "drop fewer, drop better, make
  legendaries legendary", comparison arrows and highlighted legendary
  affixes, and a legendary pity timer; players optimise fast, so item
  evaluation has to be fast
  (https://www.purediablo.com/josh-mosqueira-diablo-3-presentation-gdc-2015).
- **F7. Too much loud loot drowns the signal.** Diablo IV legendaries drop
  "with a distinct sound and an orange glow"; once they are common, players
  call the clutter unacceptable without a filter
  (https://www.dexerto.com/diablo/diablo-4-legendary-loot-overload-2867981/).
- **F8. Declining is a first-class choice.** Slay the Spire's card reward
  offers a pick or Skip; elites skew rarer, bosses are all rare, and a
  rare-pity offset rises with each common
  (https://slay-the-spire.fandom.com/wiki/Card_Rewards). Balatro packs:
  "The player is not required to select an option, and can choose to skip at
  any point" (https://balatrowiki.org/w/Booster_Packs).
- **F9. Show the next reward before the player commits.** Hades puts a
  symbol over every door previewing the next room's reward
  (https://www.inverse.com/gaming/hades-symbol-meaning-guide-hera-dionysus-apollo-ares-aphrodite).
  Boon rarity is a colour tier, Common to Heroic
  (https://primagames.com/gaming/hades-guide-olympian-god-boons-skills/2).
- **F10. Scale the effect to the event; layer channels.** Nijman's "Art of
  Screenshake" (INDIGO 2013) and Jonasson and Purho's "Juice It or Lose It"
  (2012) stack small effects and keep them small for routine actions, large
  for real events; critics note heavy constant shake nauseates and hides
  the action (summaries:
  https://github.com/dr-jam/GameplayProgramming/blob/master/Juice.md,
  https://rpgplayground.com/research-making-a-juicy-game/). A Balatro
  analysis reads every scoring event as several channels at once
  (https://blakecrosley.com/guides/design/balatro; commentary).
- **F11. Pack reveals are storyboarded to hit beats.** Hearthstone's art
  director describes each pack's opening as storyboarded to land the beats
  the team wants (TouchArcade interview with Ben Thompson, 2017,
  https://toucharcade.com/2017/04/04/designing-hearthstone-card-packs-animations-iterations-ungoro-and-more-with-art-director-ben-thompson/;
  read via search excerpt, the page refused a direct fetch).

- **F12. Stop for big moments, and keep the effects moving.** Sakurai:
  both sides freeze for the same time, longer for stronger hits, up to a
  cap; slow motion is a separate tool; characters still move a hair during
  the freeze (Famitsu column, translated,
  https://sourcegaming.info/2015/11/11/thoughts-on-hitstop-sakurais-famitsu-column-vol-490-1/).
  His YouTube series opens with "Stop for Big Moments!", where he names the
  freeze on a boss's defeat a "boss stop" (Automaton report,
  https://automaton-media.com/en/news/20220824-15208/; secondary, the video
  itself was not fetched).
- **F13. Freeze on the frame that reads best; finishers get a slower
  beat.** Freeze the attack on its most representative frame; slow motion
  before a finisher makes it clearer (Prince of Persia: Warrior Within, God
  of War); a fight without the loser's reaction at the end feels less
  natural (Soul Calibur 2) (Jiesang Song, Game Developer, 2005,
  https://www.gamedeveloper.com/audio/improving-the-combat-impact-of-action-games).
- **F14. The win is a pose and a fanfare.** Final Fantasy has ended battles
  with a victory pose and a fanfare since the first game; from VII on each
  character has their own pose, and X added voiced lines
  (https://finalfantasy.fandom.com/wiki/Final_Fantasy_X_victory_poses,
  https://finalfantasy.fandom.com/wiki/Victory_Theme; fan wikis). The
  pacing complaint lives one step later: Darkest Dungeon players asked to
  speed up the loot list after a battle, and Red Hook made "Next" finish it
  (https://steamcommunity.com/app/262060/discussions/0/1843493219422589139/).

**Principles this plan uses, with their evidence**

1. **Tell the kind and the tier, hold back the identity** (F1, F2). The
   cache says "Rare or better inside", never which card.
2. **Rarity is a material, not a label** (F2, F9), and **quality must
   track power where the game controls it** (F6).
3. **Ceremony scales with rarity, and the floor is quiet** (F3, F7, F10).
4. **Let the rare moment breathe, but never gate the choice on it** (F3,
   F4, F5).
5. **Everything skips to its end state; speed is a setting** (F5).
6. **Compare at a glance against what you wear** (F6).
7. **Declining is visible and priced** (F8).
8. **The prize promised before the fight is the prize that arrives** (F9).
9. **No dark patterns**: nothing for sale, no re-roll, no fake near miss;
   the pages are rolled at settlement (F3).
10. **The win lands before the reward opens**: a harder stop on the
    final blow, the fall in view, the survivors' answer, the sting on the
    kill, all capped and skippable (F12-F14, C0).

**Project constraints over genre habit:** no hover scaling (so no hover
tilt); the focus arrow is the selector, a halo means quality only
(`ReckoningController.cs:802-804`); stills only; enemy art is flat cel with
bold outlines (Giant Rat sheet); every literal through `UiStrings`; scenes
generated; colour is never the only channel.

## 3. Behavioural contracts

Durations are authored at the default battle speed (1x). The whole reveal
is one pure Domain model, **`RevealSchedule`** (C10), which the controller
only plays back. "Wall time" counts from the moment the wipe starts.

**Definitions used below.**

- **Live upgrade**: a card that is not inert and is good news for its
  wearer: a positive headline delta (`GearEvaluator.LargestDelta`, C5), or,
  with no stat change, an affix or trait the worn copy lacks. A relic is a
  live upgrade when it reaches at least one member who does not already get
  its mechanic (`PendingCard.AddsNothingFor`).
- **Top tier**: gear Legendary; relic Mythic or Godlike.
- **Rare+**: gear Rare or Legendary; relic Rare, UltraRare, Mythic or
  Godlike.

### C0. The run-up: from the killing blow to the wipe

Today the fight ends in one frame: the last beat plays with its ordinary
hit-stop, the survivors' `victory` stance is already on it
(`FightSession.ResolveVictory` retro-attaches it, `FightSession.Riders.cs:277-296`),
and `OnPlaybackFinished` plays the sting, the voice lines and opens the
Reckoning together (`FightController.Input.cs:1478-1510`). The last enemy's
`StageDeathFade` (0.35 s hold, 0.6 s fade, `StageDeathFade.cs:27-28`) is
only waited for in the darkness ending (`FightController.Rounds.cs:299`).
C0 extends those pieces; it builds nothing parallel. Stills only: every
motion is a tween on an existing still or a procedural effect.

In order, for a won fight:

1. **The final blow stops harder** (F12, F13). The beat that ends the fight
   carries `EndsFight` (set by `FightSession` on that beat, presentation
   only, like the retro-attached stance). `FightBeatPlayer`'s existing
   hit-stop (`FightBeatPlayer.cs:1183-1184`, a coroutine wait on the actors,
   never global `Time.timeScale`, per the game-feel override in CLAUDE.md)
   uses `HitStop.FinalBlowSeconds(class)` instead of the blow's weight:
   normal 0.22 s, elite 0.30 s, boss 0.46 s (Sakurai's "boss stop"), held to
   0.9 s of wall time at slow presets (`RunUpSchedule.StopCeilingSeconds`),
   against today's per-blow cap of 0.18 s (`HitStop.cs:50`). The stage's
   ambient motion (motes, glows, the darkness) keeps moving through it (F12:
   effects move during a freeze). `StageShake` kicks on that contact at
   0.6 / 0.8 / 1.0 by class (F10). The beat has no settle and no gap: the
   last enemy's fade starts as the stop releases.
2. **The last enemy falls in view**: its existing `StageDeathFade` plays,
   and the run-up waits for it (the darkness ending's `AnyEnemyFading` wait,
   reused), within the cap.
3. **The survivors answer** (F14): their `victory` stills are already on
   the last beat; **the moment the stop releases** each survivor hops 24 px
   and comes down over 0.25 s, then squashes on the landing (y scale 0.94
   for 0.06 s, then back), staggered 0.08 s in squad order, through the
   existing `StageActorAnimator`. A survivor without a `victory` still keeps
   its idle, no hop.
4. **The light turns** (opinion; the direction bible's "clearing eases",
   [My read]), **also from the release, so nothing waits on the fade's
   hold**: a warm wash (`proc:radial_glow`, amber, 0 -> class alpha over
   0.5 s) rises behind the party and the enemy half of the stage dims 10%.
   The wash is alpha 0.15 at scale 1.0 for a normal fight, 0.22 at 1.25 for
   an elite, 0.32 at 1.6 for a boss. At the release the last enemy also
   sags before it fades (a squash and a tip about its feet over 0.18 s):
   y scale 0.94 and 2 degrees for a normal fight, 0.88 and 4 for an elite,
   0.62 and 10 for a boss, held until the fade ends. A boss also takes one more stage shake at the
   release (0.8), stage only.
5. **The sting lands on the kill** (F14): `VictorySting` moves from
   `OnPlaybackFinished` to the moment the last enemy's fade begins, after
   its hold, so the music marks the kill registering; the voice lines follow
   0.2 s later.
6. **The wipe** (C1) starts when the last fade completes or at the cap,
   whichever comes first. A fade still running at the cap finishes under the
   gloom.

**Cap and pace.** One pure model, `RunUpSchedule` (Domain, beside
`RevealSchedule`), returns the timed events; EditMode tests pin them on
literals. At pace 1 (the 1.5x preset) a normal fight's run-up, final
contact to wipe start, is hit-stop 0.22 + fade hold 0.35 + fade 0.6 =
1.17 s, an elite's 1.25 s, and a boss's 1.41 s is cut to the **cap of
1.3 s**, which holds at any preset. At 1x (pace 2/3) the stop and the fade
run 1.5 times longer and the cap decides for every class. Durations C0 adds go through `ReckoningTime`
(`min(1, Default/preset)`, C10). The death fade keeps the fight's own
`FightBeatPlayer.Scaled` pace; at 0.5x it would run longer, so the cap, not
the fade, decides when the wipe starts.

**Skip and input.** Any Submit, click or Cancel during the run-up jumps to
the wipe: fades complete, the hops settle, the sting keeps playing (audio
is never cut). The press is consumed and does not reach the Reckoning.
There is no other input to delay: the fight's verbs are hidden once it is
over (`FightController.Input.cs:1518-1524`). The worst case from final
contact to a choosable reward row is the run-up cap plus the reveal cap,
1.3 + 1.6 = 2.9 s at 1x, before any skip.

**Precedence.** The existing endings stay first and keep their timing: a
darkness ending (`BeginDarknessEnding`) and a leader-fall ending
(`BeginLeaderFallEnding`) run as today, then C0's steps 5-6. An Endured
fight (survived, no final blow) skips steps 1-3 and the sting; a Fell
outcome with the run going on skips C0 entirely. Event fights follow the
same rules with the normal-class final-blow stop.

### C1. Arrival

1. The run-up (C0) ends; the wipe begins.
2. The wipe opens (0.34 s). The frame's crest carries the **encounter
   medallion** of the room just cleared (the map's fight / elite / boss
   plate, `Art/Backgrounds/Processed/map_node_*.png`), from C12's recorded
   class. An event fight (`SettleEventFight`, no class) shows the plain
   fight medallion. No title word.
3. **Outcome form** (a run fight that paid nothing; O2 decided, §7):
   for its first 0.4 s the gloom over the stage stays low, so the fight
   behind the overlay shows the ending (standing enemies, an empty field, a
   downed party). Then: Fell = medallion cracked, a low note; Endured =
   medallion dimmed but whole, a held drone; a paid-nothing Victory =
   medallion plain, no note. No ending word. The board (C8) shows who fell.

### C2. The haul: the map's prizes arrive as the map's icons

Sequenced, not parallel: the haul plays **inside the wipe** (0.34 s) and,
on a cache page, the open wait. It is done before the first card moves.
Driven by C12's record, in pay order, each chip appearing as the chip the
map showed (`Shown`), then becoming what was paid (`Paid`):

- **Gold**: fight gold (`CombatReward.GoldGained`, recorded with
  `Shown = None`, so it simply pours in) and every gold chip, including a
  Gear/Relic chip past the page limit that `PayOverflow` paid as gold. Coins
  spill into the **gold stack** at the frame's top-right; the number counts
  from `GoldBefore` (C12) in at most 8 soft ticks, finishing inside the
  wipe. A gear or relic chip that became gold shows its own icon first and
  dissolves into coins, so a page lost to the limit is seen.
- **Exhausted pool** (`RealmPrize.Effective` turned a Book or Relic chip
  into gold, `RealmPrizes.cs:27`): the same dissolve.
- **Potions** (`ConsumablesFor(strength)` draws into
  `save.stockpiledItems`) and **books** (into `run.unassignedSpellBooks`, no
  owner yet): one flask per potion and the book's 48 px cover drop into the
  **haul strip** along the frame's foot. The strip is the one destination for
  everything the party now holds that is not equipped (A8.1).
- **Gear / Relic chip**: the chip the map showed flies to the row's centre
  and **becomes the page** (A2): it unfolds into the cache on a cache page,
  or straight into the dealt cards otherwise.
- A merchant's line ("No refunds.") stays text: a character speaking.
- No page at all: the haul plays into the board (C8) as above.

### C3. The page arrives

One page at a time, gear then relic.

- **Every page has a beat** (owner-session point a): as each card lands,
  one of the page's *k* choice embers lights above the row, with a soft
  breath, and every card whose face shows the green up-chevron (C5) pops
  it with a short rising note, one step higher per up-card in row order
  (A1). The note follows the face: an affix-only gain on a flat bar is
  silent.
  A downgrade lands silent. A page with nothing for anyone is quiet; a page
  with two upgrades sounds like good news before the cards are read. This
  adds no time: it rides the deal or the flips.
- **No cache** unless the page earns one. **Gear page:** it holds a live
  upgrade that is Rare or better and whose *quality* is higher than the
  item its wearer wears in that slot (an empty slot counts as lower than
  anything; a wrapped bundle for a Common or Uncommon would be a false
  promise). **Boss page:** always.
  **Relic page:** a Rare+ live relic (unchanged; A9 measured gear only).
  Such a page gets a cache (§5 A1) that the map's chip unfolds into, its
  seam leaking warm light, humming. The light does not say which card, nor
  Rare from top tier. A downgrade, an inert card or a live card no better
  than the worn one does not earn a cache, so the cache never promises a
  downgrade. Why: A9's sweep (`docs/REWARD_CACHE_RATES.md`) found the old
  "Rare+ live upgrade" rule on 79% of gear pages and the Legendary beat on
  32%, against a target of one page in five; comparing with the worn item
  thins the ceremony as the party gears up.
- **No cache**: the cards deal face-up into the row (0.25 s, left to right,
  a cloth rustle per card).
- The cache's size and binding follow the page's class
  (`PendingPage.SourceClass`, highest of its chips): normal, anchor, boss.
- The cache opens by itself 0.2 s after landing; any press opens it at once.
- **Choice embers**: *k* embers, one per choice; spending a choice snuffs
  one.

**How often the cache appears (re-derived, M1).** Odds per three-card
normal page, by quality alone (senior review from `LootLadder.Climb`; Favor
14 gives step 0.434, the cap 0.55): Rare+ on the page 0.47 (cap 0.66), top
tier 0.23 (cap 0.42). Those are **upper bounds** for the cache and the held
beat: keying on a live upgrade removes every Rare+ card that is a downgrade
or inert, and that share grows as the party gears up, so the ceremony thins
out by itself late in a run, with no counter. Pity and node floors push the
other way (`GearPage.cs:147, 236-240, 431-437`), which is why this is
measured, not argued: A9's bot-seed sweep reports cache rate and top-tier
beat rate per act, and the R4 gate holds the top-tier beat to at most one
gear page in five at mid-run Favor. Wall time is short at the upper bound
anyway: 0.53 x 0.59 + 0.24 x 1.15 + 0.23 x 1.40 = 0.91 s mean to choosable
at Favor 14, 1.06 s at the cap (C4 table). Fight 200 stays short because
the wait is short and the ceremony is rarer than the quality odds, not
because a counter shortens it.
First sweep (fails at 32% beat over all pages, 15% on Normal pages): `docs/REWARD_CACHE_RATES.md`.

### C4. The opening (cache pages only)

1. The seal snaps (0.12 s), a breath of light.
2. The *k* + 2 cards slide out face-down into the row (0.25 s).
3. Cards flip **left to right in row order**, 0.06 s apart, each flip
   0.12 s. A **Legendary live upgrade over a worn item below Legendary** holds a
   0.25 s silent beat before its own flip; the cards after it wait (a boss
   page keeps this rule; only its cache is unconditional; a relic: any
   top-tier live relic). Each flip plays the flip clip at its
   quality's pitch step (one clip, pitched per step, C11). Quality here is `ContentDatabase.QualityOf`, the one source the plinth and name colour read.
4. On landing: Uncommon a cloth puff; Rare a shimmer; top tier the burst at
   full size for 0.3 s, a 0.12 s screen-edge flare in its colour, a low
   bell. Common: a flat tap. A top-tier card that is not a live upgrade
   lands like a Rare: no hold, no flare; its plinth still says what it is.
5. **A Legendary live upgrade (same rule as the hold) lingers without blocking** (F3, F4): for 1.2 s
   after the row is choosable its halo stays at full strength, motes rise
   off its plinth and the bell decays. Input works throughout. A take, a
   hone, a leave or a Submit ends the linger; **moving focus does not**.
6. **Boss peak** (A5): a boss page whose top-tier card is a live upgrade
   adds one short musical sting under the bell. The only thing above the
   ordinary top-tier landing.
7. Focus: the first card in row order, or the card the player already
   moved to (C10). Nothing marks a "best" card.
8. **No near miss** (F3): a card never shows a higher quality than it has,
   not for a frame.

**Wall time to choosable, at 1x:**

| Page | Arithmetic | Total |
|---|---|---|
| No cache, any count | wipe 0.34 + deal 0.25 | 0.59 s |
| Cache, 3 cards (k = 1, the common case) | wipe 0.34 + open wait 0.2 + seal 0.12 + slide 0.25 + flips (2 x 0.06 + 0.12) 0.24 | 1.15 s |
| Cache, 3 cards, top-tier upgrade | the above + held beat 0.25 | 1.40 s |
| Cache, 5 cards, no top tier | wipe 0.34 + 0.2 + 0.12 + 0.25 + (4 x 0.06 + 0.12) 0.36 | 1.27 s |
| Cache, 5 cards, top-tier upgrade | the above + 0.25 | 1.52 s |

**Cap: 1.6 s** at every preset: the factor never exceeds 1 (C10), so a
preset never lengthens a page. That alone does not hold the cap for every
page: two top-tier live upgrades in 4 cards run 1.71 s at 1x, and 7 cards
with one hold run 1.64 s. `RevealSchedule` shortens a page that would pass
it in this order: the open wait (to 0), then the flip stagger (floor
0.02 s), then each hold (floor 0.10 s), then, past every floor, holds and
stagger to 0. The wipe, seal, slide and the flips never shorten. The two
pages above land at exactly 1.6 s.

### C5. The card face

Chromeless (no plate; `ReckoningScreen.cs:1006-1018`). **Row order** groups
cards by wearer in squad order, stable within a wearer (`GearPage` deals in
cycling squad order, so this is a real reorder). One pure `RowOrder`
permutation beside `OfferRowLayout` maps row position to page index; the
layout, the rail, the links, the flip order and `RevealSchedule` all iterate
it, while `offerButtons[]`, test names and `RewardPages` stay page-indexed.

| Part | Shows | Cards 1-3 | Cards 4-5 |
|---|---|---|---|
| Plinth | A still under the item. Its **outline** is the primary quality channel (four silhouettes, readable in greyscale); name colour and halo repeat it | yes | yes |
| Slot band | The plinth's front band. Left: the tier numeral. Right: the **slot's honing `+N`** for this wearer and slot (`ContentDatabase.HoningOf`), lit gold when above 0. It belongs to the slot, so it stays lit when the items swap | yes | yes |
| New item | The existing icon, large, in front; halo and slow burst behind (kept) | yes | yes |
| Worn item (ghost) | What the wearer has in that slot now, behind and to the left at 55% size and 45% alpha. No honing is drawn on it. Empty slot: no ghost. Gated in R2 (§6) before its size is fixed | yes | yes |
| Lost affix | A red minus badge on the ghost when the worn copy has an affix the new one lacks; name on focus | yes | yes |
| Wearer | Round portrait medallion at the top-left (`CharacterPortraits.For`; null -> the character's plate art silhouette). Identity only | yes | yes |
| Name | Item name in its quality colour, **without** today's `+N` suffix (`RewardCardFace.cs:53`): the band shows it | yes | truncated |
| Delta | The headline stat's glyph, a signed number, and a chevron shape: up (green), down (red), flat bar (grey). Shape carries the sign | yes | yes |
| Affix | One rules line with inline icons (`DescriptionMarkup`); the trait line on a Legendary | yes | focus only |
| Inert | Art tinted grey with a chain overlay | yes | yes |

**Headline stat rule** (today's): `GearEvaluator.LargestDelta`, the stat the
swap moves most relative to what the wearer has now
(`GearEvaluator.cs:178-212`). No stat moves but affixes change: the flat bar
with the affix's icon. Inert: no delta.

**Quality and delta can disagree, and both stay true** (F6): the plinth
says what the item is, the chevron what it does for this wearer. The cache
and the top-tier ceremony key on the chevron's side (C3, C4), so the
ceremony never celebrates a downgrade. How often the two disagree is the
item plan's tuning.

**Focus detail** (on a face-up card only): the shared tooltip
(`TooltipFocusRouter`) shows every stat that changes as glyph +
`now -> next`, every affix, the lost affix by name, an inert card's
requirement. Built by a shared compare builder extracted from the dossier's
`PaintDeltaRows` (`CharacterDossierController.Loadout.cs:939-960`, over
`ItemDescription.Compare`).

**Type floor**: card name and delta are new scoped rows in
`MenuTypeLintTests` (20 mixed / 24 caps), each scope with its own
`minimumNodes`, set when the tree exists. A 5-card row is ~192 px per card
(`OfferRowLayout.CardWidth`).

**Relic card and the relic mapping** (M6). Relics use `RelicRarity`'s six
tiers (`Domain/Content/RelicRarity.cs:15-23`):

| Relic rarity | Plinth | Flip pitch step | Counts as |
|---|---|---|---|
| Common | 1 flagstone | 1 | - |
| Uncommon | 2 cloth | 2 | - |
| Rare, UltraRare | 3 blue cloth | 3 | Rare+ |
| Mythic, Godlike | 4 gold cloth | 4 | Rare+, top tier |

On anchor weights `[100, 45, 18, 6, 2, 1]` (`rewards.json`) Rare+ is ~16% a
card, ~40% of three-card pages before the live-upgrade filter; top tier
~1.7% a card. A relic card shows plinth, relic art, name, rules text with
icons. On focus the party's portraits take **three states**: gains (colour),
reached but redundant (colour with a hollow ring), not reached (grey).

**After a take**, every open card for the same wearer and slot repaints
against the new worn item (ghost, delta, lost-affix badge, live-upgrade
status); the slot band does not change.

### C6. Choosing

All three actions go through **one gate** (B1, C10): `Act(kind, rowIndex)`.

- **Take** (Submit or click on an open, face-up card): the new item lifts
  off the plinth and flies to its wearer's medallion, then the medallion
  sends a copy of its glow back down onto the slot band, which stays lit:
  the honing came with the slot, not the item. Weight by quality: Common a
  soft clink; Uncommon a clink and a cloth puff; Rare a flare into the
  medallion and the delta number rises once beside it; top tier all of that
  and the medallion rings in the item's colour for 0.4 s. The take clip
  lands on the root note of the ladder the flips and chevrons climbed (A3),
  so choosing resolves the phrase. The medallion warms on every take (A4);
  a top-tier take plays the wearer's own bark only if a fitting
  `CharacterVoice` line exists (no new voice work in this plan). The plinth
  is left empty, its band still lit.
- **Displaced copy**: the ghost drifts off the plinth into the haul strip.
  If `EquipmentOps.Equip` displaced more than one copy (R3 checks;
  `PendingCard.Displaced` holds one today), each goes.
- **Hone instead** (whetstone glyph under the card, Down from the card): on
  focus, the card's **slot band** shows `+N -> +N+1` and the tooltip shows
  the worn item's stats `now -> next` under a scoped honing preview (C13).
  On press: a spark from the whetstone into the band, `+N` ticks up, the new
  item crumbles to dust on its plinth.
- **Every shown slot at `MaxPlus` (10)**: the choice pays the card's price
  (`HoneInsteadResult.Gold`): a coin stack with the amount in the
  whetstone's place; the coins fly to the gold stack on press.
- **Leave** (door glyph at the row's right end; Right from the last card or
  Down from the whetstones): its face shows what leaving pays now, from
  `RewardPages.PreviewLeave` (C13): a whetstone per slot it would hone and
  a coin stack for any remainder. On focus, **the slot band of every card
  whose wearer and slot it would hone** shows its waiting spark, whatever
  the card's outcome (a taken card's empty plinth keeps its band). Targets
  follow page order (today's rule, `RewardPages.cs:265`). On press the
  sparks and coins play; the remaining embers snuff; open cards dim.
- **Spent page**: every card stays visible: taken = empty plinth with a lit
  band, honed = dust, sold = coin mark, unspent = dimmed. The door becomes
  the forward arrow. A on it moves on.
- **Next page**: the relic chip becomes the relic page (C2, C3-C4).

### C7. To the Reckoning

After the last page, the row sweeps out left (`SweepToSummary`, 0.18 s).
No page: C8 directly.

### C8. The Reckoning: one board, no tabs

- **The party**: one portrait per member in squad order
  (`CharacterPortraits.For`, null -> plate silhouette), each with the
  experience ring's two-tint fill (violet before, amber this fight), a
  rising hum, 0.12 s stagger. **Three states** (M4):
  - fielded: full fill;
  - fell during the fight (`Ledger.TimesDowned > 0`): full fill, and a
    small cracked-heart mark on the portrait's rim;
  - not fielded (`IsDowned`): portrait tinted grey, the **half** gain they
    did get (`VictoryRewards.ExperienceFor`, rounded up) shown in the amber
    tint. Today's screen hides it (before = after,
    `ReckoningController.cs:587-613`, and `StartBars` skips the row); R5
    fixes that as a bug, pinned to a literal.
- **Level up**: the ring closes, the portrait flares amber, the level
  numeral ticks up with a bell, the ring refills with the remainder, and
  the badge takes the reward track's waiting pulse (`BeaconPulse`, A6): it
  shows something is waiting and claims nothing (collection stays manual).
- **Deeds** under each portrait, glyph + number: sword = dealt, cracked
  shield = taken, leaf = healed (when non-zero), skull count = felled. The
  portrait's focus tooltip adds the physical/other split and **shielded**
  (`CombatLedger.Shielded`), so a tank's fight is visible. No "who carried
  it" highlight: ranking by damage undervalues healers and tanks.
- **Gold stack and haul strip**: where C2 and C6 left them. The strip is
  this fight's haul (each taken item on a small plinth, relics, books,
  potions, hone sparks) and today's "Gained" event-relic chips
  (`pendingRelicReceipts`, acknowledged on leaving, unchanged). No caption.
- **Held relics**: not on the board (O4 decided: the RUN pane shows
  them).
- **Continue**: the gold forward-arrow plate, no word. The stale ember glow
  goes, and `ReckoningPhaseTests`' pin on `ReckoningContinueGlow` changes in
  the same commit.
- **Navigation**: entry is Continue. Up goes to the haul strip (a clamped
  rail, its chips' tooltips), Up again to the portraits (a rail, their
  tooltips). Down returns. Cancel = Continue.
- **Outcome form**: portraits and deeds only (from `Tally` and `Ledger`);
  no rings (no `CombatReward`), no gold, no haul.

### C9. Leaving

Continue or Cancel leaves (`Dismiss`, acknowledging displayed receipts,
`LeaveFight`). Cancel stays a no-op while a page has choices left.

### C10. Timing, skip, input

- **`RevealSchedule`** (Domain, pure; M9): `RevealSchedule.For(cards, row
  order, live-upgrade flags, page class, preset)` returns timed events:
  haul ticks, deal or seal, slide, flip *i* with its pitch step, hold, land,
  chevron pops, linger end, choosable. Skip returns the end state, and from
  the skip on `IsRunningAt`/`GuardsAt` read the skipped timeline: input is
  live over the face-up row, behind the same double-tap guard window. The cap,
  flip order, holds, pitch steps and skip are EditMode tests on literals;
  PlayMode only checks that the controller plays the schedule.
- **`ReckoningTime`** turns an authored duration into wall time:
  `authored x min(1, BattleSpeed.DefaultDisplay / preset.Display)`, times
  the test seam `ReckoningController.SpeedMultiplier`, reading the preset
  from `GameSettings` when the overlay opens. The Reckoning speeds up at
  1.5x and 2x and never slows below its authored times (M8, O3 decided), so the cap holds at every preset. Every wait and tween goes through
  it, including today's wipe, sweep and bar fills.
- **One gate** (B1): take, hone instead and leave all call `Act(kind,
  rowIndex)`. While `RevealSchedule` reports a beat running, `Act` completes
  the beat, nudges the pressed control 4 px, and returns without calling
  `RewardPages`. The whetstones and the door exist and are focusable during
  the reveal but cannot act through any input. The cache and a transparent
  full-frame catcher route through the same skip.
- **The cache is a real `Button` and the `NavContext` entry** from the frame
  it lands (`NavContext.LazyEntry` covers the wipe frames before it exists,
  `NavContext.cs:35`). The face-down cards are declared selectables from the
  slide onward, so a player who moves to one keeps it when the cards land
  (NavContext's remembered-or-entry rule, `NavContext.cs:231-250`).
- **Skip**: any Submit or click during a beat completes it (the haul lands,
  the cache opens, all cards flip at once; a top tier keeps only its 0.12 s
  flare). The press is consumed and nudges. A press when nothing runs acts
  at once.
- **Moving focus never skips** and never ends the linger. Face-down cards
  show no tooltip (`TooltipFocusRouter` suppressed during the reveal).
- **Guard**: for 0.15 s after the last card lands, `Act` consumes a Submit
  and nudges, so a held or repeated press cannot take an unseen card.
- **B** during a beat: nothing (today's `cancelLive: () => !_choosing`).

### C11. Sound

The palette is cloth, breath, wood, soft bells and low strings: tender, not
a casino. Coins tick softly; no cascade.

Pitch steps go through `SoundController.Play(Sound, pitch)`, a pool of up
to 8 pitched sources (ebcaa92f), so one clip per cue serves every step (the
flips, the chevron-note ladder, the take's root note). When the pool is
full, the oldest still-playing shot is cut, not round-robin. Placeholder
cues never borrow `ButtonClick` or `ClickFail` clips; R6's gate checks the
clip, not only the enum. The haul is still sequenced before the flips (C2),
so layers don't pile up.

**Every Reckoning control is `.Quiet()`** (cards, cache, catcher,
whetstones, door, Continue, haul chips): the offer cards today are
`.NoChrome()` but not quiet (`ReckoningScreen.cs:1020-1021`), so
`ButtonPressAnimator` would add a `ButtonClick` on every take, and a refused
press on a non-quiet button buzzes `ClickFail`. The controller owns every
sound. Gate: `ButtonCueTests` gains a Reckoning case (a quiet control's
refused Submit is silent, as `Submit_OnAQuietButton_IsSilent_EvenWhenRefused`
pins for the general case).

| Moment | Sound | Source |
|---|---|---|
| Card deal (no cache) | cloth rustle | new |
| Ember lights | breath | new |
| Up-chevron pop | rising note ladder, pre-pitched | new |
| Cache open | `OpenPack` if it fits the palette, else a cloth-and-knot untie | exists / new |
| Card flip | four pre-pitched clips | new |
| Top-tier landing | low bell | new |
| Boss peak | short musical sting | new |
| Coin count | soft tick | new |
| Potion / book land | glass clink / paper thump | new |
| Take | soft clink on the ladder's root | new |
| Hone | whetstone scrape | new |
| Experience fill / level up | hum / bell | new |
| Fell / Endured | low string note / drone | new |

R4 ships a **placeholder layer** (`OpenPack` and one flip clip at four
pitches rendered offline), so the reveal is never judged silent (M9).

### C12. The settlement record (on `CombatReward`, additive)

- A `PaidPrize` (Domain, `Domain/Rewards/PaidPrize.cs`): `Shown` kind (the
  map chip, `node.Prize`, or `None` for the fight's own gold), `Paid` kind
  (after `RealmPrize.Effective` and overflow), amount, item or book ids.
  A Gear or Relic chip records `Paid = Gear/Relic` with no amount, so the
  record is non-empty for an item-only chip.
- `CombatReward` (a mutable class, already carrying notices) gains
  `Prizes`, `EncounterClass` (nullable) and `GoldBefore`, filled in
  `SettleFight` where `room` and `chips` are built
  (`RunOrchestrator.cs:624-631`), before `AdvanceLeg` moves the node. The
  `FightSettlement` struct does not change, so its construction sites and
  `WithNotices` need nothing (M3).
- `RealmPrizes.Pay` keeps its signature and takes an optional
  `List<PaidPrize> record = null`; `SettleFight` threads one list through
  `Pay` and `PayHeartAnchors`.
- `ReckoningRequest.Full` reads the three off `Reward`. The outcome form
  needs none: nothing was paid, and its medallion comes from `Ending`.
- Bot neutrality: nothing is added to `RunSnapshot`, a save, or an RNG
  draw (`BotRunDriver.cs:835` is a comment). The test pins the same save
  JSON and notice list for one settlement before and after, and what a Gear
  chip records.

### C13. Two pure queries (Core, additive)

- `RewardPages.PreviewLeave(save, page)` returns the hones per (recipient,
  slot) and the gold that `Leave` would pay; `Leave` is rewritten to consume
  it, so the face and the payout cannot disagree (`RewardPages.cs:259-282`
  spills hones across slots, then pays gold).
- **`HoningPreview`** (M2): one scoped, disposable override that `HoningOf`
  consults first (`ContentDatabase.Gear.cs:32`), e.g.
  `using (HoningPreview.Add(characterId, slot, +1)) { ... Compare ... }`.
  Every reader on the stats path (`ActiveLoadout`, `EffectiveStats`, weapon
  power, `SimulateEquip`) then agrees with no new parameters. It never
  writes `slotHoning`. Tests: `slotHoning` unchanged after dispose, also when
  the body throws; nested previews stack.

## 4. Precedence and edge cases

First rule that applies wins.

1. **Outcome form**: C1.3, then C8's outcome board.
2. **No page**: C2, then C8.
3. **Shown again in the same session over a revealed page** (pages are
   memory only, `PendingPage` `[NonSerialized]`; a run on disk is settled,
   never resumed): the controller keeps a transient revealed set by page
   index. The page opens face-up in its current state: no cache, no flips,
   no flares, no sounds; the haul does not replay.
4. **Every shown slot at `MaxPlus`**: coin glyph (C6).
5. **One card's slot at `MaxPlus`**: no whetstone under it; Down goes to the
   door.
6. **Inert card**: chained; can be taken (today's rule); never a live
   upgrade.
7. **Several cards share one slot**: each shows the same slot band; a leave
   or hone spark lights every one of them.
8. **Bag**: no cap; the displaced copy always lands.
9. **Party of one or two**: medallions and board centre on who is fielded.
10. **Missing content**: name and art blank, Common plinth, still
    choosable, never a live upgrade.
11. **Gamepad, choice phase**: Left/Right along the row (`RowOrder`), Right
    from the last card to the door, Down to the whetstone, Down to the door,
    Up back. A acts through `Act`; B nothing while choices remain. No extra
    shortcut. The prompt footer shows glyphs only.
12. **Mouse/keyboard**: the catcher, the cache and every control skip during
    a beat through the one gate; arrow keys mirror the D-pad.
13. **Start / system menu**: the Reckoning absorbs Start today (open call in
    `AUDIT.md`). If a menu ever opens over it, `SettleImmediately` completes
    the running beat, the gold count-up and every running fill first, so
    nothing ticks underneath.
14. **Save** after each take, hone or leave (unchanged).

## 5. Art needs

**The prompts are in `docs/ART_PROMPTS_REWARD_SCREENS.md`**, one
self-contained block per sheet (style inlined, references to attach,
output filename, keying, and whether it can be generated now or waits for a
gate). This section only lists what exists and what is gated.

| Sheet | File | Status |
|---|---|---|
| A1 Caches | `reward_caches.png` | generate now |
| A2 Plinths | `reward_plinths.png` | **waits** for the R2 ghost gate (§6): a capture of the 5-card row with ghosts on placeholder plinths must read first |
| A3 Card back | `reward_card_back.png` | generate now |
| A4 Glyphs | `reward_glyphs.png` | generate now |
| A5 Chain overlay | `reward_chain.png` | generate now |

**No new art for C0 (the run-up)**: every real character already ships a
`victory` still (`Resources/Characters/{sheep,sheep_black_ram,owl,bear,bear_berserk}/victory.png`),
and the warm wash, flare and motes are procedural.

**Placeholders**: every phase ships and passes its gates before art lands:
`proc:radial_glow` discs for plinths and the cache, a plain violet card
back, `map_chip_*` and `Resources/Icons/Ability/*` icons for glyphs. The
ghost needs no new art (it is the worn item's own icon). Reused:
`element_physical.png` (damage), `defense_physical.png` (defence),
`resource_mana.png` (mana); the map's medallions and chips, which the
Reckoning gets as its own `[SerializeField]` sprite table in `WireReckoning`
(the Map scene bakes them by key today, `MapScreen.cs:38-42`).

**Ingest**: results go to `Art/UI/Reckoning/`, keyed into
`Art/UI/Reckoning/Processed/` with a keyer recipe (`docs/ART_PIPELINE.md`
§2-3); A2 and A4 contain green and are keyed `"magenta"`.

**Tone**: the caches are Prince's offerings: things he wraps for his souls
the way a cat brings a gift, proud and tender. Cloth, knots, worn tin,
stone. Not treasure chests, and no literal household objects (the direction
bible rules those out).

## 6. Phases and gates

Each phase is one commit at a test-passing checkpoint: `tools/
run_tests_parallel.ps1 -Changed -ChangedPaths <its paths>`, plus
`-BuildScenes` whenever the tree or a `[SerializeField]` changed
(`docs/TESTING.md`). **Seeing it**: the Reckoning has no static
`screenshot.ps1 -Panel` entry (`tools/screenshot.ps1:138-146`; AUDIT #43),
so visuals come from `ReckoningCaptureTests` under `-Runtime -RuntimeFilter
ReckoningCaptureTests` and GIFs from `tools/gif.ps1`, both of which the
owner allows per session. A phase without them reports "tests green, not
seen". Strings are deleted in the phase that stops using them, never
earlier, so every checkpoint renders.

| Phase | Content | Gate beyond the commit gate |
|---|---|---|
| R0 | Art prompts A1, A3, A4, A5 to the owner (A2 waits for R2); capture fixtures in `ReckoningCaptureTests`: no-cache page, cache page with a top-tier upgrade, 5-card row with ghosts, board, outcome board | Fixtures compile and run headless as no-ops without graphics |
| RU | The run-up (C0): `EndsFight` on the final beat, `HitStop.FinalBlowSeconds`, `RunUpSchedule`, the wait on the last fade, the lift, the warm wash, the sting moved; a fight-end capture fixture (one blow from victory) | EditMode on literals: `RunUpSchedule` order, per-class stop, cap at every preset, skip end state; `EndsFight` set on exactly one beat. PlayMode: the hit-stop never touches `Time.timeScale`; a press during the run-up is consumed and opens the Reckoning at its start; darkness and leader-fall endings unchanged. **Feel gate (owner stop/go):** a GIF of the fight-end fixture (`gif.ps1` has spell, attack, relic and status modes today, so RU adds a fight-end mode beside them) for a normal, an elite and a boss fight, watched before a verdict is relayed. Independent of R1-R7: it may land at any point after R0 |
| R1 | C12 record on `CombatReward`; C13 `PreviewLeave` and `HoningPreview`; the shared compare builder; the live-upgrade predicate | Bot-neutrality test; a Gear chip's record pinned; `Leave` consumes `PreviewLeave` with today's results pinned; `HoningPreview` dispose and nesting; dossier compare tests unchanged; live-upgrade predicate on literals (positive delta, affix gain, inert, relic reach) |
| R2 | Card face (C5) with placeholders, beside the old labels, then the old labels go; `RowOrder`; slot band; name loses `+N` | Tree audit at four aspects; face test per quality, count (3 and 5), wearer, ghost, empty slot, inert, relic mapping; `RowOrder` test; Right from a card goes to the next card in row order, Right from the last to the door; `MenuTypeLintTests` scopes. **Ghost gate (owner stop/go):** the 5-card capture with ghosts must read before A2 is commissioned |
| R3 | Choice actions (C6) through `Act`: take, displaced, hone with preview, coin case, leave preview and payout, spent states, repaint after take; heading, Favor line, ledger and action strings removed; every control `.Quiet()` | PlayMode: take, hone, sell and leave drive the real `RewardPages` and leave the same save as before (pinned literals); a second card for the same slot repaints; leave sparks land on a taken card's band; `ButtonCueTests` Reckoning case |
| R4 | `RevealSchedule`, all of `ReckoningTime`, arrival and reveal (C3, C4, C10), the haul (C2) into the row, every-page beat, placeholder sound layer | EditMode on literals: schedule order, holds only for top-tier live upgrades, pitch steps, skip end state, cap at every preset (`min(1, …)`). PlayMode: the controller plays the schedule; a press during a beat on a card, a whetstone and the door, by mouse and by Submit, never calls `Take`, `HoneInstead` or `Leave`; focus on a face-down card shows no tooltip; guard swallows a repeat. A9 bot-seed sweep: top-tier beat on at most one gear page in five at mid-run Favor. **Feel gate (owner stop/go):** `gif.ps1` captures of the three R0 reveal fixtures, each watched before a verdict is relayed; R5 waits for the owner's yes |
| R5 | Board (C8); tabs, RELICS and TALLY pages removed; half-gain bug fixed | **First, alone:** the board navigation graph as an EditMode test against `RuntimeNavWiring` groups and links, committed red-to-green before the tabs go. Then PlayMode: gold stack from `GoldBefore` to a pinned literal; potions, book, overflow and exhausted-pool dissolve; the three experience states with the not-fielded half pinned to a literal; level-up pulse; outcome board. `ReckoningGamepadNavigationTests`' choice-phase cases stay untouched; `ReckoningPhaseTests`' Continue-glow pin changes in this commit |
| R6 | The full sound set (C11) replacing placeholders | Every C11 moment fires once in a PlayMode run of each fixture; no `ButtonClick` or `ClickFail` from a Reckoning control |
| R7 | Art swap as each sheet arrives | `Art/` diff reviewed after `-BuildScenes` (`LoadSprite` flips importer settings) |

Bot: R1 is the only phase near settlement, pinned neutral; RU changes only presentation timing after the session has ended, so it moves no bot number either; `tools/bot.ps1`
numbers must not move. A9's frequency report (cache rate, top-tier beat
rate, mean wall time to choosable, per act) is kept beside the plan and
re-run whenever the item plan retunes the ladder; it is a check, not a
commit gate.

## 7. Owner decisions

No owner calls are open. The four below were confirmed by the owner on
2026-10-16, as written.

1. **Favor: decided 2026-10-16.** Drop the Favor line from
   the reward screen; add a squad-Favor row to the RUN pane fed from
   `ItemOfferRoll.CurrentSquadFavor()` (`ItemOfferRoll.cs:118-133`; today
   the pane lists only the event buff, `Domain/UiKit/RunEffectRows.cs:52`).
   Favor is a stat the player builds, so it stays visible; the odds stay off
   the reward screen. `PLAN_ITEMS.md` 1.2 had the page header show it.
2. **Ending words: decided 2026-10-16.** Drop VICTORY,
   ENDURED and FELL. Fell = cracked medallion; Endured = dimmed but whole,
   which differs from a paid-nothing Victory's plain medallion; the stage
   behind the overlay shows the ending for its first 0.4 s (C1.3).
3. **Battle speed: decided 2026-10-16.** Follow faster
   presets only: `min(1, DefaultDisplay / preset.Display)`. A 2x player
   gets a shorter Reckoning; a 0.5x player chose a slower fight, not a
   longer reward screen; the 1.6 s cap holds at every preset.
4. **Held relics and tally: decided 2026-10-16.** Both off
   the board: held relics live on the RUN pane; the tally becomes deeds
   under each portrait, with the physical/other split and shielded in the
   portrait tooltip.

Decided here, not asked: Prince's-offering caches (§5 tone); the cache
opens itself; a cache only for a Rare+ live upgrade; detail in a focus
tooltip; no pad shortcut to the door; sound sourcing follows the sound pass
(owner clips first) as a dependency of R6.

## 8. Rejected alternatives and review dispositions

**Rejected alternatives**

- **Slot-machine reel** (Vampire Survivors): its players love it (F4), but
  a reel suits a haul you keep whole; this screen asks for a choice between
  k + 2 readable cards. The top-tier linger (C4.5) takes the part that
  works.
- **Player-driven flips** (Hearthstone, F2): one more press on every page of
  every fight (F5).
- **Drops on the fight stage** (Diablo beams): stage-layer work in
  `FightController`; the chip becoming the page (A2) and the stage as
  witness (A7) take the useful part.
- **Card frames per quality**: the plate behind cards was removed on
  purpose (`ReckoningScreen.cs:1006-1018`).
- **Hover lift / tilt**: banned by the no-hover-scale rule.
- **A first-time teaching ghost** for hone and leave: the no-handholding
  rule; the always-on focus previews (band shows `+N -> +N+1`, sparks wait
  on the target bands) do it.
- **Collapse the ceremony after N fights**: no hidden counter. The reason is
  now M1's, not "the cap": the ceremony keys on a visible live upgrade, so
  it thins out as the party gears up, and A9 measures that it does.
- **Focus on the best card**; **flips in ascending quality**; **the worn
  item as a medallion-rim chip**; **household keepsakes as plinths**;
  **a whetstone with a coin badge at `MaxPlus`**; **dropping the haul**: as
  rev 3 recorded.
- **Honing drawn on the item's ghost** (rev 3-4): false; honing belongs to
  the slot (B2).
- **"Who carried it" gold rim** (rev 3-4): ranking by damage undervalues
  healers and tanks (owner-session point c).
- **A pitch overload on the one `AudioSource`** (rev 3-4): it retunes every
  clip still ringing (M5); pre-pitched clips instead.

**Senior review dispositions (3055bed1).** Blockers and majors are all
taken: B1 -> C6/C10 `Act`; B2 -> C5 slot band, C6; M1 -> C3 live-upgrade
cache and the re-derived argument; M2 -> C13 `HoningPreview`; M3 -> C12 on
`CombatReward`; M4 -> C8 three states and the half-gain bug; M5 -> C11
pre-pitched clips and `.Quiet()`; M6 -> C5 relic mapping and three focus
states; M7 -> C2 sequenced, A2; M8 -> C10 `min(1, …)`; M9 -> C10
`RevealSchedule`, R4 owns `ReckoningTime`, the GIF feel gate, placeholder
sound; M10 -> R5's nav-graph-first commit and the untouched choice-phase
tests.

Additions:

- **A1** taken (C3, every-page beat).
- **A2** taken (C2, C3).
- **A3** taken (C6).
- **A4** taken in part: the medallion warms on every take; a bark only if a
  fitting `CharacterVoice` line exists, since new voice work is the sound
  pass's open item, not this plan's.
- **A5** taken (C4.6).
- **A6** taken (C8).
- **A7** taken (C1.3).
- **A8.1** taken now (one haul strip, C2). **A8.2 to A8.4** are kept as the
  cut list, ranked, if scope has to give: two caches with class by binding
  only; drop the outcome medallion states; halve the clips.
- **A9** taken (§6).

Minor items:

- **1** taken (C4 table row).
- **2** taken (C4.5, C10).
- **3** taken (C10).
- **4** taken (C6, §4.11).
- **5** taken (C5 name).
- **6** taken (C12 `Shown = None`).
- **7** taken (C12).
- **8** kept as is: the class shows twice, which is cheap and harmless; its
  cut is A8.2.
- **9** taken (§4.13).
- **10** taken (§9).

One correction to the review: the test it names,
`EveryButtonInTheScene_HasAClickSound`, does not exist in the tree; the
quiet-button behaviour is pinned in `Tests/PlayMode/Ui/ButtonCueTests.cs`,
which R3 extends instead.

## 9. Files (last)

Domain: `Domain/Rewards/ReckoningRequest.cs`, `Domain/Rewards/
CombatReward.cs` (prizes, class, gold before), a new
`Domain/Rewards/PaidPrize.cs`, a new `RevealSchedule` beside
`Domain/UiKit/OfferRowLayout.cs` with `RowOrder`,
`Domain/UiKit/Screens/ReckoningScreen.cs` (cache, embers, plinths, slot
bands, ghosts, medallions, whetstones, door, gold stack, haul strip,
catcher; every control `.Quiet()`; board replaces tabs),
`Domain/UiKit/UiStrings.cs` (Reckoning block shrinks),
`Domain/UiKit/RunEffectRows.cs` (O1); for C0: `Domain/Combat/Session/HitStop.cs` (`FinalBlowSeconds`), `CombatBeat` (`EndsFight`), `FightSession.Riders.cs` (sets it), a new `RunUpSchedule`, and `FightScreen.cs` (the warm wash node). Core: `Core/RealmPrizes.cs`,
`Core/Bot/RunOrchestrator.cs` (`SettleFight` fills the record),
`Core/RewardPages.cs` (`PreviewLeave`),
`Core/Content/ContentDatabase.Gear.cs` (`HoningOf` consults
`HoningPreview`) and a new `Core/Content/HoningPreview.cs`,
`Core/ReckoningController.cs` (split `.Reveal`, `.Board`; `Act`),
`Core/RewardCardFace.cs` (parts, not sentences; no `+N` suffix), a shared
compare builder beside `Core/ItemDescription.cs`, `Core/ReckoningTime.cs`,
`Core/Sound.cs` (new cues, pre-pitched clips), `Core/ButtonPressAnimator.cs`
(read only: the quiet path the controls rely on), and for C0
`Core/FightBeatPlayer.cs` (the final stop), `Core/FightController.Input.cs`
(`OnPlaybackFinished`: sting timing, the run-up wait) and
`Core/FightController.Rounds.cs` (the fade wait, reused). Editor:
`ScreenRegistry.WireReckoning` (map art table). Tests: every
`Reckoning*Tests` class (Screen, Tab, Phase, Comparison, Lifecycle, Capture,
Chip and Gamepad navigation), a new board nav-graph EditMode test,
`OfferRowLayoutTests`, `MenuTypeLintTests`, `ButtonCueTests`,
`HeartAnchorSettlementTests` and `RealmRunTests` (callers of `Pay`). Tools:
the A9 sweep script beside `tools/bot.ps1`. Docs: `docs/CODE_MAP.md` rows
28, 78, 81, 82, 83, 85; `docs/ART_PROMPTS_REWARD_SCREENS.md` (the prompts).

## 10. Revision log

- rev 1: first draft.
- rev 2, after round 1 (reviewer A read the code, B only the plan):
  cross-layer settlement record; one-bit cache tell; cache auto-opens; haul
  concurrent; the take carries the weight; per-count card layout; shared
  compare builder; focus not on "best"; hone preview on focus; skip through
  a catcher and a flag; one timing helper with a cap; real prize
  destinations; magenta keying; additive phase order.
- rev 3, after round 2 (C read the code, D only the plan): no cache below
  Rare; flips in row order; wall-time arithmetic; the worn item as a ghost;
  headline-stat rule; `PreviewLeave` and a honing override; shown vs paid
  chip; `GoldBefore`; `RowOrder`; the cache as nav entry; consumed presses
  nudge; no tooltip on face-down cards; repaint after take; board
  navigation; placeholder policy; sound palette; owner calls rewritten.
- rev 4, sourced research round: §2 as findings F1-F11 with sources; §11
  lists dropped unsourced observations; Legendary linger, no near miss,
  quality-versus-delta note, reel rejection corrected, player-driven flips
  rejected.
- rev 5, senior adversarial review (3055bed1) plus the session's three
  points: one `Act` gate (B1); honing on the slot band, not the ghost (B2);
  cache and top-tier beat keyed on a live upgrade, fight-200 argument
  re-derived from the odds with a bot-seed gate (M1); `HoningPreview` at
  `HoningOf` (M2); the record on `CombatReward` (M3); three experience states
  and the hidden half-gain (M4); pre-pitched clips and `.Quiet()` controls
  (M5); relic mapping and three focus states (M6); haul sequenced, chip
  becomes the page (M7, A2); faster presets only (M8); `RevealSchedule`,
  `ReckoningTime` in R4, GIF feel gate, placeholder sound (M9); nav graph
  test first (M10); every-page beat (A1, point a); ghost gate before plinth
  art (point b); "who carried it" cut (point c); shielded in the tooltip;
  A3-A9 and minors per §8; owner calls written as provisional decisions,
  then confirmed by the owner on 2026-10-16 (§7).
  No new review round: every structural change here is one the senior
  asked for; the every-page beat and the ghost gate add no contract.
  Then, at the owner's request: C0, the run-up from the killing blow to the
  wipe, extending today's hit-stop, death fade, victory stance and sting
  (F12-F14), with its own cap, skip and phase RU; §5 slimmed to a status
  table, the hand-off-ready prompts moved to
  `docs/ART_PROMPTS_REWARD_SCREENS.md`. C0 is a new contract that no
  reviewer has seen; RU's feel gate is its check.

## 11. Research notes, per game

Sourced in §2 (finding numbers). Rev-3 observations that found no source
are listed as dropped; none of them is load-bearing in the plan any more.

| Game | What the source shows | Finding |
|---|---|---|
| Destiny | Rarity colour and item type at the drop, identity on decoding; the wait argued to raise satisfaction | F1, F2 |
| Hearthstone | Five face-down cards, rarity border on hover before each player-driven flip; openings storyboarded to beats | F2, F11 |
| Genshin / gacha | Star colour before the character (commentary) | F2 |
| Vampire Survivors | A few seconds of cycling rewards, ticking gold and building music; rare jackpots louder; a player calls it the best part | F4 |
| Diablo III | Drop fewer, drop better; quality must mean power; comparison arrows; pity timer | F6 |
| Diablo IV | Distinct sound and orange glow for legendaries; overload complaint | F7 |
| Slay the Spire | Pick or Skip; rarer by fight class; rare pity offset | F8 |
| Balatro | Pick from 3-5 or skip at any point; layered feedback (commentary) | F8, F10 |
| Hades | Door symbols preview the next reward; colour tiers for boon rarity | F9 |
| Rocket League, Tiny Rogues, RPG victory screens | Repeated reward animations draw skip requests | F5 |

**Dropped, unsourced:** Hades "three cards with one changed number" and
"instant once chosen"; Balatro editions as a card shader and its pack
fan-out; Path of Exile filter beams; Hearthstone "open all"; gacha skip
placement; Borderlands chest beams and compare arrows (replaced by Diablo
III's sourced comparison arrows, F6); Dead Cells; Monster Train's reward
screen (its postmortem slides and interviews cover pick-rate analytics,
not presentation); Inscryption's table; Darkest Dungeon's loot grid;
Peglin (only reviews and fan guides found).

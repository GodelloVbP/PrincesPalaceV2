# Plan: the gear reward and the Reckoning

Status: rev 4 (rev 3 plus a sourced research round), 2026-10-16. Plan only; nothing here
is built. Two review rounds, each with one reviewer reading the code and one
reading only the plan (§10).

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
focus. No tutorial either: nothing plays "the first time" only.

**What does not change.** The decision model: `RewardPages` (take, hone
instead, leave), `PendingPage`, the page roll on `RngStreams.Reward`, the
save after each action, the bot's `SettleRewardPages` / `BotRunDriver.Offer`
(`docs/PLAN_ITEMS.md` 1.2). No RNG draw, save field or content row changes.

**What changes below the screen (cross-layer, additive).** By the time the
Reckoning opens, `RunOrchestrator.SettleFight` has paid the map prizes and
cleared the room (`RunOrchestrator.cs:624-651`), and `ReckoningRequest`
carries only notice strings about them. R1 adds a read-only record of what
was paid (C12), a pure preview of what leaving a page pays, and a stats
query at a hypothetical honing level (C13). Notice strings stay for the bot
and the tests.

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

**Project constraints over genre habit:** no hover scaling (so no hover
tilt); the focus arrow is the selector, a halo means quality only
(`ReckoningController.cs:802-804`); stills only; enemy art is flat cel with
bold outlines (Giant Rat sheet); every literal through `UiStrings`; scenes
generated; colour is never the only channel.

## 3. Behavioural contracts

Durations are authored at the default battle speed (1x) and all go through
one helper (C10). "Wall time" below counts from the moment the wipe starts.

### C1. Arrival

1. The last blow; the victory sting and voice lines (unchanged,
   `FightController.Input.cs:1490-1500`).
2. The wipe opens (0.34 s). The frame's crest carries the **encounter
   medallion** of the room just cleared (the map's fight / elite / boss
   plate, `Art/Backgrounds/Processed/map_node_*.png`), from C12's recorded
   class. An event fight (`SettleEventFight`, no class) shows the plain
   fight medallion. No title word.
3. **Outcome form** (a run fight that paid nothing): Fell = medallion
   cracked and dimmed, a low note; Endured = medallion plain, a held drone;
   a paid-nothing Victory = plain, no note. The board (C8) shows who fell.
   Endured and a paid-nothing Victory look alike without a word: owner call
   O2.

### C2. The haul: the map's prizes arrive as the map's icons

Runs **alongside** C3/C4, never before the cards. Driven by C12's record, in
pay order, 0.08 s apart, each chip appearing as **the chip the map showed**
(`Shown`) and then becoming what was paid (`Paid`):

- **Gold** (fight gold, gold chips, and a Gear/Relic chip past the page
  limit that `PayOverflow` paid as gold): coins spill into the **gold
  stack** at the frame's top-right; the number counts from `GoldBefore`
  (C12) up with a soft tick (at most 12 ticks). A gear or relic chip that
  became gold shows its own icon first and **dissolves into coins**, so a
  page the player lost to the limit is seen, not hidden.
- **Exhausted pool** (`RealmPrize.Effective` turned a Book or Relic chip
  into gold, `RealmPrizes.cs:27`): same dissolve into coins.
- **Potions** (`ConsumablesFor(strength)` draws into
  `save.stockpiledItems`): one flask per potion flies to the **satchel** at
  the frame's bottom-left and clinks in.
- **Book** (into `run.unassignedSpellBooks`, no owner yet): the book's 48 px
  cover (the existing spell-book icon bake) drops onto a **book pile**
  beside the satchel, not onto a portrait: nobody owns it yet.
- **Gear / Relic chips**: fly into their page's slot in the row (C3).
- A merchant's line ("No refunds.") stays text: a character speaking.

### C3. The page arrives: one tell

One page at a time, gear then relic. Whether a page gets a **cache** is the
tell:

- **Common/Uncommon-best page**: no cache. The cards deal face-up into the
  row (0.25 s for the whole row, left to right, a cloth-rustle per card).
  Nothing is held back because nothing is worth holding back.
- **Rare-or-better page**: a **cache** (§5 A1) drops into the row's centre
  as the wipe finishes, its seam leaking warm light, humming. The light does
  not distinguish Rare from Legendary. The cache's size and binding follow
  the page's class (`PendingPage.SourceClass`, the highest of its chips):
  normal, anchor, boss.
- **Choice embers**: *k* embers above the row (or the cache), one per
  choice. Spending a choice snuffs one.
- The cache opens by itself 0.2 s after landing; any press opens it at once.

### C4. The opening (cache pages only)

1. The seal snaps (0.12 s), a breath of light.
2. The *k* + 2 cards slide out face-down into the row (0.25 s total).
3. Cards flip **left to right in row order**, 0.06 s apart, each flip
   0.12 s. A Legendary card holds a 0.25 s silent beat before its own flip;
   the cards after it wait. A Rare card shimmers once on landing. The flip
   clip rises a step in pitch per quality.
4. On landing: Uncommon a cloth puff; Rare a shimmer; Legendary the burst
   at full size for 0.3 s, a 0.12 s screen-edge flare in its colour, a low
   bell. Common: a flat tap.
   **A Legendary lingers without blocking** (F3, F4): for 1.2 s after the
   row is choosable its halo stays at full strength, warm motes rise off its
   plinth and the bell decays. Input works throughout; a take or a press
   ends the linger at once. The rare moment gets its breath; the choice
   never waits for it.
5. Focus goes to the first card in row order. Nothing marks a "best" card.
6. **No near miss** (F3): a card never shows a higher quality than it has,
   not for a frame. The flip shows the card's own quality on the way over.

**Wall time to choosable, at 1x:**

| Page | Arithmetic | Total |
|---|---|---|
| No cache, any count | wipe 0.34 + deal 0.25 | 0.59 s |
| Cache, 5 cards, no Legendary | wipe 0.34 + open wait 0.2 + seal 0.12 + slide 0.25 + flips (4 x 0.06 + 0.12) 0.36 | 1.27 s |
| Cache, 5 cards, one Legendary | the above + held beat 0.25 | 1.52 s |

**Cap: 1.6 s wall time** at any preset. A slower preset shortens the held
beat and the open wait first, then the flips.

### C5. The card face

Chromeless (no plate; `ReckoningScreen.cs:1006-1018`). **Row order** groups
cards by wearer in squad order, stable within a wearer (`GearPage` deals in
cycling squad order, so this is a real reorder). One pure `RowOrder`
permutation beside `OfferRowLayout` maps row position to page index; the
layout, the rail, the Down links and the flip order all iterate it, while
`offerButtons[]`, test names and `RewardPages` stay page-indexed.

| Part | Shows | Cards 1-3 | Cards 4-5 |
|---|---|---|---|
| Plinth | A still under the item. Its **outline** is the primary quality channel (four distinct silhouettes, readable in greyscale); name colour and halo repeat it | yes | yes |
| Tier | Numeral engraved on the plinth's front band | yes | yes |
| New item | The existing icon, large, in front; halo and slow burst behind (kept) | yes | yes |
| **Worn item (ghost)** | The item the wearer has in that slot, drawn behind and to the left of the new one at 55% size and 45% alpha, on the same plinth. Empty slot: no ghost. The ghost is what "versus what you wear" looks like | yes | yes |
| Lost affix | A red minus badge on the ghost when the worn copy has an affix the new one lacks; name on focus | yes | yes |
| Wearer | A round portrait medallion at the card's top-left (`CharacterPortraits.For`; null -> the character's plate art silhouette). Identity only | yes | yes |
| Name | Item name in its quality colour | yes | truncated |
| Delta | The **headline stat** (below): its glyph, a signed number, and a **chevron shape**: up (green), down (red), flat bar (grey). Shape carries the sign; colour repeats it | yes | yes |
| Affix | One rules line with inline icons (`DescriptionMarkup`); the trait line on a Legendary | yes | focus only |
| Inert | Art tinted grey with a chain overlay | yes | yes |

**Headline stat rule** (today's, kept): `GearEvaluator.LargestDelta`, the
stat the swap moves most *relative to what the wearer has now* (a +6 speed
on a 40-speed member beats +9 health on 400; `GearEvaluator.cs:178-212`).
No stat moves but affixes change: the flat bar with the affix's icon. Inert:
no delta.

**Quality and delta can disagree, and both stay true** (F6): a Common card
can out-delta a Legendary for its wearer. Diablo III found blues beating
rares confused players. This screen shows the plinth (what the item is) and
the chevron (what it does for this wearer) side by side and never dims a
high-quality card for a low delta; how often the two disagree is the item
plan's tuning, not this screen's.

**Focus detail** (arrow on the card, or pointer on it, only once the card
is face-up): the shared tooltip (`TooltipFocusRouter`) shows every stat that
changes as glyph + `now -> next`, every affix, the lost affix by name, an
inert card's requirement. Built by a shared compare builder extracted from
the dossier's `PaintDeltaRows` (`CharacterDossierController.Loadout.cs:939-960`,
over `ItemDescription.Compare`).

**Type floor**: card name and delta are new scoped rows in
`MenuTypeLintTests` (20 mixed / 24 caps), each scope with its own
`minimumNodes`, set when the tree exists. At five cards a card is ~192 px
(`OfferRowLayout.CardWidth`), which is why the affix leaves the card there.

**Relic card**: plinth, relic art, name, rules text with icons. On focus,
the party's portraits show the members it reaches in colour and the rest
greyed.

**After a take**, every open card for the same wearer and slot repaints
against the new worn item (ghost, delta, lost-affix badge, hone preview).

### C6. Choosing

- **Take** (Submit or click on an open, face-up card): the new item lifts
  off the plinth and flies to its wearer's medallion. Weight by quality:
  Common a soft clink; Uncommon a clink and a cloth puff; Rare a flare into
  the medallion and the delta number rises once beside it; Legendary all of
  that and the medallion rings in the item's colour for 0.4 s. The plinth is
  left empty.
- **Displaced copy**: the ghost drifts off the plinth and into the **bag**
  at the frame's bottom-right. If `EquipmentOps.Equip` displaced more than
  one copy (R3 checks; `PendingCard.Displaced` holds one today), each goes.
- **Hone instead** (whetstone glyph under the card, Down from the card): the
  glyph carries `+N` with a small `+N+1`. **On focus the ghost lifts and the
  tooltip shows the worn item's stats `now -> next` at +1** (C13). On press:
  a spark from the whetstone into the ghost, `+N` ticks up, the new item
  crumbles to dust on its plinth.
- **Every shown slot at `MaxPlus` (10)**: the choice pays the card's price
  (`HoneInsteadResult.Gold`): a coin stack with the amount in the
  whetstone's place; a different action wears a different glyph. On press
  the coins fly to the gold stack.
- **Leave** (door glyph at the row's right end, Down from the whetstones):
  its face shows what leaving pays now, from `RewardPages.PreviewLeave`
  (C13): a whetstone per slot it would hone, a coin stack for any
  remainder. **On focus**, each slot it would hone lights on its card's
  ghost (sparks waiting), so the player sees exactly which worn items gain.
  The targets follow page order, not row order (today's rule). On press the
  sparks and coins play; the remaining embers snuff; open cards dim.
- **Spent page**: every card stays visible: taken = empty plinth, honed =
  dust, sold = coin mark, unspent = dimmed. The door becomes the forward
  arrow. A on it moves on.
- **Next page**: the relic page arrives (C3-C4).

### C7. To the Reckoning

After the last page, the row sweeps out left (`SweepToSummary`, 0.18 s).
No page: C8 directly, with C2 still landing into it.

### C8. The Reckoning: one board, no tabs

- **The party**: one portrait per fielded member, squad order
  (`CharacterPortraits.For`, null -> plate silhouette). Under each, the
  experience ring with the two-tint fill (violet before, amber this fight),
  a rising hum, 0.12 s stagger.
- **Level up**: the ring closes, the portrait flares amber, the level
  numeral on its badge ticks up with a bell, the ring refills with the
  remainder.
- **Did not fight / fell**: portrait tinted grey, ring still.
- **Who carried it**: the member with the most damage dealt wears a thin
  gold rim. No word.
- **Deeds** under each portrait, glyph + number: sword = dealt, cracked
  shield = taken, leaf = healed (when non-zero), skull count = felled. The
  physical/other split is in the portrait's focus tooltip.
- **Gold stack, satchel, book pile, bag**: where C2 and C6 left them.
- **This fight's haul**: one chip row of what the fight left the party
  holding (each taken item on a small plinth, relics, books, potions, hone
  sparks): the one recap of the choice row that just swept away. Today's
  "Gained" event-relic chips join it (`pendingRelicReceipts`, acknowledged
  on leaving, unchanged). No caption.
- **Held relics**: not on the board (owner call O5).
- **Continue**: the gold forward-arrow plate, no word. The stale ember glow
  goes.
- **Navigation**: entry is Continue. Up goes to the haul row (a clamped
  rail, its chips' tooltips), Up again to the portraits (a rail, their deed
  tooltips). Down returns. Cancel = Continue.
- **Outcome form**: portraits and deeds only (from `Tally` and `Ledger`);
  no rings (no `CombatReward`), no gold, no haul.

### C9. Leaving

Continue or Cancel leaves (`Dismiss`, acknowledging displayed receipts,
`LeaveFight`). Cancel stays a no-op while a page has choices left.

### C10. Timing, skip, input

- **One helper** (`ReckoningTime`) turns an authored duration into wall
  time: `authored x (BattleSpeed.DefaultDisplay / preset.Display)`, times
  the test seam `ReckoningController.SpeedMultiplier`, reading the preset
  from `GameSettings` when the overlay opens. Every wait and tween goes
  through it, including today's wipe, sweep and bar fills: **the Reckoning
  now follows the battle-speed preset**, which it does not today.
- **The cache is a real `Button` and the `NavContext` entry** from the
  frame it lands, so A and a click both open it; the entry moves to the
  first card when the cards land.
- **Skip**: any Submit or click while a beat runs completes it (the cache
  opens, all cards flip at once; a Legendary keeps only its 0.12 s flare).
  The skipping press is consumed. A mouse click anywhere in the frame is
  caught by a transparent full-frame catcher behind the row; Submit on a
  face-down card is caught by `Take(i)` checking `_revealing`. **A
  consumed press shows itself**: the focused card (or the cache) nudges 4 px
  and settles. A press when nothing runs acts at once; there is no
  second-press tax.
- **Moving focus never skips.** Left/Right during the reveal moves the
  arrow across face-down cards; they show no tooltip until face-up
  (`TooltipFocusRouter` suppressed while `_revealing`).
- **Guard**: for 0.15 s after the last card lands, a Submit is consumed and
  nudges, so a held or repeated press cannot take a card the player has not
  seen.
- **B** during a beat: nothing (today's `cancelLive: () => !_choosing`).

### C11. Sound

The palette is cloth, breath, wood, soft bells and low strings: tender, not
a casino. Coins tick softly; no cascade.

| Moment | Sound | Source |
|---|---|---|
| Card deal (no cache) | cloth rustle | new |
| Cache open | `OpenPack` if it fits the palette, else a cloth-and-knot untie | exists / new |
| Card flip | one flip clip, pitch a step per quality | new clip + a pitch overload on `SoundController.Play` |
| Legendary landing | low bell | new |
| Coin count | soft tick | new |
| Potion / book land | glass clink / paper thump | new |
| Take | soft clink (weight from layering, C6) | new |
| Hone | whetstone scrape | new |
| Ember snuff | breath | new |
| Experience fill / level up | hum / bell | new |
| Fell / Endured | low string note / drone | new |

No music duck: the Legendary's silent beat is a pause in the effects.

### C12. The settlement record (Domain type, additive)

- A `PaidPrize` (in `Domain/Rewards`, beside `ReckoningRequest`): `Shown`
  kind (the map chip, `node.Prize`), `Paid` kind (after
  `RealmPrize.Effective` and overflow), amount, item or book ids.
- `RealmPrizes.Pay` keeps its signature and takes an optional
  `List<PaidPrize> record = null`; `SettleFight` threads one list through
  `Pay` and `PayHeartAnchors`.
- `FightSettlement` gains `Prizes`, `EncounterClass` (nullable, captured
  before `ClearCurrentRoom`) and `GoldBefore` (captured before `Pay`).
  **`WithNotices` copies every field** (today it rebuilds through the
  three-argument constructor, `RunOrchestrator.cs:487-492`, and would drop
  them on every room fight). All five construction sites are checked.
- `ReckoningRequest` (full and outcome forms) carries the three.
- Bot neutrality holds because nothing is added to `RunSnapshot`, a save, or
  an RNG draw (`BotRunDriver.cs:835` is a comment). The test pins the same
  save JSON and the same notice list for one settlement before and after,
  and a won room fight's `Prizes` is non-empty after `WithNotices`.

### C13. Two pure queries (Core, additive)

- `RewardPages.PreviewLeave(save, page)` returns the hones per
  (recipient, slot) and the gold that `Leave` would pay; `Leave` itself is
  rewritten to consume it, so the face and the payout cannot disagree
  (`RewardPages.cs:259-282` spills hones across slots, then pays gold).
- Stats at a hypothetical honing: honing is read from the run's
  `slotHoning` through `ContentDatabase.HoningOf`
  (`ContentDatabase.Gear.cs:32`) inside `EffectiveStats`, with no override.
  R1 adds an override parameter on that path (a throwaway lookup, never a
  write to `slotHoning`, which the page saves after each action), pinned by
  a test against literals.

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
6. **Inert card**: chained; can be taken (today's rule).
7. **Bag**: no cap; the displaced copy always lands.
8. **Party of one or two**: medallions and board centre on who is fielded.
9. **Missing content**: name and art blank, Common plinth, still choosable.
10. **Gamepad, choice phase**: Left/Right along the row (`RowOrder`), Down to
    the whetstone, Down to the door, Up back. A acts; B nothing while
    choices remain. No extra shortcut. The prompt footer shows glyphs only.
11. **Mouse/keyboard**: the catcher and the cards skip during a beat; arrow
    keys mirror the D-pad.
12. **Start / system menu**: the Reckoning absorbs Start today (open call in
    `AUDIT.md`). If a menu ever opens over it, the running beat completes
    first (`SettleImmediately`), so nothing ticks underneath.
13. **Save** after each take, hone or leave (unchanged).

## 5. Art needs

References for every prompt: `Assets/_Project/Art/Enemies/Giant_rat_sheet.png`
(outline and cel shading), `Assets/_Project/Art/Backgrounds/Processed/
map_chip_gear.png` (the set they sit with) and one processed relic icon from
`Assets/_Project/Art/Items/Relics/Processed/` (object scale). Sources go to
`Art/UI/Reckoning/`, keyed into `Art/UI/Reckoning/Processed/` with a keyer
recipe (`docs/ART_PIPELINE.md` §2-3). 512 px cells. Each sheet has a
single-object fallback (same text, "one object, centred") if the generator
will not hold the grid.

**Placeholders**: every phase ships and passes its gates before art lands:
`proc:radial_glow` discs for plinths and the cache, a plain violet card back,
`map_chip_*` and `Resources/Icons/Ability/*` icons for glyphs. Art swaps in
by key with no new phase.

**Tone**: the caches are **Prince's offerings**: things he wraps for his
souls the way a cat brings a gift, proud and tender. Cloth, knots, worn tin,
stone. Not treasure chests, and no literal household objects (the direction
bible rules those out).

**Style block (first, every time):**

```
2D game art for a dark-fantasy roguelike's reward screen, matching the
attached references: flat cel shading with bold dark outlines, a restrained
grey-violet palette with warm gold accents, worn and handmade, tender rather
than grim. No gore, nothing modern. No text, letters, numbers, UI, frames or
watermark. Flat solid pure green #00FF00 background, no shadow on it, no
green inside the objects.
```

For A2 and A4 (they contain green) end instead with: `Flat solid pure
magenta #FF00FF background, no shadow on it, no magenta inside the
objects.` and key them `"magenta"`.

**A1. Caches** - `reward_caches.png`

```
A sheet of 6 objects in a 3x2 grid of equal square cells, centred, generous
margin. They are a proud cat's offerings, wrapped for the souls he sends to
fight. Row 1 (gear): a small bundle wrapped in frayed violet cloth, tied
with one cord, a single paw print pressed into the knot; the same bundle
larger with two cords and a wax seal; the same bundle largest, old iron
bands over the cloth, a crown-shaped seal. Row 2 (relics): a small dented
tin box; the same tin with brass corners and a seal; the same in dark iron
with a crown seal. Every object has a thin dark seam along its opening with
nothing drawn in it. Bold silhouettes that read at 96 pixels.
```

**A2. Plinths** - `reward_plinths.png` (magenta)

```
A sheet of 4 low resting places for an object, in a 4x1 row of equal cells,
seen from slightly above, each with a flat top, centred. Each has a
DIFFERENT outline so they read in greyscale:
1. a bare square flagstone;
2. a rough folded cloth on a round stone, a band of moss at its edge;
3. a cloth embroidered with blue thread on a taller hexagonal stone;
4. a cloth woven with gold thread on a wide scalloped stone, a small lit
   votive candle at one side.
Same width, increasing height from 1 to 4. A smooth flat band across the
front of each stone, left plain (the game engraves a number there).
```

**A3. Card back** - `reward_card_back.png`

```
One upright card back, 2:3 portrait, centred: dark violet cloth with a pale
rubbing of a cat's paw print in the middle, a worn gold thread edge. Plain
and quiet.
```

**A4. Glyphs** - `reward_glyphs.png` (magenta). Reuse first:
`Resources/Icons/Ability/element_physical.png` (damage),
`defense_physical.png` (defence), `resource_mana.png` (mana). Commission:

```
A sheet of 10 small icons in a 5x2 grid of equal square cells, centred, very
bold and simple so each reads at 24 pixels, same outline weight as the
references. Row 1: a red heart (health); a feather (speed); a whetstone
with a spark (hone); a stack of three gold coins; a small wooden door ajar
(leave). Row 2: a cloth satchel (potions); a worn leather bag (gear bag); a
cracked shield (damage taken); a green leaf (healed); a small skull
(felled).
```

**A5. Chain overlay** - `reward_chain.png`

```
One short loop of dark iron chain lying diagonally, centred, simple.
```

Procedural: seam light, embers, flip shimmer, screen-edge flare and coin
spill use `ProceduralSpriteBaker`'s `proc:radial_glow`, `proc:rarity_burst`,
`proc:arrow_ember`. The map medallions and chips are baked into the Map
scene by key today (`MapScreen.cs:38-42`), so the Reckoning gets its own
`[SerializeField]` sprite table wired in `WireReckoning`.

## 6. Phases and gates

Each phase is one commit at a test-passing checkpoint: `tools/
run_tests_parallel.ps1 -Changed -ChangedPaths <its paths>`, plus
`-BuildScenes` whenever the tree or a `[SerializeField]` changed
(`docs/TESTING.md`). **Seeing it**: the Reckoning has no static
`screenshot.ps1 -Panel` entry (`tools/screenshot.ps1:138-146`; AUDIT #43
records a false success on this panel), so visuals come from
`ReckoningCaptureTests` under `-Runtime -RuntimeFilter ReckoningCaptureTests`,
which the owner allows per session, or from the owner's play. Until then a
phase reports "tests green, not seen".

Strings are deleted in the phase that stops using them, never earlier, so
every checkpoint renders.

| Phase | Content | Gate beyond the commit gate |
|---|---|---|
| R0 | Art prompts §5 to the owner; capture fixtures added to `ReckoningCaptureTests` (no-cache page, cache page with a Legendary, 5-card row, board, outcome board) | Fixtures compile and run headless as no-ops without graphics |
| R1 | C12 settlement record; C13 `PreviewLeave` and the honing override; the shared compare builder | Bot-neutrality test; `WithNotices` keeps `Prizes`; `Leave` consumes `PreviewLeave` with today's results pinned; honing override against literals; dossier compare tests unchanged |
| R2 | Card face (C5) with placeholders, beside the old labels, then the old labels go; `RowOrder` | Tree audit at four aspects; face test per quality, count (3 and 5), wearer, ghost, empty slot, inert; `RowOrder` EditMode test; Right from a card goes to the next card in row order; `MenuTypeLintTests` scopes |
| R3 | Choice actions (C6): take, displaced, hone with preview, coin case, leave preview and payout, spent states, repaint after take; heading, Favor line, ledger and action strings removed | PlayMode: take, hone, sell and leave drive the real `RewardPages` and leave the same save as before (pinned literals); a second card for the same slot repaints |
| R4 | Arrival and reveal (C3, C4, C10) from the page data | PlayMode: no-cache page deals face-up; cache only on Rare+; flips in row order; skip lands all face-up; a press during a beat never calls `RewardPages.Take`; focus on a face-down card shows no tooltip; guard swallows a repeat; wall time within the cap at the slowest preset |
| R5 | Haul (C2) into the row and the board (C8); tabs, RELICS and TALLY pages removed | PlayMode: gold stack counts from `GoldBefore` to a pinned literal for a fixture node; potion count; book pile; overflow and exhausted-pool dissolve; level-up, fell and outcome boards; board navigation; `ReckoningTabTests`, `ReckoningChipNavigationTests`, `ReckoningGamepadNavigationTests`, `ReckoningScreenTests` rewritten |
| R6 | Sound (C11), the pitch overload, the timing helper across every wait | EditMode: every timing reads `ReckoningTime`; PlayMode: a pinned duration at two presets |
| R7 | Art swap as each sheet arrives | `Art/` diff reviewed after `-BuildScenes` (`LoadSprite` flips importer settings) |

Bot: R1 is the only phase near settlement, pinned neutral; `tools/bot.ps1`
numbers must not move.

## 7. Open owner calls

1. **Favor.** The plan drops the Favor line from the reward screen (it
   explains the dice). Its number would then show nowhere: the RUN pane
   lists only the event buff (`Domain/UiKit/RunEffectRows.cs:52`), while the
   page used `ItemOfferRoll.CurrentSquadFavor()` (squad maximum plus that
   buff). Options: (a) add a squad-Favor row to RUN, fed from
   `CurrentSquadFavor`; (b) keep a Favor mark on the reward screen; (c)
   drop the readout. `PLAN_ITEMS.md` 1.2 had the header show it.
2. **Ending words.** Drop VICTORY / ENDURED / FELL for the medallion states,
   or keep one word for Endured only (a reviewer's suggestion: it is the
   state a picture carries worst), or keep all three.
3. **Battle speed.** The Reckoning will follow the battle-speed preset
   (today it ignores it). At the default 1x it runs at the authored times
   above; a player at 2x gets it twice as fast. The evidence leans yes:
   repeated reward animations are what players ask to speed up (F5), and a
   speed setting is the answer one shipped spec gives. Confirm.
4. **Held relics and the tally.** The board drops the held-relic list (it
   stays on the RUN pane, one press away) and folds the tally into glyph
   numbers under each portrait (the physical/other split on focus). Keep
   either on the Reckoning?

Decided here, not asked: Prince's-offering caches (§5 tone); the cache
opens itself; no cache on Common/Uncommon-best pages; detail in a focus
tooltip; no pad shortcut to the door; sound sourcing follows the sound pass
(owner clips first) as a dependency of R6, not a decision.

## 8. Rejected alternatives

- **Slot-machine reel** (Vampire Survivors): its players love it (F4), but
  a reel suits a haul you keep whole; this screen asks for a choice between
  k + 2 readable cards, and a cycling reel hides them. The linger on a
  Legendary (C4.4) takes the part that works: a rare moment allowed to run.
- **Player-driven flips** (Hearthstone, F2): each flip is one more press on
  every page of every fight (F5); the cache already gives the one tell, and
  the row flips itself.
- **Drops on the fight stage** (Diablo beams): stage-layer work in
  `FightController` for one beat; the haul carries the same promise kept.
- **Card frames per quality**: the plate behind cards was removed on
  purpose (`ReckoningScreen.cs:1006-1018`); the plinth carries quality under
  the item.
- **Hover lift / tilt**: banned by the no-hover-scale rule; the item lifts
  on take, which is not hover.
- **A first-time teaching ghost** for hone and leave (round 1, cold
  reviewer): the no-handholding rule. The always-on focus previews (ghost
  lifts, sparks wait on the target) do the teaching.
- **Collapse the ceremony after N fights** (round 1, cold reviewer): no
  hidden counter; value-proportional ceremony (no cache below Rare) and the
  cap keep fight 200 short.
- **Focus on the best card** (rev 1): decided for the player and invited a
  mis-press.
- **Flips in ascending quality, best last** (rev 1-2): the order itself
  became a second tell and made flips jump around a row grouped by wearer.
- **The worn item as a chip on the medallion rim** (rev 2): ~24 px, could
  not carry "what you wear" or "what you lose" (round 2, cold reviewer).
- **Household keepsakes as plinths** (saucer, collar; round 2, cold
  reviewer): warm, but the direction bible rules out literal real-life
  objects. Cloth on stone keeps the tenderness.
- **Keep the whetstone glyph with a coin badge at `MaxPlus`** (round 1,
  cold reviewer): the press sells instead of hones; a whetstone there would
  show the wrong action.
- **Drop the haul row from the board** (round 2, cold reviewer): it is the
  only place the taken items stay visible once the choice row sweeps away.

## 9. Files (last)

Domain: `Domain/Rewards/ReckoningRequest.cs`, a new `Domain/Rewards/
PaidPrize.cs`, `Domain/UiKit/Screens/ReckoningScreen.cs` (cache, embers,
plinths, ghosts, medallions, whetstones, door, gold stack, satchel, book
pile, bag, catcher; board replaces tabs), `Domain/UiKit/OfferRowLayout.cs`
(plinth and medallion bands) and a `RowOrder` beside it,
`Domain/UiKit/UiStrings.cs` (Reckoning block shrinks),
`Domain/UiKit/RunEffectRows.cs` (O1a). Core: `Core/RealmPrizes.cs`,
`Core/Bot/RunOrchestrator.cs` (`FightSettlement`, `WithNotices`,
`SettleFight`), `Core/FightController.Input.cs` (`OpenReckoning`),
`Core/RewardPages.cs` (`PreviewLeave`), `Core/Content/ContentDatabase.*`
(honing override), `Core/ReckoningController.cs` (split `.Reveal`,
`.Board`), `Core/RewardCardFace.cs` (parts, not sentences), a shared compare
builder beside `Core/ItemDescription.cs`, `Core/ReckoningTime.cs`,
`Core/Sound.cs`, `Core/SoundController.cs` (pitch overload). Editor:
`ScreenRegistry.WireReckoning` (map art table). Tests: every
`Reckoning*Tests` class (Screen, Tab, Phase, Comparison, Lifecycle, Capture,
Chip and Gamepad navigation), `OfferRowLayoutTests`, `MenuTypeLintTests`,
`HeartAnchorSettlementTests` and `RealmRunTests` (callers of `Pay`). Docs:
`docs/CODE_MAP.md` rows 28, 78, 81, 82, 83, 85.

## 10. Revision log

- rev 1: first draft.
- rev 2, after round 1 (reviewer A read the code, B only the plan):
  cross-layer settlement record instead of "no domain change"; one-bit
  cache tell; cache auto-opens; haul concurrent; the take carries the
  weight; per-count card layout; shared compare builder; focus not on
  "best"; hone preview on focus; skip through a catcher and a flag; one
  timing helper with a cap; real prize destinations (satchel, book pile,
  multi-chip, overflow); magenta keying; additive phase order.
- rev 3, after round 2 (C read the code, D only the plan): no cache below
  Rare, so the cache's presence is the tell; flips in row order; wall-time
  arithmetic shown; the worn item as a ghost on the plinth; headline-stat
  rule stated; `PreviewLeave` and a honing override (C13) so hone and leave
  previews are buildable and truthful; `WithNotices` field loss; shown vs
  paid chip; `GoldBefore`; `RowOrder` permutation; the cache as nav entry;
  consumed presses nudge; no tooltip on face-down cards; repaint after take;
  board navigation; placeholder policy; R0 uses `ReckoningCaptureTests`;
  sound palette; owner calls rewritten (relics/tally and battle speed added,
  tone and sourcing decided).
- rev 4, sourced research round (no review round: no structural change).
  §2 rewritten as findings F1-F11 with sources; §11 lists every dropped
  unsourced observation. Changes and what drove them: the Legendary
  lingers 1.2 s without blocking (F3 Larche et al., F4 Vampire Survivors);
  no-near-miss rule (F3); a quality-versus-delta note on the card (F6
  Diablo III postmortem); the reel's rejection reason corrected (F4
  contradicted "the complaint is length"); player-driven flips rejected
  with a reason (F2, F5); owner call 3 gains its evidence (F5). The
  one-bit cache tell, no cache below Rare and the skip contract are
  confirmed, not changed (F1, F2, F5).

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

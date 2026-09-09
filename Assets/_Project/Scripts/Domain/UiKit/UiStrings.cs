using System.Collections.Generic;
using System.Reflection;

namespace PrincesPalace.Domain.UiKit
{
    // THE string manifest. AUDIT #35 asked for exactly this and v1 never had it.
    //
    // Grows as screens are rebuilt; this is the starter set, seeded from strings
    // that genuinely exist in v1 rather than invented. Entries are added here
    // and nowhere else, because Ui.Label and Ui.Button take no string overload
    // and Core's UiText extensions take no string overload either.
    //
    // One file until it hurts, then partials of this same class. The guarantee
    // is the missing overloads and the lint, not the file count.
    public static class UiStrings
    {
        // ---- paging ----------------------------------------------------------
        //
        // The two arrow glyphs, with no screen's name on them. These used to be
        // TalentPrev/TalentNext, and five of the seven screens that step
        // through something reached across for them -- the glossary, the debug
        // menu, the shop's pack and the relic draft all labelled their arrows
        // with a talent-screen string, which is how you can tell a string was
        // named after its first caller rather than after what it says.
        public static readonly UiString PagerPrev = UiString.Define("pager.prev", "<");
        public static readonly UiString PagerNext = UiString.Define("pager.next", ">");

        // --- the confirmed drift pair -------------------------------------
        // v1 wrote this twice: SceneBuilder.Hub.cs:127 baked
        // "Gold: 0    Relics: 0" while HubController.cs:67 wrote
        // $"Gold: {save.Gold}    Relics: {save.Relics}" -- the four-space
        // separator copied by hand, and therefore copyable wrong. One entry now
        // serves both the initial text and every refresh.
        public static readonly UiString WalletSummary =
            UiString.Define("wallet_summary", "Gold: {0}    Relics: {1}", "Gold: 999999    Relics: 99");

        // --- main menu ------------------------------------------------------
        // ---- talents -------------------------------------------------------

        public static readonly UiString TalentEmbers =
            UiString.Define("talent.embers", "{0} EMBERS", "9999 EMBERS");
        public static readonly UiString TalentInvest = UiString.Define("talent.invest", "KINDLE");
        public static readonly UiString TalentTaken = UiString.Define("talent.taken", "KINDLED");
        public static readonly UiString TalentLocked = UiString.Define("talent.locked", "LOCKED");
        public static readonly UiString TalentNoEmbers = UiString.Define("talent.no_embers", "NO EMBERS");
        public static readonly UiString TalentBack = UiString.Define("talent.back", "Back");

        // "RESPEC", matching the word the dossier's reward-track line uses
        // ("FREE RESPEC") rather than a thematic inverse of KINDLE. The player
        // meets the reward's name before the button, and the two have to be
        // recognisably the same thing.
        public static readonly UiString TalentRespec = UiString.Define("talent.respec", "RESPEC");

        // THE PANEL'S OWN VOCABULARY. Six kickers for six states, and a reason
        // for each of the three that refuse -- the state in a word above the
        // name, the reason in a sentence below the description. Split that way
        // because the two are read at different moments: the kicker while
        // scanning, the reason only after deciding to want the thing.
        public static readonly UiString TalentKickerLit =
            UiString.Define("talent.kicker_lit", "KINDLED");
        public static readonly UiString TalentKickerReady =
            UiString.Define("talent.kicker_ready", "READY TO KINDLE");
        public static readonly UiString TalentKickerCostly =
            UiString.Define("talent.kicker_costly", "TOO DEAR FOR NOW");
        public static readonly UiString TalentKickerGated =
            UiString.Define("talent.kicker_gated", "THE PATH IS SHORT");
        public static readonly UiString TalentKickerLocked =
            UiString.Define("talent.kicker_locked", "UNREACHABLE");
        public static readonly UiString TalentKickerUnwritten =
            UiString.Define("talent.kicker_unwritten", "UNWRITTEN");

        public static readonly UiString TalentWhyLocked =
            UiString.Define("talent.why_locked", "Kindle the star beneath it first.");
        public static readonly UiString TalentWhyGated =
            UiString.Define("talent.why_gated", "Spend further along this path to open it.");
        public static readonly UiString TalentWhyPoor =
            UiString.Define("talent.why_poor", "Not enough Embers.");

        public static readonly UiString TalentPriceEmbers =
            UiString.Define("talent.price_embers", "{0} Embers", "12 Embers");

        // The convergence and the capstone cost nothing and are gated instead.
        // Saying "0 Embers" for them would read as a bargain rather than as a
        // different kind of price.
        public static readonly UiString TalentPriceGate =
            UiString.Define("talent.price_gate", "{0} Embers spent on this path",
                "20 Embers spent on this path");

        // What the panel says with nothing picked. It stopped being hidden when
        // empty, so it needs something to be.
        public static readonly UiString TalentPickPrompt =
            UiString.Define("talent.pick_prompt", "CHOOSE A STAR");
        public static readonly UiString TalentPickBody =
            UiString.Define("talent.pick_body",
                "Every star in this constellation is a change to who they are. Pick one to read it.");

        public static readonly UiString TalentUnwrittenName =
            UiString.Define("talent.unwritten_name", "Unwritten");
        public static readonly UiString TalentUnwrittenBody =
            UiString.Define("talent.unwritten_body",
                "This path is charted but not yet lit. Its stars are waiting to be written.");

        // The confirmation. One press used to clear all three constellations
        // with nothing in between, which is why these exist -- and why the body
        // is Runtime rather than a fixed line: it names the actual refund.
        public static readonly UiString TalentRespecTitle =
            UiString.Define("talent.respec_title", "Put out every ember?");
        public static readonly UiString TalentRespecCancel =
            UiString.Define("talent.respec_cancel", "KEEP THEM LIT");
        public static readonly UiString TalentRespecConfirm =
            UiString.Define("talent.respec_confirm", "PUT THEM OUT");

        // NAMES THE EXACT REFUND. A dialog that only asked "are you sure?"
        // would be asking the player to remember what they had.
        public static readonly UiString TalentRespecPrompt =
            UiString.Define("talent.respec_prompt",
                "{0} stars go dark across every constellation, and {1} Embers come back to you.",
                "21 stars go dark across every constellation, and 148 Embers come back to you.");
        public static readonly UiString TalentPath =
            UiString.Define("talent.path", "CONSTELLATION {0} OF {1}   -   {2} KINDLED", "CONSTELLATION 3 OF 3   -   21 KINDLED");

        public static readonly UiString TrackRow =
            UiString.Define("track.row", "Reward Track");
        public static readonly UiString TrackClose =
            UiString.Define("track.close", "CLOSE");

        // "LEVEL 37 -- NEXT AT 40   A STAT POINT", set as four pieces plus a
        // rule rather than as one sentence.
        //
        // ONE STRING WAS THE FIRST BUILD, and the reason it is not one now is
        // that a sentence can only be one size. The figure is the thing this
        // row exists to say and it was set at the same 18px as the word LEVEL
        // in front of it; separate pieces let it stand at 34 while the words
        // around it stay quiet.
        //
        // THE REWARD'S NAME IS BACK, having been cut once for good reason: as
        // part of a single centred sentence it ran to eighty characters and
        // collided with CLOSE. It has its own box now, which is what the text
        // fit audit measures against, so the collision is a build failure
        // rather than something to discover in a screenshot.
        public static readonly UiString TrackLevelWord =
            UiString.Define("track.level_word", "LEVEL");

        public static readonly UiString TrackNextAt =
            UiString.Define("track.next_at", "NEXT AT {0}", "NEXT AT 100");

        public static readonly UiString TrackNextAtComplete =
            UiString.Define("track.next_at_complete", "REWARD TRACK COMPLETE");

        // COLLECT, in two forms, because "Collect 1 rewards" is the kind of
        // thing a player reads once and stops trusting the screen over.
        //
        // Two entries rather than one template with a pluralising helper: there
        // is no plural machinery in this project, inventing it for one button
        // would be the largest thing on this screen by a distance, and English
        // is the only language it currently has to be right in.
        public static readonly UiString TrackCollectOne =
            UiString.Define("track.collect_one", "COLLECT 1 REWARD");
        public static readonly UiString TrackCollectMany =
            UiString.Define("track.collect_many", "COLLECT {0} REWARDS", "COLLECT 99 REWARDS");

        // ---- the focus card ------------------------------------------------
        //
        // Four lines: what this node is TO the player, which level it is, what
        // it pays, and where they stand with it. The kicker and the state line
        // are the two halves of that last question -- the kicker says what the
        // card is showing and the state says what the player can do about it,
        // and collapsing them into one line loses whichever half is not urgent.

        public static readonly UiString TrackCardNext = UiString.Define("track.card_next", "NEXT REWARD");
        public static readonly UiString TrackCardHere = UiString.Define("track.card_here", "YOU ARE HERE");
        public static readonly UiString TrackCardWaiting = UiString.Define("track.card_waiting", "WAITING FOR YOU");
        public static readonly UiString TrackCardCollected = UiString.Define("track.card_collected", "ALREADY YOURS");
        public static readonly UiString TrackCardToCome = UiString.Define("track.card_to_come", "STILL AHEAD");

        // The card's level is a FIGURE now, standing at the right end of the
        // header row after a small "LVL" -- so the word and the number are two
        // strings, at two sizes, rather than one label reading "LEVEL 48".
        public static readonly UiString TrackCardLvl =
            UiString.Define("track.card_lvl", "LVL");

        public static readonly UiString TrackStateReady =
            UiString.Define("track.state_ready", "CLICK THE NODE TO COLLECT");
        public static readonly UiString TrackStateCollected =
            UiString.Define("track.state_collected", "COLLECTED");
        public static readonly UiString TrackStateLocked =
            UiString.Define("track.state_locked", "{0} LEVELS AWAY", "99 LEVELS AWAY");
        public static readonly UiString TrackStateNextLevel =
            UiString.Define("track.state_next_level", "THE VERY NEXT LEVEL");

        // ---- the ascent ribbon ---------------------------------------------

        // A HEAD AND AN INSTRUCTION AT OPPOSITE ENDS of the ribbon's own width,
        // with a rule between them. One centred caption saying both was the
        // first build, and it read as a note under a picture rather than as the
        // top of a scale.
        public static readonly UiString TrackRibbonTitle =
            UiString.Define("track.ribbon_title", "THE WHOLE ASCENT");

        public static readonly UiString TrackRibbonHint =
            UiString.Define("track.ribbon_hint", "DRAG TO TRAVEL   .   LEVELS 2 TO 100");

        // Above the node the caret hangs over. Nine pixels of it, which is why
        // it is one word.
        public static readonly UiString TrackNext =
            UiString.Define("track.next", "NEXT");

        public static readonly UiString DossierSpendPoint =
            UiString.Define("dossier.spend_point", "+");

        public static readonly UiString DossierRefundPoint =
            UiString.Define("dossier.refund_point", "-");
        // ---- the hub -------------------------------------------------------

        // Three currencies, not two. Embers is what talents actually cost and
        // the hub never showed it. WalletSummary is left alone -- other screens
        // still use the two-currency line.
        public static readonly UiString HubWallet =
            UiString.Define("hub.wallet", "Gold: {0}    Embers: {1}",
                "Gold: 999999    Embers: 999");

        public static readonly UiString HubBeginDescent =
            UiString.Define("hub.begin_descent", "BEGIN DESCENT");
        public static readonly UiString HubResumeFloor =
            UiString.Define("hub.resume_floor", "RESUME - FLOOR {0}", "RESUME - FLOOR 99");

        // ---- descent map ----------------------------------------------------

        public static readonly UiString MapTitle = UiString.Define("map.title", "The Descent");
        // Worst-case samples, like every other template: without one the label
        // renders its raw "{0}" at rest, which is what the resting screenshot
        // showed -- and E1 has nothing to measure the box against.
        public static readonly UiString MapDepth =
            UiString.Define("map.depth", "STEP {0}  ·  FLOOR {1}", "STEP 999  ·  FLOOR 99");
        public static readonly UiString MapGold =
            UiString.Define("map.gold", "{0} GOLD", "99999 GOLD");
        public static readonly UiString MapAbandon = UiString.Define("map.abandon", "Abandon Run");
        // A book waiting to be placed (docs/PLAN_SHOP.md §7.1 point 4) -- the
        // map's nudge toward the dossier's assignment panel, since neither
        // the shop nor the dossier is guaranteed to be the next thing opened
        // after a purchase or a drop.
        public static readonly UiString MapPendingBook =
            UiString.Define("map.pending_book", "SPELL BOOKS TO PLACE: {0}", "SPELL BOOKS TO PLACE: 99");
        // The hint at the fog, past the last generated column. A leg is rolled
        // whole and nothing exists beyond it until the party reaches the
        // boundary, so this is an honest "there is more" rather than a false
        // peek at rooms that do not exist yet.
        public static readonly UiString MapFog = UiString.Define("map.fog", "THE WOOD\nCONTINUES");

        // What the room the party just walked into did.
        //
        // Every non-fight room says something, INCLUDING the ones with no
        // content behind them yet. That is v1's rule and it is worth restating:
        // a room that does nothing without explaining itself reads as a bug,
        // and v2 had regressed to exactly that -- entering a treasure room
        // cleared it in silence.
        //
        // "Gold", not v1's "Embers". v1's treasure text said Embers while
        // crediting run gold; in v2 those are two different currencies (gold
        // is spent inside a descent, embers survive it), so the old copy would
        // now name the wrong one.
        public static readonly UiString MapRoomTreasure =
            UiString.Define("map.room.treasure", "You found a stash of {0} Gold.",
                "You found a stash of 999 Gold.");
        public static readonly UiString MapRoomRest =
            UiString.Define("map.room.rest", "The squad rests, and recovers to full health.");
        public static readonly UiString MapRoomShop =
            UiString.Define("map.room.shop", "A trader waits here. (In-run shops are not built yet.)");
        public static readonly UiString MapRoomEvent =
            UiString.Define("map.room.event", "Something stirs here. (Events are not built yet.)");
        public static readonly UiString MapRoomItem =
            UiString.Define("map.room.item", "Something glints here. (Item rooms are not built yet.)");
        public static readonly UiString MapRoomEmpty =
            UiString.Define("map.room.empty", "The room is empty.");

        // Short names, because the value beside them is what is being read.
        // Spelled out where the abbreviation would be a guess (Speed, Attack).
        public static readonly UiString StatStrength = UiString.Define("stat.str", "STR");
        public static readonly UiString StatDexterity = UiString.Define("stat.dex", "DEX");
        public static readonly UiString StatConstitution = UiString.Define("stat.con", "CON");
        public static readonly UiString StatWisdom = UiString.Define("stat.wis", "WIS");
        public static readonly UiString StatIntelligence = UiString.Define("stat.int", "INT");
        public static readonly UiString StatCharisma = UiString.Define("stat.cha", "CHA");

        public static readonly UiString StatMaxHealth = UiString.Define("stat.health", "Health");
        public static readonly UiString StatAttack = UiString.Define("stat.attack", "Attack");
        public static readonly UiString StatSpeed = UiString.Define("stat.speed", "Speed");
        public static readonly UiString StatMaxMana = UiString.Define("stat.maxmana", "Max Mana");
        public static readonly UiString StatSignatureGain =
            UiString.Define("stat.signaturegain", "Focus / turn");
        public static readonly UiString StatManaRegen = UiString.Define("stat.manaregen", "Mana Regen");

        // Formerly stat.physres/stat.magres, keyed to PhysicalResistance/
        // MagicalResistance -- renamed with the fields, not the display text
        // ("Physical DEF"/"Magical DEF" already read as Defense).
        public static readonly UiString StatPhysicalDefense =
            UiString.Define("stat.physdef", "Physical DEF");
        public static readonly UiString StatMagicalDefense =
            UiString.Define("stat.magdef", "Magical DEF");

        public static readonly UiString Play = UiString.Define("play", "Play");
        public static readonly UiString Exit = UiString.Define("exit", "Exit");
        public static readonly UiString Close = UiString.Define("close", "Close");
        public static readonly UiString Cancel = UiString.Define("cancel", "Cancel");

        // The wordmark. One entry rather than a literal in MainMenuScreen,
        // for the same reason every other piece of authored copy is: a
        // literal at the call site cannot be told apart from a stray string
        // that slipped past Contract A.
        public static readonly UiString GameTitle = UiString.Define("game_title", "Prince's Palace");

        // "Continue - Slot 3". Built with a placeholder at screen-build time
        // (Domain has no SaveSystem to ask which slot), then set for real by
        // MainMenuController.RefreshContinue once a save exists to name.
        public static readonly UiString ContinueSlot =
            UiString.Define("continue_slot", "Continue - Slot {0}", "Continue - Slot 5");

        // --- the Reckoning ------------------------------------------------------
        public static readonly UiString ReckoningTitle = UiString.Define("reckoning.title", "THE RECKONING");
        public static readonly UiString ReckoningExperience =
            UiString.Define("reckoning.experience", "EXPERIENCE");
        public static readonly UiString ReckoningGold =
            UiString.Define("reckoning.gold", "+{0} GOLD", "+99999 GOLD");
        public static readonly UiString ReckoningChooseOne =
            UiString.Define("reckoning.choose_one", "CHOOSE ONE");
        public static readonly UiString ReckoningLevel =
            UiString.Define("reckoning.level", "LEVEL {0}", "LEVEL 99");
        // The level-up is called out in the level slot rather than as a fourth
        // label, because it is the SAME fact -- what level they are now -- and
        // a separate badge would need somewhere to live on every row that
        // never earns one.
        public static readonly UiString ReckoningLevelUp =
            UiString.Define("reckoning.level_up", "LEVEL {0}  -  UP!", "LEVEL 99  -  UP!");
        public static readonly UiString ReckoningExpGain =
            UiString.Define("reckoning.exp_gain", "+{0} EXP", "+99999 EXP");
        public static readonly UiString ReckoningDowned =
            UiString.Define("reckoning.downed", "DID NOT FIGHT");
        public static readonly UiString ReckoningOfferMeta =
            UiString.Define("reckoning.offer_meta", "{0}  -  TIER {1}", "Legendary  -  TIER 10");
        public static readonly UiString ReckoningTaken =
            UiString.Define("reckoning.taken", "TAKEN");

        // The three tabs. Named for what they hold rather than numbered, and
        // spelled out here rather than derived from an enum name -- "TALLY"
        // is not what any reasonable enum member would be called.
        public static readonly UiString ReckoningTabSpoils = UiString.Define("reckoning.tab_spoils", "SPOILS");
        public static readonly UiString ReckoningTabRelics = UiString.Define("reckoning.tab_relics", "RELICS");
        public static readonly UiString ReckoningTabTally = UiString.Define("reckoning.tab_tally", "TALLY");

        public static readonly UiString ReckoningNoRelics =
            UiString.Define("reckoning.no_relics", "YOU CARRY NOTHING INTO THE DARK");
        public static readonly UiString ReckoningRelicHeld =
            UiString.Define("reckoning.relic_held", "HELD FOR THIS DESCENT");
        public static readonly UiString ReckoningTallyHeading =
            UiString.Define("reckoning.tally_heading", "WHAT THEY DID, THIS FIGHT");
        // Reuses the defeat screen's shape on purpose: the same four numbers
        // about the same people should read identically wherever they appear.
        public static readonly UiString ReckoningTallyLine =
            UiString.Define("reckoning.tally_line",
                "{0} dealt  ({1} phys / {2} other)      {3} taken      {4} healed",
                "999999 dealt  (999999 phys / 999999 other)      999999 taken      999999 healed");
        public static readonly UiString ReckoningKills =
            UiString.Define("reckoning.kills", "{0} felled", "999 felled");

        // --- the defeat screen ---------------------------------------------------
        //
        // The Reckoning's twin. Same two-column shape, same 70% frame, opposite
        // news -- which is why it reuses the strings above wherever the fact is
        // the same and only defines what is genuinely different.
        public static readonly UiString DefeatTitle = UiString.Define("defeat.title", "THE DESCENT ENDS");
        public static readonly UiString DefeatLost = UiString.Define("defeat.lost", "LOST");
        public static readonly UiString DefeatKept = UiString.Define("defeat.kept", "KEPT");
        public static readonly UiString DefeatGoldLost =
            UiString.Define("defeat.gold_lost", "{0} GOLD, UNBANKED", "99999 GOLD, UNBANKED");
        public static readonly UiString DefeatEmbers =
            UiString.Define("defeat.embers", "+{0} EMBERS", "+99 EMBERS");
        public static readonly UiString DefeatEmbersNone =
            UiString.Define("defeat.embers_none", "NO NEW BOSSES FELL");
        public static readonly UiString DefeatDepth =
            UiString.Define("defeat.depth", "DEPTH {0}   -   {1} ROOMS CLEARED", "DEPTH 999   -   999 ROOMS CLEARED");
        public static readonly UiString DefeatExp =
            UiString.Define("defeat.exp", "{0} EXPERIENCE, KEPT", "999999 EXPERIENCE, KEPT");
        public static readonly UiString DefeatStatsHeading =
            UiString.Define("defeat.stats_heading", "WHAT THEY DID");
        // Dealt / taken / healed on one line per character. Four numbers rather
        // than four labelled rows: the row is already narrow and the labels
        // would outweigh the figures.
        public static readonly UiString DefeatStatLine =
            UiString.Define("defeat.stat_line",
                "{0} dealt  ({1} phys / {2} other)      {3} taken      {4} healed",
                "999999 dealt  (999999 phys / 999999 other)      999999 taken      999999 healed");
        public static readonly UiString DefeatToHub = UiString.Define("defeat.to_hub", "Return");
        public static readonly UiString DefeatInspect = UiString.Define("defeat.inspect", "Characters");

        // --- the debug menu ---------------------------------------------------
        //
        // Developer-facing, and still routed through the manifest rather than
        // hand-typed at the call site: the E1 text-fit audit only measures what
        // it can see, and a literal is invisible to it. A debug button whose
        // label overflows its box is a small thing, but the exemption would be
        // the first crack in "every user-facing string lives here".
        public static readonly UiString DebugTitle = UiString.Define("debug.title", "DEBUG");
        public static readonly UiString DebugGiveGold = UiString.Define("debug.gold", "+10,000 GOLD");
        public static readonly UiString DebugGiveEmbers = UiString.Define("debug.embers", "+25 EMBERS");
        // A tree is 21 slots x 3 constellations at 1 ember each, so +25 is the
        // button you actually press. +1 exists only to sit on the
        // NotEnoughEmbers boundary, which is the one case +25 can never test.
        public static readonly UiString DebugGiveOneEmber = UiString.Define("debug.ember_one", "+1 EMBER");
        public static readonly UiString DebugFilterAll = UiString.Define("debug.filter_all", "ALL");
        public static readonly UiString DebugFilterConsumable = UiString.Define("debug.filter_consumable", "POTIONS");
        public static readonly UiString DebugFilterWeapon = UiString.Define("debug.filter_weapon", "WEAPONS");
        public static readonly UiString DebugFilterEquipment = UiString.Define("debug.filter_equipment", "ARMOUR");
        public static readonly UiString DebugRow =
            UiString.Define("debug.row", "T{0}  {1}", "T10  Ceremonial Greatsword of the Undying");
        public static readonly UiString DebugPage =
            UiString.Define("debug.page", "PAGE {0} OF {1}", "PAGE 99 OF 99");

        // --- the relic draft ---------------------------------------------------
        public static readonly UiString DraftTitle = UiString.Define("draft.title", "TAKE ONE INTO THE DARK");
        public static readonly UiString DraftSubtitle =
            UiString.Define("draft.subtitle", "It is yours until the descent ends.");
        public static readonly UiString DraftNoRelics =
            UiString.Define("draft.none", "NOTHING STIRS IN THE VAULT");
        public static readonly UiString DraftDescend = UiString.Define("draft.descend", "Descend");
        // Rarity is shown as a word rather than only as a colour: a band is a
        // fact about the relic, and colour alone excludes anyone who cannot
        // separate the six.
        public static readonly UiString DraftRarity =
            UiString.Define("draft.rarity", "{0}", "ULTRA-RARE");

        // --- the shop (docs/PLAN_SHOP.md, docs/handoffs/shop_v2) ------------------
        public static readonly UiString ShopTitle = UiString.Define("shop.title", "SHOP");
        public static readonly UiString ShopGold =
            UiString.Define("shop.gold", "{0} G", "9,999,999 G");
        public static readonly UiString ShopLeave = UiString.Define("shop.leave", "LEAVE");
        public static readonly UiString ShopLeaveConfirm = UiString.Define("shop.leave_confirm", "LEAVE?");
        public static readonly UiString ShopBuy = UiString.Define("shop.buy", "BUY");
        public static readonly UiString ShopPack = UiString.Define("shop.pack", "PACK");
        public static readonly UiString ShopSectionGear = UiString.Define("shop.section_gear", "GEAR");
        public static readonly UiString ShopSectionBooks = UiString.Define("shop.section_books", "SPELL BOOKS");
        public static readonly UiString ShopSectionRelics = UiString.Define("shop.section_relics", "RELICS");

        // The two panels in the design's right-hand column. "SHOP" twice over
        // -- once as the screen's title, once as the actions panel's own
        // caption -- is the prototype's own wording, and the two are separate
        // entries because a translator sizing a panel caption is not sizing a
        // 56pt title.
        public static readonly UiString ShopSectionKeeper = UiString.Define("shop.section_keeper", "SHOPKEEPER");
        public static readonly UiString ShopSectionActions = UiString.Define("shop.section_actions", "SHOP");

        // The shopkeeper's portrait slot, standing empty. Says what it is
        // rather than drawing a blank box: no shopkeeper art exists yet, and
        // an unexplained hole photographs as a bug.
        public static readonly UiString ShopKeeperPending =
            UiString.Define("shop.keeper_pending", "PORTRAIT PENDING");

        // Per-section reroll (§7.1 point 7) -- one button per shelf, its own
        // price, its own counter.
        public static readonly UiString ShopReroll =
            UiString.Define("shop.reroll", "REROLL · {0} G", "REROLL · 9999 G");
        public static readonly UiString ShopRerollNeed =
            UiString.Define("shop.reroll_need", "NEED {0}", "NEED 9999");

        // A card's own price chip, and what it becomes at each state. NEED is
        // the SHORTFALL (price minus gold), never the price itself -- showing
        // the price in red would be showing the wrong number.
        public static readonly UiString ShopCardPrice =
            UiString.Define("shop.card_price", "{0} G", "9999 G");
        public static readonly UiString ShopCardConfirm =
            UiString.Define("shop.card_confirm", "CONFIRM · {0} G", "CONFIRM · 9999 G");
        public static readonly UiString ShopCardNeed =
            UiString.Define("shop.card_need", "NEED {0}", "NEED 9999");
        public static readonly UiString ShopCardSold = UiString.Define("shop.card_sold", "SOLD");
        public static readonly UiString ShopCardNoOffer = UiString.Define("shop.card_no_offer", "NO OFFER");

        public static readonly UiString ShopGearMeta =
            UiString.Define("shop.gear_meta", "TIER {0} · +{1} · {2} AFFIX", "TIER 10 · +5 · 3 AFFIX");

        // A book card's purchase-time facts (docs/PLAN_SHOP.md §7.1 point 4):
        // the shop carries no per-character context to show a badge against,
        // but it does know the run's own learnedSpells/unassignedSpellBooks,
        // and showing what they already say costs nothing extra to roll.
        public static readonly UiString ShopBookKnownByOne =
            UiString.Define("shop.book_known_by_one", "KNOWN BY {0}", "KNOWN BY WWWWWWWWWW");
        public static readonly UiString ShopBookKnownByMany =
            UiString.Define("shop.book_known_by_many", "KNOWN BY {0}", "KNOWN BY 9");
        public static readonly UiString ShopBookAllSlotsFull =
            UiString.Define("shop.book_all_slots_full", "ALL SLOTS FULL");
        public static readonly UiString ShopBookUnassignedCopy =
            UiString.Define("shop.book_unassigned_copy", "{0} UNASSIGNED COPY", "9 UNASSIGNED COPY");
        public static readonly UiString ShopBookUnassignedCopies =
            UiString.Define("shop.book_unassigned_copies", "{0} UNASSIGNED COPIES", "9 UNASSIGNED COPIES");
        public static readonly UiString ShopBookEligible =
            UiString.Define("shop.book_eligible", "ELIGIBLE {0}/{1}", "ELIGIBLE 9/9");

        public static readonly UiString ShopDetailEmpty = UiString.Define("shop.detail_empty", "SELECT A CARD");

        // "THE PACK" on the modal's own title, "PACK" on the button that
        // opens it -- the prototype's wording for each, and the reason they
        // are two entries rather than one reused twice.
        public static readonly UiString ShopPackTitle = UiString.Define("shop.pack_title", "THE PACK");
        public static readonly UiString ShopPackEmpty = UiString.Define("shop.pack_empty", "NOTHING TO SELL");
        public static readonly UiString ShopPackPage =
            UiString.Define("shop.pack_page", "{0} / {1}", "99 / 99");
        public static readonly UiString ShopSellPriceLabel =
            UiString.Define("shop.sell_price", "SELL · {0} G", "SELL · 9999 G");
        public static readonly UiString ShopSellOneButton = UiString.Define("shop.sell_one_button", "SELL 1");
        public static readonly UiString ShopSellAllButton =
            UiString.Define("shop.sell_all_button", "SELL ALL {0}", "SELL ALL 99");

        // --- the dossier's spell-books panel (docs/PLAN_SHOP.md §1g) --------------
        public static readonly UiString DossierSpellsRow = UiString.Define("dossier.spells_row", "SPELLS");
        public static readonly UiString DossierSpellsCount =
            UiString.Define("dossier.spells_count", "{0}/{1}", "9/9");
        public static readonly UiString DossierUnassignedHeader =
            UiString.Define("dossier.unassigned_header", "UNASSIGNED");
        public static readonly UiString DossierUnassignedEmpty =
            UiString.Define("dossier.unassigned_empty", "NOTHING TO PLACE");
        public static readonly UiString DossierSlotEmpty = UiString.Define("dossier.slot_empty", "EMPTY");
        public static readonly UiString DossierSlotFilled =
            UiString.Define("dossier.slot_filled", "{0}", "LIGHTNING BOLT");

        // --- the glossary --------------------------------------------------------
        public static readonly UiString GlossaryTitle = UiString.Define("glossary.title", "THE RECORD");
        public static readonly UiString GlossaryCount =
            UiString.Define("glossary.count", "{0} / {1}", "999 / 999");
        public static readonly UiString GlossaryPage =
            UiString.Define("glossary.page", "PAGE {0} OF {1}", "PAGE 99 OF 99");
        public static readonly UiString GlossaryEmpty =
            UiString.Define("glossary.empty", "NOTHING RECORDED HERE YET");
        // Locked rows are LISTED, not hidden -- a glossary that hides what you
        // have not found cannot tell you what there is to find. The name shows
        // and the body is replaced by this.
        public static readonly UiString GlossaryLocked = UiString.Define("glossary.locked", "NOT YET FOUND");
        public static readonly UiString GlossaryLockedBy =
            UiString.Define("glossary.locked_by", "Unlocked by: {0}", "Unlocked by: Clear a hundred rooms");
        public static readonly UiString GlossaryPick =
            UiString.Define("glossary.pick", "CHOOSE AN ENTRY");

        // --- save slots -----------------------------------------------------
        public static readonly UiString ChooseSlotHeader =
            UiString.Define("choose_slot_header", "Choose a Slot");

        // What an EMPTY Manage Saves row's Top line says. Not "Slot {0}:
        // Empty" any more -- the card now carries its own number badge, so
        // repeating the slot number in the sentence next to it said the same
        // thing twice. Plain rather than templated for the same reason
        // NewDescent below is: the badge is where the number lives now.
        public static readonly UiString SlotEmpty = UiString.Define("slot_empty", "Empty");

        // The number badge on a slot card. A template rather than raw text at
        // the call site for the same Contract A reason every other authored
        // string is -- a slot's own numeral is UI copy, not content, even
        // though it looks like nothing more than a digit.
        public static readonly UiString SlotNumber = UiString.Define("slot_number", "{0}", "9");

        // "Floor 7 - 3h 12m". The character's own name is CONTENT (a roster
        // member's display name), not authored here -- see UiString.FromContent
        // at the call site -- so this template covers only the two numbers
        // that are genuinely this screen's own words.
        public static readonly UiString SlotDetail =
            UiString.Define("slot_detail", "Floor {0} - {1}", "Floor 99 - 999h 59m");

        // The card's own gold figure, sitting beside a number badge and two
        // lines of detail that already say which slot it is -- so this is a
        // bare amount, not a sentence that names the slot again. Same
        // template MapGold uses, kept as its own entry rather than reused
        // across two unrelated screens' names.
        public static readonly UiString SlotGold =
            UiString.Define("slot_gold", "{0} GOLD", "99999 GOLD");

        // What an EMPTY slot invites in the CHOOSE list specifically -- not in
        // Manage Saves, which still just says "Empty" because deleting is not
        // an invitation to start anything. "Empty" is accurate and does
        // nothing; this is the same fact stated as the thing an empty slot is
        // actually FOR.
        public static readonly UiString NewDescent = UiString.Define("new_descent", "New Descent");

        // --- destructive actions -------------------------------------------
        //
        // Reached from the slot list itself now, not from a separate Options
        // screen -- ManageSaves serves as both the button that opens it and
        // the panel's own heading, the same one-entry-two-uses shape SlotButton
        // already has.
        public static readonly UiString ManageSaves =
            UiString.Define("manage_saves", "Manage Saves");
        public static readonly UiString ManageSavesWarning =
            UiString.Define("manage_saves_warning",
                "Deleting a slot removes its run, its wallet and everything unlocked in it. There is no way back.");
        public static readonly UiString Delete = UiString.Define("delete", "Delete");
        public static readonly UiString ConfirmDelete =
            UiString.Define("confirm_delete", "Delete slot {0}? This cannot be undone.",
                            "Delete slot 5? This cannot be undone.");

        // --- combat ---------------------------------------------------------
        public static readonly UiString CommandTitle = UiString.Define("command_title", "C O M M A N D");
        public static readonly UiString Back = UiString.Define("back", "BACK");

        // --- hub --------------------------------------------------------------
        public static readonly UiString HubTitle = UiString.Define("hub_title", "DIVINE PRINCIPALITY");
        public static readonly UiString HubSubtitle = UiString.Define("hub_subtitle", "BETWEEN RUNS");
        public static readonly UiString HubTalents = UiString.Define("hub_talents", "TALENTS");
        public static readonly UiString HubPrincipality = UiString.Define("hub_principality", "PRINCIPALITY");
        public static readonly UiString HubCharacterSheet = UiString.Define("hub_character_sheet", "CHARACTER\nSHEET");
        public static readonly UiString HubRelics = UiString.Define("hub_relics", "RELICS");
        public static readonly UiString HubMainMenu = UiString.Define("hub_main_menu", "Main Menu");

        // --- the fight screen -------------------------------------------------
        //
        // The verbs are letterspaced by hand, as v1 authored them. That spacing
        // is typography, not data, which is why it belongs in the manifest text
        // and not in a formatter somewhere.
        public static readonly UiString VerbAttack = UiString.Define("verb_attack", "ATTACK");
        public static readonly UiString VerbSkill = UiString.Define("verb_skill", "SKILL");
        public static readonly UiString VerbItem = UiString.Define("verb_item", "ITEM");
        public static readonly UiString VerbMove = UiString.Define("verb_move", "MOVE");

        public static readonly UiString HotkeyOne = UiString.Define("hotkey_1", "1");
        public static readonly UiString HotkeyTwo = UiString.Define("hotkey_2", "2");
        public static readonly UiString HotkeyThree = UiString.Define("hotkey_3", "3");
        public static readonly UiString HotkeyFour = UiString.Define("hotkey_4", "4");
        public static readonly UiString HotkeyEscape = UiString.Define("hotkey_escape", "ESC");

        // A caret on a verb that opens a submenu, and nothing on one that acts
        // immediately. Two entries rather than one conditional, so the audit can
        // measure the widest either can ever be.
        public static readonly UiString VerbNestCaret = UiString.Define("verb_nest_caret", ">");

        public static readonly UiString EnemiesHeading = UiString.Define("enemies_heading", "E N E M I E S");
        public static readonly UiString WoolHeading = UiString.Define("wool_heading", "WOOL");
        public static readonly UiString HpTag = UiString.Define("hp_tag", "HP");
        public static readonly UiString MpTag = UiString.Define("mp_tag", "MP");
        public static readonly UiString Continue = UiString.Define("continue", "Continue");

        // Plate templates. v1 formatted these inline in FightController, in five
        // places, with the slash and spacing retyped each time.
        public static readonly UiString HealthValue =
            UiString.Define("health_value", "{0}/{1}", "9999/9999");
        public static readonly UiString SignatureValue =
            UiString.Define("signature_value", "{0}/{1}", "16/16");

        // THE SAME NUMBERS WITH THEIR TAG FOLDED IN, for the roster
        // mini-plate, where the value is drawn ON the bar and there is no room
        // beside it for a separate "HP" label the way the party plate has.
        // Two entries rather than one "{0} {1}/{2}" with the tag passed in:
        // the tag is authored UI copy, not data, and threading it through the
        // controller would put the literal "HP" back in Core -- which is the
        // whole thing UiStrings exists to stop.
        public static readonly UiString HpValueTagged =
            UiString.Define("hp_value_tagged", "HP {0}/{1}", "HP 9999/9999");
        public static readonly UiString MpValueTagged =
            UiString.Define("mp_value_tagged", "MP {0}/{1}", "MP 9999/9999");

        // The roster's signature line -- "Wool 3/10". The NAME is content
        // (characters.json's own resource display name), so it is a
        // parameter here rather than a second per-character entry; the
        // sample is the longest resource name on the roster plus a
        // three-digit pool, which is what E1 measures the 10pt box against.
        public static readonly UiString SignatureNamedValue =
            UiString.Define("signature_named_value", "{0} {1}/{2}", "Moonlight 999/999");

        public static readonly UiString StandingCount =
            UiString.Define("standing_count", "{0} STANDING", "99 STANDING");

        // LevelAndRole ("LV1 UTILITY") is GONE, Phase C1 -- the party plate's
        // PartyClass row it painted was removed in B2 (FightScreen.
        // BuildPartyPlate's own note); this was its last reader
        // (FightController.Hud.cs's RefreshPartyPlate).

        // Submenu chrome. The hint is the one line that tells a player the
        // column can be closed at all.
        public static readonly UiString SubmenuSkillsTitle = UiString.Define("submenu_skills_title", "S K I L L S");
        public static readonly UiString SubmenuItemsTitle = UiString.Define("submenu_items_title", "I T E M S");
        public static readonly UiString SubmenuMoveTitle = UiString.Define("submenu_move_title", "M O V E");
        public static readonly UiString SubmenuElementTitle = UiString.Define("submenu_element_title", "E L E M E N T");
        public static readonly UiString SubmenuHint = UiString.Define("submenu_hint", "ESC TO GO BACK");

        // Detail column labels.
        public static readonly UiString DetailKindSkill = UiString.Define("detail_kind_skill", "SKILL");
        public static readonly UiString DetailStatCost = UiString.Define("detail_stat_cost", "COST");
        public static readonly UiString DetailStatPower = UiString.Define("detail_stat_power", "POWER");
        public static readonly UiString DetailStatTarget = UiString.Define("detail_stat_target", "TARGET");
        public static readonly UiString DetailStatEffect = UiString.Define("detail_stat_effect", "EFFECT");
        public static readonly UiString DetailStatScaling = UiString.Define("detail_stat_scaling", "SCALES");

        // The way out of targeting. It reads ESC because that is what the row
        // it replaced said, and because a player who has learned that key in
        // every other menu should not have to unlearn it here -- even though
        // the fight scene's Escape belongs to the system menu and this is a
        // click. Named honestly rather than promising a key that does something
        // else would be the alternative, and "CANCEL" alone loses the mnemonic.
        public static readonly UiString TargetCancel = UiString.Define("fight.target_cancel", "CANCEL");

        public static readonly UiString TargetPrompt =
            UiString.Define("target_prompt", "Choose a target for {0}.", "Choose a target for Boulder Slam.");

        // The rework's group-target wording -- revives what was, until the
        // group-target confirm fix, dead copy: nothing routed an AllEnemies
        // skill through Target depth for this to ever be read. It is reachable
        // now, so it is worded distinctly from the single-target prompt above:
        // there is genuinely nothing to aim at, only something to confirm.
        public static readonly UiString TargetPromptGroup =
            UiString.Define("target_prompt_group", "{0} — confirm on any enemy plate.",
                "Boulder Slam — confirm on any enemy plate.");

        // TransformStripTurns/TransformStripPermanent are GONE with the strip
        // itself (2026-09-09, the HUD-column pass). A transformation IS a
        // status on the character, so it reads out through the badge row on
        // the party plate and the roster card like every other one, and its
        // "X turns"/"permanent" wording now lives in StatusHud.TransformRow's
        // tooltip beside the twelve status tooltips it belongs with -- a
        // hover string, not a label, so it is not a UiStrings entry at all.
        // The 36px the strip reserved is what paid for the roster cards'
        // extra rows; see FightScreen's own RosterPlateH note.

        // ---- the overarching menu ------------------------------------------
        //
        // Labels only. What each tab CONTAINS is a design job; these exist so
        // the bar can be built and measured before any of it lands.
        // ONE label where there were two. The dossier was always both halves,
        // so a separate Inventory tab was a second door onto the same room.
        public static readonly UiString SystemTabCharacterInventory =
            UiString.Define("system.tab.character_inventory", "CHARACTER & INVENTORY");
        public static readonly UiString SystemTabParty =
            UiString.Define("system.tab.party", "PARTY");
        public static readonly UiString SystemTabFloorMap =
            UiString.Define("system.tab.floor_map", "FLOOR MAP");
        public static readonly UiString SystemTabRunStats =
            UiString.Define("system.tab.run_stats", "RUN STATISTICS");
        public static readonly UiString SystemTabOptions =
            UiString.Define("system.tab.options", "OPTIONS");
        public static readonly UiString SystemTabMainMenu =
            UiString.Define("system.tab.main_menu", "MAIN MENU");
        // NOT parameterised. It was "{0} - content to come" with the tab name
        // as the argument, and nothing ever supplied one -- so every pane
        // rendered the manifest's SAMPLE text and all four read "OPTIONS -
        // content to come" whichever tab was open. A screenshot caught it; the
        // behaviour tests could not, because they assert which pane is showing
        // and not what it says. The bar overhead already names the tab, so the
        // pane never needed to.
        public static readonly UiString SystemPlaceholder =
            UiString.Define("system.placeholder", "CONTENT TO COME");
        public static readonly UiString SystemMenuTitle =
            UiString.Define("system.title", "DIVINE PRINCIPALITY");
        public static readonly UiString SystemBetweenDescents =
            UiString.Define("system.between_descents", "BETWEEN DESCENTS");
        public static readonly UiString SystemEscHint =
            UiString.Define("system.esc_hint", "ESC");
        public static readonly UiString SystemClose =
            UiString.Define("system.close", "X");

        // ---- the Options pane ----------------------------------------------
        public static readonly UiString OptionsAudio = UiString.Define("options.audio", "AUDIO");
        public static readonly UiString OptionsSound = UiString.Define("options.sound", "Sound");
        public static readonly UiString OptionsMusic = UiString.Define("options.music", "Music");
        // The label says what the setting does, which right now is "is stored".
        // GameSettings' own header asks for exactly this rather than a control
        // that looks live and is not.
        public static readonly UiString OptionsMusicNote =
            UiString.Define("options.music.note", "Stored - no music yet");
        public static readonly UiString OptionsDisplay = UiString.Define("options.display", "DISPLAY");
        public static readonly UiString OptionsResolution = UiString.Define("options.resolution", "Resolution");
        public static readonly UiString OptionsWindow = UiString.Define("options.window", "Window");
        public static readonly UiString OptionsFrameLimit = UiString.Define("options.frame_limit", "Frame limit");
        public static readonly UiString OptionsGameplay = UiString.Define("options.gameplay", "GAMEPLAY");
        public static readonly UiString OptionsBattleSpeed =
            UiString.Define("options.battle_speed", "Battle speed");
        // "{0}x", sample "1.5x" -- docs/PLAN_BATTLE_SPEED.md's own sample
        // rule. {0} is BattleSpeed.Preset.DisplayNumber, already formatted
        // with "0.##" so "1" prints "1x" rather than "1.00x"; this template
        // only adds the "x".
        public static readonly UiString OptionsBattleSpeedValue =
            UiString.Define("options.battle_speed.value", "{0}x", "1.5x");
        // Distinct from every other row's footer claim: contract 4 means a
        // beat already under way finishes on the pace it started at, so
        // "applies immediately" would describe a beat that has not opened
        // yet, not the one on screen when the player steps this row.
        public static readonly UiString OptionsBattleSpeedNote =
            UiString.Define("options.battle_speed.note", "Applies from the next action");
        public static readonly UiString OptionsRestoreDefaults =
            UiString.Define("options.restore_defaults", "RESTORE DEFAULTS");
        // Reworded, docs/PLAN_BATTLE_SPEED.md: the blanket claim stopped
        // being true the moment one row on this screen no longer retimes
        // whatever beat is already playing.
        public static readonly UiString OptionsAppliesImmediately =
            UiString.Define("options.applies",
                "Changes apply immediately. Battle speed applies from the next action.");

        // ---- the Run statistics pane ---------------------------------------
        //
        // Seventeen figures and no header. What is ABSENT is the design's
        // elapsed / days / turns -- nothing counts them -- and its floor/room
        // header, which the lintel already prints two inches above this pane.
        // See RunStatRows.
        public static readonly UiString RunStatBattle = UiString.Define("runstat.battle", "BATTLE");
        public static readonly UiString RunStatFold = UiString.Define("runstat.fold", "THE FOLD");
        public static readonly UiString RunStatSpoils = UiString.Define("runstat.spoils", "SPOILS");

        public static readonly UiString RunStatDamageDealt =
            UiString.Define("runstat.damage_dealt", "Damage dealt");
        // The two below are the breakdown of the row above them, which is why
        // they are one word each: read down the card they are a total and its
        // parts, and a card that says "Physical damage dealt" under "Damage
        // dealt" reads as two unrelated totals.
        public static readonly UiString RunStatDamagePhysical =
            UiString.Define("runstat.damage_physical", "Physical");
        public static readonly UiString RunStatDamageOther =
            UiString.Define("runstat.damage_other", "Other");
        public static readonly UiString RunStatDamageTaken =
            UiString.Define("runstat.damage_taken", "Damage taken");
        public static readonly UiString RunStatHealed = UiString.Define("runstat.healed", "Healing done");
        public static readonly UiString RunStatShielded =
            UiString.Define("runstat.shielded", "Shielding raised");
        public static readonly UiString RunStatKills = UiString.Define("runstat.kills", "Foes felled");
        public static readonly UiString RunStatTimesDowned =
            UiString.Define("runstat.times_downed", "Times downed");

        public static readonly UiString RunStatFloor = UiString.Define("runstat.floor", "Floor reached");
        public static readonly UiString RunStatRoomsCleared =
            UiString.Define("runstat.rooms_cleared", "Rooms cleared");
        public static readonly UiString RunStatDeepestRoom =
            UiString.Define("runstat.deepest_room", "Deepest room");
        public static readonly UiString RunStatBosses = UiString.Define("runstat.bosses", "Bosses felled");

        public static readonly UiString RunStatGoldHeld = UiString.Define("runstat.gold_held", "Gold held");
        public static readonly UiString RunStatGoldEarned =
            UiString.Define("runstat.gold_earned", "Gold earned");
        public static readonly UiString RunStatExpEarned =
            UiString.Define("runstat.exp_earned", "Experience earned");
        public static readonly UiString RunStatRelics = UiString.Define("runstat.relics", "Relics carried");
        public static readonly UiString RunStatPack = UiString.Define("runstat.pack", "Items in pack");

        // ---- the Main menu pane --------------------------------------------
        //
        // NOTHING HERE FIRES ON A SINGLE PRESS -- the design's rule, and these
        // strings are how it is visible rather than merely true. The two exits
        // swap their own label to ExitConfirm on the first press, which is why
        // both carry that longer text as their audit sample: E1 has to measure
        // the widest thing the box will ever hold, and a swapped label is
        // exactly a value the build cannot see.
        public static readonly UiString ExitToTitle = UiString.Define(
            "exit.to_title", "RETURN TO TITLE", "PRESS AGAIN TO CONFIRM");
        public static readonly UiString ExitQuit = UiString.Define(
            "exit.quit", "QUIT TO DESKTOP", "PRESS AGAIN TO CONFIRM");
        public static readonly UiString ExitConfirm =
            UiString.Define("exit.confirm", "PRESS AGAIN TO CONFIRM");

        // Both notes say what happens TO THE RUN, because both of these end a
        // descent and neither button's own words say so. The design asked for
        // "back to title (run stays as it is)"; RunManager's rule is that
        // leaving a descent kills the run -- no exceptions -- and the hub's own
        // title button and the main menu's quit both enforce it. Two title
        // doors with different consequences would be worse than one honest one.
        public static readonly UiString ExitToTitleNote =
            UiString.Define("exit.to_title.note", "Ends the descent. Embers it earned are kept.");
        public static readonly UiString ExitQuitNote =
            UiString.Define("exit.quit.note", "Saves and settles the run, then closes the game.");

        public static readonly UiString ExitAbandonHeading =
            UiString.Define("exit.abandon.heading", "ABANDON THE DESCENT");
        public static readonly UiString ExitAbandonNote =
            UiString.Define("exit.abandon.note", "The floor is lost. Embers already earned are kept.");
        public static readonly UiString ExitAbandonHold =
            UiString.Define("exit.abandon.hold", "HOLD TO ABANDON");

        // ---- the character dossier -----------------------------------------
        public static readonly UiString OverlayLoadout = UiString.Define("dossier.loadout", "LOADOUT");
        public static readonly UiString OverlayAttributes = UiString.Define("dossier.attributes", "ATTRIBUTES");
        public static readonly UiString OverlayXp = UiString.Define("dossier.xp", "XP");
        public static readonly UiString OverlaySkills = UiString.Define("dossier.skills", "Skills");
        public static readonly UiString OverlayPack = UiString.Define("dossier.pack", "The Pack");
        public static readonly UiString OverlayPackTitle = UiString.Define("dossier.pack_title", "THE PACK");
        public static readonly UiString OverlayPackClose = UiString.Define("dossier.pack_close", "CLOSE");
        public static readonly UiString OverlayCarried = UiString.Define("dossier.carried", "Carried");
        public static readonly UiString OverlayPrev = UiString.Define("dossier.prev", "<");
        public static readonly UiString OverlayNext = UiString.Define("dossier.next", ">");
        public static readonly UiString OverlayTwoHanded = UiString.Define("dossier.two_handed", "TWO-HANDED");
        // How the pack is ORDERED. Three keys, not four: rarity is derived
        // from tier, so a rarity button would draw the tier button's list.
        public static readonly UiString PackSortTier = UiString.Define("dossier.sort_tier", "TIER");
        public static readonly UiString PackSortPlus = UiString.Define("dossier.sort_plus", "+");
        public static readonly UiString PackSortName = UiString.Define("dossier.sort_name", "NAME");

        public static readonly UiString PackFilterAll = UiString.Define("dossier.filter_all", "All");
        public static readonly UiString PackFilterArmour = UiString.Define("dossier.filter_armour", "Armour");
        public static readonly UiString PackFilterWeapons = UiString.Define("dossier.filter_weapons", "Weapons");
        public static readonly UiString PackFilterSalves = UiString.Define("dossier.filter_salves", "Salves");

        // ---- the Party pane --------------------------------------------------
        //
        // Seat labels are FRONT/MIDDLE/REAR, not the handoff's role-neutral
        // "Position 1/2/3" -- this game's positions ARE mechanically different
        // (the front-rank rule, landed 2026-09-07), and the handoff's own
        // escape clause says to rename them the moment that is true. See
        // docs/handoffs/party_screen/DECISIONS.md.
        public static readonly UiString PartyBannerDefault =
            UiString.Define("party.banner.default", "Select a companion, then choose a position.");
        public static readonly UiString PartyBannerSelected =
            UiString.Define("party.banner.selected", "{0} selected — choose a position.",
                "Bjorn selected - choose a position.");
        public static readonly UiString PartyCancel = UiString.Define("party.cancel", "Cancel");
        public static readonly UiString PartySendToBench = UiString.Define("party.send_to_bench", "Send to bench");

        // Three modes, not two -- Camp (hub, swap freely), Run (map, reposition
        // only) and ViewOnly (fight, the live encounter owns the order so this
        // pane changes nothing and is not one of these three pill states; a
        // fight never opens this pane in a way that needs its own copy here).
        public static readonly UiString PartyStatusCamp =
            UiString.Define("party.status.camp", "AT CAMP — Swap freely.");
        public static readonly UiString PartyStatusRun =
            UiString.Define("party.status.run", "IN A RUN — Reposition only.");
        public static readonly UiString PartyStatusFight =
            UiString.Define("party.status.fight", "IN A FIGHT — Formation fixed.");

        public static readonly UiString PartyFormationHeading =
            UiString.Define("party.formation.heading", "FORMATION");
        public static readonly UiString PartyFormationSubtitle =
            UiString.Define("party.formation.subtitle",
                "The front rank takes the enemies' blows. Some skills reach only from the rank they name.");
        public static readonly UiString PartyFilledCount =
            UiString.Define("party.formation.filled_count", "{0}/3 positions filled", "3/3 positions filled");
        public static readonly UiString PartyFacingRibbon =
            UiString.Define("party.formation.facing_ribbon", "FACING THE ENEMY ->");

        public static readonly UiString PartySeatFront = UiString.Define("party.seat.front", "FRONT");
        public static readonly UiString PartySeatMiddle = UiString.Define("party.seat.middle", "MIDDLE");
        public static readonly UiString PartySeatRear = UiString.Define("party.seat.rear", "REAR");

        public static readonly UiString PartyBadgeReplace =
            UiString.Define("party.badge.replace", "Replace {0}", "Replace Bjorn");
        public static readonly UiString PartyBadgeSwap =
            UiString.Define("party.badge.swap", "Swap with {0}", "Swap with Bjorn");
        public static readonly UiString PartyBadgePlace =
            UiString.Define("party.badge.place", "Place {0} here", "Place Bjorn here");

        public static readonly UiString PartyRosterHeading = UiString.Define("party.roster.heading", "ROSTER");
        public static readonly UiString PartyCardTagInParty =
            UiString.Define("party.card.tag_in_party", "In party · {0}", "In party · Bjorn");
        public static readonly UiString PartyCardTagArtPending =
            UiString.Define("party.card.tag_art_pending", "Art pending");
        public static readonly UiString PartyCardTagBenched = UiString.Define("party.card.tag_benched", "Benched");
        public static readonly UiString PartySelectedTag = UiString.Define("party.card.selected_tag", "SELECTED");

        public static readonly UiString PartyScrimLocked = UiString.Define("party.scrim.locked", "Locked this run");
        public static readonly UiString PartyScrimClosed = UiString.Define("party.scrim.closed", "No seat yet");

        // ---- the toast, one entry per outcome kind ---------------------------
        public static readonly UiString PartyToastPlaced =
            UiString.Define("party.toast.placed", "{0} takes the {1} position.",
                "Bjorn takes the front position.");
        public static readonly UiString PartyToastMoved =
            UiString.Define("party.toast.moved", "{0} takes the {1} position.",
                "Bjorn takes the front position.");
        public static readonly UiString PartyToastSwapped =
            UiString.Define("party.toast.swapped", "Swapped {0} and {1}.",
                "Swapped Bjorn and Bjorn.");
        public static readonly UiString PartyToastReplaced =
            UiString.Define("party.toast.replaced", "{1} steps aside for {0}.",
                "Bjorn steps aside for Bjorn.");
        public static readonly UiString PartyToastBenched =
            UiString.Define("party.toast.benched", "{0} returns to the bench.",
                "Bjorn returns to the bench.");
        public static readonly UiString PartyToastFormationFixedInFight =
            UiString.Define("party.toast.formation_fixed_in_fight", "The formation is fixed during a fight.");
        public static readonly UiString PartyToastSeatNotOpen =
            UiString.Define("party.toast.seat_not_open", "That seat is not open yet.");
        public static readonly UiString PartyToastSeatLocked =
            UiString.Define("party.toast.seat_locked", "{0} is locked in this run.",
                "Bjorn is locked in this run.");
        public static readonly UiString PartyToastBenchedDuringRun =
            UiString.Define("party.toast.benched_during_run", "New companions join between runs.");
        public static readonly UiString PartyToastRepositionOnlyDuringRun =
            UiString.Define("party.toast.reposition_only_during_run", "Mid-run you can only reposition.");
        public static readonly UiString PartyToastPartyNeverEmpty =
            UiString.Define("party.toast.party_never_empty", "The party can never be empty.");

        // Every entry declared above, derived rather than restated.
        //
        // This was a hand-maintained 92-line second copy of the 317 names, and
        // the copy was the whole problem: an entry left out of it was invisible
        // to the AuditSample check and to the unique-key check, so the one place
        // that could catch a bad string silently skipped it. Reflection over the
        // class's own public static readonly UiString fields cannot skip one --
        // the declaration IS the registration, and there is no second list to
        // forget. System.Reflection is BCL, so Domain stays engine-free.
        //
        // Static, so the walk happens once per domain load rather than per test.
        public static readonly UiString[] All = Declared();

        private static UiString[] Declared()
        {
            var fields = typeof(UiStrings).GetFields(BindingFlags.Public | BindingFlags.Static);
            var found = new List<UiString>(fields.Length);
            foreach (var f in fields)
            {
                // Exact type, not assignability: All itself is a UiString[] and
                // must not enumerate itself.
                if (f.FieldType == typeof(UiString)) found.Add((UiString)f.GetValue(null));
            }
            return found.ToArray();
        }
    }
}

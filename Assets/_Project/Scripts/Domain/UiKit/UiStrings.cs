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
        public static readonly UiString TalentPrev = UiString.Define("talent.prev", "<");
        public static readonly UiString TalentNext = UiString.Define("talent.next", ">");
        public static readonly UiString TalentBack = UiString.Define("talent.back", "Back");
        public static readonly UiString TalentPath =
            UiString.Define("talent.path", "CONSTELLATION {0} OF {1}   -   {2} KINDLED", "CONSTELLATION 3 OF 3   -   21 KINDLED");

        // ---- the character overlay ------------------------------------------

        public static readonly UiString OverlayCount =
            UiString.Define("overlay.count", "x{0}", "x99");
        public static readonly UiString OverlayPlus =
            UiString.Define("overlay.plus", "+{0}", "+10");
        public static readonly UiString OverlayPage =
            UiString.Define("overlay.page", "PAGE {0} OF {1}", "PAGE 99 OF 99");
        public static readonly UiString OverlayEquip = UiString.Define("overlay.equip", "EQUIP");
        public static readonly UiString OverlayUnequip = UiString.Define("overlay.unequip", "UNEQUIP");
        public static readonly UiString OverlayCannotWear = UiString.Define("overlay.cannot_wear", "CAN'T WEAR");
        // An empty bag has to SAY it is empty. Twenty hidden cells and a lone
        // "PAGE 1 OF 1" floating over dead space reads as a grid that failed to
        // load, not as a bag with nothing in it.
        public static readonly UiString OverlayBagEmpty =
            UiString.Define("overlay.bag_empty", "YOU ARE CARRYING NOTHING");

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

        // ---- character sheet ---------------------------------------------------

        // The two panes. The sheet deliberately does NOT show the bag: a
        // paperdoll and a twenty-cell grid on one surface is what left the
        // slot cells fighting the figure for space.
        public static readonly UiString SheetTabCharacter =
            UiString.Define("sheet.tab.character", "Character");
        public static readonly UiString SheetTabInventory =
            UiString.Define("sheet.tab.inventory", "Inventory");

        // Worst-case sample is four digits: resistances are small today but a
        // late-run stack of gear is what the box has to still fit.
        public static readonly UiString SheetStatValue =
            UiString.Define("sheet.stat.value", "{0}", "9999");

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
        public static readonly UiString StatDefence = UiString.Define("stat.defence", "Defence");
        public static readonly UiString StatSpeed = UiString.Define("stat.speed", "Speed");
        public static readonly UiString StatManaRegen = UiString.Define("stat.manaregen", "Mana Regen");
        public static readonly UiString StatPhysicalResistance =
            UiString.Define("stat.physres", "Physical DEF");
        public static readonly UiString StatMagicalResistance =
            UiString.Define("stat.magres", "Magical DEF");

        // Shown on the action button, and on the compare box, when a fight is
        // in progress. Gear is locked for the duration of a battle -- being
        // able to re-plate mid-swing would make every fight a loadout puzzle
        // rather than a fight.
        public static readonly UiString OverlayLockedInFight =
            UiString.Define("overlay.locked", "Locked in battle");

        public static readonly UiString Play = UiString.Define("play", "Play");
        public static readonly UiString Options = UiString.Define("options", "Options");
        public static readonly UiString Exit = UiString.Define("exit", "Exit");
        public static readonly UiString OptionsTitle = UiString.Define("options_title", "Options");
        public static readonly UiString Close = UiString.Define("close", "Close");
        public static readonly UiString Cancel = UiString.Define("cancel", "Cancel");

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
        public static readonly UiString DebugAdd = UiString.Define("debug.add", "ADD");
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
        public static readonly UiString DraftTake = UiString.Define("draft.take", "TAKE");
        public static readonly UiString DraftNoRelics =
            UiString.Define("draft.none", "NOTHING STIRS IN THE VAULT");
        public static readonly UiString DraftDescend = UiString.Define("draft.descend", "Descend");
        // Rarity is shown as a word rather than only as a colour: a band is a
        // fact about the relic, and colour alone excludes anyone who cannot
        // separate the six.
        public static readonly UiString DraftRarity =
            UiString.Define("draft.rarity", "{0}", "ULTRA-RARE");

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
        public static readonly UiString SlotEmpty =
            UiString.Define("slot_empty", "Slot {0}: Empty", "Slot 5: Empty");

        // One entry, not "Slot {0}: {1}" wrapping "{0} gold, {1} relics". v1 had
        // this wording living inside SaveSlotLabel, which is the same thing the
        // wallet line did wrong - authored copy outside the manifest, where the
        // Play screen and the Options screen drifted into describing the same
        // slot differently.
        public static readonly UiString SlotFilled =
            UiString.Define("slot_filled", "Slot {0}: {1} gold", "Slot 5: 999999 gold");

        public static readonly UiString SlotButton =
            UiString.Define("slot_button", "Slot {0}", "Slot 5");

        // --- destructive actions -------------------------------------------
        public static readonly UiString ResetProgressHeader =
            UiString.Define("reset_progress_header", "Reset Progress");
        public static readonly UiString Delete = UiString.Define("delete", "Delete");
        public static readonly UiString ConfirmDelete =
            UiString.Define("confirm_delete", "Delete slot {0}? This cannot be undone.",
                            "Delete slot 5? This cannot be undone.");

        // --- combat ---------------------------------------------------------
        public static readonly UiString CommandTitle = UiString.Define("command_title", "C O M M A N D");
        public static readonly UiString Attack = UiString.Define("attack", "ATTACK");
        public static readonly UiString Back = UiString.Define("back", "BACK");

        // --- character select -----------------------------------------------
        public static readonly UiString ConfirmSquad = UiString.Define("confirm_squad", "Confirm Squad");

        // --- hub --------------------------------------------------------------
        public static readonly UiString HubTitle = UiString.Define("hub_title", "DIVINE PRINCIPALITY");
        public static readonly UiString HubSubtitle = UiString.Define("hub_subtitle", "BETWEEN RUNS");
        public static readonly UiString HubTalents = UiString.Define("hub_talents", "TALENTS");
        public static readonly UiString HubPrincipality = UiString.Define("hub_principality", "PRINCIPALITY");
        public static readonly UiString HubCharacterSheet = UiString.Define("hub_character_sheet", "CHARACTER\nSHEET");
        public static readonly UiString HubRelics = UiString.Define("hub_relics", "RELICS");
        public static readonly UiString HubStartRun = UiString.Define("hub_start_run", "START RUN");
        public static readonly UiString HubMainMenu = UiString.Define("hub_main_menu", "Main Menu");

        // --- the fight screen -------------------------------------------------
        //
        // The verbs are letterspaced by hand, as v1 authored them. That spacing
        // is typography, not data, which is why it belongs in the manifest text
        // and not in a formatter somewhere.
        public static readonly UiString VerbAttack = UiString.Define("verb_attack", "ATTACK");
        public static readonly UiString VerbSkill = UiString.Define("verb_skill", "SKILL");
        public static readonly UiString VerbItem = UiString.Define("verb_item", "ITEM");
        public static readonly UiString VerbRun = UiString.Define("verb_run", "RUN");
        public static readonly UiString VerbHoldBack = UiString.Define("verb_hold_back", "HOLD BACK");

        public static readonly UiString HotkeyOne = UiString.Define("hotkey_1", "1");
        public static readonly UiString HotkeyTwo = UiString.Define("hotkey_2", "2");
        public static readonly UiString HotkeyThree = UiString.Define("hotkey_3", "3");
        public static readonly UiString HotkeyFour = UiString.Define("hotkey_4", "4");
        public static readonly UiString HotkeyFive = UiString.Define("hotkey_5", "5");
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
        public static readonly UiString GuardValue =
            UiString.Define("guard_value", "GUARD {0}/{1}", "GUARD 999/999");
        public static readonly UiString SignatureValue =
            UiString.Define("signature_value", "{0}/{1}", "16/16");
        public static readonly UiString StandingCount =
            UiString.Define("standing_count", "{0} STANDING", "99 STANDING");
        public static readonly UiString LevelAndRole =
            UiString.Define("level_and_role", "LV{0} {1}", "LV20 ASSASSIN");

        // Submenu chrome. The hint is the one line that tells a player the
        // column can be closed at all.
        public static readonly UiString SubmenuSkillsTitle = UiString.Define("submenu_skills_title", "S K I L L S");
        public static readonly UiString SubmenuItemsTitle = UiString.Define("submenu_items_title", "I T E M S");
        public static readonly UiString SubmenuHint = UiString.Define("submenu_hint", "ESC TO GO BACK");

        // Detail column labels.
        public static readonly UiString DetailKindSkill = UiString.Define("detail_kind_skill", "SKILL");
        public static readonly UiString DetailKindItem = UiString.Define("detail_kind_item", "ITEM");
        public static readonly UiString DetailStatCost = UiString.Define("detail_stat_cost", "COST");
        public static readonly UiString DetailStatPower = UiString.Define("detail_stat_power", "POWER");
        public static readonly UiString DetailStatTarget = UiString.Define("detail_stat_target", "TARGET");
        public static readonly UiString DetailStatEffect = UiString.Define("detail_stat_effect", "EFFECT");

        public static readonly UiString TargetPrompt =
            UiString.Define("target_prompt", "Choose a target for {0}.", "Choose a target for Boulder Slam.");

        // ---- the overarching menu ------------------------------------------
        //
        // Labels only. What each tab CONTAINS is a design job; these exist so
        // the bar can be built and measured before any of it lands.
        public static readonly UiString SystemTabCharacter =
            UiString.Define("system.tab.character", "CHARACTER");
        public static readonly UiString SystemTabInventory =
            UiString.Define("system.tab.inventory", "INVENTORY");
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

        // Every entry declared above, for the manifest tests to sweep. Kept
        // beside the entries deliberately: a new string that someone forgets to
        // add here is invisible to the AuditSample check, so the test that walks
        // this array also asserts it is not obviously short.
        public static readonly UiString[] All =
        {
            WalletSummary,
            SystemTabCharacter, SystemTabInventory, SystemTabOptions, SystemTabMainMenu,
            SystemPlaceholder,
            Play, Options, Exit, OptionsTitle, Close, Cancel,
            ReckoningTitle, ReckoningExperience, ReckoningGold, ReckoningChooseOne,
            ReckoningLevel, ReckoningLevelUp, ReckoningExpGain, ReckoningDowned,
            ReckoningOfferMeta, ReckoningTaken,
            ReckoningTabSpoils, ReckoningTabRelics, ReckoningTabTally,
            ReckoningNoRelics, ReckoningRelicHeld, ReckoningTallyHeading,
            ReckoningTallyLine, ReckoningKills,
            DefeatTitle, DefeatLost, DefeatKept, DefeatGoldLost, DefeatEmbers, DefeatEmbersNone,
            DefeatDepth, DefeatExp, DefeatStatsHeading, DefeatStatLine, DefeatToHub, DefeatInspect,
            DraftTitle, DraftSubtitle, DraftTake, DraftNoRelics, DraftDescend, DraftRarity,
            GlossaryTitle, GlossaryCount, GlossaryPage, GlossaryEmpty,
            GlossaryLocked, GlossaryLockedBy, GlossaryPick,
            DebugTitle, DebugGiveGold, DebugGiveEmbers, DebugGiveOneEmber, DebugAdd,
            DebugFilterAll, DebugFilterConsumable, DebugFilterWeapon, DebugFilterEquipment,
            DebugRow, DebugPage,
            SlotEmpty, SlotFilled, SlotButton,
            ResetProgressHeader, Delete, ConfirmDelete,
            CommandTitle, Attack, Back,
            ConfirmSquad,
            HubTitle, HubSubtitle, HubTalents, HubPrincipality, HubCharacterSheet,
            HubRelics, HubStartRun, HubMainMenu,
            VerbAttack, VerbSkill, VerbItem, VerbRun, VerbHoldBack,
            HotkeyOne, HotkeyTwo, HotkeyThree, HotkeyFour, HotkeyFive, HotkeyEscape,
            VerbNestCaret, EnemiesHeading, WoolHeading, HpTag, MpTag, Continue,
            HealthValue, GuardValue, SignatureValue, StandingCount, LevelAndRole,
            SubmenuSkillsTitle, SubmenuItemsTitle, SubmenuHint,
            DetailKindSkill, DetailKindItem,
            DetailStatCost, DetailStatPower, DetailStatTarget, DetailStatEffect,
            TargetPrompt,
            MapTitle, MapDepth, MapGold, MapAbandon, MapFog,
            MapRoomTreasure, MapRoomRest, MapRoomShop, MapRoomEvent,
            MapRoomItem, MapRoomEmpty,
            SheetTabCharacter, SheetTabInventory, SheetStatValue, OverlayLockedInFight,
            StatStrength, StatDexterity, StatConstitution, StatWisdom,
            StatIntelligence, StatCharisma,
            StatMaxHealth, StatAttack, StatDefence, StatSpeed, StatManaRegen,
            StatPhysicalResistance, StatMagicalResistance,
            HubWallet, HubBeginDescent, HubResumeFloor,
            OverlayCount, OverlayPlus, OverlayPage,
            OverlayEquip, OverlayUnequip, OverlayCannotWear, OverlayBagEmpty,
            TalentEmbers, TalentInvest, TalentTaken, TalentLocked, TalentNoEmbers,
            TalentPrev, TalentNext, TalentBack, TalentPath,
        };
    }
}

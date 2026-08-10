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

        // ---- the hub -------------------------------------------------------

        // Three currencies, not two. Embers is what talents actually cost and
        // the hub never showed it. WalletSummary is left alone -- other screens
        // still use the two-currency line.
        public static readonly UiString HubWallet =
            UiString.Define("hub.wallet", "Gold: {0}    Relics: {1}    Embers: {2}",
                "Gold: 999999    Relics: 99    Embers: 999");

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

        public static readonly UiString Play = UiString.Define("play", "Play");
        public static readonly UiString Options = UiString.Define("options", "Options");
        public static readonly UiString Exit = UiString.Define("exit", "Exit");
        public static readonly UiString OptionsTitle = UiString.Define("options_title", "Options");
        public static readonly UiString Close = UiString.Define("close", "Close");
        public static readonly UiString Cancel = UiString.Define("cancel", "Cancel");

        // --- save slots -----------------------------------------------------
        public static readonly UiString SlotEmpty =
            UiString.Define("slot_empty", "Slot {0}: Empty", "Slot 5: Empty");

        // One entry, not "Slot {0}: {1}" wrapping "{0} gold, {1} relics". v1 had
        // this wording living inside SaveSlotLabel, which is the same thing the
        // wallet line did wrong - authored copy outside the manifest, where the
        // Play screen and the Options screen drifted into describing the same
        // slot differently.
        public static readonly UiString SlotFilled =
            UiString.Define("slot_filled", "Slot {0}: {1} gold, {2} relics", "Slot 5: 999999 gold, 99 relics");

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

        // Every entry declared above, for the manifest tests to sweep. Kept
        // beside the entries deliberately: a new string that someone forgets to
        // add here is invisible to the AuditSample check, so the test that walks
        // this array also asserts it is not obviously short.
        public static readonly UiString[] All =
        {
            WalletSummary,
            Play, Options, Exit, OptionsTitle, Close, Cancel,
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
            MapTitle, MapDepth, MapGold, MapAbandon,
            HubWallet, HubBeginDescent, HubResumeFloor,
            TalentEmbers, TalentInvest, TalentTaken, TalentLocked, TalentNoEmbers,
            TalentPrev, TalentNext, TalentBack, TalentPath,
        };
    }
}

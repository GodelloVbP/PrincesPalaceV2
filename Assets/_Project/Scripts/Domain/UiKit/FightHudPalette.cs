namespace PrincesPalace.Domain.UiKit
{
    // The battle HUD's colours, as hex.
    //
    // These were `private static readonly Color` fields in v1's
    // SceneBuilder.Widgets.cs, which put them in the Editor assembly -- so the
    // runtime controller, which recolours the same elements as state changes
    // (a verb going active, a pip filling, a plate being targeted), could not
    // read them and restated the ones it needed. Hex strings in Domain are
    // reachable from both.
    //
    // Alpha is encoded in the eighth and last two digits, matching the rest of
    // the DSL. v1 wrote it as a separate float argument; folding it in is what
    // lets one constant be a complete colour rather than half of one.
    public static class FightHudPalette
    {
        // --- panels -----------------------------------------------------------
        public const string PanelViolet = "#1A1024E6";       // 0.90
        public const string PanelVioletDeep = "#120A1AEB";   // 0.92
        public const string PanelRed = "#1F0F14DE";          // 0.87
        public const string PanelGold = "#241610E8";         // 0.91
        public const string PanelPrimary = "#33190CE8";      // 0.91
        public const string PanelActive = "#583216F2";       // 0.95
        public const string RowQuiet = "#100918C7";          // 0.78
        public const string SubmenuRowFill = "#120A1AD1";    // 0.82
        public const string Track = "#0E070CD9";             // 0.85
        public const string TrackMp = "#080A14D9";           // 0.85

        // The skill list's scrollbar. Two tokens rather than reusing Track and
        // a border: a scrollbar is the one element on this screen whose whole
        // job is to be findable WITHOUT being read, so the thumb has to carry
        // more contrast against its groove than a bar fill does against a
        // plate. Both are quiet enough to disappear when the list fits, which
        // is when neither is drawn at all.
        public const string ScrollTrack = "#0B0612B8";        // 0.72
        public const string ScrollThumb = "#C8AAE68A";        // 0.54

        // --- borders ----------------------------------------------------------
        public const string Hairline = "#C8AAE638";          // 0.22

        // ---- the system menu's two additions --------------------------------
        //
        // Both arrived with the menu's design pass and were written straight
        // into four screens instead of here, which is how one token ends up
        // with five local names. They are tokens like any other.
        public const string CardFill = "#12091C8C";          // 0.55, a group's ground
        public const string HoverTint = "#C8AAE60F";         // 0.06, a row lighting under the pointer
        public const string BorderGold = "#E7B25CB3";        // 0.70
        public const string BorderPartyGold = "#E7B25C6B";   // 0.42
        public const string BorderEnemy = "#E0786E42";       // 0.26
        public const string BorderQuiet = "#B496D233";       // 0.20
        public const string BorderSub = "#C8AAE647";         // 0.28

        // --- text -------------------------------------------------------------
        public const string TextPrimary = "#F4EBFF";
        public const string TextSecondary = "#BFB0D4";
        public const string TextMuted = "#8A7AA0";
        public const string TextDisabled = "#7F6F98";
        public const string GoldLight = "#FFE0A8";
        public const string GoldText = "#FFD9A2";
        public const string TargetAmber = "#FFC45A";
        public const string RowNameText = "#E9DFF8";
        public const string PartyNameText = "#FFF3DE";
        public const string PartyClassText = "#C8A879";
        public const string BackRowText = "#A695BC";
        public const string QuietHotkey = "#6D5F85";
        public const string LoudHotkey = "#FFDCAA8C";        // 0.55

        // --- resources --------------------------------------------------------
        public const string HpBright = "#E07A62";
        public const string HpDeep = "#8E3226";
        public const string HpText = "#F0C8BC";
        public const string MpBright = "#7EA8E6";
        public const string MpDeep = "#3A5A9A";
        public const string MpText = "#C4D8F2";
        public const string MpPreview = "#C8E2FFE6";         // 0.90
        public const string EnemyName = "#F0DCD8";
        public const string EnemyHpText = "#E0A89C";
        public const string PipFilled = "#E8E0F7";
        public const string PipEmpty = "#0E0814B3";          // 0.70

        // --- enemy intent badges ----------------------------------------------
        //
        // The icons ship as WHITE silhouettes and are tinted here, which is the
        // whole reason a silhouette set was chosen over painted illustrations:
        // at badge size over a painted battlefield, colour has to carry the
        // meaning, and a fixed illustration cannot be recoloured to say
        // "this one heals" without repainting it.
        //
        // Deliberately borrowing the HUD's OWN reds and blues rather than a new
        // palette -- a badge that says damage should be the same red the HP bar
        // already uses, or the player has two colour languages to learn.
        public const string IntentAttack = "#FFC45A";        // TargetAmber: the ordinary threat
        public const string IntentPoison = "#8FD46A";
        public const string IntentStun = "#FFE98A";
        public const string IntentWeaken = "#E07A62";        // HpBright: it is coming off your health
        public const string IntentHeal = "#7FE0A0";
        public const string IntentShield = "#7EA8E6";        // MpBright: defensive, matches the mana blue
        public const string IntentSkill = "#C79BEE";

        // --- the fight's own panel art ----------------------------------------
        public const string TargetPromptFill = "#26160AE6";   // 0.90
        public const string TargetPromptBorder = "#FFC45A8C"; // 0.55
        public const string DetailBorder = "#C8AAE642";       // 0.26
    }
}

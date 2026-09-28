using PrincesPalace.Domain.Stats;

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

        // ---- the shop's two additions ---------------------------------------
        //
        // Read off the shop_v2 prototype's own stylesheet (docs/handoffs/
        // shop_v2/Shop Screen v2.dc.html): its `body { background: #0B0612 }`
        // and the `#E7B25C` its section captions are set in. Both are the
        // OPAQUE ends of ramps this palette already owns -- ScrollTrack is the
        // same violet-black at 0.72, BorderGold the same amber at 0.70 -- not
        // new hues, which is why they belong here and not in a shop-local
        // constant.
        //
        // Opacity is the whole point of the first one. The shop is drawn over
        // the run map and the design closes it off completely; the screen used
        // PanelViolet (0.90) before, which left the map legible underneath and
        // made the surface read as an overlay rather than as a room.
        public const string ShopGround = "#0B0612FF";        // 1.00
        public const string HeadingGold = "#E7B25CFF";       // 1.00

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
        // RED, because it is the one row in the list that leaves. It was
        // #A695BC -- the same muted violet as every other piece of secondary
        // text on the screen -- which was fine while BACK was a separate button
        // below the frame and is not now it is a row among the skills. Colour
        // is the only thing separating it from an entry that commits.
        public const string BackRowText = "#D9604A";
        public const string QuietHotkey = "#6D5F85";
        public const string LoudHotkey = "#FFDCAA8C";        // 0.55

        // --- resources --------------------------------------------------------
        public const string HpBright = "#E07A62";
        public const string HpDeep = "#8E3226";
        public const string HpText = "#F0C8BC";
        public const string MpBright = "#7EA8E6";
        public const string MpDeep = "#3A5A9A";
        public const string MpText = "#C4D8F2";

        // THE TWO TONES Ui.Meter NEEDS PER BAR: the hairline that traces the
        // track, and the band under the fill. Both are HpDeep/MpDeep at an
        // alpha, written out as full 8-digit tokens rather than composed at
        // the call site -- alpha is part of a colour in this DSL (see the file
        // header), and "HpDeep plus a suffix" is a string operation that would
        // sit at four call sites and be wrong at one of them.
        public const string HpRim = "#8E3226B3";             // HpDeep, 0.70
        public const string HpShade = "#8E322670";           // HpDeep, 0.44
        public const string MpRim = "#3A5A9AB3";             // MpDeep, 0.70
        public const string MpShade = "#3A5A9A70";           // MpDeep, 0.44
        public const string EnemyName = "#F0DCD8";
        public const string EnemyHpText = "#E0A89C";
        public const string PipFilled = "#E8E0F7";
        public const string PipEmpty = "#0E0814B3";          // 0.70

        // ---- the ward segment --------------------------------------------------
        //
        // A COOL CYAN, deliberately apart from both HpBright (salmon/red) and
        // MpBright (blue) -- a shield reads as a THIRD kind of resource, not a
        // tinted mana bar, and the two existing bars already claim the warm
        // and the blue ends of this palette. Drawn as its own segment on the
        // health bar (party and enemy plates alike, one colour for both --
        // see FightController.Hud's SetWardFill), sized ward/maxHP.
        public const string WardBright = "#8FE6E0";
        public const string WardDeep = "#2E6B66";
        public const string WardText = "#D8F5F2";

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

        // The one free slot on the wheel. Every other badge sits between
        // 12 degrees and 275 degrees of hue; magenta is the gap, and it is next
        // to the generic skill violet on purpose — a summon IS a skill, just
        // the one that changes how many monsters there are. The SHAPE carries
        // the distinction at badge size (a ring against a hand), so the colour
        // only has to say "related, but not that".
        public const string IntentSummon = "#E58ACB";

        // The Bellwether's kinds (PLAN_BELLWETHER_KIT 3.8). Bleed is the blood
        // red of its own art, darker than Weaken's HP red; Pull and Knell sit
        // in the void violet/lilac the kit is painted in, the Knell lighter so
        // it reads over the dark fog.
        public const string IntentBleed = "#D2404E";
        public const string IntentPull = "#A88BD8";
        public const string IntentKnell = "#D9C8F2";

        // THE LETHAL STYLE, a state of any damage badge (not a kind): the
        // shown number is at or above the target's current health. The icon
        // and its number both take it, so "this kills you" does not depend on
        // reading the digits.
        public const string IntentLethal = "#FF3B3B";
        public const string IntentNumber = "#FFF3E0";

        // The telegraph callout's and the knell floor figures' backing plate
        // (M5): near-black violet, mostly opaque, so red and lilac text read
        // over fog, foliage or stone alike. SAFE is the seat markers' green.
        public const string IntentCalloutPlate = "#120A1AE0";
        public const string SeatFigureSafe = "#7CF08A";

        // --- the fight's own panel art ----------------------------------------
        public const string TargetPromptFill = "#26160AE6";   // 0.90
        public const string TargetPromptBorder = "#FFC45A8C"; // 0.55
        public const string DetailBorder = "#C8AAE642";       // 0.26

        // --- damage type, one token per Stats.DamageType member -----------------
        //
        // The damage popup and the skill detail card's element label both read
        // these through ForDamageType, so a hit and the card describing the
        // skill that caused it cannot disagree about what colour "Fire" is.
        //
        // Physical is the popup's own original flat red (0.93, 0.26, 0.24 in
        // DamagePopup's old hardcoded Color), carried over as a token rather
        // than changed -- this pass adds colour to the OTHER five types, it
        // does not restyle the one every hit used to show.
        public const string DamageTypePhysical = "#ED423DFF";
        public const string DamageTypeFire = "#FF6A2AFF";
        public const string DamageTypeIce = "#9FD8F5FF";

        // Not in the brief (which named Physical/Fire/Poison/Arcane and three
        // elements this enum does not have -- Lightning, Holy, Shadow), so
        // this is a judgement call rather than an authored spec: forest green,
        // a hue apart from Poison below so the two read as different things
        // rather than as one green with two names.
        public const string DamageTypeNature = "#5FA24AFF";

        // Pushed ACID rather than the brief's own #5FD35F: that value sits one
        // step from DamagePopup.HealColor (#6BDB73, still the fixed colour a
        // heal shows regardless of DamageType) and the two would read as the
        // same green on a number that only differs by a leading +/-. This is
        // the "make poison more acid if it clashes" contingency the brief
        // itself named.
        public const string DamageTypePoison = "#A8E63CFF";

        // TacticalData's own FaceColor (TmpBootstrap.Typography.cs) -- the
        // brief asked for this exact hex, and it is already the violet every
        // other numeric HUD readout on this screen is tinted with.
        public const string DamageTypeArcane = "#C69AF1FF";

        // FIVE MORE, added the moment DamageType grew five more members.
        // Earth/Water/Wind/Lightning are read straight off the brief with no
        // clash against the six above (ochre, sky blue and electric yellow
        // each sit in a different hue band from anything already claimed).
        public const string DamageTypeEarth = "#C98A3AFF";
        public const string DamageTypeWater = "#3F9BE8FF";
        public const string DamageTypeWind = "#A8F0E0FF";
        public const string DamageTypeLightning = "#F5F06AFF";

        // NOT the brief's own #B03CCF -- that hue (~287) sits only 17 degrees
        // from Arcane's own #C69AF1 (~270), and both are the same
        // blue-violet family at a glance despite Arcane being the far
        // lighter, pastel-desaturated one. Pushed to hue ~307 (true
        // magenta-violet) for a separation that reads as two different
        // colours rather than two shades of one.
        public const string DamageTypeVoid = "#C22FB0FF";

        // Every Stats.DamageType member, explicitly -- no default fallthrough
        // to white, and no default fallthrough to Physical either for a type
        // this switch has heard of; only a genuinely FUTURE enum member (one
        // this file has not been taught yet) falls through to Physical, and
        // FightHudPaletteDamageTypeTests pins every CURRENT member against a
        // literal so a new one added to the enum without a token here fails a
        // test rather than silently painting a popup white.
        public static string ForDamageType(DamageType type)
        {
            switch (type)
            {
                case DamageType.Physical: return DamageTypePhysical;
                case DamageType.Fire: return DamageTypeFire;
                case DamageType.Ice: return DamageTypeIce;
                case DamageType.Nature: return DamageTypeNature;
                case DamageType.Poison: return DamageTypePoison;
                case DamageType.Arcane: return DamageTypeArcane;
                case DamageType.Earth: return DamageTypeEarth;
                case DamageType.Water: return DamageTypeWater;
                case DamageType.Wind: return DamageTypeWind;
                case DamageType.Lightning: return DamageTypeLightning;
                case DamageType.Void: return DamageTypeVoid;
                default: return DamageTypePhysical;
            }
        }
    }
}

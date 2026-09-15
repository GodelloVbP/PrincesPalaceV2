namespace PrincesPalace.Domain.UiKit
{
    // WHAT SILVER AND GOLD ARE, once, for every surface that draws a
    // character's identity stretch.
    //
    // Progression v2's levels 31-40 pay in rims, frames and embosses, and two
    // screens draw them today (the fight party plate and the hub roster card)
    // with more to come. Left to each screen, "silver" would be a hex typed
    // twice, and the two would drift the first time either palette moved -- the
    // same argument PcTheme makes for the six per-character colours, at the
    // scale of two metals.
    //
    // NOT NEW ART AND NOT A NEW PALETTE. Every value below is an existing kit
    // token restated at the alpha this use needs, exactly as PcTheme restates
    // FightHudPalette's: the gold pair is the kit's own BorderGold/GoldLight,
    // and the silver pair is Shawn's own plate theme, which is already the
    // "cool metal" colour this game has.
    public static class IdentityMetals
    {
        // THE RIM, drawn as the plate's own glow decal rather than as four
        // Solids -- a plate's corners are an 11px arc and a rectangle of
        // hairlines sticks a square spur out of each of them (PcPlateArt.
        // GlowKey's own note). So this is a TINT ON EXISTING ART, which is
        // also why it runs bright: a decal at 0.70 behind a leather strip
        // reads as a smudge where a hairline at 0.70 would read as a line.
        public const string RimSilver = "#D7DCE8E0";
        public const string RimGold = "#E7B25CE0";

        // THE EMBOSS, a tint laid ON the plate art itself rather than beside
        // it -- Image.color multiplies, so anything below white DARKENS the
        // leather and anything at white leaves it alone. These are barely off
        // white on purpose: an emboss is a change in the metal the head is
        // struck in, not a coloured light shone at it, and the first values
        // tried (the rim colours above) turned a brown strip grey-blue.
        public const string EmbossSilver = "#EDF1F8";
        public const string EmbossGold = "#FFEFC9";

        // THE PORTRAIT FRAME, an outline round the art slot on the roster
        // card. One colour, because §4 authors exactly one PortraitFrame node
        // per track with no metal on it -- a frame is collected or it is not.
        public const string PortraitFrame = FightHudPalette.BorderGold;

        // The identity line under a name. Quieter than the name above it and
        // warmer than the role below, so the three rows read as a stack rather
        // than as three competing labels.
        public const string Line = "#E7C68CCC";

        // A metal name to its rim colour, and to its emboss tint. NULL FOR
        // ANYTHING ELSE, including null itself -- "no rim collected" and "a
        // rim whose metal nobody recognises" are the same answer to a drawing
        // question, and both mean "draw nothing" rather than "draw a guess".
        public static string RimFor(string metal)
        {
            if (metal == "gold") return RimGold;
            if (metal == "silver") return RimSilver;
            return null;
        }

        public static string EmbossFor(string metal)
        {
            if (metal == "gold") return EmbossGold;
            if (metal == "silver") return EmbossSilver;
            return null;
        }
    }
}

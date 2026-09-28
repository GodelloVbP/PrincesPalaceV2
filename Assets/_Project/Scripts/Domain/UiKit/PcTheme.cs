using System;

namespace PrincesPalace.Domain.UiKit
{
    // WHAT A PARTY MEMBER'S COLOUR ACTUALLY IS, on every HUD surface that
    // stands for them.
    //
    // Every PC gets one colour, worn by every
    // surface that means "this character" -- Shawn Silver, Bjorn Crimson,
    // Odette Blue, and a future PC picks whichever of the kit's six themes
    // suits them. The theme itself is content (characters.json's plateTheme,
    // refused at build time if unauthored); this table is the one statement
    // of what each theme LOOKS like once it reaches a card.
    //
    // TWO ROLES, NOT ONE TINT. uGUI has no inherited tint, so a card wears
    // its colour through the four Image components of its rim plus its name
    // label -- and those two want different values. A rim is a boundary at
    // 0.70 alpha against a dark fill; a name is text that has to stay
    // legible over PanelPrimary at 12-20pt. One hex could not serve both:
    // rim-strength text is dim, text-strength rim is a neon outline.
    //
    // MEANWHILE, WHAT DOES NOT MOVE. The HP/MP meters keep their resource
    // colours (red is health on every card, whoever's card it is) and the
    // stage foot rings keep their side colours (a targeting signal, not an
    // identity one). The card FILL stays FightHudPalette.PanelPrimary on
    // every theme -- the meters' contrast was tuned against that one ground,
    // and six grounds would be six separate contrast problems.
    //
    // DERIVED FROM EXISTING TOKENS, never invented: each row below names the
    // FightHudPalette constant it came from, so a palette change has one
    // place to propagate through rather than six new literals to chase. They
    // are restated here as hex rather than aliased because the alpha differs
    // (a rim runs at 0.70, the tokens they come from do not all carry that),
    // and PcThemeTests pins all twelve as literals.
    public readonly struct PcColours
    {
        public readonly string Rim;
        public readonly string Name;

        public PcColours(string rim, string name)
        {
            Rim = rim;
            Name = name;
        }
    }

    public static class PcTheme
    {
        // AN EXPLICIT SIX-ARM SWITCH THAT THROWS, not a dictionary with a
        // fallback. A theme with no row is a programmer error -- the enum has
        // six values and every one of them is answered here -- and Domain
        // throws on programmer error rather than returning a plausible wrong
        // answer (docs/CODE_STANDARDS.md "Functions"). PcThemeTests walks
        // Enum.GetValues and asserts every value has a row, so a seventh
        // theme added to ButtonTheme fails a test rather than throwing
        // mid-fight the first time someone authors it.
        public static PcColours For(ButtonTheme theme)
        {
            switch (theme)
            {
                // BorderGold / GoldLight -- the kit's own gold pair, already
                // at 0.70 on the rim side.
                case ButtonTheme.Gold:
                    return new PcColours("#E7B25CB3", "#FFE0A8");

                // BackRowText, the HUD's existing warm red for "this figure
                // is on the far rank". NOT HpDeep: that is already the HP
                // meter's own rim on the same card, and a card whose outline
                // matched its health bar would read as a health state.
                case ButtonTheme.Crimson:
                    return new PcColours("#D9604AB3", "#F3B9AC");

                // IntentSkill -- the violet this HUD already uses for "a
                // named ability is coming".
                case ButtonTheme.Violet:
                    return new PcColours("#C79BEEB3", "#DCC6F7");

                // MpBright / MpText. Blue is the untinted card's colour too
                // (what SceneBuilder bakes before any character is acting),
                // so it is the one theme whose baked and applied values are
                // the same.
                case ButtonTheme.Blue:
                    return new PcColours("#7EA8E6B3", "#C4D8F2");

                // IntentHeal.
                case ButtonTheme.Green:
                    return new PcColours("#7FE0A0B3", "#C6EFD5");

                // TextPrimary, which is OFF-white on purpose (#F4EBFF, a
                // violet-cast white): NothingOnScreenIsAWhiteQuad refuses a
                // flat #FFFFFF anywhere in this game, and Shawn wears this
                // one.
                case ButtonTheme.Silver:
                    return new PcColours("#D9D2E6B3", "#F4EBFF");

                default:
                    throw new ArgumentOutOfRangeException(nameof(theme), theme,
                        "no PC colour row for this ButtonTheme -- add one here, not a fallback");
            }
        }
    }
}

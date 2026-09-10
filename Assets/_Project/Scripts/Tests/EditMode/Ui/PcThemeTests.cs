using System;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The per-PC colour table: every theme answered, and every hex pinned.
    //
    // Two different failures, so two different tests. One is a theme with no
    // row -- which throws at paint time, in a fight, on whoever's card it is,
    // and only for the character who authored it. The other is a hex quietly
    // drifting, which nothing notices at all: rim edges are AsDecor, so
    // UiAudit never looks at their colour.
    public class PcThemeTests
    {
        // THE VACUITY GUARD (docs/CODE_STANDARDS.md Sec8): PcTheme.For throws
        // on an unhandled theme rather than falling back, which is the right
        // posture for Domain but pushes the discovery to runtime. This walks
        // the enum so a seventh ButtonTheme fails here, at build time, in the
        // file that has to gain the row.
        [Test]
        public void EveryButtonThemeHasAPcColourRow()
        {
            var themes = (ButtonTheme[])Enum.GetValues(typeof(ButtonTheme));
            Assert.AreEqual(6, themes.Length,
                "ButtonTheme changed size -- the table below is written out per theme, so add or remove " +
                "a row in PcTheme.cs and a case here in the same pass");

            foreach (var theme in themes)
            {
                var colours = PcTheme.For(theme);

                Assert.IsNotEmpty(colours.Rim, $"{theme} has no rim colour");
                Assert.IsNotEmpty(colours.Name, $"{theme} has no name colour");
                Assert.AreNotEqual(colours.Rim, colours.Name,
                    $"{theme}'s rim and name are the same value -- see PcTheme's header for why they cannot be");
            }
        }

        // LITERALS, not FightHudPalette lookups (CLAUDE.md gotcha 5). PcTheme
        // derives its values from palette tokens by hand -- the alphas
        // differ -- so a test that rebuilt them from the same tokens would
        // only be checking its own arithmetic. These are what actually
        // reaches the screen.
        [TestCase(ButtonTheme.Gold, "#E7B25CB3", "#FFE0A8")]
        [TestCase(ButtonTheme.Crimson, "#D9604AB3", "#F3B9AC")]
        [TestCase(ButtonTheme.Violet, "#C79BEEB3", "#DCC6F7")]
        [TestCase(ButtonTheme.Blue, "#7EA8E6B3", "#C4D8F2")]
        [TestCase(ButtonTheme.Green, "#7FE0A0B3", "#C6EFD5")]
        [TestCase(ButtonTheme.Silver, "#D9D2E6B3", "#F4EBFF")]
        public void EachThemesTwoHexesArePinned(ButtonTheme theme, string rim, string name)
        {
            var colours = PcTheme.For(theme);

            Assert.AreEqual(rim, colours.Rim, $"{theme} rim");
            Assert.AreEqual(name, colours.Name, $"{theme} name");
        }

        // Six characters cannot be told apart by a colour two of them share.
        [Test]
        public void NoTwoThemesShareARimColour()
        {
            var themes = (ButtonTheme[])Enum.GetValues(typeof(ButtonTheme));

            for (int i = 0; i < themes.Length; i++)
            {
                for (int j = i + 1; j < themes.Length; j++)
                {
                    Assert.AreNotEqual(PcTheme.For(themes[i]).Rim, PcTheme.For(themes[j]).Rim,
                        $"{themes[i]} and {themes[j]} wear the same rim -- two characters would be indistinguishable");
                }
            }
        }

        // NothingOnScreenIsAWhiteQuad's own rule, restated where the value
        // lives: Silver is the closest any PC colour comes to white and it is
        // deliberately violet-cast (#F4EBFF, FightHudPalette.TextPrimary).
        [Test]
        public void SilverIsOffWhiteNotWhite()
        {
            Assert.AreNotEqual("#FFFFFF", PcTheme.For(ButtonTheme.Silver).Name.ToUpperInvariant());
        }
    }
}

using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    // The sky behind one constellation, keyed on ConstellationLayout.PlotId.
    //
    // DATA, NOT A SWITCH: a figure's backdrop is one row here, so adding a
    // character's plot means adding a row and an image, never a branch in the
    // controller. A plot with no row (Shawn's ram and lamb, the spire) draws
    // the screen's default nebula, which is the graceful-degradation path.
    //
    // WHY EACH ROW CARRIES ITS OWN TINT. The screen multiplies its sky by a
    // colour so that the stones stay the brightest thing on it, and that
    // multiply was fitted to the nebula (mean luma 22.6 -> 5.74). These
    // paintings are darker than the nebula, so the same multiply would sink
    // them below the dark an ember needs to sit against. Each tint below is
    // the nebula's #3E3A58 scaled uniformly (keeping the screen's cooling)
    // until the tinted image's mean Rec.601 luma matches the nebula's:
    //   shield    19.4 -> x1.171 -> #494467  tinted 5.75
    //   axe       14.0 -> x1.701 -> #696396  tinted 5.73
    //   paw       18.5 -> x1.290 -> #504B72  tinted 5.75
    // Pinned as literals, not recomputed, so a repaint of one file shows up as
    // a deliberate edit of its row.
    public static class TalentBackdrops
    {
        public sealed class Entry
        {
            public readonly string PlotId;
            public readonly string SpriteKey;
            public readonly string TintHex;

            public Entry(string plotId, string spriteKey, string tintHex)
            {
                PlotId = plotId;
                SpriteKey = spriteKey;
                TintHex = tintHex;
            }
        }

        private const string Dir = "Assets/_Project/Art/Backgrounds/";

        public static readonly IReadOnlyList<Entry> All = new[]
        {
            new Entry("shield", Dir + "Sentinel.png", "#494467"),
            new Entry("axe", Dir + "Einherjar.png", "#696396"),
            new Entry("paw", Dir + "Juggernaut.png", "#504B72"),
        };

        // Null when the plot has no painting of its own.
        public static Entry For(string plotId)
        {
            if (string.IsNullOrEmpty(plotId)) return null;

            foreach (var entry in All)
            {
                if (string.Equals(entry.PlotId, plotId, StringComparison.Ordinal)) return entry;
            }

            return null;
        }
    }
}

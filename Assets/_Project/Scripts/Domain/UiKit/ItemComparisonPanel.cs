using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    // A SQUARE, READABLE COMPARISON PANEL: a title and a body of stat/affix
    // lines, over a dark near-opaque plate with the kit hairline -- the
    // same fill/rim/lines shape Ui.Tooltip already uses (see its own
    // header), widened from a hover ground into a STANDING panel a screen
    // shows or hides by its own selection state rather than by a pointer.
    //
    // SHOP PASS (2026-09-22, owner ask #3): the shop needs to show a
    // selected gear card's stats, its affix lines and the delta against
    // whatever the picked party character has equipped -- one body string
    // ItemDescription.ComparisonBody already renders in full. This is
    // deliberately a shared Domain/UiKit builder rather than a one-off
    // inside ShopScreen: CharacterDossierScreen's own tooltip
    // (BuildTooltip, 300x480, "#1D1226F2") is the next thing due to be
    // rebuilt onto this SAME shape, so a second, ShopScreen-local copy of
    // "fill + rim + title + body" would be the exact duplication this
    // builder exists to close before it is ever written.
    //
    // ROUGHLY SQUARE AND READABLE, in the owner's own words -- ~420x420 and
    // a body font of 15 or larger. Both are CALLER-supplied rather than
    // baked in here: a size and a type scale are content, not shape, and
    // the dossier's own eventual migration needs its own numbers without
    // this file changing under it (docs/CODE_STANDARDS.md §10, "no
    // configurability without a demonstrated purpose" -- this one already
    // has its second, named caller).
    public static class ItemComparisonPanel
    {
        // The dossier tooltip's own plate colour and hairline, carried over
        // unchanged rather than re-picked: "the reference shape the dossier
        // tooltip will later be matched to" means match its palette too, not
        // just its silhouette.
        public const string PlateHex = "#1D1226F2";
        public static readonly string RimHex = FightHudPalette.Hairline;

        private const float Pad = 20f;
        private const float TitleToBodyGap = 12f;

        // The three parts a caller wires up: the whole panel (to show/hide),
        // and the title/body labels (to paint into). No StatRows collection
        // here yet -- ComparisonBody already renders stats, affixes and the
        // VS.-EQUIPPED delta as one body string (ItemDescription.Compare),
        // so the shop's own first caller has nothing to put in a separate
        // row strip. A caller that DOES need discrete rows adds them as
        // ordinary children between title and body; the shape does not
        // change to anticipate a second caller that does not exist yet.
        public readonly struct Built
        {
            public readonly UiNode Panel;
            public readonly UiNode Title;
            public readonly UiNode Body;

            public Built(UiNode panel, UiNode title, UiNode body)
            {
                Panel = panel;
                Title = title;
                Body = body;
            }
        }

        // `size` is the WHOLE panel; `titleHeight` carves the title's own
        // box out of its top edge and the body takes what is left down to
        // the bottom pad. Runtime-only text throughout (UiString.Runtime,
        // the same posture every offer card's name/meta already takes):
        // an item's display name and its stat/affix lines are catalogue
        // content, not something this file can sample for UiTextFitAudit.
        public static Built Build(string name, Place place, UiVec size,
            float titleHeight, int titleFontSize, string titleHex,
            int bodyFontSize, string bodyHex)
        {
            float halfW = size.X * 0.5f;
            float halfH = size.Y * 0.5f;

            var parts = new List<UiNode>
            {
                Ui.Solid(name + "Fill", PlateHex, size, Place.At(0f, 0f)).AsDecor(),
            };
            parts.AddRange(Ui.Rim(name, size, RimHex));

            float titleCentreY = halfH - Pad - titleHeight * 0.5f;
            var title = Ui.Label(name + "Title", UiString.Runtime,
                    new UiVec(size.X - Pad * 2f, titleHeight), titleFontSize, titleHex,
                    Place.At(0f, titleCentreY))
                .TextAligned(UiTextAlign.Left);
            parts.Add(title);

            float bodyTop = titleCentreY - titleHeight * 0.5f - TitleToBodyGap;
            float bodyBottom = -halfH + Pad;
            float bodyHeight = bodyTop - bodyBottom;
            var body = Ui.Label(name + "Body", UiString.Runtime,
                    new UiVec(size.X - Pad * 2f, bodyHeight), bodyFontSize, bodyHex,
                    Place.At(0f, bodyBottom + bodyHeight * 0.5f))
                .TextAligned(UiTextAlign.TopLeft);
            parts.Add(body);

            var panel = Ui.Panel(name, place, UiSize.Fixed(size), parts);
            return new Built(panel, title, body);
        }
    }
}

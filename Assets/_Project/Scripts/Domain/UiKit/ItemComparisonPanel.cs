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
    // inside ShopScreen: CharacterDossierScreen's own tooltip has since
    // been rebuilt onto this SAME shape too (BuildTooltip calls
    // ItemComparisonPanel.Build with the "DossierTooltip" name prefix; the
    // old bespoke 300x480 "#1D1226F2" rectangle is retired -- see
    // BuildTooltip's own header in CharacterDossierScreen.cs). Two callers
    // today, ShopScreen's ShopDetailPanel and CharacterDossierScreen's
    // DossierTooltip, both through this one builder, so a third,
    // screen-local copy of "fill + rim + title + body" would be the exact
    // duplication this builder exists to close.
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
        // rather than re-picked: "the reference shape the dossier tooltip
        // will later be matched to" means match its palette too, not just
        // its silhouette.
        //
        // FULLY OPAQUE. It was #1D1226F2 (95%), and the 5% that got through
        // was enough: over the dossier's pack the bright gear art and the
        // cell names showed faintly under the tooltip's text (QA 2026-09-26).
        // This panel is a READING surface that stands over art by design --
        // the dossier places it over neighbouring pack cells, the shop over
        // its offer strip -- so nothing may show through it at all.
        public const string PlateHex = "#1D1226FF";
        public static readonly string RimHex = FightHudPalette.Hairline;

        public const float Pad = 20f;
        public const float TitleToBodyGap = 12f;

        // How tall the panel has to be to hold a title of `titleHeight` and
        // a body that measures `bodyHeight` -- the inverse of Build's own
        // carve-up, so a caller that sizes the panel to its text at runtime
        // (the dossier's hover tooltip) cannot disagree with the builder
        // about where the pads and the gap go.
        public static float HeightFor(float titleHeight, float bodyHeight) =>
            Pad + titleHeight + TitleToBodyGap + bodyHeight + Pad;

        // The wrap width the body label gets inside a panel `panelWidth` wide.
        public static float BodyWidthFor(float panelWidth) => panelWidth - Pad * 2f;

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
            // EVERY PART IS ANCHORED TO AN EDGE, not placed at a fixed offset
            // from the centre. The shop shows this panel at its built size and
            // nothing changes; the dossier's hover tooltip sets only the
            // panel's HEIGHT to fit its text (QA 2026-09-26: a 420x420 box
            // half empty under a six-line body), and the fill, the rim and
            // the body follow that one number because they are pinned to the
            // edges they belong to. Centre offsets would have left the title
            // floating mid-panel and the rim tracing the old 420 square.
            var parts = new List<UiNode>
            {
                Ui.Solid(name + "Fill", PlateHex, Place.Stretch(), UiSize.Fixed(size)).AsDecor(),
            };

            // The rim, one 1px edge per side, each pinned INSIDE its own edge
            // (the containment audit refuses a hairline centred on the
            // boundary; Ui.Rim's header). Same four names Ui.Rim emits.
            parts.Add(RimEdge(name + "RimTop", RimHex, new UiVec(size.X, 1f),
                Place.Frac(new UiVec(0f, 1f), new UiVec(1f, 1f), bottom: -1f)));
            parts.Add(RimEdge(name + "RimBottom", RimHex, new UiVec(size.X, 1f),
                Place.Frac(new UiVec(0f, 0f), new UiVec(1f, 0f), top: -1f)));
            parts.Add(RimEdge(name + "RimLeft", RimHex, new UiVec(1f, size.Y),
                Place.Frac(new UiVec(0f, 0f), new UiVec(0f, 1f), right: -1f)));
            parts.Add(RimEdge(name + "RimRight", RimHex, new UiVec(1f, size.Y),
                Place.Frac(new UiVec(1f, 0f), new UiVec(1f, 1f), left: -1f)));

            // Title: pinned to the top edge, its own fixed height.
            var title = Ui.Label(name + "Title", UiString.Runtime,
                    new UiVec(BodyWidthFor(size.X), titleHeight), titleFontSize, titleHex,
                    Place.Frac(new UiVec(0f, 1f), new UiVec(1f, 1f),
                        left: Pad, right: Pad, bottom: -(Pad + titleHeight), top: Pad))
                .TextAligned(UiTextAlign.Left);
            parts.Add(title);

            // Body: everything under the title down to the bottom pad, so it
            // grows and shrinks with the panel. Its declared Size is still the
            // built-size box (the solver ignores it for an edge-anchored node,
            // DossierTooltipTextFitTests reads it as the worst-case capacity).
            float bodyInsetTop = Pad + titleHeight + TitleToBodyGap;
            float bodyHeight = size.Y - bodyInsetTop - Pad;
            var body = Ui.Label(name + "Body", UiString.Runtime,
                    new UiVec(BodyWidthFor(size.X), bodyHeight), bodyFontSize, bodyHex,
                    Place.Stretch(left: Pad, right: Pad, bottom: Pad, top: bodyInsetTop))
                .TextAligned(UiTextAlign.TopLeft);
            parts.Add(body);

            var panel = Ui.Panel(name, place, UiSize.Fixed(size), parts);
            return new Built(panel, title, body);
        }

        private static UiNode RimEdge(string name, string hex, UiVec size, Place place) =>
            Ui.Solid(name, hex, place, UiSize.Fixed(size)).AsDecor();
    }
}

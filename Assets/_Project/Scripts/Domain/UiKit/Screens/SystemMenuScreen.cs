using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // The overarching menu: a title lintel, a top tab bar, and one content pane.
    //
    // EMBEDDED PER SCENE rather than registered as its own ScreenDef, because
    // ScreenRegistry maps one screen to one scene and this has to be reachable
    // from all of them.
    //
    // THE BAR IS BUILT IN MEASURED MODE and re-laid at runtime.
    //
    // That needs saying because it looks like a violation of this project's
    // first rule. The tab SET is context-driven -- three tabs out of a run,
    // five in one -- and the two sets use different layout rules, but a scene
    // is generated once. So the build authors every tab at its five-tab
    // position and SystemMenuController re-applies SystemMenuLayout for
    // whichever set is showing. The arithmetic still lives in exactly one
    // place; only the moment it runs moved.
    //
    // The cost is that UiAudit only ever sees the five-tab layout, since that
    // is what is on disk. SystemMenuLayoutTests covers the three-tab one
    // instead -- same overlap and flush-right checks, done against the numbers
    // rather than against the emitted scene.
    public sealed class SystemMenuScreen
    {
        // ---- the design pass's tokens ------------------------------------------

        private const string Scrim = "#0A0614ED";            // was D9; the thin one let the hub read through
        private const string PanelFill = "#1A1024F5";        // PanelViolet at 96%
        private const string PanelRim = "#E7B25CB3";         // BorderGold
        private const string BarPlate = "#12091C";           // opaque, the bar's own ground
        private const string Hairline = "#C8AAE638";
        private const string GoldLight = "#FFE0A8";
        private const string GoldRule = "#E7B25C8C";
        private const string TextPrimary = "#F4EBFF";
        private const string TextMuted = "#8A7AA0";
        private const string QuietHotkey = "#6D5F85";
        private const string TabHover = "#C8AAE60F";

        public CharacterDossierScreen Dossier;
        public OptionsScreen Options;

        public UiNode Root;
        public NodeRef Bar;
        public NodeRef Content;

        // The lintel, above the panel.
        public NodeRef RunTitle;
        public NodeRef ContextLine;
        public NodeRef GoldValue;
        public NodeRef EmbersValue;
        public NodeRef CloseButton;

        // Indexed by tab, in SystemMenuTabs.All order. The count audit is
        // declared against these in ScreenRegistry, so a tab that gained a
        // button but no pane fails the build rather than the eye.
        public List<NodeRef> TabButtons = new List<NodeRef>();
        public List<NodeRef> TabHovers = new List<NodeRef>();
        public List<NodeRef> TabUnderlines = new List<NodeRef>();
        public List<NodeRef> TabDividers = new List<NodeRef>();
        public List<NodeRef> Panes = new List<NodeRef>();
        public List<NodeRef> PanePlaceholders = new List<NodeRef>();

        public static SystemMenuScreen Build()
        {
            var screen = new SystemMenuScreen();
            var tabs = SystemMenuTabs.All;

            // THE GUARD, at build time, and it is arithmetic now rather than a
            // count. A count-based limit was honest while every box was the
            // same width; once a box is sized to its own label, "how many fit"
            // is a question about the words, and a localisation that doubles a
            // label overflows a bar that still has the same number of tabs.
            if (!SystemMenuLayout.StripFits(tabs))
            {
                throw new System.InvalidOperationException(
                    $"The system menu's {tabs.Count} tabs need " +
                    $"{SystemMenuLayout.StripWidth(tabs):F0}px at minimum padding but the bar row is " +
                    $"{SystemMenuLayout.RowWidth:F0}px. Shorten a label, widen the panel, or the bar needs " +
                    "to wrap or scroll - it cannot simply be given another entry. The design's own fallback " +
                    "is to shorten the first tab to CHARACTER before either of those.");
            }

            var barChildren = new List<UiNode>();

            // The bar's own plate. OPAQUE, and that is the fix for the single
            // biggest thing the skeleton got wrong: the bar was transparent
            // over whatever was behind it, so hub text read straight through it
            // and "BETWEEN DESCENTS" collided with "MAIN MENU".
            barChildren.Add(Ui.Solid("SystemMenuBarPlate", BarPlate,
                    new UiVec(SystemMenuLayout.PanelWidth, SystemMenuLayout.BarHeight),
                    Place.At(0f, 0f))
                .AsDecor()
                .AllowOverlap("the plate is what the whole bar stands on"));

            // Half a pixel in from the boundary, not on it: a 1px node centred
            // exactly on the edge pokes 0.5px outside its parent.
            barChildren.Add(Ui.Solid("SystemMenuBarEdge", Hairline,
                    new UiVec(SystemMenuLayout.PanelWidth, 1f),
                    Place.At(0f, -SystemMenuLayout.BarHeight * 0.5f + 0.5f))
                .AsDecor()
                .AllowOverlap("the bar's bottom edge sits on the plate it closes"));

            var centres = SystemMenuLayout.TabCentresX(tabs);
            var widths = SystemMenuLayout.TabWidths(tabs);

            for (int i = 0; i < tabs.Count; i++)
            {
                var def = tabs[i];
                float x = centres[i];
                float w = widths[i];

                // The hover plate, under the label and the size of the whole
                // box. The skeleton popped the tab to 1.03 instead, and the
                // design pass killed that outright: it wobbles a bar whose
                // positions are arithmetic, and a tab that moves when you
                // approach it is harder to hit than one that lights up.
                var hover = Ui.Solid($"SystemTab{def.Key}Hover", TabHover,
                        new UiVec(w, SystemMenuLayout.TabHeight), Place.At(x, 0f))
                    .Inactive()
                    .AsDecor()
                    .AllowOverlap("the hover plate sits under its own tab by construction");

                var button = Ui.Button($"SystemTab{def.Key}", def.Label,
                        new UiVec(w, SystemMenuLayout.TabHeight), 18,
                        Place.At(x, 0f))
                    .NoChrome()
                    .AllowOverlap("the tab's label sits over its own hover plate by construction");

                // WIDTH IS THE LABEL'S, not the box's. A rule as wide as a
                // generous three-tab box reads as a second divider rather than
                // as a marker for the word above it.
                var underline = Ui.Solid($"SystemTab{def.Key}Underline", GoldLight,
                        new UiVec(SystemMenuLayout.UnderlineWidth(def),
                                  SystemMenuLayout.UnderlineHeight),
                        Place.At(x, SystemMenuLayout.UnderlineOffsetY))
                    .Inactive()
                    .AsDecor();

                screen.TabHovers.Add(hover);
                screen.TabButtons.Add(button);
                screen.TabUnderlines.Add(underline);
                barChildren.Add(hover);
                barChildren.Add(button);
                barChildren.Add(underline);
            }

            // One divider per gap. Built for the widest set and switched off
            // down to (visible - 1) at runtime, for the same reason the tabs
            // are: the scene cannot know which set it will be showing.
            for (int i = 0; i < tabs.Count - 1; i++)
            {
                var divider = Ui.Solid($"SystemTabDivider{i}", Hairline,
                        new UiVec(SystemMenuLayout.DividerWidth, SystemMenuLayout.DividerHeight),
                        Place.At(SystemMenuLayout.DividerCentreX(tabs, i), 0f))
                    .AsDecor();

                screen.TabDividers.Add(divider);
                barChildren.Add(divider);
            }

            var bar = Ui.Panel("SystemMenuBar",
                Place.At(0f, SystemMenuLayout.BarCentreY),
                UiSize.Fixed(SystemMenuLayout.PanelWidth, SystemMenuLayout.BarHeight),
                barChildren);
            screen.Bar = bar;

            // ---- the panes -------------------------------------------------------
            //
            // One per tab, all stacked in the same box, all but one switched
            // off. The pane is the FULL content area rather than an inset one,
            // because the design hosts the dossier by centring it here -- 1360
            // in 1600 is 120 clear each side, 766 in 804 is 19 clear top and
            // bottom, and nothing scales.
            var paneChildren = new List<UiNode>();
            var owners = SystemMenuTabs.PaneOwners;

            for (int i = 0; i < owners.Count; i++)
            {
                var def = owners[i];

                var placeholder = Ui.Label($"SystemPane{def.Key}Placeholder",
                        UiStrings.SystemPlaceholder, new UiVec(800f, 40f), 18, TextMuted,
                        Place.At(0f, 0f))
                    .AsDecor();

                var contents = new List<UiNode> { placeholder };

                // The one pane with real content so far: the dossier, which
                // replaced CharacterOverlayScreen. Its placeholder stays
                // declared but switched off, so the pane still has something to
                // show if the dossier is ever pulled.
                if (def.Tab == SystemMenuTab.CharacterInventory)
                {
                    var dossier = CharacterDossierScreen.Build();
                    screen.Dossier = dossier;
                    contents.Add(dossier.Root);
                    placeholder.Inactive();
                }
                else if (def.Tab == SystemMenuTab.Options)
                {
                    var options = OptionsScreen.Build();
                    screen.Options = options;
                    contents.Add(options.Root);
                    placeholder.Inactive();
                }

                var pane = Ui.Panel($"SystemPane{def.Key}",
                        Place.At(0f, 0f),
                        UiSize.Fixed(SystemMenuLayout.PanelWidth, SystemMenuLayout.ContentHeight),
                        contents)
                    .Inactive()
                    .AllowOverlap("every pane shares one box and all but the selected one is switched off");

                screen.Panes.Add(pane);
                screen.PanePlaceholders.Add(placeholder);
                paneChildren.Add(pane);
            }

            var content = Ui.Panel("SystemMenuContent",
                Place.At(0f, SystemMenuLayout.ContentCentreY),
                UiSize.Fixed(SystemMenuLayout.PanelWidth, SystemMenuLayout.ContentHeight),
                paneChildren);
            screen.Content = content;

            var frameChildren = new List<UiNode>
            {
                Ui.Solid("SystemMenuFill", PanelFill,
                        new UiVec(SystemMenuLayout.PanelWidth, SystemMenuLayout.PanelHeight),
                        Place.At(0f, 0f))
                    .AsDecor()
                    .AllowOverlap("the fill is what the panel's contents stand on"),
            };
            frameChildren.AddRange(PanelRimEdges());
            frameChildren.Add(bar);
            frameChildren.Add(content);

            var frame = Ui.Panel("SystemMenuFrame", Place.At(0f, 0f),
                UiSize.Fixed(SystemMenuLayout.PanelWidth, SystemMenuLayout.PanelHeight),
                frameChildren);

            var stage = Ui.Panel("SystemMenuStage", Place.At(0f, 0f),
                UiSize.Fixed(SystemMenuLayout.StageWidth, SystemMenuLayout.StageHeight),
                BuildLintel(screen), frame);

            // A modal, so it dims whatever it is standing over -- this menu can
            // open on the fight, and a transparent overlay there would be
            // unreadable against a lit battlefield.
            screen.Root = Ui.Modal("SystemMenuPanel", Scrim, stage).Inactive();
            return screen;
        }

        // Four 1px edges rather than a painted frame. The design asks for a
        // hairline rim in a token colour, and a sprite would put that colour
        // in a PNG where nobody can grep it.
        private static IEnumerable<UiNode> PanelRimEdges()
        {
            const float w = SystemMenuLayout.PanelWidth;
            const float h = SystemMenuLayout.PanelHeight;

            yield return Edge("SystemMenuRimTop", new UiVec(w, 1f), 0f, h * 0.5f - 0.5f);
            yield return Edge("SystemMenuRimBottom", new UiVec(w, 1f), 0f, -h * 0.5f + 0.5f);
            yield return Edge("SystemMenuRimLeft", new UiVec(1f, h), -w * 0.5f + 0.5f, 0f);
            yield return Edge("SystemMenuRimRight", new UiVec(1f, h), w * 0.5f - 0.5f, 0f);
        }

        private static UiNode Edge(string name, UiVec size, float x, float y) =>
            Ui.Solid(name, PanelRim, size, Place.At(x, y))
                .AsDecor()
                .AllowOverlap("the rim traces the panel it encloses");

        // ---- the title lintel ---------------------------------------------------
        //
        // ABOVE the panel, in the 90px of screen the panel does not use, and
        // deliberately not panel chrome: it carries the run's identity and the
        // player's currency, which belong to the run rather than to whichever
        // tab is open. Inside the panel it would have cost the content pane
        // 52px for something that never changes with the tab.
        private static UiNode BuildLintel(SystemMenuScreen screen)
        {
            var title = Ui.Label("SystemLintelTitle", UiString.Runtime, new UiVec(400f, 24f), 19, GoldLight,
                Place.At(-560f, 0f)).AsDecor();

            var context = Ui.Label("SystemLintelContext", UiString.Runtime, new UiVec(420f, 20f), 14, TextMuted,
                Place.At(-120f, 0f)).AsDecor();

            var gold = Ui.Label("SystemLintelGold", UiString.Runtime, new UiVec(170f, 20f), 14, TextPrimary,
                Place.At(380f, 0f)).AsDecor();

            var embers = Ui.Label("SystemLintelEmbers", UiString.Runtime, new UiVec(170f, 20f), 14, TextPrimary,
                Place.At(570f, 0f)).AsDecor();

            // The ONLY thing QuietHotkey is allowed on. It measures 3.17:1
            // against the panel, which is under the floor for anything a player
            // has to read -- a glyph they only have to recognise is the one
            // exception the design pass carves out for it.
            var esc = Ui.Label("SystemLintelEscHint", UiStrings.SystemEscHint, new UiVec(42f, 18f), 12,
                QuietHotkey, Place.At(711f, 0f)).AsDecor();

            var close = Ui.Button("SystemLintelClose", UiStrings.SystemClose,
                    new UiVec(SystemMenuLayout.CloseButtonSize, SystemMenuLayout.CloseButtonSize), 16,
                    Place.At(759f, 0f))
                .NoChrome();

            screen.RunTitle = title;
            screen.ContextLine = context;
            screen.GoldValue = gold;
            screen.EmbersValue = embers;
            screen.CloseButton = close;

            var children = new List<UiNode>
            {
                title,
                Ui.Solid("SystemLintelRuleA", Hairline, new UiVec(1f, 24f), Place.At(-344f, 0f)).AsDecor(),
                context,
                gold,
                embers,
                Ui.Solid("SystemLintelRuleB", Hairline, new UiVec(1f, 24f), Place.At(664f, 0f)).AsDecor(),
                esc,
                close,

                // The gold rule under the whole lintel, flush against the
                // panel's top edge. Placed in lintel space, so it moves with it.
                // Half a pixel in from the bottom edge, not on it: a 1px node
                // centred exactly on the boundary pokes 0.5px outside its
                // parent and the containment audit is right to refuse that.
                Ui.Solid("SystemLintelGoldRule", GoldRule, new UiVec(SystemMenuLayout.LintelWidth, 1f),
                        Place.At(0f, -SystemMenuLayout.LintelHeight * 0.5f + 0.5f))
                    .AsDecor()
                    .AllowOverlap("the rule closes the lintel it belongs to"),
            };

            return Ui.Panel("SystemMenuLintel",
                Place.At(0f, SystemMenuLayout.LintelCentreY),
                UiSize.Fixed(SystemMenuLayout.LintelWidth, SystemMenuLayout.LintelHeight),
                children);
        }
    }
}

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
        private const string PanelRim = FightHudPalette.BorderGold;         // BorderGold
        private const string BarPlate = "#12091C";           // opaque, the bar's own ground
        private const string Hairline = FightHudPalette.Hairline;
        private const string GoldLight = FightHudPalette.GoldLight;
        private const string GoldRule = "#E7B25C8C";
        private const string TextPrimary = FightHudPalette.TextPrimary;
        private const string TextMuted = FightHudPalette.TextMuted;
        private const string QuietHotkey = FightHudPalette.QuietHotkey;
        private const string TabHover = FightHudPalette.HoverTint;

        public CharacterDossierScreen Dossier;
        public OptionsScreen Options;
        public RunStatsScreen RunStats;
        public ExitsScreen Exits;

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
            var authored = SystemMenuLayout.AuthoredWidths(tabs);
            if (!SystemMenuLayout.StripFits(authored))
            {
                throw new System.InvalidOperationException(
                    $"The system menu's {tabs.Count} tabs need " +
                    $"{SystemMenuLayout.StripWidth(authored):F0}px at minimum padding but the bar row is " +
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
                .AsDecor());

            // Half a pixel in from the boundary, not on it: a 1px node centred
            // exactly on the edge pokes 0.5px outside its parent.
            barChildren.Add(Ui.Solid("SystemMenuBarEdge", Hairline,
                    new UiVec(SystemMenuLayout.PanelWidth, 1f),
                    Place.At(0f, -SystemMenuLayout.BarHeight * 0.5f + 0.5f))
                .AsDecor());

            var centres = SystemMenuLayout.TabCentresX(authored);
            var widths = SystemMenuLayout.TabWidths(authored);

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
                    .AsDecor();

                // TRACKED, and this is not decoration.
                //
                // SystemMenuTabDef.LabelWidth carries the design's measurement
                // at 18px Chakra Petch with .14em letter-spacing, and the whole
                // bar -- box widths, gaps, dividers, underline -- is arithmetic
                // over those numbers. Drawn at the font's own spacing the same
                // label is about a quarter narrower, so every box had 50px of
                // air the design never put there and the underline overhung its
                // own word by 40px a side. Measured: 215.5px drawn against 272
                // authored, and 265.9 with this line.
                var button = Ui.Button($"SystemTab{def.Key}", def.Label,
                        new UiVec(w, SystemMenuLayout.TabHeight), 18,
                        Place.At(x, 0f))
                    .NoChrome()
                    .Tracked(SystemMenuLayout.TabLabelTracking)
                    .AllowOverlap("the tab's label sits over its own hover plate by construction");

                // WIDTH IS THE LABEL'S, not the box's. A rule as wide as a
                // generous three-tab box reads as a second divider rather than
                // as a marker for the word above it.
                var underline = Ui.Solid($"SystemTab{def.Key}Underline", GoldLight,
                        new UiVec(SystemMenuLayout.UnderlineWidth(def.LabelWidth),
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
                        Place.At(SystemMenuLayout.DividerCentreX(authored, i), 0f))
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
            // and every hosted screen is authored AT that size rather than
            // inset into it. The dossier used to be placed at its handover's
            // 1360x766 and centred, which cost 118px of dead margin a side and
            // left its columns stopping short of the floor -- it read as a
            // small screen inside a big empty one. Every pane fills the box
            // now; see DossierLayout's header for why scaling could not.
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

                // The panes with real content. FOUR OF FIVE now; only Floor map
                // is still the placeholder, and it is the one that hosts an
                // existing screen (Run Map) rather than declaring its own.
                //
                // Each hosted screen keeps its placeholder DECLARED but
                // switched off, so a pane still has something to show if its
                // screen is ever pulled -- and so the tab's Built flag has
                // something to be checked against. SystemMenuPaneTests pins the
                // two together: a tab claiming content it does not have would
                // otherwise send DefaultFor onto the words CONTENT TO COME.
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
                else if (def.Tab == SystemMenuTab.RunStats)
                {
                    var stats = RunStatsScreen.Build();
                    screen.RunStats = stats;
                    contents.Add(stats.Root);
                    placeholder.Inactive();
                }
                else if (def.Tab == SystemMenuTab.MainMenu)
                {
                    var exits = ExitsScreen.Build();
                    screen.Exits = exits;
                    contents.Add(exits.Root);
                    placeholder.Inactive();
                }

                var pane = Ui.Panel($"SystemPane{def.Key}",
                        Place.At(0f, 0f),
                        UiSize.Fixed(SystemMenuLayout.PanelWidth, SystemMenuLayout.ContentHeight),
                        contents)
                    .Inactive();

                screen.Panes.Add(pane);
                screen.PanePlaceholders.Add(placeholder);
                paneChildren.Add(pane);
            }

            // ALTERNATIVES, not a pile. Every pane is the same box and the
            // controller switches one on; declaring that exempts them from each
            // other and leaves them checked against the bar above and the
            // lintel, which the old blanket AllowOverlap did not.
            Ui.Exclusive(paneChildren);

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
                    .AsDecor(),
            };
            frameChildren.Add(bar);
            frameChildren.Add(content);

            // THE RIM GOES ON LAST, and that is the whole fix for the sides
            // disappearing along the tab bar.
            //
            // The bar's plate is opaque and spans the panel's full 1600, so it
            // covered the left and right rim for the top 96px -- the border
            // stopped at the bar and started again under it, which reads as a
            // frame that was never drawn rather than one drawn underneath.
            // Later sibling, drawn last, over everything it encloses.
            frameChildren.AddRange(Ui.Rim("SystemMenu",
                new UiVec(SystemMenuLayout.PanelWidth, SystemMenuLayout.PanelHeight),
                PanelRim));

            var frame = Ui.Panel("SystemMenuFrame", Place.At(0f, 0f),
                UiSize.Fixed(SystemMenuLayout.PanelWidth, SystemMenuLayout.PanelHeight),
                frameChildren);

            // FILL, not the reference frame's literal size. The lintel sits
            // above the panel rather than inside it, so the two need a common
            // parent, and that parent has no business being 1920 wide on a
            // canvas that is allowed to be narrower.
            var stage = Ui.Panel("SystemMenuStage", Place.At(0f, 0f), UiSize.Fill,
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
                    Place.At(-560f, 0f))
                .Tracked(SystemMenuLayout.LintelTitleTracking)
                .AsDecor();

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
                // THE LINTEL'S OWN PLATE, and it is a fix rather than dressing.
                //
                // The design resolved "the scrim is too thin" by darkening it
                // and by giving the BAR a plate. The lintel got neither, and it
                // sits over the brightest thing on the hub: at #0A0614ED the
                // hub's own "DIVINE PRINCIPALITY" still read faintly through it
                // -- and since the lintel prints those same words, the title
                // appeared twice, a few pixels apart, looking like a render
                // fault rather than like two labels.
                //
                // Same colour as the bar's plate, so the two opaque strips
                // above and below the gold rule read as one piece of chrome.
                Ui.Solid("SystemLintelPlate", BarPlate,
                        new UiVec(SystemMenuLayout.LintelWidth, SystemMenuLayout.LintelHeight),
                        Place.At(0f, 0f))
                    .AsDecor(),
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
                    .AsDecor(),
            };

            return Ui.Panel("SystemMenuLintel",
                Place.At(0f, SystemMenuLayout.LintelCentreY),
                UiSize.Fixed(SystemMenuLayout.LintelWidth, SystemMenuLayout.LintelHeight),
                children);
        }
    }
}

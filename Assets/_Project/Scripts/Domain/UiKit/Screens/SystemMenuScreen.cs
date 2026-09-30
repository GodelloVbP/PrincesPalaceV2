using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // The overarching menu: a title lintel, a top tab bar, and one content pane.
    //
    // EMBEDDED PER SCENE rather than registered as its own ScreenDef, because
    // ScreenRegistry maps one screen to one scene and this has to be reachable
    // from all of them.
    //
    // THE BAR IS BUILT FROM MEASURED WIDTHS and re-laid at runtime.
    //
    // That needs saying because it looks like a violation of this project's
    // first rule. The tab SET is context-driven -- four tabs at camp, five in
    // a descent -- and a scene is generated once. So the build authors every
    // tab at its five-tab position and SystemMenuController re-applies
    // SystemMenuLayout for whichever set is showing. The arithmetic still
    // lives in exactly one place; only the moment it runs moved. The widths
    // come from the Editor, which measures the very labels it is about to
    // emit; Domain has no font.
    //
    // UiAudit therefore only sees the five-tab layout, since that is what is
    // on disk. SystemMenuScreenTests pins the four-tab one against the numbers
    // instead: same overlap and flush-right checks, without an emitted scene.
    public sealed class SystemMenuScreen
    {
        // ---- the design pass's tokens ------------------------------------------

        private const string Scrim = "#0A0614ED";            // was D9; the thin one let the hub read through
        private const string PanelFill = "#1A1024F5";        // PanelViolet at 96%
        private const string PanelRim = FightHudPalette.BorderGold;         // BorderGold
        private const string BarPlate = "#12091C";           // opaque, the bar's own ground
        private const string PillFill = "#241634";           // a shade up from the plate, so the pill reads as raised
        private const string Hairline = FightHudPalette.Hairline;
        private const string GoldLight = FightHudPalette.GoldLight;
        private const string GoldRule = "#E7B25C8C";
        private const string TextPrimary = FightHudPalette.TextPrimary;
        private const string TextMuted = FightHudPalette.TextMuted;
        private const string TabHover = FightHudPalette.HoverTint;

        public CharacterDossierScreen Dossier;
        public RewardTrackScreen RewardTrack;
        public PartyScreen Party;
        public OptionsScreen Options;
        public RunScreen Run;
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

        // The mode pill (plan C10): the plate, and the words in it.
        public NodeRef ModePill;
        public NodeRef ModePillLabel;

        // The footer, below the panel (plan C12): the notice on its left and
        // a fixed number of prompt slots on its right. The slots are authored
        // once and re-laid at runtime by MenuPromptFooter, because how many
        // prompts show and how wide they are depend on the focus.
        public NodeRef Footer;
        public NodeRef FooterNotice;
        public List<NodeRef> PromptPlates = new List<NodeRef>();
        public List<NodeRef> PromptGlyphTexts = new List<NodeRef>();
        public List<NodeRef> PromptLabels = new List<NodeRef>();

        // Indexed by tab, in SystemMenuTabs.All order. The count audit is
        // declared against these in ScreenRegistry, so a tab that gained a
        // button but no pane fails the build rather than the eye.
        public List<NodeRef> TabButtons = new List<NodeRef>();
        public List<NodeRef> TabHovers = new List<NodeRef>();
        public List<NodeRef> TabUnderlines = new List<NodeRef>();
        public List<NodeRef> TabDividers = new List<NodeRef>();
        public List<NodeRef> Panes = new List<NodeRef>();

        // partyRosterCardCount defaults to 3, matching how many characters
        // characters.json authors today -- a COMPATIBILITY default, not a
        // design choice (docs/CODE_STANDARDS.md "Functions"): ScreenRegistry, which builds
        // this with no argument, is out of scope for the package that added
        // the parameter, so the
        // default keeps every existing caller compiling. Wiring the REAL
        // count (ContentDatabase.LoadOrdered<CharacterDefinition>().Count) is
        // that later package's job, the same way MainMenuScreen.Build reads
        // SaveSystem.SlotCount from ScreenRegistry rather than Domain.
        //
        // labelWidths are the tab labels as measured by the caller. Null takes
        // SystemMenuLabelWidths.Estimated, which is right for a look at the
        // tree and wrong for a scene: ScreenRegistry always measures.
        public static SystemMenuScreen Build(
            int partyRosterCardCount = 3, SystemMenuLabelWidths labelWidths = null)
        {
            var screen = new SystemMenuScreen();
            var tabs = SystemMenuTabs.All;
            labelWidths = labelWidths ?? SystemMenuLabelWidths.Estimated();

            // THE GUARD, at build time: Arrange throws if even the short labels
            // do not fit the row. It is arithmetic over the words rather than a
            // count of tabs -- a localisation that doubles a label overflows a
            // bar that still has the same number of tabs.
            var bar = SystemMenuLayout.Arrange(tabs, labelWidths.Full, labelWidths.Short);

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

            for (int i = 0; i < tabs.Count; i++)
            {
                var def = tabs[i];
                float x = bar.CentreX(i);
                float w = bar.Widths[i];

                // The hover plate, under the label and the size of the whole
                // box. The skeleton popped the tab to 1.03 instead, and the
                // design pass killed that outright: it wobbles a bar whose
                // positions are arithmetic, and a tab that moves when you
                // approach it is harder to hit than one that lights up.
                var hover = Ui.Solid($"SystemTab{def.Key}Hover", TabHover,
                        new UiVec(w, SystemMenuLayout.TabHeight), Place.At(x, 0f))
                    .Inactive()
                    .AsDecor();

                var button = TabButton(def, bar.ShortLabels, new UiVec(w, SystemMenuLayout.TabHeight),
                    Place.At(x, 0f));

                // WIDTH IS THE LABEL'S, not the box's. A rule as wide as the
                // box reads as a second divider rather than as a marker for
                // the word above it.
                var underline = Ui.Solid($"SystemTab{def.Key}Underline", GoldLight,
                        new UiVec(SystemMenuLayout.UnderlineWidth(bar.LabelWidths[i]),
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

            // One divider per gap between adjacent tabs of a group. Built for
            // the widest set and re-placed and re-shown at runtime, for the
            // same reason the tabs are: the scene cannot know which set it will
            // be showing. The gap between the groups gets none, so the one the
            // build authors there is off until the controller places it.
            for (int i = 0; i < tabs.Count - 1; i++)
            {
                var divider = Ui.Solid($"SystemTabDivider{i}", Hairline,
                        new UiVec(SystemMenuLayout.DividerWidth, SystemMenuLayout.DividerHeight),
                        Place.At(bar.DividerCentreX(i), 0f))
                    .AsDecor();
                if (!SystemMenuLayout.DividerFollows(tabs, i)) divider.Inactive();

                screen.TabDividers.Add(divider);
                barChildren.Add(divider);
            }

            var barPanel = Ui.Panel("SystemMenuBar",
                Place.At(0f, SystemMenuLayout.BarCentreY),
                UiSize.Fixed(SystemMenuLayout.PanelWidth, SystemMenuLayout.BarHeight),
                barChildren);
            screen.Bar = barPanel;

            // ---- the panes -------------------------------------------------------
            //
            // One per tab, all stacked in the same box, all but one switched
            // off. The pane is the FULL content area rather than an inset
            // one, and every hosted screen is authored AT that size rather
            // than inset into it -- centring a smaller screen inside a
            // bigger box reads as a small screen inside a big empty one;
            // see DossierLayout's header for why scaling could not fix that
            // instead.
            var paneChildren = new List<UiNode>();
            var owners = SystemMenuTabs.PaneOwners;

            for (int i = 0; i < owners.Count; i++)
            {
                var def = owners[i];

                var contents = new List<UiNode>();

                // A tab with no branch here has no pane, and the build refuses
                // it rather than shipping an empty box.
                switch (def.Tab)
                {
                    case SystemMenuTab.Character:
                        var dossier = CharacterDossierScreen.Build();
                        screen.Dossier = dossier;
                        contents.Add(dossier.Root);

                        // THE REWARD TRACK, over the dossier rather than
                        // beside it as another tab: the dossier answers "what
                        // am I now" and the track answers "what is coming",
                        // and the door between them is one nav row.
                        //
                        // Same construction as the dossier's own pack panel,
                        // which opens over column A from a nav row and closes
                        // on its own button. LAST in the pane so it draws over
                        // everything.
                        var track = RewardTrackScreen.Build();
                        screen.RewardTrack = track;
                        contents.Add(track.Root);
                        break;

                    case SystemMenuTab.Party:
                        var party = PartyScreen.Build(new PartyInputs(partyRosterCardCount));
                        screen.Party = party;
                        contents.Add(party.Root);
                        break;

                    case SystemMenuTab.Options:
                        var options = OptionsScreen.Build();
                        screen.Options = options;
                        contents.Add(options.Root);
                        break;

                    case SystemMenuTab.Run:
                        var run = RunScreen.Build();
                        screen.Run = run;
                        contents.Add(run.Root);
                        break;

                    case SystemMenuTab.Exit:
                        var exits = ExitsScreen.Build();
                        screen.Exits = exits;
                        contents.Add(exits.Root);
                        break;

                    default:
                        throw new System.InvalidOperationException(
                            $"The system menu tab {def.Tab} has no pane. Add its screen to " +
                            "SystemMenuScreen.Build before listing it in SystemMenuTabs.All.");
                }

                var pane = Ui.Panel($"SystemPane{def.Key}",
                        Place.At(0f, 0f),
                        UiSize.Fixed(SystemMenuLayout.PanelWidth, SystemMenuLayout.ContentHeight),
                        contents)
                    .Inactive();

                screen.Panes.Add(pane);
                paneChildren.Add(pane);
            }

            // ALTERNATIVES, not a pile. Every pane is the same box and the
            // controller switches one on; declaring that exempts them from
            // each other, not from the bar above and the lintel, which they
            // still stay checked against.
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
            frameChildren.Add(barPanel);
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
                BuildLintel(screen), frame, BuildFooter(screen));

            // A modal, so it dims whatever it is standing over -- this menu can
            // open on the fight, and a transparent overlay there would be
            // unreadable against a lit battlefield.
            screen.Root = Ui.Modal("SystemMenuPanel", Scrim, stage).Inactive();
            return screen;
        }

        // A tab's label, in the FunctionalHeading role at the bar's own size
        // and tracking, all caps. THE ONE PLACE this is spelled: the build
        // emits it, and the Editor measures it (TabLabelProbe) with the same
        // node, so the width a box is built around is the width that draws.
        //
        // Tracked over the role's own spacing, because the role's is 2 and the
        // bar's is .14em (SystemMenuLayout.TabLabelTracking).
        private static UiNode TabButton(SystemMenuTabDef def, bool shortLabel, UiVec size, Place place) =>
            Ui.Button($"SystemTab{def.Key}", shortLabel ? def.ShortLabel : def.Label, size,
                    SystemMenuLayout.TabLabelSize, place)
                .NoChrome()
                // The tab swipe (SystemMenuController.Select) is this
                // click's sound; the generic one would double it.
                .Quiet()
                .Styled(TypographyRole.FunctionalHeading)
                .TrackedOverRole(SystemMenuLayout.TabLabelTracking);

        // The node the Editor measures for a tab's label width.
        public static UiNode TabLabelProbe(SystemMenuTabDef def, bool shortLabel) =>
            TabButton(def, shortLabel, new UiVec(SystemMenuLayout.RowWidth, SystemMenuLayout.TabHeight),
                Place.At(0f, 0f));

        // A rimmed plate: a 1px ring of `rimHex` around a fill, as two stretched
        // solids rather than four edges, so the box may be resized at runtime
        // (the mode pill fits its words, a glyph plate fits its letters) with
        // nothing to re-place. Decor throughout; it is drawn, never pressed.
        private static UiNode Plate(string name, string rimHex, string fillHex, UiVec size, Place place,
                                    params UiNode[] over)
        {
            var parts = new List<UiNode>
            {
                Ui.Solid(name + "Rim", rimHex, Place.Stretch(), UiSize.Fill).AsDecor(),
                Ui.Solid(name + "Fill", fillHex, Place.Stretch(1f, 1f, 1f, 1f), UiSize.Fill).AsDecor(),
            };

            parts.AddRange(over);
            return Ui.Panel(name, place, UiSize.Fixed(size), parts);
        }

        // THE ONE GLYPH PLATE, for the lintel's Esc and every prompt in the
        // footer: a rimmed 32-tall plate with the button's letters on it. The
        // letters are TacticalData at 24, which is both readable and over the
        // all-caps floor for "LB" and "RT". Its width is the caller's; the
        // footer refits it to the measured letters.
        private static UiNode GlyphPlate(string name, UiString text, float width, Place place, out UiNode letters)
        {
            letters = Ui.Label(name + "Text", text, new UiVec(width, MenuFooterLayout.GlyphHeight),
                    GlyphLetterSize, PromptGlyphs.Plain, Place.Stretch())
                .Styled(TypographyRole.TacticalData)
                .AsDecor();

            return Plate(name, GoldRule, BarPlate, new UiVec(width, MenuFooterLayout.GlyphHeight), place, letters);
        }

        private const int GlyphLetterSize = 24;

        // ---- the title lintel ---------------------------------------------------
        //
        // ABOVE the panel, in the 90px of screen the panel does not use, and
        // deliberately not panel chrome: it carries the run's identity and the
        // player's currency, which belong to the run rather than to whichever
        // tab is open. Inside the panel it would have cost the content pane
        // 52px for something that never changes with the tab.
        //
        // Left to right: title, context, the mode pill, then gold, embers, the
        // Esc glyph and close, flush to the right pad. Every label takes a role
        // and sits at or over the type floor (MenuTypeFloor); the numbers below
        // are the room that costs, and UiTextFitAudit holds the authored ones
        // (title, Esc) to their boxes at build.
        private static UiNode BuildLintel(SystemMenuScreen screen)
        {
            var title = Ui.Label("SystemLintelTitle", UiStrings.SystemMenuTitle, new UiVec(390f, 32f), 24, GoldLight,
                    Place.At(-581f, 0f))
                .Styled(TypographyRole.FunctionalHeading)
                .TrackedOverRole(SystemMenuLayout.LintelTitleTracking)
                .AsDecor();

            var context = Ui.Label("SystemLintelContext", UiString.Runtime, new UiVec(230f, 28f), 20, TextMuted,
                    Place.At(-231f, 0f))
                .Styled(TypographyRole.Body)
                .AsDecor();

            // THE MODE PILL (plan C10): what the whole menu may change, said
            // once here so no pane needs a status of its own. Authored at its
            // widest; the controller fits it to the words for the context.
            var pillLabel = Ui.Label("SystemModePillLabel", UiString.Runtime,
                    new UiVec(SystemMenuLayout.PillMaxWidth, SystemMenuLayout.PillHeight), 24, GoldLight,
                    Place.Stretch())
                .Styled(TypographyRole.FunctionalHeading)
                .AsDecor();

            var pill = Plate("SystemModePill", GoldRule, PillFill,
                new UiVec(SystemMenuLayout.PillMaxWidth, SystemMenuLayout.PillHeight),
                Place.At(SystemMenuLayout.PillCentreX(SystemMenuLayout.PillMaxWidth), 0f), pillLabel);

            // Body, not TacticalData: that role's material carries a violet face
            // colour by design, and it multiplies whatever colour the node asks
            // for, so a TextPrimary number came out violet and read as a link.
            var gold = Ui.Label("SystemLintelGold", UiString.Runtime, new UiVec(150f, 30f), 22, TextPrimary,
                    Place.At(379f, 0f))
                .Styled(TypographyRole.Body)
                .AsDecor();

            var embers = Ui.Label("SystemLintelEmbers", UiString.Runtime, new UiVec(170f, 30f), 22, TextPrimary,
                    Place.At(551f, 0f))
                .Styled(TypographyRole.Body)
                .AsDecor();

            // The lintel's Esc is a glyph like the footer's, from the same
            // plate. It names the keyboard's key whatever device is in hand: it
            // is where the eye finds "how do I leave", and the pad's own close
            // is in the footer.
            var esc = GlyphPlate("SystemLintelEsc", UiStrings.SystemEscHint, 64f, Place.At(700f, 0f), out _);

            var close = Ui.Button("SystemLintelClose", UiStrings.SystemClose,
                    new UiVec(SystemMenuLayout.CloseButtonSize, SystemMenuLayout.CloseButtonSize), 24,
                    Place.At(758f, 0f))
                .Closes()
                .NoChrome()
                .Styled(TypographyRole.FunctionalHeading);

            screen.RunTitle = title;
            screen.ContextLine = context;
            screen.GoldValue = gold;
            screen.EmbersValue = embers;
            screen.CloseButton = close;
            screen.ModePill = pill;
            screen.ModePillLabel = pillLabel;

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
                Ui.Solid("SystemLintelRuleA", Hairline, new UiVec(1f, 24f), Place.At(-366f, 0f)).AsDecor(),
                context,
                pill,
                gold,
                embers,
                Ui.Solid("SystemLintelRuleB", Hairline, new UiVec(1f, 24f), Place.At(652f, 0f)).AsDecor(),
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

        // ---- the footer (plan C12) --------------------------------------------------
        //
        // BELOW the panel, mirroring the lintel above it, so no pane loses
        // height to it. The notice sits on the left. The prompts sit on the
        // right: MenuFooterLayout.PromptSlots plates and labels, authored at
        // the positions the row takes at typical widths and moved at runtime
        // by MenuPromptFooter to wherever the focus's prompts measure out.
        private static UiNode BuildFooter(SystemMenuScreen screen)
        {
            var notice = Ui.Label("SystemFooterNotice", UiString.Runtime,
                    new UiVec(MenuFooterLayout.NoticeWidth, MenuFooterLayout.NoticeHeight), 20, TextPrimary,
                    Place.At(MenuFooterLayout.NoticeCentreX, 0f))
                .Styled(TypographyRole.Body)
                .TextAligned(UiTextAlign.Left)
                .Truncated()
                .AsDecor();

            screen.FooterNotice = notice;

            var children = new List<UiNode>
            {
                Ui.Solid("SystemFooterPlate", BarPlate,
                        new UiVec(MenuFooterLayout.Width, MenuFooterLayout.Height), Place.At(0f, 0f))
                    .AsDecor(),

                // The rule on top, against the panel's bottom edge: the
                // lintel's own gold rule, upside down.
                Ui.Solid("SystemFooterGoldRule", GoldRule, new UiVec(MenuFooterLayout.Width, 1f),
                        Place.At(0f, MenuFooterLayout.Height * 0.5f - 0.5f))
                    .AsDecor(),
                notice,
            };

            // Where the slots sit before anyone has focus: a typical glyph and
            // label width, through the same arithmetic the runtime uses.
            var typical = new float[MenuFooterLayout.PromptSlots];
            var typicalLabel = new float[MenuFooterLayout.PromptSlots];
            for (int i = 0; i < typical.Length; i++)
            {
                typical[i] = 24f;
                typicalLabel[i] = 90f;
            }

            var placed = MenuFooterLayout.Arrange(typical, typicalLabel);
            if (placed.Count != MenuFooterLayout.PromptSlots)
            {
                throw new System.InvalidOperationException(
                    $"The footer has room for {placed.Count} typical prompts but authors {MenuFooterLayout.PromptSlots} " +
                    "slots; lower PromptSlots or widen the prompt area.");
            }

            for (int i = 0; i < placed.Count; i++)
            {
                var plate = GlyphPlate($"SystemPrompt{i}Glyph", UiString.Runtime, placed[i].PlateWidth,
                    Place.At(placed[i].PlateCentreX, 0f), out var letters);

                var label = Ui.Label($"SystemPrompt{i}Label", UiString.Runtime,
                        new UiVec(placed[i].LabelWidth, 28f), 20, TextPrimary,
                        Place.At(placed[i].LabelCentreX, 0f))
                    .Styled(TypographyRole.Body)
                    .TextAligned(UiTextAlign.Left)
                    .AsDecor();

                screen.PromptPlates.Add(plate);
                screen.PromptGlyphTexts.Add(letters);
                screen.PromptLabels.Add(label);
                children.Add(plate);
                children.Add(label);
            }

            var footer = Ui.Panel("SystemMenuFooter",
                Place.At(0f, MenuFooterLayout.CentreY),
                UiSize.Fixed(MenuFooterLayout.Width, MenuFooterLayout.Height),
                children);

            screen.Footer = footer;
            return footer;
        }
    }
}

using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // The overarching menu: a top tab bar, and one content pane under it.
    //
    // A SKELETON on purpose. Every pane is an empty named container -- the
    // design is a separate job, and this exists so that job has a frame to work
    // in, a controller that already switches panes, and a build-time check that
    // the bar still fits when a fifth tab arrives.
    //
    // EMBEDDED PER SCENE rather than registered as its own ScreenDef, because
    // ScreenRegistry maps one screen to one scene and this has to be reachable
    // from all of them. That is the same shape CharacterOverlayScreen already
    // uses to appear in the hub, the map and the fight.
    //
    // WHAT IT OVERLAPS, stated up front: "Character" and "Inventory" are the
    // two halves of the existing CharacterOverlayScreen, reachable today on C
    // and I. This does not remove that -- a skeleton that deleted a working
    // screen would be a migration, not a skeleton -- so for now the two exist
    // side by side and the panes here are empty. Folding the overlay into these
    // panes is the obvious next step and belongs in the design pass.
    public sealed class SystemMenuScreen
    {
        public const string FrameKey = "UI/Panels/Processed/panel_dark.png";

        // A UiNode, not a NodeRef: the scenes that embed this menu pass it
        // straight in as a child, and the implicit conversion only runs the
        // other way.
        public UiNode Root;
        public NodeRef Bar;
        public NodeRef Content;

        // Indexed by tab, in SystemMenuTabs.All order. The count audit is
        // declared against these in ScreenRegistry, so a tab that gained a
        // button but no pane fails the build rather than the eye.
        public List<NodeRef> TabButtons = new List<NodeRef>();
        public List<NodeRef> TabLabels = new List<NodeRef>();
        public List<NodeRef> TabUnderlines = new List<NodeRef>();
        public List<NodeRef> Panes = new List<NodeRef>();
        public List<NodeRef> PanePlaceholders = new List<NodeRef>();

        public static SystemMenuScreen Build()
        {
            var screen = new SystemMenuScreen();
            var tabs = SystemMenuTabs.All;

            // THE GUARD, at build time. The brief for this bar is "so we can
            // add more options easily", and the failure mode of exactly that is
            // a strip that silently runs off its own panel -- AUDIT #6 and #7
            // are both that bug, found by a player rather than by a build.
            if (!SystemMenuLayout.StripFits(tabs.Count))
            {
                throw new System.InvalidOperationException(
                    $"The system menu has {tabs.Count} tabs but the bar holds {SystemMenuLayout.MaxTabs()} " +
                    $"at {SystemMenuLayout.TabWidth}px wide. Either widen the panel, narrow the tabs, or the " +
                    "bar needs to wrap or scroll - it cannot simply be given another entry.");
            }

            var barChildren = new List<UiNode>();

            for (int i = 0; i < tabs.Count; i++)
            {
                var def = tabs[i];
                float x = SystemMenuLayout.TabCentreX(i);

                var button = Ui.Button($"SystemTab{def.Key}", def.Label,
                        new UiVec(SystemMenuLayout.TabWidth, SystemMenuLayout.TabHeight), 20,
                        Place.At(x, 0f))
                    .NoChrome()
                    .Hovers(1.03f);

                // The selected marker. Inactive by default; the controller
                // raises exactly one.
                var underline = Ui.Solid($"SystemTab{def.Key}Underline", "#E8D7A0",
                        new UiVec(SystemMenuLayout.TabWidth * 0.7f, 3f),
                        Place.At(x, -SystemMenuLayout.TabHeight * 0.5f - 6f))
                    .Inactive()
                    .AsDecor();

                screen.TabButtons.Add(button);
                screen.TabUnderlines.Add(underline);
                barChildren.Add(button);
                barChildren.Add(underline);

                // The divider FOLLOWS each tab except the last, exactly as the
                // sketch has it -- a trailing rule with nothing after it reads
                // as a fifth tab that failed to load.
                if (i < tabs.Count - 1)
                {
                    barChildren.Add(Ui.Solid($"SystemTabDivider{i}", "#6B5B8A",
                            new UiVec(SystemMenuLayout.DividerWidth, SystemMenuLayout.DividerHeight),
                            Place.At(SystemMenuLayout.DividerCentreX(i), 0f))
                        .AsDecor());
                }
            }

            var bar = Ui.Panel("SystemMenuBar",
                Place.At(0f, SystemMenuLayout.BarCentreY),
                UiSize.Fixed(SystemMenuLayout.PanelWidth, SystemMenuLayout.BarHeight),
                barChildren.ToArray());
            screen.Bar = bar;

            // One pane per tab, all stacked in the same box and all but one
            // switched off. Empty by design.
            var paneChildren = new List<UiNode>();
            for (int i = 0; i < tabs.Count; i++)
            {
                var def = tabs[i];

                var placeholder = Ui.Label($"SystemPane{def.Key}Placeholder",
                        UiStrings.SystemPlaceholder, new UiVec(800f, 40f), 18, "#8C7FA8",
                        Place.At(0f, 0f))
                    .AsDecor();

                var pane = Ui.Panel($"SystemPane{def.Key}",
                        Place.At(0f, 0f),
                        UiSize.Fixed(SystemMenuLayout.PanelWidth - 80f, SystemMenuLayout.ContentHeight - 60f),
                        placeholder)
                    .Inactive()
                    .AllowOverlap("every pane shares one box and all but the selected one is switched off");

                screen.Panes.Add(pane);
                screen.PanePlaceholders.Add(placeholder);
                paneChildren.Add(pane);
            }

            var content = Ui.Panel("SystemMenuContent",
                Place.At(0f, SystemMenuLayout.ContentCentreY),
                UiSize.Fixed(SystemMenuLayout.PanelWidth, SystemMenuLayout.ContentHeight),
                paneChildren.ToArray());
            screen.Content = content;

            var frame = Ui.Panel("SystemMenuFrame", Place.At(0f, 0f),
                UiSize.Fixed(SystemMenuLayout.PanelWidth, SystemMenuLayout.PanelHeight),
                bar, content);

            // A modal, so it dims whatever it is standing over -- this menu can
            // open on the fight, and a transparent overlay there would be
            // unreadable against a lit battlefield.
            screen.Root = Ui.Modal("SystemMenuPanel", "#0A0614D9", frame).Inactive();
            return screen;
        }
    }
}

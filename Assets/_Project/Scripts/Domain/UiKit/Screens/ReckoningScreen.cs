using System.Collections.Generic;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // The Reckoning: what a fight paid out.
    //
    // An OVERLAY on the fight, not a scene of its own. The stage is not torn
    // down behind it, which is what lets it expand INTO place from the fight
    // that produced it rather than cutting to a new screen.
    //
    // 70% of the frame, centred. That number is the design's, and it decides
    // the layout below more than anything else: 1344x756 is generous
    // horizontally and tight vertically, so the content runs in TWO COLUMNS --
    // experience left, gold and loot right -- rather than one tall stack that
    // would not fit four party rows and three offers.
    public sealed class ReckoningScreen
    {
        // 70% of 1920x1080.
        public const float PanelWidth = 1344f;
        public const float PanelHeight = 756f;

        // Fixed at build time, so it must cover the largest party the save can
        // field. Base squad is 1 today and the design's stated target is 3;
        // four leaves headroom without costing anything, since unused rows are
        // hidden. Pinned against EffectiveMaxSquadSize by ReckoningTests.
        public const int RowCount = 4;

        public UiNode Root;

        // The thing that actually scales. The dimmer must NOT scale with it --
        // a dimmer growing from a point would let the fight flash at full
        // brightness for the first frames of the expand.
        public NodeRef Frame;

        // The dimmer Ui.Modal builds. Held so the gloom can FADE UP with the
        // expand rather than snapping on -- a dim that arrives in one frame
        // reads as a scene cut, which is the one thing this overlay is not.
        public NodeRef Dimmer;

        public List<NodeRef> TabButtons = new List<NodeRef>();
        public List<NodeRef> TabMarkers = new List<NodeRef>();

        // One node per tab, toggled whole. Pages rather than a rebuilt list:
        // every tab's content is authored, audited and bound once at build
        // time, and switching is three SetActive calls.
        public List<NodeRef> Pages = new List<NodeRef>();

        public NodeRef RelicEmptyHint;
        public List<NodeRef> RelicRows = new List<NodeRef>();
        public List<NodeRef> RelicNames = new List<NodeRef>();
        public List<NodeRef> RelicMetas = new List<NodeRef>();
        public List<NodeRef> RelicBodies = new List<NodeRef>();

        public List<NodeRef> TallyRows = new List<NodeRef>();
        public List<NodeRef> TallyNames = new List<NodeRef>();
        public List<NodeRef> TallyStats = new List<NodeRef>();
        public List<NodeRef> TallyKills = new List<NodeRef>();

        public NodeRef GoldLabel;
        public NodeRef ContinueButton;
        public NodeRef LootHeading;

        public List<NodeRef> RowGroups = new List<NodeRef>();
        public List<NodeRef> RowNames = new List<NodeRef>();
        public List<NodeRef> RowLevels = new List<NodeRef>();
        public List<NodeRef> RowBarFills = new List<NodeRef>();
        public List<NodeRef> RowBarBefores = new List<NodeRef>();
        public List<NodeRef> RowGains = new List<NodeRef>();

        public List<NodeRef> OfferButtons = new List<NodeRef>();
        public List<NodeRef> OfferNames = new List<NodeRef>();
        public List<NodeRef> OfferMetas = new List<NodeRef>();

        private const float ColumnX = 336f;

        // 88 tall on a 96 pitch: an 8px gutter between rows, and enough height
        // for a name line, a bar and a gain line without the last of the three
        // hanging out of the box. The first attempt was 80 on 92 and the gain
        // label escaped by exactly 8px at every frame.
        private const float RowHeight = 88f;
        private const float RowPitch = 96f;
        private const float BarWidth = 560f;
        private const float BarHeight = 18f;

        // How many relics one page of the RELICS tab shows. The design says
        // "infinite slots per run"; six is what fits without paging, and the
        // controller says so out loud when a run holds more.
        public const int RelicRowCount = 6;

        private const float TabY = 272f;
        private const float TabPitch = 250f;

        public static ReckoningScreen Build()
        {
            var screen = new ReckoningScreen();

            var inside = new List<UiNode>
            {
                Ui.Label("ReckoningTitle", UiStrings.ReckoningTitle, new UiVec(700f, 56f), 34, "#F2DB9E",
                    Place.At(0f, 330f)).AsDecor(),
            };

            for (int i = 0; i < TabStrings.Length; i++)
            {
                inside.Add(screen.BuildTab(i));
            }

            inside.Add(screen.BuildSpoilsPage());
            inside.Add(screen.BuildRelicsPage());
            inside.Add(screen.BuildTallyPage());

            var continueButton = Ui.Button("ReckoningContinueButton", UiStrings.Continue,
                new UiVec(300f, 60f), 22, Place.At(0f, -330f));
            screen.ContinueButton = continueButton;
            inside.Add(continueButton);

            // The frame is a CHILD of the content panel rather than the modal
            // root, because the controller scales AND lifts this transform, and
            // a scaled dimmer would shrink the dim along with it.
            var frame = Ui.Panel("ReckoningFrame", Place.At(0f, 0f),
                    UiSize.Fixed(PanelWidth, PanelHeight), inside)
                .Coloured("#221338F5");
            screen.Frame = frame;

            var content = Ui.Panel("ReckoningContent", Place.At(0f, 0f), UiSize.Fixed(1920f, 1080f), frame);

            // 65%, not the 94% the character overlay uses. The fight is still
            // standing behind this and the expand plays against it; dimming to
            // near-black would make the overlay a screen with extra steps.
            var root = Ui.Modal("ReckoningPanel", "#0A0614A6", content).Inactive();

            // Ui.Modal builds its dimmer as the first child. Grabbed rather
            // than rebuilt, so the gloom can fade UP with the expand instead of
            // snapping on -- a dim that arrives in one frame reads as a scene
            // cut, which is the one thing this overlay is not.
            screen.Dimmer = root.Children[0];
            screen.Root = root;
            return screen;
        }

        private static readonly UiString[] TabStrings =
        {
            UiStrings.ReckoningTabSpoils,
            UiStrings.ReckoningTabRelics,
            UiStrings.ReckoningTabTally,
        };

        private UiNode BuildTab(int index)
        {
            float x = (index - (TabStrings.Length - 1) * 0.5f) * TabPitch;

            // The lit marker is a separate layer from the caption, so "which
            // tab am I on" and "what is it called" stay two channels -- the
            // same split the draft cards and the glossary rail both make.
            var marker = Ui.Solid("ReckoningTab" + index + "Marker", "#F2DB9E00",
                    Place.Frac(new UiVec(0f, 0f), new UiVec(1f, 0f), top: -4f), UiSize.Fill)
                .AsDecor();

            var tab = Ui.Button("ReckoningTab" + index, TabStrings[index],
                new UiVec(230f, 48f), 19, Place.At(x, TabY));
            tab.Children.Add(marker);

            TabButtons.Add(tab);
            TabMarkers.Add(marker);
            return tab;
        }

        // ---- page 1: the payout ----------------------------------------------

        private UiNode BuildSpoilsPage()
        {
            var children = new List<UiNode>
            {
                Ui.Label("ReckoningExpHeading", UiStrings.ReckoningExperience, new UiVec(560f, 34f), 20,
                    "#B8A8D9", Place.At(-ColumnX, 196f)).AsDecor(),
            };

            for (int i = 0; i < RowCount; i++)
            {
                children.Add(BuildRow(i));
            }

            var gold = Ui.Label("ReckoningGoldLabel", UiStrings.ReckoningGold, new UiVec(600f, 52f), 30,
                    "#F2DB9E", Place.At(ColumnX, 200f))
                .AsDecor();
            GoldLabel = gold;
            children.Add(gold);

            var lootHeading = Ui.Label("ReckoningLootHeading", UiStrings.ReckoningChooseOne, new UiVec(600f, 34f), 20,
                    "#B8A8D9", Place.At(ColumnX, 134f))
                .AsDecor();
            LootHeading = lootHeading;
            children.Add(lootHeading);

            for (int i = 0; i < ItemOfferTable.OfferCount; i++)
            {
                children.Add(BuildOffer(i));
            }

            var page = Ui.Panel("ReckoningSpoilsPage", Place.Stretch(), UiSize.Fill, children)
                .AllowOverlap("the three tab pages share one coordinate frame and exactly one is ever active");
            Pages.Add(page);
            return page;
        }

        // ---- page 2: what the descent carries ---------------------------------

        private UiNode BuildRelicsPage()
        {
            var children = new List<UiNode>
            {
                Ui.Label("ReckoningRelicHeading", UiStrings.ReckoningRelicHeld, new UiVec(700f, 34f), 20,
                    "#B8A8D9", Place.At(0f, 196f)).AsDecor(),
            };

            for (int i = 0; i < RelicRowCount; i++)
            {
                float y = 130f - i * 74f;

                var name = Ui.Label("ReckoningRelic" + i + "Name", UiString.Runtime, new UiVec(420f, 30f), 21,
                        "#EDE6FF", Place.At(-320f, 14f))
                    .AsDecor();
                var meta = Ui.Label("ReckoningRelic" + i + "Meta", UiString.Runtime, new UiVec(420f, 24f), 14,
                        "#B8A8D9", Place.At(-320f, -16f))
                    .AsDecor();
                var body = Ui.Label("ReckoningRelic" + i + "Body", UiString.Runtime, new UiVec(600f, 52f), 15,
                        "#9C8FC4", Place.At(240f, 0f))
                    .AsDecor();

                var row = Ui.Panel("ReckoningRelic" + i, Place.At(0f, y), UiSize.Fixed(1160f, 66f),
                        name, meta, body)
                    .Inactive();

                RelicRows.Add(row);
                RelicNames.Add(name);
                RelicMetas.Add(meta);
                RelicBodies.Add(body);
                children.Add(row);
            }

            var empty = Ui.Label("ReckoningRelicEmpty", UiStrings.ReckoningNoRelics, new UiVec(900f, 44f), 20,
                    "#7E6E9E", Place.At(0f, 40f))
                .AsDecor()
                .Inactive();
            RelicEmptyHint = empty;
            children.Add(empty);

            var page = Ui.Panel("ReckoningRelicsPage", Place.Stretch(), UiSize.Fill, children)
                .AllowOverlap("the three tab pages share one coordinate frame and exactly one is ever active")
                .Inactive();
            Pages.Add(page);
            return page;
        }

        // ---- page 3: what everyone did ----------------------------------------

        private UiNode BuildTallyPage()
        {
            var children = new List<UiNode>
            {
                Ui.Label("ReckoningTallyHeading", UiStrings.ReckoningTallyHeading, new UiVec(800f, 34f), 20,
                    "#B8A8D9", Place.At(0f, 196f)).AsDecor(),
            };

            for (int i = 0; i < RowCount; i++)
            {
                float y = 120f - i * 96f;

                var name = Ui.Label("ReckoningTally" + i + "Name", UiString.Runtime, new UiVec(420f, 30f), 21,
                        "#EDE6FF", Place.At(-350f, 22f))
                    .AsDecor();
                var kills = Ui.Label("ReckoningTally" + i + "Kills", UiStrings.ReckoningKills, new UiVec(300f, 26f), 15,
                        "#F2DB9E", Place.At(360f, 22f))
                    .AsDecor();
                var stats = Ui.Label("ReckoningTally" + i + "Stats", UiString.Runtime, new UiVec(1100f, 26f), 14,
                        "#9C8FC4", Place.At(0f, -20f))
                    .AsDecor();

                var row = Ui.Panel("ReckoningTally" + i, Place.At(0f, y), UiSize.Fixed(1180f, 84f),
                        name, kills, stats)
                    .Inactive();

                TallyRows.Add(row);
                TallyNames.Add(name);
                TallyKills.Add(kills);
                TallyStats.Add(stats);
                children.Add(row);
            }

            var page = Ui.Panel("ReckoningTallyPage", Place.Stretch(), UiSize.Fill, children)
                .AllowOverlap("the three tab pages share one coordinate frame and exactly one is ever active")
                .Inactive();
            Pages.Add(page);
            return page;
        }

        // One party member's line: who, what level, and a bar showing what this
        // fight was worth against what they had already.
        private UiNode BuildRow(int index)
        {
            float y = 108f - index * RowPitch;

            var name = Ui.Label($"ReckoningRow{index}Name", UiString.Runtime, new UiVec(360f, 30f), 22,
                    "#EDE6FF", Place.At(-100f, 27f))
                .AsDecor();
            var level = Ui.Label($"ReckoningRow{index}Level", UiString.Runtime, new UiVec(200f, 30f), 18,
                    "#B8A8D9", Place.At(180f, 27f))
                .AsDecor();
            var gain = Ui.Label($"ReckoningRow{index}Gain", UiString.Runtime, new UiVec(200f, 22f), 16,
                    "#9C8FC4", Place.At(180f, -30f))
                .AsDecor();

            // Two fills on one track. The dim one is where the bar stood before
            // the fight and the bright one is what was just earned, so the
            // player reads THIS FIGHT'S worth rather than only where they ended
            // up -- which is the entire reason CharacterReward carries a before
            // as well as an after.
            var before = Ui.Solid($"ReckoningRow{index}BarBefore", "#5A4A8C",
                    Place.Frac(new UiVec(0f, 0f), new UiVec(1f, 1f)), UiSize.Fill)
                .AsDecor();
            var fill = Ui.Solid($"ReckoningRow{index}BarFill", "#F2DB9E",
                    Place.Frac(new UiVec(0f, 0f), new UiVec(1f, 1f)), UiSize.Fill)
                .AsDecor();

            var track = Ui.Panel($"ReckoningRow{index}BarTrack", Place.At(0f, -6f),
                    UiSize.Fixed(BarWidth, BarHeight), before, fill)
                .Coloured("#140C24")
                .AsDecor()
                .AllowOverlap("the earned segment is drawn ON TOP of the before segment - one track, two fills, by design");

            var group = Ui.Panel($"ReckoningRow{index}", Place.At(-ColumnX, y),
                    UiSize.Fixed(600f, RowHeight), name, level, track, gain)
                .Inactive();

            RowGroups.Add(group);
            RowNames.Add(name);
            RowLevels.Add(level);
            RowBarBefores.Add(before);
            RowBarFills.Add(fill);
            RowGains.Add(gain);
            return group;
        }

        // One of the three items on offer. A button, because picking one is
        // the only decision this screen asks the player to make.
        private UiNode BuildOffer(int index)
        {
            float y = 44f - index * 112f;

            // NOT "...Label" -- UiEmitter names a button's generated caption
            // "<button>Label" and the collision is invisible to every by-name
            // lookup. See UiAudit A4b.
            var name = Ui.Label($"ReckoningOffer{index}Name", UiString.Runtime, new UiVec(540f, 34f), 20,
                    "#EDE6FF", Place.At(0f, 18f))
                .AsDecor();
            var meta = Ui.Label($"ReckoningOffer{index}Meta", UiString.Runtime, new UiVec(540f, 28f), 15,
                    "#B8A8D9", Place.At(0f, -18f))
                .AsDecor();

            var button = Ui.Button($"ReckoningOffer{index}", UiString.Runtime, new UiVec(600f, 96f), 14,
                Place.At(ColumnX, y));
            button.Children.Add(name);
            button.Children.Add(meta);

            OfferButtons.Add(button);
            OfferNames.Add(name);
            OfferMetas.Add(meta);
            return button;
        }
    }
}

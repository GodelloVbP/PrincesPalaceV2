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
        // 3:2, MATCHING THE PAINTED FRAME'S OWN ASPECT (1536x1024).
        //
        // Was 1344x756 (16:9), which would have stretched the border 18% and
        // distorted every corner ornament on it. 70% of the width and 83% of
        // the height instead -- and the extra 140px of height is what the left
        // column was short of when a party of one left three hidden rows.
        public const float PanelWidth = 1344f;
        public const float PanelHeight = 896f;

        // Where the ORNAMENT stops -- not where the art stops.
        //
        // The first pass measured the keyed alpha box (91.9% x 90.3%) and was
        // wrong in the one direction that mattered: that says where the image
        // ends, while the top edge carries a CREST reaching 16.7% down the
        // canvas against the 12.8% of the flat border beside it. The title sat
        // 60px inside the crest and collided with it on screen while every
        // test passed.
        //
        // Scanned inward per edge until the gold gives way to plain interior
        // (tools measure_frame.py): top 16.7%, bottom 14.6%, sides 9.8%.
        // ASYMMETRIC VERTICALLY, deliberately -- the crest is deeper than the
        // bottom ornament and pretending otherwise would waste 19px.
        public const float ContentHalfWidth = PanelWidth * 0.5f - PanelWidth * 0.098f;
        public const float ContentTop = PanelHeight * 0.5f - PanelHeight * 0.167f;
        public const float ContentBottom = -(PanelHeight * 0.5f - PanelHeight * 0.146f);

        // Kept for the containment test, which asks one question of both axes.
        public const float ContentHalfHeight = PanelHeight * 0.5f - PanelHeight * 0.167f;

        public const string FrameKey = "UI/Reckoning/Processed/reckoning_frame.png";
        public const string TabKey = "UI/Buttons/Processed/tab_plate.png";
        public const string ContinueKey = "UI/Buttons/Processed/continue_arrow.png";
        public const string BurstKey = "UI/Effects/Processed/rarity_burst.png";

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

        // TWO PHASES in one container: pick your spoils, then read what the
        // fight was worth. Separate from the three TABS, which belong to the
        // second phase only -- a tab strip over an item choice would offer to
        // navigate away from the one decision the screen is asking for.
        public NodeRef FrameGlow;
        public NodeRef OfferPhase;
        public NodeRef SummaryPhase;

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
        public List<NodeRef> OfferBursts = new List<NodeRef>();
        public List<NodeRef> OfferIcons = new List<NodeRef>();
        public List<NodeRef> OfferNames = new List<NodeRef>();
        public List<NodeRef> OfferMetas = new List<NodeRef>();

        // Pulled in from 336 so a 520-wide column clears the painted border:
        // 280 +/- 260 lands at 540, inside ContentHalfWidth. At the old width
        // the rows ran to 636 and would have sat on the frame's gold edge.
        private const float ColumnX = 280f;
        private const float ColumnWidth = 520f;

        // 88 tall on a 96 pitch: an 8px gutter between rows, and enough height
        // for a name line, a bar and a gain line without the last of the three
        // hanging out of the box. The first attempt was 80 on 92 and the gain
        // label escaped by exactly 8px at every frame.
        private const float RowHeight = 68f;
        private const float RowPitch = 76f;
        private const float BarWidth = 900f;
        private const float BarHeight = 18f;

        // How many relics one page of the RELICS tab shows. The design says
        // "infinite slots per run"; six is what fits without paging, and the
        // controller says so out loud when a run holds more.
        public const int RelicRowCount = 6;

        private const float TabY = 178f;
        private const float TabPitch = 250f;

        public static ReckoningScreen Build()
        {
            var screen = new ReckoningScreen();

            // ---- phase one: the choice ---------------------------------
            // Captured as LootHeading: it is the same node it always was, just
            // moved onto the phase that owns the choice.
            var lootHeading = Ui.Label("ReckoningOfferHeading", UiStrings.ReckoningChooseOne,
                    new UiVec(700f, 44f), 26, "#F2DB9E", Place.At(0f, 250f))
                .AsDecor();
            screen.LootHeading = lootHeading;

            var offerChildren = new List<UiNode> { lootHeading };

            for (int i = 0; i < ItemOfferTable.OfferCount; i++)
            {
                offerChildren.Add(screen.BuildOffer(i));
            }

            var offerPhase = Ui.Panel("ReckoningOfferPhase", Place.Stretch(), UiSize.Fill, offerChildren)
                .AllowOverlap("the two phases share one coordinate frame and exactly one is ever active");
            screen.OfferPhase = offerPhase;

            // ---- phase two: what it was worth --------------------------
            var summaryChildren = new List<UiNode>
            {
                Ui.Label("ReckoningTitle", UiStrings.ReckoningTitle, new UiVec(700f, 50f), 32, "#F2DB9E",
                    Place.At(0f, 250f)).AsDecor(),
            };

            for (int i = 0; i < TabStrings.Length; i++)
            {
                summaryChildren.Add(screen.BuildTab(i));
            }

            summaryChildren.Add(screen.BuildSpoilsPage());
            summaryChildren.Add(screen.BuildRelicsPage());
            summaryChildren.Add(screen.BuildTallyPage());

            // ONLY on the summary phase. Phase one has no way out but the
            // choice itself -- you always take something.
            var continueButton = Ui.Button("ReckoningContinueButton", UiStrings.Continue,
                new UiVec(340f, 92f), 22, Place.At(0f, -266f));
            continueButton.SpriteKey = ContinueKey;
            screen.ContinueButton = continueButton;
            summaryChildren.Add(continueButton);

            var summaryPhase = Ui.Panel("ReckoningSummaryPhase", Place.Stretch(), UiSize.Fill, summaryChildren)
                .AllowOverlap("the two phases share one coordinate frame and exactly one is ever active")
                .Inactive();
            screen.SummaryPhase = summaryPhase;

            // ---- the container -----------------------------------------
            var frame = Ui.Sprite("ReckoningFrame", FrameKey, Place.At(0f, 0f),
                UiSize.Fixed(PanelWidth, PanelHeight));
            frame.Children.Add(offerPhase);
            frame.Children.Add(summaryPhase);
            screen.Frame = frame;

            // A soft drop behind the frame. Sits BEHIND it in declaration
            // order and is deliberately larger, so it reads as the panel
            // casting light and shadow onto the fight rather than as a
            // second rectangle.
            var glow = Ui.Sprite("ReckoningFrameGlow", BurstKey, Place.At(0f, 0f),
                    UiSize.Fixed(PanelWidth * 1.35f, PanelHeight * 1.45f))
                .Coloured("#2A1A4A00")
                .AsDecor()
                .AllowOverflow("the glow is deliberately larger than the frame it sits behind - that bleed IS the drop")
                .AllowOverlap("a drop shadow covers the thing casting it by definition");
            screen.FrameGlow = glow;

            var content = Ui.Panel("ReckoningContent", Place.At(0f, 0f), UiSize.Fixed(1920f, 1080f),
                    glow, frame)
                .AllowOverlap("the glow sits under the frame by design");

            var root = Ui.Modal("ReckoningPanel", "#0A0614A6", content).Inactive();
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
                new UiVec(230f, 52f), 19, Place.At(x, TabY));
            tab.SpriteKey = TabKey;
            tab.Children.Add(marker);

            TabButtons.Add(tab);
            TabMarkers.Add(marker);
            return tab;
        }

        // ---- page 1: the payout ----------------------------------------------

        private UiNode BuildSpoilsPage()
        {
            // WIDE, not two columns. The offers moved to their own phase, so
            // the right half emptied out -- a party of one under an EXPERIENCE
            // heading beside a hole is worse than either half alone.
            var children = new List<UiNode>
            {
                Ui.Label("ReckoningGoldLabel", UiStrings.ReckoningGold, new UiVec(700f, 56f), 32,
                    "#F2DB9E", Place.At(0f, 96f)).AsDecor(),
                Ui.Label("ReckoningExpHeading", UiStrings.ReckoningExperience, new UiVec(700f, 34f), 20,
                    "#B8A8D9", Place.At(0f, 34f)).AsDecor(),
            };

            GoldLabel = children[0];

            for (int i = 0; i < RowCount; i++)
            {
                children.Add(BuildRow(i));
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
                    "#B8A8D9", Place.At(0f, 110f)).AsDecor(),
            };

            for (int i = 0; i < RelicRowCount; i++)
            {
                float y = 40f - i * 60f;

                var name = Ui.Label("ReckoningRelic" + i + "Name", UiString.Runtime, new UiVec(420f, 30f), 21,
                        "#EDE6FF", Place.At(-280f, 12f))
                    .AsDecor();
                var meta = Ui.Label("ReckoningRelic" + i + "Meta", UiString.Runtime, new UiVec(420f, 24f), 14,
                        "#B8A8D9", Place.At(-280f, -14f))
                    .AsDecor();
                var body = Ui.Label("ReckoningRelic" + i + "Body", UiString.Runtime, new UiVec(520f, 44f), 14,
                        "#9C8FC4", Place.At(240f, 0f))
                    .AsDecor();

                var row = Ui.Panel("ReckoningRelic" + i, Place.At(0f, y), UiSize.Fixed(1040f, 56f),
                        name, meta, body)
                    .Inactive();

                RelicRows.Add(row);
                RelicNames.Add(name);
                RelicMetas.Add(meta);
                RelicBodies.Add(body);
                children.Add(row);
            }

            var empty = Ui.Label("ReckoningRelicEmpty", UiStrings.ReckoningNoRelics, new UiVec(900f, 44f), 20,
                    "#7E6E9E", Place.At(0f, -40f))
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
                    "#B8A8D9", Place.At(0f, 110f)).AsDecor(),
            };

            for (int i = 0; i < RowCount; i++)
            {
                float y = 10f - i * 88f;

                var name = Ui.Label("ReckoningTally" + i + "Name", UiString.Runtime, new UiVec(380f, 30f), 20,
                        "#EDE6FF", Place.At(-300f, 20f))
                    .AsDecor();
                var kills = Ui.Label("ReckoningTally" + i + "Kills", UiStrings.ReckoningKills, new UiVec(280f, 26f), 15,
                        "#F2DB9E", Place.At(320f, 20f))
                    .AsDecor();
                var stats = Ui.Label("ReckoningTally" + i + "Stats", UiString.Runtime, new UiVec(1000f, 26f), 14,
                        "#9C8FC4", Place.At(0f, -18f))
                    .AsDecor();

                var row = Ui.Panel("ReckoningTally" + i, Place.At(0f, y), UiSize.Fixed(1040f, 78f),
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
            float y = -24f - index * RowPitch;

            var name = Ui.Label($"ReckoningRow{index}Name", UiString.Runtime, new UiVec(300f, 28f), 21,
                    "#EDE6FF", Place.At(-330f, 18f))
                .AsDecor();
            var level = Ui.Label($"ReckoningRow{index}Level", UiString.Runtime, new UiVec(220f, 28f), 17,
                    "#B8A8D9", Place.At(280f, 18f))
                .AsDecor();
            var gain = Ui.Label($"ReckoningRow{index}Gain", UiString.Runtime, new UiVec(200f, 22f), 15,
                    "#9C8FC4", Place.At(370f, -21f))
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

            var track = Ui.Panel($"ReckoningRow{index}BarTrack", Place.At(0f, -14f),
                    UiSize.Fixed(BarWidth, BarHeight), before, fill)
                .Coloured("#140C24")
                .AsDecor()
                .AllowOverlap("the earned segment is drawn ON TOP of the before segment - one track, two fills, by design");

            var group = Ui.Panel($"ReckoningRow{index}", Place.At(0f, y),
                    UiSize.Fixed(1000f, RowHeight), name, level, track, gain)
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
        // One of the three, as a CARD across the width rather than a row in a
        // list. The offers used to share the panel with the experience bar and
        // had to be small; alone on their own phase they can be the thing the
        // screen is about.
        private UiNode BuildOffer(int index)
        {
            const float CardWidth = 340f;
            const float CardGap = 30f;
            float x = (index - (ItemOfferTable.OfferCount - 1) * 0.5f) * (CardWidth + CardGap);

            // The burst spins behind the icon, tinted per rarity from the same
            // RarityColors table the name below it reads.
            var burst = Ui.Sprite($"ReckoningOffer{index}Burst", BurstKey,
                    Place.At(0f, 78f), UiSize.Fixed(250f, 250f))
                .Coloured("#FFFFFF00")
                .AsDecor()
                .AllowOverflow("the burst is deliberately larger than the icon it sits behind - that bleed IS the rarity signal");

            var icon = Ui.Sprite($"ReckoningOffer{index}Icon", null,
                    Place.At(0f, 78f), UiSize.Fixed(132f, 132f))
                .AsDecor();

            var name = Ui.Label($"ReckoningOffer{index}Name", UiString.Runtime, new UiVec(320f, 70f), 21,
                    "#EDE6FF", Place.At(0f, -80f))
                .AsDecor();
            var meta = Ui.Label($"ReckoningOffer{index}Meta", UiString.Runtime, new UiVec(320f, 28f), 15,
                    "#B8A8D9", Place.At(0f, -140f))
                .AsDecor();

            var button = Ui.Button($"ReckoningOffer{index}", UiString.Runtime,
                new UiVec(CardWidth, 400f), 14, Place.At(x, 0f));

            button.Children.Add(burst);
            button.Children.Add(icon);
            button.Children.Add(name);
            button.Children.Add(meta);

            OfferButtons.Add(button);
            OfferBursts.Add(burst);
            OfferIcons.Add(icon);
            OfferNames.Add(name);
            OfferMetas.Add(meta);
            return button;
        }
    }
}

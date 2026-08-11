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

        public static ReckoningScreen Build()
        {
            var screen = new ReckoningScreen();
            var inside = new List<UiNode>
            {
                Ui.Label("ReckoningTitle", UiStrings.ReckoningTitle, new UiVec(700f, 64f), 40, "#F2DB9E",
                    Place.At(0f, 318f)).AsDecor(),
                Ui.Label("ReckoningExpHeading", UiStrings.ReckoningExperience, new UiVec(560f, 34f), 20,
                    "#B8A8D9", Place.At(-ColumnX, 248f)).AsDecor(),
            };

            for (int i = 0; i < RowCount; i++)
            {
                inside.Add(screen.BuildRow(i));
            }

            var gold = Ui.Label("ReckoningGoldLabel", UiStrings.ReckoningGold, new UiVec(600f, 52f), 30,
                    "#F2DB9E", Place.At(ColumnX, 252f))
                .AsDecor();
            screen.GoldLabel = gold;
            inside.Add(gold);

            var lootHeading = Ui.Label("ReckoningLootHeading", UiStrings.ReckoningChooseOne, new UiVec(600f, 34f), 20,
                    "#B8A8D9", Place.At(ColumnX, 186f))
                .AsDecor();
            screen.LootHeading = lootHeading;
            inside.Add(lootHeading);

            for (int i = 0; i < ItemOfferTable.OfferCount; i++)
            {
                inside.Add(screen.BuildOffer(i));
            }

            var continueButton = Ui.Button("ReckoningContinueButton", UiStrings.Continue,
                new UiVec(300f, 60f), 22, Place.At(0f, -318f));
            screen.ContinueButton = continueButton;
            inside.Add(continueButton);

            // The frame is a CHILD of the content panel rather than the modal
            // root, because the controller scales this transform to expand it
            // and a scaled dimmer would shrink the dim with it.
            var frame = Ui.Panel("ReckoningFrame", Place.At(0f, 0f),
                    UiSize.Fixed(PanelWidth, PanelHeight), inside)
                .Coloured("#221338F5");
            screen.Frame = frame;

            var content = Ui.Panel("ReckoningContent", Place.At(0f, 0f), UiSize.Fixed(1920f, 1080f), frame);

            // 65%, not the 94% the character overlay uses. The fight is still
            // standing behind this and the expand animation plays against it;
            // dimming it to near-black would make the overlay a screen with
            // extra steps, which is the one thing it is not supposed to be.
            screen.Root = Ui.Modal("ReckoningPanel", "#0A0614A6", content).Inactive();
            return screen;
        }

        // One party member's line: who, what level, and a bar showing what this
        // fight was worth against what they had already.
        private UiNode BuildRow(int index)
        {
            float y = 160f - index * RowPitch;

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
            float y = 96f - index * 112f;

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

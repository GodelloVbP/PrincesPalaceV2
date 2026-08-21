using System.Collections.Generic;
using PrincesPalace.Domain.Progression;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // The reward track, as one long line.
    //
    // A battle pass, read left to right: a hairline rail with a node per level,
    // the reward above it and the level number below, scrolled behind a fixed
    // window that opens centred on where the player actually is.
    //
    // EVERY NODE IS EMITTED WITH ITS REAL POSITION. Level 40 sits at the same x
    // forever, so there is nothing here for a controller to place at runtime --
    // only the content rect slides, which is one anchoredPosition rather than a
    // hundred. That is the difference between this and the descent map, whose
    // node COUNT varies per leg and which therefore needs a pool.
    //
    // A hundred nodes is a lot of tree, and deliberately: UiAudit re-solves all
    // of it at four aspects, so a caption that outgrows its pitch or a dot that
    // escapes the rail is a build failure rather than something to notice in a
    // screenshot later.
    public sealed class RewardTrackScreen
    {
        public const string Rail = "#C8B4DE29";
        public const string RailDone = "#F2DB9E6B";
        public const string DotToCome = "#2E2244";
        public const string DotDone = "#F2DB9E";
        public const string DotHere = "#EDE6FF";
        public const string TextToCome = "#D6C8E852";
        public const string TextDone = "#EDE6FF";
        public const string TextHere = "#F2DB9E";

        public UiNode Root;

        public NodeRef Viewport;
        public NodeRef Content;
        public NodeRef RailFill;
        public NodeRef Summary;
        public NodeRef CloseButton;

        // One entry per level from 2 to 100, in that order -- so index i is
        // level i + RewardTrackLayout.FirstLevel and the controller never has
        // to search.
        public List<NodeRef> Dots = new List<NodeRef>();
        public List<NodeRef> Captions = new List<NodeRef>();
        public List<NodeRef> LevelNumbers = new List<NodeRef>();

        public static RewardTrackScreen Build()
        {
            var screen = new RewardTrackScreen();

            var railChildren = new List<UiNode>();

            // THE RAIL, in two layers: the whole line dim, and a second bar over
            // it that the controller stretches to however far the player has
            // got. Two nodes rather than one tinted per-segment, because
            // "progress along a line" is one rect wide and a hundred coloured
            // hairlines is a hundred things to keep in step.
            railChildren.Add(Ui.Solid("TrackRail", Rail,
                    new UiVec(RewardTrackLayout.ContentWidth, RewardTrackLayout.RailHeight),
                    Place.At(0f, 0f))
                .AsDecor());

            var railFill = Ui.Solid("TrackRailFill", RailDone,
                    new UiVec(RewardTrackLayout.ContentWidth, RewardTrackLayout.RailHeight),
                    Place.At(-RewardTrackLayout.ContentWidth * 0.5f, 0f, new UiVec(0f, 0.5f)))
                .AsDecor();
            screen.RailFill = railFill;
            railChildren.Add(railFill);

            for (int level = RewardTrackLayout.FirstLevel; level <= RewardTrack.MaxLevel; level++)
            {
                railChildren.AddRange(screen.BuildNode(level));
            }

            // Wider than the window it sits in -- that overflow IS the scroll,
            // and the viewport below clips it. Same construction as the map's
            // content rect, whose comment records the same fact.
            var content = Ui.Panel("TrackContent",
                    Place.At(0f, 0f, new UiVec(0f, 0.5f)),
                    UiSize.Fixed(RewardTrackLayout.ContentWidth, SystemMenuLayout.ContentHeight),
                    railChildren)
                .AllowOverflow("the content rect is deliberately wider than the window it sits in - that overflow IS the scroll, and TrackViewport clips it");
            screen.Content = content;

            // The window. A coordinate frame and a mask, never a surface.
            var viewport = Ui.Panel("TrackViewport", Place.Stretch(), UiSize.Fill, content)
                .Clipping()
                .AllowOverlap("a full-bleed viewport spans the summary line it shares the pane with; it draws nothing and has no graphic to intercept a click");
            screen.Viewport = viewport;

            // WHERE YOU ARE, in words, above the rail. The line answers the
            // question the rail cannot at a glance -- what level am I, and how
            // far to the next thing -- and it is the only part of this screen
            // that is legible without scrolling.
            // Narrowed and pushed left so it clears CLOSE on the right. Full
            // RowWidth centred put the two on top of each other, and UiAudit
            // refused: the button draws later, so it would have taken clicks
            // meant for nothing while hiding the end of the sentence.
            const float CloseWidth = 180f;
            const float CloseInset = 110f;
            float summaryWidth = SystemMenuLayout.RowWidth - CloseWidth * 2f - 40f;

            var summary = Ui.Label("TrackSummary", UiString.Runtime,
                new UiVec(summaryWidth, 30f), 18, TextHere,
                Place.At(-40f, SystemMenuLayout.ContentHeight * 0.5f - 34f));
            screen.Summary = summary;

            var close = Ui.Button("TrackCloseButton", UiStrings.TrackClose,
                new UiVec(CloseWidth, 44f), 15,
                Place.At(SystemMenuLayout.PanelWidth * 0.5f - CloseInset,
                         SystemMenuLayout.ContentHeight * 0.5f - 34f));
            screen.CloseButton = close;

            // ITS OWN GROUND, and FULLY OPAQUE.
            //
            // It opens OVER the dossier, so anything less lets three columns of
            // numbers read straight through a hundred captions -- which is
            // exactly what #120A18FA did: the first capture of this screen has
            // Shawn's portrait and his ability scores legible behind the rail.
            // The dossier's own ground is FA because it opens over a painted
            // hub that is meant to stay faintly present; this opens over a
            // screen full of text, which is not.
            var ground = Ui.Solid("TrackGround", "#120A18",
                    new UiVec(SystemMenuLayout.PanelWidth, SystemMenuLayout.ContentHeight),
                    Place.At(0f, 0f))
                .AsDecor();

            screen.Root = Ui.Panel("RewardTrackPanel", Place.At(0f, 0f),
                    UiSize.Fixed(SystemMenuLayout.PanelWidth, SystemMenuLayout.ContentHeight),
                    ground, viewport, summary, close)
                .Inactive();

            return screen;
        }

        private IEnumerable<UiNode> BuildNode(int level)
        {
            float x = RewardTrackLayout.NodeOffsetX(level);
            bool milestone = RewardTrackLayout.IsMilestone(level);
            float diameter = milestone
                ? RewardTrackLayout.MilestoneDiameter
                : RewardTrackLayout.NodeDiameter;

            // THE DOT IS A SQUARE, and that is not laziness: Ui.Solid is a
            // rect, the emitter has no circle primitive, and a rotated diamond
            // reads as a landmark at milestone size better than a soft circle
            // does at this scale. The rail behind it is what carries the line.
            var dot = Ui.Solid($"TrackDot{level}", DotToCome,
                    new UiVec(diameter, diameter), Place.At(x, 0f))
                .AsDecor();
            Dots.Add(dot);
            yield return dot;

            // The reward, above. Two lines of room -- the longest thing the
            // track says is "YOUR SECOND LIFE RETURNS AT EVERY BOSS", which is
            // what set NodePitch in the first place.
            var caption = Ui.Label($"TrackCaption{level}", UiString.Runtime,
                    new UiVec(RewardTrackLayout.CaptionWidth, RewardTrackLayout.CaptionHeight),
                    milestone ? 13 : 11, TextToCome,
                    Place.At(x, RewardTrackLayout.CaptionY))
                .AsDecor();
            Captions.Add(caption);
            yield return caption;

            // The level, below.
            var number = Ui.Label($"TrackLevel{level}", UiString.Runtime,
                    new UiVec(RewardTrackLayout.CaptionWidth, 22f),
                    milestone ? 16 : 12, TextToCome,
                    Place.At(x, RewardTrackLayout.LevelNumberY))
                .AsDecor();
            LevelNumbers.Add(number);
            yield return number;
        }
    }
}

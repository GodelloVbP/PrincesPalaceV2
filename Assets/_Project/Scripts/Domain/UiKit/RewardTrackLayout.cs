using System.Collections.Generic;
using PrincesPalace.Domain.Progression;

namespace PrincesPalace.Domain.UiKit
{
    // Where the reward track's hundred nodes sit along one long line, and where
    // the three bands around that line sit inside the panel.
    //
    // A BATTLE PASS READS LEFT TO RIGHT, so this is one horizontal rail with a
    // node per level and the whole thing scrolled behind a fixed window --
    // exactly the shape MapLayout gives the descent, and for the same reason:
    // the content rect is deliberately wider than the panel it lives in, and
    // that overflow IS the scroll.
    //
    // THE DESIGN HANDOFF MEASURES y DOWNWARD FROM THE PANEL'S TOP-LEFT and this
    // project measures it up from the panel's centre, so every band below is
    // written as the handoff writes it and converted once, by CentreY. Writing
    // the converted numbers straight in would make this file uncheckable
    // against the document it came from -- which is the only reason anybody
    // would ever open both at once.
    //
    // Pure and engine-free, so every coordinate here is pinned by EditMode
    // tests rather than eyeballed in a scene that is regenerated anyway.
    public static class RewardTrackLayout
    {
        // One node per level, from 2 (the first level that pays) to MaxLevel.
        // Level 1 is where a character starts, not somewhere they arrive.
        public const int FirstLevel = RewardTrack.StartingLevel + 1;
        public static int NodeCount => RewardTrack.MaxLevel - FirstLevel + 1;

        // The handoff's coordinate frame, converted: y down from the panel's
        // top edge becomes y up from its centre.
        public static float CentreY(float yFromTop) =>
            SystemMenuLayout.ContentHeight * 0.5f - yFromTop;

        // ---- the rail -----------------------------------------------------------

        // How far apart two levels sit.
        //
        // 190 is chosen from the reward NAMES rather than from the dots: the
        // longest thing the track can say is "YOUR SECOND LIFE RETURNS AT EVERY
        // BOSS", and a caption narrower than about 170 wraps it to four lines
        // and starts clipping. The dots would happily sit at 60.
        public const float NodePitch = 190f;

        public const float NodeDiameter = 26f;

        // 40 -> 44, to make room for the plate ring at inset -7 without the
        // ring crowding the caption above it. The ring is what tells a
        // milestone from filler at a glance now; size alone was the old answer,
        // and the brief's own open questions list it as not enough.
        public const float MilestoneDiameter = 44f;

        // The gold hairline plate ringing a milestone's disc, drawn OUTSIDE it:
        // inset -7 means seven pixels of clearance all round, so the ring is 14
        // wider than the disc it encloses.
        public const float PlateRingInset = 7f;
        public const float PlateRingStroke = 1f;

        public static float DiameterOf(int level) =>
            IsMilestone(level) ? MilestoneDiameter : NodeDiameter;

        public static float PlateRingDiameter(int level) =>
            DiameterOf(level) + PlateRingInset * 2f;

        // The caption block above each node, and the level number below it.
        public const float CaptionWidth = NodePitch - 20f;
        public const float CaptionHeight = 64f;
        public const float CaptionY = 66f;
        public const float LevelNumberY = -44f;
        public const float LevelNumberHeight = 22f;

        // Captions set at 11 filler / 16 milestone; level numbers at 15 / 22.
        // The numbers grew from 12/16 because they stand as FIGURES rather than
        // as a label -- a rail whose landmarks are numbers has to be countable
        // from across the panel.
        public const int CaptionFont = 11;
        public const int MilestoneCaptionFont = 16;
        public const int LevelNumberFont = 15;
        public const int MilestoneLevelNumberFont = 22;

        // The rail itself, in two weights: the unlit line the nodes sit on, and
        // the lit overlay stretched to the player's own node.
        public const float RailHeight = 4f;
        public const float RailLitHeight = 3f;

        // The travelling gold gradient. 300px of light crossing 19,000px of
        // rail, which is what stops a static line reading as a printed rule.
        public const float ShimmerWidth = 300f;

        // The reward mark, drawn inside its node.
        public const float IconSize = 15f;
        public const float MilestoneIconSize = 22f;

        public static float IconSizeOf(int level) =>
            IsMilestone(level) ? MilestoneIconSize : IconSize;

        // THE SEAL PIP, which replaced a 14px tick the brief's own open
        // questions called small and pale. A dark disc with the claimed check
        // knocked out of it, STRADDLING the disc's rim at lower-right rather
        // than sitting inside it -- cutting the edge is what makes it read as
        // applied to the node rather than as part of the reward's own mark.
        //
        // IT SCALES WITH THE DISC, which the handoff does not say and the first
        // build got wrong by following it literally.
        //
        // Section 2 gives one pip size, 15, for both disc sizes. On a 44px
        // milestone that is 34% of the disc and looks exactly like the design.
        // On a 26px filler node it is 58%, and at that size it does not sit
        // beside the reward's mark, it sits ON it: the first capture of this
        // screen has an X for Prince's Favor and a chevron for a stat point
        // that are simply not there -- eighty-seven of ninety-nine nodes with
        // their one scannable feature covered by the tick saying they had been
        // collected.
        //
        // So the RATIO is what carries across, not the pixel count. 15 of 44 is
        // 0.34; the same fraction of 26 is 9.
        public const float MilestoneSealPipSize = 15f;
        public const float SealPipStroke = 1f;

        public static float SealPipSize(int level) =>
            IsMilestone(level) ? MilestoneSealPipSize : 9f;

        // Straddling, expressed as the handoff expresses it and then
        // generalised: its +(d/2 - 8) is 8 = 15 x 0.53, the pip's own radius
        // plus a little, so a milestone still lands on exactly 14.
        //
        // NOTE THE OFFSET IS ON BOTH AXES, so the pip's centre is this times
        // root two from the disc's, which is why a number that looks like it
        // leaves the pip inside the rim in fact cuts it.
        public static float SealPipOffset(int level) =>
            DiameterOf(level) * 0.5f - SealPipSize(level) * 0.5333f;

        // The NEXT caret, above node level+1. ONE node rather than a hundred:
        // exactly one level is ever next, and the caret is the only thing on
        // this rail whose x is not fixed forever -- so it is placed at runtime
        // and nothing else is.
        //
        // WIDE AND FLAT, 26 x 14, which is not the square the handoff implies.
        //
        // The height is fixed by the gap it lives in and cannot grow. Drawn
        // square in that gap it came out as a 14px speck a hundred pixels above
        // the node it points at, and the first capture of the finished screen
        // shows it as a mote most readers would not find -- which fails the
        // open question it exists to answer. Stretched to a filler disc's own
        // width it reads as a pointer over that column instead of as dust.
        public const float CaretWidth = NodeDiameter;
        public const float CaretHeight = 14f;

        // Between the caption's top edge (66 + 64/2 = 98) and the band's top
        // (112 above the line). That 14px gap is the only clear space above a
        // node, and it is exactly one caret tall -- which is why the band is
        // asymmetric about the rail in the first place.
        public const float CaretY = CaptionY + CaptionHeight * 0.5f + CaretHeight * 0.5f;

        // The breathing halo behind the player's own node, and the pulse ring
        // on a waiting one. The halo is a single node the controller moves; the
        // pulse is per-node, because any number of levels can be waiting at
        // once and a migrated character arrives with dozens.
        public const float HaloSize = 106f;
        public const float PulseRingStroke = 1f;

        // Half a pitch of air at each end, so the first and last nodes are not
        // flush against the edge of the scroll.
        public const float EndPadding = NodePitch * 0.5f;

        public static float ContentWidth => NodeCount * NodePitch + EndPadding * 2f;

        // The x of `level`'s node, measured from the LEFT EDGE of the content
        // rect rather than from its centre -- the rect is 19,000px wide and a
        // centre-relative coordinate would be a large number either side of
        // zero, which is harder to check and harder to read in a test.
        public static float NodeX(int level)
        {
            int index = level - FirstLevel;
            if (index < 0) index = 0;
            if (index >= NodeCount) index = NodeCount - 1;

            return EndPadding + index * NodePitch + NodePitch * 0.5f;
        }

        // A node's x for PLACEMENT, which is not the same number as NodeX.
        //
        // Ui.Place.At measures from a parent's CENTRE; NodeX measures from the
        // content rect's LEFT EDGE, because a rail is easier to reason about
        // and to test as "level 40 is 7,600px along" than as a large signed
        // offset either side of a midpoint 9,500px from anywhere.
        //
        // Getting this wrong is not subtle and was not: placing NodeX directly
        // put every node half a content-width too far right, and UiAudit
        // refused the build with "TrackDot100 escapes its parent: 9,330px past
        // the right".
        public static float NodeOffsetX(int level) => NodeX(level) - ContentWidth * 0.5f;

        // ---- the four bands -----------------------------------------------------
        //
        // Handoff section 1, in its own coordinates. The panel is 1600x804 and
        // sits at (160, 186) on a 1920x1080 screen; nothing below needs to know
        // that second fact.

        // Summary: the collect button, where you are, and CLOSE.
        public const float SummaryYFromTop = 43f;         // band 20..66
        public const float SummaryInsetX = 44f;
        public const float SummaryRowHeight = 44f;
        public const float CollectWidth = 260f;
        public const float CloseWidth = 180f;

        public static float SummaryCentreY => CentreY(SummaryYFromTop);

        // Both buttons keep the same clearance from the panel's own edge,
        // whatever they are labelled.
        public static float CollectCentreX =>
            -SystemMenuLayout.PanelWidth * 0.5f + SummaryInsetX + CollectWidth * 0.5f;

        public static float CloseCentreX =>
            SystemMenuLayout.PanelWidth * 0.5f - SummaryInsetX - CloseWidth * 0.5f;

        // Centred on the panel, and no wider than twice the narrower of the two
        // gaps it has to clear.
        //
        // ARITHMETIC OVER THE TWO BUTTONS rather than a measured constant, so
        // relabelling either one cannot leave the summary sitting underneath
        // it. That collision is not hypothetical: the first version of this row
        // centred a full-width label and UiAudit refused the build, because the
        // button draws later and would have taken clicks meant for nothing
        // while hiding the end of the sentence.
        public static float SummaryWidth
        {
            get
            {
                // How much room there is between the panel's centre and the
                // INNER edge of each button. The summary is centred, so its
                // half-width may not exceed the smaller of the two.
                float leftGap = -(CollectCentreX + CollectWidth * 0.5f);
                float rightGap = CloseCentreX - CloseWidth * 0.5f;
                float gap = leftGap < rightGap ? leftGap : rightGap;

                // Sixteen pixels of air, so a font change nudging a button by a
                // pixel does not silently make this the widest thing on the row.
                return (gap - 16f) * 2f;
            }
        }

        // The focus card: what the rail is pointing at, said in words big
        // enough to read without leaning in.
        //
        // 660 wide at x 470 on a 1600 panel is panel-CENTRED, which is worth
        // saying out loud because the handoff states it as a left edge and the
        // two look like different claims.
        public const float CardYFromTop = 193f;           // band 118..268
        public const float CardWidth = 660f;
        public const float CardHeight = 150f;

        // Art plate 104x104, holding 86x86 of art inside a 9px mat.
        public const float CardPlateSize = 104f;
        public const float CardArtSize = 86f;
        public const float CardPlateInsetX = 24f;
        public const float CardTextGap = 28f;

        public static float CardCentreY => CentreY(CardYFromTop);

        public static float CardPlateCentreX =>
            -CardWidth * 0.5f + CardPlateInsetX + CardPlateSize * 0.5f;

        // Everything right of the plate, as one column.
        public static float CardTextLeft =>
            CardPlateCentreX + CardPlateSize * 0.5f + CardTextGap;

        public static float CardTextWidth => CardWidth * 0.5f - CardPlateInsetX - CardTextLeft;

        public static float CardTextCentreX => CardTextLeft + CardTextWidth * 0.5f;

        // The four lines of the card, from its own centre. The reward's name
        // gets the most room because it is the only one whose length the track
        // decides rather than this file.
        public const float CardKickerY = 56f;
        public const float CardKickerHeight = 14f;
        public const int CardKickerFont = 10;
        public const float CardLevelY = 32f;
        public const float CardLevelHeight = 36f;
        public const int CardLevelFont = 30;
        public const float CardCaptionY = -10f;
        public const float CardCaptionHeight = 48f;

        // 22, not the handoff's 32.
        //
        // A DEVIATION, and a measured one: at 32px the longest reward the track
        // can name -- "YOUR SECOND LIFE RETURNS AT EVERY BOSS" -- runs about
        // 700px against the 484 this column has, so it wraps to three lines and
        // clips out of a 48px box. 32 is right for the milestone names the
        // design was drawn against and wrong for the filler ones. If the card
        // grows, this is the number that goes back up.
        public const int CardCaptionFont = 22;
        public const float CardStateY = -54f;
        public const float CardStateHeight = 16f;
        public const int CardStateFont = 11;

        // The rail band. Asymmetric about the rail on purpose: 112px above the
        // line and 100 below, because the caption stack is taller than the
        // level number and the caret needs the difference.
        public const float BandTopFromTop = 290f;
        public const float BandBottomFromTop = 502f;
        public const float BandHairlineFromTop = 501f;
        public const float RailYFromTop = 402f;           // the panel's own vertical centre

        public static float BandHeight => BandBottomFromTop - BandTopFromTop;
        public static float BandCentreY => CentreY((BandTopFromTop + BandBottomFromTop) * 0.5f);

        // How much room there is above the rail before the band's top edge.
        // 112, against 100 below it.
        public static float RailToBandTop => RailYFromTop - BandTopFromTop;

        // THE SCROLLED CONTENT IS TALLER THAN THE BAND, and it has to be.
        //
        // Everything inside that rect is positioned relative to the RAIL, which
        // sits at its centre -- so the rect's half-height is however far the
        // tallest thing reaches from the rail, and the caret reaches 112. Sizing
        // it to the band's own 212 instead put the caret six pixels outside its
        // parent at all four aspects, which is what UiAudit refused: the band is
        // 112 above the line and 100 below, and a rect centred on the rail
        // cannot be both.
        //
        // The extra twelve pixels hang below the band and are clipped by the
        // viewport, which IS the band's height. Nothing is lost: there is
        // nothing down there, because every node's lowest element is its level
        // number at -55.
        public static float ScrollContentHeight => RailToBandTop * 2f;

        // How far the rail sits from the band's centre.
        //
        // Six pixels, and the reason it is not zero is the asymmetry above: the
        // rail has to land on the panel's exact vertical centre (402) while the
        // band around it does not straddle that line evenly.
        //
        // Applied ONCE, to the group holding the rail and every node, so every
        // coordinate inside that group is stated relative to the rail itself
        // and no node carries a correction of its own.
        public static float RailOffsetY => CentreY(RailYFromTop) - BandCentreY;

        // The ascent ribbon: all 99 levels at once, in space the rail cannot
        // use. The brief's sharpest open question was that a player at level 47
        // cannot see level 50 without scrolling; this is the answer to it.
        public const float RibbonLabelYFromTop = 655f;    // label row at 648, 14 tall
        public const float RibbonLabelHeight = 14f;
        public const int RibbonLabelFont = 10;
        public const float RibbonTopFromTop = 682f;
        public const float RibbonBottomFromTop = 768f;
        public const float RibbonInsetX = 80f;

        public static float RibbonWidth => SystemMenuLayout.PanelWidth - RibbonInsetX * 2f;
        public static float RibbonHeight => RibbonBottomFromTop - RibbonTopFromTop;
        public static float RibbonCentreY => CentreY((RibbonTopFromTop + RibbonBottomFromTop) * 0.5f);
        public static float RibbonLabelCentreY => CentreY(RibbonLabelYFromTop);

        // Inside the ribbon, measured from its own centre. The base line runs
        // through the middle; ticks and milestone dots stand above it, level
        // numbers hang below.
        public const float RibbonBaseHeight = 2f;
        public const float RibbonTickWidth = 2f;
        public const float RibbonTickHeight = 14f;
        public const float RibbonDotSize = 7f;
        public const float RibbonDotY = 22f;
        public const float RibbonNumberY = -20f;

        // 26, and the constraint is level 100 rather than the widest numeral.
        //
        // The last node sits at 705.6 of the ribbon's own 720 half-width, so
        // its label has 14.4px of room either side before it leaves the rect.
        // At 40 wide it escaped by 5.6 and UiAudit refused the build. "100" at
        // 10px measures about 17, so 26 fits the widest label the track can
        // carry AND the tightest position it can carry it in -- which are not
        // the same constraint and only one of them is obvious.
        public const float RibbonNumberWidth = 26f;
        public const float RibbonNumberHeight = 14f;
        public const int RibbonNumberFont = 10;
        public const float RibbonPlayheadWidth = 2f;
        public const float RibbonPlayheadHeight = 30f;
        public const float RibbonWindowStroke = 1f;

        // The gap between two ribbon ticks. 1440 across 99 levels is about
        // 14.5px, which is why the ticks are 2px hairlines and not dots: at
        // this pitch anything wider merges into a bar.
        public static float RibbonPitch => RibbonWidth / NodeCount;

        // Where `level` falls along the ribbon, measured from ITS centre --
        // the same left-edge-versus-centre trap NodeOffsetX exists for, and
        // resolved the same way rather than left to each call site.
        public static float RibbonOffsetX(int level) =>
            NodeX(level) / ContentWidth * RibbonWidth - RibbonWidth * 0.5f;

        // How much of the rail a window `viewportWidth` wide can see, drawn to
        // the ribbon's scale. This is the box the player drags.
        public static float RibbonWindowWidth(float viewportWidth)
        {
            float width = viewportWidth / ContentWidth * RibbonWidth;

            // A window narrower than two of its own borders is a smear rather
            // than a box. Nothing in the shipped aspects gets near this; a very
            // narrow canvas would.
            const float Minimum = RibbonWindowStroke * 4f;
            return width < Minimum ? Minimum : width;
        }

        // Where that box sits, given what the rail is currently showing.
        //
        // `scrollLeft` is the content rect's own anchoredPosition.x, which is
        // where its LEFT EDGE sits relative to the window's centre -- so this
        // reads the exact number the controller wrote, rather than a level it
        // would have to infer.
        public static float RibbonWindowOffsetX(float scrollLeft, float viewportWidth)
        {
            float leftEdge = -scrollLeft - viewportWidth * 0.5f;
            float fraction = leftEdge / ContentWidth;

            return fraction * RibbonWidth - RibbonWidth * 0.5f
                   + RibbonWindowWidth(viewportWidth) * 0.5f;
        }

        // ---- scrolling ----------------------------------------------------------

        // How far the content has to slide so `level` sits in the middle of a
        // window `viewportWidth` wide.
        //
        // In the viewport's own centre-origin coordinates: the window spans
        // [-W/2, +W/2] and the content spans [L, L + ContentWidth], where L is
        // what this returns and what the controller writes to
        // anchoredPosition.x.
        public static float ScrollFor(int level, float viewportWidth) =>
            ClampScroll(-NodeX(level), viewportWidth);

        // The same, for a point picked off the ribbon: `fraction` is how far
        // along the whole track the player grabbed.
        //
        // ONE CLAMP FOR BOTH, which is the whole reason this pair lives here
        // rather than as a subtraction at each call site. A drag that clamps
        // and a keypress that does not would disagree about where the end of
        // the track is, and only one of them could be right.
        public static float ScrollForFraction(float fraction, float viewportWidth) =>
            ClampScroll(-fraction * ContentWidth, viewportWidth);

        // CLAMPED AT BOTH ENDS. Without this, opening on level 2 scrolls the
        // rail off the right of the window and opening on level 100 scrolls it
        // off the left, and in both cases the player is looking at empty space
        // with their own progress just out of frame.
        public static float ClampScroll(float left, float viewportWidth)
        {
            // Left edge may not come right of the window's left edge; right
            // edge may not come left of the window's right edge.
            float highest = -viewportWidth * 0.5f;
            float lowest = viewportWidth * 0.5f - ContentWidth;
            if (lowest > highest) lowest = highest;

            if (left > highest) left = highest;
            if (left < lowest) left = lowest;

            return left;
        }

        // How far `level`'s node is from the middle of the window, in pixels.
        // What the depth-of-field falloff is measured against -- nodes near the
        // edges of a 1600px window are context rather than content, and drawing
        // them at full strength is what made the rail read as a wall.
        public static float DistanceFromWindowCentre(int level, float scrollLeft) =>
            NodeX(level) + scrollLeft;

        // Node opacity falls from 1 to 0.32, starting 300px off the window's
        // centre and reaching the floor 620px further out. Handoff section 7.
        public const float DepthOfFieldFrom = 300f;
        public const float DepthOfFieldOver = 620f;
        public const float DepthOfFieldFloor = 0.32f;

        public static float DepthOfFieldAt(float distanceFromCentre)
        {
            float distance = distanceFromCentre < 0f ? -distanceFromCentre : distanceFromCentre;
            if (distance <= DepthOfFieldFrom) return 1f;

            float t = (distance - DepthOfFieldFrom) / DepthOfFieldOver;
            if (t >= 1f) return DepthOfFieldFloor;

            return 1f + (DepthOfFieldFloor - 1f) * t;
        }

        // Which node is nearest the middle of the window right now. What an
        // arrow key steps away from, and what the card falls back to when the
        // pointer is nowhere.
        //
        // Rounded rather than floored: half a pitch either side of a node is
        // that node's territory, and flooring would make the left arrow a no-op
        // whenever the rail is clamped at its right end.
        public static int LevelAtCentre(float scrollLeft)
        {
            float atCentre = -scrollLeft;
            float exact = (atCentre - EndPadding - NodePitch * 0.5f) / NodePitch;

            int index = (int)(exact + (exact < 0f ? -0.5f : 0.5f));

            int level = FirstLevel + index;
            if (level < FirstLevel) level = FirstLevel;
            if (level > RewardTrack.MaxLevel) level = RewardTrack.MaxLevel;

            return level;
        }

        // ---- what a node carries ------------------------------------------------

        // Which mark a level's node carries.
        //
        // FOUR GRANT MARKS AND ONE RING, rather than one icon per reward kind.
        // Every node already carries its reward in words, so a twelfth bespoke
        // shape would be doing what the caption does; what the rail needs is to
        // be scannable WITHOUT reading. The ring stands for "a capability",
        // which is what every milestone is.
        //
        // These are now the SILHOUETTE BRIEF for the art slots in handoff
        // section 8 rather than the finished look: each one is the ghost glyph
        // its slot draws until painted art lands, and the stroke is what that
        // painting has to still read as at 15px.
        public static string IconFor(int level)
        {
            switch (RewardTrack.At(level).Reward)
            {
                case TrackReward.None: return null;
                case TrackReward.StatPoint: return "proc:track_stat";
                case TrackReward.Favor: return "proc:track_favor";
                case TrackReward.MaxHealth: return "proc:track_health";
                case TrackReward.ExpFind: return "proc:track_exp";
                default: return "proc:ring_outline";
            }
        }

        // The hue a reward kind tints its art-slot mat with.
        //
        // THE MAT ONLY, at 2E alpha or below, and only on an unreached node --
        // handoff section 5. A lit disc is gold metal, and a tint laid over
        // that reads as tarnish, which is the same way the flat-fill disc
        // failed before the gradient replaced it.
        public static string MatTintFor(int level)
        {
            switch (RewardTrack.At(level).Reward)
            {
                case TrackReward.StatPoint: return "#EDE6FF2E";
                case TrackReward.ExpFind: return "#C8B4DE2E";
                case TrackReward.MaxHealth: return "#D8B4A82E";
                case TrackReward.Favor: return "#F2DB9E2E";
                default: return "#C8B4DE1F";
            }
        }

        // The twelve landmarks, in rail order.
        //
        // ONE ENUMERATION FOR THE SCREEN AND THE CONTROLLER, which is the whole
        // reason it is here rather than a loop in each. The ribbon emits a
        // numbered dot per milestone and the controller fills those numbers in;
        // two independent "for level, if IsMilestone" loops would agree until
        // one of them gained a condition, and the failure would be a ribbon
        // labelled off-by-one rather than anything that throws.
        public static IEnumerable<int> MilestoneLevels()
        {
            for (int level = FirstLevel; level <= RewardTrack.MaxLevel; level++)
            {
                if (IsMilestone(level)) yield return level;
            }
        }

        // Milestones are drawn larger, because a rail of a hundred identical
        // dots has no landmarks and nothing for the eye to count from.
        //
        // ASKED OF THE TRACK, not guessed from the reward kind. This started as
        // a switch excluding the filler kinds and got level 10 wrong: Prince's
        // Favor +5 is a milestone and +2 Prince's Favor is filler, and they are
        // the same kind. RewardTrack.IsMilestone reads the table that decides.
        public static bool IsMilestone(int level) => RewardTrack.IsMilestone(level);
    }
}

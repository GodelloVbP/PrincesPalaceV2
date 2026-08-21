using PrincesPalace.Domain.Progression;

namespace PrincesPalace.Domain.UiKit
{
    // Where the reward track's hundred nodes sit along one long line.
    //
    // A BATTLE PASS READS LEFT TO RIGHT, so this is one horizontal rail with a
    // node per level and the whole thing scrolled behind a fixed window --
    // exactly the shape MapLayout gives the descent, and for the same reason:
    // the content rect is deliberately wider than the panel it lives in, and
    // that overflow IS the scroll.
    //
    // Pure and engine-free, so every coordinate here is pinned by EditMode
    // tests rather than eyeballed in a scene that is regenerated anyway.
    public static class RewardTrackLayout
    {
        // One node per level, from 2 (the first level that pays) to MaxLevel.
        // Level 1 is where a character starts, not somewhere they arrive.
        public const int FirstLevel = RewardTrack.StartingLevel + 1;
        public static int NodeCount => RewardTrack.MaxLevel - FirstLevel + 1;

        // How far apart two levels sit.
        //
        // 190 is chosen from the reward NAMES rather than from the dots: the
        // longest thing the track can say is "YOUR SECOND LIFE RETURNS AT EVERY
        // BOSS", and a caption narrower than about 170 wraps it to four lines
        // and starts clipping. The dots would happily sit at 60.
        public const float NodePitch = 190f;

        public const float NodeDiameter = 26f;
        public const float MilestoneDiameter = 40f;

        // The caption block above each node, and the level number below it.
        public const float CaptionWidth = NodePitch - 20f;
        public const float CaptionHeight = 64f;
        public const float CaptionY = 66f;
        public const float LevelNumberY = -44f;

        // The rail itself: a hairline the nodes sit on.
        public const float RailHeight = 2f;

        // The reward mark, drawn inside its node.
        public const float IconSize = 15f;
        public const float MilestoneIconSize = 22f;

        // The claimed tick, in the node's lower-right corner where it overlaps
        // nothing: the caption is above and the level number below.
        public const float TickSize = 14f;
        public const float TickOffset = 11f;

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

        // How far the content has to slide so `level` sits in the middle of a
        // window `viewportWidth` wide.
        //
        // In the viewport's own centre-origin coordinates: the window spans
        // [-W/2, +W/2] and the content spans [L, L + ContentWidth], where L is
        // what this returns and what the controller writes to
        // anchoredPosition.x.
        //
        // CLAMPED AT BOTH ENDS, which is the whole reason this is a function
        // and not a subtraction at the call site: without the clamp, opening on
        // level 2 scrolls the rail off the right of the window and opening on
        // level 100 scrolls it off the left, and in both cases the player is
        // looking at empty space with their own progress just out of frame.
        public static float ScrollFor(int level, float viewportWidth)
        {
            float centred = -NodeX(level);

            // Left edge may not come right of the window's left edge; right
            // edge may not come left of the window's right edge.
            float highest = -viewportWidth * 0.5f;
            float lowest = viewportWidth * 0.5f - ContentWidth;
            if (lowest > highest) lowest = highest;

            if (centred > highest) centred = highest;
            if (centred < lowest) centred = lowest;

            return centred;
        }

        // Which mark a level's node carries.
        //
        // FOUR GRANT MARKS AND ONE RING, rather than one icon per reward kind.
        // Every node already carries its reward in words, so a twelfth bespoke
        // shape would be doing what the caption does; what the rail needs is to
        // be scannable WITHOUT reading. The ring stands for "a capability",
        // which is what every milestone is.
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

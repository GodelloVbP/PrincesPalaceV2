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

        // "The panel's top edge" is not SystemMenuLayout.ContentHeight * 0.5f
        // (402, the raw frame) -- ContentTop is the pane's own declared
        // content half-extent instead (357.78, same as DossierLayout/
        // OptionsLayout/ExitsLayout's HalfHeight -- see SystemMenuLayout.
        // PaneContentHalfWidth/HalfHeight's own comment; this pane's ground
        // was a Gold 2:1 Container's own measured inset when that difference
        // was introduced, balance-bot 2026-09-02, and is a bare Panel now,
        // owner's call 2026-09-07, but the boundary is the same number
        // either way). Every yFromTop offset below is still the handoff's
        // own number, unmoved: changing this one constant shifts the whole
        // rail/card/summary block down together by the inset delta (44.22)
        // without touching any of the ~20 individual offsets.
        public static float ContentTop => SystemMenuLayout.PaneContentHalfHeight;

        // The handoff's coordinate frame, converted: y down from the panel's
        // top edge becomes y up from its centre.
        public static float CentreY(float yFromTop) =>
            ContentTop - yFromTop;

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

        // ---- what makes a milestone a milestone ---------------------------------
        //
        // Size, a plate ring, a bigger caption and a bigger number were the
        // whole of it, and all four are STATIC. Twelve landmarks on a rail that
        // shimmers, breathes and pulses everywhere else were the only things on
        // it holding perfectly still -- which reads as printed rather than as
        // important.
        //
        // Two ambient cues answer that, and neither carries information: an
        // aura that breathes behind the disc, and a broken ring that turns
        // around it. State is still said by colour and by the pulse; these say
        // only that the thing is alive.
        //
        // NOT IN THE DESIGN HANDOFF. Section 6's milestone treatment is the
        // four static differences above; this is an addition on top of it,
        // asked for after seeing the built screen.
        // TWICE THE DISC, and it cannot usefully be much more: the caption's
        // lower edge is 50 above the rail and the level number's upper edge 33
        // below it, so 88 is very nearly all the room a node has.
        //
        // Which is also why the ALPHA had to carry this rather than the size.
        // The glow is brightest at its centre and the centre is behind an
        // opaque 44px disc, so only its outer half is ever seen -- the half
        // that has already fallen to a third of full. The first attempt was
        // authored at the value it should LOOK like and came out invisible.
        public const float MilestoneAuraSize = MilestoneDiameter * 2f;

        // THE ORBIT IS THE PLATE RING ITSELF, not a second circle outside it.
        //
        // It was a second circle for one build, at 60 against the plate's 58,
        // and two hairlines two pixels apart are one hairline: the solid ring
        // filled the turning one's gaps exactly, so the dashes were invisible
        // and the rotation had nothing to show. Moving it outward is not
        // available either -- 66 is where the level number starts.
        //
        // So the milestone's ring is the dashed sprite and turns, and the
        // design's "gold hairline plate ring at inset -7" keeps its radius and
        // its weight while losing its continuity. That is the trade: a solid
        // ring cannot be seen to move, and being seen to move is the ask.

        public static float PlateRingDiameter(int level) =>
            DiameterOf(level) + PlateRingInset * 2f;

        // The caption block above each node, and the level number below it.
        //
        // THE CAPTION HANGS FROM ITS BOTTOM EDGE rather than floating at its
        // centre, which is a two-part statement: the box's lower edge sits 50
        // above the rail and never moves, and the text inside it is
        // UiTextAlign.Bottom.
        //
        // Centred was the first build and it is wrong in a way that only shows
        // up across a hundred nodes at once. Eighty-seven filler captions are
        // one line and twelve milestones are two to four, so a centred block
        // puts the last line of a one-line caption 66 above the rail and the
        // last line of a four-line one at 34 -- the gap between a reward's name
        // and its own dot changing by half its own height depending on how long
        // the name happens to be. Hung from the bottom, every caption on the
        // rail ends at the same distance from its node and the long ones grow
        // upward, away, into the space above.
        public const float CaptionWidth = NodePitch - 20f;
        public const float CaptionHeight = 64f;
        public const float CaptionBottomY = 50f;
        public const float CaptionY = CaptionBottomY + CaptionHeight * 0.5f;
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
        // ON it, at lower-right, just inside the rim.
        //
        // ONE SIZE FOR BOTH DISCS, which is what the design says and is not
        // what the first build did. It scaled the pip with the disc -- 15 on a
        // milestone, 9 on filler -- on the measured grounds that 15px of pip
        // on a 26px disc lands on the reward's own mark rather than beside it.
        // That measurement was right and the conclusion was wrong: the pip is
        // MEANT to overlap the mark's lower-right quadrant, and what made the
        // first version unreadable was not its size but that its check was
        // knocked out rather than drawn (see the seal_mark bake). At 9px the
        // pip cleared the mark and became a smudge nobody could identify;
        // at 15 it covers a corner of a mark that is still legible from the
        // other three, and the check inside it is the size of a check.
        public const float SealPipSize = 15f;
        public const float SealPipStroke = 1f;

        // THE PIP'S CENTRE SITS ON THE DISC'S RIM, half a pixel inside it, on
        // both axes -- so it is cut by the rim at 45 degrees and hangs half
        // outside the node it belongs to. That is what makes it read as applied
        // to the node rather than as part of the reward's own mark, and it is
        // also what keeps it clear of that mark: at 15px on a 26px disc the
        // pip's near edge is still 10px from the mark's own edge.
        //
        // THE DESIGN'S NUMBER IS +(d/2 - 8) AND IT IS AN EDGE, NOT A CENTRE,
        // which is worth writing down because reading it as a centre is an
        // easy mistake that lands the pip squarely on top of the mark. In the
        // prototype the pip is positioned by its top-left corner with no
        // centring transform, unlike the halo and the pulse ring beside it
        // which both carry translate(-50%,-50%) -- so d/2 - 8 puts its LEFT
        // EDGE there and its centre 7.5 further out, at d/2 - 0.5.
        public const float SealPipRimBite = 0.5f;

        public static float SealPipOffset(int level) =>
            DiameterOf(level) * 0.5f - SealPipRimBite;

        // ---- the NEXT mark ------------------------------------------------------
        //
        // Above node level+1: the word NEXT, and a hairline dropping from it
        // toward the node, fading out as it goes. ONE group rather than a
        // hundred -- exactly one level is ever next, and this is the only thing
        // on the rail whose x is not fixed forever, so it is placed at runtime
        // and nothing else is.
        //
        // A BARE CHEVRON WAS THE FIRST BUILD AND IT FAILED ITS ONE JOB. It sat
        // in the 14px gap above the caption -- the only clear space left once
        // the caption block was centred -- which put it 105px above the rail,
        // at the very top of the band, a 26x14 speck with a hundred pixels of
        // caption between it and the node it pointed at. The first capture
        // shows it reading as dust on the band's edge.
        //
        // Hanging the caption from a fixed bottom edge opened the space this
        // actually needs: the strip between the caption's lower edge at 50 and
        // the disc's own rim. A word plus a line into that gap is unmistakable
        // where a chevron above the caption was invisible.
        public const float NextMarkTopY = 46f;
        public const float NextLabelHeight = 12f;
        public const float NextLabelGap = 5f;
        public const float NextTickHeight = 14f;
        public const float NextTickWidth = 1f;

        // Wide enough for the word at 9px tracked .24em, and no wider: this
        // hangs over whichever column is next, and a box wider than the pitch
        // would reach into its neighbours.
        public const float NextMarkWidth = 60f;
        public const int NextLabelFont = 9;

        public static float NextMarkHeight =>
            NextLabelHeight + NextLabelGap + NextTickHeight;

        public static float NextMarkCentreY => NextMarkTopY - NextMarkHeight * 0.5f;

        // Inside the group, from ITS centre: the word at the top, the tick
        // hanging under it.
        public static float NextLabelCentreY =>
            NextMarkHeight * 0.5f - NextLabelHeight * 0.5f;

        public static float NextTickCentreY =>
            -NextMarkHeight * 0.5f + NextTickHeight * 0.5f;

        // The breathing halo behind the player's own node, and the pulse ring
        // on a waiting one. The halo is a single node the controller moves and
        // SIZES; the pulse is per-node, because any number of levels can be
        // waiting at once and a migrated character arrives with dozens.
        //
        // SIXTEEN PIXELS WIDER THAN THE DISC IT IS BEHIND, which is a glow and
        // not a spotlight. It was a flat 106 -- four times a filler disc --
        // and the first capture of the screen shows the player's own node as a
        // white smear a hundred pixels across with its mark invisible inside
        // it: the one node the eye is meant to land on was the one node whose
        // reward could not be read. The design breathes it to 1.5x, so 42
        // reaches 63 at the top of its cycle, which is where 106 started.
        public const float HaloPad = 16f;
        public const float PulseRingStroke = 1f;

        public static float HaloSize(int level) => DiameterOf(level) + HaloPad;

        // ---- the claim burst -----------------------------------------------------
        //
        // FOUR PARTS, NOT ONE, and the reason is what a single scaling sprite
        // can and cannot say. The first build was one rarity_burst node growing
        // from 0.3 to 3.4 and fading: it reads as a thing getting bigger, which
        // is an event happening NEAR the node rather than TO it.
        //
        // A burst that lands reads as four things at once -- a flash with no
        // shape, a shockwave leaving, rays fanning out, and debris thrown clear.
        // Each is cheap on its own; what they buy together is the difference
        // between a transition and an impact.
        public const float BurstCoreSize = 96f;
        public const float BurstRingSize = 40f;
        public const float BurstRaysSize = 120f;
        public const float BurstSparkSize = 5f;

        // How far a spark travels from the node it left.
        public const float BurstSparkReach = 46f;
        public const int BurstSparkCount = 6;

        // FOUR RIGS, CYCLED. Claims land 130ms apart and a burst lives about
        // 700, so up to five overlap -- and with one shared rig the player sees
        // each burst cut off at its 130th millisecond by the next. Four is what
        // covers the overlap at that stagger without emitting a fifth nobody
        // ever sees.
        public const int BurstRigs = 4;

        // The size of the rig's own rect, which has to hold the largest thing
        // in it at its largest: the ring reaches 3.4x.
        public static float BurstRigSize => BurstRingSize * 3.4f;

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

        // THE PANEL'S OWN FRAME: four corner brackets, 18px arms at 26px inset.
        //
        // Not a full border. The panel already has one from the system menu
        // around it, and a second full rectangle inside the first reads as a
        // mistake; four corners read as a plate. It is the cheapest thing on
        // this screen and does more than its price to stop 1600x804 of flat
        // ground looking like an untextured rect.
        public const float CornerInset = 26f;
        public const float CornerArm = 18f;
        public const float CornerStroke = 1f;

        // The pane's own declared content half-extents, not SystemMenuLayout.
        // PanelWidth/ContentHeight * 0.5f -- these corners are children of
        // the content panel Ui.SystemMenuPane insets (1488x715.56, not the
        // raw 1600x804 frame), so anchoring them to the raw frame's
        // half-extent put them 30px past the left/right and 18px past the
        // top/bottom of the box that actually contains them. Substituting
        // for a painted border was true while this ground was a Gold 2:1
        // Container (balance-bot, 2026-09-02); it is bare now (owner's call,
        // 2026-09-07), which makes these hairlines the only thing marking
        // the pane's edge at all.
        public static float CornerCentreX =>
            SystemMenuLayout.PaneContentHalfWidth - CornerInset - CornerArm * 0.5f;

        public static float CornerCentreY =>
            SystemMenuLayout.PaneContentHalfHeight - CornerInset - CornerArm * 0.5f;

        // A soft violet wash at the panel's middle, so the ground has a centre.
        // The design states it as a radial gradient at 72% of the width and 58%
        // of the height, which are RADII -- hence the doubling. Still measured
        // off the raw PanelWidth/ContentHeight (not the pane's own tighter
        // content box): it is a gradient that fades to nothing well inside its
        // own rect and is built with AllowOverflow for exactly that reason, so
        // there is no containment box for this one to clear.
        public static float WashWidth => SystemMenuLayout.PanelWidth * 1.44f;
        public static float WashHeight => SystemMenuLayout.ContentHeight * 1.16f;

        // The pane's own content canvas -- narrower than the declared
        // 1600x804 frame, per SystemMenuLayout.PaneContentHalfWidth/
        // HalfHeight's own comment. What the band wash and its edge hairlines size
        // themselves to instead of the raw PanelWidth.
        public static float PaneContentWidth => SystemMenuLayout.PaneContentHalfWidth * 2f;

        // Summary: the collect button, where you are, and CLOSE.
        public const float SummaryYFromTop = 43f;         // band 20..66
        public const float SummaryInsetX = 44f;
        public const float SummaryRowHeight = 44f;
        public const float CollectWidth = 260f;
        public const float CloseWidth = 180f;

        // TrackCollectButton/TrackCloseButton now wear a themed kit plate
        // (owner's HQ-kit instruction, 2026-09-07) in place of the hand-drawn
        // hairline rim the two used to share at a flat SummaryRowHeight.
        // static readonly, not const, for the same reason MainMenuScreen.
        // ResetHoldHeight is: Ui.PlateNominalSizeFor is not a compile-time
        // constant. The two land on DIFFERENT plate shapes at their own
        // width (260x44 is nearest Row6x1's 6:1, an 8.5% miss; 180x44 is
        // nearest FiveByOne's 5:1, an 18.2% miss) -- forcing them to a
        // shared height would leave one of the two still stretching its
        // plate, which is the exact thing ThemedButtonAspectLintTests
        // exists to catch.
        public static readonly float CollectHeight = Ui.PlateNominalSizeFor(CollectWidth, SummaryRowHeight).Y;
        public static readonly float CloseHeight = Ui.PlateNominalSizeFor(CloseWidth, SummaryRowHeight).Y;

        // ---- the summary line, as five pieces --------------------------------
        //
        // "LEVEL 47 -- NEXT AT 48   A STAT POINT", set as a rule of parts
        // rather than as one sentence: the word LEVEL small and letterspaced,
        // the figure itself at 40px, a hairline, then where the next reward is
        // and what it is.
        //
        // ONE LABEL WAS THE FIRST BUILD and it said the same words at one size,
        // which made the only number on the row that changes -- the one the
        // whole screen is about -- the same weight as the word in front of it.
        //
        // THE REWARD'S NAME IS BACK, and it was removed once for a good reason:
        // at eighty characters it collided with CLOSE. What has changed is that
        // it is now a piece with a box of its own, so the audit measures it
        // against 360px rather than against whatever is left of the row -- and
        // the longest name the track can say fits that at 12px with room over.
        public const float LevelWordWidth = 80f;
        public const int LevelWordFont = 13;
        public const float LevelFigureWidth = 80f;
        public const int LevelFigureFont = 34;
        public const float SummaryRuleWidth = 34f;
        public const float NextAtWidth = 130f;
        public const int NextAtFont = 12;

        // 420 for a name that measures about 320 at its longest.
        //
        // GENEROUS ON PURPOSE, because nothing measures it: the text fit audit
        // skips content-derived strings, so this box is checked by arithmetic
        // and by eye rather than at build time. The longest thing the track can
        // say is RewardTrackNames' "YOUR SECOND LIFE RETURNS AT EVERY BOSS" at
        // 38 characters, and the row has 200px spare before it reaches CLOSE.
        public const float NextRewardWidth = 420f;
        public const float SummaryGap = 18f;

        public static float SummaryPartsWidth =>
            LevelWordWidth + LevelFigureWidth + SummaryRuleWidth
            + NextAtWidth + NextRewardWidth + SummaryGap * 4f;

        // Each piece's centre, walked left to right from the assembly's own
        // left edge. Written as a walk rather than as five constants so that
        // widening one piece moves the rest instead of overlapping them.
        public static float SummaryPartX(int index)
        {
            float[] widths =
            {
                LevelWordWidth, LevelFigureWidth, SummaryRuleWidth,
                NextAtWidth, NextRewardWidth,
            };

            float x = -SummaryPartsWidth * 0.5f;
            for (int i = 0; i < index && i < widths.Length; i++)
            {
                x += widths[i] + SummaryGap;
            }

            return x + widths[index] * 0.5f;
        }

        public static float SummaryCentreY => CentreY(SummaryYFromTop);

        // Both buttons keep the same clearance from the panel's own edge,
        // whatever they are labelled.
        //
        // SystemMenuLayout.PaneContentHalfWidth, NOT PanelWidth * 0.5f
        // (balance-bot, 2026-09-02) -- these are children of the pane's own
        // content panel (744 half-width, not the declared 800), and at the
        // old reference both buttons ran 12px past their own edge of it.
        public static float CollectCentreX =>
            -SystemMenuLayout.PaneContentHalfWidth + SummaryInsetX + CollectWidth * 0.5f;

        public static float CloseCentreX =>
            SystemMenuLayout.PaneContentHalfWidth - SummaryInsetX - CloseWidth * 0.5f;

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
        //
        // THE SLOT IS 86 AND THE GHOST GLYPH INSIDE IT IS 46, which are two
        // different measurements and were one. Section 8 gives 86 as the plate
        // art is DELIVERED for; the prototype draws its placeholder mark at 46
        // and at 62% opacity, because a stroke blown up to fill an 86px square
        // stops reading as a mark and starts reading as a diagram. The slot
        // keeps its size for the day art lands; what stands in it until then is
        // a mark at a mark's size.
        public const float CardPlateSize = 104f;
        public const float CardArtSize = 86f;
        public const float CardMarkSize = 46f;
        public const float CardPlateInsetX = 28f;
        public const float CardTextGap = 26f;

        // The plate's own corner ticks, 9px arms 5px in -- the same gesture the
        // panel's four corners make, at the scale of a single object. This is
        // what makes an empty slot read as a FRAME AWAITING ART rather than as
        // a violet square somebody forgot to fill.
        public const float CardTickInset = 5f;
        public const float CardTickArm = 9f;

        public static float CardCentreY => CentreY(CardYFromTop);

        public static float CardPlateCentreX =>
            -CardWidth * 0.5f + CardPlateInsetX + CardPlateSize * 0.5f;

        // Everything right of the plate, as one column.
        public static float CardTextLeft =>
            CardPlateCentreX + CardPlateSize * 0.5f + CardTextGap;

        public static float CardTextWidth => CardWidth * 0.5f - CardPlateInsetX - CardTextLeft;

        public static float CardTextCentreX => CardTextLeft + CardTextWidth * 0.5f;

        // The card's three rows, from its own centre.
        //
        // A HEADER RULE, A NAME, AND A FOOTER, which is a different shape from
        // the four stacked centred lines this was built as. The design sets the
        // kicker hard left and the level hard right on one line with a hairline
        // running between them, then the reward's name below, then a divider
        // and the state along the bottom -- so the card reads as a plate with a
        // head and a foot rather than as four sentences in a column.
        //
        // The content occupies 106 of the card's 150, matching the art plate
        // beside it: 22 of padding top and bottom, exactly as the design gives
        // it, and the plate is what sets the height in the first place. Every
        // row below is inside +/-53 because of that, which is why none of them
        // is written as an offset from a shared constant -- they are authored
        // positions in a band, not a stack.
        public const float CardHeaderY = 36f;
        public const float CardHeaderHeight = 34f;
        public const float CardKickerWidth = 200f;
        public const float CardKickerHeight = 14f;
        public const int CardKickerFont = 10;
        public const float CardLevelWordWidth = 40f;
        public const int CardLevelWordFont = 13;

        // 18, not the kicker's 14: this sets three points larger, and a 13px
        // line needs 16.9 of height before its descenders are anywhere. The
        // text-fit audit refused the build over the 1.9 difference, which is
        // the whole argument for having one.
        public const float CardLevelWordHeight = 18f;
        public const float CardLevelWidth = 60f;
        public const float CardLevelHeight = 34f;
        public const int CardLevelFont = 30;

        // Between the kicker and the LVL that follows it, with 14px of air at
        // each end. Derived rather than authored, so a longer kicker cannot
        // silently run under the rule.
        public const float CardRuleGap = 14f;

        // 48 tall, which is two lines at the caption's 22px.
        //
        // The longest reward name measures about 460 against this column's 474,
        // so it fits on one line by fourteen pixels -- and a box sized for one
        // line would clip it the moment a name, a font or the plate's inset
        // moves by more than that. Two lines of room is the margin.
        public const float CardCaptionY = -5f;
        public const float CardCaptionHeight = 48f;
        public const float CardDividerY = -31f;

        // 22, not the handoff's 32.
        //
        // A DEVIATION, and a measured one: at 32px the longest reward the track
        // can name -- "YOUR SECOND LIFE RETURNS AT EVERY BOSS" -- runs about
        // 700px against the 484 this column has, so it wraps to three lines and
        // clips out of a 48px box. 32 is right for the milestone names the
        // design was drawn against and wrong for the filler ones. If the card
        // grows, this is the number that goes back up.
        public const int CardCaptionFont = 22;
        public const float CardStateY = -45f;
        public const float CardStateHeight = 14f;
        public const int CardStateFont = 11;

        // The state's own dot, before its words: 8px, coloured by state. The
        // one thing on the card that says which of the four states this is
        // without being read.
        public const float CardStateDotSize = 8f;
        public const float CardStateDotGap = 11f;

        // ---- the card's columns ---------------------------------------------
        //
        // Everything above is a y; these are the xs, and they are computed from
        // the plate rather than authored, because the plate's inset is the only
        // number the design states and every column left of the card's right
        // edge follows from it.

        public static float CardTextRight => CardWidth * 0.5f - CardPlateInsetX;

        public static float CardLevelCentreX => CardTextRight - CardLevelWidth * 0.5f;

        public static float CardLevelWordCentreX =>
            CardTextRight - CardLevelWidth - CardLevelWordWidth * 0.5f;

        public static float CardKickerCentreX => CardTextLeft + CardKickerWidth * 0.5f;

        // What is left of the header row once the kicker and the level have
        // taken theirs, less a gap at each end.
        public static float CardRuleWidth =>
            CardTextRight - CardLevelWidth - CardLevelWordWidth
            - (CardTextLeft + CardKickerWidth) - CardRuleGap * 2f;

        public static float CardRuleCentreX =>
            CardTextLeft + CardKickerWidth + CardRuleGap + CardRuleWidth * 0.5f;

        // The state row: a dot at the column's left edge, its words after.
        public static float CardStateDotCentreX =>
            CardTextLeft + CardStateDotSize * 0.5f;

        public static float CardStateTextLeft =>
            CardTextLeft + CardStateDotSize + CardStateDotGap;

        // THE FOOTER ROW CARRIES TWO FACTS, not one, since progression v2
        // phase 5: what the player's standing with this node is (left, after
        // the dot) and how many fights the next level is (right).
        //
        // THE SAME LINE RATHER THAN A NEW ONE, and the card's own geometry is
        // why. Its content occupies 106 of 150 with 22 of padding above and
        // below -- the art plate beside it is what sets that -- so a fifth row
        // would have to eat the padding the plate defines. The footer had 455px
        // for a line whose longest string measures under 200, which is where
        // the room actually was.
        public const float CardStateWordsWidth = 224f;
        public const float CardFightsGap = 12f;

        public static float CardStateTextWidth => CardTextRight - CardStateTextLeft;

        public static float CardStateCentreX =>
            CardStateTextLeft + CardStateWordsWidth * 0.5f;

        // What is left of the footer once the state has taken its share, less
        // the gap between them. Derived rather than authored for the reason
        // every other column here is: widening the state's box has to narrow
        // this one, not overlap it.
        public static float CardFightsWidth =>
            CardTextRight - CardStateTextLeft - CardStateWordsWidth - CardFightsGap;

        public static float CardFightsCentreX =>
            CardTextRight - CardFightsWidth * 0.5f;

        // The rail band. Asymmetric about the rail on purpose: 112px above the
        // line and 100 below, because the caption stack is taller than the
        // level number and the caret needs the difference.
        public const float BandTopFromTop = 290f;
        public const float BandBottomFromTop = 502f;
        public const float BandHairlineFromTop = 501f;

        // NOT a fixed 402 any more (balance-bot, 2026-09-02). 402 was "the
        // panel's own vertical centre" only because CentreY's old reference
        // (the raw 804-tall frame's own half-height) WAS 402 -- RailYFromTop
        // being equal to that reference is what made CentreY(RailYFromTop)
        // land on y=0. Now that the reference is ContentTop (357.78, the
        // pane's own declared content half-extent), the same identity needs
        // RailYFromTop to track ContentTop itself: the true vertical centre
        // of the content panel is exactly ContentTop below its own content
        // top, whatever that top happens to be.
        public static float RailYFromTop => ContentTop;

        // The two hairlines stop short of the panel's edge and fade out before
        // they get there -- see the hairline_fade bake. A rule that runs the
        // full 1600 meets the panel's own border at a hard T, which is the one
        // junction that makes a drawn line look like a table.
        public const float BandEdgeInsetX = 44f;

        // PaneContentWidth, NOT SystemMenuLayout.PanelWidth (balance-bot,
        // 2026-09-02) -- these hairlines are children of the pane's own
        // content panel, 1488 wide rather than the declared 1600 frame; at
        // the old width they ran 12px past that content panel's own left and
        // right edges.
        public static float BandEdgeWidth =>
            PaneContentWidth - BandEdgeInsetX * 2f;

        // The bloom under the lit rail. Fifteen pixels of it, against the
        // rail's own three.
        public const float RailGlowHeight = 15f;

        public static float BandHeight => BandBottomFromTop - BandTopFromTop;
        public static float BandCentreY => CentreY((BandTopFromTop + BandBottomFromTop) * 0.5f);

        // How much room there is above the rail before the band's top edge.
        // 112, against 100 below it.
        public static float RailToBandTop => RailYFromTop - BandTopFromTop;

        // THE SCROLLED CONTENT IS TALLER THAN THE BAND, and it has to be.
        //
        // Everything inside that rect is positioned relative to the RAIL, which
        // sits at its centre -- so the rect's half-height is however far the
        // tallest thing reaches from the rail. Sizing it to the band's own 212
        // instead put the tallest thing six pixels outside its parent at all
        // four aspects, which is what UiAudit refused: the band is 112 above the
        // line and 100 below, and a rect centred on the rail cannot be both.
        //
        // The tallest thing is the caption BOX, whose top edge is at 114 -- two
        // pixels above the band itself. That is the design's own geometry and
        // not a mistake in it: the box is four lines tall for the one reward
        // that needs four lines, the text inside hangs from the bottom, and the
        // two pixels of empty box that poke above the band on the other
        // ninety-eight nodes are clipped by the viewport with nothing in them.
        //
        // The overhang below is clipped the same way. Nothing is lost there
        // either: every node's lowest element is its level number at -55.
        public static float ScrollContentHeight =>
            (CaptionY + CaptionHeight * 0.5f) * 2f;

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
        //
        // SHIFTED UP 60px (balance-bot, 2026-09-02), UNLIKE every other
        // yFromTop constant on this screen. The uniform ContentTop delta
        // (44.22) alone is not enough here: the ribbon's old bottom edge
        // (768) already used nearly the whole 804-tall raw panel (36px of
        // clearance), and the pane's own content box is 88.44px shorter than
        // that (715.56) -- so after the uniform shift the ribbon's bottom
        // overran the content box's own bottom inset by 52.44px, not merely
        // moved with everything else. 60 reclaims that (plus ~7.5px of the
        // same 4px slack margin the rest of this conversion series uses) by
        // pulling the whole ribbon block closer to the band above it; the
        // internal geometry (RibbonHeight=86, the 27px label-to-top gap) is
        // untouched, only its position moved.
        public const float RibbonLabelYFromTop = 595f;    // was 655
        public const float RibbonLabelHeight = 14f;
        public const int RibbonLabelFont = 10;
        public const float RibbonTopFromTop = 622f;       // was 682
        public const float RibbonBottomFromTop = 708f;    // was 768
        public const float RibbonInsetX = 80f;

        public static float RibbonWidth => SystemMenuLayout.PanelWidth - RibbonInsetX * 2f;
        public static float RibbonHeight => RibbonBottomFromTop - RibbonTopFromTop;
        public static float RibbonCentreY => CentreY((RibbonTopFromTop + RibbonBottomFromTop) * 0.5f);
        public static float RibbonLabelCentreY => CentreY(RibbonLabelYFromTop);

        // The label row, as a title at one end and an instruction at the other
        // with a rule running between them. A single centred caption was the
        // first build; it names the thing and tells you what to do with it in
        // one breath, in the middle of 1440px of empty row, and reads as a
        // caption under a picture rather than as the head of a scale.
        public const float RibbonTitleWidth = 240f;
        public const float RibbonHintWidth = 380f;
        public const float RibbonLabelGap = 14f;

        public static float RibbonTitleCentreX =>
            -RibbonWidth * 0.5f + RibbonTitleWidth * 0.5f;

        public static float RibbonHintCentreX =>
            RibbonWidth * 0.5f - RibbonHintWidth * 0.5f;

        public static float RibbonLabelRuleWidth =>
            RibbonWidth - RibbonTitleWidth - RibbonHintWidth - RibbonLabelGap * 2f;

        public static float RibbonLabelRuleCentreX =>
            -RibbonWidth * 0.5f + RibbonTitleWidth + RibbonLabelGap
            + RibbonLabelRuleWidth * 0.5f;

        // Inside the ribbon, measured from its own centre.
        //
        // EVERYTHING SITS ON THE LINE and the numbers stand above it, which is
        // the reverse of the first build -- that one raised the dots off the
        // line and hung the numbers underneath, and the result reads as three
        // separate rows of marks rather than as one ruler. A ruler has its
        // ticks ON the edge it measures and its figures beside them.
        public const float RibbonBaseHeight = 1f;

        // A TICK FOR EVERY LEVEL, in two weights. The first build drew one only
        // where a level was reached and uncollected, so a player who collects
        // as they go saw a bare line with twelve dots on it -- 99 levels
        // rendered as 12 marks, which says nothing about how far apart they
        // are. Every level standing as a hairline is what makes the ribbon a
        // measure of the whole ascent rather than a list of its landmarks.
        public const float RibbonTickWidth = 1f;
        public const float RibbonTickHeight = 7f;
        public const float RibbonWaitingTickWidth = 2f;
        public const float RibbonWaitingTickHeight = 14f;

        public const float RibbonDotSize = 9f;
        public const float RibbonNumberY = 24f;

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
        public const int RibbonNumberFont = 12;
        public const float RibbonPlayheadWidth = 2f;
        public const float RibbonPlayheadHeight = 18f;
        public const float RibbonWindowStroke = 1f;

        // The window box is a BAND across the ruler, not a box around the whole
        // ribbon. At the ribbon's full 86px height it enclosed the numbers as
        // well, which made the twelve landmarks inside the window look like a
        // different kind of landmark from the ones outside it.
        public const float RibbonWindowHeight = 30f;

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
        //
        // FOUR PROCEDURAL BAKES PLUS THE RING DEFAULT, reused rather than
        // grown to twelve (docs/PLAN_REWARD_TRACKS.md §1): a rail mark is
        // read at a glance, not read, so the eight new reward kinds share the
        // four existing bakes by what they resemble (a capacity/gain/absorb
        // kind reads as "a capability", the same as Respec or SecondLife) and
        // only the two numeric ones with an existing bake keep their own.
        public static string IconFor(TrackReward reward)
        {
            switch (reward)
            {
                case TrackReward.None: return null;
                case TrackReward.StatPoint: return "proc:track_stat";
                case TrackReward.MaxHealth: return "proc:track_health";
                // SpellDamagePercent shares the elemental mark, because it
                // IS an elemental percentage -- one applied to every
                // non-Physical type at once (see its own header).
                case TrackReward.ElementalDamagePercent:
                case TrackReward.SpellDamagePercent: return "proc:track_exp";
                case TrackReward.MaxMana:
                case TrackReward.ManaRegen: return "proc:track_favor";
                default: return "proc:ring_outline";
            }
        }

        // WHICH PAINTED MEDALLION THE CARD SHOWS for a reward -- OR NULL,
        // which is now a real answer rather than a gap.
        //
        // NINE OF TWENTY-ONE, AND THE OTHER TWELVE GET WORDS. Phase 4 mapped
        // every kind onto a picture because RewardTrackScreenTests demanded a
        // distinct sprite per kind, and said so plainly in its own note: three
        // of the seven it added were "nearly honest" and the other four (a flat
        // delta on VULNERABLE, a per-point delta on POISON, all-spell damage on
        // FEARED, IDENTITY on PROTECT) were "chosen because they are different
        // from each other and for no other reason". A player could not read the
        // rail by glyph, and those four came from the STATUS set -- flat cel art
        // in cool violets -- which on a card whose other slots are painted gold
        // medallions reads as a bug before it reads as a reward.
        //
        // So the uniqueness rule moves from "a distinct SPRITE per kind" to
        // "a distinct VISUAL per kind", and a kind with no honest medallion
        // draws a short word in the kit's own type instead (CardGlyphFor).
        // Nothing is commissioned, nothing is arbitrary, and the status set
        // goes back to meaning statuses.
        //
        // WHAT EACH OF THE NINE IS, and why it reads right -- the talent
        // tree's painted set, whose file names undersell what is drawn on them:
        //
        //   StatPoint                 ability_score.png, a tree. It is the
        //                             ability-score icon and the reward is
        //                             ability-score points.
        //   MaxHealth                 health.png, a heart.
        //   MaxMana                   mana.png, a potion flask. (Phase 4 had
        //                             this on eye_unused and gave the flask to
        //                             a signature-gain node, which is the two
        //                             the wrong way round.)
        //   ManaRegen                 regen.png, a bolt -- the set's own regen
        //                             mark, and mana coming back is the only
        //                             regeneration this game has.
        //   UnlockSkill               skill_cost.png, an open book. Three of
        //                             the track's four ability nodes hand over
        //                             an actual book spell.
        //   SecondLife                flame_unused.png, a flame relit.
        //   SignatureAbsorbPerPoint   defense.png, a shield. The reward is
        //                             damage soaked.
        //   FuryGainOnAttack          attack.png, a sword. Fury per attack.
        //   FuryStartOfFight          fist_unused.png, a clenched fist. Fury
        //                             already up when the fight opens.
        //
        // The five medallions left over (a cross, an eye, a crosshair, a skull,
        // a winged boot) stay UNUSED on purpose. Every one of them could be
        // assigned to something; none of them would mean it, and assigning
        // them anyway is the exact move this pass exists to undo.
        public static string CardArtKeyFor(TrackReward reward)
        {
            switch (reward)
            {
                case TrackReward.StatPoint: return Screens.RewardTrackScreen.StatArtKey;
                case TrackReward.MaxHealth: return Screens.RewardTrackScreen.HealthArtKey;
                case TrackReward.MaxMana: return Screens.RewardTrackScreen.MaxManaArtKey;
                case TrackReward.ManaRegen: return Screens.RewardTrackScreen.ManaRegenArtKey;
                case TrackReward.UnlockSkill: return Screens.RewardTrackScreen.UnlockSkillArtKey;
                case TrackReward.SecondLife: return Screens.RewardTrackScreen.SecondLifeArtKey;
                case TrackReward.SignatureAbsorbPerPoint: return Screens.RewardTrackScreen.SignatureAbsorbArtKey;
                case TrackReward.FuryGainOnAttack: return Screens.RewardTrackScreen.FuryGainArtKey;
                case TrackReward.FuryStartOfFight: return Screens.RewardTrackScreen.FuryStartArtKey;

                // Everything else draws a word -- see CardGlyphFor.
                default: return null;
            }
        }

        // THE TWELVE KINDS WITH NO HONEST MEDALLION, as a short word set in the
        // kit's own face on the card's 86px plate.
        //
        // WORDS RATHER THAN INVENTED SYMBOLS, and the choice is about who has
        // to be taught. A bespoke glyph for "the flat part of one named skill"
        // is a symbol the player must learn before it says anything; "+HIT" is
        // a symbol they already know. The card's caption underneath still says
        // the whole sentence ("SLAM +5 FLAT"), so the plate's job is only to be
        // scannable and distinct -- which is what a five-letter token at 26pt
        // is, and what a violet ghost from the status set was not.
        //
        // SIX CHARACTERS IS THE CEILING. The slot is 86 wide, the face is
        // Chakra Petch and 26pt measures about 13px a character with this
        // label's tracking, so seven would touch the mat's edges. The fit audit
        // measures none of these (they are runtime text), so the ceiling is
        // kept by RewardTrackScreenTests rather than by eye.
        //
        // WHAT SHOULD BE COMMISSIONED, if a painter is ever pointed at this
        // screen: twelve small medallions in the talent tree's own hand, one
        // per token below. None is urgent -- a word is a worse picture and a
        // perfectly good label -- but the four naming a number a player tunes a
        // build around (ELEM, SPELL, -MANA, -COST) are where a picture helps
        // most.
        public static string CardGlyphFor(TrackReward reward)
        {
            switch (reward)
            {
                case TrackReward.None: return null;

                // Take your points back.
                case TrackReward.Respec: return "RESET";

                // The signature family: four rewards about one pool, told apart
                // by WHICH question they answer -- how much it holds, how fast
                // it fills, what fills it, what it stops.
                case TrackReward.SignatureCapacity: return "CAP";
                case TrackReward.SignatureGainPerTurn: return "/TURN";
                case TrackReward.SignatureGainOnDamageTaken: return "HURT";
                case TrackReward.SignatureAbsorbs: return "SOAK";

                // Damage percentages: one element, or everything she casts.
                case TrackReward.ElementalDamagePercent: return "ELEM";
                case TrackReward.SpellDamagePercent: return "SPELL";

                // The four cost/size levers, each naming the field it moves.
                case TrackReward.SpellCostDelta: return "-MANA";
                case TrackReward.SkillCostDelta: return "-COST";
                case TrackReward.SkillFlatDelta: return "+HIT";
                case TrackReward.SkillPowerDelta: return "+POWER";

                // A crest, not a title: Identity covers the rim, the frame, the
                // emboss and the pose as well as the four titles, and "TITLE"
                // would name three nodes of ten.
                case TrackReward.Identity: return "CREST";

                // Everything with a medallion.
                default: return null;
            }
        }

        // The card's ONE visual per reward kind: the medallion where there is
        // an honest one, else the word. Never both and never neither -- which
        // is what RewardTrackScreenTests asserts, and what makes "a distinct
        // visual per kind" a checkable statement rather than a hope.
        public static string CardVisualKeyFor(TrackReward reward)
        {
            string art = CardArtKeyFor(reward);
            if (!string.IsNullOrEmpty(art)) return art;

            string glyph = CardGlyphFor(reward);
            return string.IsNullOrEmpty(glyph) ? null : "glyph:" + glyph;
        }

        // The hue a reward kind tints its art-slot mat with.
        //
        // THE MAT ONLY, at 2E alpha or below, and only on an unreached node --
        // handoff section 5. A lit disc is gold metal, and a tint laid over
        // that reads as tarnish, which is the same way the flat-fill disc
        // failed before the gradient replaced it.
        public static string MatTintFor(TrackReward reward)
        {
            switch (reward)
            {
                case TrackReward.StatPoint: return "#EDE6FF2E";
                case TrackReward.MaxHealth: return "#D8B4A82E";
                case TrackReward.ElementalDamagePercent:
                case TrackReward.SpellDamagePercent: return "#C8B4DE2E";
                case TrackReward.MaxMana:
                case TrackReward.ManaRegen: return "#F2DB9E2E";
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

        // ---- completion, and the prestige stretch beyond it ---------------------
        //
        // THE TRACK HAS AN END AND THEN TEN MORE LEVELS, which is a shape the
        // rail could not say at all. Progression v2 §0 D3 names level 30 as the
        // completion point and 31-40 as an optional prestige stretch that
        // changes no combat number; drawn as forty identical nodes, the two
        // halves are indistinguishable and a player has no way to know the
        // thing they are working toward arrives ten rungs before the top.
        //
        // THREE MARKS, ALL FROM THE KIT'S EXISTING RECIPES and none of them a
        // new commission: a hairline dropped between the two nodes either side
        // of the boundary, a violet wash laid behind the ten levels past it,
        // and a word under each half saying which is which.
        //
        // IN THE GAP, NOT ON A NODE. A caption box is 170 wide on a 190 pitch,
        // so there is exactly 20px of clear air at every midpoint between two
        // nodes -- the divider is 1px of that, which is why it needs no
        // overlap allowance and why widening it past about 18 would.
        public const float PrestigeDividerWidth = 1f;
        public const float PrestigeDividerHeight = 200f;

        // The strip below the level numbers (-55) and above the band's own
        // floor (-100), which is the only clear band left on a node's column.
        // 14px of label centred at -74 sits inside both.
        public const float StretchLabelY = -74f;
        public const float StretchLabelHeight = 14f;
        public const int StretchLabelFont = 10;

        public const float CompletionLabelWidth = CaptionWidth;
        public const float PrestigeLabelWidth = 420f;

        // The boundary itself, in the content rect's own left-edge
        // coordinates: halfway between the last combat node and the first
        // identity one.
        public static float PrestigeBoundaryX =>
            NodeX(RewardTrack.CompletionLevel) + NodePitch * 0.5f;

        public static float PrestigeBoundaryOffsetX =>
            PrestigeBoundaryX - ContentWidth * 0.5f;

        // Everything right of the boundary, out to the content's own edge --
        // so the wash covers the ten prestige nodes AND the half-pitch of end
        // padding past the last of them, which is what stops it reading as a
        // box drawn around ten things rather than as the end of the rail
        // being different ground.
        public static float PrestigeWashWidth => ContentWidth - PrestigeBoundaryX;

        public static float PrestigeWashOffsetX =>
            PrestigeBoundaryX + PrestigeWashWidth * 0.5f - ContentWidth * 0.5f;

        public static float CompletionLabelOffsetX => NodeOffsetX(RewardTrack.CompletionLevel);

        // Centred on the prestige stretch rather than on any node in it: the
        // word describes the run of ten, and hanging it under one of them would
        // read as that level's own caption.
        public static float PrestigeLabelOffsetX =>
            (PrestigeBoundaryOffsetX + NodeOffsetX(RewardTrack.MaxLevel) + NodePitch * 0.5f) * 0.5f;

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

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

        // How far the painted border reaches in from each side, derived from
        // ContentHalfWidth rather than restating 9.8%. The phase clip and
        // NothingSitsOnThePaintedBorder then cannot drift apart: the box the
        // sweep is clipped to IS the box content is asserted to stay inside.
        public const float BorderInsetX = PanelWidth * 0.5f - ContentHalfWidth;

        // reckoning_frame.png's own bespoke key retired 2026-09-02 when the
        // frame moved to the Gold 3:2 kit container (see the frame's own
        // build-site comment) -- no caller reads it any more.
        public const string TabKey = "UI/Buttons/Processed/tab_plate.png";
        public const string ContinueKey = "UI/Buttons/Processed/continue_arrow.png";
        // BAKED, not painted. The painted attempt came back as a stubby star:
        // short rays, a hard silhouette, and it read as a spiky blob rather
        // than as light. The generated one holds all three things the brief
        // actually needed -- feathered tips, a fade well inside the canvas so
        // rotation cannot clip a ray, and true radial symmetry so a spin does
        // not wobble. Compared side by side before switching.
        public const string BurstKey = "proc:rarity_burst";
        public const string PlaqueKey = "UI/Reckoning/Processed/title_plaque.png";

        // Both uniform along their length, so stretching the fill to any
        // fraction of the track cannot distort it -- all the shaping is
        // vertical.
        public const string BarTrackKey = "proc:bar_track";
        public const string BarFillKey = "proc:bar_fill";

        // The light the earned segment throws onto the panel. Hollow through
        // the middle, so it lights the bar's surroundings rather than painting
        // over the bar.
        public const string BarBloomKey = "proc:bar_bloom";

        // Fire, and it carries its own hues -- see ProceduralSpriteBaker.
        // BakeEmber for why this one is not a greyscale ramp with a tint.
        public const string EmberKey = "proc:arrow_ember";

        // White, so the baked fire colours pass through unchanged, and the
        // alpha is the one dial this holds.
        //
        // 0xB4 is 71%, down from fully opaque. At FF the glow was too hot
        // against the frame's violet — seen in the running game, which is the
        // only place it could be seen, since the Reckoning is a sub-panel and
        // ScreenshotTool only knows top-level ones. The flare's own curve then
        // swings this further down again, so the peak is what is being set
        // here rather than the average.
        public const string EmberTint = "#FFFFFFB4";

        // Fixed at build time, so it must cover the largest party the save can
        // field. Base squad is 1 today and the design's stated target is 3;
        // four leaves headroom without costing anything, since unused rows are
        // hidden. Pinned against EffectiveMaxSquadSize by ReckoningTests.
        public const int RowCount = 3;

        public UiNode Root;

        // The painted frame itself. FIXED, and it stays fixed: it is revealed
        // by the wipe below rather than animated, which is the whole point.
        public NodeRef Frame;

        // The thing that actually moves. A mask whose WIDTH the controller
        // opens from 0, uncovering a frame that never changes size.
        //
        // The dimmer must NOT travel with it -- a dimmer growing from a point
        // would let the fight flash at full brightness for the first frames of
        // the expand.
        public NodeRef FrameWipe;

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
        public NodeRef ContinueGlow;
        public NodeRef LootHeading;

        public List<NodeRef> RowGroups = new List<NodeRef>();
        public List<NodeRef> RowNames = new List<NodeRef>();
        public List<NodeRef> RowLevels = new List<NodeRef>();
        public List<NodeRef> RowBarFills = new List<NodeRef>();
        public List<NodeRef> RowBarBefores = new List<NodeRef>();
        public List<NodeRef> RowGains = new List<NodeRef>();

        public List<NodeRef> OfferButtons = new List<NodeRef>();
        public NodeRef RerollButton;
        public NodeRef OfferTooltip;
        public NodeRef OfferTooltipText;
        public List<NodeRef> OfferHalos = new List<NodeRef>();
        public List<NodeRef> OfferBursts = new List<NodeRef>();

        // ITEM-MODIFIER PLAN PHASE E: the RiftTier ring, distinct from the
        // halo/burst pair above -- those two carry the item's RARITY colour
        // (see BuildOffer's own header on why), and RiftTier is a second,
        // independent axis (see RiftTierColors' own header). Inset within
        // the icon's own bounds rather than bled past it, same reasoning as
        // the dossier's pack/slot glows.
        public List<NodeRef> OfferRiftGlows = new List<NodeRef>();
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
        private const float RowHeight = 64f;
        private const float RowPitch = 72f;
        private const float BarWidth = 900f;

        // 26 read as a flat black gap rather than a channel: on a 1344-wide
        // frame that is a 1:35 slot, and at that ratio the eye takes it for a
        // hole punched in the panel. Six more pixels and a lighter tint below
        // are what turn it back into something cut INTO the violet.
        //
        // 32 is the ceiling, not a preference. The row is 64 tall and carries a
        // name line above the track and the gain line below it; the track is
        // centred at -14, so 32 puts its top at 2 with the name's baseline box
        // starting at 4. One more pixel and they touch.
        private const float BarHeight = 32f;

        // What this fight paid. A saturated amber rather than the old #FFE9A8
        // cream: against a violet panel a desaturated yellow has nothing to
        // push against and reads as beige, and this is the one element on the
        // screen that is meant to look like a reward.
        //
        // Still in the gold family the rest of the screen speaks -- the plaque,
        // the title and the tabs are all #F2DB9E -- rather than a new hue, so
        // it reads as that light turned up rather than as a second accent.
        public const string BarFillTint = "#FFC24A";

        // Where the bar already stood. The resting violet, named here rather
        // than written at the construction site so tools/measure_bar.py can
        // read all three tints out of one place instead of carrying copies.
        public const string BarBeforeTint = "#6B58A8";

        // The bloom runs HOTTER than the fill it comes off, which is what stops
        // it reading as a blurred copy of the bar: light spilling onto a
        // surface keeps the source's colour and loses its detail, so a slightly
        // oranger low-alpha wash is right where a duplicate would look wrong.
        public const string BarBloomTint = "#FFA23C8C";

        // Barely moved from #2A1C46, and that is the answer rather than a
        // hedge. The bar read as a hole because TWO darknesses compounded: a
        // baked shading whose body ran 0.10..0.22 multiplied by a tint at
        // luminance 0.13, landing on screen at #07040B against a panel of
        // #261433 -- a fifth of the surface it was cut into.
        //
        // The baked shading is where that was fixed (ProceduralSpriteBaker,
        // body raised to 0.30..0.46). Lifting the tint as well overshot badly:
        // measured at 0.85x the panel, the channel disappeared INTO the panel,
        // which is a different failure and not obviously a better one.
        //
        // 0.16 luminance puts the body at 0.6x the panel and the lit lip at
        // 1.36x it -- darker than the surface, with a bright edge where it is
        // cut. Measured, not judged: tools/measure_bar.py.
        private const string BarTrackTint = "#33224F";

        // How many relics one page of the RELICS tab shows. The design says
        // "infinite slots per run"; six is what fits without paging, and the
        // controller says so out loud when a run holds more.
        public const int RelicRowCount = 5;

        private const float TabY = 100f;
        private const float TabPitch = 250f;

        public static ReckoningScreen Build()
        {
            var screen = new ReckoningScreen();

            // ---- phase one: the choice ---------------------------------
            // Captured as LootHeading: it is the same node it always was, just
            // moved onto the phase that owns the choice.
            var lootHeading = Ui.Label("ReckoningOfferHeading", UiStrings.ReckoningChooseOne,
                    new UiVec(700f, 44f), 26, "#F2DB9E", Place.At(0f, 248f))
                .AsDecor()
                .Styled(TypographyRole.FunctionalHeading);
            screen.LootHeading = lootHeading;

            // REROLL, beside the heading rather than under the cards.
            //
            // The interior stops at +/-540.3 and the heading is 700 wide, so
            // this is the only band with room: 360..530 clears the heading's
            // right edge by 10px and the border by another 10. Under the cards
            // is where the hover tooltip lives (y -247, and its own comment
            // explains why it cannot move further down).
            //
            // Always present, hidden when the track has not granted one --
            // rather than absent and grown later -- because the tree is emitted
            // once for every save. Same reason the offer row is built four
            // wide.
            var reroll = Ui.Button("ReckoningRerollButton", UiString.Runtime,
                    new UiVec(170f, 40f), 17, Place.At(445f, 248f))
                .Themed(ButtonTheme.Violet)
                .Inactive();
            screen.RerollButton = reroll;

            var offerChildren = new List<UiNode> { lootHeading, reroll };

            // THE WIDEST the reward track can grant, not the base three. The
            // tree is emitted once for every save, so a player who has earned
            // level 50's wider offer needs the fourth card to already exist --
            // the controller hides it for everyone who has not. See
            // OfferRowLayout for why the audit wants the wide case built.
            for (int i = 0; i < OfferRowLayout.MaxCards; i++)
            {
                offerChildren.Add(screen.BuildOffer(i));
            }

            offerChildren.Add(screen.BuildOfferTooltip());

            var offerPhase = Ui.Panel("ReckoningOfferPhase", Place.Stretch(), UiSize.Fill, offerChildren);
            screen.OfferPhase = offerPhase;

            // ---- phase two: what it was worth --------------------------
            var summaryChildren = new List<UiNode>
            {
                // The plaque FIRST, so the title draws over it. Sized to the
                // keyed art's own 3.05 aspect -- the border ornament tapers to
                // points at both ends and stretching it bends them.
                Ui.Sprite("ReckoningTitlePlaque", PlaqueKey, Place.At(0f, 218f),
                    UiSize.Fixed(480f, 157f)).AsDecor(),
                Ui.Label("ReckoningTitle", UiStrings.ReckoningTitle, new UiVec(400f, 46f), 26, "#F2DB9E",
                    Place.At(0f, 218f)).AsDecor().Styled(TypographyRole.CeremonialTitle),
            };

            for (int i = 0; i < TabStrings.Length; i++)
            {
                summaryChildren.Add(screen.BuildTab(i));
            }

            // ONE BOOK, THREE PAGES. Declared as alternatives rather than
            // waived with an AllowOverlap apiece: they are exempt from each
            // other and still checked against the tab bar, the footer and
            // anything added to this panel later.
            summaryChildren.AddRange(Ui.Exclusive(
                screen.BuildSpoilsPage(),
                screen.BuildRelicsPage(),
                screen.BuildTallyPage()));

            // The heat behind the arrow. DECLARED FIRST, so it draws UNDER the
            // button rather than over it -- a child of the button would paint
            // on top of the gold and bury it, because uGUI draws children after
            // their parent's own graphic.
            //
            // Offset RIGHT of the button's centre and wider than it: the arrow
            // tapers to its point across the last fifth of its art, and the
            // glow is light thrown past that point rather than a backing plate
            // the arrow sits on.
            //
            // 88 tall is a ceiling, not a taste. The button's centre is -272 and
            // ContentBottom is -317, so anything taller than 90 puts glow on the
            // frame's painted bottom ornament -- which is exactly what
            // NothingSitsOnThePaintedBorder exists to refuse.
            // 720 wide at x 160, so the box spans -200 to 520: it EMBRACES the
            // button, whose own left edge is at -170, rather than starting
            // where the art stops. The sprite's core is offset back inside its
            // own texture, which lands it under the arrowhead — see
            // ProceduralSpriteBaker.BakeEmber for why that is what makes the
            // glow surround the arrow instead of pointing away from it.
            //
            // 520 is the right-hand limit worth knowing: ContentHalfWidth is
            // 540, so this is 20px off the painted border.
            var continueGlow = Ui.Sprite("ReckoningContinueGlow", EmberKey,
                    Place.At(160f, -272f), UiSize.Fixed(720f, 88f))
                .Coloured(EmberTint)
                .AsDecor();
            screen.ContinueGlow = continueGlow;
            summaryChildren.Add(continueGlow);

            // ONLY on the summary phase. Phase one has no way out but the
            // choice itself -- you always take something.
            // NOT migrated to the semantic kit. The brief's own plan calls
            // this Silver, but the button already wears bespoke arrow art
            // (continue_arrow.png, immediately below) with a matching glow
            // built for that shape (ReckoningContinueGlow, above) -- exactly
            // the case the migration brief itself calls out to leave alone:
            // the art already IS the continue control. A themed plate would
            // replace it outright, not dress it.
            var continueButton = Ui.Button("ReckoningContinueButton", UiStrings.Continue,
                new UiVec(340f, 84f), 22, Place.At(0f, -272f));
            continueButton.SpriteKey = ContinueKey;

            // THE EXEMPTION SITS HERE NOW, on one button, rather than on the
            // three full-size pages it crosses.
            //
            // The pages used to carry it, and carrying it there was a blanket:
            // A1 skips a pair when EITHER side has a reason, so each page
            // stopped being checked against the tab bar, the footer and
            // anything else on the phase as well. Declaring them alternatives
            // (Ui.Exclusive) narrowed that to the pairs it should be, and this
            // is what fell out -- a real overlap the blanket had been hiding:
            // Continue is a drawn button, declared after the pages, so it is on
            // top of whatever they hold at the bottom of the phase.
            //
            // Which is intended. It is the way out of the summary and it has to
            // be reachable from every tab, so it belongs to the phase rather
            // than to any one page, and the pages leave the band it occupies
            // clear. That last clause is the part A1 cannot check for itself --
            // it does not look inside a container - so it is stated here rather
            // than assumed.
            continueButton.AllowOverlap(
                "Continue belongs to the phase, not to a page: it is the only way out of the summary and " +
                "must be reachable from every tab, so it is declared last and draws over the page frames " +
                "beneath it, which leave its band clear");
            screen.ContinueButton = continueButton;
            summaryChildren.Add(continueButton);

            var summaryPhase = Ui.Panel("ReckoningSummaryPhase", Place.Stretch(), UiSize.Fill, summaryChildren)
                .Inactive();

            // The offer and the summary are the same box at two moments.
            Ui.Exclusive(offerPhase, summaryPhase);
            screen.SummaryPhase = summaryPhase;

            // ---- the container -----------------------------------------
            //
            // GOLD, 3:2 KIT CONTAINER -- not the bespoke reckoning_frame.png
            // painting this replaces. The reward container per the plan: this
            // is the screen paying out. 1344x896 (aspect 1.5) is 0.7% off the
            // kit's measured 3:2 aspect (1.49, see ContainerArt.
            // ContainerAspect3x2) -- comfortably inside the 5% band, so the
            // panel's own numbers (already chosen to match the OLD frame's
            // 1536x1024 art) needed no change at all.
            //
            // ContentHalfWidth/ContentTop/ContentBottom below still gate
            // NothingSitsOnThePaintedBorder -- they stay put deliberately,
            // not recomputed from the kit's own (looser) measured inset: they
            // are tighter than the kit's border in every direction, so
            // content already proven to clear them clears the new art's
            // border too, and nothing had to move.
            //
            // Ui.Container returns a wrapper Panel with the art as a Decor
            // CHILD ("ReckoningFrameArt"), not the sprite itself -- unlike
            // the old direct Ui.Sprite, so frame.SpriteKey is now null and
            // the art lives one level down. Un-Decor wrapper, same as every
            // other Container call site (see Ui.BuildFrameHolder).
            var frame = Ui.Container("ReckoningFrame", ButtonTheme.Gold, ContainerRatio.ThreeByTwo,
                Place.At(0f, 0f), new UiVec(PanelWidth, PanelHeight));

            // The phases hang inside a CLIP inset to the painted border rather
            // than off the frame directly.
            //
            // SweepToSummary sends the outgoing cards a full panel width
            // sideways. With nothing bounding them they crossed the gold border
            // and floated over the battlefield before disappearing. Clipped
            // here they slide UNDER the border, which is what a frame around a
            // window is for.
            //
            // Inset horizontally ONLY, and symmetrically, so the clip's centre
            // is still exactly the frame's centre. Every child inside is placed
            // relative to that centre, so an asymmetric inset -- and the
            // vertical border IS asymmetric, the crest being deeper than the
            // bottom ornament -- would silently move all of them. The sweep is
            // horizontal, so the top and bottom edges have nothing to clip
            // anyway.
            var phaseClip = Ui.Panel("ReckoningPhaseClip",
                    Place.Stretch(left: BorderInsetX, right: BorderInsetX), UiSize.Fill,
                    offerPhase, summaryPhase)
                .Clipping();

            frame.Children.Add(phaseClip);
            screen.Frame = frame;

            // The WIPE, and the reason it exists as a node at all.
            //
            // The open used to animate frame.localScale.x from 0. localScale
            // scales CHILDREN, so the border's corner ornaments, the three
            // cards and every label compressed horizontally and sprang out to
            // full width. That rubbery stretch was the single biggest reason
            // the screen read cheap, and no amount of easing fixes it -- a
            // squash is not a wipe.
            //
            // A mask whose WIDTH opens reveals a frame that never deforms.
            // Centred, and grown symmetrically about that centre, so the fixed
            // frame inside it does not drift while it is being uncovered.
            var wipe = Ui.Panel("ReckoningFrameWipe", Place.At(0f, 0f),
                    UiSize.Fixed(PanelWidth, PanelHeight), frame)
                .Clipping();
            screen.FrameWipe = wipe;

            // A soft drop behind the frame. Sits BEHIND it in declaration
            // order and is deliberately larger, so it reads as the panel
            // casting light and shadow onto the fight rather than as a
            // second rectangle.
            // RADIAL, NOT THE BURST. This used the rayed rarity_burst sprite,
            // which has spokes -- a container does not throw spokes, and at
            // #2A1A4A (near-black violet) over an already dark fight it was
            // invisible at any alpha. A soft falloff in a LIGHTER violet reads
            // as the panel lighting the room behind it, which is what the
            // "soft glow around the container" was asking for.
            var glow = Ui.Sprite("ReckoningFrameGlow", "proc:radial_glow", Place.At(0f, 0f),
                    UiSize.Fixed(PanelWidth * 1.5f, PanelHeight * 1.6f))
                .Coloured("#8A63D800")
                .AsDecor()
                .AllowOverflow("the glow is deliberately larger than the frame it sits behind - that bleed IS the drop");
            screen.FrameGlow = glow;

            var content = Ui.Panel("ReckoningContent", Place.At(0f, 0f), UiSize.Fill,
                    glow, wipe)
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
                Ui.Label("ReckoningGoldLabel", UiStrings.ReckoningGold, new UiVec(700f, 54f), 32,
                    "#F2DB9E", Place.At(0f, 30f)).AsDecor().Styled(TypographyRole.TacticalData),
                Ui.Label("ReckoningExpHeading", UiStrings.ReckoningExperience, new UiVec(700f, 32f), 20,
                    "#B8A8D9", Place.At(0f, -20f)).AsDecor().Styled(TypographyRole.FunctionalHeading),
            };

            GoldLabel = children[0];

            for (int i = 0; i < RowCount; i++)
            {
                children.Add(BuildRow(i));
            }

            var page = Ui.Panel("ReckoningSpoilsPage", Place.Stretch(), UiSize.Fill, children);
            Pages.Add(page);
            return page;
        }

        // ---- page 2: what the descent carries ---------------------------------

        private UiNode BuildRelicsPage()
        {
            var children = new List<UiNode>
            {
                Ui.Label("ReckoningRelicHeading", UiStrings.ReckoningRelicHeld, new UiVec(700f, 34f), 20,
                    "#B8A8D9", Place.At(0f, 40f)).AsDecor().Styled(TypographyRole.FunctionalHeading),
            };

            for (int i = 0; i < RelicRowCount; i++)
            {
                float y = -20f - i * 60f;

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
                    "#7E6E9E", Place.At(0f, -100f))
                .AsDecor()
                .Inactive();
            RelicEmptyHint = empty;
            children.Add(empty);

            var page = Ui.Panel("ReckoningRelicsPage", Place.Stretch(), UiSize.Fill, children)
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
                    "#B8A8D9", Place.At(0f, 40f)).AsDecor().Styled(TypographyRole.FunctionalHeading),
            };

            for (int i = 0; i < RowCount; i++)
            {
                float y = -30f - i * 88f;

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
                .Inactive();
            Pages.Add(page);
            return page;
        }

        // One party member's line: who, what level, and a bar showing what this
        // fight was worth against what they had already.
        private UiNode BuildRow(int index)
        {
            float y = -70f - index * RowPitch;

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
            // Both wear the SAME baked fill, tinted differently: the resting
            // violet for what was already earned, and the bright gold for what
            // this fight paid. One asset, two colours -- a second sprite would
            // be a second thing to keep in step with the first.
            var before = Ui.Sprite($"ReckoningRow{index}BarBefore", BarFillKey,
                    Place.Frac(new UiVec(0f, 0f), new UiVec(1f, 1f)), UiSize.Fill)
                .Coloured(BarBeforeTint)
                .AsDecor();
            // WAS #FFE9A8, a pale cream that read as washed-out rather than as
            // earned. On a violet panel a desaturated yellow has nothing to
            // push against; this is the same hue family with the saturation put
            // back, so it reads as light rather than as beige.
            var fill = Ui.Sprite($"ReckoningRow{index}BarFill", BarFillKey,
                    Place.Frac(new UiVec(0f, 0f), new UiVec(1f, 1f)), UiSize.Fill)
                .Coloured(BarFillTint)
                .AsDecor();

            // The bloom rides the fill as a CHILD, which is what makes it
            // follow the animation for free: the controller's SetSpan moves the
            // fill's anchors, and a stretched child goes with it. Spanning it
            // from the controller as well would be a second copy of the same
            // number, updated in a second place.
            //
            // Inset NEGATIVELY, so it reaches 12 past the track on both sides
            // and lights the panel; the sprite is hollow across the fill's own
            // height so the bar underneath is not painted over.
            var bloom = Ui.Sprite($"ReckoningRow{index}BarBloom", BarBloomKey,
                    Place.Frac(new UiVec(0f, 0f), new UiVec(1f, 1f), top: -12f, bottom: -12f),
                    UiSize.Fill)
                .Coloured(BarBloomTint)
                .AsDecor()
                .AllowOverflow("a bloom that stopped at the bar's edge would not be one - reaching past the track IS the effect");
            fill.Children.Add(bloom);

            // the earned segment is drawn ON TOP of the before segment - one
            // track, two fills, by design
            var track = Ui.Sprite($"ReckoningRow{index}BarTrack", BarTrackKey, Place.At(0f, -14f),
                    UiSize.Fixed(BarWidth, BarHeight))
                .Coloured(BarTrackTint)
                .AsDecor();

            track.Children.Add(before);
            track.Children.Add(fill);

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

        // How big the hover comparison is. Named because the placement
        // arithmetic needs them and they must be the same numbers the box is
        // actually built at.
        public const float TooltipWidth = 300f;

        // The TALLEST it gets, which is what the tree is emitted at and what
        // the containment audit therefore solves. The controller shrinks it to
        // whatever the text actually needs -- a squad of one produces three
        // short lines, and a 300px box holding three lines reads as a slab
        // dropped on the card next door rather than as a label belonging to
        // the one being hovered.
        //
        // ITEM-MODIFIER PLAN PHASE E: 300 -> 400. SquadComparisonBody now
        // prepends an AFFIXES heading and up to three modifier lines ahead of
        // the per-member deltas (ItemStatLines.SquadBody) -- a full squad
        // with a fully-rolled offer could already brush the old ceiling on
        // deltas alone, and the modifier section adds height no earlier
        // build of this screen had to plan for. ContentTop/ContentBottom
        // leave about 615px of vertical interior (see their own consts), so
        // 400 has plenty of room to still fit beside a card.
        //
        // ITEM-MODIFIER PLAN PHASE F: 400 -> 590. The AFFIXES section moved
        // from ONE shared block above every member to its OWN block inside
        // EACH member's (ItemStatLines.SquadBody, ModifierComparisonLines) --
        // the max squad is two (SaveData.BaseMaxSquadSize + the one
        // purchasable extra slot), and a fully different three-slot
        // Convergent item on both sides can now print a heading, up to three
        // gain lines, AND up to three "Losing: <name>" lines PER MEMBER,
        // where before the whole squad shared one heading and three lines
        // total.
        //
        // 590, not the ~598 the ~615px interior would allow: the panel is
        // AUTHORED at Place.At(0,0) -- the panel's centre -- specifically so
        // the containment audit can solve it (see BuildOfferTooltip's own
        // header), which means its authored half-height cannot exceed
        // ContentTop (298.368, the tighter of the two bounds; see
        // NothingSitsOnThePaintedBorder). 600 tried that and lost by 1.6px.
        // 590 (half 295) leaves a real margin rather than shaving it to the
        // audit's own +1f tolerance. This does not cover the absolute
        // theoretical maximum (every stat/score field moving AND every affix
        // slot rolled AND fully disjoint, on both members at once) -- that
        // combination is not a real drop a player will see, and
        // FitTooltipToBody already clamps and shrinks to whatever the actual
        // body needs, same as before.
        public const float TooltipHeight = 590f;

        // And the shortest, so a one-line body still looks like a considered
        // box rather than a strip.
        public const float TooltipMinHeight = 96f;

        // Air between the text and the box's edge, on every side.
        public const float TooltipPad = 16f;

        // What a hovered offer would actually DO, per squad member.
        //
        // BESIDE THE CARD, not under the row. This was a 920x136 strip pinned
        // across the bottom of the panel, and the comment defending that spot
        // argued the comparison is about the whole squad and so belongs where
        // the eye can hold every card and the numbers at once. In the running
        // game it did not read that way: a wide bar detached from any of the
        // three cards looks like a status line the screen always has, so
        // nothing connects the numbers to the thing under the cursor. The
        // second argument -- that a per-card popup has to dodge the panel edge
        // on the outer two, "a lot of arithmetic for no gain" -- was true and
        // is no longer, because the dossier's pack has since written that
        // arithmetic and it is now TooltipPlacement, shared by both.
        //
        // The strip also cost the cards 136px of the panel's height for a box
        // that is empty until something is hovered. That space is the icons'
        // now; see BuildOffer.
        //
        // SQUARE, like the pack's, because the content is the same shape: a
        // per-character stack of short delta lines, which runs deep rather than
        // wide and wants height far more than the 116px the strip gave it.
        //
        // Decor, so the emitter clears raycastTarget across the subtree -- a
        // tooltip that ate the click meant for the card underneath it would be
        // a cruel bug on a screen whose only job is taking one click. That
        // matters more now that it sits ON a card rather than below them all.
        private UiNode BuildOfferTooltip()
        {
            var label = Ui.Label("ReckoningOfferTooltipText", UiString.Runtime,
                    new UiVec(TooltipWidth - TooltipPad * 2f, TooltipHeight - TooltipPad * 2f), 15,
                    "#D9CCF2", Place.At(0f, 0f))
                .TextAligned(UiTextAlign.TopLeft);
            OfferTooltipText = label;

            // A FLAT GROUND rather than the tab plate this used to wear. That
            // sprite is a wide 9-slice and a 300x300 crop of it reads as a
            // button someone forgot to label; the pack's tooltip already
            // established a plain dark sheet as the house tooltip surface.
            //
            // Declared at the panel's centre and moved on every hover, exactly
            // as the dossier's is -- an authored position an inactive node
            // never draws at, chosen because it is somewhere the containment
            // audit can solve.
            var panel = Ui.Sprite("ReckoningOfferTooltip", null, Place.At(0f, 0f),
                    UiSize.Fixed(TooltipWidth, TooltipHeight))
                // FULLY OPAQUE. The pack's tooltip gets away with 95% because
                // it opens over the dossier's flat ground; this one opens over
                // a neighbouring card's item art, and at F2 the coif behind it
                // read straight through the comparison numbers -- confirmed in
                // a capture, which is the only place it could be seen.
                .Coloured("#1D1226")
                .Inactive()
                .AsDecor();
            panel.Children.Add(label);
            OfferTooltip = panel;
            return panel;
        }

        // ---- one offer card's vertical band -----------------------------------
        //
        // ALL OF IT DERIVED FROM THREE NUMBERS: where the art starts, where it
        // stops, and how tall the two lines under it are. Everything else --
        // where the name sits, where the meta sits, how tall the card is and
        // where its centre lands -- falls out.
        //
        // That is not tidiness. The card used to be a fixed 400 tall centred on
        // the phase axis with its children placed by eye inside it, and the
        // first attempt at growing the art moved the labels without moving the
        // frame: the containment audit refused the build with name and meta
        // hanging 67 and 97px below their own card. Deriving the frame from its
        // contents means the next retune of the band cannot make that mistake.
        //
        // IconBottom is -195 and not lower because the painted interior stops
        // at ContentBottom (-317.2) and these two lines plus their gaps come to
        // 108px; the 20px left over is the meta line's clearance from the
        // frame's bottom ornament.
        private const float IconTop = 205f;
        private const float IconBottom = -195f;

        // 6px binds the name to the art (see BuildOffer); 4px separates the two
        // lines from each other.
        private const float IconToNameGap = 6f;
        private const float NameToMetaGap = 4f;
        private const float NameHeight = 66f;
        private const float MetaHeight = 26f;

        private const float IconCentreY = (IconTop + IconBottom) * 0.5f;
        private const float NameCentreY = IconBottom - IconToNameGap - NameHeight * 0.5f;
        private const float MetaCentreY =
            NameCentreY - NameHeight * 0.5f - NameToMetaGap - MetaHeight * 0.5f;

        // The card's top IS the art's top -- there is no chrome above it (see
        // BuildOffer's "NO PLATE" note), so IconTop is used directly rather
        // than aliased.
        private const float CardBottom = MetaCentreY - MetaHeight * 0.5f;
        private const float CardHeight = IconTop - CardBottom;
        private const float CardCentreY = (IconTop + CardBottom) * 0.5f;


        // One of the items on offer. A button, because picking one is the only
        // decision this screen asks the player to make, and a CARD across the
        // width rather than a row in a list -- the offers used to share the
        // panel with the experience bar and had to be small; alone on their own
        // phase they can be the thing the screen is about.
        //
        // Positioned for a FULL-WIDTH row. A narrower one is re-centred at
        // runtime by ReckoningController, from the same OfferRowLayout.CardX
        // this calls, so the two widths cannot drift apart.
        private UiNode BuildOffer(int index)
        {
            // Built at the WIDEST row the track can grant, which is also the
            // narrowest each card can be. ReckoningController widens them again
            // for the three-card row every player below level 50 sees, from
            // this same OfferRowLayout, so the two cannot drift.
            float cardWidth = OfferRowLayout.CardWidth(OfferRowLayout.MaxCards);
            float labelWidth = OfferRowLayout.LabelWidth(OfferRowLayout.MaxCards);
            float x = OfferRowLayout.CardX(index, OfferRowLayout.MaxCards);

            // THE ITEM IS THE CARD, so it gets the room -- and it gets the
            // room the hover strip used to hold.
            //
            // 200x250 was the shape the art is, which was right as far as it
            // went, but the box was WIDTH-limited: a 260x384 sheet in a 200-wide
            // box draws 169x250 and the last 80px of height went to letterbox.
            // So the icon grew in the only direction that pays -- the box is
            // now as wide as its card allows (OfferRowLayout.IconWidth) and
            // deep enough that width stays the binding constraint at three
            // cards. A three-card row draws its art about 60% larger.
            //
            // -195, not lower: name and meta still hang below it, and the
            // painted interior stops at -317 (ContentBottom, which the
            // border check A5 measures). Those two lines plus their gaps come
            // to 108px, and 20px of clearance keeps the meta line off the
            // bottom ornament.
            // The band is declared once at class scope -- see the block above
            // BuildOffer. What is left here is the art's WIDTH, which depends
            // on how many cards share the row and so cannot be a constant.
            // Every y above is measured in the OFFER PHASE and every child is
            // placed relative to its CARD, so each one is rebased by exactly
            // the card's own offset from the phase axis.
            const float IconCentre = IconCentreY - CardCentreY;

            // Built at the four-card row for the same reason the card is: the
            // audit solves the emitted tree and cannot see the controller widen
            // it, so what it solves should be the tightest case. See
            // ReckoningController.LayOutOfferRow for the other half.
            float iconWidth = OfferRowLayout.IconWidth(OfferRowLayout.MaxCards);
            float haloSize = OfferRowLayout.HaloDiameter(OfferRowLayout.MaxCards);
            float burstSize = OfferRowLayout.BurstDiameter(OfferRowLayout.MaxCards);

            // TWO layers behind the icon, because one was not readable.
            //
            // The halo is the soft mass: a plain radial falloff, wider than the
            // icon, carrying the rarity colour where the eye actually looks.
            // The burst is the rayed sprite, and its rays are the only part of
            // it that ever cleared the icon -- behind a solid object a starburst
            // shows as a few spikes and nothing else, which is why the rarity
            // read as "small and hidden behind the sprite".
            var halo = Ui.Sprite($"ReckoningOffer{index}Halo", "proc:radial_glow",
                    Place.At(0f, IconCentre), UiSize.Fixed(haloSize, haloSize))
                .Coloured("#FFFFFF00")
                .AsDecor()
                .AllowOverflow("the halo is deliberately larger than the icon it sits behind - that bleed IS the rarity signal");

            // the rays are meant to read THROUGH and around the icon
            var burst = Ui.Sprite($"ReckoningOffer{index}Burst", BurstKey,
                    Place.At(0f, IconCentre), UiSize.Fixed(burstSize, burstSize))
                .Coloured("#FFFFFF00")
                .AsDecor()
                .AllowOverflow("the burst is deliberately larger than the icon it sits behind - that bleed IS the rarity signal");

            // the icon stands on its own glow - covering the middle of it is
            // the point
            var icon = Ui.Sprite($"ReckoningOffer{index}Icon", null,
                    Place.At(0f, IconCentre), UiSize.Fixed(iconWidth, IconTop - IconBottom))
                .AsDecor();

            // ITEM-MODIFIER PLAN PHASE E: the RiftTier ring. Sized to the ART
            // BOX itself (iconWidth x its own height), not larger -- unlike
            // the halo/burst pair, which are deliberately oversized and
            // bleed past the icon on purpose (see their own AllowOverflow
            // reasons), this one stays inside the icon's own bounds so it
            // needs no exemption of its own. Coloured and shown at runtime
            // by ReckoningController.PaintOffers; hidden whenever the offer
            // rolled Ordinary, the regression guard every other glow node
            // this phase adds shares.
            var riftGlow = Ui.Sprite($"ReckoningOffer{index}RiftGlow", "proc:ring_hairline",
                    Place.At(0f, IconCentre), UiSize.Fixed(iconWidth, IconTop - IconBottom))
                .Coloured("#FFFFFF00")
                .AsDecor();

            // THE NAME BELONGS TO THE ITEM, so it sits directly under it. It
            // used to float 57px below the icon with nothing in the gap, which
            // reads as two unrelated things rather than as a labelled object.
            // 6px is enough to separate them and little enough to bind them.
            var name = Ui.Label($"ReckoningOffer{index}Name", UiString.Runtime, new UiVec(labelWidth, NameHeight), 21,
                    "#EDE6FF", Place.At(0f, NameCentreY - CardCentreY))
                .AsDecor();
            var meta = Ui.Label($"ReckoningOffer{index}Meta", UiString.Runtime, new UiVec(labelWidth, MetaHeight), 15,
                    "#B8A8D9", Place.At(0f, MetaCentreY - CardCentreY))
                .AsDecor();

            // NO PLATE. The card is its burst, its icon and its two lines --
            // the item is the object, and a gold button frame behind it turned
            // three treasures into three menu entries.
            //
            // Chromeless rather than an empty SpriteKey, which the emitter
            // reads as "use the shared button frame" and is exactly how this
            // survived being called fixed twice: BuildOffer removed the
            // declared children and the plate came straight back from the
            // fallback.
            //
            // The row runs to x 540 against a clip half-width of 540.3. That is
            // deliberate -- the offers are sized to use the full interior --
            // but it means widening a card by even a pixel fails A2 rather than
            // quietly touching the paint. That constraint is why level 50's
            // wider offer divides the same budget into four narrower cards
            // instead of adding a fourth at the old width, which would have
            // needed 1450px of a 1080px interior.
            // NOT migrated to the semantic kit, even though picking one of
            // these is this screen's "Accept" (Green, per the migration
            // brief). NoChrome() a few lines below is deliberate design, not
            // an oversight -- see BuildOffer's own "NO PLATE" note: a themed
            // plate behind each card is precisely the mistake that note
            // records fixing ("a gold button frame behind it turned three
            // treasures into three menu entries"). Theming here would
            // reintroduce it in a different colour.
            var button = Ui.Button($"ReckoningOffer{index}", UiString.Runtime,
                    new UiVec(cardWidth, CardHeight), 14, Place.At(x, CardCentreY))
                .NoChrome();

            button.Children.Add(halo);
            button.Children.Add(burst);
            button.Children.Add(icon);
            button.Children.Add(riftGlow);
            button.Children.Add(name);
            button.Children.Add(meta);

            OfferButtons.Add(button);
            OfferHalos.Add(halo);
            OfferBursts.Add(burst);
            OfferIcons.Add(icon);
            OfferRiftGlows.Add(riftGlow);
            OfferNames.Add(name);
            OfferMetas.Add(meta);
            return button;
        }
    }
}

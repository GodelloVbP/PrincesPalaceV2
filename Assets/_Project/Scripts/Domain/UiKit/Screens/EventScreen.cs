using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // An event room: the art on the left, the words and the choices on the
    // right. Nested in MapScreen the way the shop is -- entered from the map,
    // no scene of its own -- and painted by EventController from
    // RunOrchestrator.CurrentEvent().
    //
    // FOUR CHOICE ROWS, pre-emitted, because a page may hold at most
    // EventEntryResolver's four and the content build refuses more. The
    // controller shows as many as the page has VISIBLE choices, top down, and
    // switches the rest off: a hiddenUntilMet choice takes no row, so the
    // stack never shows a gap.
    //
    // THE ART FRAME IS 4:3 at ArtWidth x ArtHeight, which is the size art is
    // commissioned at (docs/EVENTS.md, docs/ART_PIPELINE.md). The image keeps
    // its aspect inside the frame, so a slightly-off delivery letterboxes
    // rather than stretches; a missing one leaves the frame empty, never a
    // white quad (ItemIcons.Apply disables the Image).
    //
    // Coordinates follow ShopScreen's rule: every Place.At is relative to its
    // immediate parent -- the two columns are screen-absolute, a row's
    // children are row-local.
    public sealed class EventScreen
    {
        // ---- palette ----------------------------------------------------------
        private const string Ground = FightHudPalette.ShopGround;
        private const string FrameFill = FightHudPalette.CardFill;
        private const string FrameRim = FightHudPalette.BorderQuiet;
        private const string TitleText = FightHudPalette.HeadingGold;
        private const string BodyText = FightHudPalette.TextPrimary;
        private const string EffectsText = FightHudPalette.GoldText;
        private const string RowFill = FightHudPalette.SubmenuRowFill;
        private const string ChoiceText = FightHudPalette.RowNameText;

        // Read by EventController to grey a locked row, so the tree and the
        // runtime share one home for each colour.
        public const string ChoiceTextHex = ChoiceText;
        public const string LockedChoiceTextHex = FightHudPalette.TextMuted;
        public const string LockReasonHex = "#D9A87EFF";

        // ---- geometry (1920x1080 reference) -------------------------------------
        private const float ScreenHalfWidth = 960f;
        private const float ScreenHalfHeight = 540f;
        private const float MarginX = 80f;
        private const float MarginY = 60f;
        private const float ColumnGap = 64f;

        // The commissioning size. 4:3 is the plan's call (assumption 6); one
        // pair of constants if the owner wants portrait instead.
        public const float ArtWidth = 960f;
        public const float ArtHeight = 720f;

        private const float ArtLeft = -ScreenHalfWidth + MarginX;
        private const float ArtCentreX = ArtLeft + ArtWidth * 0.5f;

        private const float TextLeft = ArtLeft + ArtWidth + ColumnGap;
        private const float TextRight = ScreenHalfWidth - MarginX;
        private const float TextWidth = TextRight - TextLeft;
        private const float TextCentreX = (TextLeft + TextRight) * 0.5f;

        private const float ColumnTop = ScreenHalfHeight - MarginY;
        private const float ColumnBottom = -ScreenHalfHeight + MarginY;

        private const float TitleHeight = 60f;
        private const float TitleToBodyGap = 16f;
        private const float BodyToEffectsGap = 12f;
        private const float EffectsHeight = 36f;
        private const float EffectsToChoicesGap = 24f;

        public const int ChoiceRowCount = 4;
        private const float RowHeight = 80f;
        private const float RowGap = 12f;
        private const float RowPad = 20f;
        private const float RowTextHeight = 34f;
        private const float RowLockHeight = 26f;

        private const float ChoicesHeight = ChoiceRowCount * RowHeight + (ChoiceRowCount - 1) * RowGap;
        private const float ChoicesTop = ColumnBottom + ChoicesHeight;

        private const float TitleCentreY = ColumnTop - TitleHeight * 0.5f;
        private const float BodyTop = ColumnTop - TitleHeight - TitleToBodyGap;
        private const float EffectsCentreY = ChoicesTop + EffectsToChoicesGap + EffectsHeight * 0.5f;
        private const float BodyBottom = EffectsCentreY + EffectsHeight * 0.5f + BodyToEffectsGap;

        // Public so the text-fit test measures the box the tree declares
        // rather than a copy of these numbers.
        public const float BodyHeight = BodyTop - BodyBottom;
        public const float BodyWidth = TextWidth;
        public const int BodyFontSize = 24;

        private const float BodyCentreY = (BodyTop + BodyBottom) * 0.5f;

        public sealed class ChoiceRow
        {
            public NodeRef Button;
            public NodeRef Text;
            public NodeRef Lock;
        }

        public UiNode Root;

        public NodeRef ArtImage;
        public NodeRef TitleLabel;
        public NodeRef BodyLabel;
        public NodeRef EffectsLabel;
        public List<ChoiceRow> ChoiceRows = new List<ChoiceRow>();

        public static EventScreen Build()
        {
            var screen = new EventScreen();
            var children = new List<UiNode>
            {
                // Opaque, as the shop's is: the map underneath is not part of
                // this room.
                Ui.Solid("EventBackground", Ground, Place.Stretch(), UiSize.Fill).AsDecor(),
                screen.BuildArtFrame(),
                screen.BuildTextColumn(),
            };

            screen.Root = Ui.Panel("EventPanel", UiSize.Fill, children).Inactive();
            return screen;
        }

        private UiNode BuildArtFrame()
        {
            var size = new UiVec(ArtWidth, ArtHeight);

            // Decor: the rim overlaps the picture's edge by design, and
            // neither takes a click.
            var art = Ui.Sprite("EventArt", null, Place.At(0f, 0f), UiSize.Fixed(size)).AsDecor();
            art.PreserveAspect = true;
            ArtImage = art;

            return Ui.OutlineBox("EventArtFrame", Place.At(ArtCentreX, 0f), size, FrameFill, FrameRim,
                new[] { art });
        }

        private UiNode BuildTextColumn()
        {
            var parts = new List<UiNode>();
            float columnHeight = ColumnTop - ColumnBottom;
            float columnCentreY = (ColumnTop + ColumnBottom) * 0.5f;

            // Column-local y: everything below is measured from the column's
            // own centre.
            float Local(float y) => y - columnCentreY;

            var title = Ui.Label("EventTitle", UiStrings.EventTitle, new UiVec(TextWidth, TitleHeight), 36,
                    TitleText, Place.At(0f, Local(TitleCentreY)))
                .Styled(TypographyRole.FunctionalHeading)
                .TextAligned(UiTextAlign.Left);
            TitleLabel = title;
            parts.Add(title);

            // Top-left, so a short body reads from where a long one starts
            // rather than floating in the middle of the box.
            var body = Ui.Label("EventBody", UiStrings.EventBody, new UiVec(BodyWidth, BodyHeight), BodyFontSize,
                    BodyText, Place.At(0f, Local(BodyCentreY)))
                .Styled(TypographyRole.Body)
                .TextAligned(UiTextAlign.TopLeft);
            BodyLabel = body;
            parts.Add(body);

            var effects = Ui.Label("EventEffects", UiStrings.EventEffects, new UiVec(TextWidth, EffectsHeight), 19,
                    EffectsText, Place.At(0f, Local(EffectsCentreY)))
                .Styled(TypographyRole.TacticalData)
                .TextAligned(UiTextAlign.Left);
            EffectsLabel = effects;
            parts.Add(effects);

            for (int i = 0; i < ChoiceRowCount; i++)
            {
                float y = ChoicesTop - RowHeight * 0.5f - i * (RowHeight + RowGap);
                var row = BuildChoiceRow(i, Local(y));
                ChoiceRows.Add(row);
                parts.Add(row.Button.Node);
            }

            return Ui.Panel("EventTextColumn", Place.At(TextCentreX, columnCentreY),
                UiSize.Fixed(TextWidth, columnHeight), parts);
        }

        // A button with its own fill and rim rather than a themed plate: a
        // 736x80 row is nowhere near any plate's aspect, and the pack rows
        // (ShopScreen.BuildPackRow) are the same answer to the same shape.
        // Starts inactive; the controller shows one row per visible choice.
        private ChoiceRow BuildChoiceRow(int index, float y)
        {
            string name = $"EventChoice{index}";
            var size = new UiVec(TextWidth, RowHeight);
            float inner = TextWidth - RowPad * 2f;

            var children = new List<UiNode>
            {
                Ui.Solid($"{name}Fill", RowFill, size, Place.At(0f, 0f)).AsDecor(),
            };
            children.AddRange(Ui.Rim(name, size, FrameRim));

            // Text on the upper line, the lock reason under it. Fixed lines
            // rather than recentring the text when the caption is off: a row
            // that shifts its words when it locks reads as a different row.
            var text = Ui.Label($"{name}Text", UiStrings.EventChoice, new UiVec(inner, RowTextHeight), 22,
                    ChoiceText, Place.At(0f, RowHeight * 0.5f - 10f - RowTextHeight * 0.5f))
                .Styled(TypographyRole.Body)
                .TextAligned(UiTextAlign.Left)
                .AsDecor();
            children.Add(text);

            var lockReason = Ui.Label($"{name}Lock", UiStrings.EventChoiceLocked, new UiVec(inner, RowLockHeight), 17,
                    LockReasonHex, Place.At(0f, -RowHeight * 0.5f + 8f + RowLockHeight * 0.5f))
                .Styled(TypographyRole.TacticalData)
                .TextAligned(UiTextAlign.Left)
                .AsDecor()
                .Inactive();
            children.Add(lockReason);

            var button = Ui.Button(name, UiString.Runtime, size, 12, Place.At(0f, y)).NoChrome().Inactive();
            foreach (var child in children) button.Children.Add(child);

            return new ChoiceRow { Button = button, Text = text, Lock = lockReason };
        }
    }
}

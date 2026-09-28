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
    // THE ART IS 16:9 EVERYWHERE (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 1.6):
    // ArtWidth x ArtHeight in the legacy frame here, StageArtWidth x
    // StageArtHeight as the dialogue stage's set piece, both cut from the one
    // 1920x1080 commission (docs/EVENTS.md). The image keeps its aspect
    // inside either, so a slightly-off delivery letterboxes rather than
    // stretches; a missing one leaves the frame empty, never a white quad
    // (ItemIcons.Apply disables the Image).
    //
    // Coordinates follow ShopScreen's rule: every Place.At is relative to its
    // immediate parent -- the two columns are screen-absolute, a row's
    // children are row-local.
    //
    // TWO PRESENTATIONS, ONE SET OF ROWS (docs/PLAN_DIALOGUE_STAGE.md, D2).
    // A page with no dialogue lines shows the frame and the text column
    // above, exactly as before (contract 14). A page with lines shows the
    // DialogueStage instead: backdrop, shade, set piece, bust, box and name
    // plate, back to front (the plan's layer table). The four choice rows
    // live in their own EventChoices panel, drawn after the stage, and the
    // controller moves that one panel between its legacy spot here and its
    // place above the box -- the rows are never duplicated, so the nav rail,
    // the count bindings and every by-name lookup see one set.
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

        // A muted lilac that still reads on the row.
        // TextMuted (#8A7AA0) measured 3.3:1 against the row fill in the
        // captures -- the camera's grade darkens it below its authored
        // 5.0:1 -- and read as a smudge. This is 8.1:1 authored and about
        // 5.1:1 through the same grade, and still well under the open rows'
        // 15:1, so a locked row reads as locked.
        public const string LockedChoiceTextHex = "#B0A0C6FF";
        public const string LockReasonHex = "#EDC9A2FF";

        // ---- geometry (1920x1080 reference) -------------------------------------
        private const float ScreenHalfWidth = 960f;
        private const float ScreenHalfHeight = 540f;
        private const float MarginX = 80f;
        private const float MarginY = 60f;
        private const float ColumnGap = 64f;

        // The legacy frame: 16:9 at the width a 4:3 frame would have, so
        // the text column beside it keeps its left edge and only the frame
        // is shorter. Half the 1920x1080 commission.
        public const float ArtWidth = 960f;
        public const float ArtHeight = 540f;

        // The stage's set piece: 16:9, two thirds of the commission. Hung
        // SetPieceTop under the stage's top edge, its bottom lands at
        // 1080 - 64 - 720 = 296 above the stage's bottom, clear of the
        // dialogue box's top at DialogueStageLayout.BoxTop (288). The name
        // plate straddling the box's top edge does reach into it; that is the
        // layer order (plate in front), not a collision.
        public const float StageArtWidth = 1280f;
        public const float StageArtHeight = 720f;

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
        private const float RowPairGap = 2f;
        public const int ChoiceFontSize = 22;
        public const int LockFontSize = 18;

        // Narrower than the text column (736) because the stage has to fit a
        // row between the focus marker's margin and a right-hand speaker's
        // name plate on a 1920-wide screen: 63 (DialogueStageLayout
        // .ChoicesMinLeft) + 704 leaves 25px before the plate at 792. The
        // legacy rows keep their left edge, so their text starts where it did.
        public const float ChoiceRowWidth = 704f;

        public const float ChoicesHeight = ChoiceRowCount * RowHeight + (ChoiceRowCount - 1) * RowGap;

        // How tall `rows` shown rows stand, top of the first to bottom of the
        // last. The stage pins its rows by this (DialogueStageLayout
        // .ChoicesPanelBottom) so they sit on the box however many show.
        public static float ChoicesHeightFor(int rows) =>
            rows <= 0 ? 0f : rows * RowHeight + (rows - 1) * RowGap;

        // A row's label alone is centred in the row; with a lock reason the
        // two are centred as a pair, label above -- a single line at the
        // top of an 80px row leaves a hole under it. Row-local y, read by
        // EventController when it paints a row.
        private const float RowPairHeight = RowTextHeight + RowPairGap + RowLockHeight;

        public static float ChoiceTextY(bool withLock) =>
            withLock ? RowPairHeight * 0.5f - RowTextHeight * 0.5f : 0f;

        public const float ChoiceLockY = -(RowPairHeight * 0.5f - RowLockHeight * 0.5f);
        private const float ChoicesTop = ColumnBottom + ChoicesHeight;

        // The choice rows sit in their own panel (EventChoices) under the
        // text column, so the column now ends where the rows begin.
        private const float TextColumnBottom = ChoicesTop + EffectsToChoicesGap;
        private const float ChoicesCentreY = ColumnBottom + ChoicesHeight * 0.5f;

        // Read by EventController, which gives the rows' panel back its
        // legacy spot when a page without lines follows one with them.
        public const float ChoicesLegacyX = TextLeft + ChoiceRowWidth * 0.5f;
        public const float ChoicesLegacyY = ChoicesCentreY;

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

        // The legacy layout's two halves, switched off while the stage shows.
        public NodeRef ArtFrame;
        public NodeRef TextColumn;

        // The rows' shared panel, moved by the controller in stage mode.
        public NodeRef ChoicesPanel;

        // ---- the dialogue stage (D2) ---------------------------------------------
        public NodeRef DialogueStage;
        public NodeRef StageBackdrop;
        public NodeRef StageSetPiece;

        // TWO bust images at the same spot, B inactive. D2 paints A only; D3's
        // speaker change slides the old one out while the new one slides in,
        // which needs both on screen at once.
        public List<NodeRef> StageBusts = new List<NodeRef>();
        public NodeRef StageBox;
        public NodeRef StageLineText;
        public NodeRef StageNamePlate;
        public NodeRef StageName;
        public NodeRef StageEpithet;
        public NodeRef StageTitle;

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

                // After the legacy layout so it draws over it, before the rows
                // so the rows draw over it. Both are bare panels, which A1
                // already treats as unable to cover anything; the stage's own
                // contents are all decor.
                screen.BuildStage(),
                screen.BuildChoices(),
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

            var frame = Ui.OutlineBox("EventArtFrame", Place.At(ArtCentreX, 0f), size, FrameFill, FrameRim,
                new[] { art });
            ArtFrame = frame;
            return frame;
        }

        private UiNode BuildTextColumn()
        {
            var parts = new List<UiNode>();
            float columnHeight = ColumnTop - TextColumnBottom;
            float columnCentreY = (ColumnTop + TextColumnBottom) * 0.5f;

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

            var column = Ui.Panel("EventTextColumn", Place.At(TextCentreX, columnCentreY),
                UiSize.Fixed(TextWidth, columnHeight), parts);
            TextColumn = column;
            return column;
        }

        // The rows at their legacy spot, under the text column. They keep
        // their names (EventChoice0..3) and their screen positions; only their
        // parent moved.
        private UiNode BuildChoices()
        {
            var rows = new List<UiNode>();
            for (int i = 0; i < ChoiceRowCount; i++)
            {
                float y = ChoicesHeight * 0.5f - RowHeight * 0.5f - i * (RowHeight + RowGap);
                var row = BuildChoiceRow(i, y);
                ChoiceRows.Add(row);
                rows.Add(row.Button.Node);
            }

            var panel = Ui.Panel("EventChoices", Place.At(ChoicesLegacyX, ChoicesLegacyY),
                UiSize.Fixed(ChoiceRowWidth, ChoicesHeight), rows);
            ChoicesPanel = panel;
            return panel;
        }

        // A button with its own fill and rim rather than a themed plate: a
        // 704x80 row is nowhere near any plate's aspect, and the pack rows
        // (ShopScreen.BuildPackRow) are the same answer to the same shape.
        // Starts inactive; the controller shows one row per visible choice.
        private ChoiceRow BuildChoiceRow(int index, float y)
        {
            string name = $"EventChoice{index}";
            var size = new UiVec(ChoiceRowWidth, RowHeight);
            float inner = ChoiceRowWidth - RowPad * 2f;

            var children = new List<UiNode>
            {
                Ui.Solid($"{name}Fill", RowFill, size, Place.At(0f, 0f)).AsDecor(),
            };
            children.AddRange(Ui.Rim(name, size, FrameRim));

            // Built as an open row: the label centred, the lock reason hidden
            // at its place in the pair. EventController moves the label up to
            // the pair's top line when it shows a reason (ChoiceTextY).
            var text = Ui.Label($"{name}Text", UiStrings.EventChoice, new UiVec(inner, RowTextHeight), ChoiceFontSize,
                    ChoiceText, Place.At(0f, ChoiceTextY(withLock: false)))
                .Styled(TypographyRole.Body)
                .TextAligned(UiTextAlign.Left)
                .AsDecor();
            children.Add(text);

            // The label's own family, smaller, in the lock colour: a second
            // typeface under the label read as a different kind of text.
            var lockReason = Ui.Label($"{name}Lock", UiStrings.EventChoiceLocked, new UiVec(inner, RowLockHeight), LockFontSize,
                    LockReasonHex, Place.At(0f, ChoiceLockY))
                .Styled(TypographyRole.Body)
                .TextAligned(UiTextAlign.Left)
                .AsDecor()
                .Inactive();
            children.Add(lockReason);

            var button = Ui.Button(name, UiString.Runtime, size, 12, Place.At(0f, y)).NoChrome().Inactive();
            foreach (var child in children) button.Children.Add(child);

            return new ChoiceRow { Button = button, Text = text, Lock = lockReason };
        }

        // ---- the dialogue stage -------------------------------------------------
        //
        // Built at the LEFT-speaker layout (DialogueStageLayout's pins with
        // DialogueSide.Left). The controller re-pins the bust, box, plate and
        // choices for a right-hand speaker, for narration and for a speaker
        // whose bust is missing (DialogueStageLayout.FramingFor), and sizes the
        // backdrop and bust to their sprites; nothing here is stretched.
        //
        // EVERY PIECE IS DECOR. None of it takes a click in D2 (D3 adds the
        // advance press as its own catcher), and decor is what exempts the
        // intended overlaps -- bust behind box, plate across the box's top
        // edge, set piece behind the bust, the whole stage over the legacy
        // layout. An AllowOverlap on a decor node is refused by UiAudit's A7
        // as a waiver that waives nothing, so each overlap's reason is the
        // comment beside its piece rather than an AllowOverlap string.

        private const string StageGroundHex = "#07050BFF";

        // Multiplies the backdrop down so the bust and box read in front of
        // it (layer 0: "darkened so the front reads").
        private const string BackdropTintHex = "#8C8C8CFF";
        private const string ShadeHex = "#000000C8";
        private const string VignetteHex = "#00000099";
        private const float ShadeTopHeight = 180f;
        private const float ShadeBottomHeight = 460f;

        // Painted at 2x and drawn at exactly half (2560x512 -> 1280x256,
        // 960x192 -> 480x96), so the plain sprite is already at its authored
        // aspect -- no container kit, no stretch.
        public const string BoxSpriteKey = "UI/Dialogue/dialogue_box.png";
        public const string PlateSpriteKey = "UI/Dialogue/dialogue_nameplate.png";

        // The set piece hangs from the top of the stage, under the title.
        public const float SetPieceTop = 64f;

        // Inside the box's painted frame (about 26px at the drawn size), with
        // the top inset deeper so the plate's lower third never covers text.
        private const float LinePadX = 48f;
        private const float LinePadTop = 48f;
        private const float LinePadBottom = 28f;
        public const float LineWidth = DialogueStageLayout.BoxWidth - LinePadX * 2f;
        public const float LineHeight = DialogueStageLayout.BoxHeight - LinePadTop - LinePadBottom;
        // 32: the result beat binds, not the line: a
        // cap-length result plus its effects line wraps to four lines, about
        // 175 of the 180 available at 32, and overflows at 34. The line at
        // the cap alone is three. DialogueStageTextFitTests measures both in
        // the real label.
        public const int LineFontSize = 32;

        // Two lines in the plate's inner ~452x70; the controller centres the
        // name alone when the epithet is empty (every character today).
        private const float PlateTextWidth = DialogueStageLayout.PlateWidth - 40f;
        private const float NameHeight = 40f;
        private const float EpithetHeight = 26f;
        public const float NameWithEpithetY = 12f;
        public const int EpithetFontSize = 18;
        private const float EpithetY = -21f;

        private const float StageTitlePadX = 48f;
        private const float StageTitlePadTop = 20f;
        private const float StageTitleWidth = 720f;
        private const float StageTitleHeight = 44f;

        private const string StageBustAName = "StageBustA";
        private const string StageBustBName = "StageBustB";

        private UiNode BuildStage()
        {
            var parts = new List<UiNode>();

            // Contract 15: with no backdrop sprite this ground is all that
            // shows. Always under the backdrop, so the map never shows through.
            parts.Add(Ui.Solid("StageGround", StageGroundHex, Place.Stretch(), UiSize.Fill).AsDecor());

            // Layer 0. Canvas-sized at build; the controller re-sizes it to
            // cover (DialogueStageLayout.CoverSize) and the stage's mask crops
            // the overhang. Centred, so the crop is even on both sides.
            var backdrop = Ui.Sprite("StageBackdrop", null, Place.At(0f, 0f), UiSize.Fixed(UiFrames.Reference))
                .Coloured(BackdropTintHex)
                .AsDecor();
            StageBackdrop = backdrop;
            parts.Add(backdrop);

            // Layer 1. Bars top and bottom, the vignette closing both edges.
            parts.Add(Ui.Sprite("StageShadeTop", "proc:scrim_ceiling",
                    Place.Pin(new UiVec(0.5f, 1f), new UiVec(0.5f, 1f), UiVec.Zero), UiSize.FillWidth(ShadeTopHeight))
                .Coloured(ShadeHex)
                .AsDecor());
            parts.Add(Ui.Sprite("StageShadeBottom", "proc:scrim_floor",
                    Place.Pin(new UiVec(0.5f, 0f), new UiVec(0.5f, 0f), UiVec.Zero), UiSize.FillWidth(ShadeBottomHeight))
                .Coloured(ShadeHex)
                .AsDecor());
            parts.Add(Ui.Sprite("StageVignette", "proc:vignette", Place.Stretch(), UiSize.Fill)
                .Coloured(VignetteHex)
                .AsDecor());

            // Layer 2: the page's artPath, from the same table the legacy
            // frame reads (contract 6). Hidden when the page has none. The
            // bust in front of it may cover its near edge; that is the layer
            // order, not a collision.
            var setPiece = Ui.Sprite("StageSetPiece", null,
                    Place.Pin(new UiVec(0.5f, 1f), new UiVec(0.5f, 1f), new UiVec(0f, -SetPieceTop)),
                    UiSize.Fixed(StageArtWidth, StageArtHeight))
                .AsDecor();
            setPiece.PreserveAspect = true;
            StageSetPiece = setPiece;
            parts.Add(setPiece);

            // Layer 3: the busts, bottom-anchored against the speaker's edge
            // and standing BEHIND the box they overlap (the Hades layering:
            // the speaker leans into the frame, the words stay on top).
            // Square here; the controller sizes each to its sprite's aspect.
            foreach (string bustName in new[] { StageBustAName, StageBustBName })
            {
                const float height = DialogueStageLayout.BustHeight;
                var pin = DialogueStageLayout.BustPin(Content.DialogueSide.Left, height);
                var bust = Ui.Sprite(bustName, null,
                        Place.Pin(new UiVec(pin.AnchorX, 0f), new UiVec(pin.PivotX, 0f), new UiVec(pin.OffsetX, 0f)),
                        UiSize.Fixed(height, height))
                    .AsDecor();
                bust.PreserveAspect = true;
                if (bustName == StageBustBName) bust.Inactive();
                StageBusts.Add(bust);
                parts.Add(bust);
            }

            // Layer 4: the box, in front of the bust, with the line inside.
            var boxPin = DialogueStageLayout.BoxPin(StageFraming.LeftSpeaker);
            var line = Ui.Label("StageLineText", UiStrings.EventLine, new UiVec(LineWidth, LineHeight), LineFontSize,
                    BodyText, Place.At(0f, (LinePadBottom - LinePadTop) * 0.5f))
                .Styled(TypographyRole.Body)
                .TextAligned(UiTextAlign.TopLeft);
            StageLineText = line;

            var box = Ui.Sprite("StageBox", BoxSpriteKey,
                    Place.Pin(new UiVec(boxPin.AnchorX, 0f), new UiVec(boxPin.PivotX, 0f),
                        new UiVec(boxPin.OffsetX, DialogueStageLayout.BoxBottom)),
                    UiSize.Fixed(DialogueStageLayout.BoxWidth, DialogueStageLayout.BoxHeight))
                .AsDecor();
            box.PreserveAspect = true;
            box.Children.Add(line);
            StageBox = box;
            parts.Add(box);

            // The plate straddles the box's top edge at the speaker's end, in
            // front of the box: the name is a label pinned to the frame.
            var platePin = DialogueStageLayout.PlatePin(StageFraming.LeftSpeaker);
            var name = Ui.Label("StageName", UiString.Runtime, new UiVec(PlateTextWidth, NameHeight), 30,
                    TitleText, Place.At(0f, NameWithEpithetY))
                .Styled(TypographyRole.FunctionalHeading)
                .TextAligned(UiTextAlign.Left)
                .Truncated();
            StageName = name;

            var epithet = Ui.Label("StageEpithet", UiStrings.EventEpithet, new UiVec(PlateTextWidth, EpithetHeight), EpithetFontSize,
                    EffectsText, Place.At(0f, EpithetY))
                .Styled(TypographyRole.Body)
                .TextAligned(UiTextAlign.Left)
                .Inactive();
            StageEpithet = epithet;

            var plate = Ui.Sprite("StageNamePlate", PlateSpriteKey,
                    Place.Pin(new UiVec(platePin.AnchorX, 0f), new UiVec(platePin.PivotX, 0.5f),
                        new UiVec(platePin.OffsetX, DialogueStageLayout.PlateCentreY)),
                    UiSize.Fixed(DialogueStageLayout.PlateWidth, DialogueStageLayout.PlateHeight))
                .AsDecor();
            plate.PreserveAspect = true;
            plate.Children.Add(name);
            plate.Children.Add(epithet);
            StageNamePlate = plate;
            parts.Add(plate);

            // The page title, small, over the top bar.
            var title = Ui.Label("StageTitle", UiStrings.EventTitle, new UiVec(StageTitleWidth, StageTitleHeight), 30,
                    TitleText,
                    Place.Pin(new UiVec(0f, 1f), new UiVec(0f, 1f), new UiVec(StageTitlePadX, -StageTitlePadTop)))
                .Styled(TypographyRole.FunctionalHeading)
                .TextAligned(UiTextAlign.Left)
                .AsDecor();
            StageTitle = title;
            parts.Add(title);

            // The mask is what crops the cover-sized backdrop's overhang.
            var stage = Ui.Panel("DialogueStage", Place.Stretch(), UiSize.Fill, parts)
                .Clipping()
                .Inactive();
            DialogueStage = stage;
            return stage;
        }
    }
}

using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // THE "GAINED" CHIP STRIP (docs/PLAN_BUGS_QA.md C2): one chip per
    // relic the player has just received, icon and name, plus the detail
    // tooltip a chip opens. One builder, two surfaces -- the Reckoning above its
    // Continue, the event page above its choice rows -- painted by the one
    // presenter (Core/RelicChipStrip.cs) over the relic row model.
    //
    // The strip and the tooltip are separate nodes because they live in
    // different places: the strip sits where the surface puts it, the tooltip
    // is declared last in the surface's own space so it draws over everything
    // and can stand clear of the strip it describes.
    //
    // Chips are pinned by their LEFT edge inside the strip so the presenter
    // can re-lay them for the number actually shown (RelicChipLayout); every
    // child of a chip stretches with it.
    public sealed class RelicChipTree
    {
        // At most this many at once; no authored outcome grants more than 2.
        public const int ChipCount = 4;

        public const float StripHeight = 36f;
        public const float ChipHeight = 32f;
        public const float ChipGap = 8f;
        public const float CaptionWidth = 84f;
        // Room for the pad's focus marker, which stands left of a focused chip.
        public const float CaptionGap = 32f;
        public const float IconSize = 24f;
        public const float IconInset = 5f;
        public const float NameInset = IconInset + IconSize + 8f;
        public const float NameRightPad = 10f;
        public const float MinChipWidth = 96f;
        public const int NameFontSize = 20;

        public const float TooltipWidth = 380f;
        public const float TooltipPad = 14f;
        public const float TooltipNameHeight = 30f;
        public const float TooltipMetaHeight = 24f;
        public const float TooltipBodyGap = 6f;
        public const float TooltipHeaderHeight = TooltipNameHeight + TooltipMetaHeight + TooltipBodyGap;

        // The tallest a description can make it; the presenter shrinks it to
        // the text it holds.
        public const float TooltipMaxHeight = 300f;
        public const float TooltipMinBodyHeight = 26f;

        private const string ChipFill = FightHudPalette.SubmenuRowFill;
        private const string ChipRim = FightHudPalette.BorderQuiet;
        private const string NameText = FightHudPalette.RowNameText;
        private const string CaptionText = FightHudPalette.GoldText;

        public UiNode Strip;
        public UiNode Caption;
        public readonly List<NodeRef> Chips = new List<NodeRef>();
        public readonly List<NodeRef> ChipIcons = new List<NodeRef>();
        public readonly List<NodeRef> ChipNames = new List<NodeRef>();

        public UiNode Tooltip;
        public NodeRef TooltipName;
        public NodeRef TooltipMeta;
        public NodeRef TooltipBody;

        // Width left for the chips once the caption has taken its place.
        public static float ChipAreaWidth(float stripWidth) => stripWidth - CaptionWidth - CaptionGap;

        public static RelicChipTree Build(string prefix, Place place, float stripWidth, Place tooltipPlace)
        {
            var tree = new RelicChipTree();
            float chipArea = ChipAreaWidth(stripWidth);
            float share = RelicChipLayout.Share(ChipCount, chipArea, ChipGap);

            var children = new List<UiNode>();

            tree.Caption = Ui.Label(prefix + "Caption", UiStrings.RelicGained, new UiVec(CaptionWidth, StripHeight), 20,
                    CaptionText, Place.Pin(new UiVec(0f, 0.5f), new UiVec(0f, 0.5f), UiVec.Zero))
                .Styled(TypographyRole.Body)
                .TextAligned(UiTextAlign.Left)
                .AsDecor();
            children.Add(tree.Caption);

            for (int i = 0; i < ChipCount; i++)
            {
                float left = CaptionWidth + CaptionGap + i * (share + ChipGap);
                children.Add(tree.BuildChip(prefix + "Chip" + i, left, share));
            }

            tree.Strip = Ui.Panel(prefix + "Strip", place, UiSize.Fixed(stripWidth, StripHeight), children)
                .Inactive();

            tree.BuildTooltip(prefix + "Tooltip", tooltipPlace);
            return tree;
        }

        private UiNode BuildChip(string name, float left, float width)
        {
            var size = new UiVec(width, ChipHeight);

            var children = new List<UiNode>
            {
                Ui.Solid(name + "Fill", ChipFill, Place.Stretch(), UiSize.Fill).AsDecor(),
            };
            children.AddRange(StretchRim(name, ChipRim));

            var icon = Ui.Sprite(name + "Icon", null,
                    Place.Pin(new UiVec(0f, 0.5f), new UiVec(0f, 0.5f), new UiVec(IconInset, 0f)),
                    UiSize.Fixed(IconSize, IconSize))
                .AsDecor();
            icon.PreserveAspect = true;
            children.Add(icon);

            var label = Ui.Label(name + "Name", UiString.Runtime, new UiVec(width - NameInset - NameRightPad, ChipHeight),
                    NameFontSize, NameText, Place.Stretch(left: NameInset, right: NameRightPad))
                .Styled(TypographyRole.Body)
                .TextAligned(UiTextAlign.Left)
                .Truncated()
                .AsDecor();
            children.Add(label);

            var chip = Ui.Button(name, UiString.Runtime, size, 12,
                    Place.Pin(new UiVec(0f, 0.5f), new UiVec(0f, 0.5f), new UiVec(left, 0f)))
                .NoChrome()
                .Inactive();
            foreach (var child in children) chip.Children.Add(child);

            Chips.Add(chip);
            ChipIcons.Add(icon);
            ChipNames.Add(label);
            return chip;
        }

        // A one-pixel rim that follows the chip's size, since the presenter
        // resizes chips at runtime and Ui.Rim's edges are fixed boxes.
        private static IEnumerable<UiNode> StretchRim(string stem, string colorHex)
        {
            yield return Edge(stem + "RimTop", new UiVec(0f, 1f), new UiVec(1f, 1f), 0f, 0f, -1f, 0f, colorHex);
            yield return Edge(stem + "RimBottom", new UiVec(0f, 0f), new UiVec(1f, 0f), 0f, 0f, 0f, -1f, colorHex);
            yield return Edge(stem + "RimLeft", new UiVec(0f, 0f), new UiVec(0f, 1f), 0f, -1f, 0f, 0f, colorHex);
            yield return Edge(stem + "RimRight", new UiVec(1f, 0f), new UiVec(1f, 1f), -1f, 0f, 0f, 0f, colorHex);
        }

        private static UiNode Edge(string name, UiVec anchorMin, UiVec anchorMax,
            float left, float right, float bottom, float top, string colorHex) =>
            Ui.Solid(name, colorHex, Place.Frac(anchorMin, anchorMax, left, right, bottom, top), UiSize.Fill)
                .AsDecor();

        // Name, rarity and description, top-down from the box's top edge so the
        // presenter can shrink the box to the description without moving them.
        private void BuildTooltip(string name, Place place)
        {
            float inner = TooltipWidth - TooltipPad * 2f;
            var top = new UiVec(0.5f, 1f);

            var title = Ui.Label(name + "Name", UiString.Runtime, new UiVec(inner, TooltipNameHeight), 22,
                    FightHudPalette.HeadingGold, Place.Pin(top, top, new UiVec(0f, -TooltipPad)))
                .Styled(TypographyRole.Body)
                .TextAligned(UiTextAlign.Left)
                .Truncated();
            var meta = Ui.Label(name + "Meta", UiString.Runtime, new UiVec(inner, TooltipMetaHeight), 20,
                    "#B8A8D9", Place.Pin(top, top, new UiVec(0f, -TooltipPad - TooltipNameHeight)))
                .Styled(TypographyRole.Body)
                .TextAligned(UiTextAlign.Left);
            float bodyHeight = TooltipMaxHeight - TooltipPad * 2f - TooltipHeaderHeight;
            var body = Ui.Label(name + "Body", UiString.Runtime, new UiVec(inner, bodyHeight), 20,
                    "#D9CCF2", Place.Pin(top, top, new UiVec(0f, -TooltipPad - TooltipHeaderHeight)))
                .Styled(TypographyRole.Body)
                .TextAligned(UiTextAlign.TopLeft);

            TooltipName = title;
            TooltipMeta = meta;
            TooltipBody = body;

            Tooltip = Ui.Tooltip(name, null, "#1D1226", place,
                UiSize.Fixed(TooltipWidth, TooltipMaxHeight), title, meta, body);
        }
    }
}

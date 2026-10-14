using System.Collections.Generic;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // THE HONING MODAL (docs/PLAN_ITEMS.md 1.5): one member's eight slots,
    // each with its honing, what the next +1 costs, and a button. Two hosts
    // share it because the choice is the same one and only the price and the
    // way out differ:
    //
    //   - the shop opens it from HONE, priced per plus and per step, closed
    //     with Close;
    //   - a Rest room opens it with a HEAL button beside the rows, the hone
    //     free, and no way out but a pick.
    //
    // Built once, nested in MapScreen after the shop so it draws over it.
    // Every row is one slot of EquipmentSlots.All; nothing is keyed to the
    // item in the slot, because honing belongs to the slot.
    public sealed class HoneScreen
    {
        private const string PanelRim = FightHudPalette.BorderQuiet;
        private const string TitleText = FightHudPalette.TextPrimary;
        private const string MetaText = FightHudPalette.TextSecondary;
        private const string PriceText = FightHudPalette.GoldText;
        private const string GoldText = FightHudPalette.GoldLight;
        private const string DetailText = FightHudPalette.RowNameText;
        private const string QuietText = FightHudPalette.TextMuted;

        private const float ModalWidth = 1100f;
        private const float ModalHeight = 860f;
        private const float RowWidth = 980f;
        private const float RowHeight = 60f;
        private const float RowGap = 8f;
        private const float FirstRowY = 186f;

        public static readonly int RowCount = EquipmentSlots.All.Length;

        public sealed class Row
        {
            public NodeRef Root;
            public NodeRef SlotName;
            public NodeRef Level;
            public NodeRef Price;
            public NodeRef HoneButton;
            public NodeRef HoneButtonLabel;
        }

        public UiNode Root;

        public NodeRef TitleLabel;
        public NodeRef HintLabel;
        public NodeRef GoldLabel;
        public NodeRef CloseButton;
        public NodeRef HealButton;
        public NodeRef CharacterName;
        public NodeRef PrevCharacterButton;
        public NodeRef NextCharacterButton;
        public List<Row> Rows = new List<Row>();

        public static HoneScreen Build()
        {
            var screen = new HoneScreen();
            var modalSize = new UiVec(ModalWidth, ModalHeight);

            var content = new List<UiNode>
            {
                Ui.Solid("HoneBackdrop", FightHudPalette.PanelVioletDeep, Place.At(0f, 0f), UiSize.Fixed(modalSize))
                    .AsDecor(),
            };
            content.AddRange(Ui.Rim("Hone", modalSize, PanelRim));

            // Sized for the wider of its two runtime strings (HONE, REST).
            var title = Ui.Label("HoneTitle", UiStrings.HoneTitleShop, new UiVec(500f, 56f), 40,
                    TitleText, Place.At(-290f, 380f))
                .Styled(TypographyRole.FunctionalHeading)
                .TextAligned(UiTextAlign.Left);
            screen.TitleLabel = title;
            content.Add(title);

            var gold = Ui.Label("HoneGoldLabel", UiStrings.ShopGold, new UiVec(320f, 48f), 32,
                    GoldText, Place.At(190f, 380f))
                .Styled(TypographyRole.TacticalData)
                .AsDecor();
            screen.GoldLabel = gold;
            content.Add(gold);

            content.Add(Ui.Solid("HoneTitleRule", FightHudPalette.Hairline, new UiVec(ModalWidth, 1f), Place.At(0f, 344f))
                .AsDecor());

            var close = Ui.Button("HoneClose", UiStrings.Close, Ui.PlateNominalSizeFor(160f, 48f), 26,
                    Place.At(460f, 380f))
                .Closes()
                .Themed(ButtonTheme.Silver);
            screen.CloseButton = close;
            content.Add(close);

            var hint = Ui.Label("HoneHint", UiStrings.HoneHintShop, new UiVec(960f, 32f), 22,
                    QuietText, Place.At(0f, 312f))
                .Styled(TypographyRole.Body);
            screen.HintLabel = hint;
            content.Add(hint);

            // The member picker, the shop's shape: an arrow either side of a
            // runtime name.
            var name = Ui.Label("HoneCharacterName", UiString.Runtime, new UiVec(400f, 40f), 26, DetailText,
                Place.At(0f, 258f));
            screen.CharacterName = name;
            content.Add(name);

            var pager = Ui.Pager("HonePrevCharacter", Place.At(-260f, 258f), "HoneNextCharacter", Place.At(260f, 258f),
                new UiVec(40f, 40f), 18);
            screen.PrevCharacterButton = pager.Prev;
            screen.NextCharacterButton = pager.Next;
            content.Add(pager.Prev);
            content.Add(pager.Next);

            for (int i = 0; i < RowCount; i++)
            {
                var row = screen.BuildRow(i, FirstRowY - i * (RowHeight + RowGap));
                screen.Rows.Add(row);
                content.Add(row.Root.Node);
            }

            // Rest only: the other choice.
            var heal = Ui.Button("HoneHealButton", UiStrings.HoneHeal, Ui.PlateNominalSizeFor(420f, 64f), 30,
                    Place.At(0f, -372f))
                .Themed(ButtonTheme.Gold);
            screen.HealButton = heal;
            content.Add(heal);

            var panel = Ui.Panel("HoneContent", Place.At(0f, 0f), UiSize.Fixed(modalSize), content);
            screen.Root = Ui.Modal("HoneModal", "#0A0614E0", panel).Inactive();
            return screen;
        }

        // `y` is the ROW's position in the modal; its children are row-local.
        private Row BuildRow(int index, float y)
        {
            var size = new UiVec(RowWidth, RowHeight);
            string stem = $"HoneRow{index}";

            var children = new List<UiNode>
            {
                Ui.Solid($"{stem}Fill", FightHudPalette.SubmenuRowFill, size, Place.At(0f, 0f)).AsDecor(),
            };
            children.AddRange(Ui.Rim(stem, size, PanelRim));

            var slot = Ui.Label($"{stem}Slot", UiString.Runtime, new UiVec(300f, 30f), 24, DetailText,
                    Place.At(-330f, 0f))
                .Styled(TypographyRole.Body)
                .TextAligned(UiTextAlign.Left)
                .AsDecor();
            children.Add(slot);

            var level = Ui.Label($"{stem}Level", UiStrings.HoneLevel, new UiVec(220f, 26f), 20, MetaText,
                    Place.At(-80f, 0f))
                .Styled(TypographyRole.TacticalData)
                .TextAligned(UiTextAlign.Left)
                .AsDecor();
            children.Add(level);

            var price = Ui.Label($"{stem}Price", UiStrings.HonePrice, new UiVec(200f, 28f), 22, PriceText,
                    Place.At(135f, 0f))
                .Styled(TypographyRole.TacticalData)
                .AsDecor();
            children.Add(price);

            var buttonSize = Ui.PlateNominalSizeFor(170f, 44f);
            var button = Ui.Button($"{stem}Button", UiStrings.HoneButton, buttonSize, 22, Place.At(385f, 0f))
                .Themed(ButtonTheme.Silver);
            children.Add(button);

            var root = Ui.Panel(stem, Place.At(0f, y), UiSize.Fixed(size), children);

            return new Row
            {
                Root = root,
                SlotName = slot,
                Level = level,
                Price = price,
                HoneButton = button,
                HoneButtonLabel = Ui.CaptionOf(button),
            };
        }
    }
}

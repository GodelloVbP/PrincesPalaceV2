using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.Domain.UiKit.Screens
{
    // What you are wearing and what you are carrying, in one place.
    //
    // An OVERLAY, not a screen: it mounts as a Modal inside the hub and dims
    // what is behind it. v1 split this across two separate screens that could
    // only agree because both called the same service -- its own CODE_MAP
    // called one of them "legacy". One place, two panes.
    //
    // The panes share a cell builder, so a slot on the body and a cell in the
    // bag are the same object wearing different sizes. That is what makes
    // "click a bag cell, click a body slot" feel like one gesture rather than
    // two systems meeting.
    public sealed class CharacterOverlayScreen
    {
        public const string SilhouetteKey = "UI/CharacterOverlay/Processed/armour_stand.png";
        public const string CellFrameKey = "UI/Panels/gear_cell_frame.png";

        public UiNode Root;

        public NodeRef Silhouette;
        public NodeRef CharacterName;
        public NodeRef PrevCharacterButton;
        public NodeRef NextCharacterButton;
        public NodeRef CloseButton;

        public NodeRef PageLabel;
        public NodeRef PrevPageButton;
        public NodeRef NextPageButton;

        public NodeRef DetailName;
        public NodeRef DetailBody;
        public NodeRef ActionButton;
        public NodeRef ActionLabel;

        // Indexed by EquipmentSlot ordinal — declaration order IS enum order,
        // so slot i is EquipmentSlots.All[i] with no lookup table to drift.
        public List<NodeRef> SlotCells = new List<NodeRef>();
        public List<NodeRef> SlotIcons = new List<NodeRef>();
        public List<NodeRef> SlotBackings = new List<NodeRef>();
        public List<NodeRef> SlotRarityEdges = new List<NodeRef>();
        public List<NodeRef> SlotPlusLabels = new List<NodeRef>();

        // Indexed 0..BagView.CellCount-1, refilled per page at runtime.
        public List<NodeRef> BagCells = new List<NodeRef>();
        public List<NodeRef> BagIcons = new List<NodeRef>();
        public List<NodeRef> BagBackings = new List<NodeRef>();
        public List<NodeRef> BagRarityEdges = new List<NodeRef>();
        public List<NodeRef> BagCountLabels = new List<NodeRef>();
        public List<NodeRef> BagPlusLabels = new List<NodeRef>();

        public static CharacterOverlayScreen Build()
        {
            var screen = new CharacterOverlayScreen();

            var silhouette = Ui.Sprite("OverlaySilhouette", SilhouetteKey,
                    Place.At(OverlayAnchors.Silhouette.X, OverlayAnchors.Silhouette.Y),
                    UiSize.Fixed(OverlayAnchors.SilhouetteSize.X, OverlayAnchors.SilhouetteSize.Y))
                .AsDecor();
            screen.Silhouette = silhouette;

            // Decor rather than an exemption. Decor is skipped by the sibling
            // overlap check AND has its raycast cleared, which is exactly what
            // a body-shaped backdrop under eight clickable slots needs — the
            // one thing worse than a slot that looks wrong is a slot that
            // cannot be clicked because the picture behind it took the press.
            var paperdoll = new List<UiNode> { silhouette };
            paperdoll.AddRange(EquipmentSlots.All.Select(screen.BuildSlotCell));

            var bagCells = Ui.Each(
                Enumerable.Range(0, BagView.CellCount).ToList(),
                (_, i) => screen.BuildBagCell(i));

            var bag = Ui.Grid("BagGrid",
                Place.At(OverlayAnchors.Bag.X, OverlayAnchors.Bag.Y),
                OverlayAnchors.BagColumns,
                OverlayAnchors.BagCell,
                OverlayAnchors.BagGap,
                bagCells);

            var content = Ui.Panel("CharacterOverlayContent", Place.At(0f, 0f), UiSize.Fixed(1920f, 1080f),
                BuildChrome(screen).Concat(paperdoll).Append(bag));

            screen.Root = Ui.Modal("CharacterOverlayPanel", "#000000D9", content).Inactive();
            return screen;
        }

        private static IEnumerable<UiNode> BuildChrome(CharacterOverlayScreen screen)
        {
            var name = Ui.Label("OverlayCharacterName", UiString.Runtime, new UiVec(420f, 52f), 30,
                    "#EDE6FF", Place.At(OverlayAnchors.CharacterName.X, OverlayAnchors.CharacterName.Y))
                .AsDecor();
            screen.CharacterName = name;

            var prevCharacter = Ui.Button("OverlayPrevCharacter", UiStrings.TalentPrev, new UiVec(56f, 52f), 24,
                Place.At(-OverlayAnchors.CharacterArrowX, OverlayAnchors.CharacterName.Y));
            var nextCharacter = Ui.Button("OverlayNextCharacter", UiStrings.TalentNext, new UiVec(56f, 52f), 24,
                Place.At(OverlayAnchors.CharacterArrowX, OverlayAnchors.CharacterName.Y));
            screen.PrevCharacterButton = prevCharacter;
            screen.NextCharacterButton = nextCharacter;

            var page = Ui.Label("OverlayPageLabel", UiStrings.OverlayPage, new UiVec(320f, 40f), 18,
                    "#B8A8D9", Place.At(OverlayAnchors.Pager.X, OverlayAnchors.Pager.Y))
                .AsDecor();
            var prevPage = Ui.Button("OverlayPrevPage", UiStrings.TalentPrev, new UiVec(56f, 48f), 22,
                Place.At(OverlayAnchors.Pager.X - 260f, OverlayAnchors.Pager.Y));
            var nextPage = Ui.Button("OverlayNextPage", UiStrings.TalentNext, new UiVec(56f, 48f), 22,
                Place.At(OverlayAnchors.Pager.X + 260f, OverlayAnchors.Pager.Y));
            screen.PageLabel = page;
            screen.PrevPageButton = prevPage;
            screen.NextPageButton = nextPage;

            // Plate-local coordinates: these are its children, so a screen-space
            // y would put them a third of a screen below the plate meant to
            // contain them.
            var detailName = Ui.Label("OverlayDetailName", UiString.Runtime, new UiVec(1100f, 40f), 24,
                "#EDE6FF", Place.At(0f, 44f));
            var detailBody = Ui.Label("OverlayDetailBody", UiString.Runtime, new UiVec(1100f, 72f), 17,
                "#B8A8D9", Place.At(0f, -18f));
            screen.DetailName = detailName;
            screen.DetailBody = detailBody;

            var plate = Ui.Panel("OverlayDetailPlate",
                    Place.At(OverlayAnchors.DetailPlate.X, OverlayAnchors.DetailPlate.Y),
                    UiSize.Fixed(OverlayAnchors.DetailPlateSize.X, OverlayAnchors.DetailPlateSize.Y),
                    detailName, detailBody)
                .Coloured("#2C1C42E0")
                .AsDecor();

            var actionLabel = Ui.Label("OverlayActionLabel", UiStrings.OverlayEquip, new UiVec(260f, 40f), 20,
                "#F2DB9E", Place.At(0f, 0f));
            var action = Ui.Button("OverlayActionButton", UiString.Runtime,
                new UiVec(OverlayAnchors.ActionButtonSize.X, OverlayAnchors.ActionButtonSize.Y), 20,
                Place.At(OverlayAnchors.ActionButton.X, OverlayAnchors.ActionButton.Y));
            action.Children.Add(actionLabel);
            screen.ActionButton = action;
            screen.ActionLabel = actionLabel;

            var close = Ui.Button("OverlayCloseButton", UiStrings.Close, new UiVec(220f, 60f), 16,
                Place.At(OverlayAnchors.CloseButton.X, OverlayAnchors.CloseButton.Y));
            screen.CloseButton = close;

            // The plate is declared before the action button so the button
            // draws over it where they meet.
            return new[] { name, prevCharacter, nextCharacter, page, prevPage, nextPage, plate, action, close };
        }

        private UiNode BuildSlotCell(EquipmentSlot slot)
        {
            var at = OverlayAnchors.PositionFor(slot);
            var cell = BuildCell($"Slot{slot}", OverlayAnchors.SlotCell, OverlayAnchors.SlotIconInset,
                Place.At(at.X, at.Y), withCount: false, out var parts);

            SlotCells.Add(cell);
            SlotBackings.Add(parts.Backing);
            SlotRarityEdges.Add(parts.RarityEdge);
            SlotIcons.Add(parts.Icon);
            SlotPlusLabels.Add(parts.Plus);
            return cell;
        }

        private UiNode BuildBagCell(int index)
        {
            // Flow placement: the Grid owns where its children go, which is the
            // whole reason to use one.
            var cell = BuildCell($"BagCell{index}", OverlayAnchors.BagCell.X, OverlayAnchors.BagIconInset,
                Place.Flow, withCount: true, out var parts);

            BagCells.Add(cell);
            BagBackings.Add(parts.Backing);
            BagRarityEdges.Add(parts.RarityEdge);
            BagIcons.Add(parts.Icon);
            BagCountLabels.Add(parts.Count);
            BagPlusLabels.Add(parts.Plus);
            return cell;
        }

        private readonly struct CellParts
        {
            public readonly NodeRef Backing;
            public readonly NodeRef RarityEdge;
            public readonly NodeRef Icon;
            public readonly NodeRef Count;
            public readonly NodeRef Plus;

            public CellParts(NodeRef backing, NodeRef edge, NodeRef icon, NodeRef count, NodeRef plus)
            {
                Backing = backing;
                RarityEdge = edge;
                Icon = icon;
                Count = count;
                Plus = plus;
            }
        }

        // One cell, layered. Every layer says exactly one thing and every layer
        // but the button itself is decor — so intra-cell stacking needs no
        // exemptions and no layer can steal the click from the cell.
        private static UiNode BuildCell(string name, float size, float iconInset, Place place,
                                        bool withCount, out CellParts parts)
        {
            var backing = Ui.Solid($"{name}Backing", OverlayAnchors.CellBacking,
                    Place.Stretch(), UiSize.Fill)
                .AsDecor();

            // Sits along the bottom edge. Transparent at build: an empty cell
            // has no rarity, and the controller tints it when one arrives.
            var edge = Ui.Solid($"{name}Rarity", "#FFFFFF00",
                    Place.Frac(new UiVec(0f, 0f), new UiVec(1f, 0f), top: -OverlayAnchors.RarityEdgeHeight),
                    UiSize.Fill)
                .AsDecor();

            var icon = Ui.Sprite($"{name}Icon", null,
                    Place.Stretch(iconInset, iconInset, iconInset, iconInset), UiSize.Fill)
                .AsDecor();

            var frame = Ui.Sprite($"{name}Frame", CellFrameKey, Place.Stretch(), UiSize.Fill)
                .AsDecor();

            float half = size * 0.5f;
            var plus = Ui.Label($"{name}Plus", UiString.Runtime, OverlayAnchors.BadgeSize, 15, "#F2DB9E",
                    Place.At(half - OverlayAnchors.BadgeSize.X * 0.5f - OverlayAnchors.BadgeInset,
                             half - OverlayAnchors.BadgeSize.Y * 0.5f - OverlayAnchors.BadgeInset))
                .AsDecor();

            NodeRef countRef = default;
            var children = new List<UiNode> { backing, edge, icon, frame, plus };

            if (withCount)
            {
                var count = Ui.Label($"{name}Count", UiStrings.OverlayCount, OverlayAnchors.BadgeSize, 15,
                        "#EDE6FF",
                        Place.At(half - OverlayAnchors.BadgeSize.X * 0.5f - OverlayAnchors.BadgeInset,
                                 -half + OverlayAnchors.BadgeSize.Y * 0.5f + OverlayAnchors.BadgeInset))
                    .AsDecor();
                children.Add(count);
                countRef = count;
            }

            var cell = Ui.Button(name, UiString.Runtime, new UiVec(size, size), 12, place);
            foreach (var child in children) cell.Children.Add(child);

            parts = new CellParts(backing, edge, icon, countRef, plus);
            return cell;
        }
    }
}

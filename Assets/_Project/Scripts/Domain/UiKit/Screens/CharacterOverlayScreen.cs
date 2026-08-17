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
        // The painted stand, keyed off its green screen by tools/
        // key_green_screen.py's character_overlay kit.
        //
        // Replaced a procedurally baked placeholder that existed only because
        // this art had not been drawn yet. Art/Generated/armour_stand.png and
        // its baker case can go once nothing references them.
        public const string SilhouetteKey = "UI/CharacterOverlay/Processed/armour_stand.png";

        // WHITE. The stand is painted art carrying its own colour, and a tint
        // multiplies -- the #564480 that suited the white procedural placeholder
        // would have muddied this into near-black on a 94% dimmer.
        public const string SilhouetteTint = "#FFFFFF";

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
        public NodeRef BagEmptyHint;

        public NodeRef DetailPlate;
        public NodeRef DetailName;
        public NodeRef DetailBody;
        public NodeRef ActionButton;
        public NodeRef ActionLabel;

        // The two panes and the tabs that swap them. The sheet does not show
        // the bag: a paperdoll and a twenty-cell grid on one surface is what
        // left the slot cells with nowhere to go but on top of the figure.
        public NodeRef CharacterPane;
        public NodeRef InventoryPane;
        public NodeRef CharacterTab;
        public NodeRef InventoryTab;
        public NodeRef CharacterTabMarker;
        public NodeRef InventoryTabMarker;

        // Flat, in SheetStats.All order -- abilities then derived, which is
        // also the order the two columns are laid out in.
        public List<NodeRef> StatNameLabels = new List<NodeRef>();
        public List<NodeRef> StatValueLabels = new List<NodeRef>();

        // The hover box. Moved to the cell under the cursor at runtime, which
        // is why its Place here is only a starting point.
        public NodeRef ComparePlate;
        public NodeRef CompareName;
        public NodeRef CompareBody;

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
                .Coloured(SilhouetteTint)
                .AsDecor();
            screen.Silhouette = silhouette;

            // Decor rather than an exemption. Decor is skipped by the sibling
            // overlap check AND has its raycast cleared, which is exactly what
            // a body-shaped backdrop under eight clickable slots needs — the
            // one thing worse than a slot that looks wrong is a slot that
            // cannot be clicked because the picture behind it took the press.
            var paperdoll = new List<UiNode> { silhouette };
            paperdoll.AddRange(EquipmentSlots.All.Select(screen.BuildSlotCell));
            paperdoll.AddRange(screen.BuildStatRows());

            // The character pane. Holds the figure, its slots and its numbers,
            // and NOTHING carrying an inventory -- which is the whole point of
            // the split.
            //
            // DECOR, and it matters. A full-bleed pane is a sibling of every
            // chrome button, so without this the audit correctly refuses the
            // tree: uGUI draws the later sibling on top, and a pane declared
            // after the tabs would have taken every click meant for them. The
            // tabs would have been dead on arrival and nothing would have
            // looked wrong. Decor clears the raycast and takes the pane out of
            // the sibling overlap check, which is what a pure container wants
            // -- the same treatment the silhouette gets underneath the slots.
            var characterPane = Ui.Panel("CharacterPane", Place.At(0f, 0f),
                    UiSize.Fixed(1920f, 1080f), paperdoll)
                .AsDecor();
            screen.CharacterPane = characterPane;

            var bagCells = Ui.Each(
                Enumerable.Range(0, BagView.CellCount).ToList(),
                (_, i) => screen.BuildBagCell(i));

            var bag = Ui.Grid("BagGrid",
                Place.At(OverlayAnchors.Bag.X, OverlayAnchors.Bag.Y),
                OverlayAnchors.BagColumns,
                OverlayAnchors.BagCell,
                OverlayAnchors.BagGap,
                bagCells);

            // Sits in the middle of where the grid would be. Every cell hides
            // itself when unused, so an empty bag left a rectangle of nothing
            // with a pager underneath it -- which reads as twenty things that
            // failed to load rather than as a bag you have not filled yet.
            var empty = Ui.Label("OverlayBagEmpty", UiStrings.OverlayBagEmpty, new UiVec(720f, 44f), 20,
                    "#7E6E9E", Place.At(OverlayAnchors.Bag.X, OverlayAnchors.Bag.Y))
                .AsDecor()
                .Inactive();
            screen.BagEmptyHint = empty;

            // The inventory pane, with its own pager: the page controls belong
            // to the grid they page, and left in the shared chrome they sat
            // under a character sheet that has nothing to page.
            var inventoryPane = Ui.Panel("InventoryPane", Place.At(0f, 0f),
                    UiSize.Fixed(1920f, 1080f),
                    new List<UiNode> { bag, empty }.Concat(BuildPager(screen)))
                .AsDecor()
                .Inactive();
            screen.InventoryPane = inventoryPane;

            // PANES FIRST, then chrome, then the hover box.
            //
            // Declaration order is painter's order: the panes are the surface,
            // the chrome sits on it, and the compare box floats over both --
            // it follows the cursor across the bag grid, so anything drawn
            // after it would cut a hole in it.
            var content = Ui.Panel("CharacterOverlayContent", Place.At(0f, 0f), UiSize.Fixed(1920f, 1080f),
                new List<UiNode> { characterPane, inventoryPane }
                    .Concat(BuildChrome(screen))
                    .Append(BuildCompareBox(screen)));

            // 94%, and tinted into the palette rather than pure black.
            //
            // At the 85% this started on, the hub's own headings came straight
            // through -- "DIVINE PRINCIPALITY" read louder than the character
            // name on top of it, because a large light glyph survives a dim that
            // a painted building does not. The hub is still faintly there, which
            // is the whole point of an overlay; it just stops competing.
            screen.Root = Ui.Modal("CharacterOverlayPanel", "#0A0614F0", content).Inactive();
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

            // Plate-local coordinates: these are its children, so a screen-space
            // y would put them a third of a screen below the plate meant to
            // contain them.
            var detailName = Ui.Label("OverlayDetailName", UiString.Runtime, new UiVec(1100f, 40f), 24,
                "#EDE6FF", Place.At(0f, 44f));
            var detailBody = Ui.Label("OverlayDetailBody", UiString.Runtime, new UiVec(1100f, 72f), 17,
                "#B8A8D9", Place.At(0f, -18f));
            screen.DetailName = detailName;
            screen.DetailBody = detailBody;

            // Hidden until something is picked. A 1200x170 coloured slab with
            // nothing written on it is the biggest object on the overlay, and
            // nothing-selected is the state it OPENS in -- so the first thing
            // the screen showed was its own empty furniture.
            var plate = Ui.Panel("OverlayDetailPlate",
                    Place.At(OverlayAnchors.DetailPlate.X, OverlayAnchors.DetailPlate.Y),
                    UiSize.Fixed(OverlayAnchors.DetailPlateSize.X, OverlayAnchors.DetailPlateSize.Y),
                    detailName, detailBody)
                .Coloured("#2C1C42E0")
                .AsDecor()
                .Inactive();
            screen.DetailPlate = plate;

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

            // The tabs. A marker strip under the live one rather than a colour
            // swap on the button itself: the same reasoning the cell frames
            // follow, where one layer says one thing.
            var characterTab = Ui.Button("OverlayCharacterTab", UiStrings.SheetTabCharacter,
                new UiVec(OverlayAnchors.TabSize.X, OverlayAnchors.TabSize.Y), 18,
                Place.At(-OverlayAnchors.TabGap, OverlayAnchors.TabRow.Y));
            var inventoryTab = Ui.Button("OverlayInventoryTab", UiStrings.SheetTabInventory,
                new UiVec(OverlayAnchors.TabSize.X, OverlayAnchors.TabSize.Y), 18,
                Place.At(OverlayAnchors.TabGap, OverlayAnchors.TabRow.Y));

            var characterMarker = Ui.Solid("OverlayCharacterTabMarker", "#F2DB9E",
                    Place.Frac(new UiVec(0f, 0f), new UiVec(1f, 0f), top: -3f), UiSize.Fill)
                .AsDecor();
            var inventoryMarker = Ui.Solid("OverlayInventoryTabMarker", "#F2DB9E00",
                    Place.Frac(new UiVec(0f, 0f), new UiVec(1f, 0f), top: -3f), UiSize.Fill)
                .AsDecor();
            characterTab.Children.Add(characterMarker);
            inventoryTab.Children.Add(inventoryMarker);

            screen.CharacterTab = characterTab;
            screen.InventoryTab = inventoryTab;
            screen.CharacterTabMarker = characterMarker;
            screen.InventoryTabMarker = inventoryMarker;

            // The plate is declared before the action button so the button
            // draws over it where they meet.
            return new[]
            {
                name, prevCharacter, nextCharacter,
                characterTab, inventoryTab,
                plate, action, close,
            };
        }

        // The bag's own page controls, which live with the bag rather than in
        // the shared chrome.
        private static IEnumerable<UiNode> BuildPager(CharacterOverlayScreen screen)
        {
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

            return new[] { page, prevPage, nextPage };
        }

        // Thirteen rows in two columns, each a name and a value.
        //
        // Two labels per row rather than one padded string: the font is
        // proportional, so aligning a value column with spaces is a guess that
        // is wrong at every other number.
        private IEnumerable<UiNode> BuildStatRows()
        {
            var nodes = new List<UiNode>();

            for (int i = 0; i < SheetStats.Abilities.Length; i++)
            {
                nodes.AddRange(BuildStatRow(SheetStats.Abilities[i],
                    OverlayAnchors.StatColumnAbilities, i));
            }

            for (int i = 0; i < SheetStats.Derived.Length; i++)
            {
                nodes.AddRange(BuildStatRow(SheetStats.Derived[i],
                    OverlayAnchors.StatColumnDerived, i));
            }

            return nodes;
        }

        private IEnumerable<UiNode> BuildStatRow(SheetStat stat, UiVec column, int row)
        {
            float y = column.Y - row * OverlayAnchors.StatRowPitch;
            float half = OverlayAnchors.StatRowSize.X * 0.5f;

            var name = Ui.Label($"Stat{stat}Name", SheetStats.LabelFor(stat),
                    new UiVec(OverlayAnchors.StatRowSize.X * 0.6f, OverlayAnchors.StatRowSize.Y), 19,
                    "#B8A8D9",
                    Place.At(column.X - half * 0.4f, y))
                .AsDecor();

            var value = Ui.Label($"Stat{stat}Value", UiStrings.SheetStatValue,
                    new UiVec(OverlayAnchors.StatRowSize.X * 0.35f, OverlayAnchors.StatRowSize.Y), 19,
                    "#EDE6FF",
                    Place.At(column.X + half * 0.6f, y))
                .AsDecor();

            StatNameLabels.Add(name);
            StatValueLabels.Add(value);

            return new[] { name, value };
        }

        // The hover box. Decor and inactive: it never takes a click, and it
        // only exists while the cursor is on a cell.
        private static UiNode BuildCompareBox(CharacterOverlayScreen screen)
        {
            var name = Ui.Label("OverlayCompareName", UiString.Runtime,
                    new UiVec(OverlayAnchors.CompareSize.X - 28f, 34f), 20, "#EDE6FF",
                    Place.At(0f, OverlayAnchors.CompareSize.Y * 0.5f - 30f))
                .AsDecor();
            var body = Ui.Label("OverlayCompareBody", UiString.Runtime,
                    new UiVec(OverlayAnchors.CompareSize.X - 28f, OverlayAnchors.CompareSize.Y - 76f),
                    16, "#B8A8D9",
                    Place.At(0f, -18f))
                .AsDecor();

            var plate = Ui.Panel("OverlayComparePlate", Place.At(0f, 0f),
                    UiSize.Fixed(OverlayAnchors.CompareSize.X, OverlayAnchors.CompareSize.Y),
                    name, body)
                .Coloured("#150C24F2")
                .AsDecor()
                .Inactive();

            screen.ComparePlate = plate;
            screen.CompareName = name;
            screen.CompareBody = body;
            return plate;
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
            // FRAME BEFORE ICON. Declaration order is painter's order, and the
            // frame sprite is a filled panel rather than a hollow ring -- put
            // it last and it covers the very art the cell exists to show. The
            // sprites were bound correctly the whole time and simply painted
            // underneath it.
            var children = new List<UiNode> { backing, edge, frame, icon, plus };

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

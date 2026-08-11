using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.Domain.UiKit
{
    // Where the character overlay's parts sit.
    //
    // Same idiom as HubAnchors: one Domain static that the screen tree and its
    // tests both read, so a coordinate has exactly one home. The EditMode audit
    // is the tuning harness for every number here -- a change is a 1.5-second
    // loop, not a scene rebuild.
    public static class OverlayAnchors
    {
        // ---- the paperdoll -----------------------------------------------------

        // The silhouette is an armour stand, shoulders to boots. Decor, so it
        // can neither eat a cell's click nor collide with the cells laid over
        // it -- the two things that would otherwise make a body-shaped
        // background a liability.
        public static readonly UiVec Silhouette = new UiVec(-480f, 20f);
        public static readonly UiVec SilhouetteSize = new UiVec(520f, 780f);

        public const float SlotCell = 96f;

        // Read down the body, then out to the hands. Nearest vertical pitch is
        // 110px against a 96px cell, so slot-vs-slot clears the overlap audit
        // by construction rather than by exemption.
        public static UiVec PositionFor(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Head: return new UiVec(-480f, 345f);
                case EquipmentSlot.Necklace: return new UiVec(-480f, 235f);
                case EquipmentSlot.Torso: return new UiVec(-480f, 110f);
                case EquipmentSlot.Gloves: return new UiVec(-655f, -30f);
                case EquipmentSlot.Legs: return new UiVec(-480f, -130f);
                case EquipmentSlot.Shoes: return new UiVec(-480f, -330f);

                // The hands flank the body rather than sitting on it: an arm is
                // too thin a target at this size, and a weapon held out to the
                // side reads as held.
                case EquipmentSlot.Weapon1: return new UiVec(-790f, 110f);
                default: return new UiVec(-170f, 110f);
            }
        }

        // ---- the bag -----------------------------------------------------------

        public static readonly UiVec Bag = new UiVec(430f, 70f);
        public const int BagColumns = 5;
        public static readonly UiVec BagCell = new UiVec(132f, 132f);
        public static readonly UiVec BagGap = new UiVec(14f, 14f);

        // Below the grid, clear of its last row.
        public static readonly UiVec Pager = new UiVec(430f, -270f);

        // ---- chrome -------------------------------------------------------------

        public static readonly UiVec CharacterName = new UiVec(0f, 452f);
        public const float CharacterArrowX = 270f;

        public static readonly UiVec DetailPlate = new UiVec(0f, -420f);
        public static readonly UiVec DetailPlateSize = new UiVec(1200f, 170f);

        public static readonly UiVec ActionButton = new UiVec(700f, -420f);
        public static readonly UiVec ActionButtonSize = new UiVec(300f, 66f);

        // Right-hand corner, mirroring the hub's own corner button on the left
        // -- so it never lands on top of MainMenuButton showing through the
        // dimmer behind it.
        public static readonly UiVec CloseButton = new UiVec(830f, 480f);

        // ---- one cell -----------------------------------------------------------

        // A cell is layered, and each layer says ONE thing: the backing says
        // "this is a slot", the rarity edge says how good the item is, the icon
        // says what it is, the badges say how many and how honed.
        //
        // v1 made Image.color carry both the frame tint and the empty/filled
        // state, with the two constants hand-synced between the builder and the
        // controller. Nothing here does that.
        public const string CellBacking = "#241736";
        public const string CellBackingEmpty = "#1A102980";
        public const float RarityEdgeHeight = 4f;

        // The icon sits inside the cell with room for the frame to read around
        // it. Bag cells are bigger than slot cells, so the inset differs.
        public const float BagIconInset = 18f;
        public const float SlotIconInset = 12f;

        public const float BadgeInset = 6f;
        public static readonly UiVec BadgeSize = new UiVec(52f, 26f);
    }
}

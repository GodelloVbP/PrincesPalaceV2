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
        // MEASURED off the keyed art, not estimated off the image.
        //
        // The painted stand fills 81.7% of its 1024x1536 canvas (content box
        // 883x1255 at y 133..1387), so the FIGURE inside this node is 596px
        // tall centred at y=81 with a half-height of 298, and 448px wide
        // centred on the node with a half-width of 224. Every number below is
        // that mapping applied to a landmark read out of the alpha channel:
        //
        //   +0.93  neck post          -0.12  tunic hem
        //   +0.52  arms leave torso   -0.36  legs separate
        //   +0.04  hands end          -1.00  leg tips
        //
        // Three previous passes were tuned by eye off a pasted screenshot and
        // were wrong every time -- most visibly the Gloves slot, which sat at
        // nx -0.67 while the hands actually end at -0.93 and so hung in empty
        // space beside the arm. tools measure_stand.py is how this was read.
        public static readonly UiVec Silhouette = new UiVec(-480f, 78f);
        public static readonly UiVec SilhouetteSize = new UiVec(520f, 730f);

        public const float SlotCell = 96f;

        // Read down the body, then out to the hands, then out again to the
        // weapons. The centre column is spaced at 118-153px against a 96px
        // cell, so slot-vs-slot clears the overlap audit by construction.
        public static UiVec PositionFor(EquipmentSlot slot)
        {
            switch (slot)
            {
                case EquipmentSlot.Head: return new UiVec(-480f, 358f);
                case EquipmentSlot.Necklace: return new UiVec(-480f, 240f);
                case EquipmentSlot.Torso: return new UiVec(-480f, 110f);
                case EquipmentSlot.Legs: return new UiVec(-480f, -40f);
                case EquipmentSlot.Shoes: return new UiVec(-480f, -193f);

                // ON THE HANDS. The A-pose puts them 208px out from the body
                // centre at the height the arms end -- which is why this is no
                // longer tucked in beside the hip.
                case EquipmentSlot.Gloves: return new UiVec(-688f, 96f);

                // Outside the hands again, so a weapon reads as HELD OUT
                // rather than as a second glove. 130px clear of the Gloves
                // cell, which is 34px of gap at a 96px cell.
                case EquipmentSlot.Weapon1: return new UiVec(-818f, 110f);
                default: return new UiVec(-142f, 110f);
            }
        }

        // The lowest edge anything in the paperdoll reaches. Read by the test
        // that keeps it off the detail plate, so the two cannot drift.
        public static float PaperdollBottom =>
            System.Math.Min(Silhouette.Y - SilhouetteSize.Y * 0.5f,
                            PositionFor(EquipmentSlot.Shoes).Y - SlotCell * 0.5f);

        public static float DetailPlateTop => DetailPlate.Y + DetailPlateSize.Y * 0.5f;

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

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

        // ---- where the cells go, and why they left the body --------------------
        //
        // TWO COLUMNS FLANKING THE FIGURE, not a column down its front.
        //
        // The previous arrangement put every armour cell on the silhouette's
        // own centre line and the hand/weapon cells on the arms, each position
        // measured off a landmark in the painted stand. The measurements were
        // right and the idea was wrong: five opaque 96px cells stacked down the
        // torso cover the figure they are supposed to be describing, so the
        // paperdoll reads as a stack of boxes with a purple shape behind it
        // rather than as a body wearing things.
        //
        // This is the same defect the armour-stand art brief already named --
        // "no internal detail competing with the slot cells" -- arriving from
        // the other side. The art was corrected to stay quiet; the cells then
        // took the space anyway.
        //
        // So the figure gets its own space back and the cells take the gutters
        // either side. Reading order is top-down in each column: worn armour on
        // the left, hands and held things on the right.
        //
        // Geometry, all of it derived rather than eyeballed:
        //   silhouette spans x -740..-220 (centre -480, width 520)
        //   left column   x -820  -> cell spans -868..-772, 32px clear of the figure
        //   right column  x -140  -> cell spans -188..-92,  32px clear of the figure
        //   rows 300/160/20/-120 -> 140px pitch against a 96px cell, 44px of gap
        //
        // The 32px gutters and 44px row gaps are what make slot-vs-slot and
        // slot-vs-silhouette clear the overlap audit by construction rather
        // than by exemption.
        public const float SlotColumnLeftX = -820f;
        public const float SlotColumnRightX = -140f;

        private const float SlotRow0 = 300f;
        private const float SlotRow1 = 160f;
        private const float SlotRow2 = 20f;
        private const float SlotRow3 = -120f;

        public static UiVec PositionFor(EquipmentSlot slot)
        {
            switch (slot)
            {
                // Worn armour, head to legs.
                case EquipmentSlot.Head: return new UiVec(SlotColumnLeftX, SlotRow0);
                case EquipmentSlot.Necklace: return new UiVec(SlotColumnLeftX, SlotRow1);
                case EquipmentSlot.Torso: return new UiVec(SlotColumnLeftX, SlotRow2);
                case EquipmentSlot.Legs: return new UiVec(SlotColumnLeftX, SlotRow3);

                // Hands and what they hold. Shoes join this column rather than
                // extending the left one to five: a fifth row would push the
                // paperdoll down onto the detail plate, and an even 4+4 is
                // what keeps both columns inside the figure's own height.
                case EquipmentSlot.Gloves: return new UiVec(SlotColumnRightX, SlotRow0);
                case EquipmentSlot.Weapon1: return new UiVec(SlotColumnRightX, SlotRow1);
                case EquipmentSlot.Weapon2: return new UiVec(SlotColumnRightX, SlotRow2);
                case EquipmentSlot.Shoes: return new UiVec(SlotColumnRightX, SlotRow3);

                default: return new UiVec(SlotColumnRightX, SlotRow3);
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

        // ---- the stat block ----------------------------------------------------

        // Sits where the bag used to, on the character pane. The two panes are
        // never visible together, so the same real estate carries the bag grid
        // on one and the numbers on the other.
        //
        // Two columns: the six a player spends points on, then what those turn
        // into once gear and talents are folded in. 46px pitch against a 30px
        // row leaves 16px of gap, and the taller column's seven rows end at
        // y=24 -- clear of the pager row and well clear of the detail plate.
        public static readonly UiVec StatColumnAbilities = new UiVec(300f, 300f);
        public static readonly UiVec StatColumnDerived = new UiVec(660f, 300f);
        public const float StatRowPitch = 46f;
        public static readonly UiVec StatRowSize = new UiVec(300f, 30f);

        // The name sits left in the row, the value right, as two labels rather
        // than one padded string: a proportional font makes column alignment by
        // spaces a guess that is wrong at every other value.

        // ---- the two panes -----------------------------------------------------

        // Tabs above both panes. The sheet showing a paperdoll AND a bag grid
        // is what crowded the slot cells in the first place, so the bag moved
        // behind a tab rather than being deleted -- deleting it would have left
        // no way to equip anything at all.
        public static readonly UiVec TabRow = new UiVec(0f, 372f);
        public const float TabGap = 200f;
        public static readonly UiVec TabSize = new UiVec(190f, 52f);

        // ---- the compare box ---------------------------------------------------

        // Appears NEXT TO the hovered cell, not in a fixed corner: the whole
        // point is reading it against the thing under the cursor without
        // looking away. Offset to the right of the cell, nudged up so the
        // cursor never covers the first line.
        public static readonly UiVec CompareSize = new UiVec(420f, 240f);
        public const float CompareGap = 18f;

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

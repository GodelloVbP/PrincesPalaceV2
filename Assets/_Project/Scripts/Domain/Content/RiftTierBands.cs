namespace PrincesPalace.Domain.Content
{
    // What a ROLL looks like to the player -- the RiftTier twin of RarityBands.
    //
    // Deliberately a SEPARATE table rather than RarityBands grown from six
    // entries to a shared size: Rarity and RiftTier are two independent axes
    // per item (see RiftTier.cs's own header -- a common-tier sword can roll
    // Convergent and a Mythic one can roll Ordinary), and RarityColors.Cache is
    // a `Color[6]` indexed by `(int)Rarity` specifically. Resizing that cache
    // to 4 to fit RiftTier, or indexing it with a RiftTier's ordinal, would
    // silently read one axis's colour through the other axis's table the
    // moment the two enums' ordinals lined up by coincidence -- exactly the
    // "conflating two different enums' colour semantics in one hardcoded-size
    // cache" bug a same-sized sibling avoids by construction. Mirrors
    // RarityBands' own shape (hex strings, engine-free, parsed once by the
    // Unity-side RiftTierColors) rather than reinventing it.
    public static class RiftTierBands
    {
        // Ordinary never renders -- see RiftTierColors.ShouldGlow -- so its
        // hex is never actually drawn. It still gets a real answer rather than
        // null/empty, the same "no gap in the table" posture RarityBands takes
        // for every band.
        public static string HexColor(RiftTier tier)
        {
            switch (tier)
            {
                case RiftTier.RiftTouched: return "#4FE0D6";
                case RiftTier.RiftForged: return "#B15CFF";
                case RiftTier.Convergent: return "#FF4FC3";
                default: return "#FFFFFF";
            }
        }

        // "Rift-Touched", not "RiftTouched" -- the enum member name is a C#
        // identifier, not a word a player should read verbatim, the same
        // reason RarityBands.DisplayName exists instead of every caller
        // calling ToString() on the enum directly.
        public static string DisplayName(RiftTier tier)
        {
            switch (tier)
            {
                case RiftTier.RiftTouched: return "Rift-Touched";
                case RiftTier.RiftForged: return "Rift-Forged";
                case RiftTier.Convergent: return "Convergent";
                default: return "Ordinary";
            }
        }
    }
}

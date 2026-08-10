namespace PrincesPalace.Domain.Content
{
    // How a generated item's name is put together, in one place for both
    // generators.
    //
    // Armour and weapons used to compose names independently, and they had
    // drifted: a weapon read "Nimble Keen Sword +4" (modifier, tier
    // adjective, noun) while a coif read "Leather Coif +10" — no adjective at
    // all, so the only thing distinguishing the best head slot in the game
    // from the worst was a number. Both go through here now.
    //
    // The ordering is deliberate and is the reason this takes three parts
    // rather than a joined string: the MODIFIER leads (it says who the item
    // is for), the TIER ADJECTIVE sits in the middle (it says how good), and
    // the NOUN ends it. Armour simply has no modifier.
    //
    // Plus is appended at DISPLAY time and defaults to absent, because plus
    // lives on the item instance rather than on the definition — a baked
    // DisplayName that already said "+3" would be wrong for every other copy
    // of the same item.
    public static class ItemNaming
    {
        // "Nimble Keen Sword +4", "Hardened Leather Coif", "Leather Coif".
        // Any part may be null or blank and is simply skipped, so a family
        // with no adjectives and a piece with no modifier both come out
        // reading naturally rather than with a double space in them.
        public static string Compose(string prefix, string adjective, string baseName, int plus = 0)
        {
            string name = Join(Join(Trim(prefix), Trim(adjective)), Trim(baseName));
            return plus > 0 ? $"{name} +{plus}" : name;
        }

        // The two-part case: no modifier, which is every piece of armour.
        public static string Compose(string adjective, string baseName, int plus = 0)
        {
            return Compose(null, adjective, baseName, plus);
        }

        // Appends the instance's plus to an already-composed name. Kept
        // separate so a UI holding a definition's DisplayName does not have
        // to take it apart to add one number to the end.
        public static string WithPlus(string name, int plus)
        {
            string trimmed = Trim(name);
            return plus > 0 ? $"{trimmed} +{plus}" : trimmed;
        }

        // The adjective a tier wears, from a lowest-first ladder.
        //
        // The last entry covers every tier past the end of a short list
        // rather than the list having to be exactly maxTier + 1 long — so a
        // set can author three adjectives and still generate eleven items,
        // and raising maxTier later does not immediately break naming.
        // An empty ladder falls back, so "no adjectives authored yet" reads
        // as unremarkable rather than as a hole in the middle of a name.
        public static string AdjectiveAt(string[] adjectives, int tier, string fallback)
        {
            if (adjectives == null || adjectives.Length == 0)
            {
                return fallback;
            }

            int index = tier < 0 ? 0 : tier;
            if (index > adjectives.Length - 1)
            {
                index = adjectives.Length - 1;
            }

            string chosen = adjectives[index];
            return string.IsNullOrWhiteSpace(chosen) ? fallback : chosen.Trim();
        }

        private static string Trim(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "" : value.Trim();
        }

        private static string Join(string left, string right)
        {
            if (left.Length == 0)
            {
                return right;
            }

            return right.Length == 0 ? left : $"{left} {right}";
        }
    }
}

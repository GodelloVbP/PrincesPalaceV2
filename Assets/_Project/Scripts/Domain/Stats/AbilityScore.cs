namespace PrincesPalace.Domain.Stats
{
    // Classic D&D-style ability scores. Adding a new one means: add it here,
    // add a matching field to AbilityScoreBlock, and extend its indexer —
    // the compiler and AbilityScoreBlockTests.Indexer_CoversEveryAbilityScore
    // will point at anything left unwired.
    public enum AbilityScore
    {
        Strength,
        Dexterity,
        Constitution,
        Wisdom,
        Intelligence,
        Charisma
    }

    public static class AbilityScores
    {
        // The six, in authored order, as an array rather than an
        // Enum.GetValues() call at every use site. Scaling walks all six on
        // every damage calculation, and Enum.GetValues boxes and allocates
        // each time — this is the one place in Domain where the difference is
        // worth caring about, because it sits inside combat's inner loop.
        public static readonly AbilityScore[] All =
        {
            AbilityScore.Strength,
            AbilityScore.Dexterity,
            AbilityScore.Constitution,
            AbilityScore.Wisdom,
            AbilityScore.Intelligence,
            AbilityScore.Charisma,
        };

        // The three-letter form every screen already prints by hand
        // (AbilityScoreBlock.ToString, ItemChoiceController.Summarise). Here
        // so a scaling line and a bonus line cannot disagree about what
        // Constitution is abbreviated to.
        public static string ShortName(AbilityScore score)
        {
            switch (score)
            {
                case AbilityScore.Strength: return "STR";
                case AbilityScore.Dexterity: return "DEX";
                case AbilityScore.Constitution: return "CON";
                case AbilityScore.Wisdom: return "WIS";
                case AbilityScore.Intelligence: return "INT";
                case AbilityScore.Charisma: return "CHA";
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(score), score, "AbilityScores has no short name for this score.");
            }
        }

        public static bool TryParse(string raw, out AbilityScore score)
        {
            score = AbilityScore.Strength;
            if (string.IsNullOrWhiteSpace(raw))
            {
                return false;
            }

            string trimmed = raw.Trim();
            foreach (var candidate in All)
            {
                if (string.Equals(trimmed, candidate.ToString(), System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(trimmed, ShortName(candidate), System.StringComparison.OrdinalIgnoreCase))
                {
                    score = candidate;
                    return true;
                }
            }

            return false;
        }
    }
}

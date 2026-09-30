namespace PrincesPalace.Domain.UiKit
{
    // The compare column's wording (plan C16, spec 3.1).
    public static class LoadoutCompare
    {
        // "280 → 338 (+58)": what the stat is, what it would be, and the
        // change -- in that order, which is the order a change is read in.
        public static string DeltaText(string now, string next, int delta) =>
            UiStrings.CompareDelta.Format(now, next, Signed(delta));

        public static string Signed(int delta) => delta > 0 ? "+" + delta : delta.ToString();
    }
}

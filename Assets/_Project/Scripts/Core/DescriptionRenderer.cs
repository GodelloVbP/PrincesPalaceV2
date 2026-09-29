using PrincesPalace.Domain.Content;

namespace PrincesPalace
{
    // The runtime half of DescriptionMarkup: expands a string's tokens for
    // what TmpAssetResolver can actually supply on this machine, so a missing
    // bold face or sprite asset degrades to plain words and never to raw
    // markup on screen.
    public static class DescriptionRenderer
    {
        public static DescriptionRenderCaps CurrentCaps() => new DescriptionRenderCaps(
            bold: TmpAssetResolver.Font(DescriptionMarkup.BoldFontName) != null,
            icons: TmpAssetResolver.IconSprites() != null);

        // Text with no token skips the capability lookups entirely: most
        // SetContent strings are names and dialogue.
        public static string Render(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? string.Empty;
            if (text.IndexOf("**", System.StringComparison.Ordinal) < 0 && text.IndexOf('{') < 0) return text;
            return DescriptionMarkup.Expand(text, CurrentCaps());
        }

        // For a caller that already knows what it can draw, and for tests
        // that stand in a missing asset.
        public static string Render(string text, DescriptionRenderCaps caps) =>
            DescriptionMarkup.Expand(text, caps);
    }
}

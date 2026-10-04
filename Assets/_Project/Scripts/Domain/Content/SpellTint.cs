namespace PrincesPalace.Domain.Content
{
    // A LAYER'S TINT, `#RRGGBB`, parsed without allocating (it is read on the
    // frame path). Blank is "none": the layer draws as painted. The tint
    // multiplies the sheet, so it can only darken or cool a drawing: the mud
    // sheet's gold turns brown under #6B4A30, but a grey sheet cannot be made
    // gold by it.
    public static class SpellTint
    {
        public static bool TryParse(string text, out float r, out float g, out float b)
        {
            r = g = b = 1f;
            if (string.IsNullOrEmpty(text) || text.Length != 7 || text[0] != '#') return false;

            int red = Pair(text, 1), green = Pair(text, 3), blue = Pair(text, 5);
            if (red < 0 || green < 0 || blue < 0) return false;

            r = red / 255f;
            g = green / 255f;
            b = blue / 255f;
            return true;
        }

        private static int Pair(string text, int at)
        {
            int high = Nibble(text[at]), low = Nibble(text[at + 1]);
            return high < 0 || low < 0 ? -1 : high * 16 + low;
        }

        private static int Nibble(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return -1;
        }
    }
}

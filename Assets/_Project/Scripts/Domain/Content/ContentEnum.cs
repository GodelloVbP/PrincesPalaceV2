using System;

namespace PrincesPalace.Domain.Content
{
    // The one place an authored enum string becomes a value. Matches member
    // NAMES only, case-insensitively: Enum.TryParse also takes "2" (an
    // ordinal nobody authored, silently renumbered the day a member is
    // inserted) and "Fire, Ice" (a flag list no enum here is). Every
    // resolver that reads an enum from JSON goes through this.
    public static class ContentEnum
    {
        public static bool TryParseName<TEnum>(string raw, out TEnum value) where TEnum : struct, Enum
        {
            value = default;
            string trimmed = (raw ?? "").Trim();
            if (trimmed.Length == 0) return false;

            foreach (string name in Enum.GetNames(typeof(TEnum)))
            {
                if (string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase))
                {
                    value = (TEnum)Enum.Parse(typeof(TEnum), name);
                    return true;
                }
            }

            return false;
        }
    }
}

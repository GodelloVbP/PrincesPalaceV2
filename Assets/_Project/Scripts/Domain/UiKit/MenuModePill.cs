using System;

namespace PrincesPalace.Domain.UiKit
{
    // The lintel's mode pill (plan C10): what the whole menu may change, said
    // once for every tab. It states MenuPermissions' fight row in a word, so
    // the Party pane's own status pill can go.
    public static class MenuModePill
    {
        public static UiString For(MenuContext context)
        {
            switch (context)
            {
                case MenuContext.Camp: return UiStrings.SystemModeCamp;
                case MenuContext.Descent: return UiStrings.SystemModeDescent;
                case MenuContext.Fight: return UiStrings.SystemModeFight;
                default: throw new ArgumentOutOfRangeException(nameof(context), context, null);
            }
        }
    }
}

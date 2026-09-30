using System.Collections.Generic;

namespace PrincesPalace.Domain.UiKit
{
    // The map's line about a character wanting looking at (plan C20), as text:
    // the reasons that hold, then how to open the menu on them. Only reasons
    // that hold are named, in the pips' own order.
    public static class AttentionHint
    {
        public const string Separator = " · ";

        // "Points to spend · Books to place"; empty when nothing waits.
        public static string Named(AttentionReasons reasons)
        {
            var named = new List<string>();
            if ((reasons & AttentionReasons.PointsToSpend) != 0) named.Add(UiStrings.MapAttentionPoints.Format());
            if ((reasons & AttentionReasons.RewardsWaiting) != 0) named.Add(UiStrings.MapAttentionRewards.Format());
            if ((reasons & AttentionReasons.BooksToPlace) != 0) named.Add(UiStrings.MapAttentionBooks.Format());

            return string.Join(Separator, named);
        }

        // The key that opens the menu on CHARACTER, for the device in hand.
        public static string Key(PromptDevice device) => PromptGlyphs.For(device, PromptButton.Character).Text;

        // The whole line; empty when nothing waits, and the map hides its
        // label then.
        public static string Text(AttentionReasons reasons, PromptDevice device) =>
            reasons == AttentionReasons.None ? "" : UiStrings.MapAttention.Format(Named(reasons), Key(device));
    }
}

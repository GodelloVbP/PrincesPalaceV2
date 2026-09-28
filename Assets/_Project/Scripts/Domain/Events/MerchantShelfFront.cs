using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Events
{
    // WHO IS SELLING, read off an event's merchant shelf for the shop screen
    // (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 3.4; docs/EVENTS.md "Merchant
    // shelves"). The room shop has none of this: the screen shows SHOP and
    // its relic panel. A merchant shelf sells no relics, so that panel's slot
    // shows the keeper instead of standing empty.
    public sealed class MerchantShelfFront
    {
        // The screen's title: the shelf's own, else the keeper's name, else
        // "" (the caller's SHOP).
        public readonly string Title;

        // The keeper, an event speaker. Empty name: nobody named.
        public readonly string KeeperName;
        public readonly string KeeperEpithet;
        public readonly string KeeperBustFolder;

        // The keeper's first declared face (the one a line naming none takes).
        public readonly string KeeperExpression;

        // Whether this shelf stocks fakes at all (fakeShare above 0), and
        // whether they are marked.
        public readonly bool HasFakes;
        public readonly bool Revealed;

        public MerchantShelfFront(string title, ResolvedEventSpeaker keeper, bool hasFakes, bool revealed)
        {
            KeeperName = keeper?.Name ?? "";
            KeeperEpithet = keeper?.Epithet ?? "";
            KeeperBustFolder = keeper?.BustPath ?? "";
            KeeperExpression = keeper != null && keeper.Expressions != null && keeper.Expressions.Length > 0
                ? keeper.Expressions[0]
                : DialogueBust.Neutral;
            Title = string.IsNullOrWhiteSpace(title) ? KeeperName : title.Trim();
            HasFakes = hasFakes;
            Revealed = revealed;
        }
    }
}

namespace PrincesPalace
{
    // The event room's half of the map: the body of the partial hook OpenEvent
    // calls, shaped like OpenShop, and the hand-over to an event's shelf. Closing is the panel's own business (a
    // choice that ends the event, or its Leave), and it hands control back
    // through Finished, which repaints the map with the room cleared.
    public partial class MapController
    {
        partial void ShowEventPanel()
        {
            if (eventPanel == null) return;

            eventPanel.Finished = Refresh;
            eventPanel.ShelfRequested = OpenEventShelf;
            eventPanel.Open();
        }

        // An event's merchant shelf (plan 3.4), on the shop's own screen.
        // Leaving it comes back here, to the event -- never to the map, and
        // never with the room cleared: the event's own Leave does that.
        private void OpenEventShelf()
        {
            if (shop == null) return;

            shop.Finished = OpenEvent;
            shop.Open();
        }
    }
}

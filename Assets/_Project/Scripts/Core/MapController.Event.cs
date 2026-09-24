namespace PrincesPalace
{
    // The event room's half of the map: the body of the partial hook OpenEvent
    // calls, shaped like OpenShop. Closing is the panel's own business (a
    // choice that ends the event, or its Leave), and it hands control back
    // through Finished, which repaints the map with the room cleared.
    public partial class MapController
    {
        partial void ShowEventPanel()
        {
            if (eventPanel == null) return;

            eventPanel.Finished = Refresh;
            eventPanel.Open();
        }
    }
}

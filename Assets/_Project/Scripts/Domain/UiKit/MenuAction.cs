namespace PrincesPalace.Domain.UiKit
{
    // Every state change the system menu can make. A new one is a row here and
    // a row in MenuPermissions; nothing else decides whether it is allowed.
    public enum MenuAction
    {
        EquipGear,
        UnequipGear,
        PlaceBook,
        ReplaceBook,
        RemoveBook,
        SpendAttribute,
        RefundAttribute,

        // Placing, benching or swapping a roster member (roster <-> seat).
        SwapParty,

        // Moving a seated member to another seat.
        RepositionParty,
        CollectReward,
        CycleTitle,
        ChangeOption,
        ExitAction,
    }
}

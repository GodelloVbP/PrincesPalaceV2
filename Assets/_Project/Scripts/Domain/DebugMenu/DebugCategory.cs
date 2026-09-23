namespace PrincesPalace.Domain.DebugMenu
{
    // The debug menu's category rail, one value per tab, in rail order.
    //
    // Two SHAPES of tab behind one list. The first six are CONTENT: every
    // row is a thing in the catalogue, granted at the grant bar's plus and
    // quantity. Resources and Tools are ACTIONS: every row is one button's
    // worth of verb ("+1000 gold", "Heal party"), and plus/quantity mean
    // nothing to them. They share the list, the pager and the nav rather
    // than growing a second panel because a row is a row -- what differs is
    // what a click does, and the controller already branches on category to
    // decide that.
    public enum DebugCategory
    {
        Weapons,
        Equipment,
        Consumables,
        Sets,
        SpellBooks,
        Relics,
        Resources,
        Tools,
    }
}

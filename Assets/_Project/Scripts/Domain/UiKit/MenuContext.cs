namespace PrincesPalace.Domain.UiKit
{
    // Which part of the game a system menu instance sits in. One per menu,
    // fixed by the scene that carries it (ScreenRegistry.WireSystemMenu), and
    // the only input MenuPermissions needs besides the action.
    //
    // Camp is the Hub, Descent is the Map with its Shop and Event, Fight is
    // the Fight scene. Not RunManager.HasRun: a run exists from the relic
    // draft, which happens in the Hub, so HasRun cannot tell camp from descent.
    public enum MenuContext
    {
        Camp,
        Descent,
        Fight,
    }
}

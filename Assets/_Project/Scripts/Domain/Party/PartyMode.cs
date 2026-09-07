namespace PrincesPalace.Domain.Party
{
    // Camp: between runs, at the permanent store's side of the game --
    // swap freely. Run: mid-descent -- reposition only, since nobody is
    // recruited or benched partway through a dungeon. ViewOnly: inside a
    // fight, where the formation committed at the door is fixed for its
    // duration and every command reports FormationFixedInFight rather than
    // silently doing nothing.
    public enum PartyMode
    {
        Camp,
        Run,
        ViewOnly
    }
}

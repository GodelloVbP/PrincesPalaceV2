namespace PrincesPalace.Domain.Combat.Session
{
    // Whether the death a damage figure causes is credited to whoever dealt it.
    //
    // Named at every DealDamage call site and never defaulted, because what
    // this replaces was an omission rather than a decision: five call sites
    // each typed `_killedThisAction = true; RecordKill(...)` by hand after
    // their own damage call, and a sixth kill path that forgot the pair still
    // killed its target -- Trample and Bloodlust simply stopped being eligible
    // for that kill, silently, with no test able to name what had gone.
    //
    // Nobody is an answer, not an absence. A poison tick kills a combatant
    // whose applier may already be dead, and back-crediting it would put
    // points in a column the player cannot account for against any blow they
    // watched land. Spelling that as an argument is what keeps this file from
    // trading a silent missed credit for a silent over-credit.
    public enum KillCredit
    {
        // The blow's own actor scores it: the rider block's flag is raised and
        // the ledger takes a kill row.
        Attacker,

        // Counted as taken, credited to no one.
        Nobody,
    }
}

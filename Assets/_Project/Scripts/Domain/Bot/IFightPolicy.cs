using System.Collections.Generic;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Bot
{
    // One archetype's whole brain for a fight: given the legal menu, name
    // one entry. Policies hold no state between calls -- anything a policy
    // wants to remember belongs on `rng` (seeded per call site, so an
    // archetype's own tie-breaks stay reproducible) or is read fresh off
    // `session`/`actor` every time.
    public interface IFightPolicy
    {
        FightAction Choose(FightSession session, CombatantState actor, IReadOnlyList<FightAction> legal, SeededRandom rng);
    }
}

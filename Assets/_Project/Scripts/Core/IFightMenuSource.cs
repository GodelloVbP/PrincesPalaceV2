using System.Collections.Generic;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // WHERE THE SYSTEM MENU GETS A FIGHT'S VALUES (plan C4). In a fight the
    // menu's health, pools and effective stats come from here, never from
    // RunSnapshot.currentHealth, which is written only when the fight ends
    // (RunEncounter.WriteBackHealth) and so is stale for the whole fight.
    //
    // WHAT IS DISPLAYED, NOT WHAT IS TRUE. The session records a round before
    // its beats play, so its numbers can be a hit or a death ahead of the
    // player's screen. An implementation reports what the HUD has painted, and
    // the menu therefore never spoils a blow the player has not yet watched.
    //
    // The Fight scene implements it (FightController.MenuSource) and hands
    // itself to the scene's SystemMenuController when a fight is bound. Every
    // consumer reads the same facts: CHARACTER's rail and derived stats today,
    // PARTY's seat health next.
    public interface IFightMenuSource
    {
        // Every party member the HUD is showing, in the column's order.
        IReadOnlyList<FightMemberFacts> Members { get; }

        // One member by character id; false when the HUD is not showing them
        // (a benched character is not in the fight).
        bool TryGetMember(string characterId, out FightMemberFacts facts);
    }
}

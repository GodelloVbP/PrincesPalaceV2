using System.Linq;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Party;

namespace PrincesPalace.PlayModeTests
{
    // A fresh save's Bjorn has no Fury engine root, and the hub gate will not
    // take him down until he picks one. A fixture that presses the gate to
    // reach something past it calls this first, the way a player would have
    // visited the Talents screen.
    public static class EngineRoots
    {
        public static void GrantToSquad()
        {
            var save = SaveSlotManager.CurrentSave;
            foreach (var character in save.ActiveSquad())
            {
                if (!PartyReadiness.IsMissingEngineRoot(character)) continue;

                var root = ContentDatabase.TalentsFor(character)
                    .First(t => t.Data.Row == 0
                                && t.Data.Effects.Any(e => RequiredChoices.IsEngineRoot(e.Type)));
                character.unlockedTalentIds.Add(root.id);
            }
        }
    }
}

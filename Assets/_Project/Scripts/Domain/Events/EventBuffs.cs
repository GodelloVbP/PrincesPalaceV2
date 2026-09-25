using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Events
{
    // One buff an event granted this run (RunSnapshot.eventBuffs).
    //
    // ONE MODEL, TWO SCOPES. legStartStep is WholeRun (-1) for a buff that
    // lasts the run, or the run.legStartStep it was granted on for a buff
    // that lasts one leg. A leg buff is never cleared: AdvanceLeg moves
    // run.legStartStep past it and it simply stops matching. Nothing clears a
    // run buff either -- StartRun replaces the whole snapshot.
    //
    // `kind` is a string rather than an enum so that a kind a later build
    // drops is a value SaveData.Reconcile can see and prune, instead of an
    // int JsonUtility quietly maps onto whatever enum member now sits there.
    [Serializable]
    public class EventBuffEntry
    {
        public string kind = "";
        public int amount;
        public int legStartStep = EventBuffs.WholeRun;
    }

    public static class EventBuffs
    {
        public const int WholeRun = -1;

        // +amount to the squad's Prince's favor (ItemOfferRoll.CurrentSquadFavor).
        public const string PrincesFavor = "princesFavor";

        // The special pool is full when a character's turn opens, this leg
        // (read by the fight in P3).
        public const string FillSpecialPool = "fillSpecialPool";

        public static bool IsKnownKind(string kind) =>
            string.Equals(kind, PrincesFavor, StringComparison.Ordinal)
            || string.Equals(kind, FillSpecialPool, StringComparison.Ordinal);

        public static bool IsActive(EventBuffEntry buff, int currentLegStartStep) =>
            buff != null
            && (buff.legStartStep == WholeRun || buff.legStartStep == currentLegStartStep);

        // The buffs of `kind` in force right now, in grant order.
        public static IEnumerable<EventBuffEntry> Active(IReadOnlyList<EventBuffEntry> buffs, string kind,
            int currentLegStartStep)
        {
            if (buffs == null) yield break;

            foreach (var buff in buffs)
            {
                if (IsActive(buff, currentLegStartStep) && string.Equals(buff.kind, kind, StringComparison.Ordinal))
                {
                    yield return buff;
                }
            }
        }

        // Sum of the active buffs' amounts: two favor grants stack.
        public static int ActiveAmount(IReadOnlyList<EventBuffEntry> buffs, string kind, int currentLegStartStep)
        {
            int total = 0;
            foreach (var buff in Active(buffs, kind, currentLegStartStep)) total += buff.amount;
            return total;
        }

        public static bool AnyActive(IReadOnlyList<EventBuffEntry> buffs, string kind, int currentLegStartStep)
        {
            foreach (var _ in Active(buffs, kind, currentLegStartStep)) return true;
            return false;
        }

        // Load-time tidy, beside relicIds' prune: a null row or a kind this
        // build does not know is dropped rather than carried as dead weight.
        public static void Prune(List<EventBuffEntry> buffs)
        {
            buffs?.RemoveAll(buff => buff == null || !IsKnownKind(buff.kind));
        }
    }
}

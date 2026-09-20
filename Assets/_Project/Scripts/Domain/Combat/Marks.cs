using System.Linq;

namespace PrincesPalace.Domain.Combat
{
    // MARK: a general debuff status, reusable by any relic or skill effect
    // that wants "leave a mark, spend it on a later hit" — not owned by one
    // relic the way Drowned Lantern's own private mark-tracking HashSet is
    // (FightSession.Relics._marked, which predates this and is untouched).
    //
    // Rides StatusEffectType.Marked, which carries no Magnitude of its own —
    // a mark is present or it is not, and what it's worth is entirely up to
    // whoever consumes it.
    public static class Marks
    {
        // No art yet. SceneBuilder.LoadSprite degrades to a blank icon slot
        // with a console warning until this path exists, the same
        // convention every other iconPath in the project already follows —
        // registering the key costs nothing and lets the art land later
        // with no code change.
        public const string IconKey = "Assets/_Project/Art/Items/Relics/Processed/status_marked.png";

        // "Until consumed", spelled as a duration long enough that ordinary
        // turn-start ticking (StatusEffects.Tick) can never expire it
        // first — the same convention Shielded/Empowered/the runic ward
        // already use for a status that is meant to be spent, not decayed.
        public const int MarkDurationTurns = 99;

        public static void Apply(CombatantState target, CombatantState source)
        {
            if (target == null) return;
            StatusEffects.Apply(target.Statuses, StatusEffectType.Marked, 0, MarkDurationTurns, source);
        }

        public static bool IsMarked(CombatantState target)
        {
            return target != null && target.Statuses.Any(s => s.Type == StatusEffectType.Marked);
        }

        // Removes and reports whether there was actually a mark to remove —
        // callers key their bonus off the RETURN VALUE, not off calling
        // IsMarked first and ConsumeMark second, which would be two reads
        // of the same question and an easy place for the two to disagree.
        //
        // A ONE-LINE CALL TO StatusEffects.TrySpend (plan 1.8) rather than
        // its own removal any more -- Marked has exactly one live entry per
        // target (StackPolicyOf refreshes it), so "the first entry of this
        // type" and "the mark" are the same question, and this keeps its own
        // name and its own contract for the sixteen call sites that already
        // key off it.
        public static bool ConsumeMark(CombatantState target)
        {
            return target != null && StatusEffects.TrySpend(target.Statuses, StatusEffectType.Marked, out _);
        }
    }
}

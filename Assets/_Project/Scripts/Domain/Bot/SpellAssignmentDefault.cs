namespace PrincesPalace.Domain.Bot
{
    // THE ONE RULE ALL FOUR ARCHETYPES SHARE FOR PLACING A PENDING BOOK:
    // the first eligible fielded candidate (does not already know it, has a
    // free slot), in squad order. Shared rather than copied four times --
    // gate 3 has no reason yet for an aggressive archetype to place
    // differently from a defensive one, and a future archetype that DOES
    // want to weigh which character benefits most can override this call
    // instead of everyone re-deriving the same simple rule.
    //
    // Never replaces a full slot. SpellCandidateView.FreeSlot is -1 for a
    // full character precisely so this rule cannot reach for one; a
    // replacement is a real decision (which spell loses its slot) that no
    // archetype built for gate 3 is asked to make.
    public static class SpellAssignmentDefault
    {
        // A hand-rolled loop, not FirstOrDefault(c => c.Eligible): a
        // readonly struct's default (no CharacterId, FreeSlot 0) reads as
        // Eligible by its own field values, so FirstOrDefault would return
        // that default AND accept it whenever nothing in the list actually
        // qualifies -- the empty-candidate-list version of the bug an
        // unguarded sentinel always is.
        public static SpellAssignmentChoice Choose(SpellAssignmentView view)
        {
            if (view.Candidates != null)
            {
                foreach (var candidate in view.Candidates)
                {
                    if (candidate.Eligible) return SpellAssignmentChoice.AssignTo(candidate.CharacterId, candidate.FreeSlot);
                }
            }

            return SpellAssignmentChoice.Skip();
        }
    }
}

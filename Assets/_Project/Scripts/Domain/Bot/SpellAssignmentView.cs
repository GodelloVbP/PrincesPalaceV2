using System.Collections.Generic;

namespace PrincesPalace.Domain.Bot
{
    // ONE FIELDED CHARACTER, AS A CANDIDATE TO LEARN THE PENDING BOOK.
    //
    // FreeSlot is -1 when all three of SpellBooks.MaxSpellSlots are already
    // occupied -- the driver does not hand a policy occupied-slot detail
    // (which spell sits where) because no archetype built for gate 3 replaces
    // one to make room; a full character is simply not a candidate this
    // round. A future archetype that wants to weigh a replacement can be
    // given that detail when one is written to need it.
    public readonly struct SpellCandidateView
    {
        public readonly string CharacterId;
        public readonly bool AlreadyKnows;
        public readonly int FreeSlot;

        public SpellCandidateView(string characterId, bool alreadyKnows, int freeSlot)
        {
            CharacterId = characterId;
            AlreadyKnows = alreadyKnows;
            FreeSlot = freeSlot;
        }

        // A candidate worth placing into: does not already know it, and has
        // somewhere to put it.
        public bool Eligible => !AlreadyKnows && FreeSlot >= 0;
    }

    // WHAT A POLICY SEES FOR ONE PENDING BOOK (docs/PLAN_SHOP.md §1g, §2g).
    //
    // Asked once per entry still in run.unassignedSpellBooks after the
    // choice points that can grow it (a shop visit, a fight's drop) --
    // BotRunDriver's own call site says which. The bot has no dossier to
    // walk to, so it resolves every pending book the same turn it could
    // first act on it; a human player's two acts can be separated by any
    // number of rooms, which is why the bot's own numbers should be read as
    // "assigned as soon as possible," not as a measurement of promptness.
    public readonly struct SpellAssignmentView
    {
        public readonly string SkillId;
        public readonly IReadOnlyList<SpellCandidateView> Candidates;

        public SpellAssignmentView(string skillId, IReadOnlyList<SpellCandidateView> candidates)
        {
            SkillId = skillId;
            Candidates = candidates ?? System.Array.Empty<SpellCandidateView>();
        }
    }

    // ASSIGN, or SKIP -- skip is legal: a book nobody eligible can place
    // this round stays in the pool for a later visit, exactly as the
    // dossier's own panel leaves it.
    public readonly struct SpellAssignmentChoice
    {
        public readonly bool Assign;
        public readonly string CharacterId;
        public readonly int Slot;

        private SpellAssignmentChoice(bool assign, string characterId, int slot)
        {
            Assign = assign;
            CharacterId = characterId;
            Slot = slot;
        }

        public static SpellAssignmentChoice Skip() => new SpellAssignmentChoice(false, null, -1);

        public static SpellAssignmentChoice AssignTo(string characterId, int slot) =>
            new SpellAssignmentChoice(true, characterId, slot);
    }
}

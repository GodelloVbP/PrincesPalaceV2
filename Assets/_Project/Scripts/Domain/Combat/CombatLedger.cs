using System.Collections.Generic;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat
{
    // What each combatant actually did, counted while the fight resolves.
    //
    // Keyed by ID rather than by CombatantState, and that is the whole reason
    // this is usable: a CombatantState lives and dies with one encounter, so a
    // ledger keyed by object could never be folded across the fights of a run.
    // Ids survive, which is what lets a fight's ledger add into a run's.
    //
    // Counted at the moment damage lands rather than derived from the recorded
    // beats afterwards. Beats carry an actor, an amount and a healing flag, but
    // not the damage TYPE and not what a shield ate -- and a beat reports the
    // largest single hit of an AOE rather than its total, deliberately, so
    // summing beats would under-count every volley in the game.
    public sealed class CombatLedger
    {
        // One combatant's totals. A class rather than a struct because it is
        // mutated in place on every hit and a struct would need writing back
        // through the dictionary each time.
        public sealed class Line
        {
            // Split at the same seam CombatMath.IsPhysical draws: Physical
            // rides the weapon, everything else rides the spell. Two counters
            // rather than one per element because that is the split the player
            // makes decisions about, and a per-element breakdown would grow a
            // column every time content adds a type.
            public int PhysicalDealt;
            public int OtherDealt;

            // What reached this combatant's HEALTH. Shielded is counted
            // separately and NOT included here -- a point a shield ate was
            // never taken, and adding the two would double-count every blow
            // against a warded target.
            public int DamageTaken;
            public int Shielded;

            public int Healed;
            public int Kills;
            public int TimesDowned;

            public int TotalDealt => PhysicalDealt + OtherDealt;

            public void Add(Line other)
            {
                if (other == null) return;

                PhysicalDealt += other.PhysicalDealt;
                OtherDealt += other.OtherDealt;
                DamageTaken += other.DamageTaken;
                Shielded += other.Shielded;
                Healed += other.Healed;
                Kills += other.Kills;
                TimesDowned += other.TimesDowned;
            }
        }

        private readonly Dictionary<string, Line> _lines = new Dictionary<string, Line>();

        // Insertion order, kept so a screen listing the party does not reorder
        // itself between two fights. Dictionary enumeration order is not a
        // contract, and CharacterReward already carries a slot index for
        // exactly this reason.
        private readonly List<string> _order = new List<string>();

        public IReadOnlyList<string> Ids => _order;

        public bool Has(string id) => id != null && _lines.ContainsKey(id);

        // Never returns null. A combatant who did nothing has a line of zeroes,
        // which is a true statement about them and saves every caller a guard.
        public Line For(string id)
        {
            if (string.IsNullOrEmpty(id)) return new Line();

            if (!_lines.TryGetValue(id, out var line))
            {
                line = new Line();
                _lines[id] = line;
                _order.Add(id);
            }

            return line;
        }

        public void Dealt(string actorId, DamageType type, int amount)
        {
            if (string.IsNullOrEmpty(actorId) || amount <= 0) return;

            var line = For(actorId);
            if (CombatMath.IsPhysical(type)) line.PhysicalDealt += amount;
            else line.OtherDealt += amount;
        }

        // `amount` is what reached health; `shielded` is what a ward ate. Taken
        // as two numbers rather than one total precisely so the caller cannot
        // quietly decide which of the two a blow was.
        public void Took(string targetId, int amount, int shielded = 0)
        {
            if (string.IsNullOrEmpty(targetId)) return;

            var line = For(targetId);
            if (amount > 0) line.DamageTaken += amount;
            if (shielded > 0) line.Shielded += shielded;
        }

        public void Restored(string targetId, int amount)
        {
            if (string.IsNullOrEmpty(targetId) || amount <= 0) return;
            For(targetId).Healed += amount;
        }

        public void ScoredKill(string actorId)
        {
            if (string.IsNullOrEmpty(actorId)) return;
            For(actorId).Kills++;
        }

        public void WentDown(string targetId)
        {
            if (string.IsNullOrEmpty(targetId)) return;
            For(targetId).TimesDowned++;
        }

        // Folds another ledger into this one. A fight's totals added to a
        // run's, which is the operation the whole id-keyed shape exists for.
        public void Add(CombatLedger other)
        {
            if (other == null) return;

            foreach (var id in other._order)
            {
                For(id).Add(other._lines[id]);
            }
        }

        public bool IsEmpty => _lines.Count == 0;
    }
}

using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Combat;

namespace PrincesPalace
{
    // Folding a fight's totals into the run that contains it.
    //
    // The boundary between CombatLedger (in-memory, dictionary-keyed, Domain)
    // and RunSnapshot (flat lists, serializable, Data). Both shapes exist for
    // good reasons and neither can be the other, so the translation gets a name
    // rather than being smeared across FightBootstrap.
    //
    // Static and stateless: the run snapshot IS the state, and a second copy
    // held anywhere would be a second thing that can be wrong.
    public static class RunLedger
    {
        // Adds one fight's counters onto the run's.
        //
        // Called for a LOST fight as well as a won one. What a character did in
        // the fight that killed them is part of the run, and dropping it would
        // make the death screen quietly under-report the most dramatic fight in
        // it.
        public static void Fold(RunSnapshot run, CombatLedger fight)
        {
            if (run == null || fight == null) return;

            run.ledger ??= new List<RunLedgerEntry>();

            foreach (var id in fight.Ids)
            {
                var line = fight.For(id);
                var entry = EntryFor(run, id);

                entry.physicalDealt += line.PhysicalDealt;
                entry.otherDealt += line.OtherDealt;
                entry.damageTaken += line.DamageTaken;
                entry.shielded += line.Shielded;
                entry.healed += line.Healed;
                entry.kills += line.Kills;
                entry.timesDowned += line.TimesDowned;
            }
        }

        // Everything about a finished room that is not per-character.
        //
        // `won` gates the room count and the gold, not the ledger fold above:
        // a room you died in was not cleared, but the blows you struck in it
        // still happened.
        public static void RecordRoom(RunSnapshot run, bool won, int goldGained, int expGained, int step)
        {
            if (run == null) return;

            if (won)
            {
                run.roomsCleared++;
                run.goldEarned += goldGained > 0 ? goldGained : 0;
            }

            run.expEarned += expGained > 0 ? expGained : 0;

            // Furthest reached, not where they stand. A run that ends is a run
            // that stopped moving, and "how deep did I get" is the question.
            if (step > run.deepestStep) run.deepestStep = step;
        }

        // A boss put down this run. Idempotent within the run, so a resumed
        // fight that somehow reports twice cannot double-count.
        public static void RecordBossKill(RunSnapshot run, string bossId)
        {
            if (run == null || string.IsNullOrEmpty(bossId)) return;

            run.bossesKilled ??= new List<string>();
            if (!run.bossesKilled.Contains(bossId)) run.bossesKilled.Add(bossId);
        }

        // The run's totals for one character, or a line of zeroes. Never null,
        // so a screen does not need a guard per row.
        public static RunLedgerEntry For(RunSnapshot run, string characterId)
        {
            if (run?.ledger == null || string.IsNullOrEmpty(characterId))
            {
                return new RunLedgerEntry { characterId = characterId ?? "" };
            }

            return run.ledger.FirstOrDefault(e => e.characterId == characterId)
                   ?? new RunLedgerEntry { characterId = characterId };
        }

        // Everything the party dealt in this run, off its own ledger.
        //
        // LONG, because a profile that plays for a while passes two billion and
        // an int would wrap negative -- silently un-earning any achievement
        // counting it. Written twice (Achievements and RunSettlement) before
        // landing here, which is where the ledger already lives.
        public static long TotalDamage(RunSnapshot run)
        {
            if (run?.ledger == null) return 0;

            long total = 0;
            foreach (var entry in run.ledger)
            {
                if (entry != null) total += entry.TotalDealt;
            }

            return total;
        }

        private static RunLedgerEntry EntryFor(RunSnapshot run, string id)
        {
            var entry = run.ledger.FirstOrDefault(e => e.characterId == id);
            if (entry != null) return entry;

            entry = new RunLedgerEntry { characterId = id };
            run.ledger.Add(entry);
            return entry;
        }
    }
}

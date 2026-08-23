using System.Collections.Generic;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Combat.Session
{
    // HOW LONG UNTIL YOU CAN DO THAT AGAIN.
    //
    // Every skill in the game was castable every turn, gated only by what it
    // cost. That makes a kit a wallet problem: the correct play is whichever
    // skill has the best damage per mana, every turn, until the mana runs out.
    // Nothing about the ORDER of a turn mattered, so nothing about a kit could
    // be a rhythm.
    //
    // COUNTED IN THE CASTER'S OWN TURNS, not in rounds or in real time. A slow
    // character and a fast one do not experience a round the same way -- the
    // charge-based turn order means a fast one may act twice in the time a slow
    // one acts once -- so a cooldown measured in rounds would be worth twice as
    // much to one of them for reasons nobody chose.
    //
    // THE NUMBER IS "TURNS UNTIL USABLE AGAIN", counted from the turn it was
    // cast on. Cast on turn 1 with a cooldown of 2, and it comes back on turn 3.
    // That reads exactly as an author says it out loud -- "use it turn one, then
    // again turn three" -- which is the only spelling that does not need a
    // conversion table taped to the monitor.
    //
    // PER FIGHT. A cooldown surviving into the next encounter would make the
    // opening turn of a fight depend on how the last one ended, which the player
    // cannot see and cannot plan around. Fights are the unit a player thinks in.
    public partial class FightSession
    {
        // Keyed by caster and skill id. A dictionary per caster rather than one
        // flat composite key, because "clear everything this character has on
        // cooldown" is a real operation -- a respec, a transformation, the
        // relic below -- and a flat key would make it a scan.
        private readonly Dictionary<CombatantState, Dictionary<string, int>> _cooldowns =
            new Dictionary<CombatantState, Dictionary<string, int>>();

        // ZERO MEANS READY, and no entry means ready too. Both spellings exist
        // because the tally is only written for skills that have actually been
        // cast, and asking about one that has not should not create a row.
        public int CooldownRemaining(CombatantState actor, string skillId)
        {
            if (actor == null || string.IsNullOrEmpty(skillId)) return 0;

            return _cooldowns.TryGetValue(actor, out var forActor)
                   && forActor.TryGetValue(skillId, out int turns)
                ? turns
                : 0;
        }

        public bool IsOnCooldown(CombatantState actor, string skillId) =>
            CooldownRemaining(actor, skillId) > 0;

        // Put on cooldown by CASTING it, not by resolving it. A cast refused for
        // want of mana never reaches this; a cast that resolves into nothing
        // because every target died first still spent the turn and still spends
        // the cooldown.
        private void BeginCooldown(CombatantState actor, ResolvedSkill skill)
        {
            if (actor == null || skill.CooldownTurns <= 0) return;

            if (!_cooldowns.TryGetValue(actor, out var forActor))
            {
                forActor = new Dictionary<string, int>();
                _cooldowns[actor] = forActor;
            }

            forActor[skill.Id] = skill.CooldownTurns;
        }

        // ONE TURN OFF EVERYTHING THIS ACTOR IS WAITING ON, at the start of
        // their turn.
        //
        // At the START rather than the end, so a cooldown of 1 means "not this
        // turn, yes the next one". Ticked at the end it would come back on the
        // very turn it was spent, which is not a cooldown at all.
        private void TickCooldowns(CombatantState actor)
        {
            if (actor == null || !_cooldowns.TryGetValue(actor, out var forActor)) return;

            // Materialised because the loop writes to the dictionary it reads.
            var ids = new List<string>(forActor.Keys);

            foreach (var id in ids)
            {
                int left = forActor[id] - 1;

                // Removed rather than left at zero: an entry that exists means
                // "waiting", and a zero row would answer the same as no row
                // while making every debug print of this dictionary longer.
                if (left <= 0) forActor.Remove(id);
                else forActor[id] = left;
            }
        }

        // Takes `turns` off everything the actor is waiting on. The Salt
        // Ledger's whole mechanic, and deliberately general -- a talent or a
        // consumable that did the same thing would want exactly this.
        //
        // Returns how many skills it actually shortened, so the caller can say
        // so only when something happened. "Your cooldowns tick down" printed
        // on a turn where nothing was on cooldown is noise that teaches the
        // player to stop reading the log.
        private int ReduceCooldowns(CombatantState actor, int turns)
        {
            if (actor == null || turns <= 0 || !_cooldowns.TryGetValue(actor, out var forActor)) return 0;

            var ids = new List<string>(forActor.Keys);
            int shortened = 0;

            foreach (var id in ids)
            {
                int left = forActor[id] - turns;
                if (left <= 0) forActor.Remove(id);
                else forActor[id] = left;

                shortened++;
            }

            return shortened;
        }
    }
}

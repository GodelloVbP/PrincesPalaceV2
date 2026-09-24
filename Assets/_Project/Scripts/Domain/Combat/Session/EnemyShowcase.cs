using System;
using System.Collections.Generic;

namespace PrincesPalace.Domain.Combat.Session
{
    // A MONSTER SHOWING ITS WHOLE KIT, in order, once each.
    //
    // The fight picks an enemy's action by weighted roll (EnemyAbilityDraw),
    // which is right for playing and useless for looking: an author who has
    // just written a third ability onto a mob wants to SEE that ability, and a
    // 20% weight means five fights and no promise even then. tools/preview.ps1
    // hands the session one of these instead, and the enemy then takes its
    // authored abilities in order, one per turn, and swings plainly afterwards.
    //
    // PREVIEW-OWNED AND NOWHERE NEAR THE CONTENT RECORD. Nothing here is
    // written to a ResolvedEnemy, a save or an asset -- FightSession holds it
    // for the life of one session and every fight the game itself builds leaves
    // it null, which is the branch that keeps the roll.
    //
    // AN ENTRY IT CANNOT PLAY IS REPORTED BY NAME AND SKIPPED, never silently
    // swapped for something else. FightSession.EffectivePoolFor has already
    // zeroed the weight of an ability whose prerequisite is unmet this turn --
    // a summon at its cap, a plain swing while rooted -- and a showcase that
    // quietly substituted the next ability would show the author a sequence
    // their mob cannot actually perform, which is worse than showing them
    // nothing and saying so.
    public sealed class EnemyShowcase
    {
        // Per enemy, so a -Formation full stage of three copies shows the same
        // sequence three times rather than one copy racing through it.
        private readonly Dictionary<object, int> _cursors = new Dictionary<object, int>();
        private readonly Action<string> _report;

        // Everything the showcase decided, in order, for a caller that wants to
        // name the turns after the fact (the capture fixture names its files
        // from this).
        private readonly List<string> _played = new List<string>();
        private readonly List<string> _skipped = new List<string>();

        public IReadOnlyList<string> Played => _played;
        public IReadOnlyList<string> Skipped => _skipped;

        // The report sink is an Action<string> rather than anything engine-
        // shaped because this is Domain: Core passes Debug.Log, a test passes
        // its own collector.
        public EnemyShowcase(Action<string> report = null)
        {
            _report = report;
        }

        // The index into `pool` this enemy should take now, or -1 for the plain
        // attack -- which is what FightSession.BuildIntent already does with an
        // out-of-range index, so "the abilities are done" needs no second path.
        public int Next(object enemy, IReadOnlyList<EnemyAbility> pool)
        {
            if (pool == null || pool.Count == 0)
            {
                return -1;
            }

            _cursors.TryGetValue(enemy, out int cursor);

            while (cursor < pool.Count)
            {
                var ability = pool[cursor];
                cursor++;

                // THE PLAIN SWING IS THE ENCORE, not part of the running order.
                // It sits at index 0 of every authored pool (FightEncounter-
                // Adapter puts it there), so walking the pool in order without
                // this would open on the least interesting thing the mob does.
                if (ability.IsPlainSwing)
                {
                    continue;
                }

                if (ability.Weight <= 0f)
                {
                    Skip(ability.Label);
                    continue;
                }

                _cursors[enemy] = cursor;
                _played.Add(ability.Label);
                return cursor - 1;
            }

            _cursors[enemy] = cursor;
            _played.Add(FightSession.IntentAttack);
            return -1;
        }

        // How many turns it takes to show everything once, so a caller can stop
        // the fight when the kit is exhausted rather than guessing a number.
        public static int ScriptLength(IReadOnlyList<EnemyAbility> pool)
        {
            if (pool == null) return 1;

            int abilities = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                if (!pool[i].IsPlainSwing) abilities++;
            }

            // Plus the plain attack that always closes it.
            return abilities + 1;
        }

        private void Skip(string label)
        {
            string note = string.IsNullOrEmpty(label) ? "(unnamed ability)" : label;
            _skipped.Add(note);
            _report?.Invoke(
                $"[EnemyShowcase] skipped '{note}': its prerequisite cannot be met this turn " +
                "(a summon already at its cap, or a swing while rooted). Showing the next entry instead of " +
                "pretending this one played.");
        }
    }
}

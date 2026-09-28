using System.Collections.Generic;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Combat.Session
{
    // AN ENEMY'S SCHEDULE, COUNTED ON ITS OWN ACTING TURNS
    // (docs/PLAN_BELLWETHER_KIT.md 1.6, 2.2, 3.7).
    //
    // An acting turn is a turn on which the enemy's action resolves: a stunned,
    // broken or rooted-helpless turn, a "nothing in reach" forfeit and a voided
    // swing are not counted and use up no step. A sequence starts when the
    // turn about to be taken is one of an entry's onTurns, and then holds the
    // enemy's following acting turns, one step each, until done -- whatever
    // comes between, so a pair is never split.
    //
    // THE SCHEDULE DRAWS NOTHING. It only decides which pool index an
    // intent commits to, at the same point the Showcase does; the ability
    // roll and the target roll are still consumed, so a seeded run keeps the
    // shape a no-schedule monster gives it.
    public sealed partial class FightSession
    {
        private sealed class ScheduleState
        {
            public int ActingTurns;

            // The entry being played and the index of its next step, or -1.
            public int ActiveEntry = -1;
            public int Cursor;

            // Who the sequence's last resolved step was aimed at: the next
            // step is aimed at them again while they live and are in reach
            // (plan 2.6: the knell strikes the chained target).
            public CombatantState LastTarget;
        }

        // Which step is due on this enemy's next acting turn.
        private readonly struct ScheduledStep
        {
            public readonly int Entry;
            public readonly int Step;
            public readonly int PoolIndex;

            // -1 when this is the sequence's last step.
            public readonly int NextPoolIndex;

            public ScheduledStep(int entry, int step, int poolIndex, int nextPoolIndex)
            {
                Entry = entry;
                Step = step;
                PoolIndex = poolIndex;
                NextPoolIndex = nextPoolIndex;
            }

            public bool Continues => Step > 0;
        }

        private readonly Dictionary<CombatantState, ScheduleState> _schedules =
            new Dictionary<CombatantState, ScheduleState>();

        // How many acting turns this enemy has taken this fight (0 before its
        // first). Public for the bot's trace (M6) and the tests.
        public int ActingTurnsOf(CombatantState enemy) =>
            enemy != null && _schedules.TryGetValue(enemy, out var state) ? state.ActingTurns : 0;

        private ScheduleState ScheduleStateOf(CombatantState enemy)
        {
            if (!_schedules.TryGetValue(enemy, out var state))
            {
                state = new ScheduleState();
                _schedules[enemy] = state;
            }

            return state;
        }

        // The step due on the enemy's next acting turn, or null when that turn
        // is the weighted draw's. A step whose skill the kit dropped (an id no
        // skill matched, FightEncounterAdapter) ends its sequence there: the
        // turn falls back to the draw rather than stalling forever.
        private ScheduledStep? ScheduledStepFor(CombatantState enemy)
        {
            var kit = SourceFor(enemy);
            var schedule = kit?.Source?.Schedule;
            if (schedule == null || schedule.Length == 0) return null;

            _schedules.TryGetValue(enemy, out var state);
            int entry = state?.ActiveEntry ?? -1;
            int step = state?.Cursor ?? 0;

            if (entry < 0)
            {
                int next = (state?.ActingTurns ?? 0) + 1;
                for (int e = 0; e < schedule.Length && entry < 0; e++)
                {
                    var turns = schedule[e]?.OnTurns;
                    if (turns == null) continue;
                    for (int t = 0; t < turns.Length; t++)
                    {
                        if (turns[t] == next)
                        {
                            entry = e;
                            step = 0;
                            break;
                        }
                    }
                }

                if (entry < 0) return null;
            }

            var skills = schedule[entry].Skills;
            if (skills == null || step >= skills.Length) return null;

            int index = PoolIndexOf(kit.Abilities, skills[step]);
            if (index < 0) return null;

            int nextIndex = step + 1 < skills.Length ? PoolIndexOf(kit.Abilities, skills[step + 1]) : -1;
            return new ScheduledStep(entry, step, index, nextIndex);
        }

        // Every pool index some schedule entry plays, for the preview's
        // showcase (EnemyShowcase.Next). Empty for a monster with no schedule.
        private static HashSet<int> ScheduledPoolIndices(EnemyKit kit)
        {
            var indices = new HashSet<int>();
            var schedule = kit?.Source?.Schedule;
            if (schedule == null) return indices;

            foreach (var entry in schedule)
            {
                if (entry?.Skills == null) continue;
                foreach (var id in entry.Skills)
                {
                    int index = PoolIndexOf(kit.Abilities, id);
                    if (index >= 0) indices.Add(index);
                }
            }

            return indices;
        }

        private static int PoolIndexOf(IReadOnlyList<EnemyAbility> pool, string skillId)
        {
            if (pool == null || string.IsNullOrEmpty(skillId)) return -1;

            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].HasSkill && pool[i].Skill != null && pool[i].Skill.Id == skillId) return i;
            }

            return -1;
        }

        // The target a continuing step is aimed at: the one its sequence last
        // struck, while they live and the step can reach them. Null means
        // "use the ordinary pick", which has already drawn.
        private CombatantState SequenceTargetFor(CombatantState enemy, ScheduledStep? step, EnemyAbility? ability)
        {
            if (!step.HasValue || !step.Value.Continues) return null;
            if (!_schedules.TryGetValue(enemy, out var state)) return null;

            var last = state.LastTarget;
            if (last == null || !last.IsAlive) return null;
            return CanReachWithAbility(enemy, ability, last) ? last : null;
        }

        // CALLED AT THE POINT OF NO RETURN in ResolveEnemyAction: the action
        // is about to resolve, so this is an acting turn. The due step is used
        // up only when the ability resolving IS that step -- a Showcase or any
        // other override leaves it waiting.
        private void NoteEnemyActed(CombatantState enemy, int poolIndex, CombatantState target)
        {
            var due = ScheduledStepFor(enemy);
            var state = ScheduleStateOf(enemy);
            state.ActingTurns++;

            if (!due.HasValue || due.Value.PoolIndex != poolIndex) return;

            state.LastTarget = target;
            var skills = SourceFor(enemy).Source.Schedule[due.Value.Entry].Skills;
            int next = due.Value.Step + 1;

            if (next >= skills.Length)
            {
                state.ActiveEntry = -1;
                state.Cursor = 0;
                state.LastTarget = null;
            }
            else
            {
                state.ActiveEntry = due.Value.Entry;
                state.Cursor = next;
            }
        }
    }
}

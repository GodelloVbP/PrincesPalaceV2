using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PrincesPalace.Domain.Combat;
using UnityEngine;

namespace PrincesPalace
{
    // The sound a status makes when it goes ON a combatant, keyed by status
    // TYPE so a new spell, relic or talent that applies Shielded or Regen is
    // audible with no authoring. A status absent from the table is silent,
    // which is every harmful one today.
    //
    // Fired from FightBeatPlayer at each beat's impact, which hands over every
    // combatant in the beat's snapshot: a status type that a combatant did not
    // carry at its last observation and carries now was just applied. State is
    // already final when a beat plays, so the cue lands on the first beat
    // after the action that applied it. A heal that lands as a beat has its
    // own spell sfxPath; this covers the lasting effect.
    //
    // Takes are alternated at random like CharacterVoice's, and a cue plays at
    // most once per type per frame: a ward on three allies at once is one
    // chime, not a chord of three.
    public static class StatusCues
    {
        private static readonly Dictionary<StatusEffectType, string[]> ByType =
            new Dictionary<StatusEffectType, string[]>
            {
                [StatusEffectType.Shielded] = Ward,
                [StatusEffectType.Protect] = Ward,
                [StatusEffectType.Regen] = Regen,
            };

        // What each combatant carried at its last observation. Weak, so a
        // finished fight's combatants go with it and no reset call is needed.
        private static readonly ConditionalWeakTable<CombatantState, HashSet<StatusEffectType>> Seen =
            new ConditionalWeakTable<CombatantState, HashSet<StatusEffectType>>();

        private static string[] Ward => new[]
        {
            "Audio/Sfx/status_ward_1", "Audio/Sfx/status_ward_2", "Audio/Sfx/status_ward_3",
        };

        private static string[] Regen => new[]
        {
            "Audio/Sfx/status_regen_1", "Audio/Sfx/status_regen_2", "Audio/Sfx/status_regen_3",
        };

        private static readonly Dictionary<string, int> LastFrame = new Dictionary<string, int>();

        // The takes for a status type, empty when it is silent.
        public static IReadOnlyList<string> ClipsFor(StatusEffectType type) =>
            ByType.TryGetValue(type, out var clips) ? clips : Array.Empty<string>();

        public static void Observe(IEnumerable<CombatantState> combatants)
        {
            if (combatants == null) return;

            foreach (var combatant in combatants)
            {
                if (combatant == null) continue;

                var seen = Seen.GetOrCreateValue(combatant);
                var now = new HashSet<StatusEffectType>();
                foreach (var status in combatant.Statuses) now.Add(status.Type);

                foreach (var type in now)
                {
                    if (!seen.Contains(type)) PlayApplied(type);
                }

                seen.Clear();
                seen.UnionWith(now);
            }
        }

        public static void PlayApplied(StatusEffectType type)
        {
            var clips = ClipsFor(type);
            if (clips.Count == 0) return;

            int frame = Time.frameCount;
            if (LastFrame.TryGetValue(clips[0], out int last) && last == frame) return;
            LastFrame[clips[0]] = frame;

            SoundController.PlayClip(clips[UnityEngine.Random.Range(0, clips.Count)]);
        }
    }
}

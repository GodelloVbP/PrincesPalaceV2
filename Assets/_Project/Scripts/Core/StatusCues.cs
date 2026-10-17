using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using UnityEngine;

namespace PrincesPalace
{
    // The sound a status makes when it goes ON a combatant, keyed by status
    // TYPE so a new spell, relic or talent that applies Shielded or Regen is
    // audible with no authoring. A status absent from the table is silent,
    // which is every harmful one today.
    //
    // Fired from FightBeatPlayer at each beat's impact. It diffs the statuses
    // recorded in that beat's snapshot (never the live combatant, which is
    // already final for the whole round) against what the previous beat
    // recorded, so the cue lands on the beat that applied the status. The
    // first beat diffs against the fight's opening statuses
    // (FightSession.OpeningStatusesOf), so a relic ward the fight began with
    // never chimes. A heal that lands as a beat has its own spell sfxPath;
    // this covers the lasting effect.
    //
    // Takes are alternated at random like CharacterVoice's, and a cue plays at
    // most once per type per frame: a ward on three allies at once is one
    // chime, not a chord of three.
    public static class StatusCues
    {
        // Declared before ByType: static initialisers run in textual order.
        private static readonly string[] Ward =
        {
            "Audio/Sfx/status_ward_1", "Audio/Sfx/status_ward_2", "Audio/Sfx/status_ward_3",
        };

        private static readonly string[] Regen =
        {
            "Audio/Sfx/status_regen_1", "Audio/Sfx/status_regen_2", "Audio/Sfx/status_regen_3",
        };

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

        private static readonly Dictionary<string, int> LastFrame = new Dictionary<string, int>();

        // The takes for a status type, empty when it is silent.
        public static IReadOnlyList<string> ClipsFor(StatusEffectType type) =>
            ByType.TryGetValue(type, out var clips) ? clips : Array.Empty<string>();

        // `opening` answers a combatant's statuses at Begin; `play` is called
        // once per newly applied type that has a cue, and defaults to the
        // audible one so a test can record the types instead.
        public static void Observe(IReadOnlyDictionary<CombatantState, Vitals> snapshot,
                                   Func<CombatantState, StatusSnapshot> opening,
                                   Action<StatusEffectType> play = null)
        {
            if (snapshot == null) return;

            play = play ?? PlayApplied;
            foreach (var pair in snapshot)
            {
                var combatant = pair.Key;
                var recorded = pair.Value.Statuses;
                if (combatant == null || recorded == null) continue;

                if (!Seen.TryGetValue(combatant, out var seen))
                {
                    seen = new HashSet<StatusEffectType>();
                    var start = opening?.Invoke(combatant);
                    if (start != null) seen.UnionWith(start.Types());
                    Seen.Add(combatant, seen);
                }

                var now = new HashSet<StatusEffectType>(recorded.Types());
                foreach (var type in now)
                {
                    if (!seen.Contains(type) && ClipsFor(type).Count > 0) play(type);
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

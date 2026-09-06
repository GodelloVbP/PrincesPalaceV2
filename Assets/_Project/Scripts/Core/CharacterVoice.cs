using System.Collections.Generic;
using UnityEngine;

namespace PrincesPalace
{
    // What a character SAYS, as opposed to what the game does — grunts when
    // hit, a cry when they go down, a whoop when the fight is won.
    //
    // Kept apart from Sound/SoundLibrary on purpose. That enum is the game's
    // own vocabulary: a fixed, small set of UI noises where a typo should be
    // a compile error. A voice line is content — it belongs to a character,
    // there are a lot of them, and several are interchangeable takes of the
    // same moment. Folding ten near-identical grunts into that enum would
    // have buried ButtonClick in noise for no benefit.
    //
    // Everything here degrades silently. A character with no recorded voice
    // (which is every character except Shawn today) simply makes no sound,
    // exactly as before this existed — the same graceful-missing-content
    // posture the rest of the project takes toward art.
    public static class CharacterVoice
    {
        // Which moment in a fight a line belongs to.
        public enum Line
        {
            Hurt,
            Down,
            LowHealth,
            Victory,

            // What the ATTACKER says landing a blow, not what the target
            // says taking one -- keyed off the actor of a beat rather than
            // its target, the only line here that is.
            AttackHit,
        }

        // One character's recorded takes, keyed by moment.
        //
        // Paths are Resources-relative and WITHOUT the extension, which is
        // what Resources.Load expects. Several takes of one moment is the
        // normal case, not a special one: ten grunts played at random is the
        // difference between a character who reacts and a character with a
        // catchphrase.
        private static readonly Dictionary<string, Dictionary<Line, string[]>> Voices =
            new Dictionary<string, Dictionary<Line, string[]>>
            {
                ["sheep"] = new Dictionary<Line, string[]>
                {
                    [Line.Hurt] = new[]
                    {
                        "Audio/Sfx/Shawn/damage_1_shawn",
                        "Audio/Sfx/Shawn/damage_2_shawn",
                        "Audio/Sfx/Shawn/damage_3_shawn",
                        "Audio/Sfx/Shawn/damage_4_shawn",
                        "Audio/Sfx/Shawn/damage_5_shawn",
                        "Audio/Sfx/Shawn/damage_6_shawn",
                        "Audio/Sfx/Shawn/damage_7_shawn",
                        "Audio/Sfx/Shawn/damage_8_shawn",
                        "Audio/Sfx/Shawn/damage_9_shawn",
                        "Audio/Sfx/Shawn/damage_10_shawn",
                    },
                    [Line.Down] = new[] { "Audio/Sfx/Shawn/down_shawn_1" },
                    [Line.LowHealth] = new[] { "Audio/Sfx/Shawn/shawn_low_on_health" },
                    [Line.Victory] = new[] { "Audio/Sfx/Shawn/shawn_woo_happy" },
                    [Line.AttackHit] = new[]
                    {
                        "Audio/Sfx/shawn_hit_1",
                        "Audio/Sfx/shawn_hit_2",
                        "Audio/Sfx/shawn_hit_3",
                        "Audio/Sfx/shawn_hit_4",
                    },
                },
            };

        // Below this fraction of max health, a hit gets the "low on health"
        // line instead of an ordinary grunt.
        //
        // Said ONCE per crossing, not on every hit below the line — see
        // ClearSpentLines. A character who announces they are nearly dead
        // every single turn stops being informative and starts being noise,
        // which is the same reason enemy intents only telegraph skills.
        public const float LowHealthFraction = 0.3f;

        private static readonly HashSet<string> SpentThisFight = new HashSet<string>();

        // Called when a fight is built, so a line that only fires once per
        // fight is available again in the next one.
        public static void ClearSpentLines()
        {
            SpentThisFight.Clear();
        }

        public static bool Has(string characterId, Line line)
        {
            return Takes(characterId, line) != null;
        }

        // Exposed so a test can assert every authored path resolves to a real
        // clip, rather than discovering a typo by not hearing anything.
        public static string[] Takes(string characterId, Line line)
        {
            if (string.IsNullOrEmpty(characterId) || !Voices.TryGetValue(characterId, out var lines))
            {
                return null;
            }

            return lines.TryGetValue(line, out var takes) && takes.Length > 0 ? takes : null;
        }

        // Test-only door to the voice table's own keys, named ...ForTest per
        // house convention (FightBeatPlayer.WireStageForTest, FightController.
        // StageShakesForTest) since Core's InternalsVisibleTo names only the
        // Editor assembly and checking a key against ContentDatabase.Characters
        // needs PlayMode plus real content. Takes(characterId, line) already
        // degrades a miss to null silently -- by design, per this file's own
        // header -- which is exactly why nothing else would ever notice a key
        // going stale.
        public static IReadOnlyCollection<string> VoiceKeysForTest => Voices.Keys;

        public static void Play(string characterId, Line line)
        {
            var takes = Takes(characterId, line);
            if (takes == null)
            {
                return;
            }

            SoundController.PlayClip(takes[Random.Range(0, takes.Length)]);
        }

        // The bridge from Domain's VoiceLine to this catalog's own enum.
        //
        // Two enums rather than one on purpose: Domain decides WHICH line a blow
        // earned (it depends on health at the moment the blow lands, which only
        // the session knows), and this file owns the CATALOG of takes. Mapping
        // at the single boundary keeps Domain free of Resources paths and keeps
        // this file free of combat rules.
        public static void Play(string characterId, PrincesPalace.Domain.Combat.Session.VoiceLine line, bool oncePerFight)
        {
            var mapped = Map(line);
            if (oncePerFight) PlayOnce(characterId, mapped);
            else Play(characterId, mapped);
        }

        private static Line Map(PrincesPalace.Domain.Combat.Session.VoiceLine line)
        {
            switch (line)
            {
                case PrincesPalace.Domain.Combat.Session.VoiceLine.Down: return Line.Down;
                case PrincesPalace.Domain.Combat.Session.VoiceLine.LowHealth: return Line.LowHealth;
                case PrincesPalace.Domain.Combat.Session.VoiceLine.Victory: return Line.Victory;
                case PrincesPalace.Domain.Combat.Session.VoiceLine.AttackHit: return Line.AttackHit;
                default: return Line.Hurt;
            }
        }

        // Whether a once-per-fight line has already been said. Exposed so a test
        // can assert the threshold rule holds without listening for audio.
        public static bool HasSpent(string characterId, Line line) =>
            SpentThisFight.Contains(Key(characterId, line));

        private static string Key(string characterId, Line line) => $"{characterId}/{line}";

        // At most once per character per fight. For lines that mark a THRESHOLD
        // rather than an event -- a grunt is an event and repeats.
        public static void PlayOnce(string characterId, Line line)
        {
            if (!SpentThisFight.Add(Key(characterId, line)))
            {
                return;
            }

            Play(characterId, line);
        }
    }
}

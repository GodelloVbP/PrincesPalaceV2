using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using PrincesPalace;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.PlayModeTests
{
    // That the fight can actually be heard.
    //
    // Sound fails SILENTLY by its nature -- a missing clip is a null and a
    // no-op, and nothing on screen changes -- so the useful assertions are that
    // every path names a real file and that the decision layer above it is
    // reached at all.
    public class FightSoundTests
    {
        [Test]
        public void EveryVoiceLineTheFightCanRecordResolvesToARealClip()
        {
            // Domain names a line; this side owns the catalog. A line the
            // session can decide on and the catalog cannot serve is a hole that
            // only shows up as a character going quiet at the worst moment.
            var lines = (VoiceLine[])Enum.GetValues(typeof(VoiceLine));
            var missing = new List<string>();

            foreach (var line in lines)
            {
                // "sheep" is the only character with takes recorded today. When
                // a second one arrives this should sweep the roster instead.
                var takes = CharacterVoice.Takes("sheep", Mapped(line));
                if (takes == null)
                {
                    missing.Add(line + " (no take authored)");
                    continue;
                }

                // AND THAT THE FILES EXIST. "A path is written down" and "a
                // clip plays" are different claims, and only the second one
                // matters -- a typo in a Resources path is silent by nature.
                foreach (var path in takes)
                {
                    if (Resources.Load<AudioClip>(path) == null) missing.Add(path);
                }
            }

            CollectionAssert.IsEmpty(missing,
                "Shawn has no take for: " + string.Join(", ", missing));
        }

        // Mirrors CharacterVoice.Map. Duplicated deliberately: if the mapping
        // changes on one side only, this test fails, which is the entire point
        // of writing it out rather than calling the private one.
        private static CharacterVoice.Line Mapped(VoiceLine line)
        {
            switch (line)
            {
                case VoiceLine.Down: return CharacterVoice.Line.Down;
                case VoiceLine.LowHealth: return CharacterVoice.Line.LowHealth;
                case VoiceLine.Victory: return CharacterVoice.Line.Victory;
                case VoiceLine.AttackHit: return CharacterVoice.Line.AttackHit;
                default: return CharacterVoice.Line.Hurt;
            }
        }

        [Test]
        public void AnUnknownCharacterIsSilentRatherThanThrowing()
        {
            // Graceful degradation: a character with no takes recorded yet has
            // to be playable, not a crash on the first hit they take.
            Assert.IsFalse(CharacterVoice.Has("nobody", CharacterVoice.Line.Hurt));
            Assert.DoesNotThrow(() => CharacterVoice.Play("nobody", VoiceLine.Hurt, oncePerFight: false));
        }

        [Test]
        public void AnEmptyClipPathIsASilentNoOp()
        {
            // Every beat carries an SfxPath and most are blank, so playback
            // calls this constantly with nothing to play. It has to cost
            // nothing and say nothing.
            Assert.DoesNotThrow(() => SoundController.PlayClip(""));
            Assert.DoesNotThrow(() => SoundController.PlayClip(null));
        }

        [Test]
        public void AMissingClipIsSilentRatherThanThrowing()
        {
            // Content names its own clips in json; a typo must not end the
            // fight.
            Assert.DoesNotThrow(() => SoundController.PlayClip("Audio/Sfx/does_not_exist"));
        }

        [UnityTest]
        public IEnumerator NearlyDeadIsSaidOncePerFight_AndAgainInTheNext()
        {
            // "Nearly dead" marks a threshold rather than an event. The spent
            // list is what enforces that -- and clearing it when a new fight
            // starts is what stops the second fight being silent.
            CharacterVoice.ClearSpentLines();

            CharacterVoice.PlayOnce("sheep", CharacterVoice.Line.LowHealth);
            Assert.IsTrue(CharacterVoice.HasSpent("sheep", CharacterVoice.Line.LowHealth),
                "the line was not marked spent, so it would repeat all fight");

            CharacterVoice.ClearSpentLines();

            Assert.IsFalse(CharacterVoice.HasSpent("sheep", CharacterVoice.Line.LowHealth),
                "a new fight has to be able to say it again");
            yield break;
        }
    }
}

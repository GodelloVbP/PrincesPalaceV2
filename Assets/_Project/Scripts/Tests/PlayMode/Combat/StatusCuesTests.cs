using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.PlayModeTests
{
    // The status-application cue table: keyed by status type, every take a
    // clip that loads, harmful statuses silent.
    public class StatusCuesTests
    {
        [TestCase(StatusEffectType.Shielded, 3)]
        [TestCase(StatusEffectType.Protect, 3)]
        [TestCase(StatusEffectType.Regen, 3)]
        [TestCase(StatusEffectType.Poison, 0)]
        [TestCase(StatusEffectType.Stun, 0)]
        public void EachStatusHasItsTakes(StatusEffectType type, int takes)
        {
            Assert.AreEqual(takes, StatusCues.ClipsFor(type).Count);
        }

        [Test]
        public void EveryTakeLoads()
        {
            foreach (var type in new[] { StatusEffectType.Shielded, StatusEffectType.Regen })
            {
                foreach (var path in StatusCues.ClipsFor(type))
                {
                    Assert.IsNotNull(SoundController.ClipAt(path), path);
                }
            }
        }

        // ---- which beat chimes -------------------------------------------------

        private static ResolvedSkill Hit(StatusEffectType status) =>
            new ResolvedSkill("fixture", "Hex", "", "foe", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, 0, 0, false, 0, 10, false,
                null, SpellPresentation.None, 0,
                appliesStatus: status, statusMagnitude: 25, statusDuration: 2,
                playerSelectable: false);

        // The fixture fight: Shawn is the target of three casts from the foe.
        // Each cast's beat is observed in order, as the player does, and the
        // types that chimed on it are returned per beat.
        private static List<StatusEffectType[]> ChimesPerBeat(bool wardAtBegin, params StatusEffectType[] casts)
        {
            var shawn = new CombatantState("Shawn", true, 1000, 10, 20, 10);
            var foe = new CombatantState("Foe", false, 1000, 0, 0, 1);
            if (wardAtBegin) StatusEffects.ApplyWard(shawn.Statuses, 30, 3);

            var session = new FightSession(new CombatEncounter(new[] { shawn }, new[] { foe }),
                null, null, new SeededRandom(1)) { DamageVarianceRange = 0f };
            session.Begin();
            session.DrainBeats();

            var perBeat = new List<StatusEffectType[]>();
            foreach (var status in casts)
            {
                session.ResolveSkillForTest(foe, Hit(status), shawn);
                foreach (var beat in session.DrainBeats().Where(b => b.IsAction))
                {
                    var played = new List<StatusEffectType>();
                    StatusCues.Observe(beat.Snapshot, session.OpeningStatusesOf, played.Add);
                    perBeat.Add(played.ToArray());
                }
            }

            return perBeat;
        }

        [Test]
        public void AWardAppliedOnTheThirdBeatChimesOnTheThirdBeatOnly()
        {
            var chimes = ChimesPerBeat(false, StatusEffectType.Poison, StatusEffectType.Vulnerable,
                StatusEffectType.Protect);

            Assert.AreEqual(3, chimes.Count);
            CollectionAssert.IsEmpty(chimes[0]);
            CollectionAssert.IsEmpty(chimes[1]);
            CollectionAssert.AreEqual(new[] { StatusEffectType.Protect }, chimes[2]);
        }

        [Test]
        public void AStatusTheFirstActionAppliesChimesOnThatBeat()
        {
            var chimes = ChimesPerBeat(false, StatusEffectType.Regen);

            Assert.AreEqual(1, chimes.Count);
            CollectionAssert.AreEqual(new[] { StatusEffectType.Regen }, chimes[0]);
        }

        [Test]
        public void AWardThatWasThereAtBeginNeverChimes()
        {
            var chimes = ChimesPerBeat(true, StatusEffectType.Poison, StatusEffectType.Vulnerable);

            Assert.AreEqual(2, chimes.Count);
            CollectionAssert.IsEmpty(chimes[0]);
            CollectionAssert.IsEmpty(chimes[1]);
        }
    }
}

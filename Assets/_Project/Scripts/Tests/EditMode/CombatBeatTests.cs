using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    public class CombatBeatTests
    {
        private static CombatantState Fighter(string name, bool isPlayerSide, int maxHealth = 20) =>
            new CombatantState(name, isPlayerSide, maxHealth, 10, 5, 2, 5);

        // --- ImpactFraction -------------------------------------------------

        [Test]
        public void ImpactFraction_CountsFramesFromOne()
        {
            // "Frame 3 of 6 lands halfway through": three frames have shown, so
            // the impact is three sixths of the way in.
            Assert.AreEqual(0.5f, CombatBeat.ImpactFraction(3, 6), 0.0001f);
            Assert.AreEqual(1f / 6f, CombatBeat.ImpactFraction(1, 6), 0.0001f);
            Assert.AreEqual(1f, CombatBeat.ImpactFraction(6, 6), 0.0001f);
        }

        [Test]
        public void ImpactFraction_ClampsAnOutOfRangeFrame()
        {
            Assert.AreEqual(1f / 6f, CombatBeat.ImpactFraction(0, 6), 0.0001f);
            Assert.AreEqual(1f / 6f, CombatBeat.ImpactFraction(-4, 6), 0.0001f);
            Assert.AreEqual(1f, CombatBeat.ImpactFraction(99, 6), 0.0001f);
        }

        [Test]
        public void ImpactFraction_FallsBackToTheMidpointWithNoFrames()
        {
            // Art missing, or a folder that failed to load. The midpoint is
            // still nearer the truth than the end.
            Assert.AreEqual(0.5f, CombatBeat.ImpactFraction(3, 0), 0.0001f);
            Assert.AreEqual(0.5f, CombatBeat.ImpactFraction(3, -1), 0.0001f);
        }

        // --- Vitals ---------------------------------------------------------

        [Test]
        public void Vitals_CarryThreeNamedResources()
        {
            var v = new Vitals(12, 7, 3);
            Assert.AreEqual(12, v.Health);
            Assert.AreEqual(7, v.Mana);
            Assert.AreEqual(3, v.Signature);
        }

        [Test]
        public void Vitals_CompareByValue()
        {
            // Snapshots are dictionary values compared across beats; reference
            // equality would make every comparison false.
            Assert.AreEqual(new Vitals(1, 2, 3), new Vitals(1, 2, 3));
            Assert.AreNotEqual(new Vitals(1, 2, 3), new Vitals(1, 2, 4));
            Assert.AreEqual(new Vitals(1, 2, 3).GetHashCode(), new Vitals(1, 2, 3).GetHashCode());
        }

        // --- voice decisions ------------------------------------------------

        [Test]
        public void LowHealth_IsDecidedAtOrBelowThreeTenths()
        {
            // Pinned as literals rather than recomputed from LowHealthFraction:
            // the boundary is the whole behaviour, and a test that multiplied by
            // the same constant would agree with any threshold.
            Assert.IsTrue(VoiceThresholds.IsLowHealth(6, 20), "6 of 20 is exactly three tenths");
            Assert.IsTrue(VoiceThresholds.IsLowHealth(3, 20));
            Assert.IsFalse(VoiceThresholds.IsLowHealth(7, 20));
            Assert.IsFalse(VoiceThresholds.IsLowHealth(20, 20));
        }

        [Test]
        public void ADeadCombatantIsNotLowHealth_ItIsDown()
        {
            // Down is its own line. Reporting a corpse as "low on health" would
            // play the wrong take over a death.
            Assert.IsFalse(VoiceThresholds.IsLowHealth(0, 20));
            Assert.IsFalse(VoiceThresholds.IsLowHealth(-5, 20));
        }

        [Test]
        public void ZeroMaxHealth_IsNeverLow()
        {
            Assert.IsFalse(VoiceThresholds.IsLowHealth(0, 0), "degrades rather than dividing by zero");
        }

        // --- the beat itself -------------------------------------------------

        [Test]
        public void ABeatWithNoArt_HasNoSpellAnimation()
        {
            // The graceful-degradation posture: a spell with no authored art
            // still resolves, it just has nothing to play.
            var beat = new CombatBeat();
            Assert.IsFalse(beat.HasSpellAnimation);

            beat.VfxPath = "Spells/frost_flare";
            Assert.IsFalse(beat.HasSpellAnimation, "a path with no duration is not an animation");

            beat.VfxSeconds = 0.6f;
            Assert.IsTrue(beat.HasSpellAnimation);
        }

        [Test]
        public void ABeatCarriesItsOwnSnapshot_NotALiveReference()
        {
            // The reason the snapshot exists: by the time this beat plays, later
            // actions in the same chain have already landed on live state.
            var target = Fighter("Target", false);
            var beat = new CombatBeat
            {
                Actor = Fighter("Actor", true),
                Target = target,
                Amount = 5,
                Snapshot = new System.Collections.Generic.Dictionary<CombatantState, Vitals>
                {
                    [target] = new Vitals(target.CurrentHealth, target.CurrentMana, 0),
                },
            };

            int recorded = beat.Snapshot[target].Health;
            target.CurrentHealth = 1;

            Assert.AreEqual(20, recorded, "the snapshot must not move when live state does");
            Assert.AreEqual(1, target.CurrentHealth);
        }
    }
}

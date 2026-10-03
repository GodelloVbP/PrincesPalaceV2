using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Tests
{
    // The beat records the status data with the vitals, so the HUD paints a
    // status on the beat that applied it. The Core paint maps Vitals.Statuses
    // through StatusRowsFrom onto PaintStatusRow, so the recording is what is pinned here.
    public class BeatStatusSnapshotTests
    {
        private static ResolvedSkill Hit(StatusEffectType status) =>
            new ResolvedSkill("fixture", "Hex", "", "foe", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, 0, 0, false, 0, 10, false,
                null, SpellPresentation.None, 0,
                appliesStatus: status, statusMagnitude: 25, statusDuration: 2,
                playerSelectable: false);

        [Test]
        public void AStatusACastAppliesIsInThatBeatsSnapshotButNotItsPreSnapshot()
        {
            var shawn = new CombatantState("Shawn", true, 1000, 10, 20, 10);
            var foe = new CombatantState("Foe", false, 1000, 0, 0, 1);
            var session = new FightSession(new CombatEncounter(new[] { shawn }, new[] { foe }),
                null, null, new SeededRandom(1)) { DamageVarianceRange = 0f };
            session.Begin();
            session.DrainBeats();

            session.ResolveSkillForTest(foe, Hit(StatusEffectType.Vulnerable), shawn);

            var beat = session.DrainBeats().Last(b => b.IsAction);
            Assert.IsTrue(FightHudModel.StatusRowsFrom(beat.Snapshot[shawn].Statuses).Any(r => r.Code == "VLN"));
            Assert.IsFalse(FightHudModel.StatusRowsFrom(beat.PreSnapshot[shawn].Statuses).Any(r => r.Code == "VLN"));
        }
    }
}

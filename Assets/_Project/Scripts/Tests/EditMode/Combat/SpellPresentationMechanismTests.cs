using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // WHAT THE SESSION TELLS THE PRESENTATION about a cast that moves figures
    // or spends a status: the facts the `counterpart` placements, the drag a
    // `follow` layer rides and the `whenConsumed` gate read. Literals
    // throughout; the layer vocabulary itself is pinned in
    // SpellLayerMechanismRulesTests.
    public class SpellPresentationMechanismTests
    {
        private static ResolvedSkill Passage() =>
            new ResolvedSkill("palace_passage", "Palace Passage", "", "sheep", 1,
                SkillEffect.SwapAllies, SkillTargeting.SingleAlly, 7, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0, cooldownTurns: 3, freeAction: true);

        private static ResolvedSkill Bite() =>
            new ResolvedSkill("vipers_bite", "Viper's Bite", "", "hero", 1,
                SkillEffect.DamageSingle, SkillTargeting.SingleEnemy, 7, 0, false, 0, 0, false,
                new[] { new DamageInstance(DamageType.Poison, 3) }, SpellPresentation.None, 0,
                appliesStatus: StatusEffectType.Poison, statusMagnitude: 3, statusDuration: 3);

        private static CombatantState Member(string name, int speed) =>
            new CombatantState(name, true, 300, 40, 20, speed);

        private static (FightSession session, CombatantState caster, CombatantState mid) SwapFight()
        {
            var caster = Member("Caster", 30);
            var mid = Member("Mid", 12);
            var rear = Member("Rear", 6);
            var kits = new List<PlayerKit>
            {
                new PlayerKit("sheep", CharacterRole.Support, new[] { Passage() }, null, DamageType.Physical),
            };
            var session = new FightSession(
                new CombatEncounter(new[] { caster, mid, rear },
                    new[] { new CombatantState("Foe", false, 100000, 10, 1, 1) }),
                kits, null, new SeededRandom(4)) { DamageVarianceRange = 0f };
            return (session, caster, mid);
        }

        [Test]
        public void ASwapIsPaintedAtTheImpact_AndNamesTheFigureItTradedWith()
        {
            var (session, caster, mid) = SwapFight();

            Assert.IsTrue(session.CastSkillOnPicks(0, new[] { caster, mid }));

            var beat = session.DrainBeats().Single(b => b.IsAction);
            Assert.IsNotNull(beat.PreFormation, "playback opens on where everybody stood");
            Assert.AreSame(caster, beat.PreFormation.PartyField[0]);
            Assert.AreSame(mid, beat.Formation.PartyField[0], "after: Mid took the front seat");
            Assert.AreEqual(0f, beat.PlacementHoldSeconds, "the doors hide the move: no hold after the cue");
            Assert.AreSame(mid, beat.Counterpart);
        }

        [Test]
        public void AStepIntoAnEmptySeatHasNoCounterpart()
        {
            var shawn = Member("Shawn", 30);
            var kits = new List<PlayerKit>
            {
                new PlayerKit("sheep", CharacterRole.Support, new[] { Passage() }, null, DamageType.Physical),
            };
            var session = new FightSession(
                new CombatEncounter(new[] { shawn }, new[] { new CombatantState("Foe", false, 100000, 10, 1, 1) }),
                kits, null, new SeededRandom(4)) { DamageVarianceRange = 0f };

            Assert.IsTrue(session.CastSkillToSeat(0, shawn, 1));

            var beat = session.DrainBeats().Single(b => b.IsAction);
            Assert.IsNotNull(beat.PreFormation);
            Assert.IsNull(beat.Counterpart, "nobody stood in the middle seat");
        }

        [Test]
        public void APullHoldsTheChainsForPullHoldSeconds()
        {
            Assert.AreEqual(0.3f, CombatBeat.PullHoldSeconds, 0.00001f);
        }

        private static (FightSession session, CombatantState foe) BiteFight()
        {
            var hero = new CombatantState("Hero", true, 500, 50, 40, 10);
            var foe = new CombatantState("Foe", false, 1000, 10, 5, 1);
            var kit = new PlayerKit("hero", CharacterRole.Tank, new[] { Bite() }, null, null);
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { kit }, null, new SeededRandom(3)) { DamageVarianceRange = 0f };
            return (session, foe);
        }

        [Test]
        public void ABiteOnAPoisonedTargetRecordsThePoisonItSpent()
        {
            var (session, foe) = BiteFight();
            foe.Statuses.Add(new ActiveStatus(StatusEffectType.Poison, 4, 5));

            session.CastSkill(0, foe);

            var beat = session.DrainBeats().Single(b => b.IsAction);
            CollectionAssert.AreEqual(new[] { StatusEffectType.Poison }, beat.ConsumedStatuses);
        }

        // The detonation is dealt through the ledger but is not part of Amount,
        // so the stage showed "-3" over a bar that fell by 23. The beat now
        // carries it for the view's second number.
        [Test]
        public void ABiteRecordsHowMuchTheDetonationDealtApartFromItsOwnDamage()
        {
            var (session, foe) = BiteFight();
            foe.Statuses.Add(new ActiveStatus(StatusEffectType.Poison, 4, 5));
            int before = foe.CurrentHealth;

            session.CastSkill(0, foe);

            var beat = session.DrainBeats().Single(b => b.IsAction);
            Assert.AreEqual(20, beat.DetonatedAmount, "4 x 5 remaining ticks");
            Assert.AreEqual(before - beat.Amount - 20, foe.CurrentHealth, "the two numbers are the whole loss");
        }

        [Test]
        public void ABiteOnACleanTargetRecordsNoDetonation()
        {
            var (session, foe) = BiteFight();
            session.CastSkill(0, foe);
            Assert.AreEqual(0, session.DrainBeats().Single(b => b.IsAction).DetonatedAmount);
        }

        [Test]
        public void ABiteOnACleanTargetRecordsNothingSpent()
        {
            var (session, foe) = BiteFight();

            session.CastSkill(0, foe);

            var beat = session.DrainBeats().Single(b => b.IsAction);
            Assert.AreEqual(0, beat.ConsumedStatuses.Count);
        }
    }
}

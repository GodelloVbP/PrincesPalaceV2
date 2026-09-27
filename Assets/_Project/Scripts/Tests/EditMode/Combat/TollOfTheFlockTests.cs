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
    // Bell in the Fog's two relics (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 1.4,
    // 2.4, 2.12, M3). Fixture relics; the relics.json rows are M7a's.
    //
    // Toll of the Flock's arithmetic at Attack 400, against defence 0 and a
    // neutral affinity: 15% = 60; transformed with no attack bonus, x1.5 = 90;
    // Black Ram's +50% makes Attack 600, and 600 x 15% x 1.5 = 135 = 2.25 x 60.
    public class TollOfTheFlockTests
    {
        private const string Shawn = "sheep";

        private static ResolvedRelic Relic(RelicEffect effect) =>
            new ResolvedRelic(effect.ToString(), effect.ToString(), "", effect, 0, bearer: Shawn, draftable: false);

        private static ResolvedRelic Plain(RelicEffect effect) =>
            new ResolvedRelic(effect.ToString(), effect.ToString(), "", effect, 0);

        private static ResolvedEnemy Source(string id) =>
            new ResolvedEnemy(id, id, new StatBlock(), 0, 0, false, ElementalAffinity.Neutral, 0);

        private static CombatantState Foe(string name, int health = 999999, int speed = 10) =>
            new CombatantState(name, false, health, 0, 0, speed) { PhysicalDefense = 0, MagicalDefense = 0 };

        private static CombatantState Hero(int attack = 400, int speed = 10, int health = 100000, string name = "Shawn") =>
            new CombatantState(name, true, health, 0, attack, speed) { PhysicalDefense = 0, MagicalDefense = 0 };

        private static FightSession Fight(IReadOnlyList<CombatantState> party, IReadOnlyList<PlayerKit> kits,
                                          params CombatantState[] foes)
        {
            var encounter = new CombatEncounter(party, foes);
            var session = new FightSession(encounter, kits.ToList(),
                foes.Select(f => new EnemyKit(Source(f.Name), false)).ToList(), new SeededRandom(5))
            {
                DamageVarianceRange = 0f,
            };
            session.Begin();
            return session;
        }

        private static FightSession Solo(CombatantState hero, IEnumerable<ResolvedRelic> relics,
                                         IReadOnlyList<ResolvedSkill> skills, params CombatantState[] foes) =>
            Fight(new[] { hero },
                new[] { new PlayerKit(Shawn, CharacterRole.Tank, skills, relics.ToList(), null) }, foes);

        private static bool IsFlock(CombatBeat beat) => beat.Cause == BeatCause.RelicTrigger;

        // One plain swing at the front enemy by whoever holds the turn; returns
        // the beats it produced.
        private static IReadOnlyList<CombatBeat> Swing(FightSession session)
        {
            Assert.IsTrue(session.IsPlayerTurn, "fixture: control is with the party");
            session.ExecuteAttack(session.Encounter.FrontEnemy);
            return session.DrainBeats();
        }

        // ---- when it fires -----------------------------------------------------------

        [Test]
        public void ItChargesOnTheThirdSixthAndNinthOpenedTurn()
        {
            var hero = Hero();
            var session = Solo(hero, new[] { Relic(RelicEffect.TollOfTheFlock) }, null, Foe("A"), Foe("B"));
            session.DrainBeats();
            Assert.AreEqual(1, session.OpenedTurnsOf(hero), "fixture: Begin opened turn 1");

            var firedOn = new List<int>();
            while (session.OpenedTurnsOf(hero) < 10)
            {
                if (Swing(session).Any(IsFlock)) firedOn.Add(session.OpenedTurnsOf(hero));
            }

            CollectionAssert.AreEqual(new[] { 3, 6, 9 }, firedOn, "not 2, not 4");
        }

        [Test]
        public void OneChargeHitsEveryLivingEnemyForFifteenPercentOfAttack()
        {
            var hero = Hero();
            var a = Foe("A");
            var b = Foe("B");
            var dead = Foe("Dead");
            var session = Solo(hero, new[] { Relic(RelicEffect.TollOfTheFlock) }, null, a, b, dead);
            dead.CurrentHealth = 0;

            Swing(session);
            var beat = Swing(session).Single(IsFlock);

            Assert.AreEqual(hero, beat.Actor, "the bearer's damage");
            Assert.IsFalse(beat.IsAction, "but not an action he took");
            CollectionAssert.AreEqual(new[] { a, b }, beat.Results.Select(r => r.Target).ToList(), "the living only");
            CollectionAssert.AreEqual(new[] { 60, 60 }, beat.Results.Select(r => r.Amount).ToList());
            CollectionAssert.Contains(beat.Messages, FightSession.TollOfTheFlockLine);

            // His own swings went into A, so B's loss is the flock alone -- and
            // everything either lost is booked as his damage dealt.
            Assert.AreEqual(60, b.MaxHealth - b.CurrentHealth);
            Assert.AreEqual((a.MaxHealth - a.CurrentHealth) + 60, session.Ledger.For(Shawn).TotalDealt);
        }

        [Test]
        public void TransformedItIsOneAndAHalfTimesOnTheSameAttack()
        {
            var hero = Hero();
            Transformation.Enter(hero, "a form", 99, 0, 0, 0);
            Assert.AreEqual(400, hero.Attack, "fixture: this form adds no attack");

            var session = Solo(hero, new[] { Relic(RelicEffect.TollOfTheFlock) }, null, Foe("A"));
            Swing(session);
            var beat = Swing(session).Single(IsFlock);

            Assert.AreEqual(90, beat.Results.Single().Amount, "60 x 1.5");
        }

        [Test]
        public void WithBlackRamsOwnBonusItIsTwoAndAQuarterTimes()
        {
            var hero = Hero();
            Transformation.Enter(hero, "the Black Ram", 99, 50, 0, 0);
            Assert.AreEqual(600, hero.Attack, "fixture: Black Ram's +50% attack");

            var session = Solo(hero, new[] { Relic(RelicEffect.TollOfTheFlock) }, null, Foe("A"));
            Swing(session);
            var beat = Swing(session).Single(IsFlock);

            Assert.AreEqual(135, beat.Results.Single().Amount, "600 x 15% x 1.5 = 2.25 x 60");
        }

        [Test]
        public void ABloodlustActionIsNotAnOpenedTurn()
        {
            var hero = Hero();
            var frail = Foe("Frail", health: 1);
            var session = Solo(hero,
                new[] { Relic(RelicEffect.TollOfTheFlock), Plain(RelicEffect.Bloodlust) }, null, frail, Foe("Tank"));
            session.DrainBeats();

            var first = Swing(session);
            Assert.IsFalse(frail.IsAlive, "fixture: the swing killed");
            Assert.IsTrue(first.SelectMany(b => b.Messages).Any(m => m.Contains("Bloodlust")),
                "fixture: Bloodlust granted the extra action");
            Assert.AreEqual(1, session.OpenedTurnsOf(hero), "an extra action inside turn 1 opens nothing");

            Assert.IsFalse(Swing(session).Any(IsFlock), "the extra action ends; turn 2 opens");
            Assert.AreEqual(2, session.OpenedTurnsOf(hero));
            Assert.IsTrue(Swing(session).Any(IsFlock), "turn 3 opens and charges");
            Assert.AreEqual(3, session.OpenedTurnsOf(hero));
        }

        // Rate is sqrt(speed / 10): speed 40 charges at twice speed 10's rate.
        [TestCase(10, 4)]
        [TestCase(40, 8)]
        public void TwiceTheRateChargesTwiceAsOften(int speed, int charges)
        {
            var hero = Hero(speed: speed);
            var session = Solo(hero, new[] { Relic(RelicEffect.TollOfTheFlock) }, null, Foe("A"));

            int fired = 0;
            while (session.Round < 13)
            {
                fired += Swing(session).Count(IsFlock);
            }

            Assert.AreEqual(charges, fired);
        }

        [Test]
        public void APoisonTickThatDropsHimSkipsTheCharge()
        {
            var hero = Hero(health: 1000);
            var mate = Hero(name: "Bjorn");
            var session = Fight(new[] { hero, mate },
                new[]
                {
                    new PlayerKit(Shawn, CharacterRole.Tank, null, new List<ResolvedRelic> { Relic(RelicEffect.TollOfTheFlock) }, null),
                    new PlayerKit("bear", CharacterRole.Tank, null, null, null),
                },
                Foe("A"));

            while (session.OpenedTurnsOf(hero) < 2) Swing(session);
            hero.CurrentHealth = 5;
            session.ApplyStatusToForTest(hero, StatusEffectType.Poison, 50, 3);

            var beats = new List<CombatBeat>();
            for (int i = 0; i < 20 && session.OpenedTurnsOf(hero) < 3; i++) beats.AddRange(Swing(session));

            Assert.AreEqual(3, session.OpenedTurnsOf(hero), "fixture: turn 3 opened");
            Assert.IsFalse(hero.IsAlive, "fixture: the tick at the top of turn 3 dropped him");
            Assert.IsFalse(beats.Any(IsFlock), "no charge from a body");
            Assert.IsFalse(beats.SelectMany(b => b.Messages).Contains(FightSession.TollOfTheFlockLine));
        }

        // Tank and Tank2 in front take his swings; the frail one at the back is
        // the flock's (60 into 50). Bloodlust in one case, Trample in the other:
        // both key off the rider flag a Nobody-credited kill never raises.
        [TestCase(true)]
        [TestCase(false)]
        public void AFlockKillArmsNoExtraActionAndScoresNoKill(bool bloodlust)
        {
            var hero = Hero();
            var relics = new List<ResolvedRelic> { Relic(RelicEffect.TollOfTheFlock) };
            if (bloodlust) relics.Add(Plain(RelicEffect.Bloodlust));
            else hero.Talents = new TalentEffectSet(new[] { new TalentEffect(TalentEffectType.ExtraAttackOnKill, 1) });

            var tank = Foe("Tank");
            var frail = Foe("Frail", health: 50);
            var session = Solo(hero, relics, null, tank, Foe("Tank2"), frail);
            session.DrainBeats();

            Swing(session);
            var turnThree = Swing(session);

            Assert.IsTrue(turnThree.Any(IsFlock), "fixture: the flock charged");
            Assert.IsFalse(frail.IsAlive, "fixture: and killed the frail one");
            var said = turnThree.SelectMany(b => b.Messages).ToList();
            Assert.IsFalse(said.Any(m => m.Contains("Bloodlust") || m.Contains("tramples")));
            Assert.AreEqual(0, session.Encounter.PendingExtraTurns(hero));
            Assert.AreEqual(3, session.OpenedTurnsOf(hero));
            Assert.AreEqual(0, session.Ledger.For(Shawn).Kills, "credited to nobody");
            Assert.AreEqual(1, session.Ledger.For("Frail").TimesDowned, "but the body went down");

            // Not spent by it either: his own kill is still rewarded, as an
            // extra action (no new turn opens).
            tank.CurrentHealth = 1;
            var own = Swing(session);
            Assert.IsTrue(own.SelectMany(b => b.Messages).Any(m => m.Contains(bloodlust ? "Bloodlust" : "tramples")),
                "his own kill still earns it");
            Assert.AreEqual(3, session.OpenedTurnsOf(hero));
        }

        [Test]
        public void AFlockKillOfTheLastEnemyEndsTheFightBeforeHeActs()
        {
            var hero = Hero();
            var tank = Foe("Tank");
            var frail = Foe("Frail");
            var session = Solo(hero, new[] { Relic(RelicEffect.TollOfTheFlock) }, null, tank, frail);

            Swing(session);
            Assert.AreEqual(2, session.OpenedTurnsOf(hero));

            // His turn-2 swing kills the tank; turn 3 opens and the flock
            // (60) finishes the frail one.
            tank.CurrentHealth = 1;
            frail.CurrentHealth = 60;
            Swing(session);

            Assert.AreEqual(3, session.OpenedTurnsOf(hero));
            Assert.IsTrue(session.IsOver);
            Assert.AreEqual(FightEndReason.Defeated, session.EndReason);
            Assert.IsTrue(session.Payout.HasValue, "settled");
        }

        [Test]
        public void ANonBearerCarryingItGetsNoCharge()
        {
            var bear = Hero(name: "Bjorn");
            var session = Fight(new[] { bear },
                new[] { new PlayerKit("bear", CharacterRole.Tank, null, new List<ResolvedRelic> { Relic(RelicEffect.TollOfTheFlock) }, null) },
                Foe("A"));

            var beats = new List<CombatBeat>();
            while (session.OpenedTurnsOf(bear) < 6) beats.AddRange(Swing(session));

            Assert.IsFalse(beats.Any(IsFlock));
        }

        [Test]
        public void TheRelicsAuthoredPresentationRidesTheBeat()
        {
            var relic = Relic(RelicEffect.TollOfTheFlock);
            relic.Vfx = new SpellPresentation { path = "Vfx/ghost_flock", sfxPath = "Audio/Sfx/toll" };
            var session = Solo(Hero(), new[] { relic }, null, Foe("A"));

            Swing(session);
            var beat = Swing(session).Single(IsFlock);

            Assert.AreEqual("Vfx/ghost_flock", beat.Vfx.path);
            Assert.AreEqual("Audio/Sfx/toll", beat.Vfx.sfxPath);
        }

        // ---- Bellwether's Bell -----------------------------------------------------------

        private static ResolvedSkill BlackRamMode() =>
            new ResolvedSkill("black_ram_mode", "Black Ram Mode", "", Shawn, 1, SkillEffect.Transform,
                SkillEntryResolver.DefaultTargetingFor(SkillEffect.Transform),
                0, 0, false, 100, 0, false, null, SpellPresentation.None, 0,
                transform: new TransformGrant { displayName = "the Black Ram", turns = 3, attackPercent = 50 });

        private static string EntryLine(bool bell, int wrath)
        {
            var hero = Hero();
            if (wrath > 0)
            {
                hero.Talents = new TalentEffectSet(new[] { new TalentEffect(TalentEffectType.TransformDurationBonus, wrath) });
            }

            var relics = bell ? new[] { Relic(RelicEffect.BellwethersBell) } : new ResolvedRelic[0];
            var session = Solo(hero, relics, new List<ResolvedSkill> { BlackRamMode() }, Foe("A", speed: 1));
            session.DrainBeats();

            session.CastSkill(0, null);
            return session.DrainBeats().SelectMany(b => b.Messages).Single(m => m.Contains("becomes the Black Ram"));
        }

        [Test]
        public void WithoutTheBellBlackRamModeLastsItsAuthoredThree() =>
            Assert.AreEqual("Shawn becomes the Black Ram for 3 turns!", EntryLine(bell: false, wrath: 0));

        [Test]
        public void TheBellAddsOneTurn() =>
            Assert.AreEqual("Shawn becomes the Black Ram for 4 turns!", EntryLine(bell: true, wrath: 0));

        [Test]
        public void TheBellAddsToWrath()
        {
            Assert.AreEqual("Shawn becomes the Black Ram for 4 turns!", EntryLine(bell: false, wrath: 1),
                "fixture: Wrath alone is +1");
            Assert.AreEqual("Shawn becomes the Black Ram for 5 turns!", EntryLine(bell: true, wrath: 1));
        }

        [Test]
        public void TheBellOnANonBearerAddsNothing()
        {
            var bear = Hero(name: "Bjorn");
            var session = Fight(new[] { bear },
                new[]
                {
                    new PlayerKit("bear", CharacterRole.Tank, new List<ResolvedSkill> { BlackRamMode() },
                        new List<ResolvedRelic> { Relic(RelicEffect.BellwethersBell) }, null),
                },
                Foe("A", speed: 1));
            session.DrainBeats();

            session.CastSkill(0, null);

            CollectionAssert.Contains(session.DrainBeats().SelectMany(b => b.Messages).ToList(),
                "Bjorn becomes the Black Ram for 3 turns!");
        }
    }
}

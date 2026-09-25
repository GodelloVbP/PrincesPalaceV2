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
    // The Cold One (docs/PLAN_PETTING_ZOO.md, "Cold one"): while the event's
    // fillSpecialPool buff is in force, a player character's special pool --
    // signature if they have one, else primary -- is full at the moment they
    // can act. The fill is the LAST step of OpenTurnFor; an extra action
    // (ReopenTurnFor) refills nothing.
    //
    // The session only sees a flag (FillsSpecialPoolAtTurnStart); which run
    // and which leg set it is FightEncounterAdapter's business, pinned by
    // ColdOneReachesCombatTests.
    public class ColdOneFillTests
    {
        // The hero opens on initiative and keeps getting turns back quickly;
        // the foe is slow, sturdy and barely hurts.
        private static CombatantState Hero(int maxMana = 100, int speed = 50) =>
            new CombatantState("Hero", true, 500, maxMana, 20, speed);

        private static CombatantState Foe(string name, int health, int maxMana = 10, int speed = 1) =>
            new CombatantState(name, false, health, maxMana, 1, speed);

        private static FightSession Session(bool buffed, CombatantState hero, PlayerKit kit,
            params CombatantState[] foes)
        {
            var session = new FightSession(new CombatEncounter(new[] { hero }, foes),
                new List<PlayerKit> { kit ?? new PlayerKit("hero", CharacterRole.Tank, null, null, null) },
                null, new SeededRandom(3))
            {
                DamageVarianceRange = 0f,
                FillsSpecialPoolAtTurnStart = buffed,
            };
            session.Begin();
            return session;
        }

        private static ResourcePool Wool(int current, bool absorbs = false) =>
            new ResourcePool("wool", "Wool", 10, 0, 0, 0, absorbPerPoint: 1, absorbsDamage: absorbs) { Current = current };

        private static ResourcePool Fury(int current, int gainOnDamageTaken = 0) =>
            new ResourcePool("fury", "Fury", 100, 0, gainOnAttack: 0, gainOnDamageTaken: gainOnDamageTaken)
                { Current = current };

        private static ResolvedSkill Bolt(int manaCost) =>
            new ResolvedSkill("bolt", "Bolt", "", "hero", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, manaCost, 0, false, 100, 0, false,
                null, SpellPresentation.None, 0);

        // ---- which pool, and full at control ------------------------------------

        [Test]
        public void ShawnsWoolIsFullAtControl_AndHisManaIsLeftAlone()
        {
            var hero = Hero();
            hero.PrimaryPool.Current = 40;
            hero.SignaturePool = Wool(2);

            var session = Session(true, hero, null, Foe("Foe", 100000));

            Assert.IsTrue(session.IsPlayerTurn, "fixture: the hero holds the opening turn");
            Assert.AreEqual(10, hero.SignaturePool.Current, "the signature pool is the special pool");
            Assert.AreEqual(40, hero.PrimaryPool.Current, "with a signature pool, the primary is not filled");
        }

        [Test]
        public void BjornsFuryIsFullAtControl()
        {
            var hero = new CombatantState("Bjorn", true, 500, Fury(0), 20, 50);

            Session(true, hero, null, Foe("Foe", 100000));

            Assert.AreEqual(100, hero.PrimaryPool.Current, "no signature pool: the primary (fury) is filled");
        }

        [Test]
        public void OdettesManaIsFullAtControl()
        {
            var hero = Hero();
            hero.PrimaryPool.Current = 30;

            Session(true, hero, null, Foe("Foe", 100000));

            Assert.AreEqual(100, hero.PrimaryPool.Current);
        }

        [Test]
        public void WithoutTheBuffNothingIsFilled()
        {
            var hero = Hero();
            hero.PrimaryPool.Current = 30;

            Session(false, hero, null, Foe("Foe", 100000));

            Assert.AreEqual(30, hero.PrimaryPool.Current, "control: mana here has no per-turn regen");
        }

        // ---- the fill comes last --------------------------------------------------

        // FightTuning.RunicWardConversionRate (0.25) x 40 unspent mana = 10 ward
        // points, buffed or not: the conversion reads the pre-fill pool.
        [TestCase(false)]
        [TestCase(true)]
        public void TheRunicWardIsSizedOffThePreFillMana(bool buffed)
        {
            var hero = Hero();
            hero.PrimaryPool.Current = 40;
            hero.ModifierEffects = new ModifierEffectSet(new[]
                { new ModifierEffect(ModifierEffectType.ManaToWardOnTurnStartPercent, 0) });

            Session(buffed, hero, null, Foe("Foe", 100000));

            var ward = hero.Statuses.FirstOrDefault(s => s.Type == StatusEffectType.Shielded);
            Assert.IsNotNull(ward, "fixture: Runic converts at turn start");
            Assert.AreEqual(10, ward.Magnitude, "the ward is the unbuffed 10, not 25");
            Assert.AreEqual(buffed ? 100 : 40, hero.PrimaryPool.Current);
        }

        // A signature pool that soaks damage is the pool a turn-start poison
        // tick can actually take from: 5 poison eats 5 wool. The fill runs
        // after the tick, so the wool is full when control arrives.
        [TestCase(false, 5)]
        [TestCase(true, 10)]
        public void ThePoolIsFullAfterAPoisonTickAtTurnStart(bool buffed, int woolAtControl)
        {
            var hero = Hero();
            hero.SignaturePool = Wool(10, absorbs: true);
            StatusEffects.Apply(hero.Statuses, StatusEffectType.Poison, 5, 3, null);

            Session(buffed, hero, null, Foe("Foe", 100000));

            Assert.AreEqual(500, hero.CurrentHealth, "fixture: the wool ate the whole tick");
            Assert.AreEqual(woolAtControl, hero.SignaturePool.Current);
        }

        // ---- an extra action re-pays nothing ------------------------------------

        [Test]
        public void ABloodlustExtraActionDoesNotRefill()
        {
            var hero = Hero();
            var kit = new PlayerKit("hero", CharacterRole.Tank, new[] { Bolt(20) },
                new List<ResolvedRelic> { new ResolvedRelic("bloodlust", "Bloodlust", "", RelicEffect.Bloodlust, 0) },
                null);
            var weak = Foe("Weak", 1);
            var session = Session(true, hero, kit, weak, Foe("Tank", 100000));
            Assert.AreEqual(100, hero.PrimaryPool.Current, "fixture: full at control");

            Assert.IsTrue(session.CastSkill(Bolt(20), weak), "fixture: the cast goes off");

            Assert.IsFalse(weak.IsAlive, "fixture: the bolt killed");
            Assert.IsTrue(session.DrainBeats().SelectMany(b => b.Messages).Any(m => m.Contains("Bloodlust")),
                "fixture: Bloodlust granted the extra action");
            Assert.AreEqual(80, hero.PrimaryPool.Current, "the extra action is the same turn: no refill");
        }

        [Test]
        public void ATrampleExtraActionDoesNotRefill()
        {
            var hero = Hero();
            hero.Talents = new TalentEffectSet(new[] { new TalentEffect(TalentEffectType.ExtraAttackOnKill, 1) });
            var kit = new PlayerKit("hero", CharacterRole.Tank, new[] { Bolt(20) }, null, null);
            var weak = Foe("Weak", 1);
            var session = Session(true, hero, kit, weak, Foe("Tank", 100000));

            Assert.IsTrue(session.CastSkill(Bolt(20), weak), "fixture: the cast goes off");

            Assert.IsFalse(weak.IsAlive, "fixture: the bolt killed");
            Assert.IsTrue(session.DrainBeats().SelectMany(b => b.Messages).Any(m => m.Contains("tramples")),
                "fixture: Trample granted the extra action");
            Assert.AreEqual(80, hero.PrimaryPool.Current, "the extra action is the same turn: no refill");
        }

        // ACCEPTED INTERACTION (owner 2026-09-25), pinned so it stays visible:
        // ReopenTurnFor re-runs the Runic conversion on the mana left of the
        // turn's fill. Opening: 40 mana -> ward 10, then the fill. The bolt
        // costs 20 and kills; the Bloodlust action converts what is left.
        //   buffed:   100 - 20 = 80 -> +20, total 30
        //   unbuffed:  40 - 20 = 20 -> +5,  total 15
        [TestCase(true, 30)]
        [TestCase(false, 15)]
        public void AnExtraActionsRunicWardReadsWhatIsLeftOfTheFill(bool buffed, int wardTotal)
        {
            var hero = Hero();
            hero.PrimaryPool.Current = 40;
            hero.ModifierEffects = new ModifierEffectSet(new[]
                { new ModifierEffect(ModifierEffectType.ManaToWardOnTurnStartPercent, 0) });
            var kit = new PlayerKit("hero", CharacterRole.Tank, new[] { Bolt(20) },
                new List<ResolvedRelic> { new ResolvedRelic("bloodlust", "Bloodlust", "", RelicEffect.Bloodlust, 0) },
                null);
            var weak = Foe("Weak", 1);
            var session = Session(buffed, hero, kit, weak, Foe("Tank", 100000));
            Assert.AreEqual(10, StatusEffects.WardPoints(hero), "fixture: the opening ward reads the pre-fill 40");

            Assert.IsTrue(session.CastSkill(Bolt(20), weak), "fixture: the cast goes off");
            Assert.IsFalse(weak.IsAlive, "fixture: the bolt killed");

            Assert.AreEqual(wardTotal, StatusEffects.WardPoints(hero));
        }

        // ---- the next real turn does refill -------------------------------------

        [Test]
        public void TheNextRealTurnFillsAgain()
        {
            var hero = Hero();
            var kit = new PlayerKit("hero", CharacterRole.Tank, new[] { Bolt(20) }, null, null);
            var tank = Foe("Tank", 100000);
            var session = Session(true, hero, kit, tank);

            Assert.IsTrue(session.CastSkill(Bolt(20), tank), "fixture: the cast goes off");

            Assert.IsTrue(session.IsPlayerTurn, "fixture: control is back with the hero");
            Assert.AreEqual(100, hero.PrimaryPool.Current);
        }

        // ---- enemies ---------------------------------------------------------------

        [Test]
        public void EnemiesAreNeverFilled()
        {
            // 10 against 9: the hero opens, and the foe's turn comes next.
            var hero = Hero(speed: 10);
            var foe = Foe("Foe", 100000, maxMana: 50, speed: 9);
            foe.PrimaryPool.Current = 0;
            var session = Session(true, hero, null, foe);

            session.ExecuteAttack(foe);

            Assert.IsTrue(session.DrainBeats().Any(b => b.Actor == foe), "fixture: the foe's turn opened and it acted");
            Assert.AreEqual(0, foe.PrimaryPool.Current);
        }
    }
}

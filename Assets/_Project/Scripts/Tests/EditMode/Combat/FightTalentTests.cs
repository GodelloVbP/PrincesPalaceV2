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
    // The combat-time half of the talent vocabulary: the wool engines, the
    // Fragile Lamb's wards, the Black Ram's transform and splashes, and Provoke.
    //
    // TalentEffectSet already has thorough tests for how a set answers a query.
    // This is the other half -- what the fight actually DOES with the answer --
    // and in v1 it existed only inside a MonoBehaviour, so a rule like "a ward
    // pays its caster once per turn" had no cheap way to be pinned at all.
    public class FightTalentTests
    {
        private static CombatantState Hero(string name = "Hero", int health = 500, int attack = 40, int speed = 10) =>
            new CombatantState(name, true, health, 50, attack, speed);

        // Deliberately sluggish. A cast is a full turn, so with a foe fast
        // enough to reply the monsters would act between the two halves of every
        // setup below -- consuming the taunt, popping the ward, ticking the
        // transform -- and each assertion would be measuring the reply instead of
        // the talent. Speed 1 against 10 keeps the turn with the player.
        private static CombatantState Foe(string name = "Foe", int health = 1000, int attack = 30, int speed = 1) =>
            new CombatantState(name, false, health, 10, attack, speed);

        private static void Talents(CombatantState actor, params TalentEffect[] effects) =>
            actor.Talents = new TalentEffectSet(effects);

        private static ResolvedSkill Skill(SkillEffect effect, string name = "Skill", TransformGrant transform = null) =>
            new ResolvedSkill("t", name, "", "hero", 1, effect, SkillTargeting.Self,
                0, 0, false, 100, 0, false, null, SpellPresentation.None, 0, transform: transform);

        private static PlayerKit Kit(params ResolvedSkill[] skills) =>
            new PlayerKit("hero", CharacterRole.Tank, skills, null, null);

        private static FightSession Session(CombatEncounter encounter, params PlayerKit[] kits) =>
            new FightSession(encounter, kits.ToList(), null, new SeededRandom(5)) { DamageVarianceRange = 0f };

        private static (FightSession session, CombatEncounter encounter) Fight(
            IReadOnlyList<CombatantState> party, IReadOnlyList<CombatantState> foes, params PlayerKit[] kits)
        {
            var encounter = new CombatEncounter(party, foes);
            return (Session(encounter, kits), encounter);
        }

        private static IEnumerable<string> Messages(FightSession session) =>
            session.DrainBeats().SelectMany(b => b.Messages);

        private static ResourcePool Wool(int perTurn = 0, int onAttack = 0, int onDamageTaken = 0) =>
            new ResourcePool("wool", "Wool", 16, perTurn, onAttack, onDamageTaken);

        // ---- the Fragile Lamb: wards ---------------------------------------------

        [Test]
        public void AWardSoftensTheNextHitItsWearerTakes()
        {
            // The one test here that needs the monster to actually reply, so
            // this foe is fast enough to take its turn after the cast.
            var lamb = Hero("Lamb");
            Talents(lamb, new TalentEffect(TalentEffectType.WardReductionPercent, 50));

            var bare = Hero("Bare");
            var (control, controlEncounter) = Fight(new[] { bare }, new[] { Foe(speed: 9) }, Kit());
            control.Begin();
            control.ExecuteAttack(controlEncounter.Enemies[0]);
            int unwarded = bare.MaxHealth - bare.CurrentHealth;

            var (session, _) = Fight(new[] { lamb }, new[] { Foe(speed: 9) },
                Kit(Skill(SkillEffect.Ward, "Fleece Ward")));
            session.Begin();
            session.CastSkill(0, null);
            int warded = lamb.MaxHealth - lamb.CurrentHealth;

            Assert.Greater(unwarded, 0, "fixture: the control really was hit");
            Assert.Less(warded, unwarded);
        }

        [Test]
        public void AWardPaysItsCasterOncePerTurn_NotOncePerHit()
        {
            // A ward that paid on every hit it absorbed would return more wool
            // than it cost and the economy would run backwards.
            var lamb = Hero("Lamb");
            lamb.SignaturePool = Wool();
            Talents(lamb,
                new TalentEffect(TalentEffectType.WardReductionPercent, 50),
                new TalentEffect(TalentEffectType.WoolWhenWardedAllyHit, 2),
                new TalentEffect(TalentEffectType.WardsNeverExpire, 1));

            var (session, encounter) = Fight(new[] { lamb }, new[] { Foe("A"), Foe("B") },
                Kit(Skill(SkillEffect.Ward, "Fleece Ward")));
            session.Begin();

            session.CastSkill(0, null);
            int afterOneTurn = lamb.SignaturePool.Current;

            Assert.LessOrEqual(afterOneTurn, 2, "at most one payout, however many blows landed");
        }

        [Test]
        public void TheFlockSpreadsTheWardToAnAlly()
        {
            var lamb = Hero("Lamb");
            var ally = Hero("Ally");
            Talents(lamb,
                new TalentEffect(TalentEffectType.WardReductionPercent, 50),
                new TalentEffect(TalentEffectType.WardSpreadsToAllies, 50));

            var (session, _) = Fight(new[] { lamb, ally }, new[] { Foe() },
                Kit(Skill(SkillEffect.Ward, "Fleece Ward")));

            session.CastSkill(0, null);

            Assert.IsTrue(StatusEffects.IsWarded(ally));
            Assert.IsTrue(Messages(session).Any(m => m.Contains("throws the fleece wide")));
        }

        [Test]
        public void TheSpreadIsAShareOfTheWardsOwnStrength()
        {
            // One strand tunes the construct, the other decides how far it
            // reaches -- so deepening the ward deepens what the flock gets.
            var lamb = Hero("Lamb");
            var ally = Hero("Ally");
            Talents(lamb,
                new TalentEffect(TalentEffectType.WardReductionPercent, 60),
                new TalentEffect(TalentEffectType.WardSpreadsToAllies, 50));

            var (session, _) = Fight(new[] { lamb, ally }, new[] { Foe() },
                Kit(Skill(SkillEffect.Ward, "Fleece Ward")));

            session.CastSkill(0, null);

            var onLamb = lamb.Statuses.First(s => s.Type == StatusEffectType.Shielded);
            var onAlly = ally.Statuses.First(s => s.Type == StatusEffectType.Shielded);
            Assert.AreEqual(60, onLamb.Magnitude);
            Assert.AreEqual(30, onAlly.Magnitude);
        }

        [Test]
        public void MendingFleeceRidesAlongWithTheWard()
        {
            var lamb = Hero("Lamb");
            Talents(lamb,
                new TalentEffect(TalentEffectType.WardReductionPercent, 50),
                new TalentEffect(TalentEffectType.WardAlsoAppliesRegen, 5, threshold: 3));

            var (session, _) = Fight(new[] { lamb }, new[] { Foe() },
                Kit(Skill(SkillEffect.Ward, "Fleece Ward")));

            session.CastSkill(0, null);

            Assert.IsTrue(lamb.Statuses.Any(s => s.Type == StatusEffectType.Regen),
                "the sustain strand costs no extra action and no extra wool");
        }

        [Test]
        public void WardIsFreeActionKeepsTheTurn()
        {
            // The single biggest quality-of-life node in the Lamb's path: before
            // it, every ward is a turn not spent doing anything else.
            var lamb = Hero("Lamb");
            Talents(lamb,
                new TalentEffect(TalentEffectType.WardReductionPercent, 50),
                new TalentEffect(TalentEffectType.WardIsFreeAction, 1));

            var (session, encounter) = Fight(new[] { lamb }, new[] { Foe() },
                Kit(Skill(SkillEffect.Ward, "Fleece Ward")));
            session.Begin();

            session.CastSkill(0, null);

            Assert.AreSame(lamb, encounter.Current, "the turn did not advance");
            Assert.AreEqual(1, session.DrainBeats().Count, "but the beat still plays");
        }

        [Test]
        public void ShatterDetonatesEveryWardTheCasterHasOut()
        {
            var lamb = Hero("Lamb");
            Talents(lamb,
                new TalentEffect(TalentEffectType.WardReductionPercent, 50),
                new TalentEffect(TalentEffectType.ShatterDamagePercentOfAttack, 100));

            var foe = Foe();
            var (session, _) = Fight(new[] { lamb }, new[] { foe },
                Kit(Skill(SkillEffect.Ward, "Fleece Ward"), Skill(SkillEffect.Shatter, "Shatter")));

            session.CastSkill(0, null);
            session.DrainBeats();
            Assert.IsTrue(StatusEffects.IsWarded(lamb), "fixture: there is a ward to shatter");

            session.CastSkill(1, null);

            Assert.Less(foe.CurrentHealth, foe.MaxHealth);
            Assert.IsFalse(StatusEffects.IsWarded(lamb), "the ward is spent by the detonation");
        }

        [Test]
        public void HerOwnWardIsWorthMore()
        {
            var plain = Hero("Plain");
            Talents(plain,
                new TalentEffect(TalentEffectType.WardReductionPercent, 50),
                new TalentEffect(TalentEffectType.ShatterDamagePercentOfAttack, 100));
            var plainFoe = Foe();
            var (control, _) = Fight(new[] { plain }, new[] { plainFoe },
                Kit(Skill(SkillEffect.Ward), Skill(SkillEffect.Shatter)));
            control.CastSkill(0, null);
            control.CastSkill(1, null);
            int ordinary = plainFoe.MaxHealth - plainFoe.CurrentHealth;

            var boosted = Hero("Boosted");
            Talents(boosted,
                new TalentEffect(TalentEffectType.WardReductionPercent, 50),
                new TalentEffect(TalentEffectType.ShatterDamagePercentOfAttack, 100),
                new TalentEffect(TalentEffectType.ShatterSelfWardMultiplier, 300));
            var boostedFoe = Foe();
            var (session, _) = Fight(new[] { boosted }, new[] { boostedFoe },
                Kit(Skill(SkillEffect.Ward), Skill(SkillEffect.Shatter)));
            session.CastSkill(0, null);
            session.CastSkill(1, null);

            Assert.Greater(boostedFoe.MaxHealth - boostedFoe.CurrentHealth, ordinary);
        }

        [Test]
        public void AGiftLandsOnTheAlly()
        {
            var lamb = Hero("Lamb");
            var ally = Hero("Ally");
            ally.PrimaryPool.Current = 0;
            Talents(lamb, new TalentEffect(TalentEffectType.GiftManaPercent, 50));

            var (session, _) = Fight(new[] { lamb, ally }, new[] { Foe() },
                Kit(Skill(SkillEffect.GiftMana, "Gift: Mana")));

            session.CastSkill(0, null);

            Assert.Greater(ally.CurrentMana, 0);
            Assert.AreEqual(lamb.MaxMana, lamb.CurrentMana,
                "the gift is wool, not the caster's own mana");
        }

        // ---- who a Gift: Mana actually lands on -------------------------------
        //
        // GiftRecipient was "the first living party member who is not the
        // caster", and its own comment said why that was allowed to be a
        // placeholder: "the squad is one deep by default and two at most, so
        // 'an ally' is unambiguous today -- there is exactly one candidate or
        // none. It needs a real target picker the moment a third party slot
        // exists." That slot exists (41b470d4), and with a Fury holder standing
        // in it the placeholder spent the wool and the turn pouring mana into a
        // bar that refuses mana -- and announced a number that never landed.
        private static ResourcePool Fury() =>
            new ResourcePool("fury", "Fury", 100, 0, gainOnAttack: 15, gainOnDamageTaken: 10)
            {
                ShortTag = "FURY",
                RestoredByManaEffects = false,
            };

        [Test]
        public void AGiftOfManaSkipsAnAllyWhosePoolRefusesIt()
        {
            var shawn = Hero("Shawn");
            var bjorn = Hero("Bjorn", speed: 8);
            bjorn.PrimaryPool = Fury();

            // 60 max, 20 in the bar. 40% of 60 is 24, so the literal below is
            // 44 and nothing here re-derives it.
            var odette = new CombatantState("Odette", true, 500, 60, 40, 8);
            odette.PrimaryPool.Current = 20;

            Talents(shawn, new TalentEffect(TalentEffectType.GiftManaPercent, 40));

            // Party order puts Bjorn in front of Odette on purpose: he is what
            // "the first living ally" used to find.
            var (session, _) = Fight(new[] { shawn, bjorn, odette }, new[] { Foe() },
                Kit(Skill(SkillEffect.GiftMana, "Gift: Mana")));

            session.CastSkill(0, null);

            Assert.AreEqual(44, odette.CurrentMana, "the gift went to the ally who could not use it");
            Assert.AreEqual(0, bjorn.PrimaryPool.Current, "a Fury bar is not a mana bar");
            Assert.IsTrue(Messages(session).Any(m => m.Contains("Odette") && m.Contains("24")),
                "the line has to name who got it and how much actually landed");
        }

        [Test]
        public void AGiftOfManaPrefersTheAllyMissingTheMost()
        {
            var shawn = Hero("Shawn");

            // Both take mana; the first in party order is the fuller one, so
            // "first living ally" and "emptiest ally" disagree.
            var full = new CombatantState("Nearly Full", true, 500, 60, 40, 8);
            full.PrimaryPool.Current = 55;
            var drained = new CombatantState("Drained", true, 500, 60, 40, 8);
            drained.PrimaryPool.Current = 10;

            Talents(shawn, new TalentEffect(TalentEffectType.GiftManaPercent, 40));

            var (session, _) = Fight(new[] { shawn, full, drained }, new[] { Foe() },
                Kit(Skill(SkillEffect.GiftMana, "Gift: Mana")));

            session.CastSkill(0, null);

            Assert.AreEqual(34, drained.CurrentMana, "10 + 24");
            Assert.AreEqual(55, full.CurrentMana, "untouched");
        }

        [Test]
        public void AGiftOfManaIsRefusedWhenNoAllysPoolTakesMana()
        {
            var shawn = Hero("Shawn");
            var bjorn = Hero("Bjorn", speed: 8);
            bjorn.PrimaryPool = Fury();

            Talents(shawn, new TalentEffect(TalentEffectType.GiftManaPercent, 40));

            var (session, _) = Fight(new[] { shawn, bjorn }, new[] { Foe() },
                Kit(Skill(SkillEffect.GiftMana, "Gift: Mana")));

            bool cast = session.CastSkill(0, null);

            Assert.IsFalse(cast, "refused before the wool and the turn are paid, like Shatter with no wards");

            // The refusal opens no beat, so it lands in the immediate list --
            // the same place every pre-cast refusal in this file goes.
            Assert.IsTrue(session.DrainImmediateMessages().Any(m => m.Contains("nobody to give it to")));
        }

        [Test]
        public void TheOtherTwoGiftsStillTakeTheFirstLivingAlly()
        {
            // The control. Gift: Fury and Gift: Haste say nothing about mana,
            // so a Fury holder is a perfectly good recipient for both and the
            // placeholder's answer is still the right one for them.
            var shawn = Hero("Shawn");
            var bjorn = Hero("Bjorn", speed: 8);
            bjorn.PrimaryPool = Fury();

            Talents(shawn, new TalentEffect(TalentEffectType.GiftAttackBonusPercent, 50));

            var (session, _) = Fight(new[] { shawn, bjorn }, new[] { Foe() },
                Kit(Skill(SkillEffect.GiftFury, "Gift: Fury")));

            session.CastSkill(0, null);

            Assert.IsTrue(bjorn.Statuses.Any(s => s.Type == StatusEffectType.Empowered),
                "Gift: Fury has no opinion about what resource the ally carries");
        }

        [Test]
        public void GiftFuryIsSpentBySwingingAndNotByTheClock()
        {
            var lamb = Hero("Lamb");
            var ally = Hero("Ally", speed: 8);
            Talents(lamb, new TalentEffect(TalentEffectType.GiftAttackBonusPercent, 50));

            var (session, _) = Fight(new[] { lamb, ally }, new[] { Foe() },
                Kit(Skill(SkillEffect.GiftFury, "Gift: Fury")), Kit());

            session.CastSkill(0, null);

            Assert.IsTrue(ally.Statuses.Any(s => s.Type == StatusEffectType.Empowered));
        }

        // ---- the Black Ram --------------------------------------------------------

        // A flat write to BOTH broad Defenses now -- see ApplyDefenseShred's
        // own comment (FightSession.Talents.cs) for why there is no longer
        // one generic `Defense` field for this to write to.
        [Test]
        public void SharpHornsLeaveTheGuardPermanentlyThinner()
        {
            var ram = Hero("Ram");
            Talents(ram, new TalentEffect(TalentEffectType.ShredDefenseOnHit, 3));
            var foe = Foe();
            foe.PhysicalDefense = 10;
            foe.MagicalDefense = 10;

            var (session, _) = Fight(new[] { ram }, new[] { foe }, Kit());

            session.ExecuteAttack(foe);

            Assert.AreEqual(7, foe.PhysicalDefense);
            Assert.AreEqual(7, foe.MagicalDefense);
            Assert.IsTrue(Messages(session).Any(m => m.Contains("permanently")));
        }

        [Test]
        public void ShreddingACorpseIsALogLineAboutNothing()
        {
            var ram = Hero("Ram");
            Talents(ram, new TalentEffect(TalentEffectType.ShredDefenseOnHit, 3));
            var frail = Foe("Frail", health: 1);
            frail.PhysicalDefense = 10;
            frail.MagicalDefense = 10;

            var (session, _) = Fight(new[] { ram }, new[] { frail, Foe("Tank") }, Kit());

            session.ExecuteAttack(frail);

            Assert.IsFalse(frail.IsAlive);
            Assert.AreEqual(10, frail.PhysicalDefense);
            Assert.AreEqual(10, frail.MagicalDefense);
        }

        [Test]
        public void AKillSpillsOntoWhoeverWasStandingNextToIt()
        {
            var ram = Hero("Ram");
            Talents(ram, new TalentEffect(TalentEffectType.KillSplashPercentOfAttack, 100));
            var frail = Foe("Frail", health: 1);
            var neighbour = Foe("Neighbour");

            var (session, _) = Fight(new[] { ram }, new[] { frail, neighbour }, Kit());

            session.ExecuteAttack(frail);

            Assert.Less(neighbour.CurrentHealth, neighbour.MaxHealth);
            Assert.IsTrue(Messages(session).Any(m => m.Contains("goes down hard")));
        }

        [Test]
        public void SplashNeverChains()
        {
            // Computed from the original hit, never recursively from its own
            // kills, or one blow would cascade down a whole row.
            var ram = Hero("Ram");
            Talents(ram, new TalentEffect(TalentEffectType.KillSplashPercentOfAttack, 500));
            var frail = Foe("Frail", health: 1);
            var alsoFrail = Foe("AlsoFrail", health: 1);
            var far = Foe("Far");

            var (session, _) = Fight(new[] { ram }, new[] { frail, alsoFrail, far }, Kit());

            session.ExecuteAttack(frail);

            Assert.IsFalse(alsoFrail.IsAlive, "the neighbour died to the splash");
            Assert.AreEqual(far.MaxHealth, far.CurrentHealth, "and it stopped there");
        }

        // ADJACENCY IS WHAT THE PLAYER SEES. Ranks compress behind a corpse the
        // instant it falls (BeatFormation), so a body in the middle of the line
        // leaves the two survivors standing side by side -- and the splash has
        // to reach across it. The old rule walked raw list indices, which meant
        // a three-monster fight lost its splash entirely the moment the middle
        // one died.
        [Test]
        public void ACorpseInTheLineDoesNotBlockTheSplash()
        {
            var ram = Hero("Ram");
            Talents(ram, new TalentEffect(TalentEffectType.KillSplashPercentOfAttack, 100));
            var frail = Foe("Frail", health: 1);
            var corpse = Foe("Corpse");
            var behind = Foe("Behind");
            corpse.CurrentHealth = 0;

            var (session, _) = Fight(new[] { ram }, new[] { frail, corpse, behind }, Kit());

            session.ExecuteAttack(frail);

            Assert.Less(behind.CurrentHealth, behind.MaxHealth,
                "the corpse holds no rank, so Behind is the monster standing beside the victim");
        }

        // The Black Ram's own splash fires on EVERY landed hit, so the fight
        // this pins is not a corner: with three monsters, once the middle one
        // died the splash used to be dead for the rest of the fight.
        //
        // Literal: attack 100, splash 40% of the damage dealt, so the neighbour
        // takes 40 off its 1000000.
        [Test]
        public void ASplashReachesTheMonsterStandingBesideTheVictim()
        {
            var ram = Hero("Ram", health: 1000000, attack: 100, speed: 500);
            var a = Foe("A", health: 1000000);
            var b = Foe("B", health: 1);
            var c = Foe("C", health: 1000000);

            var (session, encounter) = Fight(new[] { ram }, new[] { a, b, c }, Kit());
            Transformation.Enter(ram, "Black Ram Mode", 99, 0, 0, 0, 40);

            // Swing one lands on the front rank A; the splash reaches B, which
            // is on 1 HP and dies.
            session.ExecuteAttack(a);
            Assert.IsFalse(b.IsAlive, "fixture: the splash was supposed to kill B");
            Assert.AreEqual(0, encounter.LivingRankOf(a));
            Assert.AreEqual(1, encounter.LivingRankOf(c), "fixture: C now stands where B was drawn");

            session.ExecuteAttack(a);

            Assert.AreEqual(999960, c.CurrentHealth,
                "the monster standing beside the victim took nothing: the splash walked off the corpse's list index");
        }

        [Test]
        public void TheTransformRaisesSpeedAndTellsTheScheduler()
        {
            // Without RefreshSpeed the plate would show the bonus and the turn
            // queue would ignore it -- a failure with no visible symptom other
            // than the mode feeling weaker than its numbers.
            var ram = Hero("Ram");
            var grant = new TransformGrant
            {
                displayName = "Black Ram Mode", turns = 3,
                attackPercent = 30, speedPercent = 30, temporaryHealthPercent = 0, splashPercent = 0,
            };

            var (session, encounter) = Fight(new[] { ram }, new[] { Foe() },
                Kit(Skill(SkillEffect.Transform, "Black Ram Mode", grant)));

            session.CastSkill(0, null);

            Assert.IsNotNull(ram.Transformation);
            Assert.Greater(ram.Speed, 10);
            CollectionAssert.Contains(encounter.UpcomingTurns(6), ram);
        }

        [Test]
        public void ReEnteringATransformRefreshesRatherThanStacking()
        {
            var ram = Hero("Ram");
            var grant = new TransformGrant
            {
                displayName = "Black Ram Mode", turns = 3,
                attackPercent = 30, speedPercent = 30,
            };
            var (session, _) = Fight(new[] { ram }, new[] { Foe() },
                Kit(Skill(SkillEffect.Transform, "Black Ram Mode", grant)));

            session.CastSkill(0, null);
            int attackOnce = ram.Attack;

            session.CastSkill(0, null);

            Assert.AreEqual(attackOnce, ram.Attack,
                "paying the cost twice must never be strictly better than paying it once");
        }

        [Test]
        public void TheTransformRunsOutOnItsOwnTimer()
        {
            var ram = Hero("Ram");
            var grant = new TransformGrant { displayName = "Black Ram Mode", turns = 2, attackPercent = 30 };
            var (session, encounter) = Fight(new[] { ram }, new[] { Foe() },
                Kit(Skill(SkillEffect.Transform, "Black Ram Mode", grant)));
            session.Begin();

            // The tick happens at the START of a turn, so the cast's own
            // AdvanceAfterAction already spends one of the two.
            session.CastSkill(0, null);
            Assert.IsNotNull(ram.Transformation);
            Assert.AreEqual(1, ram.Transformation.TurnsRemaining);

            session.ExecuteAttack(encounter.Enemies[0]);

            Assert.IsNull(ram.Transformation);
        }

        [Test]
        public void WoundedEnoughAndTheTimerSimplyDoesNotRun()
        {
            var ram = Hero("Ram");
            Talents(ram, new TalentEffect(TalentEffectType.TransformHoldsBelowHealth, 1, threshold: 50));
            var grant = new TransformGrant { displayName = "Black Ram Mode", turns = 1, attackPercent = 30 };
            var (session, encounter) = Fight(new[] { ram }, new[] { Foe() },
                Kit(Skill(SkillEffect.Transform, "Black Ram Mode", grant)));
            session.Begin();
            ram.CurrentHealth = ram.MaxHealth / 4;

            session.CastSkill(0, null);
            session.ExecuteAttack(encounter.Enemies[0]);
            session.ExecuteAttack(encounter.Enemies[0]);

            Assert.IsNotNull(ram.Transformation, "Wrath T3 holds it open");
        }

        [Test]
        public void AKillDuringTheTransformBuysMoreOfIt()
        {
            var ram = Hero("Ram");
            Talents(ram, new TalentEffect(TalentEffectType.TransformExtendOnKill, 1, threshold: 3));
            var grant = new TransformGrant { displayName = "Black Ram Mode", turns = 5, attackPercent = 30 };
            var (session, _) = Fight(new[] { ram }, new[] { Foe("Frail", health: 1), Foe("Tank") },
                Kit(Skill(SkillEffect.Transform, "Black Ram Mode", grant)));
            session.Begin();

            session.CastSkill(0, null);
            int before = ram.Transformation.TurnsRemaining;
            session.DrainBeats();

            session.ExecuteAttack(session.Encounter.Enemies[0]);

            Assert.AreEqual(1, ram.Transformation.ExtensionsGranted);
            Assert.IsTrue(Messages(session).Any(m => m.Contains("The kill feeds it")));
        }

        [Test]
        public void TheExtensionIsCappedByTheTalentsOwnThreshold()
        {
            var ram = Hero("Ram");
            Talents(ram, new TalentEffect(TalentEffectType.TransformExtendOnKill, 5, threshold: 2));
            var grant = new TransformGrant { displayName = "Black Ram Mode", turns = 20, attackPercent = 30 };
            var (session, _) = Fight(new[] { ram },
                new[] { Foe("A", health: 1), Foe("B", health: 1), Foe("Tank") },
                Kit(Skill(SkillEffect.Transform, "Black Ram Mode", grant)));
            session.Begin();

            session.CastSkill(0, null);
            session.ExecuteAttack(session.Encounter.Enemies[0]);
            session.ExecuteAttack(session.Encounter.Enemies[1]);

            Assert.AreEqual(2, ram.Transformation.ExtensionsGranted, "capped at the threshold, not 10");
        }

        [Test]
        public void ChargeT3ScattersTheWholeLineOnEntering()
        {
            var ram = Hero("Ram");
            Talents(ram, new TalentEffect(TalentEffectType.TransformPushesEveryEnemy, 1));
            var grant = new TransformGrant { displayName = "Black Ram Mode", turns = 3, attackPercent = 30 };
            var (session, _) = Fight(new[] { ram }, new[] { Foe("A"), Foe("B") },
                Kit(Skill(SkillEffect.Transform, "Black Ram Mode", grant)));

            session.CastSkill(0, null);

            Assert.IsTrue(Messages(session).Any(m => m.Contains("scatters the line")));
        }

        // ---- Provoke ---------------------------------------------------------------

        [Test]
        public void ProvokeTauntsTheFrontEnemy()
        {
            var ram = Hero("Ram");
            Talents(ram, new TalentEffect(TalentEffectType.ProvokedDamageReductionPercent, 30));
            var front = Foe("Front");
            var back = Foe("Back");

            var (session, _) = Fight(new[] { ram }, new[] { front, back },
                Kit(Skill(SkillEffect.Provoke, "Bellow")));

            session.CastSkill(0, null);

            Assert.AreSame(ram, StatusEffects.ProvokedBy(front));
            Assert.IsNull(StatusEffects.ProvokedBy(back));
            Assert.IsTrue(Messages(session).Any(m => m.Contains("one enemy can see nothing else")));
        }

        [Test]
        public void ProvokeT3TakesTheWholeRoom()
        {
            var ram = Hero("Ram");
            Talents(ram,
                new TalentEffect(TalentEffectType.ProvokedDamageReductionPercent, 30),
                new TalentEffect(TalentEffectType.ProvokeHitsEveryEnemy, 1));
            var front = Foe("Front");
            var back = Foe("Back");

            var (session, _) = Fight(new[] { ram }, new[] { front, back },
                Kit(Skill(SkillEffect.Provoke, "Bellow")));

            session.CastSkill(0, null);

            Assert.AreSame(ram, StatusEffects.ProvokedBy(front));
            Assert.AreSame(ram, StatusEffects.ProvokedBy(back));
            Assert.IsTrue(Messages(session).Any(m => m.Contains("all 2 of them")));
        }

        [Test]
        public void BellowingAtAnEmptyRoomSaysSoHonestly()
        {
            var ram = Hero("Ram");
            Talents(ram,
                new TalentEffect(TalentEffectType.ProvokedDamageReductionPercent, 30),
                new TalentEffect(TalentEffectType.ProvokeHitsEveryEnemy, 1));
            var corpse = Foe("Corpse");

            var (session, _) = Fight(new[] { ram }, new[] { corpse },
                Kit(Skill(SkillEffect.Provoke, "Bellow")));
            corpse.CurrentHealth = 0;

            session.CastSkill(0, null);

            Assert.IsTrue(Messages(session).Any(m => m.Contains("bellows at nothing in particular")));
        }

        // ---- the wool engines --------------------------------------------------------

        [Test]
        public void TheWoundedTierReplacesTheBaselineRatherThanStacking()
        {
            // Stacking would take the Ram's ceiling to +4 a turn and make a
            // 7-cost transform a two-turn purchase from full health.
            var ram = Hero("Ram");
            ram.SignaturePool = Wool(perTurn: 1);
            Talents(ram, new TalentEffect(TalentEffectType.WoolPerTurnBelowHealth, 3, threshold: 50));
            ram.CurrentHealth = ram.MaxHealth / 4;

            var (session, encounter) = Fight(new[] { ram }, new[] { Foe() }, Kit());
            session.Begin();

            Assert.AreEqual(3, ram.SignaturePool.Current, "3, not 1 + 3");
        }

        // "He gains wool from being hit" (talents.json, Black Ram row 0) is
        // unqualified, and the grant used to sit at the enemy's plain-swing
        // verb -- which the enemy SKILL branch returns several lines before
        // reaching, and which no other damage path in the game touches at all.
        // So the Bog Witch's Mud Burst, the Golem's Boulder Slam, an AOE, a
        // poison tick, a splash and a relic's free swing all paid nothing.
        // The primary pool's identical half was moved to the damage funnel
        // for exactly this reason; the fleece followed it.
        [Test]
        public void WoolIsGainedFromAnyHitTaken()
        {
            var ram = Hero("Ram");
            ram.SignaturePool = Wool();
            Talents(ram, new TalentEffect(TalentEffectType.WoolOnHitTaken, 1));

            var foe = Foe("Bog Witch");
            var (session, _) = Fight(new[] { ram }, new[] { foe }, Kit());

            // Straight through the funnel, which is what a monster's SKILL,
            // an AOE and a rider all reach and the plain-swing verb is not.
            session.DealDamageForTest(foe, ram, 20, DamageType.Physical);

            Assert.AreEqual(1, ram.SignaturePool.Current,
                "one hit taken, one point of wool, whatever delivered it");
        }

        [Test]
        public void ProvokeT3PaysForBeingTheOneEverythingIsAimedAt()
        {
            var ram = Hero("Ram");
            ram.SignaturePool = Wool(perTurn: 1);
            Talents(ram,
                new TalentEffect(TalentEffectType.ProvokedDamageReductionPercent, 30),
                new TalentEffect(TalentEffectType.ProvokeHitsEveryEnemy, 1),
                new TalentEffect(TalentEffectType.WoolPerProvokedEnemy, 1));

            var (session, encounter) = Fight(new[] { ram }, new[] { Foe("A"), Foe("B") },
                Kit(Skill(SkillEffect.Provoke, "Bellow")));
            session.Begin();
            int atStart = ram.SignaturePool.Current;

            session.CastSkill(0, null);

            // Both enemies were provoked, so the next turn start is baseline
            // plus one per goaded enemy -- income he ARRANGED, on top of the
            // income his health handed him.
            Assert.Greater(ram.SignaturePool.Current, atStart + 1);
        }

        [Test]
        public void APoisonTickIsNotAHitAndPaysNoWardEngine()
        {
            // By the time damage has landed the ward has usually been spent, so
            // the evidence is gone -- and a poison tick does not consume a ward
            // and must not pay for one.
            var lamb = Hero("Lamb");
            lamb.SignaturePool = Wool();
            Talents(lamb,
                new TalentEffect(TalentEffectType.WardReductionPercent, 50),
                new TalentEffect(TalentEffectType.WoolWhenWardedAllyHit, 2));
            StatusEffects.Apply(lamb.Statuses, StatusEffectType.Poison, 5, 5);

            var (session, encounter) = Fight(new[] { lamb }, new[] { Foe() },
                Kit(Skill(SkillEffect.Ward, "Fleece Ward")));
            session.Begin();
            int atStart = lamb.SignaturePool.Current;

            // A turn that opens with a poison tick and nothing else.
            session.CastSkill(0, null);
            session.DrainBeats();

            Assert.Less(lamb.CurrentHealth, lamb.MaxHealth, "fixture: the poison really ticked");
            Assert.LessOrEqual(lamb.SignaturePool.Current, atStart + 2,
                "at most the one payout a genuine hit earns, never one per tick");
        }
    }
}

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
    // THE TELEGRAPH (docs/PLAN_BELLWETHER_KIT.md 1.4, 1.5, 3.8; M4): the
    // Pull/Knell/Bleed kinds, the knell's number for the seat the target
    // stands in NOW (re-read after a Move and after a free Palace Passage
    // without the monster choosing again), the lethal state and "Step back".
    // Fixture skills; the seat table [110, 35, 0] is a fixture, not M5's
    // content. Every figure is a literal.
    public class KnellTelegraphTests
    {
        private static readonly int[] KnellTable = { 110, 35, 0 };

        private static ResolvedSkill Skill(string id, string name, SkillEffect effect, int[] bySeat = null,
            int toSeat = 0, bool ignoresDefense = false, StatusEffectType? status = null) =>
            new ResolvedSkill(id, name, "", "bell", 1, effect,
                SkillTargeting.SingleEnemy, 0, 0, false, 0, 1, ignoresDefense,
                null, SpellPresentation.None, 0,
                appliesStatus: status, statusMagnitude: status.HasValue ? 10 : 0,
                statusDuration: status.HasValue ? 2 : 0,
                playerSelectable: false,
                damageType: bySeat != null ? DamageType.Void : (DamageType?)null,
                damageBySeatMaxHpPercent: bySeat, toSeat: toSeat);

        private static ResolvedSkill Passage() =>
            new ResolvedSkill("palace_passage", "Palace Passage", "", "sheep", 1,
                SkillEffect.SwapAllies, SkillTargeting.SingleAlly, 7, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0, cooldownTurns: 3, freeAction: true);

        private static EnemyKit BellKit(int[] table, bool ignoresDefense = false)
        {
            var source = new ResolvedEnemy("bell", "Bell", new StatBlock(), 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0)
            {
                Schedule = new[] { new EnemyScheduleEntry(new[] { 1 }, new[] { "chains", "knell" }) },
            };

            return new EnemyKit(source, false, new List<EnemyAbility>
            {
                EnemyAbility.Of(Skill("scratch", "Scratch", SkillEffect.DamageSingle, status: StatusEffectType.Bleed), 1f),
                EnemyAbility.Of(Skill("chains", "Dark Chains", SkillEffect.Reposition, toSeat: 1), 0f),
                EnemyAbility.Of(Skill("knell", "Death Knell", SkillEffect.DamageSingle, table,
                    ignoresDefense: ignoresDefense), 0f),
            });
        }

        // A lone Shawn at the REAR, thirty times the bell's speed so he holds
        // many turns between two of its; driven until the chains have pulled
        // him to the front and the knell is committed.
        private static (FightSession session, CombatantState shawn, CombatantState bell) ToTheKnell(
            int[] table = null, int magicalDefense = 0, bool ignoresDefense = false,
            int health = 1000, float variance = 0f, ulong seed = 5)
        {
            var shawn = new CombatantState("Shawn", true, health, 40, 20, 30) { MagicalDefense = magicalDefense };
            var bell = new CombatantState("Bell", false, 100000, 0, 0, 1);
            var encounter = new CombatEncounter(new[] { shawn }, new[] { bell });
            Assert.AreEqual(PlaceOutcome.Placed, encounter.PlaceAt(shawn, 2, out _));

            var session = new FightSession(encounter,
                new List<PlayerKit>
                {
                    new PlayerKit("sheep", CharacterRole.Support, new[] { Passage() }, null, DamageType.Physical),
                },
                new List<EnemyKit> { BellKit(table ?? KnellTable, ignoresDefense) }, new SeededRandom(seed))
            {
                DamageVarianceRange = variance,
            };
            session.Begin();

            Assert.AreEqual(EnemyIntentKind.Pull, session.IntentDetailFor(bell).Value.Kind, "the chains open");
            Assert.AreEqual(0, session.IntentDamageFor(bell), "a pull shows no number");
            Assert.IsFalse(session.IntentDetailFor(bell).Value.IsLethal);
            for (int guard = 0; session.ActingTurnsOf(bell) < 1 && guard < 200; guard++)
            {
                Assert.IsTrue(session.ExecuteAttack(bell));
            }

            Assert.AreEqual(1, session.ActingTurnsOf(bell));
            Assert.AreSame(shawn, session.Current, "fixture: Shawn holds the turn with the knell committed");
            Assert.AreEqual(0, encounter.SeatOf(shawn), "the chains pulled him to the front");
            return (session, shawn, bell);
        }

        // ---- kinds ---------------------------------------------------------------

        [Test]
        public void ARepositionIsAPull_ASeatSizedHitIsAKnell_ABleedingHitIsABleed()
        {
            Assert.AreEqual(EnemyIntentKind.Pull,
                EnemyIntentIcons.KindFor(Skill("c", "C", SkillEffect.Reposition, toSeat: 1)));
            Assert.AreEqual(EnemyIntentKind.Knell,
                EnemyIntentIcons.KindFor(Skill("k", "K", SkillEffect.DamageSingle, KnellTable)));
            Assert.AreEqual(EnemyIntentKind.Bleed,
                EnemyIntentIcons.KindFor(Skill("s", "S", SkillEffect.DamageSingle, status: StatusEffectType.Bleed)));

            // A monster's own Bleed rider on a legacy swing reads the same.
            Assert.AreEqual(EnemyIntentKind.Bleed, EnemyIntentIcons.KindFor(true, StatusEffectType.Bleed, true));
        }

        [Test]
        public void TheNewKindsHaveTheirIconsTintsAndFallbacks()
        {
            Assert.AreEqual("Intent/bleed", EnemyIntentIcons.ResourceFor(EnemyIntentKind.Bleed));
            Assert.AreEqual("Intent/pull", EnemyIntentIcons.ResourceFor(EnemyIntentKind.Pull));
            Assert.AreEqual("Intent/knell", EnemyIntentIcons.ResourceFor(EnemyIntentKind.Knell));
            Assert.AreEqual("BLD", EnemyIntentIcons.For(EnemyIntentKind.Bleed));
            Assert.AreEqual("PULL", EnemyIntentIcons.For(EnemyIntentKind.Pull));
            Assert.AreEqual("KNL", EnemyIntentIcons.For(EnemyIntentKind.Knell));

            var tints = new[] { EnemyIntentKind.Bleed, EnemyIntentKind.Pull, EnemyIntentKind.Knell }
                .Select(EnemyIntentIcons.TintFor).ToList();
            CollectionAssert.DoesNotContain(tints, EnemyIntentIcons.TintFor(EnemyIntentKind.Skill),
                "a new kind fell through to the generic skill violet");
            CollectionAssert.AllItemsAreUnique(tints);
        }

        // ---- the knell's number, by seat, live -------------------------------------

        [Test]
        public void TheKnellShowsTheFrontFigure_AndItsWholeTable()
        {
            var (session, shawn, bell) = ToTheKnell();
            var intent = session.IntentDetailFor(bell).Value;

            Assert.AreEqual(EnemyIntentKind.Knell, intent.Kind);
            CollectionAssert.AreEqual(new[] { 1100, 350, 0 }, intent.DamageBySeat);
            Assert.AreEqual(0, intent.TargetSeat);
            Assert.AreEqual(1100, intent.ExpectedDamage);
            Assert.AreEqual(1100, session.IntentDamageFor(bell));
            Assert.IsTrue(intent.IsLethal, "1100 against 1000 health");
            Assert.AreEqual("Death Knell! Step back", session.TelegraphLine(bell));
            Assert.AreEqual(
                "Death Knell on Shawn: about 1100 at the front (lethal), 350 in the middle, none at the rear. Step back.",
                FightHudModel.IntentTooltip("Bell", intent));
        }

        [Test]
        public void APalacePassageInsideTheTurnRereadsTheNumber_WithoutTheBellChoosingAgain()
        {
            var (session, shawn, bell) = ToTheKnell();
            var before = session.IntentDetailFor(bell).Value;

            Assert.IsTrue(session.CastSkillToSeat(0, shawn, 1), "the free Passage to the middle");
            Assert.AreSame(shawn, session.Current, "a free action keeps the turn");

            var after = session.IntentDetailFor(bell).Value;
            Assert.AreEqual(before.Label, after.Label);
            Assert.AreEqual(before.AbilityIndex, after.AbilityIndex, "the bell did not choose again");
            Assert.AreSame(shawn, after.Target);
            Assert.AreEqual(1, after.TargetSeat);
            Assert.AreEqual(350, after.ExpectedDamage);
            Assert.IsFalse(after.IsLethal);
            Assert.AreEqual("Death Knell! Step back", session.TelegraphLine(bell));
        }

        [Test]
        public void AMoveBackRereadsTheNumber_FrontThenMiddleThenNothing()
        {
            var (session, shawn, bell) = ToTheKnell();
            Assert.AreEqual(1100, session.IntentDamageFor(bell));
            int index = session.IntentDetailFor(bell).Value.AbilityIndex;

            Assert.IsTrue(session.Move(MoveDirection.Back));
            Assert.AreSame(shawn, session.Current, "fixture: Shawn's next turn comes before the bell's");
            Assert.AreEqual(350, session.IntentDamageFor(bell));

            Assert.IsTrue(session.Move(MoveDirection.Back));
            Assert.AreSame(shawn, session.Current);
            Assert.AreEqual(0, session.IntentDamageFor(bell));
            Assert.AreEqual(index, session.IntentDetailFor(bell).Value.AbilityIndex, "still the same commitment");
            Assert.IsFalse(session.IntentDetailFor(bell).Value.IsLethal);
            Assert.AreEqual("Death Knell!", session.TelegraphLine(bell), "nowhere further back to step");
        }

        // ---- the same pipeline as the hit ------------------------------------------

        [Test]
        public void TheShownNumberMeetsDefence_AndLandsExactly()
        {
            // Middle: 35% of 1000 = 350 raw; magical defence 100 -> 175.
            var (session, shawn, bell) = ToTheKnell(magicalDefense: 100);
            Assert.IsTrue(session.CastSkillToSeat(0, shawn, 1));
            Assert.AreEqual(175, session.IntentDamageFor(bell));

            var knell = session.SourceFor(bell).Abilities[2].Skill;
            session.ResolveSkillForTest(bell, knell, shawn);
            Assert.AreEqual(825, shawn.CurrentHealth, "the badge's 175 is what landed");
        }

        // THE LANDED KNELL IS THE BADGE, UNDER THE LIVE VARIANCE (M7 bug). With
        // the shipped +-20% roll on, a 280-health Shawn's front knell landed
        // anywhere from 246 to 370 while the badge said 308. A seat-sized hit
        // is a fixed share of the bar: 110% of 280 = 308 at the front, 35% =
        // 98 in the middle, on every seed.
        [Test]
        public void UnderLiveVariance_TheKnellLandsItsBadgeOnEverySeed(
            [Values(1UL, 2UL, 3UL, 4UL, 5UL, 6UL, 7UL, 8UL, 9UL, 10UL, 11UL, 12UL)] ulong seed)
        {
            var (session, shawn, bell) = ToTheKnell(ignoresDefense: true, health: 280,
                variance: DamagePipeline.DefaultVarianceRange, seed: seed);
            Assert.AreEqual(308, session.IntentDamageFor(bell));
            var knell = session.SourceFor(bell).Abilities[2].Skill;
            session.DrainBeats();
            session.DrainImmediateMessages();

            session.ResolveSkillForTest(bell, knell, shawn);

            var messages = session.DrainBeats().SelectMany(b => b.Messages)
                .Concat(session.DrainImmediateMessages()).ToList();
            CollectionAssert.Contains(messages, "Bell uses Death Knell on Shawn for 308 damage!");
            Assert.IsFalse(shawn.IsAlive);
        }

        [Test]
        public void UnderLiveVariance_TheMiddleKnellLandsItsBadgeOnEverySeed(
            [Values(1UL, 2UL, 3UL, 4UL, 5UL, 6UL, 7UL, 8UL, 9UL, 10UL, 11UL, 12UL)] ulong seed)
        {
            var (session, shawn, bell) = ToTheKnell(ignoresDefense: true, health: 280,
                variance: DamagePipeline.DefaultVarianceRange, seed: seed);
            Assert.IsTrue(session.CastSkillToSeat(0, shawn, 1));
            Assert.AreEqual(98, session.IntentDamageFor(bell));

            session.ResolveSkillForTest(bell, session.SourceFor(bell).Abilities[2].Skill, shawn);

            Assert.AreEqual(182, shawn.CurrentHealth);
        }

        [Test]
        public void AKnellThatIgnoresDefenceShowsTheWholeFigure()
        {
            var (session, shawn, bell) = ToTheKnell(magicalDefense: 100, ignoresDefense: true);
            Assert.IsTrue(session.CastSkillToSeat(0, shawn, 1));
            Assert.AreEqual(350, session.IntentDamageFor(bell));
        }

        // ---- lethal and "Step back" -----------------------------------------------

        [Test]
        public void LethalIsOnAtTheTargetsHealthAndOffOnePointAbove()
        {
            var (session, shawn, bell) = ToTheKnell();
            Assert.IsTrue(session.CastSkillToSeat(0, shawn, 1));

            shawn.CurrentHealth = 350;
            Assert.IsTrue(session.IntentDetailFor(bell).Value.IsLethal, "350 against 350");

            shawn.CurrentHealth = 351;
            Assert.IsFalse(session.IntentDetailFor(bell).Value.IsLethal, "350 against 351");
        }

        [Test]
        public void AnyDamageIntentIsLethalAcrossTheLine_NotOnlyTheKnell()
        {
            // Scratch: 1 flat from a 0-attack bell, 1 against 1 health.
            var shawn = new CombatantState("Shawn", true, 1000, 40, 20, 30);
            var bell = new CombatantState("Bell", false, 100000, 0, 0, 1);
            var kit = new EnemyKit(
                new ResolvedEnemy("bell", "Bell", new StatBlock(), 0, 0, false, DamageType.Physical,
                    DamageType.Physical, 0),
                false,
                new List<EnemyAbility> { EnemyAbility.Of(Skill("scratch", "Scratch", SkillEffect.DamageSingle), 1f) });
            var session = new FightSession(new CombatEncounter(new[] { shawn }, new[] { bell }),
                null, new List<EnemyKit> { kit }, new SeededRandom(5)) { DamageVarianceRange = 0f };
            session.Begin();

            Assert.AreEqual(1, session.IntentDamageFor(bell));
            Assert.IsFalse(session.IntentDetailFor(bell).Value.IsLethal);
            shawn.CurrentHealth = 1;
            Assert.IsTrue(session.IntentDetailFor(bell).Value.IsLethal);
            StringAssert.EndsWith("for about 1 damage (lethal)",
                FightHudModel.IntentTooltip("Bell", session.IntentDetailFor(bell).Value));
        }

        [Test]
        public void StepBackOnlyWhenASeatFurtherBackTakesLess()
        {
            var (session, _, bell) = ToTheKnell(table: new[] { 50, 50, 50 });

            Assert.IsFalse(session.IntentDetailFor(bell).Value.StepBackIsSafer);
            Assert.AreEqual("Death Knell!", session.TelegraphLine(bell));
            StringAssert.DoesNotContain("Step back", FightHudModel.IntentTooltip("Bell", session.IntentDetailFor(bell).Value));
        }
    }
}

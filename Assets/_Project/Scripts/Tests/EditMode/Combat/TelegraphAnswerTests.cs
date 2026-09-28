using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Bot;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // THE BOT ANSWERS A TELEGRAPH (docs/PLAN_BELLWETHER_KIT.md 3.10, M6):
    // TelegraphAnswer before every archetype, FightRunner's -NoTelegraphAnswer
    // switch, the Passage in the legal menu, and the per-knell trace. Fixture
    // bell and knell table [110, 35, 0] (KnellTelegraphTests' own), Shawn at
    // 1000 health: 1100 / 350 / 0 by seat. Every figure is a literal.
    public class TelegraphAnswerTests
    {
        private static readonly int[] KnellTable = { 110, 35, 0 };

        private static ResolvedSkill Skill(string id, string name, SkillEffect effect, int[] bySeat = null,
            int toSeat = 0) =>
            new ResolvedSkill(id, name, "", "bell", 1, effect,
                SkillTargeting.SingleEnemy, 0, 0, false, 0, 1, false,
                null, SpellPresentation.None, 0,
                playerSelectable: false,
                damageType: bySeat != null ? DamageType.Void : (DamageType?)null,
                damageBySeatMaxHpPercent: bySeat, toSeat: toSeat);

        private static ResolvedSkill Passage() =>
            new ResolvedSkill("palace_passage", "Palace Passage", "", "sheep", 1,
                SkillEffect.SwapAllies, SkillTargeting.SingleAlly, 7, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0, cooldownTurns: 3, freeAction: true);

        // Chains on its 1st acting turn, the knell on its 2nd; Scratch
        // otherwise. `schedule` false: Scratch every turn, nothing seat-sized.
        private static EnemyKit BellKit(int[] table, bool schedule = true)
        {
            var source = new ResolvedEnemy("bell", "Bell", new StatBlock(), 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0);
            if (schedule)
            {
                source.Schedule = new[] { new EnemyScheduleEntry(new[] { 1 }, new[] { "chains", "knell" }) };
            }

            return new EnemyKit(source, false, new List<EnemyAbility>
            {
                EnemyAbility.Of(Skill("scratch", "Scratch", SkillEffect.DamageSingle), 1f),
                EnemyAbility.Of(Skill("chains", "Dark Chains", SkillEffect.Reposition, toSeat: 1), 0f),
                EnemyAbility.Of(Skill("knell", "Death Knell", SkillEffect.DamageSingle, table), 0f),
            });
        }

        // A lone Shawn at the REAR, thirty times the bell's speed. Not begun.
        private static (FightSession session, CombatantState shawn, CombatantState bell) Fight(
            bool withPassage, int[] table = null, bool schedule = true, int bellHealth = 100000)
        {
            var shawn = new CombatantState("Shawn", true, 1000, 40, 20, 30);
            var bell = new CombatantState("Bell", false, bellHealth, 0, 0, 1);
            var encounter = new CombatEncounter(new[] { shawn }, new[] { bell });
            Assert.AreEqual(PlaceOutcome.Placed, encounter.PlaceAt(shawn, 2, out _));

            var skills = withPassage ? new[] { Passage() } : new ResolvedSkill[0];
            var session = new FightSession(encounter,
                new List<PlayerKit> { new PlayerKit("sheep", CharacterRole.Support, skills, null, DamageType.Physical) },
                new List<EnemyKit> { BellKit(table ?? KnellTable, schedule) }, new SeededRandom(5))
            {
                DamageVarianceRange = 0f,
            };
            return (session, shawn, bell);
        }

        // Begun and driven until the chains have pulled Shawn to the front
        // and the knell is committed, Shawn holding the turn.
        private static (FightSession session, CombatantState shawn, CombatantState bell) ToTheKnell(
            bool withPassage, int[] table = null)
        {
            var (session, shawn, bell) = Fight(withPassage, table);
            session.Begin();
            for (int guard = 0; session.ActingTurnsOf(bell) < 1 && guard < 200; guard++)
            {
                Assert.IsTrue(session.ExecuteAttack(bell));
            }

            Assert.AreSame(shawn, session.Current, "fixture: Shawn holds the turn with the knell committed");
            Assert.AreEqual(0, session.Encounter.SeatOf(shawn), "fixture: the chains pulled him to the front");
            Assert.AreEqual(EnemyIntentKind.Knell, session.IntentDetailFor(bell).Value.Kind);
            return (session, shawn, bell);
        }

        private static IReadOnlyList<FightAction> Legal(FightSession session, CombatantState actor) =>
            FightAction.LegalActions(session, actor, System.Array.Empty<SatchelStack>());

        // ---- the rule --------------------------------------------------------------

        [Test]
        public void BeforeAKnell_WithoutTheBook_StepsBackFromTheFrontToTheMiddle()
        {
            var (session, shawn, bell) = ToTheKnell(withPassage: false);
            CollectionAssert.AreEqual(new[] { 1100, 350, 0 }, session.IntentDetailFor(bell).Value.DamageBySeat);

            var answer = TelegraphAnswer.Choose(session, shawn, Legal(session, shawn), out var kind);

            Assert.IsTrue(answer.HasValue);
            Assert.AreEqual(TelegraphAnswerKind.Step, kind);
            Assert.AreEqual(FightActionKind.Move, answer.Value.Kind);
            Assert.AreEqual(MoveDirection.Back, answer.Value.MoveDirection);

            FightAction.Apply(session, answer.Value);
            Assert.AreEqual(1, session.Encounter.SeatOf(shawn));
            Assert.AreEqual(350, session.IntentDamageFor(bell));
        }

        [Test]
        public void BeforeTheChainsResolve_NothingIsSeatSized_AndTheRuleDoesNothing()
        {
            var (session, shawn, bell) = Fight(withPassage: true);
            session.Begin();
            Assert.AreEqual(EnemyIntentKind.Pull, session.IntentDetailFor(bell).Value.Kind);

            Assert.IsNull(TelegraphAnswer.Choose(session, shawn, Legal(session, shawn), out var kind));
            Assert.AreEqual(TelegraphAnswerKind.None, kind);
        }

        [Test]
        public void AStepBack_NeedsASavingOfAQuarterOfMaxHealth()
        {
            // 20% of 1000 at the front, nothing behind: 200 saved, under 250.
            var (under, shawnUnder, _) = ToTheKnell(withPassage: false, table: new[] { 20, 0, 0 });
            Assert.IsNull(TelegraphAnswer.Choose(under, shawnUnder, Legal(under, shawnUnder), out _));

            // 25%: exactly 250 saved, which is enough.
            var (at, shawnAt, _) = ToTheKnell(withPassage: false, table: new[] { 25, 0, 0 });
            var answer = TelegraphAnswer.Choose(at, shawnAt, Legal(at, shawnAt), out var kind);
            Assert.AreEqual(TelegraphAnswerKind.Step, kind);
            Assert.AreEqual(MoveDirection.Back, answer.Value.MoveDirection);
            Assert.AreEqual(0.25f, TelegraphAnswer.StepMinSavingOfMaxHp);
        }

        [Test]
        public void WithTheBook_ThePassageReachesTheRear_AndShawnStillAttacksThatTurn()
        {
            var (session, shawn, bell) = ToTheKnell(withPassage: true);
            var policy = new GreedyAggressivePolicy();
            var rng = new SeededRandom(1);

            var first = FightRunner.ChooseCommand(session, shawn, policy, Legal(session, shawn), rng,
                TransformUse.PolicyDecides, true, out var answered);
            Assert.AreEqual(TelegraphAnswerKind.Passage, answered);
            Assert.AreEqual(FightActionKind.Skill, first.Kind);
            Assert.AreSame(shawn, first.Target);
            Assert.AreEqual(2, first.DestinationSeat, "the cheapest seat, the rear");

            FightAction.Apply(session, first);
            Assert.AreEqual(2, session.Encounter.SeatOf(shawn));
            Assert.AreSame(shawn, session.Current, "a free action keeps the turn");
            Assert.AreEqual(0, session.IntentDamageFor(bell));

            var second = FightRunner.ChooseCommand(session, shawn, policy, Legal(session, shawn), rng,
                TransformUse.PolicyDecides, true, out answered);
            Assert.AreEqual(TelegraphAnswerKind.None, answered, "nothing left to answer at the rear");
            Assert.AreEqual(FightActionKind.Attack, second.Kind);

            FightAction.Apply(session, second);
            Assert.Less(bell.CurrentHealth, 100000, "the same turn's swing landed");
            Assert.AreEqual(2, session.Encounter.SeatOf(shawn));
        }

        [Test]
        public void ANonSeatSizedIntent_IsNotAnswered_EvenWithTheBook()
        {
            var (session, shawn, bell) = Fight(withPassage: true, schedule: false);
            session.Begin();
            var intent = session.IntentDetailFor(bell).Value;
            Assert.IsNull(intent.DamageBySeat, "fixture: Scratch is not seat-sized");
            Assert.AreSame(shawn, intent.Target);

            var legal = Legal(session, shawn);
            Assert.IsTrue(legal.Any(a => a.DestinationSeat >= 0), "fixture: a Passage is on the menu");
            Assert.IsNull(TelegraphAnswer.Choose(session, shawn, legal, out var kind));
            Assert.AreEqual(TelegraphAnswerKind.None, kind);
        }

        [Test]
        public void NoTelegraphAnswer_LeavesTheArchetypeAlone()
        {
            var (session, shawn, _) = ToTheKnell(withPassage: false);
            var policy = new GreedyAggressivePolicy();

            var off = FightRunner.ChooseCommand(session, shawn, policy, Legal(session, shawn), new SeededRandom(1),
                TransformUse.PolicyDecides, false, out var answered);
            Assert.AreEqual(TelegraphAnswerKind.None, answered);
            Assert.AreEqual(FightActionKind.Attack, off.Kind, "the archetype's own swing, standing in front");

            var on = FightRunner.ChooseCommand(session, shawn, policy, Legal(session, shawn), new SeededRandom(1),
                TransformUse.PolicyDecides, true, out answered);
            Assert.AreEqual(TelegraphAnswerKind.Step, answered);
            Assert.AreEqual(FightActionKind.Move, on.Kind);
        }

        // ---- the legal menu ----------------------------------------------------------

        [Test]
        public void RandomLegal_CanPickThePassage_AndFiftyFightsPlayClean()
        {
            int passages = 0;
            for (ulong seed = 1; seed <= 50UL; seed++)
            {
                var shawn = new CombatantState("Shawn", true, 300, 40, 20, 3);
                var foe = new CombatantState("Foe", false, 200, 0, 10, 2);
                var session = new FightSession(new CombatEncounter(new[] { shawn }, new[] { foe }),
                    new List<PlayerKit>
                    {
                        new PlayerKit("sheep", CharacterRole.Support, new[] { Passage() }, null, DamageType.Physical),
                    },
                    null, new SeededRandom(seed))
                {
                    DamageVarianceRange = 0f,
                };

                var trace = new FightTrace();
                var hits = FightRunner.Play(session, new RandomLegalPolicy(), System.Array.Empty<SatchelStack>(),
                    new SeededRandom(seed), trace);

                Assert.IsEmpty(hits, $"seed {seed}: " + string.Join(" | ", hits.Select(h => h.ToString())));
                Assert.IsTrue(session.IsOver, $"seed {seed}: the fight did not end");
                passages += trace.TurnTraces.Count(t => t.Action == "Skill:palace_passage");
            }

            Assert.Greater(passages, 0, "RandomLegal never took the Passage in fifty fights");
        }

        // ---- the knell trace ---------------------------------------------------------

        [Test]
        public void TheKnellTrace_AnsweredTwiceBySteps_LandsAtTheRearForNothing()
        {
            var (session, shawn, bell) = Fight(withPassage: false, bellHealth: 2000);
            var trace = new FightTrace();
            FightRunner.Play(session, new GreedyAggressivePolicy(), System.Array.Empty<SatchelStack>(),
                new SeededRandom(1), trace);

            Assert.AreEqual(1, trace.Knells.Count);
            var knell = trace.Knells[0];
            Assert.AreEqual("Bell", knell.EnemyId);
            Assert.AreEqual(2, knell.ActingTurn);
            Assert.AreEqual("Shawn", knell.TargetId);
            Assert.AreEqual(2, knell.Seat, "front -> middle -> rear, one step a turn");
            Assert.AreEqual(0, knell.Damage);
            Assert.AreEqual(1000, knell.HpBefore);
            Assert.AreEqual("step", knell.AnsweredBy);
            Assert.IsTrue(knell.Survived);
            Assert.AreEqual(2, trace.TurnTraces.Count(t => t.Action == "Move"));
        }

        [Test]
        public void TheKnellTrace_Unanswered_KillsAtTheFront()
        {
            var (session, shawn, bell) = Fight(withPassage: false, bellHealth: 2000);
            var trace = new FightTrace();
            FightRunner.Play(session, new GreedyAggressivePolicy(), System.Array.Empty<SatchelStack>(),
                new SeededRandom(1), trace, answerTelegraphs: false);

            Assert.AreEqual(1, trace.Knells.Count);
            var knell = trace.Knells[0];
            Assert.AreEqual(2, knell.ActingTurn);
            Assert.AreEqual(0, knell.Seat);
            Assert.AreEqual(1100, knell.Damage);
            Assert.AreEqual(1000, knell.HpBefore);
            Assert.AreEqual("none", knell.AnsweredBy);
            Assert.IsFalse(knell.Survived);
            Assert.IsFalse(shawn.IsAlive);
        }

        [Test]
        public void TheKnellTrace_WithTheBook_SaysPassage()
        {
            var (session, shawn, bell) = Fight(withPassage: true, bellHealth: 2000);
            var trace = new FightTrace();
            FightRunner.Play(session, new GreedyAggressivePolicy(), System.Array.Empty<SatchelStack>(),
                new SeededRandom(1), trace);

            Assert.AreEqual(1, trace.Knells.Count);
            Assert.AreEqual(2, trace.Knells[0].Seat);
            Assert.AreEqual(0, trace.Knells[0].Damage);
            Assert.AreEqual("passage", trace.Knells[0].AnsweredBy);
            Assert.AreEqual(0, trace.TurnTraces.Count(t => t.Action == "Move"), "the Passage, not a step");
        }
    }
}

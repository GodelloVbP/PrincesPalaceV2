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
    // The Bellwether's kit, PRESENTED (docs/PLAN_BELLWETHER_KIT.md M5): the
    // knell's floor figures, the rally badge, the toll's own beat and the
    // preview showcase that now plays scheduled skills. Fixture skills and a
    // fixture rally; the content rows are pinned by BellwetherKitContentTests
    // (PlayMode, built content). Every figure is a literal.
    public class BellwetherPresentationTests
    {
        private static readonly int[] KnellTable = { 110, 35, 0 };

        private static ResolvedSkill Skill(string id, string name, SkillEffect effect, int[] bySeat = null,
            int toSeat = 0) =>
            new ResolvedSkill(id, name, "", "bell", 1, effect,
                SkillTargeting.SingleEnemy, 0, 0, false, 0, 1, bySeat != null,
                null, SpellPresentation.None, 0,
                playerSelectable: false,
                damageType: bySeat != null ? DamageType.Void : (DamageType?)null,
                damageBySeatMaxHpPercent: bySeat, toSeat: toSeat);

        private static ResolvedSkill Passage() =>
            new ResolvedSkill("palace_passage", "Palace Passage", "", "sheep", 1,
                SkillEffect.SwapAllies, SkillTargeting.SingleAlly, 7, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0, cooldownTurns: 3, freeAction: true);

        private static SpellPresentation Ripple() => new SpellPresentation
        {
            layerFormat = 1,
            hitCueSeconds = 0.2f,
            layers = new[]
            {
                new SpellLayer
                {
                    id = "ripple", render = "sprite", place = "caster-centre", at = "release",
                    path = "Spells/bellwether_toll_ripple", seconds = 0.63f, fade = 0.15f,
                },
            },
        };

        private static ResolvedEnemy BellSource(bool presents = false, bool rally = false) =>
            new ResolvedEnemy("bell", "Bell", new StatBlock(), 0, 0, false, DamageType.Physical, DamageType.Physical, 0)
            {
                Schedule = new[] { new EnemyScheduleEntry(new[] { 1, 5 }, new[] { "chains", "knell" }) },
                RallyAttackPercentPerStack = rally ? 8 : 0,
                RallyMaxStacks = rally ? 10 : 0,
                RallyStance = presents ? "cast" : "",
                RallyVfx = presents ? Ripple() : new SpellPresentation(),
            };

        private static EnemyKit BellKit(ResolvedEnemy source) =>
            new EnemyKit(source, false, new List<EnemyAbility>
            {
                EnemyAbility.Of(Skill("scratch", "Scratch", SkillEffect.DamageSingle), 1f),
                EnemyAbility.Of(Skill("chains", "Dark Chains", SkillEffect.Reposition, toSeat: 1), 0f),
                EnemyAbility.Of(Skill("knell", "Death Knell", SkillEffect.DamageSingle, KnellTable), 0f),
            });

        // A lone Shawn at the rear, thirty times the bell's speed, 1000 HP.
        private static (FightSession session, CombatantState shawn, CombatantState bell) Solo(
            ResolvedEnemy source = null, int roundLimit = 0)
        {
            var shawn = new CombatantState("Shawn", true, 1000, 40, 20, 30) { MagicalDefense = 0 };
            var bell = new CombatantState("Bell", false, 100000, 0, 0, 1);
            var encounter = new CombatEncounter(new[] { shawn }, new[] { bell });
            Assert.AreEqual(PlaceOutcome.Placed, encounter.PlaceAt(shawn, 2, out _));

            var session = new FightSession(encounter,
                new List<PlayerKit>
                {
                    new PlayerKit("sheep", CharacterRole.Support, new[] { Passage() }, null, DamageType.Physical),
                },
                new List<EnemyKit> { BellKit(source ?? BellSource()) }, new SeededRandom(5))
            {
                DamageVarianceRange = 0f,
                RoundLimit = roundLimit,
            };
            session.Begin();
            return (session, shawn, bell);
        }

        private static void UntilChainsResolved(FightSession session, CombatantState bell)
        {
            for (int guard = 0; session.ActingTurnsOf(bell) < 1 && guard < 200; guard++)
            {
                Assert.IsTrue(session.ExecuteAttack(bell));
            }

            Assert.AreEqual(1, session.ActingTurnsOf(bell), "fixture: the chains resolved");
            Assert.IsTrue(session.IsPlayerTurn);
        }

        // ---- the knell's floor figures ----------------------------------------------

        [Test]
        public void WhileTheChainsAreTheIntent_NoFloorFigureShows()
        {
            var (session, _, _) = Solo();

            var figures = FightHudModel.SeatFiguresFor(session, readable: true);

            Assert.AreEqual(3, figures.Length);
            Assert.IsTrue(figures.All(f => !f.Shown), "a pull is not a seat-sized hit");
        }

        [Test]
        public void WhileTheKnellIsCommitted_EachSeatShowsItsFigure_FrontLethalMiddleLilacRearSafe()
        {
            var (session, _, bell) = Solo();
            UntilChainsResolved(session, bell);

            var figures = FightHudModel.SeatFiguresFor(session, readable: true);

            Assert.AreEqual(FightHudModel.SeatFigureStyle.Lethal, figures[0].Style);
            Assert.AreEqual("1100", figures[0].Text);
            Assert.AreEqual(FightHudModel.SeatFigureStyle.Survivable, figures[1].Style);
            Assert.AreEqual("350", figures[1].Text);
            Assert.AreEqual(FightHudModel.SeatFigureStyle.Safe, figures[2].Style);
            Assert.AreEqual("SAFE", figures[2].Text);
            Assert.IsTrue(figures[0].IsTargetSeat, "the chains put him at the front");
            Assert.IsFalse(figures[1].IsTargetSeat || figures[2].IsTargetSeat);
        }

        [Test]
        public void WhenTheScreenIsNotReadable_EveryFigureIsHidden()
        {
            var (session, _, bell) = Solo();
            UntilChainsResolved(session, bell);

            Assert.IsTrue(FightHudModel.SeatFiguresFor(session, readable: false).All(f => !f.Shown));
            Assert.IsTrue(FightHudModel.SeatFiguresFor(null, readable: true).All(f => !f.Shown));
        }

        [Test]
        public void AfterAFreePassageToTheRear_TheFiguresStayAndTheTargetSeatMoves()
        {
            var (session, shawn, bell) = Solo();
            UntilChainsResolved(session, bell);

            Assert.IsTrue(session.CastSkillToSeat(0, shawn, 2), "the free Passage to the rear");

            var figures = FightHudModel.SeatFiguresFor(session, readable: true);
            Assert.AreEqual("1100", figures[0].Text);
            Assert.AreEqual("350", figures[1].Text);
            Assert.AreEqual("SAFE", figures[2].Text);
            Assert.IsFalse(figures[0].IsTargetSeat);
            Assert.IsTrue(figures[2].IsTargetSeat, "he stands at the rear now");
        }

        [Test]
        public void AWoundedShawn_SeesTheMiddleTurnLethal()
        {
            var (session, shawn, bell) = Solo();
            UntilChainsResolved(session, bell);

            shawn.CurrentHealth = 350;

            var figures = FightHudModel.SeatFiguresFor(session, readable: true);
            Assert.AreEqual(FightHudModel.SeatFigureStyle.Lethal, figures[1].Style, "350 at 350 HP kills");

            shawn.CurrentHealth = 351;
            Assert.AreEqual(FightHudModel.SeatFigureStyle.Survivable,
                FightHudModel.SeatFiguresFor(session, readable: true)[1].Style);
        }

        [Test]
        public void OnceTheKnellHasLanded_TheFiguresGo()
        {
            var (session, shawn, bell) = Solo();
            UntilChainsResolved(session, bell);
            Assert.IsTrue(session.CastSkillToSeat(0, shawn, 2), "stand where it passes over");

            for (int guard = 0; session.ActingTurnsOf(bell) < 2 && guard < 200; guard++)
            {
                Assert.IsTrue(session.ExecuteAttack(bell));
            }

            Assert.AreEqual(1000, shawn.CurrentHealth, "the rear takes nothing");
            Assert.IsTrue(FightHudModel.SeatFiguresFor(session, readable: true).All(f => !f.Shown),
                "the next intent is Scratch");
        }

        // ---- the pull is seen at the chains' impact ------------------------------------

        [Test]
        public void TheChainsBeatOpensOnTheOldFormation_AndPaintsThePullAtItsImpact()
        {
            var (session, shawn, bell) = Solo();
            session.DrainBeats();
            UntilChainsResolved(session, bell);

            // Every beat since Begin; the bell's one action is the chains.
            var chains = session.DrainBeats().Single(b => b.IsAction && ReferenceEquals(b.Actor, bell));

            Assert.IsNotNull(chains.PreFormation, "a placement at impact records where everybody stood before");
            Assert.AreSame(shawn, chains.PreFormation.PartyField[2], "before: Shawn in the rear seat");
            Assert.AreSame(shawn, chains.Formation.PartyField[0], "after: dragged to the front");
        }

        [Test]
        public void AnOrdinaryBeatHasNoPreFormation()
        {
            var (session, _, _) = Solo();
            Assert.IsTrue(session.DrainBeats().All(b => b.PreFormation == null));
        }

        // ---- the rally badge ----------------------------------------------------------

        [Test]
        public void TheRallyBadgeCountsItsStacks_OneToTenThenHolds()
        {
            var (session, shawn, bell) = Solo(BellSource(rally: true));
            var seen = new List<int>();

            for (int guard = 0; session.Round <= 12 && guard < 2000; guard++)
            {
                int want = session.Round > 10 ? 10 : session.Round;
                var rally = FightHudModel.StatusRowsFor(session, bell).Single(r => r.Slug == "rally");
                Assert.AreEqual(want, rally.Counter, $"round {session.Round}");
                Assert.AreEqual("×" + want, rally.CounterLabel);
                StringAssert.Contains($"+{want * 8}% attack", rally.Tooltip);
                if (!seen.Contains(want)) seen.Add(want);

                // Kept at the rear, where the fixture's knell passes over him.
                session.Encounter.PlaceAt(shawn, 2, out _);
                Assert.IsTrue(session.ExecuteAttack(bell));
            }

            CollectionAssert.AreEqual(Enumerable.Range(1, 10).ToArray(), seen, "every count from 1 to the cap of 10");
        }

        [Test]
        public void NoRally_NoBadge()
        {
            var (session, _, bell) = Solo();
            Assert.IsFalse(FightHudModel.StatusRowsFor(session, bell).Any(r => r.Slug == "rally"));
        }

        [Test]
        public void ATurnCountStillPrintsItsBareNumber()
        {
            var row = new FightHudModel.StatusRow("PSN", "poison", "", false, 3, 1);
            Assert.AreEqual("3", row.CounterLabel);
        }

        // ---- the toll's own beat ------------------------------------------------------

        [Test]
        public void APresentedToll_IsTheRoundsBeat_WithTheBellAsActor_InItsCastPose_WithItsRipple()
        {
            var (session, _, bell) = Solo(BellSource(presents: true, rally: true), roundLimit: 10);

            var opening = session.DrainBeats().Where(b => b.Cause == BeatCause.RoundStart).ToList();

            Assert.AreEqual(1, opening.Count, "one toll, carrying round 1");
            var toll = opening[0];
            Assert.AreEqual(1, toll.RoundStarted);
            Assert.AreSame(bell, toll.Actor);
            Assert.IsFalse(toll.IsAction, "a toll is not an action");
            Assert.AreEqual("cast", toll.Stances[bell]);
            Assert.AreEqual("Spells/bellwether_toll_ripple", toll.Vfx.layers[0].path);
            Assert.AreEqual(0, toll.Amount);
        }

        [Test]
        public void EveryRoundTollsOnce_InOrder_AndAPlainRallyStillRecordsTheBareRoundBeat()
        {
            var (presented, _, bell) = Solo(BellSource(presents: true, rally: true), roundLimit: 4);
            var beats = new List<CombatBeat>(presented.DrainBeats());
            for (int guard = 0; !presented.IsOver && guard < 400; guard++)
            {
                presented.ExecuteAttack(bell);
                beats.AddRange(presented.DrainBeats());
            }

            var tolls = beats.Where(b => b.Cause == BeatCause.RoundStart).ToList();
            Assert.IsTrue(tolls.All(b => ReferenceEquals(b.Actor, bell)), "every toll is the bell's");
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 },
                beats.Where(b => b.RoundStarted > 0).Select(b => b.RoundStarted).ToArray(),
                "each round is stamped exactly once");
            Assert.AreEqual(4, tolls.Count, "one toll per round started");

            var (plain, _, plainBell) = Solo(BellSource(rally: true), roundLimit: 4);
            var bare = plain.DrainBeats().Where(b => b.Cause == BeatCause.RoundStart).ToList();
            Assert.AreEqual(1, bare.Count);
            Assert.IsNull(bare[0].Actor, "no presentation: the round's beat is nobody's, as before");
        }

        [Test]
        public void WithoutALimit_OnlyAPresentedTollRecordsABeat_AndNoRoundIsStamped()
        {
            var (session, _, bell) = Solo(BellSource(presents: true, rally: true));

            var tolls = session.DrainBeats().Where(b => b.Cause == BeatCause.RoundStart).ToList();
            Assert.AreEqual(1, tolls.Count);
            Assert.AreSame(bell, tolls[0].Actor);
            Assert.AreEqual(0, tolls[0].RoundStarted, "a room fight shows no round counter");
        }

        // ---- the preview showcase -------------------------------------------------------

        [Test]
        public void TheShowcasePlaysAScheduledWeightZeroEntry_AndStillSkipsAnUnscheduledOne()
        {
            var pool = new List<EnemyAbility>
            {
                EnemyAbility.Of(Skill("scratch", "Scratch", SkillEffect.DamageSingle), 1f),
                EnemyAbility.Of(Skill("chains", "Dark Chains", SkillEffect.Reposition, toSeat: 1), 0f),
                EnemyAbility.Of(Skill("knell", "Death Knell", SkillEffect.DamageSingle, KnellTable), 0f),
            };
            var enemy = new object();

            var scheduled = new EnemyShowcase();
            var picks = Enumerable.Range(0, 4).Select(_ => scheduled.Next(enemy, pool, new HashSet<int> { 1, 2 })).ToList();
            CollectionAssert.AreEqual(new[] { 0, 1, 2, -1 }, picks, "Scratch, Chains, Knell, then the plain swing");
            CollectionAssert.IsEmpty(scheduled.Skipped);

            var unscheduled = new EnemyShowcase();
            var other = new object();
            var bare = Enumerable.Range(0, 2).Select(_ => unscheduled.Next(other, pool)).ToList();
            Assert.AreEqual(0, bare[0]);
            CollectionAssert.AreEqual(new[] { "Dark Chains", "Death Knell" }, unscheduled.Skipped);
        }

        [Test]
        public void ASessionShowcaseShowsTheBellwethersWholeKit()
        {
            var shawn = new CombatantState("Shawn", true, 100000, 40, 20, 30);
            var foe = new CombatantState("Bell", false, 100000, 0, 0, 1);
            var fight = new FightSession(new CombatEncounter(new[] { shawn }, new[] { foe }),
                new List<PlayerKit> { new PlayerKit("sheep", CharacterRole.Support, null, null, DamageType.Physical) },
                new List<EnemyKit> { BellKit(BellSource()) }, new SeededRandom(5))
            {
                DamageVarianceRange = 0f,
                Showcase = new EnemyShowcase(),
            };
            fight.Begin();

            var seen = new List<string>();
            for (int guard = 0; fight.ActingTurnsOf(foe) < 3 && guard < 300; guard++)
            {
                if (fight.ActingTurnsOf(foe) == seen.Count) seen.Add(fight.IntentDetailFor(foe).Value.Label);
                fight.Encounter.PlaceAt(shawn, 2, out _);
                Assert.IsTrue(fight.ExecuteAttack(foe));
            }

            CollectionAssert.AreEqual(new[] { "Scratch", "Dark Chains", "Death Knell" }, seen);
        }
    }
}

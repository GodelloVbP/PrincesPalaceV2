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
    // THREE POSES OF ONE BLOW, AND ONE ACTOR WEARING ANOTHER ACTOR'S ART.
    //
    // Two requests through one seam: a skill may author what the caster wears
    // while it travels and while it winds up, on top of the strike it already
    // authored, and a transform may author the stance FOLDER its holder is
    // drawn from while it runs. Both are content that has to survive
    // skills.json -> RawSkillEntry -> ResolvedSkill -> CombatBeat, and both are
    // useless if the last hop drops them -- which is exactly the failure
    // AUDIT #60 records happening twice to `transform` itself.
    //
    // THE SkillDefinition HOP IS NOT TESTED HERE and does not need to be:
    // the asset stores the whole ResolvedSkill (`SkillDefinition.Data`) rather
    // than restating its field list, so there is nothing between Resolved and
    // the catalogue for a new field to fall out of. That is the entire point of
    // the wrapper, and its own header says so.
    public class PhaseStancesAndFormsTests
    {
        // ---- the precedence table ------------------------------------------
        //
        // CombatBeat's own header states it; these are the cells. The
        // fallbacks are the interesting half, because they are what an
        // unauthored beat -- every beat in the game bar two -- actually takes.

        private const string Strike = "slam";
        private const string Rush = "rush";
        private const string Overhead = "overhead";

        [Test]
        public void NothingAuthored_OpensOnTheStrike_ForEveryApproach()
        {
            // The regression cell. A beat that authored no phase poses has to
            // behave byte for byte as it always did: the strike stance from
            // the beat's open, and no second moment for anything to change at.
            foreach (var approach in Approaches())
            {
                Assert.AreEqual(Strike, CombatBeat.OpenStanceFor(approach, Strike, "", ""),
                    $"{approach} stopped opening on its strike stance when nothing else was authored");
                Assert.IsNull(CombatBeat.ArrivalStanceFor(approach, Strike, "", ""),
                    $"{approach} grew an arrival pose out of nothing");
            }
        }

        [Test]
        public void AHoldIgnoresAnApproachPose_BecauseNothingTravels()
        {
            // Documented rather than refused: a skill's approach can be edited
            // without its poses being rewritten, and a stage that silently
            // stood still is better than a build that stopped.
            Assert.AreEqual(Strike, CombatBeat.OpenStanceFor(StageApproach.Hold, Strike, Rush, ""));
            Assert.AreEqual(Overhead, CombatBeat.OpenStanceFor(StageApproach.Hold, Strike, Rush, Overhead),
                "a Hold that authored both should wear its wind-up, not its unreachable approach");
        }

        [Test]
        public void AHoldWearsItsWindupFromTheOpen()
        {
            Assert.AreEqual(Overhead, CombatBeat.OpenStanceFor(StageApproach.Hold, Strike, "", Overhead));
            Assert.IsNull(CombatBeat.ArrivalStanceFor(StageApproach.Hold, Strike, "", Overhead),
                "a Hold has no arrival -- it never went anywhere");
        }

        [Test]
        public void ACloseWalksInOnOneAndRaisesOnTheOther()
        {
            // Bjorn's Slam, which is the whole reason any of this exists: "he
            // rushes forward in the first frame, then he holds his hammer over
            // his head, then he slams down".
            Assert.AreEqual(Rush, CombatBeat.OpenStanceFor(StageApproach.Close, Strike, Rush, Overhead));
            Assert.AreEqual(Overhead, CombatBeat.ArrivalStanceFor(StageApproach.Close, Strike, Rush, Overhead));
        }

        [Test]
        public void ACloseWithOnlyAWindupKeepsItsStrikeThroughTheWalkIn()
        {
            Assert.AreEqual(Strike, CombatBeat.OpenStanceFor(StageApproach.Close, Strike, "", Overhead));
            Assert.AreEqual(Overhead, CombatBeat.ArrivalStanceFor(StageApproach.Close, Strike, "", Overhead));
        }

        [Test]
        public void ACloseWithOnlyAnApproachChangesNothingOnArrival()
        {
            Assert.AreEqual(Rush, CombatBeat.OpenStanceFor(StageApproach.Close, Strike, Rush, ""));
            Assert.IsNull(CombatBeat.ArrivalStanceFor(StageApproach.Close, Strike, Rush, ""),
                "there is nothing to change into on arrival, so the strike waits for the impact");
        }

        [Test]
        public void ALungeOrChargeHasOnePreImpactInterval_AndTheApproachPoseWinsIt()
        {
            // Deliberate, and the deliberate part is the LAST assertion: a
            // Lunge's crouch-and-cross is one wait with no halves in it, so
            // authoring both drops the wind-up rather than inventing a
            // midpoint the animator knows nothing about.
            foreach (var approach in new[] { StageApproach.Lunge, StageApproach.Charge })
            {
                Assert.AreEqual(Rush, CombatBeat.OpenStanceFor(approach, Strike, Rush, ""));
                Assert.AreEqual(Overhead, CombatBeat.OpenStanceFor(approach, Strike, "", Overhead));
                Assert.AreEqual(Rush, CombatBeat.OpenStanceFor(approach, Strike, Rush, Overhead),
                    $"{approach} has one interval before impact and it is the travel");
                Assert.IsNull(CombatBeat.ArrivalStanceFor(approach, Strike, Rush, Overhead));
            }
        }

        [Test]
        public void NoStrikeMeansNoPhases()
        {
            // A phase pose is what is worn BEFORE the strike. With no strike
            // recorded there is nothing to change back into at impact, and the
            // return-to-idle at the end of a beat walks Stances -- which would
            // not mention the actor. The figure would hold its wind-up for the
            // rest of the fight.
            foreach (var approach in Approaches())
            {
                Assert.IsNull(CombatBeat.OpenStanceFor(approach, null, Rush, Overhead),
                    $"{approach} wore a phase pose it can never take off");
                Assert.IsNull(CombatBeat.ArrivalStanceFor(approach, "  ", Rush, Overhead));
            }
        }

        private static IEnumerable<StageApproach> Approaches() =>
            new[] { StageApproach.Hold, StageApproach.Lunge, StageApproach.Close, StageApproach.Charge };

        // ---- the fields survive the resolver -------------------------------

        [Test]
        public void PhaseStancesSurviveRawToResolved_Trimmed()
        {
            var skill = Resolve(new RawSkillEntry
            {
                id = "slam",
                displayName = "Slam",
                characterId = "bear",
                unlockLevel = 1,
                effect = "DamageSingle",
                manaCost = 6,
                flatAmount = 12,
                approach = "close",
                approachStance = "  rush  ",
                windupStance = "overhead",
                stance = "slam",
            });

            Assert.AreEqual("rush", skill.ApproachStance, "the approach pose was dropped or left untrimmed");
            Assert.AreEqual("overhead", skill.WindupStance);
            Assert.AreEqual("slam", skill.Stance, "the strike is still the strike");
            Assert.AreEqual(StageApproach.Close, skill.Approach);
        }

        [Test]
        public void AnUnauthoredPhaseStanceResolvesEmpty()
        {
            var skill = Resolve(new RawSkillEntry
            {
                id = "plain",
                displayName = "Plain",
                characterId = "bear",
                unlockLevel = 1,
                effect = "DamageSingle",
                manaCost = 4,
                flatAmount = 5,
            });

            Assert.AreEqual("", skill.ApproachStance);
            Assert.AreEqual("", skill.WindupStance);
        }

        [Test]
        public void ATransformsSpritePathSurvivesRawToResolvedToTheTransformation()
        {
            var skill = Resolve(TransformEntry("Characters/owl"));

            Assert.IsNotNull(skill.Transform);
            Assert.AreEqual("Characters/owl", skill.Transform.spritePath,
                "the form's folder was dropped between the JSON and the resolved skill");

            var holder = new CombatantState("Shawn", true, 100, 10, 10, 5);
            var running = Transformation.Enter(holder, skill.Transform.displayName, skill.Transform.turns,
                skill.Transform.attackPercent, skill.Transform.speedPercent,
                skill.Transform.temporaryHealthPercent, skill.Transform.splashPercent,
                skill.Transform.spritePath);

            Assert.AreEqual("Characters/owl", running.SpritePath);
            Assert.AreSame(running, holder.Transformation);
        }

        [Test]
        public void ATransformThatNamesNoFolderKeepsItsOwnArt()
        {
            var skill = Resolve(TransformEntry(""));
            Assert.AreEqual("", skill.Transform.spritePath);

            var holder = new CombatantState("Shawn", true, 100, 10, 10, 5);
            Transformation.Enter(holder, "X", 2, 50, 0, 0, 0, skill.Transform.spritePath);

            Assert.AreEqual("", holder.Transformation.SpritePath);
        }

        // ---- and reach a beat ----------------------------------------------

        [Test]
        public void CastingAPhasedSkillRecordsAllThreePosesOnOneBeat()
        {
            var (session, hero, foe) = Fight();

            var slam = Resolve(new RawSkillEntry
            {
                id = "slam",
                displayName = "Slam",
                characterId = "bear",
                unlockLevel = 1,
                effect = "DamageSingle",
                manaCost = 6,
                flatAmount = 12,
                approach = "close",
                approachStance = "rush",
                windupStance = "overhead",
                stance = "slam",
            });

            Assert.IsTrue(session.CastSkill(slam, foe));

            var beat = session.DrainBeats().Single(b => ReferenceEquals(b.Actor, hero));

            Assert.AreEqual("slam", beat.Stances[hero],
                "Stances[actor] still holds the STRIKE -- the two new fields are additions, not a replacement");
            Assert.AreEqual("rush", beat.ActorApproachStance);
            Assert.AreEqual("overhead", beat.ActorWindupStance);
        }

        [Test]
        public void CastingAnUnphasedSkillRecordsNullForBothPhases()
        {
            // Null, not "": the player treats null as unauthored and every
            // skill in the game bar two is this case.
            var (session, hero, foe) = Fight();

            var plain = Resolve(new RawSkillEntry
            {
                id = "plain",
                displayName = "Plain",
                characterId = "bear",
                unlockLevel = 1,
                effect = "DamageSingle",
                manaCost = 4,
                flatAmount = 5,
            });

            Assert.IsTrue(session.CastSkill(plain, foe));

            var beat = session.DrainBeats().Single(b => ReferenceEquals(b.Actor, hero));

            Assert.AreEqual(FightSession.Stances.Cast, beat.Stances[hero]);
            Assert.IsNull(beat.ActorApproachStance);
            Assert.IsNull(beat.ActorWindupStance);
            Assert.IsNull(beat.Forms, "a beat that transforms nobody must not carry a form map at all");
        }

        [Test]
        public void TheTransformBeatRecordsTheFormRatherThanLeavingItToLiveState()
        {
            // THE WHOLE OF REQUEST 2'S TIMING. Resolution is synchronous and
            // playback is later, so `hero.Transformation` is already set the
            // moment this returns -- a view reading live state would draw the
            // Black Ram from the beat's first frame, before the flash that is
            // meant to hide the change. The beat has to carry it.
            var (session, hero, _) = Fight();

            Assert.IsTrue(session.CastSkill(Resolve(TransformEntry("Characters/owl")), hero));

            var beat = session.DrainBeats().Single(b => ReferenceEquals(b.Actor, hero));

            Assert.IsNotNull(beat.Forms, "the transform beat recorded no form change");
            Assert.AreEqual("Characters/owl", beat.Forms[hero]);
            Assert.AreEqual("Characters/owl", hero.Transformation.SpritePath,
                "and live state agrees, which is what the post-playback resync reads");
        }

        // ---- fixture --------------------------------------------------------

        private static RawSkillEntry TransformEntry(string spritePath) =>
            new RawSkillEntry
            {
                id = "become",
                displayName = "Become",
                characterId = "sheep",
                unlockLevel = 1,
                effect = "Transform",
                targeting = "Self",
                manaCost = 3,
                stance = "victory",
                windupStance = "victory",
                shake = 0.7f,
                transform = new TransformGrant
                {
                    displayName = "the Black Ram",
                    turns = 3,
                    attackPercent = 50,
                    spritePath = spritePath,
                },
            };

        private static ResolvedSkill Resolve(RawSkillEntry raw)
        {
            bool ok = SkillEntryResolver.TryResolveAll(
                new List<RawSkillEntry> { raw }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            return resolved[0];
        }

        // One hero, one punchbag, no variance -- nothing here is about numbers.
        private static (FightSession session, CombatantState hero, CombatantState foe) Fight()
        {
            var hero = new CombatantState("Bjorn", true, 999, 999, 20, 10);
            var foe = new CombatantState("Foe", false, 999, 0, 1, 1);

            var kit = new PlayerKit("bear", CharacterRole.Utility,
                new List<ResolvedSkill>(), new List<ResolvedRelic>(), null);

            var enemyKits = new List<EnemyKit>
            {
                new EnemyKit(new ResolvedEnemy("foe", "Foe", new StatBlock(), 0, 0, false,
                    ElementalAffinity.Neutral, 0), false),
            };

            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { kit }, enemyKits, new SeededRandom(7)) { DamageVarianceRange = 0f };
            session.Begin();

            return (session, hero, foe);
        }
    }
}

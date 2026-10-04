using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // BORROWED MOMENT'S BEATS as literal seconds (docs/PLAN_SPELL_REWORK.md 3.9):
    // the hourglass turns for 0.3 s, the cue frame is the cyan chevrons, and the
    // grains rise off the ally. Its payoff is a position, so the turn queue the
    // beat opens on is the one BEFORE the move and the tracker changes on the cue.
    public class BorrowedMomentPresentationTests
    {
        private static SpellPresentation Borrowed()
        {
            string skills = File.ReadAllText(Path.Combine(RepoTree.Root(), "Assets", "_Project",
                "ContentData", "skills.json"));
            string skill = JsonBlocks.ObjectsInArray(skills, "skills")
                .Single(s => JsonBlocks.String(s, "id") == "borrowed_moment");
            return SpellVfxJson.OwnVfx(skill);
        }

        [Test]
        public void TheHourglassTurnsForTheCueAndTheChevronsLandOnIt()
        {
            var vfx = Borrowed();
            Assert.AreEqual(0.3f, vfx.hitCueSeconds, 1e-6f);

            var turn = vfx.layers.Single(l => l.id == "ritual");
            Assert.AreEqual("target-centre", turn.place, "on the ally");
            Assert.AreEqual(16.667f, turn.fps, 1e-3f);
            Assert.AreEqual(0.3f, turn.seconds, 1e-6f, "f0-f4");
            Assert.AreEqual(0f, turn.fade, 1e-6f, "the starburst replaces it on the cue");

            var quicken = vfx.layers.Single(l => l.id == "quicken");
            Assert.AreEqual("hit", quicken.at);
            Assert.AreEqual(0f, quicken.offset, 1e-6f);
            Assert.AreEqual("Spells/borrowed_moment_quicken", quicken.path);
            Assert.AreEqual("none", quicken.facing, "the chevrons point toward the enemy whichever side the ally stands");
            Assert.AreEqual(0.45f, quicken.drift.toX, 1e-6f, "slid forward");
            Assert.AreEqual(1.0f, quicken.glow, 1e-6f);
        }

        [Test]
        public void TheGrainsRiseAndEverythingIsCleanHalfASecondAfterTheCue()
        {
            var vfx = Borrowed();
            Assert.IsFalse(vfx.layers.Any(l => l.id == "sand" || l.id == "burst"), "the breaking hourglass and the starburst are gone");
            var quicken = vfx.layers.Single(l => l.id == "quicken");
            Assert.AreEqual(0.45f, quicken.seconds + quicken.fade, 1e-6f, "payoff to clean");

            var grains = vfx.layers.Single(l => l.id == "grains").emitter;
            Assert.AreEqual(300f, grains.gravity, 1e-6f, "pulls up: the grains quicken");
            CollectionAssert.AreEqual(new[] { 1f, 0f, 0f, 1f }, grains.weights, "sand and glints, not the swirl or the gear");
            Assert.AreEqual(0.4f, grains.lifeMax, 1e-6f);
        }

        // ---- the tracker moves on the cue, not before the cast ------------------

        private static ResolvedSkill BorrowedMoment() =>
            new ResolvedSkill("borrowed_moment", "Borrowed Moment", "test fixture", "sheep", 1,
                SkillEffect.Hasten, SkillTargeting.SingleAlly, 8, 0, false, 0, 0, false,
                null, SpellPresentation.None, 0, cooldownTurns: 2, advanceSlots: 2);

        [Test]
        public void ThePositionCastsBeatOpensOnTheOldOrderAndCarriesTheNewOne()
        {
            var caster = new CombatantState("Caster", true, 300, 50, 40, 30);
            var next = new CombatantState("Next", true, 300, 30, 20, 20);
            var back = new CombatantState("Back", true, 300, 30, 20, 4);
            var foe = new CombatantState("Foe", false, 5000, 10, 5, 1);
            var kits = new List<PlayerKit>
            {
                new PlayerKit("sheep", CharacterRole.Support, new[] { BorrowedMoment() }, null, DamageType.Physical),
                new PlayerKit("bear", CharacterRole.Tank, null, null, DamageType.Physical),
                new PlayerKit("owl", CharacterRole.Utility, null, null, DamageType.Arcane),
            };
            var session = new FightSession(new CombatEncounter(new[] { caster, next, back }, new[] { foe }),
                kits, null, new SeededRandom(5)) { DamageVarianceRange = 0f };

            var before = session.Encounter.UpcomingTurns(FightHudSpec.InitiativeSlots).ToList();
            int backBefore = before.IndexOf(back);

            Assert.IsTrue(session.CastSkill(0, back));
            var beat = session.DrainBeats().First();

            CollectionAssert.AreEqual(before, beat.PreTurnOrder.ToList(), "the beat opens on the order before the move");
            Assert.AreEqual(backBefore, beat.PreTurnOrder.ToList().IndexOf(back));
            Assert.Less(beat.TurnOrder.ToList().IndexOf(back), backBefore, "and carries the order after it");
            Assert.AreEqual(1, backBefore - beat.TurnOrder.ToList().IndexOf(back), "two slots are two charge levels, which is one place on this board");
        }

        [Test]
        public void AnyOtherBeatPaintsItsOrderFromTheStartBecauseItMovesNobody()
        {
            var hero = new CombatantState("Hero", true, 300, 50, 40, 30);
            var foe = new CombatantState("Foe", false, 5000, 10, 5, 1);
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { new PlayerKit("sheep", CharacterRole.Support, null, null, DamageType.Physical) },
                null, new SeededRandom(5)) { DamageVarianceRange = 0f };

            session.ExecuteAttack(foe);

            Assert.IsNull(session.DrainBeats().First().PreTurnOrder);
        }
    }
}

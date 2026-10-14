using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Bot;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // Finding 4 (code review): Core/Bot/BotRunDriver.cs computed
    // roomTrace.EncounterClass from its own inline copy of a ranking
    // (session.IsEliteFight ? Elite : Normal) that never checked IsBossFight
    // at all, so a boss room's TRACE reported Elite/Normal while its pages
    // were rolled as Boss. Both call sites read one property,
    // FightSession.EncounterClass, so they cannot drift apart again.
    public class BossEncounterClassTests
    {
        [SetUp]
        public void UseInMemorySaves() => BotRunDriver.InMemorySaves = true;

        [TearDown]
        public void RestoreDefault() => BotRunDriver.InMemorySaves = true;

        // FightSession.EncounterClass itself, pinned directly: Boss beats
        // Elite beats Normal, regardless of which single flag is set.
        [Test]
        public void EncounterClassRanksBossAboveEliteAboveNormal()
        {
            var hero = new CombatantState("Shawn", true, 100, 0, 10, 10);
            var foe = new CombatantState("Foe", false, 100, 0, 10, 10);
            FightSession Session(bool boss, bool elite) =>
                new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                    null, null, new SeededRandom(1), isBossFight: boss, isEliteFight: elite);

            Assert.AreEqual(EncounterClass.Normal, Session(false, false).EncounterClass);
            Assert.AreEqual(EncounterClass.Elite, Session(false, true).EncounterClass);
            Assert.AreEqual(EncounterClass.Boss, Session(true, false).EncounterClass);
            Assert.AreEqual(EncounterClass.Boss, Session(true, true).EncounterClass,
                "Boss must win even if IsEliteFight also happens to be true");
        }

        // The chips a fight of each class pays: pinned as literals, because the
        // pages a settlement rolls are keyed to the highest class among them.
        [Test]
        public void AFightPaysAGearChipOfItsClassAndAnchorsAndBossesARelicChipToo()
        {
            RunOrchestrator.ChipsFor(EncounterClass.Boss, out var bossGear, out var bossRelic);
            CollectionAssert.AreEqual(new[] { EncounterClass.Boss }, bossGear);
            CollectionAssert.AreEqual(new[] { EncounterClass.Boss }, bossRelic);

            RunOrchestrator.ChipsFor(EncounterClass.Elite, out var eliteGear, out var eliteRelic);
            CollectionAssert.AreEqual(new[] { EncounterClass.Elite }, eliteGear);
            CollectionAssert.AreEqual(new[] { EncounterClass.Elite }, eliteRelic);

            RunOrchestrator.ChipsFor(EncounterClass.Normal, out var normalGear, out var normalRelic);
            CollectionAssert.AreEqual(new[] { EncounterClass.Normal }, normalGear);
            CollectionAssert.IsEmpty(normalRelic);
        }

        // BotRunDriver's own trace, end to end through a real run: every
        // realm ends on its heart, a Boss room at offset RealmMap.SpineLength,
        // regardless of seed, so any run surviving 8 steps hits one. A
        // handful of seeds covers the (rare, unrelated) chance a run dies
        // first.
        [Test]
        public void ABossRoomsTraceReportsBossFromBotRunDriverToo()
        {
            const int depthCapSteps = 8;
            RoomTrace bossRoom = null;

            for (ulong seed = 1; seed <= 10 && bossRoom == null; seed++)
            {
                var result = BotRunDriver.PlayRun(seed, "RandomLegal", ProfilePresets.Fresh, depthCapSteps);
                bossRoom = result.Trace.Rooms.FirstOrDefault(r => r.RoomType == "Boss" && r.Offers.Count > 0);
            }

            Assert.IsNotNull(bossRoom,
                "no seed in 1..10 reached a resolved boss room with an offer at depth 8 -- widen the seed range");
            Assert.AreEqual("Boss", bossRoom.EncounterClass,
                "a boss room's own trace must report EncounterClass Boss, the same class the settlement rolled its pages as");
        }
    }
}

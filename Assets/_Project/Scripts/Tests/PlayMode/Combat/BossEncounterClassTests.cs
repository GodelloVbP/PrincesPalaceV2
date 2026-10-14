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
    // roomTrace.EncounterClass from its own inline copy of RunOrchestrator.
    // RollOffers' ranking (session.IsEliteFight ? Elite : Normal), which
    // never checked IsBossFight at all -- so once RollOffers itself was
    // fixed to check Boss first, a boss room's TRACE kept reporting
    // Elite/Normal even though the OFFER it traced had actually been rolled
    // as Boss. The fix folds both call sites onto one property,
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

        // RunOrchestrator.RollOffers' OWN read of a boss session: proven
        // through the effect only a Boss classification produces (boss_gear
        // centres one tier above the depth), since RollOffers does not
        // expose the EncounterClass it resolved directly.
        [Test]
        public void RollOffersReadsABossSessionAsBoss()
        {
            var hero = new CombatantState("Shawn", true, 100, 0, 10, 10);
            var foe = new CombatantState("Foe", false, 100, 0, 10, 10);
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                null, null, new SeededRandom(1), isBossFight: true);

            // bound - 1 fails every rung, so the tier is the bare centre:
            // 1 for a boss at depth 0, 0 for anything else.
            var offers = RunOrchestrator.RollOffers(session, bound => bound - 1);

            const int floor = 1;
            Assert.IsTrue(offers.All(o => o.Tier >= floor),
                $"a Boss session at depth 0 must roll every offer at or above the boss centre ({floor}) -- " +
                "a lower tier here means RollOffers read this session as Elite/Normal, not Boss");
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
                "a boss room's own trace must report EncounterClass Boss, the same class RollOffers used to roll its offer");
        }
    }
}

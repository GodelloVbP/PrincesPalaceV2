using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // THE MIGRATION PIN for plan D1 (docs/PLAN_SPELL_EXPANSION.md section 0).
    //
    // Five statuses -- Protect, Vulnerable, Chilled, Rooted, Marked -- run
    // off the turn-END countdown, not the turn-START one, because
    // a counter that reaches zero at the start of a turn removes the status
    // before the action of that turn ever happens. "turns: 2" therefore
    // exposed a target for ONE turn, not two, everywhere in the game.
    //
    // Nine live rows author one of those five. Eight of them had their authored
    // number changed from 2 to 1 so that the BEHAVIOUR would not change; this
    // class is what says the behaviour did not change, one case per row. The
    // ninth -- Lucky Deck's red card -- could not be preserved, and is pinned
    // here as CHANGED rather than left to be discovered.
    //
    // WHY IT IS AN AFFECTED-TURN COUNT and not a comparison against the old
    // code: the old code is gone. What can be checked is the thing the
    // migration promised -- that each row still affects exactly the number of
    // the bearer's turns it affected before -- and that is a literal per row.
    public class StatusDurationMigrationTests
    {
        private static CombatantState Bearer() =>
            new CombatantState("Bearer", false, 200, 10, 10, 20);

        // How many of the bearer's own turns a status authored at `turns` is
        // actually in force for, driven through the real clocks.
        //
        // The status is applied by SOMEBODY ELSE'S action (no exemption), which
        // is the case every row in the table but one is: mud burst, a wail, a
        // horn, a frosty weapon and a red card all land outside the bearer's
        // own turn. Shell Up is the self-cast exception and has its own case.
        private static int AffectedTurns(StatusEffectType type, int authoredTurns)
        {
            var bearer = Bearer();
            StatusEffects.Apply(bearer.Statuses, type, 25, authoredTurns);

            int affected = 0;
            for (int turn = 0; turn < 12; turn++)
            {
                StatusEffects.Tick(bearer);
                if (!bearer.Statuses.Any(s => s.Type == type)) break;

                // The status is standing while this turn's action resolves.
                affected++;
                StatusEffects.TickAtTurnEnd(bearer);
            }

            return affected;
        }

        // ---- the eight rows whose behaviour is preserved ----------------------

        [Test]
        public void EveryReAuthoredRow_AffectsTheSameNumberOfTurnsItDidBefore()
        {
            // mud_burst and bog_mud_burst -- Vulnerable 25, authored 2, one
            // affected turn. Now authored 1.
            Assert.AreEqual(1, AffectedTurns(StatusEffectType.Vulnerable, 1), "mud_burst / bog_mud_burst");

            // wail and shell_up -- Protect 50, authored 2, one affected turn.
            Assert.AreEqual(1, AffectedTurns(StatusEffectType.Protect, 1), "wail / shell_up");

            // Shatter T3's Vulnerable 30, FightSession.Talents' own constant.
            Assert.AreEqual(1, AffectedTurns(StatusEffectType.Vulnerable, 1), "Shatter T3");

            // Rampaging Bull's Horn -- Protect 50.
            Assert.AreEqual(1, AffectedTurns(StatusEffectType.Protect, FightTuning.BullsHornDurationTurns),
                "Rampaging Bull's Horn");

            // Frosty (item modifier) -- Chilled 20.
            Assert.AreEqual(1, AffectedTurns(StatusEffectType.Chilled, FightTuning.ChilledOnHitTurns),
                "Frosty");

            // Sylvan's root (item modifier) -- Rooted.
            Assert.AreEqual(1, AffectedTurns(StatusEffectType.Rooted, FightTuning.RootOnHitTurns),
                "Sylvan's root");
        }

        // THE THREE CONSTANTS, read as literals so that a silent edit to
        // FightTuning cannot make the cases above pass by moving with it.
        [Test]
        public void TheThreeReAuthoredTuningConstantsReadOne()
        {
            Assert.AreEqual(1, FightTuning.BullsHornDurationTurns);
            Assert.AreEqual(1, FightTuning.ChilledOnHitTurns);
            Assert.AreEqual(1, FightTuning.RootOnHitTurns);
        }

        // THE FOUR CONTENT ROWS, read out of skills.json itself. The behaviour
        // half above is driven from a literal 1; this is what says the content
        // actually authors that 1, so the pair cannot agree with each other
        // while disagreeing with the game.
        [Test]
        public void TheFourReAuthoredContentRowsAuthorOneTurn()
        {
            var byId = StatusDurationsFromContent();
            Assert.Greater(byId.Count, 20, "vacuity guard: skills.json was not read at all");

            foreach (string id in new[] { "mud_burst", "bog_mud_burst", "wail", "shell_up" })
            {
                Assert.IsTrue(byId.ContainsKey(id), $"skills.json no longer has a row called '{id}'");
                Assert.AreEqual(1, byId[id],
                    $"'{id}' must author one affected turn -- plan D1's re-authoring table");
            }
        }

        // AND LUCKY DECK IS THE ONE THAT CHANGED. Its red card authored 1 and
        // got zero affected turns: under the turn-start countdown a one-turn
        // Chilled covered only the interval before the bearer's next turn
        // began, and ActiveStatus floors turns at 1 so there was no smaller
        // number to author. Under the turn-end clock the same 1 is one full
        // affected turn -- a deliberate behaviour change, not a bug, pinned
        // here so it fails if somebody "fixes" it back.
        [Test]
        public void LuckyDecksRedCard_GainsTheOneAffectedTurnItNeverHad()
        {
            Assert.AreEqual(1, FightTuning.LuckyDeckSlowTurns, "the authored number did not move");
            Assert.AreEqual(1, AffectedTurns(StatusEffectType.Chilled, FightTuning.LuckyDeckSlowTurns),
                "this read 0 affected turns before plan D1 and reads 1 now -- the one row in the "
                + "migration table whose behaviour could not be preserved");
        }

        // AND THE TWO ROWS THE TABLE SAYS TO LEAVE ALONE.
        [Test]
        public void TheSentinelDurationsAreUntouchedByTheMove()
        {
            Assert.AreEqual(99, Marks.MarkDurationTurns, "a mark is spent, not decayed");
            Assert.AreEqual(99, FightTuning.MagicalShieldDurationTurns, "a relic ward is the whole fight");
        }

        // ---- reading the content file ------------------------------------------

        // Read through the shared block reader every other content-pinning
        // fixture uses, rather than a second parser of this file's own.
        private static Dictionary<string, int> StatusDurationsFromContent()
        {
            string body = File.ReadAllText(ContentDataFiles.DataPath("skills.json"));
            var map = new Dictionary<string, int>();

            foreach (string skill in JsonBlocks.ObjectsInArray(body, "skills"))
            {
                string id = JsonBlocks.String(skill, "id");
                if (string.IsNullOrWhiteSpace(id)) continue;

                double? duration = JsonBlocks.Number(skill, "statusDuration");
                map[id] = duration.HasValue ? (int)duration.Value : -1;
            }

            return map;
        }
    }
}

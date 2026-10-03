using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.Domain.Tests
{
    // The HUD badge rows each beat paints, beat by beat, pinned as literals so
    // moving the formatter off the recording path cannot change a single badge.
    // The beats here run a scripted sequence on one target: three statuses, a
    // ward, a Drowned Lantern mark (session state, not a status) and then the
    // Marked status on top of it, which must still read as one badge.
    public class BeatStatusRowsParityTests
    {
        private static ResolvedSkill Hit(StatusEffectType status) =>
            new ResolvedSkill("fixture", "Hex", "", "foe", 1, SkillEffect.DamageSingle,
                SkillTargeting.SingleEnemy, 0, 0, false, 0, 10, false,
                null, SpellPresentation.None, 0,
                appliesStatus: status, statusMagnitude: 25, statusDuration: 2,
                playerSelectable: false);

        private static void ForceLanternMark(FightSession session, CombatantState target)
        {
            var field = typeof(FightSession).GetField("_marked", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "FightSession._marked was not found by reflection -- has it been renamed?");
            ((HashSet<CombatantState>)field.GetValue(session)).Add(target);
        }

        private static string Rows(IReadOnlyList<FightHudModel.StatusRow> rows) =>
            rows.Count == 0
                ? "-"
                : string.Join(" ; ", rows.Select(r => $"{r.Code}/{r.Slug}/{r.Counter}/{r.SortKey}/{r.Tooltip}"));

        private static List<string> Record()
        {
            var shawn = new CombatantState("Shawn", true, 1000, 10, 20, 10);
            var foe = new CombatantState("Foe", false, 1000, 0, 0, 1);
            var session = new FightSession(new CombatEncounter(new[] { shawn }, new[] { foe }),
                null, null, new SeededRandom(1)) { DamageVarianceRange = 0f };
            session.Begin();
            session.DrainBeats();

            var log = new List<string>();
            int step = 0;
            void Cast(StatusEffectType status)
            {
                session.ResolveSkillForTest(foe, Hit(status), shawn);
                foreach (var beat in session.DrainBeats().Where(b => b.IsAction))
                {
                    step++;
                    log.Add($"{step} pre : {Rows(StatusRowsOf(beat.PreSnapshot[shawn]))}");
                    log.Add($"{step} post: {Rows(StatusRowsOf(beat.Snapshot[shawn]))}");
                }
            }

            Cast(StatusEffectType.Vulnerable);
            Cast(StatusEffectType.Poison);
            StatusEffects.ApplyWard(shawn.Statuses, 40, 3);
            Cast(StatusEffectType.Protect);
            ForceLanternMark(session, shawn);
            Cast(StatusEffectType.Regen);
            Cast(StatusEffectType.Marked);
            return log;
        }

        private static IReadOnlyList<FightHudModel.StatusRow> StatusRowsOf(Vitals vitals) =>
            FightHudModel.StatusRowsFrom(vitals.Statuses);

        [Test]
        public void EveryBeatPaintsTheSameRowsItAlwaysDid()
        {
            CollectionAssert.AreEqual(Expected, Record());
        }

        // The rows come from the snapshot alone: a status or mark that lands
        // after the beat was recorded does not appear when it is painted.
        [Test]
        public void ASnapshotFormatsWhatItRecordedWhateverTheLiveStateBecomes()
        {
            var shawn = new CombatantState("Shawn", true, 1000, 10, 20, 10);
            var foe = new CombatantState("Foe", false, 1000, 0, 0, 1);
            var session = new FightSession(new CombatEncounter(new[] { shawn }, new[] { foe }),
                null, null, new SeededRandom(1)) { DamageVarianceRange = 0f };
            session.Begin();
            var recorded = StatusSnapshot.Capture(session, shawn);

            StatusEffects.Apply(shawn.Statuses, StatusEffectType.Poison, 5, 2);
            ForceLanternMark(session, shawn);

            Assert.AreEqual(0, FightHudModel.StatusRowsFrom(recorded).Count);
            CollectionAssert.AreEquivalent(new[] { "PSN", "MRK" },
                FightHudModel.StatusRowsFor(session, shawn).Select(r => r.Code).ToArray());
        }

        private static readonly string[] Expected =
        {
            "1 pre : -",
            "1 post: VLN/vulnerable/2/2003/Vulnerable -- <color=#E05A5A>25% more damage taken</color>, 2 turns",
            "2 pre : VLN/vulnerable/2/2003/Vulnerable -- <color=#E05A5A>25% more damage taken</color>, 2 turns",
            "2 post: PSN/poison/2/2000/Poison -- <color=#E05A5A>25 damage each turn start</color>, 2 turns ; VLN/vulnerable/2/2003/Vulnerable -- <color=#E05A5A>25% more damage taken</color>, 2 turns",
            "3 pre : SHD/shielded/40/1205/Shielded -- <color=#8FE07F>40 shield</color>, 3 turns ; PSN/poison/2/2000/Poison -- <color=#E05A5A>25 damage each turn start</color>, 2 turns ; VLN/vulnerable/2/2003/Vulnerable -- <color=#E05A5A>25% more damage taken</color>, 2 turns",
            "3 post: SHD/shielded/27/1205/Shielded -- <color=#8FE07F>27 shield</color>, 3 turns ; PSN/poison/2/2000/Poison -- <color=#E05A5A>25 damage each turn start</color>, 2 turns ; VLN/vulnerable/2/2003/Vulnerable -- <color=#E05A5A>25% more damage taken</color>, 2 turns ; PRT/protect/2/2202/Protect -- <color=#8FE07F>25% less damage taken</color>, 2 turns",
            "4 pre : SHD/shielded/27/1205/Shielded -- <color=#8FE07F>27 shield</color>, 3 turns ; PSN/poison/2/2000/Poison -- <color=#E05A5A>25 damage each turn start</color>, 2 turns ; VLN/vulnerable/2/2003/Vulnerable -- <color=#E05A5A>25% more damage taken</color>, 2 turns ; MRK/marked/-1/2010/Marked -- <color=#E05A5A>open to focused attacks</color>, until it is used ; PRT/protect/2/2202/Protect -- <color=#8FE07F>25% less damage taken</color>, 2 turns",
            "4 post: SHD/shielded/17/1205/Shielded -- <color=#8FE07F>17 shield</color>, 3 turns ; PSN/poison/2/2000/Poison -- <color=#E05A5A>25 damage each turn start</color>, 2 turns ; VLN/vulnerable/2/2003/Vulnerable -- <color=#E05A5A>25% more damage taken</color>, 2 turns ; MRK/marked/-1/2010/Marked -- <color=#E05A5A>open to focused attacks</color>, until it is used ; RGN/regen/2/2201/Regen -- <color=#8FE07F>25 healing each turn start</color>, 2 turns ; PRT/protect/2/2202/Protect -- <color=#8FE07F>25% less damage taken</color>, 2 turns",
            "5 pre : SHD/shielded/17/1205/Shielded -- <color=#8FE07F>17 shield</color>, 3 turns ; PSN/poison/2/2000/Poison -- <color=#E05A5A>25 damage each turn start</color>, 2 turns ; VLN/vulnerable/2/2003/Vulnerable -- <color=#E05A5A>25% more damage taken</color>, 2 turns ; MRK/marked/-1/2010/Marked -- <color=#E05A5A>open to focused attacks</color>, until it is used ; RGN/regen/2/2201/Regen -- <color=#8FE07F>25 healing each turn start</color>, 2 turns ; PRT/protect/2/2202/Protect -- <color=#8FE07F>25% less damage taken</color>, 2 turns",
            "5 post: SHD/shielded/7/1205/Shielded -- <color=#8FE07F>7 shield</color>, 3 turns ; PSN/poison/2/2000/Poison -- <color=#E05A5A>25 damage each turn start</color>, 2 turns ; VLN/vulnerable/2/2003/Vulnerable -- <color=#E05A5A>25% more damage taken</color>, 2 turns ; MRK/marked/2/2010/Marked -- <color=#E05A5A>open to focused attacks</color>, 2 turns ; RGN/regen/2/2201/Regen -- <color=#8FE07F>25 healing each turn start</color>, 2 turns ; PRT/protect/2/2202/Protect -- <color=#8FE07F>25% less damage taken</color>, 2 turns",
        };
    }
}

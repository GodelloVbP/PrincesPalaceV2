using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.PlayModeTests
{
    // THE M4 KNELL FIXTURE, shared by KnellBadgeTests (headless) and
    // KnellBadgeCaptureTests (the picture): a real Shawn with Palace Passage
    // against a fixture bell whose schedule opens Chains then a seat-sized
    // Knell (docs/PLAN_BELLWETHER_KIT.md M4).
    internal static class KnellBadgeFixture
    {
        private static ResolvedSkill Skill(string id, string name, SkillEffect effect, int[] bySeat = null,
            int toSeat = 0) =>
            new ResolvedSkill(id, name, "", "bell", 1, effect,
                SkillTargeting.SingleEnemy, 0, 0, false, 0, 1, bySeat != null,
                null, SpellPresentation.None, 0, playerSelectable: false,
                damageType: bySeat != null ? DamageType.Void : (DamageType?)null,
                damageBySeatMaxHpPercent: bySeat, toSeat: toSeat);

        public static (FightSession session, CombatantState shawn, CombatantState bell) Bind(FightController fight)
        {
            var built = FightEncounterAdapter.Build(new List<string> { "sheep" }, new List<string> { "bellwether" },
                new SeededRandom(9), previewExtraSkillIds: new List<string> { "palace_passage" });
            Assert.IsNotNull(built?.Session, "fixture: the solo fight could not be built");
            var shawn = built.Party[0];
            var kit = built.Session.KitFor(shawn);
            Assert.IsNotNull(kit);

            // The real Bellwether's art, a fixture kit and schedule, speed 1 so
            // Shawn holds many turns between two of its.
            var bell = new CombatantState("The Bellwether", false, 100000, 0, 0, 1);
            var source = new ResolvedEnemy("bellwether", "The Bellwether", new StatBlock(), 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0, spritePath: "Enemies/bellwether")
            {
                Schedule = new[] { new EnemyScheduleEntry(new[] { 1 }, new[] { "chains", "knell" }) },
            };
            var enemyKit = new EnemyKit(source, false, new List<EnemyAbility>
            {
                EnemyAbility.Of(Skill("scratch", "Scratch", SkillEffect.DamageSingle), 1f),
                EnemyAbility.Of(Skill("chains", "Dark Chains", SkillEffect.Reposition, toSeat: 1), 0f),
                EnemyAbility.Of(Skill("knell", "Death Knell", SkillEffect.DamageSingle, new[] { 110, 35, 0 }), 0f),
            });

            var encounter = new CombatEncounter(new[] { shawn }, new[] { bell });
            Assert.AreEqual(PlaceOutcome.Placed, encounter.PlaceAt(shawn, 2, out _), "fixture: Shawn opens at the rear");

            var session = new FightSession(encounter, new List<PlayerKit> { kit }, new List<EnemyKit> { enemyKit },
                new SeededRandom(9)) { DamageVarianceRange = 0f };
            session.Begin();
            Assert.AreSame(shawn, session.Current, "fixture: Shawn opens");

            fight.Bind(session, Domain.Rewards.EncounterClass.Normal);
            fight.BindPartyArt(built.Party.Take(1).ToList(), built.PartyArt.Take(1).ToList());
            return (session, shawn, bell);
        }

        // Swings until the chains have resolved and the knell is committed.
        // The beats are dropped rather than played: this is set-up, and the
        // step under test (PassageTo) goes through the real post-action path.
        public static void ToTheKnell(FightController fight, FightSession session, CombatantState bell)
        {
            for (int guard = 0; session.ActingTurnsOf(bell) < 1 && guard < 200; guard++)
            {
                Assert.IsTrue(session.ExecuteAttack(bell));
            }

            Assert.AreEqual(1, session.ActingTurnsOf(bell), "the chains never resolved");
            session.DrainBeats();
            fight.RefreshUi();
        }

        public static IEnumerator PassageTo(FightController fight, FightSession session, CombatantState shawn, int seat)
        {
            int row = session.SkillOptionsFor(shawn).ToList().FindIndex(o => o.Skill.Id == "palace_passage");
            Assert.GreaterOrEqual(row, 0, "fixture: the Passage is not on his list");
            Assert.IsTrue(session.CastSkillToSeat(row, shawn, seat));

            typeof(FightController)
                .GetMethod("AfterResolution", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(fight, null);
            yield return Settle(fight);
            Assert.AreSame(shawn, session.Current, "a free action keeps the turn");
        }

        public static IEnumerator Settle(FightController fight)
        {
            var busy = typeof(FightController).GetField("_isBusy",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            for (int i = 0; i < 600 && (bool)busy.GetValue(fight); i++) yield return null;
            Assert.IsFalse((bool)busy.GetValue(fight), "the playback never finished");
            fight.RefreshUi();
            yield return null;
        }

        private static T Wired<T>(FightController fight, string field) where T : class
        {
            var array = typeof(FightController)
                .GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(fight) as T[];
            Assert.IsNotNull(array, $"{field} is not wired");
            return array[0];
        }

        public static TMP_Text Value(FightController fight) => Wired<TMP_Text>(fight, "enemyIntentValues");
        public static TMP_Text Callout(FightController fight) => Wired<TMP_Text>(fight, "enemyIntentCallouts");

        public static Image Badge(FightController fight) =>
            Wired<GameObject>(fight, "enemyIntentIcons").GetComponent<Image>();
    }
}

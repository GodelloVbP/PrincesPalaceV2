using System;
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
    // Finding 2 (code review): Feared and Marked were added to
    // StatusEffectType but never wired into any of the four places a
    // status becomes player-facing -- FightHudModel.StatusBadge,
    // FightHudModel's PillCategory switch (CategoryOf), FightHudModel's
    // pill-code switch (PillCode), or EnemyIntentIcons.KindFor. Each has its own
    // silent "unhandled" fallback ("?", PillCategory.Special, "??") that
    // hid the gap instead of failing loud.
    //
    // These tests drive all four through their PUBLIC surface --
    // BuffBadgesFor (StatusBadge), EnemyStatusLine (PillCode + CategoryOf,
    // via the pill string it renders) and EnemyIntentIcons.KindFor directly --
    // and iterate every current StatusEffectType so the NEXT status added
    // without wiring these four in fails here first, not in a screenshot
    // review three sprints later.
    public class StatusHudCoverageTests
    {
        private static CombatantState Combatant(string name = "Target") =>
            new CombatantState(name, true, 100, 0, 10, 10);

        private static EnemyKit Foe(string name = "dummy") =>
            new EnemyKit(new ResolvedEnemy(name, name, new StatBlock(), 0, 0, false,
                DamageType.Physical, DamageType.Physical, 0), false);

        private static FightSession Session()
        {
            var hero = new CombatantState("Shawn", true, 100, 0, 10, 10);
            var foe = new CombatantState("Foe", false, 100, 0, 10, 10);
            var kit = new PlayerKit("hero", CharacterRole.Tank, null, null, null);
            var session = new FightSession(new CombatEncounter(new[] { hero }, new[] { foe }),
                new List<PlayerKit> { kit }, new List<EnemyKit> { Foe() }, new SeededRandom(1));
            session.Begin();
            return session;
        }

        // Every current status must produce a real glyph, not the
        // "unhandled status" fallback ("?").
        [Test]
        public void EveryStatusGetsAPartyBadgeGlyph()
        {
            var session = Session();

            foreach (StatusEffectType type in Enum.GetValues(typeof(StatusEffectType)))
            {
                var target = Combatant(type.ToString());
                StatusEffects.Apply(target.Statuses, type, 5, 3);

                var badges = FightHudModel.BuffBadgesFor(session, target);

                Assert.AreEqual(1, badges.Count, $"{type}: exactly one status applied, exactly one badge expected");
                Assert.AreNotEqual("?", badges[0].Glyph, $"{type} fell through StatusBadge's unhandled default");
            }
        }

        // Every current status must produce a real two-letter pill code, not
        // the "unhandled status" fallback ("??") -- checked through
        // EnemyStatusLine's rendered pill string, since PillCode itself is
        // private.
        [Test]
        public void EveryStatusGetsAnEnemyPillCode()
        {
            foreach (StatusEffectType type in Enum.GetValues(typeof(StatusEffectType)))
            {
                var enemy = Combatant(type.ToString());
                StatusEffects.Apply(enemy.Statuses, type, 5, 3);

                string line = FightHudModel.EnemyStatusLine(enemy, null);

                StringAssert.DoesNotContain("??", line, $"{type} fell through PillCode's unhandled default");
            }
        }

        // Feared and Marked specifically read as HARM on the enemy plate,
        // the SAME bucket Poison/Vulnerable/Stun already sort into -- not
        // the catch-all Special bucket every unhandled type falls into.
        // CategoryOf is private, so this is asserted through the pill
        // CAP's ordering rather than directly: PillCategory declares
        // Harm(0) < Control(1) < Special(2) < Benefit(3), and
        // EnemyStatusLine sorts pills by category before taking only the
        // first EnemyPillCap (3).
        //
        // Filling the cap with exactly three Control-category statuses
        // (Provoked, Chilled, Rooted -- the only three that exist) and then
        // adding Feared and Marked: if they are correctly Harm(0), they
        // sort AHEAD of the three Control items and bump two of those OUT
        // of the cap into overflow. If they fell through to the Special(2)
        // default, they sort BEHIND all three Control items and are
        // themselves the ones pushed into overflow, invisible.
        [Test]
        public void FearedAndMarkedSortIntoTheHarmBucketNotSpecial()
        {
            var enemy = Combatant();
            StatusEffects.Apply(enemy.Statuses, StatusEffectType.Provoked, 0, 3);
            StatusEffects.Apply(enemy.Statuses, StatusEffectType.Chilled, 10, 3);
            StatusEffects.Apply(enemy.Statuses, StatusEffectType.Rooted, 0, 3);
            StatusEffects.Apply(enemy.Statuses, StatusEffectType.Feared, 0, 3);
            StatusEffects.Apply(enemy.Statuses, StatusEffectType.Marked, 0, 3);

            string line = FightHudModel.EnemyStatusLine(enemy, null);

            StringAssert.Contains("+2", line, $"'{line}': 5 pills over a cap of 3 must overflow by exactly 2");
            var codes = line.Split(new[] { "  ·  " }, StringSplitOptions.None);
            Assert.IsTrue(codes.Any(c => c.StartsWith("FR")), $"FR missing from '{line}' -- Feared sorted behind Control, i.e. fell through to Special");
            Assert.IsTrue(codes.Any(c => c.StartsWith("MK")), $"MK missing from '{line}' -- Marked sorted behind Control, i.e. fell through to Special");
        }

        // EnemyIntentIcons.KindFor: Feared must telegraph the same "your turn
        // will not go as planned" warning Stun does, not the generic Skill
        // icon every unhandled status falls back to.
        [Test]
        public void FearedTelegraphsAStunStyleWarning()
        {
            Assert.AreEqual(EnemyIntentKind.Stun, EnemyIntentIcons.KindFor(true, StatusEffectType.Feared, true));
        }

        // Every current status must resolve to SOME defined EnemyIntentKind
        // without throwing -- a sanity net for the fourth of the four
        // functions the finding names, alongside the two specific
        // assertions above (Feared's dedicated kind here, Marked's harm
        // category above) that actually catch a missing case.
        [Test]
        public void EveryStatusResolvesToADefinedIntentKindWithoutThrowing()
        {
            foreach (StatusEffectType type in Enum.GetValues(typeof(StatusEffectType)))
            {
                EnemyIntentKind kind = default;
                Assert.DoesNotThrow(() => kind = EnemyIntentIcons.KindFor(true, type, true), $"{type} threw resolving an intent kind");
                Assert.IsTrue(Enum.IsDefined(typeof(EnemyIntentKind), kind), $"{type} resolved to an undefined EnemyIntentKind");
            }
        }
    }
}

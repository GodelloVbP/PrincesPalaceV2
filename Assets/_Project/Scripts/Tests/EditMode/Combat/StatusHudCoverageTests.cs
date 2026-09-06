using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // Finding 2 (code review, pre-2026-09-06): Feared and Marked were added
    // to StatusEffectType but never wired into every place a status becomes
    // player-facing. That finding's four checks widened here for
    // PLAN_STATUS_EFFECT_UI.md's Package A: the merged StatusHud code/slug/
    // tooltip table (section 4) replacing StatusBadge's three-letter switch
    // and PillCode's disagreeing two-letter one, and the counter sentinel
    // rule (section 2) that decides when a badge draws a number at all.
    //
    // EVERY StatusEffectType value plus the two speed presentations
    // (StatusHud has no thirteenth enum member for Speed on purpose --
    // section 4) go through StatusHud.RowFor/SpeedRow, the same functions
    // FightHudModel.StatusRowsFor calls, so a status that compiles but was
    // never wired into the table fails here first, not in a screenshot
    // review three sprints later.
    public class StatusHudCoverageTests
    {
        private static readonly StatusEffectType[] AllStatusTypes =
            (StatusEffectType[])Enum.GetValues(typeof(StatusEffectType));

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

        // ---- codes -------------------------------------------------------------

        // Every status must produce a real three-letter code, and the
        // fourteen (twelve statuses, two speed presentations) must never
        // collide -- a collision would make two different badges
        // indistinguishable at a glance, which is the one thing a code
        // exists to prevent.
        [Test]
        public void EveryStatusAndSpeedPresentationHasAUniqueThreeLetterCode()
        {
            var codes = new List<string>();

            foreach (var type in AllStatusTypes)
            {
                string code = StatusHud.CodeFor(type);
                Assert.AreEqual(3, code.Length, $"{type}'s code '{code}' is not three letters");
                Assert.AreNotEqual("?", code);
                Assert.AreNotEqual("??", code);
                codes.Add(code);
            }

            codes.Add(StatusHud.SpeedUpCode);
            codes.Add(StatusHud.SpeedDownCode);
            Assert.AreEqual(3, StatusHud.SpeedUpCode.Length);
            Assert.AreEqual(3, StatusHud.SpeedDownCode.Length);

            var distinct = codes.Distinct().ToList();
            Assert.AreEqual(14, codes.Count, "expected twelve statuses plus two speed presentations");
            Assert.AreEqual(codes.Count, distinct.Count,
                $"codes are not unique: {string.Join(", ", codes)}");
        }

        // ---- tooltips ------------------------------------------------------------

        // Every status and speed presentation must produce a real tooltip --
        // non-empty, and never leaking a "?" fallback character.
        [Test]
        public void EveryStatusAndSpeedPresentationHasANonEmptyTooltipWithNoQuestionMark()
        {
            foreach (var type in AllStatusTypes)
            {
                var status = new ActiveStatus(type, 5, 3);
                string tooltip = StatusHud.RowFor(status).Tooltip;
                Assert.IsFalse(string.IsNullOrWhiteSpace(tooltip), $"{type} produced an empty tooltip");
                StringAssert.DoesNotContain("?", tooltip, $"{type}'s tooltip fell through to an unhandled default: '{tooltip}'");
            }

            var speedUp = StatusHud.SpeedRow("Relic", 10, 3).Tooltip;
            var speedDown = StatusHud.SpeedRow("Relic", -10, 3).Tooltip;
            var speedForever = StatusHud.SpeedRow("Relic", 10, -1).Tooltip;

            foreach (var tooltip in new[] { speedUp, speedDown, speedForever })
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(tooltip));
                StringAssert.DoesNotContain("?", tooltip);
            }

            // TurnsLeft < 0 reads as "for the rest of the fight", NOT as
            // Section 2's "until it is used" -- the two sentences mean
            // different things (a permanent grant versus a spent-on-one-hit
            // status) and would be a silent lie about Shielded/Empowered's
            // own wording if they shared a phrase by accident.
            StringAssert.Contains("for the rest of the fight", speedForever);
            StringAssert.DoesNotContain("until it is used", speedForever);
        }

        // Feared must say the 25% out loud -- see PLAN_STATUS_EFFECT_UI.md
        // section 5: "Today's tooltip omits the 25% entirely" was the
        // finding against the OLD BuffBadge text this table replaces.
        [Test]
        public void FearedTooltipStatesTheVulnerablePercent()
        {
            var status = new ActiveStatus(StatusEffectType.Feared, Fear.VulnerablePercent, 2);
            StringAssert.Contains("25%", StatusHud.RowFor(status).Tooltip);
        }

        // Marked must NOT promise a damage effect it doesn't have -- see
        // section 5: "Today's tooltip promises 'increased damage from
        // focused attacks', which overstates it." Marked is a token spent by
        // Marks.ConsumeMark; what it's worth is up to whoever consumes it.
        [Test]
        public void MarkedTooltipDoesNotPromiseDamage()
        {
            var status = new ActiveStatus(StatusEffectType.Marked, 0, Marks.MarkDurationTurns);
            string tooltip = StatusHud.RowFor(status).Tooltip;
            StringAssert.DoesNotContain("damage", tooltip.ToLowerInvariant());
        }

        // ---- slugs ---------------------------------------------------------------

        // Every slug must be lowercase, extension-free, and resolve (through
        // StatusBadgeIcons) to a Resources path rooted at "Status/" -- the
        // one folder STATUS_ICON_PROMPTS.md and the import postprocessor
        // both agree on.
        [Test]
        public void EveryStatusAndSpeedPresentationHasALowercaseExtensionlessSlugUnderStatus()
        {
            foreach (var type in AllStatusTypes)
            {
                string slug = StatusHud.SlugFor(type);
                AssertSlugShape(slug, type.ToString());
                Assert.AreEqual("Status/" + slug, FightHudModel.StatusBadgeIcons.ResourceFor(type));
            }

            AssertSlugShape(StatusHud.SpeedUpSlug, "speed up");
            AssertSlugShape(StatusHud.SpeedDownSlug, "speed down");

            var speedUpRow = StatusHud.SpeedRow("Relic", 10, 3);
            var speedDownRow = StatusHud.SpeedRow("Relic", -10, 3);
            Assert.AreEqual("Status/speed", FightHudModel.StatusBadgeIcons.ResourceFor(speedUpRow));
            Assert.AreEqual("Status/speed_down", FightHudModel.StatusBadgeIcons.ResourceFor(speedDownRow));
        }

        private static void AssertSlugShape(string slug, string label)
        {
            Assert.AreEqual(slug.ToLowerInvariant(), slug, $"{label}'s slug '{slug}' is not lowercase");
            StringAssert.DoesNotContain(".", slug, $"{label}'s slug '{slug}' carries an extension");
        }

        // ---- the counter sentinel (section 2) -------------------------------------

        // The three durations this game actually authors as "for all
        // practical purposes forever" all clear the sentinel with room to
        // spare -- Marks and the Fragile Lamb's ward live outside FightTuning
        // (Marks.cs, and a private FightSession constant respectively; the
        // plan's own guess at the ward constant's location was
        // FightSession.Talents.WardDurationTurns, which does not exist as a
        // nested type -- it is a private FightSession.WardDurationTurns
        // declared in the FightSession.Talents.cs partial, reached here by
        // reflection since nothing outside FightSession needs it).
        [Test]
        public void EveryAuthoredSentinelDurationClearsTheThreshold()
        {
            Assert.GreaterOrEqual(Marks.MarkDurationTurns, StatusHud.SentinelTurns);
            Assert.GreaterOrEqual(FightTuning.MagicalShieldDurationTurns, StatusHud.SentinelTurns);

            var wardField = typeof(FightSession).GetField("WardDurationTurns", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(wardField, "FightSession.WardDurationTurns was not found by reflection -- has it moved or been renamed?");
            int wardDuration = (int)wardField.GetValue(null);
            Assert.AreEqual(999, wardDuration, "pinned literal -- see FightSession.Talents.cs's own comment on why a Ward lasts effectively forever");
            Assert.GreaterOrEqual(wardDuration, StatusHud.SentinelTurns);
        }

        // Every OTHER duration FightTuning authors is a real, tickable
        // number, and must stay well clear of the sentinel from the other
        // side -- this is what stops a future 120-turn authored duration
        // from silently reading as "until it is used" on a badge. Reflected
        // over every public const int field ending "Turns" rather than
        // named one by one, so a new constant is covered the moment it is
        // added, not the moment someone remembers to extend this test.
        // MagicalShieldDurationTurns is the one deliberate exception --
        // it is the sentinel-side constant this test's sibling above already
        // pins.
        [Test]
        public void EveryOtherFightTuningTurnsConstantStaysBelowTheSentinel()
        {
            var fields = typeof(FightTuning)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(int) && f.Name.EndsWith("Turns"))
                .Where(f => f.Name != nameof(FightTuning.MagicalShieldDurationTurns))
                .ToList();

            Assert.IsNotEmpty(fields, "no FightTuning *Turns constants found -- " +
                "this test would otherwise pass vacuously if the naming convention changed");

            foreach (var field in fields)
            {
                int value = (int)field.GetValue(null);
                Assert.Less(value, StatusHud.SentinelTurns,
                    $"FightTuning.{field.Name} is {value}, which would read as \"until it is used\" " +
                    "instead of counting down -- either it is a genuine new sentinel (add it beside " +
                    "MagicalShieldDurationTurns above) or it needs retuning.");
            }
        }

        // ---- ordering (section 5) --------------------------------------------------

        // Action restrictions first, then Shielded/Empowered, then
        // everything else -- NEVER a blanket harm-first sort, which would
        // bury an enemy's own Shielded behind its Poison (section 5's own
        // stated failure mode).
        [Test]
        public void ActionRestrictionsSortBeforeShieldedAndEmpoweredWhichSortBeforeTheRest()
        {
            int[] actionRestrictions =
            {
                StatusHud.SortKeyFor(StatusEffectType.Stun),
                StatusHud.SortKeyFor(StatusEffectType.Feared),
                StatusHud.SortKeyFor(StatusEffectType.Provoked),
                StatusHud.SortKeyFor(StatusEffectType.Rooted),
            };
            int shielded = StatusHud.SortKeyFor(StatusEffectType.Shielded);
            int empowered = StatusHud.SortKeyFor(StatusEffectType.Empowered);
            int poison = StatusHud.SortKeyFor(StatusEffectType.Poison);

            foreach (int key in actionRestrictions)
            {
                Assert.Less(key, shielded, "an action restriction must sort ahead of Shielded");
                Assert.Less(key, empowered, "an action restriction must sort ahead of Empowered");
            }

            Assert.Less(shielded, poison, "Shielded must sort ahead of the rest, not behind Poison");
            Assert.Less(empowered, poison, "Empowered must sort ahead of the rest, not behind Poison");
        }

        // StatusRowsFor itself must actually apply that order, not just the
        // table it reads from -- a Rooted party member with an ordinary
        // Poison must see Rooted first.
        [Test]
        public void StatusRowsForOrdersRootedAheadOfPoison()
        {
            var session = Session();
            var target = Combatant();
            StatusEffects.Apply(target.Statuses, StatusEffectType.Poison, 5, 3);
            StatusEffects.Apply(target.Statuses, StatusEffectType.Rooted, 0, 3);

            var rows = FightHudModel.StatusRowsFor(session, target);

            Assert.AreEqual(2, rows.Count);
            Assert.AreEqual(StatusHud.CodeFor(StatusEffectType.Rooted), rows[0].Code);
            Assert.AreEqual(StatusHud.CodeFor(StatusEffectType.Poison), rows[1].Code);
        }

        // ---- the Drowned Lantern's mark (C2) --------------------------------------
        //
        // Same reflection idiom StatusBadgeIconTests already uses to reach
        // FightHudModel's own private members for the identical reason: the
        // fact under test is StatusRowsFor's de-duplication, not the
        // Lantern's own application path, which SkillDamageTypeReachesHudTests
        // and friends already exercise.
        private static void ForceLanternMark(FightSession session, CombatantState target)
        {
            var field = typeof(FightSession).GetField("_marked", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "FightSession._marked was not found by reflection -- has it been renamed?");
            var marked = (HashSet<CombatantState>)field.GetValue(session);
            marked.Add(target);
        }

        // The mark has no StatusEffectType.Marked entry behind it at all --
        // StatusRowsFor must still surface exactly one MRK row for it, not
        // silently show nothing the way it did before C2.
        [Test]
        public void StatusRowsForShowsALanternOnlyMark()
        {
            var session = Session();
            var enemy = Combatant("LanternOnly");
            ForceLanternMark(session, enemy);

            var rows = FightHudModel.StatusRowsFor(session, enemy);

            Assert.AreEqual(1, rows.Count(r => r.Code == StatusHud.CodeFor(StatusEffectType.Marked)),
                "a Lantern-only mark must show exactly one MRK row");
        }

        // Both mechanics at once must still read as ONE badge -- the doubly-
        // marked bug the plan originally found for the (now-retired)
        // EnemyStatusLine, reproduced here against StatusRowsFor since that
        // is where the mechanic lives now.
        [Test]
        public void StatusRowsForDoesNotDoubleUpADoublyMarkedEnemy()
        {
            var session = Session();
            var enemy = Combatant("DoublyMarked");
            StatusEffects.Apply(enemy.Statuses, StatusEffectType.Marked, 0, 3);
            ForceLanternMark(session, enemy);

            var rows = FightHudModel.StatusRowsFor(session, enemy);

            Assert.AreEqual(1, rows.Count(r => r.Code == StatusHud.CodeFor(StatusEffectType.Marked)),
                $"expected exactly one MRK row, found {rows.Count(r => r.Code == StatusHud.CodeFor(StatusEffectType.Marked))}");
        }

        // ---- the enemy plate's text line (Phase 3: retired to BRK only) -----------
        //
        // PLAN_STATUS_EFFECT_UI.md section 9, Phase 3: "two surfaces for one
        // fact" on the enemy plate is retired -- every status now reads only
        // off the enemy's own stage row (BuildEnemyStatusRows), and this line
        // keeps just the one fact with no other surface, the break prefix.
        // The doubly-marked-pill and Lantern-only-mark coverage this section
        // used to pin no longer applies: EnemyStatusLine does not walk
        // StatusRowsFor (or IsMarked) at all any more.

        // A broken enemy still shows BRK -- the one always-shown fact this
        // line exists to guarantee, since no badge in the stage row
        // represents BreakShield at all (StatusRowsFor never reads it).
        [Test]
        public void BrokenEnemyStillShowsBrkOnThePlate()
        {
            var enemy = Combatant("Reeling");
            enemy.BreakShield = new BreakShield(10) { IsBroken = true };

            string line = FightHudModel.EnemyStatusLine(enemy, null);

            StringAssert.Contains("BRK", line);
        }

        // An unbroken enemy carrying a full house -- more than the stage
        // row's own real capacity of four -- shows NOTHING on the plate.
        // This used to be the case that filled the line with three pills and
        // a "+2" chip; now every one of those five statuses reads only off
        // the badge row standing under the enemy's own figure.
        [Test]
        public void NonBrokenEnemyWithFiveStatusesShowsNothingOnThePlate()
        {
            var enemy = Combatant("Overloaded");
            StatusEffects.Apply(enemy.Statuses, StatusEffectType.Poison, 5, 3);
            StatusEffects.Apply(enemy.Statuses, StatusEffectType.Vulnerable, 25, 2);
            StatusEffects.Apply(enemy.Statuses, StatusEffectType.Rooted, 0, 2);
            StatusEffects.Apply(enemy.Statuses, StatusEffectType.Marked, 0, Marks.MarkDurationTurns);
            StatusEffects.Apply(enemy.Statuses, StatusEffectType.Feared, 25, 2);

            string line = FightHudModel.EnemyStatusLine(enemy, null);

            Assert.AreEqual("", line);
        }

        // ---- enemy intent (untouched by Package A, still covered) ------------------

        // EnemyIntentIcons.KindFor: Feared must telegraph the same "your turn
        // will not go as planned" warning Stun does, not the generic Skill
        // icon every unhandled status falls back to.
        [Test]
        public void FearedTelegraphsAStunStyleWarning()
        {
            Assert.AreEqual(EnemyIntentKind.Stun, EnemyIntentIcons.KindFor(true, StatusEffectType.Feared, true));
        }

        // Every current status must resolve to SOME defined EnemyIntentKind
        // without throwing.
        [Test]
        public void EveryStatusResolvesToADefinedIntentKindWithoutThrowing()
        {
            foreach (var type in AllStatusTypes)
            {
                EnemyIntentKind kind = default;
                Assert.DoesNotThrow(() => kind = EnemyIntentIcons.KindFor(true, type, true), $"{type} threw resolving an intent kind");
                Assert.IsTrue(Enum.IsDefined(typeof(EnemyIntentKind), kind), $"{type} resolved to an undefined EnemyIntentKind");
            }
        }
    }
}

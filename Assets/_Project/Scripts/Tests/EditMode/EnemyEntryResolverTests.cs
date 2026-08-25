using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    public class EnemyEntryResolverTests
    {
        private static RawEnemyEntry Minimal(string id = "test_enemy", int maxHealth = 30)
        {
            return new RawEnemyEntry { id = id, displayName = "Test Enemy", maxHealth = maxHealth };
        }

        [Test]
        public void MinimalEntry_IdNameHealthOnly_ResolvesSuccessfully()
        {
            bool ok = EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { Minimal() }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(1, resolved.Count);
            Assert.AreEqual(30, resolved[0].BaseStats.maxHealth);
        }

        [Test]
        public void MinimalEntry_DerivesEveryOmittedFieldToAPositiveValue()
        {
            EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { Minimal() }, out var resolved, out _);

            var stats = resolved[0].BaseStats;
            Assert.Greater(stats.attack, 0, "attack should be derived from maxHealth, not left at 0");
            Assert.GreaterOrEqual(stats.defense, 0);
            Assert.Greater(stats.speed, 0);
            Assert.Greater(resolved[0].ExpReward, 0);
            Assert.Greater(resolved[0].CurrencyReward, 0);
        }

        [Test]
        public void MinimalEntry_DerivesAPositiveBreakShieldFromHealth()
        {
            EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { Minimal(maxHealth: 600) }, out var resolved, out _);

            // 600 * (1/40) = 15, per EnemyEntryResolver.BreakShieldPerHealth.
            Assert.AreEqual(15, resolved[0].BreakShieldPoints);
        }

        [Test]
        public void VeryLowHealthEntry_StillGetsAtLeastTheMinimumBreakShield()
        {
            EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { Minimal(maxHealth: 1) }, out var resolved, out _);

            Assert.GreaterOrEqual(resolved[0].BreakShieldPoints, 2,
                "A monster this fragile should still get a real, if small, stagger meter rather than breaking on the first hit");
        }

        // 0 is a real authored value (no stagger meter at all) and must
        // survive resolution as exactly 0, not be treated as "omitted" and
        // silently replaced by the derived default the way a genuine -1
        // sentinel would be.
        [Test]
        public void ExplicitZeroBreakShield_MeansNoStaggerMeter_NotOmitted()
        {
            var entry = Minimal(maxHealth: 600);
            entry.breakShieldPoints = 0;

            EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { entry }, out var resolved, out _);

            Assert.AreEqual(0, resolved[0].BreakShieldPoints);
        }

        [Test]
        public void ExplicitBreakShield_OverridesTheDerivedDefault()
        {
            var entry = Minimal(maxHealth: 600);
            entry.breakShieldPoints = 3;

            EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { entry }, out var resolved, out _);

            Assert.AreEqual(3, resolved[0].BreakShieldPoints);
        }

        [Test]
        public void MinimalEntry_DerivesADistinctWeaknessAndResistance()
        {
            EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { Minimal() }, out var resolved, out _);

            var affinity = resolved[0].Affinity;

            Assert.AreEqual(1, affinity.Weaknesses.Count, "derivation invents ONE weakness, not a set");
            Assert.AreEqual(1, affinity.Resistances.Count, "derivation invents ONE resistance, not a set");
            Assert.AreNotEqual(affinity.Weaknesses[0], affinity.Resistances[0]);
        }

        [Test]
        public void DerivedWeaknessAndResistance_AreDeterministic_SameIdSameResult()
        {
            EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { Minimal(id: "goblin") }, out var first, out _);
            EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { Minimal(id: "goblin") }, out var second, out _);

            Assert.AreEqual(first[0].Affinity, second[0].Affinity);
        }

        [Test]
        public void ExplicitStats_OverrideTheDerivedDefaults()
        {
            var entry = Minimal();
            entry.attack = 99;
            entry.defense = 42;
            entry.speed = 7;
            entry.expReward = 500;
            entry.currencyReward = 300;

            EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { entry }, out var resolved, out _);

            var stats = resolved[0].BaseStats;
            Assert.AreEqual(99, stats.attack);
            Assert.AreEqual(42, stats.defense);
            Assert.AreEqual(7, stats.speed);
            Assert.AreEqual(500, resolved[0].ExpReward);
            Assert.AreEqual(300, resolved[0].CurrencyReward);
        }

        [Test]
        public void ExplicitZero_IsRespected_NotTreatedAsOmitted()
        {
            var entry = Minimal();
            entry.defense = 0;

            EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { entry }, out var resolved, out _);

            Assert.AreEqual(0, resolved[0].BaseStats.defense);
        }

        [Test]
        public void ExplicitWeaknessAndResistance_AreUsedAsGiven()
        {
            var entry = Minimal();
            entry.weakness = "Fire";
            entry.resistance = "Ice";

            EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { entry }, out var resolved, out _);

            Assert.AreEqual(new[] { DamageType.Fire }, resolved[0].Affinity.Weaknesses);
            Assert.AreEqual(new[] { DamageType.Ice }, resolved[0].Affinity.Resistances);
        }

        // ---- lists, which is the point of the whole grammar -----------------

        [Test]
        public void SeveralWeaknessesAndResistances_AreAllKept()
        {
            var entry = Minimal();
            entry.weakness = "Fire, Ice";
            entry.resistance = "Physical, Poison";

            bool ok = EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { entry }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(new[] { DamageType.Fire, DamageType.Ice }, resolved[0].Affinity.Weaknesses);
            Assert.AreEqual(new[] { DamageType.Physical, DamageType.Poison }, resolved[0].Affinity.Resistances);
        }

        // Whatever order an author writes them in, the affinity is a SET and
        // reads back in DamageType declaration order -- so a glossary row does
        // not reshuffle itself because someone tidied the JSON.
        [Test]
        public void ListOrderAndSpacingDoNotChangeTheResult()
        {
            var tidy = Minimal();
            tidy.weakness = "Fire, Ice";

            var messy = Minimal();
            messy.weakness = "  ice ,fire,  ";

            EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { tidy }, out var a, out _);
            EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { messy }, out var b, out _);

            Assert.AreEqual(new[] { DamageType.Fire, DamageType.Ice }, a[0].Affinity.Weaknesses);
            Assert.AreEqual(a[0].Affinity, b[0].Affinity);
        }

        // The difference an empty list cannot express, which is why the keyword
        // exists at all -- see RawEnemyEntry.weakness.
        [Test]
        public void NoneIsAnAuthoredAnswer_NotAnOmissionToBeDerivedOver()
        {
            var entry = Minimal();
            entry.weakness = "none";
            entry.resistance = "Fire";

            bool ok = EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { entry }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.IsEmpty(resolved[0].Affinity.Weaknesses,
                "\"none\" was derived over, so a monster can never be authored as having no weakness");
            Assert.AreEqual(new[] { DamageType.Fire }, resolved[0].Affinity.Resistances);
        }

        // Six elements, and a monster resisting five of them leaves exactly one
        // free. The single-step version of this walk could land on a collision
        // and hand back a contradiction the very next check rejects.
        [Test]
        public void DerivingAWeaknessAroundALongResistanceList_FindsTheOneFreeElement()
        {
            var entry = Minimal();
            entry.resistance = "Physical, Fire, Ice, Nature, Poison";

            bool ok = EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { entry }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(new[] { DamageType.Arcane }, resolved[0].Affinity.Weaknesses);
        }

        [Test]
        public void DamageTypeParsing_IsCaseInsensitive()
        {
            var entry = Minimal();
            entry.weakness = "fire";
            entry.resistance = "ICE";

            bool ok = EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { entry }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(new[] { DamageType.Fire }, resolved[0].Affinity.Weaknesses);
            Assert.AreEqual(new[] { DamageType.Ice }, resolved[0].Affinity.Resistances);
        }

        [Test]
        public void OnlyWeaknessGiven_ResistanceIsDerivedAndDoesNotCollide()
        {
            var entry = Minimal();
            entry.weakness = "Physical";

            EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { entry }, out var resolved, out _);

            Assert.AreEqual(new[] { DamageType.Physical }, resolved[0].Affinity.Weaknesses);
            Assert.IsFalse(resolved[0].Affinity.Resists(DamageType.Physical));
        }

        [Test]
        public void MissingId_ProducesAClearError()
        {
            var entry = Minimal();
            entry.id = "";

            bool ok = EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { entry }, out var resolved, out var errors);

            Assert.IsFalse(ok);
            Assert.IsNull(resolved);
            StringAssert.Contains("id is required", errors[0]);
        }

        [Test]
        public void MissingDisplayName_ProducesAClearError()
        {
            var entry = Minimal();
            entry.displayName = "";

            bool ok = EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("displayName is required", errors[0]);
        }

        [Test]
        public void ZeroOrNegativeMaxHealth_ProducesAClearError()
        {
            var entry = Minimal(maxHealth: 0);

            bool ok = EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("maxHealth must be a positive number", errors[0]);
        }

        [Test]
        public void UnrecognizedDamageTypeName_ProducesAClearErrorListingValidOptions()
        {
            var entry = Minimal();
            entry.weakness = "Fyre"; // typo

            bool ok = EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("Fyre", errors[0]);
            StringAssert.Contains("Fire", errors[0]); // the valid-options list should be in the message
        }

        [Test]
        public void SameWeaknessAndResistanceExplicitlyGiven_ProducesAClearError()
        {
            var entry = Minimal();
            entry.weakness = "Ice";
            entry.resistance = "Ice";

            bool ok = EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("Ice", errors[0]);
            StringAssert.Contains("both a weakness and a resistance", errors[0]);
        }

        // The same mistake, but buried in a list where it is easy to miss by
        // eye -- which is the case the equality check this replaced could not
        // have caught at all.
        [Test]
        public void AnElementOnBothLists_IsRejectedAndNamed()
        {
            var entry = Minimal();
            entry.weakness = "Fire, Ice";
            entry.resistance = "Poison, Ice";

            bool ok = EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("Ice", errors[0]);
            StringAssert.DoesNotContain("Fire", errors[0], "only the contradicting element should be named");
        }

        [Test]
        public void DuplicateIds_ProduceAClearError()
        {
            var entries = new List<RawEnemyEntry> { Minimal(id: "rat"), Minimal(id: "rat") };

            bool ok = EnemyEntryResolver.TryResolveAll(entries, out var resolved, out var errors);

            Assert.IsFalse(ok);
            Assert.IsNull(resolved);
            StringAssert.Contains("Duplicate enemy id 'rat'", errors[0]);
        }

        [Test]
        public void MultipleInvalidEntries_ReportEveryErrorTogether_NotJustTheFirst()
        {
            var entries = new List<RawEnemyEntry>
            {
                Minimal(id: "", maxHealth: 10),
                Minimal(id: "ok_one", maxHealth: 0),
            };

            bool ok = EnemyEntryResolver.TryResolveAll(entries, out _, out var errors);

            Assert.IsFalse(ok);
            Assert.AreEqual(2, errors.Count, "Both problems should be reported in one pass, not just the first");
        }

        [Test]
        public void SortOrder_FollowsFileOrderAmongValidEntries()
        {
            var entries = new List<RawEnemyEntry> { Minimal(id: "a"), Minimal(id: "b"), Minimal(id: "c") };

            EnemyEntryResolver.TryResolveAll(entries, out var resolved, out _);

            Assert.AreEqual(0, resolved[0].SortOrder);
            Assert.AreEqual(1, resolved[1].SortOrder);
            Assert.AreEqual(2, resolved[2].SortOrder);
        }

        [Test]
        public void IsBoss_DefaultsFalse_AndIsPreservedWhenTrue()
        {
            var normal = Minimal(id: "normal");
            var boss = Minimal(id: "boss");
            boss.isBoss = true;

            EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { normal, boss }, out var resolved, out _);

            Assert.IsFalse(resolved[0].IsBoss);
            Assert.IsTrue(resolved[1].IsBoss);
        }

        // --- Optional second action.

        // Every monster authored before skills existed omits all three
        // fields, and must keep behaving exactly as it did: attack, always.
        // An empty SkillName is what EnemyDefinition.HasSkill reads, so this
        // is the compatibility guarantee for the entire existing roster.
        [Test]
        public void AMonsterWithNoSkillAuthored_ResolvesWithNoSkill()
        {
            EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { Minimal() }, out var resolved, out _);

            Assert.AreEqual("", resolved[0].SkillName);
        }

        [Test]
        public void SkillFields_AreCarriedThroughWhenAuthored()
        {
            var entry = Minimal();
            entry.skillName = "Boulder Slam";
            entry.skillPower = 1.8f;
            entry.skillChance = 0.3f;

            bool ok = EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { entry }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual("Boulder Slam", resolved[0].SkillName);
            Assert.AreEqual(1.8f, resolved[0].SkillPower, 0.0001f);
            Assert.AreEqual(0.3f, resolved[0].SkillChance, 0.0001f);
        }

        // The -1 sentinels: naming a skill and leaving the numbers out is
        // the common authoring case, and must produce a usable monster
        // rather than one that casts a 0x-power spell 0% of the time.
        [Test]
        public void ASkillWithNoNumbers_GetsUsableDefaults()
        {
            var entry = Minimal();
            entry.skillName = "Hex Bolt";

            EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { entry }, out var resolved, out _);

            Assert.Greater(resolved[0].SkillPower, 1f, "a skill should hit harder than a plain attack");
            Assert.Greater(resolved[0].SkillChance, 0f, "a skill that can never be chosen is authored for nothing");
            Assert.LessOrEqual(resolved[0].SkillChance, 1f);
        }

        [Test]
        public void SkillName_IsTrimmed()
        {
            var entry = Minimal();
            entry.skillName = "  Boulder Slam  ";

            EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { entry }, out var resolved, out _);

            Assert.AreEqual("Boulder Slam", resolved[0].SkillName);
        }

        // 0 is a legitimate authored value and must survive, which is the
        // entire reason the sentinel is -1 rather than 0 (see RawEnemyEntry).
        [Test]
        public void AnExplicitZeroSkillChance_IsKeptRatherThanDefaulted()
        {
            var entry = Minimal();
            entry.skillName = "Never Used";
            entry.skillChance = 0f;

            EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { entry }, out var resolved, out _);

            Assert.AreEqual(0f, resolved[0].SkillChance, 0.0001f);
        }

        // --- How the plain attack travels.

        // Every monster authored before this field existed leaves it blank and
        // must keep lunging, exactly as it always did.
        [Test]
        public void AnUnsetAttackApproach_LungesLikeEveryPlainSwingAlwaysHas()
        {
            EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { Minimal() }, out var resolved, out _);

            Assert.AreEqual(Combat.Session.StageApproach.Lunge, resolved[0].AttackApproach);
        }

        // The beetle authors a charge so its plain attack crosses the stage and
        // bumps the party rather than leaning a third of the way in. Parsed
        // through the same shared helper the skill approach uses.
        [Test]
        public void AnAuthoredChargeAttackApproach_Resolves()
        {
            var entry = Minimal(id: "beetle");
            entry.attackApproach = "charge";

            EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { entry }, out var resolved, out _);

            Assert.AreEqual(Combat.Session.StageApproach.Charge, resolved[0].AttackApproach);
        }
    }
}

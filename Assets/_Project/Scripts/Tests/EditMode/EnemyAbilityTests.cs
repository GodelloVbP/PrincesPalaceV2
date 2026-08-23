using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // A monster's abilities: the draw, and what the telegraph promises about it.
    //
    // Both halves matter and only one of them is about correctness. A weighted
    // pick that is subtly biased makes a boss feel wrong in a way nobody can
    // name; a telegraph that describes the wrong action makes the fight
    // unplayable, because the whole point of committing an intent a turn early
    // is that the player can act on it.
    public class EnemyAbilityTests
    {
        private static EnemyAbility Legacy(string label, float power, float weight) =>
            EnemyAbility.LegacyAttack(label, power, weight);

        // ---- the draw ------------------------------------------------------------

        [Test]
        public void APoolWithOneEntryAlwaysPicksIt()
        {
            var pool = new List<EnemyAbility> { Legacy("Swing", 1f, 1f) };

            Assert.AreEqual(0, EnemyAbilityDraw.Pick(pool, 0f));
            Assert.AreEqual(0, EnemyAbilityDraw.Pick(pool, 0.5f));
            Assert.AreEqual(0, EnemyAbilityDraw.Pick(pool, 1f));
        }

        // WEIGHTS ARE RELATIVE, so a pool of 3 and 1 splits the roll at 0.75 --
        // not at 0.5, and not at some normalised probability the author has to
        // compute. Pinned with literal boundaries rather than by re-deriving
        // the division here, which would make this a restatement of the code.
        [Test]
        public void TheRollSplitsAtTheCumulativeWeight()
        {
            var pool = new List<EnemyAbility>
            {
                Legacy("Swing", 1f, 3f),
                Legacy("Slam", 1.8f, 1f),
            };

            Assert.AreEqual(0, EnemyAbilityDraw.Pick(pool, 0f), "the bottom of the range is the first entry");
            Assert.AreEqual(0, EnemyAbilityDraw.Pick(pool, 0.74f), "still inside the 3-weight share");
            Assert.AreEqual(1, EnemyAbilityDraw.Pick(pool, 0.76f), "past 0.75 belongs to the 1-weight share");
            Assert.AreEqual(1, EnemyAbilityDraw.Pick(pool, 1f), "the very top must not fall off the end");
        }

        // A generator is allowed to return exactly 1, and an unclamped
        // implementation walks off the end of the list and returns nothing from
        // a pool that plainly has entries.
        [Test]
        public void ARollOfExactlyOnePicksTheLastEntryRatherThanNothing()
        {
            var pool = new List<EnemyAbility>
            {
                Legacy("A", 1f, 1f), Legacy("B", 1f, 1f), Legacy("C", 1f, 1f),
            };

            Assert.AreEqual(2, EnemyAbilityDraw.Pick(pool, 1f));
        }

        // A zero weight is a legitimate authoring state -- "written, but switched
        // off while I tune" -- and must never be chosen.
        [Test]
        public void AZeroWeightIsNeverChosen()
        {
            var pool = new List<EnemyAbility>
            {
                Legacy("Never", 1f, 0f),
                Legacy("Always", 1f, 1f),
            };

            for (int i = 0; i <= 100; i++)
            {
                Assert.AreEqual(1, EnemyAbilityDraw.Pick(pool, i / 100f),
                    $"roll {i / 100f} chose the zero-weight entry");
            }
        }

        [Test]
        public void AnEmptyOrFullyZeroedPoolPicksNothing()
        {
            Assert.AreEqual(-1, EnemyAbilityDraw.Pick(null, 0.5f));
            Assert.AreEqual(-1, EnemyAbilityDraw.Pick(new List<EnemyAbility>(), 0.5f));
            Assert.AreEqual(-1, EnemyAbilityDraw.Pick(
                new List<EnemyAbility> { Legacy("A", 1f, 0f) }, 0.5f));
        }

        // ---- the legacy pool -----------------------------------------------------
        //
        // A monster authored before abilities existed has to behave EXACTLY as
        // it did. Its trio is a two-entry pool: skillChance of the scaled
        // attack, the remainder of a plain one.
        [Test]
        public void ALegacyMonsterBecomesATwoEntryPoolSplitOnItsOwnChance()
        {
            var kit = new EnemyKit(new ResolvedEnemy("golem", "Golem", new StatBlockOf(), 5, 3, false,
                DamageType.Physical, DamageType.Physical, 0,
                skillName: "Boulder Slam", skillPower: 1.8f, skillChance: 0.25f), false);

            Assert.AreEqual(2, kit.Abilities.Count);

            Assert.IsTrue(kit.Abilities[0].IsPlainSwing, "the first entry is the plain swing");
            Assert.AreEqual(0.75f, kit.Abilities[0].Weight, 0.0001f,
                "the swing carries the REMAINDER of the skill chance");

            Assert.AreEqual("Boulder Slam", kit.Abilities[1].Label);
            Assert.AreEqual(1.8f, kit.Abilities[1].Power, 0.0001f);
            Assert.AreEqual(0.25f, kit.Abilities[1].Weight, 0.0001f);
        }

        [Test]
        public void AMonsterWithNoSkillAtAllStillHasItsSwing()
        {
            var kit = new EnemyKit(new ResolvedEnemy("rat", "Rat", new StatBlockOf(), 5, 3, false,
                DamageType.Physical, DamageType.Physical, 0), false);

            Assert.AreEqual(1, kit.Abilities.Count, "a monster with no skill has exactly one thing it does");
            Assert.IsTrue(kit.Abilities[0].IsPlainSwing);
        }

        // ---- the telegraph -------------------------------------------------------
        //
        // The scope is read off the effect rather than authored, because an
        // author who could disagree with it could make the telegraph lie.
        [Test]
        public void ScopeFollowsTheEffect()
        {
            Assert.AreEqual(EnemyIntentScope.One, EnemyIntentIcons.ScopeFor(SkillEffect.DamageSingle));
            Assert.AreEqual(EnemyIntentScope.AllOpponents, EnemyIntentIcons.ScopeFor(SkillEffect.DamageAll));
            Assert.AreEqual(EnemyIntentScope.Self, EnemyIntentIcons.ScopeFor(SkillEffect.HealSelf));
            Assert.AreEqual(EnemyIntentScope.AllAllies, EnemyIntentIcons.ScopeFor(SkillEffect.HealParty));
        }

        // A HEAL WITH NO STATUS still has to read as a heal. The old KindFor
        // knew only about statuses, so a straight mend telegraphed as the
        // generic "skill" icon -- "something is coming" on the one turn it is
        // not coming for you.
        [Test]
        public void AHealTelegraphsAsAHealEvenWithNoStatusAttached()
        {
            Assert.AreEqual(EnemyIntentKind.Heal,
                EnemyIntentIcons.KindFor(SkillEffect.HealSelf, null, false));
            Assert.AreEqual(EnemyIntentKind.Heal,
                EnemyIntentIcons.KindFor(SkillEffect.HealParty, null, false));
        }

        [Test]
        public void ADamagingSkillStillTakesItsKindFromTheStatusItInflicts()
        {
            Assert.AreEqual(EnemyIntentKind.Weaken,
                EnemyIntentIcons.KindFor(SkillEffect.DamageSingle, StatusEffectType.Vulnerable, true));
            Assert.AreEqual(EnemyIntentKind.Skill,
                EnemyIntentIcons.KindFor(SkillEffect.DamageSingle, null, false));
        }

        // ---- what the player actually reads --------------------------------------

        [Test]
        public void TheTooltipNamesOneTargetForASingleTargetBlow()
        {
            var shawn = new CombatantState("Shawn", true, 100, 0, 10, 0, 5);
            var intent = new EnemyIntent("Mud Burst", EnemyIntentKind.Weaken, shawn, 42, 0);

            StringAssert.Contains("Shawn", FightHudModel.IntentTooltip("Bog Witch", intent));
            StringAssert.Contains("42 damage", FightHudModel.IntentTooltip("Bog Witch", intent));
        }

        // NAMING ONE TARGET FOR A SWEEP IS WORSE THAN SAYING NOTHING: it is a
        // specific claim, and it is false.
        [Test]
        public void TheTooltipSaysWholePartyForASweep()
        {
            var shawn = new CombatantState("Shawn", true, 100, 0, 10, 0, 5);
            var intent = new EnemyIntent("Wail", EnemyIntentKind.Skill, shawn, 18, 0,
                EnemyIntentScope.AllOpponents);

            string text = FightHudModel.IntentTooltip("Hollow Choir", intent);

            StringAssert.Contains("your whole party", text);
            StringAssert.Contains("each", text, "a per-target figure has to say it is per target");
            StringAssert.DoesNotContain("Shawn", text,
                "a sweep must not name one victim - that is the claim a telegraph exists to avoid");
        }

        // A MAGNITUDE WITH NO SIGN READS AS A THREAT. "for about 40" under a
        // heal icon is the one sentence that sends a player to kill the wrong
        // monster.
        [Test]
        public void TheTooltipSaysHealingRatherThanDamageForAMend()
        {
            var witch = new CombatantState("Witch", false, 100, 0, 10, 0, 5);
            var intent = new EnemyIntent("Mend", EnemyIntentKind.Heal, witch, 40, 0,
                EnemyIntentScope.AllAllies, heals: true);

            string text = FightHudModel.IntentTooltip("Bog Witch", intent);

            StringAssert.Contains("its allies", text);
            StringAssert.Contains("40 healing", text);
            StringAssert.DoesNotContain("damage", text);
        }

        // A tiny stand-in so these tests do not depend on StatBlock's own
        // constructor shape, which is not what any of them are about.
        private struct StatBlockOf
        {
            public static implicit operator Stats.StatBlock(StatBlockOf _) => new Stats.StatBlock();
        }
    }
}

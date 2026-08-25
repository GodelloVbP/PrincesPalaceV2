using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    public class SkillEntryResolverTests
    {
        private static RawSkillEntry Minimal(string id = "shear", string owner = "sheep")
        {
            return new RawSkillEntry { id = id, displayName = "Shear", characterId = owner, manaCost = 5 };
        }

        [Test]
        public void MinimalEntry_Resolves_AndDefaultsSensibly()
        {
            bool ok = SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { Minimal() }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(1, resolved[0].UnlockLevel, "Unstated unlockLevel means available from the start");
            Assert.AreEqual(SkillEffect.DamageSingle, resolved[0].Effect);
            Assert.AreEqual(SkillTargeting.SingleEnemy, resolved[0].Targeting);
        }

        [Test]
        public void Targeting_IsInferredFromTheEffect()
        {
            // Distinct unlock levels: two skills on one character sharing a
            // level is rejected outright, which is a separate rule tested
            // below and would otherwise fail this one for the wrong reason.
            var aoe = Minimal("aoe");
            aoe.effect = "DamageAll";
            aoe.unlockLevel = 2;
            var heal = Minimal("heal");
            heal.effect = "HealSelf";
            heal.flatAmount = 5;
            heal.unlockLevel = 4;

            bool ok = SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { aoe, heal }, out var resolved, out var errors);
            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));

            var byId = new Dictionary<string, ResolvedSkill>();
            foreach (var s in resolved) { byId[s.Id] = s; }

            Assert.AreEqual(SkillTargeting.AllEnemies, byId["aoe"].Targeting);
            Assert.AreEqual(SkillTargeting.Self, byId["heal"].Targeting);
        }

        [Test]
        public void CharacterId_IsRequired()
        {
            var entry = Minimal();
            entry.characterId = "";

            bool ok = SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("characterId is required", errors[0]);
        }

        // A free action is strictly better than every other action and would
        // simply be spammed.
        [Test]
        public void ASkillThatCostsNothing_IsRejected()
        {
            var entry = Minimal();
            entry.manaCost = 0;

            bool ok = SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("costs neither mana nor resource", errors[0]);
        }

        [Test]
        public void AResourceOnlySkill_IsAccepted()
        {
            var entry = Minimal();
            entry.manaCost = 0;
            entry.resourceCost = 3;
            entry.power = 2;

            bool ok = SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { entry }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(0, resolved[0].ManaCost, "Costing no mana at all is the whole shape of Shear");
            Assert.AreEqual(3, resolved[0].ResourceCost);
        }

        // Scaling per point spent, on a skill that spends none, is always
        // zero — a silent no-op the author would never notice.
        [Test]
        public void PowerWithoutAnyResourceSpend_IsRejected()
        {
            var entry = Minimal();
            entry.power = 3;

            bool ok = SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("spends none", errors[0]);
        }

        [Test]
        public void ARestorativeSkillThatRestoresNothing_IsRejected()
        {
            var entry = Minimal();
            entry.effect = "HealParty";

            bool ok = SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("restores nothing", errors[0]);
        }

        [Test]
        public void IgnoresDefenseOnANonDamageSkill_IsRejected()
        {
            var entry = Minimal();
            entry.effect = "HealSelf";
            entry.flatAmount = 10;
            entry.ignoresDefense = true;

            bool ok = SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { entry }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("only means anything for a damage effect", errors[0]);
        }

        // Sharing an unlock level is ALLOWED. An earlier version rejected it
        // as "almost always a typo", which was wrong the first time a
        // character needed to start with more than one spell - Shawn opens
        // with Shear, Lightning Bolt and Frost Flare all at level 1.
        [Test]
        public void TwoSkillsForOneCharacterMayShareAnUnlockLevel()
        {
            var a = Minimal("a");
            a.unlockLevel = 4;
            var b = Minimal("b");
            b.unlockLevel = 4;

            bool ok = SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { a, b }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(2, resolved.Count);
        }

        // Two characters may of course share a level.
        [Test]
        public void TwoCharactersUnlockingAtTheSameLevel_AreFine()
        {
            var a = Minimal("a", "sheep");
            var b = Minimal("b", "dog");

            bool ok = SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { a, b }, out _, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
        }

        [Test]
        public void TryResolveAll_ReportsEveryProblemNotJustTheFirst()
        {
            var noId = Minimal("");
            var noOwner = Minimal("b");
            noOwner.characterId = "";

            bool ok = SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { noId, noOwner }, out _, out var errors);

            Assert.IsFalse(ok);
            Assert.AreEqual(2, errors.Count, "A hand-edited file should say everything wrong with it in one pass");
        }
    }

    public class SkillResolutionTests
    {
        private static CombatantState Actor(int attack = 5)
        {
            return new CombatantState("Shawn", true, 32, 38, attack, 4, 8);
        }

        private static CombatantState Target(int defense)
        {
            return new CombatantState("Wall", false, 100, 0, 6, defense, 5);
        }

        // PINNED literals throughout — nothing here recomputes the formula.
        [Test]
        public void Damage_IsAttackPlusScaling_SoftenedByDefense()
        {
            // 5 attack + 2 power x 6 spent = 17, of which 12/(12+8) lands --
            // 10.2, rounded to 10 -- x5 for the health scale. Was 45 while
            // armour subtracted.
            Assert.AreEqual(50, SkillResolution.Amount(
                SkillEffect.DamageSingle, Actor(), Target(8), power: 2, flatAmount: 0, resourceSpent: 6, ignoresDefense: false));
        }

        [Test]
        public void Damage_IgnoringDefense_SkipsItEntirely()
        {
            // Same 17, and the target's 8 defense simply does not apply.
            Assert.AreEqual(85, SkillResolution.Amount(
                SkillEffect.DamageSingle, Actor(), Target(8), power: 2, flatAmount: 0, resourceSpent: 6, ignoresDefense: true));
        }

        // WHAT A WALL COSTS, and it is no longer everything.
        //
        // This used to assert that against Defense 99 an ordinary line does 5
        // and only the defence-ignoring one does anything. That was true, and
        // it was the subtraction cliff rather than a design: every ordinary
        // action bottomed out on max(1, ...) and STAYED there however strong
        // the attacker got, which is what made a floor-one boss with Defense 9
        // take 130 turns.
        //
        // A wall is still a wall -- 10 against 85 is a heavy penalty and the
        // defence-ignoring line is still the right answer to one -- but the
        // ordinary line now scales, so gearing up answers a wall instead of
        // bouncing off it. That is the half this pins.
        [Test]
        public void AWallBluntsAnOrdinaryLineWithoutFlatteningIt()
        {
            int ordinary = SkillResolution.Amount(
                SkillEffect.DamageSingle, Actor(), Target(99), power: 2, flatAmount: 0, resourceSpent: 6, ignoresDefense: false);
            int ram = SkillResolution.Amount(
                SkillEffect.DamageSingle, Actor(), Target(99), power: 2, flatAmount: 0, resourceSpent: 6, ignoresDefense: true);

            // raw 17 x 12/(12+99) = 1.8 -> 2 -> 10, against the ram's whole 85.
            Assert.AreEqual(10, ordinary);
            Assert.AreEqual(85, ram, "ignoring defence still skips the wall entirely");

            // AND IT STILL RESPONDS TO GEAR, which is the property the old
            // formula lacked. Attack 22 makes raw 34, exactly twice the 17
            // above, and the damage doubles with it: 34 x 12/111 = 3.7 -> 4.
            int geared = SkillResolution.Amount(
                SkillEffect.DamageSingle, Actor(22), Target(99), power: 2, flatAmount: 0, resourceSpent: 6, ignoresDefense: false);

            Assert.AreEqual(20, geared,
                "twice the attack must be twice the damage even against a wall - under the old " +
                "floor both figures were 5 and the entire difference in gear was worth nothing");
        }

        [Test]
        public void Heals_AreFlatPlusScaling_AndIgnoreAttackEntirely()
        {
            // 4 flat + 3 power x 5 spent = 19. The caster's Attack of 5 must
            // not leak into a heal.
            Assert.AreEqual(19, SkillResolution.Amount(
                SkillEffect.HealSelf, Actor(), null, power: 3, flatAmount: 4, resourceSpent: 5, ignoresDefense: false));
        }

        // ---- scaling axis (Phase 4 of the stat-scaling plan) -------------

        private static CombatantState ScaledActor(ScalingAxis? weaponAxisRides = null)
        {
            var actor = new CombatantState("Caster", true, 100, 20, 5, 0, 8)
            {
                AbilityScores = new AbilityScoreBlock(20, 10, 10, 10, 10, 10),
            };

            var sGrade = ScalingProfile.None.With(AbilityScore.Strength, ScalingGrade.S);
            actor.WeaponScaling = sGrade; // +10 STR over neutral, S = 2.00x
            actor.SkillScaling = ScalingProfile.None.With(AbilityScore.Strength, ScalingGrade.A); // 1.70x

            return actor;
        }

        // Physical + Auto resolves to the WEAPON axis — S grade at +10 STR
        // is exactly 2.00x, so Attack 5 scales to 10.
        [Test]
        public void PhysicalTypeWithAutoAxis_UsesWeaponScaling()
        {
            var actor = ScaledActor();
            int damage = SkillResolution.Amount(SkillEffect.DamageSingle, actor, Target(0),
                power: 0, flatAmount: 0, resourceSpent: 0, ignoresDefense: false,
                type: DamageType.Physical, axis: ScalingAxis.Auto);

            Assert.AreEqual(50, damage, "5 Attack x 2.00 (WeaponScaling S) = 10, x5 health scale = 50");
        }

        // Non-physical + Auto resolves to the SPELL axis — A grade at +10
        // STR is 1.70x, a DIFFERENT number than the weapon axis, proving
        // this actually reads SkillScaling and not WeaponScaling again.
        [Test]
        public void NonPhysicalTypeWithAutoAxis_UsesSkillScaling()
        {
            var actor = ScaledActor();
            int damage = SkillResolution.Amount(SkillEffect.DamageSingle, actor, Target(0),
                power: 0, flatAmount: 0, resourceSpent: 0, ignoresDefense: false,
                type: DamageType.Nature, axis: ScalingAxis.Auto);

            Assert.AreEqual(45, damage, "5 Attack x 1.70 (SkillScaling A) = 8.5, rounded away from zero to 9, x5 health scale = 45");
        }

        // An explicit override wins regardless of the damage type — this is
        // the whole mechanism battering_ram uses to stay on the weapon axis
        // despite its owner's own attackType being Nature.
        [Test]
        public void AnExplicitAxisOverride_WinsOverTheTypeDerivedOne()
        {
            var actor = ScaledActor();
            int damage = SkillResolution.Amount(SkillEffect.DamageSingle, actor, Target(0),
                power: 0, flatAmount: 0, resourceSpent: 0, ignoresDefense: false,
                type: DamageType.Nature, axis: ScalingAxis.Weapon);

            Assert.AreEqual(50, damage, "Weapon override should read WeaponScaling (2.00x) even though the type is Nature");
        }

        [Test]
        public void NoneAxis_NeverScalesAttackAtAll()
        {
            var actor = ScaledActor();
            int damage = SkillResolution.Amount(SkillEffect.DamageSingle, actor, Target(0),
                power: 0, flatAmount: 0, resourceSpent: 0, ignoresDefense: false,
                type: DamageType.Physical, axis: ScalingAxis.None);

            Assert.AreEqual(25, damage, "None should leave Attack completely unscaled: 5 x5 = 25");
        }

        // "Multiply only the actor.Attack term" — power/resourceSpent must
        // pass through untouched by the scaling axis, or it would silently
        // double-dip with the signature-resource axis.
        [Test]
        public void ScalingMultipliesOnlyAttack_NeverFlatAmountOrPower()
        {
            var actor = ScaledActor();
            int damage = SkillResolution.Amount(SkillEffect.DamageSingle, actor, Target(0),
                power: 2, flatAmount: 3, resourceSpent: 4, ignoresDefense: false,
                type: DamageType.Physical, axis: ScalingAxis.Weapon);

            // Scaled attack 10 (as in the first test above) + flatAmount 3 +
            // power 2 x resourceSpent 4 = 8, total 21, x5 = 105 — NOT
            // (5 + 3 + 8) x 2.00 x 5 = 160, which is what scaling the whole
            // sum would produce.
            Assert.AreEqual(105, damage);
        }

        [Test]
        public void ResourceToSpend_TakesTheCost_OrEverythingWhenSpendsAll()
        {
            var wool = new SignatureResource("wool", "Wool", 16, 2, 3, 1);
            wool.Gain(11);

            Assert.AreEqual(6, SkillResolution.ResourceToSpend(wool, 6, spendsAll: false));
            Assert.AreEqual(11, SkillResolution.ResourceToSpend(wool, 8, spendsAll: true));
            Assert.AreEqual(0, SkillResolution.ResourceToSpend(null, 6, spendsAll: false), "No resource means nothing to spend");
        }

        // A spendsAll capstone still needs its stated cost as a minimum, or
        // it could be fired on an empty gauge for nothing.
        [Test]
        public void CanAfford_ChecksBothMana_AndTheResourceMinimum()
        {
            var actor = Actor();
            actor.CurrentMana = 10;
            actor.Signature = new SignatureResource("wool", "Wool", 16, 2, 3, 1);
            actor.Signature.Gain(4);

            Assert.IsTrue(SkillResolution.CanAfford(actor, 10, 4));
            Assert.IsFalse(SkillResolution.CanAfford(actor, 11, 4), "Not enough mana");
            Assert.IsFalse(SkillResolution.CanAfford(actor, 10, 5), "Not enough resource");

            var noResource = Actor();
            noResource.CurrentMana = 10;
            Assert.IsFalse(SkillResolution.CanAfford(noResource, 10, 1), "No resource at all cannot pay a resource cost");
            Assert.IsTrue(SkillResolution.CanAfford(noResource, 10, 0), "But a mana-only skill is fine");
        }

        // ---- how a skill travels to what it hits ------------------------------

        // "charge" is the committed rush the Beetle's Barrel Roll authors, and
        // it has to survive the resolver as its own approach rather than
        // collapsing to the Hold a cast defaults to.
        [Test]
        public void Approach_Charge_Resolves_RatherThanFallingBackToHold()
        {
            var roll = new RawSkillEntry
            {
                id = "barrel_roll", displayName = "Barrel Roll", characterId = "beetle",
                effect = "DamageSingle", stance = "turtle_up", approach = "charge",
                playerSelectable = false,
            };

            bool ok = SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { roll }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(StageApproach.Charge, resolved[0].Approach);
        }

        // A cast that names no approach still holds, and a nonsense one falls
        // back to hold rather than stopping the catalogue -- the graceful
        // posture the shared parser takes for every misspelling.
        [Test]
        public void Approach_UnsetOrGarbage_HoldsRatherThanThrowing()
        {
            var blank = new RawSkillEntry
            {
                id = "a", displayName = "A", characterId = "beetle", effect = "HealSelf",
                flatAmount = 2, playerSelectable = false,
            };
            var junk = new RawSkillEntry
            {
                id = "b", displayName = "B", characterId = "beetle", effect = "HealSelf", approach = "sideways",
                flatAmount = 2, playerSelectable = false,
            };

            bool ok = SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { blank, junk }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(StageApproach.Hold, resolved[0].Approach);
            Assert.AreEqual(StageApproach.Hold, resolved[1].Approach);
        }
    }
}

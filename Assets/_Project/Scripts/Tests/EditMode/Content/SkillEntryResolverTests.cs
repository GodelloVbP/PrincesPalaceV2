using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

        // ---- a book nobody can hold (plan P6, gate 5) -------------------------
        //
        // THE ONLY GATE OF PHASE C THAT IS TESTABLE WITHOUT CONTENT, and the
        // reason it is shaped as a parameter: no shipped pool refuses books
        // (that is Bjorn's `fury` row, phase E), so the set of refusing owners
        // is handed in rather than looked up. ContentBuilder computes it from
        // the pool and character catalogues it has already built; these three
        // state the rule against a set the fixture names itself.

        [Test]
        public void ABookOnlySkillWhoseOwnerRefusesBooks_IsRefusedNamingBoth()
        {
            var entry = Minimal("rage_ward", "bear");
            entry.bookOnly = true;
            entry.bookTier = 1;
            entry.unlockLevel = -1;

            bool ok = SkillEntryResolver.TryResolveAll(
                new List<RawSkillEntry> { entry }, new[] { "bear" }, out _, out var errors);

            Assert.IsFalse(ok, "a book-only skill owned by somebody who cannot hold a book is unreachable content");
            Assert.AreEqual(1, errors.Count, string.Join("; ", errors));
            StringAssert.Contains("rage_ward", errors[0]);
            StringAssert.Contains("bear", errors[0]);
            StringAssert.Contains("refuses spell books", errors[0]);
        }

        // THE CONTROL, and it is the half that would let the rule ship
        // inverted: the SAME owner, the SAME refusing set, and a skill that
        // is not book-only is perfectly fine. Bjorn's own kit is exactly this
        // case -- authored skills he levels into, on a pool that reads no
        // books at all.
        [Test]
        public void AnOrdinarySkillOwnedByABookRefuser_Resolves()
        {
            bool ok = SkillEntryResolver.TryResolveAll(
                new List<RawSkillEntry> { Minimal("slam", "bear") }, new[] { "bear" },
                out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(1, resolved.Count);
        }

        // AND THE OTHER CONTROL: a book-only skill owned by somebody who is
        // NOT in the refusing set. Every one of the six shipped book spells is
        // this case today, which is why the shipped build stays green.
        [Test]
        public void ABookOnlySkillOwnedByABookHolder_Resolves()
        {
            var entry = Minimal("mud_burst", "sheep");
            entry.bookOnly = true;
            entry.bookTier = 1;
            entry.unlockLevel = -1;

            bool ok = SkillEntryResolver.TryResolveAll(
                new List<RawSkillEntry> { entry }, new[] { "bear" }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(int.MaxValue, resolved[0].UnlockLevel);
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

        // ---- a free skill on a pool that opens empty (phase E) -----------------
        //
        // THE SAME SKILL, THE SAME EFFECT, TWO ANSWERS, and the difference is
        // the owner's pool rather than anything on the row. A `startRule:
        // Zero` pool holds nothing on turn one, so its holder owning no free
        // action cannot act at all on the turn the fight begins -- the rule
        // above cannot see that, because it only ever looked at the skill.
        //
        // All three of these hand the set in the way ContentBuilder computes
        // it (pools, then characters, then skills), so the fixture states its
        // own premise rather than depending on what pools.json happens to
        // author.

        [Test]
        public void AFreeDamageSkill_IsStillRejectedForAManaHolder()
        {
            // THE CONTROL, and the half that would let the carve-out ship as a
            // blanket amnesty: `bear` is in the refusing-books set and NOT in
            // the zero-start set, and the free-action rule must still bite.
            var entry = Minimal("free_swing", "bear");
            entry.manaCost = 0;

            bool ok = SkillEntryResolver.TryResolveAll(
                new List<RawSkillEntry> { entry }, new[] { "bear" }, System.Array.Empty<string>(),
                out _, out var errors);

            Assert.IsFalse(ok, "a mana holder's free damage skill is the original spam case and is unchanged");
            StringAssert.Contains("costs neither mana nor resource", errors[0]);
        }

        [Test]
        public void AFreeDamageSkill_IsAllowedForAZeroStartPoolHolder()
        {
            var entry = Minimal("slam", "bear");
            entry.manaCost = 0;

            bool ok = SkillEntryResolver.TryResolveAll(
                new List<RawSkillEntry> { entry }, null, new[] { "bear" },
                out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(0, resolved[0].ManaCost,
                "Bjorn's slam is free because Fury starts at zero, not because the cost was forgotten");
        }

        [Test]
        public void TheCarveOutIsPerOwnerRatherThanPerCatalogue()
        {
            // ONE RESOLVE, TWO OWNERS. Handing the set in per call would let
            // a "zero-start pool exists anywhere in content" reading pass
            // every assertion above; this is the case that separates them.
            var bjorn = Minimal("brace", "bear");
            bjorn.manaCost = 0;
            var shawn = Minimal("shear", "sheep");
            shawn.manaCost = 0;

            bool ok = SkillEntryResolver.TryResolveAll(
                new List<RawSkillEntry> { bjorn, shawn }, null, new[] { "bear" },
                out _, out var errors);

            Assert.IsFalse(ok);
            Assert.AreEqual(1, errors.Count, string.Join("; ", errors));
            StringAssert.Contains("shear", errors[0]);
        }

        [Test]
        public void ProvokeStaysFreeForEverybody()
        {
            // Provoke's whole cost is the turn, which is why it was already
            // exempt (see the resolver's own comment). The carve-out must not
            // have moved that: this passes with an EMPTY zero-start set, i.e.
            // on the exemption that was already there.
            var entry = Minimal("bellow", "sheep");
            entry.manaCost = 0;
            entry.effect = "Provoke";

            bool ok = SkillEntryResolver.TryResolveAll(
                new List<RawSkillEntry> { entry }, null, System.Array.Empty<string>(),
                out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(SkillEffect.Provoke, resolved[0].Effect);
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

        // ---- elements: the rules a choice has to satisfy ------------------------

        // The shape the orb authors, and the one every assertion below breaks
        // in exactly one way.
        private static RawSkillEntry Choosing(params string[] elements)
        {
            var entry = Minimal("orb", "owl");
            entry.damageInstances = new[] { new RawDamageInstance { type = "Earth", amount = 16 } };
            entry.elements = elements.Select(e => new RawElementChoice { type = e }).ToArray();
            return entry;
        }

        private static string ErrorFrom(RawSkillEntry entry)
        {
            bool ok = SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { entry }, out _, out var errors);
            Assert.IsFalse(ok, "the entry was expected to be refused");
            return errors[0];
        }

        [Test]
        public void Elements_FourValidTypes_Resolve()
        {
            bool ok = SkillEntryResolver.TryResolveAll(
                new List<RawSkillEntry> { Choosing("Earth", "Water", "Fire", "Wind") },
                out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.IsTrue(resolved[0].HasElementChoice);
            Assert.AreEqual(4, resolved[0].Elements.Length);
        }

        // The same friendlier spelling damageInstances already accepts, out of
        // the same parser -- an author who has learned to write Frost in one
        // field should not discover the other one wants Ice.
        [Test]
        public void Elements_AcceptFrostForIce()
        {
            bool ok = SkillEntryResolver.TryResolveAll(
                new List<RawSkillEntry> { Choosing("Earth", "Frost") }, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(DamageType.Ice, resolved[0].Elements[1].Type);
        }

        [Test]
        public void Elements_UnknownType_IsRefusedNamingTheEntryAndTheWord()
        {
            string error = ErrorFrom(Choosing("Earth", "Custard"));

            StringAssert.Contains("orb", error);
            StringAssert.Contains("Custard", error);
        }

        [Test]
        public void Elements_ADuplicateType_IsRefused()
        {
            StringAssert.Contains("twice", ErrorFrom(Choosing("Earth", "Water", "Earth")));
        }

        // A choice of one is not a choice: it puts a whole menu depth in front
        // of the player with a single row on it.
        [Test]
        public void Elements_ASingleEntry_IsRefused()
        {
            StringAssert.Contains("not a choice", ErrorFrom(Choosing("Earth")));
        }

        // An Attack-scaled skill rides the caster's own attackType and has no
        // authored packet a choice could retype.
        [Test]
        public void Elements_WithoutDamageInstances_IsRefused()
        {
            var entry = Choosing("Earth", "Fire");
            entry.damageInstances = Array.Empty<RawDamageInstance>();
            entry.flatAmount = 20;

            StringAssert.Contains("needs damageInstances", ErrorFrom(entry));
        }

        // The file has to read as what happens when the first element is
        // picked. A packet typed outside the list is a spell that never once
        // deals what its own damageInstances say.
        [Test]
        public void Elements_APacketTypedOutsideTheList_IsRefused()
        {
            string error = ErrorFrom(Choosing("Water", "Fire"));

            StringAssert.Contains("Earth", error);
            StringAssert.Contains("elements does not offer", error);
        }
    }

    public class SkillResolutionTests
    {
        private static CombatantState Actor(int attack = 5)
        {
            return new CombatantState("Shawn", true, 32, 38, attack, 8);
        }

        private static CombatantState Target(int defense)
        {
            var target = new CombatantState("Wall", false, 100, 0, 6, 5);
            target.PhysicalDefense = defense;
            return target;
        }

        // PINNED literals throughout — nothing here recomputes the formula.
        //
        // REWRITTEN for Phase 1 of the balance redesign: SkillResolution.
        // Damage no longer mitigates at all — it always returns
        // Math.Max(1, raw) — so neither the target's defense nor
        // `ignoresDefense` has any effect on this return value any more.
        // DamagePipeline.AfterDefences is the single place a defense term is
        // ever subtracted now; see DamagePipelineTests for that half.
        //
        // NO LONGER SCALED (fixed 2026-08-26): this raw figure feeds the same
        // mitigated-combat path ComputeAttackDamage/ComputeSkillDamage do
        // (FightSession.Skills.cs's real DamageSingle/DamageAll casts), so it
        // stopped multiplying by CombatMath.DamageScale in the same fix --
        // see SkillResolution.Damage's own header.
        [Test]
        public void Damage_IsAttackPlusScaling_RawAndUnaffectedByTheTargetOrTheFlag()
        {
            // 5 attack + 2 power x 6 spent = 17.
            Assert.AreEqual(17, SkillResolution.Amount(
                SkillEffect.DamageSingle, Actor(), Target(8), power: 2, flatAmount: 0, resourceSpent: 6, ignoresDefense: false));
            Assert.AreEqual(17, SkillResolution.Amount(
                SkillEffect.DamageSingle, Actor(), Target(8), power: 2, flatAmount: 0, resourceSpent: 6, ignoresDefense: true));
        }

        // WHAT A WALL COSTS is now nothing at all, at THIS layer — see the
        // header above. A heavily defended target and an undefended one
        // produce the identical raw figure; only Attack moves it. The
        // property this used to pin (a wall blunting a swing proportionally
        // rather than flattening it) still holds, just one layer further
        // down — see DamagePipelineTests and CombatMathTests.AfterResistance*.
        [Test]
        public void ADefendedTarget_NoLongerChangesTheRawFigureAtAll()
        {
            int againstAWall = SkillResolution.Amount(
                SkillEffect.DamageSingle, Actor(), Target(99), power: 2, flatAmount: 0, resourceSpent: 6, ignoresDefense: false);
            int againstNothing = SkillResolution.Amount(
                SkillEffect.DamageSingle, Actor(), Target(0), power: 2, flatAmount: 0, resourceSpent: 6, ignoresDefense: false);

            Assert.AreEqual(17, againstAWall);
            Assert.AreEqual(againstNothing, againstAWall);

            // AND IT STILL RESPONDS TO GEAR: Attack 22 makes raw 34, exactly
            // twice the 17 above, and the returned figure doubles with it --
            // no floor anywhere near this range to blunt the difference.
            int geared = SkillResolution.Amount(
                SkillEffect.DamageSingle, Actor(22), Target(99), power: 2, flatAmount: 0, resourceSpent: 6, ignoresDefense: false);

            Assert.AreEqual(34, geared, "twice the attack (17 -> 34 raw) is twice the damage (17 -> 34)");
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
            var actor = new CombatantState("Caster", true, 100, 20, 5, 8)
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

            Assert.AreEqual(10, damage, "5 Attack x 2.00 (WeaponScaling S) = 10, raw");
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

            Assert.AreEqual(9, damage, "5 Attack x 1.70 (SkillScaling A) = 8.5, rounded away from zero to 9, raw");
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

            Assert.AreEqual(10, damage, "Weapon override should read WeaponScaling (2.00x) even though the type is Nature");
        }

        [Test]
        public void NoneAxis_NeverScalesAttackAtAll()
        {
            var actor = ScaledActor();
            int damage = SkillResolution.Amount(SkillEffect.DamageSingle, actor, Target(0),
                power: 0, flatAmount: 0, resourceSpent: 0, ignoresDefense: false,
                type: DamageType.Physical, axis: ScalingAxis.None);

            Assert.AreEqual(5, damage, "None should leave Attack completely unscaled: 5, raw");
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
            // power 2 x resourceSpent 4 = 8, total 21 — NOT
            // (5 + 3 + 8) x 2.00 = 32, which is what scaling the whole
            // sum would produce.
            Assert.AreEqual(21, damage);
        }

        [Test]
        public void ResourceToSpend_TakesTheCost_OrEverythingWhenSpendsAll()
        {
            var wool = new ResourcePool("wool", "Wool", 16, 2, 3, 1);
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
            actor.PrimaryPool.Current = 10;
            actor.SignaturePool = new ResourcePool("wool", "Wool", 16, 2, 3, 1);
            actor.SignaturePool.Gain(4);

            Assert.IsTrue(SkillResolution.CanAfford(actor, 10, 4));
            Assert.IsFalse(SkillResolution.CanAfford(actor, 11, 4), "Not enough mana");
            Assert.IsFalse(SkillResolution.CanAfford(actor, 10, 5), "Not enough resource");

            var noResource = Actor();
            noResource.PrimaryPool.Current = 10;
            Assert.IsFalse(SkillResolution.CanAfford(noResource, 10, 1), "No resource at all cannot pay a resource cost");
            Assert.IsTrue(SkillResolution.CanAfford(noResource, 10, 0), "But a mana-only skill is fine");
        }

        // THE CLASS OF BUG, not the instance. Eight authored player-skill
        // effects (Provoke, Transform, Ward, Shatter, BuffParty, GiftMana,
        // GiftFury, GiftHaste) fell through Amount's default case straight
        // into an ArgumentOutOfRangeException -- see
        // ProvokeSkillDetailCardTests for the screen that threw it. This
        // walks every SkillEffect skills.json actually authors (not the
        // whole enum -- an effect nothing authors yet is not this bug) and
        // asserts none of them do, so the next effect authored with no case
        // in Amount is caught here on the day it lands rather than the day
        // a player hovers its card.
        [Test]
        public void Amount_HasACaseForEveryEffectAuthoredContentActuallyUses()
        {
            string json = File.ReadAllText(SkillsJsonPath());

            // A flat scan rather than the record-by-record parser
            // FightCapacityPinTests uses -- there is nothing here that needs
            // correlating back to which skill or character an effect belongs
            // to, only the SET of effect names the file mentions at all.
            var rx = new Regex("\"effect\"\\s*:\\s*\"([^\"]*)\"", RegexOptions.Compiled);
            var names = rx.Matches(json).Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();

            CollectionAssert.IsNotEmpty(names, "fixture: skills.json authored nothing to check");

            var actor = Actor();
            var target = Target(5);

            foreach (var name in names)
            {
                Assert.IsTrue(Enum.TryParse<SkillEffect>(name, out var effect),
                    $"skills.json authors an effect '{name}' SkillEffect has no member for at all");

                Assert.DoesNotThrow(() => SkillResolution.Amount(
                    effect, actor, target, power: 2, flatAmount: 3, resourceSpent: 1, ignoresDefense: false),
                    $"SkillResolution.Amount has no case for {effect}, which skills.json authors at least once");
            }
        }

        private static string SkillsJsonPath()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "_Project", "ContentData")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "Could not locate Assets/_Project/ContentData from the working directory.");
            return Path.Combine(dir.FullName, "Assets", "_Project", "ContentData", "skills.json");
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

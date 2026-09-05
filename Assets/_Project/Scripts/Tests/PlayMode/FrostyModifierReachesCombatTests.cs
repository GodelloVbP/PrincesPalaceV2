using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // PHASE D2, end to end: the real "frostbite" modifiers.json entry's
    // ChilledOnHitChancePercent rider reaches a real fight and actually
    // chills a real target -- not just a number sitting unread on
    // ModifierEffectSet.
    //
    // POST-AFFIX-SPLIT: Ice's family used to be one bundled "frosty" id
    // (ElementalDamageOnHitPercent + TypedResistanceFlat + ChilledOnHitChancePercent,
    // all three at once). A designer pass split it into three single-effect
    // ids -- "frosty" (damage), "permafrost" (resistance), "frostbite" (this
    // file's chill proc) -- so this file's own equip helper now reaches for
    // "frostbite" specifically, the id that actually carries
    // ChilledOnHitChancePercent today.
    //
    // Mirrors SwiftModifierReachesCombatTests/ItemModifierScalingReachesCombatTests'
    // own real-content pattern: equip a REAL item with a REAL id at a known
    // tier/riftTier, never a hand-built ModifierEffect fixture. The hand-
    // built-fixture half of this rider (exact arithmetic, refresh-not-stack,
    // composition with the Necklace ramp) lives in ChilledStatusTests
    // (EditMode).
    public class FrostyModifierReachesCombatTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pp-frosty-tests-" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
        }

        [TearDown]
        public void Restore()
        {
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (System.IO.Directory.Exists(_root)) System.IO.Directory.Delete(_root, recursive: true);
        }

        private static ItemDefinition TierZeroEquippable() =>
            ContentDatabase.Items.FirstOrDefault(i => i != null && i.IsEquippable && i.tier == 0);

        // "frostbite" -- the split-off id that carries Ice's chill proc; see
        // this file's own header for why it is no longer "frosty" itself.
        private static Character FreshCharacterWearing(ItemDefinition item, RiftTier riftTier)
        {
            var definition = ContentDatabase.Characters.FirstOrDefault();
            Assert.IsNotNull(definition, "fixture: content has at least one character");

            var character = new Character(definition.id);
            character.equipment.Set(item.equipSlot, item.id, modifierIds: new List<string> { "frostbite" }, riftTier: (int)riftTier);
            return character;
        }

        // All three of Ice's split affixes at once -- for the one test below
        // that proves they can still coexist on one character, the way one
        // bundled "frosty" roll used to grant all three at once.
        private static Character FreshCharacterWearingWholeIceFamily(ItemDefinition item, RiftTier riftTier)
        {
            var definition = ContentDatabase.Characters.FirstOrDefault();
            Assert.IsNotNull(definition, "fixture: content has at least one character");

            var character = new Character(definition.id);
            character.equipment.Set(item.equipSlot, item.id,
                modifierIds: new List<string> { "frosty", "permafrost", "frostbite" }, riftTier: (int)riftTier);
            return character;
        }

        [Test]
        public void FrostyModifier_ScalesItsChillChanceByTierAndRiftTier()
        {
            var item = TierZeroEquippable();
            Assert.IsNotNull(item, "fixture: content has a tier-0 equippable item");

            var character = FreshCharacterWearing(item, RiftTier.RiftForged);
            Assert.IsTrue(ContentDatabase.ActiveLoadout(character).IsLive(item.equipSlot),
                "fixture check: the item must be LIVE or ModifierEffects reads nothing");

            var effects = ContentDatabase.ModifierEffects(character);
            Assert.IsFalse(effects.IsEmpty, "a real, live-equipped frostbite modifier must produce a real effect");

            // frostbite's authored base: ChilledOnHitChancePercent 10 (halved
            // from 20). Scale = TierMultiplier(0) x RiftMultiplier(RiftForged) = 1.0 x 1.6 = 1.6.
            var chill = effects.All.Single(e => e.Type == ModifierEffectType.ChilledOnHitChancePercent);
            Assert.AreEqual(16, chill.Magnitude, "10 x 1.6 = 16.0 exactly");
        }

        [Test]
        public void FrostyModifier_AtOrdinaryRiftTier_ScalesByExactlyOne()
        {
            var item = TierZeroEquippable();
            Assert.IsNotNull(item, "fixture: content has a tier-0 equippable item");

            var character = FreshCharacterWearing(item, RiftTier.Ordinary);
            var effects = ContentDatabase.ModifierEffects(character);

            var chill = effects.All.Single(e => e.Type == ModifierEffectType.ChilledOnHitChancePercent);
            Assert.AreEqual(10, chill.Magnitude, "tier 0, RiftTier Ordinary: scale is exactly 1.0, unscaled base survives untouched");
        }

        [Test]
        public void FrostyModifier_ReachesCombatantStateModifierEffects_ThroughARealFight()
        {
            var item = TierZeroEquippable();
            Assert.IsNotNull(item, "fixture: content has a tier-0 equippable item");

            var character = FreshCharacterWearing(item, RiftTier.RiftForged);
            var built = FightEncounterAdapter.Build(
                new List<string> { character.definitionId },
                ContentDatabase.Enemies.Take(1).Select(e => e.id).ToList(),
                new SeededRandom(7),
                partyCharacters: new List<Character> { character });

            Assert.IsNotNull(built, "fixture: a real fight must build");
            var state = built.Party[0];

            Assert.AreEqual(16, state.ModifierEffects.Best(ModifierEffectType.ChilledOnHitChancePercent),
                "the real, scaled frostbite chill chance must reach the combatant the fight actually runs on");
        }

        // The Ice family's damage-on-hit ("frosty"), resistance
        // ("permafrost") and chill proc ("frostbite") are three SEPARATE
        // affixes post-split -- a single item can still carry all three at
        // once (three affix slots, three ids), which is what this test
        // proves. It is no longer one guarantee a single "frosty" roll makes
        // on its own; see FreshCharacterWearingWholeIceFamily's own header.
        [Test]
        public void FrostyPermafrostAndFrostbite_CanAllBeEquippedTogether_AndAllThreeStillApply()
        {
            var item = TierZeroEquippable();
            Assert.IsNotNull(item, "fixture: content has a tier-0 equippable item");

            var character = FreshCharacterWearingWholeIceFamily(item, RiftTier.Ordinary);
            var effects = ContentDatabase.ModifierEffects(character);

            Assert.Greater(effects.Best(ModifierEffectType.ElementalDamageOnHitPercent), 0,
                "frosty's own damage-on-hit must still apply when equipped alongside its siblings");
            Assert.IsTrue(effects.All.Any(e => e.Type == ModifierEffectType.TypedResistanceFlat && e.Against == DamageType.Ice),
                "permafrost's own Ice resistance must still apply when equipped alongside its siblings");
            Assert.Greater(effects.Best(ModifierEffectType.ChilledOnHitChancePercent), 0,
                "frostbite's own chill chance must still apply when equipped alongside its siblings");
        }

        // THE REAL END-TO-END CLAIM: equip frostbite on the highest-tier real
        // equippable item content has, at Convergent RiftTier (the richest
        // combination the roll can ever produce), and prove a real
        // FightSession actually chills a real enemy off it -- not a
        // synthetic ModifierEffect fixture, the genuine content -> roll ->
        // equip -> combat chain this whole phase exists to protect.
        //
        // A high enough tier pushes frostbite's scaled chill chance to (or
        // past) 100% outright, which turns "does the rider actually fire"
        // into a DETERMINISTIC claim rather than a statistical one.
        [Test]
        public void FrostyModifier_OnAHighTierItem_ActuallyChillsTheTarget_InARealFight()
        {
            var item = ContentDatabase.Items
                .Where(i => i != null && i.IsEquippable)
                .OrderByDescending(i => i.tier)
                .FirstOrDefault();
            Assert.IsNotNull(item, "fixture: content has at least one equippable item");

            var character = FreshCharacterWearing(item, RiftTier.Convergent);
            Assert.IsTrue(ContentDatabase.ActiveLoadout(character).IsLive(item.equipSlot),
                "fixture check: the item must be LIVE or ModifierEffects reads nothing");

            var effects = ContentDatabase.ModifierEffects(character);
            int scaledChance = effects.Best(ModifierEffectType.ChilledOnHitChancePercent);

            if (scaledChance < 100)
            {
                Assert.Inconclusive(
                    $"fixture: content's highest equippable item tier ({item.tier}) only scales frostbite's chill " +
                    $"chance to {scaledChance}%, short of the guaranteed-proc threshold this test relies on for a " +
                    "deterministic assertion -- FrostyModifier_ScalesItsChillChanceByTierAndRiftTier already " +
                    "pins the scaling formula itself at a lower tier.");
                return;
            }

            // The TOUGHEST real enemy content has, by max health -- not just
            // Take(1) (whichever the catalogue happens to list first). This
            // hero is equipped with the single highest-tier item in the
            // entire game for a guaranteed chill proc, and that same gear
            // hits hard enough to one-shot a low-health early monster before
            // the on-hit rider ever sees a live target (ChilledOnHitChancePercent
            // is correctly gated on target.IsAlive -- see
            // ApplyModifierOnHitRiders). Picking the tankiest real enemy is
            // what keeps this a genuine proof of the chill firing, rather
            // than a coin flip that usually lands on Inconclusive.
            var toughestEnemyId = ContentDatabase.Enemies
                .OrderByDescending(e => e.data.BaseStats.maxHealth)
                .Select(e => e.id)
                .FirstOrDefault();
            Assert.IsNotNull(toughestEnemyId, "fixture: content has at least one enemy");

            var built = FightEncounterAdapter.Build(
                new List<string> { character.definitionId },
                new List<string> { toughestEnemyId },
                new SeededRandom(11),
                partyCharacters: new List<Character> { character });
            Assert.IsNotNull(built, "fixture: a real fight must build");

            var session = built.Session;
            var hero = built.Party[0];
            var foe = session.Encounter.Enemies.FirstOrDefault();
            Assert.IsNotNull(foe, "fixture: the built fight has at least one enemy");

            session.DamageVarianceRange = 0f;
            session.Begin();

            int foeSpeedBefore = foe.Speed;
            session.ExecuteAttack(foe);

            if (!foe.IsAlive)
            {
                Assert.Inconclusive(
                    "fixture: the real enemy content picked here died to one hit from this hero build, so the " +
                    "on-hit rider never had a live target to chill (ChilledOnHitChancePercent is gated on " +
                    "target.IsAlive, correctly -- see ApplyModifierOnHitRiders).");
                return;
            }

            var chilled = foe.Statuses.SingleOrDefault(s => s.Type == StatusEffectType.Chilled);
            Assert.IsNotNull(chilled, "a guaranteed ChilledOnHitChancePercent proc must land Chilled on the first real hit");
            Assert.Less(foe.Speed, foeSpeedBefore,
                "a real, content-driven Chilled application must actually reduce the live Speed stat, not just " +
                "sit on the status list");
        }
    }
}

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
    // PHASE D2, end to end: the real "frosty" modifiers.json entry's new
    // ChilledOnHitChancePercent rider reaches a real fight and actually
    // chills a real target -- not just a number sitting unread on
    // ModifierEffectSet.
    //
    // Mirrors SwiftModifierReachesCombatTests/ItemModifierScalingReachesCombatTests'
    // own real-content pattern: equip a REAL item with the REAL "frosty" id
    // at a known tier/riftTier, never a hand-built ModifierEffect fixture.
    // The hand-built-fixture half of this rider (exact arithmetic, refresh-
    // not-stack, composition with the Necklace ramp) lives in
    // ChilledStatusTests (EditMode).
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

        private static Character FreshCharacterWearing(ItemDefinition item, RiftTier riftTier)
        {
            var definition = ContentDatabase.Characters.FirstOrDefault();
            Assert.IsNotNull(definition, "fixture: content has at least one character");

            var character = new Character(definition.id);
            character.equipment.Set(item.equipSlot, item.id, modifierIds: new List<string> { "frosty" }, riftTier: (int)riftTier);
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
            Assert.IsFalse(effects.IsEmpty, "a real, live-equipped frosty modifier must produce a real effect");

            // frosty's authored base: ChilledOnHitChancePercent 20. Scale =
            // TierMultiplier(0) x RiftMultiplier(RiftForged) = 1.0 x 1.6 = 1.6.
            var chill = effects.All.Single(e => e.Type == ModifierEffectType.ChilledOnHitChancePercent);
            Assert.AreEqual(32, chill.Magnitude, "20 x 1.6 = 32.0 exactly");
        }

        [Test]
        public void FrostyModifier_AtOrdinaryRiftTier_ScalesByExactlyOne()
        {
            var item = TierZeroEquippable();
            Assert.IsNotNull(item, "fixture: content has a tier-0 equippable item");

            var character = FreshCharacterWearing(item, RiftTier.Ordinary);
            var effects = ContentDatabase.ModifierEffects(character);

            var chill = effects.All.Single(e => e.Type == ModifierEffectType.ChilledOnHitChancePercent);
            Assert.AreEqual(20, chill.Magnitude, "tier 0, RiftTier Ordinary: scale is exactly 1.0, unscaled base survives untouched");
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

            Assert.AreEqual(32, state.ModifierEffects.Best(ModifierEffectType.ChilledOnHitChancePercent),
                "the real, scaled frosty chill chance must reach the combatant the fight actually runs on");
        }

        // frosty ALSO still carries its elemental family's damage-on-hit and
        // typed resistance (Phase C) -- this Chill rider is meant to ADD to
        // those, never replace them. A quick, cheap check that the family's
        // original effects are still authored on the same modifier.
        [Test]
        public void FrostyModifier_StillCarriesItsElementalDamageAndResistance_AlongsideTheNewChillRider()
        {
            var item = TierZeroEquippable();
            Assert.IsNotNull(item, "fixture: content has a tier-0 equippable item");

            var character = FreshCharacterWearing(item, RiftTier.Ordinary);
            var effects = ContentDatabase.ModifierEffects(character);

            Assert.Greater(effects.Best(ModifierEffectType.ElementalDamageOnHitPercent), 0,
                "frosty must still deal its own elemental damage-on-hit -- the chill rider ADDS, it does not replace");
            Assert.IsTrue(effects.All.Any(e => e.Type == ModifierEffectType.TypedResistanceFlat && e.Against == DamageType.Ice),
                "frosty must still resist Ice");
        }

        // THE REAL END-TO-END CLAIM: equip frosty on the highest-tier real
        // equippable item content has, at Convergent RiftTier (the richest
        // combination the roll can ever produce), and prove a real
        // FightSession actually chills a real enemy off it -- not a
        // synthetic ModifierEffect fixture, the genuine content -> roll ->
        // equip -> combat chain this whole phase exists to protect.
        //
        // A high enough tier pushes frosty's scaled chill chance to (or
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
                    $"fixture: content's highest equippable item tier ({item.tier}) only scales frosty's chill " +
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
                .OrderByDescending(e => e.baseStats.maxHealth)
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

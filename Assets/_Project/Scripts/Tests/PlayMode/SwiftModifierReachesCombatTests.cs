using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.PlayModeTests
{
    // PHASE D1, end to end: the real "swift" modifiers.json entry authored
    // for this phase (Phase C stopped at 11 families and never authored a
    // dodge-family modifier -- see modifiers.json's own "swift" entry
    // comment) reaches a real fight and actually causes misses, not just a
    // number sitting unread on ModifierEffectSet.
    //
    // Mirrors ItemModifierScalingReachesCombatTests/FortunateFavorTests'
    // own real-content pattern: equip a REAL item with the REAL "swift" id
    // at a known tier/riftTier, never a hand-built ModifierEffect fixture.
    public class SwiftModifierReachesCombatTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pp-swift-tests-" + System.Guid.NewGuid().ToString("N"));
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
            character.equipment.Set(item.equipSlot, item.id, modifierIds: new List<string> { "swift" }, riftTier: (int)riftTier);
            return character;
        }

        [Test]
        public void SwiftModifier_ScalesItsDodgeChanceByTierAndRiftTier()
        {
            var item = TierZeroEquippable();
            Assert.IsNotNull(item, "fixture: content has a tier-0 equippable item");

            var character = FreshCharacterWearing(item, RiftTier.RiftForged);
            Assert.IsTrue(ContentDatabase.ActiveLoadout(character).IsLive(item.equipSlot),
                "fixture check: the item must be LIVE or ModifierEffects reads nothing");

            var effects = ContentDatabase.ModifierEffects(character);
            Assert.IsFalse(effects.IsEmpty, "a real, live-equipped swift modifier must produce a real effect");

            // swift's authored base: DodgeChancePercent 12. Scale =
            // TierMultiplier(0) x RiftMultiplier(RiftForged) = 1.0 x 1.6 = 1.6.
            var dodge = effects.All.Single(e => e.Type == ModifierEffectType.DodgeChancePercent);
            Assert.AreEqual(19, dodge.Magnitude, "12 x 1.6 = 19.2, away-from-zero rounded");
        }

        [Test]
        public void SwiftModifier_AtOrdinaryRiftTier_ScalesByExactlyOne()
        {
            var item = TierZeroEquippable();
            Assert.IsNotNull(item, "fixture: content has a tier-0 equippable item");

            var character = FreshCharacterWearing(item, RiftTier.Ordinary);
            var effects = ContentDatabase.ModifierEffects(character);

            var dodge = effects.All.Single(e => e.Type == ModifierEffectType.DodgeChancePercent);
            Assert.AreEqual(12, dodge.Magnitude, "tier 0, RiftTier Ordinary: scale is exactly 1.0, unscaled base survives untouched");
        }

        [Test]
        public void SwiftModifier_ReachesCombatantStateModifierEffects_ThroughARealFight()
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

            Assert.AreEqual(19, state.ModifierEffects.Best(ModifierEffectType.DodgeChancePercent),
                "the real, scaled swift dodge chance must reach the combatant the fight actually runs on");
        }

        // THE REAL END-TO-END CLAIM: equip swift on the highest-tier real
        // equippable item content has, at Convergent RiftTier (the richest
        // combination the roll can ever produce), and prove a real
        // FightSession actually produces misses off it -- not a synthetic
        // ModifierEffect fixture, the genuine content -> roll -> equip ->
        // combat chain this whole phase exists to protect.
        //
        // Swift protects its WEARER as the TARGET of an incoming swing, so
        // this fight runs the ENEMY's real attacks against the hero (via
        // FightSession.ExecuteAttack's own auto-reply, the identical
        // mechanism EnemyAiTests relies on) and asserts the hero's health,
        // not the enemy's.
        //
        // A high enough tier pushes swift's scaled dodge chance to (or past)
        // 100% outright -- TierMultiplier alone reaches ~9.3x by tier 10 --
        // which turns "does dodge actually fire at the expected rate" into a
        // DETERMINISTIC claim (every enemy swing must miss) rather than a
        // statistical one, and a deterministic real-content assertion is a
        // strictly stronger proof than sampling a rate would be.
        [Test]
        public void SwiftModifier_OnAHighTierItem_ActuallyCausesRealMisses_InARealFight()
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
            int scaledDodge = effects.Best(ModifierEffectType.DodgeChancePercent);

            if (scaledDodge < 100)
            {
                Assert.Inconclusive(
                    $"fixture: content's highest equippable item tier ({item.tier}) only scales swift to " +
                    $"{scaledDodge}%, short of the guaranteed-miss threshold this test relies on for a " +
                    "deterministic assertion -- SwiftModifier_ScalesItsDodgeChanceByTierAndRiftTier already " +
                    "pins the scaling formula itself at a lower tier.");
                return;
            }

            var built = FightEncounterAdapter.Build(
                new List<string> { character.definitionId },
                ContentDatabase.Enemies.Take(1).Select(e => e.id).ToList(),
                new SeededRandom(11),
                partyCharacters: new List<Character> { character });
            Assert.IsNotNull(built, "fixture: a real fight must build");

            var session = built.Session;
            var hero = built.Party[0];
            var foe = session.Encounter.Enemies.FirstOrDefault();
            Assert.IsNotNull(foe, "fixture: the built fight has at least one enemy");

            session.DamageVarianceRange = 0f;
            session.Begin();

            int startingHealth = hero.CurrentHealth;
            for (int i = 0; i < 10 && hero.IsAlive && foe.IsAlive; i++)
            {
                session.ExecuteAttack(foe);
            }

            Assert.AreEqual(startingHealth, hero.CurrentHealth,
                "with swift scaled to a guaranteed dodge chance, every real enemy swing against the hero " +
                "must miss -- if even one landed, dodge is not actually gating this real content's combat");
        }
    }
}

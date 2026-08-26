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
    // PHASE D3, end to end: the real "sylvan" modifiers.json entry's new
    // RootChancePercent rider reaches a real fight and actually roots a real
    // enemy -- not just a number sitting unread on ModifierEffectSet.
    //
    // Mirrors FrostyModifierReachesCombatTests' own real-content pattern:
    // equip a REAL item with the REAL "sylvan" id at a known tier/riftTier,
    // never a hand-built ModifierEffect fixture. Sylvan is a PLAYER-worn
    // weapon modifier, so this always roots an ENEMY the wearer hits --
    // never the reverse (see ModifierEffectType.RootChancePercent's own
    // comment on why the direction is one-way by construction). The hand-
    // built-fixture half of this rider (exact mechanism: the plain-attack
    // gate, the no-legal-skill forfeit, coexistence with Chilled/Dodge)
    // lives in RootedStatusTests (EditMode).
    public class SylvanModifierReachesCombatTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pp-sylvan-tests-" + System.Guid.NewGuid().ToString("N"));
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
            character.equipment.Set(item.equipSlot, item.id, modifierIds: new List<string> { "sylvan" }, riftTier: (int)riftTier);
            return character;
        }

        [Test]
        public void SylvanModifier_ScalesItsRootChanceByTierAndRiftTier()
        {
            var item = TierZeroEquippable();
            Assert.IsNotNull(item, "fixture: content has a tier-0 equippable item");

            var character = FreshCharacterWearing(item, RiftTier.RiftForged);
            Assert.IsTrue(ContentDatabase.ActiveLoadout(character).IsLive(item.equipSlot),
                "fixture check: the item must be LIVE or ModifierEffects reads nothing");

            var effects = ContentDatabase.ModifierEffects(character);
            Assert.IsFalse(effects.IsEmpty, "a real, live-equipped sylvan modifier must produce a real effect");

            // sylvan's authored base: RootChancePercent 20. Scale =
            // TierMultiplier(0) x RiftMultiplier(RiftForged) = 1.0 x 1.6 = 1.6.
            var root = effects.All.Single(e => e.Type == ModifierEffectType.RootChancePercent);
            Assert.AreEqual(32, root.Magnitude, "20 x 1.6 = 32.0 exactly");
        }

        [Test]
        public void SylvanModifier_AtOrdinaryRiftTier_ScalesByExactlyOne()
        {
            var item = TierZeroEquippable();
            Assert.IsNotNull(item, "fixture: content has a tier-0 equippable item");

            var character = FreshCharacterWearing(item, RiftTier.Ordinary);
            var effects = ContentDatabase.ModifierEffects(character);

            var root = effects.All.Single(e => e.Type == ModifierEffectType.RootChancePercent);
            Assert.AreEqual(20, root.Magnitude, "tier 0, RiftTier Ordinary: scale is exactly 1.0, unscaled base survives untouched");
        }

        [Test]
        public void SylvanModifier_ReachesCombatantStateModifierEffects_ThroughARealFight()
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

            Assert.AreEqual(32, state.ModifierEffects.Best(ModifierEffectType.RootChancePercent),
                "the real, scaled sylvan root chance must reach the combatant the fight actually runs on");
        }

        // sylvan ALSO still carries its elemental family's damage-on-hit and
        // typed resistance (Phase C) -- this Root rider is meant to ADD to
        // those, never replace them. A quick, cheap check that the family's
        // original effects are still authored on the same modifier.
        [Test]
        public void SylvanModifier_StillCarriesItsElementalDamageAndResistance_AlongsideTheNewRootRider()
        {
            var item = TierZeroEquippable();
            Assert.IsNotNull(item, "fixture: content has a tier-0 equippable item");

            var character = FreshCharacterWearing(item, RiftTier.Ordinary);
            var effects = ContentDatabase.ModifierEffects(character);

            Assert.Greater(effects.Best(ModifierEffectType.ElementalDamageOnHitPercent), 0,
                "sylvan must still deal its own elemental damage-on-hit -- the root rider ADDS, it does not replace");
            Assert.IsTrue(effects.All.Any(e => e.Type == ModifierEffectType.TypedResistanceFlat && e.Against == DamageType.Nature),
                "sylvan must still resist Nature");
        }

        // THE REAL END-TO-END CLAIM: equip sylvan on the highest-tier real
        // equippable item content has, at Convergent RiftTier (the richest
        // combination the roll can ever produce), and prove a real
        // FightSession actually roots a real enemy off it -- not a
        // synthetic ModifierEffect fixture, the genuine content -> roll ->
        // equip -> combat chain this whole phase exists to protect. Also the
        // one place this file checks the ROOTED TARGET'S OWN NEXT ACTION:
        // the enemy's reply this same round must never resolve as a plain
        // attack (RootedStatusTests proves the gate mechanism itself against
        // hand-built fixtures; this proves the real "sylvan" content
        // actually drives that exact same StatusEffects.Apply call).
        [Test]
        public void SylvanModifier_OnAHighTierItem_ActuallyRootsTheTarget_InARealFight()
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
            int scaledChance = effects.Best(ModifierEffectType.RootChancePercent);

            if (scaledChance < 100)
            {
                Assert.Inconclusive(
                    $"fixture: content's highest equippable item tier ({item.tier}) only scales sylvan's root " +
                    $"chance to {scaledChance}%, short of the guaranteed-proc threshold this test relies on for a " +
                    "deterministic assertion -- SylvanModifier_ScalesItsRootChanceByTierAndRiftTier already " +
                    "pins the scaling formula itself at a lower tier.");
                return;
            }

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

            session.ExecuteAttack(foe);
            var beats = session.DrainBeats();

            if (!foe.IsAlive)
            {
                Assert.Inconclusive(
                    "fixture: the real enemy content picked here died to one hit from this hero build, so the " +
                    "on-hit rider never had a live target to root (RootChancePercent is gated on " +
                    "target.IsAlive, correctly -- see ApplyModifierOnHitRiders).");
                return;
            }

            Assert.IsTrue(StatusEffects.HasRooted(foe.Statuses),
                "a guaranteed RootChancePercent proc must land Rooted on the first real hit");

            // The gated next action: whatever the rooted foe did on its own
            // reply this round (already resolved above, inside ExecuteAttack's
            // own AdvanceAfterAction/AutoResolveEnemyTurns), it must not be a
            // plain attack -- either it used a skill, or it had none legal
            // and forfeited (both message shapes are distinct from the plain
            // "X attacks Y for N damage!" line a legal plain swing prints).
            var enemyLines = beats
                .Where(b => b.Actor != null && !b.Actor.IsPlayerSide)
                .SelectMany(b => b.Messages)
                .ToList();

            bool resolvedAsPlainAttack = enemyLines.Any(m =>
                m.Contains($"{foe.Name} attacks") && m.Contains("damage!"));

            Assert.IsFalse(resolvedAsPlainAttack,
                "a real, content-rooted enemy must never resolve its own reply as a plain attack this round");
        }
    }
}

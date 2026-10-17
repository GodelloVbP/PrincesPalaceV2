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
    // PHASE D3, end to end: the real "entangling" modifiers.json entry's
    // RootChancePercent rider reaches a real fight and actually roots a real
    // enemy -- not just a number sitting unread on EffectSet.
    //
    // This file's equip helper reaches for "entangling", the id that carries
    // RootChancePercent.
    //
    // Mirrors FrostyModifierReachesCombatTests' own real-content pattern:
    // equip a REAL item with a REAL id at a known tier, never a
    // hand-built Effect fixture. Sylvan's family is PLAYER-worn
    // weapon gear, so this always roots an ENEMY the wearer hits -- never
    // the reverse (see EffectKind.RootChancePercent's own comment on
    // why the direction is one-way by construction). The hand-built-fixture
    // half of this rider (exact mechanism: the plain-attack gate, the
    // no-legal-skill forfeit, coexistence with Chilled/Dodge) lives in
    // RootedStatusTests (EditMode).
    public class SylvanModifierReachesCombatTests
    {
        // Far past any one swing this suite's gear can land, so the on-hit
        // riders always meet a live target (AUDIT #46).
        private const int DurableFoeHealth = 1000000;

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
            ContentDatabase.Equippables.FirstOrDefault(i => i != null);

        private static Character FreshCharacterWearing(ItemDefinition item, int tier = 0)
        {
            var definition = ContentDatabase.Characters.FirstOrDefault();
            Assert.IsNotNull(definition, "fixture: content has at least one character");

            var character = new Character(definition.id);
            character.equipment.Set(item.equipSlot, item.id, modifierIds: new List<string> { "entangling" }, tier: tier);
            return character;
        }

        // Three affixes at once, for the coexistence test below.
        private static Character FreshCharacterWearingWholeNatureFamily(ItemDefinition item)
        {
            var definition = ContentDatabase.Characters.FirstOrDefault();
            Assert.IsNotNull(definition, "fixture: content has at least one character");

            var character = new Character(definition.id);
            character.equipment.Set(item.equipSlot, item.id,
                modifierIds: new List<string> { "attuned", "veiled", "entangling" });
            return character;
        }

        [Test]
        public void SylvanModifier_ScalesItsRootChanceByTier()
        {
            var item = GearFixtures.WearableAt(5);
            Assert.IsNotNull(item, "fixture: content has an equippable the first character can wear at tier 5");

            var character = FreshCharacterWearing(item, tier: 5);
            Assert.IsTrue(ContentDatabase.ActiveLoadout(character).IsLive(item.equipSlot),
                "fixture check: the item must be LIVE or ModifierEffects reads nothing");

            var effects = ContentDatabase.CharacterEffects(character);
            Assert.IsFalse(effects.IsEmpty, "a real, live-equipped entangling modifier must produce a real effect");

            // entangling's authored base: RootChancePercent 10 (halved from 20).
            // Scale = TierMultiplier(5) = 1.25^5 = 3.0517578125, so
            // 10 x 3.0517578125 = 30.52, away-from-zero rounded to 31.
            var root = effects.All.Single(e => e.Kind == EffectKind.RootChancePercent);
            Assert.AreEqual(31, root.Magnitude);
        }

        [Test]
        public void SylvanModifier_AtTierZero_ScalesByExactlyOne()
        {
            var item = TierZeroEquippable();
            Assert.IsNotNull(item, "fixture: content has a tier-0 equippable item");

            var character = FreshCharacterWearing(item);
            var effects = ContentDatabase.CharacterEffects(character);

            var root = effects.All.Single(e => e.Kind == EffectKind.RootChancePercent);
            Assert.AreEqual(10, root.Magnitude, "tier 0: scale is exactly 1.0, unscaled base survives untouched");
        }

        [Test]
        public void SylvanModifier_ReachesCombatantStateModifierEffects_ThroughARealFight()
        {
            var item = GearFixtures.WearableAt(5);
            Assert.IsNotNull(item, "fixture: content has an equippable the first character can wear at tier 5");

            var character = FreshCharacterWearing(item, tier: 5);
            var built = FightEncounterAdapter.Build(
                new List<string> { character.definitionId },
                ContentDatabase.Enemies.Take(1).Select(e => e.id).ToList(),
                new SeededRandom(7),
                partyCharacters: new List<Character> { character });

            Assert.IsNotNull(built, "fixture: a real fight must build");
            var state = built.Party[0];

            Assert.AreEqual(31, state.Effects.Magnitude(EffectKind.RootChancePercent),
                "the real, scaled entangling root chance must reach the combatant the fight actually runs on");
        }

        // A damage rider ("attuned"), a resistance ("veiled") and the root
        // proc ("entangling") are three separate affixes; one item can carry
        // all three at once.
        [Test]
        public void AttunedVeiledAndEntangling_CanAllBeEquippedTogether_AndAllThreeStillApply()
        {
            var item = TierZeroEquippable();
            Assert.IsNotNull(item, "fixture: content has a tier-0 equippable item");

            var character = FreshCharacterWearingWholeNatureFamily(item);
            var effects = ContentDatabase.CharacterEffects(character);

            Assert.Greater(effects.Magnitude(EffectKind.ElementalDamageOnHitPercent), 0,
                "attuned's own damage-on-hit must still apply when equipped alongside its siblings");
            Assert.IsTrue(effects.All.Any(e => e.Kind == EffectKind.ResistanceFlat && e.AgainstMagical),
                "veiled's own magical resistance must still apply when equipped alongside its siblings");
            Assert.Greater(effects.Magnitude(EffectKind.RootChancePercent), 0,
                "entangling's own root chance must still apply when equipped alongside its siblings");
        }

        // THE REAL END-TO-END CLAIM: equip entangling on the highest-tier real
        // equippable item content has, at the tier cap (the richest
        // combination the roll can ever produce), and prove a real
        // FightSession actually roots a real enemy off it -- not a
        // synthetic Effect fixture, the genuine content -> roll ->
        // equip -> combat chain this whole phase exists to protect. Also the
        // one place this file checks the ROOTED TARGET'S OWN NEXT ACTION:
        // the enemy's reply this same round must never resolve as a plain
        // attack (RootedStatusTests proves the gate mechanism itself against
        // hand-built fixtures; this proves the real "entangling" content
        // actually drives that exact same StatusEffects.Apply call).
        [Test]
        public void SylvanModifier_OnAHighTierItem_ActuallyRootsTheTarget_InARealFight()
        {
            var item = GearFixtures.WearableAt(ItemInstance.TierCap);
            Assert.IsNotNull(item, "fixture: content has an equippable the first character can wear at the tier cap");

            var character = FreshCharacterWearing(item, ItemInstance.TierCap);
            Assert.IsTrue(ContentDatabase.ActiveLoadout(character).IsLive(item.equipSlot),
                "fixture check: the item must be LIVE or ModifierEffects reads nothing");

            var effects = ContentDatabase.CharacterEffects(character);
            int scaledChance = effects.Magnitude(EffectKind.RootChancePercent);

            // A CONTENT PRECONDITION, stated rather than hoped for (AUDIT #46): an
            // Inconclusive here read as green and quietly retired the end-to-end claim.
            Assert.GreaterOrEqual(scaledChance, 90,
                $"the tier cap ({ItemInstance.TierCap}) only scales entangling's root " +
                $"chance to {scaledChance}%, short of the near-certain threshold this test relies on, with the seeded draw below, for a " +
                "deterministic assertion -- SylvanModifier_ScalesItsRootChanceByTier already " +
                "pins the scaling formula itself at a lower tier." +
                " This end-to-end test has stopped covering the root proc; give it a fixture item that guarantees it.");

            // Among enemies a room can roll. The event-only Bellwether
            // (rollable: false, M7a) outranked the Forest Troll on health, and
            // at speed 8 it takes its own turn before the hero's next one: the
            // proc landed, then the one-turn status wore off on that turn,
            // before the assertions below read it. The Troll (speed 4) never
            // got a turn in between.
            var toughestEnemyId = ContentDatabase.Enemies
                .Where(e => e.Data.Rollable)
                .OrderByDescending(e => e.Data.BaseStats.maxHealth)
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
            // THE FOE OUTLASTS THE SWING BY FIXTURE, not by luck of content
            // (AUDIT #46). This used to Assert.Inconclusive whenever the
            // picked enemy died to one hit, which switched the rider check off
            // the moment gear or enemy numbers moved. Health is not what is
            // under test; a live target for the on-hit rider is.
            foe.MaxHealth = DurableFoeHealth;
            foe.CurrentHealth = DurableFoeHealth;
            session.Begin();

            session.ExecuteAttack(foe);
            var beats = session.DrainBeats();

            Assert.IsTrue(foe.IsAlive, "fixture: the durable foe died to one swing, so no on-hit rider had a live target");

            Assert.IsTrue(StatusEffects.HasRooted(foe.Statuses),
                "a guaranteed RootChancePercent proc must land Rooted on the first real hit");

            // The gated next action: whatever the rooted foe did on its own
            // reply this round (already resolved above, inside ExecuteAttack's
            // own AdvanceAfterAction/AutoResolveEnemyTurns), it must not be a
            // plain attack -- either it used a skill, or it had none legal
            // and forfeited (both message shapes are distinct from the plain
            // "X attacks Y for N damage!" line a legal plain swing prints).
            var enemyLines = beats
                .Where(b => b.IsAction && !b.Actor.IsPlayerSide)
                .SelectMany(b => b.Messages)
                .ToList();

            bool resolvedAsPlainAttack = enemyLines.Any(m =>
                m.Contains($"{foe.Name} attacks") && m.Contains("damage!"));

            Assert.IsFalse(resolvedAsPlainAttack,
                "a real, content-rooted enemy must never resolve its own reply as a plain attack this round");
        }
    }
}

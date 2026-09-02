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

            // swift's authored base: DodgeRating 6 (halved from 12). Scale =
            // TierMultiplier(0) x RiftMultiplier(RiftForged) = 1.0 x 1.6 = 1.6.
            var dodge = effects.All.Single(e => e.Type == ModifierEffectType.DodgeRating);
            Assert.AreEqual(10, dodge.Magnitude, "6 x 1.6 = 9.6, away-from-zero rounded");
        }

        [Test]
        public void SwiftModifier_AtOrdinaryRiftTier_ScalesByExactlyOne()
        {
            var item = TierZeroEquippable();
            Assert.IsNotNull(item, "fixture: content has a tier-0 equippable item");

            var character = FreshCharacterWearing(item, RiftTier.Ordinary);
            var effects = ContentDatabase.ModifierEffects(character);

            var dodge = effects.All.Single(e => e.Type == ModifierEffectType.DodgeRating);
            Assert.AreEqual(6, dodge.Magnitude, "tier 0, RiftTier Ordinary: scale is exactly 1.0, unscaled base survives untouched");
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

            Assert.AreEqual(10, state.ModifierEffects.Best(ModifierEffectType.DodgeRating),
                "the real, scaled swift dodge chance must reach the combatant the fight actually runs on");
        }

        // THE REAL END-TO-END CLAIM: equip swift on the highest-tier real
        // equippable item content has, at Convergent RiftTier (the richest
        // combination the roll can ever produce), and prove a real
        // FightSession actually reads it into fewer landed hits -- not a
        // synthetic ModifierEffect fixture, the genuine content -> roll ->
        // equip -> combat chain this whole phase exists to protect.
        //
        // Swift protects its WEARER as the TARGET of an incoming swing, so
        // this fight runs the ENEMY's real attacks against the hero (via
        // FightSession.ExecuteAttack's own auto-reply, the identical
        // mechanism EnemyAiTests relies on) and tracks the hero's own
        // cumulative damage taken, not the enemy's.
        //
        // NO LONGER A DETERMINISTIC "every swing must miss" CLAIM. Before
        // the dodge curve existed, a high enough tier pushed swift's scaled
        // Magnitude to (or past) a literal 100, which the OLD direct-percent
        // reading treated as a guaranteed dodge. Under CombatMath.
        // DodgePercentFrom, no finite rating is EVER a true guarantee (its
        // own header proves 100 * R / (R + 100) < 100 for every finite R)
        // -- which is the entire point of the fix this phase implements, so
        // a test that still demanded a deterministic zero-damage outcome
        // would be asserting the exact bug this phase exists to remove.
        //
        // Proven instead by COMPARISON: the IDENTICAL real content (item,
        // tier, enemy) fought over the same number of rounds twice, once
        // with swift equipped and once with no modifier at all -- if the
        // content -> roll -> equip -> combat chain is genuinely wiring a
        // scaled dodge rating into real combat, the swift-wearing hero
        // must take substantially less cumulative damage than the
        // identical fight with nothing granting it any dodge at all.
        //
        // SUMMED ACROSS SEVERAL SEEDS, not one. A single 40-round seed is a
        // small enough sample that a probabilistic dodge (halved from the
        // affix-power-down pass: base DodgeRating 12 -> 6, which at this
        // item's own tier/RiftTier still curves to a real ~53% dodge chance
        // -- see CombatMath.DodgePercentFrom, not negligible) can tie or
        // even lose to the no-dodge control by chance alone on any ONE
        // seed; SeededRandom(11) did exactly that after the halving. Eight
        // independent seeds, summed, is what actually distinguishes "the
        // mechanism is genuinely wired in" from "this one draw happened to
        // run cold" -- the claim under test is about the WIRING, not about
        // any single roll of it.
        [Test]
        public void SwiftModifier_OnAHighTierItem_MeaningfullyReducesRealDamageTaken_InARealFight()
        {
            var item = ContentDatabase.Items
                .Where(i => i != null && i.IsEquippable)
                .OrderByDescending(i => i.tier)
                .FirstOrDefault();
            Assert.IsNotNull(item, "fixture: content has at least one equippable item");

            int TotalDamageTakenOverRounds(List<string> modifierIds, int rounds, ulong seed)
            {
                var definition = ContentDatabase.Characters.FirstOrDefault();
                var character = new Character(definition.id);
                character.equipment.Set(item.equipSlot, item.id, modifierIds: modifierIds, riftTier: (int)RiftTier.Convergent);
                Assert.IsTrue(ContentDatabase.ActiveLoadout(character).IsLive(item.equipSlot),
                    "fixture check: the item must be LIVE or ModifierEffects reads nothing");

                var built = FightEncounterAdapter.Build(
                    new List<string> { character.definitionId },
                    ContentDatabase.Enemies.Take(1).Select(e => e.id).ToList(),
                    new SeededRandom(seed),
                    partyCharacters: new List<Character> { character });
                Assert.IsNotNull(built, "fixture: a real fight must build");

                var session = built.Session;
                var hero = built.Party[0];
                var foe = session.Encounter.Enemies.FirstOrDefault();
                Assert.IsNotNull(foe, "fixture: the built fight has at least one enemy");

                session.DamageVarianceRange = 0f;
                session.Begin();

                int totalTaken = 0;
                for (int i = 0; i < rounds && hero.IsAlive && foe.IsAlive; i++)
                {
                    int before = hero.CurrentHealth;
                    session.ExecuteAttack(foe);
                    totalTaken += before - hero.CurrentHealth;
                }

                return totalTaken;
            }

            const int rounds = 40;
            ulong[] seeds = { 11, 12, 13, 14, 15, 16, 17, 18 };

            int withSwift = 0;
            int withoutSwift = 0;
            foreach (ulong seed in seeds)
            {
                withSwift += TotalDamageTakenOverRounds(new List<string> { "swift" }, rounds, seed);
                withoutSwift += TotalDamageTakenOverRounds(new List<string>(), rounds, seed);
            }

            Assert.Less(withSwift, withoutSwift,
                "the real swift-equipped hero must take LESS cumulative damage, summed over several seeds, than " +
                "the identical fights with no dodge-granting modifier equipped -- if not, dodge is not actually " +
                "gating this real content's combat");
        }
    }
}

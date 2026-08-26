using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.PlayModeTests
{
    // The Phase A2 seam: CombatantState.ModifierEffects gets built and
    // attached at the same place Talents/relics/WeaponScaling already are.
    //
    // STILL SCOPED TO "the wire exists and answers Empty for gear with no
    // rolled modifierIds" -- it does not exercise a modifier's effect moving
    // a stat, which is what ItemModifierScalingReachesCombatTests and
    // ItemModifierCombatHookTests (Phase C) cover now that real hooks exist.
    // The sibling of EquipmentReachesCombatTests and RelicsReachCombatTests.
    public class ItemModifierEffectsReachCombatTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-modwire-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
        }

        [TearDown]
        public void Restore()
        {
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static CharacterDefinition FirstCharacter() =>
            ContentDatabase.Characters.FirstOrDefault();

        private static List<string> OneEnemy() =>
            ContentDatabase.Enemies.Take(1).Select(e => e.id).ToList();

        private static FightEncounterAdapter.BuiltFight BuildFor(Character character)
        {
            return FightEncounterAdapter.Build(
                new List<string> { character.definitionId },
                OneEnemy(),
                new Domain.Rng.SeededRandom(11),
                partyCharacters: new List<Character> { character });
        }

        // THE HEADLINE PHASE A2 CLAIM: every real character today resolves
        // to ModifierEffectSet.Empty, because Phase A1 gave modifierIds a
        // real field but nothing populates it yet (Phase A3's roll). Walks
        // every character in content rather than picking one, since the
        // claim is about ALL of them, and a fresh Character() starts with
        // nothing equipped either way.
        [Test]
        public void ModifierEffects_AreEmptyForEveryRealCharacterToday()
        {
            var characters = ContentDatabase.Characters;
            Assert.Greater(characters.Count, 0, "fixture: content has at least one character");

            foreach (var definition in characters)
            {
                var character = new Character(definition.id);
                var effects = ContentDatabase.ModifierEffects(character);

                Assert.IsTrue(effects.IsEmpty,
                    $"'{definition.id}' resolved a non-empty ModifierEffectSet, but nothing in the game " +
                    "populates modifierIds yet (Phase A3 wires the roll) -- this should be unreachable today.");
            }
        }

        // Also true with a real (still-empty) item worn: EquipmentReachesCombatTests
        // already proves a worn item's STATS reach the stage; this proves
        // wearing one does not accidentally manufacture a modifier effect
        // out of nowhere before Phase A3 exists to roll one.
        [Test]
        public void ModifierEffects_StayEmptyWithARealItemEquipped()
        {
            var definition = FirstCharacter();
            var item = ContentDatabase.Items.FirstOrDefault(i => i != null && i.IsEquippable);
            Assert.IsNotNull(definition, "fixture: content has a character");
            Assert.IsNotNull(item, "fixture: content has an equippable item");

            var character = new Character(definition.id);
            character.equipment.Set(item.equipSlot, item.id);

            var effects = ContentDatabase.ModifierEffects(character);

            Assert.IsTrue(effects.IsEmpty,
                "equipping an item with no rolled modifierIds must not produce any modifier effects");
        }

        // THE WIRE ITSELF: a real fight build reaches CombatantState.ModifierEffects
        // without throwing, and it is never null -- CombatantState's own
        // default (ModifierEffectSet.Empty) covers every combatant the
        // adapter did not explicitly set it for (every enemy, and the
        // tooling-only party path -- see FightEncounterAdapter's own
        // comment on why that overload has no Character to read equipment
        // from).
        [Test]
        public void ABuiltFight_NeverLeavesModifierEffectsNull()
        {
            var definition = FirstCharacter();
            var character = new Character(definition.id);

            var built = BuildFor(character);

            foreach (var combatant in built.Party)
            {
                Assert.IsNotNull(combatant.ModifierEffects, $"'{combatant.Name}' has a null ModifierEffects");
            }
        }

        // REGRESSION GUARD: adding the ModifierEffects assignment beside
        // Talents/relics/WeaponScaling at the adapter seam must not disturb
        // any of those. Asserted against the SAME expected values
        // EquipmentReachesCombatTests already pins for this seam, so a
        // regression here fails for the same reason it would there.
        [Test]
        public void WiringModifierEffects_DoesNotDisturbExistingStageWiring()
        {
            var definition = FirstCharacter();
            var character = new Character(definition.id);
            var expectedStats = ContentDatabase.EffectiveStats(character);

            var state = BuildFor(character).Party[0];

            Assert.AreEqual(expectedStats.maxHealth, state.MaxHealth,
                "adding ModifierEffects wiring changed MaxHealth -- the seam disturbed an unrelated assignment");
            Assert.AreEqual(expectedStats.physicalDefense, state.PhysicalDefense,
                "adding ModifierEffects wiring changed PhysicalDefense -- the seam disturbed an unrelated assignment");
            Assert.IsNotNull(state.Talents, "Talents must stay non-null exactly as before this phase");
            Assert.IsTrue(state.Talents.IsEmpty,
                "Talents IS wired at this adapter seam (ContentDatabase.TalentEffects(character)) as of the " +
                "talent-wiring fix -- it is empty here only because this fixture Character has no " +
                "unlockedTalentIds, not because the seam is unwired");
        }

        // The tooling-only path (no Character, no save) still builds and
        // still gets the correct Empty answer, explicitly rather than by
        // accident -- see FightEncounterAdapter's own comment on that
        // overload.
        [Test]
        public void TheToolingPath_StillBuildsWithEmptyModifierEffects()
        {
            var definition = FirstCharacter();

            var built = FightEncounterAdapter.Build(
                new List<string> { definition.id }, OneEnemy(),
                new Domain.Rng.SeededRandom(11));

            Assert.IsNotNull(built);
            Assert.IsTrue(built.Party[0].ModifierEffects.IsEmpty);
        }
    }
}

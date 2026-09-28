using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.PlayModeTests
{
    // Equipped gear actually reaches the fight.
    //
    // The sibling of RelicsReachCombatTests, and the same shape of bug: the
    // mechanic was built, the mechanic was tested, and the two ends were never
    // joined. FightEncounterAdapter built every party member from their
    // CharacterDefinition -- baseStats and baseAbilityScores -- so equipment,
    // talents and upgrade levels were all inert in combat. A character in full
    // runeplate hit for exactly what a naked one did.
    //
    // ContentDatabase.Effective.cs exists entirely for this, thirteen
    // accessors deep, and combat called none of them. EffectiveStats even says
    // so in its own body: "This is the one place plus reaches combat stats."
    // It reached the character sheet and stopped there.
    //
    // Every assertion is about the WIRE, not the arithmetic. What a breastplate
    // adds is StatBlock's business and already covered; that a worn breastplate
    // is present at all when the first blow lands is what nothing checked.
    public class EquipmentReachesCombatTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-gearwire-" + System.Guid.NewGuid().ToString("N"));
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

        // An item that actually moves a combat stat, whatever the content is
        // called today. Weapons and armour both qualify; what matters is that
        // its bonus is non-zero, or the test would pass on a no-op.
        //
        // Attack only counts for a weapon: EffectiveStats zeroes a worn
        // item's attack contribution, so an Equipment-kind piece whose only
        // authored line is "attack N" (itemsets.json's leather gloves)
        // moves nothing and would make this helper pick a no-op fixture. A
        // live main-hand WEAPON still moves Attack, via WeaponPower, so it
        // still qualifies.
        private static ItemDefinition AStatItem()
        {
            return ContentDatabase.Items.FirstOrDefault(i =>
                i != null
                && i.kind != ItemKind.Consumable
                && ((i.kind == ItemKind.Weapon && i.StatBonusAt(0).attack != 0)
                    || i.StatBonusAt(0).physicalDefense != 0
                    || i.StatBonusAt(0).magicalDefense != 0
                    || i.StatBonusAt(0).maxHealth != 0));
        }

        private static FightEncounterAdapter.BuiltFight BuildFor(Character character)
        {
            return FightEncounterAdapter.Build(
                new List<string> { character.definitionId },
                OneEnemy(),
                new Domain.Rng.SeededRandom(11),
                partyCharacters: new List<Character> { character });
        }

        // The headline. Two characters, same definition, one wearing the item.
        [Test]
        public void AWornItemChangesWhatTheCombatantBringsToTheStage()
        {
            var definition = FirstCharacter();
            var item = AStatItem();
            Assert.IsNotNull(definition, "fixture: content has a character");
            Assert.IsNotNull(item, "fixture: content has a non-consumable item with a stat bonus");

            var bare = new Character(definition.id);
            var geared = new Character(definition.id);
            geared.equipment.Set(item.equipSlot, item.id);

            var bareState = BuildFor(bare).Party[0];
            var gearedState = BuildFor(geared).Party[0];

            var bonus = item.StatBonusAt(0);
            // A weapon still moves Attack (via WeaponPower); a non-weapon
            // item's attack bonus does not -- see AStatItem's own header.
            bool attackShouldMove = item.kind == ItemKind.Weapon && bonus.attack != 0;
            bool moved =
                (attackShouldMove && gearedState.Attack != bareState.Attack)
                || (bonus.physicalDefense != 0 && gearedState.PhysicalDefense != bareState.PhysicalDefense)
                || (bonus.magicalDefense != 0 && gearedState.MagicalDefense != bareState.MagicalDefense)
                || (bonus.maxHealth != 0 && gearedState.MaxHealth != bareState.MaxHealth);

            Assert.IsTrue(moved,
                "equipping " + item.id + " changed nothing on the stage -- the adapter is "
                + "building from CharacterDefinition again and equipment is inert in combat");
        }

        // A non-weapon item with a stat bonus EffectiveStats actually
        // carries. Used specifically where the assertion is that
        // EffectiveStats agrees with the stage -- a WEAPON's Attack does
        // NOT flow through EffectiveStats any more (balance redesign Phase
        // 3, D3): it becomes WeaponPower at the adapter seam instead, a
        // deliberate divergence from the sheet's bare figure that
        // AWornItemChangesWhatTheCombatantBringsToTheStage already accounts
        // for. This test's claim is only true for a non-weapon item.
        private static ItemDefinition ANonWeaponStatItem()
        {
            return ContentDatabase.Items.FirstOrDefault(i =>
                i != null
                && i.kind == ItemKind.Equipment
                && (i.StatBonusAt(0).physicalDefense != 0
                    || i.StatBonusAt(0).magicalDefense != 0
                    || i.StatBonusAt(0).maxHealth != 0));
        }

        // The stat path must agree with the sheet's. If these two ever diverge
        // the player is told one number and fights with another, which is worse
        // than either being wrong on its own.
        [Test]
        public void TheStageAgreesWithTheCharacterSheet()
        {
            var definition = FirstCharacter();
            var item = ANonWeaponStatItem();
            Assert.IsNotNull(item, "fixture: content has a non-weapon item with a stat bonus");
            var character = new Character(definition.id);
            character.equipment.Set(item.equipSlot, item.id);

            var expected = ContentDatabase.EffectiveStats(character);
            var state = BuildFor(character).Party[0];

            Assert.AreEqual(expected.attack, state.Attack,
                "the stage and the sheet disagree about attack");
            Assert.AreEqual(expected.maxHealth, state.MaxHealth,
                "the stage and the sheet disagree about max health");
        }

        // EffectiveStats already folds AbilityDerivation at the end of its own
        // body, and EffectiveMaxMana already adds MaxManaBonus. Re-applying the
        // derivation on top -- which the definition path legitimately must --
        // would silently double health and attack, and nothing else would
        // notice because both numbers would still look plausible.
        [Test]
        public void AbilityDerivationIsNotCountedTwice()
        {
            var definition = FirstCharacter();
            var character = new Character(definition.id);

            var state = BuildFor(character).Party[0];

            Assert.AreEqual(ContentDatabase.EffectiveStats(character).maxHealth, state.MaxHealth);
            Assert.AreEqual(ContentDatabase.EffectiveMaxMana(character), state.MaxMana);
        }

        // Resistances were set by neither path. A resistance rolled onto a
        // breastplate was saved, shown, folded into EffectiveStats, and then
        // dropped on the way into the one system it exists for.
        [Test]
        public void ResistancesArriveOnTheCombatant()
        {
            var definition = FirstCharacter();
            var character = new Character(definition.id);
            var expected = ContentDatabase.EffectiveStats(character);

            var state = BuildFor(character).Party[0];

            Assert.AreEqual(expected.physicalDefense, state.PhysicalDefense);
            Assert.AreEqual(expected.magicalDefense, state.MagicalDefense);
        }

        // EffectiveStats' single clamp point holds two floors on top of the
        // general one: max health and speed must never reach 0, because a
        // 0-max-health combatant is dead before the fight starts and 0
        // Speed can never take a turn. This is a realistic path to it, not
        // a synthetic one: a heavily negative
        // Constitution/Dexterity investment (still floored at 0 by
        // AbilityScoreBlock's own clamp, same as any other ability score) can
        // cancel out a low-HP/low-speed character's base figures exactly, and
        // without these two floors the result would be a StatBlock nothing
        // downstream can safely act on.
        [Test]
        public void MaxHealthAndSpeedNeverReachZero_EvenOffAWorstCaseInvestment()
        {
            var definition = FirstCharacter();
            var character = new Character(definition.id);
            character.investedAbilityScores.constitution = -999;
            character.investedAbilityScores.dexterity = -999;

            var stats = ContentDatabase.EffectiveStats(character);

            Assert.GreaterOrEqual(stats.maxHealth, 1, "a combatant must never enter a fight already dead");
            Assert.GreaterOrEqual(stats.speed, 1, "a combatant at 0 Speed could never take a turn");
        }

        // ---- the spell ladder ----------------------------------------------

        // KitFor filters SpellTiers by the character's level rather than
        // taking OrderByDescending(level).First() outright, so a level 1
        // character cannot cast the endgame tier. The ladder
        // (SpellTierDefinition, TierAtLevel) scales SkillPowerMultiplier
        // for every fixed-damage skill; it does not also name a free
        // "Skill" action (docs/PLAN_SHOP.md Gate 4).
        [Test]
        public void TheSpellTierNeverOutrunsTheCharactersLevel()
        {
            var definition = FirstCharacter();
            var tiers = ContentDatabase.SpellTiers.Where(t => t != null).OrderBy(t => t.Data.Level).ToList();
            Assert.GreaterOrEqual(tiers.Count, 2, "this test needs two spell tiers to compare; content has " + tiers.Count);

            var character = new Character(definition.id) { level = 1 };
            var built = BuildFor(character);
            var kit = built.Session.KitFor(built.Party[0]);

            Assert.AreEqual(tiers[0].Data.PowerMultiplier, kit.SkillPowerMultiplier,
                "a level 1 character was handed a higher spell tier's multiplier than they have reached");
        }

        [Test]
        public void LevellingUpRaisesTheSpellTier()
        {
            var definition = FirstCharacter();
            var tiers = ContentDatabase.SpellTiers
                .Where(t => t != null)
                .OrderBy(t => t.Data.Level)
                .ToList();

            // ASSERTED, not skipped. Content ships nine spell tiers, so this
            // never fires -- and if it ever did, a suite that quietly stopped
            // covering levelling is worse than a red build saying why.
            Assert.GreaterOrEqual(tiers.Count, 2,
                "this test needs two spell tiers to compare; content has " + tiers.Count);

            var low = new Character(definition.id) { level = tiers[0].Data.Level };
            var high = new Character(definition.id) { level = tiers[tiers.Count - 1].Data.Level };

            var lowBuilt = BuildFor(low);
            var highBuilt = BuildFor(high);

            float lowMultiplier = lowBuilt.Session.KitFor(lowBuilt.Party[0]).SkillPowerMultiplier;
            float highMultiplier = highBuilt.Session.KitFor(highBuilt.Party[0]).SkillPowerMultiplier;

            Assert.Greater(highMultiplier, lowMultiplier,
                "the spell ladder does not respond to level at all");
        }

        // The character's own level has to ride the kit too: PlayerKit takes a
        // level parameter that defaulted to 1 and was never passed.
        [Test]
        public void TheKitCarriesTheCharactersRealLevel()
        {
            var definition = FirstCharacter();
            var character = new Character(definition.id) { level = 4 };

            var built = BuildFor(character);

            Assert.AreEqual(4, built.Session.KitFor(built.Party[0]).Level);
        }

        // ---- the tooling path stays put -------------------------------------

        // Opening the Fight scene with no save still has to produce a stage, or
        // screenshot comparison has nothing to compare.
        [Test]
        public void WithNoCharactersSuppliedTheDefinitionPathStillBuilds()
        {
            var definition = FirstCharacter();

            var built = FightEncounterAdapter.Build(
                new List<string> { definition.id }, OneEnemy(),
                new Domain.Rng.SeededRandom(11));

            Assert.IsNotNull(built);
            Assert.AreEqual(1, built.Party.Count);
        }
    }
}

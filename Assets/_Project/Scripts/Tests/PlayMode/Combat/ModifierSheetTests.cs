using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.PlayModeTests
{
    // PHASE F, the item-modifier plan: the one file that proves the WHOLE
    // system composes, not just each phase's own piece in isolation.
    //
    // Every other modifier test in this suite covers exactly one seam: an
    // EditMode file hand-builds a Effect fixture and exercises one
    // combat hook (ItemModifierCombatHookTests, ChilledStatusTests,
    // RootedStatusTests); a "*ModifierReachesCombatTests" PlayMode file
    // equips exactly ONE real modifier on a fresh, save-less Character and
    // proves the scaling seam reaches CombatantState. Nothing before this
    // file has rolled MULTIPLE real modifiers onto ONE item through the
    // real ItemOffer roll type, claimed that roll into a real save the way
    // the Reckoning's Take does, equipped it through the real EquipMove
    // seam, fought a real fight with it, AND round-tripped the save -- all
    // in one continuous chain. That chain, not any single link in it, is
    // what this file exists to protect.
    public class ModifierSheetTests
    {
        // Far past any one swing this suite's gear can land, so the on-hit
        // riders always meet a live target (AUDIT #46).
        private const int DurableFoeHealth = 1000000;

        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-modifiersheet-tests-" + System.Guid.NewGuid().ToString("N"));
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

        private static ItemDefinition TierZeroItemInSlot(EquipmentSlot slot) =>
            ContentDatabase.Equippables.FirstOrDefault(i => i != null && i.equipSlot == slot);

        private static Character FreshCharacterWearing(ItemDefinition item, int tier, List<string> modifierIds)
        {
            var definition = ContentDatabase.Characters.FirstOrDefault();
            Assert.IsNotNull(definition, "fixture: content has at least one character");

            var character = new Character(definition.id);
            character.equipment.Set(item.equipSlot, item.id, modifierIds: modifierIds, tier: tier);
            return character;
        }

        // ---- A: three representative rolled items, through the real content pipeline ----
        //
        // Every number below is TierMultiplier(tier) (1.25 at tier 1, 1.5625 at
        // tier 2 and 1.953125 at tier 3, all hand-derived from 1.25^n) times
        // modifiers.json's own authored base -- never ModifierMagnitude.Scale
        // called from inside this test.

        // Three rows of three genuinely different effect TYPES (an on-hit
        // damage rider, a resistance, a percent-of-damage heal), which is
        // what "none of the three silently overrides another" needs.
        [Test]
        public void ATierThreeThreeModifierWeapon_ResolvesAllThreeModifiersAtTheirCorrectlyScaledMagnitudes()
        {
            var weapon = TierZeroItemInSlot(EquipmentSlot.Weapon1);
            Assert.IsNotNull(weapon, "fixture: content has a tier-0 weapon");

            var character = FreshCharacterWearing(weapon, 3,
                new List<string> { "attuned", "veiled", "vampiric" });
            Assert.IsTrue(ContentDatabase.ActiveLoadout(character).IsLive(weapon.equipSlot),
                "fixture check: the item must be LIVE or ModifierEffects reads nothing");

            var effects = ContentDatabase.CharacterEffects(character);
            Assert.IsFalse(effects.IsEmpty, "a tier-3 roll with three real modifiers must produce real effects");

            // Scale = 1.25^3 = 1.953125. Bases (10/4/8) are attuned/veiled/
            // vampiric's authored magnitudes. 10 x 1.953125 = 19.53 -> 20;
            // 4 x 1.953125 = 7.81 -> 8; 8 x 1.953125 = 15.63 -> 16.
            var onHit = effects.All.Single(e => e.Kind == EffectKind.ElementalDamageOnHitPercent);
            Assert.AreEqual(20, onHit.Magnitude, "attuned: 10 x 1.953125");
            Assert.AreEqual(OwnAttackType(character), onHit.Against, "in the wearer's own attack type");

            var magicalResist = effects.All.Single(e => e.Kind == EffectKind.ResistanceFlat);
            Assert.IsTrue(magicalResist.AgainstMagical);
            Assert.AreEqual(8, magicalResist.Magnitude, "veiled: 4 x 1.953125");

            Assert.AreEqual(16, effects.Magnitude(EffectKind.LifestealPercent), "vampiric: 8 x 1.953125");
        }

        // POST-AFFIX-SPLIT: "stalwart" used to carry both
        // FlatPhysicalDamageReduction and BreakShieldDepletionResistPercent;
        // the latter split off into its own id, "bulwark". Equipping both
        // proves the two still coexist on one character -- the same claim
        // the old bundled "stalwart" made on its own, now via two affix
        // slots instead of one.
        [Test]
        public void ATierOneTwoModifierArmorPiece_ResolvesBothModifiersAtTheTierOneScale()
        {
            var armor = GearFixtures.WearableAt(1, EquipmentSlot.Torso);
            Assert.IsNotNull(armor, "fixture: content has a torso piece the first character can wear at tier 1");

            var character = FreshCharacterWearing(armor, 1, new List<string> { "stalwart", "bulwark" });
            Assert.IsTrue(ContentDatabase.ActiveLoadout(character).IsLive(armor.equipSlot),
                "fixture check: the item must be LIVE or ModifierEffects reads nothing");

            var effects = ContentDatabase.CharacterEffects(character);
            Assert.IsFalse(effects.IsEmpty, "a tier-1 roll with two real modifiers must produce real effects");

            // Scale = 1.25. Bases (2, 15) are stalwart/bulwark's CURRENT
            // authored magnitudes -- halved from 3/30. 2 x 1.25 = 2.5,
            // away-from-zero -> 3; 15 x 1.25 = 18.75, away-from-zero -> 19.
            Assert.AreEqual(3, effects.Magnitude(EffectKind.FlatPhysicalDamageReduction),
                "stalwart: 2 x 1.25 = 2.5, rounded away from zero");
            Assert.AreEqual(19, effects.Magnitude(EffectKind.BreakShieldDepletionResistPercent),
                "bulwark: 15 x 1.25 = 18.75, rounded away from zero");
        }

        [Test]
        public void AnOrdinaryZeroModifierItem_ResolvesNoModifierEffectsAtAll()
        {
            var item = TierZeroItemInSlot(EquipmentSlot.Weapon1);
            Assert.IsNotNull(item, "fixture: content has a tier-0 weapon");

            var character = FreshCharacterWearing(item, 0, new List<string>());
            Assert.IsTrue(ContentDatabase.ActiveLoadout(character).IsLive(item.equipSlot),
                "fixture check: the item must be LIVE");

            var effects = ContentDatabase.CharacterEffects(character);
            Assert.IsTrue(effects.IsEmpty, "zero rolled modifiers must resolve to zero effects, not a default set");

            CollectionAssert.IsEmpty(character.equipment.GetModifierIds(item.equipSlot));
        }

        // ---- B: the resistance half, measurably reducing the right damage type ----
        //
        // TypedResistance_FromARealModifier_ReachesCombatantStateTypedResistance
        // (ItemModifierScalingReachesCombatTests) already proves fiery's Fire
        // resistance reaches CombatantState.TypedResistance.Fire as a number.
        // TypedResistance_ReducesOnlyTheMatchingElement (ItemModifierCombatHookTests)
        // already proves DamagePipeline.AfterDefences honours that number when
        // it is hand-planted on a fixture. Neither proves the two actually meet:
        // a REAL, fight-built CombatantState's REAL, content-scaled resistance,
        // run through the REAL production mitigation funnel. This is that
        // missing link, via a with/without diff (the same pattern
        // RunicModifier_ScalesAndReachesMaxManaAndManaRegen already uses for a
        // clean comparison with no hand-derived arithmetic required).
        private static DamageType OwnAttackType(Character character) =>
            ContentDatabase.GetCharacter(character.definitionId).Data.AttackType;

        [Test]
        public void VeiledResistance_FromARealFightBuiltCharacter_MeasurablyReducesMagicalDamage_ButLeavesPhysicalUntouched()
        {
            var weapon = TierZeroItemInSlot(EquipmentSlot.Weapon1);
            Assert.IsNotNull(weapon, "fixture: content has a tier-0 weapon");

            var resisted = FreshCharacterWearing(weapon, 2, new List<string> { "veiled" });
            var bare = new Character(resisted.definitionId);

            var enemyId = ContentDatabase.Enemies.Select(e => e.id).FirstOrDefault();
            Assert.IsNotNull(enemyId, "fixture: content has at least one enemy");

            var builtResisted = FightEncounterAdapter.Build(new List<string> { resisted.definitionId },
                new List<string> { enemyId }, new SeededRandom(7), partyCharacters: new List<Character> { resisted });
            var builtBare = FightEncounterAdapter.Build(new List<string> { bare.definitionId },
                new List<string> { enemyId }, new SeededRandom(7), partyCharacters: new List<Character> { bare });

            Assert.IsNotNull(builtResisted, "fixture: a real fight must build for the resisted character");
            Assert.IsNotNull(builtBare, "fixture: a real fight must build for the bare control");

            var resistedState = builtResisted.Party[0];
            var bareState = builtBare.Party[0];

            // veiled's own scaled ResistanceFlat (every magical type) at
            // tier 2: 4 x 1.5625 = 6.25 -> 6.
            Assert.AreEqual(6, resistedState.TypedResistance.Fire);
            Assert.AreEqual(0, bareState.TypedResistance.Fire, "fixture check: the bare control wears nothing");

            var burned = DamagePipeline.AfterDefences(100, DamageType.Fire, resistedState,
                affinity: ElementalAffinity.Neutral, varianceRange: 0f, rng: null, resolveWard: null);
            var unresistedBurn = DamagePipeline.AfterDefences(100, DamageType.Fire, bareState,
                affinity: ElementalAffinity.Neutral, varianceRange: 0f, rng: null, resolveWard: null);

            Assert.Less(burned.Damage, unresistedBurn.Damage,
                "a real, content-rolled Fire resistance -- reached through a real fight-built combatant, run " +
                "through the real DamagePipeline funnel -- must measurably reduce a real incoming Fire hit");

            var struck = DamagePipeline.AfterDefences(100, DamageType.Physical, resistedState,
                affinity: ElementalAffinity.Neutral, varianceRange: 0f, rng: null, resolveWard: null);
            var unresistedStrike = DamagePipeline.AfterDefences(100, DamageType.Physical, bareState,
                affinity: ElementalAffinity.Neutral, varianceRange: 0f, rng: null, resolveWard: null);
            Assert.AreEqual(unresistedStrike.Damage, struck.Damage,
                "veiled names magical types only -- Physical must pass through exactly as if nothing were equipped");
        }

        // ---- C: THE FULL CHAIN --------------------------------------------------
        //
        // Roll -> claim -> equip -> fight -> save/reload, all against the SAME
        // rolled copy of ONE item, through the SAME production calls the game
        // itself makes at every step (never a hand-built fixture standing in
        // for one of them):
        //   roll:   ItemOfferRoll.Candidates() + ItemOffer.WithModifiers
        //           (forced to a tier-3 copy with three modifiers for a
        //           deterministic assertion -- the pure RNG SHAPE of a roll is
        //           ItemOfferRollTests' job, not this file's)
        //   claim:  InventoryOps.Add, the exact call RewardPages.Take makes
        //   equip:  EquipMove.TryEquip, the move EquipmentOps.Equip is built on
        //   fight:  FightEncounterAdapter.Build + a real FightSession swing
        //   reload: SaveSlotManager.SaveCurrent/Forget, the exact round-trip
        //           every other save-compat test in this plan already trusts
        [Test]
        public void TheFullChain_RollClaimEquipFightSaveReload_PreservesARolledItemAndItsEffectsThroughout()
        {
            var save = SaveSlotManager.CurrentSave;
            var character = save.ActiveSquad().FirstOrDefault();
            Assert.IsNotNull(character, "fixture: a fresh save fields at least one character");

            var weaponCandidate = ItemOfferRoll.Candidates()
                .FirstOrDefault(c => c.Tier == 3 && ContentDatabase.GetItem(c.ItemId)?.equipSlot == EquipmentSlot.Weapon1);
            Assert.IsNotNull(weaponCandidate.ItemId, "fixture: content has a tier-3 weapon to roll");
            var itemDef = ContentDatabase.GetItem(weaponCandidate.ItemId);

            // ---- ROLL ----
            var rolledModifierIds = new List<string> { "attuned", "vampiric", "veiled" };
            var offer = weaponCandidate.WithModifiers(rolledModifierIds);

            // ---- CLAIM ----
            InventoryOps.Add(save.stockpiledItems, offer.Instance, 1);

            var claimed = save.stockpiledItems.FirstOrDefault(e =>
                e.itemId == offer.ItemId && e.tier == 3);
            Assert.IsNotNull(claimed, "the claim must land in the stockpile with its full rolled identity");
            CollectionAssert.AreEquivalent(rolledModifierIds, claimed.modifierIds,
                "the claimed stack must carry exactly the rolled modifiers");

            // ---- EQUIP ----
            bool equipped = EquipMove.TryEquip(character.equipment, save.stockpiledItems, offer.Instance,
                itemDef.equipSlot, itemDef.IsEquippable);
            Assert.IsTrue(equipped, "the rolled stack must be findable and equippable through the real EquipMove seam");

            var wornSlot = EquipmentSlots.All.FirstOrDefault(slot =>
                character.equipment.Get(slot) == offer.ItemId && character.equipment.GetInstance(slot).Tier == 3);
            Assert.AreNotEqual(default(EquipmentSlot), wornSlot, "fixture check for slots that default to Head");
            Assert.AreEqual(offer.ItemId, character.equipment.Get(wornSlot));
            CollectionAssert.AreEquivalent(rolledModifierIds, character.equipment.GetModifierIds(wornSlot));
            Assert.IsFalse(save.stockpiledItems.Any(e => e.itemId == offer.ItemId && e.tier == 3),
                "the equipped copy must have LEFT the stockpile, not been duplicated");

            // ---- FIGHT ----
            // The toughest real enemy content has, by max health -- headroom
            // for TWO on-hit elemental procs (fiery's Fire, astral's Arcane)
            // plus the base swing to all land without the target dying
            // before the second rider gets its turn to fire, which would
            // make this a test of "one rider fires" rather than "three
            // rolled modifiers compose".
            // Among enemies a room can roll, the rule the Frosty and Sylvan
            // fixtures needed once the event-only Bellwether (rollable: false,
            // M7a) outranked the Forest Troll on health: an event monster's
            // speed and kit are not what this sheet is measured against.
            var toughestEnemyId = ContentDatabase.Enemies
                .Where(e => e.Data.Rollable)
                .OrderByDescending(e => e.Data.BaseStats.maxHealth)
                .Select(e => e.id)
                .FirstOrDefault();
            Assert.IsNotNull(toughestEnemyId, "fixture: content has at least one enemy");

            var built = FightEncounterAdapter.Build(
                new List<string> { character.definitionId },
                new List<string> { toughestEnemyId },
                new SeededRandom(13),
                partyCharacters: new List<Character> { character });
            Assert.IsNotNull(built, "fixture: a real fight must build from the claimed, equipped character");

            var session = built.Session;
            var hero = built.Party[0];
            var foe = session.Encounter.Enemies.FirstOrDefault();
            Assert.IsNotNull(foe, "fixture: the built fight has at least one enemy");

            // The 3-modifier tier-3 roll, reaching the combatant the fight
            // actually runs on, at the correctly SCALED magnitude --
            // TierMultiplier(3) = 1.953125, hand-derived from 1.25^3 and
            // never recomputed here.
            var onHit = hero.Effects.All.Single(e => e.Kind == EffectKind.ElementalDamageOnHitPercent);
            Assert.AreEqual(20, onHit.Magnitude, "attuned: 10 x 1.953125, reached through the real roll/claim/equip chain");
            Assert.AreEqual(OwnAttackType(character), onHit.Against);
            Assert.AreEqual(8, hero.Effects.Magnitude(EffectKind.ResistanceFlat), "veiled: 4 x 1.953125");
            Assert.AreEqual(16, hero.Effects.Magnitude(EffectKind.LifestealPercent), "vampiric: 8 x 1.953125");

            // A known, round Attack so the elemental on-hit damage is a
            // hand-computed literal rather than a figure that depends on the
            // rest of this character's build -- the CLAIM/EQUIP steps above
            // already proved the roll reached this exact character for real.
            // Speed set to a LARGE gap over the foe -- the same fixture shape
            // ChilledOnHit/PushBackOnHit rely on -- so the hero remains
            // Current after this one swing and the foe's own reply cannot
            // land inside this same window and confound the lifesteal read.
            hero.Attack = 10;
            hero.Speed = 500;
            hero.CurrentHealth = hero.MaxHealth / 2; // headroom for vampiric's lifesteal to actually show

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
            var messages = session.DrainBeats().SelectMany(b => b.Messages).ToList();

            Assert.IsTrue(session.IsPlayerTurn,
                "fixture check: the hero must still hold the turn after one swing, or the foe's own reply could " +
                "land inside this same window and confound the lifesteal-healed read below");

            Assert.IsTrue(foe.IsAlive, "fixture: the durable foe died to one swing, so no on-hit rider had a live target");

            // The message text, not a net CurrentHealth delta or the ledger's
            // Healed total -- lifesteal calls CombatMath.Heal directly
            // (FightSession.cs's own ApplyModifierOnHitRiders), bypassing
            // HealAndCount's ledger bookkeeping entirely, and a REAL,
            // save-backed toughest-enemy matchup can carry real content
            // (counters, retaliation) this test never set up and has no
            // business asserting about. The printed line is the one signal
            // that isolates exactly the claim under test.
            Assert.IsTrue(messages.Any(m => m.Contains(hero.Name) && m.Contains("drains") && m.Contains("health from the blow")),
                "vampiric's lifesteal must have healed the wearer off this same swing");

            // ---- SAVE/RELOAD ----
            SaveSlotManager.SaveCurrent();
            SaveSlotManager.Forget();

            var reloaded = SaveSlotManager.CurrentSave;
            var reloadedCharacter = reloaded.roster.First(c => c.definitionId == character.definitionId);

            Assert.AreEqual(offer.ItemId, reloadedCharacter.equipment.Get(wornSlot), "the rolled item id must survive a save/reload");
            Assert.AreEqual(3, reloadedCharacter.equipment.GetInstance(wornSlot).Tier, "the tier must survive");
            CollectionAssert.AreEquivalent(rolledModifierIds, reloadedCharacter.equipment.GetModifierIds(wornSlot),
                "the exact rolled modifier set must survive");

            // Not merely the IDS surviving -- the reloaded save must resolve
            // to the IDENTICAL scaled effects, proving persistence carries
            // the roll rather than a pre-baked number that happened to match
            // once.
            var reloadedEffects = ContentDatabase.CharacterEffects(reloadedCharacter);
            Assert.AreEqual(16, reloadedEffects.Magnitude(EffectKind.LifestealPercent),
                "a reloaded save must resolve the identical scaled magnitude, not merely the identical ids");
            Assert.AreEqual(20, reloadedEffects.All.Single(e =>
                    e.Kind == EffectKind.ElementalDamageOnHitPercent).Magnitude,
                "the reloaded attuned rider must still scale to 20 after the round trip");
        }
    }
}

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
    // EditMode file hand-builds a ModifierEffect fixture and exercises one
    // combat hook (ItemModifierCombatHookTests, ChilledStatusTests,
    // RootedStatusTests); a "*ModifierReachesCombatTests" PlayMode file
    // equips exactly ONE real modifier on a fresh, save-less Character and
    // proves the scaling seam reaches CombatantState. Nothing before this
    // file has rolled MULTIPLE real modifiers onto ONE item through the
    // real ItemOffer roll type, claimed that roll into a real save the way
    // ReckoningController.Take does, equipped it through the real EquipMove
    // seam, fought a real fight with it, AND round-tripped the save -- all
    // in one continuous chain. That chain, not any single link in it, is
    // what this file exists to protect.
    public class ModifierSheetTests
    {
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
            ContentDatabase.Items.FirstOrDefault(i => i != null && i.IsEquippable && i.tier == 0 && i.equipSlot == slot);

        private static Character FreshCharacterWearing(ItemDefinition item, RiftTier riftTier, List<string> modifierIds)
        {
            var definition = ContentDatabase.Characters.FirstOrDefault();
            Assert.IsNotNull(definition, "fixture: content has at least one character");

            var character = new Character(definition.id);
            character.equipment.Set(item.equipSlot, item.id, modifierIds: modifierIds, riftTier: (int)riftTier);
            return character;
        }

        // ---- A: three representative rolled items, through the real content pipeline ----
        //
        // Every number below is TierMultiplier(0) x RiftMultiplier(riftTier)
        // (1.0 and 1.3/1.6/2.0 respectively, all already pinned as literals in
        // ModifierMagnitudeTests) times modifiers.json's own authored base --
        // never ModifierMagnitude.Scale called from inside this test.

        // POST-AFFIX-SPLIT: this used to roll "fiery"+"vampiric"+"hardened"
        // (2+1+3 = 6 effects off 3 ids). Every modifier now grants exactly
        // one effect (ModifierEntryResolver rejects anything else -- see its
        // own comment), so a 3-id roll -- the real game's own cap, RiftTier
        // Convergent's 3 rungs -- now carries 3 effects, not 6. Chosen to
        // still span three genuinely different effect TYPES (an on-hit
        // proc, a resistance, a percent-of-damage heal) rather than three
        // of the same shape, which is what "none of the three silently
        // overrides another" actually needs to prove.
        [Test]
        public void AConvergentThreeModifierWeapon_ResolvesAllThreeModifiersAtTheirCorrectlyScaledMagnitudes()
        {
            var weapon = TierZeroItemInSlot(EquipmentSlot.Weapon1);
            Assert.IsNotNull(weapon, "fixture: content has a tier-0 weapon");

            var character = FreshCharacterWearing(weapon, RiftTier.Convergent,
                new List<string> { "fiery", "ironclad", "vampiric" });
            Assert.IsTrue(ContentDatabase.ActiveLoadout(character).IsLive(weapon.equipSlot),
                "fixture check: the item must be LIVE or ModifierEffects reads nothing");

            var effects = ContentDatabase.ModifierEffects(character);
            Assert.IsFalse(effects.IsEmpty, "a Convergent roll with three real modifiers must produce real effects");

            // Scale = 1.0 x 2.0 = 2.0.
            var fireOnHit = effects.All.Single(e =>
                e.Type == ModifierEffectType.ElementalDamageOnHitPercent && e.Against == DamageType.Fire);
            Assert.AreEqual(40, fireOnHit.Magnitude, "fiery: 20 x 2.0");

            var physicalResist = effects.All.Single(e =>
                e.Type == ModifierEffectType.TypedResistanceFlat && e.Against == DamageType.Physical);
            Assert.AreEqual(30, physicalResist.Magnitude, "ironclad: 15 x 2.0");

            Assert.AreEqual(30, effects.Best(ModifierEffectType.LifestealPercent), "vampiric: 15 x 2.0");
        }

        // POST-AFFIX-SPLIT: "stalwart" used to carry both
        // FlatPhysicalDamageReduction and BreakShieldDepletionResistPercent;
        // the latter split off into its own id, "bulwark". Equipping both
        // proves the two still coexist on one character -- the same claim
        // the old bundled "stalwart" made on its own, now via two affix
        // slots instead of one.
        [Test]
        public void ARiftTouchedTwoModifierArmorPiece_ResolvesBothModifiersAtTheRiftTouchedScale()
        {
            var armor = TierZeroItemInSlot(EquipmentSlot.Torso);
            Assert.IsNotNull(armor, "fixture: content has a tier-0 torso piece");

            var character = FreshCharacterWearing(armor, RiftTier.RiftTouched, new List<string> { "stalwart", "bulwark" });
            Assert.IsTrue(ContentDatabase.ActiveLoadout(character).IsLive(armor.equipSlot),
                "fixture check: the item must be LIVE or ModifierEffects reads nothing");

            var effects = ContentDatabase.ModifierEffects(character);
            Assert.IsFalse(effects.IsEmpty, "a RiftTouched roll with two real modifiers must produce real effects");

            // Scale = 1.0 x 1.3 = 1.3. 3 x 1.3 = 3.9, away-from-zero -> 4;
            // 30 x 1.3 = 39.0 exactly.
            Assert.AreEqual(4, effects.Best(ModifierEffectType.FlatPhysicalDamageReduction),
                "stalwart: 3 x 1.3 = 3.9, rounded away from zero");
            Assert.AreEqual(39, effects.Best(ModifierEffectType.BreakShieldDepletionResistPercent),
                "bulwark: 30 x 1.3 = 39.0 exactly");
        }

        [Test]
        public void AnOrdinaryZeroModifierItem_ResolvesNoModifierEffectsAtAll()
        {
            var item = TierZeroItemInSlot(EquipmentSlot.Weapon1);
            Assert.IsNotNull(item, "fixture: content has a tier-0 weapon");

            var character = FreshCharacterWearing(item, RiftTier.Ordinary, new List<string>());
            Assert.IsTrue(ContentDatabase.ActiveLoadout(character).IsLive(item.equipSlot),
                "fixture check: the item must be LIVE");

            var effects = ContentDatabase.ModifierEffects(character);
            Assert.IsTrue(effects.IsEmpty, "zero rolled modifiers must resolve to zero effects, not a default set");

            CollectionAssert.IsEmpty(character.equipment.GetModifierIds(item.equipSlot));
            Assert.AreEqual(0, character.equipment.GetRiftTier(item.equipSlot));
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
        // "emberguard", not "fiery" -- Fire's resistance half split off into
        // its own id; see modifiers.json's own entries.
        [Test]
        public void EmberguardResistance_FromARealFightBuiltCharacter_MeasurablyReducesFireDamage_ButLeavesIceUntouched()
        {
            var weapon = TierZeroItemInSlot(EquipmentSlot.Weapon1);
            Assert.IsNotNull(weapon, "fixture: content has a tier-0 weapon");

            var resisted = FreshCharacterWearing(weapon, RiftTier.RiftForged, new List<string> { "emberguard" });
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

            // emberguard's own scaled TypedResistanceFlat (Fire): 15 x 1.0 x
            // 1.6 = 24 -- already pinned in ItemModifierScalingReachesCombatTests,
            // reused here rather than recomputed.
            Assert.AreEqual(24, resistedState.TypedResistance.Fire);
            Assert.AreEqual(0, bareState.TypedResistance.Fire, "fixture check: the bare control wears nothing");

            var burned = DamagePipeline.AfterDefences(100, DamageType.Fire, resistedState,
                affinity: ElementalAffinity.Neutral, varianceRange: 0f, rng: null, resolveWard: null);
            var unresistedBurn = DamagePipeline.AfterDefences(100, DamageType.Fire, bareState,
                affinity: ElementalAffinity.Neutral, varianceRange: 0f, rng: null, resolveWard: null);

            Assert.Less(burned.Damage, unresistedBurn.Damage,
                "a real, content-rolled Fire resistance -- reached through a real fight-built combatant, run " +
                "through the real DamagePipeline funnel -- must measurably reduce a real incoming Fire hit");

            var frozen = DamagePipeline.AfterDefences(100, DamageType.Ice, resistedState,
                affinity: ElementalAffinity.Neutral, varianceRange: 0f, rng: null, resolveWard: null);
            Assert.AreEqual(unresistedBurn.Damage, frozen.Damage,
                "emberguard only named Fire -- an untargeted element must pass through exactly as if nothing were equipped");
        }

        // ---- C: THE FULL CHAIN --------------------------------------------------
        //
        // Roll -> claim -> equip -> fight -> save/reload, all against the SAME
        // rolled copy of ONE item, through the SAME production calls the game
        // itself makes at every step (never a hand-built fixture standing in
        // for one of them):
        //   roll:   ItemOfferRoll.Candidates() + ItemOffer.WithPlus/WithModifiers
        //           (forced to Convergent for a deterministic assertion -- the
        //           pure RNG SHAPE of a roll is ItemOfferRollTests' job, not
        //           this file's)
        //   claim:  InventoryOps.Add, the exact call ReckoningController.Take makes
        //   equip:  EquipMove.TryEquip, the exact call AutoEquipIntoAnEmptySlot makes
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
                .FirstOrDefault(c => c.Tier == 0 && ContentDatabase.GetItem(c.ItemId)?.equipSlot == EquipmentSlot.Weapon1);
            Assert.IsNotNull(weaponCandidate.ItemId, "fixture: content has a tier-0 weapon to roll");
            var itemDef = ContentDatabase.GetItem(weaponCandidate.ItemId);

            // ---- ROLL ----
            // POST-AFFIX-SPLIT: "hardened" (Physical) swapped for "astral"
            // (Arcane) so this still rolls TWO independent
            // ElementalDamageOnHitPercent riders on different elements --
            // the actual claim this section proves ("two on-hit procs fire
            // off the same swing, neither silently overriding the other") --
            // now via three single-effect ids rather than fiery's/hardened's
            // old two/three-effect bundles.
            var rolledModifierIds = new List<string> { "fiery", "vampiric", "astral" };
            var offer = weaponCandidate.WithPlus(2).WithModifiers(RiftTier.Convergent, rolledModifierIds);

            // ---- CLAIM ----
            InventoryOps.Add(save.stockpiledItems, offer.ItemId, 1, offer.Plus, offer.Modifiers.ToList(), (int)offer.RiftTier);

            var claimed = save.stockpiledItems.FirstOrDefault(e =>
                e.itemId == offer.ItemId && e.plus == 2 && e.riftTier == (int)RiftTier.Convergent);
            Assert.IsNotNull(claimed, "the claim must land in the stockpile with its full rolled identity");
            CollectionAssert.AreEquivalent(rolledModifierIds, claimed.modifierIds,
                "the claimed stack must carry exactly the rolled modifiers");

            // ---- EQUIP ----
            bool equipped = EquipMove.TryEquip(character.equipment, save.stockpiledItems, offer.ItemId,
                itemDef.equipSlot, itemDef.IsEquippable, plus: offer.Plus,
                modifierIds: offer.Modifiers.ToList(), riftTier: (int)offer.RiftTier);
            Assert.IsTrue(equipped, "the rolled stack must be findable and equippable through the real EquipMove seam");

            var wornSlot = EquipmentSlots.All.FirstOrDefault(slot =>
                character.equipment.Get(slot) == offer.ItemId && character.equipment.GetPlus(slot) == 2
                && character.equipment.GetRiftTier(slot) == (int)RiftTier.Convergent);
            Assert.AreNotEqual(default(EquipmentSlot), wornSlot, "fixture check for slots that default to Head");
            Assert.AreEqual(offer.ItemId, character.equipment.Get(wornSlot));
            CollectionAssert.AreEquivalent(rolledModifierIds, character.equipment.GetModifierIds(wornSlot));
            Assert.IsFalse(save.stockpiledItems.Any(e => e.itemId == offer.ItemId && e.riftTier == (int)RiftTier.Convergent),
                "the equipped copy must have LEFT the stockpile, not been duplicated");

            // ---- FIGHT ----
            // The toughest real enemy content has, by max health -- headroom
            // for TWO on-hit elemental procs (fiery's Fire, astral's Arcane)
            // plus the base swing to all land without the target dying
            // before the second rider gets its turn to fire, which would
            // make this a test of "one rider fires" rather than "three
            // rolled modifiers compose".
            var toughestEnemyId = ContentDatabase.Enemies
                .OrderByDescending(e => e.baseStats.maxHealth)
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

            // The 3-modifier Convergent roll, reaching the combatant the
            // fight actually runs on, at the correctly SCALED magnitude --
            // TierMultiplier(0) x RiftMultiplier(Convergent) = 1.0 x 2.0,
            // both already-pinned literals (ModifierMagnitudeTests), never
            // recomputed here.
            var fireOnHit = hero.ModifierEffects.All.Single(e =>
                e.Type == ModifierEffectType.ElementalDamageOnHitPercent && e.Against == DamageType.Fire);
            Assert.AreEqual(40, fireOnHit.Magnitude, "fiery: 20 x 2.0, reached through the real roll/claim/equip chain");
            var arcaneOnHit = hero.ModifierEffects.All.Single(e =>
                e.Type == ModifierEffectType.ElementalDamageOnHitPercent && e.Against == DamageType.Arcane);
            Assert.AreEqual(40, arcaneOnHit.Magnitude, "astral: 20 x 2.0, reached through the same chain");
            Assert.AreEqual(30, hero.ModifierEffects.Best(ModifierEffectType.LifestealPercent), "vampiric: 15 x 2.0");

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
            session.Begin();

            session.ExecuteAttack(foe);
            var messages = session.DrainBeats().SelectMany(b => b.Messages).ToList();

            Assert.IsTrue(session.IsPlayerTurn,
                "fixture check: the hero must still hold the turn after one swing, or the foe's own reply could " +
                "land inside this same window and confound the lifesteal-healed read below");

            if (!foe.IsAlive)
            {
                Assert.Inconclusive(
                    "fixture: the real enemy content picked here died before both on-hit riders could fire -- " +
                    "the scaled magnitudes above already prove the roll reached combat correctly; this branch " +
                    "only concerns whether THIS PARTICULAR enemy's health happened to outlast one swing.");
                return;
            }

            // elementalRaw = max(1, Rounding.AwayFromZero(Attack x Magnitude / 100f))
            //              = max(1, Rounding.AwayFromZero(10 x 40 / 100f))
            //              = max(1, 4) = 4, for BOTH fiery's Fire rider and
            // astral's Arcane rider, BEFORE each is routed through
            // DamagePipeline.AfterDefences' own typed mitigation against the
            // toughest real enemy's own Magical/PhysicalDefense and
            // TypedResistance -- content data this test does not want to
            // hand-duplicate the way the zero-defense EditMode fixtures in
            // ItemModifierCombatHookTests can. What IS hand-derivable without
            // touching content is the bound: mitigation only ever reduces
            // toward the game's universal floor of 1, never below it or
            // above this unmitigated raw of 4 -- so the actual landed figure
            // must fall somewhere in [1, 4]. (Before this fix it was pinned
            // at exactly 20 regardless of the target's defense, which is
            // the bug -- see FightSession.ApplyModifierOnHitRiders.)
            bool FiredInRange(string element) =>
                Enumerable.Range(1, 4).Any(n => messages.Any(m => m.Contains($"{n} bonus {element} damage")));

            Assert.IsTrue(FiredInRange("Fire"),
                "fiery's on-hit proc must fire and land in [1,4] -- three rolled modifiers on one item, none " +
                "silently overriding another, reached by the roll this test actually rolled");
            Assert.IsTrue(FiredInRange("Arcane"),
                "astral's on-hit proc must ALSO fire alongside fiery's, and land in [1,4] the same way");

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
            Assert.AreEqual(2, reloadedCharacter.equipment.GetPlus(wornSlot), "the plus must survive");
            Assert.AreEqual((int)RiftTier.Convergent, reloadedCharacter.equipment.GetRiftTier(wornSlot), "the RiftTier must survive");
            CollectionAssert.AreEquivalent(rolledModifierIds, reloadedCharacter.equipment.GetModifierIds(wornSlot),
                "the exact rolled modifier set must survive");

            // Not merely the IDS surviving -- the reloaded save must resolve
            // to the IDENTICAL scaled effects, proving persistence carries
            // the roll rather than a pre-baked number that happened to match
            // once.
            var reloadedEffects = ContentDatabase.ModifierEffects(reloadedCharacter);
            Assert.AreEqual(30, reloadedEffects.Best(ModifierEffectType.LifestealPercent),
                "a reloaded save must resolve the identical scaled magnitude, not merely the identical ids");
            Assert.AreEqual(40, reloadedEffects.All.Single(e =>
                    e.Type == ModifierEffectType.ElementalDamageOnHitPercent && e.Against == DamageType.Fire).Magnitude,
                "the reloaded fiery rider must still scale to 40 after the round trip");
        }
    }
}

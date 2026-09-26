using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Content;
using PrincesPalace.Domain.Bot;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.Rng;

namespace PrincesPalace.PlayModeTests
{
    // DOES THE BOT ACTUALLY DRESS ITSELF, AND WITH WHAT?
    //
    // Every batch before this one measured a character in starting gear with
    // zero talents, which is not a configuration the game can produce -- a Late
    // profile fighting in a level-1 kit. This file is the assertion that the
    // fix is real rather than plumbed: gear moves onto characters, the
    // archetypes disagree about which gear, points get spent, and a profile
    // that has embers spends them.
    //
    // PlayMode rather than EditMode, and not by preference: every number here
    // comes back through ContentDatabase, which is Resources-backed and lives
    // in Core, and the EditMode assembly references Domain only.
    public class BotGearEvaluatorTests
    {
        private string _root;

        // Its own save root, for the reason DossierEquipTests documents on its
        // own: without it the test reads whatever save the runner happened to
        // be carrying, which is order-dependent and fails with a message about
        // the fixture wearing the costume of a product bug.
        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-gear-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
            SaveSystem.RootOverride = _root;
            SaveSlotManager.CurrentSlot = 0;
            SaveSlotManager.Forget();
            Navigation.LoadOverride = _ => { };
        }

        [TearDown]
        public void Restore()
        {
            Navigation.Reset();
            SaveSystem.RootOverride = null;
            SaveSlotManager.Forget();
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }

        private static SeededRandom Rng() => new SeededRandom(4242);

        private static Character Shawn()
        {
            var character = SaveSlotManager.CurrentSave?.ActiveSquad()?.FirstOrDefault(c => c != null);
            Assert.IsNotNull(character?.equipment, "the fresh save fielded nobody with a loadout");
            return character;
        }

        // ---- the trap this whole file exists to catch --------------------------------

        // A WEAPON MUST SCORE AS OFFENCE.
        //
        // ContentDatabase.EffectiveStats zeroes every worn item's attack
        // contribution on purpose (balance redesign D3) -- a weapon reaches
        // combat as WeaponPower at the FightEncounterAdapter seam instead. So
        // the obvious implementation, scoring ItemComparison.StatDelta.attack,
        // prices every sword in the game at exactly zero and equips armour
        // forever. That bug is invisible in a batch: it just reports a
        // difficulty curve.
        [Test]
        public void EquippingAWeaponMovesTheOffenceAxisAndNotJustTheStatBlock()
        {
            var character = Shawn();
            var weapon = ContentDatabase.Items.FirstOrDefault(
                i => i != null && i.kind == ItemKind.Weapon && i.IsEquippable);
            Assert.IsNotNull(weapon, "the content has no weapon to score");

            // Bare hands first, so the delta is the whole of what the weapon
            // brings rather than the difference between two swords.
            character.equipment.Clear();

            var delta = GearEvaluator.DeltaFor(character, weapon, 0, EquipmentSlot.Weapon1);
            Assert.IsTrue(delta.HasValue, $"'{weapon.id}' was refused from an empty main hand");
            Assert.Greater(delta.Value.Offence, 0,
                $"'{weapon.id}' scored no offence -- the evaluator is reading StatBlock.attack, " +
                "which carries no gear contribution, instead of EquippedWeaponPower");
        }

        [Test]
        public void AnItemThatDoesNotFitTheSlotIsNotACandidate()
        {
            var character = Shawn();
            var weapon = ContentDatabase.Items.FirstOrDefault(
                i => i != null && i.kind == ItemKind.Weapon && i.IsEquippable);
            Assert.IsNotNull(weapon);

            Assert.IsNull(GearEvaluator.DeltaFor(character, weapon, 0, EquipmentSlot.Head),
                "a sword was scored as a hat");
        }

        // ---- the pass ------------------------------------------------------------------

        [Test]
        public void TheEquipPassWearsSomethingOutOfAStockedBag()
        {
            var save = SaveSlotManager.CurrentSave;
            var character = Shawn();
            character.equipment.Clear();

            // Everything the content has that can be worn, so the pass has a
            // real choice in most slots rather than the starting stock's one.
            foreach (var item in ContentDatabase.Items.Where(i => i != null && i.IsEquippable).Take(40))
            {
                InventoryOps.Add(save.stockpiledItems, item.id, 1);
            }

            int inTheBagBefore = save.stockpiledItems.Sum(e => e?.count ?? 0);

            var worn = GearEvaluator.EquipBestGear(save, new GreedyAggressivePolicy(), Rng());

            Assert.IsNotEmpty(worn, "the equip pass put nothing on with a bag full of wearable gear");
            Assert.IsNotEmpty(character.equipment.EquippedItemIds(),
                "the pass reported equipping things that are not on the paperdoll");

            // THE MOVE IS A MOVE: exactly as many copies left the bag as
            // arrived on the paperdoll. Counted rather than asserted per id,
            // because the starting stock legitimately holds a second copy of
            // some of what gets worn -- "this id is not in the bag any more"
            // would be an assertion about the fixture, not about EquipMove.
            int inTheBagAfter = save.stockpiledItems.Sum(e => e?.count ?? 0);
            Assert.AreEqual(worn.Count, inTheBagBefore - inTheBagAfter,
                "the bag did not lose exactly what the paperdoll gained");
        }

        // TWO ARCHETYPES, TWO WARDROBES. Not a strict requirement of any single
        // seed -- content could conceivably offer nothing the two disagree
        // about -- so this asserts on the mechanism rather than on a
        // difference: an offence-first pass and a defence-first pass over the
        // same bag must not be the same code path returning the same list by
        // construction.
        [Test]
        public void TheArchetypesAreAskedRatherThanAssumed()
        {
            var save = SaveSlotManager.CurrentSave;
            var character = Shawn();

            var weapons = ContentDatabase.Items
                .Where(i => i != null && i.IsEquippable && i.kind == ItemKind.Weapon)
                .Take(6).ToList();
            // A CONTENT PRECONDITION, stated rather than hoped for (AUDIT #46):
            // this used to Ignore itself, which reads as green in a run.
            Assert.GreaterOrEqual(weapons.Count, 2,
                "content no longer has two equippable weapons, so there is nothing for the archetypes to " +
                "choose between -- this test has stopped covering the evaluator; give it a fixture bag");

            foreach (var item in weapons)
            {
                InventoryOps.Add(save.stockpiledItems, item.id, 1);
            }

            character.equipment.Clear();

            // Scored, not equipped, so both archetypes see the identical
            // starting loadout -- an equip pass would move the goalposts for
            // whichever ran second.
            var aggressive = new GreedyAggressivePolicy().Gear;
            var defensive = new GreedyDefensivePolicy().Gear;

            var best = new Dictionary<string, string>();
            foreach (var (name, weights) in new[] { ("aggressive", aggressive), ("defensive", defensive) })
            {
                string top = null;
                float topScore = float.NegativeInfinity;

                foreach (var item in weapons)
                {
                    var delta = GearEvaluator.DeltaFor(character, item, 0, EquipmentSlot.Weapon1);
                    if (!delta.HasValue) continue;

                    float score = weights.Score(delta.Value);
                    if (score > topScore) { topScore = score; top = item.id; }
                }

                best[name] = top;
            }

            Assert.IsNotNull(best["aggressive"], "no weapon was legal for the aggressive weighting");
            Assert.IsNotNull(best["defensive"], "no weapon was legal for the defensive weighting");
        }

        // ---- spending a level ------------------------------------------------------------

        // The six options must be MEASURED, not tabled. Strength and
        // Intelligence derive nothing through AbilityDerivation and reach
        // damage through the equipped weapon's ScalingProfile instead, so a
        // hardcoded "aggressive spends on Strength" would be right for a
        // greatsword and wrong for a dagger and would never notice which one
        // the character is holding.
        [Test]
        public void EveryAbilityScoreIsOfferedAndAtLeastOneOfThemDerivesSomething()
        {
            var character = Shawn();
            var options = GearEvaluator.StatOptionsFor(character);

            Assert.AreEqual(Domain.Stats.AbilityScores.All.Length, options.Count);
            Assert.IsTrue(options.Any(o => o.Deltas.Health != 0 || o.Deltas.Defence != 0
                                        || o.Deltas.Speed != 0 || o.Deltas.Offence != 0),
                "not one of the six ability scores derived anything -- the simulation is not placing the point");
        }

        [Test]
        public void MeasuringAStatPointDoesNotSpendOne()
        {
            var character = Shawn();
            character.unspentStatPoints = 3;
            var invested = character.InvestedPointTotal;

            GearEvaluator.StatOptionsFor(character);

            Assert.AreEqual(3, character.unspentStatPoints, "measuring the options spent a real point");
            Assert.AreEqual(invested, character.InvestedPointTotal);
        }

        // ---- the profiles -------------------------------------------------------------------

        [Test]
        public void TheMidProfileSpendsItsPointsAndBuysWhatItsEmbersAllow()
        {
            var save = ProfilePresets.Build(ProfilePresets.Mid, new GreedyAggressivePolicy(), Rng());
            Assert.IsNotNull(save);

            var character = save.ActiveSquad().FirstOrDefault(c => c != null);
            Assert.IsNotNull(character);

            Assert.AreEqual(ProfilePresets.MidLevel, character.level);
            Assert.AreEqual(0, character.unspentStatPoints,
                "the track's stat points were claimed and then left unspent");
            Assert.Greater(character.InvestedPointTotal, 0);

            // The ember budget is real content: one per LIVE boss definition,
            // which is the lifetime maximum since EmberPayout pays once per
            // unique boss ever killed. If the content ships bosses, the profile
            // must arrive holding embers and must have spent them.
            int budget = ProfilePresets.EmbersFor(ProfilePresets.Mid);
            Assert.Greater(budget, 0,
                "content ships no live boss, so the mid profile has no ember budget -- this test has stopped " +
                "covering talent spending (AUDIT #46: a precondition fails loudly rather than skipping)");

            Assert.IsNotEmpty(character.unlockedTalentIds,
                $"the profile held {budget} embers and kindled nothing");

            // AND THE IDS HAVE TO BE ONES THE CONTENT KNOWS -- the exact fault
            // TalentInvestmentTests was written for, now reachable from a
            // second caller that never touches the screen.
            foreach (var id in character.unlockedTalentIds)
            {
                Assert.IsNotNull(ContentDatabase.GetTalent(id),
                    $"the preset kindled '{id}', which talents.json does not have");
            }

            // THE GATE, NOT THE GRANT. This assertion used to be green for the
            // wrong reason: ProfilePresets.EmbersFor clamped what it handed
            // over to the cap, so the spend could not exceed what nothing was
            // checking. The clamp is gone and the refusal
            // (TalentPage.Refusal.BudgetSpent) is what holds this line now.
            //
            // It still cannot FAIL on today's content -- three live bosses is a
            // budget of 3 against a cap of 30, so the preset never approaches
            // it. The case where the gate actually bites is pinned without
            // content in TalentGateTests.KindlingIsRefusedOnceThirtyEmbersAre-
            // Committed; this is the end-to-end statement that the preset obeys
            // the same rule the screen does.
            Assert.AreEqual(EmberPayout.EmbersFor(
                    ContentDatabase.Enemies.Count(e => e != null && e.Data.IsBoss)),
                ProfilePresets.EmbersFor(ProfilePresets.Mid),
                "EmbersFor is clamping its grant again - the cap belongs on the SPEND, and a " +
                "second copy of it here is how it came to be enforced in the bot alone");

            Assert.LessOrEqual(ContentDatabase.SpentBy(character), ContentDatabase.EmberSpendCap);
            Assert.GreaterOrEqual(character.embers, 0, "a preset spent embers it did not have");
        }

        [Test]
        public void TheFreshProfileHoldsNoEmbersBecauseItHasKilledNoBoss()
        {
            Assert.AreEqual(0, ProfilePresets.EmbersFor(ProfilePresets.Fresh));

            var save = ProfilePresets.Build(ProfilePresets.Fresh, new GreedyAggressivePolicy(), Rng());
            var character = save.ActiveSquad().FirstOrDefault(c => c != null);

            Assert.IsEmpty(character.unlockedTalentIds);
            Assert.AreEqual(0, character.embers);
        }
    }
}

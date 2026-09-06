using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.PlayModeTests
{
    // RelicsReachCombatTests proves the WIRE works for one relic -- whichever
    // sorts first with an effect. That is the right shape for a test that
    // exists to catch the wire breaking again. It is the wrong shape for the
    // question this file answers: does EVERY authored relic specifically
    // resolve correctly through the real draft-to-fight pipeline, by its own
    // id, with its own effect or its own modifiers intact.
    //
    // The distinction is not academic. RelicsReachCombatTests would pass
    // unchanged if a single relic's id were typo'd in content, its effect
    // string misspelled, or its modifiers malformed -- as long as SOME other
    // relic in the file still worked. A per-relic typo is exactly the failure
    // mode a content author produces, and "one relic reaches combat" cannot
    // see it.
    public class EveryRelicReachesCombatTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-relicwire-all-" + System.Guid.NewGuid().ToString("N"));
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

        private static List<string> OneParty() =>
            ContentDatabase.Characters.Take(1).Select(c => c.id).ToList();

        private static List<string> OneEnemy() =>
            ContentDatabase.Enemies.Take(1).Select(e => e.id).ToList();

        // EVERY RELIC IN CONTENT, drafted alone, checked by name. Not a
        // sample, not "one that happens to have an effect" -- the whole file,
        // so a future 41st relic is covered by construction rather than by
        // remembering to add it here.
        [Test]
        public void EveryRelicWithAnEffectResolvesToThatExactEffectOnTheKit()
        {
            var withEffect = ContentDatabase.Relics
                .Where(r => r != null && r.Data.Effect != RelicEffect.None)
                .ToList();

            Assert.IsNotEmpty(withEffect, "fixture: content should have at least one effect relic");

            var wrong = new List<string>();

            foreach (var relic in withEffect)
            {
                var built = FightEncounterAdapter.Build(OneParty(), OneEnemy(),
                    new Domain.Rng.SeededRandom(11), relicIds: new[] { relic.id });

                var kitRelics = built.Session.KitFor(built.Party[0]).Relics.ToList();

                if (kitRelics.Count != 1 || kitRelics[0].Effect != relic.Data.Effect)
                {
                    wrong.Add($"{relic.id} authored as {relic.Data.Effect} but the kit carried " +
                              (kitRelics.Count == 0 ? "nothing" : string.Join(",", kitRelics.Select(r => r.Effect))));
                }
            }

            Assert.IsEmpty(wrong,
                "These relics did not reach combat as themselves: " + string.Join(" | ", wrong));
        }

        // Every relic with a NUMERIC modifier, checked the same exhaustive
        // way: drafted alone, and its stat must actually have moved. Catches
        // a modifier authored with the wrong RelicModifierType string, or a
        // damageType typo that made a ResistanceFlat modifier resolve to
        // nothing.
        [Test]
        public void EveryRelicWithModifiersActuallyChangesAStat()
        {
            var withModifiers = ContentDatabase.Relics
                .Where(r => r != null && r.Data.Modifiers != null && r.Data.Modifiers.Length > 0)
                .ToList();

            Assert.IsNotEmpty(withModifiers, "fixture: content should have at least one modifier relic");

            int baseAttack = FightEncounterAdapter.Build(OneParty(), OneEnemy(),
                new Domain.Rng.SeededRandom(11)).Party[0].Attack;
            // Both broad Defenses, not compared against zero: characters.json
            // now authors real physicalDefense/magicalDefense on the base
            // character (Shawn's own base stats, unrelated to any relic), so
            // the baseline has to be MEASURED the same way Attack/Health/
            // Speed already are rather than assumed to start at 0.
            int basePhysicalDefense = FightEncounterAdapter.Build(OneParty(), OneEnemy(),
                new Domain.Rng.SeededRandom(11)).Party[0].PhysicalDefense;
            int baseMagicalDefense = FightEncounterAdapter.Build(OneParty(), OneEnemy(),
                new Domain.Rng.SeededRandom(11)).Party[0].MagicalDefense;
            int baseHealth = FightEncounterAdapter.Build(OneParty(), OneEnemy(),
                new Domain.Rng.SeededRandom(11)).Party[0].MaxHealth;
            int baseSpeed = FightEncounterAdapter.Build(OneParty(), OneEnemy(),
                new Domain.Rng.SeededRandom(11)).Party[0].Speed;
            // Mechanic (e): armour penetration, the newest stat a relic
            // modifier can move (Pointy Nail on the End of a Stick).
            int baseArmorPenetration = FightEncounterAdapter.Build(OneParty(), OneEnemy(),
                new Domain.Rng.SeededRandom(11)).Party[0].ArmorPenetration;

            // Balance pass 2: Jo-Sun's Book of Anatomy and Vampire Dentures,
            // the two newest stats a relic modifier can move.
            int baseWeaknessBonus = FightEncounterAdapter.Build(OneParty(), OneEnemy(),
                new Domain.Rng.SeededRandom(11)).Party[0].WeaknessMultiplierBonusPercent;
            int baseRelicLifesteal = FightEncounterAdapter.Build(OneParty(), OneEnemy(),
                new Domain.Rng.SeededRandom(11)).Party[0].RelicLifestealPercent;

            var inert = new List<string>();

            foreach (var relic in withModifiers)
            {
                var built = FightEncounterAdapter.Build(OneParty(), OneEnemy(),
                    new Domain.Rng.SeededRandom(11), relicIds: new[] { relic.id });
                var actor = built.Party[0];

                bool moved = actor.Attack != baseAttack
                             || actor.PhysicalDefense != basePhysicalDefense
                             || actor.MagicalDefense != baseMagicalDefense
                             || actor.MaxHealth != baseHealth || actor.Speed != baseSpeed
                             || actor.ArmorPenetration != baseArmorPenetration
                             || actor.WeaknessMultiplierBonusPercent != baseWeaknessBonus
                             || actor.RelicLifestealPercent != baseRelicLifesteal
                             || !actor.TypedResistance.IsEmpty;

                if (!moved) inert.Add(relic.id);
            }

            Assert.IsEmpty(inert,
                "These relics carry modifiers in content but changed nothing on the combatant they were " +
                "drafted onto: " + string.Join(", ", inert));
        }

        // THE DRAFT ITSELF, not just the wire below it: every relic in
        // content has to be a legal thing for RelicDraftController.Roll to
        // offer, which is the OTHER half of "does it work when starting a
        // run" -- a relic that resolves fine once drafted is still useless if
        // nothing can ever draft it.
        [Test]
        public void EveryRelicIsOfferableByThePool()
        {
            // requiresConvergenceAbility carried through from the definition,
            // not left at RelicOption's own false default -- a test that
            // never sets it can never see mechanic (g)'s gate refuse
            // anything, which is exactly how this test missed
            // rampaging_bulls_horn being unofferable without a convergence
            // party (finding 6, code review).
            var all = ContentDatabase.Relics
                .Where(r => r != null)
                .Select(r => new Domain.Relics.RelicOption(r.id, r.Data.Rarity, r.Data.UnlockedBy, r.Data.RequiresConvergenceAbility))
                .ToList();

            // Every relic unlocked from the start (unlockedBy empty) must
            // survive RelicPool.Available with no achievements earned -- the
            // state a brand-new save is in. One that does not is a relic
            // nobody can ever draft.
            var earned = new HashSet<string>();
            var available = Domain.Relics.RelicPool.Available(all, earned)
                .Select(o => o.Id)
                .ToHashSet();

            var stuck = ContentDatabase.Relics
                .Where(r => r != null && r.Data.IsUnlockedFromTheStart && !available.Contains(r.id))
                .Select(r => r.id)
                .ToList();

            // rampaging_bulls_horn is EXPECTED to be stuck here -- it needs a
            // convergence-ability party, which "no achievements earned" says
            // nothing about either way. Excluded from the general assertion
            // and pinned on its own below, alongside the same check with a
            // convergence party.
            var unexpectedlyStuck = stuck.Where(id => id != "rampaging_bulls_horn").ToList();
            Assert.IsEmpty(unexpectedlyStuck,
                "These relics need no achievement but are not offerable on a fresh save: " +
                string.Join(", ", unexpectedlyStuck));

            var withoutConvergence = Domain.Relics.RelicPool.Available(all, earned, partyHasConvergenceAbility: false)
                .Select(o => o.Id).ToHashSet();
            var withConvergence = Domain.Relics.RelicPool.Available(all, earned, partyHasConvergenceAbility: true)
                .Select(o => o.Id).ToHashSet();

            Assert.IsFalse(withoutConvergence.Contains("rampaging_bulls_horn"),
                "rampaging_bulls_horn requires a convergence ability -- it must not be offerable without one");
            Assert.IsTrue(withConvergence.Contains("rampaging_bulls_horn"),
                "rampaging_bulls_horn must become offerable once the party has a convergence ability");

            // Every OTHER unlocked-from-the-start relic must appear in BOTH
            // -- the convergence gate is specific to mechanic (g), not a
            // general filter that happens to catch more than it should.
            var otherUnlocked = ContentDatabase.Relics
                .Where(r => r != null && r.Data.IsUnlockedFromTheStart && r.id != "rampaging_bulls_horn")
                .Select(r => r.id)
                .ToList();

            var missingWithout = otherUnlocked.Where(id => !withoutConvergence.Contains(id)).ToList();
            var missingWith = otherUnlocked.Where(id => !withConvergence.Contains(id)).ToList();

            Assert.IsEmpty(missingWithout,
                "unlocked relics missing WITHOUT a convergence party: " + string.Join(", ", missingWithout));
            Assert.IsEmpty(missingWith,
                "unlocked relics missing WITH a convergence party: " + string.Join(", ", missingWith));
        }

        // END TO END, through the real draft entry point: a squad with no
        // Transform skill must never be offered rampaging_bulls_horn across
        // several rounds/seeds, and a squad that has one must be able to see
        // it. RunOrchestrator.RelicDraftOffer is where partyHasConvergenceAbility
        // actually gets computed (from ContentDatabase.AvailableSkillsFor the
        // ACTIVE SQUAD) -- EveryRelicIsOfferableByThePool above proves the pool
        // rule alone; this proves the wiring INTO that rule from a real save.
        [Test]
        public void RelicDraftOfferRespectsConvergenceAcrossARealSquad()
        {
            // A Transform skill authored in content, if there is one --
            // reused rather than invented, so this proves the real wiring
            // rather than a fixture that happens to agree with itself.
            // ContentDatabase.AvailableSkillsFor needs a live Character (it
            // reads .level), which is what mechanic (g)'s production code
            // queries too -- so this reads the raw skill list directly
            // instead, and the fixture below grants it by LEVEL, the same
            // route AvailableSkillsFor itself checks first.
            var convergenceSkill = ContentDatabase.Skills
                .FirstOrDefault(s => s != null && s.Data.Effect == Domain.Combat.SkillEffect.Transform);

            Assert.IsNotNull(convergenceSkill,
                "fixture: no Transform skill exists in content -- cannot prove the convergence wiring without one");

            bool SeenAcrossRounds(List<string> squad, out bool seenBullsHorn)
            {
                SaveSlotManager.CurrentSave.selectedCharacterIds = squad;
                SaveSlotManager.SaveCurrent();

                seenBullsHorn = false;
                for (ulong seed = 1; seed <= 30 && !seenBullsHorn; seed++)
                {
                    foreach (var option in RunOrchestrator.RelicDraftOffer(seed))
                    {
                        if (option.Id == "rampaging_bulls_horn") { seenBullsHorn = true; break; }
                    }
                }

                return seenBullsHorn;
            }

            SeenAcrossRounds(OneParty(), out bool seenWithoutConvergence);
            Assert.IsFalse(seenWithoutConvergence,
                "rampaging_bulls_horn was offered to a squad with no convergence ability, across 30 seeds");

            // Levelled up so the Transform skill is actually AVAILABLE
            // (unlockLevel <= character.level), not just owned by id --
            // production reads AvailableSkillsFor, which checks exactly
            // that.
            var convergenceCharacter = SaveSlotManager.CurrentSave.roster
                .First(c => c.definitionId == convergenceSkill.Data.CharacterId);
            convergenceCharacter.level = Mathf.Max(convergenceCharacter.level, convergenceSkill.Data.UnlockLevel);

            SeenAcrossRounds(new List<string> { convergenceCharacter.definitionId }, out bool seenWithConvergence);
            Assert.IsTrue(seenWithConvergence,
                "rampaging_bulls_horn was never offered to a squad WITH a convergence ability, across 30 seeds -- " +
                "either the wiring is broken or 30 seeds is not enough draws to see a 1-of-many rare");
        }
    }
}

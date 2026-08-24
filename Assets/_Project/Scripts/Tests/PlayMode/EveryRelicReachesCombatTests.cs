using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
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
                .Where(r => r != null && r.effect != RelicEffect.None)
                .ToList();

            Assert.IsNotEmpty(withEffect, "fixture: content should have at least one effect relic");

            var wrong = new List<string>();

            foreach (var relic in withEffect)
            {
                var built = FightEncounterAdapter.Build(OneParty(), OneEnemy(),
                    new Domain.Rng.SeededRandom(11), relicIds: new[] { relic.id });

                var kitRelics = built.Session.KitFor(built.Party[0]).Relics.ToList();

                if (kitRelics.Count != 1 || kitRelics[0].Effect != relic.effect)
                {
                    wrong.Add($"{relic.id} authored as {relic.effect} but the kit carried " +
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
                .Where(r => r != null && r.modifiers != null && r.modifiers.Length > 0)
                .ToList();

            Assert.IsNotEmpty(withModifiers, "fixture: content should have at least one modifier relic");

            int baseAttack = FightEncounterAdapter.Build(OneParty(), OneEnemy(),
                new Domain.Rng.SeededRandom(11)).Party[0].Attack;
            int baseDefense = FightEncounterAdapter.Build(OneParty(), OneEnemy(),
                new Domain.Rng.SeededRandom(11)).Party[0].Defense;
            int baseHealth = FightEncounterAdapter.Build(OneParty(), OneEnemy(),
                new Domain.Rng.SeededRandom(11)).Party[0].MaxHealth;
            int baseSpeed = FightEncounterAdapter.Build(OneParty(), OneEnemy(),
                new Domain.Rng.SeededRandom(11)).Party[0].Speed;

            var inert = new List<string>();

            foreach (var relic in withModifiers)
            {
                var built = FightEncounterAdapter.Build(OneParty(), OneEnemy(),
                    new Domain.Rng.SeededRandom(11), relicIds: new[] { relic.id });
                var actor = built.Party[0];

                bool moved = actor.Attack != baseAttack || actor.Defense != baseDefense
                             || actor.MaxHealth != baseHealth || actor.Speed != baseSpeed
                             || actor.PhysicalResistance != 0 || actor.MagicalResistance != 0
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
            var all = ContentDatabase.Relics
                .Where(r => r != null)
                .Select(r => new Domain.Relics.RelicOption(r.id, r.rarity, r.unlockedBy))
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
                .Where(r => r != null && string.IsNullOrEmpty(r.unlockedBy) && !available.Contains(r.id))
                .Select(r => r.id)
                .ToList();

            Assert.IsEmpty(stuck,
                "These relics need no achievement but are not offerable on a fresh save: " +
                string.Join(", ", stuck));
        }
    }
}

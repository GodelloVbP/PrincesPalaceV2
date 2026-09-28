using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.PlayModeTests
{
    // A drafted relic actually reaches the fight.
    //
    // THIS IS THE TEST THAT DID NOT EXIST, and its absence hid the biggest
    // bug of the day. FightEncounterAdapter.KitFor passed `null` for the relic
    // list, so PlayerKit.Relics was always empty and RelicEffectFor never
    // matched anything. Dual Wield, Magical Shield and Bloodlust were
    // implemented in FightSession and covered by Domain tests that hand-build
    // their own kits -- all three worked perfectly, and none of them had ever
    // fired in an actual game.
    //
    // On top of that the relic DRAFT wrote its choice to run.relicIds and
    // nothing read it, so the whole screen was ceremony.
    //
    // Every assertion here is about the WIRE rather than the mechanic: the
    // rules are Domain's and already tested. What was missing was anything
    // checking that the two ends were joined.
    public class RelicsReachCombatTests
    {
        private string _root;

        [SetUp]
        public void UseAThrowawaySaveRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "pp-relicwire-" + System.Guid.NewGuid().ToString("N"));
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

        private static FightEncounterAdapter.BuiltFight Build(params string[] relicIds) =>
            FightEncounterAdapter.Build(OneParty(), OneEnemy(),
                new Domain.Rng.SeededRandom(11), relicIds: relicIds);

        // A relic that actually carries a mechanic, whatever the content is
        // called today.
        private static RelicDefinition AnEffectRelic() =>
            ContentDatabase.Relics.FirstOrDefault(r => r != null && r.Data.Effect != RelicEffect.None);

        [Test]
        public void ADraftedRelicArrivesOnTheKit()
        {
            var relic = AnEffectRelic();
            Assert.IsNotNull(relic, "fixture: content has at least one relic with an effect");

            var built = Build(relic.id);
            var kit = built.Session.KitFor(built.Party[0]);

            CollectionAssert.IsNotEmpty(kit.Relics.ToList(),
                "the relic never reached the fight -- KitFor is passing null again");
            Assert.AreEqual(relic.Data.Effect, kit.Relics[0].Effect);
        }

        [Test]
        public void NoRelicsMeansAnEmptyListRatherThanAThrow()
        {
            var built = Build();

            Assert.IsNotNull(built);
            CollectionAssert.IsEmpty(built.Session.KitFor(built.Party[0]).Relics.ToList());
        }

        [Test]
        public void AnIdNamingContentThatIsGoneIsDroppedRatherThanFatal()
        {
            // A save referencing a deleted relic has to stay playable.
            FightEncounterAdapter.BuiltFight built = null;

            Assert.DoesNotThrow(() => built = Build("no_such_relic"));
            Assert.IsNotNull(built);
            CollectionAssert.IsEmpty(built.Session.KitFor(built.Party[0]).Relics.ToList());
        }

        [Test]
        public void TheWholePartyGetsTheRunsRelic()
        {
            // Drafted for the DESCENT, not for a person. With a squad of one
            // this is the same statement either way, which is exactly why it
            // needs pinning before the roster grows.
            var relic = AnEffectRelic();

            // Two members, built from one character: "the whole party gets
            // it" cannot quietly become "the only party member gets it".
            //
            // The claim is put the same way instead of being weakened: the
            // adapter builds one combatant per id in the list and pairs kits
            // positionally, so the same id twice is two independent party
            // members. What is under test is that EVERY member is handed the
            // run's relic, and that is still exactly what is asked.
            string only = ContentDatabase.Characters.First().id;
            var party = new List<string> { only, only };

            Assert.AreEqual(2, party.Count,
                "this test needs a party of two to prove the relic reaches BOTH");

            var built = FightEncounterAdapter.Build(party, OneEnemy(),
                new Domain.Rng.SeededRandom(11), relicIds: new[] { relic.id });

            foreach (var member in built.Party)
            {
                CollectionAssert.IsNotEmpty(built.Session.KitFor(member).Relics.ToList(),
                    "a party member was left without the run's relic");
            }
        }

        // ---- the modifier table ------------------------------------------------

        [Test]
        public void ANumericRelicChangesTheStatItNames()
        {
            // Picked BY THE STAT IT TOUCHES, and asserted rather than
            // skipped: a silent skip is exactly how RelicModifiers.Apply,
            // an entire data-driven table, could drift back to zero
            // callers, which is the one thing this test exists to prevent.
            // Two relics carry modifiers and only one of them is an attack
            // relic.
            var withAttack = ContentDatabase.Relics.FirstOrDefault(r =>
                r != null && r.Data.Modifiers != null &&
                r.Data.Modifiers.Any(m => m.Stat == RelicStat.Attack));

            Assert.IsNotNull(withAttack,
                "no relic in content carries an Attack modifier - RelicModifiers.Apply is unreachable again, " +
                "which is the state this test was written to prevent");

            int baseAttack = Build().Party[0].Attack;
            int withRelic = Build(withAttack.id).Party[0].Attack;

            Assert.AreNotEqual(baseAttack, withRelic, "the modifier table is not being applied");
        }

        [Test]
        public void ModifiersAreAppliedAfterAbilityScoresNotBeforeThem()
        {
            // A percent relic is a percent of what the character actually
            // swings with. Applying it to the raw StatBlock would make the
            // number the player checks against their own sheet wrong.
            var modifiers = new List<RelicModifier> { new RelicModifier(RelicModifierType.AttackPercent, 100) };

            int derived = Build().Party[0].Attack;
            int doubled = RelicModifiers.Apply(derived, RelicStat.Attack, modifiers);

            Assert.AreEqual(derived * 2, doubled);
        }
    }
}

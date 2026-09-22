using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;
using UnityEngine;

namespace PrincesPalace.Domain.Tests
{
    // Pins the REAL enemies.json content, round-tripped through
    // EnemyEntryResolver, against the D5 balance table (Balance Redesign
    // plan, Phase 5A+5C). Unlike EnemyEntryResolverTests -- which exercises
    // the resolver's rules against synthetic RawEnemyEntry values -- this
    // reads the actual file off disk (EditMode, no Resources, sub-second;
    // same convention as FightCapacityPinTests) so a typo'd literal in the
    // authored JSON fails a test instead of only showing up in a playtest.
    //
    // A representative sample, not all eighteen: rat and golem (the two
    // callouts in the phase brief) plus all four bosses, since a boss's
    // MDEF cap at 25 is the one deliberately non-obvious number in the
    // whole table (see ResolvesBossMdefCapAtTwentyFive below).
    public class EnemyContentPinTests
    {
        private static IReadOnlyDictionary<string, ResolvedEnemy> _resolved;

        private static IReadOnlyDictionary<string, ResolvedEnemy> Resolved
        {
            get
            {
                if (_resolved != null) return _resolved;

                string path = Path.Combine(ContentDataRoot(), "enemies.json");
                Assert.IsTrue(File.Exists(path), "enemies.json is missing - this pin would otherwise pass vacuously.");

                var file = JsonUtility.FromJson<RawEnemyFile>(File.ReadAllText(path));
                Assert.IsNotEmpty(file.enemies, "enemies.json parsed to zero entries - the pin is not reading the catalog.");

                bool ok = EnemyEntryResolver.TryResolveAll(file.enemies, out var resolvedList, out var errors);
                Assert.IsTrue(ok, "enemies.json failed to resolve: " + string.Join("; ", errors ?? new List<string>()));

                _resolved = resolvedList.ToDictionary(e => e.Id);
                return _resolved;
            }
        }

        private static string ContentDataRoot()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "_Project", "ContentData")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "Could not locate Assets/_Project/ContentData from the working directory.");
            return Path.Combine(dir.FullName, "Assets", "_Project", "ContentData");
        }

        private static void AssertStats(string id, int hp, int speed, int atk, int pdef, int mdef, DamageType attackType)
        {
            Assert.IsTrue(Resolved.TryGetValue(id, out var enemy), $"enemies.json has no entry '{id}'.");

            var stats = enemy.BaseStats;
            Assert.AreEqual(hp, stats.maxHealth, $"{id}: maxHealth");
            Assert.AreEqual(speed, stats.speed, $"{id}: speed");
            Assert.AreEqual(atk, stats.attack, $"{id}: attack");
            Assert.AreEqual(pdef, stats.physicalDefense, $"{id}: physicalDefense");
            Assert.AreEqual(mdef, stats.magicalDefense, $"{id}: magicalDefense");
            Assert.AreEqual(attackType, enemy.AttackType, $"{id}: attackType");
        }

        // ---- D5 table, step-0 values -------------------------------------

        [Test]
        public void Rat_MatchesD5Table()
        {
            AssertStats("rat", hp: 40, speed: 7, atk: 10, pdef: 5, mdef: 0, attackType: DamageType.Physical);
        }

        [Test]
        public void Golem_MatchesD5Table()
        {
            AssertStats("golem", hp: 80, speed: 3, atk: 15, pdef: 45, mdef: 10, attackType: DamageType.Physical);
            Assert.AreEqual(5, Resolved["golem"].MinFloor, "golem: minFloor");
        }

        [Test]
        public void HollowChoir_MatchesD5Table()
        {
            AssertStats("hollow_choir", hp: 100, speed: 14, atk: 24, pdef: 5, mdef: 25, attackType: DamageType.Arcane);
            Assert.IsTrue(Resolved["hollow_choir"].IsBoss);
            Assert.AreEqual(1, Resolved["hollow_choir"].MinFloor, "hollow_choir: minFloor");
        }

        [Test]
        public void ForestWarden_MatchesD5Table()
        {
            AssertStats("forest_warden", hp: 110, speed: 4, atk: 40, pdef: 30, mdef: 10, attackType: DamageType.Physical);
            Assert.IsTrue(Resolved["forest_warden"].IsBoss);
            Assert.AreEqual(1, Resolved["forest_warden"].MinFloor, "forest_warden: minFloor");
        }

        // The one deliberately non-obvious number in the whole D5 table: no
        // boss's MDEF exceeds 25, because Shawn's Nature basic attack meets
        // enemy MDEF and a high-MDEF boss would wall the only real character.
        // See the D5 section of the balance plan.
        [Test]
        public void NoBossExceedsTheTwentyFiveMdefCap()
        {
            var bosses = new[] { "hollow_choir", "forest_warden" };
            foreach (var id in bosses)
            {
                Assert.LessOrEqual(Resolved[id].BaseStats.magicalDefense, 25,
                    $"{id}: boss MDEF must not exceed 25 - a wall here blocks the only playable character's damage type.");
            }
        }

        // Break-shield values called out in the phase brief as "keep
        // existing" rather than re-derived from the new (much lower) HP.
        [Test]
        public void BeetleAndTreant_KeepTheirAuthoredBreakShield()
        {
            Assert.AreEqual(16, Resolved["beetle"].BreakShieldPoints, "beetle: breakShieldPoints");
            Assert.AreEqual(32, Resolved["treant"].BreakShieldPoints, "treant: breakShieldPoints");
        }

        // The bog witch's affinity pair, pinned because it is the one row on
        // the roster that does NOT follow "resists what it attacks with" and
        // the owner chose its four elements by hand (AUDIT #132). She used to
        // be weak to her own Poison and resistant to Nature -- seven other
        // elementally-typed rows resist their own attackType, so that reading
        // as a transposed pair is exactly why it was filed. The answer taken
        // was neither of the finding's two options: Wind and Arcane cut
        // through a bog, Water and Earth are the bog itself. Two elements a
        // side is also the only multi-element affinity in the file, so this
        // pin is what proves the comma-list parse in RawEnemyEntry.weakness
        // survives on shipped content and not only in the resolver's own
        // synthetic tests.
        [Test]
        public void BogWitch_IsWeakToWindAndArcaneAndResistsWaterAndEarth()
        {
            var witch = Resolved["bog_witch"];

            CollectionAssert.AreEquivalent(
                new[] { DamageType.Wind, DamageType.Arcane }, witch.Weaknesses,
                "bog_witch: weakness");
            CollectionAssert.AreEquivalent(
                new[] { DamageType.Water, DamageType.Earth }, witch.Resistances,
                "bog_witch: resistance");

            // Her own attackType is no longer among her weaknesses, which is
            // the roster pattern the row used to invert.
            Assert.AreEqual(DamageType.Poison, witch.AttackType, "bog_witch: attackType");
            Assert.IsFalse(witch.Affinity.IsWeakTo(DamageType.Poison),
                "bog_witch: a monster weak to the element it attacks with is the shape #132 was filed about.");
        }

        // minFloor callouts from the phase brief, checked directly against
        // the resolved content rather than assumed unchanged.
        [Test]
        public void MinFloorCalloutsAreAsSpecified()
        {
            Assert.AreEqual(2, Resolved["bog_witch"].MinFloor, "bog_witch: minFloor");
            Assert.AreEqual(2, Resolved["wolf"].MinFloor, "wolf: minFloor");
            Assert.AreEqual(2, Resolved["ember_hound"].MinFloor, "ember_hound: minFloor");
            Assert.AreEqual(2, Resolved["treant"].MinFloor, "treant: minFloor");
            Assert.AreEqual(3, Resolved["mire_lurker"].MinFloor, "mire_lurker: minFloor");
            Assert.AreEqual(4, Resolved["rust_knight"].MinFloor, "rust_knight: minFloor");
            Assert.AreEqual(5, Resolved["golem"].MinFloor, "golem: minFloor");
        }
    }
}

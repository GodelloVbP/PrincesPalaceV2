using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.Domain.Tests
{
    // AUDIT #145's resolver blind spots: a sibling field enforced and a related
    // one not, so an authored value validated clean while meaning nothing
    // downstream. One fixture per refusal, each breaking exactly the rule
    // under test and pinning the refusal's own wording, plus the legal
    // neighbour that must still resolve -- a rule that refused everything
    // would otherwise pass. B4 (a stance naming no still) is a
    // cross-catalogue check and lives in CatalogueCrossChecksTests.
    public class ContentResolverBlindSpotTests
    {
        // ---- B3: bookTier is a price band, 0-4 ---------------------------

        private static bool ResolveBook(int bookTier, out List<string> errors)
        {
            var entry = new RawSkillEntry
            {
                id = "mud_burst", displayName = "Mud Burst", characterId = "sheep", manaCost = 5,
                bookOnly = true, bookTier = bookTier, unlockLevel = -1,
            };
            return SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { entry }, out _, out errors);
        }

        [Test]
        public void ABookTierAboveTheTopPriceBandIsRefused()
        {
            Assert.IsFalse(ResolveBook(ShopPricing.MaxBookTier + 1, out var errors));
            StringAssert.Contains("above the top price band", string.Join("; ", errors));
        }

        [Test]
        public void TheTopPriceBandItselfResolves()
        {
            Assert.IsTrue(ResolveBook(ShopPricing.MaxBookTier, out var errors), string.Join("; ", errors));
        }

        [Test]
        public void TheTopBandIsTheLastOneBookPriceActuallyPrices()
        {
            // Pinned with literals, not recomputed: band 4 has its own price
            // and band 5 falls through to the default arm.
            Assert.AreEqual(4, ShopPricing.MaxBookTier);
            Assert.AreEqual(145, ShopPricing.BookPrice(4));
        }

        // ---- B5: no plain attack, no plain-attack staging ------------------

        private static RawEnemyEntry Enemy() => new RawEnemyEntry
        {
            id = "golem", displayName = "Golem", maxHealth = 40,
            attackType = "Earth", weakness = "Water", resistance = "Earth",
            abilities = new[] { new RawEnemyAbility { skillId = "boulder_slam", weight = 1f } },
        };

        private static bool ResolveEnemy(RawEnemyEntry raw, out List<string> errors) =>
            EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { raw }, out _, out errors);

        [Test]
        public void AttackHoldsPositionOnARowWithNoPlainAttackIsRefused()
        {
            var raw = Enemy();
            raw.attackWeight = 0f;
            raw.attackHoldsPosition = true;

            Assert.IsFalse(ResolveEnemy(raw, out var errors));
            StringAssert.Contains("attackHoldsPosition is authored on a row with attackWeight 0", string.Join("; ", errors));
        }

        [Test]
        public void AttackApproachOnARowWithNoPlainAttackIsRefused()
        {
            var raw = Enemy();
            raw.attackWeight = 0f;
            raw.attackApproach = "charge";

            Assert.IsFalse(ResolveEnemy(raw, out var errors));
            StringAssert.Contains("attackApproach is authored on a row with attackWeight 0", string.Join("; ", errors));
        }

        [Test]
        public void PlainAttackStagingOnARowThatPlainAttacksResolves()
        {
            var raw = Enemy();
            raw.attackWeight = 1f;
            raw.attackHoldsPosition = true;
            raw.attackApproach = "charge";

            Assert.IsTrue(ResolveEnemy(raw, out var errors), string.Join("; ", errors ?? new List<string>()));
        }

        [Test]
        public void ARowWithNoPlainAttackAndNoStagingResolves()
        {
            var raw = Enemy();
            raw.attackWeight = 0f;

            Assert.IsTrue(ResolveEnemy(raw, out var errors), string.Join("; ", errors ?? new List<string>()));
        }

        // ---- B6: not weak to its own attackType ----------------------------

        [Test]
        public void AnAuthoredWeaknessNamingTheRowsOwnAttackTypeIsRefused()
        {
            // The bog witch as she first shipped (#132).
            var raw = Enemy();
            raw.attackType = "Poison";
            raw.weakness = "Wind, Poison";
            raw.resistance = "Nature";

            Assert.IsFalse(ResolveEnemy(raw, out var errors));
            StringAssert.Contains("weakness names Poison, which is this monster's own attackType",
                string.Join("; ", errors));
        }

        [Test]
        public void AWeaknessToAnotherElementResolves()
        {
            var raw = Enemy();
            raw.attackType = "Poison";
            raw.weakness = "Wind, Arcane";
            raw.resistance = "Water, Earth";

            Assert.IsTrue(ResolveEnemy(raw, out var errors), string.Join("; ", errors ?? new List<string>()));
        }

        // ---- B8: attack, both defenses and favor are never negative --------

        private static readonly string[] KnownPools = { "mana" };

        private static RawCharacterEntry Character(string id, int slot) => new RawCharacterEntry
        {
            id = id, displayName = id, role = "Tank",
            maxHealth = 30, speed = 5, attack = 5, physicalDefense = 5, magicalDefense = 3,
            strength = 10, dexterity = 10, constitution = 10, wisdom = 10, intelligence = 10, charisma = 10,
            startsInSquad = true, squadSlot = slot,
            plateTheme = "Blue", plateArt = "Plates/pc_sheep",
        };

        private static bool ResolveSquadWith(System.Action<RawCharacterEntry> breakIt, out List<ResolvedCharacter> resolved,
            out List<string> errors)
        {
            var probe = Character("probe", 1);
            breakIt(probe);
            var entries = new List<RawCharacterEntry> { probe, Character("b", 2), Character("c", 3) };
            return CharacterEntryResolver.TryResolveAll(entries, KnownPools, out resolved, out errors);
        }

        [TestCase("attack")]
        [TestCase("physicalDefense")]
        [TestCase("magicalDefense")]
        [TestCase("princesFavor")]
        public void ANegativeBaseStatIsRefusedNamingTheField(string field)
        {
            bool ok = ResolveSquadWith(c =>
            {
                switch (field)
                {
                    case "attack": c.attack = -1; break;
                    case "physicalDefense": c.physicalDefense = -1; break;
                    case "magicalDefense": c.magicalDefense = -1; break;
                    case "princesFavor": c.princesFavor = -1; break;
                }
            }, out _, out var errors);

            Assert.IsFalse(ok, $"a negative {field} resolved");
            StringAssert.Contains($"{field} cannot be negative (got -1)", string.Join("; ", errors));
        }

        [Test]
        public void ZeroIsStillALegalValueForAllFour()
        {
            bool ok = ResolveSquadWith(c =>
            {
                c.attack = 0; c.physicalDefense = 0; c.magicalDefense = 0; c.princesFavor = 0;
            }, out _, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
        }

        [Test]
        public void APositiveFavorIsCarriedThroughUnchanged()
        {
            bool ok = ResolveSquadWith(c => c.princesFavor = 4, out var resolved, out var errors);

            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            Assert.AreEqual(4, resolved.Single(c => c.Id == "probe").PrincesFavor);
        }

        // ---- B9: MaxMana / ManaRegen need a pool that takes them -----------

        private static RawTrackLevel Level(int level, string reward, int amount = 0,
            string identityKind = "", string value = "") =>
            new RawTrackLevel { level = level, reward = reward, amount = amount, identityKind = identityKind, value = value };

        // A full, legal 39-level track with level 10 (a Bump slot between two
        // Choice nodes) replaced by the node under test -- the same frame
        // RewardTrackEntryResolverPhase3Tests uses.
        private static List<RawTrackLevel> LevelsWith(RawTrackLevel underTest)
        {
            var levels = new List<RawTrackLevel>();
            for (int level = 2; level <= 30; level++)
            {
                if (level == 10) continue;
                if (level == 8) levels.Add(Level(8, "Respec"));
                else if (level == 25) levels.Add(Level(25, "SecondLife", 1));
                else if (level % 2 == 0) levels.Add(Level(level, "MaxHealth", 30));
                else levels.Add(Level(level, "StatPoint", 4));
            }

            levels.Add(underTest);
            levels.Add(Level(31, "Identity", identityKind: "Title", value: "Contractor"));
            levels.Add(Level(32, "Identity", identityKind: "PlateRim", value: "silver"));
            levels.Add(Level(33, "Identity", identityKind: "Title", value: "Champion"));
            levels.Add(Level(34, "Identity", identityKind: "PortraitFrame"));
            levels.Add(Level(35, "Identity", identityKind: "PlateEmboss", value: "silver"));
            levels.Add(Level(36, "Identity", identityKind: "Title", value: "Veteran"));
            levels.Add(Level(37, "Identity", identityKind: "VictoryPose"));
            levels.Add(Level(38, "Identity", identityKind: "PlateRim", value: "gold"));
            levels.Add(Level(39, "Identity", identityKind: "Title", value: "Legend"));
            levels.Add(Level(40, "Identity", identityKind: "Mastery"));
            return levels;
        }

        private static bool ResolveTrack(RawTrackLevel node, bool poolTakesManaBonuses, out List<string> errors)
        {
            var track = new RawRewardTrackEntry { characterId = "bear", levels = LevelsWith(node).ToArray() };
            var context = new RewardTrackCharacterContext { PrimaryPoolTakesManaBonuses = poolTakesManaBonuses };
            return RewardTrackEntryResolver.TryResolveAll(new List<RawRewardTrackEntry> { track },
                new Dictionary<string, RewardTrackCharacterContext> { ["bear"] = context }, out _, out errors);
        }

        [TestCase("MaxMana", 6)]
        [TestCase("ManaRegen", 1)]
        public void AManaBonusOnAPoolThatIgnoresManaBonusesIsRefused(string reward, int amount)
        {
            Assert.IsFalse(ResolveTrack(Level(10, reward, amount), poolTakesManaBonuses: false, out var errors));
            StringAssert.Contains($"{reward} is authored on a character whose primary pool has a fixed capacity rule",
                string.Join("; ", errors));
        }

        [TestCase("MaxMana", 6)]
        [TestCase("ManaRegen", 1)]
        public void AManaBonusOnAPoolThatTakesManaBonusesResolves(string reward, int amount)
        {
            Assert.IsTrue(ResolveTrack(Level(10, reward, amount), poolTakesManaBonuses: true, out var errors),
                string.Join("; ", errors ?? new List<string>()));
        }

        [Test]
        public void AContextBuiltWithoutPoolsStillTakesManaBonuses()
        {
            // The default is the pre-rule answer, so a fixture that never
            // mentions pools is not refused for a fact it did not state.
            Assert.IsTrue(new RewardTrackCharacterContext().PrimaryPoolTakesManaBonuses);
        }
    }
}

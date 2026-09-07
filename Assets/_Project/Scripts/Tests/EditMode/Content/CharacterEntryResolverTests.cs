using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // CharacterEntryResolver's per-field parsing, for fields no other
    // resolver test file already covers. plateTheme lives here rather than
    // in StartingSquadResolverTests (which is about the squad rule, not
    // field parsing) or ArtPathConventionTests (which is about the two
    // art-path conventions, not an arbitrary enum field).
    public class CharacterEntryResolverTests
    {
        // Same discipline StartingSquadResolverTests' own Character() uses:
        // every field the resolver checks before it ever reaches plateTheme
        // has to be valid, or a case here could fail for the wrong reason --
        // including TryResolveAll's WHOLE-FILE starting-squad rule (exactly
        // three startsInSquad rows), which is why the probe character rides
        // alongside three fixture starters rather than alone.
        private static RawCharacterEntry Starter(string id, int slot)
        {
            return new RawCharacterEntry
            {
                id = id,
                displayName = id,
                role = "Tank",
                maxHealth = 30,
                speed = 5,
                attack = 5,
                physicalDefense = 5,
                magicalDefense = 3,
                strength = 10,
                dexterity = 10,
                constitution = 10,
                wisdom = 10,
                intelligence = 10,
                charisma = 10,
                startsInSquad = true,
                squadSlot = slot,
            };
        }

        private static RawCharacterEntry Probe(string plateTheme)
        {
            return new RawCharacterEntry
            {
                id = "probe",
                displayName = "Probe",
                role = "Tank",
                maxHealth = 30,
                speed = 5,
                attack = 5,
                physicalDefense = 5,
                magicalDefense = 3,
                strength = 10,
                dexterity = 10,
                constitution = 10,
                wisdom = 10,
                intelligence = 10,
                charisma = 10,
                plateTheme = plateTheme,
            };
        }

        private static bool Resolve(string plateTheme, out ResolvedCharacter probe, out string error)
        {
            var entries = new List<RawCharacterEntry>
            {
                Starter("a", 1), Starter("b", 2), Starter("c", 3), Probe(plateTheme),
            };
            bool ok = CharacterEntryResolver.TryResolveAll(entries, out var all, out var errors);
            probe = ok ? all.Single(c => c.Id == "probe") : null;
            error = ok ? null : string.Join(" | ", errors);
            return ok;
        }

        // Same probe shape as Probe(), but with the six ability scores as
        // the thing under test instead of plateTheme.
        private static RawCharacterEntry ProbeWithScores(
            int strength, int dexterity, int constitution, int wisdom, int intelligence, int charisma)
        {
            return new RawCharacterEntry
            {
                id = "probe",
                displayName = "Probe",
                role = "Tank",
                maxHealth = 30,
                speed = 5,
                attack = 5,
                physicalDefense = 5,
                magicalDefense = 3,
                strength = strength,
                dexterity = dexterity,
                constitution = constitution,
                wisdom = wisdom,
                intelligence = intelligence,
                charisma = charisma,
            };
        }

        private static bool ResolveScores(RawCharacterEntry probe, out ResolvedCharacter resolved, out string error)
        {
            var entries = new List<RawCharacterEntry> { Starter("a", 1), Starter("b", 2), Starter("c", 3), probe };
            bool ok = CharacterEntryResolver.TryResolveAll(entries, out var all, out var errors);
            resolved = ok ? all.Single(c => c.Id == "probe") : null;
            error = ok ? null : string.Join(" | ", errors);
            return ok;
        }

        // THE OLD BUDGET IS GONE (removed 2026-09-07 on the owner's call --
        // see CharacterEntryResolver's header) -- this is what replaces the
        // refusal a 66-total row used to get: it must resolve cleanly now,
        // same as any other total would.
        [Test]
        public void AbilityScoresAboveTheOldSixtyBudgetStillResolve()
        {
            // Shawn's actual post-2026-09-07 spread: total 66.
            var probe = ProbeWithScores(strength: 12, dexterity: 10, constitution: 14,
                wisdom: 12, intelligence: 8, charisma: 10);

            Assert.IsTrue(ResolveScores(probe, out var resolved, out string error),
                "a 66-total row should resolve now that the exact-budget rule is gone: " + error);
            Assert.AreEqual(new AbilityScoreBlock(12, 10, 14, 12, 8, 10), resolved.AbilityScores);
        }

        // THE SANITY FLOOR THAT REPLACES THE BUDGET: a single score still
        // cannot be zero, negative, or absurdly high, independent of what
        // the other five total. See CharacterEntryResolver's header for why
        // 0 matters even though nothing divides BY a score.
        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(31)]
        public void AbilityScoreOutsideOneToThirtyIsRefusedByName(int badValue)
        {
            var probe = ProbeWithScores(strength: badValue, dexterity: 10, constitution: 10,
                wisdom: 10, intelligence: 10, charisma: 10);

            Assert.IsFalse(ResolveScores(probe, out _, out string error),
                $"strength {badValue} is outside 1-30 and must be refused");
            StringAssert.Contains("probe", error, "the refusal must name the character");
            StringAssert.Contains("strength", error, "the refusal must name the bad field");
            StringAssert.Contains(badValue.ToString(), error, "the refusal must name the bad value");
        }

        [Test]
        public void EmptyPlateTheme_DefaultsToBlue()
        {
            Assert.IsTrue(Resolve("", out var resolved, out var error),
                "an unauthored plateTheme should still build: " + error);
            Assert.AreEqual(ButtonTheme.Blue, resolved.PlateTheme);
        }

        [TestCase("Silver", ButtonTheme.Silver)]
        [TestCase("gold", ButtonTheme.Gold)]
        [TestCase("  Violet  ", ButtonTheme.Violet)]
        [TestCase("CRIMSON", ButtonTheme.Crimson)]
        [TestCase("Green", ButtonTheme.Green)]
        public void AuthoredPlateTheme_ParsesCaseInsensitively(string authored, ButtonTheme expected)
        {
            Assert.IsTrue(Resolve(authored, out var resolved, out var error),
                $"'{authored}' should resolve: " + error);
            Assert.AreEqual(expected, resolved.PlateTheme);
        }

        [Test]
        public void UnknownPlateTheme_RefusesTheBuild_NamingTheIdAndValidNames()
        {
            Assert.IsFalse(Resolve("Rainbow", out _, out string error),
                "an unknown plateTheme must not silently fall through to a default");

            StringAssert.Contains("probe", error, "the refusal must name the character");
            StringAssert.Contains("Rainbow", error, "the refusal must name the bad value");
            foreach (string name in new[] { "Gold", "Crimson", "Violet", "Blue", "Green", "Silver" })
            {
                StringAssert.Contains(name, error, "the refusal must list the valid ButtonTheme names");
            }
        }
    }
}

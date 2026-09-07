using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
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

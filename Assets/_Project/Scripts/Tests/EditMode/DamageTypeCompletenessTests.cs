using System;
using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // A DamageType member is only real once every place that enumerates the
    // type knows about it. This walks Enum.GetValues rather than a fixed
    // list of the eleven members this pass produced, so the NEXT type added
    // to the enum is caught here too, not just Earth/Water/Wind/Lightning/
    // Void.
    //
    // FightHudPaletteDamageTypeTests already pins the palette's literals and
    // guards its own fallback path; this file does not repeat that, it
    // proves the SET properties (distinct colour, a working resistance
    // slot, a distinct name, a parseable name) hold for whatever the enum
    // currently contains.
    public class DamageTypeCompletenessTests
    {
        private static readonly DamageType[] AllTypes = (DamageType[])Enum.GetValues(typeof(DamageType));

        [Test]
        public void EveryTypeHasItsOwnDistinctPaletteColour()
        {
            var seen = new Dictionary<string, DamageType>();
            foreach (var type in AllTypes)
            {
                string hex = FightHudPalette.ForDamageType(type);
                Assert.IsFalse(string.IsNullOrEmpty(hex), $"{type} has no palette colour");

                if (seen.TryGetValue(hex, out var earlier))
                {
                    Assert.Fail($"{type} shares palette colour {hex} with {earlier} -- give it its own token");
                }

                seen[hex] = type;
            }
        }

        // Struct-with-a-field-per-type (see ResistanceByType's own header
        // for why), so the coverage question is "does For(type) read back
        // whatever With(type, amount) just wrote", for every type.
        [Test]
        public void EveryTypeHasAWritableResistanceSlot()
        {
            foreach (var type in AllTypes)
            {
                var resistance = default(ResistanceByType).With(type, 25);
                Assert.AreEqual(25, resistance.For(type), $"{type}'s resistance slot did not round-trip");
            }
        }

        // GlossaryEntries.Listed and FightHudModel.DamageTypeLabel both
        // print DamageType.ToString() directly rather than through a
        // separate display-name table -- the enum member IS the table.
        // Enum.IsDefined guards the one way that table could go wrong: a
        // duplicate underlying int silently aliasing two members onto the
        // same name, which the enemies.json weakness/resistance mask (an
        // int under the hood) would then read as one element wearing two
        // names.
        [Test]
        public void EveryTypeHasADistinctDisplayName()
        {
            var seen = new HashSet<string>();
            foreach (var type in AllTypes)
            {
                Assert.IsTrue(Enum.IsDefined(typeof(DamageType), type), $"{type} is not a defined DamageType member");

                string name = type.ToString();
                Assert.IsFalse(string.IsNullOrWhiteSpace(name), $"{type} has a blank display name");
                Assert.IsTrue(seen.Add(name), $"{type}'s display name is reused by another member");
            }
        }

        // THE CONTENT PARSER, exercised through the real resolver rather
        // than re-testing System.Enum.TryParse in isolation: enemies.json's
        // weakness/resistance field is a comma-separated list of these exact
        // names (EnemyEntryResolver.TryResolveDamageTypes), and this proves
        // every current member's own name actually authors that weakness,
        // not just that SOME string happens to parse.
        [Test]
        public void EveryTypeNameRoundTripsThroughTheContentParser()
        {
            foreach (var type in AllTypes)
            {
                var entry = new RawEnemyEntry
                {
                    id = $"completeness_{type}".ToLowerInvariant(),
                    displayName = "Completeness Fixture",
                    maxHealth = 30,
                    weakness = type.ToString(),
                };

                bool ok = EnemyEntryResolver.TryResolveAll(
                    new List<RawEnemyEntry> { entry }, out var resolved, out var errors);

                Assert.IsTrue(ok, $"{type}'s own name did not parse as a weakness: " +
                                   string.Join("; ", errors ?? new List<string>()));
                Assert.IsTrue(resolved[0].Affinity.IsWeakTo(type),
                    $"{type} did not round-trip as the authored weakness");
            }
        }
    }
}

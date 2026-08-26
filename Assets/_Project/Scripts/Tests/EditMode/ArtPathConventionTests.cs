using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // Two art-path conventions coexist in content JSON and are distinguished
    // ONLY by field name. Getting one wrong fails silently in both directions,
    // so these tests cover three separate things and the third is the one that
    // matters most:
    //
    //   1. the rule itself accepts and rejects the right strings
    //   2. the rule cannot fall behind the content types (the reflection sweep)
    //   3. the RESOLVERS actually call it
    //
    // (3) exists because of finding #42: relics were implemented, correct, and
    // unreachable from play for their entire life, and every relic test passed
    // throughout, because each one tested the rule and none tested the wiring.
    // A rule with no caller is exactly as useful as no rule.
    public class ArtPathConventionTests
    {
        // Fields on a JSON-deserialised entry that hold a path. Suffix-matched
        // on both spellings because iconSheet is a path field whose name does
        // not end in "Path" -- a sweep keyed on that suffix alone would miss it,
        // which is the sort of gap this test exists to refuse.
        //
        // AND "path" ON ITS OWN, which is the same gap arriving from the other
        // direction. A field inside a nested block does not need to repeat the
        // block's name: SpellPresentation.path is "vfx.path" to an author, and
        // calling it "vfxPath" inside a type already called a presentation
        // would be the stutter the nesting removed. The sweep found
        // "vfx.sfxPath" and missed "vfx.path" until this said so.
        private static bool IsPathShaped(string fieldName) =>
            fieldName.EndsWith("Path") || fieldName.EndsWith("Sheet")
            || string.Equals(fieldName, "path", StringComparison.Ordinal);

        // ONE LEVEL DOWN AS WELL AS ON THE ENTRY ITSELF, reported dotted.
        //
        // A raw entry's path fields used to all be flat. SpellPresentation moved
        // six of them into a nested "vfx" block, and a sweep that only looked at
        // the entry's own fields stopped seeing two of them -- which made the
        // reverse test below declare "vfx.path" orphaned while the resolver was
        // checking it on every skill in the game.
        //
        // One level, not arbitrary depth: the content DTOs are flat by
        // convention and the one exception is a presentation. A recursive walk
        // would be guarding against a shape nobody has proposed, and would need
        // its own cycle check to be correct.
        private static List<(string Type, string Field)> PathFieldsOnRawEntries()
        {
            var found = new List<(string, string)>();

            foreach (var type in typeof(RawEnemyEntry).Assembly.GetTypes()
                         .Where(t => t.Namespace == "PrincesPalace.Domain.Content" && t.Name.StartsWith("Raw")))
            {
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (field.FieldType == typeof(string))
                    {
                        if (IsPathShaped(field.Name)) found.Add((type.Name, field.Name));
                        continue;
                    }

                    if (field.FieldType.IsPrimitive || field.FieldType.IsEnum || field.FieldType.IsArray) continue;
                    if (field.FieldType.Namespace != "PrincesPalace.Domain.Content") continue;

                    foreach (var nested in field.FieldType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (nested.FieldType == typeof(string) && IsPathShaped(nested.Name))
                        {
                            found.Add((type.Name, $"{field.Name}.{nested.Name}"));
                        }
                    }
                }
            }

            return found;
        }

        // ---- the table keeps up with the content types -------------------------

        [Test]
        public void EveryPathShapedFieldOnEveryRawEntryIsClassified()
        {
            var unclassified = PathFieldsOnRawEntries()
                .Where(f => !ArtPathConvention.IsClassified(f.Field))
                .Select(f => $"{f.Type}.{f.Field}")
                .Distinct()
                .ToList();

            Assert.IsEmpty(unclassified,
                "These path fields have no entry in ArtPathConvention, so nothing checks which convention " +
                "they follow and a wrong one would fail silently: " + string.Join(", ", unclassified) +
                ". Add each to ArtPathConvention.Kinds and call Check from its resolver.");
        }

        // The sweep above passes trivially if the filter is broken, which is how
        // a guard quietly stops guarding. Pinned against the nine fields that
        // exist today so a filter that finds nothing fails loudly.
        [Test]
        public void TheSweepActuallyFindsThePathFields()
        {
            var found = PathFieldsOnRawEntries();

            Assert.GreaterOrEqual(found.Count, 9,
                "The reflection sweep found " + found.Count + " path fields. It should see at least the nine " +
                "that exist (iconPath x4, iconSheet x2, portraitPath, battleSpritePath, spritePath, and the " +
                "nested vfx.path x2 / vfx.sfxPath x2) -- a sweep finding nothing would pass every other test " +
                "in this file.");
            CollectionAssert.Contains(found.Select(f => f.Field).ToList(), "vfx.path",
                "the sweep stopped descending into nested blocks, so a presentation's paths are unguarded");
            CollectionAssert.Contains(found.Select(f => f.Field).ToList(), "iconSheet",
                "iconSheet is the field whose name does not end in 'Path'; if the sweep misses it the suffix " +
                "matching has regressed.");
        }

        // The reverse direction: a table entry naming a field nothing has any
        // more is dead weight that reads as coverage.
        [Test]
        public void EveryClassifiedFieldNameStillExistsOnSomeRawEntry()
        {
            var live = PathFieldsOnRawEntries().Select(f => f.Field).Distinct().ToList();
            var orphaned = ArtPathConvention.ClassifiedFields.Where(name => !live.Contains(name)).ToList();

            Assert.IsEmpty(orphaned,
                "ArtPathConvention classifies these field names, but no Raw*Entry has them any more: " +
                string.Join(", ", orphaned) + ". A renamed field leaves the old name checking nothing.");
        }

        // ---- the rule itself ----------------------------------------------------

        [Test]
        public void AnEditorBakedFieldRejectsAResourcesRelativePath()
        {
            Assert.IsFalse(ArtPathConvention.Check("item 'x'", "iconPath", "Items/potion", out string error));
            StringAssert.Contains("ASSETS-relative", error);
        }

        [Test]
        public void ARuntimeLoadedFieldRejectsAnAssetsRelativePath()
        {
            Assert.IsFalse(ArtPathConvention.Check("enemy 'x'", "spritePath",
                "Assets/_Project/Art/Enemies/rat.png", out string error));
            StringAssert.Contains("RESOURCES-relative", error);
        }

        // Resources.Load takes a path WITHOUT an extension and returns null with
        // one, which is a second way to write a Resources-relative path that
        // still loads nothing.
        [Test]
        public void ARuntimeLoadedFieldRejectsAFileExtension()
        {
            Assert.IsFalse(ArtPathConvention.Check("enemy 'x'", "spritePath", "Enemies/rat.png", out string error));
            StringAssert.Contains("extension", error);
        }

        [Test]
        public void TheRealShippedShapesAreAccepted()
        {
            Assert.IsTrue(ArtPathConvention.Check("x", "spritePath", "Enemies/rat", out _));
            Assert.IsTrue(ArtPathConvention.Check("x", "vfx.sfxPath", "Audio/Sfx/frost_flare", out _));
            Assert.IsTrue(ArtPathConvention.Check("x", "battleSpritePath", "Characters/sheep", out _));
            Assert.IsTrue(ArtPathConvention.Check("x", "iconPath",
                "Assets/_Project/Art/Items/helmets_str/level_1.png", out _));
            Assert.IsTrue(ArtPathConvention.Check("x", "iconSheet",
                "Assets/_Project/Art/Items/longswords", out _));
        }

        // Art is optional throughout this project and the missing-art fallbacks
        // are deliberate, so "no icon" has to stay a valid authoring choice.
        // Only a wrongly-WRITTEN path is an error.
        [Test]
        public void AnEmptyPathIsAlwaysAllowed()
        {
            Assert.IsTrue(ArtPathConvention.Check("x", "iconPath", "", out _));
            Assert.IsTrue(ArtPathConvention.Check("x", "spritePath", null, out _));
            Assert.IsTrue(ArtPathConvention.Check("x", "vfx.path", "   ", out _));
        }

        [Test]
        public void AnUnclassifiedFieldNameFailsClosedRatherThanWavingItThrough()
        {
            Assert.IsFalse(ArtPathConvention.Check("x", "someNewPath", "whatever", out string error));
            StringAssert.Contains("not a classified art path", error);
        }

        // ---- the resolvers actually call it -------------------------------------

        [Test]
        public void EnemyResolver_RejectsAnAssetsPathInSpritePath()
        {
            var raw = new RawEnemyEntry
            {
                id = "test_enemy", displayName = "Test Enemy", maxHealth = 30,
                spritePath = "Assets/_Project/Art/Enemies/rat.png",
            };

            bool ok = EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { raw }, out _, out var errors);

            Assert.IsFalse(ok, "an Assets/ path in spritePath loads nothing at runtime and must not resolve");
            StringAssert.Contains("spritePath", string.Join(" ", errors));
        }

        [Test]
        public void EnemyResolver_RejectsAnAssetsPathInVfxAndSfx()
        {
            var vfx = new RawEnemyEntry
            {
                id = "e1", displayName = "E", maxHealth = 30,
                vfx = new SpellPresentation { path = "Assets/_Project/Art/Spells/boulder.png" },
            };
            var sfx = new RawEnemyEntry
            {
                id = "e2", displayName = "E", maxHealth = 30,
                vfx = new SpellPresentation { sfxPath = "Assets/_Project/Audio/hit.wav" },
            };

            Assert.IsFalse(EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { vfx }, out _, out _));
            Assert.IsFalse(EnemyEntryResolver.TryResolveAll(new List<RawEnemyEntry> { sfx }, out _, out _));
        }

        [Test]
        public void ItemResolver_RejectsAResourcesPathInIconPath()
        {
            var raw = new RawItemEntry { id = "potion", displayName = "Potion", iconPath = "Items/potion" };

            bool ok = ItemEntryResolver.TryResolveAll(new List<RawItemEntry> { raw }, out _, out var errors);

            Assert.IsFalse(ok, "a Resources-style iconPath is never baked into the scene and must not resolve");
            StringAssert.Contains("iconPath", string.Join(" ", errors));
        }

        [Test]
        public void SkillResolver_RejectsAnAssetsPathInVfxPath()
        {
            var raw = new RawSkillEntry
            {
                id = "s1", displayName = "Zap", characterId = "sheep", manaCost = 5,
                vfx = new SpellPresentation { path = "Assets/_Project/Art/Spells/lightning_bolt.png" },
            };

            bool ok = SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { raw }, out _, out var errors);

            Assert.IsFalse(ok);
            StringAssert.Contains("vfx.path", string.Join(" ", errors));
        }

        [Test]
        public void CharacterResolver_StillEnforcesBothOfItsPathsAfterMovingToTheSharedRule()
        {
            // These two were the only guarded fields in the codebase before the
            // rule was shared. Re-asserted here so routing them through
            // ArtPathConvention cannot have quietly loosened either.
            var portrait = MinimalCharacter();
            portrait.portraitPath = "Portraits/sheep";
            var battle = MinimalCharacter();
            battle.battleSpritePath = "Assets/_Project/Art/Characters/sheep.png";

            Assert.IsFalse(CharacterEntryResolver.TryResolveAll(
                new List<RawCharacterEntry> { portrait }, out _, out var portraitErrors));
            StringAssert.Contains("portraitPath", string.Join(" ", portraitErrors));

            Assert.IsFalse(CharacterEntryResolver.TryResolveAll(
                new List<RawCharacterEntry> { battle }, out _, out var battleErrors));
            StringAssert.Contains("battleSpritePath", string.Join(" ", battleErrors));
        }

        private static RawCharacterEntry MinimalCharacter()
        {
            // Ability scores must total exactly CharacterEntryResolver's budget,
            // so a character that fails for the WRONG reason cannot masquerade
            // as a path rejection.
            return new RawCharacterEntry
            {
                id = "test_character",
                displayName = "Test",
                role = "Tank",
                maxHealth = 30,
                speed = 5,
                attack = 5,
                physicalDefense = 5,
                magicalDefense = 3,
                strength = 12,
                dexterity = 10,
                constitution = 12,
                wisdom = 10,
                intelligence = 8,
                charisma = 8,
            };
        }
    }
}

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
        // The pool ids ContentBuilder hands the resolver. A LITERAL rather
        // than a read of pools.json: every case here is about one character
        // field, and reading the real catalogue would make a pool row's
        // rename fail these for a reason that has nothing to do with what
        // they assert. The one case that IS about the catalogue names its
        // own ids inline.
        private static readonly string[] KnownPools = { "mana" };

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

                // REQUIRED as of 2026-09-10 -- plateTheme is a character's
                // identity colour now, so an unauthored one refuses the
                // build. These three exist only to satisfy the whole-file
                // starting-squad rule, so any valid theme does; a case that
                // is ABOUT the theme uses Probe() below.
                plateTheme = "Blue",

                // REQUIRED for the same reason and in the same pass: the
                // plate IS the character on the fight column now, so a row
                // without one has no face there at all. The convention check
                // is what the value's SHAPE has to satisfy (Resources-
                // relative, no extension); whether the file exists is
                // ContentDatabase.ValidateContent's job and not this
                // resolver's, so a fixture path that names nothing is fine
                // here.
                plateArt = "Plates/pc_sheep",
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
                plateArt = "Plates/pc_sheep",
            };
        }

        private static bool Resolve(string plateTheme, out ResolvedCharacter probe, out string error)
        {
            var entries = new List<RawCharacterEntry>
            {
                Starter("a", 1), Starter("b", 2), Starter("c", 3), Probe(plateTheme),
            };
            bool ok = CharacterEntryResolver.TryResolveAll(entries, KnownPools, out var all, out var errors);
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

                // Authored so an ABILITY-SCORE case cannot fail for a theme
                // or a plate reason -- both are required now.
                plateTheme = "Blue",
                plateArt = "Plates/pc_sheep",
            };
        }

        private static bool ResolveScores(RawCharacterEntry probe, out ResolvedCharacter resolved, out string error)
        {
            var entries = new List<RawCharacterEntry> { Starter("a", 1), Starter("b", 2), Starter("c", 3), probe };
            bool ok = CharacterEntryResolver.TryResolveAll(entries, KnownPools, out var all, out var errors);
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

        // WAS EmptyPlateTheme_DefaultsToBlue, until 2026-09-10. The theme
        // stopped selecting one of six near-identical dark kit frames and
        // became the character's identity colour on every HUD card that
        // stands for them -- at which point there is no such thing as a
        // sensible default, because every valid value is somebody else's
        // colour and Blue in particular is Odette's. An unauthored row would
        // not look unthemed; it would look like her.
        [Test]
        public void EmptyPlateTheme_RefusesTheBuild()
        {
            Assert.IsFalse(Resolve("", out _, out string error),
                "an unauthored plateTheme must refuse the build, not fall through to Blue");

            StringAssert.Contains("probe", error, "the refusal must name the character");
            StringAssert.Contains("plateTheme", error, "the refusal must name the field");

            foreach (var theme in System.Enum.GetNames(typeof(ButtonTheme)))
            {
                StringAssert.Contains(theme, error,
                    $"the refusal must list every valid theme so an author can fix it in one pass -- '{theme}' is missing");
            }
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

        // ---- plateArt -------------------------------------------------------
        //
        // The character's OWN fight-HUD plate (the leather strip with their
        // head embossed at the right end). REQUIRED, unlike every other art
        // path in this project: art is optional almost everywhere and the
        // missing-art fallbacks are deliberate, but the HUD column has no
        // fallback plate to fall back TO -- the acting character is a
        // highlight on their own plate now rather than a promotion to a
        // bigger card, so a plateless row is an empty rectangle where the
        // other two have a face.
        //
        // WHETHER THE FILE EXISTS is not this resolver's job and is not
        // tested here: a Domain resolver cannot open a file, and
        // ContentDatabase.ValidateContent is what refuses a path that loads
        // nothing. What is tested here is the two things a resolver CAN see
        // -- that the field was authored at all, and that it was authored in
        // the right convention.

        private static RawCharacterEntry ProbeWithPlateArt(string plateArt)
        {
            var probe = Probe("Blue");
            probe.plateArt = plateArt;
            return probe;
        }

        private static bool ResolvePlateArt(string plateArt, out ResolvedCharacter resolved, out string error)
        {
            var entries = new List<RawCharacterEntry>
            {
                Starter("a", 1), Starter("b", 2), Starter("c", 3), ProbeWithPlateArt(plateArt),
            };
            bool ok = CharacterEntryResolver.TryResolveAll(entries, KnownPools, out var all, out var errors);
            resolved = ok ? all.Single(c => c.Id == "probe") : null;
            error = ok ? null : string.Join(" | ", errors);
            return ok;
        }

        [Test]
        public void AnAuthoredPlateArt_ResolvesOntoTheCharacter()
        {
            Assert.IsTrue(ResolvePlateArt("Plates/pc_bear", out var resolved, out string error), error);
            Assert.AreEqual("Plates/pc_bear", resolved.PlateArt);
        }

        [TestCase("")]
        [TestCase("   ")]
        public void AnUnauthoredPlateArt_RefusesTheBuild(string authored)
        {
            Assert.IsFalse(ResolvePlateArt(authored, out _, out string error),
                "a character with no plate has no face on the fight column -- that must not build");

            StringAssert.Contains("probe", error, "the refusal must name the character");
            StringAssert.Contains("plateArt", error, "the refusal must name the field");
            StringAssert.Contains("Plates/pc_sheep", error,
                "the refusal must show the shape an author has to write");
        }

        // THE TWO CONVENTIONS COEXIST IN CONTENT JSON AND ARE DISTINGUISHED
        // ONLY BY FIELD NAME (ArtPathConvention's own header). Getting this
        // one wrong fails SILENTLY at runtime -- Resources.Load returns null
        // for an Assets/ path and for a path with an extension -- so the
        // resolver refuses both shapes rather than letting a blank plate
        // ship.
        [Test]
        public void AnAssetsRelativePlateArt_IsRefusedAsTheWrongConvention()
        {
            Assert.IsFalse(
                ResolvePlateArt("Assets/_Project/Resources/Plates/pc_bear.png", out _, out string error),
                "plateArt is Resources.Load'ed at runtime, so an Assets/ path loads nothing");
            StringAssert.Contains("RESOURCES-relative", error);
        }

        [Test]
        public void APlateArtCarryingAFileExtension_IsRefused()
        {
            Assert.IsFalse(ResolvePlateArt("Plates/pc_bear.png", out _, out string error),
                "Resources.Load takes the path without an extension and returns null with one");
            StringAssert.Contains("extension", error);
        }

        // ---- primaryPoolId -------------------------------------------------
        //
        // Which resource a character's skills spend, as a pools.json id. The
        // field defaults to "mana" so the whole shipped roster needed no
        // edit; what has to be pinned is that the default really is mana,
        // that a blank behaves like an absent key, and that an id no pool
        // defines is refused rather than carried around as a dangling
        // string.

        private static RawCharacterEntry ProbeWithPool(string primaryPoolId, bool authorTheField)
        {
            var probe = new RawCharacterEntry
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

                // Authored explicitly so a later change to how plateTheme
                // or plateArt is defaulted cannot make a POOL case fail for
                // an identity reason.
                plateTheme = "Blue",
                plateArt = "Plates/pc_sheep",
            };

            // `authorTheField` is what separates "the author omitted the
            // key" (JsonUtility leaves the C# initialiser in place) from
            // "the author wrote an empty string". Both must mean mana, and
            // only writing them differently proves it.
            if (authorTheField)
            {
                probe.primaryPoolId = primaryPoolId;
            }

            return probe;
        }

        private static bool ResolvePool(RawCharacterEntry probe, string[] knownPools,
            out ResolvedCharacter resolved, out string error)
        {
            var entries = new List<RawCharacterEntry> { Starter("a", 1), Starter("b", 2), Starter("c", 3), probe };
            bool ok = CharacterEntryResolver.TryResolveAll(entries, knownPools, out var all, out var errors);
            resolved = ok ? all.Single(c => c.Id == "probe") : null;
            error = ok ? null : string.Join(" | ", errors);
            return ok;
        }

        [Test]
        public void AnOmittedPrimaryPoolId_IsMana()
        {
            Assert.IsTrue(ResolvePool(ProbeWithPool(null, authorTheField: false), KnownPools,
                    out var resolved, out string error),
                "a character that says nothing about pools must still build: " + error);
            Assert.AreEqual("mana", resolved.PrimaryPoolId);
        }

        [Test]
        public void AnEmptyPrimaryPoolId_IsAlsoMana()
        {
            Assert.IsTrue(ResolvePool(ProbeWithPool("", authorTheField: true), KnownPools,
                    out var resolved, out string error),
                "an explicitly blank primaryPoolId must mean the same as an omitted one: " + error);
            Assert.AreEqual("mana", resolved.PrimaryPoolId);
        }

        [Test]
        public void AnAuthoredPrimaryPoolId_ResolvesWhenThatPoolExists()
        {
            // Two ids in the catalogue, so this cannot pass by the resolver
            // simply accepting whatever it is handed against a one-element
            // list.
            Assert.IsTrue(ResolvePool(ProbeWithPool("fury", authorTheField: true), new[] { "mana", "fury" },
                    out var resolved, out string error),
                "a pool the catalogue defines must be accepted: " + error);
            Assert.AreEqual("fury", resolved.PrimaryPoolId);
        }

        [Test]
        public void AnUnknownPrimaryPoolId_RefusesTheBuild_NamingTheIdAndTheKnownPools()
        {
            Assert.IsFalse(ResolvePool(ProbeWithPool("rage", authorTheField: true), new[] { "mana", "fury" },
                    out _, out string error),
                "a primaryPoolId no pool defines must not be carried around as a dangling string");

            StringAssert.Contains("probe", error, "the refusal must name the character");
            StringAssert.Contains("rage", error, "the refusal must name the bad value");
            StringAssert.Contains("mana", error, "the refusal must list the pools that do exist");
            StringAssert.Contains("fury", error, "the refusal must list the pools that do exist");
        }
    }
}

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // The description token grammar (**bold**, {icon}): what Validate accepts,
    // what Expand writes for each capability set, and that nothing without a
    // well-formed token is touched.
    public class DescriptionMarkupTests
    {
        private const string Bold = "SourceSans3-Bold SDF";

        private static string Full(string text) => DescriptionMarkup.Expand(text, DescriptionRenderCaps.Full);
        private static string Plain(string text) => DescriptionMarkup.Expand(text, DescriptionRenderCaps.Plain);

        // ---- Expand -----------------------------------------------------------

        [Test]
        public void Expand_Bold_WrapsTheRunInTheBoldFont()
        {
            Assert.AreEqual($"Deals <font=\"{Bold}\">Vulnerable</font> now", Full("Deals **Vulnerable** now"));
        }

        [Test]
        public void Expand_Icon_BecomesASpriteTagNamedFromTheRegistry()
        {
            Assert.AreEqual("Hits <sprite=\"DescriptionIcons\" name=\"element_fire\"> hard", Full("Hits {fire} hard"));
            Assert.AreEqual("<sprite=\"DescriptionIcons\" name=\"resource_mana\">", Full("{mana}"));
        }

        [Test]
        public void Expand_BoldAndIconTogether_MatchesTheDesignExample()
        {
            Assert.AreEqual(
                $"Deals <font=\"{Bold}\">Vulnerable</font> <sprite=\"DescriptionIcons\" name=\"element_fire\">",
                Full("Deals **Vulnerable** {fire}"));
        }

        [Test]
        public void Expand_AdjacentTokens_AreEachExpanded()
        {
            Assert.AreEqual(
                "<sprite=\"DescriptionIcons\" name=\"element_fire\"><sprite=\"DescriptionIcons\" name=\"element_ice\">",
                Full("{fire}{ice}"));
            Assert.AreEqual($"<font=\"{Bold}\">a</font><font=\"{Bold}\">b</font>", Full("**a****b**"));
            Assert.AreEqual($"<font=\"{Bold}\">a</font> <font=\"{Bold}\">b</font>", Full("**a** **b**"));
        }

        [Test]
        public void Expand_AnIconInsideABoldRun_IsExpandedInsideIt()
        {
            Assert.AreEqual(
                $"<font=\"{Bold}\">Burn <sprite=\"DescriptionIcons\" name=\"element_fire\"></font>",
                Full("**Burn {fire}**"));
        }

        [Test]
        public void Expand_WithNoCaps_BoldIsPlainTextAndIconsAreTheirWords()
        {
            Assert.AreEqual("Deals Vulnerable Fire", Plain("Deals **Vulnerable** {fire}"));
            Assert.AreEqual("Costs 3 mana, cooldown 2, cost 1", Plain("Costs 3 {mana}, {cooldown} 2, {cost} 1"));
        }

        [Test]
        public void Expand_WithOneCapMissing_DegradesOnlyThatToken()
        {
            var boldOnly = new DescriptionRenderCaps(bold: true, icons: false);
            var iconsOnly = new DescriptionRenderCaps(bold: false, icons: true);

            Assert.AreEqual($"<font=\"{Bold}\">Hit</font> Fire",
                DescriptionMarkup.Expand("**Hit** {fire}", boldOnly));
            Assert.AreEqual("Hit <sprite=\"DescriptionIcons\" name=\"element_fire\">",
                DescriptionMarkup.Expand("**Hit** {fire}", iconsOnly));
        }

        [TestCase("")]
        [TestCase("Plain words.")]
        [TestCase("<i>Whisper</i> {0} 100% a*b")]
        [TestCase("a ** b")]
        [TestCase("****")]
        [TestCase("{bogus} and {")]
        [TestCase("a } b")]
        public void Expand_TextWithNoWellFormedToken_IsReturnedUnchanged(string text)
        {
            Assert.AreEqual(text, Full(text));
            Assert.AreEqual(text, Plain(text));
        }

        [Test]
        public void Expand_NullBecomesEmpty()
        {
            Assert.AreEqual("", Full(null));
        }

        [Test]
        public void Expand_AnUnpairedMarkerStaysWhole_AndDoesNotPairWithAnEarlierStar()
        {
            Assert.AreEqual($"<font=\"{Bold}\">a</font> ** b", Full("**a** ** b"));
        }

        [Test]
        public void Expand_EveryMarkerIsADelimiter_SoTwoStarsInANumberPairUp()
        {
            Assert.AreEqual($"2<font=\"{Bold}\">3 and 4</font>5", Full("2**3 and 4**5"));
        }

        [Test]
        public void Expand_NeverLeavesATokenBehind_ForAnyValidDescriptionAndAnyCaps()
        {
            string text = "**Bold {fire}** then {ice} and **{mana}** and {void}{wind}";
            Assert.IsNull(DescriptionMarkup.Validate(text));

            foreach (var caps in new[]
            {
                DescriptionRenderCaps.Full, DescriptionRenderCaps.Plain,
                new DescriptionRenderCaps(true, false), new DescriptionRenderCaps(false, true),
            })
            {
                string rendered = DescriptionMarkup.Expand(text, caps);
                StringAssert.DoesNotContain("**", rendered);
                StringAssert.DoesNotContain("{", rendered);
                StringAssert.DoesNotContain("}", rendered);
            }
        }

        // ---- Validate ---------------------------------------------------------

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Deals 5 damage.")]
        [TestCase("**Bold** and {fire} and {mana}.")]
        [TestCase("**a****b**")]
        [TestCase("**{fire}**")]
        [TestCase("a * b")]
        [TestCase("2**3 and 4**5")]
        public void Validate_AcceptsWellFormedText(string text)
        {
            Assert.IsNull(DescriptionMarkup.Validate(text));
        }

        [TestCase("**open", "unbalanced")]
        [TestCase("close**", "unbalanced")]
        [TestCase("2**3", "unbalanced")]
        [TestCase("****", "empty bold")]
        [TestCase("{bogus}", "unknown icon '{bogus}'")]
        [TestCase("{Fire}", "unknown icon '{Fire}'")]
        [TestCase("{}", "unknown icon '{}'")]
        [TestCase("{fire", "'{' at index 0 is never closed")]
        [TestCase("fire}", "'}' at index 4 closes nothing")]
        [TestCase("<b>x</b>", "contains '<'")]
        [TestCase("a < b", "contains '<'")]
        public void Validate_RefusesMalformedText(string text, string expected)
        {
            string error = DescriptionMarkup.Validate(text);

            Assert.IsNotNull(error, $"'{text}' should be refused");
            StringAssert.Contains(expected, error);
        }

        [Test]
        public void Validate_ReportsEveryProblemInOnePass()
        {
            string error = DescriptionMarkup.Validate("**a {x} <i>");

            StringAssert.Contains("contains '<'", error);
            StringAssert.Contains("unknown icon '{x}'", error);
            StringAssert.Contains("unbalanced", error);
        }

        [Test]
        public void Validate_NamesTheKnownTokens_WhenOneIsUnknown()
        {
            StringAssert.Contains("fire, ice, nature", DescriptionMarkup.Validate("{bogus}"));
        }

        // ---- the icon table ---------------------------------------------------

        [Test]
        public void TheIconTable_IsPinned()
        {
            var expected = new[]
            {
                "fire=element_fire=Fire", "ice=element_ice=Ice", "nature=element_nature=Nature",
                "physical=element_physical=Physical", "poison=element_poison=Poison", "arcane=element_arcane=Arcane",
                "earth=element_earth=Earth", "lightning=element_lightning=Lightning", "water=element_water=Water",
                "wind=element_wind=Wind", "void=element_void=Void", "mana=resource_mana=mana",
                "cooldown=resource_cooldown=cooldown", "cost=resource_cost=cost",
            };

            CollectionAssert.AreEqual(expected,
                DescriptionMarkup.Icons.Select(i => $"{i.Token}={i.SpriteName}={i.PlainWord}").ToArray());
        }

        [Test]
        public void EveryIconSprite_HasItsSourcePng()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Assets", "_Project", "Scripts")))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, "Could not locate the repo root from the working directory.");

            foreach (var icon in DescriptionMarkup.Icons)
            {
                string png = Path.Combine(dir.FullName, "Assets", "_Project", "Resources", "Icons", "Ability", icon.SpriteName + ".png");
                Assert.IsTrue(File.Exists(png), $"{icon.Token}: no source art at {png}");
            }
        }

        // ---- shipped content --------------------------------------------------

        [TestCase("skills.json", "skills", 50)]
        [TestCase("talents.json", "talents", 90)]
        [TestCase("relics.json", "relics", 35)]
        public void EveryShippedDescription_Validates(string file, string arrayKey, int minimumRows)
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Assets", "_Project", "ContentData", file)))
            {
                dir = dir.Parent;
            }

            Assert.IsNotNull(dir, $"Could not locate ContentData/{file} from the working directory.");

            var rows = JsonBlocks.ObjectsInArray(File.ReadAllText(Path.Combine(dir.FullName, "Assets", "_Project", "ContentData", file)), arrayKey);
            Assert.GreaterOrEqual(rows.Count, minimumRows, $"{file}: scanned too few rows for this check to mean anything");

            var offenders = new List<string>();
            foreach (string row in rows)
            {
                string error = DescriptionMarkup.Validate(JsonBlocks.String(row, "description"));
                if (error != null) offenders.Add($"{JsonBlocks.String(row, "id")}: {error}");
            }

            Assert.IsEmpty(offenders, string.Join("\n", offenders));
        }
    }

    // A description the resolvers refuse fails the content build, naming the row.
    public class DescriptionMarkupResolverTests
    {
        private static readonly string[] KnownAchievements = { "first_forest_boss" };
        private static readonly string[] KnownCharacters = { "sheep", "owl", "bear" };

        private static string SkillError(string description)
        {
            var raw = new RawSkillEntry { id = "shear", displayName = "Shear", characterId = "sheep", manaCost = 5, description = description };
            return SkillEntryResolver.TryResolveAll(new List<RawSkillEntry> { raw }, out _, out var errors)
                ? null : string.Join("; ", errors);
        }

        private static string TalentError(string description)
        {
            var raw = new RawTalentEntry
            {
                id = "test_node", displayName = "Test Node", characterId = "sheep", column = 0, row = 1,
                description = description,
                effects = new[] { new RawTalentEffect { type = "IgnoreDefensePercent", magnitude = 25 } },
            };
            return TalentEntryResolver.TryResolveAll(new[] { raw }, out _, out var errors)
                ? null : string.Join("; ", errors);
        }

        private static string RelicError(string description)
        {
            var raw = new RawRelicEntry { id = "dual_wield", displayName = "Dual Wield", effects = new[] { new RawEffect { type = "DualWield" } }, description = description };
            return RelicEntryResolver.TryResolveAll(new List<RawRelicEntry> { raw }, KnownAchievements, KnownCharacters, out _, out var errors)
                ? null : string.Join("; ", errors);
        }

        [Test]
        public void ATokenisedDescription_Resolves_InAllThreeTypes()
        {
            const string ok = "Deals **big** {fire} damage.";

            Assert.IsNull(SkillError(ok));
            Assert.IsNull(TalentError(ok));
            Assert.IsNull(RelicError(ok));
        }

        [Test]
        public void ABadDescription_FailsTheBuild_NamingTheRow()
        {
            StringAssert.Contains("skill 'shear': description", SkillError("**oops"));
            StringAssert.Contains("unbalanced", SkillError("**oops"));
            StringAssert.Contains("talent 'test_node': description", TalentError("{bogus}"));
            StringAssert.Contains("unknown icon '{bogus}'", TalentError("{bogus}"));
            StringAssert.Contains("relic 'dual_wield': description", RelicError("<b>x</b>"));
            StringAssert.Contains("contains '<'", RelicError("<b>x</b>"));
        }
    }
}

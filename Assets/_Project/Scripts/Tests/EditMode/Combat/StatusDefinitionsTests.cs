using System;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace.Domain.Tests
{
    // THE LIST THE GLOSSARY'S STATUSES CATEGORY READS. It is built off
    // StatusEffectType plus the presentation-only statuses, and its wording is
    // the fight tooltip's own with the number left open, so what these pin is
    // that no status is missing, that no two share a badge, and that the record
    // and the fight say the same thing.
    public class StatusDefinitionsTests
    {
        private static StatusDefinition Named(string name) =>
            StatusDefinitions.All.Single(d => d.Name == name);

        // Every enum member plus speed up, speed down, the transformation, rally,
        // silence and disarm.
        [Test]
        public void EveryStatusABadgeCanShowHasADefinition()
        {
            int engine = Enum.GetValues(typeof(StatusEffectType)).Length;

            Assert.AreEqual(16, engine, "fixture: the enum grew, so this count and the wording both need a look");
            Assert.AreEqual(engine + 6, StatusDefinitions.All.Count);

            foreach (StatusEffectType type in Enum.GetValues(typeof(StatusEffectType)))
            {
                Assert.IsTrue(StatusDefinitions.All.Any(d => d.Slug == StatusHud.SlugFor(type)),
                    $"{type} has no glossary definition");
            }
        }

        [Test]
        public void NoTwoDefinitionsShareASlugACodeOrAName()
        {
            CollectionAssert.AllItemsAreUnique(StatusDefinitions.All.Select(d => d.Slug).ToList());
            CollectionAssert.AllItemsAreUnique(StatusDefinitions.All.Select(d => d.Code).ToList());
            CollectionAssert.AllItemsAreUnique(StatusDefinitions.All.Select(d => d.Name).ToList());
        }

        [Test]
        public void TheListIsAlphabeticalByName()
        {
            CollectionAssert.AreEqual(
                new[]
                {
                    "Bleed", "Burn", "Chilled", "Disarmed", "Empowered", "Feared", "Fortified", "Marked",
                    "Poison", "Protect", "Provoked", "Rallied", "Regen", "Rooted", "Shielded", "Silenced",
                    "Speed down", "Speed up", "Stunned", "Thorned", "Transformed", "Vulnerable",
                },
                StatusDefinitions.All.Select(d => d.Name).ToList());
        }

        // Literal pins on the words a player reads, spread over the shapes: a
        // stacking tick, a one-shot, a state, a two-part status, and each of
        // the presentation-only ones.
        [TestCase("Poison", "Poison -- X damage each turn start. Separate applications add together.", "PSN", false)]
        [TestCase("Stunned", "Stunned -- turn skipped.", "STN", false)]
        [TestCase("Rooted", "Rooted -- Skill Only.", "RTD", false)]
        [TestCase("Feared", "Feared -- turn skipped and X% more damage taken.", "FER", false)]
        [TestCase("Fortified", "Fortified -- +X Defense and Magical Defense.", "FRT", true)]
        [TestCase("Shielded", "Shielded -- X shield.", "SHD", true)]
        [TestCase("Speed up", "Speed up -- +X Speed.", "SP+", true)]
        [TestCase("Speed down", "Speed down -- -X Speed.", "SP-", false)]
        [TestCase("Transformed", "Transformed -- transformed.", "FRM", true)]
        [TestCase("Rallied", "Rallied -- +X% attack. It lasts the rest of the fight and grows each round.", "RLY", true)]
        [TestCase("Silenced", "Silenced -- cannot cast.", "SIL", false)]
        [TestCase("Disarmed", "Disarmed -- -X% attack.", "DSM", false)]
        public void ADefinitionReadsAsTheFightsTooltipWithTheNumberLeftOpen(
            string name, string description, string code, bool positive)
        {
            var definition = Named(name);

            Assert.AreEqual(description, definition.Description);
            Assert.AreEqual(code, definition.Code);
            Assert.AreEqual(positive, definition.Positive);
        }

        // THE ONE-SOURCE CHECK. Every engine status's glossary clause is the
        // clause its live badge shows: put a number where the X is and it is
        // inside the fight's own tooltip.
        [Test]
        public void EveryEngineDefinitionsClauseIsInsideItsLiveTooltip()
        {
            foreach (StatusEffectType type in Enum.GetValues(typeof(StatusEffectType)))
            {
                var definition = StatusDefinitions.All.Single(d => d.Slug == StatusHud.SlugFor(type));
                string live = type == StatusEffectType.Shielded
                    ? StatusHud.WardRow(new StatusEffects.WardSummary(7, 1, 3, false)).Tooltip
                    : StatusHud.RowFor(new StatusEffects.StatusSummary(type, 7, 1, 3)).Tooltip;

                StringAssert.Contains(definition.Effect.Replace("X", "7"), live, $"{type}");
                StringAssert.StartsWith(definition.Name + " -- ", live, $"{type}");
            }
        }

        [Test]
        public void TheStatusesAStackingTooltipMentionsAreTheOnesTheGlossarySaysAdd()
        {
            foreach (StatusEffectType type in Enum.GetValues(typeof(StatusEffectType)))
            {
                if (type == StatusEffectType.Shielded) continue;

                string twoStacks = StatusHud.RowFor(new StatusEffects.StatusSummary(type, 7, 2, 3)).Tooltip;
                var definition = StatusDefinitions.All.Single(d => d.Slug == StatusHud.SlugFor(type));

                Assert.AreEqual(twoStacks.Contains("across 2 stacks"), definition.Note.Contains("add together"),
                    $"{type}: the tooltip and the glossary disagree about stacking");
            }
        }

        [Test]
        public void TheNameForAStatusIsItsEnumNameExceptStun()
        {
            Assert.AreEqual("Stunned", StatusHud.NameFor(StatusEffectType.Stun));
            Assert.AreEqual("Burn", StatusHud.NameFor(StatusEffectType.Burn));
        }

        [Test]
        public void AStatusWithNoWordingThrowsRatherThanReadingBlank()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => StatusHud.EffectPhrase((StatusEffectType)999, "X"));
        }
    }
}

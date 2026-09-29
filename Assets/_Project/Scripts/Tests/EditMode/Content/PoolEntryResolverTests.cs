using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;

namespace PrincesPalace.Domain.Tests
{
    // PoolEntryResolver's per-field rules. A pool row is the whole definition
    // of a combat resource -- how big it is, where the number comes from,
    // what fills it, what drains it, what colour it is -- so every refusal
    // here is a thing that would otherwise ship as a meter that reads wrong
    // rather than as a crash.
    //
    // THE MODEL IS VALIDATED AGAINST TWO USES, not one: the accepted cases
    // below are mana (WisdomDerived, Full, no gains, no decay) and the shape
    // Bjorn's Fury will have (Fixed 100, Zero, gains on attack and on damage
    // taken, decay per idle turn) -- the second is not authored in pools.json
    // yet, and asserting it here is what stops phase E finding out the type
    // cannot express it.
    public class PoolEntryResolverTests
    {
        private static RawPoolEntry Valid()
        {
            return new RawPoolEntry
            {
                id = "probe",
                displayName = "Probe",
                shortTag = "PR",
                capacityRule = "WisdomDerived",
                capacity = 30,
                startRule = "Full",
                brightHex = "#7EA8E6",
                deepHex = "#3A5A9A",
                textHex = "#C4D8F2",
            };
        }

        private static bool Resolve(RawPoolEntry raw, out ResolvedPool pool, out string error)
        {
            bool ok = PoolEntryResolver.TryResolveAll(new List<RawPoolEntry> { raw }, out var all, out var errors);
            pool = ok ? all.Single() : null;
            error = ok ? null : string.Join(" | ", errors);
            return ok;
        }

        private static string Refusal(RawPoolEntry raw)
        {
            Assert.IsFalse(Resolve(raw, out _, out string error), "this row should have been refused");
            return error;
        }

        // ---- the two shapes the model has to fit ---------------------------

        [Test]
        public void ManasShapeResolves()
        {
            Assert.IsTrue(Resolve(Valid(), out var pool, out string error), error);

            Assert.AreEqual(PoolCapacityRule.WisdomDerived, pool.CapacityRule);
            Assert.AreEqual(PoolStartRule.Full, pool.StartRule);
            Assert.AreEqual(30, pool.Capacity);

            // The two switches that default true, pinned as values rather
            // than assumed from the field initialisers: a pool with no
            // opinion is mana-shaped.
            Assert.IsTrue(pool.AllowsSpellBooks);
            Assert.IsTrue(pool.RestoredByManaEffects);

            // And the blank decayUnless resolves to the narrow reading.
            Assert.AreEqual(PoolDecayTrigger.Damage, pool.DecayUnless);
        }

        [Test]
        public void ARageBarShapeResolves()
        {
            var raw = Valid();
            raw.id = "fury";
            raw.displayName = "Fury";
            raw.shortTag = "FURY";
            raw.capacityRule = "Fixed";
            raw.capacity = 100;
            raw.startRule = "Zero";
            raw.gainOnAttack = 15;
            raw.gainOnDamageTaken = 10;
            raw.decayPerIdleTurn = 10;
            raw.decayUnless = "Damage";
            raw.pulse = true;
            raw.allowsSpellBooks = false;
            raw.restoredByManaEffects = false;

            Assert.IsTrue(Resolve(raw, out var pool, out string error), error);

            Assert.AreEqual(PoolCapacityRule.Fixed, pool.CapacityRule);
            Assert.AreEqual(PoolStartRule.Zero, pool.StartRule);
            Assert.AreEqual(100, pool.Capacity);
            Assert.AreEqual(15, pool.GainOnAttack);
            Assert.AreEqual(10, pool.GainOnDamageTaken);
            Assert.AreEqual(10, pool.DecayPerIdleTurn);
            Assert.IsTrue(pool.Pulse);
            Assert.IsFalse(pool.AllowsSpellBooks);
            Assert.IsFalse(pool.RestoredByManaEffects);
        }

        // ---- required strings ----------------------------------------------

        [Test]
        public void AnEmptyIdIsRefusedByEntryNumber()
        {
            var raw = Valid();
            raw.id = "";

            StringAssert.Contains("entry #1", Refusal(raw),
                "a row with no id cannot be named by id, so the refusal must locate it in the file");
        }

        [TestCase("displayName")]
        [TestCase("shortTag")]
        public void AMissingRequiredNameIsRefused(string field)
        {
            var raw = Valid();
            if (field == "displayName") raw.displayName = "";
            else raw.shortTag = "";

            string error = Refusal(raw);
            StringAssert.Contains("probe", error, "the refusal must name the pool");
            StringAssert.Contains(field, error, "the refusal must name the field");
        }

        [Test]
        public void AShortTagWithWhitespaceIsRefused()
        {
            var raw = Valid();
            raw.shortTag = "MAX MP";

            StringAssert.Contains("shortTag", Refusal(raw));
        }

        [Test]
        public void AShortTagLongerThanTheMeterRowIsRefused()
        {
            var raw = Valid();

            // Seven characters, one past PoolEntryResolver.MaxShortTagLength.
            // The literal is deliberate: rebuilding it from the constant
            // would make this test agree with whatever the constant says.
            raw.shortTag = "OVERAGE";

            string error = Refusal(raw);
            StringAssert.Contains("shortTag", error);
            StringAssert.Contains("7", error, "the refusal must say how long the tag actually is");
        }

        // ---- the two required enums ----------------------------------------

        [Test]
        public void AMissingCapacityRuleIsRefusedRatherThanDefaulted()
        {
            var raw = Valid();
            raw.capacityRule = "";

            string error = Refusal(raw);
            StringAssert.Contains("capacityRule", error);
            StringAssert.Contains("WisdomDerived", error, "the refusal must list the valid rules");
            StringAssert.Contains("Fixed", error, "the refusal must list the valid rules");
        }

        [Test]
        public void AMissingStartRuleIsRefusedRatherThanDefaulted()
        {
            var raw = Valid();
            raw.startRule = "";

            string error = Refusal(raw);
            StringAssert.Contains("startRule", error);
            foreach (string name in new[] { "Full", "Zero", "Value" })
            {
                StringAssert.Contains(name, error, "the refusal must list the valid start rules");
            }
        }

        [TestCase("fixed", PoolCapacityRule.Fixed)]
        [TestCase("  WISDOMDERIVED  ", PoolCapacityRule.WisdomDerived)]
        public void CapacityRuleParsesCaseInsensitively(string authored, PoolCapacityRule expected)
        {
            var raw = Valid();
            raw.capacityRule = authored;
            if (expected == PoolCapacityRule.Fixed) raw.capacity = 100;

            Assert.IsTrue(Resolve(raw, out var pool, out string error), error);
            Assert.AreEqual(expected, pool.CapacityRule);
        }

        [Test]
        public void AnUnknownCapacityRuleIsRefusedNamingTheValue()
        {
            var raw = Valid();
            raw.capacityRule = "CharismaDerived";

            StringAssert.Contains("CharismaDerived", Refusal(raw));
        }

        // ---- numbers --------------------------------------------------------

        [TestCase(0)]
        [TestCase(-5)]
        public void ANonPositiveCapacityIsRefused(int capacity)
        {
            var raw = Valid();
            raw.capacity = capacity;

            StringAssert.Contains("capacity", Refusal(raw));
        }

        [TestCase("gainPerTurn")]
        [TestCase("gainOnAttack")]
        [TestCase("gainOnDamageTaken")]
        [TestCase("decayPerIdleTurn")]
        public void ANegativeGainOrDecayIsRefusedByName(string field)
        {
            var raw = Valid();
            switch (field)
            {
                case "gainPerTurn": raw.gainPerTurn = -1; break;
                case "gainOnAttack": raw.gainOnAttack = -1; break;
                case "gainOnDamageTaken": raw.gainOnDamageTaken = -1; break;
                default: raw.decayPerIdleTurn = -1; break;
            }

            StringAssert.Contains(field, Refusal(raw));
        }

        // ---- start value ----------------------------------------------------

        [Test]
        public void AStartValueOnAPoolThatDoesNotUseOneIsRefused()
        {
            var raw = Valid();
            raw.startValue = 5;

            string error = Refusal(raw);
            StringAssert.Contains("startValue", error);
            StringAssert.Contains("Full", error, "the refusal must name the start rule that ignores it");
        }

        [TestCase(0)]
        [TestCase(31)]
        public void AStartValueOutsideOneToCapacityIsRefused(int startValue)
        {
            var raw = Valid();
            raw.startRule = "Value";
            raw.startValue = startValue;
            raw.gainPerTurn = 1;

            StringAssert.Contains("startValue", Refusal(raw));
        }

        [Test]
        public void AStartValueInsideCapacityResolves()
        {
            var raw = Valid();
            raw.startRule = "Value";
            raw.startValue = 12;

            Assert.IsTrue(Resolve(raw, out var pool, out string error), error);
            Assert.AreEqual(PoolStartRule.Value, pool.StartRule);
            Assert.AreEqual(12, pool.StartValue);
        }

        // ---- decay ----------------------------------------------------------

        [Test]
        public void ADecayTriggerOnAPoolThatNeverDecaysIsRefused()
        {
            var raw = Valid();
            raw.decayUnless = "AnyAction";

            string error = Refusal(raw);
            StringAssert.Contains("decayUnless", error);
            StringAssert.Contains("decayPerIdleTurn", error,
                "the refusal must say WHY the trigger could never fire");
        }

        [Test]
        public void TheWiderDecayTriggerIsAuthorableWithoutACodeChange()
        {
            var raw = Valid();
            raw.decayPerIdleTurn = 5;
            raw.decayUnless = "anyaction";

            Assert.IsTrue(Resolve(raw, out var pool, out string error), error);
            Assert.AreEqual(PoolDecayTrigger.AnyAction, pool.DecayUnless);
        }

        // ---- colours ---------------------------------------------------------

        [TestCase("")]
        [TestCase("7EA8E6")]
        [TestCase("#7EA8E")]
        [TestCase("#7EA8E6FFF")]
        [TestCase("#GGGGGG")]
        public void ABadColourTokenIsRefused(string bright)
        {
            var raw = Valid();
            raw.brightHex = bright;

            StringAssert.Contains("brightHex", Refusal(raw));
        }

        [Test]
        public void AnEightDigitColourTokenIsAccepted()
        {
            var raw = Valid();
            raw.deepHex = "#3A5A9AB3";

            Assert.IsTrue(Resolve(raw, out var pool, out string error), error);
            Assert.AreEqual("#3A5A9AB3", pool.DeepHex);
        }

        // ---- whole-file rules -------------------------------------------------

        [Test]
        public void APoolThatStartsEmptyAndGainsNothingIsRefused()
        {
            var raw = Valid();
            raw.startRule = "Zero";

            StringAssert.Contains("zero", Refusal(raw),
                "a pool that can never fill must be refused, not shipped as a dead meter");
        }

        [Test]
        public void APoolAnEngineFills_MayStartEmptyAndAuthorNoGain()
        {
            var raw = Valid();
            raw.startRule = "Zero";
            raw.engineFed = true;

            Assert.IsTrue(Resolve(raw, out var pool, out string error), error);
            Assert.AreEqual(PoolStartRule.Zero, pool.StartRule);
            Assert.AreEqual(0, pool.GainOnAttack);
        }

        [Test]
        public void DuplicateIdsAreRefusedNamingTheId()
        {
            var first = Valid();
            var second = Valid();

            Assert.IsFalse(PoolEntryResolver.TryResolveAll(new List<RawPoolEntry> { first, second },
                out _, out var errors));
            StringAssert.Contains("probe", string.Join(" | ", errors));
        }

        [Test]
        public void EveryRowIsReportedNotJustTheFirst()
        {
            var bad1 = Valid();
            bad1.id = "one";
            bad1.capacity = 0;

            var bad2 = Valid();
            bad2.id = "two";
            bad2.shortTag = "";

            Assert.IsFalse(PoolEntryResolver.TryResolveAll(new List<RawPoolEntry> { bad1, bad2 },
                out _, out var errors));
            Assert.AreEqual(2, errors.Count,
                "one build must tell an author everything wrong with the file: " + string.Join(" | ", errors));
        }

        [Test]
        public void SortOrderIsFileOrder()
        {
            var first = Valid();
            first.id = "first";
            var second = Valid();
            second.id = "second";

            Assert.IsTrue(PoolEntryResolver.TryResolveAll(new List<RawPoolEntry> { first, second },
                out var resolved, out _));
            Assert.AreEqual(0, resolved[0].SortOrder);
            Assert.AreEqual(1, resolved[1].SortOrder);
        }
    }
}

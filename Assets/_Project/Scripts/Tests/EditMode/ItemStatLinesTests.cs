using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Tests
{
    // How an item's numbers read.
    //
    // NONE OF THIS WAS TESTABLE IN v1, which is the reason it moved to Domain
    // on the way across. v1's version reached into ItemDefinition -- a
    // ScriptableObject -- so the EditMode suite could not see any of it, and
    // the bug that prompted writing it in the first place (two screens listing
    // different subsets of the same fields) is exactly the kind a test catches
    // and a reader does not.
    public class ItemStatLinesTests
    {
        private static StatBlock Stats(int hp = 0, int spd = 0, int atk = 0, int def = 0,
                                       int regen = 0, int pres = 0, int mres = 0)
        {
            var block = StatBlock.Zero;
            block.maxHealth = hp;
            block.speed = spd;
            block.attack = atk;
            block.defense = def;
            block.manaRegen = regen;
            block.physicalResistance = pres;
            block.magicalResistance = mres;
            return block;
        }

        private static AbilityScoreBlock Scores(int str = 0, int dex = 0, int con = 0,
                                                int wis = 0, int intel = 0, int cha = 0)
        {
            return new AbilityScoreBlock(str, dex, con, wis, intel, cha);
        }

        // ---- the field walk ------------------------------------------------

        [Test]
        public void EveryStatFieldGetsALine_WhichIsTheWholePointOfTheClass()
        {
            // The v1 bug: one caller listed maxHealth/speed/attack/defense and
            // silently omitted manaRegen and both resistances, so a leather
            // torso's largest contribution never appeared. Every field, or the
            // class has failed at its only job.
            var parts = ItemStatLines.BonusParts(
                Stats(hp: 1, spd: 2, atk: 3, def: 4, regen: 5, pres: 6, mres: 7),
                Scores(str: 8, dex: 9, con: 10, wis: 11, intel: 12, cha: 13));

            Assert.AreEqual(13, parts.Count, "a field stopped being listed: " + string.Join(", ", parts));

            foreach (string expected in new[]
                     {
                         "+1 HP", "+2 SPD", "+3 ATK", "+4 DEF", "+5 MP/turn",
                         "+6 phys res", "+7 magic res",
                         "+8 STR", "+9 DEX", "+10 CON", "+11 WIS", "+12 INT", "+13 CHA",
                     })
            {
                CollectionAssert.Contains(parts, expected);
            }
        }

        [Test]
        public void ZeroFieldsAreOmittedRatherThanPrintedAsZero()
        {
            var parts = ItemStatLines.BonusParts(Stats(atk: 2), Scores());

            CollectionAssert.AreEqual(new[] { "+2 ATK" }, parts);
        }

        [Test]
        public void PenaltiesKeepTheirSign()
        {
            // Steel is slow on purpose, so this is a real case rather than a
            // defensive one.
            var parts = ItemStatLines.BonusParts(Stats(spd: -2), Scores());

            CollectionAssert.AreEqual(new[] { "-2 SPD" }, parts);
        }

        // ---- the two joiners -----------------------------------------------

        [Test]
        public void ScalingTrailsAfterABullet_SoItDoesNotReadAsAnotherFlatBonus()
        {
            string compact = ItemStatLines.Compact(Stats(atk: 3), Scores(str: 1), "STR B");

            Assert.AreEqual("+3 ATK, +1 STR - scales STR B", compact);
        }

        [Test]
        public void AWeaponWithNoFlatBonusesStillPrintsItsGrades()
        {
            // For a weapon the grades ARE the item; a card that printed nothing
            // because the flat block was empty would hide the only reason to
            // pick it up.
            Assert.AreEqual("SCALES  STR A", ItemStatLines.Card(StatBlock.Zero, Scores(), "STR A"));
            Assert.AreEqual("", ItemStatLines.Card(StatBlock.Zero, Scores(), ""));
        }

        // ---- requirements --------------------------------------------------

        [Test]
        public void AMetRequirementNamesOnlyWhatIsNeeded()
        {
            string line = ItemStatLines.RequirementLine(Scores(str: 15), Scores(str: 20), isLive: true);

            StringAssert.Contains("Requires STR 15", line);
            StringAssert.Contains(ItemStatLines.GainHex, line);
            StringAssert.DoesNotContain("have", line, "a met requirement does not need to argue its case");
        }

        [Test]
        public void AnUnmetRequirementNamesTheShortfall()
        {
            string line = ItemStatLines.RequirementLine(Scores(str: 15), Scores(str: 9), isLive: false);

            StringAssert.Contains("Requires STR 15", line);
            StringAssert.Contains("have STR 9", line);
            StringAssert.Contains(ItemStatLines.LossHex, line);
        }

        [Test]
        public void TheColourFollowsTheResolverRatherThanTheArithmetic()
        {
            // isLive is passed IN, not re-derived from the two blocks. The
            // resolver settles a whole loadout at once, and a piece can be
            // inert while its own numbers look met -- because the scores that
            // met them came from a piece this one displaced. Re-deriving here
            // would give a green line on a dead item.
            string line = ItemStatLines.RequirementLine(Scores(str: 5), Scores(str: 99), isLive: false);

            StringAssert.Contains(ItemStatLines.LossHex, line);
        }

        // ---- the comparison body -------------------------------------------

        private static ItemComparison Comparison(
            StatBlock statDelta = default, AbilityScoreBlock scoreDelta = default,
            IReadOnlyList<EquipmentSlot> inert = null, IReadOnlyList<EquipmentSlot> live = null,
            bool candidateIsLive = true)
        {
            return new ItemComparison(statDelta, scoreDelta,
                inert ?? new List<EquipmentSlot>(), live ?? new List<EquipmentSlot>(), candidateIsLive);
        }

        [Test]
        public void AGainAndALossAreColouredApart()
        {
            var lines = ItemStatLines.DeltaLines(Comparison(Stats(atk: 4, spd: -1)));

            Assert.AreEqual(2, lines.Count);
            Assert.IsTrue(lines.Any(l => l.Contains("+4 Attack") && l.Contains(ItemStatLines.GainHex)));
            Assert.IsTrue(lines.Any(l => l.Contains("-1 Speed") && l.Contains(ItemStatLines.LossHex)));
        }

        [Test]
        public void TheCascadeIsReportedEvenWhenTheNetIsAnImprovement()
        {
            // The case the whole struct exists for: a swap can read as a clean
            // gain and still switch a worn piece off, because requirements are
            // met from everything ELSE worn. A net total cannot say that.
            string body = ItemStatLines.Body(
                Stats(atk: 6), Scores(str: 3), "",
                Scores(), Scores(),
                Comparison(Stats(atk: 6), Scores(str: 3),
                    inert: new[] { EquipmentSlot.Shoes }));

            StringAssert.Contains("+6 Attack", body);
            StringAssert.Contains("Also breaks", body);
            StringAssert.Contains(EquipmentSlots.DisplayName(EquipmentSlot.Shoes), body);
            StringAssert.Contains(ItemStatLines.LossHex, body);
        }

        [Test]
        public void UnlockingSomethingDormantIsReportedToo()
        {
            string body = ItemStatLines.Body(
                StatBlock.Zero, Scores(str: 5), "",
                Scores(), Scores(),
                Comparison(scoreDelta: Scores(str: 5), live: new[] { EquipmentSlot.Torso }));

            StringAssert.Contains("Also unlocks", body);
            StringAssert.Contains(ItemStatLines.GainHex, body);
        }

        [Test]
        public void ASwapThatChangesNothingPrintsNoHeading()
        {
            // Selecting the item already worn nets a zero delta. It should say
            // what the item is and stop, not print an empty VS. EQUIPPED.
            string body = ItemStatLines.Body(
                Stats(atk: 3), Scores(), "",
                Scores(), Scores(),
                Comparison());

            StringAssert.Contains("+3 ATK", body);
            StringAssert.DoesNotContain("VS. EQUIPPED", body);
        }

        [Test]
        public void AnEmptyComparisonKnowsItIsEmpty()
        {
            Assert.IsTrue(ItemComparison.None.IsEmpty);
            Assert.IsFalse(Comparison(Stats(atk: 1)).IsEmpty);
            Assert.IsFalse(Comparison(inert: new[] { EquipmentSlot.Head }).IsEmpty,
                "a swap that only breaks something has still changed something");
        }
    }
}

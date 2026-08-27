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
        private static StatBlock Stats(int hp = 0, int spd = 0, int atk = 0,
                                       int regen = 0, int pdef = 0, int mdef = 0)
        {
            var block = StatBlock.Zero;
            block.maxHealth = hp;
            block.speed = spd;
            block.attack = atk;
            block.manaRegen = regen;
            block.physicalDefense = pdef;
            block.magicalDefense = mdef;
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
            // The v1 bug: one caller listed maxHealth/speed/attack and
            // silently omitted manaRegen and both defenses, so a leather
            // torso's largest contribution never appeared. Every field, or the
            // class has failed at its only job. Six stat fields now (the old
            // `defense` is gone, not replaced one-for-one) plus six scores.
            //
            // BALANCE REDESIGN PHASE 6 (D7.1): each score's line now names
            // what it derives -- see AWisdomGrantNamesWhatItDerives and its
            // neighbours below for that annotation pinned in isolation.
            var parts = ItemStatLines.BonusParts(
                Stats(hp: 1, spd: 2, atk: 3, regen: 5, pdef: 6, mdef: 7),
                Scores(str: 8, dex: 9, con: 10, wis: 11, intel: 12, cha: 13));

            Assert.AreEqual(12, parts.Count, "a field stopped being listed: " + string.Join(", ", parts));

            foreach (string expected in new[]
                     {
                         "+1 HP", "+2 SPD", "+3 ATK", "+5 MP/turn",
                         "+6 phys def", "+7 magic def",
                         "+8 STR (weapon scaling)",
                         "+9 DEX (Speed, weapon scaling)",
                         "+10 CON (+200 HP, +20 Phys Def)",
                         "+11 WIS (+22 Mana, +22 Mag Def)",
                         "+12 INT (spell scaling)",
                         "+13 CHA (Signature Gain)",
                     })
            {
                CollectionAssert.Contains(parts, expected);
            }
        }

        // ---- ability-score grant annotations (D7.1) -------------------------

        [Test]
        public void AWisdomGrantNamesWhatItDerives()
        {
            // The plan's own worked example: +2 WIS is 2 x AbilityDerivation's
            // ManaPerPoint and MagicalDefensePerPoint (both 2), exactly,
            // regardless of the wearer's own Wisdom -- see
            // AbilityGrantAnnotation's header for why that exactness holds.
            var parts = ItemStatLines.BonusParts(StatBlock.Zero, Scores(wis: 2));

            CollectionAssert.AreEqual(new[] { "+2 WIS (+4 Mana, +4 Mag Def)" }, parts);
        }

        [Test]
        public void AConstitutionGrantNamesWhatItDerives()
        {
            var parts = ItemStatLines.BonusParts(StatBlock.Zero, Scores(con: 3));

            CollectionAssert.AreEqual(new[] { "+3 CON (+60 HP, +6 Phys Def)" }, parts);
        }

        [Test]
        public void ANegativeGrantKeepsItsSignThroughTheAnnotation()
        {
            var parts = ItemStatLines.BonusParts(StatBlock.Zero, Scores(con: -1));

            CollectionAssert.AreEqual(new[] { "-1 CON (-20 HP, -2 Phys Def)" }, parts);
        }

        [Test]
        public void StrengthAndIntelligenceNameTheScalingAxisRatherThanANumber()
        {
            // Neither derives a flat stat any more -- weapon/spell GRADES
            // answer "how hard do I hit" instead, so there is no number this
            // function could honestly print for either.
            CollectionAssert.AreEqual(new[] { "+4 STR (weapon scaling)" },
                ItemStatLines.BonusParts(StatBlock.Zero, Scores(str: 4)));
            CollectionAssert.AreEqual(new[] { "+4 INT (spell scaling)" },
                ItemStatLines.BonusParts(StatBlock.Zero, Scores(intel: 4)));
        }

        [Test]
        public void DexterityAndCharismaNameWhatTheyFeedWithoutClaimingAnExactNumber()
        {
            // Both divide ((score - 10) / 2 and / 4 in AbilityDerivation), so
            // a flat grant's marginal effect depends on the wearer's own
            // remainder -- this function has no wearer to check, so it names
            // the destination rather than guessing a figure.
            CollectionAssert.AreEqual(new[] { "+2 DEX (Speed, weapon scaling)" },
                ItemStatLines.BonusParts(StatBlock.Zero, Scores(dex: 2)));
            CollectionAssert.AreEqual(new[] { "+4 CHA (Signature Gain)" },
                ItemStatLines.BonusParts(StatBlock.Zero, Scores(cha: 4)));
        }

        // ---- the defense-percentage formatter (D7.1) ------------------------

        [Test]
        public void TheDefenseCurveReadsAsThePlansOwnPinnedExamples()
        {
            // Straight off the design doc's own "communicable rendering"
            // line: DEF 25 = 20% less, DEF 50 = 33%, DEF 100 = 50%, DEF 300 =
            // 75%. Pinned literally rather than recomputed (CLAUDE.md gotcha
            // 5) -- these four also happen to land on exact or near-exact
            // percentages, which is what makes them good boundary cases.
            Assert.AreEqual(20, ItemStatLines.DamageReductionPercent(25));
            Assert.AreEqual(33, ItemStatLines.DamageReductionPercent(50));
            Assert.AreEqual(50, ItemStatLines.DamageReductionPercent(100));
            Assert.AreEqual(75, ItemStatLines.DamageReductionPercent(300));
        }

        [Test]
        public void ZeroOrLessDefenseReducesNothing()
        {
            Assert.AreEqual(0, ItemStatLines.DamageReductionPercent(0));
            Assert.AreEqual(0, ItemStatLines.DamageReductionPercent(-5));
        }

        // ---- the weapon DMG line (D7.2) --------------------------------------

        [Test]
        public void AWeaponWithNoEquippedComparisonJustShowsItsNumber()
        {
            Assert.AreEqual("DMG 87", ItemStatLines.WeaponDamageText(87));
        }

        [Test]
        public void AWeaponComparedAgainstEquippedShowsBothNumbers()
        {
            Assert.AreEqual("DMG 87 -> 104", ItemStatLines.WeaponDamageText(104, equippedDamage: 87));
        }

        [Test]
        public void HoveringWhatIsAlreadyEquippedDoesNotClaimAnUpgrade()
        {
            // Comparing an item to itself nets identical numbers -- the line
            // should read as a plain figure, not "DMG 87 -> 87".
            Assert.AreEqual("DMG 87", ItemStatLines.WeaponDamageText(87, equippedDamage: 87));
        }

        [Test]
        public void TheDmgLineLeadsTheCardWhenPresent()
        {
            string card = ItemStatLines.Card(Stats(atk: 3), Scores(), "STR B", weaponDamage: "DMG 87 -> 104");

            Assert.AreEqual("DMG 87 -> 104\n+3 ATK\nSCALES  STR B", card);
        }

        [Test]
        public void TheCardOmitsTheDmgLineWhenThereIsNone()
        {
            // Every existing caller with no viewer in scope keeps compiling
            // and simply loses the DMG line -- this is the "no change" case.
            string card = ItemStatLines.Card(Stats(atk: 3), Scores(), "STR B");

            Assert.AreEqual("+3 ATK\nSCALES  STR B", card);
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

            Assert.AreEqual("+3 ATK, +1 STR (weapon scaling) - scales STR B", compact);
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

        // ---- item-modifier plan Phase E: the AFFIXES section ----------------

        [Test]
        public void ModifierSection_WithNoLines_PrintsNothingAtAll()
        {
            // The vast majority of items roll no modifiers. "AFFIXES" over a
            // blank line would read as a content gap rather than "none".
            Assert.AreEqual("", ItemStatLines.ModifierSection(null));
            Assert.AreEqual("", ItemStatLines.ModifierSection(new List<string>()));
        }

        [Test]
        public void ModifierSection_ListsEachLineUnderOneHeading()
        {
            string section = ItemStatLines.ModifierSection(new[]
            {
                "Fiery -- <color=#E0B84D>+20%</color> Fire dmg on hit",
                "Swift -- <color=#E0B84D>+12%</color> dodge chance",
            });

            Assert.AreEqual(
                "<color=#A695BC>AFFIXES</color>\n" +
                "Fiery -- <color=#E0B84D>+20%</color> Fire dmg on hit\n" +
                "Swift -- <color=#E0B84D>+12%</color> dodge chance",
                section);
        }

        [Test]
        public void Card_AppendsTheAffixesSectionAfterABlankLine_OnlyWhenThereAreModifiers()
        {
            string withoutModifiers = ItemStatLines.Card(Stats(atk: 3), Scores(), "STR B");
            Assert.AreEqual("+3 ATK\nSCALES  STR B", withoutModifiers);

            string withModifiers = ItemStatLines.Card(Stats(atk: 3), Scores(), "STR B",
                modifierLines: new[] { "Fiery -- <color=#E0B84D>+20%</color> Fire dmg on hit" });

            StringAssert.Contains("+3 ATK\nSCALES  STR B\n\n<color=#A695BC>AFFIXES</color>\n" +
                "Fiery -- <color=#E0B84D>+20%</color> Fire dmg on hit", withModifiers);
        }

        // ITEM-MODIFIER PLAN PHASE F: this used to assert the AFFIXES section
        // printed ONCE, above every member, because the roll used to be one
        // fact shared by the whole squad. It no longer is -- PHASE F diffs
        // each member's line against what THAT member currently has
        // equipped in the slot (see Core.ItemDescription.ModifierComparisonLines),
        // so two members can legitimately see different colours for the
        // exact same candidate. Each member now carries and prints its own
        // AFFIXES block.
        [Test]
        public void SquadBody_PrintsEachMembersOwnAffixesUnderTheirOwnBlock()
        {
            var squad = new (string, ItemComparison, IReadOnlyList<string>)[]
            {
                ("Shawn", Comparison(Stats(atk: 2)), new[] { "Fiery -- <color=#E0B84D>+20%</color> Fire dmg on hit" }),
                ("Wool", Comparison(candidateIsLive: false), new[] { "Fiery -- <color=#E0B84D>+20%</color> Fire dmg on hit" }),
            };

            string body = ItemStatLines.SquadBody(squad);

            int firstOccurrence = body.IndexOf("Fiery", System.StringComparison.Ordinal);
            int secondOccurrence = body.IndexOf("Fiery", firstOccurrence + 1, System.StringComparison.Ordinal);
            Assert.AreNotEqual(-1, secondOccurrence,
                "each member's own comparison can differ, so each member prints its own AFFIXES block");

            StringAssert.Contains($"Shawn\n   <color={ItemStatLines.GainHex}>+2 Attack</color>\n" +
                "   <color=#A695BC>AFFIXES</color>\n" +
                "   Fiery -- <color=#E0B84D>+20%</color> Fire dmg on hit", body);
        }

        [Test]
        public void SquadBody_OmitsAffixesForAMemberWithNoRolledLines()
        {
            var squad = new (string, ItemComparison, IReadOnlyList<string>)[]
            {
                ("Shawn", Comparison(Stats(atk: 2)), null),
            };

            string body = ItemStatLines.SquadBody(squad);

            StringAssert.DoesNotContain("AFFIXES", body);
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

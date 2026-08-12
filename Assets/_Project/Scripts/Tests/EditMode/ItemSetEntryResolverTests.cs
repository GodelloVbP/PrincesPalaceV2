using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;

namespace PrincesPalace.Domain.Tests
{
    public class ItemSetEntryResolverTests
    {
        private static RawItemSetEntry Minimal(int maxTier = 10)
        {
            return new RawItemSetEntry
            {
                id = "leather",
                displayName = "Leather",
                maxTier = maxTier,
                cost = 30,
                costPerTier = 20,
                pieces = new[]
                {
                    new RawSetPiece
                    {
                        id = "boots",
                        displayName = "Boots",
                        slot = "Shoes",
                        baseStats = new[] { "dexterity 1", "speed 1" },
                        topStats = new[] { "dexterity 3", "speed 3" },
                    }
                }
            };
        }

        private static List<ResolvedSetPiece> Resolve(params RawItemSetEntry[] sets)
        {
            bool ok = ItemSetEntryResolver.TryResolveAll(sets, out var resolved, out var errors);
            Assert.IsTrue(ok, string.Join("; ", errors ?? new List<string>()));
            return resolved;
        }

        // ---- the interpolation ------------------------------------------
        //
        // PINNED literals. Nothing here recomputes the formula or calls
        // ValueAt to build its own expected value (CLAUDE.md gotcha #5,
        // AUDIT.md #18).

        // GEOMETRIC now, not a straight line. GearScaling.TierGrowth is 1.25 a
        // tier, so the ladder accelerates and the last floor is the wild one.
        //
        // The flat start is the visible cost of that, and is asserted on
        // purpose rather than worked around. Across a span of four nothing
        // moves until tier 6, because the curve has only spent a third of its
        // travel by then. That is fine for the ability scores this exists to
        // carry, whose spans run 3 to 37 — and it is why a small secondary
        // stat (leather's speed, 1 to 3) sits still for most of the ladder. If
        // that ever reads as broken, the fix is a wider span on that stat, not
        // a gentler curve.
        [TestCase(0, 2)]
        [TestCase(1, 2)]
        [TestCase(3, 2)]
        [TestCase(5, 2)]
        [TestCase(6, 3)]
        [TestCase(7, 3)]
        [TestCase(8, 4)]
        [TestCase(9, 5)]
        [TestCase(10, 6)]
        public void ValueAt_AcceleratesBetweenTheTwoAuthoredEnds(int plus, int expected)
        {
            // 2 at plus 0, 6 at plus 10.
            Assert.AreEqual(expected, ItemSetEntryResolver.ValueAt(2, 6, plus, 10));
        }

        // Both ends are exact, always. A designer who writes "6 at max"
        // should get 6 at max and not 5 to a rounding rule.
        [Test]
        public void ValueAt_HitsBothEndsExactly()
        {
            Assert.AreEqual(2, ItemSetEntryResolver.ValueAt(2, 17, 0, 10));
            Assert.AreEqual(17, ItemSetEntryResolver.ValueAt(2, 17, 10, 10));
        }

        // ---- which drawing a plus level wears ---------------------------
        //
        // Ten drawings across eleven plus levels. PINNED literals again: the
        // whole point of the table is that it is checked against what the
        // sheet actually holds, not re-derived from the same expression the
        // production code uses.

        [TestCase(0, 1)]
        [TestCase(1, 2)]
        [TestCase(2, 3)]
        [TestCase(3, 4)]
        [TestCase(4, 5)]
        [TestCase(5, 6)]
        [TestCase(6, 6)]
        [TestCase(7, 7)]
        [TestCase(8, 8)]
        [TestCase(9, 9)]
        [TestCase(10, 10)]
        public void IconLevelFor_SpreadsTenDrawingsAcrossElevenPlusLevels(int plus, int expected)
        {
            Assert.AreEqual(expected, ItemSetEntryResolver.IconLevelFor(plus, maxTier: 10, levels: 10));
        }

        // The decision the spread exists for. A player's first piece and their
        // last are the two they actually look at, so the repeat belongs in the
        // middle — clamping would have doubled up the top instead.
        [Test]
        public void IconLevelFor_GivesBothEndsTheirOwnDrawing()
        {
            Assert.AreEqual(1, ItemSetEntryResolver.IconLevelFor(0, 10, 10));
            Assert.AreEqual(10, ItemSetEntryResolver.IconLevelFor(10, 10, 10));
            Assert.AreNotEqual(
                ItemSetEntryResolver.IconLevelFor(9, 10, 10),
                ItemSetEntryResolver.IconLevelFor(10, 10, 10),
                "The best piece in the set must not look identical to the one below it");
        }

        // Raising maxTier stretches the art the same way it stretches the
        // stats, rather than running out of drawings partway up.
        [Test]
        public void IconLevelFor_StretchesWhenASetGoesHigher()
        {
            Assert.AreEqual(1, ItemSetEntryResolver.IconLevelFor(0, 20, 10));
            Assert.AreEqual(6, ItemSetEntryResolver.IconLevelFor(10, 20, 10));
            Assert.AreEqual(10, ItemSetEntryResolver.IconLevelFor(20, 20, 10));
        }

        [Test]
        public void IconLevelFor_SurvivesDegenerateSheetsAndSets()
        {
            Assert.AreEqual(1, ItemSetEntryResolver.IconLevelFor(3, maxTier: 0, levels: 10));
            Assert.AreEqual(1, ItemSetEntryResolver.IconLevelFor(3, maxTier: 10, levels: 1));
            Assert.AreEqual(1, ItemSetEntryResolver.IconLevelFor(3, maxTier: 10, levels: 0));
        }

        [Test]
        public void IconPathFor_NamesTheSlicedFileAndToleratesATrailingSlash()
        {
            Assert.AreEqual("Art/Items/boots/level_1.png", ItemSetEntryResolver.IconPathFor("Art/Items/boots", 0, 10, 10));
            Assert.AreEqual("Art/Items/boots/level_10.png", ItemSetEntryResolver.IconPathFor("Art/Items/boots/", 10, 10, 10));
        }

        // No sheet is a supported state, not a hole to fill with a broken
        // path — most pieces still have no art.
        [Test]
        public void IconPathFor_WithNoSheet_IsEmptyRatherThanAPathToNothing()
        {
            Assert.AreEqual("", ItemSetEntryResolver.IconPathFor("", 3, 10, 10));
            Assert.AreEqual("", ItemSetEntryResolver.IconPathFor(null, 3, 10, 10));
            Assert.AreEqual("", ItemSetEntryResolver.IconPathFor("   ", 3, 10, 10));
        }

        [Test]
        public void EveryPlusLevelOfAPieceWithASheet_GetsItsOwnIconPath()
        {
            var set = Minimal();
            set.pieces[0].iconSheet = "Assets/_Project/Art/Items/boots_sheet";

            var boots = Resolve(set).OrderBy(p => p.Tier).ToList();

            Assert.AreEqual(11, boots.Count);
            Assert.AreEqual("Assets/_Project/Art/Items/boots_sheet/level_1.png", boots[0].IconPath);
            Assert.AreEqual("Assets/_Project/Art/Items/boots_sheet/level_10.png", boots[10].IconPath);
            CollectionAssert.AllItemsAreNotNull(boots.Select(p => p.IconPath).ToList());
        }

        [Test]
        public void APieceWithNoSheet_ResolvesWithNoIconRatherThanFailing()
        {
            var boots = Resolve(Minimal());

            CollectionAssert.AreEquivalent(new[] { "" }, boots.Select(p => p.IconPath).Distinct().ToList());
        }

        // A stat that goes DOWN as the piece improves — steel is slow — has
        // to floor the same direction as one that goes up, or the same
        // authored span behaves differently depending on its sign. C#
        // integer division truncates toward zero and would get this wrong.
        //
        // Floor is not symmetric about zero (floor(-0.2) is -1, not 0 —
        // the same reason AbilityDerivation.Modifier is asymmetric about its
        // centre rather than truncating), so a descending span reaches its
        // first negative step at plus 1, not at the same plus an ascending
        // span of equal magnitude would first move. That is correct floor
        // behaviour, not a bug: the assertions below are real floor(-0.2),
        // floor(-1.0), floor(-1.8) and the exact top, not a truncated guess.
        [Test]
        public void ValueAt_FloorsDescendingStatsTheSameWayAsAscendingOnes()
        {
            // 0 down to -2 across ten pluses.
            Assert.AreEqual(-1, ItemSetEntryResolver.ValueAt(0, -2, 1, 10), "floor(-0.2)");
            Assert.AreEqual(-1, ItemSetEntryResolver.ValueAt(0, -2, 5, 10), "floor(-1.0), exact");
            Assert.AreEqual(-2, ItemSetEntryResolver.ValueAt(0, -2, 9, 10), "floor(-1.8)");
            Assert.AreEqual(-2, ItemSetEntryResolver.ValueAt(0, -2, 10, 10), "the authored top, exact");
        }

        // The property that makes maxTier safe to raise later: plus 0 is the
        // authored floor whatever the ceiling is.
        [Test]
        public void RaisingMaxPlus_LeavesTheBottomOfTheCurveWhereItWas()
        {
            Assert.AreEqual(2, ItemSetEntryResolver.ValueAt(2, 6, 0, 10));
            Assert.AreEqual(2, ItemSetEntryResolver.ValueAt(2, 6, 0, 20));
            Assert.AreEqual(6, ItemSetEntryResolver.ValueAt(2, 6, 20, 20));
        }

        // ---- expansion ---------------------------------------------------

        [Test]
        public void OnePiece_BecomesOneItemPerTier_Inclusive()
        {
            var resolved = Resolve(Minimal(maxTier: 10));

            Assert.AreEqual(11, resolved.Count, "Tier 0 through 10 is eleven items, not ten");
            CollectionAssert.AreEquivalent(
                Enumerable.Range(0, 11).Select(p => $"leather_boots_p{p}").ToList(),
                resolved.Select(p => p.Id).ToList());
        }

        // A set with no authored ladder keeps the bare piece name rather than
        // inventing a word for it — the graceful-degradation posture the rest
        // of the content layer takes on missing art and missing ids.
        [Test]
        public void WithNoTierAdjectives_TheNameIsJustThePiece()
        {
            var byId = Resolve(Minimal()).ToDictionary(p => p.Id);

            Assert.AreEqual("Leather Boots", byId["leather_boots_p0"].DisplayName);
            Assert.AreEqual("Leather Boots", byId["leather_boots_p7"].DisplayName,
                "Without a ladder every tier reads the same — the tier is still in the id and the stats");
        }

        // The ladder is what makes a tier-7 piece read as a better OBJECT
        // rather than as a bigger number. Weapons have had this since they
        // were authored; armour was the half that was missing.
        [Test]
        public void TierAdjectives_LeadTheName()
        {
            var set = Minimal();
            set.tierAdjectives = new[] { "Ragged", "Worn", "Supple", "Cured", "Hardened" };

            var byId = Resolve(set).ToDictionary(p => p.Id);

            Assert.AreEqual("Ragged Leather Boots", byId["leather_boots_p0"].DisplayName);
            Assert.AreEqual("Hardened Leather Boots", byId["leather_boots_p4"].DisplayName);
            Assert.AreEqual("Hardened Leather Boots", byId["leather_boots_p10"].DisplayName,
                "The last adjective covers every tier past the end of a short list");
        }

        // Plus lives on the instance, so a definition that named one would be
        // wrong for every other copy of the same item.
        [Test]
        public void TheGeneratedName_NeverCarriesAPlus()
        {
            var set = Minimal();
            set.tierAdjectives = new[] { "Ragged", "Worn", "Supple" };

            foreach (var piece in Resolve(set))
            {
                StringAssert.DoesNotContain("+", piece.DisplayName,
                    $"'{piece.DisplayName}' bakes a plus into a definition");
            }
        }

        [Test]
        public void APieceCarriesItsSetAndSlotThrough()
        {
            var piece = Resolve(Minimal()).First(p => p.Tier == 4);

            Assert.AreEqual(EquipmentSlot.Shoes, piece.Slot);
            Assert.AreEqual("leather", piece.SetId);
            Assert.AreEqual("Leather", piece.SetDisplayName);
            Assert.AreEqual("boots", piece.PieceId);
            Assert.AreEqual(4, piece.Tier);
        }

        [Test]
        public void CostClimbsWithThePlus()
        {
            var byTier = Resolve(Minimal()).ToDictionary(p => p.Tier);

            Assert.AreEqual(30, byTier[0].Cost, "The authored base cost");
            Assert.AreEqual(30 + 20 * 7, byTier[7].Cost);
        }

        // Stats named as strings land in whichever of the two blocks owns
        // them, which is the whole reason one flat vocabulary exists.
        [Test]
        public void AbilityScoresAndStats_AreBothAuthorableInTheSameList()
        {
            var set = Minimal();
            set.pieces[0].baseStats = new[] { "constitution 2", "physicalResistance 6", "manaRegen 1" };
            set.pieces[0].topStats = new[] { "constitution 4", "physicalResistance 13", "manaRegen 5" };

            var top = Resolve(set).First(p => p.Tier == 10);

            Assert.AreEqual(4, top.AbilityScoreBonus.constitution);
            Assert.AreEqual(13, top.StatBonus.physicalResistance);
            Assert.AreEqual(5, top.StatBonus.manaRegen);
        }

        // A stat present at one end only starts (or finishes) at zero rather
        // than being an error — it is how "this piece only gets slow once it
        // is heavily reinforced" is written.
        [Test]
        public void AStatNamedAtOneEndOnly_StartsFromZero()
        {
            var set = Minimal();
            set.pieces[0].topStats = new[] { "dexterity 3", "speed 3", "magicalResistance 10" };

            var byTier = Resolve(set).ToDictionary(p => p.Tier);

            Assert.AreEqual(0, byTier[0].StatBonus.magicalResistance);
            Assert.AreEqual(10, byTier[10].StatBonus.magicalResistance);
        }

        // ---- rejections --------------------------------------------------

        private static List<string> Errors(params RawItemSetEntry[] sets)
        {
            Assert.IsFalse(ItemSetEntryResolver.TryResolveAll(sets, out _, out var errors));
            return errors;
        }

        [Test]
        public void AnUnknownStatName_IsRejectedAndSaysWhatIsAvailable()
        {
            var set = Minimal();
            set.pieces[0].baseStats = new[] { "luck 3" };

            var errors = Errors(set);

            StringAssert.Contains("luck", errors[0]);
            StringAssert.Contains("dexterity", errors[0], "The error should list the vocabulary it does accept");
            StringAssert.Contains("physicalResistance", errors[0]);
        }

        [Test]
        public void AMalformedStatLine_IsRejected()
        {
            var set = Minimal();
            set.pieces[0].baseStats = new[] { "dexterity" };

            StringAssert.Contains("<stat> <amount>", Errors(set)[0]);
        }

        [Test]
        public void ANonNumericAmount_IsRejected()
        {
            var set = Minimal();
            set.pieces[0].baseStats = new[] { "dexterity lots" };

            StringAssert.Contains("whole number", Errors(set)[0]);
        }

        // Both ends are parsed before bailing, so one rebuild shows every
        // typo instead of one per attempt.
        [Test]
        public void EveryBadStatName_IsReported_NotJustTheFirst()
        {
            var set = Minimal();
            set.pieces[0].baseStats = new[] { "luck 3" };
            set.pieces[0].topStats = new[] { "swagger 9" };

            Assert.AreEqual(2, Errors(set).Count);
        }

        [Test]
        public void APieceThatGrantsNothing_IsRejected()
        {
            var set = Minimal();
            set.pieces[0].baseStats = new string[0];
            set.pieces[0].topStats = new string[0];

            StringAssert.Contains("grants nothing", Errors(set)[0]);
        }

        [Test]
        public void AnUnknownSlot_IsRejected()
        {
            var set = Minimal();
            set.pieces[0].slot = "Backpack";

            StringAssert.Contains("Backpack", Errors(set)[0]);
        }

        [Test]
        public void TwoPiecesWithTheSameIdInOneSet_AreRejected()
        {
            var set = Minimal();
            set.pieces = new[] { set.pieces[0], set.pieces[0] };

            StringAssert.Contains("appears twice", Errors(set)[0]);
        }

        // The id of a generated item is "<set>_<piece>_p<plus>", so two sets
        // sharing an id would silently overwrite each other's assets.
        [Test]
        public void TwoSetsSharingAnId_AreRejected()
        {
            StringAssert.Contains("share the id", Errors(Minimal(), Minimal())[0]);
        }

        // The same piece id in DIFFERENT sets is fine and expected — leather
        // and silk both have gloves.
        [Test]
        public void ThesamePieceIdInTwoDifferentSets_IsFine()
        {
            var silk = Minimal();
            silk.id = "silk";
            silk.displayName = "Silk";

            var resolved = Resolve(Minimal(), silk);

            Assert.AreEqual(22, resolved.Count);
            CollectionAssert.Contains(resolved.Select(p => p.Id).ToList(), "silk_boots_p0");
            CollectionAssert.Contains(resolved.Select(p => p.Id).ToList(), "leather_boots_p0");
        }

        // A guard against a typo, not a design limit — but an unguarded
        // maxTier is a runaway asset generator.
        [Test]
        public void AnAbsurdMaxPlus_IsRejectedRatherThanGeneratingMillions()
        {
            StringAssert.Contains("ceiling", Errors(Minimal(maxTier: 100000))[0]);
        }

        [Test]
        public void ASetWithNoPieces_IsRejected()
        {
            var set = Minimal();
            set.pieces = new RawSetPiece[0];

            StringAssert.Contains("no pieces", Errors(set)[0]);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Stage;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace.Domain.Tests
{
    // The battle screen's tree, audited before any scene exists.
    //
    // This is the whole payoff of the construction layer stated as one test
    // class: the largest screen in the game builds, solves and audits clean at
    // four canvas aspects in milliseconds, with no Unity anywhere. In v1 the
    // equivalent confidence needed a scene rebuild and a screenshot, and the
    // rect collisions the audit finds here were found by playing the game.
    public class FightScreenTests
    {
        private static FightScreen Screen() => FightScreen.Build();

        private static SolvedNode Solve(UiVec frame = default) =>
            UiSolver.Solve(Screen().Root, frame.X > 0 ? frame : UiFrames.Reference);

        private static SolvedNode Find(SolvedNode root, string name) =>
            root.Name == name ? root : root.Descendants().FirstOrDefault(n => n.Name == name);

        private static UiRect RectOf(string name)
        {
            var node = Find(Solve(), name);
            Assert.IsNotNull(node, $"no node named '{name}' in the fight tree");
            return node.Rect;
        }

        // ---- the headline ------------------------------------------------------

        [Test]
        public void TheFightScreenAuditsCleanAtEveryFrame()
        {
            var errors = UiAudit.RunAllFrames(Screen().Root).ToList();

            Assert.IsEmpty(errors,
                "The fight tree failed its own audit:\n" +
                string.Join("\n", errors.Select(e => $"  [{e.Check}] {UiFrames.Describe(e.Frame)} {e.Message}")));
        }

        [Test]
        public void EveryExemptionStatesAReason()
        {
            // The escape hatches are only worth having while each one is
            // greppable and reviewable. UiNode.AllowOverlap already throws on an
            // empty reason; this proves the SCREEN actually uses them that way
            // rather than having found some way around it.
            var exempted = AllNodes(Screen().Root)
                .Where(n => n.AllowOverlapReason != null || n.AllowOverflowReason != null)
                .ToList();

            Assert.IsNotEmpty(exempted, "a screen this dense with deliberate layering should have exemptions");

            foreach (var node in exempted)
            {
                if (node.AllowOverlapReason != null)
                {
                    Assert.Greater(node.AllowOverlapReason.Length, 20,
                        $"'{node.Name}' overlap reason is too short to be a reason: '{node.AllowOverlapReason}'");
                }

                if (node.AllowOverflowReason != null)
                {
                    Assert.Greater(node.AllowOverflowReason.Length, 20,
                        $"'{node.Name}' overflow reason is too short to be a reason: '{node.AllowOverflowReason}'");
                }
            }
        }

        [Test]
        public void OneSiblingCarryingTheExemptionIsEnoughForThePair()
        {
            // Asked explicitly by the plan, because it decides how many
            // exemptions this screen needs: the stages overlap most of the HUD,
            // and if BOTH members of every pair needed the flag, every plate,
            // verb and column would have to carry one -- which would make the
            // flag meaningless.
            //
            // Pinned as a property of the audit, not assumed from reading it.
            var marked = Ui.Solid("Marked", "#ffffff", new UiVec(100f, 100f), Place.At(0f, 0f))
                .AllowOverlap("this one states why it sits on its neighbour, and its neighbour does not");
            var bare = Ui.Solid("Bare", "#ffffff", new UiVec(100f, 100f), Place.At(10f, 0f));
            var pair = Ui.Panel("Pair", UiSize.Fixed(400f, 400f), marked, bare);

            var errors = UiAudit.RunAllFrames(pair)
                .Where(e => e.Check == UiAuditCheck.SiblingOverlap)
                .ToList();

            Assert.IsEmpty(errors, "one exemption should clear the pair; both members needing it would make the flag noise");
        }

        // ---- capacities come from ONE place --------------------------------------

        [Test]
        public void TheTreeIsSizedFromFightHudSpec_NotFromRestatedNumbers()
        {
            var screen = Screen();

            Assert.AreEqual(FightHudSpec.InitiativeSlots, screen.InitiativeIcons.Count);
            Assert.AreEqual(FightHudSpec.EnemyPlates, screen.EnemyPlates.Count);
            Assert.AreEqual(FightHudSpec.WoolPips, screen.WoolPips.Count);
            Assert.AreEqual(FightHudSpec.Verbs, screen.VerbButtons.Count);
            Assert.AreEqual(FightHudSpec.DamagePopups, screen.DamagePopups.Count);
            Assert.AreEqual(FightHudSpec.DetailStatRows, screen.DetailStatKeys.Count);
            Assert.AreEqual(FightHudSpec.StageSlotsPerSide, screen.EnemySlots.Count);
            Assert.AreEqual(FightHudSpec.StageSlotsPerSide, screen.PartySlots.Count);
            Assert.AreEqual(FightSubmenuLayout.MaxRows, screen.SubmenuRows.Count);
        }

        [Test]
        public void EveryParallelRefListIsTheSameLength()
        {
            // The shipped v1 Store bug in miniature: a strip sized from one
            // collection and filled from another. Here the lists ARE the
            // declaration, so this can only fail if a build path forgot one.
            var s = Screen();

            AssertSameLength("initiative", s.InitiativeIcons, s.InitiativeRings, s.InitiativeLabels);
            AssertSameLength("enemy plates", s.EnemyPlates, s.EnemyPlateNames, s.EnemyPlateHps,
                s.EnemyPlateHpFills, s.EnemyPlateTags, s.EnemyPlateReticles);
            AssertSameLength("verbs", s.VerbButtons, s.VerbLabels, s.VerbCarets);
            AssertSameLength("submenu", s.SubmenuRows, s.SubmenuMarks, s.SubmenuNames, s.SubmenuMetas, s.SubmenuCosts);
            AssertSameLength("detail stats", s.DetailStatKeys, s.DetailStatValues);
            AssertSameLength("popups", s.DamagePopups, s.DamagePopupLabels);
            AssertSameLength("enemy stage", s.EnemySlots, s.EnemySprites, s.EnemyHitFlashes,
                s.EnemyNameplates, s.EnemyFootShadows);
            AssertSameLength("party stage", s.PartySlots, s.PartySprites, s.PartyHitFlashes,
                s.PartyNameplates, s.PartyFootShadows);
        }

        [Test]
        public void EveryBoundRefResolvedToARealNode()
        {
            // A NodeRef left at default() is the v2 shape of v1's silently-null
            // SetField: it compiles, and the wiring pass then binds nothing.
            var s = Screen();
            var lists = new Dictionary<string, List<NodeRef>>
            {
                { "InitiativeIcons", s.InitiativeIcons }, { "InitiativeRings", s.InitiativeRings },
                { "InitiativeLabels", s.InitiativeLabels },
                { "EnemySlots", s.EnemySlots }, { "EnemySprites", s.EnemySprites },
                { "EnemyHitFlashes", s.EnemyHitFlashes }, { "EnemyNameplates", s.EnemyNameplates },
                { "EnemyFootShadows", s.EnemyFootShadows },
                { "PartySlots", s.PartySlots }, { "PartySprites", s.PartySprites },
                { "PartyHitFlashes", s.PartyHitFlashes }, { "PartyNameplates", s.PartyNameplates },
                { "PartyFootShadows", s.PartyFootShadows },
                { "EnemyPlates", s.EnemyPlates }, { "VerbButtons", s.VerbButtons },
                { "SubmenuRows", s.SubmenuRows }, { "WoolPips", s.WoolPips },
                { "DamagePopups", s.DamagePopups },
            };

            foreach (var pair in lists)
            {
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    Assert.IsTrue(pair.Value[i].IsValid, $"{pair.Key}[{i}] was never assigned a node");
                }
            }

            Assert.IsTrue(s.PartyPlate.IsValid);
            Assert.IsTrue(s.SubmenuColumn.IsValid);
            Assert.IsTrue(s.DetailColumn.IsValid);
            Assert.IsTrue(s.TargetPrompt.IsValid);
            Assert.IsTrue(s.SpellVfx.IsValid);
            Assert.IsTrue(s.ContinueButton.IsValid);
        }

        // ---- geometry pins, against v1 ---------------------------------------------

        [Test]
        public void ThePanelIsTheReferenceCanvas()
        {
            var root = Solve();
            Assert.AreEqual(1920f, root.Rect.Width, 0.01f);
            Assert.AreEqual(1080f, root.Rect.Height, 0.01f);
        }

        [Test]
        public void ThePartyPlateSitsWhereV1PutIt()
        {
            var rect = RectOf("PartyPlate");
            Assert.AreEqual(-694f, rect.Centre.X, 0.01f);
            Assert.AreEqual(-392f, rect.Centre.Y, 0.01f);
            Assert.AreEqual(452f, rect.Width, 0.01f);
            Assert.AreEqual(216f, rect.Height, 0.01f);
        }

        [Test]
        public void TheEnemyPlatesStepDownByTheirOwnPitch()
        {
            float first = RectOf("EnemyPlate0").Centre.Y;
            float second = RectOf("EnemyPlate1").Centre.Y;
            float third = RectOf("EnemyPlate2").Centre.Y;

            Assert.AreEqual(332f, first, 0.01f);
            Assert.AreEqual(116f, first - second, 0.01f, "plate height plus a 12px gutter");
            Assert.AreEqual(first - second, second - third, 0.01f, "and the same step again");
            Assert.AreEqual(720f, RectOf("EnemyPlate0").Centre.X, 0.01f);
        }

        [Test]
        public void TheVerbsRunBottomUpFromTheCommandLine()
        {
            float attack = RectOf("Verb0").Centre.Y;
            float skill = RectOf("Verb1").Centre.Y;

            Assert.AreEqual(FightSubmenuLayout.CommandBottom + 26f, attack, 0.01f);
            Assert.Greater(skill, attack, "ATTACK sits nearest the cursor, at the bottom");
            Assert.AreEqual(62f, skill - attack, 0.01f);
            Assert.AreEqual(-286f, RectOf("Verb0").Centre.X, 0.01f);
        }

        [Test]
        public void TheSubmenuRowsComeFromTheSharedLayoutFunction()
        {
            // The single most important binding on this screen: the same
            // function the runtime controller calls to RE-anchor these rows is
            // the one that placed them here. v1 had two hand-mirrored copies.
            int count = FightSubmenuLayout.MaxRows;
            for (int i = 0; i < count; i++)
            {
                Assert.AreEqual(FightSubmenuLayout.RowY(count, i), RectOf($"CharacterSkill{i}").Centre.Y, 0.01f);
            }
        }

        [Test]
        public void TheBottomRowLandsJustAboveBack()
        {
            // What "anchored to the bottom" buys: the last row sits in the same
            // slot whether the actor has two skills or eight.
            float back = RectOf("SubmenuBack").Top;
            float lastRow = RectOf($"CharacterSkill{FightSubmenuLayout.MaxRows - 1}").Bottom;

            Assert.Greater(lastRow, back, "rows stack above BACK, never through it");
            Assert.Less(lastRow - back, 8f, "and immediately above it, with no dead space");
        }

        [Test]
        public void TheStagesAreOneMirroredFrame()
        {
            var enemy = RectOf("EnemyStage");
            var party = RectOf("PartyStage");

            Assert.AreEqual(enemy.Centre.X, party.Centre.X, 0.01f);
            Assert.AreEqual(enemy.Centre.Y, party.Centre.Y, 0.01f);
            Assert.AreEqual(FightStageAnchors.StageSize.X, enemy.Width, 0.01f);
            Assert.AreEqual(FightStageAnchors.StageSize.Y, enemy.Height, 0.01f);
        }

        [Test]
        public void SlotZeroIsNearestAndDrawnInFront()
        {
            var root = Solve();
            var stage = Find(root, "EnemyStage");
            var order = stage.Children.Select(c => c.Name).ToList();

            // Declaration order IS the painter's algorithm: uGUI draws later
            // siblings on top, so the nearest slot must be declared LAST.
            Assert.AreEqual("Enemy2Slot", order[0], "farthest first");
            Assert.AreEqual("Enemy0Slot", order[order.Count - 1], "nearest last, so it paints over the others");
        }

        [Test]
        public void ASlotSitsWhereTheDepthCurveSaysItDoes()
        {
            int count = FightHudSpec.StageSlotsPerSide;
            for (int slot = 0; slot < count; slot++)
            {
                var expected = FightStageAnchors.SlotOffset(slot, count, mirrored: false);
                var rect = RectOf($"Enemy{slot}Slot");

                // Bottom pivot: the offset positions the GROUND LINE, which is
                // what makes depth scaling grow a figure upward from the floor
                // rather than around its middle.
                Assert.AreEqual(expected.X, rect.Centre.X, 0.01f, $"slot {slot} x");
                Assert.AreEqual(expected.Y, rect.Bottom, 0.01f, $"slot {slot} ground line");
            }
        }

        [Test]
        public void ThePartySideIsTheEnemySideMirroredInXOnly()
        {
            // Mirroring only X keeps the ground line identical on both sides,
            // which is what makes the two halves read as one floor.
            for (int slot = 0; slot < FightHudSpec.StageSlotsPerSide; slot++)
            {
                var enemy = RectOf($"Enemy{slot}Slot");
                var party = RectOf($"Party{slot}Slot");

                Assert.AreEqual(-enemy.Centre.X, party.Centre.X, 0.01f);
                Assert.AreEqual(enemy.Bottom, party.Bottom, 0.01f, "one floor, not two platforms");
            }
        }

        [Test]
        public void ASlotIsScaledByTheDepthCurveTimesTheGlobalShrink()
        {
            var root = Solve();
            for (int slot = 0; slot < FightHudSpec.StageSlotsPerSide; slot++)
            {
                float expected = FightStageAnchors.SlotScale(slot, FightHudSpec.StageSlotsPerSide);
                var node = Find(root, $"Enemy{slot}Slot");
                Assert.AreEqual(expected, node.Scale.X, 0.0001f);
                Assert.AreEqual(expected, node.Scale.Y, 0.0001f);
            }
        }

        [Test]
        public void TheInitiativeTrackerIsPinnedToTheTopLeftCorner()
        {
            // Pinned, not centred: it has to keep its relationship to that
            // corner when the canvas aspect changes, which is one of the shapes
            // v1's centre-anchor-only helper simply could not express.
            var reference = Find(UiSolver.Solve(Screen().Root, UiFrames.Reference), "InitiativeTracker").Rect;
            var wide = Find(UiSolver.Solve(Screen().Root, UiFrames.UltraWide), "InitiativeTracker").Rect;

            float refInsetLeft = reference.Left - (-UiFrames.Reference.X / 2f);
            float wideInsetLeft = wide.Left - (-UiFrames.UltraWide.X / 2f);

            Assert.AreEqual(28f, refInsetLeft, 0.01f);
            Assert.AreEqual(refInsetLeft, wideInsetLeft, 0.01f, "the same 28px from the left edge at 21:9");
        }

        [Test]
        public void TheInitiativeSlotsAreSpacedByTheContainer_NotByAnIndexedFormula()
        {
            float a = RectOf("InitiativeSlot0").Centre.X;
            float b = RectOf("InitiativeSlot1").Centre.X;

            Assert.AreEqual(FightStageAnchors.InitiativeIconSize + FightStageAnchors.InitiativeIconGap,
                b - a, 0.01f);
        }

        [Test]
        public void TheWoolPipsFitInsideThePlate()
        {
            var row = RectOf("WoolRow");
            for (int i = 0; i < FightHudSpec.WoolPips; i++)
            {
                var pip = RectOf($"WoolPip{i}");
                Assert.IsTrue(row.Contains(pip), $"WoolPip{i} escapes the wool row");
            }
        }

        [Test]
        public void PoolMembersAreTheOnlyRuntimePositionedElements()
        {
            // The exemption's whole boundary, asserted. If a third pool ever
            // appears, this test is where the decision gets re-made rather than
            // where it quietly widens.
            var pools = AllNodes(Screen().Root).Where(n => n.Kind == UiNodeKind.Pool).Select(n => n.Name).ToList();

            CollectionAssert.AreEquivalent(new[] { "SpellVfx", "DamagePopups" }, pools);
        }

        // ---- the tree does not restate a string ------------------------------------

        [Test]
        public void EveryDeclaredLabelDrawsAManifestStringOrNamedContent()
        {
            // Ui.Label has no string overload, so this cannot fail by a literal
            // sneaking in. What it CAN catch is a FromContent("...") smuggling
            // authored copy past the manifest -- content placeholders should be
            // blank, not prose.
            var suspicious = AllNodes(Screen().Root)
                .Where(n => n.Text.IsValid && !UiStrings.All.Any(s => ReferenceEquals(s.Key, n.Text.Key)))
                .Where(n => !string.IsNullOrWhiteSpace(n.Text.Template))
                .Select(n => $"{n.Name}: '{n.Text.Template}'")
                .ToList();

            Assert.IsEmpty(suspicious,
                "these labels carry authored copy outside UiStrings:\n  " + string.Join("\n  ", suspicious));
        }

        // ---- helpers -----------------------------------------------------------------

        private static IEnumerable<UiNode> AllNodes(UiNode root)
        {
            yield return root;
            foreach (var child in root.Children)
            {
                foreach (var deeper in AllNodes(child)) yield return deeper;
            }
        }

        private static void AssertSameLength(string what, params List<NodeRef>[] lists)
        {
            for (int i = 1; i < lists.Length; i++)
            {
                Assert.AreEqual(lists[0].Count, lists[i].Count,
                    $"{what}: parallel ref list {i} is a different length from the first");
            }
        }
    }
}

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
            Assert.AreEqual(FightSubmenuLayout.PoolSize, screen.SubmenuRows.Count);
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
            AssertSameLength("submenu", s.SubmenuRows, s.SubmenuMarks, s.SubmenuNames);
            AssertSameLength("detail stats", s.DetailStatKeys, s.DetailStatValues);
            AssertSameLength("popups", s.DamagePopups, s.DamagePopupLabels);
            AssertSameLength("enemy stage", s.EnemySlots, s.EnemySprites, s.EnemyHitFlashes,
                s.EnemyNameplates, s.EnemyFootShadows, s.EnemyFootGlows);
            AssertSameLength("party stage", s.PartySlots, s.PartySprites, s.PartyHitFlashes,
                s.PartyNameplates, s.PartyFootShadows, s.PartyFootGlows);

            // The roster card grew from one readout to four in 2026-09-09's
            // column pass, and RefreshRoster indexes all of them off the same
            // loop counter -- exactly the shape the Store bug had.
            AssertSameLength("roster", s.RosterPlates, s.RosterNames, s.RosterHpValues,
                s.RosterHpFills, s.RosterMpValues, s.RosterMpFills, s.RosterSignatures);
        }

        // C3's review: a runtime enemy name (the box is a fixed 72x20, UiString.
        // Runtime, so no authored string exists for UiTextFitAudit to measure
        // against) must never be allowed to wrap onto a second line the box
        // has no height for. Truncated() is the fix -- pinned here rather than
        // only by the render capture, because the capture can only show that
        // ONE particular name happened to fit; this proves every plate's name
        // label carries the setting regardless of what content lands in it.
        [Test]
        public void EveryEnemyPlateNameIsTruncatedNotWrapped()
        {
            var s = Screen();

            foreach (var nameRef in s.EnemyPlateNames)
            {
                Assert.IsTrue(nameRef.Node.Truncates,
                    $"'{nameRef.Node.Name}' can still word-wrap a long runtime name onto a second line");
            }
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
                { "EnemyFootShadows", s.EnemyFootShadows }, { "EnemyFootGlows", s.EnemyFootGlows },
                { "PartySlots", s.PartySlots }, { "PartySprites", s.PartySprites },
                { "PartyHitFlashes", s.PartyHitFlashes }, { "PartyNameplates", s.PartyNameplates },
                { "PartyFootShadows", s.PartyFootShadows }, { "PartyFootGlows", s.PartyFootGlows },
                { "EnemyPlates", s.EnemyPlates }, { "VerbButtons", s.VerbButtons },
                { "SubmenuRows", s.SubmenuRows }, { "WoolPips", s.WoolPips },
                { "DamagePopups", s.DamagePopups },
                { "SpellVfx", s.SpellVfx }, { "SpellVfxNext", s.SpellVfxNext },
            };

            foreach (var pair in lists)
            {
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    Assert.IsTrue(pair.Value[i].IsValid, $"{pair.Key}[{i}] was never assigned a node");
                }
            }

            Assert.IsTrue(s.PartyPlate.IsValid);
            Assert.IsTrue(s.PartyPlateArt.IsValid);
            Assert.IsTrue(s.SubmenuColumn.IsValid);
            Assert.IsTrue(s.DetailColumn.IsValid);
            Assert.IsTrue(s.DetailDamageType.IsValid);
            Assert.IsTrue(s.TargetPrompt.IsValid);
            Assert.IsTrue(s.SpellVfxPool.IsValid);
            Assert.IsTrue(s.SpellGroundVfxPool.IsValid);
            Assert.AreEqual(FightHudSpec.SpellGroundRenderers, s.SpellGroundVfx.Count,
                "the shared ground band was never filled");
            Assert.AreEqual(FightHudSpec.SpellGroundRenderers, s.SpellGroundVfxNext.Count,
                "its dissolve layers were never filled");
            Assert.AreEqual(FightHudSpec.SpellLayerRenderers, s.SpellVfx.Count);
            Assert.IsTrue(s.SpellParticlePool.IsValid, "the drops were given no pool node");
            Assert.AreEqual(FightHudSpec.SpellParticles, s.SpellParticles.Count);
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
            // LEFT EDGE ONLY, now -- the one thing that has stayed fixed
            // through both B1 and B2. B1 moved the BOTTOM off a flat -500
            // (the canvas's own 40px HUD margin, with no reference to the
            // verb column at all) onto FightSubmenuLayout.VisibleBottomLine
            // instead (see its own header). B2 then shrank WIDTH/HEIGHT for
            // the cozy-plate pass (452x228.28 -> 380x191.92, still the 2:1
            // aspect -- see BuildPartyPlate's own header) and re-solved the
            // bottom against the SAME VisibleBottomLine at the new height,
            // which is why the numbers below moved a second time.
            //
            // History: v1/A1 -500 (rect) / -385.858 (centre); B1 -486.972 /
            // -372.830 (bottom moved onto the verb column's visible line);
            // B2 -486.703 / -390.743 (bottom re-solved at the new, shorter
            // height -- 0.27px from B1's own number, since VisibleBottomLine
            // itself did not move and only the plate's own height did);
            // 2026-09-07 -486.627 / -391.627, the kit repin. TWO numbers moved
            // there and neither is a layout decision: the 2:1 art is at a true
            // 2.0 now (it measured 1.98), so 380 wide is 190 tall rather than
            // 191.92; and the Row6x1 plate's own bottom halo pad moved
            // 0.0138 -> 0.0117 of the verb row's height, which is what shifts
            // VisibleBottomLine itself by 0.11px.
            var rect = RectOf("PartyPlate");
            Assert.AreEqual(-920f, rect.Centre.X - rect.Width * 0.5f, 0.01f, "the left edge must not move");
            Assert.AreEqual(-391.627f, rect.Centre.Y, 0.01f);
            Assert.AreEqual(380f, rect.Width, 0.01f);
            Assert.AreEqual(190f, rect.Height, 0.01f);
            Assert.AreEqual(-486.627f, rect.Centre.Y - rect.Height * 0.5f, 0.01f,
                "the bottom edge is solved from FightSubmenuLayout.VisibleBottomLine, not a flat -500");
        }

        // The party plate's container theme/ratio and content inset are
        // covered by KitContainerPlacementTests, not repeated here.

        [Test]
        public void ThePartyPlateChildrenRideInsideTheFrame()
        {
            // If the HP/MP rows or the wool meter were siblings of the frame
            // rather than descendants, moving or hiding the plate would leave
            // them stranded next to it instead of with it.
            var plate = Walk(Screen().Root).First(n => n.Name == "PartyPlate");
            var names = Walk(plate).Select(n => n.Name).ToList();

            CollectionAssert.Contains(names, "PartyHpBar");
            CollectionAssert.Contains(names, "PartyMpBar");
            CollectionAssert.Contains(names, "WoolRow");
        }

        // TWO ABREAST, NOT A COLUMN OF THREE. Half-width plates in two columns
        // put the same three readouts in two rows of the same total width, and
        // the height that buys goes back to the stage they describe.
        [Test]
        public void TheEnemyPlatesFillTwoColumnsBeforeStartingASecondRow()
        {
            var first = RectOf("EnemyPlate0").Centre;
            var second = RectOf("EnemyPlate1").Centre;
            var third = RectOf("EnemyPlate2").Centre;

            Assert.AreEqual(first.Y, second.Y, 0.01f, "the first two sit side by side");

            // 236, UP FROM 216 (owner's HQ-kit instruction, 2026-09-07):
            // PlateW grew 200 to 220 so the Crimson TwoByOne frame's own
            // content inset leaves the icon/name/tag/hp row the same usable
            // width it always had -- see PlateW's own comment. The 16px
            // gutter itself did not move; the plate width inside it did.
            Assert.AreEqual(236f, second.X - first.X, 0.01f, "plate width plus a 16px gutter");

            Assert.AreEqual(first.X, third.X, 0.01f, "the third starts the next row under the first");

            // 122, UP FROM 76: PlateH grew 64 to 110 (owner's HQ-kit
            // instruction, 2026-09-07 -- ValidateContainerAspect at the new
            // PlateW=220, see PlateH's own comment). The 12px gutter itself
            // is unchanged; the plate height inside it grew.
            Assert.AreEqual(122f, first.Y - third.Y, 0.01f, "plate height plus a 12px gutter");

            // 392 still, and the block's RIGHT edge is what is pinned -- it sits
            // against the same margin the heading and the standing-count do.
            Assert.AreEqual(392f, first.Y, 0.01f);
            Assert.AreEqual(920f, RectOf("EnemyPlate1").Centre.X + RectOf("EnemyPlate1").Width * 0.5f, 0.01f);

            // The plates are the stage's ceiling: the tallest actor needs 300
            // above the front slot's ground line, so its head reaches 72, and
            // the middle slot's reaches 89 while still just crossing the plates
            // in x. Two rows of 110 (was 64, see PlateH's own comment) still
            // clear both -- by less margin than before, since the taller
            // plate and the wider gutter both push this edge down. 55, not
            // 32: half of the new 110-tall plate, not the old 64.
            //
            // NOT RE-VERIFIED AGAINST tools/measure_stage.py as part of the
            // HQ-kit conversion -- this Domain-only check uses the same
            // hand-derived 89f+12f the old assertion did, which is a real
            // gap this test cannot close on its own.
            Assert.Greater(third.Y - 55f, 89f + 12f,
                "the bottom plate has dropped back onto an actor's head - see tools/measure_stage.py");
        }

        // Regression for the blank 12px band Phase 3 left behind: retiring
        // EnemyStatusLine to BRK-only (FightHudModel.EnemyStatusLine's own
        // header) freed the middle row, and the fix folds Tags INTO the name
        // row rather than leaving the gap where the status line used to sit.
        [Test]
        public void TheEnemyPlateTopRowAndBarAreCentredWithNoBlankBand()
        {
            var plate = RectOf("EnemyPlate0");
            var name = RectOf("EnemyPlate0Name");
            var hp = RectOf("EnemyPlate0Hp");
            var tags = RectOf("EnemyPlate0Tags");
            var bar = RectOf("EnemyPlate0Bar");

            // Name, HP and the BRK tag are one row now, not three.
            Assert.AreEqual(name.Centre.Y, hp.Centre.Y, 0.01f, "name and HP share the top row");
            Assert.AreEqual(name.Centre.Y, tags.Centre.Y, 0.01f,
                "BRK now lives IN the name row - it is not a row of its own any more");

            // Symmetric margins to the plate's own top/bottom edges is what
            // "no blank band" actually means: the old layout left an unequal
            // gap where the status line used to sit. hp is the taller of the
            // two top-row boxes, so its edge is the row's real top edge.
            float topMargin = plate.Top - hp.Top;
            float bottomMargin = bar.Bottom - plate.Bottom;
            Assert.AreEqual(topMargin, bottomMargin, 0.01f,
                "the top row and the bar should be centred as one block, not offset toward one edge");

            // 34, UP FROM 11: PlateH grew 64 to 110 (owner's HQ-kit
            // instruction, 2026-09-07 -- see PlateH's own comment), and
            // topY/barY did not move with it, so the block that used to sit
            // with 11px of margin now sits with 34 -- still centred (the
            // assertion above), just inside a taller frame.
            Assert.AreEqual(34f, topMargin, 0.01f);

            // A deliberate small gap between the two rows, not the old blank
            // band (which was the full retired status line's own height).
            Assert.AreEqual(8f, hp.Bottom - bar.Top, 0.01f);

            // The tag sits strictly between the name and the HP value at
            // every audited aspect - it cannot collide with either.
            Assert.LessOrEqual(name.Right, tags.Left, "BRK must not overlap the name");
            Assert.LessOrEqual(tags.Right, hp.Left, "BRK must not overlap the HP value");
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
            // + (FrameContentCentreY - ContainerCentreY): the rows are
            // reparented under the Violet 3:4 frame's content inset now
            // (balance-bot 2026-09-02), not a bare Panel at the old
            // ContainerCentreY -- see that constant's own comment. RowY
            // itself is untouched.
            float shift = FightSubmenuLayout.FrameContentCentreY - FightSubmenuLayout.ContainerCentreY;
            int count = FightSubmenuLayout.PoolSize;
            for (int i = 0; i < count; i++)
            {
                Assert.AreEqual(FightSubmenuLayout.RowY(count, i) + shift,
                    RectOf($"CharacterSkill{i}").Centre.Y, 0.01f);
            }
        }

        [Test]
        public void TheBottomRowLandsJustAboveBack()
        {
            // What "anchored to the bottom" buys: the last row sits in the same
            // slot whether the actor has two skills or eight.
            float back = RectOf("SubmenuBack").Top;
            float lastRow = RectOf($"CharacterSkill{FightSubmenuLayout.PoolSize - 1}").Bottom;

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

            // FILTERED TO SLOT NODES, not stage.Children as a whole: the
            // status rows (PLAN_STATUS_EFFECT_UI) are appended after all
            // three slots, so "last child of the stage" no longer means
            // "nearest slot" -- it means the last status row. The claim this
            // test actually makes is about SLOTS painting over each other,
            // which the status rows do not participate in at all (they sit
            // below every slot's feet, on clear floor -- see
            // NoAlwaysVisiblePanelStandsInFrontOfAFigureSFeet).
            var order = stage.Children.Where(c => c.Name.EndsWith("Slot")).Select(c => c.Name).ToList();

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
        public void ThePartySideIsTheEnemySideMirroredInXPlusTheRetreat()
        {
            // C4: X is no longer a pure mirror -- the party side stands
            // FightStageAnchors.PartyRetreat further back than the enemy
            // side's mirror image. The ground line (Y) is still identical
            // on both sides, which is what makes the two halves read as
            // one floor.
            for (int slot = 0; slot < FightHudSpec.StageSlotsPerSide; slot++)
            {
                var enemy = RectOf($"Enemy{slot}Slot");
                var party = RectOf($"Party{slot}Slot");

                Assert.AreEqual(-(enemy.Centre.X + FightStageAnchors.PartyRetreat), party.Centre.X, 0.01f);
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
            // The exemption's whole boundary, asserted. If a FIFTH pool ever
            // appears, this test is where the decision gets re-made rather than
            // where it quietly widens.
            //
            // SpellGroundVfx is the third, and the decision was made rather
            // than inherited: it is runtime-positioned in exactly the way the
            // other two are -- its position AND its size come from the slots
            // the living enemies actually occupy, which is precisely what
            // UiSolver cannot compute -- and it could not be a member of the
            // SpellVfx pool beside it, because depth in uGUI is sibling order
            // and this one has to draw BEHIND the racks that one draws over.
            //
            // SpellParticles is the fourth, and its argument is the same one
            // read from the other side: a droplet's position comes from a
            // closed-form ballistic path evaluated per frame, which nothing
            // static can solve. It is a separate NODE from SpellVfx rather than
            // more members of it because every member of that pool carries a
            // dissolve child and a renderer component -- dead weight on a drop
            // that draws one still and never cross-fades -- and it sits between
            // that pool and the damage numbers, which is its whole draw order.
            var pools = AllNodes(Screen().Root).Where(n => n.Kind == UiNodeKind.Pool).Select(n => n.Name).ToList();

            CollectionAssert.AreEquivalent(
                new[] { "SpellVfx", "SpellParticles", "SpellGroundVfx", "DamagePopups" }, pools);
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

        // ---- the thing the audit structurally cannot see ------------------------------

        // How much of a slot, measured up from its own ground line, has to stay
        // clear of the HUD. The contact ring straddles the line (8 below it),
        // the bloom sits under that, and a figure's feet and lower legs occupy
        // the rest -- so a panel crossing this band stands in front of the very
        // thing that makes an actor read as touching the floor.
        private const float FootBand = 40f;
        private const float RingDrop = 8f;

        [Test]
        public void NoAlwaysVisiblePanelStandsInFrontOfAFigureSFeet()
        {
            // A1 CANNOT DO THIS ONE, and saying why matters because the build
            // gate looks green either way.
            //
            // The stage frame carries an AllowOverlap, correctly -- it really
            // does overlap the other stage and every panel. But A1 skips a pair
            // the moment EITHER side carries a reason, so one exemption written
            // on the frame silenced every actor-versus-panel pair on the
            // screen. v2 shipped with Near.Y at -300, standing all three
            // front-row figures shin-deep in the HUD, and nothing failed.
            //
            // Declared geometry only, like the rest of the audit. A slot's
            // 320x200 is a placeholder the runtime swaps for the real sprite
            // canvas, which is WIDER for every actor in the manifest (the
            // golem's is 525), so this under-reports rather than over-, and
            // tools/measure_stage.py is what checks against the art itself.
            var root = Solve();
            var occluders = PanelsDrawnOverTheStage(root).ToList();

            Assert.IsNotEmpty(occluders,
                "no HUD panels were collected at all - this test would pass vacuously");

            var offenders = new List<string>();

            foreach (bool mirrored in new[] { true, false })
            {
                for (int slot = 0; slot < FightHudSpec.StageSlotsPerSide; slot++)
                {
                    var band = FootBandOf(slot, mirrored);

                    foreach (var panel in occluders)
                    {
                        if (!panel.Footprint.Overlaps(band)) continue;

                        var over = panel.Footprint.OverlapExtent(band);
                        offenders.Add(
                            $"{(mirrored ? "Party" : "Enemy")} slot {slot}'s feet are behind " +
                            $"'{panel.Path}' by {over.X:0}x{over.Y:0}px");
                    }
                }
            }

            CollectionAssert.IsEmpty(offenders,
                "the ground-contact band is drawn under the HUD - raise FightStageAnchors.Near/Far, " +
                "or lower the panel:\n  " + string.Join("\n  ", offenders));
        }

        // The band a slot's ground contact occupies, in canvas coordinates.
        private static UiRect FootBandOf(int slot, bool mirrored)
        {
            var offset = FightStageAnchors.SlotOffset(slot, FightHudSpec.StageSlotsPerSide, mirrored);
            float scale = FightStageAnchors.SlotScale(slot, FightHudSpec.StageSlotsPerSide);

            // 320 is the slot's declared placeholder width; scale is the depth
            // curve times the global shrink, exactly as the emitter applies it.
            float width = 320f * scale;
            float height = FootBand + RingDrop;

            // The slot's pivot is (0.5, 0), so offset.Y IS the ground line: the
            // band runs from RingDrop below it to FootBand above.
            return new UiRect(
                new UiVec(offset.X, offset.Y + FootBand * 0.5f - RingDrop * 0.5f),
                new UiVec(width, height));
        }

        // Everything that DRAWS, is visible from the first frame, and is
        // declared AFTER both stages -- which is what "in front of" means in
        // uGUI, where sibling order is the only stacking rule.
        private static IEnumerable<SolvedNode> PanelsDrawnOverTheStage(SolvedNode root)
        {
            // The stages live one level deeper than root, inside the
            // "FightHud" nested canvas (see FightScreen.Build), without
            // that changing their RELATIVE order against everything that
            // occludes them: FightHud's own children still run
            // stage-then-HUD-panels in the same sequence root's children
            // once did. Scan whichever level actually holds one of the two
            // stage nodes, so this test's real invariant -- nothing
            // declared after the stage stands on a figure's feet --
            // survives the nesting.
            //
            // EXACT NAME, not EndsWith("Stage"), so a future sibling whose
            // name merely ends that way cannot be mistaken for the stage
            // and silently move the whole scan to the wrong scope.
            bool IsUiStage(string name) => name == "EnemyStage" || name == "PartyStage";

            var scope = root;
            if (!scope.Children.Any(c => IsUiStage(c.Name)))
            {
                var hud = root.Children.FirstOrDefault(c => c.Name == "FightHud");
                if (hud != null) scope = hud;
            }

            int lastStage = -1;
            for (int i = 0; i < scope.Children.Count; i++)
            {
                if (IsUiStage(scope.Children[i].Name)) lastStage = i;
            }

            Assert.Greater(lastStage, -1, "no stage found in the fight tree");

            for (int i = lastStage + 1; i < scope.Children.Count; i++)
            {
                foreach (var node in DrawnAndVisible(scope.Children[i]))
                {
                    yield return node;
                }
            }
        }

        // A subtree that starts inactive contributes nothing: it is not on
        // screen until something turns it on, and the submenu, the detail
        // column, Continue, the Reckoning and the defeat screen are all in that
        // category. Descending into one would assert against a layout that
        // never coexists with the stage it is being compared to.
        private static IEnumerable<SolvedNode> DrawnAndVisible(SolvedNode node)
        {
            if (node.Source != null && node.Source.StartInactive) yield break;

            if (Draws(node)) yield return node;

            foreach (var child in node.Children)
            {
                foreach (var deeper in DrawnAndVisible(child)) yield return deeper;
            }
        }

        // A Panel is an invisible grouping rect UNLESS it was given a colour,
        // which is the same rule UiEmitter follows when deciding whether to add
        // an Image. Labels are excluded deliberately: text over a foot is
        // untidy, but it is not the opaque plate this test exists to find.
        private static bool Draws(SolvedNode node)
        {
            if (node.Source == null) return false;

            switch (node.Kind)
            {
                case UiNodeKind.Sprite:
                case UiNodeKind.Solid:
                case UiNodeKind.Button:
                    return true;
                case UiNodeKind.Panel:
                    return !string.IsNullOrEmpty(node.Source.ColorHex);
                default:
                    return false;
            }
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
        // NOTHING ON THE STAGE MAY TAKE A CLICK EXCEPT THE INTENT BADGE.
        //
        // The stage is declared after the command UI and therefore sits on top
        // of it. A figure whose Image keeps raycastTarget is a full rectangular
        // blocker -- an Image raycasts against its RECT, not its alpha -- so
        // with the skill list open against three monsters, the rat covered
        // three of its rows and swallowed every click on them.
        //
        // Nothing else could catch this. UiAudit checks overlap between
        // SIBLINGS and the stage and the command column are not siblings, and
        // every PlayMode test clicks through button.onClick.Invoke(), which
        // bypasses the EventSystem and so cannot see a blocker at all.
        [Test]
        public void TheStageFiguresDoNotTakeClicks()
        {
            var screen = FightScreen.Build();

            var offenders = new List<string>();

            // INHERITED, because that is what the emitter does: AsDecor clears
            // raycastTarget throughout a subtree, so a child of a decor node is
            // already safe and its own flag stays false. Reading the per-node
            // flag reported every FootGlow as an offender when its parent
            // shadow already covered it.
            WalkDecor(screen.Root, false, (node, decor) =>
            {
                if (node.Kind != UiNodeKind.Sprite) return;

                bool onStage = node.Name.EndsWith("Sprite")
                               || node.Name.EndsWith("HitFlash")
                               || node.Name.EndsWith("FootShadow")
                               || node.Name.EndsWith("FootGlow");

                if (onStage && !decor) offenders.Add(node.Name);
            });

            CollectionAssert.IsEmpty(offenders,
                "these stage layers still take clicks, so they block whatever the command UI has " +
                "underneath them: " + string.Join(", ", offenders));
        }

        // The exception, pinned so the sweep above cannot be "fixed" by
        // marking the whole stage decor and quietly killing the hover the
        // enemy intent badge exists for.
        [Test]
        public void TheIntentBadgeStillTakesOne()
        {
            var screen = FightScreen.Build();

            var badges = new List<UiNode>();
            Walk(screen.Root, n => { if (n.Name.Contains("Intent") && n.Kind == UiNodeKind.Button) badges.Add(n); });

            CollectionAssert.IsNotEmpty(badges, "the enemy intent badges are gone");
            foreach (var badge in badges)
            {
                Assert.IsFalse(badge.Decor,
                    $"'{badge.Name}' is decor, so it can never be hovered - and the badge exists to be");
            }
        }

        private static void Walk(UiNode node, System.Action<UiNode> visit)
        {
            visit(node);
            foreach (var child in node.Children) Walk(child, visit);
        }

        // Carries decor down the way UiEmitter does.
        private static void WalkDecor(UiNode node, bool inherited, System.Action<UiNode, bool> visit)
        {
            bool decor = inherited || node.Decor;
            visit(node, decor);
            foreach (var child in node.Children) WalkDecor(child, decor, visit);
        }


        // ---- there is always a way out of targeting -------------------------------
        //
        // The skill list folds once something is picked, and BACK was a row in
        // that list. Escape belongs to the system menu in this scene, so with
        // the list gone there was nothing left that could cancel: a player who
        // changed their mind had to pick a victim anyway.
        //
        // Asserted against the BUILT TREE rather than the menu model, because
        // the failure is a missing node, not a wrong state -- the model already
        // said Back() works, and it did, with nothing wired to call it.
        [Test]
        public void TargetingOffersACancelThatIsNotInsideTheFoldedList()
        {
            var screen = FightScreen.Build();

            var cancel = Walk(screen.Root).FirstOrDefault(n => n.Name == "TargetCancelButton");
            Assert.IsNotNull(cancel, "targeting has no way out - see FightMenuState.SubmenuOpen");

            var inColumn = Walk(screen.SubmenuColumn.Node).Any(n => n.Name == "TargetCancelButton");
            Assert.IsFalse(inColumn,
                "the cancel is inside the column that folds when a skill is picked, so it is gone " +
                "in exactly the state it exists for");
        }

        // AND IT HAS TO TAKE CLICKS. The banner it rides is AsDecor -- it hangs
        // over the stage the player is aiming at, and a bar that swallowed a
        // click on the monster behind it would be worse than no bar. AsDecor
        // walks the subtree when it is called, so a cancel appended before that
        // call is a button with its raycast cleared: present, correct-looking,
        // and dead.
        [Test]
        public void TheCancelIsNotDecorEvenThoughTheBannerAroundItIs()
        {
            var screen = FightScreen.Build();
            var prompt = Walk(screen.Root).First(n => n.Name == "TargetPrompt");

            Assert.IsTrue(prompt.Decor, "the banner must not take clicks meant for the stage");

            var cancel = prompt.Children.First(c => c.Name == "TargetCancelButton");
            Assert.IsFalse(cancel.Decor, "the one control on the banner cannot be pressed");
        }

        // ---- the two columns of the command UI end on one line ---------------------
        //
        // The skill panel floated 36px above the verb column beside it, which
        // reads as a panel that missed rather than as two halves of one
        // control. B1 found a second, smaller version of the same defect
        // one layer down: every kit PNG carries a transparent halo outside
        // its own painted border (tools/measure_ui_kit.py measures it), so
        // even rects that agree on CommandBottom exactly show two DIFFERENT
        // painted edges -- a rect-flush is not a paint-flush. This now
        // checks the VISIBLE bottoms (rect bottom + height * that art's own
        // measured pad, the same arithmetic FightSubmenuLayout.
        // VisibleBottomLine and FightScreen.BuildPartyPlate both place
        // against) of all three surfaces that share this line: the verb
        // column, the skill panel frame, and the party plate.
        [Test]
        public void TheSkillPanelEndsOnTheSameLineAsTheVerbColumn()
        {
            var attack = RectOf("Verb0");
            var panel = RectOf("SubmenuContainer");
            var partyPlate = RectOf("PartyPlate");

            float verbVisibleBottom = VisibleBottom(attack, Ui.PlateVisiblePad(Ui.PlateShapeFor(attack.Width, attack.Height)));
            float panelVisibleBottom = VisibleBottom(panel, Ui.ContainerVisiblePad(ContainerRatio.ThreeByFour));
            float plateVisibleBottom = VisibleBottom(partyPlate, Ui.ContainerVisiblePad(ContainerRatio.TwoByOne));

            Assert.AreEqual(verbVisibleBottom, panelVisibleBottom, 0.01f,
                "the skill panel and the verb column no longer end on the same VISIBLE line");
            Assert.AreEqual(verbVisibleBottom, plateVisibleBottom, 0.01f,
                "the party plate and the verb column no longer end on the same VISIBLE line");
            Assert.AreEqual(FightSubmenuLayout.VisibleBottomLine, verbVisibleBottom, 0.01f,
                "and that line is FightSubmenuLayout.VisibleBottomLine, which is what all three are measured from");
        }

        private static float VisibleBottom(UiRect rect, ContentInsetFrac pad) =>
            (rect.Centre.Y - rect.Height * 0.5f) + rect.Height * pad.Bottom;

        // ---- the submenu's Violet 3:4 container ------------------------------------
        //
        // Same pattern as CharacterDossierScreenTests/RelicDraftScreenTests: a
        // flat Solid + Ui.Rim became Ui.Container(Violet, ThreeByFour) sized
        // from FightSubmenuLayout.FrameWidth/FrameHeight, balance-bot 2026-09-02.
        // The submenu frame's container theme/ratio and content inset are
        // covered by KitContainerPlacementTests, not repeated here.

        [Test]
        public void TheSubmenuFrameChildrenRideInsideTheFrame()
        {
            // If the viewport, scrollbar or BACK were siblings of the frame
            // rather than descendants, hiding SubmenuColumn would leave them
            // stranded next to it instead of with it.
            var frame = Walk(Screen().Root).First(n => n.Name == "SubmenuContainer");
            var names = Walk(frame).Select(n => n.Name).ToList();

            CollectionAssert.Contains(names, "SubmenuViewport");
            CollectionAssert.Contains(names, "SubmenuScrollTrack");
            CollectionAssert.Contains(names, "SubmenuBack");
        }

        // ---- verb theming --------------------------------------------------------

        [TestCase(0, ButtonTheme.Crimson)]
        [TestCase(1, ButtonTheme.Violet)]
        [TestCase(2, ButtonTheme.Green)]
        [TestCase(3, ButtonTheme.Blue)]
        public void EachVerbWearsItsOwnTheme(int index, ButtonTheme expected)
        {
            var verb = Screen().VerbButtons[index].Node;

            Assert.AreEqual(expected, verb.Theme, $"Verb{index} should be Themed({expected})");
            Assert.IsTrue(verb.CaptionPreserving,
                $"Verb{index} declares its own hotkey/text/caret caption - ThemedPlate(), not Themed()");
        }

        [Test]
        public void AVerbRowsTree_HasVisualsButNoGeneratedLabel()
        {
            var verb = Screen().VerbButtons[0].Node;

            var visuals = Walk(verb).FirstOrDefault(n => n.Name == "Visuals");
            Assert.IsNotNull(visuals, "Verb0 has no Visuals child - ThemedPlate() should have built one");

            var generatedLabel = Walk(verb).FirstOrDefault(n => n.Name == "Verb0Label");
            Assert.IsNull(generatedLabel,
                "a caption-preserving verb must not also get a generated 'Verb0Label' - that would be a second, " +
                "blank, centred label drawn under the row's own hand-declared caption");

            Assert.IsNotNull(Walk(verb).FirstOrDefault(n => n.Name == "Verb0Hotkey"));
            Assert.IsNotNull(Walk(verb).FirstOrDefault(n => n.Name == "Verb0Text"));
            Assert.IsNotNull(Walk(verb).FirstOrDefault(n => n.Name == "Verb0Caret"));
        }

        [Test]
        public void AVerbRowsVisualsIsLayeredWithItsCaption_NotAllowOverlap()
        {
            var verb = Screen().VerbButtons[0].Node;

            var offenders = Walk(verb).Where(n => n.AllowOverlapReason != null).Select(n => n.Name).ToList();
            CollectionAssert.IsEmpty(offenders,
                "a themed verb row must not carry AllowOverlap anywhere in its declared subtree: " +
                string.Join(", ", offenders));

            var visuals = Walk(verb).First(n => n.Name == "Visuals");
            var text = Walk(verb).First(n => n.Name == "Verb0Text");
            Assert.IsNotNull(visuals.LayerGroup);
            Assert.AreSame(visuals.LayerGroup, text.LayerGroup,
                "the caption sits ON its own plate - Visuals and the caption children share one LayerGroup token");
        }

        [Test]
        public void AVerbRowsAspectMatches300By52()
        {
            var rect = RectOf("Verb0");

            Assert.AreEqual(300f, rect.Width, 0.01f);
            Assert.AreEqual(52f, rect.Height, 0.01f);
        }

        // ---- the skill detail card's element tag ------------------------------

        [Test]
        public void TheDamageTypeTagSitsBetweenThePowerKeyAndItsValue()
        {
            var keyRect = RectOf("DetailStatKey1");
            var valueRect = RectOf("DetailStatValue1");
            var tagRect = RectOf("DetailDamageType");

            // Same row -- riding POWER rather than a row of its own, since
            // FightHudSpec.DetailStatRows (pinned at 5 by
            // TheTreeIsSizedFromFightHudSpec_NotFromRestatedNumbers) leaves no
            // sixth line to give it.
            Assert.AreEqual(keyRect.Centre.Y, tagRect.Centre.Y, 0.01f);
            Assert.AreEqual(valueRect.Centre.Y, tagRect.Centre.Y, 0.01f);

            Assert.LessOrEqual(keyRect.Right, tagRect.Left,
                "the element tag reaches back into the POWER key's own box");
            Assert.LessOrEqual(tagRect.Right, valueRect.Left,
                "the element tag reaches into the POWER value's own box");
        }

        // ---- status badges (PLAN_STATUS_EFFECT_UI, package B) -----------------

        [Test]
        public void StatusBadgeListsAreSizedAndNamedAsThePlanFixes()
        {
            var screen = Screen();
            var root = UiSolver.Solve(screen.Root, UiFrames.Reference);

            // 3 slots x 5 (4 statuses + the "+N" overflow chip), flattened
            // slot-major; 2 roster rows x 5, flattened roster-major; the
            // party plate raised 4 -> 6 with no new list.
            Assert.AreEqual(15, screen.EnemyStatusBadges.Count);
            Assert.AreEqual(3, screen.EnemyStatusStrips.Count);
            Assert.AreEqual(10, screen.RosterStatusBadges.Count);

            // 12 SINCE 2026-09-09, up from 6: the owner's mock-up gives the
            // party plate two badge lines of six. Row-major, so slot 6 starts
            // the second line -- PartyBuffLinesAreSixWideAndRowMajor below
            // pins that half; this one only pins the count the controller's
            // PartyStatusBadgeCount has to agree with.
            Assert.AreEqual(12, screen.PartyBuffIcons.Count);

            for (int slot = 0; slot < FightHudSpec.StageSlotsPerSide; slot++)
            {
                Assert.IsNotNull(Find(root, $"EnemyStatusStrip{slot}"), $"missing EnemyStatusStrip{slot}");

                for (int i = 0; i < 5; i++)
                {
                    var name = $"EnemyStatusBadge{slot}_{i}";
                    var badge = Find(root, name);
                    Assert.IsNotNull(badge, $"missing {name}");
                    Assert.IsNotNull(Find(badge, "Glyph"), $"{name} has no Glyph child");
                    Assert.IsNotNull(Find(badge, "Code"), $"{name} has no Code child");
                    Assert.IsNotNull(Find(badge, "Counter"), $"{name} has no Counter child");
                }
            }

            for (int r = 0; r < 2; r++)
            {
                for (int i = 0; i < 5; i++)
                {
                    var name = $"RosterStatusBadge{r}_{i}";
                    var badge = Find(root, name);
                    Assert.IsNotNull(badge, $"missing {name}");
                    Assert.IsNotNull(Find(badge, "Glyph"), $"{name} has no Glyph child");
                    Assert.IsNotNull(Find(badge, "Code"), $"{name} has no Code child");
                    Assert.IsNotNull(Find(badge, "Counter"), $"{name} has no Counter child");
                }
            }

            // PartyBuff{i} is rebuilt through the same BuildStatusBadge
            // anatomy as the enemy and roster rows above -- the first
            // capture's worst defect was this row NOT having its own Glyph/
            // Counter children, which forced a tinted root Image to double
            // as the glyph and Code to carry a folded "CODE·N" caption.
            for (int i = 0; i < 12; i++)
            {
                var name = $"PartyBuff{i}";
                var badge = Find(root, name);
                Assert.IsNotNull(badge, $"missing {name}");
                Assert.IsNotNull(Find(badge, "Glyph"), $"{name} has no Glyph child");
                Assert.IsNotNull(Find(badge, "Code"), $"{name} has no Code child");
                Assert.IsNotNull(Find(badge, "Counter"), $"{name} has no Counter child");
            }

            Assert.IsNotNull(Find(root, "StatusTooltip"));
            Assert.IsNotNull(Find(root, "StatusTooltipText"));

            // PartyBuffTooltip is GONE (S4's review) -- the party plate
            // migrated onto the shared StatusTooltip above and nothing was
            // ever wired to the old fixed-spot one again.
            Assert.IsNull(Find(root, "PartyBuffTooltip"));
        }

        [Test]
        public void EmptyStatusRowsAndBadgesStartInactive()
        {
            var root = Solve();

            for (int slot = 0; slot < FightHudSpec.StageSlotsPerSide; slot++)
            {
                Assert.IsTrue(Find(root, $"EnemyStatusStrip{slot}").Source.StartInactive,
                    $"EnemyStatusStrip{slot} must start inactive - an empty row renders nothing (section 7)");

                for (int i = 0; i < 5; i++)
                {
                    var name = $"EnemyStatusBadge{slot}_{i}";
                    Assert.IsTrue(Find(root, name).Source.StartInactive, $"{name} must start inactive");
                }
            }

            for (int r = 0; r < 2; r++)
            {
                for (int i = 0; i < 5; i++)
                {
                    var name = $"RosterStatusBadge{r}_{i}";
                    Assert.IsTrue(Find(root, name).Source.StartInactive, $"{name} must start inactive");
                }
            }
        }

        // PINNED LITERALS (S11's review), not FightStageAnchors.SlotOffset --
        // a test recomputing the production formula to build its own
        // expected value is a tautology (CLAUDE.md's gotcha 5): it can only
        // ever catch a hand-typed mistake in THIS test, never a real
        // regression in SlotOffset itself. Values were docs/
        // PLAN_STATUS_EFFECT_UI.md section 1's own table -- X is each slot's
        // ground position (Near 300, mid 432.5, Far 565), Y is offset.Y - 60
        // worked out there once (-278, -231.5, -185).
        //
        // X RE-PINNED 2026-09-09: Far.X widened 565 -> 660 (party overlap
        // fix, see FightStageAnchors' own comment on Far), so mid and far
        // move to 480 and 660. Y is untouched -- Near.Y/Far.Y did not move,
        // only X did -- so -278/-231.5/-185 stand as they were.
        private static readonly (float X, float Y)[] EnemyStatusRowCentres =
        {
            (300f, -278f),
            (480f, -231.5f),
            (660f, -185f),
        };

        [Test]
        public void EnemyStatusRowCentresSitSixtyBelowTheirSlotSGroundLine()
        {
            var root = Solve();
            Assert.AreEqual(FightHudSpec.StageSlotsPerSide, EnemyStatusRowCentres.Length,
                "pinned centres no longer match the number of stage slots -- see this test's own header");

            for (int slot = 0; slot < EnemyStatusRowCentres.Length; slot++)
            {
                var expected = EnemyStatusRowCentres[slot];
                var strip = RectOf($"EnemyStatusStrip{slot}");

                Assert.AreEqual(expected.X, strip.Centre.X, 0.01f, $"slot {slot} row x");
                Assert.AreEqual(expected.Y, strip.Centre.Y, 0.01f, $"slot {slot} row y");
            }
        }

        // How far below its own ground line the nameplate hangs, in REAL
        // (rendered) pixels: (|NameplateOffset| + half its height) * the
        // slot's own depth scale.
        //
        // NOT RectOf("Enemy{slot}Nameplate").Bottom. The nameplate is a
        // child of a WithScale slot, and UiSolver -- like Unity's own
        // RectTransform -- does not cascade an ancestor's scale into a
        // descendant's own Rect: that is a render-time transform, not a
        // second layout pass, so the solved Rect is the UNSCALED 48px
        // number, not the 48*scale one this row's own drop was measured
        // against (section 1). Comparing against the solved Rect directly
        // overstates the real on-screen collision by exactly that scale
        // factor -- caught the hard way, as a failing test, before this
        // helper existed.
        private static float RealNameplateReach(int slot, int count) =>
            (34f + 14f) * FightStageAnchors.SlotScale(slot, count);

        [Test]
        public void EnemyStatusRowsClearTheirOwnNameplateAndStayAboveTheCanvasFloor()
        {
            var root = Solve();
            int count = FightHudSpec.StageSlotsPerSide;

            for (int slot = 0; slot < count; slot++)
            {
                var offset = FightStageAnchors.SlotOffset(slot, count, mirrored: false);
                var strip = RectOf($"EnemyStatusStrip{slot}");
                float nameplateBottom = offset.Y - RealNameplateReach(slot, count);

                // Thin at the near slot (section 1's own "23.5px" is a
                // centre-to-reach distance, not edge-to-edge; the real
                // margin here is close to 5.5px) but must stay positive.
                Assert.Less(strip.Top, nameplateBottom,
                    $"slot {slot}'s status row reaches up into its own nameplate's real (scaled) band");

                // Comfortably inside the canvas at every audited aspect -
                // UiFrames.Reference is the smallest of them (section 1).
                Assert.Greater(strip.Bottom, -(UiFrames.Reference.Y * 0.5f),
                    $"slot {slot}'s status row falls off the bottom of the canvas");
            }
        }

        [Test]
        public void EnemyStatusRowsDoNotOverlapEachOtherVertically()
        {
            var root = Solve();
            int count = FightHudSpec.StageSlotsPerSide;
            var rows = Enumerable.Range(0, count).Select(slot => RectOf($"EnemyStatusStrip{slot}")).ToList();

            for (int a = 0; a < count; a++)
            {
                for (int b = a + 1; b < count; b++)
                {
                    Assert.IsFalse(rows[a].Overlaps(rows[b]),
                        $"enemy status rows {a} and {b} overlap - see FightStageAnchors.SlotOffset");
                }
            }
        }

        // NOT 40px, as section 1's prose ("row centres land at -278, -231.5
        // and -185, spans 40px apart") could be misread to claim: 40 is the
        // BADGE pitch within one row. The measured row-to-row distance is the
        // slots' own ground-line spacing, 46.5px here, comfortably more than
        // the 36px badge height so the rows never collide vertically even
        // where their x-ranges do (see the far slot's row, which reaches
        // further right than the near one's).
        [Test]
        public void EnemyStatusBadgesWithinARowSitFortyPixelsApart()
        {
            for (int slot = 0; slot < FightHudSpec.StageSlotsPerSide; slot++)
            {
                for (int i = 0; i < 4; i++)
                {
                    var a = RectOf($"EnemyStatusBadge{slot}_{i}");
                    var b = RectOf($"EnemyStatusBadge{slot}_{i + 1}");
                    Assert.AreEqual(40f, b.Centre.X - a.Centre.X, 0.01f, $"slot {slot} badge {i}->{i + 1} pitch");
                }
            }
        }

        // ---- the bottom-left HUD column (owner's mock-up, 2026-09-09) --------

        // THE PIN THAT MATTERS MOST ON THIS COLUMN, and the only one that is
        // about something outside the column itself.
        //
        // The stack grows UPWARD from the party plate's top edge, and the
        // party stage stands over it: the stage is declared before the HUD
        // plates but the middle party slot's contact ring and feet reach down
        // into this band, and the stage draws OVER the column, so anything
        // this stack gains in height is paid for in roster text with a boot
        // on it. The budget is therefore the stack's own TOP, and it must not
        // rise.
        //
        // PINNED AS A LITERAL, not recomputed from RosterFirstY/RosterPitchY/
        // RosterPlateH (CLAUDE.md's gotcha 5): a test that rebuilds the
        // production expression can only catch a typo in itself. -160.627 is
        // what the stack topped out at BEFORE this pass, when the 136px
        // between PartyPlateTopY and here was spent on
        // 36 (transform strip) + 6 + 44 + 6 + 44. It is spent on
        // 2 + 66 + 2 + 66 now, which is the same 136 -- that is the whole
        // arithmetic of removing the strip.
        private const float RosterStackTopBudget = -160.627f;

        [Test]
        public void TheRosterStackTopDoesNotRiseAboveItsBudget()
        {
            var top = RectOf("Roster1");

            Assert.AreEqual(RosterStackTopBudget, top.Centre.Y + top.Height * 0.5f, 0.01f,
                "the HUD column's top edge moved -- the party stage draws OVER this column, so a taller " +
                "stack puts a figure's feet on top of the roster text. Pay for new rows by trimming cell " +
                "heights, never by raising this line.");
        }

        // "Have all the containers be flush with one another on a horizontal
        // axis. So no container bigger than the other in width" -- the owner,
        // 2026-09-09. Three cards, one left edge, one right edge.
        [Test]
        public void EveryCardInTheHudColumnSharesTheSameVerticalEdges()
        {
            var cards = new[] { "PartyPlate", "Roster0", "Roster1" };

            foreach (var card in cards)
            {
                var rect = RectOf(card);
                Assert.AreEqual(-920f, rect.Left, 0.01f, $"{card}'s left edge");
                Assert.AreEqual(-540f, rect.Right, 0.01f, $"{card}'s right edge");
            }
        }

        // The mock-up's roster card: one outlined rectangle, three stacked
        // cells, a hairline between each pair. Checked as ORDER and
        // CLEARANCE rather than as coordinates -- the numbers are free to be
        // retuned, the reading order is not.
        [Test]
        public void EachRosterCardStacksNameAndBarsThenSignatureThenBadges()
        {
            for (int r = 0; r < 2; r++)
            {
                var name = RectOf($"Roster{r}Name");
                var hpBar = RectOf($"Roster{r}HpBar");
                var mpBar = RectOf($"Roster{r}MpBar");
                var rule0 = RectOf($"Roster{r}Rule0");
                var signature = RectOf($"Roster{r}Signature");
                var rule1 = RectOf($"Roster{r}Rule1");

                Assert.LessOrEqual(hpBar.Top, name.Bottom, $"roster {r}: the bars must sit UNDER the name");
                Assert.AreEqual(hpBar.Centre.Y, mpBar.Centre.Y, 0.01f,
                    $"roster {r}: the mock-up puts the MP bar at the SAME height as the HP bar, not below it");
                Assert.LessOrEqual(hpBar.Right, mpBar.Left, $"roster {r}: HP on the left half, MP on the right");

                Assert.LessOrEqual(rule0.Top, hpBar.Bottom, $"roster {r}: rule 0 closes the bar cell");
                Assert.LessOrEqual(signature.Top, rule0.Bottom, $"roster {r}: the signature line is cell 2");
                Assert.LessOrEqual(rule1.Top, signature.Bottom, $"roster {r}: rule 1 closes the signature cell");

                for (int i = 0; i < 5; i++)
                {
                    var badge = RectOf($"RosterStatusBadge{r}_{i}");
                    Assert.LessOrEqual(badge.Top, rule1.Bottom,
                        $"roster {r} badge {i} has left the third cell");
                    Assert.IsTrue(RectOf($"Roster{r}").Contains(badge),
                        $"roster {r} badge {i} escapes the card");
                }
            }
        }

        // The two numbers are drawn ON their own bars, which is the only way
        // two meters fit across a 380px card -- so each value label has to be
        // INSIDE its own track's rect, not beside it.
        [Test]
        public void TheRosterValuesAreDrawnOnTheirOwnBars()
        {
            for (int r = 0; r < 2; r++)
            {
                Assert.IsTrue(RectOf($"Roster{r}HpBar").Contains(RectOf($"Roster{r}HpValue")),
                    $"roster {r}: the HP value has slipped off its bar");
                Assert.IsTrue(RectOf($"Roster{r}MpBar").Contains(RectOf($"Roster{r}MpValue")),
                    $"roster {r}: the MP value has slipped off its bar");
            }
        }

        // EVERY meter on this column is the same widget (Ui.Meter): a track,
        // the fill FightController.SetFill drains, and the two shading strips
        // that make it read as a bar rather than as a coloured rectangle.
        //
        // The strips must be CHILDREN OF THE FILL, and that is the half worth
        // pinning: SetFill moves the fill's own anchorMax.x, so a strip
        // declared as a sibling of the fill would stay full width while the
        // bar drained under it -- a highlight floating over an empty track.
        [Test]
        public void EveryMeterOnTheHudColumnCarriesItsShadingInsideItsFill()
        {
            var fills = new List<string> { "PartyHpFill", "PartyMpFill" };
            for (int r = 0; r < 2; r++)
            {
                fills.Add($"Roster{r}HpFill");
                fills.Add($"Roster{r}MpFill");
            }

            var root = Solve();
            foreach (var fillName in fills)
            {
                var fill = Find(root, fillName);
                Assert.IsNotNull(fill, $"no meter fill named '{fillName}'");

                var children = fill.Children.Select(c => c.Name).ToList();
                CollectionAssert.Contains(children, fillName + "Sheen",
                    $"{fillName} has no top highlight - it will read as a flat rectangle");
                CollectionAssert.Contains(children, fillName + "Shade",
                    $"{fillName} has no bottom shade band");
            }
        }

        // "The bars should be slightly bigger in height. It should feel like
        // a proper HP bar not a red bar. Same for MP." -- the owner. Pinned
        // as a floor rather than an exact value: the point is that a future
        // tidy-up cannot quietly shrink them back toward the 13.45 the
        // capture was taken at.
        [Test]
        public void TheHudColumnsBarsAreTallEnoughToReadAsMeters()
        {
            Assert.GreaterOrEqual(RectOf("PartyHpBar").Height, 20f, "the party HP bar is back to a stripe");
            Assert.GreaterOrEqual(RectOf("PartyMpBar").Height, 20f, "the party MP bar is back to a stripe");

            for (int r = 0; r < 2; r++)
            {
                Assert.GreaterOrEqual(RectOf($"Roster{r}HpBar").Height, 12f, $"roster {r} HP bar");
                Assert.GreaterOrEqual(RectOf($"Roster{r}MpBar").Height, 12f, $"roster {r} MP bar");
            }
        }

        [Test]
        public void PartyBuffLinesAreSixWideAndRowMajor()
        {
            var first = RectOf("PartyBuff0");
            var seventh = RectOf("PartyBuff6");

            Assert.AreEqual(first.Centre.X, seventh.Centre.X, 0.01f,
                "slot 6 must start the SECOND line under slot 0 -- PaintStatusRow fills a flat range in " +
                "order, so a column-major layout would fill down before across");
            Assert.Less(seventh.Centre.Y, first.Centre.Y, "the second line sits below the first");

            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(RectOf($"PartyBuff{i}").Centre.Y, RectOf($"PartyBuff{i + 1}").Centre.Y, 0.01f,
                    $"badges {i} and {i + 1} are on the same line");
            }
        }

        // Two features removed in the same pass as the column redraw, both
        // pinned as absences because a re-added node would otherwise cost
        // vertical budget nothing here would notice.
        [Test]
        public void TheTransformStripAndTheDeadPortraitAreGone()
        {
            var root = Solve();

            // A transformation is a StatusRow now (StatusHud.TransformRow),
            // so it shows on the party plate AND on a transformed ally's
            // roster card -- which the strip, describing only the acting
            // character, never could. Its 36px is what bought the roster
            // cards their extra cells.
            Assert.IsNull(Find(root, "TransformStrip"));
            Assert.IsNull(Find(root, "TransformStripText"));

            // Never loaded anything: no sprite key, always inactive, and the
            // controller field it bound to had no reader and no writer.
            Assert.IsNull(Find(root, "PartyPortrait"));
        }

        private static IEnumerable<UiNode> Walk(UiNode node)
        {
            yield return node;
            foreach (var child in node.Children)
            {
                foreach (var found in Walk(child)) yield return found;
            }
        }
    }
}

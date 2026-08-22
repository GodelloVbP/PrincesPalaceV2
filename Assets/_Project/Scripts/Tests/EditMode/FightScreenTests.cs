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
            Assert.AreEqual(216f, second.X - first.X, 0.01f, "plate width plus a 16px gutter");

            Assert.AreEqual(first.X, third.X, 0.01f, "the third starts the next row under the first");
            Assert.AreEqual(76f, first.Y - third.Y, 0.01f, "plate height plus a 12px gutter");

            // 392 still, and the block's RIGHT edge is what is pinned -- it sits
            // against the same margin the heading and the standing-count do.
            Assert.AreEqual(392f, first.Y, 0.01f);
            Assert.AreEqual(920f, RectOf("EnemyPlate1").Centre.X + RectOf("EnemyPlate1").Width * 0.5f, 0.01f);

            // The plates are the stage's ceiling: the tallest actor needs 300
            // above the front slot's ground line, so its head reaches 72, and
            // the middle slot's reaches 89 while still just crossing the plates
            // in x. Two rows of 64 clear both by more than the three of 104 did.
            Assert.Greater(third.Y - 32f, 89f + 12f,
                "the bottom plate has dropped back onto an actor's head - see tools/measure_stage.py");
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
            int count = FightSubmenuLayout.PoolSize;
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
            int lastStage = -1;
            for (int i = 0; i < root.Children.Count; i++)
            {
                if (root.Children[i].Name.EndsWith("Stage")) lastStage = i;
            }

            Assert.Greater(lastStage, -1, "no stage found in the fight tree");

            for (int i = lastStage + 1; i < root.Children.Count; i++)
            {
                foreach (var node in DrawnAndVisible(root.Children[i]))
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

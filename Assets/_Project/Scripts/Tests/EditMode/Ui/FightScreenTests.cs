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
                s.PartyNameplates, s.PartyFootShadows, s.PartyFootGlows, s.PartyHitAreas);

            // TEN PARALLEL LISTS OVER ONE COLUMN, and RefreshPcPlate indexes
            // every one of them off the same loop counter -- exactly the
            // shape the Store bug had. One card language means one length to
            // agree on, where the party plate and the roster cards used to
            // be two families that could drift apart independently.
            AssertSameLength("pc plates", s.PcPlates, s.PcPlateArts, s.PcPlateHighlights,
                s.PcPlateReticles, s.PcNames, s.PcSignatures, s.PcHpValues, s.PcHpFills,
                s.PcMpValues, s.PcMpFills, s.PcMpShades);

            Assert.AreEqual(FightScreen.PcPlateCount, s.PcPlates.Count,
                "one plate per party seat, always -- see BuildPcPlates' own header");

            // FOUR PER PLATE, kept OUT of AssertSameLength above -- these are
            // 4x the length of the plate lists by construction, so the helper
            // would refuse them for being exactly right.
            // FightController.ApplyPoolTheme indexes pcMpRims as
            // i * RimsPerCard, so a list that is not 4x the plates recolours
            // one ally's bar to another ally's resource.
            Assert.AreEqual(s.PcPlates.Count * 4, s.PcMpRims.Count,
                "pcMpRims is CARD-MAJOR: exactly four meter rim edges per plate, in order");

            // FIVE PER PLATE, same flattening, same silent failure mode.
            Assert.AreEqual(s.PcPlates.Count * FightScreen.PcStatusBadgesPerPlate, s.PcStatusBadges.Count,
                "pcStatusBadges is CARD-MAJOR: exactly five badges per plate, in order");
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
                { "SubmenuRows", s.SubmenuRows }, { "SubmenuMarks", s.SubmenuMarks },
                { "DamagePopups", s.DamagePopups },
                { "SpellVfx", s.SpellVfx }, { "SpellVfxNext", s.SpellVfxNext },
                { "PartyHitAreas", s.PartyHitAreas },
                { "PcPlates", s.PcPlates }, { "PcPlateArts", s.PcPlateArts },
                { "PcPlateHighlights", s.PcPlateHighlights },
                { "PcPlateReticles", s.PcPlateReticles },
                { "PcNames", s.PcNames }, { "PcSignatures", s.PcSignatures },
                { "PcHpValues", s.PcHpValues }, { "PcHpFills", s.PcHpFills },
                { "PcMpValues", s.PcMpValues }, { "PcMpFills", s.PcMpFills },
                { "PcMpShades", s.PcMpShades }, { "PcMpRims", s.PcMpRims },
                { "PcStatusBadges", s.PcStatusBadges },
            };

            foreach (var pair in lists)
            {
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    Assert.IsTrue(pair.Value[i].IsValid, $"{pair.Key}[{i}] was never assigned a node");
                }
            }

            Assert.IsTrue(s.SecondLifeBadge.IsValid, "the party revive badge was never assigned a node");
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

        // ---- the PC plate column (2026-09-10) --------------------------------

        [Test]
        public void ThePcPlateStackIsFlushLeftAndBottomAtTheMeasuredAspect()
        {
            // THE LEFT EDGE HAS NOT MOVED SINCE v1: -920. Everything else on
            // this column has, twice, and the left edge is the one anchor
            // the owner has kept fixed through all of it.
            //
            // WIDTH 452, back to the number the column carried before the
            // 380-wide card pass -- the extra 72px is what pays for 16pt
            // names and 14pt numbers instead of 12/10/9.
            //
            // HEIGHT IS NOT A LITERAL DECISION, it is 452 / PcPlateArt.Aspect
            // = 80.5546. Pinned here as a literal anyway (gotcha 5) so a
            // redelivered plate at a different aspect fails HERE, saying the
            // art moved, rather than quietly letterboxing inside its rect.
            //
            // THE BOTTOM lands exactly on FightSubmenuLayout.VisibleBottomLine
            // (-485.392), the same line the verb column and the skill panel
            // end on: these plates are cropped to their own alpha bounding
            // box, so their VisiblePad is 0 on every edge and rect bottom IS
            // painted bottom.
            var bottom = RectOf("PcPlate0");
            Assert.AreEqual(-920f, bottom.Centre.X - bottom.Width * 0.5f, 0.01f, "the left edge must not move");
            Assert.AreEqual(452f, bottom.Width, 0.01f);
            Assert.AreEqual(80.5546f, bottom.Height, 0.01f);
            Assert.AreEqual(-485.392f, bottom.Centre.Y - bottom.Height * 0.5f, 0.01f,
                "the bottom edge is FightSubmenuLayout.VisibleBottomLine itself -- these plates carry no halo");

            // STACKED BOTTOM-UP WITH A 4px GAP, every plate identical.
            var middle = RectOf("PcPlate1");
            var top = RectOf("PcPlate2");
            Assert.AreEqual(84.5546f, middle.Centre.Y - bottom.Centre.Y, 0.01f, "plate height plus a 4px gap");
            Assert.AreEqual(84.5546f, top.Centre.Y - middle.Centre.Y, 0.01f, "the same pitch all the way up");

            foreach (string plate in new[] { "PcPlate0", "PcPlate1", "PcPlate2" })
            {
                var rect = RectOf(plate);
                Assert.AreEqual(-920f, rect.Centre.X - rect.Width * 0.5f, 0.01f, plate + " left edge");
                Assert.AreEqual(-468f, rect.Centre.X + rect.Width * 0.5f, 0.01f, plate + " right edge");
            }
        }

        // PINNED AS A LITERAL, not recomputed from PcPlateFirstY/PcPlatePitchY
        // (CLAUDE.md's gotcha 5): a test that rebuilds the production
        // expression can only catch a typo in itself.
        //
        // THE BUDGET IS THE CONSTRAINT AND THE LITERAL IS THE MEASUREMENT.
        // -160.627 is where the party stage's middle figure starts, and the
        // stage draws OVER the HUD, so a stack whose top rises past it puts
        // feet on plate text. Both asserts are kept: the LessOrEqual is the
        // rule, the AreEqual is what makes an unnoticed drift toward it fail
        // while there is still room to think about it.
        private const float PcStackTopBudget = -160.627f;

        // -235.7273: three 80.5546-tall plates and two 4px gaps stacked up
        // from -485.392. It used to be -195.392 with a 154-tall card and two
        // 66-tall roster cards; the column is 40px taller now and still 75px
        // clear of its ceiling.
        private const float PcStackTop = -235.7273f;

        [Test]
        public void ThePcPlateStackTopDoesNotRiseAboveItsBudget()
        {
            var top = RectOf("PcPlate2");
            float stackTop = top.Centre.Y + top.Height * 0.5f;

            Assert.LessOrEqual(stackTop, PcStackTopBudget,
                $"the plate stack's top edge is at {stackTop}, past the {PcStackTopBudget} the party stage " +
                "leaves it -- the stage draws over the HUD, so this puts feet on plate text");

            Assert.AreEqual(PcStackTop, stackTop, 0.01f,
                "the stack's top edge moved -- re-derive it and re-pin the literal, deliberately");
        }

        // ---- the ally picker's own surfaces (AUDIT #147) ----------------------

        [Test]
        public void EveryPcPlateIsAButtonWithAMarkerInTheMargin()
        {
            // The two halves the enemy plates have carried all along, stated
            // as a pin because neither is visible in a capture of an idle
            // fight: the card takes a click, and the marker sits OUTSIDE the
            // card so it reads as a target rather than as a badge.
            var s = Screen();

            for (int i = 0; i < FightScreen.PcPlateCount; i++)
            {
                var plate = Walk(s.Root).First(n => n.Name == $"PcPlate{i}");
                Assert.AreEqual(UiNodeKind.Button, plate.Kind,
                    $"PcPlate{i} has to take the click that confirms a single-ally cast");

                var card = RectOf($"PcPlate{i}");
                var marker = RectOf($"PcPlate{i}Reticle");
                Assert.Less(marker.Centre.X + marker.Width * 0.5f, card.Centre.X - card.Width * 0.5f + 0.01f,
                    "the marker belongs in the margin beside the plate, not on it");
            }
        }

        [Test]
        public void BothRacksGiveEveryFigureAClickTargetOfItsOwn()
        {
            // Clicking the character you mean to help is what a player tries
            // first, exactly as clicking the monster is -- and neither may be
            // the sprite, which raycasts against its RECT and would stand over
            // whatever is behind it (see BuildStage's own header).
            var s = Screen();

            Assert.AreEqual(s.EnemySlots.Count, s.EnemyHitAreas.Count);
            Assert.AreEqual(s.PartySlots.Count, s.PartyHitAreas.Count);

            for (int slot = 0; slot < s.PartyHitAreas.Count; slot++)
            {
                var area = s.PartyHitAreas[slot].Node;
                Assert.AreEqual($"PartyHitArea{slot}", area.Name);
                Assert.AreEqual(UiNodeKind.Button, area.Kind);
                Assert.IsTrue(area.StartInactive,
                    "a live rectangle over the battlefield eats clicks meant for whatever is behind it");
                Assert.IsTrue(area.Chromeless, "the figure is the button; this only hears the click");
            }
        }

        [Test]
        public void EveryPcPlateCarriesItsOwnArtAndItsOwnActingHighlight()
        {
            var s = Screen();

            for (int i = 0; i < FightScreen.PcPlateCount; i++)
            {
                var plate = Walk(s.Root).First(n => n.Name == $"PcPlate{i}");
                var names = Walk(plate).Select(n => n.Name).ToList();

                CollectionAssert.Contains(names, $"PcPlate{i}Art");
                CollectionAssert.Contains(names, $"PcPlate{i}Reticle");
                CollectionAssert.Contains(names, $"PcPlate{i}Highlight");
                CollectionAssert.Contains(names, $"PcPlate{i}HpBar");
                CollectionAssert.Contains(names, $"PcPlate{i}MpBar");
                CollectionAssert.Contains(names, $"PcPlate{i}Name");
                CollectionAssert.Contains(names, $"PcPlate{i}Signature");
            }

            // THE ACTING CARD IS GONE, and named rather than counted: a
            // "PartyPlate" or a "Roster0" reappearing would mean the two-card
            // language came back in some other form.
            var all = Walk(s.Root).Select(n => n.Name).ToList();
            CollectionAssert.DoesNotContain(all, "PartyPlate");
            CollectionAssert.DoesNotContain(all, "Roster0");
            CollectionAssert.DoesNotContain(all, "WoolRow");
            CollectionAssert.DoesNotContain(all, "WoolPip0");
        }

        // THE ART IS THE IDENTITY, so three plates must not carry one sprite.
        // The baked defaults are what an unrefreshed scene shows; the
        // controller overwrites them from content, and
        // PartyFormationCaptureTests checks the runtime half.
        [Test]
        public void TheThreePlatesAreBakedWithThreeDifferentHeads()
        {
            var s = Screen();
            var keys = s.PcPlateArts.Select(a => a.Node.SpriteKey).ToList();

            CollectionAssert.AllItemsAreNotNull(keys);
            Assert.AreEqual(3, keys.Distinct().Count(),
                "three plates baked with one sprite would read as three copies of one character");

            foreach (string key in keys)
            {
                StringAssert.StartsWith("Assets/_Project/Resources/Plates/pc_", key,
                    "a plate's baked key names the same PNG PcPlateSprites loads at runtime");
            }
        }

        // NOTHING ENTERS THE HEAD ZONE. The embossed head owns the right
        // ~19% of every plate (PcPlateArt.HeadZoneFrac, measured off the
        // committed art), and a name or a badge running under a bear's muzzle
        // is the one way this column can look broken while every rect in it
        // is legal -- UiAudit checks overlap and containment, and a label
        // sitting on its own plate's own art violates neither.
        [Test]
        public void NoTextOrBadgeEntersTheEmbossedHeadZone()
        {
            for (int i = 0; i < FightScreen.PcPlateCount; i++)
            {
                var plate = RectOf($"PcPlate{i}");
                float headLeft = plate.Centre.X + FightScreen.PcHeadZoneLeft;

                var occupants = new List<string>
                {
                    $"PcPlate{i}Name", $"PcPlate{i}Signature",
                    $"PcPlate{i}HpBar", $"PcPlate{i}MpBar",
                };
                for (int k = 0; k < FightScreen.PcStatusBadgesPerPlate; k++)
                {
                    occupants.Add($"PcStatusBadge{i}_{k}");
                }

                foreach (string node in occupants)
                {
                    var rect = RectOf(node);
                    Assert.LessOrEqual(rect.Centre.X + rect.Width * 0.5f, headLeft + 0.01f,
                        $"{node} reaches x {rect.Centre.X + rect.Width * 0.5f}, into the head zone that " +
                        $"starts at {headLeft} -- see PcPlateArt.HeadZoneFrac");
                }
            }
        }

        // THE OWNER'S OWN FLOOR: "it should feel like a proper HP bar, not a
        // red bar". 20px, and the bars are where the numbers are drawn now,
        // so this is also the box that has to hold 14pt text.
        [Test]
        public void BothMetersOnEveryPlateAreAtLeastTwentyPixelsTall()
        {
            for (int i = 0; i < FightScreen.PcPlateCount; i++)
            {
                Assert.GreaterOrEqual(RectOf($"PcPlate{i}HpBar").Height, 20f, $"PcPlate{i}HpBar");
                Assert.GreaterOrEqual(RectOf($"PcPlate{i}MpBar").Height, 20f, $"PcPlate{i}MpBar");
            }
        }

        // THE NAMES, AS LITERALS -- CLAUDE.md gotcha 5.
        //
        // This is the one assert that catches a card-major/edge-major mix-up
        // in PcMpRims, and it only catches it by being written out rather
        // than rebuilt from the stem: a test that generated the expected
        // names the same way the production code generates the actual ones
        // can only find a typo in itself. The failure this guards is silent
        // by construction -- rim edges are AsDecor, so UiAudit never looks at
        // their colour, and a mix-up paints the wrong ally's resource on a
        // bar rather than throwing anything.
        [Test]
        public void ThePoolMeterNodesCarryTheNamesTheControllerBindsTo()
        {
            var s = Screen();

            CollectionAssert.AreEqual(
                new[] { "PcPlate0MpFillShade", "PcPlate1MpFillShade", "PcPlate2MpFillShade" },
                s.PcMpShades.Select(r => r.Node.Name).ToList(),
                "Ui.Meter names the band under the fill '<fillName>Shade'");

            CollectionAssert.AreEqual(
                new[]
                {
                    "PcPlate0MpBarRimTop", "PcPlate0MpBarRimBottom", "PcPlate0MpBarRimLeft", "PcPlate0MpBarRimRight",
                    "PcPlate1MpBarRimTop", "PcPlate1MpBarRimBottom", "PcPlate1MpBarRimLeft", "PcPlate1MpBarRimRight",
                    "PcPlate2MpBarRimTop", "PcPlate2MpBarRimBottom", "PcPlate2MpBarRimLeft", "PcPlate2MpBarRimRight",
                },
                s.PcMpRims.Select(r => r.Node.Name).ToList(),
                "CARD-MAJOR -- FightController indexes these as i * RimsPerCard, and the rim is named off " +
                "the TRACK, not the fill (Ui.Meter passes trackName to Ui.Rim)");
        }

        // WHAT THE METER IS BAKED AS, which is MANA and has to stay mana.
        //
        // The controller overwrites every surface at runtime from the
        // holder's own pool (plan P7), and for every character shipped today
        // that pool IS mana -- so this pins that the bake and the pools.json
        // row cannot drift apart and make the first painted frame of a fight
        // differ from the second.
        [Test]
        public void ThePoolMeterIsBakedAsMana()
        {
            var s = Screen();

            foreach (var fill in s.PcMpFills)
            {
                Assert.AreEqual(FightHudPalette.MpBright, fill.Node.ColorHex, fill.Node.Name);
            }

            foreach (var shade in s.PcMpShades)
            {
                Assert.AreEqual(FightHudPalette.MpShade, shade.Node.ColorHex, shade.Node.Name);
            }

            // THE CAPTION IS THE EXCEPTION, and it is not baked as mana at
            // all: it is drawn ON the fill, so its job is contrast against
            // an arbitrary colour rather than agreement with one. Pinned as
            // the neutral it must stay, beside the four surfaces that do
            // carry the pool -- if it ever starts being themed, the first
            // pool with a light fill makes it vanish.
            foreach (var value in s.PcMpValues.Concat(s.PcHpValues))
            {
                Assert.AreEqual(FightHudPalette.TextPrimary, value.Node.ColorHex, value.Node.Name);
            }

            foreach (var edge in s.PcMpRims)
            {
                Assert.AreEqual(FightHudPalette.MpRim, edge.Node.ColorHex, edge.Node.Name);
            }
        }

        // THE POOL CAPTION NAMES ITS POOL BY PARAMETER, not by a literal.
        // A plate that printed "MP" over a bar full of somebody's rage is the
        // whole failure this UiString change exists to prevent, and the
        // template is the one place it can be reintroduced.
        [Test]
        public void ThePoolCaptionTakesItsTagAsAnArgument()
        {
            var s = Screen();

            foreach (var value in s.PcMpValues)
            {
                Assert.AreEqual(UiStrings.PoolNamedValue.Key, value.Node.Text.Key,
                    $"{value.Node.Name} must read its tag off the pool, not out of UiStrings");
            }

            Assert.AreEqual("FURY 100/100", UiStrings.PoolNamedValue.Format("FURY", 100, 100));
            Assert.AreEqual("MP 34/34", UiStrings.PoolNamedValue.Format("MP", 34, 34));
        }

        // THE OWNER'S TYPE SIZES, pinned where they can regress. The
        // complaint that started this pass was 9/10pt numbers at 1080p.
        [Test]
        public void NamesAreSixteenPointAndEveryNumberIsFourteen()
        {
            var s = Screen();

            foreach (var name in s.PcNames)
            {
                Assert.AreEqual(16, name.Node.FontSize, name.Node.Name);
            }

            foreach (var label in s.PcHpValues.Concat(s.PcMpValues).Concat(s.PcSignatures))
            {
                Assert.AreEqual(14, label.Node.FontSize, label.Node.Name);
            }
        }

        // ON-BAR TEXT CARRIES THE ON-BAR MATERIAL, which is the one role whose
        // TMP material has an outline actually SWITCHED ON -- a number drawn
        // on a meter has to stay readable over a full fill and over the empty
        // track behind it alike, and those are two unrelated backgrounds no
        // face colour can serve at once. Stated as a role rather than as a
        // per-label outline so the treatment lives in one place.
        //
        // NOT TacticalData, which is what these carried while the captions sat
        // on a dark chip: TacticalData's outline is authored (0.07) and dead
        // (no OUTLINE_ON keyword), which is exactly why the chip existed.
        [Test]
        public void EveryNumberDrawnOnABarUsesTheOnBarCaptionRole()
        {
            var s = Screen();

            foreach (var label in s.PcHpValues.Concat(s.PcMpValues))
            {
                Assert.AreEqual(TypographyRole.OnBarCaption, label.Node.Role,
                    $"{label.Node.Name} is drawn ON a meter and needs the outlined on-bar material");
            }
        }

        // AND NOTHING IS DRAWN BETWEEN THE FILL AND THE CAPTION. Two passes of
        // this column put each number on a dark quad -- first bare, then
        // rimmed -- and both captures read a FULL HP bar as half drained,
        // because a dark patch on a meter is the same colour as that meter's
        // own empty track. A bar that lies about its own value is worse than
        // the low contrast the chip was fixing, so the chip is gone and this
        // is what keeps it from coming back a third time.
        [Test]
        public void NoPlateDrawsAGroundBehindItsBarCaptions()
        {
            var solved = Solve();

            for (int i = 0; i < FightScreen.PcPlateCount; i++)
            {
                Assert.IsNull(Find(solved, $"PcPlate{i}HpValueGround"),
                    $"plate {i}: the HP caption is back on a chip -- outline it instead");
                Assert.IsNull(Find(solved, $"PcPlate{i}MpValueGround"),
                    $"plate {i}: the pool caption is back on a chip -- outline it instead");
            }
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

            // 276, UP FROM 236 (owner, 2026-09-10, "enemy names keep
            // truncating"): PlateW grew 220 to 260, which is what pays for a
            // 118px name box at 14pt -- see PlateW's own comment. The 16px
            // gutter itself did not move; the plate width inside it did.
            Assert.AreEqual(276f, second.X - first.X, 0.01f, "plate width plus a 16px gutter");

            Assert.AreEqual(first.X, third.X, 0.01f, "the third starts the next row under the first");

            // 142, UP FROM 122: PlateH is PlateW/2 (the 2:1 container's own
            // aspect), so widening the plate to 260 makes it 130 tall. The
            // 12px gutter itself is unchanged; the plate height inside it
            // grew with the width.
            Assert.AreEqual(142f, first.Y - third.Y, 0.01f, "plate height plus a 12px gutter");

            // 392 still, and the block's RIGHT edge is what is pinned -- it sits
            // against the same margin the heading and the standing-count do.
            Assert.AreEqual(392f, first.Y, 0.01f);
            Assert.AreEqual(920f, RectOf("EnemyPlate1").Centre.X + RectOf("EnemyPlate1").Width * 0.5f, 0.01f);

            // The plates are the stage's ceiling: the tallest actor needs 300
            // above the front slot's ground line, so its head reaches 72, and
            // the middle slot's reaches 89 while still just crossing the
            // plates in x. Two rows of 130 (was 110, see PlateW's 2026-09-10
            // note) still clear both, with 36 of daylight against the 12 this
            // layout is toleranced to. 65, not 55: half of the new 130-tall
            // plate.
            //
            // NOT RE-VERIFIED AGAINST tools/measure_stage.py as part of
            // either widening -- this Domain-only check uses the same
            // hand-derived 89f+12f the old assertion did, which is a real
            // gap this test cannot close on its own.
            Assert.Greater(third.Y - 65f, 89f + 12f,
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

            // Name and HP are one row; BRK rides the BAR's row, right of
            // the bar (2026-09-10 -- folding it into the name row is what had
            // squeezed the name to 72px at 11pt, which is the complaint that
            // started this pass).
            Assert.AreEqual(name.Centre.Y, hp.Centre.Y, 0.01f, "name and HP share the top row");
            Assert.AreEqual(bar.Centre.Y, tags.Centre.Y, 0.01f,
                "BRK shares the bar's row now - it is not in the name row and not a row of its own");

            // Symmetric margins to the plate's own top/bottom edges is what
            // "no blank band" actually means: the old layout left an unequal
            // gap where the status line used to sit. hp is the taller of the
            // two top-row boxes, so its edge is the row's real top edge.
            float topMargin = plate.Top - hp.Top;
            float bottomMargin = bar.Bottom - plate.Bottom;
            Assert.AreEqual(topMargin, bottomMargin, 0.01f,
                "the top row and the bar should be centred as one block, not offset toward one edge");

            // 40.5, UP FROM 34: PlateH grew 110 to 130 with PlateW
            // 220 -> 260 (see PlateW's own 2026-09-10 note) while the HP box
            // grew 27 -> 34 with its font, and barY was re-solved against
            // both -- see the two equations in BuildEnemyPlates' own topY/
            // barY comment. Still centred (the assertion above), inside a
            // taller frame, with a box 7px taller in it.
            //
            // hp is still the taller of the two top-row boxes at 34 against
            // the name's 24, so its edge is still the row's real top edge.
            Assert.AreEqual(40.5f, topMargin, 0.01f);

            // A deliberate small gap between the two rows, not the old blank
            // band (which was the full retired status line's own height).
            // STILL 8, deliberately: the HP box grew 27 -> 34 with its font
            // (10 -> 13pt) and barY was re-solved rather than left where it
            // was, so the gap between the rows is the same one it has always
            // been and the extra height came out of the outer margins.
            Assert.AreEqual(8f, hp.Bottom - bar.Top, 0.01f);

            // The name cannot reach the HP value, and the bar cannot reach
            // the tag that now shares its row.
            Assert.LessOrEqual(name.Right, hp.Left, "the name must not overlap the HP value");
            Assert.LessOrEqual(bar.Right, tags.Left, "the bar must not run under BRK");
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

        // BOTH SIDES, since 6fedab18: each side stands where ITS OWN
        // StageFormation says (FightStageAnchors.Enemy / .Party), so the
        // party side is checked against the mirrored call rather than
        // against a mirror of the enemy rects -- the two are no longer the
        // same claim.
        [TestCase("Enemy", false)]
        [TestCase("Party", true)]
        public void ASlotSitsWhereTheDepthCurveSaysItDoes(string side, bool mirrored)
        {
            int count = FightHudSpec.StageSlotsPerSide;
            for (int slot = 0; slot < count; slot++)
            {
                var expected = FightStageAnchors.SlotOffset(slot, count, mirrored);
                var rect = RectOf($"{side}{slot}Slot");

                // Bottom pivot: the offset positions the GROUND LINE, which is
                // what makes depth scaling grow a figure upward from the floor
                // rather than around its middle.
                Assert.AreEqual(expected.X, rect.Centre.X, 0.01f, $"{side} slot {slot} x");
                Assert.AreEqual(expected.Y, rect.Bottom, 0.01f, $"{side} slot {slot} ground line");
            }
        }

        [Test]
        public void TheTwoFrontRanksShareTheFloorTheyFaceEachOtherAcross()
        {
            // "One floor, not two platforms" used to be asserted for every
            // slot, back when both sides were one Near/Far pair mirrored in
            // X. Since 6fedab18 the party has its own formation
            // (StageFormation.Party): its floor has the HUD column standing
            // on it and its back ranks rise faster to clear it, so the back
            // ranks' ground lines legitimately differ. What still has to
            // hold is the pair the eye compares -- the two FRONT figures,
            // squared off across the open middle of the stage, stand on one
            // line. Which side stands where is ASlotSitsWhereTheDepthCurve-
            // SaysItDoes' job, per side; this only says the two fronts agree.
            var enemy = RectOf("Enemy0Slot");
            var party = RectOf("Party0Slot");

            Assert.AreEqual(enemy.Bottom, party.Bottom, 0.01f,
                "the two front ranks still share the floor they face each other across");
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
            var bottomPlate = RectOf("PcPlate0");

            float verbVisibleBottom = VisibleBottom(attack, Ui.PlateVisiblePad(Ui.PlateShapeFor(attack.Width, attack.Height)));
            float panelVisibleBottom = VisibleBottom(panel, Ui.ContainerVisiblePad(ContainerRatio.ThreeByFour));

            // THE RAW RECT BOTTOM for the third surface, and the asymmetry
            // is the point rather than an oversight. The verb row and the
            // skill panel are kit art, which paints a transparent halo
            // outside its own border, so their painted edge is inset from
            // their rect and has to be corrected for. The PC plates are
            // cropped to their own alpha bounding box
            // (PcPlateArt.VisiblePad, measured at 0 on every edge), so their
            // rect bottom IS their last painted pixel and a pad here would
            // move the comparison off the line it is checking.
            float plateVisibleBottom = bottomPlate.Centre.Y - bottomPlate.Height * 0.5f;

            Assert.AreEqual(verbVisibleBottom, panelVisibleBottom, 0.01f,
                "the skill panel and the verb column no longer end on the same VISIBLE line");
            Assert.AreEqual(verbVisibleBottom, plateVisibleBottom, 0.01f,
                "the bottom PC plate and the verb column no longer end on the same VISIBLE line");
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

            // 3 stage slots x 5 (4 statuses + the "+N" overflow chip),
            // flattened slot-major; 3 PC plates x 5, flattened card-major.
            //
            // ONE COUNT WHERE THERE WERE TWO (2026-09-10). The column used to
            // carry 10 roster badges plus 12 on the acting card -- two ranges
            // of different widths that PaintStatusRow had to be told about
            // separately, and an ally's statuses were readable in five slots
            // while the acting character's were readable in twelve. Every
            // party member gets the same five now, which is what the
            // controller's PcStatusBadgeCount has to agree with.
            Assert.AreEqual(15, screen.EnemyStatusBadges.Count);
            Assert.AreEqual(3, screen.EnemyStatusStrips.Count);
            Assert.AreEqual(15, screen.PcStatusBadges.Count);

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

            // ONE ANATOMY ON EVERY SURFACE. The first capture of this
            // feature had the party row NOT carrying its own Glyph/Counter
            // children, which forced a tinted root Image to double as the
            // glyph and Code to carry a folded caption; every badge in the
            // game comes out of BuildStatusBadge now, so this walks the PC
            // plates with the same assertions as the enemy rows above.
            for (int r = 0; r < FightScreen.PcPlateCount; r++)
            {
                for (int i = 0; i < FightScreen.PcStatusBadgesPerPlate; i++)
                {
                    var name = $"PcStatusBadge{r}_{i}";
                    var badge = Find(root, name);
                    Assert.IsNotNull(badge, $"missing {name}");
                    Assert.IsNotNull(Find(badge, "Glyph"), $"{name} has no Glyph child");
                    Assert.IsNotNull(Find(badge, "Code"), $"{name} has no Code child");
                    Assert.IsNotNull(Find(badge, "Counter"), $"{name} has no Counter child");
                }
            }

            // THE BOX, not a tooltip. StatusTooltip/StatusTooltipText were
            // removed outright (2026-09-19): they answered for ONE badge,
            // sat beside that badge, and a pad -- which has no badge to
            // hover -- could not open them at all. PartyBuffTooltip went the
            // same way one pass earlier.
            Assert.IsNull(Find(root, "StatusTooltip"));
            Assert.IsNull(Find(root, "PartyBuffTooltip"));

            Assert.IsNotNull(Find(root, "StatusBox"));
            for (int i = 0; i < FightScreen.StatusBoxRows; i++)
            {
                Assert.IsNotNull(Find(root, $"StatusBoxIcon{i}"), $"missing StatusBoxIcon{i}");
                Assert.IsNotNull(Find(root, $"StatusBoxText{i}"), $"missing StatusBoxText{i}");
            }
        }

        // The box is the one surface that answers "what is on this actor" in
        // full, so its geometry is pinned here rather than left to the eye.
        [Test]
        public void TheStatusBoxStacksItsRowsInsideItsOwnPadding()
        {
            float boxHeight = FightScreen.StatusBoxMaxHeight;
            float rowHeight = FightScreen.StatusBoxRowMaxHeight;

            float firstTop = FightScreen.StatusBoxRowCentreY(boxHeight, 0f, rowHeight) + rowHeight * 0.5f;
            Assert.AreEqual(boxHeight * 0.5f - FightScreen.StatusBoxPad, firstTop, 0.01f,
                "row 0's top edge must sit exactly one pad below the box's top");

            float usedByAll = FightScreen.StatusBoxRows * rowHeight
                              + (FightScreen.StatusBoxRows - 1) * FightScreen.StatusBoxRowGap;
            float lastBottom = FightScreen.StatusBoxRowCentreY(boxHeight,
                usedByAll - rowHeight, rowHeight) - rowHeight * 0.5f;
            Assert.AreEqual(-boxHeight * 0.5f + FightScreen.StatusBoxPad, lastBottom, 0.01f,
                "the last row's bottom edge must sit exactly one pad above the box's bottom - " +
                "StatusBoxMaxHeight is derived from the row count, so a row that does not fit " +
                "means the derivation and the stacking disagree");
        }

        // UNDER THE ACTOR, ABOVE IT WHEN UNDER DOES NOT FIT. Both arms are
        // reachable on the real screen, which is why the flip exists: the
        // rear enemy slot's badge row bottoms out well short of the room a
        // full eight-row box needs below it on the shortest audited canvas.
        [Test]
        public void TheStatusBoxHangsUnderItsActorAndFlipsAboveWhenTheCanvasFloorIsCloser()
        {
            const float Margin = 8f;
            float halfW = 1920f * 0.5f - Margin;
            float halfH = 1080f * 0.5f - Margin;

            // A near enemy with a short box: plenty of floor left.
            var under = FightScreen.StatusBoxAt(300f, -280f, -100f,
                FightScreen.StatusBoxWidth, 90f, -halfW, halfW, -halfH, halfH);
            Assert.Less(under.Y, -280f, "a box that fits below its actor hangs below it");
            Assert.AreEqual(300f, under.X, 0.01f, "and is centred on the actor");

            // The same actor with the tallest box the runtime can build.
            var flipped = FightScreen.StatusBoxAt(300f, -280f, -100f,
                FightScreen.StatusBoxWidth, FightScreen.StatusBoxMaxHeight, -halfW, halfW, -halfH, halfH);
            Assert.Greater(flipped.Y, -100f, "a box with no room below its actor flips above it");
            Assert.LessOrEqual(flipped.Y + FightScreen.StatusBoxMaxHeight * 0.5f, halfH + 0.01f,
                "and still lands inside the canvas");

            // An actor at the right-hand edge: x is pulled back inside.
            var clamped = FightScreen.StatusBoxAt(halfW, -280f, -100f,
                FightScreen.StatusBoxWidth, 90f, -halfW, halfW, -halfH, halfH);
            Assert.AreEqual(halfW - FightScreen.StatusBoxWidth * 0.5f, clamped.X, 0.01f,
                "the box never hangs off the side of the canvas to stay centred on its actor");
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

            for (int r = 0; r < FightScreen.PcPlateCount; r++)
            {
                for (int i = 0; i < FightScreen.PcStatusBadgesPerPlate; i++)
                {
                    var name = $"PcStatusBadge{r}_{i}";
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
            var fills = new List<string>();
            for (int i = 0; i < FightScreen.PcPlateCount; i++)
            {
                fills.Add($"PcPlate{i}HpFill");
                fills.Add($"PcPlate{i}MpFill");
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

        // The two numbers are drawn ON their own bars, which is the only way
        // two meters and a name fit on an 80px strip -- so each value label
        // has to be INSIDE its own track's rect, not beside it.
        [Test]
        public void ThePcValuesAreDrawnOnTheirOwnBars()
        {
            for (int i = 0; i < FightScreen.PcPlateCount; i++)
            {
                Assert.IsTrue(RectOf($"PcPlate{i}HpBar").Contains(RectOf($"PcPlate{i}HpValue")),
                    $"plate {i}: the HP value has slipped off its bar");
                Assert.IsTrue(RectOf($"PcPlate{i}MpBar").Contains(RectOf($"PcPlate{i}MpValue")),
                    $"plate {i}: the pool value has slipped off its bar");
            }
        }

        // The plate's reading order: name, signature and badges across the
        // top; the two meters side by side under them, split by a hairline.
        // Checked as ORDER and CLEARANCE rather than as coordinates -- the
        // numbers are free to be retuned, the reading order is not.
        [Test]
        public void EveryPlateStacksItsNameRowAboveItsTwoMeters()
        {
            for (int i = 0; i < FightScreen.PcPlateCount; i++)
            {
                var plate = RectOf($"PcPlate{i}");
                var name = RectOf($"PcPlate{i}Name");
                var signature = RectOf($"PcPlate{i}Signature");
                var hpBar = RectOf($"PcPlate{i}HpBar");
                var mpBar = RectOf($"PcPlate{i}MpBar");

                Assert.LessOrEqual(hpBar.Top, name.Bottom, $"plate {i}: the bars must sit UNDER the name row");
                Assert.AreEqual(hpBar.Centre.Y, mpBar.Centre.Y, 0.01f,
                    $"plate {i}: the pool bar sits at the SAME height as the HP bar, not below it");
                Assert.LessOrEqual(hpBar.Right, mpBar.Left, $"plate {i}: HP on the left half, the pool on the right");

                Assert.LessOrEqual(name.Right, signature.Left,
                    $"plate {i}: the signature reads after the name, on the same row");
                Assert.AreEqual(name.Centre.Y, signature.Centre.Y, 0.01f,
                    $"plate {i}: name and signature share the top row");

                for (int k = 0; k < FightScreen.PcStatusBadgesPerPlate; k++)
                {
                    var badge = RectOf($"PcStatusBadge{i}_{k}");
                    Assert.LessOrEqual(signature.Right, badge.Left,
                        $"plate {i} badge {k} has walked into the signature");
                    Assert.IsTrue(plate.Contains(badge), $"plate {i} badge {k} escapes the plate");
                }
            }
        }

        // BADGES RUN LEFT TO RIGHT IN INDEX ORDER, which PaintStatusRow
        // depends on: it walks a flat range and fills it in order, so a
        // right-to-left flattening would put the first status furthest right
        // and the "+N" overflow chip on the left end of the row.
        [Test]
        public void EveryPlatesBadgesRunLeftToRightInIndexOrder()
        {
            for (int i = 0; i < FightScreen.PcPlateCount; i++)
            {
                for (int k = 0; k + 1 < FightScreen.PcStatusBadgesPerPlate; k++)
                {
                    Assert.Less(RectOf($"PcStatusBadge{i}_{k}").Centre.X,
                        RectOf($"PcStatusBadge{i}_{k + 1}").Centre.X,
                        $"plate {i}: badge {k} must sit left of badge {k + 1}");
                    Assert.AreEqual(RectOf($"PcStatusBadge{i}_{k}").Centre.Y,
                        RectOf($"PcStatusBadge{i}_{k + 1}").Centre.Y, 0.01f,
                        $"plate {i}: badges {k} and {k + 1} are on the same line");
                }
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
            // so it shows on every PC plate rather than only for whoever is
            // acting -- which the strip, describing only the acting
            // character, never could.
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

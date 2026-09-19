using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // Pins HubController.WireNavigation's hubRing shape without a scene --
    // the same UiNavLinkBuilder.Build() call RuntimeNavWiring.Apply drives at
    // runtime, fed the five buildings in the order HubController now derives
    // them (ascending HubAnchors x, gate included by its own fixed x), rather
    // than the hand-typed order a9f89ebe shipped.
    //
    // WHY THIS EXISTS (2026-09-19 owner report, after a9f89ebe's own fix):
    // "In the main menu, it still is not ordered properly. If you press left
    // twice you are left middle for example." a9f89ebe hand-typed the ring as
    // Talents<->Principality<->Gate<->Relics<->CharacterSheet -- the literal
    // order the owner's 2026-09-18 words gave ("from the gate, Left goes
    // Principality then Talents") -- but on screen Talents (x -349) sits
    // BETWEEN Principality (x -673) and the gate (x 0), so that order made
    // Left overshoot Talents on the first press and land back on it, reading
    // as "middle", on the second. HubAnchorsTests already pins the x values
    // this file sorts by (Principality -673, Relics 295); Talents (-349) and
    // CharacterSheet (628) are HubController's own screenshot-measured
    // figures (its WireNavigation header), pinned here as literals rather
    // than recomputed, same as HubAnchorsTests' own rule (CLAUDE.md's fifth
    // gotcha) -- a formula test that rebuilds its own expected value from the
    // formula under test proves nothing.
    //
    // Literal names, not indices: a reviewer can read "gate.Left is talents"
    // without cross-referencing an array position.
    public class HubRingAdjacencyTests
    {
        private UiNode _gate, _principality, _talents, _relics, _characterSheet;
        private IReadOnlyDictionary<UiNode, UiNavLinkBuilder.NavLinkTargets<UiNode>> _resolved;

        private static UiNode Btn(string name) => Ui.Button(name, UiString.FromContent(name), new UiVec(100f, 40f), 18);

        [SetUp]
        public void BuildTheRing()
        {
            _gate = Btn("StartRunGate");
            _principality = Btn("PrincipalityBuilding");
            _talents = Btn("TalentsBuilding");
            _relics = Btn("RelicsBuilding");
            _characterSheet = Btn("CharacterSheetBuilding");

            // The same sort HubController.WireNavigation performs: ascending
            // by each control's actual on-screen x. Literal x's, per this
            // file's own header.
            var ordered = new[]
            {
                (node: _principality, x: -673f),
                (node: _talents, x: -349f),
                (node: _gate, x: 0f),
                (node: _relics, x: 295f),
                (node: _characterSheet, x: 628f),
            }
            .OrderBy(p => p.x)
            .Select(p => p.node)
            .ToArray();

            var group = new UiNavGroup<UiNode>("hubRing", UiNavGroupKind.Rail, ordered);
            var nav = new UiNavDeclaration<UiNode>(new[] { group });
            _resolved = UiNavLinkBuilder.Build(nav);
        }

        [Test]
        public void Left_FromTheGate_ReachesTalents_ThenPrincipality()
        {
            Assert.AreEqual(_talents, _resolved[_gate].Left,
                "Talents (x -349) is the nearest thing left of the gate (x 0) on screen");
            Assert.AreEqual(_principality, _resolved[_talents].Left,
                "Principality (x -673) is the next nearest, one more press further left");
        }

        [Test]
        public void Right_FromRelics_ReachesCharacterSheet()
        {
            Assert.AreEqual(_characterSheet, _resolved[_relics].Right,
                "Character Sheet (x 628) is the nearest thing right of Relics (x 295) -- " +
                "the owner's other stated expectation, unchanged by the 2026-09-19 fix");
        }

        [Test]
        public void TheRingIsMonotonicByScreenX_NoOvershootAndRetreat()
        {
            // The defect this whole file exists to close: a hand-typed order
            // that puts the FARTHER building before the nearer one makes a
            // repeated press in one direction jump past a building and then
            // double back onto it, which reads as "left twice you are left
            // middle" rather than as steady progress toward the edge.
            Assert.AreEqual(_gate, _resolved[_talents].Right, "stepping right from Talents returns to the gate");
            Assert.AreEqual(_relics, _resolved[_gate].Right, "stepping right from the gate reaches Relics");
            Assert.AreEqual(_talents, _resolved[_principality].Right, "stepping right from Principality reaches Talents");
        }
    }
}

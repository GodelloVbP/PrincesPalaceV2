using System.Collections.Generic;
using NUnit.Framework;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Tests
{
    // The Reckoning board's pad graph (docs/PLAN_REWARD_SCREENS.md C8), as the
    // groups and links the controller hands RuntimeNavWiring, resolved by the
    // same UiNavLinkBuilder. Entry is Continue; Up reaches the haul strip (a
    // clamped rail), Up again the portraits (a wrapped rail); Down returns.
    public class ReckoningBoardNavTests
    {
        private sealed class Node
        {
            public readonly string Name;
            public Node(string name) { Name = name; }
            public override string ToString() => Name;
        }

        private static readonly Node P0 = new Node("P0"), P1 = new Node("P1"), P2 = new Node("P2");
        private static readonly Node H0 = new Node("H0"), H1 = new Node("H1");
        private static readonly Node Go = new Node("Continue");

        private static IReadOnlyDictionary<Node, UiNavLinkBuilder.NavLinkTargets<Node>> Resolve(
            ReckoningBoardNav<Node> board) =>
            UiNavLinkBuilder.Build(new UiNavDeclaration<Node>(board.Groups, board.Links));

        private static UiNavLinkBuilder.NavLinkTargets<Node> At(
            IReadOnlyDictionary<Node, UiNavLinkBuilder.NavLinkTargets<Node>> resolved, Node node) =>
            resolved.TryGetValue(node, out var targets) ? targets : new UiNavLinkBuilder.NavLinkTargets<Node>();

        [Test]
        public void TheEntryIsContinue()
        {
            var board = ReckoningBoardNav<Node>.For(new[] { P0, P1, P2 }, new[] { H0, H1 }, Go);
            Assert.AreSame(Go, board.Entry);
        }

        [Test]
        public void FullBoard_UpFromContinue_IsTheHaul_ThenThePortraits_AndDownReturns()
        {
            var board = ReckoningBoardNav<Node>.For(new[] { P0, P1, P2 }, new[] { H0, H1 }, Go);
            var nav = Resolve(board);

            Assert.AreSame(H0, At(nav, Go).Up, "Continue's Up is the haul strip");
            Assert.IsNull(At(nav, Go).Down);
            Assert.IsNull(At(nav, Go).Left);
            Assert.IsNull(At(nav, Go).Right);

            Assert.AreSame(P0, At(nav, H0).Up, "the haul's Up is the portraits");
            Assert.AreSame(P0, At(nav, H1).Up);
            Assert.AreSame(Go, At(nav, H0).Down, "the haul's Down returns to Continue");
            Assert.AreSame(Go, At(nav, H1).Down);

            Assert.AreSame(H0, At(nav, P0).Down, "a portrait's Down returns to the haul");
            Assert.AreSame(H0, At(nav, P2).Down);
            Assert.IsNull(At(nav, P1).Up, "nothing sits above the portraits");
        }

        [Test]
        public void TheHaulIsAClampedRail()
        {
            var nav = Resolve(ReckoningBoardNav<Node>.For(new[] { P0, P1, P2 }, new[] { H0, H1 }, Go));

            Assert.AreSame(H1, At(nav, H0).Right);
            Assert.IsNull(At(nav, H0).Left, "the strip clamps at its first chip");
            Assert.AreSame(H0, At(nav, H1).Left);
            Assert.IsNull(At(nav, H1).Right, "the strip clamps at its last chip");
        }

        [Test]
        public void ThePortraitsAreAWrappedRail()
        {
            var nav = Resolve(ReckoningBoardNav<Node>.For(new[] { P0, P1, P2 }, new[] { H0, H1 }, Go));

            Assert.AreSame(P1, At(nav, P0).Right);
            Assert.AreSame(P2, At(nav, P0).Left, "the portraits wrap");
            Assert.AreSame(P0, At(nav, P2).Right);
        }

        [Test]
        public void NoHaul_TheColumnClosesUp_ContinueToPortraitsAndBack()
        {
            // The outcome form has no haul; neither does a fight that paid
            // nothing but experience.
            var nav = Resolve(ReckoningBoardNav<Node>.For(new[] { P0, P1 }, new Node[0], Go));

            Assert.AreSame(P0, At(nav, Go).Up);
            Assert.AreSame(Go, At(nav, P0).Down);
            Assert.AreSame(Go, At(nav, P1).Down);
        }

        [Test]
        public void ContinueAlone_HasNoNeighbours()
        {
            var board = ReckoningBoardNav<Node>.For(new Node[0], new Node[0], Go);
            var nav = Resolve(board);

            Assert.AreEqual(0, board.Groups.Count);
            Assert.IsNull(At(nav, Go).Up);
            Assert.AreSame(Go, board.Entry);
        }

        [Test]
        public void TheGroupsAreNamed_PortraitsThenHaul()
        {
            var board = ReckoningBoardNav<Node>.For(new[] { P0 }, new[] { H0 }, Go);

            Assert.AreEqual(2, board.Groups.Count);
            Assert.AreEqual("reckoningPortraits", board.Groups[0].Id);
            Assert.AreEqual(UiNavGroupKind.Rail, board.Groups[0].Kind);
            Assert.AreEqual(UiNavWrap.Wrap, board.Groups[0].EffectiveWrap);
            Assert.AreEqual("reckoningHaul", board.Groups[1].Id);
            Assert.AreEqual(UiNavGroupKind.Rail, board.Groups[1].Kind);
            Assert.AreEqual(UiNavWrap.Clamp, board.Groups[1].EffectiveWrap);
        }
    }
}

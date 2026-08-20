using System;
using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.UiKit
{
    // The screen-authoring vocabulary. Every screen in the game is a plain C#
    // function returning one of these trees.
    //
    // An embedded DSL rather than JSON or ScriptableObjects, deliberately: this
    // keeps closures, static types, refactoring and go-to-definition, and it
    // keeps screen descriptions out of the "hand-authored asset" category that
    // rule 1 exists to forbid.
    //
    // Note what is missing from every child signature: a position. Flow children
    // cannot be given one, because the container owns spacing and padding. That
    // absence is the mechanism, not an oversight.
    public static class Ui
    {
        public static UiNode Panel(string name, UiSize size, params UiNode[] children) =>
            Node(name, UiNodeKind.Panel, Place.Stretch(), size, children);

        public static UiNode Panel(string name, UiSize size, IEnumerable<UiNode> children) =>
            Node(name, UiNodeKind.Panel, Place.Stretch(), size, children);

        public static UiNode Panel(string name, Place place, UiSize size, params UiNode[] children) =>
            Node(name, UiNodeKind.Panel, place, size, children);

        public static UiNode Panel(string name, Place place, UiSize size, IEnumerable<UiNode> children) =>
            Node(name, UiNodeKind.Panel, place, size, children);

        public static UiNode Column(string name, Place place, float spacing, UiAlign align, params UiNode[] children) =>
            Flow(name, UiNodeKind.Column, place, spacing, align, children);

        public static UiNode Column(string name, Place place, float spacing, UiAlign align, IEnumerable<UiNode> children) =>
            Flow(name, UiNodeKind.Column, place, spacing, align, children);

        public static UiNode Row(string name, Place place, float spacing, UiAlign align, params UiNode[] children) =>
            Flow(name, UiNodeKind.Row, place, spacing, align, children);

        public static UiNode Row(string name, Place place, float spacing, UiAlign align, IEnumerable<UiNode> children) =>
            Flow(name, UiNodeKind.Row, place, spacing, align, children);

        public static UiNode Grid(string name, Place place, int columns, UiVec cell, UiVec gap, IEnumerable<UiNode> children)
        {
            var node = Node(name, UiNodeKind.Grid, place, UiSize.FromChildren(), children);
            node.Columns = Math.Max(1, columns);
            node.Cell = cell;
            node.Gap = gap;
            return node;
        }

        public static UiNode Label(string name, UiString text, UiVec size, int fontSize = 24,
                                   string colorHex = null, Place? place = null)
        {
            RequireText(text, name, nameof(Label));
            var node = Node(name, UiNodeKind.Label, place ?? Place.Flow, UiSize.Fixed(size));
            node.Text = text;
            node.FontSize = fontSize;
            node.ColorHex = colorHex;
            return node;
        }

        public static UiNode Button(string name, UiString text, UiVec size, int fontSize = 24, Place? place = null)
        {
            RequireText(text, name, nameof(Button));
            var node = Node(name, UiNodeKind.Button, place ?? Place.Flow, UiSize.Fixed(size));
            node.Text = text;
            node.FontSize = fontSize;
            return node;
        }

        public static UiNode Sprite(string name, string spriteKey, UiVec size, Place? place = null)
        {
            var node = Node(name, UiNodeKind.Sprite, place ?? Place.Flow, UiSize.Fixed(size));
            node.SpriteKey = spriteKey;
            return node;
        }

        public static UiNode Sprite(string name, string spriteKey, Place place, UiSize size)
        {
            var node = Node(name, UiNodeKind.Sprite, place, size);
            node.SpriteKey = spriteKey;
            return node;
        }

        public static UiNode Solid(string name, string colorHex, UiVec size, Place? place = null)
        {
            var node = Node(name, UiNodeKind.Solid, place ?? Place.Flow, UiSize.Fixed(size));
            node.ColorHex = colorHex;
            return node;
        }

        public static UiNode Solid(string name, string colorHex, Place place, UiSize size)
        {
            var node = Node(name, UiNodeKind.Solid, place, size);
            node.ColorHex = colorHex;
            return node;
        }

        // THE FOUR 1px EDGES THAT TRACE A BOX.
        //
        // Written out four times before this -- the system menu's panel, the
        // Options card, the Run statistics card and the Main menu's plates --
        // with the same half-pixel inset arithmetic copied each time. That
        // inset is the fiddly part and the reason this is worth a helper: a
        // 1px node centred exactly on the boundary pokes half a pixel outside
        // its parent, which the containment audit is right to refuse, so every
        // copy carried the same +/- 0.5 correction and any one of them could
        // have lost it.
        //
        // Decor throughout: a rim is drawn, never pressed.
        // NO `reason` PARAMETER, and its absence is the point.
        //
        // This took one, threaded it to all four edges, and spent it on an
        // AllowOverlap that could never fire: the edges are AsDecor, and A1
        // exempts decoration outright. Five call sites were writing "the rim
        // traces the plate it encloses" into a string that reached nothing, and
        // four inert allowances came out of every one of them -- twenty in
        // total, which no search for `.AsDecor().AllowOverlap(...)` could see,
        // because the pair only ever existed here.
        //
        // A7 found them, which is the argument for a rule over a sweep: a sweep
        // sees the shapes you thought of.
        public static IEnumerable<UiNode> Rim(
            string stem, UiVec size, string colorHex, UiVec centre = default)
        {
            float w = size.X;
            float h = size.Y;

            yield return RimEdge($"{stem}RimTop", new UiVec(w, 1f), centre.X, centre.Y + h * 0.5f - 0.5f, colorHex);
            yield return RimEdge($"{stem}RimBottom", new UiVec(w, 1f), centre.X, centre.Y - h * 0.5f + 0.5f, colorHex);
            yield return RimEdge($"{stem}RimLeft", new UiVec(1f, h), centre.X - w * 0.5f + 0.5f, centre.Y, colorHex);
            yield return RimEdge($"{stem}RimRight", new UiVec(1f, h), centre.X + w * 0.5f - 0.5f, centre.Y, colorHex);
        }

        private static UiNode RimEdge(string name, UiVec size, float x, float y, string colorHex) =>
            Solid(name, colorHex, size, Place.At(x, y))
                .AsDecor();

        // These nodes are ALTERNATIVES: one box, one at a time.
        //
        // Tab pages, wizard phases, the panes of a menu -- anything a
        // controller switches between. They are declared at the same place and
        // the same size deliberately, so A1 has to be told, and until now they
        // told it with AllowOverlap and a sentence. That worked and said too
        // much: A1 skips a pair when EITHER side carries a reason, so a page
        // exempted to sit on its sibling pages stopped being checked against
        // everything else on the screen as well, permanently.
        //
        // Call it once where the set is complete, and add the members as
        // children as usual -- this marks them, it does not group them in the
        // tree. Nothing about the emitted hierarchy changes, which is the point:
        // the controllers that switch these find them by name.
        //
        // A8 checks the other half of the claim, that at most one of them is
        // active at declaration time. "Exactly one is ever active" was written
        // in six places and verified in none.
        public static IReadOnlyList<UiNode> Exclusive(params UiNode[] nodes) =>
            Exclusive((IEnumerable<UiNode>)nodes);

        public static IReadOnlyList<UiNode> Exclusive(IEnumerable<UiNode> nodes)
        {
            var list = nodes?.Where(n => n != null).ToList() ?? new List<UiNode>();

            if (list.Count < 2)
            {
                throw new ArgumentException(
                    $"Ui.Exclusive needs at least two alternatives to mean anything; got {list.Count}. " +
                    "A single node that overlaps something is not a set of pages -- if it sits ON a " +
                    "specific sibling, say that instead.");
            }

            var token = new object();
            foreach (var node in list) node.ExclusiveGroup = token;
            return list;
        }

        // Blank main-axis extent inside a flow container. Exists so a gap can be
        // declared where it happens rather than smuggled into a neighbour's
        // size, which is how v1's offsets became unreadable.
        public static UiNode Space(float pixels) =>
            Node("Space", UiNodeKind.Space, Place.Flow, UiSize.Fixed(pixels, pixels));

        public static UiNode NestedCanvas(string name, int sortingOrder, params UiNode[] children)
        {
            var node = Node(name, UiNodeKind.NestedCanvas, Place.Stretch(), UiSize.Fill, children);
            node.SortingOrder = sortingOrder;
            return node;
        }

        // A dimmer plus its content, with the stacking stated once.
        //
        // Stacking comes from SIBLING ORDER, not from a canvas. This docstring
        // used to claim the emitter "always sets overrideSorting on the canvas
        // it creates" -- it creates no canvas for a Modal at all, and there is
        // no UiNodeKind.Modal case in UiEmitter. The claim was aspirational and
        // read as a guarantee, which is worse than saying nothing.
        //
        // What actually retires v1's inert-BarkCanvas bug is that a modal is
        // emitted as an ordinary subtree in declaration order, so "declared
        // later draws on top" is the only rule, with no second sorting system
        // that can silently disagree with it.
        public static UiNode Modal(string name, string dimmerHex, UiNode content)
        {
            return Node(name, UiNodeKind.Modal, Place.Stretch(), UiSize.Fill, new[]
            {
                Solid(name + "Dimmer", dimmerHex, Place.Stretch(), UiSize.Fill)
                    .AllowOverlap("a modal dimmer covers the whole screen by definition, including its own content"),
                content,
            })
            // Declared once, in the factory, rather than at every modal site: a
            // modal is drawn OVER whatever it interrupts, and taking the clicks
            // meant for what is behind it is the entire job.
            .AllowOverlap("a modal deliberately covers its siblings - intercepting their input is what modal means");
        }

        // ONE declaration that is simultaneously the element count, the names,
        // and the refs the wiring binds. This is what makes v1's Store bug
        // (builder sized the strip off Items, controller filled it from
        // Consumables) unwritable: there is no second place to state a count.
        public static IEnumerable<UiNode> Each<T>(IReadOnlyList<T> source, Func<T, int, UiNode> build)
        {
            if (source == null) yield break;
            for (int i = 0; i < source.Count; i++)
            {
                yield return build(source[i], i);
            }
        }

        // Fixed-capacity placeholders for anything positioned at runtime (the
        // descent map's tiles and links). Capacity comes from a Domain constant
        // the runtime view reads too, so the pool cannot be sized from one
        // number and filled from another.
        public static UiNode Pool(string name, int capacity, Func<int, UiNode> build)
        {
            var children = new List<UiNode>(capacity);
            for (int i = 0; i < capacity; i++) children.Add(build(i));
            return Node(name, UiNodeKind.Pool, Place.Stretch(), UiSize.Fill, children);
        }

        private static UiNode Flow(string name, UiNodeKind kind, Place place, float spacing, UiAlign align,
                                   IEnumerable<UiNode> children)
        {
            var node = Node(name, kind, place, UiSize.FromChildren(), children);
            node.Spacing = spacing;
            node.Align = align;
            return node;
        }

        private static UiNode Node(string name, UiNodeKind kind, Place place, UiSize size,
                                   IEnumerable<UiNode> children = null)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Every node needs a name - names are how tests and the screenshot tool find things.", nameof(name));
            }

            return new UiNode
            {
                Name = name,
                Kind = kind,
                Place = place,
                Size = size,
                Children = children?.Where(c => c != null).ToList() ?? new List<UiNode>(),
            };
        }

        private static void RequireText(UiString text, string name, string factory)
        {
            if (!text.IsValid)
            {
                throw new ArgumentException(
                    $"Ui.{factory}(\"{name}\") was given an empty UiString. Declare it in UiStrings, or mark it as " +
                    $"data with UiString.FromContent(value).");
            }
        }
    }
}

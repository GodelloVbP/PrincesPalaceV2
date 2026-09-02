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

        // A BUTTON SIZED BY ITS ANCHORS rather than by a fixed box.
        //
        // Every other button on every screen has a size it was designed at,
        // which is why the fixed-size overload below is the one that reads like
        // the primary. This one exists for a control that has to TRACK
        // something the runtime resizes: the fight's enemy hit areas fill a
        // stage slot whose rect is set from the real sprite on load, so a fixed
        // box authored here would be the wrong shape for every monster.
        public static UiNode Button(string name, UiString text, Place place, UiSize size,
            int fontSize = 24)
        {
            RequireText(text, name, nameof(Button));
            var node = Node(name, UiNodeKind.Button, place, size);
            node.Text = text;
            node.FontSize = fontSize;
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

        // These nodes are ONE WIDGET, stacked on purpose.
        //
        // A fill inside its track, a label over its own plate, the ring and
        // portrait and initial of a single badge. Unlike Exclusive they are all
        // on screen together -- the stacking IS the thing, and the draw order
        // is already decided by the order they are declared in.
        //
        // Symmetric, because for the audit's purposes it is: which one is on
        // top is a fact about declaration order, not something a second word
        // should restate. "Over" and "under" would be two names for one
        // relationship, and one of them would eventually disagree with the code.
        //
        // Only what it says: these nodes are exempt from EACH OTHER. Each is
        // still checked against every other sibling, which the AllowOverlap this
        // replaces was not.
        public static IReadOnlyList<UiNode> Layered(params UiNode[] nodes) =>
            Layered((IEnumerable<UiNode>)nodes);

        public static IReadOnlyList<UiNode> Layered(IEnumerable<UiNode> nodes)
        {
            var list = nodes?.Where(n => n != null).ToList() ?? new List<UiNode>();

            if (list.Count < 2)
            {
                throw new ArgumentException(
                    $"Ui.Layered needs at least two layers to mean anything; got {list.Count}. " +
                    "One node is not a stack -- if it sits on something, name the something.");
            }

            var token = new object();
            foreach (var node in list) node.LayerGroup = token;
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

        // Same shape as NestedCanvas, but ScreenSpaceCamera instead of
        // Overlay -- see UiNode.WorldInterleaved for why that distinction is
        // load-bearing rather than cosmetic. `sortingLayerName` places this
        // canvas's WHOLE subtree in the SAME SortingLayer comparison as
        // whatever world-space SpriteRenderers it needs to sandwich against
        // by SortingOrder; pass the layer those renderers use.
        public static UiNode WorldInterleavedCanvas(string name, string sortingLayerName, int sortingOrder, params UiNode[] children)
        {
            var node = Node(name, UiNodeKind.NestedCanvas, Place.Stretch(), UiSize.Fill, children);
            node.SortingOrder = sortingOrder;
            node.WorldInterleaved = true;
            node.SortingLayerName = sortingLayerName;
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

        // ---- themed buttons ----------------------------------------------------
        //
        // Called by UiNode.Themed(), not directly -- see that method's own
        // comment for why the split exists. Builds Visuals(Glow, Plate) +
        // Label as REAL UiNode children, so the same tree UiAudit and
        // UiKitAuditTests already walk sees a themed button's true shape.
        // UiEmitter reads these children back rather than inventing its own,
        // which is the other half of that guarantee.
        internal static void ApplyTheme(UiNode node, ButtonTheme theme)
        {
            if (node.Kind != UiNodeKind.Button)
            {
                throw new ArgumentException(
                    $"Themed({theme}) only applies to a Button node; '{node.Name}' is a {node.Kind}.");
            }

            if (node.Chromeless)
            {
                throw new ArgumentException(
                    $"'{node.Name}' is both Chromeless and Themed({theme}) - no plate at all and a themed " +
                    "plate are contradictory. Drop whichever one this button does not actually want.");
            }

            node.Theme = theme;

            var buttonSize = new UiVec(node.Size.X, node.Size.Y);

            // Larger than the plate on every edge, so a focused button reads
            // as lit rather than as a second, smaller plate underneath the
            // first. Overflows Visuals by design - see the AllowOverflow.
            const float GlowMargin = 28f;
            var glowSize = new UiVec(buttonSize.X + GlowMargin, buttonSize.Y + GlowMargin);

            // radial_glow: a white radial falloff with no colour of its own,
            // tinted per theme through ColorHex exactly like every other use
            // of that one baked asset (stars, motes, window bloom). Alpha 0 at
            // idle - ThemedButtonState fades it up on focus, so this is not
            // authoring a glow that is lit from the very first frame.
            var glow = Sprite("Glow", "proc:radial_glow", glowSize, Place.At(0f, 0f))
                .AsDecor()
                .Coloured(ThemeGlowHex(theme))
                .AllowOverflow(
                    "the focus glow is deliberately larger than the plate it sits behind so a focused " +
                    "button reads as lit, not merely outlined");

            var plate = Sprite("Plate", $"UI/Buttons/Processed/button_plate_{ThemeKey(theme)}.png",
                buttonSize, Place.At(0f, 0f));

            // Glow under Plate (declared first, drawn first) and exempt from
            // each other - a glow behind its own plate is one widget, not two
            // things that happen to share a box.
            Layered(glow, plate);

            var visuals = Panel("Visuals", Place.Stretch(), UiSize.Fill, glow, plate);

            var label = Label(node.Name + "Label", node.Text, buttonSize,
                node.FontSize > 0 ? node.FontSize : 29, "#FFFFFFFF", Place.Stretch());
            label.Tracking = ButtonTracking(node.Text);

            // Visuals and Label occupy the SAME box on purpose - the caption
            // sits on its own plate, which is exactly what Layered exists to
            // say. Without it, A1 reads Label as a real graphic sitting on top
            // of Visuals' own (invisible) rect and reports the collision the
            // bare-container exemption protects against in the OTHER
            // direction only (a drawn sibling on top of an empty frame), not
            // this one (a drawn sibling on top of an empty frame's own
            // drawn contents).
            Layered(visuals, label);

            node.Children.Add(visuals);
            node.Children.Add(label);
        }

        // <=7 chars tracks the most (a short word needs the most help reading
        // as more than a stub), <=10 a little, longer none. The same design
        // instinct SystemMenuLayout.TabLabelTracking states as a fixed .14em,
        // scaled down here because a button's own word is read alone rather
        // than in a row of others exactly like it.
        public static float ButtonTracking(UiString text)
        {
            string display = text.IsTemplated ? text.AuditSample : text.Template;
            int length = display?.Length ?? 0;
            if (length <= 7) return 3f;
            if (length <= 10) return 1f;
            return 0f;
        }

        private static string ThemeKey(ButtonTheme theme) => theme.ToString().ToLowerInvariant();

        // The glow's own tint per theme - NOT the plate's colour, which is
        // baked into its PNG. Not reused from FightHudPalette: those tokens
        // answer a different question (a fight HUD's own borders and text),
        // and a coincidental hex match there would be exactly that, a
        // coincidence, not a shared role - see UiKitLintTests' own reasoning
        // on ItemStatLines.HeadingHex for the same call made the other way.
        private static string ThemeGlowHex(ButtonTheme theme)
        {
            switch (theme)
            {
                case ButtonTheme.Gold: return "#F0CE7A00";
                case ButtonTheme.Crimson: return "#D9584A00";
                case ButtonTheme.Violet: return "#B98CE000";
                case ButtonTheme.Blue: return "#5FB0E600";
                case ButtonTheme.Green: return "#78CE7200";
                default: return "#D6D6E000"; // Silver
            }
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

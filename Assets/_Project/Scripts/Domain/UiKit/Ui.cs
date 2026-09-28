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
        // EVERY BUTTON GETS THE HOVER/FOCUS RIM BY DEFAULT, built right here
        // at declaration time (AttachDefaultHoverBox below) -- owner's call,
        // 2026-09-23 (second pass): "every hoverable/focusable element in the
        // game shows a solid box on hover and on gamepad focus." Before this,
        // only the ~15 screens that remembered to call .Hovers() got one, and
        // every OTHER unthemed button's only hover/focus feedback was the
        // press animator's now-removed scale-pop -- about 30 call sites
        // across 15 screens went silent the moment that pop was pulled. This
        // is the seam that fixes all of them without editing any of their
        // files: change what Button() builds, not what 30 call sites ask for.
        //
        // .Themed()/.ThemedPlate() strip this rim back off (ThemedButtonState
        // already drives that button's own focus visual on Glow/Plate) and
        // .NoHoverBox("reason") strips it for a control that draws its own --
        // both work regardless of which order they run in relative to this.
        public static UiNode Button(string name, UiString text, Place place, UiSize size,
            int fontSize = 24)
        {
            RequireText(text, name, nameof(Button));
            var node = Node(name, UiNodeKind.Button, place, size);
            node.Text = text;
            node.FontSize = fontSize;
            AttachDefaultHoverBox(node);
            return node;
        }

        public static UiNode Button(string name, UiString text, UiVec size, int fontSize = 24, Place? place = null)
        {
            RequireText(text, name, nameof(Button));
            var node = Node(name, UiNodeKind.Button, place ?? Place.Flow, UiSize.Fixed(size));
            node.Text = text;
            node.FontSize = fontSize;
            AttachDefaultHoverBox(node);
            return node;
        }

        // Skips a button whose box is not known yet (UiSize.Fill/FillWidth/
        // FillHeight -- the fight's hit areas, resized from the real sprite
        // at runtime) rather than padding a rim around a zero-size rect.
        // Nothing currently calls .Hovers() on one of these either, which is
        // the same fact stated the other way round: a runtime-sized button
        // has never had a declaration-time rim to build.
        private static void AttachDefaultHoverBox(UiNode node)
        {
            if (node.Size.ModeX == UiSizeMode.Fixed && node.Size.ModeY == UiSizeMode.Fixed)
            {
                ApplyHoverBox(node);
            }
        }

        // The other half of NoHoverBox()/Themed()/ThemedPlate(): removes the
        // rim AttachDefaultHoverBox may already have added, so either of
        // those can run before or after it without leaving a stray child (or
        // throwing trying to add a second "HoverRim"). A no-op when nothing
        // is there, which covers a Fill-sized button and a call-site order
        // where the rim was already stripped.
        internal static void RemoveDefaultHoverBox(UiNode node)
        {
            node.Children.RemoveAll(c => c.Name == "HoverRim");
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

        // ---- container / flag-banner kit ---------------------------------------
        //
        // A themed, non-interactive frame of exact-ratio art (Art/UI/Buttons/
        // Processed/container_<theme>_<ratio>.png) that CONTENT is declared
        // INSIDE, via ContainerContent -- the container itself is a Sprite
        // node like any other, and a plain UiNode.Children.Add would work just
        // as well, but ContainerContent also applies the measured inset so a
        // caller cannot forget it and let a label sit under the painted
        // border.
        //
        // Reuses ButtonTheme rather than a parallel ContainerTheme: the six
        // colours are the same semantic vocabulary a button already speaks
        // (Gold = the recommended path, Crimson = loss/danger, and so on),
        // and a second enum naming the same six things would only be able to
        // disagree with the first one.
        //
        // REFUSES a size whose aspect does not match the delivered art within
        // 5% -- see ContainerArt's own header for where the measured aspect
        // comes from. A container is drawn Simple + preserveAspect, so a
        // mismatched size would not stretch visibly WRONG so much as pad or
        // crop the art inside its own box, which is a quieter, harder-to-spot
        // version of the same mistake; refusing it at declaration time is
        // cheaper than finding it in a screenshot.
        // THE SAME KEY Container() BAKES ITS FRAME FROM, exposed publicly.
        // ContainerArt is internal to this assembly and ScreenRegistry (Editor)
        // carries no InternalsVisibleTo grant into it, so a caller that needs
        // to LoadSpriteByKey a themed container at runtime -- the party plate's
        // per-theme sprite array, Phase C1 -- would otherwise have to restate
        // the "UI/Buttons/Processed/container_<theme>_<ratio>.png" format
        // itself. Reusing the same builder is what keeps the two unable to
        // disagree.
        public static string ContainerKey(ButtonTheme theme, ContainerRatio ratio) =>
            ContainerArt.Key(ContainerKind.Container, theme, ratio);

        public static UiNode Container(string name, ButtonTheme theme, ContainerRatio ratio, Place place, UiVec size)
        {
            ValidateContainerAspect(name, "Container", ContainerKind.Container, ratio, size);
            return BuildFrameHolder(name, ContainerArt.Key(ContainerKind.Container, theme, ratio), place, size);
        }

        // Same rules as Container, always Decor, never a Button -- a flag
        // banner names a place or an identity, it does not do anything, so it
        // gets no ThemedButtonState and no click target at all.
        public static UiNode FlagBanner(string name, ButtonTheme theme, ContainerRatio ratio, Place place, UiVec size)
        {
            ValidateContainerAspect(name, "FlagBanner", ContainerKind.FlagBanner, ratio, size);
            return BuildFrameHolder(name, ContainerArt.Key(ContainerKind.FlagBanner, theme, ratio), place, size);
        }

        // Builds the holder Container/FlagBanner returns: a plain, un-Decor
        // Panel (draws nothing of its own -- EmitsNoGraphic) sized and placed
        // exactly as the caller asked, wrapping the themed frame art as ITS
        // OWN Decor Sprite child rather than being that Sprite itself.
        //
        // The frame used to BE the returned node, marked AsDecor directly --
        // which worked for the art but was wrong the moment ContainerContent
        // added real content as ITS child. UiAudit.Walk's Decor exemption
        // (A1) is inherited by an entire subtree unconditionally: `bool
        // decorHere = isDecor || node.Source.Decor`, carried into every
        // descendant with no way for a lower node to opt back in. So content
        // living under a Decor frame was never checked for overlap against
        // its own siblings -- two labels stacked on each other inside one
        // ContainerContent audited clean, because as far as A1 was concerned
        // everything below the frame was ambient decoration.
        //
        // Keeping the frame Decor but demoting it to a CHILD of a non-Decor
        // wrapper fixes that without touching A1's semantics at all: the
        // wrapper's own decorHere is false, so content added beside the frame
        // (see ContainerContent) is audited exactly like any panel's
        // children, while CheckSiblingOverlap's existing "either side is
        // Decor" skip still exempts the frame/content pair itself -- the
        // frame is meant to sit under the content, not be checked against it.
        private static UiNode BuildFrameHolder(string name, string spriteKey, Place place, UiVec size)
        {
            var frame = Sprite(name + "Art", spriteKey, Place.Stretch(), UiSize.Fill);
            frame.PreserveAspect = true;
            frame.AsDecor();
            return Panel(name, place, UiSize.Fixed(size), frame);
        }

        // The padded surface a Container/FlagBanner's real content goes on --
        // the painted border on every side, and (for a banner) the V-notch at
        // the bottom, must never hold a label or a button. Adds a plain,
        // graphic-less Panel as a SIBLING of the frame art inside `holder`
        // (a Container or FlagBanner node), stretched to the measured inset,
        // and returns it for the caller to add children to.
        //
        // Draws nothing itself (EmitsNoGraphic), so it never collides with
        // the art it sits on; UiAudit's containment check (A2) still measures
        // every child declared inside it against ITS rect, which is what
        // keeps a label from escaping into the border -- the whole reason
        // this exists instead of a bare Place.Stretch() at each call site.
        // Sibling of the frame, not a descendant of it, is what keeps this
        // content's own children subject to the sibling-overlap check (A1)
        // instead of inheriting the frame's Decor exemption -- see
        // BuildFrameHolder for why that distinction has to live there.
        public static UiNode ContainerContent(UiNode holder, ContainerRatio ratio, string name, params UiNode[] children)
        {
            if (holder == null)
            {
                throw new ArgumentNullException(nameof(holder), "ContainerContent needs the Container/FlagBanner node its content sits on.");
            }

            // Kind (not ratio) is the only thing this needs to infer, and it
            // reads it off the frame child's own SpriteKey -- the wrapper
            // `holder` itself carries no sprite -- rather than taking a
            // second parameter that could disagree with which factory built
            // it -- a banner's bottom inset differs from a container's for a
            // reason (the V) that is a fact about the ASSET, not about which
            // of the two nominal ratios was chosen.
            var frame = holder.Children.FirstOrDefault();
            var kind = frame?.SpriteKey != null && frame.SpriteKey.Contains("banner_flag_")
                ? ContainerKind.FlagBanner
                : ContainerKind.Container;

            var inset = ContainerArt.Inset(kind, ratio);
            float left = holder.Size.X * inset.Left;
            float right = holder.Size.X * inset.Right;
            float top = holder.Size.Y * inset.Top;
            float bottom = holder.Size.Y * inset.Bottom;

            var content = Panel(name, Place.Stretch(left, right, bottom, top), UiSize.Fill, children);
            holder.Children.Add(content);
            return content;
        }

        // A system-menu screen's whole pane in one call: a plain, graphic-less
        // ground the size of the pane, with a content Panel inset by
        // SystemMenuLayout's pane content half-extents so nothing inside it
        // moves.
        //
        // WAS a themed 2:1 Container until the owner called every kit frame
        // inside the system menu ugly and asked for the bare violet pane the
        // design pass actually
        // specified (2026-09-07). Exits, Options, Party, Run statistics and
        // Reward track each built the container by hand (a Container call, a
        // ContainerContent call, then `screen.Root = ground`) with the same
        // six-line "this pane sat on the shared SystemMenuFill with no ground
        // of its own" comment repeated at every site; this is the one place
        // that construction happens now, so a caller only says what size its
        // pane is. The content inset is the SAME 744/357.78 the container's
        // measured border used to leave (SystemMenuLayout.PaneContentHalf
        // Width/HalfHeight's own comment), so no label, card or button in any
        // hosted pane moved when the frame came off. The ground node is still
        // handed back so a caller that needs to mark it Inactive() (Reward
        // track, which opens over the dossier) or read it back into
        // `screen.Root` can do so itself.
        public static UiNode SystemMenuPane(string groundName, string contentName, UiVec size, params UiNode[] children)
        {
            var ground = Panel(groundName, Place.At(0f, 0f), UiSize.Fixed(size.X, size.Y));

            float insetX = size.X * 0.5f - SystemMenuLayout.PaneContentHalfWidth;
            float insetY = size.Y * 0.5f - SystemMenuLayout.PaneContentHalfHeight;
            var content = Panel(contentName, Place.Stretch(insetX, insetX, insetY, insetY), UiSize.Fill, children);
            ground.Children.Add(content);
            return ground;
        }

        private static void ValidateContainerAspect(string name, string factory, ContainerKind kind, ContainerRatio ratio, UiVec size)
        {
            if (size.X <= 0f || size.Y <= 0f)
            {
                throw new ArgumentException($"Ui.{factory}(\"{name}\") needs a positive size; got {size}.");
            }

            float expected = ContainerArt.Aspect(kind, ratio);
            float actual = size.X / size.Y;
            float relativeError = Math.Abs(actual - expected) / expected;

            if (relativeError > ContainerArt.AspectTolerance)
            {
                float expectedWidth = size.Y * expected;
                float expectedHeight = size.X / expected;
                throw new ArgumentException(
                    $"Ui.{factory}(\"{name}\", {ratio}) was given a {size.X:0.#}x{size.Y:0.#} box, whose aspect " +
                    $"({actual:0.###}) misses the delivered art's measured aspect ({expected:0.###}) by " +
                    $"{relativeError:P1} - more than the {ContainerArt.AspectTolerance:P0} this kit allows. " +
                    $"Stretching the art into the wrong ratio is exactly what this check exists to catch. Fix by " +
                    $"using a {size.X:0.#}x{expectedHeight:0.#} or {expectedWidth:0.#}x{size.Y:0.#} box instead - " +
                    $"or Ui.ContainerSizeForHeight/ForWidth, which always produces one.");
            }
        }

        // Produces a size that already passes the aspect check above, so a
        // caller who has a height (or width) to fill never has to compute the
        // matching other axis by hand. Kind-parameterised; ContainerSizeFor*/
        // FlagBannerSizeFor* below are thin forwards kept for the ~50 call
        // sites (mostly tests) that name a kind explicitly.
        private static UiVec SizeForHeight(ContainerKind kind, ContainerRatio ratio, float height) =>
            new UiVec(height * ContainerArt.Aspect(kind, ratio), height);

        private static UiVec SizeForWidth(ContainerKind kind, ContainerRatio ratio, float width) =>
            new UiVec(width, width / ContainerArt.Aspect(kind, ratio));

        public static UiVec ContainerSizeForHeight(ContainerRatio ratio, float height) =>
            SizeForHeight(ContainerKind.Container, ratio, height);

        public static UiVec ContainerSizeForWidth(ContainerRatio ratio, float width) =>
            SizeForWidth(ContainerKind.Container, ratio, width);

        public static UiVec FlagBannerSizeForHeight(ContainerRatio ratio, float height) =>
            SizeForHeight(ContainerKind.FlagBanner, ratio, height);

        public static UiVec FlagBannerSizeForWidth(ContainerRatio ratio, float width) =>
            SizeForWidth(ContainerKind.FlagBanner, ratio, width);

        // The measured content inset, exposed for a test (or a caller
        // building content by hand rather than through ContainerContent) to
        // check a rect against.
        private static ContentInsetFrac ContentInset(ContainerKind kind, ContainerRatio ratio) =>
            ContainerArt.Inset(kind, ratio);

        public static ContentInsetFrac ContainerContentInset(ContainerRatio ratio) =>
            ContentInset(ContainerKind.Container, ratio);

        public static ContentInsetFrac FlagBannerContentInset(ContainerRatio ratio) =>
            ContentInset(ContainerKind.FlagBanner, ratio);

        // The measured VISIBLE-EDGE pad (the transparent halo outside the
        // painted border, distinct from the content inset above) -- see
        // ContainerArt.VisiblePad's own comment for where the numbers come
        // from. Exposed the same way ContainerContentInset/
        // FlagBannerContentInset expose Inset: ContainerArt itself is
        // internal to this assembly.
        public static ContentInsetFrac ContainerVisiblePad(ContainerRatio ratio) =>
            ContainerArt.VisiblePad(ContainerKind.Container, ratio);

        public static ContentInsetFrac FlagBannerVisiblePad(ContainerRatio ratio) =>
            ContainerArt.VisiblePad(ContainerKind.FlagBanner, ratio);

        // Same idea, for a themed button's plate art -- see
        // ButtonPlateArt.VisiblePad's own comment.
        public static ContentInsetFrac PlateVisiblePad(ButtonPlateShape shape) =>
            ButtonPlateArt.VisiblePad(shape);

        // The plate's measured width/height. Containers already expose theirs
        // through ContainerSizeForHeight; plates have no size helper because
        // no caller sizes a button from its art, so the aspect is forwarded
        // directly -- UiKitAspectPinTests needs it to check the literal
        // against the PNG on disk, and ButtonPlateArt is internal to this
        // assembly.
        public static float PlateAspect(ButtonPlateShape shape) => ButtonPlateArt.Aspect(shape);

        // Same reason PlateAspect is forwarded here rather than read directly
        // off ButtonPlateArt: it is internal to this assembly, and
        // ThemedButtonAspectLintTests needs to compute a rect's error against
        // the plate it wears without an InternalsVisibleTo grant.
        // ButtonPlateArt.AspectError's own comment says why this is a plain
        // relative error rather than ShapeFor's ratio/log distance.
        public static float PlateAspectError(float width, float height) =>
            ButtonPlateArt.AspectError(width, height);

        // The same-width box that already passes PlateAspectError at zero --
        // see ButtonPlateArt.NominalSizeFor for why width stays and height
        // moves.
        public static UiVec PlateNominalSizeFor(float width, float height) =>
            ButtonPlateArt.NominalSizeFor(width, height);

        // Where a rect's CENTRE has to sit so that its VISIBLE bottom edge
        // (rect bottom + height * the art's own bottom VisiblePad fraction)
        // lands exactly on `visibleBottomLine`. One line, but it is the one
        // line FightSubmenuLayout's frame and FightScreen's party plate both
        // solve -- named so neither restates the algebra
        // (visibleBottomLine + height * (0.5 - pad) falls out of "rect
        // bottom = centre - height*0.5" and "visible bottom = rect bottom +
        // height*pad" but is not obviously either of those by itself).
        public static float CentreYForVisibleBottom(float visibleBottomLine, float height, float bottomPadFraction) =>
            visibleBottomLine + height * (0.5f - bottomPadFraction);

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

        // THE FOUR EDGES BACK OUT AGAIN, in the order Rim yielded them
        // (Top, Bottom, Left, Right).
        //
        // A caller that recolours a rim at RUNTIME needs the four Images, not
        // the box -- the fight HUD's cards wear their occupant's identity
        // colour, and uGUI has no inherited tint, so that is four writes per
        // card. The alternative was for every such caller to rebuild
        // $"{name}Rim{edge}" itself, which puts Rim's naming convention in as
        // many places as there are callers and makes a typo silent: a rim
        // edge is AsDecor, so UiAudit never looks at it, and a card that
        // simply never got recoloured looks like a card whose character has
        // no colour.
        //
        // OutlineBox's own signature is deliberately untouched by this -- its
        // other callers (Options, Run statistics, Exits, the system menu, the
        // shop's chips) paint their rim once at build time and have nothing
        // to hand back.
        //
        // Throws rather than returning what it found: fewer than four edges
        // means the box was not built by OutlineBox/Rim at all, and a caller
        // silently tinting two of them is the exact failure this exists to
        // stop.
        public static IReadOnlyList<UiNode> RimEdgesOf(UiNode box)
        {
            if (box == null) throw new ArgumentNullException(nameof(box));

            var found = new List<UiNode>();
            foreach (string edge in RimEdgeOrder)
            {
                var child = box.Children.FirstOrDefault(c => c.Name == box.Name + "Rim" + edge);
                if (child != null) found.Add(child);
            }

            if (found.Count != 4)
            {
                throw new InvalidOperationException(
                    $"'{box.Name}' has {found.Count} of the 4 rim edges Ui.Rim names -- it was not built " +
                    "by Ui.OutlineBox/Ui.Rim, so there is no rim here to recolour.");
            }

            return found;
        }

        private static readonly string[] RimEdgeOrder = { "Top", "Bottom", "Left", "Right" };

        // A FILL AND THE RIM THAT TRACES IT, as one box.
        //
        // Rim above gives the four edges; every caller that wanted a *card*
        // rather than an outline then wrote the same three lines around it --
        // a Solid the same size, the Rim, and a Panel to hold both (Options,
        // Run statistics, Exits, the system menu's own frame). That triple is
        // the shape, so it gets a name.
        //
        // `fillHex` may be null for a pure outline with nothing behind it --
        // the shop's price chips and its outlined buttons sit on the panel
        // they are already inside, and a second fill there would darken it
        // twice.
        public static UiNode OutlineBox(string name, Place place, UiVec size, string fillHex,
            string rimHex, IEnumerable<UiNode> children = null)
        {
            var parts = new List<UiNode>();

            if (!string.IsNullOrEmpty(fillHex))
            {
                parts.Add(Solid(name + "Fill", fillHex, size, Place.At(0f, 0f)).AsDecor());
            }

            parts.AddRange(Rim(name, size, rimHex));
            if (children != null) parts.AddRange(children);

            return Panel(name, place, UiSize.Fixed(size), parts);
        }

        // ---- meters ------------------------------------------------------------
        //
        // A TRACK, THE FILL THAT READS IT, AND THE TWO STRIPS THAT MAKE IT A
        // METER RATHER THAN A COLOURED RECTANGLE.
        //
        // The fight HUD wrote this by hand three times -- the party plate's HP
        // row, its MP row, and the roster mini-plate's -- and every copy was
        // the same two lines: a Panel tinted Track with one full-bleed Solid
        // inside it. At 6-13px tall that reads as a red stripe, which is
        // exactly the owner's note on the 2026-09-09 capture ("it should feel
        // like a proper HP bar, not a red bar"). What makes it read as a meter
        // is not height alone: it is a rim in the fill's own deep tone, a
        // highlight along the top of the fill and a darker band under it.
        //
        // THE STRIPS ARE CHILDREN OF THE FILL, at FRACTIONAL anchors, and both
        // halves of that matter. FightController.SetFill drains a bar by moving
        // the fill's own anchorMax.x, so anything anchored INSIDE the fill
        // narrows with it for free and needs no second runtime writer; and
        // fractional anchors mean the strips stay the same PROPORTION of the
        // bar however tall the caller makes it, so one recipe serves a 22px
        // party bar and a 14px roster bar without a second set of numbers.
        //
        // Anything that must NOT drain with the fill -- the roster's
        // "HP 34/40" caption, drawn on the bar itself -- is passed as `over`
        // and lands on the TRACK, layered against the fill rather than
        // overlapping it by accident.
        public readonly struct MeterNodes
        {
            public readonly UiNode Track;

            // The node FightController.SetFill writes. Named by the caller,
            // never derived here: PartyHpFill/Roster0HpFill are wiring
            // contract, and a helper that minted its own name would rename
            // three serialized fields as a side effect of a refactor.
            public readonly UiNode Fill;

            // THE BAND UNDER THE FILL, handed back for the same reason
            // Ui.RimEdgesOf exists: a caller that recolours a meter at
            // RUNTIME (the fight HUD's second meter wears its holder's own
            // resource, whose colours are content) needs the Image, and the
            // alternative is every such caller rebuilding $"{fillName}Shade"
            // itself -- which puts this recipe's naming convention in as
            // many places as there are callers and makes a typo silent,
            // because the shade is AsDecor and UiAudit never looks at it.
            //
            // The SHEEN is deliberately not exposed: it is a fixed warm
            // white by design (see MeterSheenHex), the one part of the
            // recipe that is the same on every bar whatever colour it is.
            public readonly UiNode Shade;

            internal MeterNodes(UiNode track, UiNode fill, UiNode shade)
            {
                Track = track;
                Fill = fill;
                Shade = shade;
            }
        }

        // A WARM white at 0.18, and the top 40% / bottom 25% split beneath it.
        // One statement of the recipe, so a red bar and a blue bar are the
        // same widget in two colours rather than two bars that happen to be
        // lit similarly. Only the two TONES are per-caller (a meter's shade
        // has to be its own deep colour, or the band under a blue bar reads
        // brown).
        //
        // WARM, NOT #FFFFFF, for two reasons that happen to agree. A pure
        // white highlight desaturates the red bar it sits on; #FFF3E0 is the
        // same lamp-lit white GoldLight already uses and leaves the bar its
        // own hue. And FightPlayableTests.NothingOnScreenIsAWhiteQuad sweeps
        // for sprite-less Images left at construction white -- which is a
        // guard worth keeping strict, so the deliberate highlight moves off
        // pure white rather than the sweep gaining an alpha exemption every
        // real white quad could then hide behind.
        private const string MeterSheenHex = "#FFF3E02E";
        private const float MeterSheenFromY = 0.6f;
        private const float MeterShadeToY = 0.25f;

        public static MeterNodes Meter(string trackName, string fillName, Place place, UiVec size,
            string trackHex, string rimHex, string fillHex, string shadeHex, params UiNode[] over)
        {
            // AsDecor, both: a highlight is drawn, never pressed -- same rule
            // Rim's own edges follow -- and it keeps them out of A1's sibling
            // check against each other, which they would otherwise need a
            // Layered() for despite never touching.
            var sheen = Solid(fillName + "Sheen", MeterSheenHex,
                    Place.Frac(new UiVec(0f, MeterSheenFromY), UiVec.One), UiSize.Fill)
                .AsDecor();
            var shade = Solid(fillName + "Shade", shadeHex,
                    Place.Frac(UiVec.Zero, new UiVec(1f, MeterShadeToY)), UiSize.Fill)
                .AsDecor();

            // ONE PIXEL IN FROM THE TRACK on every side, so the rim below
            // traces the fill rather than being covered by it at full health.
            var fill = Solid(fillName, fillHex, Place.Stretch(1f, 1f, 1f, 1f), UiSize.Fill);
            fill.Children.Add(sheen);
            fill.Children.Add(shade);

            var parts = new List<UiNode> { fill };
            parts.AddRange(Rim(trackName, size, rimHex));

            var captions = over == null ? new List<UiNode>() : over.Where(n => n != null).ToList();
            if (captions.Count > 0)
            {
                parts.AddRange(captions);

                // The caption sits ON the fill by design -- that is what "the
                // text is drawn on the bar" means -- so it is a layer, not an
                // overlap to be exempted one pair at a time.
                Layered(new List<UiNode> { fill }.Concat(captions));
            }

            var track = Panel(trackName, place, UiSize.Fixed(size), parts).Coloured(trackHex);
            return new MeterNodes(track, fill, shade);
        }

        // OutlineButton (the hairline-rim, no-plate button) retired
        // 2026-09-07: the shop was its only caller and every one of those
        // sites now wears a kit plate (owner's HQ-kit instruction). The
        // `<name>Caption` convention it established outlives it -- a
        // ThemedPlate() button whose caption is more than one centred string
        // (the shop's small sell plates, Reckoning's tabs, FightScreen's verb
        // rows) still declares its own `<name>Caption`/`<name>Text` child
        // rather than relying on the label Themed() generates, which is what
        // CaptionOf below still has to find both shapes of.
        //
        // A button's caption node, whichever factory built it: a declared
        // `<name>Caption` child, or the `<name>Label` Themed() generates.
        // The one way to reach a caption that changes at runtime.
        public static UiNode CaptionOf(UiNode button) =>
            button?.Children.Find(c => c.Name == button.Name + "Caption")
            ?? button?.Children.Find(c => c.Name == button.Name + "Label");

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

        // The three nodes a paging control is made of, so a caller can still
        // decorate each one -- which every caller needs to do, see Pager.
        public readonly struct PagerNodes
        {
            public readonly UiNode Prev;
            public readonly UiNode Next;

            // Null at the three sites that step through characters and
            // constellations rather than pages, and so have nothing to count.
            public readonly UiNode Label;

            internal PagerNodes(UiNode prev, UiNode next, UiNode label)
            {
                Prev = prev;
                Next = next;
                Label = label;
            }
        }

        // PREV, NEXT, and optionally the counter between them.
        //
        // Seven screens step through something, and what they share is smaller
        // than it looks: both arrows are chromeless (there is no plate to
        // reskin on a 40px glyph, and four sites carried a comment saying so),
        // and both say the same two characters. THAT is what moves here. The
        // strings in particular: five of the seven reached for
        // UiStrings.TalentPrev/TalentNext, so the glossary, the debug menu, the
        // shop and the relic draft were all labelling their arrows with a
        // talent-screen string. They are UiStrings.PagerPrev/PagerNext now and
        // TalentPrev/TalentNext are gone -- nothing talent-specific was left.
        //
        // WHAT DELIBERATELY DOES NOT MOVE is the decoration, and this is the
        // CODE_STANDARDS SS2 line about a helper you are entitled to bypass.
        // The seven disagree: the relic draft and the shop start all three
        // nodes inactive, three sites mark the label AsDecor and the shop's is
        // not decor at all, two style the label as TacticalData. Baking any of
        // those in would silently change three trees; offering a flag for each
        // would make the signature longer than the code it replaces. The nodes
        // come back mutable and the caller says the rest in one line.
        //
        // Places are per-node rather than a centre and an offset because the
        // sites are not symmetric: the relic draft's arrows sit at y 20 and its
        // counter at y -280, and the talent tree's flank the sky at its edges.
        public static PagerNodes Pager(
            string prevName, Place prevPlace,
            string nextName, Place nextPlace,
            UiVec arrowSize, int arrowFontSize,
            string labelName = null, UiString labelText = default, Place labelPlace = default,
            UiVec labelSize = default, int labelFontSize = 0, string labelHex = null)
        {
            var prev = Button(prevName, UiStrings.PagerPrev, arrowSize, arrowFontSize, prevPlace).NoChrome();
            var next = Button(nextName, UiStrings.PagerNext, arrowSize, arrowFontSize, nextPlace).NoChrome();
            var label = labelName == null
                ? null
                : Label(labelName, labelText, labelSize, labelFontSize, labelHex, labelPlace);
            return new PagerNodes(prev, next, label);
        }

        // A HOVER TOOLTIP: a ground that takes no clicks, and the lines on it.
        //
        // Four screens built this by hand and each one re-derived the same two
        // calls. Inactive() because a tooltip is hidden until something is
        // hovered, and AsDecor() because it floats OVER the thing it explains
        // -- a tooltip that swallowed the click meant for the enemy, the offer
        // card or the pack cell underneath it would be worse than no tooltip at
        // all. Both were remembered correctly at all four sites, which is the
        // argument for the helper rather than against it: they were remembered
        // because someone copied the site next door, and the fifth site is the
        // one that copies only half.
        //
        // spriteKey OR groundHex, whichever the screen paints with: the fight's
        // two wear the violet panel art, the reckoning's and the dossier's a
        // flat dark sheet. Passing null for one of them is the normal case, not
        // a degraded one.
        //
        // The lines are built by the caller, because what a tooltip SAYS is the
        // one thing the four do not share -- one runtime label on the fight
        // screen, a title and a body on the dossier.
        public static UiNode Tooltip(string name, string spriteKey, string groundHex,
                                     Place place, UiSize size, params UiNode[] lines)
        {
            var panel = Sprite(name, spriteKey, place, size);
            if (groundHex != null) panel.Coloured(groundHex);
            panel.Inactive().AsDecor();
            if (lines != null)
            {
                foreach (var line in lines)
                {
                    if (line != null) panel.Children.Add(line);
                }
            }
            return panel;
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

        // ---- hover/focus rim -----------------------------------------------------
        //
        // Called by Button()'s AttachDefaultHoverBox for every fixed-size,
        // unthemed button -- not by UiNode.Hovers(), which is a no-op since
        // 2026-09-23 (second pass). Owner's call, 2026-09-23: no more
        // hover-scale pop anywhere in the game -- a solid rim instead, shown
        // on pointer hover OR gamepad/keyboard focus (Core/HoverBox.cs),
        // never a transform change, and on EVERY hoverable/focusable
        // control, not only the ones that remembered to ask. Builds the rim
        // as a REAL UiNode child for the same reason ApplyTheme builds
        // Visuals/Label as real children: UiAudit and UiKitAuditTests only
        // walk the UiNode tree, so a rim UiEmitter invented for itself would
        // be neither.
        //
        // A hollow rectangle built the SAME way the kit's other four-edge
        // rims are (Rim, below: each edge is a Solid RectTransform,
        // AsDecor), but NOT through Rim() itself -- Rim's 1px hairline is
        // right for a border that sits flush on something (Options, Run
        // statistics, Exits, the system menu, the shop's chips); a box that
        // has to read as a solid outline around a control from across the
        // room does not. Owner's call, 2026-09-23 (second pass): the first
        // 1px/pad-6 rim did not read as solid enough. 2px, padded 4 --
        // closer to the control so the box reads as ABOUT the button, not a
        // second frame floating near it.
        private const float HoverRimThickness = 2f;
        private const float HoverRimPad = 4f;

        // Kit silver, full alpha -- neutral against every ButtonTheme's own
        // plate colour (Gold/Crimson/Violet/Blue/Green), so one rim colour
        // reads correctly no matter which theme, or no theme at all, sits
        // under it. Unlike ThemeGlowHex's baked-at-alpha-0 pairs, this rim
        // does not fade -- HoverBox shows it by activating the whole
        // GameObject, so it is baked at full alpha here.
        private const string HoverRimHex = "#D6D6E0FF";

        // Rim()'s own four-edge shape (RimEdge below is the same private
        // helper Rim() uses), but at HoverRimThickness rather than Rim's
        // fixed 1f -- kept local to the hover box rather than a new
        // parameter threaded onto Rim() itself, which every one of its
        // other five 1px callers would have to keep passing 1f through for
        // no reason of their own.
        // ANCHORED, NOT SIZED. Each edge is pinned to its own side of the rim
        // (Frac anchors) and the rim itself stretches over the button with
        // its pad as a negative inset, so the box follows whatever size the
        // button is given AT RUNTIME. The first cut sized every edge from the
        // button's build-time size, which is the WIDEST a map room can be
        // (MapScreen declares every tile at the boss size, and
        // MapController.PaintNode shrinks the rest) -- so an ordinary room's
        // hover box drew a boss-sized frame around a smaller tree.
        private static IEnumerable<UiNode> HoverRimEdges()
        {
            float t = HoverRimThickness;

            yield return HoverRimEdge("HoverRimTop", new UiVec(0f, 1f), new UiVec(1f, 1f), 0f, 0f, -t, 0f);
            yield return HoverRimEdge("HoverRimBottom", new UiVec(0f, 0f), new UiVec(1f, 0f), 0f, 0f, 0f, -t);
            yield return HoverRimEdge("HoverRimLeft", new UiVec(0f, 0f), new UiVec(0f, 1f), 0f, -t, 0f, 0f);
            yield return HoverRimEdge("HoverRimRight", new UiVec(1f, 0f), new UiVec(1f, 1f), -t, 0f, 0f, 0f);
        }

        private static UiNode HoverRimEdge(string name, UiVec anchorMin, UiVec anchorMax,
                                           float left, float right, float bottom, float top) =>
            Solid(name, HoverRimHex, Place.Frac(anchorMin, anchorMax, left, right, bottom, top), UiSize.Fill)
                .AsDecor();

        internal static void ApplyHoverBox(UiNode node)
        {
            if (node.Kind != UiNodeKind.Button)
            {
                throw new ArgumentException(
                    $"The hover/focus rim only applies to a Button node; '{node.Name}' is a {node.Kind}.");
            }

            float pad = HoverRimPad;

            // Rim's own edges are already AsDecor -- what that exempts them
            // from is each other (they meet, and would otherwise overlap, at
            // the frame's four corners) AND every real sibling the button
            // already carries or ever will, since CheckSiblingOverlap skips
            // any pair where either side is Decor. Without it, every unthemed
            // button that already carries its own children (badges, labels,
            // solids) would need its own new AllowOverlap the moment this
            // shipped, which is exactly the "edit every caller" this seam
            // exists to avoid.
            var rim = Panel("HoverRim", Place.Stretch(-pad, -pad, -pad, -pad), UiSize.Fill, HoverRimEdges())
                .AsDecor()
                .AllowOverflow(
                    "the hover/focus rim pads outward past the button it wraps (HoverRimPad) so it reads as a " +
                    "box AROUND the control, not as a second, smaller plate sitting on it")
                .Inactive();

            node.Children.Add(rim);
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

            // A themed button wears ThemedButtonState's own Glow/Plate focus
            // visual instead -- strip the default rim Button() already
            // attached, or a themed plate would show both on the same hover.
            RemoveDefaultHoverBox(node);

            var buttonSize = new UiVec(node.Size.X, node.Size.Y);
            var shape = node.PlateShapeOverride ?? PlateShapeFor(buttonSize.X, buttonSize.Y);
            var visuals = BuildVisuals(theme, shape, buttonSize);

            var label = Label(node.Name + "Label", node.Text, buttonSize,
                node.FontSize > 0 ? node.FontSize : 29, "#FFFFFFFF", Place.Stretch());
            label.Tracking = ButtonTracking(node.Text);

            // Every themed button's caption follows ButtonLabel -- the one
            // migration step 1 does automatically, without a screen having to
            // say .Styled(ButtonLabel) at every Themed() call site. See
            // UiEmitter.ApplyTypography for what this actually changes.
            label.Role = TypographyRole.ButtonLabel;

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

        // Called by UiNode.ThemedPlate() -- the CAPTION-PRESERVING mode.
        // Builds the SAME Visuals(Glow, Plate) ApplyTheme does, but declares
        // no <name>Label: the caller already has its own caption children
        // and adds them itself, then calls LayerCaptionWithVisuals() to state
        // the same "one widget" relationship Themed()'s own Layered(visuals,
        // label) states for the label-generating path.
        internal static void ApplyThemePlateOnly(UiNode node, ButtonTheme theme)
        {
            if (node.Kind != UiNodeKind.Button)
            {
                throw new ArgumentException(
                    $"ThemedPlate({theme}) only applies to a Button node; '{node.Name}' is a {node.Kind}.");
            }

            if (node.Chromeless)
            {
                throw new ArgumentException(
                    $"'{node.Name}' is both Chromeless and ThemedPlate({theme}) - no plate at all and a themed " +
                    "plate are contradictory. Drop whichever one this button does not actually want.");
            }

            node.Theme = theme;
            node.CaptionPreserving = true;

            // Same reason ApplyTheme strips it: ThemedButtonState owns this
            // button's focus visual now.
            RemoveDefaultHoverBox(node);

            var buttonSize = new UiVec(node.Size.X, node.Size.Y);
            var shape = node.PlateShapeOverride ?? PlateShapeFor(buttonSize.X, buttonSize.Y);
            var visuals = BuildVisuals(theme, shape, buttonSize);
            node.Children.Add(visuals);
        }

        // The band ValidateContainerAspect refuses a Container/FlagBanner
        // outside of, forwarded for the same reason PlateAspect is: a caller
        // (ThemedButtonAspectLintTests) outside this assembly needs the exact
        // number the container-side rule enforces so a button-side mirror of
        // it can cite the same tolerance rather than a second copy of it.
        public static float ContainerAspectTolerance => ContainerArt.AspectTolerance;

        // The plate shape a themed button of this rect gets when it does not
        // force one with .Plate(). Ratio-nearest by the same rule
        // ButtonPlateArt.ShapeFor states -- exposed here (rather than only
        // internally) so a screen or a test can ask what a rect resolves to
        // without going through a full Themed() build.
        public static ButtonPlateShape PlateShapeFor(float width, float height) =>
            ButtonPlateArt.ShapeFor(width, height);

        // Visuals(Glow, Plate) -- the half of a themed button's tree BOTH
        // ApplyTheme and ApplyThemePlateOnly need, factored out once they
        // stopped being the same method. The Glow/Plate pair's own Layered()
        // exemption lives here with it, since the two are built together.
        private static UiNode BuildVisuals(ButtonTheme theme, ButtonPlateShape shape, UiVec buttonSize)
        {
            // Larger than the plate on every edge, so a focused button reads
            // as lit rather than as a second, smaller plate underneath the
            // first. Overflows Visuals by design - see the AllowOverflow.
            // 38, not the original 28 - the smaller margin read as a thin
            // outline rather than a glow at hover; a soft radial sized to
            // roughly the plate's own footprint plus this margin is what
            // "medium to medium-strong, centred on the plate" needs at idle
            // alpha before ThemedButtonState's Selected halo scales it up
            // further still (see GlowRect/SelectedGlowScale there). Capped
            // at 38, not pushed further: FightScreenTests.NoAlwaysVisible
            // PanelStandsInFrontOfAFigureSFeet caught 48 putting the HOLD
            // BACK verb's glow 2px into the front stage slot's foot band --
            // the verb column is always visible, so its glow's static
            // footprint is load-bearing stage clearance, not just cosmetic.
            const float GlowMargin = 38f;
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

            var plate = Sprite("Plate", ButtonPlateArt.Key(theme, shape),
                buttonSize, Place.At(0f, 0f));

            // Glow under Plate (declared first, drawn first) and exempt from
            // each other - a glow behind its own plate is one widget, not two
            // things that happen to share a box.
            Layered(glow, plate);

            return Panel("Visuals", Place.Stretch(), UiSize.Fill, glow, plate);
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

        // The glow's own tint per theme - NOT the plate's colour, which is
        // baked into its PNG. Not reused from FightHudPalette: those tokens
        // answer a different question (a fight HUD's own borders and text),
        // and a coincidental hex match there would be exactly that, a
        // coincidence, not a shared role - see UiKitLintTests' own reasoning
        // on ItemStatLines.HeadingHex for the same call made the other way.
        //
        // Public so UiEmitter can read the SAME Gold value for a menu-state
        // Primary ring (ThemedButtonState.SetMenuState) rather than a second
        // hex literal drifting from this one.
        public static string ThemeGlowHex(ButtonTheme theme)
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

using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace;
using PrincesPalace.Domain.UiKit;

// THE only place in this project that calls new GameObject() to build UI.
//
// v1 had 86 such sites, each re-typing the same anchor/pivot/size/position
// block, and a helper that covered 13 of them because it could not express
// stretch or edge pins. Here the whole preamble exists once, and there is
// nothing the DSL cannot say that would justify going around it -- which is
// what makes UiKitLintTests' "only the emitter may create GameObjects" rule
// enforceable rather than aspirational.
//
// A UiKitLintTests rule allows `new GameObject(` in this file by name.
public static class UiEmitter
{
    public static UiEmitResult Emit(UiNode tree, Transform parent)
    {
        // Audit BEFORE emitting. A layout collision that throws during
        // generation is free; one that ships is not.
        var errors = UiAudit.RunAllFrames(tree);
        if (errors.Count > 0)
        {
            throw new System.Exception(FormatAuditFailure(tree.Name, errors));
        }

        var solved = UiSolver.Solve(tree, UiFrames.Reference);
        var result = new UiEmitResult();
        EmitNode(solved, parent, parentRect: null, result, decorInherited: false);
        return result;
    }

    private static string FormatAuditFailure(string screen, IReadOnlyList<UiAuditError> errors)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[SceneBuilder] FAILED: '{screen}' has {errors.Count} layout problem(s):");

        // Grouped so one mistake visible at four frames reads as one mistake.
        foreach (var group in errors.GroupBy(e => e.Message))
        {
            var frames = string.Join(", ", group.Select(e => UiFrames.Describe(e.Frame)).Distinct());
            sb.AppendLine($"  [{group.First().Check}] at {frames}");
            sb.AppendLine($"    {group.Key}");
        }

        return sb.ToString();
    }

    private static void EmitNode(SolvedNode solved, Transform parent, UiRect? parentRect,
                                 UiEmitResult result, bool decorInherited)
    {
        var node = solved.Source;
        bool decor = decorInherited || node.Decor;

        var go = new GameObject(solved.Name, typeof(RectTransform));
        go.transform.SetParent(parent, worldPositionStays: false);
        var rect = go.GetComponent<RectTransform>();

        ApplyPlacement(rect, node, solved, parentRect);

        switch (node.Kind)
        {
            case UiNodeKind.Label:
                EmitLabel(go, node, decor);
                break;
            case UiNodeKind.Button:
                EmitButton(go, node, solved);
                break;
            case UiNodeKind.Sprite:
                EmitImage(go, SceneBuilder.LoadSpriteByKey(node.SpriteKey), node.ColorHex, decor);
                break;
            case UiNodeKind.Solid:
                EmitImage(go, null, node.ColorHex ?? "#ffffff", decor);
                break;
            case UiNodeKind.NestedCanvas:
                EmitNestedCanvas(go, node);
                break;
            case UiNodeKind.Panel:
                // A Panel is normally an invisible grouping rect. Given a colour
                // it becomes a drawn plate - the hub's currency backing, a
                // framed card. Without this the colour was silently ignored and
                // the plate simply did not appear.
                if (!string.IsNullOrEmpty(node.ColorHex)) EmitImage(go, null, node.ColorHex, decor);
                break;
        }

        // BEFORE children, because a RectMask2D has to exist by the time the
        // things it clips are parented under it - Unity rebuilds the clip set
        // from the hierarchy, and a mask that arrives after its content leaves
        // the first frame unclipped.
        if (node.Masks) go.AddComponent<RectMask2D>();

        // The open animation needs its CanvasGroup to exist before it does
        // ([RequireComponent] would add one, but adding it here keeps the two
        // in one visible place). Also before children, for the same reason the
        // mask is: the group has to be in the hierarchy the first frame the
        // subtree draws, or the column's first open flashes at full alpha.
        if (node.OpensOnShow)
        {
            go.AddComponent<CanvasGroup>();
            go.AddComponent<ColumnOpenAnimator>();
        }

        if (node.Rotation != 0f) rect.localRotation = Quaternion.Euler(0f, 0f, node.Rotation);
        if (!node.Scale.Equals(UiVec.One)) rect.localScale = new Vector3(node.Scale.X, node.Scale.Y, 1f);

        result.Record(node, go);

        foreach (var child in solved.Children)
        {
            EmitNode(child, go.transform, solved.Rect, result, decor);
        }

        // A THEMED BUTTON'S WIRING IS DONE HERE, AFTER ITS CHILDREN EXIST.
        //
        // EmitButton ran before this loop and could only add the Button
        // component - its targetGraphic is the Plate Image, and Plate is a
        // declared child (see Ui.ApplyTheme), which the loop above has only
        // just emitted. Wiring it any earlier would be wiring a null.
        if (node.Kind == UiNodeKind.Button && node.Theme.HasValue)
        {
            WireThemedButton(go, node, result);
        }

        // AFTER children, so a subtree that starts inactive is fully built first
        // - Start() only runs on activation, and a half-built inactive tree is
        // the sort of thing that fails much later and somewhere else.
        if (node.StartInactive) go.SetActive(false);
    }

    // The rect preamble. Once.
    private static void ApplyPlacement(RectTransform rect, UiNode node, SolvedNode solved, UiRect? parentRect)
    {
        var place = node.Place;

        switch (place.Kind)
        {
            case PlaceKind.Stretch:
            case PlaceKind.Frac:
                // Emitted with REAL anchor semantics rather than baked
                // coordinates, so the relationship survives at runtime on a
                // canvas size the build never saw.
                rect.anchorMin = new Vector2(place.AnchorMin.X, place.AnchorMin.Y);
                rect.anchorMax = new Vector2(place.AnchorMax.X, place.AnchorMax.Y);
                rect.pivot = new Vector2(place.Pivot.X, place.Pivot.Y);
                rect.offsetMin = new Vector2(place.Left, place.Bottom);
                rect.offsetMax = new Vector2(-place.Right, -place.Top);
                return;

            case PlaceKind.Pin:
                rect.anchorMin = rect.anchorMax = new Vector2(place.AnchorMin.X, place.AnchorMin.Y);
                rect.pivot = new Vector2(place.Pivot.X, place.Pivot.Y);
                rect.sizeDelta = new Vector2(solved.Rect.Width, solved.Rect.Height);
                rect.anchoredPosition = new Vector2(place.Offset.X, place.Offset.Y);
                return;

            default:
            {
                // Flow and At: centre-anchored absolute, v1's convention. The
                // SOLVED rect is the authority here - for a flow child that is
                // the entire point, and for At it is identical to the declared
                // offset anyway.
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(place.Pivot.X, place.Pivot.Y);
                rect.sizeDelta = new Vector2(solved.Rect.Width, solved.Rect.Height);

                var centre = parentRect.HasValue
                    ? new Vector2(solved.Rect.Centre.X - parentRect.Value.Centre.X,
                                  solved.Rect.Centre.Y - parentRect.Value.Centre.Y)
                    : new Vector2(solved.Rect.Centre.X, solved.Rect.Centre.Y);

                // anchoredPosition positions the PIVOT, so shift by however far
                // the pivot sits from the centre.
                rect.anchoredPosition = centre + new Vector2(
                    (place.Pivot.X - 0.5f) * solved.Rect.Width,
                    (place.Pivot.Y - 0.5f) * solved.Rect.Height);
                return;
            }
        }
    }

    // What a label is BAKED with.
    //
    // A templated entry formatted with no arguments bakes its raw template -
    // the hub's currency line read literally "Gold: {0}    Relics: {1}" until
    // this existed. Baking the AuditSample instead means the Edit Mode
    // screenshot shows the realistic WORST CASE, which is both a sensible
    // placeholder and the same string E1 measures the box against. The
    // controller overwrites it with real values on Start.
    private static string BakedText(UiString text) =>
        text.IsTemplated ? text.AuditSample : text.Format();

    private static void EmitLabel(GameObject go, UiNode node, bool decor)
    {
        var text = go.AddComponent<TextMeshProUGUI>();
        text.font = SceneBuilder.UiFont;
        text.text = BakedText(node.Text);
        text.fontSize = node.FontSize;

        // TMP's characterSpacing is already in hundredths of an em, so this is
        // a straight pass-through of the .14em the design writes. Set BEFORE
        // UiTextFitAudit runs, which measures off this very component -- a
        // tracked label is wider, and the audit has to see the width that will
        // actually be drawn.
        text.characterSpacing = node.Tracking;

        text.alignment = Alignment(node.TextAlign);
        text.color = SceneBuilder.ParseHex(node.ColorHex, Color.white);
        text.raycastTarget = false; // a label is never the click target
        if (decor) text.raycastTarget = false;
    }

    // A LABEL'S ALIGNMENT, and only a label's: a button's caption fills its
    // face and is centred in it whatever the tree says, because a button is
    // sized around its word rather than the other way about.
    //
    // Midline rather than Baseline for the two vertically-centred cases. TMP's
    // Center measures the whole line box, so a row of labels at different point
    // sizes lines up on their middles; Baseline would line up their feet, which
    // is right for type set on one line and wrong for a stack of boxes.
    private static TextAlignmentOptions Alignment(UiTextAlign align)
    {
        switch (align)
        {
            case UiTextAlign.Left: return TextAlignmentOptions.Left;
            case UiTextAlign.Right: return TextAlignmentOptions.Right;
            case UiTextAlign.Bottom: return TextAlignmentOptions.Bottom;
            case UiTextAlign.BottomLeft: return TextAlignmentOptions.BottomLeft;
            case UiTextAlign.TopLeft: return TextAlignmentOptions.TopLeft;
            default: return TextAlignmentOptions.Center;
        }
    }

    private static void EmitButton(GameObject go, UiNode node, SolvedNode solved)
    {
        // THEMED BUTTONS TAKE A COMPLETELY SEPARATE PATH.
        //
        // No Image on the root at all: the Plate a declared child carries (see
        // Ui.ApplyTheme) is the visible face AND the raycast target, wired
        // once that child exists (WireThemedButton, called from EmitNode after
        // this node's children are emitted - Plate does not exist yet here).
        // Everything below this branch is the UNTHEMED path, unchanged, so a
        // screen that never calls .Themed() keeps the exact emission it always
        // had.
        if (node.Theme.HasValue)
        {
            go.AddComponent<Button>();
            return;
        }

        var image = go.AddComponent<Image>();

        // A button may wear its own art instead of the shared button frame -
        // the hub buildings are buttons whose face is a painted building. Falls
        // back to the shared sprite when no key is given.
        //
        // Chromeless is the third case and it is NOT expressible as an empty
        // SpriteKey, which selects the fallback above. The Image stays: a
        // Button needs a targetGraphic, and Image raycasts against its RECT
        // rather than its alpha, so a fully transparent one takes the click
        // exactly as a painted plate would. Dropping the Image instead would
        // make the offer cards unclickable.
        var sprite = node.Chromeless
            ? null
            : string.IsNullOrEmpty(node.SpriteKey)
                ? SceneBuilder.ButtonSprite()
                : SceneBuilder.LoadSpriteByKey(node.SpriteKey);

        if (node.Chromeless)
        {
            image.color = new Color(1f, 1f, 1f, 0f);
        }
        else if (sprite != null)
        {
            image.sprite = sprite;
            // Type.Simple, not Sliced. v1 shipped an invisible button on every
            // screen through Sliced and never found a fix; whether it works at
            // runtime is now actually testable via screenshot.ps1 -Runtime, but
            // until someone checks, Simple is what is known to render.
            image.type = Image.Type.Simple;
            image.color = Color.white;
        }
        else
        {
            image.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        }

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;

        // EVERY button gets its motion from here, which is the whole point --
        // v1 attached these per-call-site across several CreateButton variants
        // and this is one function, so "a button with no press animation" is
        // not a state that can be reached by forgetting.
        //
        // The two are mutually exclusive: both drive localScale, and a node
        // carrying both would have them fight every frame. A wide row asks for
        // the gentler one with Hovers(); everything else pops.
        //
        // Note the sound follows the press animator, which is v1's behaviour
        // reproduced deliberately rather than by omission: a hover-scaled wide
        // row (submenu row, enemy plate) clicks SILENTLY there too. Worth
        // knowing it is a choice, since it is easy to read as a bug.
        if (node.HoverScale > 0f)
        {
            go.AddComponent<SubtleHoverScale>().HoverScale = node.HoverScale;
        }
        else
        {
            var press = go.AddComponent<ButtonPressAnimator>();
            if (node.SilentClick) press.clickSound = Sound.None;
        }

        var labelGo = new GameObject(node.Name + "Label", typeof(RectTransform));
        labelGo.transform.SetParent(go.transform, worldPositionStays: false);
        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        var text = labelGo.AddComponent<TextMeshProUGUI>();
        text.font = SceneBuilder.UiFont;
        text.text = BakedText(node.Text);
        text.fontSize = node.FontSize;
        text.characterSpacing = node.Tracking;
        text.alignment = TextAlignmentOptions.Center;

        // Black ONLY for the missing-art fallback, which draws a light grey
        // plate. A chromeless button has no plate at all, so its caption sits
        // on whatever is behind it - here that is the painted violet panel, and
        // black on violet is unreadable.
        text.color = sprite == null && !node.Chromeless ? Color.black : Color.white;
        text.raycastTarget = false;
    }

    // Finishes what EmitButton's Themed() branch started, once Visuals'
    // Glow/Plate and the declared Label child actually exist as GameObjects.
    //
    // Finds them BY NAME rather than by position in node.Children - Ui.
    // ApplyTheme is the only writer of this shape, but matching on the same
    // names it uses is cheaper to read than trusting an index never to drift
    // if that method's own child order ever changes.
    private static void WireThemedButton(GameObject go, UiNode node, UiEmitResult result)
    {
        var visuals = node.Children.FirstOrDefault(c => c.Name == "Visuals");
        var glowNode = visuals?.Children.FirstOrDefault(c => c.Name == "Glow");
        var plateNode = visuals?.Children.FirstOrDefault(c => c.Name == "Plate");
        var labelNode = node.Children.FirstOrDefault(c => c.Name == node.Name + "Label");

        if (visuals == null || glowNode == null || plateNode == null || labelNode == null)
        {
            throw new System.Exception(
                $"[UiEmitter] '{node.Name}' is Themed() but its Visuals/Glow/Plate/Label children are missing - " +
                "did something build this node without going through Ui.ApplyTheme?");
        }

        var plateImage = result.Objects[plateNode].GetComponent<Image>();
        var glowImage = result.Objects[glowNode].GetComponent<Image>();
        var labelText = result.Objects[labelNode].GetComponent<TextMeshProUGUI>();

        var button = go.GetComponent<Button>();
        button.targetGraphic = plateImage;

        var state = go.AddComponent<ThemedButtonState>();
        state.Glow = glowImage;
        state.Plate = plateImage;
        if (node.SilentClick) state.clickSound = Sound.None;

        // Recorded so UiWiringSweep (E3) checks Glow/Plate the same way it
        // checks every other controller's serialized references - it only
        // ever walks result.AttachedControllers, and AddComponent alone would
        // leave this component invisible to that sweep.
        result.AttachedControllers.Add(state);

        // THE LABEL DRESSING THE PLAIN DSL CANNOT SAY.
        //
        // Auto-fit and an ellipsis fallback so a caption that runs long
        // shrinks before it clips off a fixed-size plate, and a margin that
        // keeps the word off the plate's own painted border - none of these
        // are things any other screen's Label needs, which is why they live
        // here rather than as new UiNode fields every OTHER label would carry
        // for nothing.
        labelText.enableAutoSizing = true;
        labelText.fontSizeMin = 20f;
        labelText.fontSizeMax = labelNode.FontSize;
        labelText.alignment = TextAlignmentOptions.Center;
        labelText.overflowMode = TextOverflowModes.Ellipsis;
        labelText.margin = new Vector4(18f, 4f, 18f, 4f);

        // Non-uniform stretch, not preserveAspect. The plate is 482x174
        // (2.79:1); several migrated buttons run far wider than that (5.15:1
        // for Continue at 340x66), and preserveAspect would fit the plate
        // to the SHORTER axis and leave 70-80px of dead transparent space on
        // each side - a small plate floating in an oversized invisible box.
        // A stretched plate is visibly distorted on those buttons instead,
        // and distortion of a texture reads as less broken than a plate that
        // does not reach its own button's edges. Unity's own Image default
        // (preserveAspect = false) already does this, so nothing to set here
        // - stated for the reader, since it is a decision and not an oversight.
    }

    private static void EmitImage(GameObject go, Sprite sprite, string colorHex, bool decor)
    {
        var image = go.AddComponent<Image>();
        if (sprite != null) image.sprite = sprite;
        image.color = SceneBuilder.ParseHex(colorHex, Color.white);

        // Decoration can never take a click. In v1 this was a hand-written sweep
        // over 110 ambient sprites, and one missed entry is an unclickable
        // button with no visible cause.
        if (decor) image.raycastTarget = false;
    }

    private static void EmitNestedCanvas(GameObject go, UiNode node)
    {
        var canvas = go.AddComponent<Canvas>();

        if (node.WorldInterleaved)
        {
            // ScreenSpaceCamera, not Overlay: this canvas needs to genuinely
            // interleave with camera-rendered content (a rig's
            // SpriteRenderers) by SortingLayer+SortingOrder, which an
            // Overlay canvas cannot do -- Overlay composites after ALL
            // camera rendering unconditionally, so it can only ever sit in
            // front of a SpriteRenderer, never behind one, no matter what
            // SortingOrder says. worldCamera/planeDistance are wired in a
            // post-pass (SceneBuilder.WireWorldInterleavedCanvases) once the
            // scene's real camera exists -- this method only builds the
            // node, it doesn't have that reference.
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
        }
        else
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }

        // ALWAYS set, never optional. A nested Canvas ignores sortingOrder
        // without it, which is why v1's BarkCanvas sat at an inert 500 and
        // rendered on top only by call-order accident. Setting it here kills
        // that bug as a category rather than as an instance.
        //
        // sortingLayerName MUST be set AFTER overrideSorting, not before --
        // Unity silently no-ops a Canvas's sortingLayerName/sortingOrder
        // writes until overrideSorting is true, so setting the layer first
        // (the order this used to be in) left sortingLayerID at 0 (Default)
        // no matter what name was assigned, while sortingOrder -- set after
        // overrideSorting in both orderings -- looked like it was working.
        // Caught by checking the actual emitted scene, not by reasoning
        // about the API.
        canvas.overrideSorting = true;
        canvas.sortingOrder = node.SortingOrder;
        if (node.WorldInterleaved && !string.IsNullOrEmpty(node.SortingLayerName))
        {
            canvas.sortingLayerName = node.SortingLayerName;
        }

        go.AddComponent<GraphicRaycaster>();
    }
}

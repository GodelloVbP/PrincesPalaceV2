using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // THE ONE PAD-FOCUS INDICATOR, project-wide: a small arrow that sits
    // beside whatever the pad has focused, on every screen, driven from one
    // place (NavigationInputModule).
    //
    // What it replaces, and why it is one object rather than a treatment per
    // screen: every screen that had a pad-focus visual at all grew its own
    // (Hub's building halos, Party's seat/card halos, the Dossier's cell
    // halos, the Reckoning's brightened offer halo), three screens had none
    // (Talents, Options, Fight), and the owner's hardware play-test rejected
    // the halo on both counts at once -- "the selector on the start descent
    // is huge and looks weird", "the gold halo (e.g. in party screen) is way
    // too strong", "a player can't see where they're going in the character
    // sheets screen". A glow scaled to the control is as big as the control,
    // so it cannot be small on a 620x620 building and visible on a 96x96 orb;
    // a marker of fixed size beside the control is the same size everywhere by
    // construction.
    //
    // A SCENE ROOT FIXTURE, built by SceneBuilder beside the camera, the
    // global Volume, the canvas and the EventSystem -- not a node in any
    // screen tree, and not created at runtime either.
    //
    // Runtime creation was the first shape and it was wrong for a stated
    // reason rather than a matter of taste: it put a second rect preamble
    // (anchors, pivot, sizeDelta) in this file, which UiKitLintTests'
    // OnlyTheEmitterMayCreateGameObjects exists to prevent -- v1 had 86
    // hand-rolled UI construction sites and they drifted. A screen tree was
    // wrong too, for the opposite reason: this marker belongs to the
    // DISPATCHER, not to any one screen, and putting it in eleven trees would
    // be eleven NodeRefs, eleven bindings and eleven UiAudit overlap
    // exemptions for an object whose whole job is to sit on top of whatever
    // it points at.
    //
    // A scene root fixture is neither. It is generated (CLAUDE.md rule 1),
    // it is configured in exactly one place, and SceneBuilder's own allowance
    // for these is bounded by a NUMBER a lint makes somebody edit
    // deliberately -- which is the project's own mechanism for "this is a
    // fixture, and here is the visible decision that says so".
    //
    // The cost, stated: a PlayMode fixture that builds its own EventSystem by
    // hand (NavigationDispatcherTests) has no marker, so the dispatcher's
    // marker calls are no-ops there. Every test that asserts anything about
    // the marker drives a real, SceneBuilder-generated scene.
    //
    // NEVER DEACTIVATED. Hiding is `_image.enabled = false`, not
    // SetActive(false) -- a deactivated GameObject stops receiving LateUpdate,
    // so a marker that hid itself that way could never notice it had a target
    // again and would stay gone for the rest of the scene.
    public class FocusMarker : MonoBehaviour
    {
        public const string ObjectName = "FocusMarker";

        // The kit's own ivory-gold (Domain/UiKit/FightHudPalette.GoldLight),
        // not a new colour: the marker has to read as part of this UI, and
        // every other focus/target cue in the game is already in this family.
        public const string Tint = FightHudPalette.GoldLight;

        private RectTransform _rect;
        private Image _image;
        private RectTransform _target;
        private Canvas _canvas;

        public RectTransform Target => _target;
        public bool IsShown => _image != null && _image.enabled;

        // Where the marker actually ended up, in its canvas's local space --
        // the value a PlayMode test reads back to prove it followed a Move to
        // the literal node, without having to redo the placement arithmetic
        // the test is checking (CLAUDE.md gotcha 5: a test that recomputes the
        // formula is a tautology).
        public Vector2 LocalPosition => _rect == null ? Vector2.zero : (Vector2)_rect.localPosition;

        // HIDDEN UNTIL SOMETHING FOCUSES SOMETHING. Awake rather than the
        // scene's serialized state, so a scene built before this rule existed
        // cannot ship a marker frozen over whatever control it happened to be
        // saved above.
        private void Awake()
        {
            _rect = (RectTransform)transform;
            _image = GetComponent<Image>();
            if (_image != null) _image.enabled = false;
        }

        // Called by the dispatcher once per frame with the settled focus, or
        // with null for "nothing is focused, or the mouse is driving".
        public void PointAt(RectTransform target)
        {
            _target = target;
            Reposition();
        }

        public void Hide() => PointAt(null);

        // LateUpdate, not Update: a control can be moved by a LayoutGroup or
        // by its own animator during the frame, and a marker positioned
        // before that is a frame behind the thing it points at on every
        // screen that animates -- which here is most of them.
        private void LateUpdate() => Reposition();

        // USABLE, not merely non-null: a focused control that was hidden or
        // destroyed mid-frame leaves a marker hovering over nothing, and
        // "shown and not destroyed" is the same question
        // NavigationInputModule.Usable already asks of a selection. The
        // dispatcher will have moved the focus by its next Process(); this
        // just refuses to draw in the gap.
        private void Reposition()
        {
            if (_rect == null || _image == null) return;

            if (_target == null || !_target.gameObject.activeInHierarchy)
            {
                _image.enabled = false;
                return;
            }

            var canvas = _target.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                _image.enabled = false;
                return;
            }

            AttachTo(canvas);

            var canvasRect = (RectTransform)_canvas.transform;
            var box = BoxIn(canvasRect, _target);

            // SCROLLED OUT OF ITS OWN WINDOW. A row a list has clipped away is
            // still active, still focused, and still has a rect -- it is just
            // not drawn. Pointing at it put the arrow in empty stage space
            // beside no row (QA 2026-09-26, the fight's twelve-potion list
            // wheeled two notches down with Potion 1 still focused). Every
            // enabled RectMask2D above the target is a window it can be
            // scrolled out of, whichever screen it is on.
            if (!VisibleThroughEveryClip(canvasRect, _target, box))
            {
                _image.enabled = false;
                return;
            }
            var frame = new UiRect(
                new UiVec(canvasRect.rect.center.x, canvasRect.rect.center.y),
                new UiVec(canvasRect.rect.width, canvasRect.rect.height));

            var edge = FocusMarkerPlacement.EdgeFor(box.Size);
            var at = FocusMarkerPlacement.Place(box, frame, edge)
                     + FocusMarkerPlacement.BobOffset(edge, Time.unscaledTime);

            _rect.localPosition = new Vector3(at.X, at.Y, 0f);
            _rect.localRotation = Quaternion.Euler(0f, 0f, FocusMarkerPlacement.RotationFor(edge));
            _rect.localScale = Vector3.one;
            _image.enabled = true;
        }

        // DRAWN ABOVE EVERYTHING THE TARGET IS DRAWN WITH, which in uGUI
        // means "last child of the target's OWN canvas". Re-asserted every
        // frame rather than once, because a screen that opens a panel after
        // this marker attached would otherwise draw over it.
        //
        // THE TARGET'S NEAREST CANVAS, NOT THE ROOT ONE, and the difference is
        // not theoretical: FightScreen wraps its whole HUD in
        // Ui.NestedCanvas("FightHud", 1000) with overrideSorting, so anything
        // parented to the root canvas draws UNDER all of it however late a
        // sibling it is. The first capture run caught it -- the Reckoning's
        // marker reported itself shown, at the right coordinates, and was
        // nowhere in the picture. Sharing the target's canvas is the rule that
        // cannot be wrong: a canvas that covers the marker covers the control
        // it points at too.
        private void AttachTo(Canvas canvas)
        {
            if (ReferenceEquals(_canvas, canvas) && _rect.parent == canvas.transform)
            {
                _rect.SetAsLastSibling();
                return;
            }

            _canvas = canvas;
            _rect.SetParent(canvas.transform, worldPositionStays: false);
            _rect.SetAsLastSibling();
        }

        private static readonly System.Collections.Generic.List<RectMask2D> Clips =
            new System.Collections.Generic.List<RectMask2D>();

        private static bool VisibleThroughEveryClip(RectTransform frame, RectTransform target, UiRect box)
        {
            target.GetComponentsInParent(false, Clips);

            for (int i = 0; i < Clips.Count; i++)
            {
                var clip = Clips[i];
                if (clip == null || !clip.enabled) continue;
                if (!FocusMarkerPlacement.IsVisibleWithin(box, BoxIn(frame, clip.rectTransform))) return false;
            }

            return true;
        }

        // The target's own rect, expressed in `frame`'s local space -- corners
        // first, then their axis-aligned bounding box.
        //
        // CORNERS, not position-and-sizeDelta: a control can be rotated (the
        // talent tree rotates its edges), scaled (ButtonPressAnimator lerps
        // every themed button's scale on hover) or nested under a parent doing
        // either, and only the corners carry all three. This is the same
        // reasoning UiRect.BoundingBoxAfterRotation already states for the
        // build-time audit, applied at runtime.
        private static UiRect BoxIn(RectTransform frame, RectTransform target)
        {
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            for (int i = 0; i < 4; i++)
            {
                var local = frame.InverseTransformPoint(corners[i]);
                if (local.x < minX) minX = local.x;
                if (local.x > maxX) maxX = local.x;
                if (local.y < minY) minY = local.y;
                if (local.y > maxY) maxY = local.y;
            }

            return new UiRect(
                new UiVec((minX + maxX) / 2f, (minY + maxY) / 2f),
                new UiVec(maxX - minX, maxY - minY));
        }
    }
}

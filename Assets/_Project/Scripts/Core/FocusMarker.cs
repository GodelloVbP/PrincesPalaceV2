using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // THE ONE PAD-FOCUS INDICATOR, project-wide: a small arrow that sits
    // beside whatever the pad has focused, on every screen, driven from one
    // place (NavigationInputModule).
    //
    // ONE OBJECT RATHER THAN A TREATMENT PER SCREEN: a per-screen halo
    // (Hub's building halos, Party's seat/card halos, the Dossier's cell
    // halos, the Reckoning's brightened offer halo) is scaled to its
    // control, so it cannot be small on a 620x620 building and visible on a
    // 96x96 orb; a marker of fixed size beside the control is the same size
    // everywhere by construction, and a screen with no halo of its own
    // (Talents, Options, Fight) gets one for free.
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
            // not drawn. Pointing at it would put the arrow in empty stage
            // space beside no row. Every enabled RectMask2D above the target
            // is a window it can be scrolled out of, whichever screen it is on.
            if (!VisibleThroughEveryClip(canvasRect, _target, box))
            {
                _image.enabled = false;
                return;
            }
            var frame = new UiRect(
                new UiVec(canvasRect.rect.center.x, canvasRect.rect.center.y),
                new UiVec(canvasRect.rect.width, canvasRect.rect.height));

            var spot = SpotFor(canvasRect, box, frame);
            var edge = spot.Edge;
            var at = spot.Centre + FocusMarkerPlacement.BobOffset(edge, Time.unscaledTime);

            _rect.localPosition = new Vector3(at.X, at.Y, 0f);
            _rect.localRotation = Quaternion.Euler(0f, 0f, FocusMarkerPlacement.RotationFor(edge));
            _rect.localScale = Vector3.one;
            _image.enabled = true;
        }

        // WHICH EDGE, with the neighbours consulted (FocusMarkerPlacement.
        // EdgeFor's header: a marker on another control reads as selecting
        // it). Decided EVERY FRAME, not cached: the first version kept its
        // answer for a quarter second and the capture caught it deciding
        // while the pack panel was still fading in (its sort tabs at alpha
        // 0, so not yet neighbours) and then standing on TIER and "+" long
        // after they appeared. The walk is kept cheap instead -- see
        // CollectObstacles.
        private readonly List<UiRect> _obstacles = new List<UiRect>();
        private readonly List<UiRect> _keepClear = new List<UiRect>();
        private static readonly List<TMP_Text> Texts = new List<TMP_Text>();
        private static Selectable[] _selectables = new Selectable[64];

        private FocusSpot SpotFor(RectTransform canvasRect, UiRect box, UiRect frame)
        {
            CollectObstacles(canvasRect, box, frame);
            return FocusMarkerPlacement.Resolve(box, frame, _obstacles, _keepClear);
        }

        private static readonly FocusEdge[] AllEdges =
            { FocusEdge.Left, FocusEdge.Above, FocusEdge.Right, FocusEdge.Below };
        private readonly UiRect[] _reach = new UiRect[4];

        // Every OTHER control and every piece of text a player can SEE on
        // the target's root canvas that one of the four candidate marker
        // boxes would touch: active, enabled, not faded out by a CanvasGroup,
        // not scrolled out of a clipping window, and not hidden under a
        // panel drawn over it. The target's own subtree (its label) and its
        // ancestors (the row a stepper sits in) are not neighbours. Text
        // counts by its INK (textBounds), not its rect -- a 380-wide label
        // box holding "ITEM" is not an obstacle 380 wide.
        //
        // CHEAP TESTS FIRST: the box overlap is arithmetic, so only a
        // control or label a candidate marker would actually touch gets the
        // parent walks, the ink measure and the raycast.
        private void CollectObstacles(RectTransform canvasRect, UiRect target, UiRect frame)
        {
            _obstacles.Clear();
            _keepClear.Clear();
            var root = _canvas == null ? null : _canvas.rootCanvas;
            if (root == null) return;

            // Keep-clear rects (a list's scrollbar, FocusKeepClear) first:
            // they can step a candidate spot outward, and the occupants
            // worth testing are the ones near where the marker will REALLY
            // stand. Few enough (one per visible bar) to test every one; the
            // same visibility filters as a control, minus the raycast -- a
            // bar's thumb is drawn over its track, and the track is the lane.
            var bars = FocusKeepClear.Active;
            for (int i = 0; i < bars.Count; i++)
            {
                var bar = bars[i];
                if (bar == null) continue;

                var rect = bar.Rect;
                var barBox = BoxIn(canvasRect, rect);
                if (barBox.Width <= 0f || barBox.Height <= 0f) continue;
                if (!IsNeighbour(bar.transform, root)) continue;
                if (!VisibleThroughEveryClip(canvasRect, rect, barBox)) continue;
                _keepClear.Add(barBox);
            }

            for (int e = 0; e < AllEdges.Length; e++)
                _reach[e] = FocusMarkerPlacement.MarkerBoxAt(
                    FocusMarkerPlacement.PlaceClear(target, frame, AllEdges[e], _keepClear));

            int count = Selectable.allSelectableCount;
            if (_selectables.Length < count) _selectables = new Selectable[count * 2];
            count = Selectable.AllSelectablesNoAlloc(_selectables);

            for (int i = 0; i < count; i++)
            {
                var other = _selectables[i];
                if (other == null) continue;

                var rect = (RectTransform)other.transform;
                var otherBox = BoxIn(canvasRect, rect);
                if (!Reached(otherBox) || !IsNeighbour(other.transform, root)) continue;
                if (!VisibleThroughEveryClip(canvasRect, rect, otherBox)) continue;
                if (!DrawnOnTop(other.transform, rect, otherBox, canvasRect, root)) continue;
                _obstacles.Add(otherBox);
            }

            root.GetComponentsInChildren(false, Texts);
            for (int i = 0; i < Texts.Count; i++)
            {
                var text = Texts[i];
                if (text == null || !text.enabled || text.color.a < 0.05f) continue;
                if (!Reached(BoxIn(canvasRect, text.rectTransform))) continue;
                if (string.IsNullOrWhiteSpace(text.text) || !IsNeighbour(text.transform, root)) continue;

                var ink = InkIn(canvasRect, text);
                if (ink.Width <= 0f || ink.Height <= 0f || !Reached(ink)) continue;
                if (!VisibleThroughEveryClip(canvasRect, text.rectTransform, ink)) continue;
                if (!DrawnOnTop(text.transform, text.rectTransform, ink, canvasRect, root)) continue;
                _obstacles.Add(ink);
            }
        }

        private bool Reached(UiRect box)
        {
            for (int e = 0; e < _reach.Length; e++)
            {
                if (_reach[e].Overlaps(box)) return true;
            }
            return false;
        }

        // NOT HIDDEN UNDER ANOTHER PANEL. An overlay leaves what it covers
        // active -- the dossier's pack sits over column A's own buttons, and
        // counting those put every edge of a pack cell "occupied" and the
        // marker back on the sort tabs it should have dodged. So: raycast at
        // the obstacle's centre, and it is on top when the first thing hit
        // is the obstacle itself, something inside it, or something it sits
        // inside (a label on its own plate). Nothing hit at all counts as on
        // top -- a bare label on the stage has nothing to test against, and
        // wrongly keeping an obstacle only costs a marker a side.
        private static readonly List<RaycastResult> Hits = new List<RaycastResult>();
        private static PointerEventData _probe;
        private static EventSystem _probeSystem;

        private static bool DrawnOnTop(Transform obstacle, RectTransform rect, UiRect box,
            RectTransform canvasRect, Canvas root)
        {
            var events = EventSystem.current;
            if (events == null) return true;

            var world = canvasRect.TransformPoint(new Vector3(box.Centre.X, box.Centre.Y, 0f));
            var camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, world);

            if (_probe == null || _probeSystem != events)
            {
                _probe = new PointerEventData(events);
                _probeSystem = events;
            }
            _probe.position = screen;

            Hits.Clear();
            events.RaycastAll(_probe, Hits);
            if (Hits.Count == 0 || Hits[0].gameObject == null) return true;

            var top = Hits[0].gameObject.transform;
            return top == obstacle || top.IsChildOf(obstacle) || obstacle.IsChildOf(top);
        }

        private static readonly List<CanvasGroup> Groups = new List<CanvasGroup>();

        private bool IsNeighbour(Transform other, Canvas root)
        {
            if (!other.gameObject.activeInHierarchy) return false;
            if (other == _target || other.IsChildOf(_target) || _target.IsChildOf(other)) return false;
            if (other.IsChildOf(transform)) return false;

            var canvas = other.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.rootCanvas != root) return false;

            other.GetComponentsInParent(false, Groups);
            for (int i = 0; i < Groups.Count; i++)
            {
                if (Groups[i].alpha < 0.05f) return false;
                if (Groups[i].ignoreParentGroups) break;
            }

            return true;
        }

        private static UiRect InkIn(RectTransform frame, TMP_Text text)
        {
            var bounds = text.textBounds;
            if (bounds.size.x <= 0f || bounds.size.y <= 0f) return BoxIn(frame, text.rectTransform);

            var t = text.rectTransform;
            var a = frame.InverseTransformPoint(t.TransformPoint(bounds.min));
            var b = frame.InverseTransformPoint(t.TransformPoint(bounds.max));

            float minX = Mathf.Min(a.x, b.x), maxX = Mathf.Max(a.x, b.x);
            float minY = Mathf.Min(a.y, b.y), maxY = Mathf.Max(a.y, b.y);
            return new UiRect(
                new UiVec((minX + maxX) / 2f, (minY + maxY) / 2f),
                new UiVec(maxX - minX, maxY - minY));
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
        // One buffer, not one per call: BoxIn runs for every nearby control
        // and label on every frame the marker is drawn.
        private static readonly Vector3[] Corners = new Vector3[4];

        private static UiRect BoxIn(RectTransform frame, RectTransform target)
        {
            var corners = Corners;
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

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PrincesPalace
{
    public enum CursorVisualState
    {
        Idle,
        Hover,
        Click,
    }

    // The painted cursor, ported from v1.
    //
    // Self-bootstrapping, the same shape SoundController uses: it fires before
    // any scene loads, so it works everywhere without a wired reference and
    // without SceneBuilder needing to know it exists. Three painted textures,
    // swapped on whether the left button is held and whether the pointer is
    // over something actually clickable.
    //
    // The art was already here -- Resources/Cursors/Cursor_idle|hover|click,
    // 67x50 apiece, imported as readable Default textures. Only the controller
    // was missing, which is why this is a port of one file rather than a
    // feature.
    public class CursorController : MonoBehaviour
    {
        // The arrow's point inside each texture: the pixel treated as the
        // actual click location.
        //
        // ALL THREE TEXTURES SHARE ONE CANVAS AND ONE TIP POSITION, cropped to
        // the same UNION bounding box rather than to their own -- so swapping
        // state mid-hover cannot make the pointer jump. v1 scaled the original
        // 96x71 art by 0.70 to reach 67x50 and scaled this with it, because a
        // hotspot is in texture pixels and would otherwise drift off the tip.
        private static readonly Vector2 Hotspot = new Vector2(11f, 4f);

        // ForceSoftware, not Auto, and this is the setting that makes the
        // cursor the size it looks. Auto hands the texture to the OS as a
        // hardware cursor, where Windows caps it around 32px and quietly
        // scales anything larger back down -- which is why enlarging the
        // source art alone did nothing in v1. Software means Unity draws it as
        // part of the frame, so the texture's size IS the on-screen size. The
        // cost is that it updates with the frame rather than with the OS
        // pointer, which is a fair trade in a turn-based game and the only way
        // to control the rendered size at all.
        private const CursorMode Mode = CursorMode.ForceSoftware;

        private static CursorController _instance;

        private Texture2D _idleTexture;
        private Texture2D _hoverTexture;
        private Texture2D _clickTexture;
        private bool _hasAppliedOnce;
        private CursorVisualState _currentState;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null)
            {
                return;
            }

            var go = new GameObject("CursorController");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<CursorController>();
            _instance.LoadTextures();
        }

        private void LoadTextures()
        {
            _idleTexture = Resources.Load<Texture2D>("Cursors/Cursor_idle");
            _hoverTexture = Resources.Load<Texture2D>("Cursors/Cursor_hover");
            _clickTexture = Resources.Load<Texture2D>("Cursors/Cursor_click");
        }

        private void Update()
        {
            // Missing art leaves the OS cursor alone rather than fighting it --
            // the same graceful-degradation posture the rest of the project
            // takes toward art that has not landed yet.
            if (_idleTexture == null)
            {
                return;
            }

            var desired = DetermineState(Input.GetMouseButton(0), IsPointerOverInteractable());

            if (_hasAppliedOnce && desired == _currentState)
            {
                return;
            }

            Apply(desired);
        }

        // Reused rather than allocated: this runs every frame, and RaycastAll
        // into a fresh List would produce garbage continuously.
        private static readonly List<RaycastResult> RaycastBuffer = new List<RaycastResult>();

        // "You can click THIS", not "the pointer is somewhere over the UI".
        //
        // EventSystem.IsPointerOverGameObject answers the second question, and
        // in this game it is true essentially always -- every panel has a
        // full-screen background Image and they are all raycast targets. v1
        // used it and the cursor sat permanently in its hover state; the idle
        // art was never once seen.
        //
        // So: raycast, then WALK the hit stack for a Selectable that is enabled
        // and interactable. Walking rather than testing the topmost hit is the
        // part that matters -- a button's own label is usually the top hit and
        // carries no Selectable, so checking only the first result reports "not
        // interactable" while the pointer is dead centre on a button.
        private static bool IsPointerOverInteractable()
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return false;
            }

            var pointer = new PointerEventData(eventSystem) { position = Input.mousePosition };
            RaycastBuffer.Clear();
            eventSystem.RaycastAll(pointer, RaycastBuffer);

            foreach (var hit in RaycastBuffer)
            {
                if (hit.gameObject == null)
                {
                    continue;
                }

                var selectable = hit.gameObject.GetComponentInParent<Selectable>();
                if (selectable != null && selectable.isActiveAndEnabled && selectable.interactable)
                {
                    return true;
                }
            }

            return false;
        }

        // Pure, and separated for that reason: the decision is testable without
        // a mouse, an EventSystem or a scene. Click beats everything -- a press
        // is a press regardless of what is under the pointer.
        public static CursorVisualState DetermineState(bool isMouseDown, bool isPointerOverUi)
        {
            if (isMouseDown)
            {
                return CursorVisualState.Click;
            }

            return isPointerOverUi ? CursorVisualState.Hover : CursorVisualState.Idle;
        }

        private void Apply(CursorVisualState state)
        {
            _currentState = state;
            _hasAppliedOnce = true;

            Texture2D texture;
            switch (state)
            {
                case CursorVisualState.Hover:
                    texture = _hoverTexture;
                    break;
                case CursorVisualState.Click:
                    texture = _clickTexture;
                    break;
                default:
                    texture = _idleTexture;
                    break;
            }

            if (texture != null)
            {
                Cursor.SetCursor(texture, Hotspot, Mode);
            }
        }
    }
}

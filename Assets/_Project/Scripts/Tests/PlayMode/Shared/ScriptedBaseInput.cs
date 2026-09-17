using UnityEngine;
using UnityEngine.EventSystems;

namespace PrincesPalace.PlayModeTests
{
    // A scripted UnityEngine.EventSystems.BaseInput, assigned through
    // BaseInputModule.inputOverride. docs/GAMEPAD_NAVIGATION_PLAN.md section 2
    // verifies every value StandaloneInputModule reads for UI dispatch goes
    // through this exact seam (input.mousePosition, input.GetAxisRaw,
    // input.GetButtonDown, input.GetMouseButtonDown/Up) -- so a test that
    // sets these fields and drives the REAL module with `yield return null`
    // proves the dispatcher's own behaviour, not a direct handler call.
    //
    // Every "Down"/"Up" field is a ONE-FRAME edge, matching real input: the
    // test sets it before a `yield return null`, then calls
    // ClearOneFrameFlags before the next frame it drives. Axis values and
    // mouse position are levels and persist until the test changes them.
    public class ScriptedBaseInput : BaseInput
    {
        public Vector2 MousePosition;
        public bool MouseButton0Down;
        public bool MouseButton0Up;
        public bool MouseButton0Held;
        public float Vertical;
        public float Horizontal;
        public bool SubmitDown;
        public bool CancelDown;

        public override Vector2 mousePosition => MousePosition;
        public override bool mousePresent => true;

        public override bool GetMouseButtonDown(int button) => button == 0 && MouseButton0Down;
        public override bool GetMouseButtonUp(int button) => button == 0 && MouseButton0Up;
        public override bool GetMouseButton(int button) => button == 0 && MouseButton0Held;

        public override float GetAxisRaw(string axisName)
        {
            if (axisName == "Vertical") return Vertical;
            if (axisName == "Horizontal") return Horizontal;
            return 0f;
        }

        public override bool GetButtonDown(string buttonName)
        {
            if (buttonName == "Submit") return SubmitDown;
            if (buttonName == "Cancel") return CancelDown;
            return false;
        }

        public void ClearOneFrameFlags()
        {
            MouseButton0Down = false;
            MouseButton0Up = false;
            SubmitDown = false;
            CancelDown = false;
        }
    }
}

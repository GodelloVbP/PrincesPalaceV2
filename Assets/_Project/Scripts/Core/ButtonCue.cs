using UnityEngine;
using UnityEngine.UI;

namespace PrincesPalace
{
    // THE ONE PLACE A BUTTON'S PRESS CUE IS DECIDED, whatever input activated
    // it. ButtonPressAnimator and ThemedButtonState each own a `clickSound`
    // (Quiet()/Closes() set it at build time); this is the rule they share:
    // a refused press on a non-interactable button is ClickFail, a Silent
    // button stays silent even when refused, anything else plays its cue.
    //
    // Reached from two directions that end in the same call. The EventSystem
    // delivers a pointer click or a Submit to the components' handlers
    // (IPointerClickHandler, ISubmitHandler), and the gamepad dispatchers that
    // press a control without an EventSystem event -- the fight's
    // ConfirmFocus -- call Activate on the focused node.
    public static class ButtonCue
    {
        // Two sound components on one button (nothing builds that today)
        // would both hear the same click. The second component to fire for
        // the same button in the same frame is dropped; the same component
        // firing twice is two presses.
        private static int _lastFrame = -1;
        private static EntityId _lastButton;
        private static EntityId _lastOwner;

        public static void Fire(Component owner, Selectable button, Sound cue)
        {
            int frame = Time.frameCount;
            var buttonId = owner.gameObject.GetEntityId();
            var ownerId = owner.GetEntityId();
            bool echo = frame == _lastFrame && buttonId == _lastButton && ownerId != _lastOwner;
            _lastFrame = frame;
            _lastButton = buttonId;
            _lastOwner = ownerId;
            if (echo) return;

            if (button != null && !button.interactable)
            {
                if (cue != Sound.None) SoundController.Play(Sound.ClickFail);
                return;
            }

            SoundController.Play(cue);
        }

        // Presses `node`'s cue for a dispatcher that activates it without an
        // EventSystem event. A node with no cue component (a decor object, a
        // null) is silent.
        public static void Activate(GameObject node)
        {
            if (node == null) return;

            var press = node.GetComponent<ButtonPressAnimator>();
            if (press != null)
            {
                press.Activate();
                return;
            }

            var themed = node.GetComponent<ThemedButtonState>();
            if (themed != null) themed.Activate();
        }
    }
}

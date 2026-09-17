using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PrincesPalace
{
    // Reports EventSystem SELECTION (not pointer hover) on one indexed thing
    // -- the gamepad-navigation counterpart to HoverIndex, for a control whose
    // job when the module selects it is distinct from the job a mouse hover
    // already does (docs/GAMEPAD_NAVIGATION_PLAN.md section 7: "selecting a
    // node is a distinct action from revealing it"). RewardTrack's ribbon
    // nodes are the first user: becoming selected calls the controller's
    // existing ScrollTo(level), the same reveal a mouse hover already
    // triggers through HoverIndex, but driven by Move instead of the pointer
    // -- one input (Move) moves selection, selection alone drives the
    // reveal, so there is no second poll left to double-drive it.
    //
    // Added at RUNTIME by the controller, same bargain HoverIndex's own
    // header makes: it carries a delegate and an index, neither of which a
    // scene can serialise usefully.
    public class SelectIndex : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        public int Index;
        public Action<int, bool> Changed;

        public void OnSelect(BaseEventData eventData) => Changed?.Invoke(Index, true);

        public void OnDeselect(BaseEventData eventData) => Changed?.Invoke(Index, false);
    }
}

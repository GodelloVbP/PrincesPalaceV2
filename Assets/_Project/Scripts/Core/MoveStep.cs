using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PrincesPalace
{
    // Reports a Left/Right Move on whatever it is attached to, as -1/+1, and
    // ignores Up/Down entirely -- those stay with the Selectable's own
    // Explicit links.
    //
    // For a control whose horizontal axis ADJUSTS something rather than
    // navigating (the reward track's ribbon: Left/Right scrubs the window).
    // It sits beside the emitted Button rather than replacing it, which is
    // only safe because the caller leaves that Button's Left/Right links
    // empty: ExecuteEvents runs every IMoveHandler on the object, and a
    // Left link as well would move focus AND step on the same press.
    // OptionRow is the other shape -- one Selectable subclass doing both --
    // for rows the tree emits as that type.
    //
    // Added at RUNTIME, same bargain SelectIndex and HoverIndex make: a
    // delegate is not something a scene can serialise.
    public class MoveStep : MonoBehaviour, IMoveHandler
    {
        public Action<int> Stepped;

        public void OnMove(AxisEventData eventData)
        {
            if (eventData.moveDir == MoveDirection.Left) Stepped?.Invoke(-1);
            else if (eventData.moveDir == MoveDirection.Right) Stepped?.Invoke(1);
        }
    }
}

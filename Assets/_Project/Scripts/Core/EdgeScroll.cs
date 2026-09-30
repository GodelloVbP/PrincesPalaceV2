using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PrincesPalace
{
    // Reports an Up/Down Move on a cell of a windowed grid, so the window can
    // move under a focus that stays put. The LOADOUT pack shows twelve cells of
    // a list of any length: Down off its bottom row scrolls one row and leaves
    // focus on the same cell, which now shows the entry below.
    //
    // It sits beside the cell's own Button, which is safe only because the
    // caller leaves that Button's link empty in exactly the direction it wants
    // to scroll: ExecuteEvents runs every IMoveHandler on the object, and a
    // link as well would move focus AND scroll on the same press. The same
    // bargain MoveStep makes on the other axis.
    //
    // Added at RUNTIME, like SelectIndex and HoverIndex: a delegate is not
    // something a scene can serialise.
    public class EdgeScroll : MonoBehaviour, IMoveHandler
    {
        public Action<MoveDirection> Moved;

        public void OnMove(AxisEventData eventData)
        {
            if (eventData.moveDir == MoveDirection.Up || eventData.moveDir == MoveDirection.Down)
            {
                Moved?.Invoke(eventData.moveDir);
            }
        }
    }
}

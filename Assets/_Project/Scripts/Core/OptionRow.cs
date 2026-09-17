using UnityEngine.EventSystems;

namespace PrincesPalace
{
    // The ONE component handling an Options row's movement and hover (plan
    // section 7). Not a plain Selectable plus a second IMoveHandler:
    // ExecuteEvents.Execute<T> runs EVERY matching component on the target,
    // so two would both fire on the same press. This REPLACES the row's old
    // plain Panel + HoverIndex pair with the one component that is now both.
    //
    // Left/Right adjusts the row's own bound value and consumes the event
    // without calling base.OnMove; Up/Down calls base.OnMove unchanged, which
    // is ordinary Unity row-to-row navigation over whatever Explicit chain
    // OptionsController.Wire set up (RuntimeNavWiring, a List group, clamp).
    //
    // OnLeftRight is the ONLY thing this component knows how to do -- it does
    // not know GameSettings, a key, or even whether it is a Slider or a
    // Stepper row. OptionsController.Wire supplies one delegate per row,
    // closed over that row's own key and kind, so this stays a pure input
    // adapter and every GameSettings binding stays exactly where it already
    // lived. HoverChanged is the same shape HoverIndex.Changed used to be,
    // just per-instance instead of index-keyed, since each row now owns its
    // own component instance rather than sharing one dispatcher.
    public class OptionRow : UnityEngine.UI.Selectable
    {
        public System.Action<int> OnLeftRight;
        public System.Action<bool> HoverChanged;

        public override void OnMove(AxisEventData eventData)
        {
            switch (eventData.moveDir)
            {
                case MoveDirection.Left:
                    OnLeftRight?.Invoke(-1);
                    eventData.Use();
                    return;

                case MoveDirection.Right:
                    OnLeftRight?.Invoke(1);
                    eventData.Use();
                    return;

                default:
                    base.OnMove(eventData);
                    return;
            }
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            HoverChanged?.Invoke(true);
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);
            HoverChanged?.Invoke(false);
        }
    }
}

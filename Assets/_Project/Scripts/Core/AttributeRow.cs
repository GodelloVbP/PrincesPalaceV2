using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace PrincesPalace
{
    // A ROW THAT ANSWERS LEFT/RIGHT AS REFUND/SPEND -- the attributes
    // panel's own stepper row (CharacterDossierController), matching the
    // WIRING SHAPE Core/OptionRow.cs already uses for the Options screen
    // (OnMove intercepts Left/Right and calls a delegate; every other
    // direction falls through to ordinary row-to-row navigation), without
    // being a copy of that class: OptionRow extends Selectable and has no
    // Submit-triggered action at all (an Options row is never itself
    // pressed -- its steppers are separate mouse-only buttons beside it).
    // This one extends Button instead, specifically so Submit -- and an
    // ordinary mouse click on the row's own body -- fires the exact same
    // spend Right does, through the onClick every Button already carries.
    // Only Left/Right needed a deliberate override.
    //
    // ITS OWN FILE, not a second class tucked inside
    // CharacterDossierController.cs where it first lived: Unity's scene
    // serialization ties a component reference to the MonoScript asset
    // whose FILE NAME matches the class, and a class living in a
    // differently-named file can write back as a missing script once a
    // scene is rebuilt in a synced TestRunner copy -- exactly the "The
    // referenced script (Unknown) on this Behaviour is missing!" warning a
    // batch of unrelated *LifecycleTests started throwing the moment this
    // component's instances first appeared in Hub.unity. Same rule
    // Core/OptionRow.cs already follows for the same reason.
    //
    // Attached at build time by ScreenRegistry.WireDossier
    // (result.Attach<AttributeRow>), the same seam WireOptions uses for
    // OptionRow -- see CharacterDossierScreen.BuildAttributesPanel's own
    // comment for why the row has to be built as a plain Panel rather than
    // a Ui.Button for this to attach cleanly (one Selectable per
    // GameObject, not two fighting over the same rect).
    public sealed class AttributeRow : Button
    {
        public System.Action<int> OnLeftRight;

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
    }
}

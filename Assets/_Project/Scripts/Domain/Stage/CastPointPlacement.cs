using PrincesPalace.Domain.UiKit;

namespace PrincesPalace.Domain.Stage
{
    // WHERE AN AUTHORED CAST POINT LANDS ON STAGE, right now -- the one piece
    // of FightController's placement seam (CasterCastPoint) that is a
    // FORMULA rather than "ask the transform", pulled out here so it can be
    // pinned with literal numbers in the fast EditMode suite (Domain is
    // engine-free by asmdef) instead of only from a running scene the way
    // every other placement rule in FightController.SpellVfx.cs has to be.
    //
    // THE REDUCTION THIS RELIES ON. FightController actually places a cast
    // point through RectTransform.TransformPoint/InverseTransformPoint, the
    // same two calls every other point in that file goes through -- this is
    // not a second, competing implementation, it is what that call reduces
    // to for the one case a cast point is ever asked about: a LOCAL point on
    // a slot that carries no rotation, converted into a sibling transform
    // (the VFX pool) that shares the slot's own frame -- both are centred at
    // the HUD's own origin (see FightScreen.BuildStage/BuildSpellVfx). With
    // no rotation and a shared frame, `TransformPoint` is exactly
    // `origin + local * scale` and `InverseTransformPoint` undoes nothing
    // further -- so `slotOrigin` (the caster's own pivot, already carrying
    // depth scale and the current hover offset because AnchorOne writes
    // depth into localScale and SetHover writes hover straight into
    // anchoredPosition every frame) plus the authored offset, scaled and
    // mirrored, is the whole answer.
    //
    // `mirror` is the SPRITE's own facing flip (StageFacing.MirrorScaleX),
    // never the cast's target-relative facing: dx is authored relative to
    // how the actor is actually drawn, not to which target it is hitting.
    // `scale` is the slot's own uniform localScale (AnchorOne always writes
    // (scale, scale, 1) -- FightStageAnchors.SlotScale times the actor's own
    // stage presence), the same number that already resizes the sprite,
    // never re-derived here.
    public static class CastPointPlacement
    {
        public static UiVec OnStage(CastPointSpec point, float mirror, float scale, UiVec slotOrigin)
        {
            return new UiVec(
                slotOrigin.X + point.Dx * mirror * scale,
                slotOrigin.Y + point.Dy * scale);
        }
    }
}

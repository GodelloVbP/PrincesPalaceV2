using System.Collections.Generic;
using UnityEngine;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Presentation;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Stage;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // Landing a spell's frame sequence on the thing it hit.
    //
    // Almost all of this file is one problem: WHERE the bottom of the effect
    // actually is. Three separate things move it, and every one of them was
    // found by an effect erupting somewhere anatomically wrong.
    public partial class FightController
    {
        // The box a layer's art is fitted into.
        //
        // AUTHORED PER LAYER. This was a constant for every effect there had
        // ever been -- fine for a bolt, wrong in both directions for a boss's
        // slam and for a status glint -- then per spell, and now per layer,
        // because a compact core and the crown it breaks into are two sizes in
        // one cast.
        //
        // SQUARE UNLESS THE LAYER SAYS OTHERWISE. `aspect` 0 means "the box is
        // square and preserveAspect fits the drawing inside it", which is what
        // every sheet before the ground fault was drawn for; an authored aspect
        // is for art whose box must differ from the frame.
        //
        // `fitFactor` IS INERT UNLESS `fit: target` IS AUTHORED -- every call
        // site passes 1f for a layer that does not fit and gets its authored
        // box back. SpellLayerRules.CheckPlacement is what keeps a layer from
        // authoring the word where no single target's body is resolved, so
        // this function does not have to ask which placement it was given.
        private static Vector2 BoxForLayer(SpellLayer layer, float fitFactor)
        {
            float size = (layer.size > 0f ? layer.size : SpellPresentation.DefaultSize)
                         * (layer.scale > 0f ? layer.scale : 1f);
            if (layer.Fit == SpellFit.Target) size *= fitFactor;
            float aspect = layer.aspect;
            return aspect > 0f ? new Vector2(size, size / aspect) : new Vector2(size, size);
        }

        // THE SAME MEASURE StageStandOff READS OFF AN ACTOR TO CLOSE A GAP --
        // RefreshStage composes it once, at AnchorOne, as `FightStageAnchors.
        // SlotScale(rank, count) * the enemy's own authored StageScale`, and
        // writes it onto the slot's localScale; the animator's BaseScale is
        // that same number sampled off the RESTING pose rather than the live
        // one, which is what FightBeatPlayer.ScaleOf and PlayContactFx's own
        // ContactBoxFor already read for the identical reason: the struck
        // target's localScale is mid-squash on the very frame an effect is
        // being placed, and BaseScale is not.
        //
        // FALLS BACK TO THE SLOT'S OWN localScale for a combatant with no
        // animator wired (a synthetic fixture), and to 1 for a zero or
        // unanchored scale -- the same "not yet anchored" floor ContactBoxFor
        // uses, so a body on an unplaced slot measures at its drawn size
        // rather than at nothing.
        private float RestingScaleOf(CombatantState who, RectTransform slot)
        {
            var animator = who != null ? AnimatorFor(who) : null;
            float scale = animator != null
                ? Mathf.Abs(animator.BaseScale.x)
                : slot != null ? Mathf.Abs(slot.localScale.x) : 1f;
            return scale > 0.01f ? scale : 1f;
        }

        // WHERE THIS COMBATANT'S VISIBLE BODY IS, in the effect pool's frame --
        // the target-bounds contract every per-target layer positions and
        // sizes against (Domain.Stage.TargetBody has the why). The slot's
        // origin through the same TransformPoint dance as everything else in
        // this file; the opaque box off the idle drawing; the arithmetic in
        // Domain where TargetBodyTests pins it.
        //
        // AN ACTOR WITH NO READABLE ART falls back to the slot's own rect --
        // exactly what every placement here measured before the body existed,
        // so a synthetic fixture or a missing sheet lands where it always did.
        private StageBody BodyOf(CombatantState who, RectTransform slot, Transform parent)
        {
            var origin = parent.InverseTransformPoint(slot.TransformPoint(Vector3.zero));
            float scale = RestingScaleOf(who, slot);

            string folder = who != null ? SpriteFolderFor(who) : null;
            var drawn = OpaqueBoxForActor(folder);

            StageBody local;
            if (drawn.HasValue)
            {
                var side = who.IsPlayerSide ? StageSide.Left : StageSide.Right;
                float mirror = StageFacing.MirrorScaleX(FacingOf(who), side);
                var box = drawn.Value.Box;
                local = TargetBody.SlotLocal(box.xMin, box.yMin, box.xMax, box.yMax,
                    drawn.Value.CanvasWidth, StanceManifestLoader.Manifest.GroundLineFor(folder), mirror);
            }
            else
            {
                var rect = slot.rect;
                local = new StageBody(rect.xMin, rect.xMax, rect.yMin, rect.yMax);
            }

            return TargetBody.OnStage(local, scale, new UiVec(origin.x, origin.y));
        }

        // Public for the PlayMode fixtures, for the reason every seam in this
        // file is: InternalsVisibleTo names the EDITOR assembly only.
        public StageBody BodyOfForTest(CombatantState who)
        {
            var slot = SlotFor(who);
            var parent = PrimaryPlayer != null ? PrimaryPlayer.transform.parent : null;
            return slot == null || parent == null ? default : BodyOf(who, slot, parent);
        }

        // THE IDLE DRAWING'S OPAQUE BOX, in canvas pixels up from the canvas's
        // bottom-left, plus that canvas's width. Cached per folder and cleared
        // per fight beside the stage's other pixel caches, for the reason that
        // clear gives: an asset-only re-slice recompiles nothing.
        private readonly struct DrawnBox
        {
            internal readonly Rect Box;
            internal readonly float CanvasWidth;

            internal DrawnBox(Rect box, float canvasWidth)
            {
                Box = box;
                CanvasWidth = canvasWidth;
            }
        }

        private static readonly Dictionary<string, DrawnBox?> OpaqueBoxCache = new Dictionary<string, DrawnBox?>();

        private static void ClearOpaqueBoxCache() => OpaqueBoxCache.Clear();

        private static DrawnBox? OpaqueBoxForActor(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) return null;
            if (OpaqueBoxCache.TryGetValue(folder, out var cached)) return cached;

            var idle = StanceAnimationLibrary.Resolve(folder, FightSession.Stances.Idle);
            var measured = OpaqueBox(idle);
            OpaqueBoxCache[folder] = measured;
            return measured;
        }

        // TRIMMED-CROP AWARE, the lesson FootBandCentreFraction's header
        // records: GetPixels32 indexes the TEXTURE at textureRect, and a pixel
        // found there is `textureRectOffset` further into the sprite's own
        // canvas. Same alpha floor as the stage's other scans.
        private static DrawnBox? OpaqueBox(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null || !sprite.texture.isReadable) return null;

            var crop = sprite.textureRect;
            int cropX = (int)crop.x;
            int cropY = (int)crop.y;
            int cropWidth = (int)crop.width;
            int cropHeight = (int)crop.height;
            if (cropWidth <= 0 || cropHeight <= 0 || sprite.rect.width <= 0f) return null;

            var pixels = sprite.texture.GetPixels32();
            int textureWidth = sprite.texture.width;
            int left = int.MaxValue, right = int.MinValue, bottom = int.MaxValue, top = int.MinValue;

            for (int y = 0; y < cropHeight; y++)
            {
                int rowStart = (cropY + y) * textureWidth + cropX;
                for (int x = 0; x < cropWidth; x++)
                {
                    if (pixels[rowStart + x].a <= AlphaFloorByte) continue;
                    if (x < left) left = x;
                    if (x > right) right = x;
                    if (y < bottom) bottom = y;
                    if (y > top) top = y;
                }
            }

            if (left > right || bottom > top) return null;

            var offset = sprite.textureRectOffset;
            return new DrawnBox(
                new Rect(offset.x + left, offset.y + bottom, right - left + 1, top - bottom + 1),
                sprite.rect.width);
        }

        // A per-pixel scan of every frame of a sheet, asked for on every cast.
        // Keyed by PATH rather than by sprite so nothing accumulates across
        // scene loads -- the same arrangement the stage's content-centre cache
        // uses.
        private static readonly Dictionary<string, float> VfxPaddingCache = new Dictionary<string, float>();

        // How long after the beat opens the blow actually lands.
        //
        // Zero for a plain swing, which is what makes the whole impact-delay
        // machinery invisible for the overwhelming majority of beats. For a
        // spell it is the fraction of the sequence the impact frame sits at --
        // computed in Domain, because "which frame is the impact" is authored
        // content and not a view decision.
        //
        // Public, so a test can assert the plain-swing case without reflection.
        // NO LONGER A FUNCTION OF PNGs ON DISK, for a spell that authors its cue
        // in seconds. A pre-layer block still derives it from impactFrame over
        // the frame count -- exactly the expression that used to live here --
        // because changing that would retime five shipped spells whose art went
        // momentarily unreadable. Both derivations live in SpellPerformance,
        // which is the only thing in the program that reads layerFormat at run time.
        public float ImpactDelayFor(CombatBeat beat) => ResolveCast(beat)?.HitCueSeconds ?? 0f;

        // Every layer of this beat's cast, fanned out to the targets it struck,
        // with every time already absolute. Pure apart from the frame counts it
        // reads off disk, so calling it twice for one beat -- the beat player
        // asks for the cue after it asks for the drawing -- gives the same
        // answer both times rather than two schedules that could disagree.
        private SpellPerformance ResolveCast(CombatBeat beat)
        {
            if (beat?.Vfx == null) return null;

            int struck = 0;
            foreach (var _ in StruckBy(beat)) struck++;

            return SpellPerformance.Resolve(beat.Vfx, struck, FrameCountOf);
        }

        // How many frames a Resources folder holds. Frames are a property of
        // the SHEET, so asking any pool member gives the same answer -- asking
        // a fixed one keeps that obvious.
        private int FrameCountOf(string path) => PrimaryPlayer?.Frames(path)?.Length ?? 0;

        // ---- seams for the PlayMode tests -----------------------------------------
        //
        // Placement is the half of this file that has ever been wrong, and it is
        // only observable from a running scene: the maths depends on the stage's
        // depth scaling and on where the slots actually landed, neither of which
        // an EditMode test can build. InternalsVisibleTo names the EDITOR
        // assembly only, so a PlayMode test reaches these or reaches nothing.

        // RETURNS THE HANDLE, because "did this cast keep its own renderers
        // while that one drew" is the whole of the ownership contract and is
        // unaskable without one. Callers that only wanted the drawing ignore
        // the value, which is why widening it broke nothing.
        public CastHandle PlaySpellVfxForTest(CombatBeat beat) => PlaySpellVfx(beat);

        // The house's OWN contact language, for the allocation test that
        // proves the cached templates actually stopped it building fresh
        // SpellLayer objects every plain melee blow. PlayContactFx has no
        // return value FightBeatPlayer's own caller wants, so there is
        // nothing to hand back here either.
        public void PlayContactFxForTest(CombatBeat beat) => PlayContactFx(beat);

        // The module itself, for the tests that ask which member a cast holds.
        // Public for the reason every seam in this file is: InternalsVisibleTo
        // names the EDITOR assembly only, so a PlayMode test reaches this or
        // reaches nothing.
        public SpellPerformancePlayer PerformancePlayerForTest => performancePlayer;

        public RectTransform SlotForTest(CombatantState combatant) => SlotFor(combatant);

        public FightSession SessionForTest => _session;

        public StageShake[] StageShakesForTest => stageShakes;

        // The shared ground layer. Public for the same reason the seams above
        // are: InternalsVisibleTo names the EDITOR assembly only, so a PlayMode
        // test reaches this or reaches nothing -- and "is there exactly one
        // fault, behind everyone" is only answerable from a running scene.
        public SpellVfxPlayer GroundVfxPlayerForTest =>
            spellGroundVfxPlayers != null && spellGroundVfxPlayers.Length > 0
                ? spellGroundVfxPlayers[0]
                : null;

        // THE WHOLE BAND, because it stopped being a pool of one. A fixture
        // that wants "a per-target renderer" has to exclude every ground
        // member, not the first: they carry the same component and are declared
        // FIRST in the tree (behind the racks), so excluding one by identity
        // silently hands back the next one -- a member a per-target cast never
        // touches, which reads as "the effect was never drawn".
        public IReadOnlyList<SpellVfxPlayer> GroundVfxPlayersForTest =>
            spellGroundVfxPlayers ?? System.Array.Empty<SpellVfxPlayer>();

        // Index 0, and the only one the measurement helpers ever touch. Frames
        // and dead space are properties of the SHEET, so asking any member
        // gives the same answer -- asking a fixed one keeps that obvious.
        private SpellVfxPlayer PrimaryPlayer =>
            spellVfxPlayers != null && spellVfxPlayers.Length > 0 ? spellVfxPlayers[0] : null;

        // EVERY LAYER OF THE CAST, PLACED, THEN HANDED TO THE MODULE.
        //
        // What this replaces walked the struck list handing out pool members by
        // POSITION IN THAT WALK, so every cast started again at member 0 and a
        // second cast on a live member restarted it. Ownership is the cast now,
        // and this file keeps the half it is actually about -- WHERE the bottom
        // of an effect is, which is the question every bug in it came from --
        // while SpellPerformancePlayer owns when things start, when they stop
        // and which renderer they hold.
        private CastHandle PlaySpellVfx(CombatBeat beat)
        {
            if (performancePlayer == null) return CastHandle.None;

            var performance = ResolveCast(beat);
            if (performance == null || performance.Instances.Count == 0) return CastHandle.None;

            PlaceCast(beat, performance);
            return performancePlayer.Begin(performance);
        }

        // WHERE EACH INSTANCE'S BOX GOES, written onto the instance once.
        //
        // Here rather than inside the module, because every line of it depends
        // on the stage's depth scaling, on where the slots actually landed and
        // on a sheet's own transparent margins -- the three things this file
        // exists to measure and the module has no opinion about.
        private void PlaceCast(CombatBeat beat, SpellPerformance performance)
        {
            var parent = PrimaryPlayer != null ? PrimaryPlayer.transform.parent : null;
            if (parent == null) return;

            var struck = new List<CombatantState>();
            foreach (var one in StruckBy(beat)) struck.Add(one);

            var casterRect = SlotFor(beat.Actor);
            float casterX = casterRect == null
                ? 0f
                : parent.InverseTransformPoint(casterRect.TransformPoint(Vector3.zero)).x;

            // FACING IS A PROPERTY OF THE CAST, decided once and before any box
            // is placed -- the horizontal impact correction depends on it, and
            // a projectile and the wake riding it must not disagree about which
            // way they point. Read off the PRIMARY target, since a caster never
            // stands between two of its own targets: both racks are on one side
            // of the stage. If a formation ever straddles a caster, the
            // per-cast answer becomes the wrong one for the far target and the
            // fix is to decide it per struck slot again.
            float facing = 1f;
            if (casterRect != null && struck.Count > 0)
            {
                var primaryRect = SlotFor(struck[0]);
                if (primaryRect != null)
                {
                    facing = AimPoint(parent, primaryRect, centred: true).x >= casterX ? 1f : -1f;
                }
            }

            foreach (var instance in performance.Instances)
            {
                instance.Facing = facing;
                PlaceOne(instance, performance, parent, struck, beat.Actor, casterRect, casterX);
            }
        }

        private void PlaceOne(SpellLayerInstance instance, SpellPerformance performance, Transform parent,
            List<CombatantState> struck, CombatantState caster, RectTransform casterRect, float casterX)
        {
            var layer = instance.Layer;

            if (layer.Place == SpellPlace.Formation)
            {
                PlaceOnFormation(instance, parent, struck);
                return;
            }

            // WHOSE BODY. A caster-anchored effect moves its origin, not its
            // shape. A travelling one always AIMS at the target however
            // caster-side its placement is -- `caster` says where the box
            // starts, and a cast reaching three enemies needs three boxes
            // leaving the same point.
            var targetCombatant = instance.TargetIndex >= 0 && instance.TargetIndex < struck.Count
                ? struck[instance.TargetIndex]
                : struck.Count > 0 ? struck[0] : null;
            var targetRect = SlotFor(targetCombatant);

            var on = SpellPlaceNames.OnCaster(layer.Place) && !layer.Travels
                ? casterRect ?? targetRect
                : targetRect;

            // NOWHERE TO PUT IT -- a cast-level layer whose beat struck no
            // target, most often. Box/To/From are left at their zero default,
            // and instance.Placed = false is what tells Open() not to spend a
            // pooled renderer drawing a box that sits at the stage origin for
            // its whole lifetime.
            if (on == null)
            {
                instance.Placed = false;
                return;
            }

            // THE TARGET'S VISIBLE BODY, once, for everything below that lands
            // on it: the aim point, the air point a sky layer amasses at, and
            // the factor `fit: target` sizes by. A caster-side layer that never
            // reaches a target still resolves one when a target exists, which
            // costs a cached lookup and nothing else.
            StageBody? body = targetRect != null ? BodyOf(targetCombatant, targetRect, parent) : (StageBody?)null;

            float fit = layer.Fit == SpellFit.Target && body.HasValue ? TargetBody.FitFactor(body.Value) : 1f;
            instance.Fit = fit;

            var box = BoxForLayer(layer, fit);
            bool fromCaster = SpellPlaceNames.OnCaster(layer.Place) && !layer.Travels;
            bool centred = SpellPlaceNames.Centred(layer.Place);

            // WHERE IT IS AIMED. On the caster, the slot as it always was; on
            // the target -- a target placement, or any traveller's arrival --
            // the visible body: its middle when centred, its own ground line
            // under its middle when standing. A sky layer that does not
            // travel is aimed at the air point itself.
            Vector2 aim;
            if (fromCaster || !body.HasValue)
            {
                aim = AimPoint(parent, on, centred);
            }
            else if (layer.Place == SpellPlace.Sky && !layer.Travels)
            {
                aim = SkyPointFor(caster, casterRect, parent, body.Value);
            }
            else
            {
                aim = AimAtBody(body.Value, parent, targetRect, centred);
            }

            // A NON-TRAVELLING `caster` OR `caster-centre` PLACEMENT used to
            // mean "the slot's own origin" or "the slot's own middle" --
            // exactly what AimPoint just computed above. Both now mean the
            // caster's own cast point instead, when one is authored: the
            // model is that an authored castPoint IS where every cast leaves
            // that actor's body, not just the ones that happen to travel.
            // Found missing when prismatic_orb's caster-centre `charge` layer
            // kept forming at Odette's slot centre -- her spine, roughly --
            // while the travelling `core` layer right behind it (routed
            // through CasterCastPoint below regardless of `place`, since it
            // only checks Travels) left correctly from her book. One seam,
            // one rule, or a caster-centre effect and a travelling one on the
            // same cast disagree about where "the caster" is.
            //
            // CasterCastPoint falls back to that same AimPoint answer
            // verbatim for every actor that authors nothing, which is every
            // actor but the two this feature was written for -- so
            // `caster-centre` for an unauthored actor keeps meaning the
            // figure's own centre exactly as before.
            if (fromCaster && casterRect != null)
            {
                aim = CasterCastPoint(caster, casterRect, parent, aim);
            }

            float facing = instance.DrawFacing;
            instance.Box = new UiVec(box.x, box.y);

            // TURNED ONTO ITS FLIGHT. The launch is decided first, because the
            // angle is the line from it to the aim; then the sheet's impact
            // point -- a spear's tip, not the box's middle -- is put ON that
            // line at both ends, so the tip flies straight at what it hits and
            // the painted shaft trails along the same line behind it.
            if (layer.Travels && layer.Orient == SpellOrient.Path)
            {
                var from = LaunchPoint(layer, caster, casterRect, parent, body, casterX, aim.y);
                float degrees = SpellFlight.Turn(
                    SpellFlight.DegreesOf(new UiVec(from.x, from.y), new UiVec(aim.x, aim.y)),
                    layer.artDegrees, facing);

                var art = RenderedSize(layer.path, box);
                var tip = layer.HasImpactPoint
                    ? SpellFlight.ImpactOffset(layer.impactX, layer.impactY, new UiVec(art.x, art.y), facing, degrees)
                    : UiVec.Zero;

                instance.Degrees = degrees;
                instance.To = new UiVec(aim.x, aim.y) - tip;
                instance.From = new UiVec(from.x, from.y) - tip;
                return;
            }

            var to = BoxCentreForLayer(layer, performance, instance, aim, box, facing, standing: !centred);
            instance.To = new UiVec(to.x, to.y);

            // THE LAUNCH IS NOT IMPACT-CORRECTED, and the asymmetry is on
            // purpose. `to` is placed so the sheet's impact sits on the target;
            // the same offset at the other end would shift a DIFFERENT part of
            // the drawing -- the conjuring glyph -- away from the caster rather
            // than onto them.
            if (layer.Travels)
            {
                var launch = LaunchPoint(layer, caster, casterRect, parent, body, casterX, to.y);
                instance.From = new UiVec(launch.x, launch.y);
            }
            else
            {
                instance.From = instance.To;
            }
        }

        // WHERE A TRAVELLING LAYER LEAVES FROM: the air point for `sky`, the
        // caster's cast point for the caster words.
        //
        // THE CASTER'S LAUNCH falls back to (casterX, `height`) verbatim for an
        // actor that authors no cast point, which is what this line always
        // computed before the cast point existed -- a flat throw at the
        // arrival's own height. With no caster slot at all it leaves from the
        // aim's own column, which is a flight of zero length and draws the
        // sheet where it lands rather than nowhere.
        private Vector2 LaunchPoint(SpellLayer layer, CombatantState caster, RectTransform casterRect,
            Transform parent, StageBody? body, float casterX, float height)
        {
            if (layer.Place == SpellPlace.Sky && body.HasValue)
            {
                return SkyPointFor(caster, casterRect, parent, body.Value);
            }

            return casterRect != null
                ? CasterCastPoint(caster, casterRect, parent, new Vector2(casterX, height))
                : new Vector2(casterX, height);
        }

        // THE AIR POINT for this caster and this target (SpellFlight.SkyPoint):
        // halfway from where the cast leaves the caster to the target's middle,
        // above the taller of the two. The caster's point is its cast point
        // when it authors one and its own body's middle otherwise; with no
        // caster slot the target stands in for it, which puts the point
        // straight above the target.
        private Vector2 SkyPointFor(CombatantState caster, RectTransform casterRect, Transform parent,
            StageBody target)
        {
            var casterBody = casterRect != null ? BodyOf(caster, casterRect, parent) : target;
            var middle = casterBody.Centre;
            var leaves = casterRect != null
                ? CasterCastPoint(caster, casterRect, parent, new Vector2(middle.X, middle.Y))
                : new Vector2(middle.X, middle.Y);

            var point = SpellFlight.SkyPoint(new UiVec(leaves.x, leaves.y), casterBody.Top, target);
            return new Vector2(point.X, point.Y);
        }

        // THE POINT ON A STRUCK BODY an effect is aimed at: the middle of what
        // is drawn when centred; standing, the same column on the slot's own
        // ground line -- which is authored (StanceManifest groundLine) and
        // outranks any measurement of where the lowest pixel happens to be.
        private static Vector2 AimAtBody(StageBody body, Transform parent, RectTransform slot, bool centred)
        {
            var middle = body.Centre;
            if (centred) return new Vector2(middle.X, middle.Y);

            float ground = parent.InverseTransformPoint(slot.TransformPoint(Vector3.zero)).y;
            return new Vector2(middle.X, ground);
        }

        // WHERE A CAST ACTUALLY LEAVES THIS ACTOR'S BODY, on stage, right now
        // -- the seam every caster-anchored placement above goes through
        // instead of re-deriving the slot's own origin.
        //
        // TWO HALVES, ONE ENGINE-FREE. `slotOrigin` asks the SAME
        // TransformPoint/InverseTransformPoint dance every other point in
        // this file goes through for "where is this slot, right now" -- it
        // already carries the slot's depth scale (AnchorOne writes
        // FightStageAnchors.SlotScale into localScale) and its current hover
        // offset (SetHover writes straight into the slot's anchoredPosition
        // every frame, so a flying actor's cast point rides the bob for
        // free) without this function having an opinion about either.
        // CastPointPlacement.OnStage is the one piece that IS a formula --
        // an authored (dx, dy) plus the slot's own scale and mirror -- and it
        // lives in Domain, engine-free, so it is what CastPointPlacementTests
        // pins with literal numbers instead of only a running scene.
        //
        // MIRRORED BEFORE THE SCALE, not after -- dx is authored relative to
        // the actor's own drawn facing, and the slot itself is never
        // mirrored (only the sprite Image inside it is, in
        // RefreshCombatantSprite). Reading FacingOf/side the same way that
        // call site does is what keeps this from disagreeing with which way
        // the figure is actually drawn.
        //
        // UNAUTHORED ACTORS GET `fallback` VERBATIM. The two call sites above
        // disagreed about what "no opinion" meant before this seam existed --
        // a travelling layer's launch took the target's own height (to.y), a
        // plain `caster` placement took the slot's ground line -- so that
        // choice cannot live inside this function without changing one of
        // the two answers. It stays the caller's, which is what makes
        // "unauthored actors get exactly today's answer" true by
        // construction rather than by re-deriving today's formula twice.
        private Vector2 CasterCastPoint(CombatantState caster, RectTransform casterRect, Transform parent,
            Vector2 fallback)
        {
            if (caster == null || casterRect == null) return fallback;

            var authored = StanceManifestLoader.Manifest.CastPointFor(SpriteFolderFor(caster));
            if (!authored.HasValue) return fallback;

            var side = caster.IsPlayerSide ? StageSide.Left : StageSide.Right;
            float mirror = StageFacing.MirrorScaleX(FacingOf(caster), side);

            var slotOrigin = parent.InverseTransformPoint(casterRect.TransformPoint(Vector3.zero));
            var stage = CastPointPlacement.OnStage(authored.Value, mirror, casterRect.localScale.x,
                new UiVec(slotOrigin.x, slotOrigin.y));
            return new Vector2(stage.X, stage.Y);
        }

        // ONE FAULT UNDER THE WHOLE FORMATION, sized to the enemies actually
        // standing in it rather than to a slot or to a constant.
        //
        // WHY THE SPAN IS MEASURED AND NOT AUTHORED. A formation is one, two or
        // three figures at different depths, and a fault authored wide enough
        // for three reaches half the stage past a lone rat -- the effect then
        // describes ground nothing is standing on, which is the same class of
        // wrongness as an impact landing off the target.
        //
        // MEASURED THROUGH EACH BODY'S OWN EDGES, not from its centre plus a
        // margin. A back-row slot is depth-scaled, so its half-width in the
        // renderer's coordinates is not a front-row slot's -- one authored
        // margin would over-reach on one rank and under-reach on the other.
        //
        // MEASURING IS THIS FUNCTION'S WHOLE JOB; what to do with the numbers
        // belongs to the layer's own `align` word and to one of the two
        // branches below. The split is here rather than inside one function
        // with an if in the middle because the level box and the spanning box
        // share their inputs and nothing else -- a different centre, a
        // different width, a different correction axis, a rotation one of them
        // does not have.
        private void PlaceOnFormation(SpellLayerInstance instance, Transform parent,
            List<CombatantState> struck)
        {
            // THE PRE-DAMAGE SNAPSHOT, which is why the beat carries one: an
            // enemy killed by this very cast still stood in the fault when it
            // opened, and reading the living list would shrink the fault away
            // from a corpse that is still on screen.
            var stood = new List<FootPrint>();
            foreach (var one in struck)
            {
                var slot = SlotFor(one);
                if (slot == null) continue;

                // THE VISIBLE BODY'S EDGES, not the slot's: a slot is the
                // sprite's canvas, and the rat's runs 225 canvas pixels wider
                // than the rat -- a fault measured off it overshot every lone
                // rat by that padding. Ground stays the slot's own origin,
                // which is the authored ground line.
                var body = BodyOf(one, slot, parent);
                float centre = body.Centre.X;
                float slotLeft = body.Left;
                float slotRight = body.Right;

                float ground = parent.InverseTransformPoint(slot.TransformPoint(Vector3.zero)).y;

                stood.Add(new FootPrint(centre, ground, slotLeft, slotRight));
            }

            // Nobody with a slot: an off-stage or synthetic target. A fault of
            // no width would be a zero-sized graphic, so draw none.
            if (stood.Count == 0) return;

            if (instance.AlignKind == SpellAlign.Span)
            {
                PlaceAlongRank(instance, stood);
                return;
            }

            PlaceLevelAcrossRank(instance, stood);
        }

        // ONE STRUCK BODY'S FOOTING: where it stands, what it is standing on,
        // and how far its slot reaches either side of it. A struct rather than
        // four parallel lists because the span below has to pick ENDS out of
        // these, and picking an end from one list and its width from another is
        // the bug this shape cannot have.
        private readonly struct FootPrint
        {
            internal readonly float Centre;
            internal readonly float Ground;
            internal readonly float Left;
            internal readonly float Right;

            internal FootPrint(float centre, float ground, float left, float right)
            {
                Centre = centre;
                Ground = ground;
                Left = left;
                Right = right;
            }
        }

        // TODAY'S BOX, KEPT: as wide as the rank's horizontal extent, sitting
        // on its average ground line, axis-aligned.
        //
        // THE AVERAGE GROUND LINE across the ranks it opens under, not the
        // front one's. The racks stand at different depths and a level drawing
        // is one flat thing: pinned to the front rank it would float above the
        // back one, and pinned to the back it would cut through the front
        // one's feet. Halfway is the only choice that is wrong by the same
        // small amount at both ends -- which is exactly the compromise
        // `align: span` below exists to stop having to make.
        private void PlaceLevelAcrossRank(SpellLayerInstance instance, List<FootPrint> stood)
        {
            float left = float.MaxValue;
            float right = float.MinValue;
            float ground = 0f;

            foreach (var foot in stood)
            {
                left = Mathf.Min(left, foot.Left);
                right = Mathf.Max(right, foot.Right);
                ground += foot.Ground;
            }

            if (right <= left) return;
            ground /= stood.Count;

            var box = GroundBoxFor(instance.Layer, right - left);
            if (box.x <= 0f || box.y <= 0f) return;

            // The sheet's own ground line onto the formation's, against the
            // RENDERED size rather than the box, because a box whose aspect the
            // author overrode letterboxes inside itself.
            var art = RenderedSize(instance.Layer.path, box);
            float sheetGround = instance.Layer.HasImpactY ? instance.Layer.impactY : 0.5f;

            instance.Box = new UiVec(box.x, box.y);
            instance.Degrees = 0f;
            instance.To = new UiVec((left + right) * 0.5f, ground + (0.5f - sheetGround) * art.y);
            instance.From = instance.To;
        }

        // THE BOX LAID ALONG THE RANK, which is what a crack in the floor
        // actually is.
        //
        // THE RANK IS A DIAGONAL AND ALWAYS HAS BEEN. FightStageAnchors runs
        // the enemy line (300, -218) -> (660, -125) and the party's
        // (320, -218) -> (810, -64), so a drawing spanning three bodies covers
        // 93 units of rise it was drawing none of. The level branch above puts
        // the whole sheet on the MEAN ground line, so the fault opened half a
        // rank's rise below the back body's feet and the same distance above
        // the front one's. Owner, 2026-09-19: the line "should follow the mobs,
        // who stand in a diagonal line".
        //
        // LEFT TO RIGHT, NOT STRUCK ORDER. FormationSpan's own header gives the
        // reason -- an angle taken from the struck walk would be 180 degrees
        // out for a rank reached the other way round and would render the sheet
        // upside down -- and this is where that ordering is actually imposed.
        //
        // THE PADS ARE THE TWO END SLOTS' OWN HALF-WIDTHS, each measured
        // through that slot's own edges. A back-rank slot is depth-scaled, so
        // one shared margin would over-reach at one end and under-reach at the
        // other; that is the same reason the level branch reads edges rather
        // than centres, carried through the rotation.
        private void PlaceAlongRank(SpellLayerInstance instance, List<FootPrint> stood)
        {
            var first = stood[0];
            var last = stood[0];

            foreach (var foot in stood)
            {
                if (foot.Centre < first.Centre) first = foot;
                if (foot.Centre > last.Centre) last = foot;
            }

            var span = FormationSpan.Between(
                new UiVec(first.Centre, first.Ground),
                new UiVec(last.Centre, last.Ground),
                first.Centre - first.Left,
                last.Right - last.Centre);

            var box = GroundBoxFor(instance.Layer, span.Length);
            if (box.x <= 0f || box.y <= 0f) return;

            // THE GROUND-LINE CORRECTION MOVES ALONG THE BOX'S OWN UP, not
            // along world +Y. The box is rotated, so its vertical axis is
            // rotated with it: correcting on +Y would slide the art off the
            // line it was just fitted to, by the correction times the tangent
            // of the tilt. Small at 14 degrees and not zero, and it grows with
            // every future formation that leans harder.
            var art = RenderedSize(instance.Layer.path, box);
            float sheetGround = instance.Layer.HasImpactY ? instance.Layer.impactY : 0.5f;
            var lift = span.Up * ((0.5f - sheetGround) * art.y);

            instance.Box = new UiVec(box.x, box.y);
            instance.Degrees = span.Degrees;
            instance.To = span.Centre + lift;
            instance.From = instance.To;
        }

        // Everyone this beat's effect is drawn on, primary first. The same walk
        // PlaySpellVfx makes over the pool, extracted so the ground layer and
        // the per-target layer cannot disagree about who was struck.
        private static IEnumerable<CombatantState> StruckBy(CombatBeat beat)
        {
            if (beat.Target != null) yield return beat.Target;
            if (beat.SplashTargets == null) yield break;

            foreach (var also in beat.SplashTargets)
            {
                if (also == null || ReferenceEquals(also, beat.Target)) continue;
                yield return also;
            }
        }

        // The box the fault is fitted into: as wide as the formation, and as
        // tall as its own shape says.
        //
        // NOT SQUARE, which is the one thing that separates it from BoxFor. A
        // square box for a sheet drawn as a wide fault is not merely inelegant
        // -- preserveAspect would letterbox the drawing into a strip a third of
        // the width it was asked to span, so the fault would stop short of the
        // enemies at both ends of the rack it is supposed to run under.
        //
        // ZERO groundAspect means "the sheet's own", which is the right default
        // and not a fallback: a frame's width over its height IS the shape the
        // artist drew, and authoring a second copy of it would be a number with
        // two homes. Authored only where the box must differ from the frame.
        private Vector2 GroundBoxFor(SpellLayer layer, float span)
        {
            float aspect = layer.aspect;

            if (aspect <= 0f)
            {
                var frames = PrimaryPlayer?.Frames(layer.path);
                var frame = frames != null && frames.Length > 0 ? frames[0] : null;
                aspect = frame != null && frame.rect.height > 0f
                    ? frame.rect.width / frame.rect.height
                    : 1f;
            }

            if (aspect <= 0f) aspect = 1f;

            return new Vector2(span, span / aspect);
        }

        // ---- the house's own contact language, for a swing that authored none --

        // THE ATTACK GRAPHIC AND THE IMPACT BURST for a single-drawing melee
        // blow. Fired at the impact instant by FightBeatPlayer, which owns the
        // decision of WHICH beats get this (see its WantsContactFx); all this
        // owns is where the two effects go.
        //
        // MEMBERS 0 AND 1 OF THE SPELL POOL, borrowed rather than pooled
        // separately. FightBeatPlayer only calls this for a beat with no
        // authored VFX path, and PlaySpellVfx returns on its first line for
        // exactly that beat -- so the whole pool is idle whenever this runs,
        // and a second pool would be three more Images on the canvas for a
        // case that cannot overlap the first.
        //
        // THE ARC IS SKIPPED FOR A CHARGE. The slash arc reads a lean-and-cut
        // (docs/archive/STATIC_COMBAT_ART_DEEP_DIVE.md's "Attack families" draws that
        // language for Slash, not for a committed rush), and a Charge is a
        // bump rather than a cut -- the same table's "Blunt" row calls for a
        // burst and a longer hit-stop instead of an arc. This is the burst
        // half of that: the simplest honest version of the distinction, and
        // it costs nothing the pool did not already have. The hit-stop half
        // is not implemented -- HitStopFor is keyed on the blow's weight for
        // every approach alike, and giving Charge a longer one is a separate,
        // undecided change to that shared curve.
        private void PlayContactFx(CombatBeat beat)
        {
            if (beat?.Target == null || performancePlayer == null) return;

            var parent = PrimaryPlayer != null ? PrimaryPlayer.transform.parent : null;
            if (parent == null) return;

            var targetRect = SlotFor(beat.Target);
            if (targetRect == null) return;

            // THROUGH THE SAME ALLOCATOR AS EVERY OTHER CAST, which is a change
            // and a necessary one. This used to take pool members 0 and 1 by
            // index, safe only because the beat player calls it exclusively for
            // beats with no authored VFX -- and once a tail is allowed to
            // outlive the beat that cast it, member 0 can still be holding a
            // spell from the previous round when the next melee blow lands.
            // Borrowing it then would steal a live renderer.
            //
            // A PRESENTATION RATHER THAN TWO DIRECT CALLS, because the arc and
            // the burst ARE two sprite layers on the target: one mirrored by
            // the attacker's side, one symmetrical. Saying so in the same
            // vocabulary every spell uses is what keeps this from being a
            // second orchestration path for the house's own contact language.
            var contact = ContactPresentation(beat);
            var performance = SpellPerformance.Resolve(contact, 1, FrameCountOf);

            // THE CONTENT CENTRE, not the ground line. A slash lands on the
            // body; the standing/letterbox correction is for an effect drawn
            // erupting from a floor, and neither of these sheets is. Both are
            // generated with their impact at the exact centre of the frame
            // (tools/make_contact_fx.py), so the aim point IS the box centre
            // and there is no offset to cancel.
            var aim = AimPoint(parent, targetRect, centred: true);
            var box = ContactBoxFor(targetRect, AnimatorFor(beat.Target));

            // MIRRORED FOR A MONSTER: the arc is drawn sweeping left to right,
            // so a blow coming back across the stage has to run the other way
            // or the sweep trails the strike instead of leading it. Read off
            // which SIDE the attacker is on rather than off slot positions, so
            // a back-row monster hit by a status tick cannot flip it.
            float facing = beat.Actor != null && !beat.Actor.IsPlayerSide ? -1f : 1f;

            foreach (var instance in performance.Instances)
            {
                instance.Facing = facing;
                instance.Box = new UiVec(box.x, box.y);
                instance.To = new UiVec(aim.x, aim.y);
                instance.From = instance.To;
            }

            performancePlayer.Begin(performance);

            SoundController.PlayClip(ContactCues.ThudClipPath);
        }

        // WHAT THE FORM THE ACTOR IS WEARING ADDS TO THE BLOW, on every body
        // the blow reached.
        //
        // THROUGH PlaceCast, THE SAME PLACEMENT EVERY AUTHORED CAST GETS,
        // rather than through PlayContactFx's centred-box shortcut above: this
        // one IS authored content, so it can state its own `place`, its own
        // size and its own impact point, and it fans out over StruckBy exactly
        // as a multi-target spell does -- which is how the Black Ram's splash
        // onto the neighbours gets punctuated with the same burst as the blow
        // that caused it.
        //
        // BEGUN AT THE IMPACT INSTANT, so its layers author `at: release` and
        // not `at: hit`: this performance's own clock starts on the frame the
        // blow lands. `hit` is the word for a layer inside a cast that opened
        // at the top of the beat and still has to wait for its cue.
        //
        // NO SOUND OF ITS OWN HERE. A presentation carries sfxPath and the
        // module plays it on the same clock as its layers, so an authored clip
        // is already handled and a second PlayClip would double it.
        private void PlayFormHitFx(CombatBeat beat)
        {
            if (performancePlayer == null || beat?.FormVfx == null) return;

            int struck = 0;
            foreach (var _ in StruckBy(beat)) struck++;
            if (struck == 0) return;

            var performance = SpellPerformance.Resolve(beat.FormVfx, struck, FrameCountOf);
            if (performance == null || performance.Instances.Count == 0) return;

            PlaceCast(beat, performance);
            performancePlayer.Begin(performance);
        }

        // THE HOUSE'S DEFAULT CONTACT LANGUAGE, said in layers. Built in code
        // rather than authored, because it is what a blow that authored NOTHING
        // gets -- there is no content row for it to live on, and inventing one
        // would put a house rule in a file a designer edits.
        //
        // THE ARC IS SKIPPED FOR A CHARGE. The slash arc reads a lean-and-cut
        // and a Charge is a bump rather than a cut; the same table's "Blunt"
        // row calls for a burst and a longer hit-stop instead of an arc. This
        // is the burst half of that distinction.
        //
        // BUILT ONCE, NOT PER BEAT. Every plain melee blow in the game calls
        // this, and until this fix it allocated a List<SpellLayer> plus one or
        // two SpellLayer objects fresh every single time even though there are
        // only ever two possible outputs -- neither of which varies with
        // anything about `beat` beyond the Charge/not-Charge branch. Caching
        // is safe because the mutable half of a cast (Box/To/From/Placed) was
        // always on SpellLayerInstance, never on SpellLayer -- SpellPerformance
        // .Resolve's fan-out already hands multiple concurrent casts a
        // REFERENCE to one shared SpellLayer for any authored skill (a
        // ResolvedSkill's Vfx is built once and cast from for the rest of the
        // run); a contact beat sharing its two templates the same way is the
        // same contract, not a new one. SpellPerformance.Resolve still builds
        // a fresh SpellLayerInstance list per beat (each cast needs its own
        // Box/To/From/pool-member state, so that part cannot be shared across
        // concurrent casts) -- see SpellAllocationTests
        // .APlainMeleeBlowsContactFxCostsNoMoreThanIdle for what that leaves.
        private static readonly SpellPresentation _contactWithArc = BuildContactPresentation(withArc: true);
        private static readonly SpellPresentation _contactBurstOnly = BuildContactPresentation(withArc: false);

        private static SpellPresentation ContactPresentation(CombatBeat beat) =>
            beat.Approach == StageApproach.Charge ? _contactBurstOnly : _contactWithArc;

        private static SpellPresentation BuildContactPresentation(bool withArc)
        {
            var layers = new List<SpellLayer>(2);

            if (withArc)
            {
                layers.Add(new SpellLayer
                {
                    id = "arc",
                    render = "sprite",
                    place = "target-centre",
                    at = "release",
                    path = ContactCues.SlashArcPath,
                    seconds = ContactCues.SlashSeconds,
                    until = "once",
                    facing = "auto",
                    sort = "effects",
                });
            }

            // The burst is radially symmetrical, so it has no direction to
            // mirror -- which is what `none` says, rather than being a defensive
            // SetFacing(1f) undoing whatever the last user of a pool member
            // left behind.
            layers.Add(new SpellLayer
            {
                id = "burst",
                render = "sprite",
                place = "target-centre",
                at = "release",
                path = ContactCues.ImpactBurstPath,
                seconds = ContactCues.BurstSeconds,
                until = "once",
                facing = "none",
                sort = "effects",
            });

            return new SpellPresentation
            {
                layerFormat = SpellLayerRules.CurrentLayerFormat,
                layers = layers.ToArray(),
            };
        }

        // The effect box, shrunk to the depth the target is standing at.
        //
        // A back-row figure is drawn smaller (FightStageAnchors.SlotScale), so
        // an effect at a fixed canvas size would be correct on the front rank
        // and swallow the one behind it. Taken from the animator's BaseScale
        // rather than the live localScale because the target is being punched
        // on this exact frame and its localScale is mid-squash -- the same
        // distinction AnchorOne draws when it compares marks.
        //
        // The spell path deliberately does NOT do this: a spell authors its
        // own size per skill and a caster tunes it against what they see. This
        // one has no authored size to tune, so the depth has to come from
        // somewhere.
        // HANDED THE ANIMATOR rather than GetComponent'ing it off the slot --
        // the caller already has the combatant and asks AnimatorFor the same
        // way SlotFor is asked for the rect.
        private static Vector2 ContactBoxFor(RectTransform slot, StageActorAnimator animator)
        {
            float depth = animator != null
                ? Mathf.Abs(animator.BaseScale.x)
                : Mathf.Abs(slot.localScale.x);

            // A zero scale is a slot that has not been anchored yet; drawing
            // the effect at full size beats drawing it at nothing.
            if (depth <= 0.01f) depth = 1f;

            float size = ContactCues.BoxSize * depth;
            return new Vector2(size, size);
        }

        // THE POINT ON A COMBATANT AN EFFECT IS AIMED AT, in the effect pool's
        // own coordinates.
        //
        // The SLOT, not the sprite Image inside it. The Image is the art's raw
        // canvas and its bottom edge is wherever the sheet happened to be cut;
        // the slot's bottom edge is the stage's ground line, which the stage
        // visuals stand every frame's feet on. Aiming at the canvas put a
        // strike below the feet by whatever padding that sheet carried -- and a
        // MOVING amount, since the offset is per frame.
        //
        // Converted through the shared parent rather than read off
        // anchoredPosition, because the slot is nested inside the stage and
        // scaled by depth: its own coordinates are not where it appears.
        //
        // NOW ONLY THE CASTER'S SIDE. `centred` takes the slot's midpoint,
        // and the slot is the sprite's raw CANVAS rather than the figure drawn
        // on it -- 62 empty rows over the golem's head -- which is why every
        // target-side aim goes through BodyOf/AimAtBody instead. A caster
        // mostly authors a cast point, which overrides this anyway.
        private static Vector2 AimPoint(Transform parent, RectTransform slot, bool centred)
        {
            var rect = slot.rect;
            float x = parent.InverseTransformPoint(slot.TransformPoint(Vector3.zero)).x;
            float y = parent.InverseTransformPoint(
                slot.TransformPoint(new Vector3(0f, centred ? rect.center.y : rect.yMin, 0f))).y;

            return new Vector2(x, y);
        }

        // WHERE THE BOX GOES so that the SHEET'S OWN POINT OF CONTACT lands on
        // the point the spell is aimed at.
        //
        // Two rules, and which one applies is the sheet's choice rather than
        // this file's.
        //
        // AUTHORED (SpellPresentation.impactX/impactY): the offset from the
        // box's centre to the drawn impact is arithmetic, and the box is placed
        // to cancel it. Correct on BOTH axes, which the measured rule never
        // was: mud_burst's impact sits two thirds of the way across its frame,
        // so centring the box put the detonation a sixth of a box to one side
        // of everything it hit.
        //
        // MEASURED (the fallback, for any sheet that does not state a point):
        // the box's bottom edge on the ground line, less the transparent margin
        // the sheet carries below its landed content. Kept verbatim rather than
        // replaced -- it is right for an effect drawn standing on a floor, it
        // is what golem_boulder is tuned against, and a sheet that says nothing
        // must not move.
        //
        // `facing` is +1 as drawn and -1 mirrored, and only the horizontal
        // correction cares: a mirrored sheet's impact is the same distance from
        // the other edge, so the box has to sit on the other side of the target.
        // Getting this wrong is invisible on Shawn (who always casts rightward)
        // and doubles the error on every enemy.
        private Vector2 BoxCentreForLayer(SpellLayer layer, SpellPerformance performance,
            SpellLayerInstance instance, Vector2 aim, Vector2 box, float facing, bool standing)
        {
            if (layer.HasImpactPoint)
            {
                var art = RenderedSize(layer.path, box);
                return new Vector2(
                    aim.x + (0.5f - layer.impactX) * art.x * (facing < 0f ? -1f : 1f),
                    aim.y + (0.5f - layer.impactY) * art.y);
            }

            if (!standing) return aim;

            return new Vector2(aim.x,
                aim.y + box.y * 0.5f - VfxDeadSpaceBelow(layer.path, box, LandedFrame(layer, performance, instance)));
        }

        // WHICH FRAME OF THIS LAYER THE BLOW LANDS ON, 1-based, for the scan
        // below that measures the LANDED effect's own ground line.
        //
        // Derived from the cast's hit cue rather than authored, and that is the
        // point rather than a convenience: a pre-layer block's cue IS
        // seconds * impactFrame / frameCount, so dividing it back by the
        // per-frame time returns exactly the impactFrame it was authored with
        // and no shipped sheet moves. A layered spell gets the same question
        // answered from the one number it does author.
        private int LandedFrame(SpellLayer layer, SpellPerformance performance, SpellLayerInstance instance)
        {
            int frames = FrameCountOf(layer.path);
            float length = instance.EndSeconds - instance.StartSeconds;
            if (frames <= 0 || length <= 0f) return 0;

            float perFrame = length / frames;
            int landed = Mathf.RoundToInt((performance.HitCueSeconds - instance.StartSeconds) / perFrame);
            return Mathf.Clamp(landed, 0, frames);
        }

        // How big the art actually draws once preserveAspect has fitted it into
        // the box. The box is square and not every sheet is, so a fraction of
        // the FRAME is only a distance on screen after this.
        //
        // Falls back to the box itself for a sheet with no readable frames --
        // the same answer a square sheet gives, and the one that degrades to
        // "no correction" rather than to a division by zero.
        private Vector2 RenderedSize(string vfxPath, Vector2 box)
        {
            var frames = PrimaryPlayer?.Frames(vfxPath);
            if (frames == null || frames.Length == 0 || frames[0] == null) return box;

            float frameWidth = frames[0].rect.width;
            float frameHeight = frames[0].rect.height;
            if (frameWidth <= 0f || frameHeight <= 0f || box.x <= 0f || box.y <= 0f) return box;

            float frameAspect = frameWidth / frameHeight;
            float boxAspect = box.x / box.y;

            return frameAspect <= boxAspect
                ? new Vector2(box.y * frameAspect, box.y)
                : new Vector2(box.x, box.x / frameAspect);
        }

        // How much empty box sits BELOW the visible art once preserveAspect has
        // fitted this sheet into the box. Two corrections, both earned.
        //
        // THE BOX IS A PARAMETER, not the old constant: the letterbox is a
        // function of both the art's aspect and the box's, so a spell that
        // authored its own size and kept reading 380 here would be corrected by
        // the wrong margin and land off the ground line.
        //
        // ONE: THE LETTERBOX. Anchoring the BOX's bottom edge to the target's
        // ground line is not enough on its own, because the box is square and
        // not every sheet is. preserveAspect fits the art INSIDE the box, so a
        // wider-than-square sheet gets letterboxed and the visible art no longer
        // reaches the box's own bottom edge -- it floats, centred, with dead
        // space beneath it. Shawn's two spells are 512x512, fill the square
        // exactly, and hid this completely. The golem's Boulder Slam is 598x433,
        // so in a 380x380 box it renders 380x275 with ~52 units of empty box
        // below -- which is why its ground spike erupted around the target's
        // midriff instead of under their feet.
        //
        // Measured off the sheet's own dimensions rather than assumed, because
        // the assumption is exactly what broke: this comment used to say the
        // frames "are a fixed 512x512 square", true of the only two spells that
        // existed when it was written and false the moment an enemy VFX was cut
        // at a different aspect.
        //
        // TWO: THE ART'S OWN BOTTOM MARGIN, at full extent. An earlier version
        // corrected only the letterbox, reasoning that a frame's bottom margin
        // is animation and compensating per frame would drag the effect down the
        // screen as it faded. The first half is right and the second half is the
        // wrong conclusion from it. The margin IS animation -- the lightning
        // bolt strikes to its canvas floor and its afterglow then retracts
        // upward, clearing 0, 0, 35, 66, 72, 60 across six frames -- so what the
        // effect needs is not a per-frame correction but a single one taken at
        // the moment it is fully extended. That is the MINIMUM margin across the
        // sheet, and it is the effect's own ground line: correcting by it lands
        // the strike on the feet and still lets the fade retreat upward.
        private float VfxDeadSpaceBelow(string vfxPath, Vector2 box, int impactFrame)
        {
            var frames = PrimaryPlayer?.Frames(vfxPath);
            if (frames == null || frames.Length == 0 || frames[0] == null) return 0f;
            if (box.y <= 0f) return 0f;

            // Height-constrained (tall or square) art already reaches the box's
            // bottom edge; only wider-than-the-box art letterboxes. How much is
            // RenderedSize's answer rather than a second copy of the same fit --
            // two copies of a preserveAspect calculation is exactly the shape of
            // the bug this method's own header records, one layer up.
            float renderedHeight = RenderedSize(vfxPath, box).y;
            float letterboxBelow = (box.y - renderedHeight) * 0.5f;

            return letterboxBelow + VfxContentPaddingFraction(vfxPath, frames, impactFrame) * renderedHeight;
        }

        // The smallest transparent bottom margin from the IMPACT frame onward,
        // as a fraction of frame height -- the LANDED effect at its fullest
        // extent.
        //
        // NOT the whole sheet, though it used to be, and that was the bug:
        // lightning_bolt's pre-impact travel frames (a thin descending bolt
        // tip) happen to touch the canvas floor harder than the actual impact
        // burst (a wide starburst that never reaches as low) ever does, so
        // scanning from frame 0 measured the wind-up's ground line and applied
        // it to the landed effect -- the strike rendered floating above
        // whatever it hit instead of on it. Starting the scan one frame before
        // impactFrame (rather than exactly at it) keeps a frame of lead-in
        // margin without reintroducing the travel frames that caused this.
        //
        // Public rather than internal: InternalsVisibleTo names the EDITOR
        // assembly only, and the sheet-margin maths is the part of this file
        // most worth pinning -- every one of its rules came from a bug.
        // impactFrame defaults to 0, which scans the whole sheet -- the
        // original behaviour, still correct for content whose margin is
        // consistent across every frame (Shawn's two spells, this file's own
        // pinned test fixtures).
        public static float VfxContentPaddingFraction(string vfxPath, Sprite[] frames, int impactFrame = 0)
        {
            if (vfxPath != null && VfxPaddingCache.TryGetValue(vfxPath, out var cached)) return cached;

            int start = Mathf.Clamp(impactFrame - 1, 0, frames.Length - 1);
            float smallest = float.MaxValue;
            for (int i = start; i < frames.Length; i++)
            {
                float padding = BottomPaddingFraction(frames[i]);
                if (padding >= 0f && padding < smallest) smallest = padding;
            }

            // No readable frame, or a sheet that is entirely blank: fall back to
            // no correction rather than throwing. "Uncorrected" is exactly the
            // old behaviour rather than something new and worse.
            float result = smallest == float.MaxValue ? 0f : smallest;
            if (vfxPath != null) VfxPaddingCache[vfxPath] = result;
            return result;
        }

        // Fraction of the frame's height that is empty below its lowest opaque
        // row.
        //
        // NEGATIVE for a frame that cannot be measured or has no opaque pixel at
        // all. golem_boulder's f0 is a deliberately blank wind-up frame, and
        // letting it report 0 would claim the effect reaches the floor before it
        // has appeared -- which is worse than not correcting, because it is
        // confidently wrong.
        public static float BottomPaddingFraction(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null || !sprite.texture.isReadable) return -1f;

            var r = sprite.rect;
            int w = Mathf.RoundToInt(r.width);
            int h = Mathf.RoundToInt(r.height);
            if (w <= 0 || h <= 0) return -1f;

            var pixels = sprite.texture.GetPixels(Mathf.RoundToInt(r.x), Mathf.RoundToInt(r.y), w, h);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    if (pixels[y * w + x].a > 0.02f) return y / (float)h;
                }
            }

            return -1f;
        }
    }
}

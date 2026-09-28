using System;
using System.Collections.Generic;
using System.Linq;

namespace PrincesPalace.Domain.Content
{
    // WHAT A LAYERED PRESENTATION MAY SAY, checked at content build and never
    // at run time.
    //
    // Unlike SpellPresentation.anchor, an unknown word here does NOT fall back.
    // A graceful default is right for a placement -- a misplaced effect is
    // still an effect -- and wrong for `render`, where there is no safe answer
    // to "what draws". So every rule below refuses the build, which is
    // docs/CODE_STANDARDS.md section 5's "degrade, but never quietly lie"
    // applied at the layer that has an author to tell.
    //
    // EVERY PROBLEM IN ONE PASS, never the first only, matching every other
    // resolver in this folder: a hand-edited file gets fixed once rather than
    // once per typo. SkillEntryResolver.TryResolveOne carries a single `out
    // string error`, so the call site joins this list -- the one-error-per-
    // ENTRY shape is TryResolveAll's and is not changed here.
    //
    // WHAT THESE RULES DO NOT POLICE: the output of SpellPresentation.ToLayers.
    // The adapter runs at run time inside Begin, from a block the resolver has
    // already validated, so there is no author to report to and no asset to
    // refuse. Its correctness is pinned by the existing regression suite
    // instead, which is the right instrument -- an adapter emitting an illegal
    // layer shows up as a retimed spell, not as a message nobody reads.
    public static class SpellLayerRules
    {
        // THE LONGEST A LAYER'S ENDING MAY TAKE, and it is bracketed rather
        // than picked. It must be LONGER than FightBeatPlayer.BeatHoldSeconds,
        // because a tail outliving the beat that cast it is the whole point of
        // the no-flush contract; and SHORTER than BeatHoldSeconds +
        // BeatGapSeconds, because a fade still running when the beat after the
        // next one opens has stopped being attributable to a cast by eye.
        //
        // Those two constants are Core's and Domain cannot see them, so the
        // bracket is asserted from PlayMode rather than restated here as
        // arithmetic -- see SpellLayerFadeBoundTests, which fails if either
        // beat constant moves past this number.
        public const float MaxFadeSeconds = 0.5f;

        // THE LARGEST OVERSHOOT A LAYER MAY OPEN AT, as a fraction over its
        // fitted box. 1.0 -- double size -- rather than something tighter,
        // because a plume punching hard is a legitimate look and the number
        // only has to rule out the case where `punch` was typed as a
        // percentage (20 instead of 0.2), which is the mistake this catches.
        public const float MaxPunch = 1f;

        // The version of the layer vocabulary this code reads. A block that
        // authors layers must state it; see SpellPresentation.layerFormat for
        // the migration policy the number exists to make arguable.
        public const int CurrentLayerFormat = 1;

        // THE FORM EVERY RESOLVER CALLS. TryResolveOne carries a single
        // `out string error`, so the collected list is joined here rather than
        // at each call site -- one place decides what a multi-problem block
        // reads as, and the one-error-per-ENTRY shape stays TryResolveAll's.
        public static bool TryCheck(string label, SpellPresentation vfx, out string error)
        {
            var problems = Check(label, vfx);
            error = problems.Count == 0 ? null : string.Join("; ", problems);
            return problems.Count == 0;
        }

        // `label` names the entry the way SkillEntryResolver already builds it
        // ("skill 'cinderfault'", "... element #2"), so an author reads the row
        // rather than an index into a list they cannot see.
        public static List<string> Check(string label, SpellPresentation vfx)
        {
            var problems = new List<string>();
            if (vfx == null) return problems;

            var layers = vfx.layers ?? Array.Empty<SpellLayer>();

            CheckPresentation(label, vfx, layers, problems);
            if (layers.Length == 0) return problems;

            for (int i = 0; i < layers.Length; i++)
            {
                CheckOneLayer(label, layers[i], i, problems);
            }

            CheckAcrossLayers(label, layers, problems);
            return problems;
        }

        // ---- the block as a whole ------------------------------------------------

        private static void CheckPresentation(string label, SpellPresentation vfx, SpellLayer[] layers,
            List<string> problems)
        {
            if (layers.Length > 0 && vfx.layerFormat < CurrentLayerFormat)
            {
                problems.Add($"{label}: vfx authors layers but no layerFormat. " +
                             $"Add \"layerFormat\": {CurrentLayerFormat}.");
            }

            // TWO ORCHESTRATION PATHS IN ONE BLOCK is the thing this whole
            // design exists to remove, so a block that asks for both is
            // refused rather than resolved by precedence.
            if (layers.Length > 0 && !string.IsNullOrWhiteSpace(vfx.path))
            {
                problems.Add($"{label}: vfx authors both layers and the single-block path. " +
                             "Move the block into a layer or delete it.");
            }

            if (layers.Length > 0 && !string.IsNullOrWhiteSpace(vfx.groundPath))
            {
                problems.Add($"{label}: vfx authors both layers and the single-block groundPath. " +
                             "Author the ground layer as a layer with sort 'ground' instead.");
            }

            // THE SINGLE BLOCK'S OWN `fit`, carried onto the layer the adapter
            // builds. A layered block says it per layer, where it can differ
            // between a projectile and its splash; on the block as well it
            // would be a second home for the same answer.
            if (!SpellFitNames.IsKnown(vfx.fit))
            {
                problems.Add($"{label}: vfx.fit '{vfx.fit}' is not a sizing rule. " +
                             $"Known: {string.Join(", ", SpellFitNames.All)}.");
            }
            else if (layers.Length > 0 && vfx.Fit != SpellFit.None)
            {
                problems.Add($"{label}: vfx authors the single-block fit beside layers. " +
                             "Author fit on each layer instead.");
            }
            else if (vfx.Fit == SpellFit.Target &&
                     (vfx.Anchor == SpellAnchor.Caster || vfx.Anchor == SpellAnchor.CasterCentre))
            {
                problems.Add($"{label}: vfx authors fit 'target' on anchor '{vfx.anchor.Trim()}', which " +
                             "never touches a target's body. Only a target or travelling anchor does.");
            }

            if (vfx.hitCueSeconds < 0f)
            {
                problems.Add($"{label}: vfx.hitCueSeconds {Num(vfx.hitCueSeconds)} cannot be negative.");
            }

            if (vfx.hitCueSeconds > 0f && vfx.layerFormat < CurrentLayerFormat)
            {
                problems.Add($"{label}: vfx.hitCueSeconds {Num(vfx.hitCueSeconds)} needs layerFormat " +
                             $"{CurrentLayerFormat}; a pre-layer block derives its cue from impactFrame.");
            }

            // THE SAME BOUND CheckPlacement enforces on a layer's impactX/
            // impactY (see its own comment on the incident this closes): a
            // pixel coordinate typed where a 0..1 fraction belonged reads as
            // unauthored at runtime -- SpellPresentation.HasImpactPoint and
            // HasGroundImpactY both gate on SpellLayer.IsUnitFraction, so an
            // out-of-range value falls back silently instead of erroring.
            // -1 (Unauthored) stays allowed; only a value that is NEITHER
            // Unauthored NOR a unit fraction is refused.
            bool hasImpactX = vfx.impactX >= 0f;
            bool hasImpactY = vfx.impactY >= 0f;

            if (hasImpactX && !SpellLayer.IsUnitFraction(vfx.impactX))
            {
                problems.Add($"{label}: vfx.impactX {Num(vfx.impactX)}, outside 0..1 -- it is a fraction " +
                             "of the frame, not a pixel coordinate.");
            }

            if (hasImpactY && !SpellLayer.IsUnitFraction(vfx.impactY))
            {
                problems.Add($"{label}: vfx.impactY {Num(vfx.impactY)}, outside 0..1 -- it is a fraction " +
                             "of the frame, not a pixel coordinate.");
            }

            if (vfx.groundImpactY >= 0f && !SpellLayer.IsUnitFraction(vfx.groundImpactY))
            {
                problems.Add($"{label}: vfx.groundImpactY {Num(vfx.groundImpactY)}, outside 0..1 -- it is a " +
                             "fraction of the frame, not a pixel coordinate.");
            }
        }

        // ---- one layer -----------------------------------------------------------

        private static void CheckOneLayer(string label, SpellLayer layer, int index, List<string> problems)
        {
            string at = $"{label}: vfx.layers[{index}]";

            if (layer == null)
            {
                problems.Add($"{at} is empty.");
                return;
            }

            CheckWords(at, layer, problems);
            CheckNumbers(at, layer, problems);
            CheckWhatItNeeds(at, layer, problems);
            CheckPlacement(at, layer, problems);
        }

        private static void CheckWords(string at, SpellLayer layer, List<string> problems)
        {
            // BLANK IS REFUSED for these two and known for the other four.
            // There is no safe default for what draws or where it goes; there
            // is one for every other word, and refusing an omission would make
            // every optional word mandatory.
            if (string.IsNullOrWhiteSpace(layer.render))
            {
                problems.Add($"{at}.render is blank, and there is no default for what a layer draws. " +
                             $"Known: {string.Join(", ", SpellRenderNames.All)}.");
            }
            else if (!SpellRenderNames.IsKnown(layer.render))
            {
                problems.Add($"{at}.render '{layer.render}' is not a renderer. " +
                             $"Known: {string.Join(", ", SpellRenderNames.All)}.");
            }

            if (string.IsNullOrWhiteSpace(layer.place))
            {
                problems.Add($"{at}.place is blank, and there is no default for where a layer goes. " +
                             $"Known: {string.Join(", ", SpellPlaceNames.All)}.");
            }
            else if (!SpellPlaceNames.IsKnown(layer.place))
            {
                problems.Add($"{at}.place '{layer.place}' is not a placement. " +
                             $"Known: {string.Join(", ", SpellPlaceNames.All)}.");
            }

            if (!SpellCueNames.IsKnown(layer.at))
            {
                problems.Add($"{at}.at '{layer.at}' is not a schedule point. " +
                             $"Known: {string.Join(", ", SpellCueNames.All)}.");
            }

            if (!SpellEndNames.IsKnown(layer.until))
            {
                problems.Add($"{at}.until '{layer.until}' is not an end policy. " +
                             $"Known: {string.Join(", ", SpellEndNames.All)}.");
            }

            if (!SpellFacingNames.IsKnown(layer.facing))
            {
                problems.Add($"{at}.facing '{layer.facing}' is not a mirroring policy. " +
                             $"Known: {string.Join(", ", SpellFacingNames.All)}.");
            }

            if (!SpellSortNames.IsKnown(layer.sort))
            {
                problems.Add($"{at}.sort '{layer.sort}' is not a draw band. " +
                             $"Known: {string.Join(", ", SpellSortNames.All)}.");
            }

            if (!SpellAlignNames.IsKnown(layer.align))
            {
                problems.Add($"{at}.align '{layer.align}' is not an alignment. " +
                             $"Known: {string.Join(", ", SpellAlignNames.All)}.");
            }

            if (!SpellFitNames.IsKnown(layer.fit))
            {
                problems.Add($"{at}.fit '{layer.fit}' is not a sizing rule. " +
                             $"Known: {string.Join(", ", SpellFitNames.All)}.");
            }

            if (!SpellOrientNames.IsKnown(layer.orient))
            {
                problems.Add($"{at}.orient '{layer.orient}' is not an orientation. " +
                             $"Known: {string.Join(", ", SpellOrientNames.All)}.");
            }
        }

        private static void CheckNumbers(string at, SpellLayer layer, List<string> problems)
        {
            Positive(at, "seconds", layer.seconds, problems);
            Positive(at, "fps", layer.fps, problems);
            Positive(at, "travelSeconds", layer.travelSeconds, problems);
            Positive(at, "travelDelay", layer.travelDelay, problems);
            Positive(at, "fade", layer.fade, problems);
            Positive(at, "size", layer.size, problems);
            Positive(at, "aspect", layer.aspect, problems);
            Positive(at, "punch", layer.punch, problems);
            Positive(at, "glow", layer.glow, problems);

            // A PUNCH IS AN ACCENT, NOT A ZOOM. Past MaxPunch the layer opens
            // at more than double size and the settle reads as the effect
            // rushing the camera rather than as the ground taking a hit --
            // and, on a formation layer, an oversized first frame reaches well
            // past the bodies it is supposed to be opening under.
            if (layer.punch > MaxPunch)
            {
                problems.Add($"{at}.punch {Num(layer.punch)} is past the {Num(MaxPunch)} a layer's " +
                             "overshoot may reach; that is a zoom rather than an accent.");
            }

            if (layer.startFrame < 0)
            {
                problems.Add($"{at}.startFrame {layer.startFrame} cannot be negative; frames count from 1 " +
                             "and 0 means the first.");
            }

            // ABOVE ZERO, AT MOST ONE. Zero would be a layer that is scheduled,
            // pooled and timed and draws nothing -- delete it instead; above
            // one cannot be drawn (alpha clamps) and would read as "brighter",
            // which is what `glow` is for.
            if (!(layer.opacity > 0f) || layer.opacity > 1f)
            {
                problems.Add($"{at}.opacity {Num(layer.opacity)} must be above 0 and at most 1; 1 is the sheet " +
                             "as painted, and brightness past it is `glow`.");
            }

            if (layer.fade > MaxFadeSeconds)
            {
                problems.Add($"{at}.fade {Num(layer.fade)} is longer than the {Num(MaxFadeSeconds)}s a " +
                             "layer's ending may take.");
            }

            var emitter = layer.emitter;
            if (emitter == null) return;

            Positive(at, "emitter.rate", emitter.rate, problems);
            Positive(at, "emitter.window", emitter.window, problems);
            Positive(at, "emitter.speedMin", emitter.speedMin, problems);
            Positive(at, "emitter.speedMax", emitter.speedMax, problems);
            Positive(at, "emitter.lifeMin", emitter.lifeMin, problems);
            Positive(at, "emitter.lifeMax", emitter.lifeMax, problems);
            Positive(at, "emitter.drag", emitter.drag, problems);

            if (emitter.burst < 0)
            {
                problems.Add($"{at}.emitter.burst {emitter.burst} cannot be negative.");
            }

            if (emitter.lifeMax < emitter.lifeMin)
            {
                problems.Add($"{at}.emitter.lifeMax {Num(emitter.lifeMax)} is shorter than lifeMin " +
                             $"{Num(emitter.lifeMin)}, so no particle could live a legal length.");
            }

            if (emitter.speedMax < emitter.speedMin)
            {
                problems.Add($"{at}.emitter.speedMax {Num(emitter.speedMax)} is slower than speedMin " +
                             $"{Num(emitter.speedMin)}, so no particle could leave at a legal speed.");
            }

            if (emitter.sizeMax < emitter.sizeMin)
            {
                problems.Add($"{at}.emitter.sizeMax {Num(emitter.sizeMax)} is smaller than sizeMin " +
                             $"{Num(emitter.sizeMin)}.");
            }

            if (emitter.spinMax < emitter.spinMin)
            {
                problems.Add($"{at}.emitter.spinMax {Num(emitter.spinMax)} is less than spinMin " +
                             $"{Num(emitter.spinMin)}.");
            }

            if (emitter.inherit < 0f || emitter.inherit > 1f)
            {
                problems.Add($"{at}.emitter.inherit {Num(emitter.inherit)} is a fraction of the source's " +
                             "velocity and must be between 0 and 1.");
            }

            if (emitter.fadeFrom < 0f || emitter.fadeFrom > 1f)
            {
                problems.Add($"{at}.emitter.fadeFrom {Num(emitter.fadeFrom)} is a fraction of a particle's " +
                             "life and must be between 0 and 1.");
            }

            CheckWeights(at, emitter, problems);
        }

        // WHAT THIS RULE CAN CHECK WITHOUT THE DISK, and no more. Whether
        // `weights.Length` matches the folder's own frame count is not
        // answerable here -- Domain has no filesystem -- so that half of the
        // brief lives beside the identical rule for startFrame, in
        // SpellVfxRecipeDriftTests.NoSkillTimesABeatToAFrameItsFolderDoesNotHave.
        // What IS answerable from the numbers alone: a negative or
        // non-finite entry is never a legal chance, and an array that is
        // every entry zero could never pick a cell at all -- SpellEmitterSim
        // falls back to uniform when `weights` is null or empty, but an
        // author who typed three zeros meant something and got silence.
        private static void CheckWeights(string at, SpellEmitter emitter, List<string> problems)
        {
            var weights = emitter.weights;
            if (weights == null || weights.Length == 0) return;

            bool anyPositive = false;

            for (int i = 0; i < weights.Length; i++)
            {
                float value = weights[i];

                if (float.IsNaN(value) || float.IsInfinity(value))
                {
                    problems.Add($"{at}.emitter.weights[{i}] {value} is not a finite number.");
                    continue;
                }

                if (value < 0f)
                {
                    problems.Add($"{at}.emitter.weights[{i}] {Num(value)} cannot be negative.");
                    continue;
                }

                if (value > 0f) anyPositive = true;
            }

            if (!anyPositive)
            {
                problems.Add($"{at}.emitter.weights are all zero, so no cell could ever be picked. Give " +
                             "at least one a positive weight, or remove weights for uniform selection.");
            }
        }

        // What a layer of each kind has to carry to draw anything at all, and
        // what it may not carry from another kind.
        private static void CheckWhatItNeeds(string at, SpellLayer layer, List<string> problems)
        {
            if (!SpellRenderNames.IsKnown(layer.render) || string.IsNullOrWhiteSpace(layer.render)) return;

            var emitter = layer.emitter ?? new SpellEmitter();

            if (layer.Render == SpellRender.Emitter)
            {
                if (string.IsNullOrWhiteSpace(emitter.path))
                {
                    problems.Add($"{at} is an emitter and authors no emitter.path, so it has no sprite to " +
                                 "throw.");
                }

                if (emitter.rate <= 0f && emitter.burst <= 0)
                {
                    problems.Add($"{at} is an emitter and authors neither rate nor burst, so it emits " +
                                 "nothing.");
                }

                // The three sprite words an emitter cannot obey. A particle's
                // own fadeFrom and endScale are its ending, so `until` has
                // nothing to end; there is no sequence for `fps` to pace or
                // `startFrame` to index into.
                if (layer.fps != 0f || layer.startFrame != 0 || !string.IsNullOrWhiteSpace(layer.until))
                {
                    problems.Add($"{at} is an emitter and authors fps, startFrame or until, which are " +
                                 "properties of a played sequence. A particle's own fadeFrom and endScale " +
                                 "are its ending.");
                }

                if (!string.IsNullOrWhiteSpace(layer.path))
                {
                    problems.Add($"{at} is an emitter and authors path; the frames it throws come from " +
                                 "emitter.path.");
                }

                return;
            }

            if (string.IsNullOrWhiteSpace(layer.path))
            {
                problems.Add($"{at} is a {layer.render.Trim().ToLowerInvariant()} and authors no path, so " +
                             "it has no frames to draw.");
            }

            // COMPARED AGAINST A DEFAULT-CONSTRUCTED EMITTER, not against zero.
            // sizeMin, sizeMax and fadeFrom all initialise to 1f, so a
            // "non-zero" test would refuse every sprite layer that ships.
            if (emitter.IsAuthored)
            {
                problems.Add($"{at} is a {layer.render.Trim().ToLowerInvariant()} and authors emitter " +
                             "settings, which are inert. Remove them or change render to emitter.");
            }
        }

        private static void CheckPlacement(string at, SpellLayer layer, List<string> problems)
        {
            if (string.IsNullOrWhiteSpace(layer.place) || !SpellPlaceNames.IsKnown(layer.place)) return;

            bool onFormation = layer.Place == SpellPlace.Formation;

            if (onFormation && layer.size > 0f)
            {
                problems.Add($"{at} is placed on the formation and authors size {Num(layer.size)}, which " +
                             "the measured span overrides. Remove it.");
            }

            if (onFormation && layer.dx != 0f)
            {
                problems.Add($"{at} is placed on the formation and authors dx {Num(layer.dx)}, which the " +
                             "measured span's own midpoint overrides. Remove it.");
            }

            // ONLY WHERE A SINGLE STRUCK BODY IS RESOLVED. `target` and
            // `target-centre` are the two placements PlaceOne resolves against
            // one struck combatant's own rect; `formation` measures a span
            // over every struck body at once and `caster`/`caster-centre`
            // (without travel) never touch a target's rect at all -- none of
            // the three has "the struck target" for `fit: target` to read a
            // footprint off, so it is refused here rather than silently doing
            // nothing, the same rule this file already applies to `size` and
            // `align` on placements that cannot use them.
            //
            // A TRAVELLING LAYER RESOLVES ONE TOO -- it arrives on a single
            // struck body and is fanned out per target for exactly that reason
            // (SpellLayer.IsPerTarget), so a bolt that should land at the size
            // of what it hits may say so.
            if (SpellFitNames.IsKnown(layer.fit) && layer.Fit == SpellFit.Target &&
                !SpellPlaceNames.PerTarget(layer.Place) && !layer.Travels)
            {
                problems.Add($"{at} authors fit 'target' but is placed on {layer.place.Trim()}, which " +
                             "resolves no single struck target to read a footprint off. Only target, " +
                             "target-centre, sky or a travelling layer do.");
            }

            // A STILL BOX HAS NO LINE OF FLIGHT to turn onto, and an unturned
            // layer has no use for the direction its drawing points -- both
            // refused rather than left inert, the rule `align` and `size`
            // already follow.
            if (SpellOrientNames.IsKnown(layer.orient) && layer.Orient == SpellOrient.Path && !layer.Travels)
            {
                problems.Add($"{at} authors orient 'path' but does not travel, so there is no flight for " +
                             "it to point along. Give it travelSeconds or remove orient.");
            }

            if (layer.artDegrees != 0f &&
                !(SpellOrientNames.IsKnown(layer.orient) && layer.Orient == SpellOrient.Path))
            {
                problems.Add($"{at} authors artDegrees {Num(layer.artDegrees)} but not orient 'path', so " +
                             "nothing reads it. Add orient 'path' or remove it.");
            }

            // ONLY A FORMATION HAS A LINE TO LIE ALONG. Every other placement
            // resolves to ONE point -- a slot's origin, a slot's middle, a
            // caster's cast point -- and there is no second point to take a
            // direction from. Refused rather than ignored, because a word that
            // silently does nothing on five of the six placements is a word an
            // author will keep re-typing and keep not seeing.
            if (!onFormation && SpellAlignNames.IsKnown(layer.align) &&
                layer.Align != SpellAlign.Level)
            {
                problems.Add($"{at} authors align '{layer.align.Trim()}' but is placed on " +
                             $"{layer.place.Trim()}, which is one point and has no line to lie along. " +
                             "Only 'formation' spans a rank.");
            }

            // BOTH HALVES OF THE POINT OR NEITHER, the rule
            // SpellPresentation.HasImpactPoint already states -- EXCEPT on the
            // formation, whose horizontal centre is the measured midpoint of
            // the struck span. There is nothing there for an impactX to
            // correct, and the pre-layer block it replaces has no
            // groundImpactX field to have come from.
            bool hasX = layer.impactX >= 0f;
            bool hasY = layer.impactY >= 0f;

            // THE UPPER HALF OF THE SAME BOUND SpellLayer.HasImpactPoint
            // already enforces (impactX/impactY <= 1f, both axes). Without
            // this, a value like 65 -- a pixel coordinate typed where a
            // fraction belonged -- passed CheckPlacement as "authored" and
            // failed silently at runtime instead: HasImpactPoint would read
            // false for it, and the layer would fall back to whatever an
            // unauthored impact point does, with no error pointing at why.
            if (hasX && !SpellLayer.IsUnitFraction(layer.impactX))
            {
                problems.Add($"{at} authors impactX {Num(layer.impactX)}, outside 0..1 -- it is a fraction " +
                             "of the frame, not a pixel coordinate.");
            }

            if (hasY && !SpellLayer.IsUnitFraction(layer.impactY))
            {
                problems.Add($"{at} authors impactY {Num(layer.impactY)}, outside 0..1 -- it is a fraction " +
                             "of the frame, not a pixel coordinate.");
            }

            if (hasX && !hasY)
            {
                problems.Add($"{at} states impactX and not impactY. A sheet corrected on one axis and left " +
                             "on the other lands where neither rule intended.");
            }

            if (hasY && !hasX && !onFormation)
            {
                problems.Add($"{at} states impactY and not impactX. A sheet corrected on one axis and left " +
                             "on the other lands where neither rule intended.");
            }

            if (layer.travelSeconds > 0f)
            {
                if (layer.Render == SpellRender.Emitter)
                {
                    problems.Add($"{at} travels, but an emitter has no box to move -- its particles detach " +
                                 "the instant they exist. Place it on the travelling layer instead.");
                }
                else if (!SpellPlaceNames.CanLaunch(layer.Place))
                {
                    problems.Add($"{at} travels but is placed on {layer.place.Trim()}, so it has nowhere to " +
                                 "travel from. A projectile leaves the caster or the sky.");
                }
            }
            else if (layer.travelDelay > 0f)
            {
                problems.Add($"{at} authors travelDelay {Num(layer.travelDelay)} but does not travel, so " +
                             "there is no motion for it to hold back. Use offset to delay the whole layer.");
            }

            // A NON-EMITTER LAYER'S LIFETIME IS FINITE. `loop` and `hold`
            // describe what the sheet does while it is alive, never how long it
            // lives, so one of the three bounds has to be there: an authored
            // length, an arrival to end at, or a source whose ending it takes.
            // An emitter is exempt by construction -- window + lifeMax is
            // finite whatever an author writes.
            bool bounded = layer.seconds > 0f ||
                           layer.travelSeconds > 0f ||
                           layer.Place == SpellPlace.Layer;

            if (layer.Render != SpellRender.Emitter && !bounded &&
                (layer.Until == SpellEnd.Loop || layer.Until == SpellEnd.Hold) &&
                !string.IsNullOrWhiteSpace(layer.until))
            {
                problems.Add($"{at} is until '{layer.until.Trim()}' with no seconds, no travel and nothing " +
                             "to follow, so it would draw for the rest of the fight. Give it seconds, give " +
                             "it travelSeconds, or place it on a layer.");
            }
        }

        // ---- the layers against each other ---------------------------------------

        private static void CheckAcrossLayers(string label, SpellLayer[] layers, List<string> problems)
        {
            var declared = new List<string>();
            foreach (var layer in layers)
            {
                if (layer != null && !string.IsNullOrWhiteSpace(layer.id)) declared.Add(layer.id.Trim());
            }

            foreach (string duplicate in declared.GroupBy(id => id, StringComparer.Ordinal)
                         .Where(g => g.Count() > 1).Select(g => g.Key))
            {
                problems.Add($"{label}: vfx.layers id '{duplicate}' is used twice, so a reference to it " +
                             "could name either.");
            }

            for (int i = 0; i < layers.Length; i++)
            {
                string reference = layers[i] == null ? "" : layers[i].FollowsLayerId;
                if (string.IsNullOrWhiteSpace(reference)) continue;

                if (!declared.Contains(reference, StringComparer.Ordinal))
                {
                    string known = declared.Count > 0 ? string.Join(", ", declared) : "none";
                    problems.Add($"{label}: vfx.layers[{i}].place 'layer:{reference}' names no layer. " +
                                 $"Declared: {known}.");
                }
            }

            CheckNoCycles(label, layers, problems);
            CheckArrival(label, layers, problems);
        }

        // THE ONE REFERENCE IN THE VOCABULARY IS THE ONE THING THAT CAN CYCLE.
        // The schedule has no references at all -- every cue is an absolute
        // word plus an offset -- so this is the whole of the graph checking the
        // design needs, which is why there is no dependency graph, no
        // topological order and no scheduler that knows about either.
        private static void CheckNoCycles(string label, SpellLayer[] layers, List<string> problems)
        {
            var sourceOf = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var layer in layers)
            {
                if (layer == null || string.IsNullOrWhiteSpace(layer.id)) continue;
                string reference = layer.FollowsLayerId;
                if (string.IsNullOrWhiteSpace(reference)) continue;
                if (!sourceOf.ContainsKey(layer.id.Trim())) sourceOf[layer.id.Trim()] = reference;
            }

            foreach (string start in sourceOf.Keys)
            {
                var walked = new List<string> { start };
                string current = start;

                while (sourceOf.TryGetValue(current, out string next))
                {
                    if (walked.Contains(next, StringComparer.Ordinal))
                    {
                        walked.Add(next);
                        problems.Add($"{label}: vfx.layers: '{string.Join("' is placed on '", walked)}', " +
                                     "which is a cycle -- nothing in it has a position to start from.");
                        return;
                    }

                    walked.Add(next);
                    current = next;
                }
            }
        }

        // ARRIVAL IS A CAST-LEVEL TIME, so both rules below are cast-scoped.
        // A layer scheduled at arrival does not have to travel itself and does
        // not have to name the layer that does -- Water's splash is placed on
        // the target and neither travels nor references the core.
        private static void CheckArrival(string label, SpellLayer[] layers, List<string> problems)
        {
            var travellers = layers.Where(l => l != null && l.Travels).ToList();
            var atArrival = layers
                .Select((layer, index) => (layer, index))
                .Where(p => p.layer != null && SpellCueNames.IsKnown(p.layer.at) && p.layer.At == SpellCue.Arrival)
                .ToList();

            if (atArrival.Count == 0) return;

            if (travellers.Count == 0)
            {
                foreach (var (_, index) in atArrival)
                {
                    problems.Add($"{label}: vfx.layers[{index}] fires at arrival, but nothing in this spell " +
                                 "travels. Use release, or give a layer travelSeconds.");
                }

                return;
            }

            // AT MOST ONE TRAVELLER, and this rule is what makes "the first
            // travelling layer in authored order" a safe definition of the
            // cast's arrival rather than an arbitrary tie-break. A second
            // traveller's own arrival becomes expressible the day something
            // wants it, by naming the arrival layer -- and this rule is what
            // gets deleted to allow it.
            if (travellers.Count > 1)
            {
                string names = string.Join("' and '", travellers.Select(NameOf));
                problems.Add($"{label}: vfx.layers '{names}' both travel, so 'arrival' names two instants. " +
                             "Give one of them travelSeconds 0, or schedule off release with an offset.");
            }
        }

        private static string NameOf(SpellLayer layer) =>
            string.IsNullOrWhiteSpace(layer.id) ? layer.render : layer.id.Trim();

        private static void Positive(string at, string field, float value, List<string> problems)
        {
            if (value < 0f) problems.Add($"{at}.{field} {Num(value)} cannot be negative.");
        }

        // Invariant formatting, so an error message reads the same on a machine
        // whose decimal separator is a comma as on one whose is a dot -- a
        // content error quoted back with the wrong separator sends an author
        // looking for a value that is not in their file.
        private static string Num(float value) =>
            value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }
}

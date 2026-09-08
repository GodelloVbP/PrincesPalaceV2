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

            if (vfx.hitCueSeconds < 0f)
            {
                problems.Add($"{label}: vfx.hitCueSeconds {Num(vfx.hitCueSeconds)} cannot be negative.");
            }

            if (vfx.hitCueSeconds > 0f && vfx.layerFormat < CurrentLayerFormat)
            {
                problems.Add($"{label}: vfx.hitCueSeconds {Num(vfx.hitCueSeconds)} needs layerFormat " +
                             $"{CurrentLayerFormat}; a pre-layer block derives its cue from impactFrame.");
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

            if (layer.startFrame < 0)
            {
                problems.Add($"{at}.startFrame {layer.startFrame} cannot be negative; frames count from 1 " +
                             "and 0 means the first.");
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

            // BOTH HALVES OF THE POINT OR NEITHER, the rule
            // SpellPresentation.HasImpactPoint already states -- EXCEPT on the
            // formation, whose horizontal centre is the measured midpoint of
            // the struck span. There is nothing there for an impactX to
            // correct, and the pre-layer block it replaces has no
            // groundImpactX field to have come from.
            bool hasX = layer.impactX >= 0f;
            bool hasY = layer.impactY >= 0f;

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
                else if (!SpellPlaceNames.OnCaster(layer.Place))
                {
                    problems.Add($"{at} travels but is placed on {layer.place.Trim()}, so it has nowhere to " +
                                 "travel from. A projectile leaves the caster.");
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

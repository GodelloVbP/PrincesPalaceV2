using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Events;

namespace PrincesPalace.Domain.Content
{
    // Returning events, event fights and event speakers
    // (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 1.1-1.3 and 3.5, Stage A). Its own
    // file for the reason the Dialogue half has one: the Caravan's shelves
    // (Stage B) land beside these without meeting the page/choice code.
    //
    // WHAT IS REFUSED, and why each is a build error rather than a runtime
    // guess:
    //   - finish on an event without mayReturn: it would mark seen an event
    //     that opening already marked seen, so it reads as meaning something
    //     it does not.
    //   - a fight effect in a choice's effects, more than one in an outcome,
    //     or beside a non-empty goTo: the fight's result is what decides where
    //     the event goes, so exactly one fight owns the "next" of its outcome.
    //   - requires on a fight result: the fight already happened; there is
    //     no "else" to fall through to.
    //   - onFell on endRun (the run is over, nothing reads it); onFell
    //     missing on wake, onSurvived missing with a round limit, onSurvived
    //     with none: a result that can happen needs an outcome, one that
    //     cannot is authored and never read.
    //   - enemies past the stage's three slots by slotSpan, and any unknown
    //     id: a fight, enemy, character or speaker that does not exist.
    //   - a speaker id that is a character id: a line naming it would be
    //     ambiguous between the party bust and the event's.
    //   - a line naming an expression its event speaker did not declare.
    public static partial class EventEntryResolver
    {
        // The round counter's word ("Toll"). M6 measures the counter's box
        // against this; raising it is a layout change, like every cap here.
        public const int MaxRoundLabelLength = 12;

        // The stage's enemy side, counted by slotSpan exactly as the room
        // encounter builder counts it.
        public const int MaxFightSlots = FightHudSpec.StageSlotsPerSide;

        // Bust file names: DialogueBust joins them onto a Resources folder.
        private static readonly Regex ExpressionNamePattern = new Regex("^[a-z0-9_]+$");

        // What every page, choice and outcome of one event resolves against.
        // One object rather than nine parameters threaded through five
        // methods, so a new per-event fact (Stage B's shelves) is one field.
        private sealed class EventScope
        {
            public string EventId = "";
            public bool MayReturn;
            public HashSet<string> PageIds = new HashSet<string>(StringComparer.Ordinal);
            public HashSet<string> FightIds = new HashSet<string>(StringComparer.Ordinal);
            public Dictionary<string, ResolvedEventSpeaker> Speakers =
                new Dictionary<string, ResolvedEventSpeaker>(StringComparer.Ordinal);

            public IReadOnlyDictionary<string, string> CharacterDisplayNamesById;
            public IReadOnlyCollection<string> KnownItemIds;
            public IReadOnlyDictionary<string, string> RelicDisplayNamesById;
            public IReadOnlyDictionary<string, int> EnemySlotSpansById;
            public HashSet<string> IncrementedCounters;
            public List<(string CounterId, string Label)> RequiredCounters;
        }

        // ---- speakers ---------------------------------------------------------

        private static bool TryResolveSpeakers(RawEventSpeaker[] raw, string eventLabel, EventScope scope,
            out ResolvedEventSpeaker[] speakers, out string error)
        {
            speakers = Array.Empty<ResolvedEventSpeaker>();
            var list = new List<ResolvedEventSpeaker>();

            foreach (var row in raw ?? Array.Empty<RawEventSpeaker>())
            {
                if (row == null) continue;

                string id = (row.id ?? "").Trim();
                string label = $"{eventLabel} speaker '{id}'";

                if (id.Length == 0)
                {
                    error = $"{eventLabel}: a speaker has no id.";
                    return false;
                }

                if (string.Equals(id, NarrationSpeaker, StringComparison.OrdinalIgnoreCase))
                {
                    error = $"{label}: '{NarrationSpeaker}' is the unvoiced line's keyword, not a speaker id.";
                    return false;
                }

                if (scope.CharacterDisplayNamesById != null && scope.CharacterDisplayNamesById.ContainsKey(id))
                {
                    error = $"{label}: the id is a character id in characters.json -- a line naming it could mean " +
                            "either bust. Give the event speaker its own id.";
                    return false;
                }

                if (scope.Speakers.ContainsKey(id))
                {
                    error = $"{label}: duplicate speaker id -- every speaker id must be unique within its event.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(row.name))
                {
                    error = $"{label}: name is required -- it is what the name plate shows.";
                    return false;
                }

                if (!WithinCap(label, "epithet", row.epithet, CharacterEntryResolver.MaxEpithetLength, out error)
                    || !ArtPathConvention.Check(label, "bustPath", row.bustPath, out error))
                {
                    return false;
                }

                var expressions = new List<string>();
                foreach (string rawExpression in row.expressions ?? Array.Empty<string>())
                {
                    string expression = (rawExpression ?? "").Trim();
                    if (!ExpressionNamePattern.IsMatch(expression))
                    {
                        error = $"{label}: expression '{rawExpression}' must be lowercase letters, digits and " +
                                "underscores -- it is a bust file name.";
                        return false;
                    }

                    if (expressions.Contains(expression))
                    {
                        error = $"{label}: expression '{expression}' is declared twice.";
                        return false;
                    }

                    expressions.Add(expression);
                }

                if (expressions.Count == 0) expressions.Add(DialogueBust.Neutral);

                var speaker = new ResolvedEventSpeaker(id, row.name.Trim(), (row.epithet ?? "").Trim(),
                    (row.bustPath ?? "").Trim(), expressions.ToArray());
                scope.Speakers.Add(id, speaker);
                list.Add(speaker);
            }

            speakers = list.ToArray();
            error = null;
            return true;
        }

        // An event speaker's face: one it declared (matched ignoring case,
        // stored as declared), or its first when the line names none.
        private static bool TryEventSpeakerExpression(ResolvedEventSpeaker speaker, string expressionText, string lineLabel,
            out string expression, out string error)
        {
            expression = "";
            if (expressionText.Length == 0)
            {
                expression = speaker.Expressions.Length > 0 ? speaker.Expressions[0] : DialogueBust.Neutral;
                error = null;
                return true;
            }

            foreach (string declared in speaker.Expressions)
            {
                if (string.Equals(declared, expressionText, StringComparison.OrdinalIgnoreCase))
                {
                    expression = declared;
                    error = null;
                    return true;
                }
            }

            error = $"{lineLabel}: expression '{expressionText}' is not declared by speaker '{speaker.Id}' " +
                    $"({string.Join(", ", speaker.Expressions)}). Add it to the speaker's expressions, or use one of those.";
            return false;
        }

        // ---- fights -----------------------------------------------------------

        private static bool TryResolveFights(RawEventFight[] raw, string eventLabel, EventScope scope,
            out ResolvedEventFight[] fights, out string error)
        {
            fights = Array.Empty<ResolvedEventFight>();
            var list = new List<ResolvedEventFight>();

            foreach (var row in raw ?? Array.Empty<RawEventFight>())
            {
                if (row == null) continue;

                string id = (row.id ?? "").Trim();
                if (id.Length == 0)
                {
                    error = $"{eventLabel}: a fight has no id.";
                    return false;
                }

                if (!scope.FightIds.Add(id))
                {
                    error = $"{eventLabel} fight '{id}': duplicate fight id -- every fight id must be unique within its event.";
                    return false;
                }

                if (!TryResolveFight(row, id, $"{eventLabel} fight '{id}'", scope, out var fight, out error))
                {
                    return false;
                }

                list.Add(fight);
            }

            fights = list.ToArray();
            error = null;
            return true;
        }

        private static bool TryResolveFight(RawEventFight raw, string id, string label, EventScope scope,
            out ResolvedEventFight fight, out string error)
        {
            fight = null;

            // Enemies: known, and no more body than the stage has floor.
            var enemies = (raw.enemies ?? Array.Empty<string>()).Select(e => (e ?? "").Trim()).ToArray();
            if (enemies.Length == 0)
            {
                error = $"{label}: enemies is empty -- a fight needs at least one enemy.";
                return false;
            }

            int slots = 0;
            foreach (string enemy in enemies)
            {
                if (scope.EnemySlotSpansById == null || !scope.EnemySlotSpansById.TryGetValue(enemy, out int span))
                {
                    error = $"{label}: enemy '{enemy}' is not an active enemy in enemies.json.";
                    return false;
                }

                slots += span > 0 ? span : 1;
            }

            if (slots > MaxFightSlots)
            {
                error = $"{label}: enemies take {slots} stage slots by slotSpan ({string.Join(", ", enemies)}), over the " +
                        $"{MaxFightSlots} the stage has.";
                return false;
            }

            // Party override: known characters, each once.
            var party = new List<string>();
            foreach (string rawMember in raw.party ?? Array.Empty<string>())
            {
                string member = (rawMember ?? "").Trim();
                if (!TryKnownCharacter(member, label, "party entry", scope.CharacterDisplayNamesById, out _, out error))
                {
                    return false;
                }

                if (party.Contains(member))
                {
                    error = $"{label}: party lists '{member}' twice.";
                    return false;
                }

                party.Add(member);
            }

            // Round limit, and the three fields only a round limit reads.
            if (raw.surviveRounds < 0)
            {
                error = $"{label}: surviveRounds is {raw.surviveRounds}; 0 means no round limit, above 0 is the limit.";
                return false;
            }

            bool hasLimit = raw.surviveRounds > 0;
            string overlayPath = (raw.roundOverlay?.path ?? "").Trim();
            if (!hasLimit)
            {
                string unread = !string.IsNullOrWhiteSpace(raw.roundLabel) ? "roundLabel"
                    : !string.IsNullOrWhiteSpace(raw.roundSfx) ? "roundSfx"
                    : overlayPath.Length > 0 ? "roundOverlay.path"
                    : null;
                if (unread != null)
                {
                    error = $"{label}: {unread} is set on a fight with no round limit (surviveRounds 0), so it would " +
                            "never be read. Set surviveRounds, or drop it.";
                    return false;
                }
            }

            if (!WithinCap(label, "roundLabel", raw.roundLabel, MaxRoundLabelLength, out error))
            {
                return false;
            }

            // Loss policy: empty is a room's own rule, the run ends.
            var loss = EventFightLoss.EndRun;
            if (!string.IsNullOrWhiteSpace(raw.onLoss) && !TryParseExactName(raw.onLoss, out loss))
            {
                error = $"{label}: onLoss '{raw.onLoss}' is not endRun or wake.";
                return false;
            }

            // Art and sound.
            if (!TryResolveBackdrop(label, scope.EventId, raw.backdrop, out error)
                || !ArtPathConvention.Check(label, "roundSfx", raw.roundSfx, out error)
                || !ArtPathConvention.Check(label, "ambience", raw.ambience, out error)
                || !ArtPathConvention.Check(label, "roundOverlay.path", overlayPath, out error)
                || !IsFiledInItsEventFolder(label, scope.EventId, overlayPath, out error, "roundOverlay.path"))
            {
                return false;
            }

            float fromScale = raw.roundOverlay?.fromScale ?? 1f;
            float toScale = raw.roundOverlay?.toScale ?? 1f;
            if (overlayPath.Length > 0 && (fromScale <= 0f || toScale <= 0f))
            {
                error = $"{label}: roundOverlay scales must be above 0 (fromScale {fromScale}, toScale {toScale}).";
                return false;
            }

            // Results. Which ones must exist follows from what can happen.
            if (!TryResolveFightResult(raw.onDefeated, EventFightResult.Defeated, label, scope,
                    required: true, refusedWhy: null, out var onDefeated, out _, out error)
                || !TryResolveFightResult(raw.onSurvived, EventFightResult.Survived, label, scope,
                    required: hasLimit, refusedWhy: hasLimit ? null : "the fight has no round limit (surviveRounds 0)",
                    out var onSurvived, out bool hasSurvived, out error)
                || !TryResolveFightResult(raw.onFell, EventFightResult.Fell, label, scope,
                    required: loss == EventFightLoss.Wake,
                    refusedWhy: loss == EventFightLoss.EndRun ? "onLoss is endRun, so a loss ends the run" : null,
                    out var onFell, out bool hasFell, out error))
            {
                return false;
            }

            fight = new ResolvedEventFight
            {
                Id = id,
                EnemyIds = enemies,
                Elite = raw.elite,
                PartyIds = party.ToArray(),
                SurviveRounds = raw.surviveRounds,
                RoundLabel = (raw.roundLabel ?? "").Trim(),
                Loss = loss,
                Pays = raw.pays,
                BackdropKey = (raw.backdrop ?? "").Trim(),
                RoundSfxPath = (raw.roundSfx ?? "").Trim(),
                AmbiencePath = (raw.ambience ?? "").Trim(),
                RoundOverlayKey = overlayPath,
                RoundOverlayFromScale = fromScale,
                RoundOverlayToScale = toScale,
                OnDefeated = onDefeated,
                OnSurvived = onSurvived ?? new ResolvedEventOutcome(),
                OnFell = onFell ?? new ResolvedEventOutcome(),
                HasOnSurvived = hasSurvived,
                HasOnFell = hasFell,
            };
            error = null;
            return true;
        }

        // JsonUtility builds every nested block whether or not the author
        // wrote it, so "absent" means nothing in it was set.
        private static bool IsAuthored(RawEventOutcome raw) =>
            raw != null
            && (!string.IsNullOrWhiteSpace(raw.goTo)
                || !string.IsNullOrEmpty(raw.result)
                || (raw.effects != null && raw.effects.Length > 0)
                || (raw.requires != null && raw.requires.Length > 0));

        // `refusedWhy` non-null: this result cannot happen, so authoring it
        // is refused. Otherwise `required` says whether it must be there.
        private static bool TryResolveFightResult(RawEventOutcome raw, EventFightResult kind, string fightLabel,
            EventScope scope, bool required, string refusedWhy,
            out ResolvedEventOutcome outcome, out bool authored, out string error)
        {
            outcome = null;
            authored = IsAuthored(raw);
            string field = ResultFieldName(kind);
            string label = $"{fightLabel} {field}";

            if (authored && refusedWhy != null)
            {
                error = $"{label}: is authored, but {refusedWhy} -- it would never be read.";
                return false;
            }

            if (!authored)
            {
                if (required)
                {
                    error = kind == EventFightResult.Survived
                        ? $"{label}: is missing, and surviveRounds is above 0 -- a fight with a round limit can end Survived."
                        : kind == EventFightResult.Fell
                            ? $"{label}: is missing, and onLoss is wake -- the run goes on after a loss, so it needs an outcome."
                            : $"{label}: is missing -- every fight can end with its enemies down.";
                    return false;
                }

                error = null;
                return true;
            }

            if (raw.requires != null && raw.requires.Any(r => r != null))
            {
                error = $"{label}: carries requires -- a fight result has no other outcome to fall through to. " +
                        "Put the requirement on the choice or outcome that starts the fight.";
                return false;
            }

            if (!TryResolveOutcome(raw, label, true, scope, true, out outcome, out error))
            {
                return false;
            }

            error = null;
            return true;
        }

        public static string ResultFieldName(EventFightResult kind)
        {
            switch (kind)
            {
                case EventFightResult.Defeated: return "onDefeated";
                case EventFightResult.Survived: return "onSurvived";
                case EventFightResult.Fell: return "onFell";
                default: return kind.ToString();
            }
        }

        // An outcome whose effects start `fightEffects` fights: exactly one,
        // with no goTo of its own, and never from a fight's own result (M2's
        // settlement applies a result once, with one save; a chained fight is
        // not in that contract).
        private static bool TryCheckFightStart(string label, string goTo, int fightEffects, bool isFightResult,
            out string error)
        {
            if (isFightResult)
            {
                error = $"{label}: starts a fight from a fight's own result -- a fight cannot chain into another.";
                return false;
            }

            if (fightEffects > 1)
            {
                error = $"{label}: an outcome starts {fightEffects} fights -- at most one per outcome.";
                return false;
            }

            if (goTo.Length > 0)
            {
                error = $"{label}: an outcome that starts a fight has goTo '{goTo}' -- leave it empty; the fight's " +
                        "onDefeated/onSurvived/onFell outcome says where the event goes next.";
                return false;
            }

            error = null;
            return true;
        }

        // `fight` and `finish`: effects on the event itself, not on the run.
        private static bool TryResolveEventControlEffect(RawEventEffect row, EventEffectKind kind, string label,
            EventScope scope, out EventEffect effect, out string error)
        {
            effect = null;

            if (row.amount != 0)
            {
                error = $"{label}: {row.kind} effect takes no amount, was {row.amount}.";
                return false;
            }

            if (kind == EventEffectKind.Finish)
            {
                if (!scope.MayReturn)
                {
                    error = $"{label}: finish on an event without mayReturn -- opening it already marks it seen, so " +
                            "finish would do nothing. Set mayReturn on the event, or drop the effect.";
                    return false;
                }

                effect = EventEffect.Finish();
                error = null;
                return true;
            }

            string fightId = (row.fight ?? "").Trim();
            if (fightId.Length == 0)
            {
                error = $"{label}: fight effect needs a fight id.";
                return false;
            }

            if (!scope.FightIds.Contains(fightId))
            {
                error = $"{label}: fight effect names fight '{fightId}', which is not in this event's fights.";
                return false;
            }

            effect = EventEffect.StartFight(fightId);
            error = null;
            return true;
        }

        // A fight nothing starts is authored and never fought.
        private static bool TryCheckEveryFightStarts(ResolvedEventDefinition evt, string eventLabel, out string error)
        {
            var started = new HashSet<string>(StringComparer.Ordinal);
            foreach (var page in evt.Pages)
            {
                foreach (var choice in page?.Choices ?? Array.Empty<ResolvedEventChoice>())
                {
                    foreach (var outcome in choice?.Outcomes ?? Array.Empty<ResolvedEventOutcome>())
                    {
                        string fightId = outcome?.FightId ?? "";
                        if (fightId.Length > 0) started.Add(fightId);
                    }
                }
            }

            foreach (var fight in evt.Fights)
            {
                if (started.Contains(fight.Id)) continue;

                error = $"{eventLabel} fight '{fight.Id}': no outcome starts it -- add a fight effect naming it, or drop it.";
                return false;
            }

            error = null;
            return true;
        }

    }
}

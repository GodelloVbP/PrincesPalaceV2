using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Content
{
    // Validates events.json. Same collected-not-first-only reporting as the
    // other resolvers (RelicEntryResolver, EnemyEntryResolver, ...).
    //
    // WHY CHARACTER DISPLAY NAMES TRAVEL IN RATHER THAN BEING LOOKED UP AT
    // RUNTIME. EventRequirement.Evaluate has to produce "Requires Shawn"
    // without touching ContentDatabase (Domain/Events stays engine-free and
    // save-free per plan). ContentBuilder already resolves characters.json
    // before events.json (same ordering reason achievements come before
    // relics), so the real display name is baked into the requirement here,
    // once, rather than the runtime doing a lookup Domain cannot make.
    public static partial class EventEntryResolver
    {
        // Every player-facing string an event carries has a cap here, and
        // each cap is the length of the sample EventScreen's box is audited
        // against (UiStrings.EventTitle/EventBody/EventChoice/EventChoiceLocked
        // build their samples FROM these constants). So the screen is always
        // measured at exactly the longest string the build lets through, and
        // moving a cap re-measures the box at the next scene build instead of
        // drifting past it.
        //
        // Body and outcome result share one box (a result replaces the body).
        public const int MaxBodyLength = 600;
        public const int MaxTitleLength = 28;
        public const int MaxChoiceTextLength = 50;

        // Checked against every caption a CHOICE-level requirement can show,
        // authored or generated (the implied gold gate included). Event-level
        // and outcome-level rows never show a caption, so theirs is not
        // measured.
        public const int MaxLockReasonLength = 46;

        private const int MaxChoicesPerPage = 4;
        private const string LeaveKeyword = "Leave";

        public static bool TryResolveAll(
            IReadOnlyList<RawEventEntry> entries,
            IReadOnlyDictionary<string, string> characterDisplayNamesById,
            IReadOnlyCollection<string> knownItemIds,
            out List<ResolvedEventDefinition> resolved,
            out List<string> errors)
        {
            resolved = new List<ResolvedEventDefinition>();
            errors = new List<string>();

            var incrementedCounters = new HashSet<string>(StringComparer.Ordinal);
            var requiredCounters = new List<(string CounterId, string Label)>();

            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    if (TryResolveOne(entries[i], i, resolved.Count, characterDisplayNamesById, knownItemIds,
                            incrementedCounters, requiredCounters, out var single, out string error))
                    {
                        resolved.Add(single);
                    }
                    else
                    {
                        errors.Add(error);
                    }
                }
            }

            foreach (string duplicateId in resolved.GroupBy(e => e.Id).Where(g => g.Count() > 1).Select(g => g.Key))
            {
                errors.Add($"Duplicate event id '{duplicateId}' -- every id must be unique.");
            }

            // The typo guard, checked once over the whole file rather than
            // per event: a counter is a profile-save value, not scoped to
            // one event, so a requirement in event A can legitimately read a
            // counter an effect in event B increments.
            foreach (var (counterId, label) in requiredCounters)
            {
                if (!incrementedCounters.Contains(counterId))
                {
                    errors.Add($"{label}: counter requirement names '{counterId}', which no effect in any event " +
                               "increments -- likely a typo of the counter id.");
                }
            }

            if (errors.Count > 0)
            {
                resolved = null;
                return false;
            }

            return true;
        }

        private static bool TryResolveOne(RawEventEntry raw, int index, int sortOrder,
            IReadOnlyDictionary<string, string> characterDisplayNamesById,
            IReadOnlyCollection<string> knownItemIds,
            HashSet<string> incrementedCounters,
            List<(string CounterId, string Label)> requiredCounters,
            out ResolvedEventDefinition resolvedEvent, out string error)
        {
            resolvedEvent = default;
            string eventLabel = string.IsNullOrEmpty(raw.id) ? $"events.json entry #{index + 1}" : $"event '{raw.id}'";

            if (string.IsNullOrWhiteSpace(raw.id))
            {
                error = $"{eventLabel}: id is required.";
                return false;
            }

            var floors = raw.floors ?? Array.Empty<int>();

            if (!TryResolveRequirements(raw.requires, eventLabel, characterDisplayNamesById,
                    incrementedCounters, requiredCounters, out var requires, out error))
            {
                return false;
            }

            var rawPages = raw.pages ?? Array.Empty<RawEventPage>();
            if (rawPages.Length == 0)
            {
                error = $"{eventLabel}: has no pages -- an event needs at least one page to open on.";
                return false;
            }

            var pageIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rawPage in rawPages)
            {
                string pageId = rawPage?.id ?? "";
                if (string.IsNullOrWhiteSpace(pageId))
                {
                    error = $"{eventLabel}: a page has no id.";
                    return false;
                }

                if (!pageIds.Add(pageId))
                {
                    error = $"{eventLabel}: duplicate page id '{pageId}' -- every page id must be unique within its event.";
                    return false;
                }
            }

            if (!TryResolveBackdrop(eventLabel, raw.id, raw.backdrop, out error))
            {
                return false;
            }

            string eventBackdrop = string.IsNullOrWhiteSpace(raw.backdrop) ? DefaultBackdrop : raw.backdrop.Trim();

            var resolvedPages = new List<ResolvedEventPage>(rawPages.Length);
            foreach (var rawPage in rawPages)
            {
                if (!TryResolvePage(rawPage, raw.id, eventLabel, eventBackdrop, pageIds, characterDisplayNamesById, knownItemIds,
                        incrementedCounters, requiredCounters, out var page, out error))
                {
                    return false;
                }

                resolvedPages.Add(page);
            }

            var candidate = new ResolvedEventDefinition(raw.id, sortOrder, floors, requires, resolvedPages.ToArray(), eventBackdrop);

            // Needs the whole page graph, so it runs once every page resolved.
            if (!TryCheckSpeakersPresent(candidate, eventLabel, out error))
            {
                return false;
            }

            resolvedEvent = candidate;
            error = null;
            return true;
        }

        public const string EventArtRoot = "Assets/_Project/Art/Events/";

        // ONE FOLDER PER EVENT, NAMED BY ITS ID: Art/Events/<event_id>/<file>.
        //
        // Owner's call, 2026-09-24. Nothing at runtime depends on the layout --
        // ScreenRegistry.WireEvent bakes each page's art by its full path -- so
        // a file filed elsewhere would still show. What this refuses is the
        // drift: art for one event sitting loose in the root or in another
        // event's folder, which is how a delete-this-event pass later takes the
        // wrong files or leaves some behind. Art outside Art/Events/ (a shared
        // background, say) is not this rule's business and passes.
        private static bool IsFiledInItsEventFolder(string pageLabel, string eventId, string artPath, out string error,
            string fieldName = "artPath")
        {
            error = null;
            if (string.IsNullOrWhiteSpace(artPath) || !artPath.StartsWith(EventArtRoot, StringComparison.Ordinal))
            {
                return true;
            }

            string rest = artPath.Substring(EventArtRoot.Length);
            int slash = rest.IndexOf('/');
            bool oneFolderDeep = slash > 0 && slash < rest.Length - 1 && rest.IndexOf('/', slash + 1) < 0;
            if (oneFolderDeep && string.Equals(rest.Substring(0, slash), eventId, StringComparison.Ordinal))
            {
                return true;
            }

            error = $"{pageLabel}: {fieldName} '{artPath}' must be filed as {EventArtRoot}{eventId}/<file> " +
                    "-- every event keeps its art in a folder named by its id.";
            return false;
        }

        private static bool TryResolvePage(RawEventPage raw, string eventId, string eventLabel, string eventBackdrop,
            HashSet<string> pageIds,
            IReadOnlyDictionary<string, string> characterDisplayNamesById,
            IReadOnlyCollection<string> knownItemIds,
            HashSet<string> incrementedCounters,
            List<(string CounterId, string Label)> requiredCounters,
            out ResolvedEventPage resolvedPage, out string error)
        {
            resolvedPage = default;
            string pageLabel = $"{eventLabel} page '{raw.id}'";

            if (!WithinCap(pageLabel, "title", raw.title, MaxTitleLength, out error)
                || !WithinCap(pageLabel, "body", raw.body, MaxBodyLength, out error))
            {
                return false;
            }

            if (!ArtPathConvention.Check(pageLabel, "artPath", raw.artPath, out error)
                || !IsFiledInItsEventFolder(pageLabel, eventId, raw.artPath, out error))
            {
                return false;
            }

            var rawChoices = raw.choices ?? Array.Empty<RawEventChoice>();
            if (rawChoices.Length > MaxChoicesPerPage)
            {
                error = $"{eventLabel}: page '{raw.id}' has {rawChoices.Length} choices, over the {MaxChoicesPerPage} allowed.";
                return false;
            }

            var resolvedChoices = new List<ResolvedEventChoice>(rawChoices.Length);
            bool hasUnconditionalChoice = false;

            foreach (var rawChoice in rawChoices)
            {
                if (!TryResolveChoice(rawChoice, eventLabel, raw.id, pageIds, characterDisplayNamesById, knownItemIds,
                        incrementedCounters, requiredCounters, out var choice, out error))
                {
                    return false;
                }

                // "Unconditional" is the runtime gate's own answer, not a
                // second reading of the raw fields: a way out the build
                // accepts must be one EventChoiceGate can never lock.
                if (EventChoiceGate.Requirements(choice).Count == 0 && !choice.HiddenUntilMet)
                {
                    hasUnconditionalChoice = true;
                }

                resolvedChoices.Add(choice);
            }

            if (!hasUnconditionalChoice)
            {
                error = $"{eventLabel}: page '{raw.id}' has no unconditional choice (no requires, not hiddenUntilMet, " +
                        "and no implied cost gate) -- the player could be trapped here.";
                return false;
            }

            if (!TryResolveBackdrop(pageLabel, eventId, raw.backdrop, out error)
                || !TryResolveLines(raw, pageLabel, characterDisplayNamesById, out var cast, out var lines, out error))
            {
                return false;
            }

            string pageBackdrop = string.IsNullOrWhiteSpace(raw.backdrop) ? eventBackdrop : raw.backdrop.Trim();

            resolvedPage = new ResolvedEventPage(raw.id, raw.artPath ?? "", raw.title ?? "", raw.body ?? "", resolvedChoices.ToArray(),
                pageBackdrop, cast, lines);
            error = null;
            return true;
        }

        private static bool TryResolveChoice(RawEventChoice raw, string eventLabel, string pageId, HashSet<string> pageIds,
            IReadOnlyDictionary<string, string> characterDisplayNamesById,
            IReadOnlyCollection<string> knownItemIds,
            HashSet<string> incrementedCounters,
            List<(string CounterId, string Label)> requiredCounters,
            out ResolvedEventChoice resolvedChoice, out string error)
        {
            resolvedChoice = default;
            string choiceLabel = $"{eventLabel} page '{pageId}' choice '{raw.text}'";

            if (!WithinCap(choiceLabel, "text", raw.text, MaxChoiceTextLength, out error))
            {
                return false;
            }

            if (!TryResolveRequirements(raw.requires, choiceLabel, characterDisplayNamesById,
                    incrementedCounters, requiredCounters, out var requires, out error))
            {
                return false;
            }

            if (!TryResolveEffects(raw.effects, choiceLabel, knownItemIds, incrementedCounters, out var effects, out error))
            {
                return false;
            }

            // Contract 6: derived, never authored -- a cost and its gate
            // cannot drift apart because there is only one number.
            var implied = EventRequirement.ImpliedGoldRequirement(effects);
            if (implied != null)
            {
                var withImplied = new EventRequirement[requires.Length + 1];
                Array.Copy(requires, withImplied, requires.Length);
                withImplied[requires.Length] = implied;
                requires = withImplied;
            }

            foreach (var requirement in requires)
            {
                foreach (var reason in requirement.PossibleReasons())
                {
                    if (!WithinCap(choiceLabel, "lock reason", reason, MaxLockReasonLength, out error))
                    {
                        return false;
                    }
                }
            }

            var rawOutcomes = raw.outcomes ?? Array.Empty<RawEventOutcome>();
            if (rawOutcomes.Length == 0)
            {
                error = $"{choiceLabel}: has no outcomes.";
                return false;
            }

            var resolvedOutcomes = new List<ResolvedEventOutcome>(rawOutcomes.Length);
            for (int i = 0; i < rawOutcomes.Length; i++)
            {
                bool isLast = i == rawOutcomes.Length - 1;
                if (!TryResolveOutcome(rawOutcomes[i], choiceLabel, isLast, pageIds, characterDisplayNamesById,
                        knownItemIds, incrementedCounters, requiredCounters, out var outcome, out error))
                {
                    return false;
                }

                resolvedOutcomes.Add(outcome);
            }

            resolvedChoice = new ResolvedEventChoice(raw.text ?? "", requires, raw.hiddenUntilMet, effects, resolvedOutcomes.ToArray());
            error = null;
            return true;
        }

        private static bool TryResolveOutcome(RawEventOutcome raw, string choiceLabel, bool isLastOutcome,
            HashSet<string> pageIds,
            IReadOnlyDictionary<string, string> characterDisplayNamesById,
            IReadOnlyCollection<string> knownItemIds,
            HashSet<string> incrementedCounters,
            List<(string CounterId, string Label)> requiredCounters,
            out ResolvedEventOutcome resolvedOutcome, out string error)
        {
            resolvedOutcome = default;

            if (!TryResolveRequirements(raw.requires, choiceLabel, characterDisplayNamesById,
                    incrementedCounters, requiredCounters, out var requires, out error))
            {
                return false;
            }

            if (isLastOutcome && requires.Length > 0)
            {
                error = $"{choiceLabel}: the last outcome carries requirements -- a choice must always resolve to " +
                        "something, so its last outcome must be unconditional.";
                return false;
            }

            if (!TryResolveEffects(raw.effects, choiceLabel, knownItemIds, incrementedCounters, out var effects, out error))
            {
                return false;
            }

            if (!WithinCap(choiceLabel, "outcome result", raw.result, MaxBodyLength, out error))
            {
                return false;
            }

            string goTo = (raw.goTo ?? "").Trim();
            bool isLeave = string.Equals(goTo, LeaveKeyword, StringComparison.OrdinalIgnoreCase);

            if (!isLeave)
            {
                if (string.IsNullOrEmpty(goTo))
                {
                    error = $"{choiceLabel}: an outcome's goTo is required -- a page id, or the literal 'Leave'.";
                    return false;
                }

                if (!pageIds.Contains(goTo))
                {
                    error = $"{choiceLabel}: an outcome's goTo names page '{goTo}', which does not exist in this event.";
                    return false;
                }
            }

            resolvedOutcome = new ResolvedEventOutcome(requires, effects, raw.result ?? "", isLeave ? "" : goTo, isLeave);
            error = null;
            return true;
        }

        private static bool TryResolveRequirements(RawEventRequirement[] raw, string label,
            IReadOnlyDictionary<string, string> characterDisplayNamesById,
            HashSet<string> incrementedCounters,
            List<(string CounterId, string Label)> requiredCounters,
            out EventRequirement[] resolved, out string error)
        {
            resolved = Array.Empty<EventRequirement>();
            if (raw == null || raw.Length == 0)
            {
                error = null;
                return true;
            }

            var list = new List<EventRequirement>(raw.Length);
            foreach (var row in raw)
            {
                if (row == null) continue;

                if (!Enum.TryParse<EventRequirementKind>(row.kind, ignoreCase: true, out var kind))
                {
                    error = $"{label}: requirement kind '{row.kind}' is not a known EventRequirementKind " +
                            "(inParty, memberLevel, ability, counter, gold).";
                    return false;
                }

                switch (kind)
                {
                    case EventRequirementKind.InParty:
                    {
                        if (!TryKnownCharacter(row.character, label, "inParty requirement", characterDisplayNamesById,
                                out string displayName, out error))
                        {
                            return false;
                        }

                        list.Add(EventRequirement.InParty(row.character, displayName));
                        break;
                    }

                    case EventRequirementKind.MemberLevel:
                    {
                        if (row.min < 0)
                        {
                            error = $"{label}: memberLevel requirement needs min.";
                            return false;
                        }

                        string displayName = "";
                        if (!string.IsNullOrWhiteSpace(row.character)
                            && !TryKnownCharacter(row.character, label, "memberLevel requirement", characterDisplayNamesById,
                                out displayName, out error))
                        {
                            return false;
                        }

                        list.Add(EventRequirement.MemberLevel(row.min, row.character ?? "", displayName));
                        break;
                    }

                    case EventRequirementKind.Ability:
                    {
                        if (!AbilityScores.TryParse(row.ability, out var ability))
                        {
                            error = $"{label}: ability requirement names '{row.ability}', which is not a known AbilityScore.";
                            return false;
                        }

                        if (row.min < 0)
                        {
                            error = $"{label}: ability requirement needs min.";
                            return false;
                        }

                        string displayName = "";
                        if (!string.IsNullOrWhiteSpace(row.character)
                            && !TryKnownCharacter(row.character, label, "ability requirement", characterDisplayNamesById,
                                out displayName, out error))
                        {
                            return false;
                        }

                        list.Add(EventRequirement.AbilityAtLeast(ability, row.min, row.character ?? "", displayName));
                        break;
                    }

                    case EventRequirementKind.Counter:
                    {
                        if (string.IsNullOrWhiteSpace(row.counter))
                        {
                            error = $"{label}: counter requirement needs a counter id.";
                            return false;
                        }

                        int? min = row.min >= 0 ? row.min : (int?)null;
                        int? max = row.max >= 0 ? row.max : (int?)null;
                        requiredCounters.Add((row.counter, label));
                        list.Add(EventRequirement.Counter(row.counter, min, max));
                        break;
                    }

                    case EventRequirementKind.Gold:
                    {
                        if (row.min < 0)
                        {
                            error = $"{label}: gold requirement needs min.";
                            return false;
                        }

                        list.Add(EventRequirement.Gold(row.min));
                        break;
                    }

                    default:
                        error = $"{label}: requirement kind '{row.kind}' is not a known EventRequirementKind.";
                        return false;
                }

                // Any kind: the author's caption replaces the generated one.
                list[list.Count - 1].WithAuthoredReason(row.reason);
            }

            resolved = list.ToArray();
            error = null;
            return true;
        }

        private static bool TryResolveEffects(RawEventEffect[] raw, string label,
            IReadOnlyCollection<string> knownItemIds, HashSet<string> incrementedCounters,
            out EventEffect[] resolved, out string error)
        {
            resolved = Array.Empty<EventEffect>();
            if (raw == null || raw.Length == 0)
            {
                error = null;
                return true;
            }

            var list = new List<EventEffect>(raw.Length);
            foreach (var row in raw)
            {
                if (row == null) continue;

                if (!Enum.TryParse<EventEffectKind>(row.kind, ignoreCase: true, out var kind))
                {
                    error = $"{label}: effect kind '{row.kind}' is not a known EventEffectKind " +
                            "(gold, healPercent, damagePercent, exp, item, counter).";
                    return false;
                }

                switch (kind)
                {
                    case EventEffectKind.Gold:
                        if (row.amount == 0)
                        {
                            error = $"{label}: gold effect has an amount of 0 and would do nothing.";
                            return false;
                        }

                        list.Add(EventEffect.Gold(row.amount));
                        break;

                    case EventEffectKind.HealPercent:
                        if (row.amount < 1 || row.amount > 100)
                        {
                            error = $"{label}: healPercent effect amount must be 1-100, was {row.amount}.";
                            return false;
                        }

                        list.Add(EventEffect.HealPercent(row.amount));
                        break;

                    case EventEffectKind.DamagePercent:
                        if (row.amount < 1 || row.amount > 100)
                        {
                            error = $"{label}: damagePercent effect amount must be 1-100, was {row.amount}.";
                            return false;
                        }

                        list.Add(EventEffect.DamagePercent(row.amount));
                        break;

                    case EventEffectKind.Exp:
                        if (row.amount <= 0)
                        {
                            error = $"{label}: exp effect amount must be greater than 0, was {row.amount}.";
                            return false;
                        }

                        list.Add(EventEffect.Exp(row.amount));
                        break;

                    case EventEffectKind.Item:
                        if (string.IsNullOrWhiteSpace(row.item))
                        {
                            error = $"{label}: item effect needs an item id.";
                            return false;
                        }

                        if (knownItemIds == null || !knownItemIds.Contains(row.item))
                        {
                            error = $"{label}: item effect names '{row.item}', which is not an item in items.json/itemsets.json/weapons.json.";
                            return false;
                        }

                        list.Add(EventEffect.ItemGrant(row.item, row.amount > 0 ? row.amount : 1));
                        break;

                    case EventEffectKind.Counter:
                        if (string.IsNullOrWhiteSpace(row.counter))
                        {
                            error = $"{label}: counter effect needs a counter id.";
                            return false;
                        }

                        if (row.amount == 0)
                        {
                            error = $"{label}: counter effect '{row.counter}' has an amount of 0 and would do nothing.";
                            return false;
                        }

                        incrementedCounters.Add(row.counter);
                        list.Add(EventEffect.Counter(row.counter, row.amount));
                        break;

                    default:
                        error = $"{label}: effect kind '{row.kind}' is not a known EventEffectKind.";
                        return false;
                }
            }

            resolved = list.ToArray();
            error = null;
            return true;
        }

        // One refusal shape for every capped string: where, which field, how
        // long, what the cap is. The label already names the event (and page
        // and choice where there is one).
        private static bool WithinCap(string label, string field, string value, int cap, out string error)
        {
            int length = (value ?? "").Length;
            if (length > cap)
            {
                error = $"{label}: {field} is {length} characters, over the {cap}-character cap.";
                return false;
            }

            error = null;
            return true;
        }

        // `what` names the field doing the naming ("inParty requirement",
        // "speaker", "cast entry") so the refusal reads right for each.
        private static bool TryKnownCharacter(string characterId, string label, string what,
            IReadOnlyDictionary<string, string> characterDisplayNamesById, out string displayName, out string error)
        {
            displayName = "";

            if (string.IsNullOrWhiteSpace(characterId))
            {
                error = $"{label}: {what} needs a character id.";
                return false;
            }

            if (characterDisplayNamesById == null || !characterDisplayNamesById.TryGetValue(characterId, out displayName))
            {
                error = $"{label}: {what} names character '{characterId}', which is not in characters.json.";
                return false;
            }

            // The caption shows this name; an empty one would read "Requires ".
            if (string.IsNullOrWhiteSpace(displayName))
            {
                error = $"{label}: {what} names character '{characterId}', which has no display name.";
                return false;
            }

            error = null;
            return true;
        }
    }
}

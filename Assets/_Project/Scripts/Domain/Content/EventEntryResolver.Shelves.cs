using System;
using System.Collections.Generic;
using System.Linq;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.Rewards;

namespace PrincesPalace.Domain.Content
{
    // Merchant shelves (docs/PLAN_EVENTS_BELL_AND_CARAVAN.md 1.5, 3.4, 3.5,
    // Stage B): an event's `shelves[]` and the `shelf` / `takeShelf` effects
    // that name them. Beside the fights for the reason the Fights file gives:
    // one per-event fact, one file, no reach into the page/choice code beyond
    // the hooks that call here.
    //
    // WHAT IS REFUSED, and why each is a build error rather than a runtime
    // guess:
    //   - a shelf with no id, a duplicate id, or one no effect ever names: a
    //     stock nobody can reach is authored and never rolled.
    //   - priceFactorPercent outside 1-100, a negative fakeShare, a
    //     consumableCount outside 0 to ShopStock.ConsumableCount (the panel
    //     that shows them has that many cards), a shelf that stocks nothing.
    //   - a section that is not 'gear', or named twice. Books and relics are
    //     named in the refusal: they carry no item instance, so a fake
    //     could not apply to them (plan 6.2).
    //   - a title over the page-title cap (the shelf screen's title box is
    //     audited at it); a keeper that is not one of the event's speakers
    //     (the screen draws the keeper's bust and plate from that row).
    //   - a shelf/takeShelf effect with no shelf, or an unknown one; `shelf`
    //     or `reveal` on another kind (it would read as meaning something);
    //     an amount on shelf; a negative takeShelf amount.
    //   - a shelf opened by a pick that leaves the event, starts a fight or
    //     opens a second shelf: leaving the shelf returns to the event, so
    //     the event must still be open and nothing else may claim the screen.
    //   - a shelf opened from a fight's result: the fight's own screens come
    //     first, and a result applies once, with one save (M2's contract).
    public static partial class EventEntryResolver
    {
        public const string GearSectionName = "gear";

        private static bool TryResolveShelves(RawEventShelf[] raw, string eventLabel, EventScope scope,
            out ResolvedEventShelf[] shelves, out string error)
        {
            shelves = Array.Empty<ResolvedEventShelf>();
            var list = new List<ResolvedEventShelf>();

            foreach (var row in raw ?? Array.Empty<RawEventShelf>())
            {
                if (row == null) continue;

                string id = (row.id ?? "").Trim();
                if (id.Length == 0)
                {
                    error = $"{eventLabel}: a shelf has no id.";
                    return false;
                }

                string label = $"{eventLabel} shelf '{id}'";
                if (!scope.ShelfIds.Add(id))
                {
                    error = $"{label}: duplicate shelf id -- every shelf id must be unique within its event.";
                    return false;
                }

                if (row.priceFactorPercent < 1 || row.priceFactorPercent > 100)
                {
                    error = $"{label}: priceFactorPercent must be 1-100 (a percent of the room shop's price), was " +
                            $"{row.priceFactorPercent}.";
                    return false;
                }

                if (row.fakeShare < 0)
                {
                    error = $"{label}: fakeShare is {row.fakeShare}; 0 means no fakes, 3 means a third of the cards.";
                    return false;
                }

                if (row.consumableCount < 0 || row.consumableCount > ShopStock.ConsumableCount)
                {
                    error = $"{label}: consumableCount must be 0-{ShopStock.ConsumableCount} (the cards its panel " +
                            $"shows), was {row.consumableCount}.";
                    return false;
                }

                var sections = new List<int>();
                foreach (string rawSection in row.sections ?? Array.Empty<string>())
                {
                    string section = (rawSection ?? "").Trim();
                    if (!string.Equals(section, GearSectionName, StringComparison.OrdinalIgnoreCase))
                    {
                        bool noInstance = string.Equals(section, "books", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(section, "relics", StringComparison.OrdinalIgnoreCase);
                        error = noInstance
                            ? $"{label}: section '{rawSection}' is refused -- books and relics carry no item " +
                              "instance, so a fake could not apply to them. A shelf stocks 'gear' and consumables."
                            : $"{label}: section '{rawSection}' is not a shelf section; the only one is 'gear'.";
                        return false;
                    }

                    if (sections.Contains(ShopStock.GearSection))
                    {
                        error = $"{label}: section 'gear' is named twice.";
                        return false;
                    }

                    sections.Add(ShopStock.GearSection);
                }

                if (sections.Count == 0 && row.consumableCount == 0)
                {
                    error = $"{label}: stocks nothing -- name the 'gear' section, or a consumableCount above 0.";
                    return false;
                }

                if (!WithinCap(label, "title", row.title, MaxTitleLength, out error)) return false;

                string keeper = (row.keeper ?? "").Trim();
                if (keeper.Length > 0 && !scope.Speakers.ContainsKey(keeper))
                {
                    error = $"{label}: keeper '{keeper}' is not one of this event's speakers -- the shelf screen " +
                            "shows the keeper's bust and name plate, so it must be a declared speaker.";
                    return false;
                }

                // THE TITLE THE SCREEN WILL SHOW, not only the one authored:
                // an empty title falls back to the keeper's name
                // (MerchantShelfFront), and that lands in the same title box
                // the cap above measures. A speaker's name has no cap of its
                // own, so the fallback is checked here.
                if (string.IsNullOrWhiteSpace(row.title) && keeper.Length > 0)
                {
                    string keeperName = scope.Speakers[keeper].Name ?? "";
                    if (keeperName.Length > MaxTitleLength)
                    {
                        error = $"{label}: has no title, so the shelf screen's title is keeper '{keeper}''s name " +
                                $"'{keeperName}', {keeperName.Length} characters -- over the title cap of " +
                                $"{MaxTitleLength}. Give the shelf a title, or the keeper a shorter name.";
                        return false;
                    }
                }

                list.Add(new ResolvedEventShelf(id, row.priceFactorPercent, row.fakeShare, sections.ToArray(),
                    row.consumableCount, (row.title ?? "").Trim(), keeper));
            }

            shelves = list.ToArray();
            error = null;
            return true;
        }

        // `shelf` and `takeShelf`: effects on the event's own stock.
        private static bool TryResolveShelfEffect(RawEventEffect row, EventEffectKind kind, string label,
            EventScope scope, out EventEffect effect, out string error)
        {
            effect = null;

            string shelfId = (row.shelf ?? "").Trim();
            if (shelfId.Length == 0)
            {
                error = $"{label}: {row.kind} effect needs a shelf id.";
                return false;
            }

            if (!scope.ShelfIds.Contains(shelfId))
            {
                error = $"{label}: {row.kind} effect names shelf '{shelfId}', which is not in this event's shelves.";
                return false;
            }

            if (kind == EventEffectKind.Shelf)
            {
                if (row.amount != 0)
                {
                    error = $"{label}: shelf effect takes no amount, was {row.amount}.";
                    return false;
                }

                effect = EventEffect.OpenShelf(shelfId, row.reveal);
                error = null;
                return true;
            }

            if (row.amount < 0)
            {
                error = $"{label}: takeShelf amount is the cards lost first, 0 or more; was {row.amount}.";
                return false;
            }

            effect = EventEffect.TakeShelf(shelfId, row.amount);
            error = null;
            return true;
        }

        // The field half of the shelf effects: `shelf` and `reveal` mean
        // nothing on any other kind, so a row carrying them is a typo.
        private static bool TryCheckShelfFields(RawEventEffect row, EventEffectKind kind, string label, out string error)
        {
            bool isShelfKind = kind == EventEffectKind.Shelf || kind == EventEffectKind.TakeShelf;
            if (!string.IsNullOrWhiteSpace(row.shelf) && !isShelfKind)
            {
                error = $"{label}: shelf is only read by a shelf or takeShelf effect, not by {row.kind}.";
                return false;
            }

            if (row.reveal && kind != EventEffectKind.Shelf)
            {
                error = $"{label}: reveal is only read by a shelf effect, not by {row.kind}.";
                return false;
            }

            error = null;
            return true;
        }

        // One pick = the choice's own effects plus one outcome's. The shelf it
        // opens returns to the event, so the pick must leave the event open
        // and hand the screen to nothing else.
        private static bool TryCheckShelfOpening(string choiceLabel, EventEffect[] choiceEffects,
            ResolvedEventOutcome outcome, out string error)
        {
            var pick = (choiceEffects ?? Array.Empty<EventEffect>())
                .Concat(outcome?.Effects ?? Array.Empty<EventEffect>())
                .Where(e => e != null)
                .ToList();

            if (!TryCheckFinishAfterShelves(choiceLabel, pick, out error)) return false;

            int opened = pick.Count(e => e.Kind == EventEffectKind.Shelf);
            if (opened == 0)
            {
                error = null;
                return true;
            }

            if (pick.Any(e => e.Kind == EventEffectKind.Finish))
            {
                error = $"{choiceLabel}: a pick opens a shelf and finishes the event -- finish ends the event's stock, " +
                        "so the shelf would open on nothing. Finish from another choice.";
                return false;
            }

            if (opened > 1)
            {
                error = $"{choiceLabel}: a pick opens {opened} shelves -- at most one; the screen shows one stock.";
                return false;
            }

            if (pick.Any(e => e.Kind == EventEffectKind.Fight))
            {
                error = $"{choiceLabel}: a pick both opens a shelf and starts a fight -- each takes the screen; " +
                        "split them into two choices.";
                return false;
            }

            if (outcome != null && outcome.IsLeave)
            {
                error = $"{choiceLabel}: a pick opens a shelf and leaves the event -- leaving the shelf returns to " +
                        "the event, so the outcome's goTo must be a page.";
                return false;
            }

            error = null;
            return true;
        }

        // `finish` ends the event's stock, so a shelf or takeShelf after it in
        // the same pick would roll a fresh stock nothing ever sees again.
        private static bool TryCheckFinishAfterShelves(string label, IEnumerable<EventEffect> effects, out string error)
        {
            bool finished = false;
            foreach (var effect in effects ?? Array.Empty<EventEffect>())
            {
                if (effect == null) continue;
                if (effect.Kind == EventEffectKind.Finish) finished = true;
                else if (finished && (effect.Kind == EventEffectKind.Shelf || effect.Kind == EventEffectKind.TakeShelf))
                {
                    error = $"{label}: {effect.Kind} comes after finish -- finish ends the event's stock, so put it last.";
                    return false;
                }
            }

            error = null;
            return true;
        }

        // A fight's result applies once, after the fight's own screens.
        private static bool TryCheckNoShelfFromFightResult(string label, EventEffect[] effects, out string error)
        {
            if (!TryCheckFinishAfterShelves(label, effects, out error)) return false;

            if ((effects ?? Array.Empty<EventEffect>()).Any(e => e != null && e.Kind == EventEffectKind.Shelf))
            {
                error = $"{label}: opens a shelf from a fight's result -- open it from a choice; a fight's result " +
                        "can hand a shelf over with takeShelf.";
                return false;
            }

            error = null;
            return true;
        }

        // A shelf no effect names is authored and never rolled.
        private static bool TryCheckEveryShelfUsed(ResolvedEventDefinition evt, string eventLabel, out string error)
        {
            var named = new HashSet<string>(StringComparer.Ordinal);
            void Collect(IEnumerable<EventEffect> effects)
            {
                foreach (var effect in effects ?? Array.Empty<EventEffect>())
                {
                    if (effect != null && (effect.Kind == EventEffectKind.Shelf || effect.Kind == EventEffectKind.TakeShelf))
                    {
                        named.Add(effect.ShelfId ?? "");
                    }
                }
            }

            foreach (var page in evt.Pages)
            {
                foreach (var choice in page?.Choices ?? Array.Empty<ResolvedEventChoice>())
                {
                    Collect(choice?.Effects);
                    foreach (var outcome in choice?.Outcomes ?? Array.Empty<ResolvedEventOutcome>()) Collect(outcome?.Effects);
                }
            }

            foreach (var fight in evt.Fights)
            {
                foreach (var outcome in fight.Outcomes()) Collect(outcome?.Effects);
            }

            foreach (var shelf in evt.Shelves)
            {
                if (named.Contains(shelf.Id)) continue;

                error = $"{eventLabel} shelf '{shelf.Id}': no effect opens or takes it -- add a shelf or takeShelf " +
                        "effect naming it, or drop it.";
                return false;
            }

            error = null;
            return true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.DebugMenu;
using PrincesPalace.Domain.Economy;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // The debug menu, driven.
    //
    // Grants content and currency into the save, and runs a handful of
    // shortcuts on the run. Every rule it needs already exists -- Wallet.Add,
    // InventoryOps.Add, Character.AddExperience, TalentOps.Respec,
    // RunEncounter.HealPartyToFull, RunManager.AdvanceLeg -- so this resolves
    // content, paints the rail and the list, and calls them. Nothing here is a
    // second implementation of a rule; a debug grant that went around the
    // real path would test the debug menu instead of the game.
    //
    // WHY A PICKER AND NOT A "GIVE ME EVERYTHING" BUTTON: there are 260+
    // generated item assets and the bag shows 20 per page behind prev/next
    // arrows with no filter. Dumping the catalogue into the bag would produce
    // an unnavigable pile and look like a bug. The browsing happens here,
    // where a category, a sub-filter and a pager are cheap, and the bag only
    // ever holds what was deliberately asked for.
    //
    // REBUILT 2026-09-23: a category rail over a two-column list, a grant bar
    // with sticky plus/quantity, spell books and relics, and the Resources/
    // Tools tabs that replaced the three currency buttons. Mounted in the hub
    // AND on the map, because books, relics and every Tools row live on the
    // descent and the hub has none.
    public class DebugMenuController : MonoBehaviour
    {
        [SerializeField] internal Button closeButton;

        [SerializeField] internal Button[] categoryButtons;
        [SerializeField] internal Button[] subFilterButtons;
        [SerializeField] internal TMP_Text[] subFilterLabels;
        [SerializeField] internal Button[] rowButtons;
        [SerializeField] internal TMP_Text[] rowLabels;

        [SerializeField] internal TMP_Text pageLabel;
        [SerializeField] internal Button prevPageButton;
        [SerializeField] internal Button nextPageButton;

        [SerializeField] internal Button plusMinusButton;
        [SerializeField] internal Button plusPlusButton;
        [SerializeField] internal TMP_Text plusLabel;
        [SerializeField] internal Button[] qtyButtons;
        [SerializeField] internal TMP_Text toastLabel;

        // Told after every write, so whatever screen this is mounted on can
        // repaint what it shows (the map's gold, the leg it just jumped). Set
        // at runtime by the host controller, not serialized -- a delegate is
        // not something a scene can hold.
        public Action Changed { get; set; }

        // Named so the amounts are stated once and are visible to the test
        // that pins them, rather than buried in a lambda.
        public static readonly int[] GoldGrants = { 100, 1000, 10000 };
        public static readonly int[] EmberGrants = { 1, 25 };
        public const int RunGoldGrant = 1000;

        // x1 / x5 / x10, indexed to match QtyButtons.
        private static readonly int[] QtyValues = { 1, 5, 10 };

        // Rail order IS DebugCategory's declared order -- the screen builds
        // one button per value in the same order, so an index means the same
        // thing on both sides.
        private static readonly DebugCategory[] Categories =
            (DebugCategory[])Enum.GetValues(typeof(DebugCategory));

        // The six non-weapon paperdoll slots. Weapon1/Weapon2 never appear:
        // a Weapon-kind item is in the Weapons tab, and nothing of kind
        // Equipment is worn in a weapon slot.
        private static readonly EquipmentSlot[] EquipmentSlots =
        {
            EquipmentSlot.Head, EquipmentSlot.Necklace, EquipmentSlot.Torso,
            EquipmentSlot.Legs, EquipmentSlot.Shoes, EquipmentSlot.Gloves,
        };

        private static readonly UiString[] EquipmentSlotCaptions =
        {
            UiStrings.DebugSlotHead, UiStrings.DebugSlotNecklace, UiStrings.DebugSlotTorso,
            UiStrings.DebugSlotLegs, UiStrings.DebugSlotShoes, UiStrings.DebugSlotGloves,
        };

        // Item tiers run 0..10; book tiers 1..3 (the content's own range --
        // a T0 chip on the books tab would always be empty).
        private static readonly int[] ItemTiers = Enumerable.Range(0, 11).ToArray();
        private static readonly int[] BookTiers = { 1, 2, 3 };

        private int _categoryIndex;

        // Which pooled sub-filter button is selected, 0 being "All". Reset on
        // every category change: a chip that meant "Necklace" under Armour
        // means T1, or nothing, under anything else.
        private int _subFilterButtonIndex;

        private int _page;

        // PLUS and QUANTITY are sticky across rows, categories and pages --
        // they live here, not per-row, and only the scene reloading resets
        // them.
        private int _plus;
        private int _qtyIndex;

        private bool _wired;
        private IReadOnlyList<DebugItem> _filtered = new List<DebugItem>();

        // The verbs behind the Resources/Tools rows, keyed by the row's Id.
        // Rebuilt with the catalogue on every Refresh, because several rows
        // say something live ("NOW 12", "40 STACKS") and a level-up row for a
        // character who is not on the roster any more must not survive.
        private readonly Dictionary<string, DebugAction> _actions = new Dictionary<string, DebugAction>();

        // Pushed at the end of every Refresh() (docs/GAMEPAD_NAVIGATION_PLAN.md
        // phase 3, AUDIT.md #158), popped on OnDisable -- Close() below is
        // the only way this screen shuts, mouse or Cancel alike.
        private NavContext _navContext;

        private DebugCategory CurrentCategory => Categories[_categoryIndex];

        private sealed class DebugAction
        {
            public string Label;
            public bool NeedsRun;
            public Action Run;
        }

        private void Start()
        {
            Wire();
        }

        // Repaint on OPEN. The save moves underneath this menu constantly --
        // that is what it is for -- so a stale page would be actively
        // misleading about what a grant did.
        private void OnEnable()
        {
            if (rowButtons != null) Refresh();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            closeButton.onClick.AddListener(Close);

            for (int i = 0; i < categoryButtons.Length; i++)
            {
                int index = i;
                categoryButtons[i].onClick.AddListener(() => SetCategory(index));
            }

            for (int i = 0; i < subFilterButtons.Length; i++)
            {
                int index = i;
                subFilterButtons[i].onClick.AddListener(() => SetSubFilter(index));
            }

            for (int i = 0; i < rowButtons.Length; i++)
            {
                int index = i;
                rowButtons[i].onClick.AddListener(() => PressRow(index));
            }

            prevPageButton.onClick.AddListener(() => StepPage(-1));
            nextPageButton.onClick.AddListener(() => StepPage(1));

            plusMinusButton.onClick.AddListener(() => StepPlus(-1));
            plusPlusButton.onClick.AddListener(() => StepPlus(1));

            for (int i = 0; i < qtyButtons.Length; i++)
            {
                int index = i;
                qtyButtons[i].onClick.AddListener(() => SetQty(index));
            }
        }

        private static SaveData Save => SaveSlotManager.CurrentSave;

        // hasRun, not DescentIsUnderWay: a run whose first room has not been
        // entered yet still has a snapshot to put a book in.
        private static RunSnapshot LiveRun => RunManager.HasRun ? RunManager.Run : null;

        // ONE implementation, shared by the Close button's own click and
        // this context's Cancel handler.
        private void Close() => gameObject.SetActive(false);

        // ---- pressing a row -----------------------------------------------------

        // Public so PlayMode can press a row by index without synthesising a
        // click; the button's own listener calls exactly this.
        public void PressRow(int index)
        {
            var page = DebugMenuCatalog.Page(_filtered, _page);
            if (index < 0 || index >= page.Count) return;

            var item = page[index];
            int qty = QtyValues[_qtyIndex];

            switch (CurrentCategory)
            {
                case DebugCategory.Sets:
                    GrantSet(item, qty);
                    break;
                case DebugCategory.SpellBooks:
                    GrantBook(item, qty);
                    break;
                case DebugCategory.Relics:
                    GrantRelic(item);
                    break;
                case DebugCategory.Resources:
                case DebugCategory.Tools:
                    RunAction(item);
                    break;
                default:
                    GrantItem(item, qty);
                    break;
            }
        }

        // Plus is ignored for consumables -- a potion has no honing axis,
        // InventoryOps keys stacks on plus among other things, and a debug
        // potion minted at +7 would silently split from every other stack of
        // the same potion.
        private void GrantItem(DebugItem item, int qty)
        {
            var save = Save;
            if (save == null) return;

            int plus = item.Kind == (int)ItemKind.Consumable ? 0 : _plus;
            InventoryOps.Add(save.stockpiledItems, item.Id, qty, plus);

            Wrote();
            ToastGranted(qty, item.Name, plus);
        }

        // ONE ROW GRANTS THE WHOLE SET: every ItemDefinition whose setId
        // matches, each at the grant bar's quantity and plus. Read fresh from
        // ContentDatabase rather than trusted from the row, for the same
        // staleness reason Catalogue() is read fresh every Refresh.
        private void GrantSet(DebugItem row, int qty)
        {
            var save = Save;
            if (save == null || string.IsNullOrEmpty(row.SetId)) return;

            var pieces = ContentDatabase.Items.Where(i => i != null && i.setId == row.SetId).ToList();
            if (pieces.Count == 0) return;

            foreach (var piece in pieces)
            {
                int plus = piece.kind == ItemKind.Consumable ? 0 : _plus;
                InventoryOps.Add(save.stockpiledItems, piece.id, qty, plus);
            }

            Wrote();
            ToastGranted(qty, row.Name, _plus);
        }

        // Into the UNASSIGNED pile, never a slot. Placing a book is the
        // dossier's job and has its own refusals (a full row, a pool that
        // reads no books); a debug path that learned the spell directly would
        // skip the half of the feature most worth testing.
        private void GrantBook(DebugItem item, int qty)
        {
            var run = LiveRun;
            if (run == null)
            {
                toastLabel.Set(UiStrings.DebugToastNeedsRun);
                return;
            }

            run.unassignedSpellBooks ??= new List<string>();
            for (int i = 0; i < qty; i++) run.unassignedSpellBooks.Add(item.Id);

            Wrote();
            ToastGranted(qty, item.Name, 0);
        }

        // One copy, whatever the quantity: a relic is a thing a character
        // carries or does not, and FightEncounterAdapter.ResolveRelics has no
        // notion of two of the same. A second press says so rather than
        // stacking a duplicate nothing would read.
        private void GrantRelic(DebugItem item)
        {
            var run = LiveRun;
            if (run == null)
            {
                toastLabel.Set(UiStrings.DebugToastNeedsRun);
                return;
            }

            run.relicIds ??= new List<string>();
            if (!run.relicIds.Contains(item.Id)) run.relicIds.Add(item.Id);

            Wrote();
            ToastGranted(1, item.Name, 0);
        }

        private void RunAction(DebugItem row)
        {
            if (!_actions.TryGetValue(row.Id, out var action)) return;

            if (action.NeedsRun && LiveRun == null)
            {
                toastLabel.Set(UiStrings.DebugToastNeedsRun);
                return;
            }

            // The label BEFORE the action: "+1 LEVEL (NOW 12)" is what was
            // pressed, and the repaint below rewrites the row to "NOW 13".
            string label = action.Label;
            action.Run();

            Wrote();
            toastLabel.Set(UiStrings.DebugToastDone, label);
        }

        private void ToastGranted(int qty, string name, int plus)
        {
            if (plus > 0) toastLabel.Set(UiStrings.DebugToastGranted, qty, name, plus);
            else toastLabel.Set(UiStrings.DebugToastGrantedNoPlus, qty, name);
        }

        // Every write persists, repaints this menu, and tells the host.
        private void Wrote()
        {
            if (Save != null) SaveSlotManager.SaveCurrent();
            Refresh();
            Changed?.Invoke();
        }

        // ---- the Resources and Tools verbs -------------------------------------

        private IEnumerable<DebugItem> Actions(DebugCategory category)
        {
            var built = category == DebugCategory.Resources ? ResourceActions() : ToolActions();

            int order = 0;
            foreach (var action in built)
            {
                // Id is category-qualified so the two tabs cannot collide in
                // the one dictionary; Tier is the row's position, which is
                // what the catalogue's tier-first sort then lays out in.
                string id = $"{category}:{order}";
                _actions[id] = action;
                yield return new DebugItem(id, action.Label, -1, order, category);
                order++;
            }
        }

        private IEnumerable<DebugAction> ResourceActions()
        {
            // Gold lands on the SAVE's wallet, not the run's: that is the pile
            // you spend in the hub, and the one that survives whatever you are
            // about to do to the run. Run gold is its own row for the shop.
            foreach (int amount in GoldGrants)
            {
                yield return Verb(UiStrings.DebugGold.Format(amount), () => Save?.wallet?.Add(CurrencyType.Gold, amount));
            }

            yield return Verb(UiStrings.DebugRunGold.Format(RunGoldGrant), () => LiveRun.gold += RunGoldGrant,
                needsRun: true);

            // Embers are per CHARACTER. The whole roster, not the squad: the
            // point is to test any character's tree without fielding them.
            foreach (int amount in EmberGrants)
            {
                yield return Verb(UiStrings.DebugEmbers.Format(amount), () =>
                {
                    foreach (var character in Roster()) character.embers += amount;
                });
            }

            foreach (var character in Roster())
            {
                var c = character;
                string name = CharacterName(c);
                yield return Verb(UiStrings.DebugLevelUp.Format(name, c.level), () => LevelUp(c));
                yield return Verb(UiStrings.DebugLevelMax.Format(name), () => MaxLevel(c));
            }
        }

        private IEnumerable<DebugAction> ToolActions()
        {
            var save = Save;
            var run = LiveRun;

            yield return Verb(UiStrings.DebugHealParty.Format(), () => RunEncounter.HealPartyToFull(LiveRun),
                needsRun: true);
            yield return Verb(UiStrings.DebugJumpToBoss.Format(), JumpToBoss, needsRun: true);
            yield return Verb(UiStrings.DebugNextLeg.Format(), RunManager.AdvanceLeg, needsRun: true);

            yield return Verb(UiStrings.DebugClearStash.Format(save?.stockpiledItems?.Count ?? 0),
                () => Save?.stockpiledItems?.Clear());
            yield return Verb(UiStrings.DebugClearRunBag.Format(run?.inventory?.Count ?? 0),
                () => LiveRun.inventory?.Clear(), needsRun: true);

            yield return Verb(UiStrings.DebugResetTalents.Format(), () =>
            {
                foreach (var character in Roster()) TalentOps.Respec(character, ContentDatabase.SpentBy(character));
            });
            yield return Verb(UiStrings.DebugResetTracks.Format(), () =>
            {
                foreach (var character in Roster()) ResetTrack(character);
            });

            // EVENT ROOMS. Opening one goes through RunOrchestrator's own
            // door (OpenEventForDebug), which leaves exactly the state a real
            // arrival leaves -- so the choices, the reload and Leave that
            // follow are the game's, not the menu's. The map's Changed
            // handler opens the panel.
            foreach (var definition in ContentDatabase.Events)
            {
                string id = definition?.Data?.Id;
                if (string.IsNullOrEmpty(id)) continue;

                yield return Verb(UiStrings.DebugOpenEvent.Format(id),
                    () => RunOrchestrator.OpenEventForDebug(id), needsRun: true);
            }

            // The counters an "on the Nth time" outcome reads. The add uses
            // the sticky quantity, read at PRESS time, so x10 then one press
            // is the tenth toss's precondition in one step.
            foreach (string counterId in KnownEventCounters(save))
            {
                string counter = counterId;
                int now = save?.EventCounter(counter) ?? 0;
                yield return Verb(UiStrings.DebugCounterAdd.Format(counter, now),
                    () => Save?.AddEventCounter(counter, QtyValues[_qtyIndex]));
                yield return Verb(UiStrings.DebugCounterReset.Format(counter),
                    () => Save?.SetEventCounter(counter, 0));
            }
        }

        // Every counter content names anywhere (an effect or a requirement,
        // at any depth of any event), plus any the save holds that content
        // no longer names -- so a renamed counter can still be reset.
        private static IEnumerable<string> KnownEventCounters(SaveData save)
        {
            var ids = new SortedSet<string>();

            foreach (var definition in ContentDatabase.Events)
            {
                var data = definition?.Data;
                if (data == null) continue;

                AddCounters(ids, data.Requires, null);
                foreach (var page in data.Pages ?? new Domain.Content.ResolvedEventPage[0])
                {
                    foreach (var choice in page?.Choices ?? new Domain.Content.ResolvedEventChoice[0])
                    {
                        if (choice == null) continue;
                        AddCounters(ids, choice.Requires, choice.Effects);
                        foreach (var outcome in choice.Outcomes ?? new Domain.Content.ResolvedEventOutcome[0])
                        {
                            if (outcome != null) AddCounters(ids, outcome.Requires, outcome.Effects);
                        }
                    }
                }
            }

            foreach (var entry in save?.eventCounters ?? new List<EventCounterEntry>())
            {
                if (!string.IsNullOrEmpty(entry?.id)) ids.Add(entry.id);
            }

            return ids;
        }

        private static void AddCounters(SortedSet<string> ids,
            Domain.Events.EventRequirement[] requires, Domain.Events.EventEffect[] effects)
        {
            foreach (var requirement in requires ?? new Domain.Events.EventRequirement[0])
            {
                if (requirement != null && requirement.Kind == Domain.Events.EventRequirementKind.Counter
                    && !string.IsNullOrEmpty(requirement.CounterId)) ids.Add(requirement.CounterId);
            }

            foreach (var effect in effects ?? new Domain.Events.EventEffect[0])
            {
                if (effect != null && effect.Kind == Domain.Events.EventEffectKind.Counter
                    && !string.IsNullOrEmpty(effect.CounterId)) ids.Add(effect.CounterId);
            }
        }

        private static DebugAction Verb(string label, Action run, bool needsRun = false) =>
            new DebugAction { Label = label, NeedsRun = needsRun, Run = run };

        private static IEnumerable<Character> Roster() =>
            Save?.roster?.Where(c => c != null) ?? Enumerable.Empty<Character>();

        private static string CharacterName(Character character)
        {
            string name = ContentDatabase.GetCharacter(character.definitionId)?.Data?.DisplayName;
            return string.IsNullOrEmpty(name) ? character.definitionId : name;
        }

        // Exactly the experience to the next level, through AddExperience, so
        // the cap and every level-up rule stay in the one place they live.
        // The reward track is NOT claimed: collecting is manual on purpose,
        // and a debug level that auto-collected would hide the thing a level
        // grant is usually for.
        private static void LevelUp(Character character)
        {
            var costs = ContentDatabase.LevelCosts;
            if (character.level >= LevelCurve.MaxLevel(costs)) return;

            int owed = LevelCurve.ExpToNextLevel(costs, character.level) - character.exp;
            character.AddExperience(Math.Max(1, owed));
        }

        // A level at a time rather than one lump, so a table with a zero or a
        // sentinel cost in it stops the loop instead of spinning it: each
        // pass must move the level or the loop ends.
        private static void MaxLevel(Character character)
        {
            int cap = LevelCurve.MaxLevel(ContentDatabase.LevelCosts);
            while (character.level < cap)
            {
                int before = character.level;
                LevelUp(character);
                if (character.level == before) break;
            }
        }

        // The track back to waiting, and the points it paid taken back with
        // it -- the same three fields SaveData's own track migration resets,
        // for the same reason: points that came off the track cannot stay
        // spent once the claim that paid them is unwound. Talents, embers,
        // level and equipment are untouched.
        //
        // Invested points move max health (Constitution), so the run's
        // carried health is rescaled against the new maximum, the rule
        // RunEncounter.ScaleCarriedHealth exists to keep.
        private static void ResetTrack(Character character)
        {
            int previousMax = ContentDatabase.EffectiveStats(character).maxHealth;

            character.claimedTrackLevel = 0;
            character.unspentStatPoints = 0;
            character.investedAbilityScores = default;

            RunEncounter.ScaleCarriedHealth(character, previousMax);
        }

        // Stands the party on a room one step before the boss, so the boss is
        // the next choice on the map. Only the map's own adjacency decides
        // which room that is -- the same authority MoveTo defers to -- and
        // the step is written the way MoveTo writes it, absolute.
        private static void JumpToBoss()
        {
            var map = RunManager.Map;
            var run = LiveRun;
            var boss = map?.Boss;
            if (map == null || run == null || boss == null) return;

            var before = map.Nodes.FirstOrDefault(n => map.CanMove(n.Id, boss.Id));
            if (before == null) return;

            run.currentNodeId = before.Id;
            run.step = run.legStartStep + before.Depth;
            if (run.step > run.deepestStep) run.deepestStep = run.step;
            if (!run.clearedNodeIds.Contains(before.Id)) run.clearedNodeIds.Add(before.Id);
        }

        // ---- input -----------------------------------------------------------------

        private void SetCategory(int index)
        {
            _categoryIndex = index < 0 || index >= Categories.Length ? 0 : index;

            // Back to All and page one: a chip or a page number that meant
            // something under the last category can mean something else, or
            // nothing, under this one.
            _subFilterButtonIndex = 0;
            _page = 0;
            Refresh();
        }

        private void SetSubFilter(int buttonIndex)
        {
            _subFilterButtonIndex = buttonIndex;
            _page = 0;
            Refresh();
        }

        private void StepPage(int direction)
        {
            _page = DebugMenuCatalog.ClampPage(_page + direction, _filtered.Count);
            Refresh();
        }

        private void StepPlus(int direction)
        {
            _plus = DebugMenuCatalog.StepPlus(_plus, direction);
            Refresh();
        }

        private void SetQty(int index)
        {
            _qtyIndex = index < 0 || index >= QtyValues.Length ? 0 : index;
            Refresh();
        }

        // The values a category's sub-filter chips stand for, "All" aside.
        // Empty means no chip row at all.
        private static IReadOnlyList<int> SubFilterValuesFor(DebugCategory category)
        {
            switch (category)
            {
                case DebugCategory.Weapons: return ItemTiers;
                case DebugCategory.Equipment: return EquipmentSlots.Select(s => (int)s).ToArray();
                case DebugCategory.SpellBooks: return BookTiers;
                default: return Array.Empty<int>();
            }
        }

        private int EffectiveSubFilter()
        {
            var values = SubFilterValuesFor(CurrentCategory);
            int index = _subFilterButtonIndex - 1;
            return index < 0 || index >= values.Count ? DebugMenuCatalog.SubFilterAll : values[index];
        }

        // ---- painting ----------------------------------------------------------------

        public void Refresh()
        {
            var category = CurrentCategory;
            _filtered = DebugMenuCatalog.Filter(Catalogue(category), category, EffectiveSubFilter());
            _page = DebugMenuCatalog.ClampPage(_page, _filtered.Count);

            pageLabel.Set(UiStrings.DebugPage, _page + 1, DebugMenuCatalog.PageCount(_filtered.Count));
            plusLabel.Set(UiStrings.DebugPlusValue, _plus);

            for (int i = 0; i < categoryButtons.Length; i++)
            {
                // Themed(Silver): targetGraphic is the plate, so a direct
                // colour write here would fight ThemedButtonState's own
                // idle/hover/press tint.
                ThemedButtonState.ApplySelection(categoryButtons[i], i == _categoryIndex);
            }

            PaintSubFilterRow(category);

            for (int i = 0; i < qtyButtons.Length; i++)
            {
                ThemedButtonState.ApplySelection(qtyButtons[i], i == _qtyIndex);
            }

            var page = DebugMenuCatalog.Page(_filtered, _page);
            bool tiered = !DebugMenuCatalog.IsAction(category) && category != DebugCategory.Relics;

            for (int i = 0; i < rowButtons.Length; i++)
            {
                bool present = i < page.Count;

                // Hidden rather than drawn blank, same rule as the bag: a
                // half-full list of empty boxes reads as a load failure.
                rowButtons[i].gameObject.SetActive(present);
                if (!present) continue;

                if (tiered) rowLabels[i].Set(UiStrings.DebugRow, page[i].Tier, page[i].Name);
                else rowLabels[i].Set(UiStrings.DebugRowPlain, page[i].Name);
            }

            RefreshNavigation();
        }

        private void PaintSubFilterRow(DebugCategory category)
        {
            var values = SubFilterValuesFor(category);
            int count = values.Count == 0 ? 0 : values.Count + 1;

            for (int i = 0; i < subFilterButtons.Length; i++)
            {
                bool present = i < count;
                subFilterButtons[i].gameObject.SetActive(present);
                if (!present) continue;

                if (i == 0) subFilterLabels[i].Set(UiStrings.DebugSubFilterAll);
                else if (category == DebugCategory.Equipment) subFilterLabels[i].Set(EquipmentSlotCaptions[i - 1]);
                else subFilterLabels[i].Set(UiStrings.DebugTierChip, values[i - 1]);

                ThemedButtonState.ApplySelection(subFilterButtons[i], i == _subFilterButtonIndex);
            }
        }

        // SIX GROUPS, LINKED by real screen position (DebugMenuScreen's own
        // Place.At coordinates): the category rail down the left, the chips
        // across the top of the list, the two-column list, the pager, the
        // grant bar, and Close. Chained with explicit links rather than
        // folded into one group that would have to pretend differently shaped
        // rows are the same group. Rebuilt every Refresh() because which
        // rows and chips are present changes with the category and the page.
        private void RefreshNavigation()
        {
            var activeSubFilters = subFilterButtons?.Where(b => b != null && b.gameObject.activeSelf).ToList()
                                    ?? new List<Button>();
            var activeRows = rowButtons?.Where(r => r != null && r.gameObject.activeSelf).ToList()
                             ?? new List<Button>();

            var grantBar = new[] { plusMinusButton, plusPlusButton }
                .Concat(qtyButtons ?? Array.Empty<Button>())
                .Where(b => b != null)
                .ToArray();

            var firstCategory = categoryButtons != null && categoryButtons.Length > 0 ? categoryButtons[0] : null;
            var firstSubFilter = activeSubFilters.Count > 0 ? activeSubFilters[0] : null;
            var firstRow = activeRows.Count > 0 ? activeRows[0] : null;
            var lastRow = activeRows.Count > 0 ? activeRows[activeRows.Count - 1] : null;
            var firstGrant = grantBar.Length > 0 ? grantBar[0] : null;
            var lastGrant = grantBar.Length > 0 ? grantBar[grantBar.Length - 1] : null;

            // EVERY TAB steps Right into the content, and Left out of it lands
            // on the tab being shown. Only the first tab used to carry the
            // Right link, so from any other tab a pad had to climb back to
            // WEAPONS before it could reach a single row.
            var currentCategory = categoryButtons != null && _categoryIndex < categoryButtons.Length
                ? categoryButtons[_categoryIndex]
                : firstCategory;
            var content = firstSubFilter ?? firstRow ?? prevPageButton;

            var links = new List<UiNavLink<Selectable>?>
            {
                RuntimeNavWiring.Link(firstSubFilter ?? firstRow, UiNavDirection.Left, currentCategory),
                RuntimeNavWiring.Link(firstSubFilter, UiNavDirection.Down, firstRow ?? prevPageButton),
                RuntimeNavWiring.Link(firstRow, UiNavDirection.Up, firstSubFilter),
                RuntimeNavWiring.Link(lastRow, UiNavDirection.Down, prevPageButton),
                RuntimeNavWiring.Link(prevPageButton, UiNavDirection.Up, lastRow ?? firstSubFilter),
                RuntimeNavWiring.Link(prevPageButton, UiNavDirection.Down, firstGrant ?? closeButton),
                RuntimeNavWiring.Link(nextPageButton, UiNavDirection.Down, firstGrant ?? closeButton),
                RuntimeNavWiring.Link(firstGrant, UiNavDirection.Up, prevPageButton),
                RuntimeNavWiring.Link(lastGrant, UiNavDirection.Down, closeButton),
                RuntimeNavWiring.Link(closeButton, UiNavDirection.Up, lastGrant ?? prevPageButton),
            };

            foreach (var category in categoryButtons ?? Array.Empty<Button>())
            {
                links.Add(RuntimeNavWiring.Link(category, UiNavDirection.Right, content));
            }

            var groups = new List<UiNavGroup<Selectable>>
            {
                // List, not Rail: the category rail is a VERTICAL column, and
                // Rail means horizontal Left/Right in this codebase -- the
                // same choice GlossaryController makes for its vertical rail.
                RuntimeNavWiring.Group("debugCategories", UiNavGroupKind.List, categoryButtons),
            };
            if (activeSubFilters.Count > 0)
            {
                groups.Add(RuntimeNavWiring.Group("debugSubFilters", UiNavGroupKind.Rail, activeSubFilters));
            }
            // Grid, not List: the list is two columns laid out row-major
            // (DebugMenuScreen.RowButtons' own comment), so Left/Right steps
            // between columns -- ShopController's gear grid does the same.
            groups.Add(RuntimeNavWiring.Group("debugRows", UiNavGroupKind.Grid, activeRows,
                DebugMenuCatalog.Columns, wrap: UiNavWrap.Clamp));
            groups.Add(RuntimeNavWiring.Group("debugPager", UiNavGroupKind.Rail,
                new[] { prevPageButton, nextPageButton }));
            groups.Add(RuntimeNavWiring.Group("debugGrantBar", UiNavGroupKind.Rail, grantBar));

            RuntimeNavWiring.Apply(groups, links);

            var selectables = new Dictionary<string, object>();
            foreach (var button in categoryButtons ?? Array.Empty<Button>())
            {
                if (button != null) selectables[button.name] = button.gameObject;
            }
            foreach (var button in activeSubFilters) selectables[button.name] = button.gameObject;
            foreach (var row in activeRows) selectables[row.name] = row.gameObject;
            if (prevPageButton != null) selectables[prevPageButton.name] = prevPageButton.gameObject;
            if (nextPageButton != null) selectables[nextPageButton.name] = nextPageButton.gameObject;
            foreach (var button in grantBar) selectables[button.name] = button.gameObject;
            if (closeButton != null) selectables[closeButton.name] = closeButton.gameObject;

            object entry = firstCategory != null ? firstCategory.gameObject
                : closeButton != null ? closeButton.gameObject : (object)null;

            if (_navContext == null) _navContext = new NavContext(entry, selectables, cancel: Close);
            else _navContext.Reconfigure(entry, selectables);

            // PushIfAbsent, not Push: this context outlives its time on the
            // stack (see OnDisable), so re-opening the menu puts the SAME
            // instance back rather than building a new one, and a repaint
            // while it is already top must not stack a second copy.
            NavigationInputModule.Contexts?.PushIfAbsent(_navContext);
        }

        private void OnDisable()
        {
            if (_navContext == null) return;

            NavigationInputModule.Contexts?.Remove(_navContext);
            // _navContext is NOT nulled. It carries this screen's focus
            // memory (plan section 4: "remembered focus by stable id"), and a
            // modal that can be re-opened has to survive being closed for
            // that memory to mean anything -- a context recreated on the next
            // open would land on its entry every single time, which is the
            // whole of AUDIT.md #163's second half.
        }

        // ---- the catalogue -------------------------------------------------------------

        // Read fresh each Refresh rather than cached at Start.
        //
        // ContentDatabase is loaded from Resources, and in the editor a
        // content rebuild can replace it underneath a running scene. A debug
        // menu holding a stale catalogue across exactly that operation would
        // be worse than useless.
        //
        // Only the CURRENT category is built: the Resources rows read the
        // roster and the Tools rows the run, and neither is worth doing for a
        // tab nobody is looking at.
        private IEnumerable<DebugItem> Catalogue(DebugCategory category)
        {
            _actions.Clear();

            switch (category)
            {
                case DebugCategory.Sets: return SetRows();
                case DebugCategory.SpellBooks: return BookRows();
                case DebugCategory.Relics: return RelicRows();
                case DebugCategory.Resources:
                case DebugCategory.Tools:
                    return Actions(category).ToList();
                default: return ItemRows(category);
            }
        }

        private static IEnumerable<DebugItem> ItemRows(DebugCategory category)
        {
            var kind = category == DebugCategory.Weapons ? ItemKind.Weapon
                : category == DebugCategory.Equipment ? ItemKind.Equipment
                : ItemKind.Consumable;

            return ContentDatabase.Items
                .Where(i => i != null && i.kind == kind)
                .Select(i => new DebugItem(i.id, DisplayName(i), (int)i.kind, i.tier, category,
                    subKey: kind == ItemKind.Equipment ? (int)i.equipSlot : DebugMenuCatalog.SubFilterAll));
        }

        // One row per setId, not per piece. Kind -1: GrantSet reads each
        // piece's own kind, never the row's.
        private static IEnumerable<DebugItem> SetRows() =>
            ContentDatabase.Items
                .Where(i => i != null && i.IsSetPiece)
                .GroupBy(i => i.setId)
                .Select(set => new DebugItem(set.Key, set.Key, -1, set.Min(i => i.tier), DebugCategory.Sets,
                    setId: set.Key));

        // Every skill with a book tier -- the set SaveData.Reconcile lets
        // stay in unassignedSpellBooks, and so the widest set that can be put
        // there without being pruned on the next load. Wider than the shop's
        // shelf on purpose: the shelf drops books the squad already knows,
        // and "what happens when I get a duplicate" is a thing to test.
        private static IEnumerable<DebugItem> BookRows() =>
            ContentDatabase.Skills
                .Where(s => s != null && s.Data != null && s.Data.BookTier > 0)
                .Select(s => new DebugItem(s.id,
                    string.IsNullOrEmpty(s.Data.DisplayName) ? s.id : s.Data.DisplayName,
                    -1, s.Data.BookTier, DebugCategory.SpellBooks));

        private static IEnumerable<DebugItem> RelicRows() =>
            ContentDatabase.Relics
                .Where(r => r != null && r.Data != null)
                .Select(r => new DebugItem(r.Data.Id,
                    string.IsNullOrEmpty(r.Data.DisplayName) ? r.Data.Id : r.Data.DisplayName,
                    -1, 0, DebugCategory.Relics));

        private static string DisplayName(ItemDefinition item) =>
            string.IsNullOrEmpty(item.displayName) ? item.id : item.displayName;
    }
}

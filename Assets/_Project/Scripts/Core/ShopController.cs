using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace
{
    // The shop, driven. Mirrors RelicDraftController's shape deliberately --
    // Open()/Close(), a Finished event, one Paint() that repaints everything
    // from the run rather than patching individual widgets -- because both
    // screens answer the same question ("what does this overlay look like
    // right now, given the run") and a second answer style would be a second
    // thing to keep consistent with the first.
    //
    // SINGLE GLOBAL SELECTION (docs/PLAN_SHOP.md §7.1 point 6): one card is
    // selected at a time, across every section. A second press on it, or
    // BUY, commits. Unaffordable cards stay selectable for inspection --
    // only BUY can refuse, and it refuses through RunOrchestrator's own
    // ShopResult rather than a client-side guess at affordability.
    public class ShopController : MonoBehaviour
    {
        [SerializeField] internal TMP_Text goldLabel;
        [SerializeField] internal Button leaveButton;
        [SerializeField] internal TMP_Text leaveButtonLabel;
        [SerializeField] internal TMP_Text detailLabel;
        [SerializeField] internal Button buyButton;
        [SerializeField] internal Button packButton;

        // The card art, and the two lookup tables it comes from. Items and
        // relics author an iconPath; skills do not, so a book card's slot is
        // painted from a null table and ItemIcons.Apply hides the Image --
        // which is the same graceful-degradation path a gear card with no
        // authored art already takes, not a special case for books.
        [SerializeField] internal IconEntry[] itemArt;
        [SerializeField] internal IconEntry[] relicArt;

        [SerializeField] internal Button gearReroll;
        [SerializeField] internal TMP_Text gearRerollLabel;
        [SerializeField] internal Button[] gearCards;
        [SerializeField] internal Image[] gearIcons;
        [SerializeField] internal TMP_Text[] gearNames;
        [SerializeField] internal TMP_Text[] gearMetas;
        [SerializeField] internal TMP_Text[] gearPrices;

        [SerializeField] internal Button bookReroll;
        [SerializeField] internal TMP_Text bookRerollLabel;
        [SerializeField] internal Button[] bookCards;
        [SerializeField] internal Image[] bookIcons;
        [SerializeField] internal TMP_Text[] bookNames;
        [SerializeField] internal TMP_Text[] bookMetas;
        [SerializeField] internal TMP_Text[] bookPrices;

        [SerializeField] internal Button relicReroll;
        [SerializeField] internal TMP_Text relicRerollLabel;
        [SerializeField] internal Button[] relicCards;
        [SerializeField] internal Image[] relicIcons;
        [SerializeField] internal TMP_Text[] relicNames;
        [SerializeField] internal TMP_Text[] relicMetas;
        [SerializeField] internal TMP_Text[] relicPrices;

        [SerializeField] internal GameObject packRoot;
        [SerializeField] internal Button packCloseButton;
        [SerializeField] internal GameObject packEmptyHint;
        [SerializeField] internal Button packPrevButton;
        [SerializeField] internal Button packNextButton;
        [SerializeField] internal TMP_Text packPageLabel;
        [SerializeField] internal GameObject[] packRows;
        [SerializeField] internal TMP_Text[] packNames;
        [SerializeField] internal TMP_Text[] packMetas;
        [SerializeField] internal TMP_Text[] packPrices;
        [SerializeField] internal Button[] packSellOneButtons;
        [SerializeField] internal Button[] packSellAllButtons;

        // -1, -1 is "nothing selected". Section indices match ShopStock's
        // (GearSection/BookSection/RelicSection).
        private int _selectedSection = -1;
        private int _selectedIndex = -1;

        private bool _leaveArmed;
        private bool _packOpen;
        private int _packPage;
        private bool _wired;

        // WHY THE LAST MUTATION DID NOTHING, or default when it did something.
        //
        // A UiString rather than a ShopResult: what the screen holds is the
        // line it is showing, and keeping the result instead would leave two
        // places (here and PaintDetail) deciding what a refusal means. Cleared
        // when another card is selected, when the pack opens and when the shop
        // does -- a refusal is about one press and outlives nothing else.
        private UiString _refusal;

        // Raised when LEAVE actually leaves. The map owns navigation, the
        // same reason the draft's Finished exists.
        public System.Action Finished;

        public void Open()
        {
            gameObject.SetActive(true);
            Wire();

            _selectedSection = -1;
            _selectedIndex = -1;
            _leaveArmed = false;
            _packOpen = false;
            _packPage = 0;
            _refusal = default;
            if (packRoot != null) packRoot.SetActive(false);

            RegisterNavContext();
            Paint();
        }

        private void OnDisable()
        {
            PopNavContext();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            WireSection(gearCards, ShopStock.GearSection);
            WireSection(bookCards, ShopStock.BookSection);
            WireSection(relicCards, ShopStock.RelicSection);

            if (gearReroll != null) gearReroll.onClick.AddListener(() => Reroll(ShopStock.GearSection));
            if (bookReroll != null) bookReroll.onClick.AddListener(() => Reroll(ShopStock.BookSection));
            if (relicReroll != null) relicReroll.onClick.AddListener(() => Reroll(ShopStock.RelicSection));

            if (buyButton != null) buyButton.onClick.AddListener(Buy);
            if (packButton != null) packButton.onClick.AddListener(OpenPack);
            if (leaveButton != null) leaveButton.onClick.AddListener(Leave);

            if (packCloseButton != null) packCloseButton.onClick.AddListener(ClosePack);
            if (packPrevButton != null) packPrevButton.onClick.AddListener(() => StepPackPage(-1));
            if (packNextButton != null) packNextButton.onClick.AddListener(() => StepPackPage(1));

            for (int i = 0; i < packRows.Length; i++)
            {
                int index = i;
                if (packSellOneButtons != null && index < packSellOneButtons.Length && packSellOneButtons[index] != null)
                    packSellOneButtons[index].onClick.AddListener(() => SellRow(index, all: false));
                if (packSellAllButtons != null && index < packSellAllButtons.Length && packSellAllButtons[index] != null)
                    packSellAllButtons[index].onClick.AddListener(() => SellRow(index, all: true));
            }
        }

        private void WireSection(Button[] cards, int section)
        {
            if (cards == null) return;
            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;
                if (cards[i] != null) cards[i].onClick.AddListener(() => Select(section, index));
            }
        }

        // ---- selection and purchase -------------------------------------------

        private void Select(int section, int index)
        {
            var entry = EntryAt(section, index);
            if (entry == null || entry.noOffer || entry.sold) return;

            bool sameCard = _selectedSection == section && _selectedIndex == index;
            if (sameCard)
            {
                Commit(section, index);
                return;
            }

            _selectedSection = section;
            _selectedIndex = index;
            _leaveArmed = false;
            _refusal = default;
            Paint();
        }

        // ---- saying why a mutation did nothing (AUDIT #115) ------------------
        //
        // The one place a ShopResult becomes words. Commit, Reroll and SellRow
        // each hand their result here instead of discarding it, and PaintDetail
        // shows whatever is standing rather than the selected card's
        // description -- which is what makes "cleared on the next selection"
        // the whole of the clearing rule.
        //
        // NOTHING IS SAID ABOUT A SUCCESS. A card that turns SOLD and a purse
        // that drops say it already, and a line confirming what the screen just
        // showed is noise the next refusal has to be read through.
        private void PaintRefusal(ShopResult result)
        {
            if (result.Outcome == ShopOutcome.Ok)
            {
                _refusal = default;
                return;
            }

            _refusal = result.Outcome == ShopOutcome.AppliedNotPersisted
                ? UiStrings.ShopAppliedNotPersisted
                : result.Reason == ShopRefusal.NotEnoughGold
                    ? UiStrings.ShopRefusedNotEnoughGold
                    : UiStrings.ShopRefusedGeneric;
        }

        private void Buy()
        {
            if (_selectedSection < 0) return;
            Commit(_selectedSection, _selectedIndex);
        }

        private void Commit(int section, int index)
        {
            ShopResult result;
            switch (section)
            {
                case ShopStock.GearSection:
                    result = RunOrchestrator.BuyGear(index);
                    break;
                case ShopStock.RelicSection:
                    result = RunOrchestrator.BuyRelic(index);
                    break;
                default:
                    result = RunOrchestrator.BuyBook(index);
                    break;
            }

            if (result.Applied)
            {
                _selectedSection = -1;
                _selectedIndex = -1;
            }

            PaintRefusal(result);
            _leaveArmed = false;
            Paint();
        }

        private void Reroll(int section)
        {
            // KEPT, not discarded. A reroll is a purchase like any other and
            // refuses NotEnoughGold the same way -- the interactable gate on
            // the button is a hint, not the decision.
            var result = RunOrchestrator.RerollSection(section);

            // A reroll replaces the whole shelf, so a selection into it is
            // now pointing at a card that may not exist any more.
            if (_selectedSection == section)
            {
                _selectedSection = -1;
                _selectedIndex = -1;
            }

            PaintRefusal(result);
            _leaveArmed = false;
            Paint();
        }

        // ---- leaving --------------------------------------------------------

        private void Leave()
        {
            if (!_leaveArmed)
            {
                _leaveArmed = true;
                Paint();
                return;
            }

            RunOrchestrator.LeaveShop();
            gameObject.SetActive(false); // triggers OnDisable's PopNavContext
            Finished?.Invoke();
        }

        // ---- the pack modal ---------------------------------------------------

        private void OpenPack()
        {
            _packOpen = true;
            _packPage = 0;

            // The pack modal owns selection while open (§7.1 point 6) -- the
            // main screen's own selection is cleared, not merely hidden
            // behind the modal, so re-opening the main screen never finds a
            // stale CONFIRM on a card the player was not looking at.
            _selectedSection = -1;
            _selectedIndex = -1;
            _leaveArmed = false;
            _refusal = default;

            if (packRoot != null) packRoot.SetActive(true);
            Paint();
        }

        private void ClosePack()
        {
            _packOpen = false;

            // A sell's refusal belongs to the pack it was pressed in. Carrying
            // it out onto the shelf would be a line about a row nothing on
            // screen is showing any more.
            _refusal = default;

            if (packRoot != null) packRoot.SetActive(false);
            Paint();
        }

        private void StepPackPage(int direction)
        {
            int count = PackPageCount();
            int next = Mathf.Clamp(_packPage + direction, 0, count - 1);
            if (next == _packPage) return;
            _packPage = next;
            PaintPack();
        }

        private void SellRow(int rowIndex, bool all)
        {
            var save = SaveSlotManager.CurrentSave;
            if (save?.stockpiledItems == null) return;

            int absolute = _packPage * ShopScreen.PackRowCount + rowIndex;
            if (absolute < 0 || absolute >= save.stockpiledItems.Count) return;

            var entry = save.stockpiledItems[absolute];
            if (entry == null) return;

            int quantity = all ? entry.count : 1;

            // KEPT for the same reason Reroll's is. A sell can refuse NotInBag
            // -- SellPriceOf answers 0 for an item the catalogue no longer has
            // and the row stays pressable in the SELL ALL column -- and that
            // refusal used to be thrown away here.
            var result = RunOrchestrator.Sell(absolute, quantity);
            PaintRefusal(result);

            // Selling can drop the page below its own content (the last
            // item on a page sold away), so re-clamp before painting.
            int count = PackPageCount();
            if (_packPage >= count) _packPage = Mathf.Max(0, count - 1);

            Paint();
        }

        private int PackPageCount()
        {
            var save = SaveSlotManager.CurrentSave;
            int items = save?.stockpiledItems?.Count ?? 0;
            return Mathf.Max(1, (items + ShopScreen.PackRowCount - 1) / ShopScreen.PackRowCount);
        }

        // ---- painting ---------------------------------------------------------

        private void Paint()
        {
            var run = RunManager.Run;
            if (run == null) return;

            if (goldLabel != null) goldLabel.Set(UiStrings.ShopGold, run.gold);
            if (leaveButtonLabel != null)
                leaveButtonLabel.Set(_leaveArmed ? UiStrings.ShopLeaveConfirm : UiStrings.ShopLeave);

            PaintSection(ShopStock.GearSection, gearCards, gearIcons, itemArt, gearNames, gearMetas,
                gearPrices, gearReroll, gearRerollLabel);
            PaintSection(ShopStock.BookSection, bookCards, bookIcons, null, bookNames, bookMetas,
                bookPrices, bookReroll, bookRerollLabel);
            PaintSection(ShopStock.RelicSection, relicCards, relicIcons, relicArt, relicNames, relicMetas,
                relicPrices, relicReroll, relicRerollLabel);

            PaintDetail(run);
            PaintPack();
            RefreshNavigation();
        }

        private void PaintSection(int section, Button[] cards, Image[] icons, IconEntry[] art,
            TMP_Text[] names, TMP_Text[] metas, TMP_Text[] prices, Button reroll, TMP_Text rerollLabel)
        {
            var run = RunManager.Run;
            if (run == null || cards == null) return;

            for (int i = 0; i < cards.Length; i++)
            {
                var entry = EntryAt(section, i);
                bool selected = _selectedSection == section && _selectedIndex == i;

                if (entry == null || entry.noOffer)
                {
                    if (names != null && i < names.Length) names[i].SetContent("");
                    if (metas != null && i < metas.Length) metas[i].SetContent("");
                    if (prices != null && i < prices.Length) prices[i].Set(UiStrings.ShopCardNoOffer);
                    if (icons != null && i < icons.Length) ItemIcons.Apply(icons[i], art, null);
                    continue;
                }

                if (icons != null && i < icons.Length) ItemIcons.Apply(icons[i], art, entry.contentId);

                var (name, meta) = DescribeEntry(entry);
                if (names != null && i < names.Length) names[i].SetContent(name);
                if (metas != null && i < metas.Length) metas[i].SetContent(meta);

                if (prices == null || i >= prices.Length) continue;

                if (entry.sold)
                {
                    prices[i].Set(UiStrings.ShopCardSold);
                }
                else if (selected)
                {
                    prices[i].Set(UiStrings.ShopCardConfirm, entry.price);
                }
                else if (run.gold < entry.price)
                {
                    prices[i].Set(UiStrings.ShopCardNeed, entry.price - run.gold);
                }
                else
                {
                    prices[i].Set(UiStrings.ShopCardPrice, entry.price);
                }
            }

            int rerollPrice = RunOrchestrator.RerollPriceFor(section);
            bool canReroll = run.gold >= rerollPrice;
            if (reroll != null) reroll.interactable = canReroll;
            if (rerollLabel != null)
            {
                if (canReroll) rerollLabel.Set(UiStrings.ShopReroll, rerollPrice);
                else rerollLabel.Set(UiStrings.ShopRerollNeed, rerollPrice - run.gold);
            }
        }

        private (string name, string meta) DescribeEntry(ShopStockEntry entry)
        {
            switch (entry.kind)
            {
                case ShopEntryKind.Gear:
                {
                    var item = ContentDatabase.GetItem(entry.contentId);
                    string name = item?.displayName ?? entry.contentId;
                    string meta = UiStrings.ShopGearMeta.Format(item?.tier ?? 0, entry.plus,
                        entry.modifiers?.Count ?? 0);
                    return (name, meta);
                }
                case ShopEntryKind.Relic:
                {
                    var relic = ContentDatabase.GetRelic(entry.contentId);
                    string name = relic?.Data.DisplayName ?? entry.contentId;
                    string meta = relic == null
                        ? ""
                        : UiStrings.DraftRarity.Format(RelicRarityNames.Of(relic.Data.Rarity));
                    return (name, meta);
                }
                default:
                {
                    var skill = ContentDatabase.GetSkill(entry.contentId);
                    string name = skill?.Data.DisplayName ?? entry.contentId;
                    string meta = BookFactLine(entry.contentId);
                    return (name, meta);
                }
            }
        }

        private void PaintDetail(RunSnapshot run)
        {
            if (detailLabel == null) return;

            // AHEAD OF THE DESCRIPTION, and ahead of the pack check too: a
            // sell is refused from inside the pack modal, and a refusal
            // nobody can see is what #115 was.
            if (_refusal.IsValid)
            {
                detailLabel.Set(_refusal);
                return;
            }

            if (_packOpen || _selectedSection < 0)
            {
                detailLabel.Set(UiStrings.ShopDetailEmpty);
                return;
            }

            var entry = EntryAt(_selectedSection, _selectedIndex);
            if (entry == null)
            {
                detailLabel.Set(UiStrings.ShopDetailEmpty);
                return;
            }

            string description = entry.kind switch
            {
                ShopEntryKind.Gear => ContentDatabase.GetItem(entry.contentId)?.description ?? "",
                ShopEntryKind.Relic => ContentDatabase.GetRelic(entry.contentId)?.Data.Description ?? "",
                _ => ContentDatabase.GetSkill(entry.contentId)?.Data.Description ?? "",
            };

            var (name, meta) = DescribeEntry(entry);
            detailLabel.SetContent(string.IsNullOrEmpty(description)
                ? $"{name} - {meta}"
                : $"{name} - {meta} - {description}");
        }

        private void PaintPack()
        {
            var save = SaveSlotManager.CurrentSave;
            var bag = save?.stockpiledItems ?? new List<InventoryEntry>();

            if (packEmptyHint != null) packEmptyHint.SetActive(bag.Count == 0);

            int pageCount = PackPageCount();
            if (_packPage >= pageCount) _packPage = Mathf.Max(0, pageCount - 1);
            bool paged = pageCount > 1;

            if (packPrevButton != null)
            {
                packPrevButton.gameObject.SetActive(paged);
                packPrevButton.interactable = _packPage > 0;
            }

            if (packNextButton != null)
            {
                packNextButton.gameObject.SetActive(paged);
                packNextButton.interactable = _packPage < pageCount - 1;
            }

            if (packPageLabel != null)
            {
                packPageLabel.gameObject.SetActive(paged);
                if (paged) packPageLabel.Set(UiStrings.ShopPackPage, _packPage + 1, pageCount);
            }

            for (int i = 0; i < packRows.Length; i++)
            {
                int absolute = _packPage * ShopScreen.PackRowCount + i;
                bool present = absolute < bag.Count;

                if (packRows[i] != null) packRows[i].SetActive(present);
                if (!present) continue;

                var entry = bag[absolute];
                var item = ContentDatabase.GetItem(entry.itemId);

                if (packNames != null && i < packNames.Length)
                    packNames[i].SetContent(item?.displayName ?? entry.itemId);

                if (packMetas != null && i < packMetas.Length)
                    packMetas[i].Set(UiStrings.ShopGearMeta, item?.tier ?? 0, entry.plus,
                        entry.modifierIds?.Count ?? 0);

                int sellPrice = RunOrchestrator.SellPriceOf(entry);
                if (packPrices != null && i < packPrices.Length)
                    packPrices[i].Set(UiStrings.ShopSellPriceLabel, sellPrice);

                if (packSellOneButtons != null && i < packSellOneButtons.Length && packSellOneButtons[i] != null)
                    packSellOneButtons[i].interactable = sellPrice > 0;

                if (packSellAllButtons != null && i < packSellAllButtons.Length && packSellAllButtons[i] != null)
                {
                    bool stack = entry.count > 1;
                    packSellAllButtons[i].gameObject.SetActive(stack);
                    if (stack)
                    {
                        var label = packSellAllButtons[i].GetComponentInChildren<TMP_Text>();
                        if (label != null) label.Set(UiStrings.ShopSellAllButton, entry.count);
                    }
                }
            }
        }

        // A book card's meta line, in priority order (§7.1 point 4): the shop
        // has no per-character context to badge against, but it does know
        // the run's own state, and showing what that already says costs
        // nothing extra to roll. A skill known by EVERY fielded character
        // never reaches this card at all (excluded at roll time, §2d), so
        // that case does not need handling here.
        private static string BookFactLine(string skillId)
        {
            var run = RunManager.Run;
            var save = SaveSlotManager.CurrentSave;
            var squad = save?.ActiveSquad() ?? new List<Character>();
            if (run == null || squad.Count == 0) return "";

            var learned = run.learnedSpells ?? new List<LearnedSpellEntry>();
            var knownBy = squad.Where(c => c != null
                && learned.Exists(e => e != null && e.characterId == c.definitionId && e.skillId == skillId)).ToList();

            // ELIGIBLE MEANS "COULD ACTUALLY END UP HOLDING THIS", which is
            // two facts now (plan P6, gate 2): does not already know it, AND
            // can hold a book at all. Both lines below read this list -- the
            // all-slots-full line and the "for N of M" count -- and both are
            // lies if it counts somebody whose pool refuses books: the first
            // would say "every slot is full" of a character with no slots,
            // and the second would offer the book to a party member who can
            // never take it.
            var eligible = squad
                .Where(c => c != null && !knownBy.Contains(c) && ContentDatabase.CanHoldSpellBooks(c.definitionId))
                .ToList();

            // Every eligible (doesn't-already-know-it) fielded character is
            // full. Not "nobody can ever place it" -- a replace still can --
            // but the honest read at a glance, same as the shop card's own
            // "unaffordable" state naming the fact rather than hiding it.
            bool allFull = eligible.Count > 0 && eligible.All(c => RunOrchestrator.CanLearn(c.definitionId) < 0);
            if (allFull) return UiStrings.ShopBookAllSlotsFull.Format();

            int unassignedCopies = (run.unassignedSpellBooks ?? new List<string>()).Count(id => id == skillId);
            if (unassignedCopies > 0)
            {
                return unassignedCopies == 1
                    ? UiStrings.ShopBookUnassignedCopy.Format(unassignedCopies)
                    : UiStrings.ShopBookUnassignedCopies.Format(unassignedCopies);
            }

            if (knownBy.Count == 1)
                return UiStrings.ShopBookKnownByOne.Format(DisplayNameOf(knownBy[0]));
            if (knownBy.Count > 1)
                return UiStrings.ShopBookKnownByMany.Format(knownBy.Count);

            return UiStrings.ShopBookEligible.Format(eligible.Count, squad.Count);
        }

        private static string DisplayNameOf(Character character)
        {
            var definition = ContentDatabase.Characters.FirstOrDefault(c => c != null && c.id == character.definitionId);
            return definition?.Data.DisplayName ?? character.definitionId;
        }

        private static ShopStockEntry EntryAt(int section, int index)
        {
            return RunOrchestrator.CurrentShopStock
                .FirstOrDefault(e => e != null && e.section == section && e.index == index);
        }

        // ---- gamepad navigation (docs/GAMEPAD_NAVIGATION_PLAN.md phase 3) --------
        //
        // A MODAL, not a whole-scene base context (Hub/Map/Talent/MainMenu's
        // shape) -- pushed on Open(), popped on the real Leave() and, as the
        // OnDisable/OnDestroy safety net plan section 4 calls for, on ANY
        // deactivation this class did not initiate itself.
        //
        // ONE CONTEXT, reconfigured between the shelf and the pack modal --
        // NavContext.Reconfigure's own documented case (Map's runtime rewrite,
        // MainMenuController's own save-slot toggle): the navigable set
        // genuinely changes contents, it is not a fixed set with members
        // hidden.
        private NavContext _navContext;

        private void RegisterNavContext()
        {
            if (_navContext != null) return;

            _navContext = new NavContext(entry: null, selectables: null, cancel: HandleCancel);
            NavigationInputModule.Contexts?.Push(_navContext);
        }

        private void PopNavContext()
        {
            if (_navContext == null) return;

            NavigationInputModule.Contexts?.Remove(_navContext);
            _navContext = null;
        }

        // Pack open: Cancel closes it, the same job PackCloseButton already
        // does. Pack closed: Cancel is the SAME two-press arm/confirm
        // ShopLeaveButton already is -- Leave() itself, not a copy of its
        // logic, so a future change to what leaving costs has one call site.
        private void HandleCancel()
        {
            if (_packOpen) ClosePack();
            else Leave();
        }

        // Called from the end of Paint() -- every selection, purchase,
        // reroll, pack toggle and page step already funnels through it, the
        // same shape MapController.RefreshNavLinks and TalentController.
        // RefreshOrbNavigation both use.
        private void RefreshNavigation()
        {
            if (_navContext == null) return;

            if (_packOpen) RefreshPackNavigation();
            else RefreshShelfNavigation();
        }

        // THE BUY GRID is Gear alone (GearColumns == 2, the only section laid
        // out as an actual rectangle -- ShopScreen.cs's own header on why:
        // "its cards sit in a 2-wide grid because the panel spans two
        // columns"). Relic and Book are each a single narrow column, so they
        // are Lists, not Grids. Cross-links between the three shelves and
        // into the actions column are positional and clamped, the same shape
        // PartyController.CrossLinks already uses between seats and cards.
        private const int GearColumns = 2;

        private void RefreshShelfNavigation()
        {
            var relic = Present(relicCards);
            var book = Present(bookCards);
            var gear = Present(gearCards);
            var actions = Present(new[] { buyButton, packButton, leaveButton });

            var groups = new List<UiNavGroup<Selectable>>();
            void AddGroup(string id, UiNavGroupKind kind, List<Button> members, int gridRowLength = 1)
            {
                var group = RuntimeNavWiring.Group(id, kind, members, gridRowLength);
                if (group != null) groups.Add(group);
            }

            AddGroup("shopRelic", UiNavGroupKind.List, relic);
            AddGroup("shopBook", UiNavGroupKind.List, book);
            AddGroup("shopGear", UiNavGroupKind.Grid, gear, GearColumns);
            AddGroup("shopActions", UiNavGroupKind.List, actions);

            var links = new List<UiNavLink<Selectable>?>();
            void Link(Button from, UiNavDirection dir, Button to) => links.Add(RuntimeNavWiring.Link(from, dir, to));

            // Relic <-> Book, row for row (both are 3 long today; clamped to
            // whichever is shorter if that ever changes).
            for (int i = 0; i < relic.Count && i < book.Count; i++)
            {
                Link(relic[i], UiNavDirection.Right, book[i]);
                Link(book[i], UiNavDirection.Left, relic[i]);
            }

            // Each shelf's own reroll, off its first card -- the same "one
            // detail action, one override link" shape TalentController uses
            // for InvestButton, except these three are always visible so the
            // link is unconditional rather than selection-gated.
            LinkRerollAbove(relic, relicReroll, links);
            LinkRerollAbove(book, bookReroll, links);
            LinkRerollAbove(gear, gearReroll, links);

            // Down from the bottom of each narrow shelf into Gear's top row
            // (Relic is the left column, Book the right -- ShopScreen's own
            // "Row 1: RELICS, SPELL BOOKS" / "Row 2: GEAR" comment).
            if (relic.Count > 0 && gear.Count > 0)
            {
                Link(relic[relic.Count - 1], UiNavDirection.Down, gear[0]);
                Link(gear[0], UiNavDirection.Up, relic[relic.Count - 1]);
            }
            if (book.Count > 0 && gear.Count > 1)
            {
                Link(book[book.Count - 1], UiNavDirection.Down, gear[1]);
                Link(gear[1], UiNavDirection.Up, book[book.Count - 1]);
            }

            // Gear's right column into the actions list -- fixed to the
            // second card (the top-right cell), the simplest stable target
            // regardless of which cell a player is actually standing on,
            // matching Map's own "one arbitrary, stated tie-break" precedent
            // for its gate's Up link.
            if (gear.Count > 1 && actions.Count > 0)
            {
                Link(gear[1], UiNavDirection.Right, actions[0]);
                Link(actions[0], UiNavDirection.Left, gear[1]);
            }

            RuntimeNavWiring.Apply(groups, links);

            var selectables = new Dictionary<string, object>();
            void Declare(List<Button> buttons, string prefix)
            {
                for (int i = 0; i < buttons.Count; i++) selectables[$"{prefix}{i}"] = buttons[i].gameObject;
            }
            Declare(relic, "relic");
            Declare(book, "book");
            Declare(gear, "gear");
            Declare(actions, "action");

            // Entry: the first buy card -- ShopGearCard0, not the first
            // relic/book row. The buy grid is this screen's stated primary
            // surface (plan's own phrasing for this screen).
            var entry = gear.Count > 0 ? gear[0].gameObject : null;
            _navContext.Reconfigure(entry, selectables);
        }

        private static void LinkRerollAbove(List<Button> cards, Button reroll, List<UiNavLink<Selectable>?> links)
        {
            if (cards.Count == 0 || reroll == null) return;

            links.Add(RuntimeNavWiring.Link(cards[0], UiNavDirection.Up, reroll));
            links.Add(RuntimeNavWiring.Link(reroll, UiNavDirection.Down, cards[0]));
        }

        // THE PAGED ROW: PackPrevButton/PackNextButton, a two-member Rail
        // (wrap -- trivial at two members, but stated rather than special-
        // cased away). Sell buttons (disable-on-ineligible, PaintPack's own
        // `packSellOneButtons[i].interactable = sellPrice > 0`, line 553's
        // original position) reach the pager and PackCloseButton via
        // explicit links -- Explicit still passes Move through a
        // non-interactable cell, Button.OnSubmit no-ops there, no
        // skip-on-Move, the exact rule this file's own header already
        // states for this line.
        private void RefreshPackNavigation()
        {
            var rows = new List<Button>();
            var rails = new List<UiNavLink<Selectable>?>();

            for (int i = 0; i < packRows.Length; i++)
            {
                if (packRows[i] == null || !packRows[i].activeSelf) continue;

                var one = At(packSellOneButtons, i);
                var all = At(packSellAllButtons, i);
                if (one == null) continue;

                rows.Add(one);
                if (all != null && all.gameObject.activeSelf)
                {
                    rails.Add(RuntimeNavWiring.Link(one, UiNavDirection.Right, all));
                    rails.Add(RuntimeNavWiring.Link(all, UiNavDirection.Left, one));
                }
            }

            var groups = new List<UiNavGroup<Selectable>>();
            var rowGroup = RuntimeNavWiring.Group("shopPackRows", UiNavGroupKind.List, rows);
            if (rowGroup != null) groups.Add(rowGroup);

            var pagerButtons = Present(new[] { packPrevButton, packNextButton });
            var pagerGroup = RuntimeNavWiring.Group("shopPackPager", UiNavGroupKind.Rail, pagerButtons);
            if (pagerGroup != null) groups.Add(pagerGroup);

            if (rows.Count > 0 && pagerButtons.Count > 0)
            {
                rails.Add(RuntimeNavWiring.Link(rows[rows.Count - 1], UiNavDirection.Down, pagerButtons[0]));
                rails.Add(RuntimeNavWiring.Link(pagerButtons[0], UiNavDirection.Up, rows[rows.Count - 1]));
            }

            if (packCloseButton != null)
            {
                var closeTarget = pagerButtons.Count > 0 ? pagerButtons[pagerButtons.Count - 1]
                    : rows.Count > 0 ? rows[rows.Count - 1] : null;
                if (closeTarget != null)
                {
                    rails.Add(RuntimeNavWiring.Link(closeTarget, UiNavDirection.Down, packCloseButton));
                    rails.Add(RuntimeNavWiring.Link(packCloseButton, UiNavDirection.Up, closeTarget));
                }
            }

            RuntimeNavWiring.Apply(groups, rails);

            var selectables = new Dictionary<string, object>();
            for (int i = 0; i < rows.Count; i++) selectables[$"row{i}"] = rows[i].gameObject;
            for (int i = 0; i < pagerButtons.Count; i++) selectables[$"pager{i}"] = pagerButtons[i].gameObject;
            if (packCloseButton != null) selectables["close"] = packCloseButton.gameObject;

            // Entry: the first visible row's sell button, or PackCloseButton
            // when the bag is empty (packEmptyHint's own case) and there is
            // nothing else in this modal to stand on.
            var entry = rows.Count > 0 ? rows[0].gameObject
                : packCloseButton != null ? packCloseButton.gameObject : null;
            _navContext.Reconfigure(entry, selectables);
        }

        private static List<Button> Present(IEnumerable<Button> buttons) =>
            (buttons ?? System.Array.Empty<Button>()).Where(b => b != null).ToList();

        private static Button At(Button[] array, int index) =>
            array != null && index >= 0 && index < array.Length ? array[index] : null;
    }
}

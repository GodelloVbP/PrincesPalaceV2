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
            if (packRoot != null) packRoot.SetActive(false);

            Paint();
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
            Paint();
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

            _leaveArmed = false;
            Paint();
        }

        private void Reroll(int section)
        {
            RunOrchestrator.RerollSection(section);

            // A reroll replaces the whole shelf, so a selection into it is
            // now pointing at a card that may not exist any more.
            if (_selectedSection == section)
            {
                _selectedSection = -1;
                _selectedIndex = -1;
            }

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
            gameObject.SetActive(false);
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

            if (packRoot != null) packRoot.SetActive(true);
            Paint();
        }

        private void ClosePack()
        {
            _packOpen = false;
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
            RunOrchestrator.Sell(absolute, quantity);

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

            var eligible = squad.Where(c => c != null && !knownBy.Contains(c)).ToList();

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
    }
}

using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Events;
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
    //
    // INavSectionStrip (owner ask #3, 2026-09-22): LB/RB step the character
    // picker the same way the dossier's shoulders page its own roster --
    // StepSection is the ONE path both the shoulder buttons and the
    // on-screen arrows call, so they cannot disagree about where wrap lands.
    public class ShopController : MonoBehaviour, INavSectionStrip
    {
        [SerializeField] internal TMP_Text goldLabel;
        [SerializeField] internal Button leaveButton;
        [SerializeField] internal TMP_Text leaveButtonLabel;
        [SerializeField] internal Button buyButton;
        [SerializeField] internal Button packButton;

        // The character picker (owner ask #3): who the comparison panel
        // below is comparing a selected gear card AGAINST. Defaults to the
        // party leader (ActiveSquad()[0]) exactly the way
        // CharacterDossierController's own _index starts at 0.
        [SerializeField] internal TMP_Text characterName;
        [SerializeField] internal Button prevCharacterButton;
        [SerializeField] internal Button nextCharacterButton;

        // The comparison panel (ItemComparisonPanel). detailPanelRoot is
        // shown only for a valid, non-refused GEAR selection; every other
        // state (nothing selected, a refusal, a relic/book card) paints
        // into detailBody alone the way the old single-line detail label
        // always did -- see PaintDetail's own header.
        [SerializeField] internal GameObject detailPanelRoot;
        [SerializeField] internal TMP_Text detailTitle;
        [SerializeField] internal TMP_Text detailBody;

        // The card art, and the three lookup tables it comes from. A skill
        // with no authored iconPath (most skills, still, since only the four
        // book spells carry art) hides its Image via ItemIcons.Apply's own
        // graceful-degradation path -- not a special case for books, the
        // same one a gear card with no art already takes.
        [SerializeField] internal IconEntry[] itemArt;
        [SerializeField] internal IconEntry[] relicArt;
        [SerializeField] internal IconEntry[] skillArt;

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

        // A MERCHANT SHELF (an event's, plan 3.4) is shown on this same
        // screen with what a merchant does not have stood down: the relic
        // panel, every reroll and the pack (no selling to a merchant), and
        // the book panel shows its consumable cards under its own header.
        [SerializeField] internal TMP_Text bookHeader;
        [SerializeField] internal GameObject relicPanel;

        // ...and the title and the relic panel's slot say who is selling: the
        // shelf's own title for SHOP, and the keeper's panel (bust or PORTRAIT
        // PENDING, name, epithet, a line on the fakes) where the relics were.
        [SerializeField] internal TMP_Text title;
        [SerializeField] internal GameObject merchantPanel;
        [SerializeField] internal TMP_Text merchantName;
        [SerializeField] internal TMP_Text merchantEpithet;
        [SerializeField] internal Image merchantBust;
        [SerializeField] internal GameObject merchantBustPending;
        [SerializeField] internal TMP_Text merchantNote;

        [SerializeField] internal Button relicReroll;
        [SerializeField] internal TMP_Text relicRerollLabel;
        [SerializeField] internal Button[] relicCards;
        [SerializeField] internal Image[] relicIcons;
        [SerializeField] internal TMP_Text[] relicNames;
        [SerializeField] internal TMP_Text[] relicMetas;
        [SerializeField] internal TMP_Text[] relicPrices;

        // The overarching menu this screen's Start opens (owner's
        // 2026-09-19 ask: "press Start in the shop to check on your chars'
        // equipment / skills"), assigned by ScreenRegistry.Map's Wire lambda
        // from the same instance it hands MapController -- one
        // SystemMenuController per Map scene, not a second copy for the
        // shop to own, the same sharing MapController.systemMenu documents.
        [SerializeField] internal SystemMenuController systemMenu;

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

        // Which party member the comparison panel scores a selected gear
        // card against (owner ask #3). Reset to 0 -- the party leader -- on
        // every Open(), the same "start on the leader" default the dossier
        // itself resets to when nothing says otherwise.
        private int _charIndex;

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
            _charIndex = 0;
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

            WireSection(gearCards, () => ShopStock.GearSection);
            WireSection(bookCards, () => MiddleSection);
            WireSection(relicCards, () => ShopStock.RelicSection);

            if (gearReroll != null) gearReroll.onClick.AddListener(() => Reroll(ShopStock.GearSection));
            if (bookReroll != null) bookReroll.onClick.AddListener(() => Reroll(ShopStock.BookSection));
            if (relicReroll != null) relicReroll.onClick.AddListener(() => Reroll(ShopStock.RelicSection));

            if (buyButton != null) buyButton.onClick.AddListener(Buy);
            if (packButton != null) packButton.onClick.AddListener(OpenPack);
            if (leaveButton != null) leaveButton.onClick.AddListener(Leave);

            if (prevCharacterButton != null) prevCharacterButton.onClick.AddListener(() => StepCharacter(-1));
            if (nextCharacterButton != null) nextCharacterButton.onClick.AddListener(() => StepCharacter(1));

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

        // The section is asked at the press, not captured at wiring: the book
        // panel is the books in a room shop and the consumables on a
        // merchant shelf, and the same buttons serve both.
        private void WireSection(Button[] cards, System.Func<int> section)
        {
            if (cards == null) return;
            for (int i = 0; i < cards.Length; i++)
            {
                int index = i;
                if (cards[i] != null) cards[i].onClick.AddListener(() => Select(section(), index));
            }
        }

        // Whether the stock in front is an event's merchant shelf.
        private static bool Merchant => RunOrchestrator.ShelfInFrontIsMerchant;

        // Which section the book panel shows.
        private static int MiddleSection => Merchant ? ShopStock.ConsumableSection : ShopStock.BookSection;

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

        // ---- the character picker (owner ask #3) ------------------------------
        //
        // Mirrors CharacterDossierController.Squad()/Step() deliberately:
        // same source (ActiveSquad, filtered of nulls), same wrap-by-modulo,
        // same "index 0 is the default" posture -- a second, differently-
        // shaped picker here would be a second thing for a player to learn.
        private static List<Character> Squad()
        {
            var save = SaveSlotManager.CurrentSave;
            return save == null ? new List<Character>() : save.ActiveSquad().Where(c => c != null).ToList();
        }

        private void StepCharacter(int by)
        {
            var squad = Squad();
            if (squad.Count == 0) return;

            _charIndex = (_charIndex + by + squad.Count) % squad.Count;
            Paint();
        }

        // INavSectionStrip: the SAME path the on-screen arrows call, so a
        // gamepad shoulder press and a mouse click can never disagree about
        // where wrap lands (INavCancelClaim.cs's own contract for this
        // interface).
        public void StepSection(int direction) => StepCharacter(direction);

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
                case ShopStock.ConsumableSection:
                    result = RunOrchestrator.BuyConsumable(index);
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

            // A merchant shelf goes back to its event, which is still open;
            // the room shop clears its room.
            if (Merchant) RunOrchestrator.LeaveShelf();
            else RunOrchestrator.LeaveShop();
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

            bool merchant = Merchant;
            PaintMerchantLayout(merchant);

            PaintSection(ShopStock.GearSection, gearCards, gearIcons, itemArt, gearNames, gearMetas,
                gearPrices, gearReroll, gearRerollLabel);
            PaintSection(MiddleSection, bookCards, bookIcons, merchant ? itemArt : skillArt, bookNames, bookMetas,
                bookPrices, bookReroll, bookRerollLabel);
            if (!merchant)
            {
                PaintSection(ShopStock.RelicSection, relicCards, relicIcons, relicArt, relicNames, relicMetas,
                    relicPrices, relicReroll, relicRerollLabel);
            }

            PaintCharacterPicker();
            PaintDetail(run);
            PaintPack();
            RefreshNavigation();
        }

        // What the stock in front has panels for. Set every paint, both ways,
        // because one controller serves the room shop and every merchant
        // shelf and must not carry one's layout into the other.
        private void PaintMerchantLayout(bool merchant)
        {
            SetShown(relicPanel, !merchant);
            SetShown(gearReroll, !merchant);
            SetShown(bookReroll, !merchant);
            SetShown(relicReroll, !merchant);
            SetShown(packButton, !merchant);
            if (bookHeader != null)
                bookHeader.Set(merchant ? UiStrings.ShopSectionConsumables : UiStrings.ShopSectionBooks);

            var front = merchant ? RunOrchestrator.ShelfInFront : null;
            SetShown(merchantPanel, front != null);
            PaintTitle(front);
            if (front != null) PaintMerchant(front);
        }

        private void PaintTitle(MerchantShelfFront front)
        {
            if (title == null) return;
            if (front == null || string.IsNullOrEmpty(front.Title)) title.Set(UiStrings.ShopTitle);
            else title.Set(UiStrings.ShopShelfTitle, front.Title);
        }

        // The keeper's bust loads as the dialogue stage's does: the face,
        // then neutral, then none -- and none shows PORTRAIT PENDING.
        private void PaintMerchant(MerchantShelfFront front)
        {
            if (merchantName != null) merchantName.SetContent(front.KeeperName);
            if (merchantEpithet != null) merchantEpithet.SetContent(front.KeeperEpithet);

            if (merchantNote != null)
            {
                SetShown(merchantNote, front.HasFakes);
                merchantNote.Set(front.Revealed ? UiStrings.ShopFakesMarked : UiStrings.ShopFakesHidden);
            }

            Sprite sprite = null;
            string path = DialogueBust.FirstAvailable(front.KeeperBustFolder, front.KeeperExpression,
                candidate => (sprite = Resources.Load<Sprite>(candidate)) != null);
            if (string.IsNullOrEmpty(path)) sprite = null;

            if (merchantBust != null)
            {
                merchantBust.sprite = sprite;
                merchantBust.preserveAspect = true;
                SetShown(merchantBust, sprite != null);
            }

            SetShown(merchantBustPending, sprite == null);
        }

        private static void SetShown(Component component, bool shown)
        {
            if (component != null) SetShown(component.gameObject, shown);
        }

        private static void SetShown(GameObject target, bool shown)
        {
            if (target != null && target.activeSelf != shown) target.SetActive(shown);
        }

        // ---- the character picker (owner ask #3) ------------------------------
        private void PaintCharacterPicker()
        {
            var squad = Squad();

            // Hidden below two members -- CharacterDossierController's own
            // threshold and its own reasoning: a control that answers a
            // click by not moving is worse than one that is not there.
            bool canPage = squad.Count > 1;
            if (prevCharacterButton != null && prevCharacterButton.gameObject.activeSelf != canPage)
                prevCharacterButton.gameObject.SetActive(canPage);
            if (nextCharacterButton != null && nextCharacterButton.gameObject.activeSelf != canPage)
                nextCharacterButton.gameObject.SetActive(canPage);

            if (characterName == null) return;

            if (squad.Count == 0)
            {
                characterName.SetContent("");
                return;
            }

            if (_charIndex >= squad.Count) _charIndex = 0;
            var definition = ContentDatabase.GetCharacter(squad[_charIndex].definitionId);
            string name = definition == null || string.IsNullOrWhiteSpace(definition.Data.DisplayName)
                ? squad[_charIndex].definitionId
                : definition.Data.DisplayName;
            characterName.SetContent(name);
        }

        private Character SelectedCharacter()
        {
            var squad = Squad();
            if (squad.Count == 0) return null;
            if (_charIndex >= squad.Count) _charIndex = 0;
            return squad[_charIndex];
        }

        private void PaintSection(int section, Button[] cards, Image[] icons, IconEntry[] art,
            TMP_Text[] names, TMP_Text[] metas, TMP_Text[] prices, Button reroll, TMP_Text rerollLabel)
        {
            var run = RunManager.Run;
            if (run == null || cards == null) return;

            bool merchant = Merchant;
            for (int i = 0; i < cards.Length; i++)
            {
                var entry = EntryAt(section, i);
                bool selected = _selectedSection == section && _selectedIndex == i;

                // A merchant shelf stocks as many cards as its recipe says
                // (two consumables in a three-card panel): a card with no
                // entry behind it is not part of this shelf and is not shown.
                // The room shop always fills every card, NO OFFER included.
                if (cards[i] != null) SetShown(cards[i], !merchant || entry != null);

                if (entry == null || entry.noOffer)
                {
                    if (names != null && i < names.Length) names[i].SetContent("");
                    if (metas != null && i < metas.Length) metas[i].SetContent("");
                    if (prices != null && i < prices.Length) prices[i].Set(UiStrings.ShopCardNoOffer);
                    if (icons != null && i < icons.Length) ItemIcons.Apply(icons[i], art, null);
                    continue;
                }

                if (icons != null && i < icons.Length) ItemIcons.Apply(icons[i], art, entry.contentId);

                var (name, meta) = DescribeCard(entry);
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

        // Graceful degradation on missing content is the house style
        // (CLAUDE.md's own "Conventions"): an id with no matching
        // ModifierDefinition, or no authored DisplayName, prints the raw id
        // rather than dropping it from the list.
        private static string AffixDisplayName(string modifierId)
        {
            var modifier = ContentDatabase.GetModifier(modifierId);
            return modifier == null || string.IsNullOrEmpty(modifier.Data.DisplayName)
                ? modifierId
                : modifier.Data.DisplayName;
        }

        // A card as the shelf shows it: DescribeEntry, with a revealed
        // merchant shelf's fake mark leading the meta line ("Browse with
        // Odette", plan 1.5). The mark is the shelf's, never the item's: it
        // does not follow the copy into the bag.
        private (string name, string meta) DescribeCard(ShopStockEntry entry)
        {
            var (name, meta) = DescribeEntry(entry);
            if (entry.fake && RunOrchestrator.ShelfInFrontIsRevealed) meta = UiStrings.ShopCardFakeMeta.Format(meta);
            return (name, meta);
        }

        private (string name, string meta) DescribeEntry(ShopStockEntry entry)
        {
            switch (entry.kind)
            {
                case ShopEntryKind.Consumable:
                {
                    var item = ContentDatabase.GetItem(entry.contentId);
                    return (item?.displayName ?? entry.contentId, UiStrings.ShopConsumableMeta.Format());
                }
                case ShopEntryKind.Gear:
                {
                    var item = ContentDatabase.GetItem(entry.contentId);
                    string name = item?.displayName ?? entry.contentId;
                    // VISIBLE AFFIXES (owner ask #2): the card names its
                    // modifiers, not just how many there are. What each one
                    // DOES, in numbers, is more than this line can hold --
                    // that is ModifierAffixLines.LinePairs' text, and it
                    // lives in the selected card's comparison panel
                    // (PaintDetail) instead of being crammed in here.
                    string affixNames = entry.modifiers != null && entry.modifiers.Count > 0
                        ? string.Join(", ", entry.modifiers.Select(AffixDisplayName))
                        : UiStrings.ShopGearMetaNoAffix.Format();
                    string meta = UiStrings.ShopGearMeta.Format(item?.tier ?? 0, entry.plus, affixNames);
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

        // THE ONE BOX FOR "WHAT THE SHOP IS TELLING THE PLAYER RIGHT NOW"
        // (ShopScreen.DetailBody's own header). Three shapes, same priority
        // order the old single-line label always used:
        //
        //   1. a refusal, or the pack/empty-selection placeholder -- plain
        //      text in detailBody, panel hidden (nothing to compare).
        //   2. a relic or book -- the old name/meta/description line, same
        //      text, same box (a comparison needs an equip slot; neither
        //      kind has one).
        //   3. a gear card -- detailTitle gets its name, the panel shows,
        //      and detailBody carries ItemDescription.ComparisonBody: the
        //      item's stats, its affix lines (owner ask #2's full numbers)
        //      and the delta against whatever SelectedCharacter() has
        //      equipped in that slot (owner ask #3).
        private void PaintDetail(RunSnapshot run)
        {
            if (detailTitle != null) detailTitle.SetContent("");
            SetDetailPanelActive(false);

            if (detailBody == null) return;

            // AHEAD OF EVERYTHING ELSE, and ahead of the pack check too: a
            // sell is refused from inside the pack modal, and a refusal
            // nobody can see is what #115 was.
            if (_refusal.IsValid)
            {
                detailBody.Set(_refusal);
                return;
            }

            if (_packOpen || _selectedSection < 0)
            {
                detailBody.Set(UiStrings.ShopDetailEmpty);
                return;
            }

            var entry = EntryAt(_selectedSection, _selectedIndex);
            if (entry == null)
            {
                detailBody.Set(UiStrings.ShopDetailEmpty);
                return;
            }

            if (entry.kind == ShopEntryKind.Gear)
            {
                PaintGearComparison(entry);
                return;
            }

            string description = entry.kind == ShopEntryKind.Relic
                ? ContentDatabase.GetRelic(entry.contentId)?.Data.Description ?? ""
                : entry.kind == ShopEntryKind.Consumable
                    ? ContentDatabase.GetItem(entry.contentId)?.description ?? ""
                    : ContentDatabase.GetSkill(entry.contentId)?.Data.Description ?? "";

            var (name, meta) = DescribeCard(entry);
            detailBody.SetContent(string.IsNullOrEmpty(description)
                ? $"{name} - {meta}"
                : $"{name} - {meta} - {description}");
        }

        // A picked character and a resolvable item are both required for a
        // comparison to mean anything -- with either missing, this degrades
        // to the SAME plain name/meta line a relic or book gets, rather than
        // showing a panel that compares against nobody.
        private void PaintGearComparison(ShopStockEntry entry)
        {
            var item = ContentDatabase.GetItem(entry.contentId);
            var character = SelectedCharacter();

            if (item == null || character == null)
            {
                var (name, meta) = DescribeCard(entry);
                detailBody.SetContent($"{name} - {meta}");
                return;
            }

            if (detailTitle != null) detailTitle.SetContent(item.displayName ?? entry.contentId);

            var riftTier = (RiftTier)entry.riftTier;
            detailBody.SetContent(ItemDescription.ComparisonBody(character, item, entry.plus,
                riftTier, entry.modifiers));

            SetDetailPanelActive(true);
        }

        private void SetDetailPanelActive(bool active)
        {
            if (detailPanelRoot != null && detailPanelRoot.activeSelf != active)
                detailPanelRoot.SetActive(active);
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
            // systemMenu, not folded into cancel: AUDIT.md #173 found the
            // shop absorbed Start with no handler of its own (RaiseSystemMenu
            // no-ops here, and the Map context underneath never gets a turn --
            // a context that is top is the thing the player is talking to).
            // Mirrors MapController.RegisterNavContext, which is why this
            // context's own Cancel is untouched: HandleCancel still means
            // "close the pack, else arm/confirm Leave", and opening the menu
            // is a second, independent press exactly as it is on the map.
            if (_navContext == null)
                _navContext = new NavContext(entry: null, selectables: null, cancel: HandleCancel,
                    systemMenu: HandleSystemMenu, sectionStrip: () => this);

            // PushIfAbsent, not Push: this context outlives its time on the
            // stack now (see PopNavContext), so re-entering the shop puts the
            // SAME instance back rather than building a new one.
            NavigationInputModule.Contexts?.PushIfAbsent(_navContext);
        }

        private void PopNavContext()
        {
            if (_navContext == null) return;

            NavigationInputModule.Contexts?.Remove(_navContext);
            // _navContext is NOT nulled. It carries this screen's focus
            // memory (plan section 4: "remembered focus by stable id"), and a
            // modal that can be re-opened has to survive being closed for
            // that memory to mean anything -- a context recreated on the next
            // open would land on its entry every single time, which is the
            // whole of AUDIT.md #163's second half. What leaves is its place
            // on the stack; what stays is the controller's own record of
            // where the player was.
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

        // Start, over EITHER the shelf or the pack modal, opens the menu --
        // no branch on _packOpen the way HandleCancel has one. A pack view
        // is a page of rows, not a pending transaction (nothing here commits
        // until a sell button is pressed, and that press is itself atomic),
        // so it does not earn the Party-carry treatment (AUDIT.md #174):
        // ShopController implements no INavCancelClaim, and
        // SystemMenuController.CloseUnlessTheActivePaneClaimsIt's claimant
        // lookup is scoped to the MENU's own active pane, never to whatever
        // sits underneath it, so there was nothing to wire here either way.
        // Same shape as MapController.HandleSystemMenu, down to the null-
        // and already-open tolerance OpenFromRoot provides.
        private void HandleSystemMenu() => SystemMenuController.OpenFromRoot(systemMenu);

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
            // Shown() rather than Present(): a merchant shelf stands down the
            // relic panel, the pack and the rerolls, and its unstocked cards.
            var relic = Shown(relicCards);
            var book = Shown(bookCards);
            var gear = Shown(gearCards);
            var actions = Shown(new[] { buyButton, packButton, leaveButton });

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
            if (cards.Count == 0 || reroll == null || !reroll.gameObject.activeSelf) return;

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

        // Present, and standing: neither the button nor a panel above it
        // stood down (PaintMerchantLayout). In a room shop that is every
        // button, so its navigation is exactly Present's.
        private static List<Button> Shown(IEnumerable<Button> buttons) =>
            Present(buttons).Where(b => b.gameObject.activeSelf
                && (b.transform.parent == null || b.transform.parent.gameObject.activeSelf)).ToList();

        private static Button At(Button[] array, int index) =>
            array != null && index >= 0 && index < array.Length ? array[index] : null;
    }
}

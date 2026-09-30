using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Rewards;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // LOADOUT (plan C16, spec 3.1): the gear and book slots, the pack beside
    // them, and the compare column. Every rule about what can be worn lives in
    // EquipMove and every rule about books in RunOrchestrator; this picks which
    // entry and which slot and asks them.
    //
    // THE TWO PENDING STATES (plan C5). SlotArmed: A on an empty slot moves
    // focus into the pack, filtered to that slot, and the next A there fills
    // it. BookPicked: A on a pack book while every book slot is full moves
    // focus to the book slots, and the next A there replaces that one. Any
    // other press, and every tab switch, character step, section change and
    // close, drops the pending state with nothing changed (plan C6).
    public partial class CharacterDossierController
    {
        // ---- the slots, indexed by EquipmentSlots.All ------------------------------
        [SerializeField] internal Button[] slotCells;
        [SerializeField] internal Image[] slotIcons;
        [SerializeField] internal Image[] slotRarityTicks;
        [SerializeField] internal Image[] slotRiftGlows;
        [SerializeField] internal TMP_Text[] slotCaptions;
        [SerializeField] internal TMP_Text[] slotNames;
        [SerializeField] internal GameObject[] slotArmedRims;

        // ---- the book row, SpellBooks.MaxSpellSlots long ----------------------------
        [SerializeField] internal GameObject booksHeading;
        [SerializeField] internal Button[] bookCells;
        [SerializeField] internal Image[] bookIcons;
        [SerializeField] internal TMP_Text[] bookCaptions;
        [SerializeField] internal TMP_Text[] bookNames;
        [SerializeField] internal GameObject[] bookArmedRims;
        [SerializeField] internal TMP_Text noBooksLine;

        // ---- the pack ------------------------------------------------------------------
        [SerializeField] internal Button[] filterChips;
        [SerializeField] internal GameObject[] filterMarkers;
        [SerializeField] internal Button[] packCells;
        [SerializeField] internal Image[] packIcons;
        [SerializeField] internal Image[] packRarityTicks;
        [SerializeField] internal Image[] packRiftGlows;
        [SerializeField] internal TMP_Text[] packCounts;
        [SerializeField] internal TMP_Text[] packNames;
        [SerializeField] internal GameObject[] packArmedRims;
        [SerializeField] internal Image packScrollTrack;
        [SerializeField] internal RectTransform packScrollThumb;
        [SerializeField] internal TMP_Text packCarried;
        [SerializeField] internal GameObject packEmptyHint;

        // ---- compare ---------------------------------------------------------------------
        [SerializeField] internal TMP_Text compareName;
        [SerializeField] internal TMP_Text compareKind;
        [SerializeField] internal TMP_Text compareDescription;
        [SerializeField] internal TMP_Text compareBody;
        [SerializeField] internal TMP_Text[] compareStatNames;
        [SerializeField] internal TMP_Text[] compareStatValues;

        // Everything the pack holds, and the part of it the filter (or an
        // armed slot) lets through, in grid order.
        private IReadOnlyList<PackEntry> _pack = new List<PackEntry>();
        private List<PackEntry> _visible = new List<PackEntry>();

        private PackFilter _filter = PackFilter.All;

        // The window: how many rows of _visible are scrolled past.
        private int _scrollRow;
        private float _scrollDrag;

        private LoadoutPending _pending = LoadoutPending.None;

        // The filter a SlotArmed replaced, put back when it ends.
        private PackFilter _filterBeforeArm = PackFilter.All;

        // Who the compare column is describing: the pointer or the selection,
        // arbitrated so focus outranks hover (TooltipFocusRouter).
        private readonly TooltipFocusRouter _compare = new TooltipFocusRouter();

        public PackFilter Filter => _filter;

        public int ScrollRow => _scrollRow;

        public LoadoutPending Pending => _pending;

        public bool HasPending => !_pending.IsNone;

        // What the pack shows right now, in grid order, for a test to state
        // the filter rule against.
        public IReadOnlyList<PackEntry> VisiblePack => _visible;

        private void WireLoadout()
        {
            for (int i = 0; slotCells != null && i < slotCells.Length; i++)
            {
                int index = i;
                slotCells[i].onClick.AddListener(() => PressGearSlot(index));
                RegisterCompare(slotCells[i], entered => { if (entered) PaintCompareForGearSlot(index); else PaintCompareStats(); });
            }

            for (int i = 0; bookCells != null && i < bookCells.Length; i++)
            {
                int index = i;
                bookCells[i].onClick.AddListener(() => PressBookSlot(index));
                RegisterCompare(bookCells[i], entered => { if (entered) PaintCompareForBookSlot(index); else PaintCompareStats(); });
            }

            for (int i = 0; packCells != null && i < packCells.Length; i++)
            {
                int index = i;
                packCells[i].onClick.AddListener(() => PressPackCell(index));
                RegisterCompare(packCells[i], entered => { if (entered) PaintCompareForPackCell(index); else PaintCompareStats(); });

                // Off the window's edge row, a Move scrolls the pack instead of
                // leaving it, so every entry is reachable on a pad.
                var edge = packCells[i].gameObject.GetComponent<EdgeScroll>()
                           ?? packCells[i].gameObject.AddComponent<EdgeScroll>();
                edge.Moved = direction => EdgeMove(index, direction);

                // The wheel and a drag over the cells move the window by rows.
                var scroll = packCells[i].gameObject.GetComponent<ListScroll>()
                             ?? packCells[i].gameObject.AddComponent<ListScroll>();
                scroll.WheelStep = DossierLayout.PackCell + DossierLayout.PackCellGap;
                scroll.Scrolled = ScrollByPixels;
            }

            for (int i = 0; filterChips != null && i < filterChips.Length; i++)
            {
                int index = i;
                filterChips[i].onClick.AddListener(() => PressFilter(index));
            }

            // A grab on the track seeks to that point of the pack.
            if (packScrollTrack != null)
            {
                var seek = packScrollTrack.gameObject.GetComponent<ListScroll>()
                           ?? packScrollTrack.gameObject.AddComponent<ListScroll>();
                seek.Seeked = SeekPack;
                FocusKeepClear.Mark(packScrollTrack.gameObject);
            }
        }

        // The pointer is one caller of `show`, the selection the other; the
        // router decides which may call it (focus outranks hover).
        private void RegisterCompare(Button control, System.Action<bool> show)
        {
            if (control == null) return;

            var node = control.gameObject;
            _compare.Register(control, show);

            var hover = node.GetComponent<HoverIndex>() ?? node.AddComponent<HoverIndex>();
            hover.Changed = (_, entered) => _compare.Pointer(node, entered);
        }

        private void RefreshLoadout(Character character)
        {
            PaintSlots(character);
            PaintBooks(character);
            RebuildPack();
            RepaintCompareForSelection();
        }

        // ---- the gear slots ------------------------------------------------------------------

        private void PaintSlots(Character character)
        {
            var loadout = ContentDatabase.ActiveLoadout(character);

            for (int i = 0; i < EquipmentSlots.All.Length && slotCells != null && i < slotCells.Length; i++)
            {
                var slot = EquipmentSlots.All[i];
                string itemId = character.equipment?.Get(slot);
                var item = string.IsNullOrEmpty(itemId) ? null : ContentDatabase.GetItem(itemId);

                // ACTIVATE the icon object, not just its Image: the node is
                // declared inactive so an empty slot draws nothing, and
                // ItemIcons.Apply only sets `enabled`.
                bool hasArt = Has(slotIcons, i) && ItemIcons.Apply(slotIcons[i], icons, item?.id);
                if (Has(slotIcons, i)) slotIcons[i].gameObject.SetShown(hasArt);
                ShowNameInCell(slotNames, i, item != null && !hasArt, item?.displayName);

                if (Has(slotRarityTicks, i))
                {
                    slotRarityTicks[i].gameObject.SetShown(item != null);
                    if (item != null) slotRarityTicks[i].color = TierColour(item.tier);
                }

                // The RiftTier ring, hidden at Ordinary so an item that rolled
                // nothing draws exactly as it always did (RiftTierColors.ShouldGlow).
                if (Has(slotRiftGlows, i))
                {
                    var riftTier = (RiftTier)(character.equipment?.GetRiftTier(slot) ?? 0);
                    bool glows = item != null && RiftTierColors.ShouldGlow(riftTier);
                    slotRiftGlows[i].gameObject.SetShown(glows);
                    if (glows) slotRiftGlows[i].color = RiftTierColors.For(riftTier);
                }

                // A slot a two-hander has taken is INERT, not empty: the
                // caption says why it cannot be used.
                bool blocked = item == null
                               && loadout.InertEntries != null
                               && loadout.InertEntries.Any(e => e.Entry.slot == slot);

                if (Has(slotCaptions, i))
                {
                    if (blocked) slotCaptions[i].Set(UiStrings.LoadoutTwoHanded);
                    else slotCaptions[i].Set(UiStrings.EquipmentSlotName(slot));
                }

                if (Has(slotArmedRims, i)) slotArmedRims[i].SetShown(_pending.ArmsGear && _pending.GearSlot == i);
            }
        }

        // The name stands in for art that is not there, so a cell is never a
        // blank square (AUDIT #131: the salves have no icon yet).
        private static void ShowNameInCell(TMP_Text[] names, int i, bool show, string name)
        {
            if (!Has(names, i)) return;

            names[i].gameObject.SetShown(show);
            if (show) names[i].SetContent(name ?? "");
        }

        private bool WornAt(int slotIndex)
        {
            var character = Current();
            if (character?.equipment == null || slotIndex < 0 || slotIndex >= EquipmentSlots.All.Length) return false;
            return !string.IsNullOrEmpty(character.equipment.Get(EquipmentSlots.All[slotIndex]));
        }

        // ---- the book slots ---------------------------------------------------------------

        private static List<LearnedSpellEntry> Learned() => RunManager.Run?.learnedSpells ?? new List<LearnedSpellEntry>();

        private string BookIn(string characterId, int slot) =>
            Learned().FirstOrDefault(e => e != null && e.characterId == characterId && e.slot == slot)?.skillId;

        private void PaintBooks(Character character)
        {
            // THE WHOLE BOOK ROW, OR ONE SENTENCE (plan C16): a character whose
            // pool refuses books never gets greyed slots inviting a press.
            if (booksHeading != null) booksHeading.SetShown(_canHoldSpellBooks);
            if (noBooksLine != null)
            {
                noBooksLine.gameObject.SetShown(!_canHoldSpellBooks);
                if (!_canHoldSpellBooks) noBooksLine.Set(UiStrings.DossierNoSpellBooks, DisplayNameOf(character.definitionId));
            }

            for (int i = 0; bookCells != null && i < bookCells.Length; i++)
            {
                bookCells[i].gameObject.SetShown(_canHoldSpellBooks);
                if (Has(bookCaptions, i)) bookCaptions[i].gameObject.SetShown(_canHoldSpellBooks);
                if (!_canHoldSpellBooks) continue;

                string skillId = BookIn(character.definitionId, i);
                var skill = string.IsNullOrEmpty(skillId) ? null : ContentDatabase.GetSkill(skillId);

                bool hasArt = Has(bookIcons, i) && ItemIcons.Apply(bookIcons[i], icons, skillId);
                if (Has(bookIcons, i)) bookIcons[i].gameObject.SetShown(hasArt);
                ShowNameInCell(bookNames, i, skill != null && !hasArt, skill?.Data.DisplayName);

                if (Has(bookCaptions, i))
                {
                    if (skill == null) bookCaptions[i].Set(UiStrings.LoadoutBookSlot, i + 1);
                    else bookCaptions[i].SetContent(skill.Data.DisplayName);
                }

                if (Has(bookArmedRims, i)) bookArmedRims[i].SetShown(_pending.ArmsBook && _pending.BookSlot == i);
            }
        }

        private bool BookAt(int slot)
        {
            string id = CharacterId;
            return id != null && !string.IsNullOrEmpty(BookIn(id, slot));
        }

        // ---- the pack ------------------------------------------------------------------------

        // Gear and salves from the save's pack, books from the run's unassigned
        // list, as one view (LoadoutPack). Nothing moves between the two lists
        // by being shown together.
        private void RebuildPack()
        {
            var save = SaveSlotManager.CurrentSave;
            var items = (save?.stockpiledItems ?? new List<InventoryEntry>()).Select(ToBagItem).Where(i => i.Count > 0);

            var run = RunManager.Run;
            var books = (run?.unassignedSpellBooks ?? new List<string>())
                .Select(id => new PackBook(id, ContentDatabase.GetSkill(id)?.Data.DisplayName ?? id));

            _pack = LoadoutPack.Build(items, books);
            ApplyView();
        }

        // Filters _pack into _visible: through the armed slot while one is
        // armed, else through the chosen chip.
        private void ApplyView()
        {
            var character = Current();

            if (_pending.ArmsGear)
            {
                var slot = EquipmentSlots.All[_pending.GearSlot];
                _visible = _pack.Where(e => LoadoutPack.FitsGearSlot(e, slot)).ToList();
            }
            else if (_pending.ArmsBook)
            {
                _visible = _pack.Where(e => e.Kind == PackEntryKind.Book && !Knows(character, e.BookId)).ToList();
            }
            else
            {
                _visible = LoadoutPack.Filter(_pack, _filter).ToList();
            }

            _scrollRow = Mathf.Clamp(_scrollRow, 0, MaxScrollRow());
            BindPackWindow();
            PaintFilterMarkers();

            if (packCarried != null) packCarried.Set(UiStrings.PackCarried, _pack.Count);
        }

        private bool Knows(Character character, string skillId) =>
            character != null && Learned().Exists(e => e != null && e.characterId == character.definitionId && e.skillId == skillId);

        private int TotalRows => (_visible.Count + DossierLayout.PackColumns - 1) / DossierLayout.PackColumns;

        private int MaxScrollRow() => Mathf.Max(0, TotalRows - DossierLayout.PackRows);

        private PackEntry? EntryAtCell(int cell)
        {
            int index = _scrollRow * DossierLayout.PackColumns + cell;
            return cell >= 0 && index < _visible.Count ? _visible[index] : (PackEntry?)null;
        }

        // Puts the window onto the cells. The cells stay put and what they
        // show moves; a cell past the end of the list is switched off, so a
        // Move never lands on an empty square.
        private void BindPackWindow()
        {
            for (int i = 0; packCells != null && i < packCells.Length; i++)
            {
                var entry = EntryAtCell(i);
                packCells[i].gameObject.SetShown(entry.HasValue);
                if (!entry.HasValue) continue;

                var e = entry.Value;

                bool hasArt = Has(packIcons, i) && ItemIcons.Apply(packIcons[i], icons, e.IconId);
                if (Has(packIcons, i)) packIcons[i].gameObject.SetShown(hasArt);
                ShowNameInCell(packNames, i, !hasArt, e.DisplayName);

                bool item = e.Kind != PackEntryKind.Book;
                if (Has(packRarityTicks, i))
                {
                    packRarityTicks[i].gameObject.SetShown(item);
                    if (item) packRarityTicks[i].color = TierColour(e.Item.Tier);
                }

                if (Has(packRiftGlows, i))
                {
                    bool glows = item && RiftTierColors.ShouldGlow(e.Item.RiftTier);
                    packRiftGlows[i].gameObject.SetShown(glows);
                    if (glows) packRiftGlows[i].color = RiftTierColors.For(e.Item.RiftTier);
                }

                // A count only where there is more than one.
                if (Has(packCounts, i))
                {
                    bool stacked = e.Count > 1;
                    packCounts[i].gameObject.SetShown(stacked);
                    if (stacked) packCounts[i].Set(UiStrings.PackStackCount, e.Count);
                }

                if (Has(packArmedRims, i))
                {
                    bool picked = _pending.Kind == LoadoutPendingKind.BookPicked
                                  && e.Kind == PackEntryKind.Book && e.BookId == _pending.BookId;
                    packArmedRims[i].SetShown(picked);
                }
            }

            if (packEmptyHint != null)
            {
                bool empty = _visible.Count == 0;
                packEmptyHint.SetShown(empty);
                var words = packEmptyHint.GetComponent<TMP_Text>();
                if (empty && words != null)
                {
                    words.Set(_pack.Count == 0 && !HasPending && _filter == PackFilter.All
                        ? UiStrings.PackEmpty
                        : UiStrings.PackFilterEmpty);
                }
            }

            PaintScrollThumb();

            // Which cells a Move may land on changed with the window.
            if (_section == CharacterSection.Loadout) RefreshNavigation();
        }

        private void PaintFilterMarkers()
        {
            // While a slot is armed the chip of its kind stands lit: the grid
            // is narrower still, but that is the family it belongs to.
            var lit = _pending.ArmsGear ? LoadoutPack.FilterFor(EquipmentSlots.All[_pending.GearSlot])
                : _pending.ArmsBook ? PackFilter.Books
                : _filter;

            for (int i = 0; filterMarkers != null && i < filterMarkers.Length; i++)
            {
                filterMarkers[i].SetShown(i < LoadoutPack.Filters.Length && LoadoutPack.Filters[i] == lit);
            }
        }

        // The thumb's length says how much of the pack is showing and its
        // position says where. Nothing to scroll: no bar at all.
        private void PaintScrollThumb()
        {
            int rows = TotalRows;
            bool scrolls = rows > DossierLayout.PackRows;

            if (packScrollTrack != null) packScrollTrack.gameObject.SetShown(scrolls);
            if (packScrollThumb == null) return;

            packScrollThumb.gameObject.SetShown(scrolls);
            if (!scrolls) return;

            float height = DossierLayout.ScrollThumbHeight(rows);
            float travel = DossierLayout.PackGridHeight - height;
            float t = MaxScrollRow() <= 0 ? 0f : (float)_scrollRow / MaxScrollRow();

            packScrollThumb.sizeDelta = new Vector2(DossierLayout.ScrollbarWidth, height);
            packScrollThumb.anchoredPosition = new Vector2(packScrollThumb.anchoredPosition.x,
                DossierLayout.PackGridCentre.Y + travel * 0.5f - travel * t);
        }

        // Moves the window by `rows`, keeping focus on the same cell -- which
        // now shows the next row, so the pad reads it as a step down the list.
        public void ScrollPack(int rows)
        {
            int before = _scrollRow;
            _scrollRow = Mathf.Clamp(_scrollRow + rows, 0, MaxScrollRow());
            if (_scrollRow == before) return;

            BindPackWindow();
            RepaintCompareForSelection();
        }

        private void ScrollByPixels(float pixels)
        {
            float step = DossierLayout.PackCell + DossierLayout.PackCellGap;
            _scrollDrag += pixels;

            while (Mathf.Abs(_scrollDrag) >= step)
            {
                int rows = _scrollDrag > 0f ? 1 : -1;
                _scrollDrag -= rows * step;
                ScrollPack(rows);
            }
        }

        private void SeekPack(float fraction)
        {
            int row = Mathf.RoundToInt(Mathf.Clamp01(fraction) * MaxScrollRow());
            ScrollPack(row - _scrollRow);
        }

        // Up off the window's top row, Down off its bottom: the window moves
        // one row and focus stays put, so every entry is reachable on a pad.
        private void EdgeMove(int cell, MoveDirection direction)
        {
            int row = cell / DossierLayout.PackColumns;
            if (direction == MoveDirection.Down && row == DossierLayout.PackRows - 1) ScrollPack(1);
            else if (direction == MoveDirection.Up && row == 0) ScrollPack(-1);
        }

        private List<Selectable> BoundPackCells()
        {
            var cells = new List<Selectable>();
            for (int i = 0; packCells != null && i < packCells.Length; i++)
            {
                if (packCells[i] != null && packCells[i].gameObject.activeSelf) cells.Add(packCells[i]);
            }

            return cells;
        }

        private static BagItem ToBagItem(InventoryEntry entry)
        {
            var item = ContentDatabase.GetItem(entry.itemId);
            if (item == null)
            {
                // A save naming content that no longer exists is shown as
                // itself rather than dropped.
                return new BagItem(entry.itemId, entry.itemId, 0, EquipmentSlot.Weapon1,
                    0, entry.plus, entry.count, "", false, instance: entry.Instance);
            }

            return new BagItem(item.id, RarityColors.NameOf(item, entry.plus), (int)item.kind,
                item.equipSlot, item.tier, entry.plus, entry.count, item.iconPath, item.IsEquippable,
                entry.modifierIds, entry.riftTier, entry.Instance);
        }

        private static Color TierColour(int tier) => Hex(RarityBands.HexColorForTier(tier));

        // ---- presses --------------------------------------------------------------------------

        private void PressFilter(int index)
        {
            if (index < 0 || index >= LoadoutPack.Filters.Length) return;

            CancelPending();

            _filter = LoadoutPack.Filters[index];
            _scrollRow = 0;
            ApplyView();
        }

        // A worn slot unequips; an empty one arms (plan C16).
        private void PressGearSlot(int index)
        {
            if (index < 0 || index >= EquipmentSlots.All.Length) return;

            // "Click elsewhere" while something is pending drops it first
            // (plan C6), then the press does what it always does.
            CancelPending(restoreFocus: false);

            if (WornAt(index))
            {
                UnequipSlot(index);
                return;
            }

            if (SystemMenuController.Refuses(menu, MenuAction.EquipGear)) return;

            var slot = EquipmentSlots.All[index];
            if (!_pack.Any(e => LoadoutPack.FitsGearSlot(e, slot)))
            {
                menu?.ShowNotice(UiStrings.LoadoutNothingFits);
                return;
            }

            Arm(LoadoutPending.ArmGear(index), UiStrings.LoadoutArmedLine, UiStrings.EquipmentSlotName(slot).Format());
        }

        // A filled book slot gives its book back to the pack; an empty one
        // arms; while a book is picked, this is the slot it replaces.
        private void PressBookSlot(int index)
        {
            var character = Current();
            if (character == null || index < 0 || index >= SpellBooks.MaxSpellSlots) return;

            if (_pending.Kind == LoadoutPendingKind.BookPicked)
            {
                PlacePickedBook(character, index);
                return;
            }

            CancelPending(restoreFocus: false);

            if (BookAt(index))
            {
                if (SystemMenuController.Refuses(menu, MenuAction.RemoveBook)) return;

                if (RunOrchestrator.UnlearnSpell(Context, character.definitionId, index).Applied) Refresh();
                return;
            }

            if (SystemMenuController.Refuses(menu, MenuAction.PlaceBook)) return;

            if (!_pack.Any(e => e.Kind == PackEntryKind.Book && !Knows(character, e.BookId)))
            {
                menu?.ShowNotice(UiStrings.LoadoutNothingFits);
                return;
            }

            Arm(LoadoutPending.ArmBook(index), UiStrings.LoadoutArmedBookLine);
        }

        // Gear equips (into the armed slot while one is armed); a book goes to
        // the lowest free slot, or is picked when every slot is full. A salve
        // has nothing to do outside a fight.
        private void PressPackCell(int cell)
        {
            var entry = EntryAtCell(cell);
            var character = Current();
            if (!entry.HasValue || character == null) return;

            var e = entry.Value;

            if (_pending.Kind == LoadoutPendingKind.SlotArmed)
            {
                CommitArmed(character, e);
                return;
            }

            CancelPending(restoreFocus: false);

            if (e.Kind == PackEntryKind.Gear)
            {
                EquipFromPack(character, e, preferredSlot: null);
                return;
            }

            if (e.Kind == PackEntryKind.Book) PressPackBook(character, e, cell);
        }

        private void PressPackBook(Character character, PackEntry entry, int cell)
        {
            if (SystemMenuController.Refuses(menu, MenuAction.PlaceBook)) return;

            if (!_canHoldSpellBooks)
            {
                menu?.ShowNotice(UiStrings.DossierNoSpellBooks, new object[] { DisplayNameOf(character.definitionId) });
                return;
            }

            if (Knows(character, entry.BookId))
            {
                menu?.ShowNotice(UiStrings.DossierSpellAlreadyKnown);
                return;
            }

            if (RunOrchestrator.CanLearn(character.definitionId) >= 0)
            {
                var result = RunOrchestrator.LearnSpell(Context, character.definitionId, entry.BookId);
                if (result.Applied) Refresh();
                return;
            }

            // Every slot is full: choose which book this one replaces.
            _pending = LoadoutPending.PickBook(entry.BookId);
            menu?.SetStateLine(UiStrings.LoadoutPickedLine, entry.BookName);
            BindPackWindow();
            RefreshNavigation();
            Select(bookCells != null && bookCells.Length > 0 ? bookCells[0] : null);
        }

        // ---- the pending states --------------------------------------------------------------

        private void Arm(LoadoutPending pending, UiString line, params object[] args)
        {
            _pending = pending;
            _filterBeforeArm = _filter;
            _scrollRow = 0;

            menu?.SetStateLine(line, args);

            var character = Current();
            if (character != null)
            {
                PaintSlots(character);
                PaintBooks(character);
            }

            ApplyView();

            // Focus goes to the first entry that fits (spec section 9).
            var cells = BoundPackCells();
            Select(cells.Count > 0 ? cells[0] : null);
        }

        private void CommitArmed(Character character, PackEntry entry)
        {
            var pending = _pending;

            if (pending.ArmsGear)
            {
                if (entry.Kind != PackEntryKind.Gear) return;
                if (SystemMenuController.Refuses(menu, MenuAction.EquipGear)) return;

                EndPending();
                EquipFromPack(character, entry, EquipmentSlots.All[pending.GearSlot]);
                Select(Cell(slotCells, pending.GearSlot));
                return;
            }

            if (!pending.ArmsBook || entry.Kind != PackEntryKind.Book) return;
            if (SystemMenuController.Refuses(menu, MenuAction.PlaceBook)) return;

            var result = RunOrchestrator.LearnSpell(Context, character.definitionId, entry.BookId, pending.BookSlot);
            if (!result.Applied && result.Reason == ShopRefusal.AlreadyKnown)
            {
                menu?.ShowNotice(UiStrings.DossierSpellAlreadyKnown);
                return;
            }

            EndPending();
            Refresh();
            Select(Cell(bookCells, pending.BookSlot));
        }

        private void PlacePickedBook(Character character, int slot)
        {
            string bookId = _pending.BookId;
            bool occupied = BookAt(slot);

            if (SystemMenuController.Refuses(menu, occupied ? MenuAction.ReplaceBook : MenuAction.PlaceBook)) return;

            var result = occupied
                ? RunOrchestrator.ReplaceSpell(Context, character.definitionId, bookId, slot)
                : RunOrchestrator.LearnSpell(Context, character.definitionId, bookId, slot);

            if (!result.Applied && result.Reason == ShopRefusal.AlreadyKnown)
            {
                menu?.ShowNotice(UiStrings.DossierSpellAlreadyKnown);
                return;
            }

            EndPending();
            Refresh();
            Select(Cell(bookCells, slot));
        }

        // IMenuPending: drops the pending state with no change to the game,
        // and focus goes back to where it was created -- the armed slot, or
        // the pack cell of the picked book.
        public void CancelPending() => CancelPending(restoreFocus: true);

        private void CancelPending(bool restoreFocus)
        {
            if (!HasPending) return;

            var pending = _pending;
            EndPending();

            var character = Current();
            if (character != null)
            {
                PaintSlots(character);
                PaintBooks(character);
            }

            ApplyView();
            PaintCompareStats();

            if (!restoreFocus) return;

            if (pending.ArmsGear) Select(Cell(slotCells, pending.GearSlot));
            else if (pending.ArmsBook) Select(Cell(bookCells, pending.BookSlot));
            else Select(PackCellOfBook(pending.BookId) ?? LoadoutEntry());
        }

        private void EndPending()
        {
            if (_pending.Kind == LoadoutPendingKind.SlotArmed) _filter = _filterBeforeArm;
            _pending = LoadoutPending.None;
            menu?.ClearStateLine();
        }

        private Selectable PackCellOfBook(string bookId)
        {
            int index = _visible.FindIndex(e => e.Kind == PackEntryKind.Book && e.BookId == bookId);
            if (index < 0) return null;

            _scrollRow = Mathf.Clamp(index / DossierLayout.PackColumns - DossierLayout.PackRows + 1, 0, MaxScrollRow());
            BindPackWindow();
            return Cell(packCells, index - _scrollRow * DossierLayout.PackColumns);
        }

        private static void Select(Selectable control)
        {
            if (control != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(control.gameObject);
        }

        private static Selectable Cell(Button[] cells, int index) =>
            cells != null && index >= 0 && index < cells.Length ? cells[index] : null;

        // ---- putting gear on and taking it off ------------------------------------------------

        private void EquipFromPack(Character character, PackEntry entry, EquipmentSlot? preferredSlot)
        {
            if (SystemMenuController.Refuses(menu, MenuAction.EquipGear)) return;

            var save = SaveSlotManager.CurrentSave;
            if (character?.equipment == null || save == null) return;

            // The row's whole copy (ItemInstance), so a rolled item keeps its
            // affixes and a lot copy its provenance; EquipmentOps measures and
            // rescales carried health around the swap.
            if (!EquipmentOps.Equip(Context, save, character, entry.Item.Instance, entry.Item.Slot,
                    entry.Item.IsEquippable, preferredSlot))
            {
                return;
            }

            // WRITTEN IMMEDIATELY: gear that vanishes because the game closed
            // between an equip and a save is the least forgivable thing here.
            SaveSlotManager.SaveCurrent();
            Refresh();
            RepaintCompareForSelection();
        }

        private void UnequipSlot(int index)
        {
            var character = Current();
            var save = SaveSlotManager.CurrentSave;
            if (character?.equipment == null || save == null) return;

            if (SystemMenuController.Refuses(menu, MenuAction.UnequipGear)) return;

            if (!EquipmentOps.Unequip(Context, save, character, EquipmentSlots.All[index])) return;

            SaveSlotManager.SaveCurrent();
            Refresh();
            RepaintCompareForSelection();
        }

        // ---- the compare column --------------------------------------------------------------

        // What is under the focus after something moved under it: the pack
        // shifted, or a slot changed hands.
        private void RepaintCompareForSelection()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

            int cell = IndexIn(packCells, selected);
            if (cell >= 0 && packCells[cell].gameObject.activeInHierarchy) { PaintCompareForPackCell(cell); return; }

            int slot = IndexIn(slotCells, selected);
            if (slot >= 0) { PaintCompareForGearSlot(slot); return; }

            int book = IndexIn(bookCells, selected);
            if (book >= 0) { PaintCompareForBookSlot(book); return; }

            PaintCompareStats();
        }

        // Nothing focused: the character's derived stats.
        private void PaintCompareStats()
        {
            var character = Current();
            if (character == null) return;

            SetCompareHead(UiStrings.CompareStats.Format(), "", "");

            PaintCurrentStatRows(character);
        }

        private void PaintCompareForGearSlot(int index)
        {
            var character = Current();
            if (character == null || index < 0 || index >= EquipmentSlots.All.Length) return;

            var slot = EquipmentSlots.All[index];
            string itemId = character.equipment?.Get(slot);
            var item = string.IsNullOrEmpty(itemId) ? null : ContentDatabase.GetItem(itemId);

            if (item == null)
            {
                SetCompareHead(UiStrings.EquipmentSlotName(slot).Format(), UiStrings.CompareEmptySlot.Format(), "");
                PaintCurrentStatRows(character);
                return;
            }

            int plus = character.equipment.GetPlus(slot);
            var riftTier = (RiftTier)character.equipment.GetRiftTier(slot);
            var modifierIds = character.equipment.GetModifierIds(slot);

            SetCompareHead(RarityColors.Wrap(item, plus), KindLine(item),
                ItemDescription.CardSummary(item, plus, character, riftTier, modifierIds));
            PaintCurrentStatRows(character);
        }

        private void PaintCompareForBookSlot(int index)
        {
            var character = Current();
            if (character == null) return;

            string skillId = BookIn(character.definitionId, index);
            var skill = string.IsNullOrEmpty(skillId) ? null : ContentDatabase.GetSkill(skillId);

            if (skill == null)
            {
                SetCompareHead(UiStrings.LoadoutBooksHeading.Format(), UiStrings.CompareEmptyBookSlot.Format(), "");
                HideStatRows();
                return;
            }

            PaintCompareForBook(skill);
        }

        // A book says what it teaches: its cost in the pool's own name, and
        // what it does.
        private void PaintCompareForBook(SkillDefinition skill)
        {
            string cost = GlossaryEntries.SpellCost(skill);
            string description = skill.Data.Description ?? "";
            string body = string.IsNullOrWhiteSpace(description) ? cost : $"{cost}\n{description}";

            SetCompareBody(skill.Data.DisplayName, UiStrings.CompareKindBook.Format(), body);
        }

        private void PaintCompareForPackCell(int cell)
        {
            var character = Current();
            var entry = EntryAtCell(cell);
            if (character == null || !entry.HasValue) { PaintCompareStats(); return; }

            var e = entry.Value;

            if (e.Kind == PackEntryKind.Book)
            {
                var skill = ContentDatabase.GetSkill(e.BookId);
                if (skill != null) PaintCompareForBook(skill);
                else SetCompareHead(e.BookName, UiStrings.CompareKindBook.Format(), "");
                return;
            }

            var item = ContentDatabase.GetItem(e.Item.Id);
            if (item == null)
            {
                SetCompareHead(e.Item.Name, "", "");
                HideStatRows();
                return;
            }

            string description = ItemDescription.CardSummary(item, e.Item.Plus,
                riftTier: e.Item.RiftTier, modifierIds: e.Item.ModifierIds);

            if (e.Kind == PackEntryKind.Salve)
            {
                SetCompareBody(RarityColors.Wrap(item, e.Item.Plus), UiStrings.CompareKindSalve.Format(), description);
                return;
            }

            // Gear: what each stat would become, "280 → 338 (+58)", for the
            // stats that move -- by the same hypothetical-equip pass the rest
            // of the game compares with (ItemDescription.Compare).
            int shown = PaintDeltaRows(character, item, e.Item.Plus);
            if (shown == 0) description = $"{description}\n{UiStrings.CompareNoChange.Format()}";

            SetCompareHead(RarityColors.Wrap(item, e.Item.Plus), KindLine(item), description);
        }

        private int PaintDeltaRows(Character character, ItemDefinition item, int plus)
        {
            var comparison = ItemDescription.Compare(character, item, plus);
            var previewScores = _scores + comparison.ScoreDelta;
            var stats = ContentDatabase.EffectiveStats(character);

            int row = 0;
            foreach (var stat in SheetStats.Derived)
            {
                if (!StatApplies(stat)) continue;

                int delta = DeltaFor(stat, comparison, previewScores);
                if (delta == 0 || !TryCurrentValue(character, stat, stats, _scores, out int now)) continue;

                string text = LoadoutCompare.DeltaText(
                    SheetStats.DisplayText(stat, now), SheetStats.DisplayText(stat, now + delta), delta);
                SetCompareRow(row++, StatLabel(stat), text, delta > 0 ? Good : Bad);
            }

            for (int i = row; i < SheetStats.Derived.Length; i++) SetCompareRow(i, "", "", TextBright);
            return row;
        }

        // Every stat that applies to this character, closed up in order: a
        // row that does not apply leaves no hole, and the rows past the last
        // are blank.
        private void PaintCurrentStatRows(Character character)
        {
            var stats = ContentDatabase.EffectiveStats(character);

            int row = 0;
            foreach (var stat in SheetStats.Derived)
            {
                if (!StatApplies(stat)) continue;

                SetCompareRow(row++, StatLabel(stat), DisplayValue(character, stat, stats, _scores), TextBright);
            }

            for (int i = row; i < SheetStats.Derived.Length; i++) SetCompareRow(i, "", "", TextBright);
        }

        private void HideStatRows()
        {
            for (int i = 0; i < SheetStats.Derived.Length; i++) SetCompareRow(i, "", "", TextBright);
        }

        private void SetCompareHead(string name, string kind, string description)
        {
            if (compareName != null) compareName.SetContent(name ?? "");
            if (compareKind != null) compareKind.SetContent(kind ?? "");
            if (compareDescription != null)
            {
                compareDescription.gameObject.SetShown(true);
                compareDescription.SetContent(description ?? "");
            }

            if (compareBody != null) compareBody.gameObject.SetShown(false);

            PlaceCompareRows(DossierLayout.CompareRowsTopFor(!string.IsNullOrEmpty(kind), !string.IsNullOrEmpty(description)));
        }

        // The rows start under the last head line that has words, so "Stats"
        // over the character's own numbers has no hole between them.
        private void PlaceCompareRows(float rowsTop)
        {
            for (int i = 0; i < SheetStats.Derived.Length; i++)
            {
                MoveTo(compareStatNames, i, DossierLayout.CompareNameLineCentreY(i, rowsTop));
                MoveTo(compareStatValues, i, DossierLayout.CompareValueLineCentreY(i, rowsTop));
            }
        }

        private static void MoveTo(TMP_Text[] labels, int i, float centreY)
        {
            if (!Has(labels, i)) return;

            var rect = labels[i].rectTransform;
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, centreY);
        }

        // A book or a salve: its words take the column, and no stat row shows.
        private void SetCompareBody(string name, string kind, string body)
        {
            SetCompareHead(name, kind, "");
            HideStatRows();

            if (compareDescription != null) compareDescription.gameObject.SetShown(false);
            if (compareBody == null) return;

            compareBody.gameObject.SetShown(true);
            compareBody.SetContent(body ?? "");
        }

        private void SetCompareRow(int index, string name, string value, Color colour)
        {
            if (Has(compareStatNames, index)) compareStatNames[index].SetContent(name);
            if (Has(compareStatValues, index))
            {
                compareStatValues[index].SetContent(value);
                compareStatValues[index].color = colour;
            }
        }

        private static string KindLine(ItemDefinition item)
        {
            var kind = item.kind == ItemKind.Weapon ? UiStrings.CompareKindWeapon
                : item.kind == ItemKind.Consumable ? UiStrings.CompareKindSalve
                : UiStrings.CompareKindArmour;
            return UiStrings.CompareKindTier.Format(kind.Format(), item.tier);
        }

        // ---- the pad's map (spec section 9, LOADOUT) ------------------------------------------

        private Selectable LoadoutEntry() => Cell(slotCells, System.Array.IndexOf(EquipmentSlots.All, EquipmentSlot.Head));

        // The slot column as rows, top to bottom, each left to right: the four
        // gear rows DossierLayout.GearCell draws, then the book row when the
        // character can carry books.
        private List<List<Selectable>> SlotRows()
        {
            var rows = new List<List<Selectable>>();
            for (int r = 0; r < DossierLayout.GearRows; r++) rows.Add(new List<Selectable>());

            var byX = EquipmentSlots.All
                .Select((slot, index) =>
                {
                    DossierLayout.GearCell(slot, out int row, out float x);
                    return (row, x, cell: Cell(slotCells, index));
                })
                .Where(t => t.cell != null)
                .OrderBy(t => t.x);

            foreach (var t in byX) rows[t.row].Add(t.cell);

            if (_canHoldSpellBooks && bookCells != null)
            {
                rows.Add(bookCells.Where(b => b != null && b.gameObject.activeSelf).Cast<Selectable>().ToList());
            }

            return rows.Where(r => r.Count > 0).ToList();
        }

        private void DeclareLoadout(List<UiNavGroup<Selectable>> groups, List<UiNavLink<Selectable>?> links)
        {
            var rows = SlotRows();
            var rail = SectionRow(CharacterSection.Loadout);
            var cells = BoundPackCells();
            var filters = filterChips?.Where(f => f != null).Cast<Selectable>().ToList() ?? new List<Selectable>();

            // The slots' right edge: the last member of every row, which is
            // what Left from the pack reaches.
            var rightEdge = rows.Select(r => r[r.Count - 1]).ToList();

            for (int r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                for (int i = 0; i < row.Count; i++)
                {
                    var cell = row[i];

                    links.Add(RuntimeNavWiring.Link(cell, UiNavDirection.Left, i > 0 ? row[i - 1] : rail));

                    var right = i < row.Count - 1
                        ? row[i + 1]
                        : cells.Count > 0 ? Nearest(cells, cell, horizontal: true) : Nearest(filters, cell, horizontal: true);
                    links.Add(RuntimeNavWiring.Link(cell, UiNavDirection.Right, right));

                    links.Add(RuntimeNavWiring.Link(cell, UiNavDirection.Up, r > 0 ? Nearest(rows[r - 1], cell, horizontal: false) : Tab));
                    if (r < rows.Count - 1) links.Add(RuntimeNavWiring.Link(cell, UiNavDirection.Down, Nearest(rows[r + 1], cell, horizontal: false)));
                }
            }

            // The filters: a rail over the grid.
            groups.Add(RuntimeNavWiring.Group("dossierFilters", UiNavGroupKind.Rail, filters, wrap: UiNavWrap.Clamp));
            foreach (var chip in filters)
            {
                links.Add(RuntimeNavWiring.Link(chip, UiNavDirection.Up, Tab));
                links.Add(RuntimeNavWiring.Link(chip, UiNavDirection.Down, Nearest(cells, chip, horizontal: false)));
            }

            if (filters.Count > 0) links.Add(RuntimeNavWiring.Link(filters[0], UiNavDirection.Left, Nearest(rightEdge, filters[0], horizontal: true)));

            // The grid, cell by cell: the bound cells are always a prefix of
            // the window, so a cell's index is its place.
            int columns = DossierLayout.PackColumns;
            for (int k = 0; k < cells.Count; k++)
            {
                int column = k % columns;
                int row = k / columns;
                var cell = cells[k];

                links.Add(RuntimeNavWiring.Link(cell, UiNavDirection.Left,
                    column > 0 ? cells[k - 1] : Nearest(rightEdge, cell, horizontal: true)));
                if (column < columns - 1 && k + 1 < cells.Count) links.Add(RuntimeNavWiring.Link(cell, UiNavDirection.Right, cells[k + 1]));

                // Off the top: the filters, unless the window can scroll up
                // (EdgeScroll takes that press).
                if (row > 0) links.Add(RuntimeNavWiring.Link(cell, UiNavDirection.Up, cells[k - columns]));
                else if (_scrollRow == 0) links.Add(RuntimeNavWiring.Link(cell, UiNavDirection.Up, Nearest(filters, cell, horizontal: false)));

                // Down: the cell below, or the last cell of a short last row.
                if (k + columns < cells.Count) links.Add(RuntimeNavWiring.Link(cell, UiNavDirection.Down, cells[k + columns]));
                else if ((row + 1) * columns < cells.Count) links.Add(RuntimeNavWiring.Link(cell, UiNavDirection.Down, cells[cells.Count - 1]));
            }
        }

        // ---- the footer's words for the loadout (spec section 8) --------------------------------

        private MenuPrompt? GearSlotPrompt(int index)
        {
            if (WornAt(index)) return new MenuPrompt(PromptButton.Confirm, UiStrings.PromptUnequip, MenuAction.UnequipGear);
            return new MenuPrompt(PromptButton.Confirm, UiStrings.PromptChooseFromPack, MenuAction.EquipGear);
        }

        private MenuPrompt? BookSlotPrompt(int index)
        {
            if (_pending.Kind == LoadoutPendingKind.BookPicked)
            {
                return new MenuPrompt(PromptButton.Confirm, UiStrings.PromptReplaceThisBook, MenuAction.ReplaceBook);
            }

            if (BookAt(index)) return new MenuPrompt(PromptButton.Confirm, UiStrings.PromptRemove, MenuAction.RemoveBook);
            return new MenuPrompt(PromptButton.Confirm, UiStrings.PromptChooseFromPack, MenuAction.PlaceBook);
        }

        private MenuPrompt? PackCellPrompt(int cell)
        {
            var entry = EntryAtCell(cell);
            if (!entry.HasValue) return null;

            switch (entry.Value.Kind)
            {
                case PackEntryKind.Gear:
                    return new MenuPrompt(PromptButton.Confirm, UiStrings.PromptEquip, MenuAction.EquipGear);
                case PackEntryKind.Book:
                    return new MenuPrompt(PromptButton.Confirm,
                        _pending.ArmsBook ? UiStrings.PromptEquip : UiStrings.PromptPlaceBook, MenuAction.PlaceBook);
                default:
                    return null;
            }
        }
    }
}

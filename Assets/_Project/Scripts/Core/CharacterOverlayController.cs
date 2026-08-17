using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // The character overlay, driven.
    //
    // Every rule about what can be worn lives in EquipMove and BagView, in
    // Domain, with their own tests. This resolves ids to content, paints, and
    // asks -- it decides nothing.
    //
    // Scene-agnostic by construction: it reads SaveSlotManager and nothing
    // else, so mounting the same tree in the descent map later needs no change
    // here.
    public class CharacterOverlayController : MonoBehaviour
    {
        [SerializeField] internal Button[] slotCells;
        [SerializeField] internal Image[] slotIcons;
        [SerializeField] internal Image[] slotBackings;
        [SerializeField] internal Image[] slotRarityEdges;
        [SerializeField] internal TMP_Text[] slotPlusLabels;

        [SerializeField] internal Button[] bagCells;
        [SerializeField] internal Image[] bagIcons;
        [SerializeField] internal Image[] bagBackings;
        [SerializeField] internal Image[] bagRarityEdges;
        [SerializeField] internal TMP_Text[] bagCountLabels;
        [SerializeField] internal TMP_Text[] bagPlusLabels;

        [SerializeField] internal TMP_Text characterName;
        [SerializeField] internal TMP_Text pageLabel;
        [SerializeField] internal GameObject bagEmptyHint;
        [SerializeField] internal GameObject detailPlate;
        [SerializeField] internal TMP_Text detailName;
        [SerializeField] internal TMP_Text detailBody;
        [SerializeField] internal TMP_Text actionLabel;
        [SerializeField] internal Button actionButton;
        [SerializeField] internal Button prevCharacterButton;
        [SerializeField] internal Button nextCharacterButton;
        [SerializeField] internal Button prevPageButton;
        [SerializeField] internal Button nextPageButton;
        [SerializeField] internal Button closeButton;

        // The armour stand. Its art lands late, so the same rule icons follow
        // applies here: a spriteless Image is a solid white rectangle, and at
        // 520x780 that is the largest one this project could produce.
        [SerializeField] internal Image silhouette;

        // The two panes and their tabs.
        [SerializeField] internal GameObject characterPane;
        [SerializeField] internal GameObject inventoryPane;
        [SerializeField] internal Button characterTab;
        [SerializeField] internal Button inventoryTab;
        [SerializeField] internal Image characterTabMarker;
        [SerializeField] internal Image inventoryTabMarker;

        // Flat, in SheetStats.All order. Only the values are bound: the names
        // are static labels the builder already wrote.
        [SerializeField] internal TMP_Text[] statValues;

        // The hover box.
        [SerializeField] internal RectTransform comparePlate;
        [SerializeField] internal TMP_Text compareName;
        [SerializeField] internal TMP_Text compareBody;

        // Set at BUILD time, per scene, rather than detected at runtime.
        //
        // The hub's copy is free and the fight's copy is locked. Deciding this
        // from a serialized bool means no static "is a fight happening" flag to
        // leak between tests (AUDIT #23) and no FindFirstObjectByType, which
        // this project retired.
        [SerializeField] internal bool lockedForFight;

        // Art, as two parallel arrays: a scene serialises arrays and does not
        // serialise dictionaries.
        [SerializeField] internal string[] iconIds;
        [SerializeField] internal Sprite[] iconSprites;

        private static readonly Color CellEmpty = new Color(0.10f, 0.06f, 0.16f, 0.50f);
        private static readonly Color CellFilled = new Color(0.14f, 0.09f, 0.21f, 1f);
        private static readonly Color CellSelected = new Color(0.28f, 0.20f, 0.40f, 1f);
        private static readonly Color EdgeHidden = new Color(1f, 1f, 1f, 0f);

        private int _character;
        private int _page;

        // Exactly one of these is ever set. A selection in both panes at once
        // would leave the action button with two possible meanings.
        private int _selectedBag = -1;
        private EquipmentSlot? _selectedSlot;

        private bool _wired;
        private IReadOnlyList<BagItem> _sorted = new List<BagItem>();

        // Which pane is up. The sheet opens on the character, because that is
        // what "open my character sheet" means -- the bag is a second thought.
        private bool _showingInventory;

        private static readonly Color TabLive = new Color(0.949f, 0.859f, 0.620f, 1f);
        private static readonly Color TabIdle = new Color(0.949f, 0.859f, 0.620f, 0f);

        private void Start()
        {
            Wire();

            // Hidden until the art exists rather than left showing a white
            // slab. Checked once: the sprite is bound at build time and cannot
            // change while the game runs.
            if (silhouette != null) silhouette.enabled = silhouette.sprite != null;
        }

        // Refresh on OPEN, not on Start: the overlay is entered repeatedly and
        // the save moves underneath it between visits.
        private void OnEnable()
        {
            if (slotCells != null) Refresh();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            for (int i = 0; i < slotCells.Length; i++)
            {
                int index = i;
                slotCells[i].onClick.AddListener(() => SelectSlot(index));
            }

            for (int i = 0; i < bagCells.Length; i++)
            {
                int index = i;
                bagCells[i].onClick.AddListener(() => SelectBag(index));
            }

            prevCharacterButton.onClick.AddListener(() => StepCharacter(-1));
            nextCharacterButton.onClick.AddListener(() => StepCharacter(1));
            prevPageButton.onClick.AddListener(() => StepPage(-1));
            nextPageButton.onClick.AddListener(() => StepPage(1));
            actionButton.onClick.AddListener(Commit);
            closeButton.onClick.AddListener(() => gameObject.SetActive(false));

            if (characterTab != null) characterTab.onClick.AddListener(() => ShowPane(inventory: false));
            if (inventoryTab != null) inventoryTab.onClick.AddListener(() => ShowPane(inventory: true));
        }

        // ---- panes ----------------------------------------------------------------

        // Which half is up, for whoever owns the key that opened it.
        public bool ShowingInventory => _showingInventory;

        // Read-only, like the hub's own open/closed accessor: callers outside
        // Core can ask whether this copy is the live one without being able to
        // flip it. Which scene got which is decided once, at build time.
        public bool EquipLocked => lockedForFight;

        // Opened onto a named pane, so C lands on the character and I on the
        // bag rather than both landing on whichever was up last. Called by
        // whoever owns the key -- the hub in the hub, the fight in the fight.
        public void OpenOn(bool inventory)
        {
            _showingInventory = inventory;
            ClearSelection();
            HideCompare();

            // SetActive AFTER the pane is chosen: OnEnable calls Refresh, and
            // refreshing into the pane the player is leaving would paint one
            // frame of the wrong half.
            gameObject.SetActive(true);
            if (isActiveAndEnabled) Refresh();
        }

        // The character sheet does not show the bag, and the bag does not show
        // the paperdoll. One surface carrying both is what left eight slot
        // cells with nowhere to sit but on top of the figure they describe.
        private void ShowPane(bool inventory)
        {
            _showingInventory = inventory;

            // A selection cannot survive the switch: the action button would
            // otherwise still be offering to equip something the player can no
            // longer see.
            ClearSelection();
            HideCompare();
            Refresh();
        }

        private void PaintPanes()
        {
            if (characterPane != null) characterPane.SetActive(!_showingInventory);
            if (inventoryPane != null) inventoryPane.SetActive(_showingInventory);

            if (characterTabMarker != null)
                characterTabMarker.color = _showingInventory ? TabIdle : TabLive;
            if (inventoryTabMarker != null)
                inventoryTabMarker.color = _showingInventory ? TabLive : TabIdle;
        }

        // ---- what it reads ------------------------------------------------------

        private SaveData Save => SaveSlotManager.CurrentSave;

        private List<Character> Roster => Save?.roster ?? new List<Character>();

        private Character Current =>
            Roster.Count == 0 ? null : Roster[Mathf.Clamp(_character, 0, Roster.Count - 1)];

        // stockpiledItems is the single live bag for now. activeRun.inventory
        // stays dormant until mid-run drops exist -- so items are not at risk
        // on a defeat, which matches gold being the only at-risk currency.
        private List<InventoryEntry> Bag => Save?.stockpiledItems ?? new List<InventoryEntry>();

        // ---- input ----------------------------------------------------------------

        private void SelectBag(int index)
        {
            _selectedSlot = null;
            _selectedBag = index < _sorted.Count - _page * BagView.CellCount ? index : -1;
            Refresh();
        }

        private void SelectSlot(int index)
        {
            _selectedBag = -1;
            _selectedSlot = index >= 0 && index < EquipmentSlots.All.Length
                ? EquipmentSlots.All[index]
                : (EquipmentSlot?)null;
            Refresh();
        }

        private void StepCharacter(int direction)
        {
            if (Roster.Count <= 1) return;

            _character = (_character + direction + Roster.Count) % Roster.Count;

            // A different character wears different things, so a selection
            // cannot survive the switch -- it would describe a slot that is no
            // longer under the cursor.
            ClearSelection();
            Refresh();
        }

        private void StepPage(int direction)
        {
            _page = BagView.ClampPage(_page + direction, _sorted.Count);
            ClearSelection();
            Refresh();
        }

        private void ClearSelection()
        {
            _selectedBag = -1;
            _selectedSlot = null;
        }

        private void Commit()
        {
            // Gear is locked for the duration of a battle. The sheet stays
            // fully readable -- which is the point of it being reachable at all
            // mid-fight -- but nothing moves in or out of a slot.
            //
            // Checked HERE rather than only on the button, because the double
            // click reaches this by another road and a rule enforced at one of
            // two entrances is not enforced.
            if (lockedForFight) return;

            var character = Current;
            if (character?.equipment == null || Save == null) return;

            bool moved = _selectedSlot.HasValue
                ? EquipMove.TryUnequip(character.equipment, Bag, _selectedSlot.Value)
                : TryEquipSelected(character);

            if (!moved) return;

            // Written immediately. Gear that vanishes because the game closed
            // between an equip and a save is the least forgivable thing this
            // screen could do.
            SaveSlotManager.SaveCurrent();
            ClearSelection();
            Refresh();
        }

        private bool TryEquipSelected(Character character)
        {
            var picked = SelectedBagItem();
            if (!picked.HasValue) return false;

            var item = picked.Value;
            return EquipMove.TryEquip(character.equipment, Bag, item.Id, item.Slot,
                item.IsEquippable, plus: item.Plus);
        }

        // ---- painting ---------------------------------------------------------------

        public void Refresh()
        {
            var character = Current;
            if (character == null) return;

            _sorted = BagView.Sorted(Bag.Select(ToBagItem).Where(i => i.Count > 0));
            _page = BagView.ClampPage(_page, _sorted.Count);

            characterName.SetContent(character.definitionId);
            pageLabel.Set(UiStrings.OverlayPage, _page + 1, BagView.PageCount(_sorted.Count));

            // An empty bag says so, and takes its pager with it. The cells
            // already hide themselves when unused, which on an empty bag left a
            // blank rectangle sitting under "PAGE 1 OF 1" -- furniture for a
            // thing that is not there.
            bool carrying = _sorted.Count > 0;
            if (bagEmptyHint != null) bagEmptyHint.SetActive(!carrying);
            pageLabel.gameObject.SetActive(carrying);
            prevPageButton.gameObject.SetActive(carrying);
            nextPageButton.gameObject.SetActive(carrying);

            PaintPanes();
            PaintSlots(character);
            PaintBag();
            PaintStats(character);
            PaintDetail(character);

            prevCharacterButton.interactable = Roster.Count > 1;
            nextCharacterButton.interactable = Roster.Count > 1;
        }

        // The thirteen numbers, from the SAME EffectiveStats the fight now
        // reads. Before this the sheet showed no numbers at all -- the one
        // screen for deciding what to wear never said what wearing it did.
        private void PaintStats(Character character)
        {
            if (statValues == null) return;

            var stats = ContentDatabase.EffectiveStats(character);
            var scores = ContentDatabase.EffectiveAbilityScores(character);

            for (int i = 0; i < statValues.Length && i < SheetStats.All.Length; i++)
            {
                if (statValues[i] == null) continue;
                statValues[i].Set(UiStrings.SheetStatValue,
                    SheetStats.ValueOf(SheetStats.All[i], stats, scores));
            }
        }

        // ---- hover and double click -------------------------------------------------

        // Called by CellPointer, which is attached to every cell at build time.
        // The controller is found by walking up the tree rather than wired
        // through a delegate, because this Wire step runs at BUILD time and
        // delegates do not serialise into a scene.
        internal void HoverCell(int index, bool isBagCell, bool entered)
        {
            if (!entered)
            {
                HideCompare();
                return;
            }

            var character = Current;
            if (character == null || comparePlate == null) return;

            ItemDefinition item;
            int plusValue;
            Button cell;

            if (isBagCell)
            {
                var page = BagView.Page(_sorted, _page);
                if (index < 0 || index >= page.Count) { HideCompare(); return; }

                item = ContentDatabase.GetItem(page[index].Id);
                plusValue = page[index].Plus;
                cell = bagCells[index];
            }
            else
            {
                if (index < 0 || index >= EquipmentSlots.All.Length) { HideCompare(); return; }

                var slot = EquipmentSlots.All[index];
                string id = character.equipment.Get(slot);
                if (string.IsNullOrEmpty(id)) { HideCompare(); return; }

                item = ContentDatabase.GetItem(id);
                plusValue = character.equipment.GetPlus(slot);
                cell = slotCells[index];
            }

            if (item == null) { HideCompare(); return; }

            compareName.SetContent(RarityColors.NameOf(item, plusValue));

            // The same comparison the detail plate uses, so hovering and
            // selecting can never tell the player two different things.
            string body = ItemDescription.ComparisonBody(character, item, plusValue);
            if (lockedForFight)
            {
                body = body.Length == 0
                    ? UiStrings.OverlayLockedInFight.Format()
                    : body + "\n\n" + UiStrings.OverlayLockedInFight.Format();
            }

            compareBody.SetContent(body);
            comparePlate.gameObject.SetActive(true);
            PlaceCompareBeside(cell);
        }

        // Beside the cell, flipping to its other side rather than running off
        // the panel. A box that leaves the screen is the same as no box.
        private void PlaceCompareBeside(Button cell)
        {
            var cellRect = cell.transform as RectTransform;
            var parent = comparePlate.parent as RectTransform;
            if (cellRect == null || parent == null) return;

            Vector3 centre = parent.InverseTransformPoint(cellRect.TransformPoint(Vector3.zero));

            float halfCell = cellRect.rect.width * 0.5f;
            float halfBox = comparePlate.rect.width * 0.5f;
            float x = centre.x + halfCell + OverlayAnchors.CompareGap + halfBox;

            float limit = parent.rect.width * 0.5f;
            if (x + halfBox > limit)
            {
                x = centre.x - halfCell - OverlayAnchors.CompareGap - halfBox;
            }

            // Kept inside vertically too, so a cell on the bottom row does not
            // push half the box under the detail plate.
            float halfHeight = comparePlate.rect.height * 0.5f;
            float y = Mathf.Clamp(centre.y, -parent.rect.height * 0.5f + halfHeight,
                                            parent.rect.height * 0.5f - halfHeight);

            comparePlate.anchoredPosition = new Vector2(x, y);
        }

        private void HideCompare()
        {
            if (comparePlate != null) comparePlate.gameObject.SetActive(false);
        }

        // A double click does what the action button would have done, so the
        // gesture and the button can never disagree -- both land on Commit.
        internal void ActivateCell(int index, bool isBagCell)
        {
            if (isBagCell) SelectBag(index);
            else SelectSlot(index);

            Commit();
        }

        private BagItem ToBagItem(InventoryEntry entry)
        {
            var item = ContentDatabase.GetItem(entry.itemId);
            if (item == null)
            {
                // A save naming content that no longer exists. Shown as itself
                // rather than dropped, so the player can see what happened
                // instead of watching the bag quietly shrink.
                return new BagItem(entry.itemId, entry.itemId, 0, EquipmentSlot.Weapon1,
                    0, entry.plus, entry.count, "", false);
            }

            return new BagItem(item.id, RarityColors.NameOf(item, entry.plus), (int)item.kind,
                item.equipSlot, item.tier, entry.plus, entry.count, item.iconPath, item.IsEquippable);
        }

        private void PaintSlots(Character character)
        {
            for (int i = 0; i < slotCells.Length && i < EquipmentSlots.All.Length; i++)
            {
                var slot = EquipmentSlots.All[i];
                string id = character.equipment.Get(slot);
                var item = string.IsNullOrEmpty(id) ? null : ContentDatabase.GetItem(id);
                bool selected = _selectedSlot == slot;

                PaintCell(slotBackings[i], slotRarityEdges[i], slotIcons[i], null, slotPlusLabels[i],
                    item, character.equipment.GetPlus(slot), 1, selected);
            }
        }

        private void PaintBag()
        {
            var page = BagView.Page(_sorted, _page);

            for (int i = 0; i < bagCells.Length; i++)
            {
                bool present = i < page.Count;

                // An unused cell is HIDDEN, not drawn empty. A grid of twenty
                // boxes holding four things reads as sixteen things that failed
                // to load.
                bagCells[i].gameObject.SetActive(present);
                if (!present) continue;

                var entry = page[i];
                var item = ContentDatabase.GetItem(entry.Id);

                PaintCell(bagBackings[i], bagRarityEdges[i], bagIcons[i], bagCountLabels[i],
                    bagPlusLabels[i], item, entry.Plus, entry.Count, _selectedBag == i);
            }
        }

        private void PaintCell(Image backing, Image edge, Image icon, TMP_Text count, TMP_Text plus,
                               ItemDefinition item, int plusValue, int stackCount, bool selected)
        {
            bool filled = item != null;

            backing.color = selected ? CellSelected : filled ? CellFilled : CellEmpty;

            // Rarity is the item's, so an empty cell has none -- the strip goes
            // fully transparent rather than to some "empty" colour that would
            // read as a seventh band.
            edge.color = filled ? RarityColors.For(item) : EdgeHidden;

            ItemIcons.Apply(icon, iconIds, iconSprites, filled ? item.id : null);

            // A +0 badge on every plain item is noise; the plus only appears
            // when there is one.
            bool honed = filled && plusValue > 0;
            plus.gameObject.SetActive(honed);
            if (honed) plus.Set(UiStrings.OverlayPlus, plusValue);

            if (count == null) return;

            bool stacked = filled && stackCount > 1;
            count.gameObject.SetActive(stacked);
            if (stacked) count.Set(UiStrings.OverlayCount, stackCount);
        }

        private void PaintDetail(Character character)
        {
            var item = SelectedItem(character, out int plusValue);

            if (item == null)
            {
                detailName.SetContent("");
                detailBody.SetContent("");
                // The plate goes too. Blanking the labels and leaving the slab
                // is how the overlay came to open onto its own empty furniture:
                // the plate frames an answer, and nothing has been asked yet.
                if (detailPlate != null) detailPlate.SetActive(false);
                actionButton.gameObject.SetActive(false);
                return;
            }

            if (detailPlate != null) detailPlate.SetActive(true);
            actionButton.gameObject.SetActive(true);
            detailName.SetContent(RarityColors.NameOf(item, plusValue));
            detailBody.SetContent(Describe(character, item, plusValue));

            // In a fight the button says why it will not work rather than
            // simply refusing the press. A dead control that looks alive is
            // the thing this screen has been bitten by before.
            if (lockedForFight)
            {
                actionLabel.Set(UiStrings.OverlayLockedInFight);
                actionButton.interactable = false;
                return;
            }

            if (_selectedSlot.HasValue)
            {
                actionLabel.Set(UiStrings.OverlayUnequip);
                actionButton.interactable = true;
                return;
            }

            // Everything stays selectable, including what cannot be worn --
            // pressing it is how the plate gets to explain why. A cell that
            // simply refuses the click can only say no by doing nothing.
            actionLabel.Set(item.IsEquippable ? UiStrings.OverlayEquip : UiStrings.OverlayCannotWear);
            actionButton.interactable = item.IsEquippable;
        }

        private ItemDefinition SelectedItem(Character character, out int plusValue)
        {
            plusValue = 0;

            if (_selectedSlot.HasValue)
            {
                string id = character.equipment.Get(_selectedSlot.Value);
                if (string.IsNullOrEmpty(id)) return null;

                plusValue = character.equipment.GetPlus(_selectedSlot.Value);
                return ContentDatabase.GetItem(id);
            }

            var picked = SelectedBagItem();
            if (!picked.HasValue) return null;

            plusValue = picked.Value.Plus;
            return ContentDatabase.GetItem(picked.Value.Id);
        }

        private BagItem? SelectedBagItem()
        {
            if (_selectedBag < 0) return null;

            var page = BagView.Page(_sorted, _page);
            return _selectedBag < page.Count ? page[_selectedBag] : (BagItem?)null;
        }

        // The plate printed a slot name and the flavour text and stopped there,
        // which made the one screen for deciding what to wear the one screen
        // that never said what anything did. It now leads with that header and
        // hands over to ItemDescription for the numbers, the requirement, and
        // what the swap would cost elsewhere.
        private static string Describe(Character character, ItemDefinition item, int plus)
        {
            if (!item.IsEquippable)
            {
                return string.IsNullOrEmpty(item.description) ? "A consumable." : item.description;
            }

            string slot = EquipmentSlots.DisplayName(item.equipSlot);
            string header = string.IsNullOrEmpty(item.description) ? slot : $"{slot}  ·  {item.description}";

            // Against the character this overlay is actually showing. Comparing
            // against whoever happens to be first in the roster would be worse
            // than comparing against nothing, because it would look right.
            string body = ItemDescription.ComparisonBody(character, item, plus);

            return body.Length == 0 ? header : $"{header}\n\n{body}";
        }
    }
}

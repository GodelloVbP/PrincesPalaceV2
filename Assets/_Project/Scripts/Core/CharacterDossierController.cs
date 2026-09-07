using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // Fills the character dossier, and drives the one mechanic it is built
    // around: hover an attribute, see what it is actually doing for you.
    //
    // The numbers come from the SAME ContentDatabase.Effective* calls the fight
    // uses, never from a second computation -- a sheet that recomputed its own
    // figures would eventually disagree with the battle, and the player would be
    // right either way.
    public class CharacterDossierController : MonoBehaviour
    {
        [SerializeField] internal TMP_Text characterName;
        [SerializeField] internal TMP_Text subLine;
        [SerializeField] internal RectTransform xpFill;
        [SerializeField] internal TMP_Text xpRemaining;

        // What the next reward-track level pays. See PaintTrackLine.
        [SerializeField] internal TMP_Text trackNext;
        [SerializeField] internal Button prevCharacterButton;
        [SerializeField] internal Button nextCharacterButton;

        [SerializeField] internal TMP_Text skillsCount;
        [SerializeField] internal Button packRow;

        // The reward track's row and the panel it opens. Same pair as the pack
        // below: a nav row and a panel that closes on its own button.
        [SerializeField] internal Button trackRow;
        [SerializeField] internal GameObject trackPanel;

        // THE PANEL'S OWN CONTROLLER, not just the GameObject it lives on --
        // so ShowTrack can tell it WHICH character to paint before it opens
        // (docs/PLAN_REWARD_TRACKS.md §8). Assigned by ScreenRegistry
        // (ScreenRegistry.WireSystemMenu), which is the one place that has
        // both wiring calls' return values in hand; not auto-bound, because
        // it is a controller reference rather than one of UiAutoBind's five
        // node types.
        [SerializeField] internal RewardTrackController trackScreen;
        [SerializeField] internal TMP_Text packChevron;
        [SerializeField] internal GameObject packPanel;
        [SerializeField] internal Button packCloseButton;

        // The spell-books panel (docs/PLAN_SHOP.md §1g, gate 3). Same
        // row+panel+close shape as Pack/Track above.
        [SerializeField] internal Button spellsRow;
        [SerializeField] internal TMP_Text spellsChevron;
        [SerializeField] internal TMP_Text spellsCount;
        [SerializeField] internal GameObject spellsPanel;
        [SerializeField] internal Button spellsCloseButton;
        [SerializeField] internal Button[] spellSlots;
        [SerializeField] internal TMP_Text[] spellSlotNames;
        [SerializeField] internal Image[] spellSlotSelections;
        [SerializeField] internal GameObject unassignedEmptyHint;
        [SerializeField] internal Button[] unassignedRows;
        [SerializeField] internal TMP_Text[] unassignedNames;
        [SerializeField] internal Image[] unassignedSelections;

        // The fight's copy is readable and inert; every other copy is live.
        //
        // Decided at BUILD time rather than sniffed at runtime, which is the
        // arrangement CharacterOverlayController already used for exactly this
        // -- gear is locked for the duration of a battle, and the sheet the
        // fight shows must not be able to change the fight it is describing.
        // Putting C and I on this menu made that invariant this screen's
        // problem: before, those keys reached a panel that had already been
        // wired inert.
        [SerializeField] internal bool lockedForFight;

        // Public because the serialized field is internal and the PlayMode test
        // assembly cannot see internals. Same name the overlay exposed, so the
        // test that pins "the fight's copy is inert" reads unchanged.
        public bool EquipLocked => lockedForFight;

        // The menu this dossier lives in -- same shape as ExitsController.menu,
        // and for the same reason: a second serialized bool here would be one
        // more thing that can disagree with the menu's own state. Set by
        // ScreenRegistry.WireSystemMenu, not part of WireDossier's signature,
        // because it is not carried by WireDossier's own parameters.
        //
        // Gates the dossier's refund minus: only the hub copy of this screen
        // (InDescent false) shows it, so a build revised mid-run can only be
        // reconsidered between descents, never undone inside one.
        [SerializeField] internal SystemMenuController menu;

        public bool InDescent => menu != null && menu.InDescent;

        [SerializeField] internal Button[] slotCells;
        [SerializeField] internal Image[] slotIcons;
        [SerializeField] internal TMP_Text[] slotLabels;
        [SerializeField] internal GameObject[] slotBlockedCaptions;

        [SerializeField] internal Button[] attributeCells;

        // One "+" per attribute cell, and the label saying how many points are
        // waiting. See Spend().
        [SerializeField] internal Button[] attributePluses;

        // One "-" per attribute cell, mirrored off the plus. See Refund().
        [SerializeField] internal Button[] attributeMinuses;
        [SerializeField] internal TMP_Text unspentPoints;
        [SerializeField] internal TMP_Text[] attributeValues;
        [SerializeField] internal TMP_Text[] attributeKeys;

        [SerializeField] internal Image[] slotRarityTicks;

        // ITEM-MODIFIER PLAN PHASE E: a rolled item's RiftTier, distinct from
        // its Rarity tick above -- an inset ring drawn ON the icon rather
        // than a corner tick, so the two axes never compete for the same
        // pixel. Hidden (SetShown false) at RiftTier.Ordinary, which is the
        // regression guard: an item that rolled nothing must look EXACTLY
        // as it did before this phase. See RiftTierColors.ShouldGlow.
        [SerializeField] internal Image[] slotRiftGlows;

        [SerializeField] internal Button[] packCells;
        [SerializeField] internal Image[] packIcons;
        [SerializeField] internal Image[] packRarityTicks;
        [SerializeField] internal Image[] packRiftGlows;
        [SerializeField] internal TMP_Text[] packCounts;
        [SerializeField] internal TMP_Text[] packNames;
        [SerializeField] internal Button[] packSortTabs;
        [SerializeField] internal GameObject[] packSortUnderlines;
        [SerializeField] internal Image packScrollTrack;
        [SerializeField] internal RectTransform packScrollThumb;
        [SerializeField] internal TMP_Text carriedValue;

        [SerializeField] internal GameObject tooltip;
        [SerializeField] internal TMP_Text tooltipTitle;
        [SerializeField] internal TMP_Text tooltipBody;

        [SerializeField] internal TMP_Text[] statValues;
        [SerializeField] internal TMP_Text[] statPreviews;
        [SerializeField] internal GameObject[] statHighlights;

        // The portrait slot. The FACES ARE NOT BOUND HERE any more: they were
        // an IconEntry[] baked by ScreenRegistry from every character with a
        // portraitPath, which meant the scene held a photograph of the roster
        // taken at scene-build time and a character authored afterwards showed
        // an empty plate no content rebuild could fill. CharacterPortraits
        // loads them off Resources by id instead.
        [SerializeField] internal Image portrait;

        // Item art, bound at build time from every ItemDefinition with an icon.
        [SerializeField] internal IconEntry[] icons;

        private static readonly Color Neutral = Hex(Domain.UiKit.Screens.CharacterDossierScreen.Text);
        private static readonly Color Dim = Hex(Domain.UiKit.Screens.CharacterDossierScreen.TextDim);
        private static readonly Color Accent = Hex(Domain.UiKit.Screens.CharacterDossierScreen.Accent);
        private static readonly Color Good = Hex("#8FB37A");
        private static readonly Color Bad = Hex("#D99A8C");
        private static readonly Color AccentHi = Hex(Domain.UiKit.Screens.CharacterDossierScreen.AccentHi);

        private bool _wired;
        private int _index;

        // Which attribute sits in which cell. Rebuilt per character because the
        // handover wants the grid ordered highest-first, so the shape of a build
        // reads off the top row -- which means cell 0 is not always Strength.
        private readonly List<AbilityScore> _cellOrder = new List<AbilityScore>();

        // Who _cellOrder was sorted for. See RefreshAttributes -- the order is
        // FROZEN while one character is on screen, and this is how it knows the
        // character changed.
        private string _cellOrderFor;

        // Held so the tooltip can measure the NEXT point from what the
        // character actually has, rather than from a neutral block.
        private AbilityScoreBlock _scores;
        private AbilityScoreBlock _previewScores;

        private void OnEnable()
        {
            Wire();
            Refresh();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            if (prevCharacterButton != null) prevCharacterButton.onClick.AddListener(() => Step(-1));
            if (nextCharacterButton != null) nextCharacterButton.onClick.AddListener(() => Step(1));
            if (packRow != null) packRow.onClick.AddListener(TogglePack);

            // Wired ABOVE the lockedForFight guard, so the track opens from the
            // fight's copy of the dossier too. It changes nothing -- it is a
            // list of what levelling gives -- and a player checking what is
            // coming mid-battle is exactly when they would want to.
            if (trackRow != null) trackRow.onClick.AddListener(ShowTrack);
            if (packCloseButton != null) packCloseButton.onClick.AddListener(() => ShowPack(false));

            if (spellsRow != null) spellsRow.onClick.AddListener(ToggleSpells);
            if (spellsCloseButton != null) spellsCloseButton.onClick.AddListener(() => ShowSpells(false));

            if (spellSlots != null)
            {
                for (int i = 0; i < spellSlots.Length; i++)
                {
                    int slot = i;
                    if (spellSlots[i] != null) spellSlots[i].onClick.AddListener(() => PressSlot(slot));
                }
            }

            if (unassignedRows != null)
            {
                for (int i = 0; i < unassignedRows.Length; i++)
                {
                    int row = i;
                    if (unassignedRows[i] != null) unassignedRows[i].onClick.AddListener(() => SelectUnassigned(row));
                }
            }

            // The attribute link, driven from hover AND from the button itself,
            // so a controller player reaches it too -- the handover asks for
            // that explicitly and it is one line here.
            if (attributeCells != null)
            {
                for (int i = 0; i < attributeCells.Length; i++)
                {
                    if (attributeCells[i] == null) continue;

                    int cell = i;
                    var hover = attributeCells[i].gameObject.AddComponent<HoverIndex>();
                    hover.Index = cell;
                    hover.Changed = OnAttributeHover;
                }
            }

            AttachHovers(slotCells, OnSlotHover);
            AttachHovers(packCells, OnPackHover);

            // Clicking equips; clicking a worn slot takes it off. A screen that
            // could only put gear ON would be a trap, so both gestures exist --
            // the same pair the old sheet had.
            //
            // Not attached at all in a fight, rather than attached and refused:
            // a button that visibly does nothing invites the player to press it
            // again, and there is nowhere on this screen to explain why.
            if (lockedForFight) return;

            // THE "+" PER SCORE, which is what Character.Invest's own comment
            // has always described and what nothing ever built (AUDIT #53).
            // Below the lockedForFight guard for the same reason the equip
            // handlers are: a stat point spent mid-battle would change the
            // fight the sheet is describing.
            //
            // Indexed by CELL, not by AbilityScore. The grid is re-ordered per
            // character so the build reads highest-first, so which score a cell
            // holds is a runtime fact -- _cellOrder is the only thing that
            // knows, and reading it at click time rather than at wire time is
            // what keeps the "+" pointing at the score under it after a
            // character step.
            if (attributePluses != null)
            {
                for (int i = 0; i < attributePluses.Length; i++)
                {
                    if (attributePluses[i] == null) continue;

                    int cell = i;
                    attributePluses[i].onClick.AddListener(() => Spend(cell));
                }
            }

            // THE "-" PER SCORE, beside the plus for the same reasons: below
            // the lockedForFight guard, indexed by cell and resolved through
            // _cellOrder at click time.
            if (attributeMinuses != null)
            {
                for (int i = 0; i < attributeMinuses.Length; i++)
                {
                    if (attributeMinuses[i] == null) continue;

                    int cell = i;
                    attributeMinuses[i].onClick.AddListener(() => Refund(cell));
                }
            }

            if (packSortTabs != null)
            {
                for (int i = 0; i < packSortTabs.Length; i++)
                {
                    if (packSortTabs[i] == null) continue;
                    int key = i;
                    packSortTabs[i].onClick.AddListener(() => SortBy(key));
                }
            }

            // Dragging the thumb, through the same component the Options
            // sliders use -- it reports a 0..1 position on a bar, which is
            // exactly what a scrollbar is.
            if (packScrollTrack != null)
            {
                var bar = packScrollTrack.gameObject.AddComponent<BarSlider>();
                bar.Changed = ScrollToFraction;
            }

            for (int i = 0; i < packCells.Length; i++)
            {
                if (packCells[i] == null) continue;
                int index = i;
                packCells[i].onClick.AddListener(() => EquipFromPack(index));
            }

            for (int i = 0; i < slotCells.Length; i++)
            {
                if (slotCells[i] == null) continue;
                int index = i;
                slotCells[i].onClick.AddListener(() => UnequipSlot(index));
            }
        }

        private static void AttachHovers(Button[] buttons, System.Action<int, bool> changed)
        {
            if (buttons == null) return;

            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;

                int index = i;
                var hover = buttons[i].gameObject.AddComponent<HoverIndex>();
                hover.Index = index;
                hover.Changed = changed;
            }
        }

        public void ShowPack(bool open)
        {
            packPanel.SetShown(open);
            if (packChevron != null) packChevron.SetContent(open ? "<" : ">");
        }

        // Read by SheetPanel so I/C can tell "already showing what was asked
        // for" from "showing the other state" -- the distinction that makes
        // the second press of the OTHER key switch instead of closing.
        public bool IsPackShown => packPanel != null && packPanel.activeSelf;

        private void TogglePack() => ShowPack(packPanel != null && !packPanel.activeSelf);

        // ---- spell books (docs/PLAN_SHOP.md §1g, gate 3) -------------------------

        // Which unassigned-book row is selected, as an index into the SAME
        // snapshot RefreshSpells just painted -- not into run.unassignedSpellBooks
        // directly, because a duplicate skillId can occupy more than one row
        // and "row 2" has to mean row 2, not "some row with this skillId".
        private int _selectedUnassignedRow = -1;
        private List<string> _unassignedSnapshot = new List<string>();

        public void ShowSpells(bool open)
        {
            spellsPanel.SetShown(open);
            if (spellsChevron != null) spellsChevron.SetContent(open ? "<" : ">");
            if (!open) _selectedUnassignedRow = -1;
            RefreshSpells();
        }

        private void ToggleSpells() => ShowSpells(spellsPanel != null && !spellsPanel.activeSelf);

        private void SelectUnassigned(int row)
        {
            if (row < 0 || row >= _unassignedSnapshot.Count) return;

            // Re-pressing the selected row deselects, same rule the relic
            // draft's own card press follows -- a screen whose whole job is
            // comparing things should let a choice be reconsidered.
            _selectedUnassignedRow = _selectedUnassignedRow == row ? -1 : row;
            RefreshSpells();
        }

        // A press on a slot COMMITS the currently selected unassigned book
        // into THAT slot -- an empty slot learns it directly (LearnSpell's
        // explicit-slot overload, not the lowest-free-slot one: the player
        // clicked THIS box, and placing it in a different one because it
        // happened to sort lower would be a screen disagreeing with its own
        // click), a full slot replaces it (ReplaceSpell, which returns the
        // displaced book to the pool rather than destroying it -- §7.1
        // point 5). Nothing selected is a no-op; there is nothing to place.
        private void PressSlot(int slot)
        {
            if (_selectedUnassignedRow < 0 || _selectedUnassignedRow >= _unassignedSnapshot.Count) return;

            var squad = Squad();
            if (_index < 0 || _index >= squad.Count) return;
            var character = squad[_index];
            string skillId = _unassignedSnapshot[_selectedUnassignedRow];

            var run = RunManager.Run;
            bool slotOccupied = run?.learnedSpells != null
                && run.learnedSpells.Exists(e => e != null && e.characterId == character.definitionId && e.slot == slot);

            var result = slotOccupied
                ? RunOrchestrator.ReplaceSpell(character.definitionId, skillId, slot)
                : RunOrchestrator.LearnSpell(character.definitionId, skillId, slot);

            if (result.Applied) _selectedUnassignedRow = -1;

            Refresh();
        }

        private void RefreshSpells()
        {
            var squad = Squad();
            var run = RunManager.Run;

            var character = (_index >= 0 && _index < squad.Count) ? squad[_index] : null;
            var learned = run?.learnedSpells ?? new List<LearnedSpellEntry>();
            var unassigned = run?.unassignedSpellBooks ?? new List<string>();

            if (spellsCount != null && character != null)
            {
                int filled = learned.Count(e => e != null && e.characterId == character.definitionId);
                spellsCount.Set(UiStrings.DossierSpellsCount, filled, SpellBooks.MaxSpellSlots);
            }

            if (spellSlots != null)
            {
                for (int i = 0; i < spellSlots.Length; i++)
                {
                    string skillId = character == null
                        ? null
                        : learned.FirstOrDefault(e => e != null && e.characterId == character.definitionId && e.slot == i)?.skillId;

                    if (spellSlotNames != null && i < spellSlotNames.Length)
                    {
                        if (string.IsNullOrEmpty(skillId))
                        {
                            spellSlotNames[i].Set(UiStrings.DossierSlotEmpty);
                        }
                        else
                        {
                            var definition = ContentDatabase.GetSkill(skillId);
                            spellSlotNames[i].Set(UiStrings.DossierSlotFilled, definition?.Data.DisplayName ?? skillId);
                        }
                    }

                    // A green preview when the currently selected book would
                    // FILL this slot, so a full-vs-empty slot reads the same
                    // way the shop's own would-fill/would-replace preview
                    // does, before the second click commits either.
                    if (spellSlotSelections != null && i < spellSlotSelections.Length && spellSlotSelections[i] != null)
                    {
                        bool previewFill = _selectedUnassignedRow >= 0 && string.IsNullOrEmpty(skillId);
                        spellSlotSelections[i].gameObject.SetActive(previewFill);
                    }
                }
            }

            _unassignedSnapshot = new List<string>(unassigned);

            if (unassignedEmptyHint != null) unassignedEmptyHint.SetActive(_unassignedSnapshot.Count == 0);

            if (unassignedRows != null)
            {
                for (int i = 0; i < unassignedRows.Length; i++)
                {
                    bool present = i < _unassignedSnapshot.Count;
                    unassignedRows[i].gameObject.SetActive(present);
                    if (!present) continue;

                    if (unassignedNames != null && i < unassignedNames.Length)
                    {
                        var definition = ContentDatabase.GetSkill(_unassignedSnapshot[i]);
                        unassignedNames[i].SetContent(definition?.Data.DisplayName ?? _unassignedSnapshot[i]);
                    }

                    if (unassignedSelections != null && i < unassignedSelections.Length && unassignedSelections[i] != null)
                    {
                        unassignedSelections[i].gameObject.SetActive(i == _selectedUnassignedRow);
                    }
                }
            }
        }

        private void Step(int by)
        {
            var squad = Squad();
            if (squad.Count == 0) return;

            _index = (_index + by + squad.Count) % squad.Count;
            Refresh();
        }

        private static List<Character> Squad()
        {
            var save = SaveSlotManager.CurrentSave;
            return save == null ? new List<Character>() : save.ActiveSquad().Where(c => c != null).ToList();
        }

        public void Refresh()
        {
            var squad = Squad();
            if (squad.Count == 0) return;

            if (_index >= squad.Count) _index = 0;
            var character = squad[_index];

            // The paging arrows, hidden when there is nobody to page to.
            //
            // These were always live and Step wrapped a one-element list, so
            // pressing either did nothing at all -- a control that answers a
            // click by not moving is worse than one that is not there. The same
            // reasoning as TalentController's pair, and the same threshold: at
            // two characters they come back on their own.
            bool canPage = squad.Count > 1;

            if (prevCharacterButton != null
                && prevCharacterButton.gameObject.activeSelf != canPage)
            {
                prevCharacterButton.gameObject.SetActive(canPage);
            }
            if (nextCharacterButton != null
                && nextCharacterButton.gameObject.activeSelf != canPage)
            {
                nextCharacterButton.gameObject.SetActive(canPage);
            }

            var stats = ContentDatabase.EffectiveStats(character);
            var scores = ContentDatabase.EffectiveAbilityScores(character);

            RefreshIdentity(character);
            _scores = scores;
            RefreshAttributes(scores, character.definitionId);

            // AFTER RefreshAttributes, not inside RefreshIdentity where it
            // started. _cellOrder is rebuilt in there, and on the first open it
            // is still empty -- so a "+" painted before it would hide on every
            // cell for a character who has points to spend, which is precisely
            // the bug this whole feature exists to end.
            PaintStatSpending(character);
            RefreshStats(character, stats, scores);
            RefreshSlots(character);
            RefreshPack();
            RefreshSpells();
        }

        // The bag, through the SAME BagView the old sheet sorted with -- the
        // ordering rules are not worth a second opinion.
        private IReadOnlyList<BagItem> _bag = new List<BagItem>();

        // Which ordering the player chose, and how far down the list the
        // window sits. Both survive a refresh: equipping something must not
        // throw the pack back to the top or re-sort it under the cursor.
        private BagSortKey _sortKey = BagSortKey.Tier;
        private int _scroll;

        // Exposed so a test can state the scroll rule without reaching into
        // private state -- "the window never runs off the end of the list" is
        // the whole of it.
        public int ScrollOffset => _scroll;

        public BagSortKey SortKey => _sortKey;

        public void SortBy(int keyIndex)
        {
            if (keyIndex < 0 || keyIndex >= BagSort.All.Length) return;

            if (BagSort.All[keyIndex] == _sortKey) return;

            _sortKey = BagSort.All[keyIndex];

            // BACK TO THE TOP, because the item under the cursor is not the
            // same item any more. Keeping the offset across a re-sort leaves
            // the player looking at an arbitrary slice of a list they just
            // reordered.
            _scroll = 0;
            RefreshPack();
        }

        // SCROLLING REBINDS, IT DOES NOT REBUILD.
        //
        // Both of these used to call RefreshPack, which re-reads the save,
        // resolves every entry through ContentDatabase and re-sorts the whole
        // bag -- to move a window by two indices, at mouse-wheel frequency.
        // The bag has not changed; only which slice of it is on screen has.
        public void Scroll(int rows)
        {
            int before = _scroll;
            _scroll = ClampScroll(_scroll + rows * (int)DossierLayout.PackColumns);

            // Nothing to redraw at either end of the list, which is where a
            // wheel spends most of its time.
            if (_scroll != before) BindPackWindow();
        }

        private void ScrollToFraction(float t)
        {
            int rows = MaxScrollRows();

            // The bar reads left-to-right and the list runs top-to-bottom, so
            // the fraction is inverted: the top of the track is offset zero.
            int row = rows <= 0 ? 0 : Mathf.RoundToInt((1f - Mathf.Clamp01(t)) * rows);

            int before = _scroll;
            _scroll = ClampScroll(row * (int)DossierLayout.PackColumns);
            if (_scroll != before) BindPackWindow();
        }

        private int MaxScrollRows()
        {
            int columns = (int)DossierLayout.PackColumns;
            int totalRows = (_bag.Count + columns - 1) / columns;
            int rows = totalRows - DossierLayout.PackVisibleRows;
            return rows < 0 ? 0 : rows;
        }

        private int ClampScroll(int offset)
        {
            int max = MaxScrollRows() * (int)DossierLayout.PackColumns;
            if (offset > max) offset = max;
            return offset < 0 ? 0 : offset;
        }

        // The wheel, while the pack is open and the pointer is over it. Read
        // here rather than through an event so it works over any part of the
        // panel rather than only over the bar.
        private void Update()
        {
            if (packPanel == null || !packPanel.activeSelf) return;

            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) < 0.01f) return;

            Scroll(wheel > 0f ? -1 : 1);
        }

        private void RefreshPack()
        {
            var save = SaveSlotManager.CurrentSave;
            var entries = save?.stockpiledItems ?? new List<InventoryEntry>();

            // ORDERED BY WHAT THE PLAYER CHOSE. BagSort.By defers to
            // BagView.Sorted for the default, so the pack's long-standing order
            // is still exactly one implementation.
            _bag = BagSort.By(entries.Select(ToBagItem).Where(i => i.Count > 0), _sortKey);

            // The window can be left past the end by anything that shortens the
            // bag -- equipping the last item, or a sort that ran on a longer
            // list. Clamped here rather than at each of those call sites.
            _scroll = ClampScroll(_scroll);

            BindPackWindow();

            // CAPACITY IS NO LONGER A THING THIS SCREEN HAS. The grid used to be
            // 24 cells against a save that could hold more, so the footer had to
            // say "24 of 27" and admit that three were unreachable. The list
            // scrolls now, so every item is reachable and the footer is simply
            // how many are carried.
            if (carriedValue != null) carriedValue.SetContent(_bag.Count.ToString());
        }

        // Puts _bag's current window onto the cells. The only thing scrolling
        // has to do, and deliberately free of anything that reads the save.
        private void BindPackWindow()
        {
            for (int i = 0; i < packCells.Length; i++)
            {
                // THE WINDOW, and this one line is the whole of the scrolling.
                // The cells stay put; which item each shows moves.
                int index = _scroll + i;
                var item = index < _bag.Count ? _bag[index] : (BagItem?)null;

                if (packIcons != null && i < packIcons.Length)
                {
                    bool shown = item.HasValue &&
                        ItemIcons.Apply(packIcons[i], icons, item.Value.Id);
                    packIcons[i].gameObject.SetShown(shown);
                }

                // The rarity TICK, not a border or a glow -- an 8px corner mark,
                // which is the handover's rule and the one that survives an icon
                // being busy.
                if (packRarityTicks != null && i < packRarityTicks.Length)
                {
                    packRarityTicks[i].gameObject.SetShown(item.HasValue);
                    if (item.HasValue) packRarityTicks[i].color = TierColour(item.Value.Tier);
                }

                // ITEM-MODIFIER PLAN PHASE E: see RefreshSlots' identical
                // block for why this is its own node rather than a repaint
                // of packRarityTicks above.
                if (packRiftGlows != null && i < packRiftGlows.Length)
                {
                    bool glows = item.HasValue && RiftTierColors.ShouldGlow(item.Value.RiftTier);
                    packRiftGlows[i].gameObject.SetShown(glows);
                    if (glows) packRiftGlows[i].color = RiftTierColors.For(item.Value.RiftTier);
                }

                // THE NAME, which is what two abreast bought. Through
                // RarityColors so a Legendary reads the same here as everywhere
                // else, and carrying its own plus so two stacks of the same item
                // are told apart without hovering either.
                if (packNames != null && i < packNames.Length)
                {
                    packNames[i].gameObject.SetShown(item.HasValue);
                    if (item.HasValue)
                    {
                        // Through RarityColors.Wrap, the one place an item's
                        // name becomes coloured and plus-suffixed, so a
                        // Legendary reads the same here as on every other
                        // screen. A save naming content that no longer exists
                        // falls back to the bare name BagView already carries
                        // rather than drawing nothing.
                        var definition = ContentDatabase.GetItem(item.Value.Id);
                        packNames[i].SetContent(definition != null
                            ? RarityColors.Wrap(definition, item.Value.Plus)
                            : item.Value.Name);
                    }
                }

                // A count only where there is more than one, so a single item
                // does not carry a redundant "1".
                if (packCounts != null && i < packCounts.Length)
                {
                    bool stacked = item.HasValue && item.Value.Count > 1;
                    packCounts[i].gameObject.SetShown(stacked);
                    if (stacked) packCounts[i].SetContent(item.Value.Count.ToString());
                }
            }

            RefreshSortMarkers();
            RefreshScrollThumb();
        }

        private void RefreshSortMarkers()
        {
            if (packSortUnderlines == null) return;

            int live = BagSort.IndexOf(_sortKey);
            for (int i = 0; i < packSortUnderlines.Length; i++)
            {
                packSortUnderlines[i].SetShown(i == live);
            }
        }

        // The thumb's LENGTH says how much of the list is on screen and its
        // POSITION says where. A fixed-length thumb would answer only half of
        // that, and on a pack of a hundred items the half it drops is the one
        // that matters.
        private void RefreshScrollThumb()
        {
            if (packScrollThumb == null) return;

            float height = DossierLayout.PackThumbHeight(_bag.Count);
            packScrollThumb.sizeDelta =
                new Vector2(DossierLayout.PackScrollbarWidth, height);

            int maxRows = MaxScrollRows();
            float travel = DossierLayout.PackTrackHeight - height;

            float t = maxRows <= 0
                ? 0f
                : (_scroll / (float)DossierLayout.PackColumns) / maxRows;

            // Measured from the track's own centre, downwards: offset zero is
            // the top of the track.
            packScrollThumb.anchoredPosition = new Vector2(
                packScrollThumb.anchoredPosition.x,
                DossierLayout.PackTrackCentreY + travel * 0.5f - travel * Mathf.Clamp01(t));

            // Nothing to scroll means nothing to drag. Hidden rather than shown
            // full-length, so the bar's presence itself says there is more.
            packScrollThumb.gameObject.SetShown(maxRows > 0);
            if (packScrollTrack != null) packScrollTrack.gameObject.SetShown(maxRows > 0);
        }

        private static BagItem ToBagItem(InventoryEntry entry)
        {
            var item = ContentDatabase.GetItem(entry.itemId);
            if (item == null)
            {
                // A save naming content that no longer exists is shown as itself
                // rather than dropped, so the player sees what happened instead
                // of watching the bag quietly shrink.
                return new BagItem(entry.itemId, entry.itemId, 0, EquipmentSlot.Weapon1,
                    0, entry.plus, entry.count, "", false);
            }

            return new BagItem(item.id, RarityColors.NameOf(item, entry.plus), (int)item.kind,
                item.equipSlot, item.tier, entry.plus, entry.count, item.iconPath, item.IsEquippable,
                entry.modifierIds, entry.riftTier);
        }

        private static Color TierColour(int tier) => Hex(RarityBands.HexColorForTier(tier));

        private void RefreshIdentity(Character character)
        {
            var definition = ContentDatabase.GetCharacter(character.definitionId);
            string name = definition == null || string.IsNullOrWhiteSpace(definition.Data.DisplayName)
                ? character.definitionId
                : definition.Data.DisplayName;

            if (characterName != null) characterName.SetContent(name);
            if (subLine != null) subLine.SetContent($"Level {character.level}");

            // The face, not a mannequin.
            //
            // A MISS LEAVES THE STAND STANDING. The tree has already put an
            // armour stand in this slot as the placeholder, so blanking or
            // disabling the Image (what ItemIcons.Apply would do, correctly,
            // for an item cell) would turn graceful degradation into a hole in
            // the layout.
            var face = CharacterPortraits.For(character.definitionId);
            if (portrait != null && face != null)
            {
                portrait.sprite = face;
                portrait.enabled = true;
                portrait.preserveAspect = true;

                // AND THE TINT COMES OFF. The tree dims this slot to a faint
                // violet so the armour-stand placeholder recedes, and an Image
                // tint MULTIPLIES -- so that same dimming was landing on the
                // painted portrait, which arrived grey-pink at a third opacity
                // and read as a bad illustration rather than as a tint that was
                // never meant for it.
                portrait.color = Color.white;
            }

            int next = Character.ExpToNextLevel(character.level);
            if (xpFill != null)
            {
                float fraction = next <= 0 ? 0f : Mathf.Clamp01((float)character.exp / next);

                // WIDTH, against a left pivot -- not anchors.
                //
                // This drove anchorMax.x to the fraction and zeroed sizeDelta,
                // which looks equivalent and is not: ANCHORS ARE RELATIVE TO
                // THE PARENT. The parent here is column A, not the 226px track,
                // so a bar meant to cross the track crossed that fraction of
                // the whole column instead -- out past its own "258 left"
                // caption and on into the loadout stage beside it.
                //
                // The Options slider's fill records the identical mistake in
                // its own comment, made and fixed while this one was still
                // live. The tree already authors this node at the track's width
                // with a left pivot, so setting width is all that was ever
                // needed, and DossierXpBarTests holds the fill inside the track
                // by measuring both rather than by trusting the mechanism.
                xpFill.sizeDelta = new Vector2(
                    DossierLayout.XpTrackWidth * fraction, xpFill.sizeDelta.y);
            }

            if (xpRemaining != null) xpRemaining.SetContent(Mathf.Max(0, next - character.exp) + " left");

            PaintTrackLine(character);
            if (skillsCount != null)
            {
                // Talent-granted only, which is what this screen can actually
                // count without a fight session in hand.
                skillsCount.SetContent(ContentDatabase.TalentGrantedSkillsFor(character).Count + " known");
            }
        }

        private void ShowTrack()
        {
            if (trackPanel == null) return;

            // TOLD WHICH CHARACTER BEFORE IT OPENS. ShowFor only stores the
            // id; the panel's own OnEnable->Refresh is what resolves it
            // against the save, so calling this before SetActive is what
            // makes that first Refresh already correct (docs/PLAN_REWARD_
            // TRACKS.md §8) rather than one frame of showing whoever the
            // panel last painted.
            if (trackScreen != null)
            {
                var squad = Squad();
                if (_index >= 0 && _index < squad.Count && squad[_index] != null)
                {
                    trackScreen.ShowFor(squad[_index].definitionId);
                }
            }

            // SetActive rather than a toggle: the row is a door in, and the
            // panel's own CLOSE is the way out. A row that also closed it would
            // be a second control for one state, hidden behind the panel that
            // covers it.
            trackPanel.SetActive(true);
        }

        // Places one of this character's unspent points into the score sitting
        // in cell `index`.
        //
        // THE CELL IS NOT THE SCORE. The six cells are filled highest-first per
        // character, so cell 0 is whatever this character's best ability
        // happens to be. Resolving through _cellOrder at click time is what
        // makes that safe -- binding the score at wire time would spend into
        // whatever was strongest when the screen was built, which is a bug that
        // only appears after stepping to a second character.
        //
        // Character.Invest refuses when there is nothing to spend and returns
        // false, so the guard here is about not repainting rather than about
        // correctness.
        private void Spend(int index)
        {
            if (lockedForFight) return;
            if (!TryResolveCell(index, out var character, out var score)) return;

            if (!character.Invest(score)) return;

            // Written immediately. A placed point is permanent until a respec,
            // so losing one to a crash costs the player a level's reward.
            SaveSlotManager.SaveCurrent();
            Refresh();
        }

        // Takes one point back out of the score sitting in cell `index`. Same
        // cell-not-score resolution as Spend, and the same reason for it.
        //
        // A hidden button is still EventSystem-reachable, so this guards
        // itself rather than trusting that the minus was inactive when
        // clicked -- Spend guards inside the method for the same reason.
        private void Refund(int index)
        {
            if (lockedForFight || InDescent) return;
            if (!TryResolveCell(index, out var character, out var score)) return;

            if (!character.Refund(score)) return;

            SaveSlotManager.SaveCurrent();
            Refresh();
        }

        // THE CELL IS NOT THE SCORE (see Spend's own header). Resolves cell
        // `index` against the squad member on screen and _cellOrder's
        // click-time sort, or refuses -- shared by Spend and Refund so the
        // resolution rule can only drift out of sync with itself once,
        // rather than once per caller.
        private bool TryResolveCell(int index, out Character character, out AbilityScore score)
        {
            character = null;
            score = default;

            var squad = Squad();
            if (squad.Count == 0) return false;
            if (_index < 0 || _index >= squad.Count) return false;

            character = squad[_index];
            if (character == null) return false;
            if (index < 0 || index >= _cellOrder.Count) return false;

            score = _cellOrder[index];
            return true;
        }

        // Shows the "+" on every cell when there is a point to spend, and says
        // how many.
        //
        // HIDDEN rather than disabled when there are none, because six greyed
        // plus signs on a character with nothing to spend is six pieces of
        // furniture. The count label is what makes the whole mechanic
        // discoverable -- a 22px "+" in the corner of a cell is easy to have
        // and never notice, which is most of how levelling came to hand out
        // something no player could see.
        private void PaintStatSpending(Character character)
        {
            int points = character?.unspentStatPoints ?? 0;
            bool canSpend = points > 0 && !lockedForFight;

            if (unspentPoints != null)
            {
                unspentPoints.SetContent(canSpend
                    ? (points == 1 ? "1 POINT TO SPEND" : $"{points} POINTS TO SPEND")
                    : "");
            }

            if (attributePluses == null) return;

            for (int i = 0; i < attributePluses.Length; i++)
            {
                if (attributePluses[i] == null) continue;

                // Only cells that actually hold a score. _cellOrder is rebuilt
                // per character and a content pass could leave it short.
                attributePluses[i].gameObject.SetActive(canSpend && i < _cellOrder.Count);
            }

            // The minus is gated PER CELL, where the plus above is gated
            // GLOBALLY, and that is the whole reason this is not a copy-paste
            // of the loop above. An unspent point can go into any of the six
            // scores, so one condition answers all six pluses. A refund can
            // only come back out of a score that has something invested in
            // it, which is a different condition on every cell -- and it is
            // exactly what makes Character.Refund return true for that cell,
            // so the button and the model can never disagree about it.
            if (attributeMinuses == null) return;

            bool canRefund = !lockedForFight && !InDescent;
            for (int i = 0; i < attributeMinuses.Length; i++)
            {
                if (attributeMinuses[i] == null) continue;

                bool hasInvested = character != null
                    && i < _cellOrder.Count
                    && character.investedAbilityScores[_cellOrder[i]] > 0;
                attributeMinuses[i].gameObject.SetActive(canRefund && hasInvested);
            }
        }

        // What this character's next level is worth, under the bar that says
        // how far away it is.
        //
        // THE TRACK WAS PAYABLE BEFORE IT WAS LEGIBLE. Levels grant Favor, stat
        // points, extra starting relics and a wider item offer, and until this
        // line existed nothing on any screen said so -- a player could pass
        // level 25 and find themselves drafting two relics with no way to learn
        // why, which reads as a bug rather than as a reward.
        //
        // Reads `level` directly rather than the claim watermark, because it is
        // describing the track and not the payout: what level 30 gives is the
        // same sentence whether or not this character has been paid yet.
        private void PaintTrackLine(Character character)
        {
            if (trackNext == null) return;

            var track = RewardTracks.For(character);
            int next = track.NextRewardLevel(character.level);
            if (next <= 0)
            {
                // The end of the track is a real state, not an empty one. A
                // blank line here would read as a missing value.
                trackNext.SetContent("REWARD TRACK COMPLETE");
                return;
            }

            string reward = RewardTrackNames.Of(track.At(next));
            if (string.IsNullOrEmpty(reward))
            {
                // A reward kind with no name is a content-shaped gap rather
                // than an error -- say where it lands and stay quiet about
                // what it is, the same graceful-degradation posture the rest
                // of this screen takes for missing art.
                trackNext.SetContent($"NEXT REWARD AT LEVEL {next}");
                return;
            }

            trackNext.SetContent($"LEVEL {next}: {reward}");
        }

        // Highest first, so the top row IS the build.
        private void RefreshAttributes(AbilityScoreBlock scores, string characterId)
        {
            // SORTED ONCE PER CHARACTER, NOT ONCE PER REFRESH.
            //
            // The order is highest-first so the shape of a build reads off the
            // top row -- but spending a stat point CHANGES that order, and
            // Spend() refreshes. Re-sorting here meant the grid reshuffled
            // under the player's cursor mid-click: raise a score that was tied
            // with the one above it and the two swap cells, so pressing "+"
            // twice in the same place put the two points into two DIFFERENT
            // abilities. Nothing on screen warned them.
            //
            // The cost of freezing is that the row can be a little stale after
            // equipping something that moves a score. That is cosmetic -- every
            // value shown is still correct, only their arrangement is older --
            // and it re-sorts the moment the player steps to another character
            // and back.
            if (_cellOrder.Count == 0 || _cellOrderFor != characterId)
            {
                _cellOrderFor = characterId;
                _cellOrder.Clear();
                _cellOrder.AddRange(AbilityScores.All
                    .OrderByDescending(s => ValueOf(scores, s))
                    .ThenBy(s => (int)s));
            }

            // ONE tint, never two. A tie goes to the first declared, because two
            // highlighted attributes stop being a cue at all.
            AbilityScore dominant = _cellOrder[0];

            for (int i = 0; i < attributeCells.Length && i < _cellOrder.Count; i++)
            {
                var score = _cellOrder[i];
                bool isDominant = score == dominant;

                if (attributeValues[i] != null)
                {
                    attributeValues[i].SetContent(ValueOf(scores, score).ToString());
                    attributeValues[i].color = isDominant ? AccentHi : Neutral;
                }

                if (attributeKeys[i] != null)
                {
                    attributeKeys[i].SetContent(AbilityScores.ShortName(score));
                    attributeKeys[i].color = isDominant ? Accent : Dim;
                }
            }
        }

        private static int ValueOf(AbilityScoreBlock scores, AbilityScore score) => scores[score];

        private void RefreshStats(Character character, StatBlock stats, AbilityScoreBlock scores)
        {
            for (int i = 0; i < SheetStats.Derived.Length && i < statValues.Length; i++)
            {
                var stat = SheetStats.Derived[i];
                if (statValues[i] == null) continue;

                statValues[i].SetContent(DisplayValue(character, stat, stats, scores));
                statHighlights[i].SetShown(false);
            }
        }

        // Two rows do not live on StatBlock -- SheetStats.ValueOf returns 0 for
        // them by design rather than inventing a number, so they are resolved
        // here where the Character is in hand.
        private static string DisplayValue(Character character, SheetStat stat,
                                           StatBlock stats, AbilityScoreBlock scores) =>
            TryCurrentValue(character, stat, stats, scores, out int value) ? SheetStats.DisplayText(stat, value) : "-";

        // What a stat reads as RIGHT NOW, from the model.
        //
        // The equip preview needs the same number the row is showing, and it
        // used to get it by int.TryParse-ing the label's rendered text. That
        // works only for as long as every stat renders as a bare integer: the
        // moment one gains a separator, a unit or a percent sign the parse
        // fails silently, falls back to 0, and the preview prints a confident
        // wrong total. Signature gain ALREADY renders as "-" when a character
        // has no signature resource, so the failure case was live.
        //
        // False means there is no number to show, which is a different thing
        // from zero and the reason this is not just an int.
        private static bool TryCurrentValue(Character character, SheetStat stat,
                                            StatBlock stats, AbilityScoreBlock scores, out int value)
        {
            if (stat == SheetStat.MaxMana)
            {
                value = ContentDatabase.EffectiveMaxMana(character);
                return true;
            }

            if (stat == SheetStat.SignatureGain)
            {
                var signature = ContentDatabase.BuildSignatureResource(character);
                value = signature?.GainPerTurn ?? 0;
                return signature != null;
            }

            value = SheetStats.ValueOf(stat, stats, scores);
            return true;
        }

        // THE mechanic: light the rows this attribute actually feeds, and dim
        // the rest so the answer is unmissable.
        private void LightRowsFor(int cell, bool entered)
        {
            if (statHighlights == null || cell < 0 || cell >= _cellOrder.Count) return;

            var fed = entered
                ? new HashSet<SheetStat>(SheetStats.Feeds(_cellOrder[cell]))
                : new HashSet<SheetStat>();

            for (int i = 0; i < SheetStats.Derived.Length && i < statHighlights.Length; i++)
            {
                bool lit = fed.Contains(SheetStats.Derived[i]);
                statHighlights[i].SetShown(lit);

                if (statValues[i] != null)
                {
                    statValues[i].color = !entered || lit ? Neutral : Dim;
                }
            }
        }

        private void RefreshSlots(Character character)
        {
            var loadout = ContentDatabase.ActiveLoadout(character);

            for (int i = 0; i < EquipmentSlots.All.Length && i < slotCells.Length; i++)
            {
                var slot = EquipmentSlots.All[i];

                if (slotLabels[i] != null)
                {
                    slotLabels[i].SetContent(EquipmentSlots.DisplayName(slot).ToUpperInvariant());
                }

                string itemId = character.equipment == null ? null : character.equipment.Get(slot);
                var item = string.IsNullOrEmpty(itemId) ? null : ContentDatabase.GetItem(itemId);

                // ACTIVATE the icon object, not just its Image. The node is
                // declared Inactive so an empty slot draws nothing, and
                // ItemIcons.Apply only sets `enabled` -- on an inactive
                // GameObject that is invisible either way, which is why the
                // slots came up bare with items equipped.
                bool hasArt = ItemIcons.Apply(slotIcons[i], icons, item?.id);
                slotIcons[i].gameObject.SetShown(hasArt);

                if (slotRarityTicks != null && i < slotRarityTicks.Length)
                {
                    slotRarityTicks[i].gameObject.SetShown(item != null);
                    if (item != null) slotRarityTicks[i].color = TierColour(item.tier);
                }

                // ITEM-MODIFIER PLAN PHASE E: the RiftTier ring, a SEPARATE
                // node from the rarity tick above so the two axes never
                // fight for one colour. Hidden whenever the roll is Ordinary
                // -- see RiftTierColors.ShouldGlow's own header -- which is
                // what keeps every item that has never touched this system
                // drawing exactly as it did before it existed.
                if (slotRiftGlows != null && i < slotRiftGlows.Length)
                {
                    var riftTier = (RiftTier)(character.equipment?.GetRiftTier(slot) ?? 0);
                    bool glows = item != null && RiftTierColors.ShouldGlow(riftTier);
                    slotRiftGlows[i].gameObject.SetShown(glows);
                    if (glows) slotRiftGlows[i].color = RiftTierColors.For(riftTier);
                }

                // A slot a two-hander has taken must never read as merely empty
                // -- the player has to see WHY it cannot be used.
                // INERT, not empty. A slot a two-hander has taken carries an
                // entry that is not live, which is exactly the case the player
                // must be shown a reason for rather than a blank square.
                bool blocked = item == null
                    && loadout.InertEntries != null
                    && loadout.InertEntries.Any(e => e.Entry.slot == slot);
                slotBlockedCaptions[i].SetShown(blocked);
            }
        }

        // ---- putting gear on and taking it off -----------------------------------
        //
        // Every RULE about what can be worn lives in EquipMove; this only picks
        // which item and then writes the save. Re-implementing the rules here is
        // how a screen ends up disagreeing with the one that already had them.

        private void EquipFromPack(int index)
        {
            var squad = Squad();
            if (squad.Count == 0 || index >= _bag.Count) return;

            var character = squad[_index];
            var save = SaveSlotManager.CurrentSave;
            if (character?.equipment == null || save == null) return;

            var item = _bag[index];

            // modifierIds/riftTier travel through the same way plus does --
            // InventoryOps.TryRemoveAt inside TryEquip keys on the full
            // (itemId, plus, modifierIds, riftTier) stack (see
            // RunOrchestrator.AutoEquipIntoAnEmptySlot's identical note),
            // so omitting them here would look for the wrong stack and
            // silently strip a rolled item's affixes the moment it is worn
            // from the pack.
            //
            // The "measure max health before the swap, rescale carried health
            // after it" pair that used to sit around this call is inside
            // EquipmentOps.Equip now -- three screens and the bot each had
            // their own copy of it. Nothing about the move changed.
            if (!EquipmentOps.Equip(save, character, item.Id, item.Slot, item.IsEquippable,
                                    plus: item.Plus,
                                    modifierIds: item.ModifierIds?.ToList(),
                                    riftTier: (int)item.RiftTier))
            {
                return;
            }

            // WRITTEN IMMEDIATELY. Gear that vanishes because the game closed
            // between an equip and a save is the least forgivable thing this
            // screen could do -- the old sheet said so and it still holds.
            SaveSlotManager.SaveCurrent();
            Refresh();

            // The pointer has not moved, but what is under it HAS: the item just
            // equipped left the pack and everything after it shifted up. Leaving
            // the tooltip alone left it naming an item that was no longer there,
            // beside a preview of a swap that had already happened. Re-asking
            // the hover handler for this cell is the whole fix -- it either
            // describes the new occupant or clears itself if the pack ran out.
            OnPackHover(index, entered: true);
        }

        private void UnequipSlot(int index)
        {
            var squad = Squad();
            if (squad.Count == 0 || index >= EquipmentSlots.All.Length) return;

            var character = squad[_index];
            var save = SaveSlotManager.CurrentSave;
            if (character?.equipment == null || save == null) return;

            // Scales carried health both ways -- see EquipmentOps.Unequip,
            // which carries the symmetry note this call site used to.
            if (!EquipmentOps.Unequip(save, character, EquipmentSlots.All[index]))
            {
                return;
            }

            SaveSlotManager.SaveCurrent();
            ClearPreview();
            HideTooltip();
            Refresh();
        }

        // ---- the equip preview ------------------------------------------------------
        //
        // Hovering a pack item writes the value each stat WOULD take beside it.
        // Computed by the same hypothetical-equip pass the Reckoning uses --
        // ItemDescription.Compare clones the character, equips the candidate and
        // re-resolves -- so the two screens cannot disagree about an item.

        private void ShowPreviewFor(BagItem entry)
        {
            var squad = Squad();
            var item = ContentDatabase.GetItem(entry.Id);
            if (squad.Count == 0 || item == null) { ClearPreview(); return; }

            var character = squad[_index];
            var comparison = ItemDescription.Compare(character, item, entry.Plus);
            _previewScores = _scores + comparison.ScoreDelta;

            var stats = ContentDatabase.EffectiveStats(character);

            // Bounded by BOTH arrays, not just one. They are built together and
            // are the same length, but half a guard is the shape of an
            // IndexOutOfRange that only shows up once somebody adds a stat.
            int count = System.Math.Min(SheetStats.Derived.Length,
                System.Math.Min(statPreviews.Length, statValues.Length));

            for (int i = 0; i < count; i++)
            {
                var stat = SheetStats.Derived[i];
                int delta = DeltaFor(stat, comparison);

                // ONLY the stats that actually move, and only those that have a
                // number to move. A column of arrows against unchanged numbers
                // buries the two rows that did change.
                if (delta == 0 || !TryCurrentValue(character, stat, stats, _scores, out int now))
                {
                    if (statPreviews[i] != null) statPreviews[i].SetContent("");
                    if (statValues[i] != null) statValues[i].color = Neutral;
                    continue;
                }

                if (statPreviews[i] != null)
                {
                    statPreviews[i].SetContent("> " + (now + delta));
                    statPreviews[i].color = delta > 0 ? Good : Bad;
                }

                // The current figure steps back so the new one leads.
                if (statValues[i] != null) statValues[i].color = Dim;
            }
        }

        private void ClearPreview()
        {
            if (statPreviews == null) return;

            for (int i = 0; i < statPreviews.Length; i++)
            {
                if (statPreviews[i] != null) statPreviews[i].SetContent("");
                if (statValues != null && i < statValues.Length && statValues[i] != null)
                {
                    statValues[i].color = Neutral;
                }
            }
        }

        private int DeltaFor(SheetStat stat, ItemComparison comparison)
        {
            switch (stat)
            {
                case SheetStat.MaxHealth: return comparison.StatDelta.maxHealth;
                case SheetStat.Attack: return comparison.StatDelta.attack;
                case SheetStat.Speed: return comparison.StatDelta.speed;
                case SheetStat.ManaRegen: return comparison.StatDelta.manaRegen;
                case SheetStat.PhysicalDefense: return comparison.StatDelta.physicalDefense;
                case SheetStat.MagicalDefense: return comparison.StatDelta.magicalDefense;

                // These two are derived from WISDOM and CHARISMA, so an item
                // that shifts an ability score shifts them as well -- which a
                // StatDelta alone would miss entirely.
                case SheetStat.MaxMana:
                    return AbilityDerivation.MaxManaBonus(_previewScores)
                         - AbilityDerivation.MaxManaBonus(_scores);
                case SheetStat.SignatureGain:
                    return AbilityDerivation.SignatureGainBonus(_previewScores)
                         - AbilityDerivation.SignatureGainBonus(_scores);
                default: return 0;
            }
        }

        // ---- the shared tooltip -------------------------------------------------
        //
        // ONE instance for the whole screen, as the handover specifies. The
        // DERIVATION lives here rather than on the row: printing "396 from CON"
        // beside every stat would double the width of column C to say something
        // the player only wants once.

        private void OnAttributeHover(int cell, bool entered)
        {
            LightRowsFor(cell, entered);

            if (!entered || cell >= _cellOrder.Count) { HideTooltip(); return; }

            var score = _cellOrder[cell];
            ShowTooltip(AbilityScores.ShortName(score), SheetStats.PerPointSummary(_scores, score),
                       RectOf(attributeCells, cell));
        }

        private void OnSlotHover(int index, bool entered)
        {
            if (!entered || index >= EquipmentSlots.All.Length) { HideTooltip(); return; }

            var squad = Squad();
            if (squad.Count == 0) { HideTooltip(); return; }

            var slot = EquipmentSlots.All[index];
            var character = squad[_index];
            string itemId = character.equipment?.Get(slot);
            var item = string.IsNullOrEmpty(itemId) ? null : ContentDatabase.GetItem(itemId);

            if (item == null)
            {
                ShowTooltip(EquipmentSlots.DisplayName(slot), "Nothing equipped.", RectOf(slotCells, index));
                return;
            }

            // PLUS, NOT JUST THE ITEM: found while wiring the DMG line
            // (D7.2) -- this used to call CardSummary(item) with no plus at
            // all, so an equipped slot's OWN hover silently showed the
            // unhoned figure for every stat, weapon included. Pre-existing
            // and out of D7's scope to have gone looking for, but the DMG
            // line this phase adds would have made it worse (a honed sword's
            // card understating its own damage), not merely stayed wrong.
            int plus = character.equipment?.GetPlus(slot) ?? 0;

            // ITEM-MODIFIER PLAN PHASE E: read BEFORE anything displaces this
            // entry, same rule EquipmentLoadout.GetModifierIds/GetRiftTier's
            // own header states for GetPlus.
            var modifierIds = character.equipment?.GetModifierIds(slot);
            var riftTier = (RiftTier)(character.equipment?.GetRiftTier(slot) ?? 0);

            ShowTooltip(RarityColors.NameOf(item),
                ItemDescription.CardSummary(item, plus, character, riftTier, modifierIds),
                RectOf(slotCells, index));
        }

        private void OnPackHover(int index, bool entered)
        {
            if (!entered || index >= _bag.Count) { HideTooltip(); ClearPreview(); return; }

            var entry = _bag[index];
            var item = ContentDatabase.GetItem(entry.Id);
            var squad = Squad();

            // COMPARISON, not the bare card. "ATK +3" on its own is a number
            // with no context -- the player cannot tell whether that is
            // meaningful or trivial without knowing what they already have.
            // ComparisonBody is the same VS.-EQUIPPED breakdown the stat sheet
            // preview already computes; this just also puts it in words in the
            // tooltip itself, since the live preview only covers the derived
            // stat ROWS and not everything a body of text can say (cascade
            // notes, requirement lines).
            string body = item == null ? ""
                : squad.Count > 0 ? ItemDescription.ComparisonBody(squad[_index], item, entry.Plus, entry.RiftTier, entry.ModifierIds)
                : ItemDescription.CardSummary(item, entry.Plus, riftTier: entry.RiftTier, modifierIds: entry.ModifierIds);

            ShowTooltip(entry.Name, body, RectOf(packCells, index));
            ShowPreviewFor(entry);
        }

        private void ShowTooltip(string title, string body, RectTransform near)
        {
            if (tooltip == null) return;

            if (tooltipTitle != null) tooltipTitle.SetContent(title ?? "");
            if (tooltipBody != null) tooltipBody.SetContent(body ?? "");
            PlaceTooltip(near);
            tooltip.SetShown(true);
        }

        // The tooltip follows what it describes.
        //
        // It was authored at a FIXED position -- the dossier's own centre --
        // which put it squarely over the mannequin for every hover. That is the
        // worst possible place for it: the whole point of hovering a pack item
        // is to weigh it against what is currently worn, and the box answering
        // the question was covering the evidence. It also hid the Weapon 1 slot
        // exactly when an equip landed there.
        //
        // Beside the cell if there is room on the right, flipped to the left if
        // there is not, and clamped so it never leaves the panel.
        //
        // The arithmetic moved to TooltipPlacement when the Reckoning needed
        // the same answer for its offer cards. What is left here is the part
        // that is genuinely this screen's: which rect is being hovered, and
        // what space it has to be expressed in.
        private void PlaceTooltip(RectTransform near)
        {
            var self = tooltip == null ? null : tooltip.transform as RectTransform;
            var parent = self == null ? null : self.parent as RectTransform;
            if (self == null || parent == null || near == null) return;

            // Through world space, because the pack cells live inside the pack
            // panel and the tooltip does not; their local coordinates are not
            // the same space and treating them as one is how the pack children
            // ended up 339px off the panel they belong to.
            Vector2 local = parent.InverseTransformPoint(near.TransformPoint(Vector3.zero));

            const float Margin = 8f;
            var at = TooltipPlacement.Beside(
                local.x, local.y, near.rect.width,
                self.sizeDelta.x, self.sizeDelta.y,
                interiorLeft: -DossierLayout.HalfWidth + Margin,
                interiorRight: DossierLayout.HalfWidth - Margin,
                interiorBottom: -DossierLayout.HalfHeight + Margin,
                interiorTop: DossierLayout.HalfHeight - Margin);

            self.anchoredPosition = new Vector2(at.X, at.Y);
        }

        private static RectTransform RectOf(Button[] cells, int index)
        {
            if (cells == null || index < 0 || index >= cells.Length || cells[index] == null) return null;
            return cells[index].transform as RectTransform;
        }

        private void HideTooltip() => tooltip.SetShown(false);

        private static Color Hex(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out var color) ? color : Color.white;

            }
}

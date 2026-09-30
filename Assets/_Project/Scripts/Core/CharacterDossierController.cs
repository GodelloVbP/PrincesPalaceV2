using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // The CHARACTER tab (plan C15-C21): the portrait strip, the rail and the
    // section in the detail area. This file is the shell -- who is shown, which
    // section, the pad's map and the menu's seams; the LOADOUT, ATTRIBUTES and
    // SKILLS sections live in the other partial files, and the REWARD TRACK is
    // its own controller (RewardTrackController) this one shows and points.
    //
    // The numbers come from the SAME ContentDatabase.Effective* calls the fight
    // uses, never from a second computation -- a sheet that recomputed its own
    // figures would eventually disagree with the battle.
    //
    // WHAT A PRESS MAY CHANGE is MenuPermissions' answer for the menu's
    // context (plan C3): every handler that changes something asks it first,
    // and a refusal goes to the menu's notice line. A fight's copy reads
    // everything and changes nothing.
    public partial class CharacterDossierController : MonoBehaviour,
        INavPaneEntry, INavCancelClaim, INavSectionStrip, IMenuPromptSource, IMenuPending
    {
        // ---- the strip -----------------------------------------------------------
        [SerializeField] internal Button stripPrev;
        [SerializeField] internal Button stripNext;
        [SerializeField] internal RectTransform stripPrevPlate;
        [SerializeField] internal RectTransform stripNextPlate;
        [SerializeField] internal TMP_Text stripPrevGlyph;
        [SerializeField] internal TMP_Text stripNextGlyph;
        [SerializeField] internal GameObject stripDivider;
        [SerializeField] internal Button[] chips;
        [SerializeField] internal Image[] chipPortraits;
        [SerializeField] internal TMP_Text[] chipNames;
        [SerializeField] internal TMP_Text[] chipSeats;
        [SerializeField] internal GameObject[] chipPips;
        [SerializeField] internal GameObject[] chipSelectedRims;

        // ---- the rail --------------------------------------------------------------
        [SerializeField] internal Image portrait;
        [SerializeField] internal TMP_Text characterName;
        [SerializeField] internal TMP_Text levelLine;
        [SerializeField] internal RectTransform xpFill;
        [SerializeField] internal TMP_Text xpRemaining;
        [SerializeField] internal Button[] sectionRows;
        [SerializeField] internal TMP_Text[] sectionCaptions;
        [SerializeField] internal Image[] sectionBars;
        [SerializeField] internal GameObject[] sectionPips;

        // ---- the sections, in CharacterSections.All order ------------------------
        [SerializeField] internal GameObject loadoutSection;
        [SerializeField] internal GameObject attributesSection;
        [SerializeField] internal GameObject skillsSection;
        [SerializeField] internal GameObject rewardTrackSection;

        // The REWARD TRACK section's own controller: told which character to
        // show before it is shown. Assigned by ScreenRegistry.WireSystemMenu,
        // which has both controllers in hand.
        [SerializeField] internal RewardTrackController trackScreen;

        // The menu this pane lives in, whose context every change asks
        // MenuPermissions about. Set by ScreenRegistry.WireSystemMenu.
        [SerializeField] internal SystemMenuController menu;

        // Item and book art, bound at build time from every item and skill
        // with an icon.
        [SerializeField] internal IconEntry[] icons;

        // A pane with no menu reads as camp; the build refuses a null
        // reference (UiWiringSweep), so that is a fixture's case only.
        public MenuContext Context => menu != null ? menu.Context : MenuContext.Camp;

        private static readonly Color TextBright = Hex(Domain.UiKit.Screens.CharacterDossierScreen.Text);
        private static readonly Color TextDim = Hex(Domain.UiKit.Screens.CharacterDossierScreen.TextSecondary);
        private static readonly Color TextMuted = Hex(Domain.UiKit.Screens.CharacterDossierScreen.TextMuted);
        private static readonly Color GoldBar = Hex(Domain.UiKit.Screens.CharacterDossierScreen.Gold);
        private static readonly Color Clear = new Color(0f, 0f, 0f, 0f);
        private static readonly Color Good = Hex("#8FB37A");
        private static readonly Color Bad = Hex("#D99A8C");

        // A benched chip stands at 60% (spec section 3).
        private const float BenchedAlpha = 0.6f;

        private bool _wired;

        // Who the strip shows, in its order, and which of them is on screen.
        private IReadOnlyList<StripChip> _chips = new List<StripChip>();
        private int _index;

        private CharacterSection _section = CharacterSection.Loadout;
        private bool _lastDeviceWasPad;

        public CharacterSection Section => _section;

        // The character on screen, by definition id, or null with no save.
        public string CharacterId => _index >= 0 && _index < _chips.Count ? _chips[_index].Id : null;

        private void OnEnable()
        {
            Wire();

            // The pack's view is per visit: a filter or scroll left from the
            // last opening would hide gear that has landed since.
            _filter = PackFilter.All;
            _scrollRow = 0;
            Refresh();

            // The section the player last used here (plan C7).
            ShowSection(MenuMemory.SectionFor(Context), focusRow: false);

            _compare.Attach(NavigationInputModule.Contexts);
        }

        private void OnDisable() => _compare.Detach();

        private void Update()
        {
            bool pad = NavigationInputModule.LastInputWasPad;
            if (pad != _lastDeviceWasPad)
            {
                _lastDeviceWasPad = pad;
                PaintPagerGlyphs();
            }
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            if (stripPrev != null) stripPrev.onClick.AddListener(() => StepSection(-1));
            if (stripNext != null) stripNext.onClick.AddListener(() => StepSection(1));
            SetNoNavigation(stripPrev);
            SetNoNavigation(stripNext);

            // The strip is not in the D-pad graph (spec section 9), so the
            // character never changes by a stray Move; the mouse clicks a chip,
            // the pad uses its shoulders.
            for (int i = 0; chips != null && i < chips.Length; i++)
            {
                int slot = i;
                chips[i].onClick.AddListener(() => PressChip(slot));
                SetNoNavigation(chips[i]);
            }

            // SELECTION FOLLOWS FOCUS on the rail: the row the pad stands on is
            // the section shown, so Down once and Right is ATTRIBUTES' entry
            // (spec section 10, task 6). A click does the same.
            for (int i = 0; sectionRows != null && i < sectionRows.Length; i++)
            {
                int index = i;
                sectionRows[i].onClick.AddListener(() => ShowSection(CharacterSections.All[index], focusRow: true));

                var watch = sectionRows[i].gameObject.GetComponent<SelectIndex>()
                            ?? sectionRows[i].gameObject.AddComponent<SelectIndex>();
                watch.Index = index;
                watch.Changed = (row, selected) =>
                {
                    if (selected) ShowSection(CharacterSections.All[row], focusRow: false);
                };
            }

            if (trackScreen != null && sectionRows != null)
            {
                trackScreen.ExitTo = SectionRow(CharacterSection.RewardTrack);
            }

            // A claim is the one change the track makes that this pane's own
            // handlers do not see.
            if (trackScreen != null) trackScreen.Claimed += PaintAttention;

            WireLoadout();
            WireAttributes();
            WireSkills();
            PaintPagerGlyphs();
        }

        // ---- who is shown -----------------------------------------------------------

        // Every character, seated party first in stage order, then the bench
        // (CharacterStrip). Rebuilt on every refresh: Party can reseat or bench
        // someone while this pane is hidden.
        private void ResolveChips()
        {
            var save = SaveSlotManager.CurrentSave;
            if (save == null)
            {
                _chips = new List<StripChip>();
                return;
            }

            string current = CharacterId;
            var roster = (save.roster ?? new List<Character>()).Where(c => c != null).Select(c => c.definitionId).ToList();
            _chips = CharacterStrip.Order(save.selectedCharacterIds, roster);

            int kept = current == null ? -1 : IndexOfChip(current);
            _index = kept >= 0 ? kept : Mathf.Clamp(_index, 0, Mathf.Max(0, _chips.Count - 1));
        }

        private int IndexOfChip(string id)
        {
            for (int i = 0; i < _chips.Count; i++)
            {
                if (_chips[i].Id == id) return i;
            }

            return -1;
        }

        private Character Current()
        {
            string id = CharacterId;
            var save = SaveSlotManager.CurrentSave;
            return id == null || save?.roster == null ? null : save.roster.FirstOrDefault(c => c != null && c.definitionId == id);
        }

        // Selects a character by definition id, for a deep link
        // (SystemMenuController.Open with a MenuTarget). False, and nothing
        // moved, when nobody on the strip has that id.
        public bool SelectCharacter(string definitionId)
        {
            Wire();
            ResolveChips();

            int index = IndexOfChip(definitionId);
            if (index < 0) return false;

            MoveTo(index);
            return true;
        }

        // LB/RB (INavSectionStrip): the previous or next chip, wrapping over
        // the whole strip, bench included. A pending state is dropped first
        // (plan C6); focus stays where it was when that control is still
        // there for the new character, else it goes to the rail.
        public void StepSection(int direction)
        {
            Wire();
            ResolveChips();
            if (_chips.Count == 0) return;

            MoveTo(CharacterStrip.Step(_index, _chips.Count, direction));
        }

        private void PressChip(int slot)
        {
            int start = CharacterStrip.WindowStart(_index, _chips.Count, DossierLayout.ChipCapacity);
            int index = start + slot;
            if (index < 0 || index >= _chips.Count) return;

            MoveTo(index);
        }

        private void MoveTo(int index)
        {
            CancelPending();

            _index = index;
            Refresh();

            if (_section == CharacterSection.RewardTrack && trackScreen != null && CharacterId != null)
            {
                trackScreen.ShowFor(CharacterId);
                if (trackScreen.isActiveAndEnabled) trackScreen.Refresh();
            }

            // A control that vanished with the old character (a book slot for
            // someone who cannot carry books) hands focus to the rail.
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && !selected.activeInHierarchy && selected.transform.IsChildOf(transform))
            {
                EventSystem.current.SetSelectedGameObject(SectionRow(_section)?.gameObject);
            }
        }

        public void Refresh()
        {
            Wire();
            ResolveChips();

            var character = Current();
            if (character == null) return;

            ResolveCharacter(character);
            PaintStrip();
            PaintIdentity(character);
            PaintRail();
            PaintAttention();
            RefreshLoadout(character);
            RefreshAttributes(character);
            RefreshSkills(character);
            RefreshNavigation();
        }

        // ---- the strip ----------------------------------------------------------------

        private void PaintStrip()
        {
            int count = _chips.Count;
            int start = CharacterStrip.WindowStart(_index, count, DossierLayout.ChipCapacity);
            int shown = Mathf.Min(DossierLayout.ChipCapacity, count - start);
            int divider = CharacterStrip.DividerIndex(_chips, start, shown);

            for (int i = 0; chips != null && i < chips.Length; i++)
            {
                bool present = i < shown;
                chips[i].gameObject.SetShown(present);
                if (!present) continue;

                var chip = _chips[start + i];
                var rect = chips[i].transform as RectTransform;
                if (rect != null) rect.anchoredPosition = new Vector2(DossierLayout.ChipCentreX(i, divider), rect.anchoredPosition.y);

                float alpha = chip.Benched ? BenchedAlpha : 1f;
                if (Has(chipNames, i))
                {
                    chipNames[i].SetContent(DisplayNameOf(chip.Id));
                    chipNames[i].color = WithAlpha(TextBright, alpha);
                }

                if (Has(chipSeats, i))
                {
                    chipSeats[i].SetContent(SeatWord(chip));
                    chipSeats[i].color = WithAlpha(TextMuted, alpha);
                }

                if (Has(chipPortraits, i)) PaintPortrait(chipPortraits[i], chip.Id, alpha);
                if (Has(chipSelectedRims, i)) chipSelectedRims[i].SetShown(start + i == _index);
            }

            if (stripDivider != null)
            {
                stripDivider.SetShown(divider >= 0);
                var rect = stripDivider.transform as RectTransform;
                if (divider >= 0 && rect != null)
                {
                    rect.anchoredPosition = new Vector2(DossierLayout.DividerCentreX(divider), rect.anchoredPosition.y);
                }
            }

            // Nobody to page to: the plates go, like a pager with one page.
            bool pages = count > 1;
            if (stripPrev != null) stripPrev.gameObject.SetShown(pages);
            if (stripNext != null) stripNext.gameObject.SetShown(pages);
        }

        private void PaintPagerGlyphs()
        {
            var device = PromptGlyphs.DeviceFor(NavigationInputModule.LastInputWasPad);
            PaintPagerGlyph(stripPrevPlate, stripPrevGlyph, PromptGlyphs.For(device, PromptButton.PageBack));
            PaintPagerGlyph(stripNextPlate, stripNextGlyph, PromptGlyphs.For(device, PromptButton.PageForward));
        }

        // The footer's own plate: the letters, their tint and a width refitted
        // to them, from the same table and the same arithmetic.
        private static void PaintPagerGlyph(RectTransform plate, TMP_Text letters, PromptGlyph glyph)
        {
            if (letters == null) return;

            letters.SetContent(glyph.Text);
            if (ColorUtility.TryParseHtmlString(glyph.TintHex, out var tint)) letters.color = tint;

            if (plate != null)
            {
                float width = MenuFooterLayout.PlateWidthFor(letters.GetPreferredValues(glyph.Text).x);
                plate.sizeDelta = new Vector2(width, MenuFooterLayout.GlyphHeight);
            }
        }

        private static string SeatWord(StripChip chip)
        {
            if (chip.Benched) return UiStrings.PartyCardTagBenched.Format();

            switch (chip.Seat)
            {
                case Domain.Party.PartySeat.Front: return UiStrings.FightSeatFront.Format();
                case Domain.Party.PartySeat.Middle: return UiStrings.FightSeatMiddle.Format();
                case Domain.Party.PartySeat.Rear: return UiStrings.FightSeatRear.Format();
                default: return "";
            }
        }

        // The face, not the placeholder. A miss leaves the armour stand the
        // tree put there, at its own dim tint, rather than a hole.
        private static void PaintPortrait(Image image, string id, float alpha)
        {
            var face = CharacterPortraits.For(id);
            if (image == null || face == null) return;

            image.sprite = face;
            image.enabled = true;
            image.preserveAspect = true;
            image.color = new Color(1f, 1f, 1f, alpha);
        }

        // WHAT TO CALL A CHARACTER ON SCREEN, falling back to the id so a
        // definition that has gone missing still names something.
        private static string DisplayNameOf(string id)
        {
            var definition = ContentDatabase.GetCharacter(id);
            return definition == null || string.IsNullOrWhiteSpace(definition.Data.DisplayName)
                ? id ?? ""
                : definition.Data.DisplayName;
        }

        // ---- the rail: identity -------------------------------------------------------

        private void PaintIdentity(Character character)
        {
            if (characterName != null) characterName.SetContent(DisplayNameOf(character.definitionId));
            if (levelLine != null)
            {
                // In a fight the line carries the health the HUD is showing
                // (plan C4), never the run's carried figure: that one is
                // written when the fight ends.
                if (_fight != null) levelLine.Set(UiStrings.CharacterLevelHealth, character.level, _fight.Health, _fight.MaxHealth);
                else levelLine.Set(UiStrings.CharacterLevel, character.level);
            }
            if (portrait != null) PaintPortrait(portrait, character.definitionId, 1f);

            int next = Character.ExpToNextLevel(character.level);
            if (xpFill != null)
            {
                float fraction = next <= 0 ? 1f : Mathf.Clamp01((float)character.exp / next);

                // WIDTH, against the left pivot the tree authored -- never
                // anchors, which are relative to the rail rather than the track.
                xpFill.sizeDelta = new Vector2(DossierLayout.XpTrackWidth * fraction, xpFill.sizeDelta.y);
            }

            if (xpRemaining != null)
            {
                if (next <= 0) xpRemaining.Set(UiStrings.CharacterXpMax);
                else xpRemaining.Set(UiStrings.CharacterXpToNext, Mathf.Max(0, next - character.exp));
            }
        }

        // ---- the rail: sections ---------------------------------------------------------

        // Shows `section` in the detail area. A pending state is dropped first
        // (plan C6: a rail section change cancels). `focusRow` moves focus to
        // the section's rail row, for a click or a deep link; a Move that
        // landed on the row already has it there.
        public void ShowSection(CharacterSection section, bool focusRow)
        {
            Wire();
            bool entering = section != _section;
            if (entering) CancelPending();

            _section = section;
            MenuMemory.RememberSection(Context, section);

            // The track is told whose it is BEFORE it is shown, so its first
            // paint on enable is already that character's.
            if (section == CharacterSection.RewardTrack && trackScreen != null && CharacterId != null)
            {
                trackScreen.ShowFor(CharacterId);
            }

            SetSectionShown(loadoutSection, section == CharacterSection.Loadout);
            SetSectionShown(attributesSection, section == CharacterSection.Attributes);
            SetSectionShown(skillsSection, section == CharacterSection.Skills);
            SetSectionShown(rewardTrackSection, section == CharacterSection.RewardTrack);

            // The list opens at its head each visit, like the pack.
            if (entering && section == CharacterSection.Skills) ResetSkillsWindow();

            if (section == CharacterSection.RewardTrack && trackScreen != null && trackScreen.isActiveAndEnabled)
            {
                trackScreen.Refresh();
            }

            PaintRail();
            RefreshNavigation();

            // The tab's Down link points at this pane's entry, the section row.
            if (menu != null) menu.RefreshNavLinks();

            if (focusRow && EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(SectionRow(section)?.gameObject);
            }
        }

        private static void SetSectionShown(GameObject section, bool shown)
        {
            if (section != null && section.activeSelf != shown) section.SetActive(shown);
        }

        private void PaintRail()
        {
            for (int i = 0; sectionRows != null && i < sectionRows.Length; i++)
            {
                bool selected = i < CharacterSections.All.Length && CharacterSections.All[i] == _section;
                if (Has(sectionCaptions, i)) sectionCaptions[i].color = selected ? TextBright : TextDim;
                if (Has(sectionBars, i)) sectionBars[i].color = selected ? GoldBar : Clear;
            }
        }

        // THE PIPS (plan C20): a chip's when its character wants looking at at
        // all, a rail row's when that section is what they are owed. Both from
        // CharacterAttention over the same feed the map's hint reads, so the
        // three cannot disagree. Repainted by every Refresh -- which every
        // spend, placement and removal ends in -- and by a track claim.
        private void PaintAttention()
        {
            var save = SaveSlotManager.CurrentSave;
            var run = RunManager.Run;

            int start = CharacterStrip.WindowStart(_index, _chips.Count, DossierLayout.ChipCapacity);
            for (int i = 0; chipPips != null && i < chipPips.Length; i++)
            {
                if (chipPips[i] == null) continue;

                int at = start + i;
                chipPips[i].SetShown(at < _chips.Count
                    && CharacterAttention.Any(CharacterAttentionFeed.For(RosterEntry(save, _chips[at].Id), run)));
            }

            var facts = CharacterAttentionFeed.For(Current(), run);
            for (int i = 0; sectionPips != null && i < sectionPips.Length && i < CharacterSections.All.Length; i++)
            {
                if (sectionPips[i] != null) sectionPips[i].SetShown(CharacterAttention.Lights(CharacterSections.All[i], facts));
            }
        }

        private static Character RosterEntry(SaveData save, string id) =>
            save?.roster?.FirstOrDefault(c => c != null && c.definitionId == id);

        private Button SectionRow(CharacterSection section)
        {
            int index = CharacterSections.IndexOf(section);
            return sectionRows != null && index >= 0 && index < sectionRows.Length ? sectionRows[index] : null;
        }

        // ---- navigation (spec section 9) -----------------------------------------------
        //
        // THE PANE'S ENTRY (INavPaneEntry) is the rail's selected section row:
        // where the tab's Down lands and where opening or LT/RT land.
        public Selectable NavEntry => SectionRow(_section);

        // Where Right from the rail goes: the selected section's own entry.
        private Selectable DetailEntry()
        {
            switch (_section)
            {
                case CharacterSection.Loadout: return LoadoutEntry();
                case CharacterSection.Attributes: return AttributesEntry();
                case CharacterSection.Skills: return SkillsEntryControl();
                case CharacterSection.RewardTrack: return trackScreen != null ? trackScreen.Entry : null;
                default: return null;
            }
        }

        private Selectable Tab => menu != null ? menu.SelectedTabSelectable : null;

        // REWIRED ON EVERY PAINT, in one authoritative pass: which pack cells
        // hold anything, whether the book row exists and which section is
        // shown are all runtime facts. The reward track wires its own
        // selectables (RewardTrackController.WireNavigation).
        private void RefreshNavigation()
        {
            var groups = new List<UiNavGroup<Selectable>>
            {
                RuntimeNavWiring.Group("dossierRail", UiNavGroupKind.List, sectionRows, wrap: UiNavWrap.Clamp),
            };

            var links = new List<UiNavLink<Selectable>?>();

            if (sectionRows != null)
            {
                var entry = DetailEntry();
                foreach (var row in sectionRows) links.Add(RuntimeNavWiring.Link(row, UiNavDirection.Right, entry));
                if (sectionRows.Length > 0) links.Add(RuntimeNavWiring.Link(sectionRows[0], UiNavDirection.Up, Tab));
            }

            if (_section == CharacterSection.Loadout) DeclareLoadout(groups, links);
            else if (_section == CharacterSection.Attributes) DeclareAttributes(groups, links);
            else if (_section == CharacterSection.Skills) DeclareSkills(groups, links);

            RuntimeNavWiring.Apply(groups.Where(g => g != null), links);
        }

        // The member of `candidates` nearest `of` along the axis a press in
        // that direction does NOT travel: a Left/Right press keeps its height,
        // an Up/Down press keeps its column. Strictly less-than, so a tie keeps
        // the earlier candidate -- Down from Head reaches the left file.
        private static Selectable Nearest(IReadOnlyList<Selectable> candidates, Selectable of, bool horizontal)
        {
            if (of == null || candidates == null) return null;

            Vector2 anchor = CentreOf(of);
            Selectable best = null;
            float bestGap = float.MaxValue;

            foreach (var candidate in candidates)
            {
                if (candidate == null) continue;

                Vector2 centre = CentreOf(candidate);
                float gap = Mathf.Abs(horizontal ? centre.y - anchor.y : centre.x - anchor.x);
                if (gap >= bestGap) continue;

                best = candidate;
                bestGap = gap;
            }

            return best;
        }

        // World space, and the rect's centre rather than its pivot: these
        // controls live under different parents.
        private static Vector2 CentreOf(Selectable control)
        {
            var rect = control.transform as RectTransform;
            return rect == null ? (Vector2)control.transform.position : (Vector2)rect.TransformPoint(rect.rect.center);
        }

        // ---- Cancel and the pending states (plan C5, C6) -------------------------------

        // B steps back out of a pending state, and only then: with nothing
        // pending it declines and the menu closes (plan C6).
        bool INavCancelClaim.ClaimCancel()
        {
            if (!HasPending) return false;

            CancelPending();
            return true;
        }

        // ---- the hotkey I and deep links ----------------------------------------------

        // LOADOUT with focus in the pack: the target of I (MenuTarget.Pack).
        public void FocusPack()
        {
            Wire();
            ShowSection(CharacterSection.Loadout, focusRow: false);

            var cells = BoundPackCells();
            Selectable target = cells.Count > 0 ? cells[0] : (filterChips != null && filterChips.Length > 0 ? filterChips[0] : null);
            if (target != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        // Whether I's target is already showing, so a second I closes the
        // menu (MenuHotkeyRule): LOADOUT, with focus in the pack or its
        // filters.
        public bool IsPackFocused
        {
            get
            {
                if (_section != CharacterSection.Loadout || !isActiveAndEnabled) return false;

                var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                if (selected == null) return false;

                return Owns(packCells, selected) || Owns(filterChips, selected);
            }
        }

        // ---- the footer's prompts (IMenuPromptSource, spec section 8) --------------------
        //
        // What A does on the control the pad stands on, and what B will do. Each
        // prompt that changes state names its MenuAction, and the footer marks
        // it locked wherever MenuPermissions refuses it, so this never promises
        // what the control's own handler then refuses. Controls this pane does
        // not own get null, so the reward track and the frame answer for theirs.
        IReadOnlyList<MenuPrompt> IMenuPromptSource.PromptsFor(Selectable focused)
        {
            if (focused == null) return null;

            var tab = SystemMenuTabs.Def(SystemMenuTab.Character);
            var cancel = new MenuPrompt(PromptButton.Cancel, HasPending ? UiStrings.PromptBack : UiStrings.PromptClose);

            IReadOnlyList<MenuPrompt> Row(MenuPrompt? confirm) =>
                confirm.HasValue ? new[] { confirm.Value, cancel } : new[] { cancel };

            // The rail: selection follows focus, so A does nothing there, and
            // the row names the tab and character shortcuts instead.
            if (Owns(sectionRows, focused.gameObject)) return MenuPrompts.ReadOnly(tab, paged: _chips.Count > 1);

            if (Owns(filterChips, focused.gameObject)) return Row(new MenuPrompt(PromptButton.Confirm, UiStrings.PromptSelect));

            int slot = IndexIn(slotCells, focused.gameObject);
            if (slot >= 0) return Row(GearSlotPrompt(slot));

            int book = IndexIn(bookCells, focused.gameObject);
            if (book >= 0) return Row(BookSlotPrompt(book));

            int cell = IndexIn(packCells, focused.gameObject);
            if (cell >= 0) return Row(PackCellPrompt(cell));

            // The skill and talent rows only inform, so A is not offered on them;
            // the constellation link is the one press in the list.
            if (Owns(skillRows, focused.gameObject)) return MenuPrompts.ReadOnly(tab, paged: _chips.Count > 1);
            if (skillLink != null && focused.gameObject == skillLink.gameObject)
            {
                return Row(new MenuPrompt(PromptButton.Confirm, UiStrings.PromptOpen));
            }

            if (Owns(attributePluses, focused.gameObject))
            {
                return Row(new MenuPrompt(PromptButton.Confirm, UiStrings.PromptSpendPoint, MenuAction.SpendAttribute));
            }

            if (Owns(attributeMinuses, focused.gameObject))
            {
                return Row(new MenuPrompt(PromptButton.Confirm, UiStrings.PromptRefundPoint, MenuAction.RefundAttribute));
            }

            return null;
        }

        // ---- small helpers -------------------------------------------------------------------

        private static bool Owns<T>(T[] controls, GameObject focused) where T : Component => IndexIn(controls, focused) >= 0;

        private static int IndexIn<T>(T[] controls, GameObject focused) where T : Component
        {
            if (controls == null || focused == null) return -1;

            for (int i = 0; i < controls.Length; i++)
            {
                if (controls[i] != null && controls[i].gameObject == focused) return i;
            }

            return -1;
        }

        private static bool Has<T>(T[] array, int index) where T : Object =>
            array != null && index >= 0 && index < array.Length && array[index] != null;

        // UnityEngine.UI.Navigation spelled out in full: PrincesPalace.Navigation
        // (the map's graph state) sits in this namespace and would shadow it.
        private static void SetNoNavigation(Selectable selectable)
        {
            if (selectable == null) return;
            var nav = selectable.navigation;
            nav.mode = UnityEngine.UI.Navigation.Mode.None;
            selectable.navigation = nav;
        }

        private static Color WithAlpha(Color colour, float alpha) => new Color(colour.r, colour.g, colour.b, colour.a * alpha);

        private static Color Hex(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out var color) ? color : Color.white;
    }
}

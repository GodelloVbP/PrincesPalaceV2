using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // The SKILLS section (plan C18, spec 3.3): everything the character can do
    // in a fight, grouped by where it came from, then the talents kindled and the
    // embers left.
    //
    // ONE LIST, WINDOWED BY ENTRY (SkillsListModel). The tree holds a pool of
    // nodes per kind of entry, and this file stands each where the window puts
    // it; a Down off the window's foot moves the window one entry and focus
    // follows the CONTENT, not the slot, because a header and a row are not the
    // same height and a slot kept in place would end up showing a different row.
    //
    // The rows are focusable so the pad can scroll to and read them, and a
    // press does nothing on them (the footer shows no A). The constellation link
    // is the one thing here a press acts on, and only at camp.
    public partial class CharacterDossierController
    {
        [SerializeField] internal TMP_Text[] skillHeaders;
        [SerializeField] internal Button[] skillRows;
        [SerializeField] internal TMP_Text[] skillRowNames;
        [SerializeField] internal TMP_Text[] skillRowMetas;
        [SerializeField] internal TMP_Text[] skillRowDescriptions;
        [SerializeField] internal TMP_Text[] skillNotes;
        [SerializeField] internal Button skillLink;
        [SerializeField] internal Image skillsScrollTrack;
        [SerializeField] internal RectTransform skillsScrollThumb;

        private List<SkillsEntry> _skillEntries = new List<SkillsEntry>();
        private int _skillsTop;

        // The entry the pad last stood on, so a window that moves under it
        // (the wheel, a new character) can put focus back on the same content.
        private int _skillsFocus = -1;
        private string _skillsFor;
        private float _skillsDrag;

        // Which entry each pooled row, and the link, is showing; -1 when hidden.
        private int[] _rowEntry = new int[0];
        private int _linkEntry = -1;

        public int SkillsTop => _skillsTop;
        public int SkillEntryCount => _skillEntries.Count;

        private const float SkillsViewport = DossierLayout.DetailHeight;

        private void WireSkills()
        {
            _rowEntry = new int[skillRows?.Length ?? 0];
            for (int i = 0; i < _rowEntry.Length; i++) _rowEntry[i] = -1;

            for (int i = 0; skillRows != null && i < skillRows.Length; i++)
            {
                WireSkillControl(skillRows[i], i);
            }

            if (skillLink != null)
            {
                skillLink.onClick.AddListener(OpenConstellation);
                WireSkillControl(skillLink, -1);
            }

            // A grab on the track seeks to that point of the list.
            if (skillsScrollTrack != null)
            {
                var seek = skillsScrollTrack.gameObject.GetComponent<ListScroll>()
                           ?? skillsScrollTrack.gameObject.AddComponent<ListScroll>();
                seek.Seeked = SeekSkills;
                FocusKeepClear.Mark(skillsScrollTrack.gameObject);
            }
        }

        // A row (`slot` >= 0) or the link (-1): Up and Down move through the
        // list by entry, the wheel scrolls it, and being selected is remembered.
        private void WireSkillControl(Button control, int slot)
        {
            if (control == null) return;

            var edge = control.gameObject.GetComponent<EdgeScroll>() ?? control.gameObject.AddComponent<EdgeScroll>();
            edge.Moved = direction => SkillsMove(EntryOf(slot), direction);

            var scroll = control.gameObject.GetComponent<ListScroll>() ?? control.gameObject.AddComponent<ListScroll>();
            scroll.WheelStep = DossierLayout.SkillsRowHeight;
            scroll.Scrolled = ScrollSkillsByPixels;

            var watch = control.gameObject.GetComponent<SelectIndex>() ?? control.gameObject.AddComponent<SelectIndex>();
            watch.Index = slot;
            watch.Changed = (index, selected) =>
            {
                if (selected && EntryOf(index) >= 0) _skillsFocus = EntryOf(index);
            };
        }

        private int EntryOf(int slot) =>
            slot < 0 ? _linkEntry : slot < _rowEntry.Length ? _rowEntry[slot] : -1;

        // ---- what the list holds ---------------------------------------------------------

        private void RefreshSkills(Character character)
        {
            bool sameCharacter = _skillsFor == character.definitionId;
            _skillsFor = character.definitionId;

            var skills = new List<SkillListRow>();
            foreach (var sourced in ContentDatabase.SkillSourcesFor(character))
            {
                // The costs the fight will charge, track discounts included.
                var data = ContentDatabase.ApplyRewardTrackSkillDeltas(character, sourced.Skill.Data);
                skills.Add(new SkillListRow(sourced.Source, data.DisplayName,
                    SkillListing.MetaText(data, _primaryPoolName, _signatureName), data.Description));
            }

            var talents = new List<TalentListRow>();
            foreach (string talentId in character.unlockedTalentIds)
            {
                var talent = ContentDatabase.GetTalent(talentId);
                if (talent != null) talents.Add(new TalentListRow(talent.Data.DisplayName, talent.Data.Description));
            }

            // The constellation is a camp building: the link is offered where
            // the player can walk to it (plan C18).
            _skillEntries = SkillsListModel.Build(skills, talents, ContentDatabase.EmbersLeftFor(character),
                showLink: Context == MenuContext.Camp);

            if (!sameCharacter)
            {
                _skillsTop = 0;
                _skillsFocus = -1;
            }

            BindSkillsWindow();
        }

        // Stands the window's entries on the pooled nodes, top to bottom.
        private void BindSkillsWindow()
        {
            _skillsTop = Mathf.Clamp(_skillsTop, 0, SkillsListModel.MaxTop(_skillEntries, SkillsViewport));
            int shown = SkillsListModel.Fit(_skillEntries, _skillsTop, SkillsViewport);

            HideAll(skillHeaders);
            HideAll(skillRows);
            HideAll(skillNotes);
            if (skillLink != null) skillLink.gameObject.SetShown(false);
            for (int i = 0; i < _rowEntry.Length; i++) _rowEntry[i] = -1;
            _linkEntry = -1;

            int headers = 0, rows = 0, notes = 0;
            float y = 0f;

            for (int i = 0; i < shown; i++)
            {
                int index = _skillsTop + i;
                var entry = _skillEntries[index];
                float height = SkillsListModel.HeightOf(entry.Kind);
                float centre = DossierLayout.SkillsCentreY(y, height);
                y += height;

                switch (entry.Kind)
                {
                    case SkillsEntryKind.Header:
                        if (!Has(skillHeaders, headers)) break;
                        PlaceAt(skillHeaders[headers], centre);
                        skillHeaders[headers].SetContent(entry.Title);
                        headers++;
                        break;

                    case SkillsEntryKind.Skill:
                    case SkillsEntryKind.Talent:
                        if (!Has(skillRows, rows)) break;
                        PlaceAt(skillRows[rows], centre);
                        if (Has(skillRowNames, rows)) skillRowNames[rows].SetContent(entry.Title);
                        if (Has(skillRowMetas, rows)) skillRowMetas[rows].SetContent(entry.Meta);
                        if (Has(skillRowDescriptions, rows)) skillRowDescriptions[rows].SetContent(entry.Body);
                        _rowEntry[rows] = index;
                        rows++;
                        break;

                    case SkillsEntryKind.Note:
                        if (!Has(skillNotes, notes)) break;
                        PlaceAt(skillNotes[notes], centre);
                        skillNotes[notes].SetContent(entry.Title);
                        notes++;
                        break;

                    case SkillsEntryKind.Link:
                        if (skillLink == null) break;
                        PlaceAt(skillLink, centre);
                        _linkEntry = index;
                        break;
                }
            }

            PaintSkillsThumb(shown);
            KeepSkillsFocus();
        }

        private static void HideAll(TMP_Text[] labels)
        {
            for (int i = 0; labels != null && i < labels.Length; i++)
            {
                if (labels[i] != null) labels[i].gameObject.SetShown(false);
            }
        }

        private static void HideAll(Button[] buttons)
        {
            for (int i = 0; buttons != null && i < buttons.Length; i++)
            {
                if (buttons[i] != null) buttons[i].gameObject.SetShown(false);
            }
        }

        private static void PlaceAt(Component node, float centreY)
        {
            node.gameObject.SetShown(true);
            var rect = node.transform as RectTransform;
            if (rect != null) rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, centreY);
        }

        private void PaintSkillsThumb(int shown)
        {
            float total = 0f;
            foreach (var entry in _skillEntries) total += SkillsListModel.HeightOf(entry.Kind);

            float visible = 0f;
            for (int i = 0; i < shown; i++) visible += SkillsListModel.HeightOf(_skillEntries[_skillsTop + i].Kind);

            bool scrolls = SkillsListModel.MaxTop(_skillEntries, SkillsViewport) > 0;
            if (skillsScrollTrack != null) skillsScrollTrack.gameObject.SetShown(scrolls);
            if (skillsScrollThumb == null) return;

            skillsScrollThumb.gameObject.SetShown(scrolls);
            if (!scrolls) return;

            float height = DossierLayout.SkillsThumbHeight(visible, total);
            int max = SkillsListModel.MaxTop(_skillEntries, SkillsViewport);
            float travel = DossierLayout.DetailHeight - height;

            skillsScrollThumb.sizeDelta = new Vector2(DossierLayout.ScrollbarWidth, height);
            skillsScrollThumb.anchoredPosition = new Vector2(skillsScrollThumb.anchoredPosition.x,
                travel * 0.5f - travel * _skillsTop / max);
        }

        // ---- moving through it ------------------------------------------------------------

        // Up or Down off `entry`: the next control the pad can stand on, with the
        // window moved just far enough to show it. Off the first one, Up goes to
        // the tab; off the last, Down stays put.
        private void SkillsMove(int entry, MoveDirection direction)
        {
            if (entry < 0) return;

            bool down = direction == MoveDirection.Down;
            if (!down && direction != MoveDirection.Up) return;

            int next = SkillsListModel.NextFocusable(_skillEntries, entry, down ? 1 : -1);
            if (next < 0)
            {
                if (!down)
                {
                    // Leaving through the top puts the window back at its head,
                    // so the way back in shows the first group's name.
                    _skillsTop = 0;
                    BindSkillsWindow();
                    RefreshNavigation();
                    if (Tab != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(Tab.gameObject);
                }

                return;
            }

            _skillsTop = SkillsListModel.TopToReveal(_skillEntries, _skillsTop, next, SkillsViewport);
            _skillsFocus = next;
            BindSkillsWindow();
            RefreshNavigation();
            SelectSkillEntry(next);
        }

        // Moves the window by `entries`, keeping the pad on the content it was on
        // when that is still in view.
        public void ScrollSkills(int entries)
        {
            int before = _skillsTop;
            _skillsTop = Mathf.Clamp(_skillsTop + entries, 0, SkillsListModel.MaxTop(_skillEntries, SkillsViewport));
            if (_skillsTop == before) return;

            BindSkillsWindow();
            RefreshNavigation();
        }

        private void ScrollSkillsByPixels(float pixels)
        {
            _skillsDrag += pixels;
            float step = DossierLayout.SkillsRowHeight;

            while (Mathf.Abs(_skillsDrag) >= step)
            {
                int entries = _skillsDrag > 0f ? 1 : -1;
                _skillsDrag -= entries * step;
                ScrollSkills(entries);
            }
        }

        private void SeekSkills(float fraction)
        {
            int max = SkillsListModel.MaxTop(_skillEntries, SkillsViewport);
            ScrollSkills(Mathf.RoundToInt(Mathf.Clamp01(fraction) * max) - _skillsTop);
        }

        private Selectable SlotShowing(int entry)
        {
            if (entry < 0) return null;
            if (entry == _linkEntry) return skillLink;

            for (int i = 0; i < _rowEntry.Length; i++)
            {
                if (_rowEntry[i] == entry) return skillRows[i];
            }

            return null;
        }

        private void SelectSkillEntry(int entry)
        {
            var slot = SlotShowing(entry);
            if (slot != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(slot.gameObject);
        }

        // The pad is standing in this list: after the window moved, it stands on
        // the same content when that is still shown, else on the first thing in
        // view. Nothing to do when focus is elsewhere.
        private void KeepSkillsFocus()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null || !StandsInSkills(selected)) return;

            var same = SlotShowing(_skillsFocus);
            if (same != null)
            {
                if (same.gameObject != selected) EventSystem.current.SetSelectedGameObject(same.gameObject);
                return;
            }

            var first = FirstShownSkillControl();
            if (first != null) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        private bool StandsInSkills(GameObject selected) =>
            Owns(skillRows, selected) || (skillLink != null && skillLink.gameObject == selected);

        // The topmost control the pad can stand on in the current window.
        private Selectable FirstShownSkillControl()
        {
            int shown = SkillsListModel.Fit(_skillEntries, _skillsTop, SkillsViewport);
            for (int i = 0; i < shown; i++)
            {
                int index = _skillsTop + i;
                if (!_skillEntries[index].Focusable) continue;

                var slot = SlotShowing(index);
                if (slot != null) return slot;
            }

            return null;
        }

        // The window at rest: the head of the list, and the pad's memory of it
        // forgotten.
        private void ResetSkillsWindow()
        {
            _skillsTop = 0;
            _skillsFocus = -1;
            if (_skillEntries.Count > 0) BindSkillsWindow();
        }

        private Selectable SkillsEntryControl() => FirstShownSkillControl();

        // Left off a row goes to the rail; Up and Down are the list's own
        // (SkillsMove), so no link is declared in those directions.
        private void DeclareSkills(List<UiNavGroup<Selectable>> groups, List<UiNavLink<Selectable>?> links)
        {
            var rail = SectionRow(CharacterSection.Skills);

            for (int i = 0; skillRows != null && i < skillRows.Length; i++)
            {
                links.Add(RuntimeNavWiring.Link(skillRows[i], UiNavDirection.Left, rail));
            }

            links.Add(RuntimeNavWiring.Link(skillLink, UiNavDirection.Left, rail));
        }

        // ---- the constellation ---------------------------------------------------------------

        // Through the same route the hub's Talents building takes, opening on
        // this character's page.
        private void OpenConstellation()
        {
            if (Context != MenuContext.Camp) return;

            string id = CharacterId;
            if (string.IsNullOrEmpty(id)) return;

            TalentController.RequestedCharacterId = id;
            if (menu != null) menu.Close();
            Navigation.Go(Navigation.Talents);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Equipment;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace
{
    public enum HoneMode
    {
        // Opened from the shop's HONE: each +1 is paid for, Close leaves.
        Shop,

        // A Rest room: one free +1, or the heal. No way out but a pick.
        Rest,
    }

    // THE HONING MODAL, driven (docs/PLAN_ITEMS.md 1.5). Paints one member's
    // eight slots from the run's slotHoning and sends a press to
    // RunOrchestrator (BuyHone, RestHone, RestHeal), which owns every rule;
    // nothing here decides a price or an outcome.
    //
    // INavSectionStrip: LB/RB step the member the way the shop's do.
    public class HoneController : MonoBehaviour, INavSectionStrip
    {
        [SerializeField] internal TMP_Text titleLabel;
        [SerializeField] internal TMP_Text hintLabel;
        [SerializeField] internal TMP_Text goldLabel;
        [SerializeField] internal Button closeButton;
        [SerializeField] internal Button healButton;
        [SerializeField] internal TMP_Text characterName;
        [SerializeField] internal Button prevCharacterButton;
        [SerializeField] internal Button nextCharacterButton;
        [SerializeField] internal TMP_Text[] slotNames;
        [SerializeField] internal TMP_Text[] levels;
        [SerializeField] internal TMP_Text[] prices;
        [SerializeField] internal Button[] honeButtons;
        [SerializeField] internal TMP_Text[] honeButtonLabels;
        [SerializeField] internal SystemMenuController systemMenu;

        private HoneMode _mode;
        private int _charIndex;
        private bool _wired;
        private NavContext _navContext;

        // Raised when the modal goes away: Close in the shop, a pick in a
        // Rest room. The host repaints.
        public System.Action Finished;

        public bool IsOpen => gameObject.activeSelf;

        public void Open(HoneMode mode, int startIndex = 0)
        {
            _mode = mode;
            _charIndex = startIndex < 0 ? 0 : startIndex;

            gameObject.SetActive(true);
            Wire();
            RegisterNavContext();
            Paint();
        }

        private void OnDisable()
        {
            if (_navContext != null) NavigationInputModule.Contexts?.Remove(_navContext);
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (healButton != null) healButton.onClick.AddListener(Heal);
            if (prevCharacterButton != null) prevCharacterButton.onClick.AddListener(() => StepSection(-1));
            if (nextCharacterButton != null) nextCharacterButton.onClick.AddListener(() => StepSection(1));

            for (int i = 0; i < honeButtons.Length; i++)
            {
                int index = i;
                if (honeButtons[i] != null) honeButtons[i].onClick.AddListener(() => Press(index));
            }
        }

        private static List<Character> Squad()
        {
            var save = SaveSlotManager.CurrentSave;
            return save == null ? new List<Character>() : save.ActiveSquad().Where(c => c != null).ToList();
        }

        private Character Selected()
        {
            var squad = Squad();
            if (squad.Count == 0) return null;
            if (_charIndex >= squad.Count) _charIndex = 0;
            return squad[_charIndex];
        }

        public void StepSection(int direction)
        {
            var squad = Squad();
            if (squad.Count == 0) return;

            _charIndex = (_charIndex + direction + squad.Count) % squad.Count;
            Paint();
        }

        private void Press(int row)
        {
            var character = Selected();
            if (character == null || row < 0 || row >= EquipmentSlots.All.Length) return;

            var slot = EquipmentSlots.All[row];
            if (_mode == HoneMode.Rest)
            {
                if (RunOrchestrator.RestHone(character.definitionId, slot)) Finish();
                return;
            }

            RunOrchestrator.BuyHone(character.definitionId, slot);
            Paint();
        }

        private void Heal()
        {
            if (_mode == HoneMode.Rest && RunOrchestrator.RestHeal()) Finish();
        }

        private void Close()
        {
            if (_mode == HoneMode.Shop) Finish();
        }

        private void Finish()
        {
            gameObject.SetActive(false);
            Finished?.Invoke();
        }

        // ---- painting ---------------------------------------------------------

        private void Paint()
        {
            var run = RunManager.Run;
            var character = Selected();
            if (run == null || character == null) return;

            bool shop = _mode == HoneMode.Shop;

            titleLabel.Set(shop ? UiStrings.HoneTitleShop : UiStrings.HoneTitleRest);
            hintLabel.Set(shop ? UiStrings.HoneHintShop : UiStrings.HoneHintRest);
            goldLabel.gameObject.SetActive(shop);
            if (shop) goldLabel.Set(UiStrings.ShopGold, run.gold);
            closeButton.gameObject.SetActive(shop);
            healButton.gameObject.SetActive(!shop);

            bool canPage = Squad().Count > 1;
            prevCharacterButton.gameObject.SetActive(canPage);
            nextCharacterButton.gameObject.SetActive(canPage);

            var definition = ContentDatabase.GetCharacter(character.definitionId);
            characterName.SetContent(definition == null || string.IsNullOrWhiteSpace(definition.Data.DisplayName)
                ? character.definitionId
                : definition.Data.DisplayName);

            for (int i = 0; i < EquipmentSlots.All.Length; i++)
            {
                var slot = EquipmentSlots.All[i];
                int plus = SlotHoning.PlusOf(run.slotHoning, character.definitionId, slot);
                bool maxed = !SlotHoning.CanHone(run.slotHoning, character.definitionId, slot);
                int price = shop ? RunOrchestrator.HonePriceFor(character.definitionId, slot) : 0;

                slotNames[i].Set(UiStrings.EquipmentSlotName(slot));
                if (maxed) levels[i].Set(UiStrings.HoneLevelMax, plus);
                else levels[i].Set(UiStrings.HoneLevel, plus, plus + 1);

                prices[i].gameObject.SetActive(!maxed);
                if (!maxed)
                {
                    if (shop) prices[i].Set(UiStrings.HonePrice, price);
                    else prices[i].Set(UiStrings.HoneFree);
                }

                honeButtonLabels[i].Set(maxed ? UiStrings.HoneMaxButton : UiStrings.HoneButton);
                honeButtons[i].interactable = !maxed && (!shop || run.gold >= price);
            }

            RefreshNavigation();
        }

        // ---- navigation -------------------------------------------------------

        private void RegisterNavContext()
        {
            if (_navContext == null)
            {
                _navContext = new NavContext(entry: null, selectables: null, cancel: HandleCancel,
                    systemMenu: () => SystemMenuController.OpenFromRoot(systemMenu), sectionStrip: () => this);
            }

            NavigationInputModule.Contexts?.PushIfAbsent(_navContext);
        }

        // Cancel closes the shop's modal; a Rest room has nothing to back
        // out to.
        private void HandleCancel()
        {
            if (_mode == HoneMode.Shop) Close();
        }

        private void RefreshNavigation()
        {
            if (_navContext == null) return;

            var rows = honeButtons.Where(b => b != null).ToList();
            var footer = _mode == HoneMode.Shop ? closeButton : healButton;

            var groups = new List<UiNavGroup<Selectable>>();
            var rowGroup = RuntimeNavWiring.Group("honeRows", UiNavGroupKind.List, rows);
            if (rowGroup != null) groups.Add(rowGroup);

            var links = new List<UiNavLink<Selectable>?>();
            if (rows.Count > 0 && footer != null)
            {
                links.Add(RuntimeNavWiring.Link(rows[rows.Count - 1], UiNavDirection.Down, footer));
                links.Add(RuntimeNavWiring.Link(footer, UiNavDirection.Up, rows[rows.Count - 1]));
            }

            RuntimeNavWiring.Apply(groups, links);

            var selectables = new Dictionary<string, object>();
            for (int i = 0; i < rows.Count; i++) selectables[$"row{i}"] = rows[i].gameObject;
            if (footer != null) selectables["footer"] = footer.gameObject;

            var entry = rows.FirstOrDefault(b => b.interactable)?.gameObject ?? footer?.gameObject;
            _navContext.Reconfigure(entry, selectables);
        }
    }
}

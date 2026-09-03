using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.DebugMenu;
using PrincesPalace.Domain.Economy;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // The debug menu, driven.
    //
    // Grants currency and single items into the save. Every rule it needs
    // already exists -- Wallet.Add, InventoryOps.Add, DebugMenuCatalog -- so
    // this resolves content, paints rows, and writes.
    //
    // WHY A PICKER AND NOT A "GIVE ME EVERYTHING" BUTTON: there are 261
    // generated item assets and the bag shows 20 per page behind prev/next
    // arrows with no filter. Dumping the catalogue into the bag would produce
    // 14 unnavigable pages and look like a bug. The browsing happens here,
    // where a filter and a pager are cheap, and the bag only ever holds what
    // was deliberately asked for.
    public class DebugMenuController : MonoBehaviour
    {
        [SerializeField] internal Button closeButton;
        [SerializeField] internal Button giveGoldButton;
        [SerializeField] internal Button giveEmbersButton;
        [SerializeField] internal Button giveOneEmberButton;

        [SerializeField] internal Button[] filterButtons;
        [SerializeField] internal Button[] rowButtons;
        [SerializeField] internal TMP_Text[] rowLabels;

        [SerializeField] internal TMP_Text pageLabel;
        [SerializeField] internal Button prevPageButton;
        [SerializeField] internal Button nextPageButton;

        // Named so the amounts are stated once and are visible to the test
        // that pins them, rather than buried in a lambda.
        public const int GoldGrant = 10000;
        public const int EmberGrant = 25;

        // KindAll, then the three ItemKind ordinals, matching the order the
        // screen declares its filter buttons in.
        private static readonly int[] FilterKinds =
        {
            DebugMenuCatalog.KindAll,
            (int)ItemKind.Consumable,
            (int)ItemKind.Weapon,
            (int)ItemKind.Equipment,
        };

        private int _filter;
        private int _page;
        private bool _wired;
        private IReadOnlyList<DebugItem> _filtered = new List<DebugItem>();

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

            closeButton.onClick.AddListener(() => gameObject.SetActive(false));

            giveGoldButton.onClick.AddListener(() => Give(CurrencyType.Gold, GoldGrant));
            giveEmbersButton.onClick.AddListener(() => GiveEmbers(EmberGrant));
            giveOneEmberButton.onClick.AddListener(() => GiveEmbers(1));

            for (int i = 0; i < filterButtons.Length; i++)
            {
                int index = i;
                filterButtons[i].onClick.AddListener(() => SetFilter(index));
            }

            for (int i = 0; i < rowButtons.Length; i++)
            {
                int index = i;
                rowButtons[i].onClick.AddListener(() => GrantRow(index));
            }

            prevPageButton.onClick.AddListener(() => StepPage(-1));
            nextPageButton.onClick.AddListener(() => StepPage(1));
        }

        private static SaveData Save => SaveSlotManager.CurrentSave;

        // ---- granting -------------------------------------------------------------

        // Gold lands on the SAVE's wallet, not RunState's.
        //
        // Gold exists on both (see CurrencyType: RunState holds the at-risk
        // stake, SaveData the banked total). Granting the banked one is right
        // for a debug tool -- that is the pile you spend in the hub, and it is
        // the one that survives whatever you are about to do to the run.
        private void Give(CurrencyType currency, int amount)
        {
            var save = Save;
            if (save?.wallet == null) return;

            save.wallet.Add(currency, amount);
            SaveSlotManager.SaveCurrent();
            Refresh();
        }

        // Embers are per CHARACTER, so a debug grant has to say to whom. The
        // whole roster, not the squad: the point of the button is to be able to
        // test any character's tree without first fielding them.
        private void GiveEmbers(int amount)
        {
            var save = Save;
            if (save?.roster == null) return;

            foreach (var character in save.roster)
            {
                if (character != null) character.embers += amount;
            }

            SaveSlotManager.SaveCurrent();
            Refresh();
        }

        private void GrantRow(int index)
        {
            var save = Save;
            if (save == null) return;

            var page = DebugMenuCatalog.Page(_filtered, _page);
            if (index < 0 || index >= page.Count) return;

            // One at a time, at +0. A debug menu that grants a stack of ten
            // makes the bag's stacking the thing under test instead of
            // whatever you opened the menu to look at; press it ten times if
            // that is what you want.
            InventoryOps.Add(save.stockpiledItems, page[index].Id);
            SaveSlotManager.SaveCurrent();
        }

        // ---- input -----------------------------------------------------------------

        private void SetFilter(int index)
        {
            _filter = index < 0 || index >= FilterKinds.Length ? 0 : index;

            // Back to page one: page 7 of the weapons is not page 7 of the
            // potions, and landing on an empty page reads as a broken filter.
            _page = 0;
            Refresh();
        }

        private void StepPage(int direction)
        {
            _page = DebugMenuCatalog.ClampPage(_page + direction, _filtered.Count);
            Refresh();
        }

        // ---- painting ----------------------------------------------------------------

        public void Refresh()
        {
            _filtered = DebugMenuCatalog.Filter(Catalogue(), FilterKinds[_filter]);
            _page = DebugMenuCatalog.ClampPage(_page, _filtered.Count);

            pageLabel.Set(UiStrings.DebugPage, _page + 1, DebugMenuCatalog.PageCount(_filtered.Count));

            for (int i = 0; i < filterButtons.Length; i++)
            {
                // Themed(Silver) now (balance-bot, 2026-09-02): targetGraphic
                // is the plate, so a direct colour write here would fight
                // ThemedButtonState's own idle/hover/press tint. SetMenuState
                // (through ApplySelection) is the same Open/Idle distinction
                // the old flat colours used to draw, painted on the plate
                // instead.
                ThemedButtonState.ApplySelection(filterButtons[i], i == _filter);
            }

            var page = DebugMenuCatalog.Page(_filtered, _page);

            for (int i = 0; i < rowButtons.Length; i++)
            {
                bool present = i < page.Count;

                // Hidden rather than drawn blank, same rule as the bag: a
                // half-full list of empty boxes reads as a load failure.
                rowButtons[i].gameObject.SetActive(present);
                if (!present) continue;

                rowLabels[i].Set(UiStrings.DebugRow, page[i].Tier, page[i].Name);
            }
        }

        // Read fresh each Refresh rather than cached at Start.
        //
        // ContentDatabase is loaded from Resources, and in the editor a
        // content rebuild can replace it underneath a running scene. A debug
        // menu holding a stale catalogue across exactly that operation would
        // be worse than useless.
        private static IEnumerable<DebugItem> Catalogue()
        {
            return ContentDatabase.Items.Select(item => new DebugItem(
                item.id,
                string.IsNullOrEmpty(item.displayName) ? item.id : item.displayName,
                (int)item.kind,
                item.tier));
        }
    }
}

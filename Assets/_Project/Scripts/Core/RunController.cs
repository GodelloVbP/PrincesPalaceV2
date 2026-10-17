using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // Fills and navigates the RUN pane (plan C23, spec sections 4 and 9).
    //
    // Everything on it is read off the run when the pane opens, never cached:
    // RunManager.Run for the relics, buffs, bonus damage and gold, the save's
    // stockpile for the supplies. In a fight that is still right, because the
    // fight writes the run and the stockpile as it goes and none of the pane's
    // values are the fight's own (plan C4).
    //
    // NAVIGATION. Two focus columns, matching where the focusable rows are:
    // the relic list, and the effects and supplies list stacked under one
    // another. Up/Down step within a column, Left/Right cross to the nearest
    // row of the other, and Up from a column's first row reaches the selected
    // tab. LB/RB page the relics (INavSectionStrip).
    public class RunController : MonoBehaviour, INavPaneEntry, INavSectionStrip, IMenuPromptSource
    {
        [SerializeField] internal SystemMenuController menu;

        // ---- relics
        [SerializeField] internal Button[] relicButtons;
        [SerializeField] internal Image[] relicIcons;
        [SerializeField] internal TMP_Text[] relicNames;
        [SerializeField] internal TMP_Text[] relicRarities;
        [SerializeField] internal TMP_Text[] relicBodies;
        [SerializeField] internal Button relicPrevPageButton;
        [SerializeField] internal Button relicNextPageButton;
        [SerializeField] internal TMP_Text relicPageLabel;
        [SerializeField] internal GameObject relicEmptyHint;
        [SerializeField] internal TMP_Text relicsHeading;

        // ---- effects
        [SerializeField] internal Button[] effectButtons;
        [SerializeField] internal TMP_Text[] effectLabels;
        [SerializeField] internal TMP_Text[] effectValues;
        [SerializeField] internal GameObject effectsEmptyHint;

        // ---- supplies
        [SerializeField] internal Button[] supplyButtons;
        [SerializeField] internal Image[] supplyIcons;
        [SerializeField] internal TMP_Text[] supplyNames;
        [SerializeField] internal TMP_Text[] supplyCounts;
        [SerializeField] internal TMP_Text[] supplyEffects;
        [SerializeField] internal GameObject suppliesEmptyHint;
        [SerializeField] internal TMP_Text supplyMore;
        [SerializeField] internal TMP_Text goldValue;

        // ---- this descent: keyed rather than index-aligned with
        // RunStatRows.AllRows, so reordering the table cannot silently swap two
        // figures.
        [SerializeField] internal string[] statKeys;
        [SerializeField] internal TMP_Text[] statValues;

        // Relic and item art by id, baked with the scene.
        [SerializeField] internal IconEntry[] icons;

        // What a figure reads while there is no run to describe. The tab is
        // run-only, so this is unreachable through the UI; a screen that
        // printed zeroes for a run that does not exist would be claiming the
        // run went badly.
        private const string NoRun = "-";

        private RelicListPresenter _relics;
        private bool _wired;

        private RelicListPresenter Relics
        {
            get
            {
                if (_relics != null) return _relics;

                var rows = new GameObject[relicButtons == null ? 0 : relicButtons.Length];
                for (int i = 0; i < rows.Length; i++) rows[i] = relicButtons[i] == null ? null : relicButtons[i].gameObject;

                _relics = new RelicListPresenter(new RelicListWidgets(
                    rows, relicIcons, relicNames, relicRarities, relicBodies,
                    relicPrevPageButton, relicNextPageButton, relicPageLabel, relicEmptyHint, icons));
                return _relics;
            }
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            Relics.WirePager();
            Relics.Paged = RefreshNavigation;
        }

        private void OnEnable()
        {
            Wire();
            Refresh();
        }

        public void Refresh()
        {
            Wire();
            var run = RunManager.Run;

            var held = run != null && run.hasRun ? run.relicIds : null;
            Relics.Show(held);
            if (relicsHeading != null) relicsHeading.Set(UiStrings.RunRelicsHeading, held?.Count ?? 0);

            PaintEffects(run);
            PaintSupplies(run);
            PaintFigures(run);
            RefreshNavigation();
        }

        // ---- effects ------------------------------------------------------------

        private void PaintEffects(RunSnapshot run)
        {
            var rows = run == null || !run.hasRun
                ? new List<RunEffectRow>()
                : RunEffectRows.Rows(run.eventBuffs, run.legStartStep, ItemOfferRoll.CurrentSquadFavor(),
                    run.bonusDamagePercent,
                    SquadTrack.SecondLivesLeft(run));

            if (effectsEmptyHint != null) effectsEmptyHint.SetActive(rows.Count == 0);

            for (int i = 0; i < effectButtons.Length; i++)
            {
                bool present = i < rows.Count;
                effectButtons[i].gameObject.SetActive(present);
                if (!present) continue;

                effectLabels[i].SetContent(rows[i].Label);
                effectValues[i].SetContent(rows[i].Value);
            }
        }

        // ---- supplies -----------------------------------------------------------

        private void PaintSupplies(RunSnapshot run)
        {
            var stacks = run != null && run.hasRun ? RunOrchestrator.BuildSatchel() : new List<SatchelStack>();

            if (suppliesEmptyHint != null) suppliesEmptyHint.SetActive(stacks.Count == 0);

            for (int i = 0; i < supplyButtons.Length; i++)
            {
                bool present = i < stacks.Count;
                supplyButtons[i].gameObject.SetActive(present);
                if (!present) continue;

                var stack = stacks[i];
                var item = ContentDatabase.GetItem(stack.ItemId);

                supplyNames[i].SetContent(stack.DisplayName);
                supplyCounts[i].Set(UiStrings.RunSupplyCount, stack.Count);
                supplyEffects[i].SetContent(item != null ? item.description : "");
                ItemIcons.Apply(At(supplyIcons, i), icons, stack.ItemId);
            }

            int hidden = stacks.Count - supplyButtons.Length;
            if (supplyMore != null)
            {
                supplyMore.gameObject.SetActive(hidden > 0);
                if (hidden > 0) supplyMore.Set(UiStrings.RunSupplyMore, hidden);
            }

            if (goldValue != null) goldValue.SetContent(Figure(run, "gold_held"));
        }

        // ---- this descent ---------------------------------------------------------

        private void PaintFigures(RunSnapshot run)
        {
            if (statValues == null || statKeys == null) return;

            int count = Mathf.Min(statValues.Length, statKeys.Length);
            for (int i = 0; i < count; i++)
            {
                if (statValues[i] != null) statValues[i].SetContent(Figure(run, statKeys[i]));
            }
        }

        public static string Figure(RunSnapshot run, string key)
        {
            if (run == null || !run.hasRun) return NoRun;

            // A key with nothing behind it reads as absent rather than as zero.
            // Zero is a claim about the run; this is a claim about the build.
            if (!TryFigure(run, key, out long value)) return NoRun;

            // Grouped, because these run to five figures by the second floor and
            // "8420" is a number you have to count. Invariant rather than the
            // machine's culture: the rest of the UI is not localised, and a
            // German build printing 8.420 beside an English label would be
            // stranger than either alone.
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }

        // THE BINDING. Every key RunStatRows declares has to appear here (a
        // test sweeps them), and a switch with no default arm that returns true
        // is the rule "every row binds to a tracked field or gets cut" as code.
        //
        // "gold_held" is the Supplies column's own gold row, not a stat row.
        public static bool TryFigure(RunSnapshot run, string key, out long value)
        {
            value = 0L;
            if (run == null) return false;

            switch (key)
            {
                // ---- Battle: the run ledger, summed across the party --------
                //
                // RunLedger.TotalDamage rather than a hand-rolled sum: it is
                // already the one place that walks the ledger, and it returns
                // long because a long profile passes two billion.
                case "damage_dealt": value = RunLedger.TotalDamage(run); return true;
                case "damage_taken": value = SumLedger(run, e => e.damageTaken); return true;
                case "healed": value = SumLedger(run, e => e.healed); return true;
                case "kills": value = SumLedger(run, e => e.kills); return true;
                case "times_downed": value = SumLedger(run, e => e.timesDowned); return true;

                // ---- The Fold: how far down it got --------------------------
                case "floor": value = run.floor; return true;
                case "rooms_cleared": value = run.roomsCleared; return true;

                // deepestStep, NOT step. The latter is where the party is
                // standing, and a run that ends is a run that stopped moving.
                case "deepest_room": value = run.deepestStep; return true;
                case "bosses": value = run.bossesKilled?.Count ?? 0; return true;

                // ---- Spoils / supplies --------------------------------------
                //
                // Held and earned are different numbers the moment anything is
                // spent, and a shop exists.
                case "gold_held": value = run.gold; return true;
                case "gold_earned": value = run.goldEarned; return true;
                case "exp_earned": value = run.expEarned; return true;
                case "relics": value = run.relicIds?.Count ?? 0; return true;

                // Stacks counted by their contents, not by their entries: three
                // salves in one slot are three items in the pack.
                case "pack": value = PackCount(); return true;

                default: return false;
            }
        }

        private static long SumLedger(RunSnapshot run, System.Func<RunLedgerEntry, long> pick)
        {
            if (run.ledger == null) return 0L;

            long total = 0L;
            foreach (var entry in run.ledger)
            {
                if (entry != null) total += pick(entry);
            }

            return total;
        }

        // The same stacks the Supplies list paints, so the figure and the list
        // cannot disagree.
        private static long PackCount()
        {
            long total = 0L;
            foreach (var stack in RunOrchestrator.BuildSatchel()) total += stack.Count;
            return total;
        }

        // ---- deep link ------------------------------------------------------------------

        // `Open(Run, relic id)`: turns to the page holding the relic and puts
        // focus on its row. False when the run does not hold it.
        public bool ShowRelic(string relicId)
        {
            Wire();
            int slot = Relics.ShowRelic(relicId);
            if (slot < 0) return false;

            RefreshNavigation();
            var row = slot < relicButtons.Length ? relicButtons[slot] : null;
            if (row != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(row.gameObject);
            return true;
        }

        // ---- navigation -------------------------------------------------------------------

        // INavPaneEntry: the first relic row; with none, the first effects or
        // supplies row, else the frame's own fallback.
        public Selectable NavEntry
        {
            get
            {
                var relics = ActiveRows(relicButtons);
                if (relics.Count > 0) return relics[0];

                var other = OtherColumn();
                return other.Count > 0 ? other[0] : null;
            }
        }

        private static List<Selectable> ActiveRows(Button[] buttons)
        {
            var active = new List<Selectable>();
            if (buttons == null) return active;

            foreach (var button in buttons)
            {
                if (button != null && button.gameObject.activeSelf) active.Add(button);
            }

            return active;
        }

        // Effects, then supplies: one column on screen, one list to the pad.
        private List<Selectable> OtherColumn()
        {
            var column = ActiveRows(effectButtons);
            column.AddRange(ActiveRows(supplyButtons));
            return column;
        }

        private void RefreshNavigation()
        {
            var relics = ActiveRows(relicButtons);
            var other = OtherColumn();
            var tab = menu != null ? menu.SelectedTabSelectable : null;

            var links = new List<UiNavLink<Selectable>?>();
            foreach (var row in relics)
            {
                links.Add(RuntimeNavWiring.Link(row, UiNavDirection.Right, Nearest(other, row)));
            }

            foreach (var row in other)
            {
                links.Add(RuntimeNavWiring.Link(row, UiNavDirection.Left, Nearest(relics, row)));
            }

            // Up from a column's first row reaches the tab, as every pane does.
            if (relics.Count > 0) links.Add(RuntimeNavWiring.Link(relics[0], UiNavDirection.Up, tab));
            if (other.Count > 0) links.Add(RuntimeNavWiring.Link(other[0], UiNavDirection.Up, tab));

            RuntimeNavWiring.Apply(new[]
            {
                RuntimeNavWiring.Group("runRelics", UiNavGroupKind.List, relics),
                RuntimeNavWiring.Group("runOther", UiNavGroupKind.List, other),
            }.Where(g => g != null), links);
        }

        // The row of `candidates` level with `from`, so a sideways move keeps
        // the eye on the same line.
        private static Selectable Nearest(List<Selectable> candidates, Selectable from)
        {
            Selectable best = null;
            float bestGap = float.MaxValue;
            float y = from.transform.position.y;

            foreach (var candidate in candidates)
            {
                float gap = Mathf.Abs(candidate.transform.position.y - y);
                if (gap < bestGap)
                {
                    bestGap = gap;
                    best = candidate;
                }
            }

            return best;
        }

        // LB/RB: the relic page. Focus stays on the same slot of the new page
        // (or its last row), so paging never drops the pad into the void.
        public void StepSection(int direction)
        {
            Wire();
            int slot = FocusedRelicSlot();
            if (!Relics.Step(direction)) return;

            RefreshNavigation();
            if (slot < 0 || EventSystem.current == null) return;

            var rows = ActiveRows(relicButtons);
            if (rows.Count > 0) EventSystem.current.SetSelectedGameObject(rows[Mathf.Min(slot, rows.Count - 1)].gameObject);
        }

        private int FocusedRelicSlot()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null || relicButtons == null) return -1;

            for (int i = 0; i < relicButtons.Length; i++)
            {
                if (relicButtons[i] != null && relicButtons[i].gameObject == selected) return i;
            }

            return -1;
        }

        // ---- the footer ---------------------------------------------------------------------

        // No "A" on the pane's own rows (or with nothing focused): nothing on
        // it does anything when pressed. Elsewhere (the tab bar) the frame's row
        // answers, and the frame drops A on the tab that is already showing.
        // Either way LB/RB is offered only while the relics span more than one
        // page.
        IReadOnlyList<MenuPrompt> IMenuPromptSource.PromptsFor(Selectable focused)
        {
            Wire();
            var tab = SystemMenuTabs.Def(SystemMenuTab.Run);
            bool paged = Relics.Model.IsPaged;

            return focused == null || IsOwnRow(focused)
                ? MenuPrompts.ReadOnly(tab, paged)
                : MenuPrompts.Defaults(tab, paged);
        }

        private bool IsOwnRow(Selectable focused)
        {
            foreach (var group in new[] { relicButtons, effectButtons, supplyButtons })
            {
                if (group == null) continue;
                foreach (var button in group)
                {
                    if (button == focused) return true;
                }
            }

            return false;
        }

        private static T At<T>(T[] all, int index) where T : class =>
            all != null && index >= 0 && index < all.Length ? all[index] : null;
    }
}

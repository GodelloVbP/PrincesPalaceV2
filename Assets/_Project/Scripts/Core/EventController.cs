using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Events;
using PrincesPalace.Domain.UiKit;
using PrincesPalace.Domain.UiKit.Screens;

namespace PrincesPalace
{
    // An event room, driven. The shop's shape (Open, one Paint from the run,
    // a Finished the map hands its Refresh to), reading nothing but
    // RunOrchestrator.CurrentEvent() and acting through ChooseEventOption /
    // LeaveEvent -- the panel holds no event state of its own, so a reload,
    // the debug menu and a real walk all paint the same thing.
    //
    // ONE RULE DECIDES WHAT IS PICKABLE, and it is not in this file:
    // EventChoiceView.Enabled comes from EventChoiceGate, the same gate
    // ChooseEventOption refuses with and the bot skips by. This class greys
    // what that says is locked and keeps it off the rail; it never decides.
    //
    // CANCEL DOES NOTHING HERE (plan contract 12). Leaving is always an
    // authored choice or the concluded state's own Leave, so a stray back
    // press can never skip a room. Start still reaches the system menu.
    public class EventController : MonoBehaviour
    {
        [SerializeField] internal Image artImage;
        [SerializeField] internal TMP_Text titleLabel;
        [SerializeField] internal TMP_Text bodyLabel;
        [SerializeField] internal TMP_Text effectsLabel;

        // One entry per row, EventScreen.ChoiceRowCount long.
        [SerializeField] internal Button[] choiceButtons;
        [SerializeField] internal TMP_Text[] choiceTexts;
        [SerializeField] internal TMP_Text[] choiceLocks;

        // Every page's art, keyed by its authored artPath, baked by
        // ScreenRegistry.WireEvent because only the builder can reach
        // AssetDatabase. A page whose key is absent shows the empty frame.
        [UiOptional("no event authors art yet -- the demo event deliberately has none, to exercise the missing-art path")]
        [SerializeField] internal IconEntry[] eventArt;

        // The map's own menu instance, shared as the shop shares it.
        [SerializeField] internal SystemMenuController systemMenu;

        // Raised when the event closes. The map owns navigation.
        public System.Action Finished;

        // What each visible row does: a choice's AUTHORED index, or LeaveRow
        // for the concluded state's own Leave. Rows are packed top-down over
        // the visible choices, so row i is not choice i.
        private const int LeaveRow = -1;
        private readonly int[] _rowAction = new int[EventScreen.ChoiceRowCount];

        // The concluded state has no page, so CurrentEvent() gives it no
        // title or art. The panel keeps showing the page the player was on
        // rather than blanking the header under the result they are reading.
        private string _heldEventId;
        private string _heldTitle;
        private string _heldArtKey;

        private bool _wired;
        private NavContext _navContext;

        private static readonly Color ChoiceColour = Hex(EventScreen.ChoiceTextHex);
        private static readonly Color LockedColour = Hex(EventScreen.LockedChoiceTextHex);

        private static Color Hex(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out var colour) ? colour : Color.white;

        public bool IsOpen => gameObject.activeSelf;

        public void Open()
        {
            gameObject.SetActive(true);
            Wire();
            RegisterNavContext();
            Paint();
        }

        private void OnDisable()
        {
            PopNavContext();
        }

        private void OnDestroy()
        {
            PopNavContext();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                int row = i;
                choiceButtons[i].onClick.AddListener(() => Press(row));
            }
        }

        // ---- painting -----------------------------------------------------------

        private void Paint()
        {
            var view = RunOrchestrator.CurrentEvent();
            if (view == null)
            {
                // Nothing open under the party: the room was closed from
                // somewhere else (the debug menu, a reconcile). Showing a
                // stale page would offer choices the orchestrator refuses.
                Close();
                return;
            }

            if (!view.Concluded)
            {
                _heldEventId = view.EventId;
                _heldTitle = view.Title;
                _heldArtKey = view.ArtKey;
            }

            bool held = view.Concluded && _heldEventId == view.EventId;
            string title = view.Concluded ? (held ? _heldTitle : "") : view.Title;
            string artKey = view.Concluded ? (held ? _heldArtKey : "") : view.ArtKey;

            ItemIcons.Apply(artImage, eventArt, artKey);
            titleLabel.Set(UiStrings.EventTitle, title);

            // The result REPLACES the body (contract 11): it is what just
            // happened, and the page text above it is what the player already
            // read to choose.
            bodyLabel.Set(UiStrings.EventBody, string.IsNullOrEmpty(view.ResultText) ? view.Body : view.ResultText);
            effectsLabel.Set(UiStrings.EventEffects, view.EffectsLine);

            PaintRows(view);
            RefreshNavigation();
        }

        private void PaintRows(EventView view)
        {
            int row = 0;

            if (view.Concluded)
            {
                PaintRow(row++, UiStrings.EventLeave.Format(), enabled: true, lockReason: "", action: LeaveRow);
            }
            else
            {
                foreach (var choice in view.Choices)
                {
                    if (choice == null || !choice.Visible) continue;
                    if (row >= choiceButtons.Length) break;
                    PaintRow(row++, choice.Text, choice.Enabled, choice.LockReason, choice.Index);
                }
            }

            for (; row < choiceButtons.Length; row++)
            {
                choiceButtons[row].gameObject.SetActive(false);
            }
        }

        private void PaintRow(int row, string text, bool enabled, string lockReason, int action)
        {
            var button = choiceButtons[row];
            button.gameObject.SetActive(true);
            button.interactable = enabled;
            _rowAction[row] = action;

            choiceTexts[row].Set(UiStrings.EventChoice, text);
            choiceTexts[row].color = enabled ? ChoiceColour : LockedColour;

            bool showLock = !enabled && !string.IsNullOrEmpty(lockReason);
            choiceLocks[row].gameObject.SetActive(showLock);
            if (showLock) choiceLocks[row].Set(UiStrings.EventChoiceLocked, lockReason);
        }

        // ---- acting ---------------------------------------------------------------

        private void Press(int row)
        {
            if (row < 0 || row >= choiceButtons.Length) return;

            // A locked row is interactable=false and off the rail, so no
            // input reaches here for it; checked anyway because onClick.Invoke
            // does not ask the Button first.
            if (!choiceButtons[row].interactable || !choiceButtons[row].gameObject.activeInHierarchy) return;

            if (_rowAction[row] == LeaveRow)
            {
                RunOrchestrator.LeaveEvent();
                Close();
                return;
            }

            var result = RunOrchestrator.ChooseEventOption(_rowAction[row]);
            if (result.Closed)
            {
                Close();
                return;
            }

            // Applied or refused, the page to show is whatever the run now
            // says -- a refusal leaves it unchanged, which repaints the same.
            Paint();
        }

        private void Close()
        {
            gameObject.SetActive(false); // OnDisable pops the context
            Finished?.Invoke();
        }

        // ---- gamepad ----------------------------------------------------------------
        //
        // A MODAL CONTEXT, the shop's shape: pushed on Open, popped on any
        // deactivation. Kept across closes so it carries its focus memory.

        private void RegisterNavContext()
        {
            if (_navContext == null)
            {
                _navContext = new NavContext(entry: null, selectables: null, cancel: null,
                    systemMenu: HandleSystemMenu);
            }

            NavigationInputModule.Contexts?.PushIfAbsent(_navContext);
        }

        private void PopNavContext()
        {
            if (_navContext == null) return;
            NavigationInputModule.Contexts?.Remove(_navContext);
        }

        private void HandleSystemMenu() => SystemMenuController.OpenFromRoot(systemMenu);

        // ONE VERTICAL RAIL of the pickable rows. A locked row is not a member
        // and its own navigation is cleared, so no neighbour can step onto it
        // and no stale link from an earlier page can survive into this one.
        private void RefreshNavigation()
        {
            var members = new List<Selectable>();
            var selectables = new Dictionary<string, object>();

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                var button = choiceButtons[i];
                if (button == null) continue;

                if (button.gameObject.activeSelf && button.interactable)
                {
                    members.Add(button);
                    selectables[$"eventChoice{i}"] = button.gameObject;
                }
                else
                {
                    button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
                }
            }

            var group = RuntimeNavWiring.Group("eventChoices", UiNavGroupKind.List, members);
            if (group != null) RuntimeNavWiring.Apply(group);

            // THE GAMEOBJECT, never the Button -- ReckoningController's
            // RefreshNavigation says why.
            object entry = members.Count > 0 ? members[0].gameObject : null;
            _navContext?.Reconfigure(entry, selectables);
        }
    }
}

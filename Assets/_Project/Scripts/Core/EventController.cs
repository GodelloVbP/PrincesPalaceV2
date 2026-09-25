using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.Content;
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

        // ---- the dialogue stage (docs/PLAN_DIALOGUE_STAGE.md, D2) ----------------
        //
        // Shown instead of the legacy frame and text column when the page has
        // lines (EventView.HasLines); every other page paints exactly as
        // before (contract 14). D2 is the STATIC stage: the page's first line
        // with its speaker, laid out. Stepping, the typewriter and the slides
        // are D3's.

        // The legacy layout's two halves, switched off under the stage.
        [SerializeField] internal GameObject artFrame;
        [SerializeField] internal GameObject textColumn;

        // The rows' one panel, which both presentations share and the stage
        // moves above its box.
        [SerializeField] internal RectTransform choicesPanel;

        [SerializeField] internal RectTransform dialogueStage;
        [SerializeField] internal Image stageBackdrop;
        [SerializeField] internal Image stageSetPiece;

        // Two, for D3's speaker slide; D2 paints [0] and keeps [1] off.
        [SerializeField] internal Image[] stageBusts;
        [SerializeField] internal RectTransform stageBox;
        [SerializeField] internal TMP_Text stageLineText;
        [SerializeField] internal RectTransform stageNamePlate;
        [SerializeField] internal TMP_Text stageName;
        [SerializeField] internal TMP_Text stageEpithet;
        [SerializeField] internal TMP_Text stageTitle;

        // Every backdrop key an event or page can name, baked by
        // ScreenRegistry.WireEvent like eventArt. A key that is absent draws
        // the stage's solid ground (contract 15).
        [UiOptional("a backdrop whose file is missing is left out of the bake, and an empty table is legal -- the stage then draws its solid ground (contract 15)")]
        [SerializeField] internal IconEntry[] backdropArt;

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

            // Contract 14: a page without lines never touches the stage, and
            // the rows go back to their legacy spot in case the page before
            // this one had lines.
            bool staged = view.HasLines;
            ShowStage(staged);

            if (staged)
            {
                PaintStage(view);
            }
            else
            {
                ItemIcons.Apply(artImage, eventArt, artKey);
                titleLabel.Set(UiStrings.EventTitle, title);

                // The result REPLACES the body (contract 11): it is what just
                // happened, and the page text above it is what the player
                // already read to choose.
                bodyLabel.Set(UiStrings.EventBody, string.IsNullOrEmpty(view.ResultText) ? view.Body : view.ResultText);
                effectsLabel.Set(UiStrings.EventEffects, view.EffectsLine);
            }

            PaintRows(view);
            RefreshNavigation();
        }

        // ---- the dialogue stage ---------------------------------------------------

        // Where the tree put the rows, captured before the stage first moves
        // them, so a lines-less page after a staged one is laid out exactly
        // as the build laid it out.
        private bool _choicesHomeKnown;
        private Vector2 _choicesHomeAnchorMin;
        private Vector2 _choicesHomeAnchorMax;
        private Vector2 _choicesHomePivot;
        private Vector2 _choicesHomePosition;

        private void ShowStage(bool staged)
        {
            if (dialogueStage != null) dialogueStage.gameObject.SetActive(staged);
            if (artFrame != null) artFrame.SetActive(!staged);
            if (textColumn != null) textColumn.SetActive(!staged);

            if (choicesPanel == null) return;

            if (!_choicesHomeKnown)
            {
                _choicesHomeKnown = true;
                _choicesHomeAnchorMin = choicesPanel.anchorMin;
                _choicesHomeAnchorMax = choicesPanel.anchorMax;
                _choicesHomePivot = choicesPanel.pivot;
                _choicesHomePosition = choicesPanel.anchoredPosition;
            }

            if (!staged)
            {
                choicesPanel.anchorMin = _choicesHomeAnchorMin;
                choicesPanel.anchorMax = _choicesHomeAnchorMax;
                choicesPanel.pivot = _choicesHomePivot;
                choicesPanel.anchoredPosition = _choicesHomePosition;
            }
        }

        // The page's FIRST line, statically (D2). Choices show at once; D3
        // gates them behind the last line. The result text of the choice
        // that led here is not shown on a staged page yet -- contract 3's
        // result-then-lines ordering is D3's.
        private void PaintStage(EventView view)
        {
            var line = view.Lines[0];
            bool narration = line.IsNarration;
            var side = line.Side;

            PaintBackdrop(view.BackdropKey);

            // Contract 15: no set piece hides layer 2 (Apply disables the
            // Image on a miss, so it is never a white quad).
            ItemIcons.Apply(stageSetPiece, eventArt, view.ArtKey);

            stageTitle.Set(UiStrings.EventTitle, view.Title);

            PaintBust(narration ? null : line, side);
            if (stageBusts != null && stageBusts.Length > 1 && stageBusts[1] != null)
            {
                stageBusts[1].gameObject.SetActive(false);
            }

            Pin(stageBox, DialogueStageLayout.BoxPin(narration, side), 0f, DialogueStageLayout.BoxBottom);

            // Narration has no plate (contract 8). A speaker with no bust
            // still gets one (contract 15).
            stageNamePlate.gameObject.SetActive(!narration);
            if (!narration)
            {
                Pin(stageNamePlate, DialogueStageLayout.PlatePin(side), 0.5f, DialogueStageLayout.PlateCentreY);
                stageName.SetContent(line.SpeakerName);

                bool hasEpithet = !string.IsNullOrEmpty(line.Epithet);
                stageEpithet.gameObject.SetActive(hasEpithet);
                if (hasEpithet) stageEpithet.Set(UiStrings.EventEpithet, line.Epithet);

                var namePosition = stageName.rectTransform.anchoredPosition;
                namePosition.y = hasEpithet ? EventScreen.NameWithEpithetY : 0f;
                stageName.rectTransform.anchoredPosition = namePosition;
            }

            // TMP's synthetic italic: SourceSans3 ships Regular/SemiBold/Bold
            // only. A real SourceSans3-Italic is pending the owner's OK to
            // download. Authored <i> inside a line uses the same path.
            stageLineText.fontStyle = narration ? FontStyles.Italic : FontStyles.Normal;
            stageLineText.Set(UiStrings.EventLine, line.Text);

            if (choicesPanel != null)
            {
                Pin(choicesPanel, DialogueStageLayout.ChoicesPin(narration, side), 0f, DialogueStageLayout.ChoicesBottom);
            }
        }

        // Cover-crop (contract 7): the sprite's own aspect, scaled until it
        // fills the stage at the stage's aspect, centred; the stage's
        // RectMask2D crops the overhang. No sprite hides the Image and leaves
        // the solid ground under it.
        private void PaintBackdrop(string backdropKey)
        {
            if (!ItemIcons.Apply(stageBackdrop, backdropArt, backdropKey)) return;

            var sprite = stageBackdrop.sprite;
            var canvas = dialogueStage.rect.size;
            var cover = DialogueStageLayout.CoverSize(
                new UiVec(sprite.rect.width, sprite.rect.height), new UiVec(canvas.x, canvas.y));

            var rect = stageBackdrop.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(cover.X, cover.Y);
        }

        // Requested expression, then neutral, then no bust (contract 11);
        // the plate and text still show without one (contract 15).
        private void PaintBust(EventLineView line, DialogueSide side)
        {
            if (stageBusts == null || stageBusts.Length == 0 || stageBusts[0] == null) return;
            var bust = stageBusts[0];

            Sprite sprite = null;
            if (line != null)
            {
                string path = DialogueBust.FirstAvailable(line.BustFolder, line.Expression,
                    candidate => (sprite = Resources.Load<Sprite>(candidate)) != null);
                if (string.IsNullOrEmpty(path)) sprite = null;
            }

            bust.gameObject.SetActive(sprite != null);
            if (sprite == null) return;

            bust.sprite = sprite;
            bust.preserveAspect = true;

            var size = DialogueStageLayout.BustSize(new UiVec(sprite.rect.width, sprite.rect.height));
            var rect = bust.rectTransform;
            rect.sizeDelta = new Vector2(size.X, size.Y);
            Pin(rect, DialogueStageLayout.BustPin(side, size.X), 0f, 0f);

            // Painted facing right (contract 9): mirrored on the right so the
            // speaker faces inward. The pin's centre pivot keeps it in place.
            rect.localScale = new Vector3(DialogueStageLayout.BustMirrorX(side), 1f, 1f);
        }

        // A bottom-edge pin: x from DialogueStageLayout, y measured up from
        // the stage's bottom edge to the given pivot height.
        private static void Pin(RectTransform rect, StagePin pin, float pivotY, float y)
        {
            if (rect == null) return;
            rect.anchorMin = rect.anchorMax = new Vector2(pin.AnchorX, 0f);
            rect.pivot = new Vector2(pin.PivotX, pivotY);
            rect.anchoredPosition = new Vector2(pin.OffsetX, y);
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

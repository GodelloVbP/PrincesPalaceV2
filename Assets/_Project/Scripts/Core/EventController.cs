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
    //
    // A STAGED PAGE PLAYS (docs/PLAN_DIALOGUE_STAGE.md, D3). DialoguePlayback
    // owns the state machine; this class feeds it presses (Submit through
    // INavSubmitClaim, a left press on the stage through PointerPressRelay),
    // unscaled time, and the system menu's open/closed, and paints what it
    // answers. The rows stay hidden until it reaches Choices.
    public class EventController : MonoBehaviour, INavSubmitClaim
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

        // ---- the dialogue stage (docs/PLAN_DIALOGUE_STAGE.md, D2/D3) -------------
        //
        // Shown instead of the legacy frame and text column when the page has
        // lines, or when the event concluded and has lines on any page
        // (EventView.Staged); every other page paints exactly as before
        // (contract 14), with no playback at all.

        // The legacy layout's two halves, switched off under the stage.
        [SerializeField] internal GameObject artFrame;
        [SerializeField] internal GameObject textColumn;

        // The rows' one panel, which both presentations share and the stage
        // moves above its box.
        [SerializeField] internal RectTransform choicesPanel;

        [SerializeField] internal RectTransform dialogueStage;
        [SerializeField] internal Image stageBackdrop;
        [SerializeField] internal Image stageSetPiece;

        // Two, for the speaker slide: the outgoing speaker leaves on one
        // while the incoming one arrives on the other.
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

        // The page those rows were painted from ("" when concluded). A pick
        // names it, so a press that lands after the run has moved on is
        // refused as StalePage rather than read against the new page.
        private string _paintedPageId = "";

        // The frame an accepted choose last ran on. A Submit and a mouse
        // click landing on the same frame both reach Press before either
        // repaint runs, so the second one carries the page the first press
        // just painted -- StalePage can't catch that, it's the current page.
        private int _lastChooseFrame = -1;

        // The concluded state has no page, so CurrentEvent() gives it no
        // title or art. The panel keeps showing the page the player was on
        // rather than blanking the header under the result they are reading.
        private string _heldEventId;
        private string _heldTitle;
        private string _heldArtKey;

        // The page's backdrop, held the same way, so a concluded stage keeps
        // the ground the player chose on. Whether it concludes on the stage
        // at all is not held: EventView.Staged answers it from the event, so
        // a resume from a save, which holds nothing, paints the same (D4).
        private string _heldBackdropKey;

        // The playing page, or null on a legacy page (contract 14).
        private DialoguePlayback _playback;

        // Which beat the stage last painted, so a frame repaints only when
        // the playback moved on.
        private int _paintedBeat = -1;

        // Which of stageBusts holds the current speaker; the other is the
        // outgoing one during a slide.
        private int _activeBust;
        private readonly float[] _bustRestX = new float[2];
        private readonly DialogueSide[] _bustSide = new DialogueSide[2];

        private bool _rowsShown = true;

        // How many rows the last PaintRows showed, so the stage can stand
        // exactly that many on its box (DialogueStageLayout.ChoicesPanelBottom).
        private int _paintedRowCount;

        // TMP draws every character up to this; set once a line is full, so
        // a count that disagrees with TMP's by a character never clips it.
        private const int AllCharacters = 99999;

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

            WireStageClick();
        }

        // THE LEFT PRESS THAT ADVANCES A LINE (contract 17). Added at runtime
        // on the stage root, ListScroll's bargain: a transparent Image
        // raycasts against the stage's whole rect, and every piece inside the
        // stage is decor with raycastTarget cleared, so a press anywhere on
        // the stage lands here. The rows are the stage's later sibling and
        // draw over it, so once they show they take their own clicks first.
        private void WireStageClick()
        {
            if (dialogueStage == null) return;

            var go = dialogueStage.gameObject;
            // Explicit null checks, not `??`: in the Editor a missing
            // component comes back as Unity's fake null, which `??` keeps.
            var catcher = go.GetComponent<Image>();
            if (catcher == null) catcher = go.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            catcher.raycastTarget = true;

            var relay = go.GetComponent<PointerPressRelay>();
            if (relay == null) relay = go.AddComponent<PointerPressRelay>();
            relay.Pressed = AdvanceFromStage;
        }

        private void AdvanceFromStage()
        {
            if (_playback == null || _playback.State == DialogueState.Choices) return;
            _playback.Advance(Time.frameCount);
            ApplyPlayback();
        }

        // SUBMIT/A/ENTER WHILE LINES PLAY (INavSubmitClaim). Claimed in every
        // state but Choices -- including Entering and Transitioning, where the
        // playback ignores it, so the press is spent rather than left to reach
        // anything else. In Choices, and on a legacy page, Submit goes to the
        // selected row as it always has.
        bool INavSubmitClaim.ClaimSubmit()
        {
            if (_playback == null || !isActiveAndEnabled) return false;
            if (_playback.State == DialogueState.Choices || _playback.State == DialogueState.Closed) return false;

            _playback.Advance(Time.frameCount);
            ApplyPlayback();
            return true;
        }

        // Unscaled, as the UI's own animations are (ColumnOpenAnimator, the
        // reckoning's sweeps): the system menu stops Time.timeScale, and the
        // battle speed preset is the fight's own multiplier, not this room's.
        // The menu suspends the playback explicitly, read off its IsOpen so
        // every way it opens and closes is covered, not only Start.
        private void Update()
        {
            if (_playback == null) return;

            if (systemMenu != null && systemMenu.IsOpen) _playback.Suspend();
            else _playback.Resume();

            _playback.Tick(Time.unscaledDeltaTime);
            ApplyPlayback();
        }

        // ---- painting -----------------------------------------------------------

        private void Paint()
        {
            // A FIGHT THIS EVENT STARTED goes before any page is painted: the
            // pick that started it, or a reload that finds it still pending
            // (the map reopens the event, and the event hands on to the
            // fight). The Fight screen builds it through
            // RunOrchestrator.CurrentEncounterRequest; leaving the fight goes
            // back to the Map, which reopens this panel on the fight's result.
            if (RunOrchestrator.EventFightPending)
            {
                LaunchFight();
                return;
            }

            var view = RunOrchestrator.CurrentEvent();
            if (view == null)
            {
                // Nothing open under the party: the room was closed from
                // somewhere else (the debug menu, a reconcile). Showing a
                // stale page would offer choices the orchestrator refuses.
                Close();
                return;
            }

            _paintedPageId = view.PageId;

            if (!view.Concluded)
            {
                _heldEventId = view.EventId;
                _heldTitle = view.Title;
                _heldArtKey = view.ArtKey;
                _heldBackdropKey = view.BackdropKey;
            }

            bool held = view.Concluded && _heldEventId == view.EventId;
            string title = view.Concluded ? (held ? _heldTitle : "") : view.Title;
            string artKey = view.Concluded ? (held ? _heldArtKey : "") : view.ArtKey;

            // Contract 14: a page without lines never touches the stage, and
            // the rows go back to their legacy spot in case the page before
            // this one had lines. Contract 3: a concluded event shows its
            // result on the stage when the event has lines anywhere (D4),
            // whether it concluded just now or was resumed from a save.
            bool staged = view.Staged;
            ShowStage(staged);

            if (staged)
            {
                StartStage(view, title, artKey, held ? _heldBackdropKey : view.BackdropKey);
            }
            else
            {
                _playback = null;

                ItemIcons.Apply(artImage, eventArt, artKey);
                titleLabel.Set(UiStrings.EventTitle, title);

                // The result REPLACES the body (contract 11): it is what just
                // happened, and the page text above it is what the player
                // already read to choose.
                bodyLabel.Set(UiStrings.EventBody, string.IsNullOrEmpty(view.ResultText) ? view.Body : view.ResultText);
                effectsLabel.Set(UiStrings.EventEffects, view.EffectsLine);
            }

            PaintRows(view);

            // A legacy page shows its rows at once; a staged one hides them
            // until the playback reaches Choices.
            SetRowsShown(_playback == null || _playback.ChoicesShown, force: true);
            ApplyPlayback();
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

        // A new playback for this page: the result first when there is one,
        // then the page's lines (contract 3). Resume from a save lands here
        // through Open with the persisted result, so it replays the result
        // and then line 1 (contract 18). Nothing here calls ChooseEventOption:
        // replay is presentation only.
        private void StartStage(EventView view, string title, string artKey, string backdropKey)
        {
            PaintBackdrop(backdropKey);

            // Contract 15: no set piece hides layer 2 (Apply disables the
            // Image on a miss, so it is never a white quad).
            ItemIcons.Apply(stageSetPiece, eventArt, artKey);

            stageTitle.Set(UiStrings.EventTitle, title);

            _playback = new DialoguePlayback(DialogueBeat.Sequence(view.ResultText, view.EffectsLine, view.Lines));
            _paintedBeat = -1;
            _activeBust = 0;
            HideBust(0);
            HideBust(1);
        }

        // THE PLAYBACK, DRAWN. Called every frame from Update and straight
        // after each press, so the rows a press opens are up (and on the
        // rail) before the dispatcher settles that same frame's selection.
        private void ApplyPlayback()
        {
            if (_playback == null) return;

            if (_playback.BeatIndex != _paintedBeat)
            {
                _paintedBeat = _playback.BeatIndex;
                var beat = _playback.Current;
                if (beat != null) PaintBeat(beat, _playback.Cue);
            }

            PlaceBusts();

            if (stageLineText != null)
            {
                var current = _playback.Current;
                int visible = _playback.VisibleCharacters;
                stageLineText.maxVisibleCharacters =
                    current != null && visible >= current.VisibleLength ? AllCharacters : visible;
            }

            SetRowsShown(_playback.ChoicesShown, force: false);
        }

        // One beat's box, plate, text and bust, laid out for its side.
        private void PaintBeat(DialogueBeat beat, BustCue cue)
        {
            var line = beat.Line;
            bool narration = line.IsNarration;
            var side = line.Side;

            switch (cue)
            {
                case BustCue.Hidden:
                    HideBust(0);
                    HideBust(1);
                    break;
                case BustCue.SlideIn:
                    PaintBust(_activeBust, line);
                    HideBust(1 - _activeBust);
                    break;
                case BustCue.SwapInPlace:
                    PaintBust(_activeBust, line);
                    break;
                case BustCue.SlideOutIn:
                    // The bust on screen becomes the outgoing one, as painted.
                    _activeBust = 1 - _activeBust;
                    PaintBust(_activeBust, line);
                    break;
            }

            // Read after the cue painted it: a speaker whose bust failed every
            // fallback has none showing, and is framed centred.
            var activeBust = BustAt(_activeBust);
            bool hasBust = activeBust != null && activeBust.gameObject.activeSelf;
            var framing = DialogueStageLayout.FramingFor(narration, hasBust, side);

            Pin(stageBox, DialogueStageLayout.BoxPin(framing), 0f, DialogueStageLayout.BoxBottom);

            // Narration has no plate (contract 8). A speaker with no bust
            // still gets one (contract 15), at the centred box's left end.
            stageNamePlate.gameObject.SetActive(!narration);
            if (!narration)
            {
                Pin(stageNamePlate, DialogueStageLayout.PlatePin(framing), 0.5f, DialogueStageLayout.PlateCentreY);
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
            //
            // The result beat slants its text by tag instead, so the effects
            // line under it stays upright and gold, as the legacy layout's
            // effects label is.
            if (beat.IsResult)
            {
                stageLineText.fontStyle = FontStyles.Normal;
                string text = string.IsNullOrEmpty(line.Text) ? "" : "<i>" + line.Text + "</i>";
                if (beat.Effects.Length > 0)
                {
                    if (text.Length > 0) text += "\n";
                    text += "<color=" + FightHudPalette.GoldText + ">" + beat.Effects + "</color>";
                }

                stageLineText.Set(UiStrings.EventLine, text);
            }
            else
            {
                stageLineText.fontStyle = narration ? FontStyles.Italic : FontStyles.Normal;
                stageLineText.Set(UiStrings.EventLine, line.Text);
            }

            // Pinned by the beat on screen, so when the last beat opens the
            // rows they stand clear of its plate, and by the rows PaintRows
            // showed, so the last of them sits just above the box.
            if (choicesPanel != null)
            {
                var panel = choicesPanel.rect.size;
                float stageWidth = dialogueStage != null ? dialogueStage.rect.width : panel.x;
                Pin(choicesPanel, DialogueStageLayout.ChoicesPin(framing, stageWidth, panel.x), 0f,
                    DialogueStageLayout.ChoicesPanelBottom(panel.y, EventScreen.ChoicesHeightFor(_paintedRowCount)));
            }
        }

        // Each bust at its rest pin, pushed toward its OWN screen edge by one
        // bust width while sliding: the outgoing one leaves, the incoming one
        // arrives. Eased so neither starts or stops dead.
        private void PlaceBusts()
        {
            if (stageBusts == null) return;

            float progress = _playback.SlideProgress;
            float t = Mathf.SmoothStep(0f, 1f, progress);
            var cue = _playback.Cue;

            OffsetBust(_activeBust, cue == BustCue.SlideIn || cue == BustCue.SlideOutIn ? 1f - t : 0f);

            int outgoing = 1 - _activeBust;
            if (_playback.Outgoing != null && progress < 1f) OffsetBust(outgoing, t);
            else if (cue != BustCue.Hidden) HideBust(outgoing);
        }

        private void OffsetBust(int index, float fraction)
        {
            var bust = BustAt(index);
            if (bust == null || !bust.gameObject.activeSelf) return;

            var rect = bust.rectTransform;
            float away = _bustSide[index] == DialogueSide.Left ? -1f : 1f;
            rect.anchoredPosition = new Vector2(_bustRestX[index] + away * rect.sizeDelta.x * fraction, 0f);
        }

        private Image BustAt(int index) =>
            stageBusts != null && index >= 0 && index < stageBusts.Length ? stageBusts[index] : null;

        private void HideBust(int index)
        {
            var bust = BustAt(index);
            if (bust != null) bust.gameObject.SetActive(false);
        }

        // Close mid-slide (Transition table): the current bust to its rest
        // pin and the outgoing one gone, so nothing is left half-slid.
        private void SnapBusts()
        {
            OffsetBust(_activeBust, 0f);
            HideBust(1 - _activeBust);
        }

        // The rows appear only when the playback reaches Choices (contract
        // 17). Hidden as a whole panel, so while lines play no row is active,
        // selectable or on the rail, and the D-pad has nothing to move to.
        private void SetRowsShown(bool shown, bool force)
        {
            if (!force && shown == _rowsShown) return;
            _rowsShown = shown;

            if (choicesPanel != null) choicesPanel.gameObject.SetActive(shown);
            RefreshNavigation();
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
        private void PaintBust(int index, EventLineView line)
        {
            var bust = BustAt(index);
            if (bust == null) return;

            Sprite sprite = null;
            if (line != null)
            {
                string path = DialogueBust.FirstAvailable(line.BustFolder, line.Expression,
                    candidate => (sprite = Resources.Load<Sprite>(candidate)) != null);
                if (string.IsNullOrEmpty(path)) sprite = null;
            }

            bust.gameObject.SetActive(sprite != null);
            if (sprite == null) return;

            var side = line.Side;
            bust.sprite = sprite;
            bust.preserveAspect = true;

            var size = DialogueStageLayout.BustSize(new UiVec(sprite.rect.width, sprite.rect.height));
            var rect = bust.rectTransform;
            rect.sizeDelta = new Vector2(size.X, size.Y);
            var pin = DialogueStageLayout.BustPin(side, size.X);
            Pin(rect, pin, 0f, 0f);
            _bustRestX[index] = pin.OffsetX;
            _bustSide[index] = side;

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

            _paintedRowCount = row;

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

            // Alone, the label is centred in the row; over a reason, the two
            // are centred as a pair (EventScreen.ChoiceTextY).
            var label = choiceTexts[row].rectTransform;
            label.anchoredPosition = new Vector2(label.anchoredPosition.x, EventScreen.ChoiceTextY(showLock));
        }

        // ---- acting ---------------------------------------------------------------

        private void Press(int row)
        {
            if (row < 0 || row >= choiceButtons.Length) return;

            // A locked row is interactable=false and off the rail, so no
            // input reaches here for it; checked anyway because onClick.Invoke
            // does not ask the Button first.
            if (!choiceButtons[row].interactable || !choiceButtons[row].gameObject.activeInHierarchy) return;

            // A staged page's rows take only a press that started after they
            // appeared (contract 17), never the one that finished the last
            // line. Belt and braces: the rows are inactive until then, so no
            // pointer press can start on one, and the Submit claim clears the
            // selection on the frame it opens them.
            if (_playback != null && !_playback.AcceptsChoicePress(Time.frameCount)) return;

            // At most one choose per frame -- a same-frame Submit + click
            // double-press would otherwise both reach here before Paint()
            // repaints, so the second carries the page the first just made current.
            if (_lastChooseFrame == Time.frameCount) return;

            if (_rowAction[row] == LeaveRow)
            {
                RunOrchestrator.LeaveEvent();
                Close();
                return;
            }

            _lastChooseFrame = Time.frameCount;
            var result = RunOrchestrator.ChooseEventOption(_rowAction[row], _paintedPageId);
            if (result.Closed)
            {
                Close();
                return;
            }

            // Applied or refused, the page to show is whatever the run now
            // says -- a refusal leaves it unchanged, which repaints the same;
            // a StalePage refusal repaints the page the run is really on.
            Paint();
        }

        // Not Close(): the event is still open, and Finished would repaint a
        // map that is about to be unloaded. The scene change takes the panel
        // with it.
        private void LaunchFight()
        {
            if (_playback != null)
            {
                _playback.Close();
                SnapBusts();
            }

            Navigation.Go(Navigation.Fight);
        }

        private void Close()
        {
            if (_playback != null)
            {
                _playback.Close();
                SnapBusts();
            }

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
                    systemMenu: HandleSystemMenu, submitClaim: () => this);
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

                if (_rowsShown && button.gameObject.activeSelf && button.interactable)
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

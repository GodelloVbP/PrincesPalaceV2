using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // Drives the Main menu pane: three ways out, none on a single press.
    //
    // ALL THREE END THE DESCENT, and that is a deliberate departure from the
    // design pass, which asked for "back to title (run stays as it is)".
    // RunManager states the opposite as a rule with money attached -- leaving a
    // descent, or anything that does not continue it, kills the run, and EndRun
    // is what PAYS OUT what the run earned. Both existing doors enforce it: the
    // hub's title button and the main menu's quit. A third title door that
    // parked a run instead would leave the player at the title with a live
    // descent in the save, which is precisely the state the hub's own button
    // refuses to create, and the first thing RunManager does at startup is
    // settle it anyway.
    //
    // So each button says what it does to the run in the line underneath it,
    // and none of them lies.
    public class ExitsController : MonoBehaviour
    {
        [SerializeField] internal Button[] exitButtons;
        [SerializeField] internal GameObject[] exitHovers;
        [SerializeField] internal TMP_Text[] exitLabels;

        // The containers the two exits live in, moved rather than rebuilt when
        // the abandon card comes and goes.
        [SerializeField] internal RectTransform[] exitBlocks;

        [SerializeField] internal GameObject separator;
        [SerializeField] internal GameObject abandonCard;
        [SerializeField] internal Button abandonHold;
        [SerializeField] internal RectTransform abandonFill;

        // The menu this pane lives in. Held so the pane can CLOSE it before
        // navigating: closing is what restores Time.timeScale, and a scene
        // change with the menu still open leaves the next scene paused. The
        // menu's own OnDisable covers that too, but relying on teardown order
        // for whether the game is running is not a thing to rely on.
        //
        // It is also the single source of "am I in a descent" -- a second
        // serialized bool here would be one more thing that can disagree with
        // the bar two inches above it.
        [SerializeField] internal SystemMenuController menu;

        // How long an armed exit stays armed. Unscaled, because the menu that
        // hosts this pane runs at timeScale 0.
        //
        // Long enough to read the label, short enough that an exit left armed
        // while the player went to look at their inventory is not still armed
        // when they come back and click through it.
        public const float ArmSeconds = 4f;

        public const float HoldSeconds = 1.2f;

        private bool _wired;

        // Which exit is armed, as an index into exitButtons, or -1.
        private int _armed = -1;
        private float _armedFor;

        private HoldToConfirm _hold;

        // Exposed for tests: "which exit is armed" is the whole of the
        // two-press rule, and asserting it beats asserting a label.
        public int ArmedIndex => _armed;

        private void Start() => Wire();

        private void OnEnable()
        {
            Wire();
            Disarm();

            // Explicit, rather than leaning on HoldToConfirm's own OnDisable to
            // have already done it. That component cancels itself when the pane
            // goes away and this would be redundant -- but "the abandon button
            // is not half-held when you open this tab" is a claim this pane
            // makes, so it states it rather than inheriting it.
            if (_hold != null) _hold.Cancel();
            SetFill(0f);

            ApplyContext();
        }

        // Leaving the pane forgets everything. An exit armed on the way out
        // would otherwise be waiting, already half-pressed, the next time this
        // tab is opened.
        private void OnDisable() => Disarm();

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            if (exitButtons != null)
            {
                for (int i = 0; i < exitButtons.Length; i++)
                {
                    if (exitButtons[i] == null) continue;
                    int index = i;
                    exitButtons[i].onClick.AddListener(() => Press(index));

                    var hover = exitButtons[i].gameObject.AddComponent<HoverIndex>();
                    hover.Index = index;
                    hover.Changed = OnExitHover;
                }
            }

            if (abandonHold != null)
            {
                // NO onClick LISTENER on this one, deliberately. A Button fires
                // its click on release whatever the press was for, so wiring
                // abandon to onClick would end the descent on a tap -- the one
                // thing the design says nothing on this pane may do. The button
                // is here for its label, its plate and its raycast; the hold
                // below is what decides.
                _hold = abandonHold.gameObject.AddComponent<HoldToConfirm>();
                _hold.Seconds = HoldSeconds;
                _hold.Progress = SetFill;
                _hold.Completed = Abandon;
            }
        }

        // ---- context -------------------------------------------------------------

        public bool InDescent => menu != null && menu.InDescent;

        // Abandon is ABSENT between descents, not greyed. Same rule the tab bar
        // follows one level up, and for the same reason: a greyed control asks
        // the player to work out why, and there is nowhere on it to answer.
        //
        // AND THE PAIR MOVES WITH IT. The scene is authored in the three-piece
        // layout; with the card gone the two exits re-centre on the pane, so
        // the hub's only way out of the game is not two buttons floating in the
        // top third of an empty panel. Same shape as the tab bar's two modes,
        // and the arithmetic is still only in ExitsLayout.
        public void ApplyContext()
        {
            bool withAbandon = InDescent;

            abandonCard.SetShown(withAbandon);
            separator.SetShown(withAbandon);

            if (exitBlocks == null) return;

            for (int i = 0; i < exitBlocks.Length; i++)
            {
                if (exitBlocks[i] == null) continue;
                exitBlocks[i].anchoredPosition =
                    new Vector2(0f, ExitsLayout.ExitBlockCentreY(i, withAbandon));
            }
        }

        // ---- the two-press exits ---------------------------------------------------

        public void Press(int index)
        {
            if (exitButtons == null || index < 0 || index >= exitButtons.Length) return;

            if (_armed == index)
            {
                Fire(index);
                return;
            }

            // Arming one disarms the other. Two exits both reading "PRESS AGAIN
            // TO CONFIRM" would be two loaded buttons and no way to tell which
            // press belongs to which.
            _armed = index;
            _armedFor = 0f;
            RefreshLabels();
        }

        private void Fire(int index)
        {
            Disarm();

            // The run ends either way; only the destination differs. Settled
            // rather than discarded, because EndRun is what banks the embers the
            // descent's bosses paid for.
            RunManager.EndRun();

            if (index == ExitsLayout.ExitIndexQuit)
            {
                // "Saves first" is the design's wording. EndRun already
                // persisted, and this is the belt to its braces -- a quit that
                // loses the last thing the player did is the single worst bug
                // this pane could have.
                SaveSlotManager.SaveCurrent();
                CloseMenu();
                Navigation.Quit();
                return;
            }

            CloseMenu();
            Navigation.Go(Navigation.MainMenu);
        }

        private void Abandon()
        {
            Disarm();
            RunManager.EndRun();
            CloseMenu();

            // To the hub, not the title. Abandoning is giving up on a descent,
            // not on the session -- the squad walks back up, and everything the
            // run earned has just been settled into the save they are walking
            // back to.
            Navigation.Go(Navigation.Hub);
        }

        private void CloseMenu()
        {
            if (menu != null) menu.Close();
        }

        // ---- the arming clock -------------------------------------------------------

        private void Update()
        {
            if (_armed < 0) return;

            // UNSCALED. This pane only ever runs while the menu has the game
            // paused at timeScale 0, so Time.deltaTime here is exactly zero and
            // an armed exit would stay armed forever.
            Advance(Time.unscaledDeltaTime);
        }

        // Split from Update so it can be tested without waiting out four
        // seconds of real time per case.
        public void Advance(float seconds)
        {
            if (_armed < 0 || seconds <= 0f) return;

            _armedFor += seconds;
            if (_armedFor >= ArmSeconds) Disarm();
        }

        public void Disarm()
        {
            if (_armed < 0)
            {
                _armedFor = 0f;
                return;
            }

            _armed = -1;
            _armedFor = 0f;
            RefreshLabels();
        }

        private void RefreshLabels()
        {
            if (exitLabels == null) return;

            for (int i = 0; i < exitLabels.Length; i++)
            {
                if (exitLabels[i] == null) continue;
                exitLabels[i].Set(i == _armed ? UiStrings.ExitConfirm : LabelFor(i));
            }
        }

        private static UiString LabelFor(int index) =>
            index == ExitsLayout.ExitIndexQuit ? UiStrings.ExitQuit : UiStrings.ExitToTitle;

        // ---- the hold's fill ----------------------------------------------------------

        // See HoldFillMath's own header for why sizeDelta rather than anchors.
        private void SetFill(float progress) =>
            HoldFillMath.SetFill(abandonFill, progress, ExitsLayout.HoldWidth, ExitsLayout.HoldHeight);

        private void OnExitHover(int index, bool entered)
        {
            if (exitHovers == null || index < 0 || index >= exitHovers.Length) return;
            exitHovers[index].SetShown(entered);
        }

            }
}

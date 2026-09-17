using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Content;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // The defeat screen, driven.
    //
    // Reads a RunSettlement.Result and nothing else. That is not a style
    // preference: by the time this draws, RunManager.EndRun has already
    // discarded the snapshot, so the run it is describing does not exist any
    // more. The settlement is the only surviving record of it.
    public class DefeatController : MonoBehaviour
    {
        [SerializeField] internal RectTransform frame;

        [SerializeField] internal TMP_Text goldLostLabel;
        [SerializeField] internal TMP_Text embersLabel;
        [SerializeField] internal TMP_Text depthLabel;
        [SerializeField] internal TMP_Text expLabel;

        [SerializeField] internal GameObject[] rowGroups;
        [SerializeField] internal TMP_Text[] rowNames;
        [SerializeField] internal TMP_Text[] rowStats;

        [SerializeField] internal Button returnButton;
        [SerializeField] internal Button inspectButton;

        private const float ExpandSeconds = 0.34f;
        private const float ExpandFrom = 0.86f;

        private RunSettlement.Result _settlement;
        private bool _wired;
        private Coroutine _animation;

        // Pushed once per Show() (docs/GAMEPAD_NAVIGATION_PLAN.md phase 3b),
        // popped on OnDisable rather than on either button's own handler:
        // both Dismissed and InspectRequested end in a scene change
        // (LeaveFight / Navigation.Go(Hub)), and OnDisable is what actually
        // fires when that change tears this screen down -- a pop hung on
        // the click instead would double-pop if something else also
        // deactivated this GameObject first.
        private NavContext _navContext;

        // Raised when the player leaves. An event rather than a Navigation call
        // for the same reason FightController.FightEnded is one: the screen has
        // no business knowing what comes after a run.
        public System.Action Dismissed;

        // Raised by the Characters button. Separate from Dismissed because it
        // is a different intention -- look at what survived, not leave.
        public System.Action InspectRequested;

        private void Start()
        {
            Wire();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            returnButton.onClick.AddListener(() => Dismissed?.Invoke());
            inspectButton.onClick.AddListener(() => InspectRequested?.Invoke());
        }

        public void Show(RunSettlement.Result settlement)
        {
            _settlement = settlement;

            gameObject.SetActive(true);
            Wire();
            Paint();
            PushNavContext();

            if (_animation != null) StopCoroutine(_animation);

            if (isActiveAndEnabled) _animation = StartCoroutine(PlayIn());
            else if (frame != null) frame.localScale = Vector3.one;
        }

        // Inspect then Return, left to right (DefeatScreen's own Place.At
        // layout) -- a Rail, wrap by the owner default, entry on the first.
        // Guarded so a second Show() (there is no known caller today, but
        // nothing stops one) reconfigures rather than double-pushing.
        private void PushNavContext()
        {
            var members = new List<Button>();
            if (inspectButton != null) members.Add(inspectButton);
            if (returnButton != null) members.Add(returnButton);

            RuntimeNavWiring.Apply(RuntimeNavWiring.Group("defeatButtons", UiNavGroupKind.Rail, members));

            object entry = members.Count > 0 ? members[0].gameObject : null;
            var selectables = new Dictionary<string, object>();
            foreach (var member in members) selectables[member.name] = member.gameObject;

            if (_navContext != null)
            {
                _navContext.Reconfigure(entry, selectables);
                return;
            }

            // Cancel does what Return does -- there is no back-out affordance
            // on the mouse path either, only leave or inspect, so Cancel
            // spends itself on the same "leave" action Return's own click
            // handler raises.
            _navContext = new NavContext(entry, selectables, cancel: () => Dismissed?.Invoke());
            NavigationInputModule.Contexts?.Push(_navContext);
        }

        // Never OnEnable: Show() is the one place this context is pushed
        // (it needs Wire()'s buttons to already exist), and OnDisable is
        // what actually fires when either button's own handler tears this
        // screen down (LeaveFight / Navigation.Go(Hub) both end in a scene
        // change) -- see PushNavContext's own header.
        private void OnDisable()
        {
            if (_navContext == null) return;

            NavigationInputModule.Contexts?.Remove(_navContext);
            _navContext = null;
        }

        private void Paint()
        {
            var settlement = _settlement ?? new RunSettlement.Result();

            goldLostLabel.Set(UiStrings.DefeatGoldLost, settlement.GoldLost);
            expLabel.Set(UiStrings.DefeatExp, settlement.ExpEarned);
            depthLabel.Set(UiStrings.DefeatDepth, settlement.DeepestStep, settlement.RoomsCleared);

            // "No new bosses fell" rather than "+0 EMBERS". A zero next to a
            // currency reads as the payout being broken; a sentence explains
            // why there was nothing to pay -- and tells the player, without a
            // tutorial, what the ember source actually is.
            if (settlement.EmbersEarned > 0) embersLabel.Set(UiStrings.DefeatEmbers, settlement.EmbersEarned);
            else embersLabel.Set(UiStrings.DefeatEmbersNone);

            PaintRows(settlement);
        }

        private void PaintRows(RunSettlement.Result settlement)
        {
            // The party, in squad-slot order, so the rows match every other
            // screen that lists them. The ledger is keyed by id and its own
            // order is first-seen, which is combat order and not the player's.
            var squad = SaveSlotManager.CurrentSave?.ActiveSquad() ?? new List<Character>();

            for (int i = 0; i < rowGroups.Length; i++)
            {
                bool present = i < squad.Count;

                rowGroups[i].SetActive(present);
                if (!present) continue;

                var character = squad[i];
                var line = settlement.Ledger?.FirstOrDefault(e => e.characterId == character.definitionId);

                rowNames[i].SetContent(DisplayNameFor(character.definitionId));

                // A character who was benched all run gets a row of zeroes
                // rather than no row. "You did nothing" is a true and useful
                // statement; a missing row is a bug the player has to guess at.
                rowStats[i].Set(UiStrings.DefeatStatLine,
                    line?.TotalDealt ?? 0,
                    line?.physicalDealt ?? 0,
                    line?.otherDealt ?? 0,
                    line?.damageTaken ?? 0,
                    line?.healed ?? 0);
            }
        }

        private static string DisplayNameFor(string definitionId)
        {
            var definition = ContentDatabase.Characters.FirstOrDefault(c => c.id == definitionId);
            return definition == null ? definitionId : definition.Data.DisplayName;
        }

        private IEnumerator PlayIn()
        {
            frame.localScale = Vector3.one * ExpandFrom;

            for (float t = 0f; t < ExpandSeconds; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / ExpandSeconds);
                frame.localScale = Vector3.one * Mathf.Lerp(ExpandFrom, 1f, k * k * (3f - 2f * k));
                yield return null;
            }

            frame.localScale = Vector3.one;
            _animation = null;
        }
    }
}

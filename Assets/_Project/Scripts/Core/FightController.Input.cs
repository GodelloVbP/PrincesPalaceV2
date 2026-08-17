using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;

namespace PrincesPalace
{
    // Clicks in, session commands out.
    //
    // Every handler is the same three steps: refuse if it is not the player's
    // turn, move the menu, repaint. The refusal is checked HERE and nowhere
    // else, which is the fix for v1 asking the same question in two places and
    // getting two answers.
    public partial class FightController
    {
        // What a click is allowed to do at all. One question, one place.
        private bool CanAct => _session != null && !_session.IsOver && _session.IsPlayerTurn && !_isBusy;

        private void WireInput()
        {
            if (_wired) return;
            _wired = true;

            for (int i = 0; i < verbButtons.Length; i++)
            {
                int index = i;
                verbButtons[i].onClick.AddListener(() => OnVerbPressed(index));
            }

            for (int i = 0; i < submenuRows.Length; i++)
            {
                int index = i;
                submenuRows[i].onClick.AddListener(() => OnRowPressed(index));
                AddHover(submenuRows[i].gameObject, index);
            }

            for (int i = 0; i < enemyPlates.Length; i++)
            {
                int index = i;
                enemyPlates[i].onClick.AddListener(() => OnEnemyPressed(index));
            }

            submenuBackButton.onClick.AddListener(OnBackPressed);
            continueButton.onClick.AddListener(OnContinuePressed);
        }

        // Selection-by-hover is deliberate: it makes the detail column feel
        // instant, and confirming stays a separate click. Added in code rather
        // than as a serialized EventTrigger because a build-time delegate does
        // not serialise -- the same trap that made v1's slot refresh silently
        // not exist in the shipped scene.
        private void AddHover(GameObject row, int index)
        {
            var trigger = row.GetComponent<EventTrigger>() ?? row.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => OnRowHovered(index));
            trigger.triggers.Add(enter);
        }

        private void OnVerbPressed(int index)
        {
            if (!CanAct) return;

            switch (index)
            {
                case 0:
                    _menu.OpenAttack();
                    break;

                case 1:
                    _menu.OpenBranch(MenuBranch.Skill);
                    break;

                case 2:
                    // An empty satchel says so rather than opening a column with
                    // nothing in it: a submenu that can only be backed out of is
                    // worse than a refusal that explains itself.
                    if (_satchel == null || _satchel.Count == 0)
                    {
                        _session.AppendMessage("No items to use.");
                        break;
                    }
                    _menu.OpenBranch(MenuBranch.Item);
                    break;

                case 3:
                    OnRunPressed();
                    return;

                case 4:
                    _session.HoldBack();
                    AfterResolution();
                    return;
            }

            RefreshUi();
        }

        private void OnRowHovered(int index)
        {
            var rows = CurrentRows();
            int cost = index >= 0 && index < rows.Count ? rows[index].ManaCost : 0;

            // Only repaints when something actually changed -- hovering across
            // one row fires this repeatedly.
            if (_menu.Select(index, cost)) RefreshUi();
        }

        private void OnRowPressed(int index)
        {
            if (!CanAct) return;

            var rows = CurrentRows();
            if (index < 0 || index >= rows.Count || !rows[index].Affordable) return;

            _menu.Select(index, rows[index].ManaCost);

            // An item resolves on the actor immediately; a skill needs a mark.
            if (_menu.Branch == MenuBranch.Item)
            {
                UseSatchelItem(index);
                return;
            }

            _menu.EnterTargeting();
            RefreshUi();
        }

        private void OnEnemyPressed(int index)
        {
            if (!CanAct || !_menu.IsTargeting) return;

            var enemies = Enemies;
            if (index < 0 || index >= enemies.Count) return;

            var target = enemies[index];
            if (!target.IsAlive) return;

            // The front-rank rule is the SESSION's to state, not the view's --
            // it is a combat rule that happens to be enforced at a click.
            if (!_session.CanMeleeReach(target) && _menu.Branch == MenuBranch.Attack)
            {
                _session.AppendMessage($"{target.Name} is out of reach behind the front rank.");
                RefreshUi();
                return;
            }

            switch (_menu.Branch)
            {
                case MenuBranch.Attack:
                    _session.ExecuteAttack(target);
                    break;

                case MenuBranch.Skill:
                    var kit = _session.KitFor(_session.Current);
                    int index2 = _menu.Selection;
                    bool isBasic = kit == null || index2 >= kit.Skills.Count;

                    // The basic spell's row and an authored one go down two
                    // different session commands, and which is which is decided
                    // by ONE index rule shared with the row builder.
                    if (isBasic) _session.ExecuteSkill(target);
                    else _session.CastSkill(index2, target);
                    break;

                default:
                    return;
            }

            AfterResolution();
        }

        private void OnBackPressed()
        {
            if (_menu.Back()) RefreshUi();
        }

        private void OnRunPressed()
        {
            // Fleeing is the run's business, not the fight's -- the session only
            // ever answers whether it is allowed.
            _session?.AppendMessage("There is no way out of this one.");
            RefreshUi();
        }

        private void OnContinuePressed()
        {
            SetActive(continueButton.gameObject, false);
            LeaveFight();
        }

        // Leaving the fight for good. Called by the fight's own Continue on a
        // defeat, and by the Reckoning's on a victory -- ONE exit, so the two
        // paths cannot drift into disagreeing about where a fight goes next.
        //
        // Back to the descent if one is still live, and to the hub if it is not
        // -- which is what a defeat leaves behind, since losing ends the run
        // rather than the room.
        internal void LeaveFight()
        {
            Navigation.Go(RunManager.HasRun ? Navigation.Map : Navigation.Hub);
        }

        // Raised once, the moment the last beat of a finished fight has played.
        //
        // MOVED here from the Continue press. The Reckoning has to show what
        // the fight paid, and the payout does not exist until this fires --
        // RewardApplier runs inside the subscriber. Raising it on dismissal
        // meant the only screen that wants the numbers opened before they were
        // computed.
        //
        // An event rather than a direct call into a run manager: the fight
        // screen has no business knowing what comes after it.
        public System.Action<bool> FightEnded;

        // Guards against a second playback pass on an already-finished fight
        // raising it twice -- which would bank the same gold and award the same
        // experience again.
        private bool _endRaised;

        // What the Reckoning needs, supplied from outside.
        //
        // A FUNC rather than the reward itself, because the payout does not
        // exist until FightEnded has been handled and this is read immediately
        // after. Left null by anything that has no reward screen -- a headless
        // fixture, a preview -- in which case the plain Continue button stands
        // in, which is exactly the pre-Reckoning behaviour.
        internal System.Func<Domain.Rewards.CombatReward> RewardSource;

        // What the run that just ended cost and paid. Same Func reasoning as
        // RewardSource: the settlement does not exist until FightEnded has been
        // handled, and it is read immediately after.
        internal System.Func<RunSettlement.Result> SettlementSource;

        [SerializeField] internal ReckoningController reckoning;
        [SerializeField] internal DefeatController defeat;

        // True when the defeat screen took over. A run always ends on a loss,
        // so there is no "keep playing" branch here -- the only choices are
        // leave, or go and look at the roster that survived.
        private bool OpenDefeat()
        {
            if (defeat == null) return false;

            var settlement = SettlementSource?.Invoke();
            if (settlement == null) return false;

            defeat.Dismissed = LeaveFight;

            // Straight to the hub, where the character overlay lives. The
            // defeat screen cannot show it itself: the overlay is mounted in
            // the hub's tree, not the fight's.
            defeat.InspectRequested = () =>
            {
                Navigation.Go(Navigation.Hub);
            };

            defeat.Show(settlement);
            return true;
        }

        // True when the Reckoning took over. False means the caller should fall
        // back to the Continue button -- no controller wired, or no reward to
        // show, both of which are states a test can legitimately be in.
        private bool OpenReckoning()
        {
            if (reckoning == null) return false;

            var reward = RewardSource?.Invoke();
            if (reward == null) return false;

            reckoning.Dismissed = LeaveFight;
            reckoning.Show(reward, RollOffers());
            return true;
        }

        // The loot on offer, rolled against how deep the run is and what class
        // of thing was just killed.
        //
        // UnityEngine.Random rather than the session's seeded stream, and
        // deliberately: the offer is not part of the fight's simulation and
        // must not shift the beats a replay would produce. Domain takes the
        // randomness as a Func for exactly this reason.
        private System.Collections.Generic.List<Domain.Rewards.ItemOffer> RollOffers()
        {
            var encounter = _session != null && _session.IsEliteFight
                ? Domain.Rewards.EncounterClass.Elite
                : Domain.Rewards.EncounterClass.Normal;

            int depth = _session?.DepthStep ?? 0;
            // A lambda, not the method group: Random.Range is overloaded on
            // (int,int) and (float,float), and the int overload is the
            // upper-bound-EXCLUSIVE one both Domain tables expect.
            return ItemOfferRoll.Roll(encounter, depth, n => UnityEngine.Random.Range(0, n));
        }

        private void UseSatchelItem(int index)
        {
            // Consuming from the run's inventory is Core's, and the session is
            // handed an already-resolved effect. The satchel lives in RunState
            // and has no business being reachable from combat.
            var stack = _satchel[index];
            ItemUsed?.Invoke(stack.ItemId);
            AfterResolution();
        }

        public System.Action<string> ItemUsed;

        // Every path that resolves an action ends here: the menu closes, the
        // recorded beats are handed to playback, and the screen repaints.
        private void AfterResolution()
        {
            _menu.Reset();

            foreach (var line in _session.DrainImmediateMessages()) PushLogLine(line);

            var beats = _session.DrainBeats();

            if (beatPlayer != null && beats.Count > 0)
            {
                _isBusy = true;
                beatPlayer.Play(beats, OnPlaybackFinished);
            }
            else
            {
                // No player attached (a headless test, a preview): the beats
                // still have to be drained and their lines still have to show,
                // or the log would silently lose a whole round.
                foreach (var beat in beats)
                {
                    foreach (var line in beat.Messages) PushLogLine(line);
                }
                OnPlaybackFinished();
            }
        }

        private void OnPlaybackFinished()
        {
            _isBusy = false;

            // The survivors whoop, once, after the last beat has played.
            //
            // The session COLLECTED these rather than playing them, because
            // Domain has no audio -- and until now nothing on this side drained
            // the list, so the celebration was decided and never heard.
            if (_session != null && !_spokeVictory && _session.PlayerWon)
            {
                _spokeVictory = true;
                foreach (var id in _session.VictoryVoiceIds)
                {
                    CharacterVoice.Play(id, Domain.Combat.Session.VoiceLine.Victory, oncePerFight: true);
                }
            }

            bool over = _session != null && _session.IsOver;

            if (over && !_endRaised)
            {
                _endRaised = true;
                FightEnded?.Invoke(_session.PlayerWon);
            }

            // A finished fight opens ITS OWN screen instead of the Continue
            // button -- the Reckoning on a win, the defeat screen on a loss.
            // Two dismissals to leave one fight is one more than the moment
            // deserves, and both expand better against the stage the last blow
            // landed on than against a screen already acknowledged.
            //
            // The Continue button survives as the fallback for anything with
            // neither wired: a headless fixture, a preview.
            bool tookOver = over && (_session.PlayerWon ? OpenReckoning() : OpenDefeat());

            SetActive(continueButton.gameObject, over && !tookOver);

            // The verb column is fully HIDDEN when the fight is over, not
            // merely dimmed -- it and Continue swap footprints, which is why
            // the tree declares them as a deliberate overlap.
            foreach (var verb in verbButtons) SetActive(verb.gameObject, !over);

            RefreshUi();
        }

        // What the bark shows: the most recent lines, oldest first, trimmed from
        // the FRONT. The trim direction is why the session retro-attaches a
        // consequence to the beat that caused it -- dropping from the front
        // would otherwise discard a cause to make room for its own effect.
        private void TrimLog()
        {
            while (_log.Count > MaxRecentMessages) _log.RemoveAt(0);
        }

        private const int MaxRecentMessages = 4;

        private readonly List<string> _log = new List<string>();

        // Guards the whoop against a second playback pass on the same won fight.
        private bool _spokeVictory;

        internal void PushLogLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return;
            _log.Add(line);
            TrimLog();
            barkLabel.SetContent(string.Join("\n", _log));
        }

        // ---- the character sheet, mid-fight -----------------------------------

        // Same two keys as the hub, so "what am I wearing" is one gesture
        // wherever the player is standing. The sheet is wired read-only in
        // this scene, so opening it cannot change the fight it is describing.
        private void Update()
        {
            if (characterSheetPanel == null) return;

            if (Input.GetKeyDown(KeyCode.C)) ToggleCharacterSheet(inventory: false);
            else if (Input.GetKeyDown(KeyCode.I)) ToggleCharacterSheet(inventory: true);
            else if (Input.GetKeyDown(KeyCode.Escape) && characterSheetPanel.activeSelf)
            {
                SetCharacterSheet(false);
            }
        }

        // Separated from the key for the same reason HubController separates
        // its own: legacy Input cannot be simulated headlessly, so a test that
        // had to press C could not exist. Tests drive these directly.
        public bool CharacterSheetIsOpen => SheetPanel.IsOpen(characterSheetPanel);

        public void ToggleCharacterSheet(bool inventory = false) =>
            SheetPanel.Toggle(characterSheetPanel, inventory);

        public void SetCharacterSheet(bool open, bool inventory = false) =>
            SheetPanel.Set(characterSheetPanel, open, inventory);
    }
}

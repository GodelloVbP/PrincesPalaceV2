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

            bool won = _session != null && _session.PlayerWon;

            // The event first, so anything that wants to bank the payout gets it
            // BEFORE the scene is torn down. Nothing subscribes yet -- the run
            // that will is the next system -- but the ordering is the part that
            // would be painful to discover later.
            FightEnded?.Invoke(won);

            // Back to the descent if one is still live, and to the hub if it is
            // not -- which is what a defeat leaves behind, since losing ends the
            // run rather than the room.
            Navigation.Go(RunManager.HasRun ? Navigation.Map : Navigation.Hub);
        }

        // Raised once, when the player dismisses the end-of-fight button. An
        // event rather than a direct call into a run manager: the fight screen
        // has no business knowing what comes after it.
        public System.Action<bool> FightEnded;

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
            SetActive(continueButton.gameObject, over);

            // The verb column is fully HIDDEN when Continue is up, not merely
            // dimmed -- the two swap footprints, which is why the tree declares
            // them as a deliberate overlap.
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
    }
}

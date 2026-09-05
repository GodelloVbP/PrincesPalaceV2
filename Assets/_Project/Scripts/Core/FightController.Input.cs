using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Stats;

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
                AddEnemyHover(enemyPlates[i].gameObject, index);
            }

            // THE SAME HANDLER FROM THE FIGURE ITSELF. Two ways to name the
            // same monster and one place that decides what naming it means --
            // the alternative is a second copy of the front-rank rule and the
            // is-it-alive check, which is how two paths to one action drift.
            if (enemyHitAreas != null)
            {
                for (int i = 0; i < enemyHitAreas.Length; i++)
                {
                    if (enemyHitAreas[i] == null) continue;

                    int index = i;
                    enemyHitAreas[i].onClick.AddListener(() => OnEnemyPressed(index));
                }
            }

            submenuBackButton.onClick.AddListener(OnBackPressed);
            if (targetCancelButton != null) targetCancelButton.onClick.AddListener(OnBackPressed);
            continueButton.onClick.AddListener(OnContinuePressed);
        }

        // Selection-by-hover is deliberate: it makes the detail column feel
        // instant, and confirming stays a separate click. Added in code rather
        // than as a serialized trigger because a build-time delegate does
        // not serialise -- the same trap that made v1's slot refresh silently
        // not exist in the shipped scene.
        //
        // HoverIndex, NOT EventTrigger -- THE WHEEL SCROLL FIX. EventTrigger
        // implements EVERY UGUI event interface unconditionally, including
        // IScrollHandler, whether or not a Scroll entry was ever added to it.
        // ExecuteEvents walks up from wherever the pointer hit looking for
        // the NEAREST IScrollHandler, so an EventTrigger sitting on the row
        // (added here only for PointerEnter) was the first one found on every
        // wheel notch over a row -- ahead of SubmenuViewport's real
        // ListScroll, several ancestors further up. Its own OnScroll is a
        // no-op (no Scroll entry registered), so the notch was silently
        // swallowed: no exception, no log, just a scrollbar that "doesn't
        // work", which is exactly the report and exactly why 9a208f3 could
        // not reproduce it by calling ListScroll.Scrolled directly -- that
        // seam skips the raycast/bubble entirely. HoverIndex (already used
        // for the intent icons and the reward track) implements only
        // IPointerEnterHandler/IPointerExitHandler, so it is invisible to an
        // IScrollHandler search and the notch reaches ListScroll instead.
        private void AddHover(GameObject row, int index)
        {
            var hover = row.GetComponent<HoverIndex>() ?? row.AddComponent<HoverIndex>();
            hover.Index = index;
            hover.Changed = (i, entered) => { if (entered) OnRowHovered(i); };
        }

        // Which enemy plate the mouse is over, -1 for none. Enter AND exit,
        // unlike the submenu's own AddHover -- a submenu row's selection is
        // meant to persist until another row takes it, but a plate stops
        // being "the one under the cursor" the instant the cursor leaves it,
        // which is what the turn-order ghost preview (RefreshInitiative)
        // needs to know to stop showing.
        private int _hoveredEnemyIndex = -1;

        // Read-only window for GamepadNavigationTests, same reason and same
        // shape as FocusedVerbForTest.
        public int HoveredEnemyIndexForTest => _hoveredEnemyIndex;

        private void AddEnemyHover(GameObject plate, int index)
        {
            var trigger = plate.GetComponent<EventTrigger>() ?? plate.AddComponent<EventTrigger>();

            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ => OnEnemyHovered(index));
            trigger.triggers.Add(enter);

            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => OnEnemyUnhovered(index));
            trigger.triggers.Add(exit);
        }

        // ---- the preview's one scripted turn ---------------------------------
        //
        // tools/preview.ps1 -Spell / -Character wants the cast to happen
        // WITHOUT a hand on the mouse, and to happen through the buttons a
        // hand would use. Anything else -- calling FightSession.CastSkill
        // directly, say -- photographs the session rather than the game: the
        // menu chrome, the target prompt, the affordability grey and the
        // front-rank refusal are all view-side rules that a direct call skips,
        // and every one of them is a way a preview could be wrong while
        // looking right.
        //
        // So this drives the SAME three handlers a click drives: OnVerbPressed
        // for SKILL, OnRowPressed for the row, OnEnemyPressed to confirm a
        // target when the skill needs one. If any of them refuses, the refusal
        // is the session's own message and the author reads it in the log.
        private string _forcedFirstAction;

        // Set by FightBootstrap from DevForcedFirstAction. Public because the
        // headless capture fixture is in the PlayMode assembly, which reaches
        // internals of neither Core nor Editor.
        public void ForceFirstAction(string skillId)
        {
            _forcedFirstAction = string.IsNullOrWhiteSpace(skillId) ? null : skillId;
        }

        // POLLED RATHER THAN FIRED AT Bind, because the player's turn may not
        // be first: a monster faster than the whole party opens the fight, and
        // a cast attempted before its beats have played is refused by CanAct
        // and silently lost. Update is where "is it my turn yet" is already
        // asked twice (RescueAStrandedTurn, RescueAStalledEnemyTurn), so it is
        // where this waits too.
        private void TryForcedFirstAction()
        {
            if (_forcedFirstAction == null || !CanAct) return;

            string skillId = _forcedFirstAction;

            // CONSUMED BEFORE THE PRESS, not after. A skill the caster cannot
            // afford leaves OnRowPressed refusing every frame otherwise, which
            // is a hang rather than a report.
            _forcedFirstAction = null;

            var options = SkillOptions(_session.Current);
            int row = -1;
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Skill != null && options[i].Skill.Id == skillId) row = i;
            }

            if (row < 0)
            {
                _session.AppendMessage($"preview: '{skillId}' is not on this caster's kit this turn.");
                Debug.LogWarning($"[FightController] forced first action '{skillId}' is not among the " +
                                 $"{options.Count} option(s) for {_session.Current?.Name}: " +
                                 string.Join(", ", options.Select(o => o.Skill?.Id)));
                RefreshUi();
                return;
            }

            OnVerbPressed(1);
            OnRowPressed(row);

            // Self and Party resolved inside OnRowPressed; everything else is
            // waiting on a plate. Any living enemy will do -- ResolveDamageAll
            // ignores which one confirmed it, and a single-target cast against
            // the front rank is the encounter the preview built for it.
            if (_menu.IsTargeting) OnEnemyPressed(FirstLivingEnemyIndex());
        }

        private int FirstLivingEnemyIndex()
        {
            var enemies = Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null && enemies[i].IsAlive) return i;
            }

            return 0;
        }

        private void OnEnemyHovered(int index)
        {
            if (_hoveredEnemyIndex == index) return;
            _hoveredEnemyIndex = index;
            RefreshUi();
        }

        private void OnEnemyUnhovered(int index)
        {
            // Only clears if THIS plate was the one being tracked -- the
            // pointer can arrive at the next plate before it leaves the last
            // one's collider, and an exit event landing after that entry
            // event would otherwise clear a hover that is already correct.
            if (_hoveredEnemyIndex != index) return;
            _hoveredEnemyIndex = -1;
            RefreshUi();
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
                    // Same reasoning as the Item branch below: BasicSpell
                    // (docs/PLAN_SHOP.md §4 Phase E) used to guarantee every
                    // character at least one row here regardless of what
                    // they had learned. It's gone, and bookOnly skills with
                    // no unlockLevel are now a live authoring shape, so an
                    // empty list is reachable content, not just a test
                    // fixture -- refuse rather than open nothing to back out of.
                    //
                    // THROUGH HasAnySkillOption, not SkillOptionsFor(...).Count
                    // -- this fires ahead of RefreshUi, so nothing has
                    // memoized the list yet, and the emptiness check is the
                    // only place in the controller that ever wants a yes/no
                    // answer rather than the rows themselves. Building and
                    // then discarding a full ResolvedSkillOption list just to
                    // read its Count is the allocation HasAnySkillOption
                    // exists to skip.
                    if (!_session.HasAnySkillOption(_session.Current))
                    {
                        _session.AppendMessage("No skills to use.");
                        break;
                    }
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

            // Self and Party resolve the same way -- Wool Gathering (HealSelf)
            // was routing through the enemy-target prompt regardless, so the
            // player clicked an enemy plate for a self-heal that ignored the
            // click and healed the caster anyway (ResolveCharacterSkillInner's
            // HealSelf case never reads its target argument). Neither has
            // anything to aim at: the target set is fixed the instant the
            // skill is picked, so there is nothing a Target-depth click could
            // change.
            //
            // AllEnemies no longer joins them here. An earlier version of this
            // check routed every non-SingleEnemy skill through the same
            // instant-resolve path, which meant a group cast -- real mana
            // cost, real variance, the most expensive miscast in the menu --
            // had LESS confirmation friction than a single-target one. It now
            // enters Target depth exactly like SingleEnemy does; the click
            // that confirms it can land on any enemy plate, because
            // ResolveDamageAll already hits every living opponent and ignores
            // which one was actually clicked -- see its own header.
            if (_menu.Branch == MenuBranch.Skill)
            {
                var options = SkillOptions(_session.Current);
                if (index < options.Count)
                {
                    var targeting = options[index].Skill.Targeting;
                    if (targeting == Domain.Combat.SkillTargeting.Self
                        || targeting == Domain.Combat.SkillTargeting.Party)
                    {
                        _session.CastSkill(options[index].Index, _session.Current);
                        AfterResolution();
                        return;
                    }
                }
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

            // AGAINST THE OPTION LIST, not the kit, resolved ONCE and shared
            // by the reach check below and the cast below that -- row
            // position and kit index used to disagree the moment the
            // appended basic-spell row existed; now every skill-branch row
            // has a real ResolvedSkillOption behind it (docs/PLAN_SHOP.md §4
            // Phase E), so a row that does not resolve is simply an invalid
            // selection rather than a second, synthesised kind of row.
            IReadOnlyList<ResolvedSkillOption> options = null;
            int row = -1;
            bool validSkillRow = false;
            if (_menu.Branch == MenuBranch.Skill)
            {
                options = SkillOptions(_session.Current);
                row = _menu.Selection;
                validSkillRow = row >= 0 && row < options.Count;
            }

            // THE FRONT-RANK RULE, asked about whatever this click would
            // actually cast. Attack is always the plain Strike, which has
            // always reached this way. A skill reaches the same way only when
            // it says meleeReach -- every authored skill that leaves the
            // field unset is ranged or magical and was never subject to this
            // rule, so a click on it never asks.
            bool meleeSelected = _menu.Branch == MenuBranch.Attack
                || (_menu.Branch == MenuBranch.Skill && validSkillRow && options[row].Skill.MeleeReach);

            // The front-rank rule is the SESSION's to state, not the view's --
            // it is a combat rule that happens to be enforced at a click.
            if (meleeSelected && !_session.CanMeleeReach(target))
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
                    if (validSkillRow) _session.CastSkill(options[row].Index, target);
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

        private void OnContinuePressed()
        {
            continueButton.gameObject.SetShown(false);
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

            // The same roll the first offer came from, so a reroll is a fresh
            // draw of the identical shape rather than a second, subtly
            // different table.
            reckoning.RerollSource = RollOffers;
            reckoning.Show(reward, RollOffers());
            return true;
        }

        // The loot on offer, rolled against how deep the run is and what class
        // of thing was just killed.
        //
        // The roll itself is RunOrchestrator.RollOffers now, so the bot rolls
        // its offers from the same table (docs/PLAN_BALANCE_BOT.md F2). What
        // stays HERE is the choice of randomness, which is the part that is
        // genuinely the screen's: UnityEngine.Random rather than the session's
        // seeded stream, and deliberately -- the offer is not part of the
        // fight's simulation and must not shift the beats a replay would
        // produce. The bot hands in a seeded one for the opposite reason.
        private System.Collections.Generic.List<Domain.Rewards.ItemOffer> RollOffers()
        {
            // A lambda, not the method group: Random.Range is overloaded on
            // (int,int) and (float,float), and the int overload is the
            // upper-bound-EXCLUSIVE one both Domain tables expect.
            return RunOrchestrator.RollOffers(_session, n => UnityEngine.Random.Range(0, n));
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

            // SET BEFORE THE REPAINT, not after. RefreshMenuChrome (below)
            // includes RefreshVerbs, which now hides ATTACK/SKILL/ITEM/HOLD
            // BACK for exactly the same reason the detail card, submenu and
            // target prompt already hide -- the fight phase has begun, and
            // none of them describe a choice the player is currently making.
            // Setting this after the repaint would have RefreshVerbs read the
            // stale "not busy yet" state on the one frame it actually matters.
            _isBusy = true;

            // REPAINTED HERE, not left for OnPlaybackFinished. _menu.Reset()
            // above already closes the domain state, but nothing was telling
            // the view -- RefreshUi() only ran once the whole beat sequence
            // finished, so the skill-detail card, submenu list, target
            // prompt and verb column stayed on screen for the full swing/cast
            // animation and only vanished at the very end. RefreshMenuChrome()
            // is the menu-only half of RefreshUi(): it skips the plates/
            // stage/initiative, which stay on their pre-resolution snapshot
            // on purpose until FightBeatPlayer paints each beat's vitals --
            // painting those here would snap health bars to their final
            // value before the animation plays.
            RefreshMenuChrome();

            foreach (var line in _session.DrainImmediateMessages()) PushLogLine(line);

            var beats = _session.DrainBeats();

            // THE ONE PLACE A BEAT LEARNS ITS DAMAGE TYPE. CombatBeat itself
            // stays Physical (see its own field comment) -- FightSession
            // never sets this, so it is filled in HERE, the single choke
            // point every drained batch already passes through regardless of
            // which verb produced it (attack, skill, an enemy's own reply).
            // ActorAttackType is the same public read FightHudModel's detail
            // card already uses for its element label, so a hit and the card
            // that described the skill causing it cannot disagree about what
            // "Fire" means.
            foreach (var beat in beats)
            {
                if (beat == null) continue;
                beat.DamageType = _session.ActorAttackType(beat.Actor) ?? DamageType.Physical;
            }

            if (beatPlayer != null && beats.Count > 0)
            {
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

        // THE FIGHT CANNOT STAY BUSY FOR A PLAYBACK THAT IS NOT RUNNING.
        //
        // _isBusy is half of CanAct and is cleared only by OnPlaybackFinished,
        // so anything that ends a playback without calling back leaves every
        // verb disabled for the rest of the fight -- no error, no message, just
        // a screen that stops responding. An exception inside the beat
        // coroutine does exactly that, and so did Flush before it learned to
        // report.
        //
        // The two are set together and synchronously -- _isBusy = true is
        // immediately followed by Play, which sets IsPlaying in the same frame
        // -- so "busy but nothing playing" is not a window this can catch
        // mid-transition. It is only ever the stranded state, which is why it
        // can be recovered from rather than merely logged.
        //
        // Logged AS WELL, once, because a fight that silently repairs itself
        // teaches nobody what broke it.
        private bool _reportedStrandedTurn;

        private void RescueAStrandedTurn()
        {
            if (!_isBusy || _session == null) return;
            if (beatPlayer == null || beatPlayer.IsPlaying) return;

            if (!_reportedStrandedTurn)
            {
                _reportedStrandedTurn = true;
                Debug.LogWarning(
                    "[FightController] The round ended without reporting, so the fight was left busy " +
                    "with every verb disabled. Recovered. If an exception was logged just before " +
                    "this, that is what stopped the playback.");
            }

            OnPlaybackFinished();
        }

        // THE FIGHT CANNOT SIT ON A TURN NOBODY WILL TAKE.
        //
        // The sibling of the rescue above, and the one that would have caught
        // AUDIT.md #46 on the first fight instead of on a player's report.
        //
        // That bug was a fight opening on a monster's turn with nothing wired
        // to resolve it -- FightSession.Begin had 36 callers and all of them
        // were tests. The screen showed monsters standing still, no intents,
        // and every verb dead. Nothing threw, and the busy watchdog stayed
        // quiet because _isBusy is only ever set by the player acting, which is
        // exactly what could not happen.
        //
        // So the flag that watchdog guards was the wrong one. This guards the
        // state the PLAYER experiences: it is not my turn, nothing is playing,
        // and the fight is not over. However that state is arrived at -- a
        // missing Begin, an enemy turn that resolves into nothing, a future
        // status effect that skips an actor without advancing -- it means the
        // same thing, and it can be recovered from the same way.
        //
        // Recovered by resolving the enemy turns that are owed, which is what
        // was missing rather than a reset: a fight nudged back to the player
        // without the monsters having acted would silently skip their round.
        //
        // Logged once, loudly, with the state named. A screen that repairs
        // itself and says nothing is how a bug survives 36 tests.
        private bool _reportedStalledEnemyTurn;

        private void RescueAStalledEnemyTurn()
        {
            if (_session == null || _isBusy) return;
            if (beatPlayer != null && beatPlayer.IsPlaying) return;
            if (_session.IsOver || _session.IsPlayerTurn) return;

            if (!_reportedStalledEnemyTurn)
            {
                _reportedStalledEnemyTurn = true;
                Debug.LogError(
                    "[FightController] The fight is sitting on an enemy turn that nothing is resolving: " +
                    "not the player's turn, no playback running, fight not over. Every verb is disabled " +
                    "and the stage looks frozen. Recovering by resolving the enemy turns that are owed. " +
                    "If the session was never opened (FightSession.Begin), that is the cause -- see " +
                    "AUDIT.md #46.");
            }

            _session.AutoResolveEnemyTurns();

            // The turn came back, so the player needs the telegraph and the
            // HUD that Bind would have painted for an opening that worked.
            if (!_session.IsOver && _session.IsPlayerTurn) _session.PrepareEnemyIntents();

            foreach (var line in _session.DrainImmediateMessages()) PushLogLine(line);

            RefreshUi();
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

            continueButton.gameObject.SetShown(over && !tookOver);

            // The verb column is fully HIDDEN when the fight is over, not
            // merely dimmed -- it and Continue swap footprints, which is why
            // the tree declares them as a deliberate overlap.
            foreach (var verb in verbButtons) verb.gameObject.SetShown(!over);

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
            ShowBark(true);
        }

        // The banner is HIDDEN until it has something to say.
        //
        // It is an 840x104 painted strip across the top centre of the screen and
        // it was drawn from the first frame of every fight, empty -- the single
        // largest piece of furniture on the screen, reserved for nothing. A
        // speech banner with no speech in it does not read as "nothing has been
        // said yet", it reads as a panel that failed to load.
        //
        // Toggled on the PANEL rather than by clearing the label, because an
        // empty label leaves the frame behind, which is the state being fixed.
        private void ShowBark(bool visible)
        {
            if (barkLabel == null) return;
            var panel = barkLabel.transform.parent;
            if (panel != null) panel.gameObject.SetShown(visible);
        }

        // ---- the character sheet, mid-fight -----------------------------------

        // Same two keys as the hub, so "what am I wearing" is one gesture
        // wherever the player is standing. The sheet is wired read-only in
        // this scene, so opening it cannot change the fight it is describing.
        private void Update()
        {
            RescueAStrandedTurn();
            RescueAStalledEnemyTurn();
            TryForcedFirstAction();
            PollGamepadNavigation();

            if (characterSheetPanel == null) return;

            if (Input.GetKeyDown(KeyCode.C)) ToggleCharacterSheet(inventory: false);
            else if (Input.GetKeyDown(KeyCode.I)) ToggleCharacterSheet(inventory: true);
            // Escape belongs to SystemMenuController now, because this panel IS
            // that menu. Closing it here as well would race: Unity does not
            // order Update between components, so the menu's own handler could
            // run after this one, see a closed menu and nothing owning Escape,
            // and reopen it in the same frame the player closed it.
        }

        // ---- gamepad / keyboard navigation -------------------------------------
        //
        // ONE INPUT MODEL, not two. Moving focus calls the exact same
        // OnRowHovered/OnEnemyHovered/_focusedVerb the mouse's own hover
        // already drives, so the three visual states (idle/hovered/selected)
        // stay shared rather than growing a fourth "gamepad-focused" look
        // nobody asked for. Confirming calls the exact same OnVerbPressed/
        // OnRowPressed/OnEnemyPressed a click already does.
        //
        // LEGACY Input, matching every other key this file reads (see
        // ToggleCharacterSheet's own "legacy Input cannot be simulated
        // headlessly" comment) -- untestable for the same reason and by the
        // same design: the logic every one of these calls into is already
        // tested directly (FightMenuStateTests, the click handlers' own
        // callers), so this is thin glue reading an axis, not a second copy
        // of a rule that could disagree with the first.
        //
        // Vertical/Horizontal/Submit/Cancel are Unity's own default-mapped
        // axes -- already wired to a joystick's stick and D-pad in this
        // project's InputManager.asset, so this needed no project-settings
        // change to reach a controller, only the code that reads them.
        private int _focusedVerb;
        private bool _verticalAxisArmed = true;

        // Read-only window for GamepadNavigationTests -- the field itself
        // stays private because nothing outside this file ever needs to SET
        // it directly, only to move it through MoveFocus the way a stick
        // press would.
        public int FocusedVerbForTest => _focusedVerb;

        private void PollGamepadNavigation()
        {
            if (_session == null || _isBusy) return;

            float vertical = Input.GetAxisRaw("Vertical");
            if (Mathf.Abs(vertical) < 0.5f)
            {
                _verticalAxisArmed = true;
            }
            else if (_verticalAxisArmed)
            {
                _verticalAxisArmed = false;
                MoveFocus(vertical > 0f ? -1 : 1);
            }

            if (Input.GetButtonDown("Submit")) ConfirmFocus();
            if (Input.GetButtonDown("Cancel")) OnBackPressed();
        }

        public static int Wrap(int value, int count) => count <= 0 ? 0 : ((value % count) + count) % count;

        // internal, not private -- legacy Input cannot be simulated
        // headlessly (see PollGamepadNavigation's own comment), so a test
        // that had to press a stick to reach this could not exist. Tests
        // drive it directly, the same way ToggleCharacterSheet already does.
        public void MoveFocus(int delta)
        {
            switch (_menu.Depth)
            {
                case MenuDepth.Root:
                    // BOTTOM-UP, not top-down -- BuildVerbColumn's own header:
                    // ATTACK sits at the bottom of the column, so moving the
                    // stick DOWN (delta +1) has to step toward index 0, the
                    // opposite of a list that reads top to bottom. Negated
                    // here rather than at the call site, so every other
                    // depth's delta keeps its ordinary reading-order meaning.
                    _focusedVerb = Wrap(_focusedVerb - delta, verbButtons?.Length ?? 0);
                    RefreshUi();
                    break;

                case MenuDepth.Sub:
                    var rows = CurrentRows();
                    if (rows.Count == 0) return;

                    // AND SCROLLED TO, which hovering never has to do: the
                    // stick can walk the selection clean off the window, and
                    // until it followed, a long list was navigable only for the
                    // eight rows that happened to be on screen.
                    int row = Wrap(_menu.Selection + delta, rows.Count);
                    OnRowHovered(row);
                    ScrollSubmenuRowIntoView(row);
                    break;

                case MenuDepth.Target:
                    var enemies = Enemies;
                    var living = new List<int>();
                    for (int i = 0; i < enemies.Count; i++)
                    {
                        if (enemies[i].IsAlive) living.Add(i);
                    }

                    if (living.Count == 0) return;
                    int at = living.IndexOf(_hoveredEnemyIndex);
                    int next = Wrap((at < 0 ? 0 : at) + delta, living.Count);
                    OnEnemyHovered(living[next]);
                    break;
            }
        }

        public void ConfirmFocus()
        {
            switch (_menu.Depth)
            {
                case MenuDepth.Root:
                    OnVerbPressed(_focusedVerb);
                    break;

                case MenuDepth.Sub:
                    // Nothing hovered yet (a branch just opened, the stick has
                    // not moved) presses row 0 rather than doing nothing --
                    // the same "the first press should work" reasoning
                    // MoveFocus' Wrap already leans on.
                    OnRowPressed(_menu.Selection >= 0 ? _menu.Selection : 0);
                    break;

                case MenuDepth.Target:
                    if (_hoveredEnemyIndex >= 0)
                    {
                        OnEnemyPressed(_hoveredEnemyIndex);
                        break;
                    }

                    var enemies = Enemies;
                    for (int i = 0; i < enemies.Count; i++)
                    {
                        if (!enemies[i].IsAlive) continue;
                        OnEnemyPressed(i);
                        break;
                    }

                    break;
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

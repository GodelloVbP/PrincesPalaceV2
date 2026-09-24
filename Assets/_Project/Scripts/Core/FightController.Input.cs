using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using PrincesPalace.Domain.Combat;
using PrincesPalace.Domain.Combat.Session;
using PrincesPalace.Domain.Stats;
using PrincesPalace.Domain.UiKit;

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

        // Fight has no EventSystem selection, ever (plan section 4) -- the
        // dispatcher's null-assert (NavigationInputModule.ProcessFight) is
        // what actually makes Move/Submit inert, but Navigation.Mode.None
        // stays a defensive second layer against a stray click landing a
        // selection anyway (Selectable.OnPointerDown only calls
        // SetSelectedGameObject when navigation.mode != Navigation.Mode.None).
        //
        // UnityEngine.UI.Navigation, fully qualified -- this file's own
        // namespace (PrincesPalace) already owns a Navigation (Navigation.cs,
        // the scene-load helper), and a bare "Navigation" here resolves to
        // THAT one before the `using UnityEngine.UI;` candidate.
        private static void NoNavigation(Selectable selectable)
        {
            if (selectable == null) return;

            var nav = selectable.navigation;
            nav.mode = UnityEngine.UI.Navigation.Mode.None;
            selectable.navigation = nav;
        }

        private void WireInput()
        {
            if (_wired) return;
            _wired = true;

            for (int i = 0; i < verbButtons.Length; i++)
            {
                int index = i;
                verbButtons[i].onClick.AddListener(() => OnVerbPressed(index));
                NoNavigation(verbButtons[i]);
            }

            for (int i = 0; i < submenuRows.Length; i++)
            {
                int index = i;
                submenuRows[i].onClick.AddListener(() => OnRowPressed(index));
                AddHover(submenuRows[i].gameObject, index);
                NoNavigation(submenuRows[i]);
            }

            for (int i = 0; i < enemyPlates.Length; i++)
            {
                int index = i;
                enemyPlates[i].onClick.AddListener(() => OnEnemyPressed(index));
                AddEnemyHover(enemyPlates[i].gameObject, index);
                NoNavigation(enemyPlates[i]);
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
                    NoNavigation(enemyHitAreas[i]);
                }
            }

            // ---- and the same three ways on the party side -----------------
            //
            // FOUR SURFACES, ONE CONFIRM. Both plate racks and both figure
            // racks end in ConfirmTarget; what differs between them is only
            // how an index names a combatant, which is what the three tiny
            // resolvers below exist for. Duplicating the handler per side was
            // the alternative, and a second copy of "is it alive, is it
            // eligible, which row is selected, which element was chosen" is
            // four more rules that can drift.
            if (pcPlates != null)
            {
                for (int i = 0; i < pcPlates.Length; i++)
                {
                    if (pcPlates[i] == null) continue;

                    int index = i;
                    pcPlates[i].onClick.AddListener(() => OnAllyPlatePressed(index));

                    // HOVER, EVEN THOUGH A PLATE IS ONLY CLICKABLE DURING AN
                    // ALLY PICK. The question "what is on this squadmate"
                    // is a fair one on any turn, and the party's own figures
                    // are unreachable to a pointer outside a pick the same
                    // way the enemies' are -- so the plate is where a mouse
                    // asks it. Inspection is not targeting: this opens the
                    // status box and touches neither _hoveredAllyIndex nor
                    // anything else the pick reads.
                    var hover = pcPlates[i].gameObject.GetComponent<HoverIndex>()
                                ?? pcPlates[i].gameObject.AddComponent<HoverIndex>();
                    hover.Index = index;
                    hover.Changed = InspectAllyOnPlate;

                    NoNavigation(pcPlates[i]);
                }
            }

            if (partyHitAreas != null)
            {
                for (int i = 0; i < partyHitAreas.Length; i++)
                {
                    if (partyHitAreas[i] == null) continue;

                    int slot = i;
                    partyHitAreas[i].onClick.AddListener(() => OnAllyFigurePressed(slot));
                    NoNavigation(partyHitAreas[i]);
                }
            }

            submenuBackButton.onClick.AddListener(OnBackPressed);
            NoNavigation(submenuBackButton);
            if (targetCancelButton != null) targetCancelButton.onClick.AddListener(OnBackPressed);
            NoNavigation(targetCancelButton);
            continueButton.onClick.AddListener(OnContinuePressed);
            NoNavigation(continueButton);
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

        // Which party PLATE the stick has walked to during an ally pick, -1
        // for none.
        //
        // A SECOND FIELD RATHER THAN ONE CURSOR AND A SIDE, and the reason is
        // that these are two different facts, not one fact with a flag: the
        // enemy cursor is also the turn-order ghost preview's input
        // (RefreshInitiative reads it for a push skill's projected
        // destination) and is meaningful outside an ally pick, while this one
        // exists only while one is open. Sharing a field would make the ghost
        // preview read a party plate index as an enemy's.
        private int _hoveredAllyIndex = -1;

        public int HoveredAllyIndexForTest => _hoveredAllyIndex;

        // HOW FAR INTO A TWO-PICK CAST THE MENU IS, for the pad tests (plan
        // 1.12). The same ...ForTest seam FocusedVerbForTest already opens,
        // and read-only for the same reason: the gamepad path has no other
        // way to say "the first press was recorded and the cast has NOT
        // committed", and that distinction is precisely the one a pad bug
        // would blur -- a Submit that both opened the picker and confirmed
        // its first pick looks, from outside, like a picker that works.
        public int PicksHeldForTest => _menu.PickCount;

        public int PicksRequiredForTest => _menu.RequiredPicks;

        public bool IsPickingAllyForTest => _menu.IsPickingAlly;

        private void OnAllyHovered(int plate)
        {
            if (_hoveredAllyIndex == plate) return;
            _hoveredAllyIndex = plate;
            RefreshUi();
        }

        // THE POINTER'S OWN PATH, and the inspect call belongs HERE rather
        // than inside OnEnemyHovered: that method is shared with MoveFocus,
        // which is the pad walking the rack, and the pad must not write the
        // pointer's field. _inspectedActor is cleared by an EXIT event, and
        // a stick press produces none -- so a pad target pick would have left
        // the box open over an actor nothing was pointing at any more.
        // HoverIndex, NOT EventTrigger -- same rule as AddHover above, plus a
        // second reason specific to this rack: a plate is deactivated
        // (SetShown(false)) the instant its monster dies (RefreshEnemyPlates,
        // FightController.Hud.cs), and Unity delivers no PointerExit to a
        // disabled object. EventTrigger's own PointerExit entry would simply
        // never fire, leaving _hoveredEnemyIndex and _inspectedActor pointed
        // at a corpse. HoverIndex.OnDisable exists precisely for this and
        // fires the exit itself.
        private void AddEnemyHover(GameObject plate, int index)
        {
            var hover = plate.GetComponent<HoverIndex>() ?? plate.AddComponent<HoverIndex>();
            hover.Index = index;
            hover.Changed = (i, entered) =>
            {
                if (entered)
                {
                    OnEnemyHovered(i);

                    // The plate names a monster, so it opens that monster's
                    // box -- under the FIGURE, not beside the plate: the
                    // figure is the thing being asked about and the plate is
                    // its readout (FightController.Hud's PlaceStatusBox).
                    InspectEnemyAt(i, true);
                }
                else
                {
                    OnEnemyUnhovered(i);
                    InspectEnemyAt(i, false);
                }
            };
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
        private string _forcedElement;

        // Set by FightBootstrap from DevForcedFirstAction. Public because the
        // headless capture fixture is in the PlayMode assembly, which reaches
        // internals of neither Core nor Editor.
        //
        // `element` is tools/preview.ps1 -Element, already validated against
        // the skill's own elements[] by PreviewFight.ForSpell. Null keeps the
        // first-that-draws rule.
        public void ForceFirstAction(string skillId) => ForceFirstAction(skillId, null);

        public void ForceFirstAction(string skillId, string element)
        {
            _forcedFirstAction = string.IsNullOrWhiteSpace(skillId) ? null : skillId;
            _forcedElement = string.IsNullOrWhiteSpace(element) ? null : element;
        }

        // The bark lines this fight has shown, newest last. Read by
        // ForcedFirstActionElementTests, which has to prove the forced path
        // pressed the element it was ASKED for rather than merely that it
        // pressed one -- and the log line naming it is the only place that
        // distinction is visible from outside the controller.
        public IReadOnlyList<string> RecentLogForTest => _log;

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
            string element = _forcedElement;

            // CONSUMED BEFORE THE PRESS, not after. A skill the caster cannot
            // afford leaves OnRowPressed refusing every frame otherwise, which
            // is a hang rather than a report. The element goes with it: a
            // request half-consumed would press the asked-for element on
            // whatever cast the next refusal let through.
            _forcedFirstAction = null;
            _forcedElement = null;

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

            // AN ELEMENT SKILL STOPS ONE DEPTH SHORT of the plate, so the
            // forced path presses an element too -- the same row press a hand
            // would make, not a shortcut past the menu.
            //
            // THE ELEMENT THE AUTHOR ASKED FOR, AND FAILING THAT THE FIRST ONE
            // THAT DRAWS -- AND IT SAYS WHICH IT DID. Picking silently would
            // put a Fire number on a picture captioned "prismatic_orb" with
            // nothing on screen explaining where Fire came from. Named in the
            // log, the way every other accommodation a preview makes is
            // (PreviewFight.Notes).
            //
            // NOT ELEMENT ZERO, which is what this took before and what made
            // the Water pilot unphotographable: the orb's first element is
            // Earth, Earth authored no art, and the capture came back as an
            // empty stage with a damage number on it. PreviewFight owns the
            // choice so the headless route times its samples against the same
            // element this presses.
            if (_menu.Depth == MenuDepth.Element)
            {
                var elements = options[row].Skill?.Elements;
                var chosen = PreviewFight.PreviewElementOf(options[row].Skill, element);
                if (elements == null || chosen == null)
                {
                    _session.AppendMessage(element == null
                        ? $"preview: '{skillId}' asks for an element and offers none."
                        : $"preview: '{skillId}' was asked to cast as {element} and does not offer it.");
                    RefreshUi();
                    return;
                }

                int at = System.Array.IndexOf(elements, chosen);
                _session.AppendMessage($"preview: casting {options[row].Skill.DisplayName} as " +
                                       $"{chosen.Type} -- " +
                                       (element == null
                                           ? "the first element it offers that draws anything."
                                           : "the element -Element asked for."));
                OnRowPressed(at < 0 ? 0 : at);
            }

            // Self and Party resolved inside OnRowPressed; everything else is
            // waiting on a plate. Any living enemy will do -- ResolveDamageAll
            // ignores which one confirmed it, and a single-target cast against
            // the front rank is the encounter the preview built for it.
            //
            // AN ALLY PICK CONFIRMS ON THE CASTER'S OWN RACK, whichever
            // squadmate the cast will accept first. tools/preview.ps1 refuses
            // Ward and the three Gifts outright today (they need a talent
            // tree or a drained party first, see WORKFLOW section 8), so this
            // arm is for whatever single-ally skill is authored next rather
            // than for anything currently photographable.
            if (!_menu.IsTargeting) return;

            if (_menu.Side == TargetSide.Allies)
            {
                // ONE CONFIRM PER PICK THE CAST NEEDS. A two-pick skill is
                // still waiting on the rack after the first press (plan
                // 1.12), so a preview that pressed once would photograph a
                // half-made cast rather than the spell. Bounded by
                // RequiredPicks rather than looping until it commits, so a
                // squad too small to supply a second pick stops rather than
                // spinning.
                for (int pick = 0; pick < _menu.RequiredPicks && _menu.IsTargeting; pick++)
                {
                    ConfirmTarget(TargetSide.Allies, NextEligibleAlly());
                }
            }
            else OnEnemyPressed(FirstLivingEnemyIndex());
        }

        // The first squadmate this cast will accept that it has not already
        // been given. Only the preview harness picks for the player -- a real
        // cast is refused outright for a null target (AUDIT #147) -- so the
        // "not already picked" half lives here rather than in the session.
        private CombatantState NextEligibleAlly()
        {
            var skill = SelectedSkill();
            var eligible = _session.EligibleAllies(_session.Current, skill);
            foreach (var ally in eligible)
            {
                if (!_menu.HasPicked(ally)) return ally;
            }

            return null;
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

                    // A FRESH PICK HOVERS THE FRONT LIVING ENEMY EXPLICITLY
                    // (owner's 2026-09-19 hardware-round call) -- not left at
                    // -1 for MoveFocus/ConfirmFocus to treat as an implicit
                    // 0. OpenAttack always lands on the enemy rack (its own
                    // header: "does not nest -- it jumps straight to picking
                    // a mark"), so there is no ally branch to guard here.
                    _hoveredEnemyIndex = FirstLivingEnemyIndex();
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
                    SelectFirstCurrentRow();
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
                    SelectFirstCurrentRow();
                    break;

                case 3:
                    // OPENS A COLUMN, where HOLD BACK resolved on the press.
                    // Refused with a sentence when neither direction is legal
                    // -- the same "a submenu that can only be backed out of is
                    // worse than a refusal that explains itself" rule the Skill
                    // and Item branches above already state. A solo party and a
                    // rooted character are both real, reachable cases here.
                    if (!_session.CanMove(_session.Current, Domain.Combat.MoveDirection.Forward)
                        && !_session.CanMove(_session.Current, Domain.Combat.MoveDirection.Back))
                    {
                        _session.AppendMessage("Nowhere to move.");
                        break;
                    }
                    _menu.OpenBranch(MenuBranch.Move);
                    SelectFirstCurrentRow();
                    break;
            }

            RefreshUi();
        }

        private void SelectFirstCurrentRow()
        {
            var rows = CurrentRows();
            if (rows.Count > 0) _menu.Select(0, rows[0].ManaCost);
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

            // AN ELEMENT ROW COMMITS THE ELEMENT. The element is read off the
            // skill rather than parsed back out of the row's own text:
            // FightHudModel.ElementRows builds those names from this same
            // list in this same order, and a label round-trip is a second
            // home for the mapping.
            if (_menu.Depth == Domain.Combat.Session.MenuDepth.Element)
            {
                var elementSkill = SelectedSkill();
                var elements = elementSkill?.Elements;
                if (elements == null || index >= elements.Length || elements[index] == null) return;

                _menu.ChooseElement(elements[index].Type);

                // SELF/PARTY RESOLVES HERE, same as it always has for an
                // element-less Self/Party skill -- there is still nothing a
                // Target-depth click could change, elements[] or not. A
                // SingleEnemy skill has a mark to pick, so it falls through
                // to Target depth exactly like the non-element case does.
                if (elementSkill.Targeting == Domain.Combat.SkillTargeting.Self
                    || elementSkill.Targeting == Domain.Combat.SkillTargeting.Party)
                {
                    var options = SkillOptions(_session.Current);
                    int row = _menu.Selection;
                    if (row < 0 || row >= options.Count) return;

                    TryResolveInstant(options[row], _menu.ChosenElement);
                    return;
                }

                // AN ELEMENTAL SINGLE-ALLY SKILL points at the party rack
                // instead. No such row is authored today; stated here anyway
                // because the alternative is the element path silently
                // sending one at the monsters, and this is one ternary rather
                // than a second decision about targeting living somewhere
                // else.
                if (elementSkill.Targeting == Domain.Combat.SkillTargeting.SingleAlly)
                {
                    _hoveredAllyIndex = -1;
                    _menu.EnterTargeting(TargetSide.Allies);
                }

                RefreshUi();
                return;
            }

            // An item resolves on the actor immediately; a skill needs a mark.
            if (_menu.Branch == MenuBranch.Item)
            {
                UseSatchelItem(index);
                return;
            }

            // A move likewise has nothing to aim at: the direction IS the
            // whole command, and who it trades places with falls out of the
            // formation (FightSession.Move picks the nearest living ally).
            // Row 0 is FORWARD, row 1 is BACK -- FightHudModel.MoveRows' own
            // fixed order.
            if (_menu.Branch == MenuBranch.Move)
            {
                _session.Move(index == 0
                    ? Domain.Combat.MoveDirection.Forward
                    : Domain.Combat.MoveDirection.Back);
                AfterResolution();
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
                    // WHICH ELEMENT, THEN WHICHEVER TARGETING THE SKILL
                    // ACTUALLY HAS. This used to run AFTER the Self/Party
                    // instant-resolve below, which meant a Self/Party skill
                    // that also authored elements[] never reached here at
                    // all -- it cast on the row press with whatever element
                    // ResolvedSkill defaulted to, uncastable from the menu on
                    // any other element even though FightAction.LegalActions
                    // enumerates every one of them for the bot. Checking the
                    // choice first, for any targeting shape, is what makes
                    // the element press (above) the place that decides
                    // Self/Party-resolves-now versus SingleEnemy-needs-a-mark.
                    if (options[index].Skill.HasElementChoice)
                    {
                        _menu.EnterElementChoice();
                        RefreshUi();
                        return;
                    }

                    // Self and Party resolve the same way -- Wool Gathering
                    // (HealSelf) was routing through the enemy-target prompt
                    // regardless, so the player clicked an enemy plate for a
                    // self-heal that ignored the click and healed the caster
                    // anyway (ResolveCharacterSkillInner's HealSelf case
                    // never reads its target argument). Neither has anything
                    // to aim at: the target set is fixed the instant the
                    // skill is picked, so there is nothing a Target-depth
                    // click could change.
                    //
                    // AllEnemies no longer joins them here. An earlier
                    // version of this check routed every non-SingleEnemy
                    // skill through the same instant-resolve path, which
                    // meant a group cast -- real mana cost, real variance,
                    // the most expensive miscast in the menu -- had LESS
                    // confirmation friction than a single-target one. It now
                    // enters Target depth exactly like SingleEnemy does; the
                    // click that confirms it can land on any enemy plate,
                    // because ResolveDamageAll already hits every living
                    // opponent and ignores which one was actually clicked --
                    // see its own header.
                    var targeting = options[index].Skill.Targeting;
                    if (targeting == Domain.Combat.SkillTargeting.Self
                        || targeting == Domain.Combat.SkillTargeting.Party)
                    {
                        TryResolveInstant(options[index], null);
                        return;
                    }

                    // SINGLE-ALLY ENTERS THE SAME DEPTH ON THE OTHER RACK
                    // (AUDIT #147). A ward and the three gifts used to resolve
                    // on this press, because the engine chose the recipient;
                    // the player chooses now, so they stop here exactly the
                    // way a SingleEnemy cast does -- one depth, one confirm,
                    // one cancel, and BACK lands on the skill list either way.
                    if (targeting == Domain.Combat.SkillTargeting.SingleAlly)
                    {
                        // HOW MANY PICKS IS THE EFFECT'S ANSWER, not the
                        // row's and not this method's (plan 1.12;
                        // SkillEffects.PicksRequired is the one place it
                        // lives). Palace Passage stops twice on the same rack
                        // at the same depth; everything else stops once, and
                        // the count of 1 is what makes that literally the
                        // same code path rather than a preserved special
                        // case.
                        _hoveredAllyIndex = -1;
                        _menu.EnterTargeting(TargetSide.Allies,
                            Domain.Combat.SkillEffects.PicksRequired(options[index].Skill.Effect));
                        RefreshUi();
                        return;
                    }
                }
            }

            // A FRESH PICK HOVERS THE FRONT LIVING ENEMY EXPLICITLY (owner's
            // 2026-09-19 hardware-round call), the same reason OpenAttack's
            // own branch above sets it rather than leaving -1.
            _hoveredEnemyIndex = FirstLivingEnemyIndex();
            _menu.EnterTargeting();
            RefreshUi();
        }

        // ---- confirming a target ---------------------------------------------
        //
        // THREE RESOLVERS AND ONE HANDLER. A click names a combatant, and the
        // three surfaces name one differently: an enemy plate and an enemy
        // figure share an index (nothing on that side ever moves, so a plate
        // index and a stage slot are the same number), a party PLATE is
        // indexed by RANK (RefreshPcPlates paints party[i] onto plate i, so
        // the column follows a Move -- AUDIT #144), and a party FIGURE is
        // indexed by fight-long SLOT (FightController.StageVisuals.DrawSide:
        // "the two halves are indexed differently and that is the point").
        //
        // That is why the shared handler takes a COMBATANT rather than a side
        // and an index: an int means three things here, and passing it down
        // would push the disagreement into the one place that must not have
        // it.
        private void OnEnemyPressed(int index)
        {
            var enemies = Enemies;
            ConfirmTarget(TargetSide.Enemies, index >= 0 && index < enemies.Count ? enemies[index] : null);
        }

        private void OnAllyPlatePressed(int plate) => ConfirmTarget(TargetSide.Allies, PartyMemberOnPlate(plate));

        private void OnAllyFigurePressed(int slot) => ConfirmTarget(TargetSide.Allies, PartyMemberInSlot(slot));

        // WHAT THE PLAYER SAW ON THAT CARD, not what the live list holds now.
        // _plateOccupants is the painted-occupancy record RefreshPcPlates
        // writes (AUDIT #144); a click can only arrive while CanAct, which
        // means not busy, which means the two agree -- so this is a guard
        // against the class of bug rather than a live one, and it costs a
        // bounds check.
        private CombatantState PartyMemberOnPlate(int plate)
        {
            if (_plateOccupants != null && plate >= 0 && plate < _plateOccupants.Length)
            {
                return _plateOccupants[plate];
            }

            var party = _session?.Encounter.PlayerParty;
            return party != null && plate >= 0 && plate < party.Count ? party[plate] : null;
        }

        // The party member standing in stage slot `slot` -- deliberately NOT
        // party[slot]. SlotIndexOf is the same map DrawSide draws by, so the
        // figure under the cursor and the combatant this returns cannot
        // disagree about who was clicked.
        private CombatantState PartyMemberInSlot(int slot)
        {
            var party = _session?.Encounter.PlayerParty;
            if (party == null) return null;

            for (int i = 0; i < party.Count; i++)
            {
                if (SlotIndexOf(party[i]) == slot) return party[i];
            }

            return null;
        }

        private void ConfirmTarget(TargetSide side, CombatantState target)
        {
            if (!CanAct || !_menu.IsTargeting || _menu.Side != side) return;
            if (target == null || !target.IsAlive) return;

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

            // ---- the ally side, which has no reach question to ask ---------
            //
            // Rank masks and the front-rank rule are about fighting PAST
            // somebody and nothing stands between a caster and his own squad
            // (docs/handoffs/archive/battle_ui/README.md:230). What DOES gate
            // the click is whether this effect accepts this squadmate, and
            // that is EligibleAllies -- the same list the plates were lit
            // from, asked again here for the same reason the enemy branch
            // below re-asks CanReachEnemy: so a refusal reads as a message
            // rather than as a click that did nothing.
            if (side == TargetSide.Allies)
            {
                if (_menu.Branch != MenuBranch.Skill || !validSkillRow) return;

                if (!_session.EligibleAllies(_session.Current, options[row].Skill).Contains(target))
                {
                    _session.AppendMessage($"{target.Name} cannot take {options[row].Skill.DisplayName}.");
                    RefreshUi();
                    return;
                }

                // THE THIRD ILLEGAL CASE, and the only one that exists solely
                // because a cast is half made (plan 1.12's state table): dead
                // and wrong-side are refused above by EligibleAllies, but
                // "you already picked them" is a fact about this cast, so the
                // menu is the one that knows it. Refused with a sentence
                // rather than silently ignored, for the same reason every
                // other arm of this method says something.
                if (_menu.HasPicked(target))
                {
                    _session.AppendMessage($"{target.Name} is already going.");
                    RefreshUi();
                    return;
                }

                // NOTHING IS WRITTEN UNTIL THE LAST PICK. A first pick on a
                // two-pick cast records and repaints and stops -- no mana, no
                // cooldown, no free-action lock, no swap -- which is what
                // makes a cancel here cost nothing by construction rather
                // than by cleanup (plan 1.12).
                if (!_menu.RecordPick(target, out bool complete)) return;

                if (!complete)
                {
                    // AND THE STICK LETS GO OF THE ALLY IT JUST TOOK. The
                    // cursor is a hover, and a hover on somebody this cast can
                    // no longer accept is a hover the player cannot act on --
                    // leaving it there would put the focus marker on a plate
                    // whose Submit is a refusal. Cleared to -1 so the "nothing
                    // hovered presses the first pickable one" rule takes over,
                    // which is the same reasoning OnBackPressed already gives
                    // for clearing it on the way out.
                    _hoveredAllyIndex = -1;
                    RefreshUi();
                    return;
                }

                // ONE CAST, WITH EVERY PICK IT COLLECTED. A one-pick skill
                // hands over a list of one and reaches the identical
                // dispatcher -- see FightSession.CastSkillOnPicks.
                _session.CastSkillOnPicks(options[row].Index, _menu.Picks, _menu.ChosenElement);
                AfterResolution();
                return;
            }

            // THE REACH OF WHATEVER THIS CLICK WOULD ACTUALLY DO. Attack is
            // always the plain Strike (Reach.Melee); a skill carries its own,
            // which is Reach.Any for every ranged or magical one and was never
            // a restriction at all. One question, one primitive, whichever
            // branch is open -- there is no longer a "does this rule apply"
            // flag to get wrong, because Reach.Any answering true IS the rule
            // not applying.
            var reach = _menu.Branch == MenuBranch.Skill && validSkillRow
                ? options[row].Skill.Reach
                : Domain.Combat.Reach.Melee;

            // The rule is the SESSION's to state, not the view's -- it is a
            // combat rule that happens to be enforced at a click. The command
            // refuses it a second time on its own (ExecuteAttack/CastSkill);
            // this exists so the refusal reads as a message rather than as a
            // click that did nothing.
            if (!_session.CanReachEnemy(_session.Current, reach, target))
            {
                _session.AppendMessage($"{target.Name} is out of reach.");
                RefreshUi();
                return;
            }

            switch (_menu.Branch)
            {
                case MenuBranch.Attack:
                    _session.ExecuteAttack(target);
                    break;

                case MenuBranch.Skill:
                    // The element the player picked one depth up, or null for
                    // every skill that never asked. FightSession refuses a
                    // mismatch outright and spends nothing -- see
                    // TryTakeElementChoice.
                    if (validSkillRow) _session.CastSkill(options[row].Index, target, _menu.ChosenElement);
                    break;

                default:
                    return;
            }

            AfterResolution();
        }

        // THE FIGHT'S OWN WAY TO THE OVERARCHING MENU, wired as this
        // context's `systemMenu` handler (RegisterNavContext) and reached by
        // Start, the same button that reaches it from the hub and the map.
        //
        // NOT guarded on _isBusy, unlike the three IFightNavigationTarget
        // members: opening the menu is not a fight action and does not ask
        // the session for anything -- pausing mid-playback is exactly when a
        // player wants it. Ordering is not a hazard either: opening pushes
        // the menu's own context above Fight's, so the dispatcher's next
        // call runs the menu's branch, not Fight's (topAtStart is captured
        // once per call, plan section 3).
        private void OpenSystemMenu() => SystemMenuController.OpenFromRoot(systemMenu);

        private void OnBackPressed()
        {
            // THE ROOT CASE IS NOW "NOTHING HAPPENS", and that is the whole
            // of the owner's 2026-09-19 call. FightMenuState.Back() returns
            // false at MenuDepth.Root and only there -- a submenu, an element
            // list and a target pick each step back one level and return true
            // -- so a false here is "B with nothing left to back out of",
            // which used to open the system menu (AUDIT.md #155) and now
            // belongs to Start instead (OpenSystemMenu above). B keeps one
            // meaning in a fight rather than two.
            if (!_menu.Back()) return;

            // The stick's ally cursor belongs to one open pick and to nothing
            // else. Left set, a later pick would open with the stick already
            // resting on whoever it last walked to -- which is a hover the
            // player never made.
            _hoveredAllyIndex = -1;
            RefreshUi();
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

        // SELF/PARTY CASTS RESOLVE THE INSTANT THEY ARE PICKED -- neither
        // targeting has a mark left to aim at (see the two call sites' own
        // headers for why), so the click that reaches this method IS the
        // whole command. Shared by the Element-depth branch (a self/party
        // skill with an element choice, where the element press itself
        // commits) and the plain row-select branch (a self/party skill with
        // no choice at all) so the cast-and-repaint pair has one home.
        private bool TryResolveInstant(ResolvedSkillOption option, DamageType? element)
        {
            var targeting = option.Skill.Targeting;
            if (targeting != Domain.Combat.SkillTargeting.Self
                && targeting != Domain.Combat.SkillTargeting.Party)
            {
                return false;
            }

            _session.CastSkill(option.Index, _session.Current, element);
            AfterResolution();
            return true;
        }

        // Every path that resolves an action ends here: the menu closes, the
        // recorded beats are handed to playback, and the screen repaints.
        private void AfterResolution()
        {
            _menu.Reset();
            _hoveredAllyIndex = -1;
            _hoveredEnemyIndex = -1;

            // AND THE PAD IS BACK ON THE VERBS. A round resolving under a
            // player who was reading a monster's statuses is the fight moving
            // on without them; leaving the flag set would put the marker back
            // on an actor the moment _isBusy cleared, on a turn the player
            // arrived at fresh.
            _inspecting = false;
            _characterSelecting = false;
            _inspectingIntent = false;
            _targetIntentEnemyIndex = -1;

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

            // WHERE A BEAT LEARNS ITS DAMAGE TYPE FROM WHOEVER SWUNG, which is
            // most beats but no longer all of them. This is still the single
            // choke point every drained batch passes through regardless of
            // which verb produced it (attack, skill, an enemy's own reply),
            // and ActorAttackType is still the same public read
            // FightHudModel's detail card uses for its element label -- so a
            // hit and the card that described the skill causing it cannot
            // disagree about what "Fire" means.
            //
            // PAINT, NOT ASSIGN. A status tick records its own element off
            // the status (CombatBeat.DeclareDamageType) and has no actor to
            // read one from; PaintActorDamageType is what lets this loop stay
            // a loop without having to ask each beat which kind it is. The
            // `?? Physical` that used to live on this line moved inside it
            // for the same reason -- see that method's own header.
            foreach (var beat in beats)
            {
                if (beat == null) continue;
                beat.PaintActorDamageType(_session.ActorAttackType(beat.Actor));
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
        // coroutine used to do exactly that, and so did Flush before it
        // learned to report.
        //
        // IsPlaying IS TRUSTWORTHY HERE ONLY BECAUSE FightBeatPlayer MAKES IT
        // SO: its playback clears the flag and reports from a finally, so a
        // coroutine that died by exception cannot leave it stuck true (see
        // PlayBeats). Before that, this watchdog asked the dead coroutine's
        // own flag whether it was alive and was told yes forever. It stays as
        // the backstop for a finish that is lost some other way.
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

        // docs/archive/PLAN_BATTLE_SPEED.md G5: the one door onto the visible bark
        // feed a PlayMode test can reach -- PlayMode has no InternalsVisibleTo
        // grant, same reason every other *ForTest method on this controller
        // exists. Used to burn the preset's name into the runtime capture's
        // own frames, so a viewer scrubbing the assembled GIF can read which
        // preset is playing without a caption baked in after the fact.
        public void PushLogLineForTest(string line) => PushLogLine(line);

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

            // ABOVE the characterSheetPanel guard below, and that placement
            // is the point: the sheet being unwired is a reason not to read
            // its two hotkeys, not a reason for a card on the HUD behind it
            // to stop breathing. Costs one int compare on every frame of
            // every fight shipped today -- no pool authors pulse yet, so the
            // list it guards is empty (see RefreshPoolPulse).
            RefreshPoolPulse();

            if (characterSheetPanel == null) return;

            // GATED ON TOP OF STACK (plan section 4/11), same shape as
            // Hub's and Map's: a modal that has pushed its own context over
            // Fight's must not also see C/I land underneath it. Fight's own
            // _navContext is registered by RegisterNavContext the moment a
            // session becomes active, so this is meaningful from the first
            // frame of a real fight.
            if (NavigationInputModule.Contexts != null && !NavigationInputModule.Contexts.IsTop(_navContext))
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.C)) ToggleCharacterSheet(inventory: false);
            else if (Input.GetKeyDown(KeyCode.I)) ToggleCharacterSheet(inventory: true);

            // Cancel/Escape is not read here at all -- it belongs to the ONE
            // dispatch point now (NavigationInputModule's Fight branch,
            // OnBackPressed), not a second poll racing it.
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
        // The axis/button READ used to live here too (PollGamepadNavigation,
        // called from this file's own Update()). It moved verbatim into
        // NavigationInputModule's Fight branch (docs/GAMEPAD_NAVIGATION_PLAN.md
        // section 3) -- Draft 2's gating-on-"am I top" approach could not
        // stop two readers from each independently, correctly, seeing
        // themselves as authorized in the same frame; one dispatch point
        // reading the axis once is the structural fix. MoveFocus/
        // ConfirmFocus/OnBackPressed themselves are UNCHANGED and stay
        // internal/private-tested directly, exactly as FightGamepadNavigationTests
        // and AllyTargetPickerTests already do.
        private int _focusedVerb;

        // Read-only window for GamepadNavigationTests -- the field itself
        // stays private because nothing outside this file ever needs to SET
        // it directly, only to move it through MoveFocus the way a stick
        // press would.
        public int FocusedVerbForTest => _focusedVerb;

        // The dispatcher's Fight target, reached only through the interface
        // (plan section 3) -- MoveFocus/ConfirmFocus/OnBackPressed stay
        // public/private exactly as before, called directly by every
        // existing test; these three explicit members exist only to
        // reinstate PollGamepadNavigation's own busy guard, which the
        // dispatcher itself has no reason to know about (Fight's model, not
        // the dispatch mechanism, owns "is this action legal right now").
        // WHERE INSPECT IS LAYERED IN, and why it is here rather than inside
        // MoveFocus/ConfirmFocus/OnBackPressed themselves: those three are
        // the frozen focus model (docs/GAMEPAD_NAVIGATION_PLAN.md section 1)
        // and are called directly, unguarded, by every existing test and by
        // the Back button's own onClick. These wrappers are already the seam
        // that reinstates the busy guard for the pad path only -- inspect is
        // a pad-only state for exactly the same reason, so it is the same
        // seam's job.
        void IFightNavigationTarget.MoveFocus(int delta)
        {
            if (_session == null || _isBusy) return;

            // WHILE INSPECTING THE STICK WALKS THE ACTORS, both axes. The
            // verb column is not somewhere the vertical axis can quietly move
            // to while the marker stands on a monster -- B would then return
            // to a verb the player never saw themselves choose.
            if (IsInspecting)
            {
                InspectVerticalStep(delta);
                return;
            }

            _inspecting = false;
            _characterSelecting = false;
            _inspectingIntent = false;

            // THE PAD'S OWN FLIP, NOT MoveFocus' -- MoveFocus is the frozen
            // focus model (this file's own "ONE INPUT MODEL" header) and
            // every direct-call test drives it with delta UNCHANGED
            // (CycleTarget's own header names them); this wrapper is the
            // dispatch-boundary seam that already exists to layer pad-only
            // behaviour on top of it (inspect, the busy guard), so it is
            // where a pad-only sign correction belongs too.
            //
            // ProcessFight hands every call here Vertical's OWN sign (Up ->
            // -1, Down -> +1, armed that way so Root's bottom-up verb column
            // reads right -- MoveFocus' own Root case). That is backwards
            // for a target rack: the owner's 2026-09-19 call is Up = one
            // slot DEEPER, Down = one slot nearer, so it needs one negation
            // to land in CycleTarget's "+1 is deeper" convention. Y is not
            // mirrored between the two racks (FightStageAnchors.SlotOffset
            // only negates X), so "further back is higher on screen" is true
            // for both, and this single flip serves both racks -- unlike
            // InspectMove's horizontal case, which needs a per-side one.
            //
            // TARGET DEPTH GOES THROUGH CycleTargetFromPad, NOT MoveFocus,
            // for a second, separate reason (2026-09-19, the gate's own
            // find): FocusedElement/ConfirmFocus already treat "nothing
            // hovered" as enemy 0 -- the marker sits there and Submit would
            // hit it -- so a pad player's FIRST press has to read as one
            // step FROM that implicit 0, not as WrapFromNoHover's older
            // "first press is index 0" rule (CycleTarget's own header). That
            // older rule is still exactly right for the direct MoveFocus(int)
            // convention below (every FightGamepadNavigationTests/
            // AllyTargetPickerTests direct call pins it), so it could not be
            // changed in place -- CycleTargetFromPad is the same cycle with
            // only the no-hover case resolved differently, kept to this one
            // pad entry point and InspectMove's own Target branch.
            if (_menu.Depth == MenuDepth.Target)
            {
                if (_menu.Side == TargetSide.Enemies)
                {
                    if (IsTargetIntentInspection)
                    {
                        if (delta > 0) SetTargetIntentInspection(false);
                        return;
                    }

                    if (delta < 0)
                    {
                        SetTargetIntentInspection(true);
                        return;
                    }
                }

                CycleTargetFromPad(-delta);
                return;
            }

            _targetIntentEnemyIndex = -1;
            MoveFocus(delta);
        }

        void IFightNavigationTarget.ConfirmFocus()
        {
            if (_session == null || _isBusy) return;

            // INSPECT IS A LOOK, NOT A CHOICE. There is no verb pending, so
            // there is nothing for Submit to confirm -- pressing it must not
            // fall through to Root's own ConfirmFocus and open ATTACK on the
            // monster the player was only reading.
            if (IsInspecting || IsTargetIntentInspection) return;

            _inspecting = false;
            _characterSelecting = false;
            _inspectingIntent = false;
            _targetIntentEnemyIndex = -1;
            ConfirmFocus();
        }

        void IFightNavigationTarget.OnBackPressed()
        {
            if (_session == null || _isBusy) return;

            if (IsTargetIntentInspection)
            {
                SetTargetIntentInspection(false);
                return;
            }

            // BEFORE _menu.Back(), and that order is the whole of it: B at
            // Root is "nothing happens" by the owner's own 2026-09-19 call,
            // and inspect sits on top of Root. Asking the menu first would
            // spend the press on a no-op and leave the player stuck on the
            // actor row with no way back to the verbs.
            if (LeaveInspect()) return;

            OnBackPressed();
        }

        void IFightNavigationTarget.InspectMove(int delta)
        {
            if (_session == null || _isBusy) return;
            InspectMove(delta);
        }

        // WHERE THE FOCUS MARKER GOES ON THIS SCREEN (hardware round 1's
        // visual pass; Core/FocusMarker.cs has the whole argument). Fight is
        // the one screen with no EventSystem selection to read -- ProcessFight
        // asserts it null every frame on purpose -- so the dispatcher asks the
        // model instead, and this is the model's answer.
        //
        // READ-ONLY. Every index below was already kept and already moved by
        // MoveFocus/ConfirmFocus; nothing here writes anything, and the direct-
        // call tests those three have (FightGamepadNavigationTests,
        // AllyTargetPickerTests) are untouched by it.
        //
        // NULL WHILE BUSY, explicitly rather than by relying on the verb
        // column being hidden: at Target depth a beat can resolve with an
        // enemy plate still on screen, and a marker sitting on a target the
        // player can no longer pick is worse than no marker.
        object IFightNavigationTarget.FocusedElement => FocusedElement();

        // internal, same seam FocusedVerbForTest already opens -- a PlayMode
        // test asserts which literal node the marker is on without having to
        // reach through the explicit interface implementation.
        internal GameObject FocusedElement()
        {
            if (_session == null || _isBusy) return null;

            // INSPECT FIRST, because it sits on top of Root: the marker
            // belongs on the actor being read, not on the verb the player
            // will come back to.
            //
            // The same EnemyFigureOrPlate/AllyFigureOrPlate pair a target
            // pick uses, and it resolves differently here for a reason worth
            // knowing: the hit areas over the figures come up only while a
            // target is being chosen (RefreshEnemyPlates, PaintPcPlate), so at
            // Root both fall back to the PLATE. The marker therefore stands
            // on the plate in the corner while PlaceStatusBox puts the box
            // itself under the figure on the battlefield -- the two ends of
            // one actor, which is the best this can do without the hit areas
            // being brought up outside a pick (a Hud change, not this file's).
            if (InspectedStop(out var inspected))
            {
                if (_inspectingIntent && !inspected.IsAlly && enemyIntentIcons != null
                    && inspected.Index >= 0 && inspected.Index < enemyIntentIcons.Length)
                {
                    return enemyIntentIcons[inspected.Index];
                }

                return inspected.IsAlly
                    ? AllyFigureOrPlate(inspected.Index)
                    : EnemyFigureOrPlate(inspected.Index);
            }

            switch (_menu.Depth)
            {
                case MenuDepth.Root:
                    return NodeAt(verbButtons, _focusedVerb);

                case MenuDepth.Element:
                case MenuDepth.Sub:
                    // Clamped to the rows actually built, not to the row
                    // count: CurrentRows can be longer than submenuRows (the
                    // column truncates and says so in its own hint), and a
                    // selection past the last built row has no node.
                    return NodeAt(submenuRows, _menu.RowSelection);

                case MenuDepth.Target:
                    if (IsTargetIntentInspection)
                    {
                        int intentIndex = _hoveredEnemyIndex >= 0
                            ? _hoveredEnemyIndex
                            : FirstLivingEnemyIndex();
                        if (enemyIntentIcons != null && intentIndex >= 0 && intentIndex < enemyIntentIcons.Length)
                        {
                            return enemyIntentIcons[intentIndex];
                        }
                    }

                    if (_menu.Side == TargetSide.Allies)
                    {
                        int plate = _hoveredAllyIndex;
                        if (plate < 0)
                        {
                            // NOTHING HOVERED YET points at what Submit would
                            // actually press, which ConfirmFocus already
                            // defines as the first candidate -- the same rule,
                            // read off the same helper, so the arrow can never
                            // point at one thing while Submit hits another.
                            var pickable = PickableAllyPlates();
                            if (pickable.Count == 0) return null;
                            plate = pickable[0];
                        }

                        return AllyFigureOrPlate(plate);
                    }

                    var enemies = Enemies;

                    // A DEAD HOVER READS AS NO HOVER, same as FocusedActor and
                    // ConfirmFocus's own no-hover branch (~2496-2502): the
                    // pointer can still be sitting on a plate/figure whose
                    // monster died under it (AddEnemyHover wires PointerEnter
                    // straight to OnEnemyHovered with no alive check), and the
                    // marker must not stand on a corpse.
                    if (_hoveredEnemyIndex >= 0 && _hoveredEnemyIndex < enemies.Count
                        && enemies[_hoveredEnemyIndex].IsAlive)
                    {
                        return EnemyFigureOrPlate(_hoveredEnemyIndex);
                    }

                    for (int i = 0; i < enemies.Count; i++)
                    {
                        if (enemies[i].IsAlive) return EnemyFigureOrPlate(i);
                    }

                    return null;
            }

            return null;
        }

        // WHICH COMBATANT THE PAD IS ON, or null when it is on something that
        // is not one. FocusedElement's question answered one step earlier:
        // that one resolves a node for the marker to stand on, this one
        // resolves the actor the status box describes, and both read the same
        // two cursors MoveFocus already maintains. Kept beside it so the two
        // cannot drift into disagreeing about where the pad is.
        //
        // TWO WAYS THE PAD CAN BE ON AN ACTOR, and they are different
        // questions rather than one with a flag. At Target depth the pad is
        // CHOOSING one, and the box is a side effect of the choice. While
        // inspecting it is only READING one, at Root, with nothing pending --
        // the owner's 2026-09-19 call, and the reason the horizontal axis now
        // reaches this method at all (InspectMove above,
        // NavigationInputModule.ProcessFight for the axis read).
        internal CombatantState FocusedActor()
        {
            if (_session == null || _isBusy) return null;

            if (InspectedStop(out var inspected)) return inspected.Actor;

            if (_menu.Depth != MenuDepth.Target) return null;

            if (_menu.Side == TargetSide.Allies)
            {
                if (_hoveredAllyIndex >= 0) return PartyMemberOnPlate(_hoveredAllyIndex);

                // NOTHING HOVERED YET names what Submit would press, which is
                // the first candidate -- read off the same helper ConfirmFocus
                // and FocusedElement use, so the box can never describe one
                // monster while the arrow stands on another.
                var pickable = PickableAllyPlates();
                return pickable.Count > 0 ? PartyMemberOnPlate(pickable[0]) : null;
            }

            var enemies = Enemies;

            // Same dead-hover-reads-as-no-hover rule as FocusedElement above:
            // a hover can outlive the monster it landed on.
            if (_hoveredEnemyIndex >= 0 && _hoveredEnemyIndex < enemies.Count
                && enemies[_hoveredEnemyIndex].IsAlive)
            {
                return enemies[_hoveredEnemyIndex];
            }

            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].IsAlive) return enemies[i];
            }

            return null;
        }

        // THE FIGURE ON THE BATTLEFIELD, not the plate in the corner.
        //
        // The owner asked for "a small hovering arrow for the thing you're
        // targeting", and while a target is being chosen the thing being
        // targeted is the monster, not its HUD readout twenty rows away at
        // the top of the screen. The hit areas exist for exactly this window
        // -- RefreshEnemyPlates shows them only while picking ("the figure is
        // a target only while one is being chosen") -- so asking for one and
        // taking the plate when it is not up is the same question answered
        // for both states rather than a special case for one.
        private GameObject EnemyFigureOrPlate(int index)
        {
            var figure = NodeAt(enemyHitAreas, index);
            return figure != null && figure.activeInHierarchy ? figure : NodeAt(enemyPlates, index);
        }

        // THE ALLY SIDE'S TWO ARRAYS ARE NOT INDEXED THE SAME WAY, which is
        // why this is six lines and its enemy twin is one. partyHitAreas is
        // indexed by STAGE SLOT (a slot belongs to one character for the whole
        // fight) and pcPlates by PLATE (the party list, which reorders on
        // every Move) -- FightScreen.PartyHitAreas' own header says so and
        // calls the difference the point. The bridge is the combatant itself,
        // through the two lookups the controller already owns for exactly
        // this: PartyMemberOnPlate and PartyMemberInSlot, which is what
        // OnAllyPlatePressed and OnAllyFigurePressed each use to resolve their
        // own click.
        private GameObject AllyFigureOrPlate(int plate)
        {
            var member = PartyMemberOnPlate(plate);
            if (member != null && partyHitAreas != null)
            {
                for (int slot = 0; slot < partyHitAreas.Length; slot++)
                {
                    if (!ReferenceEquals(PartyMemberInSlot(slot), member)) continue;

                    var figure = NodeAt(partyHitAreas, slot);
                    if (figure != null && figure.activeInHierarchy) return figure;
                    break;
                }
            }

            return NodeAt(pcPlates, plate);
        }

        private static GameObject NodeAt(Button[] nodes, int index)
        {
            if (nodes == null || index < 0 || index >= nodes.Length) return null;
            return nodes[index] == null ? null : nodes[index].gameObject;
        }

        // Pushed once per scene load, the moment a session becomes active --
        // the same "_session != null" half of PollGamepadNavigation's own
        // guard, just moved from a per-frame check to a one-time
        // registration. Idempotent: Bind() can in principle run more than
        // once against a live controller (a rebind), and pushing a second
        // Fight context for the same controller would leave a stale entry
        // under the stack no Close() will ever reach.
        private void RegisterNavContext()
        {
            if (_navContext != null) return;

            // systemMenu, NOT Cancel (the owner's 2026-09-19 call): Start
            // reaches the overarching menu from inside a fight, and B is
            // left meaning one thing only -- step back a level.
            _navContext = NavContext.ForFight(this, systemMenu: OpenSystemMenu);
            NavigationInputModule.Contexts?.Push(_navContext);
        }

        // Fight is a whole scene (docs/CODE_MAP.md), not a panel toggled by
        // Open()/Close() -- so OnDestroy, fired when the scene unloads, is
        // where "the session ends" actually happens. Remove, not Pop: this
        // is the OnDisable/OnDestroy safety net plan section 4 calls for,
        // not the ordinary top-of-stack case, so it must not assume Fight is
        // still on top when the scene goes down.
        private void OnDestroy()
        {
            if (_navContext == null) return;

            NavigationInputModule.Contexts?.Remove(_navContext);
            _navContext = null;
        }

        public static int Wrap(int value, int count) => count <= 0 ? 0 : ((value % count) + count) % count;

        // THE FIRST STICK PRESS FROM NO HOVER, on either target rack. Folding
        // a virtual "at 0" through Wrap(at + delta, count) -- what both racks
        // did before this helper existed -- happened to give "previous" the
        // right answer by accident (Wrap(-1, count) == count-1, the last
        // index) but gave "next" the wrong one: Wrap(0 + 1, count) == 1,
        // skipping index 0 entirely. ConfirmFocus already treats no-hover as
        // index 0 (ally and enemy racks below), so ":next" landing on 1
        // disagreed with what confirming it unhovered would have picked.
        // ONE HELPER for both racks, so a future third rack cannot drift back
        // into the two independently-reasoned cases this replaces -- see
        // FightGamepadNavigationTests for the literal indices this pins.
        //
        // STILL LIVE, not superseded by CycleTargetFromPad (2026-09-19): the
        // at<0 branch below is exactly right for a DIRECT MoveFocus(int)/
        // CycleTarget(delta) call -- the only kind FightGamepadNavigationTests'
        // MovingFocusAtTargetDepthCyclesTheHoveredLivingEnemy/
        // AFirstPreviousPressAtTargetDepthLandsOnTheLastLivingEnemy and every
        // AllyTargetPickerTests case drive -- where "nothing hovered" really
        // does mean nothing, not an implicit enemy 0. CycleTargetFromPad
        // resolves that implicit-0 case itself, one layer up, so by the time
        // it calls this the `at` argument is never negative and this method's
        // own no-hover branch is simply never reached from a pad path.
        public static int WrapFromNoHover(int at, int delta, int count) =>
            at < 0 ? (delta > 0 ? 0 : count - 1) : Wrap(at + delta, count);

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

                case MenuDepth.Element:
                case MenuDepth.Sub:
                    var rows = CurrentRows();
                    if (rows.Count == 0) return;

                    // AND SCROLLED TO, which hovering never has to do: the
                    // stick can walk the selection clean off the window, and
                    // until it followed, a long list was navigable only for the
                    // eight rows that happened to be on screen.
                    // RowSelection, because at Element depth the list under
                    // the stick is the element list -- see FightMenuState.
                    int row = Wrap(_menu.RowSelection + delta, rows.Count);
                    OnRowHovered(row);
                    ScrollSubmenuRowIntoView(row);
                    break;

                case MenuDepth.Target:
                    // DELTA HERE IS ALREADY IN CycleTarget's OWN CONVENTION
                    // (+1 is one slot deeper) -- this case is reached only by
                    // a direct call (every test in this family, plus the
                    // Back button's own callers) and by the explicit
                    // IFightNavigationTarget.MoveFocus wrapper below, which
                    // does the one sign flip a raw vertical press needs
                    // before it gets here. See CycleTarget's own header for
                    // why a second, ally-only flip lives in InspectMove
                    // instead of here.
                    CycleTarget(delta);
                    break;
            }
        }

        // ONE CYCLING RULE FOR BOTH TARGET RACKS AND BOTH AXES (the owner's
        // 2026-09-19 follow-up: "selecting different mobs with gamepad goes
        // with up down, but it should work with left right"). `delta` is
        // DEPTH-SPACE: +1 steps one slot DEEPER (the living/pickable list's
        // next index -- further right on stage for the enemy rack, further
        // left for the mirrored ally one), -1 steps one slot nearer.
        //
        // MoveFocus' own Target case above calls this with delta UNCHANGED
        // (preserving every existing direct-call test's literal indices --
        // FightGamepadNavigationTests and AllyTargetPickerTests both drive
        // MoveFocus directly, bypassing the interface wrapper entirely) AND
        // with WrapFromNoHover's OLDER "first press is index 0" no-hover
        // rule, via the shared FromPad=false core below. InspectMove and the
        // IFightNavigationTarget.MoveFocus wrapper are the two pad entry
        // points and go through CycleTargetFromPad instead, which resolves
        // "nothing hovered" to enemy/plate 0 before delta is applied -- see
        // that method's own header for why a pad press needs a different
        // no-hover rule than a direct call does. Both still TRANSLATE an
        // axis's raw sign into this method's "+1 is deeper" convention
        // themselves, because the two axes disagree with each other about
        // what "+1" already means: vertical arrives through
        // IFightNavigationTarget.MoveFocus, which flips ONE sign for both
        // racks (Y is not mirrored, so "up is deeper" is true on both);
        // horizontal arrives through InspectMove, which flips per SIDE
        // instead (X is mirrored, so "right is deeper" is true only for the
        // enemy rack -- see InspectMove's own header).
        private void CycleTarget(int delta) => CycleTarget(delta, fromPad: false);

        // THE PAD-ONLY ENTRY POINT (2026-09-19, the gate's own find): a pad
        // player's first press at Target depth has to read as one step FROM
        // enemy/plate 0, not as CycleTarget's own "first press lands ON
        // index 0" rule -- FocusedElement/ConfirmFocus already treat no
        // hover as enemy 0 (the marker sits there before any press, and
        // Submit would hit it), so leaving WrapFromNoHover's no-hover branch
        // in place here made the first Up/Right press look like it did
        // nothing at all. Used only by the IFightNavigationTarget.MoveFocus
        // wrapper and InspectMove's Target branch -- both pad paths, never a
        // direct call.
        private void CycleTargetFromPad(int delta) => CycleTarget(delta, fromPad: true);

        private void CycleTarget(int delta, bool fromPad)
        {
            // THE SIDE BEING PICKED, not always the monsters. The cycle
            // itself is identical on both racks -- the living and legal
            // candidates, in screen order, wrapping -- so the only thing the
            // side decides is which list is walked and which cursor
            // remembers where the stick got to.
            if (_menu.Side == TargetSide.Allies)
            {
                var allies = PickableAllyPlates();
                if (allies.Count == 0) return;

                int atAlly = allies.IndexOf(_hoveredAllyIndex);
                if (fromPad && atAlly < 0) atAlly = 0;
                OnAllyHovered(allies[WrapFromNoHover(atAlly, delta, allies.Count)]);
                return;
            }

            var enemies = Enemies;
            var living = new List<int>();
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].IsAlive) living.Add(i);
            }

            if (living.Count == 0) return;
            int at = living.IndexOf(_hoveredEnemyIndex);
            if (fromPad && at < 0) at = 0;
            int next = WrapFromNoHover(at, delta, living.Count);
            OnEnemyHovered(living[next]);
        }

        // The PLATE indices an ally pick will actually accept, in column
        // order. Built from EligibleAllies rather than from "alive", because
        // a gift the caster cannot give himself must not be a stop on the
        // stick's way round -- the same list the reticles are lit from.
        private List<int> PickableAllyPlates()
        {
            var pickable = new List<int>();
            var eligible = _session?.EligibleAllies(_session.Current, SelectedSkill());
            if (eligible == null || pcPlates == null) return pickable;

            for (int i = 0; i < pcPlates.Length; i++)
            {
                var member = PartyMemberOnPlate(i);

                // AN ALLY ALREADY PICKED IS NOT PICKABLE (plan 1.12), and this
                // is the line that keeps the PAD path reachable rather than
                // merely tidy. Submit with nothing hovered presses
                // `pickable[0]`; without this the second press of a two-pick
                // cast would land on the ally the first one took, be refused
                // as "already going", and the cast could never be completed
                // from a controller at all -- a whole input method quietly
                // unable to cast one spell.
                //
                // ONE RULE, THREE READERS: this list is what Submit presses,
                // what the stick cycles through (CycleTarget) and what the
                // focus marker stands on, so all three skip the taken ally
                // together.
                if (member != null && eligible.Contains(member) && !_menu.HasPicked(member)) pickable.Add(i);
            }

            return pickable;
        }

        // ---- inspecting, which costs no verb (the owner, 2026-09-19) ----------
        //
        // "In a fight, how does a player hover over a mob or a PC to check
        // (de)buffs? (gamepad)" -- and until this, they could not. Fight's
        // three depths are the verb column, a submenu list and a target rack,
        // and only the last one stands on an actor, so the pad reached a
        // monster only by first pressing A on ATTACK or a spell. Reading a
        // monster is not choosing to hit it.
        //
        // NOT A FOURTH MenuDepth. MenuDepth belongs to FightMenuState, which
        // is the domain model of what the player is COMMITTING to -- every
        // one of its values is a step of one decision, and Back() walks them.
        // Inspecting commits to nothing, resolves nothing, and is not a step
        // back from anything. It is a pad-only reading position ON TOP of
        // Root, so it is two fields here rather than a value in a domain enum
        // that would then have to be given a meaning on every mouse path,
        // every Back(), and in FightHudModel.
        private bool _inspecting;
        private bool _characterSelecting;
        private bool _inspectingIntent;
        // Target selection needs its own intent state. Root inspection also
        // uses _inspectingIntent and is refreshed with the actor ring; sharing
        // that flag made a pending target lose its return point between input
        // frames even while its intent icon still held focus.
        private int _targetIntentEnemyIndex = -1;
        private bool IsTargetIntentInspection =>
            _menu.Depth == MenuDepth.Target
            && _menu.Side == TargetSide.Enemies
            && _targetIntentEnemyIndex >= 0;

        // WHO THE PAD WAS LAST READING, kept across a leave so that stepping
        // back onto the row returns to the same actor rather than to the
        // front of it. Never trusted on its own: every read resolves it
        // against the ring built from the CURRENT fight (InspectedStop), so a
        // combatant that has since died, or that belonged to the last
        // encounter entirely, simply is not found and inspect reads as off.
        // That is what makes this state safe to hold without a hook in
        // ResetStagePresentation, which this file does not own.
        private CombatantState _inspectCursor;

        // Read-only window for GamepadNavigationTests, same reason and same
        // shape as FocusedVerbForTest -- the ring's own actor is what a
        // character-select test needs to name (FocusName only proves WHICH
        // FIGURE the marker sits on, not which combatant it is; a test
        // walking Bjorn/Shawn/Odette needs both).
        public string InspectedActorNameForTest => _inspectCursor?.Name;

        // INSPECT ONLY EVER EXISTS ON TOP OF ROOT, and the flag alone cannot
        // say so: a MOUSE click on a verb while the pad was reading a monster
        // moves the depth out from under it without the pad pressing
        // anything. Asked here rather than trusted, so the stick goes
        // straight back to walking whatever list that click opened. Every
        // reader clears the stale flag on its way past.
        private bool IsInspecting => _inspecting && _menu.Depth == MenuDepth.Root;

        // One stop on the ring: the actor, and the two things needed to draw
        // a marker on it. The side decides which of the two racks names it,
        // and they name it differently -- an enemy by index, an ally by PLATE
        // (see OnEnemyPressed's own header on the three indexing schemes).
        private readonly struct InspectStop
        {
            public readonly CombatantState Actor;
            public readonly bool IsAlly;
            public readonly int Index;

            public InspectStop(CombatantState actor, bool isAlly, int index)
            {
                Actor = actor;
                IsAlly = isAlly;
                Index = index;
            }
        }

        // THE RING, IN SCREEN ORDER: every living monster, then every living
        // squadmate. One list rather than two rows switched by Left/Right,
        // because the racks are already built by two different index schemes
        // and a second "which rack am I on" cursor would be a third one -- the
        // combatant itself is the only name both sides agree on, and it is
        // what _inspectCursor holds.
        //
        // Allies BY PLATE, not by party index: PartyMemberOnPlate and
        // AllyFigureOrPlate both speak plates, and the column follows a Move
        // (AUDIT #144), so walking the party list would step the stick in an
        // order the screen does not show.
        //
        // Rebuilt per call, the same way MoveFocus' own target branch and
        // PickableAllyPlates already rebuild theirs: the alternative is a
        // cached list that a death, a Move or a new encounter can invalidate
        // between frames.
        private List<InspectStop> InspectRing()
        {
            var ring = new List<InspectStop>();
            ring.AddRange(LivingEnemyStops());
            ring.AddRange(PartyInspectRing());
            return ring;
        }

        // Living enemies, FRONT FIRST -- FightStageAnchors' enemy rack runs
        // outward unmirrored (Near.X 300 -> Far.X 660), so ascending index
        // IS ascending screen X here already; nothing to reverse, unlike the
        // party below.
        private List<InspectStop> LivingEnemyStops()
        {
            var ring = new List<InspectStop>();
            var enemies = Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] != null && enemies[i].IsAlive) ring.Add(new InspectStop(enemies[i], false, i));
            }

            return ring;
        }

        private List<InspectStop> PartyInspectRing()
        {
            var ring = new List<InspectStop>();
            if (pcPlates == null) return ring;

            for (int i = 0; i < pcPlates.Length; i++)
            {
                var member = PartyMemberOnPlate(i);
                if (member != null && member.IsAlive) ring.Add(new InspectStop(member, true, i));
            }

            return ring;
        }

        // ---- the character-select ring (owner playtest, 2026-09-23) --------------
        //
        // "While picking a target, left/right should walk the formation
        // spatially -- party on the left, mobs on the right -- and never
        // jump to the command menu." Y/Triangle (EnterCharacterSelect)
        // starts on the front party member (PartyInspectRing()[0], per
        // CharacterSelectStartsOnTheFrontLivingPartyMember), and until now
        // Left/Right there only ever walked PartyInspectRing() -- so Left
        // off the front member fell all the way through StepInspectRing's
        // -1 and landed back on the verb column (the bug: "LEFT goes back
        // to the Attack command button"), and Right could never leave the
        // party at all ("RIGHT goes to the player party" -- nowhere new to
        // go, because the enemies were never part of this ring).
        //
        // ONE FLAT LIST IN SCREEN ORDER, LEFT TO RIGHT: the party, DEEPEST
        // FIRST, then the enemies, FRONT FIRST. The party has to reverse
        // because its rack is MIRRORED (SlotOffset negates X, same reason
        // InspectMove's own Target-depth header gives) -- the front party
        // member (plate 0) sits nearest the CENTRE of the screen, so it is
        // the party's own RIGHTMOST member, one step from the first enemy.
        // Walking this list forward (Right) from the front party member
        // therefore lands on the front enemy, and walking it backward
        // (Left) from the front party member lands on plate 1 -- the
        // second party member, "party position 2" in the owner's own words
        // -- exactly the two presses the playtest pinned.
        private List<InspectStop> CharacterSelectRing()
        {
            var party = PartyInspectRing();
            var ring = new List<InspectStop>(party.Count + Enemies.Count);
            for (int i = party.Count - 1; i >= 0; i--) ring.Add(party[i]);
            ring.AddRange(LivingEnemyStops());
            return ring;
        }

        // THE RING'S ONE RULE, as arithmetic, so the two callers below cannot
        // disagree about it and a test can pin it without a scene:
        //
        //   the verb column is the stop BEFORE the first monster. Stepping
        //   backwards off the front of the ring lands there; stepping forwards
        //   off the end wraps round to the front. One direction always gets
        //   you home, the other always gets you round.
        //
        // -1 means "off the front" -- the verb column -- and is why this
        // returns an int rather than wrapping like FightController.Wrap does.
        // An empty ring answers -1 for either direction: there is nobody to
        // stand on, so the only legal position is back on the verbs.
        public static int StepInspectRing(int at, int delta, int count)
        {
            if (count <= 0) return -1;

            int next = at + (delta > 0 ? 1 : -1);
            if (next < 0) return -1;
            return next >= count ? 0 : next;
        }

        // THE CHARACTER-SELECT RING'S OWN RULE -- CLAMPED, NOT LEFT/WRAPPED,
        // and that is the whole fix (owner playtest, 2026-09-23: "never jump
        // to the command menu"). StepInspectRing's -1-means-the-verb-column
        // convention exists FOR the plain Root inspect ring on purpose (its
        // own header: "the verb column is the stop before the first
        // monster") -- Y/Triangle's character-select ring is a different
        // control with a different contract: once on it, every press has to
        // land on SOMEBODY, because the whole point (CharacterSelectRing's
        // own header) is that Left/Right now reaches the enemies too, not
        // just the party. Running off either end simply stops at that end.
        public static int ClampInspectRing(int at, int delta, int count)
        {
            if (count <= 0) return -1;

            int next = at + (delta > 0 ? 1 : -1);
            if (next < 0) return 0;
            return next >= count ? count - 1 : next;
        }

        // The horizontal axis' whole meaning in a fight. At a submenu it
        // still does what it always did, which is nothing -- a skill or
        // item LIST is walked by Up/Down alone. At Root it walks the inspect
        // ring, unchanged below. AT TARGET DEPTH (the owner's 2026-09-19
        // follow-up, "selecting different mobs with gamepad goes with up
        // down, but it should work with left right") it now cycles the rack
        // exactly like Up/Down do, through the same CycleTargetFromPad
        // IFightNavigationTarget.MoveFocus's own Target branch uses -- see
        // that method's own header for the shared "+1 is deeper" convention
        // this translates into.
        internal void InspectMove(int delta)
        {
            if (_menu.Depth == MenuDepth.Target)
            {
                if (IsTargetIntentInspection) SetTargetIntentInspection(false);

                // THE ENEMY RACK RUNS OUTWARD, UNMIRRORED (FightStageAnchors:
                // Near.X 300 -> Far.X 660), so Right (raw +1, straight off
                // ProcessFight) already points at "further right on stage,
                // one slot deeper" -- CycleTarget's own convention -- and
                // needs no flip.
                //
                // THE ALLY RACK IS MIRRORED (SlotOffset negates X for the
                // party), so ITS depth runs the other way on screen:
                // Near.X 320 -> Far.X 810 becomes on-screen -320 -> -810,
                // meaning the FARTHER ally sits FURTHER LEFT. Right has to
                // mean "toward the near end" there to keep pointing
                // rightward on screen -- a NEGATIVE step in CycleTarget's
                // depth ordering -- so this is the one place the two racks
                // disagree and the only side that gets flipped.
                //
                // FromPad, not CycleTarget directly -- this is a pad press
                // reaching Target depth exactly the way IFightNavigationTarget.
                // MoveFocus's own Target branch does, so a fresh pick needs
                // the same "nothing hovered reads as enemy/plate 0" fix (that
                // wrapper's own header explains why the plain no-hover rule
                // is wrong here now).
                CycleTargetFromPad(_menu.Side == TargetSide.Allies ? -delta : delta);
                return;
            }

            if (_menu.Depth != MenuDepth.Root) return;

            if (IsInspecting)
            {
                if (_inspectingIntent) SetIntentInspection(false);
                InspectStep(delta);
                return;
            }

            // Left at the verb column has nowhere to go: the row is to the
            // right of it, and entering it on a press that means "back" would
            // make Left and Right both mean the same thing from here.
            if (delta <= 0) return;

            var ring = InspectRing();
            if (ring.Count == 0) return;

            int at = IndexOnRing(ring, _inspectCursor);
            _inspectCursor = ring[at < 0 ? 0 : at].Actor;
            _inspecting = true;
            RefreshUi();
        }

        private void InspectStep(int delta)
        {
            // A cursor that is no longer on the ring (its owner died while
            // being read) is picked back up at the front rather than dropped:
            // the press the player made was "move", and the nearest honest
            // answer to it is the first monster.
            if (_characterSelecting)
            {
                var spatial = CharacterSelectRing();
                if (spatial.Count == 0)
                {
                    LeaveInspect();
                    return;
                }

                int spatialAt = IndexOnRing(spatial, _inspectCursor);
                int spatialNext = ClampInspectRing(spatialAt < 0 ? 0 : spatialAt, delta, spatial.Count);
                _inspectCursor = spatial[spatialNext].Actor;
                RefreshUi();
                return;
            }

            var ring = InspectRing();
            int at = IndexOnRing(ring, _inspectCursor);
            int next = StepInspectRing(at < 0 ? 0 : at, delta, ring.Count);

            if (next < 0)
            {
                LeaveInspect();
                return;
            }

            _inspectCursor = ring[next].Actor;
            RefreshUi();
        }

        private void InspectVerticalStep(int delta)
        {
            if (_inspectingIntent)
            {
                if (delta > 0) SetIntentInspection(false);
                return;
            }

            if (delta < 0 && InspectedStop(out var current) && !current.IsAlly)
            {
                SetIntentInspection(true);
                return;
            }

            InspectStep(delta);
        }

        private void SetTargetIntentInspection(bool shown)
        {
            int index = shown
                ? (_hoveredEnemyIndex >= 0 ? _hoveredEnemyIndex : FirstLivingEnemyIndex())
                : _targetIntentEnemyIndex;

            if (shown)
            {
                _targetIntentEnemyIndex = index;
            }
            else
            {
                if (_targetIntentEnemyIndex >= 0) _hoveredEnemyIndex = _targetIntentEnemyIndex;
                _targetIntentEnemyIndex = -1;
            }

            ShowIntentTooltip(index, shown);
        }

        void IFightNavigationTarget.EnterCharacterSelect()
        {
            if (_session == null || _isBusy || _menu.Depth != MenuDepth.Root) return;

            if (_characterSelecting && IsInspecting)
            {
                LeaveInspect();
                return;
            }

            var party = PartyInspectRing();
            if (party.Count == 0) return;

            SetIntentInspection(false);
            _characterSelecting = true;
            _inspectCursor = party[0].Actor;
            _inspecting = true;
            RefreshUi();
        }

        private void SetIntentInspection(bool shown)
        {
            _inspectingIntent = shown;
            if (!shown)
            {
                if (intentTooltip != null) intentTooltip.SetShown(false);
                return;
            }

            if (!InspectedStop(out var stop) || stop.IsAlly)
            {
                _inspectingIntent = false;
                if (intentTooltip != null) intentTooltip.SetShown(false);
                return;
            }

            ShowIntentTooltip(stop.Index, true);
        }

        // True when there was an inspection to leave, so the caller can spend
        // the press on it. _inspectCursor is deliberately NOT cleared -- it is
        // the "or the remembered one" half of re-entering.
        private bool LeaveInspect()
        {
            // A flag left standing after the depth moved under it is dropped
            // here and the press falls through to whatever the new depth
            // wants -- it is not an inspection to spend a Back on.
            if (!IsInspecting)
            {
                _inspecting = false;
                return false;
            }

            _inspecting = false;
            _characterSelecting = false;
            SetIntentInspection(false);
            RefreshUi();
            return true;
        }

        // IS THE PAD PERCHED ON THIS PARTICULAR ACTOR? Through InspectedStop,
        // the same resolver FocusedElement and FocusedActor both go through,
        // so the figure that comes UP and the figure the marker lands ON
        // cannot be two different answers -- which is exactly what asking
        // IsInspecting at one call site and InspectedStop at the other would
        // have allowed the moment one of them grew a condition the other did
        // not.
        //
        // Per actor rather than per side, so only the ONE figure being read
        // is raised. A whole rack of invisible rectangles coming up at once
        // is more of the stage changing than the player asked for.
        private bool IsPerchedOn(CombatantState actor) =>
            actor != null && InspectedStop(out var stop) && ReferenceEquals(stop.Actor, actor);

        // A FIGURE TARGET HAS TWO REASONS TO BE ON SCREEN AND ONLY ONE OF
        // THEM TAKES CLICKS.
        //
        // `live` is the old reason and the only one that was ever there: a
        // pick is open and this figure is one of its marks. `perched` is the
        // new one: the pad is inspecting and the marker has to stand
        // somewhere. Without it the marker fell back to the plate in the
        // corner while PlaceStatusBox drew the box under the figure on the
        // battlefield, which is one actor indicated at two ends of the
        // screen.
        //
        // RAYCASTING FOLLOWS `live` ALONE, and that is the whole safety of
        // this. A hit area is a NoChrome Button -- a transparent Image that
        // raycasts against its RECT (FightScreen.BuildStage, UiEmitter.
        // EmitButton) -- so a perched one left raycastable would be exactly
        // the rectangle over the battlefield eating clicks that BuildStage's
        // own header warns about, starting with the intent icon underneath
        // it, which is the MOUSE's way to inspect that same monster
        // (FightController.StageVisuals). A raycast it never receives cannot
        // reach OnPointerClick, so onClick cannot fire and no pick can start
        // from a perch -- which is the same answer Submit gets while
        // inspecting, arrived at one layer lower.
        //
        // NOT WRITTEN AS "live ? ... : perched": the two are mutually
        // exclusive today only because picking is Target depth and
        // inspecting is Root, and a comment asserting exactly that kind of
        // exclusivity is what AUDIT.md #169 turned out to be.
        private void ShowFigureTarget(Button hitArea, bool live, bool perched)
        {
            if (hitArea == null) return;

            hitArea.gameObject.SetShown(live || perched);

            if (hitArea.targetGraphic != null) hitArea.targetGraphic.raycastTarget = live;
        }

        private static int IndexOnRing(List<InspectStop> ring, CombatantState actor)
        {
            if (actor == null) return -1;

            for (int i = 0; i < ring.Count; i++)
            {
                if (ReferenceEquals(ring[i].Actor, actor)) return i;
            }

            return -1;
        }

        // WHERE THE PAD IS STANDING WHILE INSPECTING, resolved against the
        // live ring every time it is asked. Every way inspect can go stale
        // answers false here rather than needing its own teardown hook: a
        // verb opened a submenu (depth moved off Root), a round resolved
        // (_isBusy, and AfterResolution clears the flag outright), the actor
        // died, or a whole new encounter was bound onto this controller and
        // the remembered combatant belongs to the fight before it.
        private bool InspectedStop(out InspectStop stop)
        {
            stop = default;
            if (!IsInspecting || _session == null || _isBusy || _session.IsOver) return false;

            var ring = _characterSelecting ? CharacterSelectRing() : InspectRing();
            int at = IndexOnRing(ring, _inspectCursor);
            if (at < 0) return false;

            stop = ring[at];
            return true;
        }

        public void ConfirmFocus()
        {
            switch (_menu.Depth)
            {
                case MenuDepth.Root:
                    OnVerbPressed(_focusedVerb);
                    break;

                case MenuDepth.Element:
                case MenuDepth.Sub:
                    // Nothing hovered yet (a branch just opened, the stick has
                    // not moved) presses row 0 rather than doing nothing --
                    // the same "the first press should work" reasoning
                    // MoveFocus' Wrap already leans on.
                    OnRowPressed(_menu.RowSelection >= 0 ? _menu.RowSelection : 0);
                    break;

                case MenuDepth.Target:
                    // SAME "the first press should work" rule on both racks:
                    // nothing hovered yet confirms the first candidate rather
                    // than doing nothing.
                    if (_menu.Side == TargetSide.Allies)
                    {
                        if (_hoveredAllyIndex >= 0)
                        {
                            OnAllyPlatePressed(_hoveredAllyIndex);
                            break;
                        }

                        var pickable = PickableAllyPlates();
                        if (pickable.Count > 0) OnAllyPlatePressed(pickable[0]);
                        break;
                    }

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

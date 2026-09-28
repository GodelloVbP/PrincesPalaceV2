using System.Collections.Generic;
using PrincesPalace.Domain.Stats;

namespace PrincesPalace.Domain.Combat.Session
{
    // How deep into the command menu the player is.
    public enum MenuDepth
    {
        Root,
        Sub,

        // ONLY A SKILL THAT OFFERS A CHOICE STOPS HERE (ResolvedSkill.
        // HasElementChoice). Every other command's path is exactly what it has
        // always been -- Root to Sub to Target, or an instant resolve for
        // Self/Party -- because nothing else can enter this depth.
        //
        // BETWEEN Sub AND Target rather than after it: the element decides what
        // the cast IS, and the detail card has to be able to describe the thing
        // before the player is asked to point it at something.
        Element,

        Target,
    }

    // WHICH RACK IS BEING POINTED AT while the menu sits at Target depth.
    //
    // A SIDE, NOT A SET OF PLATES. The view owns which plates exist and how
    // they are drawn; what it cannot work out for itself -- without re-reading
    // the selected skill's targeting in four places and getting a different
    // answer in one of them -- is which half of the screen this particular
    // pick belongs to. That is one fact, so it is one field.
    public enum TargetSide
    {
        // The default in every sense: it is what ATTACK and every
        // SingleEnemy/AllEnemies cast enters, and it is what Reset returns
        // to, so a state that never says otherwise behaves exactly as it did
        // before allies could be picked at all.
        Enemies,

        Allies,
    }

    // Which verb's branch is open.
    public enum MenuBranch
    {
        None,
        Attack,
        Skill,
        Item,

        // Replaced HOLD BACK on the fourth verb row. It NESTS -- two rows,
        // FORWARD and BACK -- where Hold Back resolved on the press, so it is
        // a Skill/Item-shaped branch rather than an Attack-shaped one.
        Move,
    }

    // The command menu, as state rather than as a scatter of controller fields.
    //
    // MOVED OUT OF THE VIEW, unlike the pending SELECTION it tracks. That looks
    // like a contradiction with FightSession's own note ("pending selection is
    // view state and stays in Core") and is not: what stays in Core is the
    // SESSION not knowing about it -- a session that also tracked what was
    // highlighted could disagree with the view about whose turn it was, which is
    // the desync class v1 never had. This type is still the view's, it just is
    // not a MonoBehaviour, so its transitions can be tested in milliseconds.
    //
    // Plain state, not a stack. The graph has exactly five edges and a stack
    // would let it reach positions the design does not have -- v1's own note,
    // preserved because it is the reason this is three fields.
    public sealed class FightMenuState
    {
        public MenuDepth Depth { get; private set; } = MenuDepth.Root;
        public MenuBranch Branch { get; private set; } = MenuBranch.None;

        // Which submenu row the detail column is describing. Set by HOVERING as
        // well as by clicking: selection-by-hover is what makes the detail
        // column feel instant, and confirming is a separate click.
        //
        // THE SUB ROW, AND IT STAYS THE SUB ROW through Element and Target
        // depth. Four call sites in FightController read this to find the
        // selected SKILL -- its reach, its targeting, its queue push, its
        // detail card -- so reusing the field for the element row would have
        // made every one of them read the wrong list the moment an element
        // skill existed. The element row has its own index below.
        public int Selection { get; private set; } = -1;

        // Which ELEMENT row the detail column is describing, at Element depth.
        // Separate from Selection for the reason above, and cleared whenever
        // the element flow is left.
        public int ElementSelection { get; private set; } = -1;

        // The element the player committed to at Element depth, carried into
        // Target depth and handed to FightSession.CastSkill by the enemy click.
        // Null at every other depth and for every other command.
        //
        // REMEMBERED RATHER THAN RE-DERIVED from ElementSelection later: the
        // row list is rebuilt on every repaint, and an index into a list that
        // no longer exists is precisely the disagreement the split above
        // avoids once already.
        public DamageType? ChosenElement { get; private set; }

        // Which row of the list CURRENTLY on screen is lit. One row pool serves
        // the skill list and the element list, so the view asks this rather
        // than picking between the two indices itself.
        public int RowSelection => Depth == MenuDepth.Element ? ElementSelection : Selection;

        public bool IsOpen => Branch != MenuBranch.None;

        // Driven by THE BRANCH ALONE, never by a second "can the player act"
        // test computed alongside it.
        //
        // v1 asked twice and the two answers disagreed: opening a branch guarded
        // on the SIMULATION's turn while the redraw read the DISPLAYED one, and
        // during playback those are different actors. The branch would be set
        // and the column would stay hidden, which made the basic spell
        // unreachable in exactly the states where playback was still catching
        // up. The branch is only ever set through a guarded path and Reset
        // clears it on every resolution, so it is already the authority.
        // THE LIST FOLDS ONCE SOMETHING IS PICKED.
        //
        // It used to stay up through targeting, so that the player could see
        // what they had chosen. The detail column says that, in more words, and
        // the target prompt names it outright -- while the list itself was
        // sitting over the middle of the battlefield, which is the half of the
        // screen the player has just been asked to point at.
        //
        // ATTACK was already excluded, because it skips the submenu on the way
        // in. That exception is now the rule.
        // Element depth keeps the column up: it is the same column showing a
        // different list, not a second piece of chrome.
        public bool SubmenuOpen => IsOpen && (Depth == MenuDepth.Sub || Depth == MenuDepth.Element);

        // The detail column is populated for ATTACK too, through a synthetic
        // "Strike" entry: a verb that jumps straight to targeting would
        // otherwise be the one command with nothing to read about it.
        public bool DetailOpen => IsOpen;

        public bool IsTargeting => Depth == MenuDepth.Target;

        // WHICH RACK THE PICK IS ON. Meaningless outside Target depth and
        // reset to Enemies whenever the menu leaves it, so a reader that
        // forgets to check IsTargeting first gets the old answer rather than
        // a stale ally pick.
        public TargetSide Side { get; private set; } = TargetSide.Enemies;

        // The two questions the view actually asks, named rather than spelled
        // out at each of the half-dozen call sites that would otherwise write
        // `IsTargeting && Side == ...` and eventually write one of them wrong.
        // They are mutually exclusive by construction: exactly one rack is
        // live at a time, which is the rule the plates are painted from.
        public bool IsPickingEnemy => IsTargeting && Side == TargetSide.Enemies;
        public bool IsPickingAlly => IsTargeting && Side == TargetSide.Allies;

        // ---- two picks (plan 1.12) ------------------------------------------
        //
        // A REQUIRED PICK COUNT, NOT A SECOND DEPTH. Palace Passage asks for
        // two allies and every other command asks for one; the question being
        // asked is the same question twice, so it is the same depth twice.
        // A MenuDepth.SecondTarget would have needed its own Back edge, its
        // own prompt, its own painting rule and its own entry from Element
        // depth -- four places for the two to disagree about what the player
        // is looking at.
        //
        // AND THE PICKS LIVE HERE, not on the session. Nothing is written to
        // the board until the last one is submitted, so a cancel at pick 1
        // has nothing to clean up: the whole of "atomic cancellation" is that
        // there is no half-cast anywhere to unwind. The session holds no
        // pending-pick state between calls and is handed the finished list.
        public int RequiredPicks { get; private set; } = 1;

        private readonly List<CombatantState> _picks = new List<CombatantState>();

        public IReadOnlyList<CombatantState> Picks => _picks;

        // How many the player has committed so far -- "1 of 2" on the prompt.
        public int PickCount => _picks.Count;

        public bool NeedsAnotherPick => IsTargeting && _picks.Count < RequiredPicks;

        // "Already picked" is one of the three illegal-ally cases (the other
        // two, dead and not on my side, belong to the session), and it is the
        // only one that exists solely because a cast is half made.
        public bool HasPicked(CombatantState candidate) => candidate != null && _picks.Contains(candidate);

        // ---- a seat as the last pick (PLAN_BELLWETHER_KIT 1.2/3.6) -------------
        //
        // Palace Passage's second pick is a DESTINATION SEAT, occupied or
        // empty. An occupied seat is recorded exactly as before -- the
        // occupant goes into Picks through RecordPick, so the cast and every
        // message are today's two-ally swap. Only an EMPTY seat, which has
        // nobody to put in a combatant list, is held here instead.
        //
        // The caller's read of the skill (SkillEffects.LastPickIsSeat), for
        // the reason RequiredPicks is: this type holds no content.
        public bool LastPickIsSeat { get; private set; }

        // The empty seat the last pick named, -1 while none is held. Set only
        // by RecordSeatPick; a held seat always completes the cast.
        public int DestinationSeat { get; private set; } = -1;

        // THE LAST PRESS OF A SEAT-DESTINATION CAST IS BEING ASKED FOR: the
        // rack the view offers now is the three seats (minus the traveller's
        // own), empty ones included, not the party plates alone.
        public bool IsPickingSeat =>
            IsPickingAlly && LastPickIsSeat && _picks.Count == RequiredPicks - 1 && DestinationSeat < 0;

        // Records an EMPTY seat as the last pick. Refused (nothing recorded)
        // outside a seat pick or for a seat number that does not exist; an
        // occupied seat goes through RecordPick with its occupant instead.
        public bool RecordSeatPick(int seat, out bool complete)
        {
            complete = false;
            if (!IsPickingSeat || seat < 0) return false;

            DestinationSeat = seat;
            complete = true;
            return true;
        }

        // Records one pick and says whether that was the LAST one, which is
        // the caller's signal to commit. Returns false without recording for
        // a null, for a repeat, or for a cast that is already full -- a
        // refusal to record, never a silent overwrite.
        public bool RecordPick(CombatantState target, out bool complete)
        {
            complete = false;
            if (!IsTargeting || target == null || _picks.Contains(target) || _picks.Count >= RequiredPicks
                || DestinationSeat >= 0)
            {
                return false;
            }

            _picks.Add(target);
            complete = _picks.Count >= RequiredPicks;
            return true;
        }

        // Undoes the most recent pick and stays where it is. Cancel's first
        // step at Target depth, and the reason Back() below never leaves the
        // depth while a pick is still held: the owner's rule is that Cancel
        // steps back ONE level, and a recorded pick is a level. A held seat is
        // the most recent pick whenever there is one.
        public bool DropLastPick()
        {
            if (DestinationSeat >= 0)
            {
                DestinationSeat = -1;
                return true;
            }

            if (_picks.Count == 0) return false;

            _picks.RemoveAt(_picks.Count - 1);
            return true;
        }

        // ATTACK does not nest -- it jumps straight to picking a mark.
        public void OpenAttack()
        {
            Branch = MenuBranch.Attack;
            Depth = MenuDepth.Target;
            Side = TargetSide.Enemies;
            Selection = -1;
            ForgetPicks();
            ForgetElement();
        }

        public void OpenBranch(MenuBranch branch)
        {
            if (branch == MenuBranch.None) return;
            if (branch == MenuBranch.Attack)
            {
                OpenAttack();
                return;
            }

            Branch = branch;
            Depth = MenuDepth.Sub;
            Selection = -1;
            ForgetPicks();
            ForgetElement();
        }

        // Hovering or arrowing onto a row. Returns false when nothing changed,
        // so a caller can skip a redraw rather than repaint on every mouse move
        // across one row.
        public bool Select(int index, int manaCost)
        {
            if (Depth == MenuDepth.Element)
            {
                if (index == ElementSelection) return false;

                ElementSelection = index;
                return true;
            }

            if (Depth != MenuDepth.Sub || index == Selection) return false;

            Selection = index;
            return true;
        }

        // Committing a row: the submenu picks, and targeting is what confirms.
        //
        // THE SIDE IS THE CALLER'S READ OF THE SKILL, the same way whether a
        // skill asks for an element is (EnterElementChoice's own note): this
        // type holds no content and asks no content question. Defaulted to
        // Enemies so every existing call site keeps its meaning unchanged.
        //
        // `requiredPicks` is the CALLER'S read of the skill too
        // (SkillEffects.PicksRequired), for the same reason the side is:
        // this type holds no content. Defaulted to 1 so every existing call
        // site keeps its meaning unchanged.
        //
        // `lastPickIsSeat` likewise (SkillEffects.LastPickIsSeat), defaulted
        // false so every existing call site keeps its meaning.
        public void EnterTargeting(TargetSide side = TargetSide.Enemies, int requiredPicks = 1,
            bool lastPickIsSeat = false)
        {
            if (!IsOpen) return;
            Depth = MenuDepth.Target;
            Side = side;
            RequiredPicks = requiredPicks < 1 ? 1 : requiredPicks;
            LastPickIsSeat = lastPickIsSeat;
            DestinationSeat = -1;
            _picks.Clear();
        }

        // Committing a SKILL that asks which element it is. WHETHER a skill
        // asks is the caller's read of the skill (ResolvedSkill.
        // HasElementChoice) -- this type holds no content and asks no content
        // question, the same way it does not know what a skill costs.
        public void EnterElementChoice()
        {
            if (!IsOpen) return;
            Depth = MenuDepth.Element;
            ForgetElement();
        }

        // Committing an element row, which is what carries the menu into
        // targeting.
        public void ChooseElement(DamageType element, TargetSide side = TargetSide.Enemies)
        {
            if (!IsOpen) return;
            ChosenElement = element;
            Depth = MenuDepth.Target;
            Side = side;

            // An element skill is SingleEnemy and asks for one pick; said
            // explicitly rather than left to whatever the last cast set,
            // because a stale two-pick count here would stop a plain spell
            // from ever committing.
            ForgetPicks();
        }

        // One step back up the graph. Returns false at the root, where there is
        // nothing to back out of -- the caller uses that to decide whether the
        // press was consumed.
        public bool Back()
        {
            switch (Depth)
            {
                case MenuDepth.Target:
                    // A RECORDED PICK IS A LEVEL, and Cancel steps back one
                    // level (the owner's gamepad rule, 2026-09-18). So the
                    // first Cancel at picks 1/2 drops that pick and stays on
                    // the rack; only a Cancel with nothing held leaves the
                    // depth. Nothing was spent to record it, so nothing is
                    // refunded here -- see RequiredPicks' own header.
                    if (DropLastPick()) return true;

                    // THE SIDE IS FORGOTTEN ON THE WAY OUT, unconditionally
                    // and before the branching below -- every arm of it
                    // leaves Target depth, and a Side left pointing at the
                    // party would have the next repaint light the party
                    // plates for a menu that is no longer asking anything.
                    // Backing out of an ally pick is otherwise identical to
                    // backing out of an enemy one, which is the contract.
                    Side = TargetSide.Enemies;
                    ForgetPicks();

                    // BACK RETRACES THE WAY IN, whichever way that was. An
                    // element skill came through Element depth, so that is
                    // where it lands -- with the choice cleared, because
                    // standing on the element list still holding the element
                    // just undone would make the detail card describe a
                    // decision the player has taken back.
                    if (ChosenElement.HasValue)
                    {
                        Depth = MenuDepth.Element;
                        ChosenElement = null;
                        return true;
                    }

                    // ATTACK skipped the submenu on the way in, so it skips it
                    // on the way out. Anything else lands back on its list.
                    Depth = Branch == MenuBranch.Attack ? MenuDepth.Root : MenuDepth.Sub;
                    if (Depth == MenuDepth.Root)
                    {
                        Branch = MenuBranch.None;
                        Selection = -1;
                    }
                    return true;

                case MenuDepth.Element:
                    // Back to the skill list with the skill still selected --
                    // Selection was never the element's, so the row the player
                    // pressed is still lit and still described.
                    Depth = MenuDepth.Sub;
                    ForgetElement();
                    return true;

                case MenuDepth.Sub:
                    Depth = MenuDepth.Root;
                    Branch = MenuBranch.None;
                    Selection = -1;
                    return true;

                default:
                    return false;
            }
        }

        // Back to the root, unconditionally. Called on every resolution, which
        // is what makes the branch trustworthy as the sole authority above.
        public void Reset()
        {
            Depth = MenuDepth.Root;
            Branch = MenuBranch.None;
            Side = TargetSide.Enemies;
            Selection = -1;
            ForgetPicks();
            ForgetElement();
        }

        private void ForgetElement()
        {
            ElementSelection = -1;
            ChosenElement = null;
        }

        // Back to one pick and none held. Called from every edge that leaves
        // or re-enters Target depth, so a two-pick cast abandoned halfway
        // cannot leave its count or its first ally behind for the next
        // command to trip over.
        private void ForgetPicks()
        {
            RequiredPicks = 1;
            LastPickIsSeat = false;
            DestinationSeat = -1;
            _picks.Clear();
        }

        // Which verb row is lit. Exactly one can be, so this is an index rather
        // than a set.
        public int ActiveVerbIndex =>
            Branch == MenuBranch.Attack ? 0
            : Branch == MenuBranch.Skill ? 1
            : Branch == MenuBranch.Item ? 2
            : Branch == MenuBranch.Move ? 3
            : -1;
    }
}

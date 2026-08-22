namespace PrincesPalace.Domain.Combat.Session
{
    // How deep into the command menu the player is.
    public enum MenuDepth
    {
        Root,
        Sub,
        Target,
    }

    // Which verb's branch is open.
    public enum MenuBranch
    {
        None,
        Attack,
        Skill,
        Item,
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
        public int Selection { get; private set; } = -1;

        // What the hovered row would spend, for the mana bar's cost preview.
        public int ManaPreview { get; private set; }

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
        public bool SubmenuOpen => IsOpen && Depth == MenuDepth.Sub;

        // The detail column is populated for ATTACK too, through a synthetic
        // "Strike" entry: a verb that jumps straight to targeting would
        // otherwise be the one command with nothing to read about it.
        public bool DetailOpen => IsOpen;

        public bool IsTargeting => Depth == MenuDepth.Target;

        // ATTACK does not nest -- it jumps straight to picking a mark.
        public void OpenAttack()
        {
            Branch = MenuBranch.Attack;
            Depth = MenuDepth.Target;
            Selection = -1;
            ManaPreview = 0;
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
            ManaPreview = 0;
        }

        // Hovering or arrowing onto a row. Returns false when nothing changed,
        // so a caller can skip a redraw rather than repaint on every mouse move
        // across one row.
        public bool Select(int index, int manaCost)
        {
            if (Depth != MenuDepth.Sub || index == Selection) return false;

            Selection = index;
            ManaPreview = manaCost;
            return true;
        }

        // Committing a row: the submenu picks, and targeting is what confirms.
        public void EnterTargeting()
        {
            if (!IsOpen) return;
            Depth = MenuDepth.Target;
        }

        // One step back up the graph. Returns false at the root, where there is
        // nothing to back out of -- the caller uses that to decide whether the
        // press was consumed.
        public bool Back()
        {
            switch (Depth)
            {
                case MenuDepth.Target:
                    // ATTACK skipped the submenu on the way in, so it skips it
                    // on the way out. Anything else lands back on its list.
                    Depth = Branch == MenuBranch.Attack ? MenuDepth.Root : MenuDepth.Sub;
                    if (Depth == MenuDepth.Root)
                    {
                        Branch = MenuBranch.None;
                        Selection = -1;
                        ManaPreview = 0;
                    }
                    return true;

                case MenuDepth.Sub:
                    Depth = MenuDepth.Root;
                    Branch = MenuBranch.None;
                    Selection = -1;
                    ManaPreview = 0;
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
            Selection = -1;
            ManaPreview = 0;
        }

        // Which verb row is lit. Exactly one can be, so this is an index rather
        // than a set.
        public int ActiveVerbIndex =>
            Branch == MenuBranch.Attack ? 0
            : Branch == MenuBranch.Skill ? 1
            : Branch == MenuBranch.Item ? 2
            : -1;
    }
}

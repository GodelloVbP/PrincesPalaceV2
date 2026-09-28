using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PrincesPalace.Content;
using PrincesPalace.Domain.Progression;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // What the player can do to the reward track, and what happens when they
    // do it.
    //
    // ONE VERB PER SURFACE. A node claims or centres; the ribbon seeks; the
    // rail scrolls; the arrows step. Nothing here is a mode -- there is no
    // state in which a click means something different from what it meant a
    // moment ago, which is the property that lets a hundred identical discs be
    // safe to press.
    //
    // NEVER A NO-OP, and that is the rule the whole file is built around. A
    // hundred dots that do nothing when pressed teach the player that none of
    // them do, and the ones that DO matter are then invisible.
    public partial class RewardTrackController
    {
        private RailScroll _railScroll;
        private BarSlider _ribbonSeek;

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (collectButton != null) collectButton.onClick.AddListener(CollectAll);

            WireNodes();
            WireRail();
            WireRibbon();
        }

        // A hundred discs, each carrying its own index into every array on this
        // controller.
        //
        // ADDED AT RUNTIME rather than baked, the same bargain HoverIndex's own
        // header records: these carry delegates and an integer, and a scene
        // cannot serialise the first usefully. The scale-on-hover half IS baked
        // (UiNode.Hovers), so a disc still answers the pointer even if this
        // never runs.
        private void WireNodes()
        {
            if (dots == null) return;

            for (int i = 0; i < dots.Length; i++)
            {
                if (dots[i] == null) continue;

                int level = RewardTrackLayout.FirstLevel + i;

                dots[i].onClick.AddListener(() => Press(level));

                var hover = dots[i].gameObject.GetComponent<HoverIndex>()
                            ?? dots[i].gameObject.AddComponent<HoverIndex>();
                hover.Index = level;
                hover.Changed = OnHover;

                // Selecting a node is a distinct action from hovering it
                // The module drives this one through Move alone, calling
                // the same ScrollTo(level) a mouse hover already triggers
                // through OnHover above -- one input (Move) moves
                // selection, selection alone drives the reveal, so there is
                // no second poll that could double-drive it.
                var select = dots[i].gameObject.GetComponent<SelectIndex>()
                             ?? dots[i].gameObject.AddComponent<SelectIndex>();
                select.Index = level;
                select.Changed = OnSelected;
            }
        }

        // THE WHOLE PAD MAP, in one authoritative pass -- three rows, top to
        // bottom as they are drawn:
        //
        //   the rail     Left/Right disc to disc (a Rail, wrap by default)
        //   the ribbon   Left/Right scrubs the window (StepRibbon)
        //   the footer   COLLECT <-> CLOSE, or CLOSE alone when nothing is owed
        //
        // Down from every disc is the ribbon, Down from the ribbon is the
        // footer, and Up walks the same edges back. EVERY SELECTABLE THIS
        // PANEL OWNS IS NAMED HERE, Explicit rather than left to Unity's
        // Automatic, which navigates by screen position with no idea this
        // panel is drawn OVER the dossier -- so a Move could otherwise walk
        // onto a dossier row sitting underneath, and the focus marker and A
        // would belong to a control the player could not see.
        // Explicit with nothing further in a direction is a wall, which is
        // what the panel's edge is.
        //
        // REBUILT EVERY REFRESH, not once: whether COLLECT exists is save
        // state (PaintCollectButton hides it at owed == 0), and the ribbon's
        // Down and CLOSE's Left both change with it. Called after
        // PaintCollectButton for that reason, and after _level is resolved
        // so the ribbon's Up has a real disc to name.
        private void WireNavigation()
        {
            if (dots == null || dots.Length == 0) return;

            var footer = collectButton != null && collectButton.gameObject.activeSelf ? collectButton : null;
            var ribbonDown = footer != null ? footer : closeButton;

            var links = new List<UiNavLink<UnityEngine.UI.Selectable>?>();
            links.AddRange(dots.Select(dot => RuntimeNavWiring.Link(dot, UiNavDirection.Down, ribbonGrab)));
            links.Add(RuntimeNavWiring.Link(ribbonGrab, UiNavDirection.Up, RailFocusDot()));
            links.Add(RuntimeNavWiring.Link(ribbonGrab, UiNavDirection.Down, ribbonDown));
            links.Add(RuntimeNavWiring.Link(closeButton, UiNavDirection.Up, ribbonGrab));

            if (footer != null)
            {
                links.Add(RuntimeNavWiring.Link(footer, UiNavDirection.Up, ribbonGrab));
                links.AddRange(RuntimeNavWiring.LinkBoth(footer, UiNavDirection.Right, closeButton));
            }

            RuntimeNavWiring.Apply(
                RuntimeNavWiring.Group("rewardTrackRibbon", UiNavGroupKind.Rail, dots),
                links);
        }

        // Which disc Up from the ribbon returns to: the one last selected on
        // the rail, or -- once the ribbon has been scrubbed -- the one now in
        // the middle of the window, so Up lands on what the player is looking
        // at rather than on a disc the scrub carried off screen.
        private int _railFocus = -1;

        private UnityEngine.UI.Button RailFocusDot()
        {
            if (_railFocus < RewardTrackLayout.FirstLevel) return CurrentLevelDot();

            int index = Mathf.Clamp(_railFocus - RewardTrackLayout.FirstLevel, 0, dots.Length - 1);
            return dots[index] != null ? dots[index] : CurrentLevelDot();
        }

        // A single field write, not a re-Apply: only the ribbon's Up moved.
        private void PointRibbonUpAtRailFocus()
        {
            if (ribbonGrab == null) return;

            var nav = ribbonGrab.navigation;
            nav.selectOnUp = RailFocusDot();
            ribbonGrab.navigation = nav;
        }

        // A disc gaining or losing pad focus. The card follows it exactly as
        // it follows the pointer -- before this, selection only scrolled, so
        // the marker sat on one disc while the card went on describing the
        // next reward, and nothing a pad player could do ever changed it.
        //
        // PAD ONLY for the card. OnEnable selects the current disc for every
        // player, and for a mouse player that selection is invisible; letting
        // it move the card would replace "the next reward" -- what the card
        // rests on when nothing is pointed at -- with "you are here" on every
        // open.
        private void OnSelected(int level, bool entered)
        {
            if (NavigationInputModule.LastInputWasPad) OnHover(level, entered);

            if (!entered) return;

            _railFocus = level;
            PointRibbonUpAtRailFocus();
            ScrollTo(level);
        }

        private void WireRail()
        {
            if (viewport == null) return;

            _railScroll = viewport.gameObject.GetComponent<RailScroll>()
                          ?? viewport.gameObject.AddComponent<RailScroll>();

            _railScroll.Scrolled = ScrollBy;
            _railScroll.Grabbed = CancelGlide;

            // The viewport draws nothing, so it has no Graphic and would never
            // receive a pointer event. A fully transparent Image raycasts
            // against its RECT, which is exactly what a grab surface wants --
            // the same mechanism UiEmitter uses for a chromeless button.
            //
            // Added here rather than declared in the tree because the tree has
            // no way to say "an invisible surface that is not a button": the
            // viewport is a Panel, and giving it a graphic in the emitter would
            // give one to every clipping panel in the project.
            var catcher = viewport.gameObject.GetComponent<UnityEngine.UI.Image>()
                          ?? viewport.gameObject.AddComponent<UnityEngine.UI.Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            catcher.raycastTarget = true;
        }

        private void WireRibbon()
        {
            if (ribbonGrab == null) return;

            _ribbonSeek = ribbonGrab.gameObject.GetComponent<BarSlider>()
                          ?? ribbonGrab.gameObject.AddComponent<BarSlider>();

            _ribbonSeek.Changed = SeekTo;

            // THE PAD'S HALF OF THE SLIDER. The ribbon is a Button, and
            // WireNavigation leaves its Left/Right empty, so the Button's own
            // OnMove does nothing on that axis and this is the only thing
            // that answers it -- the two IMoveHandlers on one object never
            // both act on the same press.
            var step = ribbonGrab.gameObject.GetComponent<MoveStep>()
                       ?? ribbonGrab.gameObject.AddComponent<MoveStep>();
            step.Stepped = StepRibbon;

            // A on the ribbon goes back up to the rail at the window's
            // centre -- never a no-op, the rule this file is built around.
            // PAD ONLY: a mouse click here is BarSlider's seek, and the same
            // click also reaches this Button's onClick.
            ribbonGrab.onClick.AddListener(() =>
            {
                if (!NavigationInputModule.LastInputWasPad) return;

                var dot = RailFocusDot();
                if (dot != null) UnityEngine.EventSystems.EventSystem.current?.SetSelectedGameObject(dot.gameObject);
            });
        }

        // ONE PRESS, ONE WINDOW, less a disc of overlap so the player keeps
        // their place -- a stick held down crosses the track in about a dozen
        // repeats. Stepped from _railFocus rather than from the live scroll,
        // so a press landing mid-glide adds to where the last one was going
        // instead of to wherever the animation happened to be.
        private void StepRibbon(int direction)
        {
            if (viewport == null) return;

            int from = _railFocus >= RewardTrackLayout.FirstLevel
                ? _railFocus
                : RewardTrackLayout.LevelAtCentre(content.anchoredPosition.x);

            int page = Mathf.Max(1, Mathf.FloorToInt(viewport.rect.width / RewardTrackLayout.NodePitch) - 1);
            int target = Mathf.Clamp(from + direction * page, RewardTrackLayout.FirstLevel, RewardTrack.MaxLevel);

            _railFocus = target;
            PointRibbonUpAtRailFocus();
            GlideTo(target);
        }

        // ---- pressing a node -------------------------------------------------

        // A waiting node collects; anything else glides to centre it.
        //
        // CLAIMING IS SEQUENTIAL: pressing level 30 with a watermark at 12
        // collects 13 through 30, all of them, which is what keeps
        // claimedTrackLevel a single integer. There is never a collected hole
        // below an uncollected level, so nothing has to store a set.
        private void Press(int level)
        {
            if (RewardTrack.IsWaiting(level, _level, _claimed))
            {
                Claim(level);
                return;
            }

            // AND THE CARD SAYS WHAT IT IS. A pad's A on a disc the rail has
            // already centred glided nowhere, so pressing a locked reward
            // looked like nothing happened; the card coming up on it -- with
            // its rise, even when it was already showing -- is the answer to
            // "what will this give me".
            _hovered = level;
            BeginCardSwap(level);
            GlideTo(level);
        }

        // COLLECT EVERYTHING is exactly pressing the player's own node, not a
        // second path to the same place. Two mechanisms for one action is how
        // they drift.
        private void CollectAll() => Claim(_level);

        private void Claim(int throughLevel)
        {
            var character = ResolveCharacter();
            if (character == null) return;

            int from = character.claimedTrackLevel;
            if (throughLevel <= from) return;

            // TO THE NODE THAT WAS PRESSED, and no further -- `throughLevel`
            // bounds the claim rather than settling the entire gap. The
            // watermark is still a single integer: a claim starts where the
            // last one stopped, so there is no hole to store.
            //
            // The max-health nodes are the reason this also has to go through
            // ScaleCarriedHealth below: collecting one moves a character's
            // maximum, and a run that is carrying their current health has to
            // be told, or the bar grows a permanently empty tail.
            //
            // THE READ ORDER IS WHAT MAKES THIS WORK: EffectiveStats sums
            // the track's MaxHealth entries at levels <= claimedTrackLevel,
            // so `maxBefore` is a photograph taken BEFORE the claim moves
            // the watermark on the next line, and `EffectiveStats` inside
            // ScaleCarriedHealth is one taken after. Reading maxBefore any
            // later silently makes the scale a no-op.
            int maxBefore = ContentDatabase.EffectiveStats(character).maxHealth;

            if (!character.ClaimTrackRewards(RewardTracks.For(character), throughLevel)) return;

            RunEncounter.ScaleCarriedHealth(character, maxBefore);

            SaveSlotManager.SaveCurrent();

            int claimedBefore = _claimed;
            Refresh();

            // One burst per level collected, ascending, 130ms apart. Fired
            // against what the screen showed a moment ago rather than against
            // the save, because the save has already moved.
            BeginClaimBursts(claimedBefore + 1, _claimed);

            // NO SOUND, and the omission is deliberate rather than pending.
            // Sound is an enum of meanings whose every value is asserted to
            // resolve to a file that exists, so "collect" would be a new value,
            // a new case in SoundLibrary.PathOf and a clip nobody has recorded
            // -- and the suite would go red until it was.
        }

        // ---- hovering ---------------------------------------------------------

        // The card follows the pointer and falls back to the next reward when
        // it leaves. HoverIndex reports both edges, so this never has to guess
        // which node it is leaving.
        private void OnHover(int level, bool entered)
        {
            if (entered) _hovered = level;
            else if (_hovered == level) _hovered = -1;
            else return;   // an exit for a node we already left; nothing moved

            BeginCardSwap(CardLevel());
        }

        // ---- scrolling ---------------------------------------------------------

        private void ScrollBy(float delta)
        {
            if (content == null || viewport == null) return;

            SetScroll(content.anchoredPosition.x + delta);
        }

        // Where the ribbon was grabbed, as a fraction of the whole track.
        private void SeekTo(float fraction)
        {
            if (content == null || viewport == null) return;

            CancelGlide();
            SetScroll(RewardTrackLayout.ScrollForFraction(fraction, viewport.rect.width));

            _railFocus = RewardTrackLayout.LevelAtCentre(content.anchoredPosition.x);
            PointRibbonUpAtRailFocus();
        }

        // THE ONE PLACE anything writes the content's x.
        //
        // Six things move this rail -- the fly-in, a glide, the wheel, a drag,
        // the arrows and the ribbon -- and every one of them goes through here,
        // so the clamp is applied once and the ribbon's window box is updated
        // once. The alternative is six call sites that each have to remember
        // both, and the one that forgets the window leaves a box pointing at
        // the wrong part of the track with nothing to say it is wrong.
        private void SetScroll(float left)
        {
            float clamped = RewardTrackLayout.ClampScroll(left, viewport.rect.width);

            content.anchoredPosition = new Vector2(clamped, content.anchoredPosition.y);

            MoveRibbonWindow();
            PaintDepthOfField();
        }

        private void ScrollTo(int level)
        {
            if (content == null || viewport == null) return;

            SetScroll(RewardTrackLayout.ScrollFor(level, viewport.rect.width));
        }

        // ---- the keyboard --------------------------------------------------------

        // ONE Update for the whole controller, which C# requires and which is
        // also the right shape: the ambient cues are four lines of arithmetic
        // over Time.time, and four coroutines that never finish would be four
        // objects doing the same work with more ceremony.
        // No raw LeftArrow/RightArrow poll lives here: keyboard arrow
        // support comes for free, because the stock "Horizontal" axis
        // already binds left/right (InputManager.asset), driving ordinary
        // Selectable navigation across the Rail WireNodes just wired
        // instead of a
        // second, raw pixel-scroll poll racing it. Selecting a disc reveals
        // it through the exact same ScrollTo(level) a mouse hover already
        // calls (SelectIndex.Changed, wired in WireNodes) -- one input
        // (Move) drives selection, selection alone drives the reveal.
        private void Update()
        {
            Animate();
        }
    }
}

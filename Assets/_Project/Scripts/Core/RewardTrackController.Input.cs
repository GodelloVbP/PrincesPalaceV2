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
                // (plan section 7): the module drives this one through
                // Move alone, calling the same ScrollTo(level) a mouse
                // hover already triggers through OnHover above -- one
                // input (Move) moves selection, selection alone drives the
                // reveal, so there is no second poll left to double-drive
                // it the way the deleted raw LeftArrow/RightArrow poll
                // (formerly PollKeys, see Update() below) used to risk.
                var select = dots[i].gameObject.GetComponent<SelectIndex>()
                             ?? dots[i].gameObject.AddComponent<SelectIndex>();
                select.Index = level;
                select.Changed = (lvl, entered) => { if (entered) ScrollTo(lvl); };
            }

            // A Rail group (plan section 5), wrap by the owner's default --
            // Left/Right steps disc to disc -- plus the collect button as
            // every disc's Down link, in ONE authoritative pass (RuntimeNav
            // Wiring.Apply writes all four directions of every node it
            // resolves, so the rail and the links have to arrive together).
            //
            // DOWN FROM EVERY DISC, not from one distinguished disc: claiming
            // what is owed is not tied to which disc happens to be focused.
            // Eligibility itself (hide-on-owed-zero, plan section 6) stays
            // exactly where it already lived -- PaintCollectButton's own
            // SetActive call, untouched by this.
            //
            // Built ONCE: unlike Map's runtime-varying node set, every one of
            // these hundred discs exists and is active for the life of the
            // panel (WireNodes' own header: "a hundred discs", never
            // toggled), so there is nothing here a later Refresh() rebuilds.
            RuntimeNavWiring.Apply(
                RuntimeNavWiring.Group("rewardTrackRibbon", UiNavGroupKind.Rail, dots),
                dots.Select(dot => RuntimeNavWiring.Link(dot, UiNavDirection.Down, collectButton)));
        }

        // THE COHERENT MAP'S OTHER HALF: Down from any dot reaches
        // collectButton (WireNodes above, built once); Up from collectButton
        // has to come back, or the pad has a one-way door onto it -- exactly
        // the owner's hardware complaint ("mapping completely off and
        // horrible to navigate"). collectButton was never a Rail member and
        // never a Link's `from`, so UiNavLinkBuilder.Build had no entry for
        // it at all and RuntimeNavWiring.Apply never touched its
        // `.navigation` -- Up did whatever Unity's own default (Automatic,
        // or whatever the scene last authored) happened to compute.
        //
        // A SINGLE FIELD WRITE, not a re-Apply of the whole graph: dots'
        // own Rail and Down links are declared ONCE (WireNodes' own header,
        // "built once... there is nothing here a later Refresh() rebuilds"),
        // and that still holds -- the hundred discs themselves never change.
        // What changes is which one collectButton's Up should return to, so
        // only that one link is rewritten, every Refresh, off the same
        // CurrentLevelDot() the pad's initial focus (OnEnable) resolves to --
        // the one dot both the fly-in and the ribbon rail agree is "here".
        //
        // CALLED FROM Refresh(), not WireNodes: WireNodes runs from Wire(),
        // which OnEnable calls BEFORE Refresh() has resolved _level for the
        // first time, so a link built there would answer for whatever _level
        // defaulted to (RewardTrack.StartingLevel) rather than the character
        // actually showing.
        private void RefreshCollectButtonUpLink()
        {
            if (collectButton == null) return;

            var nav = collectButton.navigation;
            nav.mode = UnityEngine.UI.Navigation.Mode.Explicit;
            nav.selectOnUp = CurrentLevelDot();
            collectButton.navigation = nav;
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
            // THE READ ORDER IS WHAT MAKES THIS WORK, and it stopped being
            // incidental when max health stopped being a stored field.
            // EffectiveStats now sums the track's MaxHealth entries at levels
            // <= claimedTrackLevel, so `maxBefore` is a photograph taken
            // BEFORE the claim moves the watermark on the next line and
            // `EffectiveStats` inside ScaleCarriedHealth is one taken after.
            // The delta between the two is exactly what the old
            // bonusMaxHealth += used to produce. Reading maxBefore any later
            // silently makes the scale a no-op.
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
            // -- and the suite would go red until it was. The design handoff
            // specifies no audio; when it does, that is the shape of the change.
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
        // The raw LeftArrow/RightArrow poll that used to live here is gone
        // (docs/GAMEPAD_NAVIGATION_PLAN.md phase 2, step C: "deleted
        // outright, not gated") -- keyboard arrow support survives for
        // free, because the stock "Horizontal" axis already binds
        // left/right (InputManager.asset), now driving ordinary Selectable
        // navigation across the Rail WireNodes just wired instead of a
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

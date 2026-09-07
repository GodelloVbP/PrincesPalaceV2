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
        // How far one arrow press moves. A whole pitch, so the rail steps node
        // to node -- the wheel is what sweeps across them.
        private const float ArrowStep = RewardTrackLayout.NodePitch;

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
            }
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

            // TO THE NODE THAT WAS PRESSED, and no further.
            //
            // This passed no argument for one build, so ClaimTrackRewards
            // settled the entire gap and pressing any waiting node collected
            // every waiting node. The watermark is still a single integer --
            // a claim starts where the last one stopped, so there is no hole
            // to store -- which is the thing the old comment here thought it
            // was protecting.
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
        private void Update()
        {
            PollKeys();
            Animate();
        }

        private void PollKeys()
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow)) Step(-ArrowStep);
            else if (Input.GetKeyDown(KeyCode.RightArrow)) Step(ArrowStep);
        }

        private void Step(float pixels)
        {
            if (content == null || viewport == null) return;

            CancelGlide();

            // NEGATED, because the content slides opposite to the direction the
            // eye travels: pressing right walks forward along the track, which
            // pulls the rail left.
            SetScroll(content.anchoredPosition.x - pixels);
        }
    }
}

using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PrincesPalace.Domain.UiKit;

namespace PrincesPalace
{
    // The Divine Principality hub.
    //
    // The currency line is the reason UiStrings exists. In v1 the builder baked
    // "Gold: 0    Relics: 0" and this controller wrote
    // $"Gold: {save.Gold}    Relics: {save.Relics}" - two hand-typed copies of a
    // four-space separator, in two files, with nothing tying them together. One
    // entry serves both now, and the lint refuses a bare `.text =` here.
    public class HubController : MonoBehaviour
    {
        [SerializeField] internal Button talentsButton;
        [SerializeField] internal GameObject characterOverlayPanel;
        [SerializeField] internal GameObject debugMenuPanel;
        [SerializeField] internal RelicDraftController relicDraft;
        [SerializeField] internal GameObject glossaryPanel;
        [SerializeField] internal Button principalityButton;
        [SerializeField] internal Button characterSheetButton;
        [SerializeField] internal Button relicsButton;
        [SerializeField] internal Button startRunButton;
        [SerializeField] internal Button mainMenuButton;
        [SerializeField] internal TMP_Text currencyLabel;
        [SerializeField] internal TMP_Text startRunCaption;
        [SerializeField] internal Button[] unbuiltButtons;

        // The overarching menu Cancel now opens (docs/GAMEPAD_NAVIGATION_PLAN.md
        // section 3/4) -- the job SystemMenuController's own now-deleted Escape
        // poll used to do independently, racing HubController's. One context,
        // one Cancel handler, HandleEscape below.
        [SerializeField] internal SystemMenuController systemMenu;

        // THE TEST SEAM FOR THE DESCENT TRANSITION'S OWN CLOCK, the same
        // shape TalentController.MotionSpeedMultiplier already gives its
        // screen. 1 outside a test, so play is unchanged; a test sets it high
        // enough that BeginDescentTransition finishes inside a couple of
        // frames instead of ~0.7 real seconds.
        public static float MotionSpeedMultiplier = 1f;

        private bool _descending;

        // The hub's own place on the navigation stack (plan section 4) --
        // pushed once in Start() and never popped while this scene is
        // loaded, since the hub is not a modal that opens and closes, it is
        // the scene's own base context. Registered here (not a selectable
        // set of its own yet -- the hub's buildings are phase 3's rollout)
        // so its Cancel handler runs through the ONE dispatch point instead
        // of a raw Update() poll, and so C/I/F1 below can ask "am I still
        // top" instead of firing under a modal that has since covered them.
        private NavContext _navContext;

        // OnEnable, not Start: the hub is returned to repeatedly -- from a
        // finished fight, from an abandoned run -- and Start fires once. Gold
        // banked during a descent has to be on the plate when the player gets
        // back, not one scene load later.
        private void OnEnable()
        {
            Refresh();
        }

        // What the screen says about the save. Everything here is read, never
        // stored: a second copy of the wallet on this controller is a second
        // thing that can be wrong.
        public void Refresh()
        {
            var save = SaveSlotManager.CurrentSave;
            if (save != null)
            {
                // Embers are per CHARACTER now, so the hub shows the roster's total --
                // an "unspent somewhere" figure. The per-character breakdown is
                // the talent screen's job, which is where they are spent.
                currencyLabel.Set(UiStrings.HubWallet, save.wallet.gold, save.EmberTotal());
            }

            RefreshGateCaption();
            DimTheUnbuilt();
        }

        // The gate knows whether you are starting or going back down. No new
        // save field: RunManager already knows, and the floor is already stored.
        private void RefreshGateCaption()
        {
            if (startRunCaption == null) return;

            if (RunManager.HasRun) startRunCaption.Set(UiStrings.HubResumeFloor, RunManager.Run.floor);
            else startRunCaption.Set(UiStrings.HubBeginDescent);
        }

        // Buildings whose screens do not exist yet hang dark and unpressable.
        //
        // Dimmed rather than hidden, and rather than a click that logs a
        // refusal: v2 has no bark or sound machinery to make a refusal feel
        // deliberate, so a press that produces nothing reads as a broken button.
        // A dark, still building over the void reads as a place that is asleep,
        // which is the mystery the design is after. The list shrinks as screens
        // land.
        private void DimTheUnbuilt()
        {
            if (unbuiltButtons == null) return;

            foreach (var button in unbuiltButtons)
            {
                if (button == null) continue;

                button.interactable = false;
                if (button.targetGraphic is UnityEngine.UI.Image image)
                {
                    image.color = UnbuiltTint;
                }
            }
        }



        private static readonly Color UnbuiltTint = new Color(0.55f, 0.55f, 0.62f, 1f);

        private void Start()
        {

            // The first of the four annexes to actually lead somewhere.
            talentsButton.onClick.AddListener(() => Navigation.Go(Navigation.Talents));
            principalityButton.onClick.AddListener(() => Debug.Log("Principality"));
            // An overlay, not a scene load: it opens over the hub and the hub
            // is still standing behind it when it closes.
            characterSheetButton.onClick.AddListener(() => SetCharacterOverlay(true));
            // The Relics building opens the RECORD, not a relic screen.
            // Relics stopped being permanent progression, so a screen about
            // owning them had nothing to show; a record of every relic,
            // monster, spell, item, talent and deed does.
            relicsButton.onClick.AddListener(() => SetGlossary(true));
            // The two that go somewhere. The other four are screens that do not
            // exist yet, and a button that logs is more honest than one that
            // loads an empty scene.
            startRunButton.onClick.AddListener(StartOrResumeRun);
            // Going to the title ENDS the run first. Leaving a descent, or
            // anything that is not continuing it, kills it -- no exceptions --
            // and EndRun is what settles the books, so skipping it here would
            // throw away embers the run had already earned rather than just
            // ending it.
            mainMenuButton.onClick.AddListener(() =>
            {
                RunManager.EndRun();
                Navigation.Go(Navigation.MainMenu);
            });

            RegisterNavContext();
        }

        // Idempotent for the same reason FightController's own
        // RegisterNavContext is: a second push for a controller that never
        // torn down would leave a stale entry under the stack no Close()
        // will ever reach.
        private void RegisterNavContext()
        {
            if (_navContext != null) return;

            _navContext = new NavContext(entry: null, selectables: null, cancel: HandleEscape);
            NavigationInputModule.Contexts?.Push(_navContext);
        }

        // The hub is a whole scene, not a panel -- OnDestroy, on scene
        // unload, is where its context actually goes away. Remove, not Pop:
        // this is the OnDisable/OnDestroy safety net plan section 4 calls
        // for, not the ordinary top-of-stack case.
        private void OnDestroy()
        {
            if (_navContext == null) return;

            NavigationInputModule.Contexts?.Remove(_navContext);
            _navContext = null;
        }

        // ---- keyboard ------------------------------------------------------------

        // C and I open the character overlay; Escape closes it.
        //
        // Both keys land on the SAME overlay. v1 had two separate screens and a
        // key each -- an inventory and a paperdoll that could only agree because
        // both called the same service. v2 merged them, so honouring both keys
        // costs nothing and spares anyone their muscle memory.
        //
        // This lives on the hub rather than on CharacterOverlayController
        // because that component sits on the modal node, which is INACTIVE
        // while the overlay is closed. An Update() on a disabled object cannot
        // be the thing that opens it.
        private void Update()
        {
            // GATED ON TOP OF STACK (plan section 4/11): a modal covering the
            // hub -- the system menu, the glossary, the relic draft, the
            // debug menu itself -- must not also see C/I/F1 land underneath
            // it. None of these three push their own context yet (that is
            // phase 3's rollout), so "top" here means "nothing else pushed
            // on top of the hub's own base context" -- which the debug menu,
            // glossary and relic draft all currently achieve by covering the
            // hub WITHOUT pushing a context of their own, so this guard is
            // silent about them today and only actually bites once the
            // system menu (or a future modal) registers its own.
            if (NavigationInputModule.Contexts != null && !NavigationInputModule.Contexts.IsTop(_navContext))
            {
                return;
            }

            // F1 for the debug menu, and ONLY in the editor or a development
            // build. Debug.isDebugBuild is true for both and false in a release
            // player, so a shipped build has no key that grants 10,000 gold.
            //
            // The gate is on the KEY, not on ToggleDebugMenu -- the methods stay
            // callable so PlayMode can drive them, and PlayMode runs in the
            // editor where this is true anyway.
            if (Debug.isDebugBuild && Input.GetKeyDown(KeyCode.F1))
            {
                ToggleDebugMenu();
                return;
            }

            if (characterOverlayPanel == null) return;

            // C is the sheet and I is the bag, now that they are two panes.
            // Both used to open whichever pane happened to be up last, which
            // made I a second key for the same thing.
            if (Input.GetKeyDown(KeyCode.C))
            {
                ToggleCharacterOverlay(inventory: false);
            }
            else if (Input.GetKeyDown(KeyCode.I))
            {
                ToggleCharacterOverlay(inventory: true);
            }

            // Escape/Cancel no longer read here at all -- it belongs to the
            // ONE dispatch point now (NavigationInputModule), which calls
            // this context's Cancel handler (HandleEscape, registered in
            // RegisterNavContext) once per frame instead of racing
            // SystemMenuController's own separate poll for the same key.
        }

        // This IS the hub's NavContext.Cancel handler now (registered in
        // RegisterNavContext), called once a frame by the ONE dispatch point
        // rather than raced against a second poll. It used to be split from
        // its own key read only so it could be tested; that reason is now
        // also the mechanism -- there is nothing left for a raw Escape read
        // to do.
        //
        // Closing beats opening, same order the old two-poller race
        // happened to settle on by luck: debug menu first, since it draws
        // over everything including the character overlay, then the
        // glossary. A relic draft in progress REFUSES to hand Escape to the
        // system menu at all (matching the old escapeConsumers gate
        // SystemMenuController used to check) -- there is nothing on the
        // draft itself that Escape closes, so this press is simply spent.
        // Otherwise, nothing else owns it: open the overarching menu, the
        // job SystemMenuController's own now-deleted poll used to do.
        public void HandleEscape()
        {
            if (debugMenuPanel != null && debugMenuPanel.activeSelf)
            {
                SetDebugMenu(false);
                EscapeKey.Consume();
                return;
            }

            if (glossaryPanel != null && glossaryPanel.activeSelf)
            {
                SetGlossary(false);
                EscapeKey.Consume();
                return;
            }

            if (relicDraft != null && relicDraft.gameObject.activeSelf) return;

            SystemMenuController.OpenOnCancel(systemMenu);
        }

        // The key read above is deliberately separated from the action here:
        // legacy Input cannot be simulated headlessly, so a test that had to
        // press C could not exist. Tests drive these two directly -- and so
        // does the Character Sheet building, which means the button and the
        // key can never drift apart.
        // Read-only, so callers outside Core can ask without being able to
        // reach past the two methods that are allowed to answer. Null-tolerant
        // for the same reason those are: a scene may mount this controller
        // without an overlay.
        public bool CharacterOverlayIsOpen =>
            characterOverlayPanel != null && characterOverlayPanel.activeSelf;

        // Toggling to a named pane: pressing C while the bag is up switches to
        // the sheet rather than closing the overlay, which is what a reader
        // reaching for the other half actually means. Pressing the key for the
        // pane already showing closes it.
        public void ToggleCharacterOverlay(bool inventory = false) =>
            SheetPanel.Toggle(characterOverlayPanel, inventory);

        public void SetCharacterOverlay(bool open, bool inventory = false) =>
            SheetPanel.Set(characterOverlayPanel, open, inventory);

        public bool GlossaryIsOpen => glossaryPanel != null && glossaryPanel.activeSelf;

        public void SetGlossary(bool open)
        {
            if (glossaryPanel == null) return;
            glossaryPanel.SetActive(open);
        }

        public bool DebugMenuIsOpen => debugMenuPanel != null && debugMenuPanel.activeSelf;

        public void ToggleDebugMenu()
        {
            if (debugMenuPanel == null) return;
            SetDebugMenu(!debugMenuPanel.activeSelf);
        }

        public void SetDebugMenu(bool open)
        {
            if (debugMenuPanel == null) return;
            debugMenuPanel.SetActive(open);
        }

        // Kept for the tests and callers that set a wallet explicitly. Goes
        // through the same three-currency line the save-backed path uses, so
        // the two cannot disagree about the format.
        public void RefreshCurrency(int gold, int embers = 0)
        {
            currencyLabel.Set(UiStrings.HubWallet, gold, embers);
        }
    
        // The gate both STARTS and RESUMES.
        //
        // One button rather than two, because the player's intent is the same
        // ("go down") and a run they forgot they had is not a different
        // decision. A second button would also need a rule for what happens
        // when it is pressed with no run, which is a state the design does not
        // have.
        //
        // The press itself only starts the MOCK-UP transition below; every
        // rule this comment used to describe (seeding a fresh run, skipping a
        // drafted relic on resume) now lives in EnterTheDescent, which the
        // transition calls once it has finished playing.
        private void StartOrResumeRun()
        {
            if (_descending) return; // one gate press, not a queue of them
            StartCoroutine(BeginDescentTransition());
        }

        // ---- the descent transition ------------------------------------------

        // Zoom out, then a fast zoom into the gate with a fade at the tail of
        // it, THEN whatever the gate always did. Playing dumb about which of
        // StartRun/ResumeRun/relic-draft comes next is the point: this is
        // pure camera-work sitting in front of a decision that already
        // existed, not a second copy of it that could drift from the first.
        //
        // Scales the hub's own panel rather than a camera: HubController has
        // no camera reference and the panel IS the framing the screen already
        // has (a full-bleed root RectTransform under the canvas), the same
        // thing PushInScale zooms on the talent screen for the same reason
        // -- Screen Space Overlay has no camera to push.
        private const float ZoomOutScale = 0.94f;
        private const float ZoomOutSeconds = 0.25f;
        private const float ZoomInScale = 2.5f;
        private const float ZoomInSeconds = 0.35f;
        private const float FadeSeconds = 0.12f;

        private IEnumerator BeginDescentTransition()
        {
            _descending = true;
            if (startRunButton != null) startRunButton.interactable = false;

            var panel = transform as RectTransform;
            Vector2 startPos = panel != null ? panel.anchoredPosition : Vector2.zero;

            // THE POINT THE ZOOM HOLDS STILL, captured once before any
            // scaling starts -- read after the panel has moved and it would
            // be chasing a target that is itself sliding.
            Vector2 gateLocal = Vector2.zero;
            var gateRect = startRunButton != null ? startRunButton.transform as RectTransform : null;
            if (panel != null && gateRect != null)
            {
                var screenPoint = RectTransformUtility.WorldToScreenPoint(null, gateRect.position);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(panel, screenPoint, null, out gateLocal);
            }

            yield return AnimateZoom(panel, 1f, ZoomOutScale, ZoomOutSeconds, Easing.SmoothStep, startPos, gateLocal);
            yield return AnimateZoom(panel, ZoomOutScale, ZoomInScale, ZoomInSeconds, EaseIn, startPos, gateLocal);
            yield return FadeToBlack(panel, FadeSeconds);

            EnterTheDescent();

            // Left disabled deliberately when a scene load follows -- the
            // panel is about to be destroyed with the rest of the Hub scene.
            // The relic-draft path is the one that stays on this screen, so
            // that is the one that needs the gate (and the panel it sits on)
            // handed back.
            if (relicDraft != null && relicDraft.gameObject.activeSelf)
            {
                if (panel != null)
                {
                    panel.localScale = Vector3.one;
                    panel.anchoredPosition = startPos;
                    var group = panel.GetComponent<CanvasGroup>();
                    if (group != null) group.alpha = 1f;
                }

                if (startRunButton != null) startRunButton.interactable = true;
            }

            _descending = false;
        }

        // Zooming AROUND A POINT OTHER THAN THE PIVOT, without a pivot that
        // sits on that point: at every scale s the panel is repositioned so
        // gateLocal lands back where it started, which is the standard
        // "hold this point still while everything around it grows" trick for
        // a transform scaled from its own centre.
        //
        // THE ACCUMULATE-THEN-DECIDE ORDER IS THE TEST SEAM. Time.
        // unscaledDeltaTime is already valid the instant this coroutine
        // starts -- Unity sets it once per frame, before anything the frame
        // runs -- so a MotionSpeedMultiplier large enough to clear `seconds`
        // in a single step never reaches the `yield return null` below at
        // all: the whole phase resolves in the same frame the gate was
        // pressed in, same as every render-then-yield loop this project
        // already writes it the other way around (render, THEN yield) would
        // have cost it a guaranteed frame per phase no multiplier could skip.
        private static IEnumerator AnimateZoom(
            RectTransform panel, float fromScale, float toScale, float seconds,
            Func<float, float> ease, Vector2 startPos, Vector2 gateLocal)
        {
            if (panel == null) yield break;

            float elapsed = 0f;
            while (true)
            {
                elapsed += Time.unscaledDeltaTime * MotionSpeedMultiplier;
                if (elapsed >= seconds) break;

                float scale = Mathf.Lerp(fromScale, toScale, ease(elapsed / seconds));
                ApplyZoom(panel, scale, startPos, gateLocal);
                yield return null;
            }

            ApplyZoom(panel, toScale, startPos, gateLocal);
        }

        private static void ApplyZoom(RectTransform panel, float scale, Vector2 startPos, Vector2 gateLocal)
        {
            panel.localScale = new Vector3(scale, scale, 1f);
            panel.anchoredPosition = startPos - gateLocal * (scale - 1f);
        }

        // A GET-OR-ADD CanvasGroup, the same move ReckoningController's own
        // BlurGroup makes on an existing node rather than a scene change: the
        // Hub panel was never built with one because nothing needed to fade
        // it before this.
        private static IEnumerator FadeToBlack(RectTransform panel, float seconds)
        {
            if (panel == null) yield break;

            var group = panel.GetComponent<CanvasGroup>();
            if (group == null) group = panel.gameObject.AddComponent<CanvasGroup>();

            float elapsed = 0f;
            while (true)
            {
                elapsed += Time.unscaledDeltaTime * MotionSpeedMultiplier;
                if (elapsed >= seconds) break;

                group.alpha = 1f - (elapsed / seconds);
                yield return null;
            }

            group.alpha = 0f;
        }

        private static float EaseIn(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t;
        }

        // EVERYTHING THE GATE ALWAYS DID, unchanged, now called once the
        // mock-up has played instead of on the press itself.
        private void EnterTheDescent()
        {
            if (!RunManager.HasRun)
            {
                // Seeded from the save so a slot's descent is ITS OWN and
                // reproduces on resume. Not Random: a run that reshuffled its
                // own map when reloaded would make the map screen a lie.
                RunOrchestrator.StartRun(RunManager.NewSeed());
            }

            // The relic draft stands between the gate and the map, and only on
            // a run that has not drafted yet. RESUMING walks straight past it:
            // the relic was chosen when this descent began, and offering again
            // would let a player re-roll it by walking back to the hub.
            if (relicDraft != null && RunOrchestrator.NeedsRelicDraft())
            {
                relicDraft.Finished = () => Navigation.Go(Navigation.Map);
                relicDraft.Open(RunManager.Run.runSeed);
                return;
            }

            // Into the MAP, not straight into a fight. Which room to enter is
            // the player's first decision of a descent, and skipping it was the
            // placeholder this replaces.
            Navigation.Go(Navigation.Map);
        }
}
}

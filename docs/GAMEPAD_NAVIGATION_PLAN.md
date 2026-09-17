# Gamepad Navigation — Plan v3

> **Status: approved 2026-09-17. PHASE 2 COMPLETE.** Phase 1 landed
> (`81f1de14`). Phase 2: step A `00bae36d` (build-time nav declarations --
> since deleted, see step E), step B `c8837374` (SystemMenu nested modal +
> Options row adjustment), step C `da205520` (RewardTrack ribbon as a Rail),
> step E `6c3eb085` (ONE navigation model: the build-time declaration path
> deleted, `UiNavLinkBuilder` made generic, `RuntimeNavWiring` reduced to a
> thin adapter -- section 9 has the decision and its evidence), step F
> `4eab048b` (Cancel opens the system menu on the map and in the fight again,
> `AUDIT.md` #155), step D `467770ef` (Party: seats/cards as navigable Rails,
> selection-driven carrying, a pane's first refusal on Cancel, and the visual
> acceptance capture, `AUDIT.md` #156).
>
> **Two things phase 2 leaves for the owner, both found rather than assumed:**
> (1) the Party capture shows THREE states, not four -- Party's slots have no
> mouse-hover treatment at all (no `Hovers()`, no `HoverIndex`, no
> `ThemedButtonState`), so "can hover be told from selection" cannot be
> answered there until a hover channel exists (section 8/12.2);
> (2) Party's Send-to-bench link is not in a nav group yet, so benching
> mid-carry is mouse-only -- phase 3's rollout.
>
> Research base: `docs/GAMEPAD_NAVIGATION_RESEARCH.md`. Draft 1 (screen
> inventory) and Draft 2 (the rejected gating-on-"am I top" approach this
> draft replaces, section 1) are not carried into the repo — this file and
> the research base are the two documents kept.
>
> Two orchestrator resolutions, settled before phase 1 landed:
>
> 1. **The dispatcher owns stick, Submit and Cancel only.** The C/I/F1
>    keyboard-shortcut polls Draft 2's own section 2 inventoried
>    (`ToggleCharacterSheet`'s `C`/`I`, `SystemMenuController`'s `F1`, and
>    the rest of that list) stay exactly where they are — raw `Update()`
>    polls, outside this dispatcher — through phase 1. They get gated on
>    `Stack.IsTop(myContext)` in phase 2, alongside the screens/contexts they
>    each belong to, rather than folded into this dispatcher now: none of
>    them are stick/Submit/Cancel, so none of them are what section 1's
>    double-drive bug was ever about, and gating them correctly needs the
>    real per-screen contexts phase 2 builds, not the fixture this phase
>    proves the mechanism on.
> 2. **The Fight-to-modal transition needs no mirror of test 1.** Section 3
>    notes the reverse direction of test 1's transition (Fight top, ending
>    combat pushes Reckoning/Defeat) "shares this mechanism but never
>    exercises it same-call, since `_isBusy` already defers it." Phase 1
>    covers that hazard with test 2 instead (same-call ordering against a
>    Fight stub built to select synchronously on purpose) rather than a
>    second transition test, because a mirror of test 1 against the real
>    Fight-to-Reckoning path would exercise `_isBusy`'s deferral, not the
>    dispatcher's own same-frame ordering — the thing actually still open
>    per section 3's "not live today, verified" note.

## 1. Intent

Every screen operable on stick + Submit + Cancel alone. Frozen: legacy
Input Manager, every screen's mouse/click behaviour, and Fight's focus
**model** (`MenuDepth`, `MoveFocus`, `ConfirmFocus`, `WrapFromNoHover`, and
the tests that drive them directly). **Revised freeze, stated plainly**:
Fight's raw `Update()` poll (`FightController.Input.cs:1204-1221`) is not
part of that model — it is the double-drive source Draft 2 tried to gate
around and failed to. It is deleted; the same debounce logic moves into
the dispatcher's Fight branch (§3). Done when: one ordered dispatch point
decides every frame's input, provably (by a test through the real
dispatcher, not a direct handler call); a Cancel that closes a modal
cannot also reach the context exposed underneath it in the same frame,
proven for both transition directions; `UiAudit`'s structural claim is
kept separate from what only a runtime test can prove.

Draft 2 was rejected because gating readers on "am I top of stack" doesn't
stop two readers from each independently, correctly, seeing themselves as
authorized in the same frame (Options closes, Fight becomes top, Fight's
own `Update()` still runs later that frame). The fix in this draft is
structural, not another gate: one method, called once per frame by Unity
itself, is the only place any of this is read.

## 2. Verification

Carried over from Draft 2, unchanged and still load-bearing: (a) Options'
stepper claim is false — only Stepper-kind rows have buttons
(`OptionRows.cs:5-13`, `OptionsScreen.cs:190-247`); (b) Shop has both
eligibility shapes (`ShopController.cs:553` disables sell, `:207-244`
refuses-on-press for buy); Party never disables (`PartyController.cs:765-768`);
Talent/RewardTrack hide (`TalentController.cs:792-819`,
`RewardTrackController.cs:507`); Dossier has no content-hiding tabs or
scroll; exactly two scroll surfaces exist project-wide
(`ListScroll.cs`/`RailScroll.cs`); five screens are separate scenes, the
rest toggle via `Open()` with content populated after activation
(`ShopController.cs:103-117`); modals are scene siblings
(`HubController.cs:146-149`); pause doesn't stall repeat
(`Time.unscaledTime`, `StandaloneInputModule.cs:513,527,533`).

**New verification for this draft — the dispatcher seam is real:**
- `BaseInputModule.Process()` is `public abstract void Process();`
  (`BaseInputModule.cs:125`); `StandaloneInputModule : PointerInputModule`
  is not sealed, and its own `public override void Process()`
  (`StandaloneInputModule.cs:294`) is not sealed either — a project
  subclass can override it again.
- `BaseInputModule` already has exactly the injectable seam claimed:
  `protected BaseInput m_InputOverride`, a public `input` property
  returning `m_InputOverride` when set (else a lazily-created default),
  and a public `inputOverride` setter (`BaseInputModule.cs:58,64-90,98-102`).
- **Every** value `StandaloneInputModule` reads goes through that `input`
  property, never the static `UnityEngine.Input` class directly:
  `input.mousePosition` (`:176`), `input.GetButtonDown`/`GetAxisRaw` in
  `ShouldActivateModule` (`:253-258`), `input.GetButtonDown` in
  `SendSubmitEventToSelectedObject` (`:476,479`), `input.GetAxisRaw`/
  `GetButtonDown` in `GetRawMoveVector` (`:487,490,497`). `BaseInput`
  itself (`BaseInput.cs`, full file read) declares every one of these
  (`GetAxisRaw`, `GetButtonDown`, `mousePosition`, `GetMouseButtonDown`,
  `touchCount`, `GetTouch`, `mousePresent`) as `virtual`, each defaulting
  to the matching `UnityEngine.Input` call — a test subclass can override
  all of them and feed the real module scripted values.
- **Repeat timing is not part of that seam.** `Time.unscaledTime`
  (`:513,527,533`) is a direct static call inside `StandaloneInputModule`,
  with no `BaseInput` equivalent — a scripted `BaseInput` cannot fake it.
  A repeat-cadence test therefore has to wait real frames in PlayMode
  (slow, borderline-flaky) or stay hardware-acceptance-only; a single-press
  Move/Submit/Cancel test does not need it at all.
- `EventSystem.Update()` (`EventSystem.cs:473`, `protected virtual`) calls
  `m_CurrentInputModule.Process()` (`:516`) once per engine frame,
  automatically. A PlayMode test sets the scripted `BaseInput`'s values,
  `yield return null` once, and asserts the result — it never needs to
  call `Process()` by hand.
- **`ExecuteEvents.Execute<T>` invokes every matching component on the
  target, not just one** (`ExecuteEvents.cs:328-339`,
  `go.GetComponents(components)` then adds every one that matches `T`) —
  confirms Draft 2's point-4 mistake: a bare `Selectable` plus a second,
  separate `IMoveHandler` on the same row would **both** run. One subclass
  replacing the plain `Selectable` is required, not an addition.
- **`Selectable.OnMove` is `public virtual`**, switching on
  `eventData.moveDir` (`Selectable.cs:1064-1084`) — directly overridable
  per-axis.
- **`InputManager.asset`'s stock "Horizontal" axis already binds the arrow
  keys**: `negativeButton: left`, `positiveButton: right`, plus
  `altNegativeButton: a`/`altPositiveButton: d`
  (`ProjectSettings/InputManager.asset:9-21`; a second "Horizontal" entry,
  `:153-165`, is the analog stick). Deleting RewardTrackController's own
  arrow-key poll and routing through the module's Horizontal-driven Move
  does not regress keyboard users.
- **A capture pipeline already exists for point 8's visual proof**:
  `tools/screenshot.ps1 -Runtime -RuntimeFilter <PlayModeTestClass>`
  (`tools/screenshot.ps1:4,17,131-169`) drives a named PlayMode test class
  through the test runner and captures the running game, animators
  ticking — reusable as-is for a new capture test class.
- **Modals already block the click that would reach behind them.** All
  eleven (grepped, one shape) are built by the same `Ui.Modal`
  factory (`Ui.cs:730-742`) from a full-screen `Solid` dimmer never marked
  `.AsDecor()` (`Ui.cs:734-735`), so `UiEmitter.EmitImage`'s decor branch
  (`UiEmitter.cs:539`: `if (decor) image.raycastTarget = false;`) never
  fires and the dimmer keeps `Image`'s default `raycastTarget = true` —
  the factory's own comment: "intercepting their input is what modal
  means" (`Ui.cs:738-741`). One canvas/raycaster per scene, so a click on
  the screen underneath never fires that control's `OnPointerDown`.

## 3. Input ownership — one dispatch point

**Mechanism: a project subclass of `StandaloneInputModule` overriding
`Process()`**, attached in place of the stock module wherever `SceneBuilder`
adds one. It is the only method in the game that reads gamepad/keyboard UI
input; nothing else polls `Input.*`/`input.*` for this purpose after this
lands.

Each call: ask the context stack for the top context **once**, at entry,
into `topAtStart` — never re-queried mid-call, which is what makes a
same-frame transition safe (below).

- **Top is Fight**: five steps, nothing else runs this call.
  1. Capture `topAtStart`.
  2. `EventSystem.current.SetSelectedGameObject(null)` — every frame,
     unconditionally: selection is null *because this asserts it every
     frame*, defeating every counter-example (a stale modal selection, an
     Explicit link, a stray programmatic call) by brute force, not trust.
  3. `base.Process()`, unchanged — **verified safe**:
     `SendSubmitEventToSelectedObject` returns immediately on
     `currentSelectedGameObject == null` (`:472-473`), and
     `SendMoveEventToSelectedObject`'s `ExecuteEvents.Execute` against a
     null target resolves through `GetEventList`'s `go == null` return
     (`ExecuteEvents.cs:325`) — mouse/click still runs (needed for Fight's
     own Buttons); Move/Submit dispatch is structurally inert. (Its
     debounce fields still update off the axis read with nothing to
     fire — harmless, but explains a slightly-off first-repeat cadence
     right after leaving Fight.)
  4. Read the same sampled `input.GetAxisRaw`/`input.GetButtonDown`
     values (Fight's debounce logic, moved verbatim from the deleted
     `PollGamepadNavigation`) and call `Fight.MoveFocus`/`ConfirmFocus`/
     `OnBackPressed` directly. `Navigation.None` on Fight's Buttons
     (Draft 2's fix) stays as a defensive second layer against a stray
     click — the null-assert, not this, is what makes Move/Submit inert.
  5. Nothing else.

  **3 before 4.** A future Fight verb that synchronously selects an
  `EventSystem` object (a shared menu opened mid-fight), run in the other
  order, would hand that selection to *this same call's* still-true
  Submit (`StandaloneInputModule.cs:476-479`) — opening the menu and
  firing its first item in one press. Step 3 first spends the frame's
  Submit against nothing (step 2); the new target waits for the *next*
  call. **Not live today, verified**: `OnBackPressed` only calls
  `_menu.Back()` (`FightController.Input.cs:737-739`), and ending combat
  is deferred past this call by `_isBusy`/`OnPlaybackFinished`
  (`:1206,1049`) — future-proofing, proven by a Fight stub built to
  select synchronously on purpose (test 2, below).
- **Top is not Fight**: `base.Process()` unchanged — free Move/Submit to
  whatever is selected (§4); then read `input.GetButtonDown(cancelButton)`
  once for that context's Cancel handler. (Its own internal Cancel read,
  `SendSubmitEventToSelectedObject:479-480`, is harmless in parallel —
  nothing implements `ICancelHandler` on any Selectable here, grepped.)

  **Then, every call, one rule after `base.Process()` returns** — the
  ordinary-context twin of the Fight branch's null-assert: if selection is
  null, or is a `GameObject` outside `topAtStart`'s declared Selectable
  set, reselect its remembered node, else its entry. One rule for three
  cases: (a) a background click nulls selection via legacy input's
  `DeselectIfSelectionChanged` (`PointerInputModule.cs:427-435`), restored
  in the **same call** — no first-Move-restores step, nothing crosses a
  frame boundary null; (b) a mouse click on a Selectable under a modal,
  which `OnPointerDown` would otherwise select (`Selectable.cs:1226-1233`:
  `if (IsInteractable() && navigation.mode != Navigation.Mode.None &&
  EventSystem.current != null) SetSelectedGameObject(gameObject,
  eventData);`) — but **verified this project's modals already block
  that click** (§2), so (b) is defence in depth, no scrim needed (§4);
  (c) a stray or programmatic `SetSelectedGameObject` from elsewhere.

**Consumption across transitions — the fix for point 2.** `topAtStart` is
captured once and `EventSystem.Update()` calls `Process()` at most once a
frame (`EventSystem.cs:473,516`), so a Cancel popping Options runs against
`topAtStart == Options` even though Fight becomes top before the call
returns — Fight's branch waits for the *next* call (test 1). The reverse
(Fight top, ending combat pushes Reckoning/Defeat) shares this mechanism
but never exercises it same-call, since `_isBusy` already defers it — no
mirror test needed; test 2 targets the hazard `_isBusy` doesn't cover.

**Acceptance tests, through the real dispatcher** (scripted `BaseInput`
via `inputOverride`, `yield return null`, assert resulting state — not a
direct handler call):
1. Modal-to-Fight: Options top over Fight; one Cancel frame. Assert
   Options closed and Fight's `MenuDepth` unmoved.
2. Same-call ordering: Fight top; one Submit frame against a Fight stub
   whose `ConfirmFocus` synchronously selects a stub menu entry (standing
   in for the not-yet-live case above). Assert `ConfirmFocus` ran once
   and its entry did not also receive this Submit.
3. Mouse-then-Submit: click a real Button (scripted mouse position/
   `GetMouseButtonDown`), then one Submit frame; assert the clicked
   control's action ran once, not `ConfirmFocus` (Draft 2's Test 1
   watched the wrong signal). Also, with a modal top, script that same
   click at the underlying screen's coordinates then Submit: assert the
   click reaches nothing and Submit still resolves against the modal's
   selected control.
4. Fight isolation: click a Fight verb Button (mouse), then a
   stick-and-Submit frame; assert selection is null next `Process()` and
   no module-driven action fired — only what `MoveFocus`/`ConfirmFocus`
   themselves produce.

Direct-handler tests (`MoveFocus`/`ConfirmFocus` called directly) stay
**alongside**, pinning model logic; the tests above pin the dispatcher
that decides whether the model or the module gets the frame.

## 4. Navigation context stack

- **Owns**: its Selectable set (Fight: none, §3); a declared entry target;
  remembered focus by stable id.
- **Push** (`Open()`, never `OnEnable`, §2): selects
  `remembered ?? entry`; Fight has no `GameObject` to select, it registers
  only its boolean/branch membership.
- **Pop** (`Close()`): restores the previous context's remembered node, or
  falls back per §6's invariants below — no single blanket "never null"
  rule, because Fight and an empty context both legitimately have no
  selection to restore.
- **Registration and cleanup**: pushed only from `Open()`; popped from
  `Close()`, **and** from `OnDisable()`/`OnDestroy()` as a safety net for
  external deactivation (a parent hidden, a scene unload) — the stack
  removes that context wherever it sits, not only if it's on top; removing
  a non-top entry changes nothing about the current top's selection,
  removing the top restores the next one down per the invariants (§6).
- **Runtime graph rewrites**: `MapController.Refresh()` (`:184-249`, the
  same method that already recomputes `reachable`/`occupied` and calls
  `PaintNode` per node) is also where Map's Explicit links between nodes
  are rewritten each call — a runtime write, on the same state-change
  trigger the paint step already has, not a build-time-only graph and not
  a per-frame rebuild.
- **Shortcut suspension** (Draft 2's inventory, unchanged): every listed
  `Update()` poll outside this dispatcher and Fight's now-deleted poll
  checks `Stack.IsTop(myContext)`; the two Escape-literal reads
  (`HubController.cs:183`, `SystemMenuController.cs:143`) are deleted,
  their job absorbed into §3's Cancel branch.
- **Fight as a context**: no `EventSystem` selection, ever (§3) — its
  membership is the dispatcher's branch condition, not a selected node.
- **Modals already own their raycasts** (§2): §3/§6's post-dispatch
  reselection rule is defence in depth for a click through a modal,
  earning its place instead on a background click and a
  stray/programmatic selection. No scrim change needed; `Ui.Modal`
  already behaves like one.

## 5. Navigation groups

Unchanged: List (Options rows, Talent's detail actions), Rail (RewardTrack,
Shop's paged row), Grid (Talent's `orbs[OrbCount]`, `:24`), Graph (Map,
links from `MapController.Refresh()` per §4). `Navigation.Explicit` links
set at scene-build time or, for Map only, rewritten at `Refresh()`-time.
Wrap/clamp per group+axis, not per screen (owner call, §12).

## 6. Runtime eligibility and lifecycle invariants

**Eligibility** (Draft 2, unchanged): hide-on-ineligible (Talent,
RewardTrack) re-resolves on the controller's own repaint; disable-on-
ineligible (Shop's sell buttons) needs no graph change, `Button.OnSubmit`
no-ops on a disabled target; never-disable/refuse-on-press (Party, Shop's
buy button) needs no recovery rule at all.

**Selection invariants, three separate cases — not one blanket rule:**
- **Ordinary context, nonempty group**: selection is never null while this
  context is top, enforced every call by §3's post-dispatch reselection
  rule rather than assumed. On push: `remembered ?? entry`. If the
  focused node vanishes mid-session: the group's next-nearest eligible
  node, or the entry if none remain. A background click, a stray
  leftover, or a programmatic call can unset selection **within** a call;
  §3's rule closes it out before that call returns, so no first-Move-
  restores special case exists — nothing ever crosses a frame null.
- **Ordinary context, its entry's whole group has emptied** (every node in
  it hidden/destroyed): falls back to **screen-level fallback** — select
  nothing, keep Cancel live (the context's Cancel handler still runs, so
  the screen can still be left), and re-select the first node that
  reappears in any group on the next repaint. This is the case Draft 2's
  "fall back to the group entry" silently couldn't handle, and the one
  case §3's rule does not paper over, since there is nothing to reselect.
- **Fight**: selection is null by construction, always, asserted every
  frame by the dispatcher (§3) — not an absence to recover from, the
  correct steady state.

## 7. Contracts

Unchanged from Draft 2 unless noted: initial focus/memory, Submit
semantics, mouse coexistence, tooltips, scroll-follow, tabs, reward-track
claim, text entry.

- **Options adjustment — one Move implementation, not two components.**
  A dedicated `OptionRow : Selectable` (or a thin subclass over whatever
  base the kit's rows already use) is the **only** component on
  `RowHovers[i]` handling movement — not a plain `Selectable` plus a
  second `IMoveHandler`, since `ExecuteEvents.Execute<T>` runs every
  matching component on the target (`ExecuteEvents.cs:328-339`, §2), so
  two would both fire. It overrides `OnMove` (`Selectable.cs:1064`):
  `MoveDirection.Left/Right` adjusts the bound value (`Step`/`SetSlider`,
  unchanged targets) and consumes the event without calling
  `base.OnMove`; `Up/Down` calls `base.OnMove(eventData)` unchanged,
  which is Unity's own ordinary row-to-row navigation. Repeat rides the
  same base `Move Repeat Delay/Rate` the framework already applies before
  calling `OnMove` — no separate repeat code, confirmed by
  `SendMoveEventToSelectedObject`'s debounce running before dispatch
  (`:511-554`). **Stepper Buttons get `Navigation.Mode.None`**: never
  selected by a mouse click, never a navigation target
  (`Selectable.cs:1232-1233`, same mechanism as Fight's fix, §3's prior
  draft) — clicking one still fires its `onClick`/`Step` (the pointer-
  click path is independent of the selection side-effect
  `Navigation.None` suppresses), and does **nothing** to the row's
  remembered focus, since `OnPointerDown` never calls
  `SetSelectedGameObject` for a `None`-mode Selectable.
- **RewardTrack — corrected, the double-drive Party had is fixed, this one
  wasn't.** Ribbon nodes become ordinary Rail-group Selectables navigated
  by the module (§5); `RewardTrackController.Input.cs:250-251`'s raw
  `KeyCode.LeftArrow`/`RightArrow` poll is **deleted outright**, not
  gated — keyboard arrow support survives for free because the stock
  "Horizontal" axis already binds `left`/`right`
  (`InputManager.asset:9-21`, §2). Selecting a node is a distinct action
  from revealing it: `RailScroll` gains an `ISelectHandler`/
  `IDeselectHandler` hook on each node (mirroring §7 v2's visual fix
  shape) that calls the controller's existing `ScrollTo(level)`
  (`RewardTrackController.Input.cs:229-233`) when that node becomes
  selected — one input (Move) drives selection, selection alone drives
  the reveal; there is no second raw poll left to double-drive it.
- **Selected vs Hovered** (Draft 2's finding, unchanged, treated as a
  candidate not a verified fix, §8 below): `ThemedButtonState` already
  gives Selected a distinct, `Max`-composed halo over Hover
  (`ThemedButtonState.cs:270-280,98-99`); `HoverIndex`-only controls
  (Party, Options rows, SystemMenu tabs) need the same shape added.

## 8. Party state table

Unchanged mechanism from Draft 2: destination is the `EventSystem`
selection itself (seats/cards are real Buttons, §5); `Formation.Drop`
reads the selected Button's index via the existing `IndexOfButton` helper
(`PartyController.cs:541-548`) instead of a raycast hit; no custom
`IMoveHandler` needed, ordinary `Selectable.OnMove` relocates the
candidate during Carrying; Cancel restores selection to the source
explicitly.

**Point 8's demand, unresolved by assertion — requires a visual acceptance
capture, not a claim.** A new PlayMode test drives Party into all four
states simultaneously: a carried source, a distinct valid destination, the
current `EventSystem`-selected Submit target, and a mouse hovering a fifth,
unrelated card — then captures via the existing pipeline
(`tools/screenshot.ps1 -Runtime -RuntimeFilter PartyGamepadVisualCaptureTests`,
§2). The reused `ThemedButtonState` alpha/scale ratio (§7) is a **candidate
treatment** applied to this capture, not a verified solution — the owner
reviews the picture; if the four states aren't distinguishable at a glance,
this is the point to add a fifth visual channel (e.g. a border colour
distinct from all three glow tiers) rather than tune the existing numbers
further.

## 9. Audit, and the one navigation model

**Revised 2026-09-17, phase 2 step E, against what step A actually built.**
Step A declared navigation on `ScreenDef.Nav` and wrote
`Navigation.Explicit` at scene-build time (`UiNavWiring`), audited by
`UiAudit.CheckNavigable`. Steps B and C then found that no screen could
use it and added a second, runtime path (`RuntimeNavWiring`) with its own
copy of the prev/next math. Two implementations of one rule is what
`docs/CODE_STANDARDS.md` section 10 forbids, so the two were collapsed
into one:

**The decision: runtime wiring is the only path; the build-time
declaration path is deleted.** The navigable set is a runtime fact on
every surface this phase touches, so a build-time write would be correct
for no frame a player ever sees:

- SystemMenu's tab strip — the scene carries five tabs, a context shows
  three, recomputed in `ApplyContext` (`SystemMenuScreen`'s own header:
  the scene cannot know which).
- RewardTrack — the collect button is hidden whenever nothing is owed
  (`PaintCollectButton`), so the rail's Down target exists or does not by
  save state.
- Party — which seats are occupied, and whether a carry is in progress,
  decide what a Move should reach.
- Map — `MapController.Refresh()` recomputes the reachable set per floor
  (section 4 already said so).

Every one of those rewires on its own repaint, so build-time links are
overwritten before first use. `ScreenDef.Nav`, `UiNavWiring` and
`UiAudit.CheckNavigable` are therefore deleted rather than kept for a
consumer that does not exist; nothing lost with them had a reader
(`UiNavDeclaration.Entry` duplicated `NavContext.Entry`, which is what
actually gets selected on push; `UiRequiredAction` was read only by
`CheckNavigable`).

**One declaration shape, one builder.** `UiNavSpec`'s types are now
generic over what a node is (`UiNavGroup<TNode>`, `UiNavLink<TNode>`,
`UiNavDeclaration<TNode>`) and `UiNavLinkBuilder.Build<TNode>` is the ONE
implementation of List/Rail/Grid + wrap/clamp + inter-group links. Domain
stays engine-free (it never inspects `TNode`), and `RuntimeNavWiring` is a
thin adapter: it feeds live `Selectable`s in and writes the resulting
`Navigation.Explicit` out, computing nothing itself. It is
**authoritative** — `Apply` writes all four directions of every node its
declaration resolved — so each surface passes its whole shape in one call
rather than dribbling one axis per call and depending on what the last
call left behind.

**(a) Structural, build-time — what survives.** `UiNavControlsAudit`, and
it is no longer opt-in. A screen's emitted component set is fixed by the
build, so "this thing answers a click, carries no `Selectable`, and nobody
said why no stick can reach it" is answerable there and nowhere else; the
fix is `AllowUnreachable("reason")` on the node or making it a real
Selectable wired into a group. Its limits are stated rather than hidden:
most custom actionable components in this project (`BarSlider`,
`HoldToConfirm`, `ListScroll`, `RailScroll`) are `AddComponent`'d by their
controller at runtime where no build-time scan sees them, so today it
fires on the build-time `Attach<T>` path only — the path that grows. A
GameObject that IS a Selectable is exempt wholesale: whatever else sits on
it is reached through that Selectable, not around it.

**(b) Behavioural, runtime PlayMode** — unchanged, and now carrying the
whole load that (a) used to share: for each required action, a test drives
the production dispatcher (section 3's scripted `BaseInput`) from the
context's entry to that action, in the state the action requires — Options'
adjustment with no Button target, Party's drop with the model in Carrying,
Talent's invest with an eligible slot selected. The intent statement
(section 1) narrows accordingly: the build proves the control inventory,
runtime proves operability.

## 10. Test strategy

- **Direct-handler tests**: kept (Fight's `MoveFocus`/`ConfirmFocus`,
  Formation's drop/cancel) — they pin model logic, not dispatch.
- **Dispatcher integration tests** (new, the centre of phase 1): scripted
  `BaseInput` + `inputOverride`, driven by `yield return null` per frame,
  asserting resulting actions/state — §3's four acceptance tests, run on a
  fixture scene with two ordinary Buttons, a modal, and a Fight stub
  exposing `MoveFocus`/`ConfirmFocus`/`OnBackPressed` call counts.
- **Repeat cadence**: hardware-acceptance only (§2) — not unit-tested,
  documented as a known gap rather than silently skipped.
- **Options row / RewardTrack node**: `OnMove` override tested directly
  (Left/Right adjusts, Up/Down delegates) plus one dispatcher-integration
  pass confirming a stepper mouse-click doesn't disturb remembered focus.
- **Party**: dispatcher-level Carrying test (module Move relocates the
  candidate, Submit resolves through `IndexOfButton`+`Formation.Drop`);
  the visual acceptance capture (§8).
- **Audit**: one test per §9(a) rule, one PlayMode test per §9(b) required
  action, growing with each screen in phase 3.
- **Hardware acceptance**: unspecified in repo/research; recommend Xbox +
  DualSense/DualShock on the shipping platform (owner call, §12).

## 11. Phases

**1 — prove ownership across transitions, on a fixture, before touching
real screens.** The module subclass; the context stack; Fight's branch and
deleted poll; the four dispatcher acceptance tests — the transition case
and the same-call ordering case — on two Buttons + a modal + a Fight
stub. *Gate*: all four pass through the real dispatcher, not a direct
call.

**2 — the hard screens.** SystemMenu (nested modal + Options), Options
(the `OnMove`-override row), RewardTrack (selection-drives-reveal), Party
(selection-based Carrying + the visual capture, §8). *Gate*: §9(b)
behavioural tests for each pass; a human plays all four with no mouse.

**3 — rollout.** Remaining screens, `CheckNavigable` (a) strengthened and
(b) behavioural tests written alongside each. *Gate*: every screen has both
kinds of proof, not structural alone.

**4 — full journeys.** Controller-only and mouse-only passes across the
whole game, including system menu mid-fight (§3's transition case) and a
mixed-input pass (§3/§6's reselection rule). *Gate*: both clean, on
hardware from §12.

No effort estimates.

## 12. Open owner calls

1. **Options**: row-focus-adjust (this draft's default) vs. real steppers
   on `sound`/`music` too, retiring `OptionKind.Slider`.
2. **Selected-visual treatment** (§7/§8): the `ThemedButtonState` ratio is
   a candidate pending the visual capture, not approved — review the
   four-state screenshot before treating it as done.
3. **Wrap/clamp defaults** (§5): wrap Rail/Grid-row, clamp List, Map
   follows reachability.
4. **Hardware list**: recommend Xbox + DualSense/DualShock on the shipping
   platform.
5. **Party pick-up depth**: Submit-to-pick-up/drop only, unchanged default.
6. **Repeat-cadence testing gap** (§10): accept hardware-acceptance-only,
   or invest in a real-time PlayMode wait despite the speed/flakiness cost.

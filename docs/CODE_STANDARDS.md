# Code Standards

Rules this codebase holds that a competent C# engineer would not assume.
Area rules load by path from `.claude/rules/` (`ui.md`, `content.md`,
`tests.md`, `tools.md`).

## Layering

Five asmdefs; references point only Editor/Tests → Core → Domain.

- `Domain/`: engine-free (`noEngineReferences`), no content-layer concepts
  (`CharacterRole`, `PrincesPalace.Content`, any `*Definition`).
- `Core/`: MonoBehaviours, controllers, `Content/` definitions, `Data/` save state.
- `Editor/`: build-time only. `Tests/EditMode/` sees Domain only;
  `Tests/PlayMode/` sees Core + Domain.

Placing new code: needs a `UnityEngine` type to do its job → Core, else
Domain. Needs a content type → Core. Build time only → Editor. A
MonoBehaviour wrapped around arithmetic → split: arithmetic to Domain,
wrapper stays. Test: could an EditMode test call it? If not, and no engine
dependency forces that, it is in the wrong assembly.

## Reuse

Search before writing a helper; the second copy-paste of a pattern is the
signal to promote it. UI helpers: `.claude/rules/ui.md`.

## Partial classes

- `ClassName.Topic.cs`, one topic per file; a part repeats its class's
  namespace wrapper exactly (or none).
- MonoBehaviour: the root file is named exactly after the class and holds
  the declaration, every `[SerializeField]`, const and state field; parts
  hold methods. The scene binds through that file's GUID.
- Engine-free class: each part owns the state its topic needs, beside the
  comment explaining its invariant (`Domain/Combat/Session/FightSession.*.cs`).
- No `static readonly` initializer may read a `static readonly` declared in
  another part (cross-file initializer order is unspecified).
- Never add an `.asmdef` inside a folder holding parts of an existing class.
- No renames in a pure-move split; tests reference methods by name.

## Functions

- The unit is a reason to change, not a line count. A long declaration (a
  screen's `Build`) is fine; a procedure whose paragraphs change for
  different reasons is not. If you cannot name an extracted piece after
  what it computes, it is not a seam.
- Take the narrowest type that answers the question (a count, not the list).
- Past ~4 parameters, take an inputs struct.
- A bool parameter may be state (`OnSlotHover(i, entered)`), never a
  behaviour selector (`Refresh(true)`): that is two functions.
- `out` only for the Try-pattern.
- A default parameter value only when the default is the meaning.
- Validators, audits and lints collect every error in one pass.
- Degrade, but never quietly lie: Domain throws on programmer error; Core degrades on
  missing content or art (graceful degradation is house style). Never
  return a plausible wrong answer (a zero, an empty list) a caller cannot
  tell from a real one.
- Rejected here: an interface per class plus DI, null checks at every
  level (one guard at the seam), `GameObject.Find`/`FindObjectOfType` for
  wiring. Changing that takes a commit message that argues it.

## Values

Every number has one home; everything else derives from it. Prefer derived
(`TalentSkeleton.SlotCount - 1`), then declared once
(`FightHudSpec.StageSlotsPerSide`); never restated. Grep before adding a
value and alias the existing name. Measure what the program can measure
(text width, sprite bounds) instead of authoring it. A reserved capacity
states why and has a pin that fails when content outgrows it. No parameter
without a second caller.

## Statics and lifetime

- Decide cache or state. A cache is keyed by what built it; state lives in
  the save, the static only a thin accessor.
- Every static cache has a reset seam (`ContentDatabase.Reset()`).
- A public mutable static argues in its header why it is a player setting
  (`FightBeatPlayer.BeatSpeedMultiplier`); otherwise make it `internal`.
- Nothing that outlives a scene is a MonoBehaviour unless it is a scene
  object; run-lifetime state goes through the save.

## Comments

- A comment is a restated value: reference the name
  (`bounded by TalentSkeleton.SlotCount`), not the number. Where it must
  state a fact, an assertion is the form that cannot rot.
- A comment claiming who calls something is not evidence; grep.
- When fixing a bug, name the tier that kills its class: T1 the API cannot
  express it (`Ui.Label` takes `UiString`), T2 one code path owns it
  (`UiEmitter`), T3 a lint with a vacuity guard. An unmechanisable rule
  still gets a greppable exemption (`AllowOverlap("reason")`).

## Build the model

Before extending a system, check its design still fits the request and the
known next ones; if not, say so and propose the proportionate change —
never silently shrink the requested experience. Separate reusable behaviour
from authored content: a new combination of supported behaviour costs data,
not code. For a real architectural decision, validate the model against two
materially different uses, define ownership, lifecycle and failure
handling, and keep one implementation of every shared rule. No abstraction
without a second concrete user, no configurability without a demonstrated
purpose. Scale this to the task.

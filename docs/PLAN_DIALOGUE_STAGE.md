# Plan: Dialogue stage (Hades-style event dialogue)

Status: Revision 2, 2026-09-25: absorbs two reviews (routing, counter timing, result ordering, input state machine, bust art). Approved for implementation.
Companion plan: `docs/PLAN_PETTING_ZOO.md`. Reference: owner's Hades 2 screenshot (Nemesis) -- layered stage, one chest-up speaker overlapping an ornate box with name + epithet. Not a copy; the layering and presentation are the point.

Items marked **(assumed)** are gap-fills the owner may overturn.

## Layers (back to front)

| Layer | What | Source |
|---|---|---|
| 0 Region backdrop | Full-bleed, cover-cropped, darkened so the front reads | event `backdrop`, default `Art/Backgrounds/Dungeon.png` |
| 1 Shade | Top/bottom gradient bars + vignette; page `title` sits over the top bar | UI kit, no art |
| 2 Set piece | The page's existing `artPath`, centred in the upper stage | `Art/Events/<event_id>/` |
| 3 Speaker | Bust, ~840px tall, bottom-anchored against its side edge, overlapping the box | `Resources/Portraits/Dialogue/<characterId>/<expression>.png` |
| 4 Dialogue box | 1280x256 box + 480x96 name plate (name + gold epithet), on the side opposite the bust | UI art (v1: existing 5x1 plates) |
| 5 Choices | Appear after the last line; a press already in flight when they appear is ignored, the first press that starts after they appear acts normally; locked ones show their reason | existing EventChoiceGate |

## Behavioural contracts

### Routing and content
1. Every choice's last outcome is unconditional (`Domain/Content/EventEntryResolver.cs:~348` already refuses otherwise). That unconditional outcome is the fallback dialogue variant, so it must be narration-only or use only guaranteed speakers. A "Shawn-only" variant gated on `inParty sheep` is a conditional variant, not the terminal one -- see the worked event below.
2. Counter timing: choice-level effects apply before outcomes are evaluated (`Core/Bot/RunOrchestrator.Event.cs:214` then `:218`); outcome-level effects apply after the pick is resolved. Either convention is valid, but an event states which it uses. The worked example uses an outcome-level bump gated on the pre-increment value (the Petting Zoo convention).
3. Result ordering after a pick: the outcome's `result` (if any) plays first as narration, together with the effects summary line, then the destination page's lines, then its choices. Replay (resume, repaint) is presentation only, never calls `ChooseEventOption`, so effects never re-apply. A concluded event (`eventPageId` empty) shows the result as narration plus a single Leave, no bust. It plays on the stage when the event has lines on any page (`ResolvedEventDefinition.HasAnyLines`, `EventView.Staged`), otherwise in the legacy layout -- nothing persisted, so a resume paints what the live pick painted (D4).
4. Cast scope for v1: a speaker is a party character id or `narration`. Event-local speakers (a host, a merchant) are out of scope, stated as later work. Presence is checked at content build by a forward dataflow over the page graph: the characters guaranteed present at a page = intersection over every incoming edge of (event-level requires + choice requires + outcome requires + what was guaranteed at the source page). The start page gets event-level requires only. Loops iterate to a fixpoint. A named `inParty`, `memberLevel` or ability row all imply presence (`EventRequirement.cs:158-181`, `namedInSquad`). A speaker not guaranteed at its page is refused at build.
5. Sides: a page may declare `cast` [{character, side}]. A speaker not listed defaults by order of first appearance on that page: first distinct speaker left, second right, third left, alternating. A page-level `backdrop` overrides the event-level one.

### Presentation
6. Layer 2 is the page's existing `artPath` field (baked at scene build, `Art/Events/<event_id>/`, 960x720 per `docs/EVENTS.md`). No new `setPiece` field.
7. Layer 0 is the new event-level `backdrop` (Assets-relative, baked at scene build like `artPath`), overridable per page, default `Art/Backgrounds/Dungeon.png`. Drawn cover-crop (fill the canvas at every aspect, preserve aspect, crop overflow), never stretched. No `AspectRatioFitter` exists in the project; the cover-crop is computed by the controller or UiKit.
8. Layout at 1920x1080 reference: dialogue box 1280x256, bottom margin 32, on the side opposite the bust. Name plate 480x96, overlapping the box's top edge on the speaker's side, two lines (name + gold epithet). Bust ~840px tall, bottom-anchored, against the screen edge on its side, overlapping the box. Narration: no bust, no name plate, box centred, italic text. Owner rule 2026-09-23, "no stretched container art": the box and name plate are painted at final size, never stretched. v1 placeholders: the UI kit's existing 5x1 container plates (they fit the 5% container aspect lint). Final art ships in the designer handover: box 2560x512, plate 960x192, delivered to `Art/UI/Dialogue/`.
9. Busts: all three characters are painted facing the viewer's right. Left-side speakers show as painted; right-side speakers are mirrored, so a speaker always faces inward. The painting's right edge must be clean -- Odette's is; Shawn's (9% cut) and Bjorn's (19% cut) are being repainted per the art handover.
10. Bust delivery follows the plate precedent (`tools/normalize_pc_plates.py`): a new `tools/normalize_dialogue_busts.py` reads `Art/Portraits/<Folder>/Processed/<Name>_<expression>.png`, pads every expression of one character onto one shared canvas with one registration (bottom-left anchored; measured silhouette IoU between expressions is already 0.93-1.00), and writes `Resources/Portraits/Dialogue/<characterId>/<expression>.png` plus a `recipe.json` that replays byte-identical. Runtime loads by Resources path, so new expression art needs no scene rebuild. Character content gets `dialogueBustPath` (Resources-relative folder, no extension, optional) and `epithet` (string, optional).
11. Expressions: neutral, happy, annoyed, nervous, sad, surprised (a content enum, refused if unknown). Runtime fallback: requested, then neutral, then no bust (name plate and text still show). Today Shawn has no neutral (commissioned) and Bjorn has only neutral. Content build WARNS, never refuses, on a missing expression file -- art arrives after content.
12. Rich text: `<i>` only. `<b>` is refused at build unless D2 confirms the TMP font renders bold (`Domain/Combat/ModifierEffectText.cs:37-41` records no confirmed bold glyph). Any other tag is refused.
13. Caps: at most 12 lines per page, at most 200 characters per line (D2 re-pins the cap by measuring the longest line at 4:3 and 21:9). Epithet at most 32 characters. An outcome `result` plays as a line, so it takes the 200 cap when the page that owns the choice has lines or its goTo page has lines; a result between line-less pages keeps the body's 600 (owner-side call, D4).
14. A page with no `lines` behaves exactly as today: body shown instantly with choices at once, no typewriter, no stage transition. `demo_wishing_well` stays untouched as the compatibility fixture.
15. Graceful degradation: no bust -> name plate and text still show; no set piece -> layer 2 hidden; no backdrop -> solid dark. Never a runtime error.
16. UiAudit passes at all four aspects; the bust/box overlap is an `AllowOverlap("...")` with its reason.

### Input
17. Only Submit/A/Enter and a left click advance. The D-pad does nothing while lines play, and navigates rows once choices are shown, using the existing 2026-09-18 selector (small arrow marker) on the armed row. Cancel stays inert in events (existing contract, `Core/EventController.cs:22`). Start opens the system menu and suspends playback. Every advance press is consumed once. While the rows appear, any press that began before they appeared is ignored; the first press that starts after they appear acts normally and picks the focused or clicked choice, so the press that finishes the last line can never also pick a choice. Submit-with-nothing-selected needs a new claim hook in `Core/NavigationInputModule.cs`, like the existing `INavCancelClaim`; D3 adds it. **(assumed)** No skip-all in v1 -- a press only completes the current line or advances, it never fast-forwards the rest of the page.
18. Save/resume mid-dialogue: the line index is not persisted, so a resume replays the result (if `eventResult` is non-empty) and then the page from line 1. This is why it restarts; `ReconcileOpenEvent` only repairs a missing page.

## Worked event (Petting Zoo sheep step, validator-legal)

```
page pen (start): lines [narration intro]; choices:
  "Pet the sheep":
    outcomes (counter read BEFORE this pick's bump, which lives in outcome effects):
      {counter sheep_visits 0..0, inParty sheep, inParty owl} effects [counter +1] -> step1_shawn_odette
      {counter sheep_visits 0..0, inParty sheep}              effects [counter +1] -> step1_shawn
      ... one pair per step ...
      {} -> pet_narration   (unconditional, narration-only, no bump)
  "Leave": outcomes [{} -> Leave]
```

The common intro page is intended: the variant plays after the player chooses to pet.

## Transition table

| State | On enter | Submit/A/click | Other input |
|---|---|---|---|
| Entering | Bust slides in (~0.2s) | Ignored | Close mid-slide: snap to final, kill tweens |
| Revealing | Typewriter runs (TMP `maxVisibleCharacters`) | Completes the line, consumed | Start: Suspended |
| Revealed | Line full | Next line -> Revealing; different speaker -> Transitioning; after the last line -> Choices | D-pad: nothing |
| Transitioning | Old bust out to its edge, new one in (~0.2s); same speaker with a new expression swaps in place, no slide | Ignored | Close: snap |
| Choices | Rows shown; a press already in flight is ignored | First press that starts after rows appear picks the focused/clicked choice, then normal button behaviour | D-pad navigates rows |
| Suspended | System menu open, typewriter paused | Goes to the menu | Menu closed: back to the prior state |

Resume from save: Result (if any) -> page line 1.

## Data additions

- Character: `dialogueBustPath` (Resources-relative folder, no extension, optional), `epithet` (string, optional, <=32 chars).
- Page: `backdrop` (Assets-relative, optional, overrides event backdrop), `cast` [{character, side}], `lines` [{speaker, expression, text}].
- Event: `backdrop` (Assets-relative, default `Art/Backgrounds/Dungeon.png`).
- No `setPiece` field -- layer 2 reuses the existing `artPath`.

## Phases (one chat; commit each only when its gate is green)

- D0 Busts: `tools/normalize_dialogue_busts.py` + Resources output for current art + recipe, content fields `dialogueBustPath`/`epithet`. Re-run when the repainted art lands.
- D1 Data: page lines/cast/backdrop, event backdrop, the presence dataflow, expression/tag/length checks, `docs/CONTENT_SCHEMA.md` regenerated, tests.
- D2 Stage: layers in `Domain/UiKit/Screens/EventScreen.cs`, `ScreenRegistry.WireEvent`, `AllowOverlap` for bust/box with reason, cover-crop, scene rebuild (`-BuildScenes`).
- D3 Playback: the state machine above, the typewriter, slides, the Submit claim, result ordering, resume. As built: `Domain/Events/DialoguePlayback.cs` (60 chars/s, 0.2s slides, press frames not durations, a `Closed` terminal state beyond the table's six), `INavSubmitClaim` in `Domain/UiKit/INavCancelClaim.cs`, the stage click as a runtime `PointerPressRelay` on the stage root (no new node, no new SerializeField). Rows are hidden as a panel until Choices. (D4 replaced the concluded rule: see contract 3.)
- D4 Fixture + captures: a separate fixture event `demo_dialogue` (two speakers, narration, result, missing bust, four choice rows locked and open; `demo_wishing_well` untouched). Named captures: longest line and epithet, both sides, narration, missing bust, result, four choice rows, at all four aspects, plus a runtime overflow check on the box text (UiAudit only sees build-time geometry). Captures steal focus, so they run when the owner is away.

## Open owner calls (non-blocking)

- Epithets for Shawn, Odette and Bjorn.
- The art handover (Shawn_neutral, clean right edges, box/plate).
- Mirroring is assumed OK.

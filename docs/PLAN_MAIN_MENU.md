# Main menu redesign

Owner-approved 2026-09-29. Placeholder logo for now (the existing gold
`TitleLabel`); a commissioned lockup replaces it later without layout change.

## Behavioural contracts

**C1 Composition.** No `TextScrim` container. A dark gradient down the left
edge only, for text contrast; it fades out before the terrace lantern at art
pixel (276, 708) = canvas (-643, -272) so the ambience glow there is not
dimmed. Title and menu share one left edge inside the safe area; nothing
covers the palace (right of canvas x = -100). Four-frame `UiAudit` passes.

**C2 Menu column**, top to bottom, built from the items visible right now
(hidden items close the gap; no item is positioned outside the column):

| Item | Visible | Default focus |
|---|---|---|
| Continue (+ subline) | a save exists | yes when shown |
| New Run | always | yes when no save |
| Load | always | |
| Settings | always | |
| Quit | always | |

**C3 Continue** enters `SaveSystem.MostRecentSlot()` and goes to the hub,
no slot list. Subline: character, floor, playtime from `SaveSlotDetail.For`.
Re-evaluated after any delete (existing `RefreshContinue`).

**C4 New Run** enters the lowest empty slot (`SaveSlotController.Choose`
path). All five full: opens the Load list instead; it never overwrites.

**C5 Load list**: one modal, five cards. Filled card: enter it. Empty card:
new run there. Every filled card carries its own Delete, opening the
existing confirm dialog (focus on No, 1.2 s hold). The Manage Saves panel,
its button and its Back are removed. Pad: Up/Down between cards, Right to
the card's Delete, Left back.

**C6 Hold on every device.** `HoldToConfirm` owns the pad/keyboard hold:
while its own GameObject is the selected object and Submit is held, it
holds; release, deselect or disable cancels. `ExitsController`'s private
copy of this polling goes. Pointer behaviour unchanged. Unscaled time.

**C7 Settings** opens the existing `OptionsScreen` as a modal over the
menu, same controller, same `GameSettings` persistence.

**C8 Cancel** closes the topmost layer: confirm, then Settings or Load; a
no-op at the base menu.

## Phases (each a green, committed checkpoint)

1. C6 hold seam.
2. C1-C4 composition and menu column (Play/Manage Saves still reachable
   through Load until phase 3).
3. C5 Load list with per-card delete; Manage Saves removed.
4. C7-C8 Settings modal.
5. Captures at 1080p for owner review.

Credits and a "press any button" title gate are out of scope.

## Status

Phases 1-5 landed 2026-09-29: d7263825, 1dd862a3, 63b8fa6d, 2e7088b3,
1f41d77d (Options note clear of Restore), ef08ff10 (Cancel close cue only
when a layer is open). Delete lives in `SaveSlotController`;
`ResetProgressController` is gone. Open: commissioned logo replaces the
`TitleLabel` text; whether menu items drop their plates for plain text.

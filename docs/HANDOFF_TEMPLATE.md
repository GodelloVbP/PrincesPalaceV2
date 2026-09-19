# Handoff Template

The shape a design handoff should take, and what happens to it after the
build lands — derived from the pattern that already worked across five real
handoffs (`run_map`, `relic_screen`, `shop`, `talent_tree`, `battle_ui`).

## When a handoff is required

Any new screen, or any major rework of an existing screen's layout or
interaction model. Not required for a bug fix, a balance tweak, or an
additive change that doesn't change a screen's structure.

## Required README sections

The handoffs that shipped fastest and needed the fewest clarification
rounds were the ones that pre-answered questions before they were asked.
Structure the README around:

1. **Overview** — what this screen is, one paragraph, and what it replaces
   if anything.
2. **Files** — the prototype file(s) this hands off, and which one is
   authoritative for behavior vs. which is just a visual reference.
3. **Layout** — exact coordinates against a stated reference resolution
   (this project uses 1920×1080). Every element's position and size, not
   just a description of where things roughly go.
4. **States** — a table of every visual state an interactive element can be
   in (locked/available/invested, equipped/held-elsewhere/unclaimed, etc.)
   and exactly how each reads (color, border, glow, badge text).
5. **Interaction/prerequisite logic** — the actual rules, spelled out
   unambiguously enough that "click to invest" doesn't need a follow-up
   question about what happens on a second click, what un-does an action,
   or what's disabled vs. hidden.
6. **Design tokens** — the color/font/spacing vocabulary, so a new screen
   reads as part of the same suite rather than its own thing.
7. **"For the engine" notes** — anything the prototype's own tooling faked
   that the real implementation needs to do differently (e.g., "this mock
   recomputes X client-side; the real version reads it from Y").
8. **Explicit out-of-scope** — what this handoff deliberately does *not*
   cover, so a gap doesn't get read as an oversight.

## Prototype conventions

- One prototype file (`<Name>.dc.html`) + its `support.js` + (when useful) a
  `reference_screenshot.png`, all together in `docs/handoffs/<slug>/`.
- `<slug>` is lowercase, underscore-separated, no spaces — matches the
  folder structure already in place (`docs/handoffs/archive/relic_screen/`, not
  `docs/handoffs/Relic Screen/`).
- The interactive prototype is authoritative for **behavior and layout**;
  a reference screenshot (when present) is a secondary visual anchor, not
  a replacement for reading the interaction spec.

## The GAP_AUDIT.md format

Once a handoff is substantially built (or claimed to be), audit it
section-by-section against the README rather than assuming a build that
compiles and runs matches the spec. Write `GAP_AUDIT.md` in the same
handoff folder:

| Handoff section | Spec'd | Built | Verdict |
|---|---|---|---|
| *(one row per README section or discrete requirement)* | *what the doc asked for* | *what's actually in the code, file:line* | *match / partial / missing / deliberate-deviation* |

- **deliberate-deviation** needs a one-line rationale (ideally citing an
  existing code comment that already explains the choice) — this is for
  cases where the build intentionally didn't follow the spec for a good
  reason, not a placeholder for "we'll get to it."
- Strike a row (or update its verdict) as the corresponding gap closes.
  The file stays in the handoff folder as a permanent record of what was
  actually checked, not a scratch document that gets deleted once green.

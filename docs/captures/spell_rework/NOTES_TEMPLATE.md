# <Spell name>: state record

Copy to `docs/captures/spell_rework/after/<spell_id>/NOTES.md`. This file, not
one agent's memory, carries the spell across rounds. Rules it records against:
`docs/SPELL_FEEL.md` (identity, rubric, checks, polish menu). Target: under
about 120 lines, excluding the art prompt blocks.

Writing rules:

1. **Replace, don't append.** Edit the section in place; a changed fact
   overwrites the old one.
2. **Superseded playback and checks are deleted.** Only the latest round's
   playback and check results stay, as Open items or Snapshot lines. Older
   rounds are one Round log row.
3. **Frame numbers over adjectives.** "Ring covers the face f38-f46", not
   "the ring is a bit large". Name the capture run id once, in Snapshot.
4. **No narration of attempts.** Record what is true now and what to do next.
   A rejected approach is one clause in the Round log, only if a worker could
   repeat it.
5. **History lives in the Round log.** One row per round, nowhere else.
6. **One home per fact.** Rubric text, check definitions and tool usage are
   linked from `SPELL_FEEL.md` and `ART_PIPELINE.md`, never restated.

## Status

<One line: step (1-7 of `docs/PROPOSAL_SPELL_WORKFLOW.md`), last commit, what it waits on (owner, art, worker).>

## Identity

Locked before blocking; the owner confirms or edits it with the first GIF.

- **Verb:**
- **Signature beat:**
- **Must read as:**
- **Must NOT read as:**
- **Motion reference per moving element** (physics or a real thing, never invented parameters): `<layer>`: <e.g. a censer on a long chain>
- **Intentional exceptions** (so binding checks do not flag them):

## Beat table

Authored seconds on the release clock; hit cue at <t> s (frame <n> at 1x, <m> at 1.5x). Box = the layer's size in px.

| t | Beat | Layer | Art piece | Box / feel |
|---|---|---|---|---|

## Open items

Numbered, one line each, frame-referenced. Mark **owner call** where the owner decides.

1.

## Decisions

Owner and orchestrator answers, dated, one line each. Each question is asked once.

- YYYY-MM-DD (owner|orchestrator): <answer>

## Art

| Piece | File | Recipe | Status (accepted / rejected / stand-in) |
|---|---|---|---|

Step-4 prompts, verbatim. These are the canonical source for
`docs/prompts/<spell>_spell_art_batch.md`; keep them complete.

```
<prompt block per piece, per docs/prompts/TEMPLATE_spell_art.md>
```

## Round log

| Date | Round | What changed | Caught by | Commit |
|---|---|---|---|---|

## Measures

| Measure | Value |
|---|---|
| Owner reviews (target 3) | |
| Internal retries | |
| Attempts per art piece | |
| Active / waiting time | |
| Owner handling time | |
| Owner-found defects the checks should have caught (target 0) | |
| Quality vs reference pack (owner's verdict) | |

## Snapshot

Written at step 7; the capture the owner approved, the assets and the gate describe this one commit.

- **Commit:**
- **Capture run id:**
- **Gate:** <command, result>
- **Binding checks:** <pass/fail per check, `docs/SPELL_FEEL.md`>
- **Rubric (motion, art):** <✔/✘/~ per item>
- **Still weak:**

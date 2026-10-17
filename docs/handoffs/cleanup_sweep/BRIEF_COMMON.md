Do NOT use the Agent tool. Run every command in the foreground; never wait on a background task.

# Cleanup sweep: common brief

Plan: `docs/PLAN_CLEANUP_SWEEP.md`. Do not read it unless your suffix names a section.
Base commit for slice line numbers: `c8d0c631`. Cite every location as
`path:line@c8d0c631 (Symbol)` so it survives other lanes' edits.

## Finding contract

Categories:
- BUG: a reachable wrong behaviour, crash, save corruption, or wrong number shown.
- PERF: cost on a hot path (per frame, per beat, per cast, per scene load) or in the
  dev loop. Name the call frequency.
- SIMP: a deletion or collapse that keeps behaviour.
- DRIFT: a comment, doc or schema line that is false today.

Severity:
- S1: player-visible or data loss.
- S2: latent; reachable but rare.
- S3: PERF or SIMP.
- S4: DRIFT.

Intent precedence, highest first:
1. The Decided list below, or an AUDIT entry.
2. Tests with pinned literals.
3. Docs.
4. Content data.
5. Comments. A comment is a lead, never a spec.

A BUG needs two agreeing intent sources, or one Decided/AUDIT line. Anything else
is `owner-fork`.

States: `candidate`, `confirmed`, `disproven`, `owner-fork`, `awaiting-repro`,
`fixed`, `filed`. Never drop a finding for lack of a repro.

Grades: `asserted` (a test went red, then green), `executed` (ran it and saw it),
`static` (read only).

"Dead" means zero references in all of these:
- C#;
- UiAutoBind/ScreenRegistry exact-field-name binding;
- `[SerializeField]` names in scenes;
- `Resources` path strings;
- ContentData keys and enum names;
- reflection, tests and tools.

Out of scope:
- balance numbers;
- art;
- comment density;
- new features;
- anything on the Decided list.

Zero findings is a fine result. Coverage beats speed.

Row format, one line per finding:
`id | slice | path:line@c8d0c631 (Symbol) | cat | sev | state | grade | contract (source) | failure: state -> wrong result | proposed fix | lane`

## Git rules (writers only)

In the main tree, never run `git checkout`, `restore`, `reset`, `stash` or
`clean` on any path. In your own sweep worktree, `git rebase`, `git rebase --abort`
and `git switch -C sweep/<lane> main` are allowed. Stage explicit paths only, and
include the `.meta` of any new file. Commit in one invocation:
`git add <paths> && git commit --only -F <msg> -- <paths>`. Then confirm with
`git log -1 --format='%h %s'` and quote the sha. A non-zero exit is a failure to
report, not to retry blindly. The message names the ledger id and ends with
`Co-Authored-By: Claude <the model you run on> <noreply@anthropic.com>`.

Sweep working files (notes/, scan/, ledger/) go in `C:\Games\sweep-2026-10-17\`,
never inside the repo.

## Decided (do not report against these; cite the source if relevant)

- An extra turn (Trample, Bloodlust) re-pays only lock resets, the pool tick and
  the Runic/necklace recompute. Source: AUDIT #113, c477205f.
- Bloodlust is one extra turn on the first kill of each fight. Trample is counted
  separately. Shatter is one explosion per warded ally. Source: 84d35c95.
- The party max is 3: EffectiveMaxSquadSize caps at StageSlotsPerSide. Source: owner decision 2026-09-24, 84d35c95.
- Benching survives a reload. Source: #118, 82385df6.
- Continue opens the last played save. Source: #117, b8242045.
- The starting kit is removed. Item sets are not re-authored until the item
  redesign. Source: #114; item sets wait for docs/PLAN_ITEMS.md.
- Shawn is "he". Source: #130.
- Bog witch is weak to Wind/Arcane and resists Water/Earth. Source: #132.
- Times downed counts teammates; damage taken has no overkill. Source: #124(a), 25a0333c.
- The HUD plate column follows a Move. Source: #144.
- Reward collection is manual. The auto-claim was removed on purpose.
- No hover scale anywhere; HoverRim is the default. Source: 47358962.
- There is no exact-60 ability budget. The 1-30 per-score check stays.
- No global `Time.timeScale` hit-stop. Source: CLAUDE.md.
- Only Shawn, Odette and Bjorn are real characters. Turtle, Fly and Dog are
  placeholders.
- Every monster and form has a head point.
- Cancel steps back one level first.
- The top-tier reward reveal stays as it is. Card faces are opaque.
- The Magical Shield ward lasts 1 turn. Source: 320c49f1.
- AUDIT.md open entries are known. Cite the #; do not re-report them.

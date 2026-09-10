# docs/hunt

- `MANIFEST.md` -- the hunt's ledger. One generated row per scoped file (Domain,
  Core, Data, Editor, tools, hooks, ContentData, stance manifest, art recipes,
  rule-bearing docs), plus hand-authored sections: the cross-file obligations
  (the four seams, the open leads) and the clearance ledger.
- `SCENARIOS.md` -- the Stage 3b lifecycle scenarios, written before any finder
  runs. Every row's required evidence kind is `asserted`.

Regenerate: `python tools/hunt_manifest.py` (no arguments, no dependencies;
`--check` exits 1 if the file would change, for a hook or a gate).

The script MERGES rather than overwrites: `owner`, `status`, `evidence`,
`revision` and `invalidated-by` are carried forward per row, a row whose file
has moved past its recorded revision is flipped to `re-open`, new files arrive
as `open`, and a file that left the tree drops into the removed section keeping
its evidence. Everything OUTSIDE the `HUNT-MANIFEST:BEGIN`/`END` markers is
preserved verbatim, which is what makes the hand sections survive -- so edit
them freely, and never edit between the markers.

The manifest is a working document, not a gate: nothing in
`run_tests_parallel.ps1` runs this script.

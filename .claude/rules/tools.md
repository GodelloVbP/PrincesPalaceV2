---
paths:
  - "tools/**"
---

# Tools rules

- `.ps1` files: ASCII outside comments, no BOM (`tools/githooks/pre-commit`).
- Every path in this project contains a space and an apostrophe: quote
  every path; split command output on tab, never on space.
- Every Unity launch goes through `Start-UnityQuiet`
  (`tools/unity_path.ps1`), never `Start-Process` on `Unity.exe`. Batchmode
  runs on the hidden desktop `WinSta0\PPHeadless`
  (`PP_GRAPHICS_DESKTOP=visible` opts out); `preview.ps1 -Launch` stays visible.
- Every entry point that can spawn a window calls `Start-FocusGuard` near
  its top and `Stop-FocusGuard` in a matching `finally`. After touching a
  launch site or either guard, run
  `tools/focus_check.ps1 -Mode Command -CommandScript tools\<script>.ps1 [args]`;
  it reports `FOCUS CHECK` and `COMMAND` separately.
- Capture launches pass `-ppReferenceScreen` so play mode runs at 1920x1080
  (`Editor/CaptureReferenceScreen.cs`).
- A tool that mirrors into or launches a `-TestRunner*`/`-Bot*` copy first
  takes it with `Enter-RunnerClaim` (`tools/unity_lock.ps1`) and holds it to
  exit; a child passes through its parent's claim. A Unity lockfile is
  cleared only via `Clear-StaleUnityLock`.
- A tool that can overwrite art refuses anything registered in
  `Assets/_Project/Art/Sheets/hand_assembled.json`.
- Test a destructive tool on a throwaway copy first, never on live data.
- `tools/githooks/comment_history.py` enforces CLAUDE.md's "comments say why,
  present tense" on added `*.cs` comment lines carrying an ISO date; runs
  inside `tools/githooks/pre-commit`, or standalone via
  `git diff --cached -U0 -- '*.cs' | python3 tools/githooks/comment_history.py`.
- `tools/check_comment_only_diff.py` guards a comment-sweep commit so no code
  line slips in: `python3 tools/check_comment_only_diff.py [--cached | rev..rev]`.

---
paths:
  - "Assets/_Project/ContentData/**"
  - "Assets/_Project/Scripts/Domain/Content/**"
  - "Assets/_Project/Scripts/Core/Content/**"
  - "Assets/_Project/Scripts/Editor/ContentBuilder.cs"
---

# Content rules

Content is generated from `ContentData/*.json` by `ContentBuilder`
(`CLAUDE.md` rule 2); fields and enums: `docs/CONTENT_SCHEMA.md`.

- A new `Raw*Entry` field gets a `[ContentDoc]`; run
  `tools/content_schema.ps1` and commit `docs/CONTENT_SCHEMA.md`.
- Ordering (`CLAUDE.md` gotcha 4): most types return their authored
  `sortOrder`; a type sorted by another key (spell tier `level`, talent grid
  position) returns that key instead of a second authored int.
- A resolver refuses the whole type when any row fails.
- A Domain record stored inside a `ScriptableObject` cannot be `readonly`:
  Unity skips readonly fields and it round-trips as `default`. It is a plain
  mutable struct, written once by the resolver.
- An id written into save data is never renamed once a save exists.

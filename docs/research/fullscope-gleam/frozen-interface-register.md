# Frozen-Interface Register — pointer

The authoritative frozen-interface register for feature 059 (full-scope Gleam GLP) is:

**[`phase2-plan/frozen-interface-register.md`](phase2-plan/frozen-interface-register.md)**

## Why this file exists

The FINAL Phase-2 outline plan was produced by three *blind* builders, and two of them named different paths for the register: `docs/research/fullscope-gleam/phase2-plan/frozen-interface-register.md` (11 acceptance references, builder-1's freeze WPs) and `docs/research/fullscope-gleam/frozen-interface-register.md` (6 references, builder-2's freeze WPs). Both are legitimate acceptance evidence for their WPs.

Rather than silently pick a winner and leave six acceptance checks pointing at a missing file, wave 1 writes **one** register at the majority path and leaves this pointer at the other. Every wave-1 acceptance check therefore resolves from a fresh session, and no entry content is duplicated (duplication would itself become a drift source).

Entries named by builder-2 WPs — `body-kernel`, `bytecode-runner`, `guard-kernel`, `link-layer`, `module-system`, `embeddability-api` — are all present in the authoritative file.

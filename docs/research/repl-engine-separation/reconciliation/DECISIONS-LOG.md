# Ratified Decisions Log — `engine-separation` epic

Owner-ratified pre-`/buildkit-specify` decisions. Each is **binding** on the named seed's spec unless the owner revisits it. Companion: deferrals these create are tracked in [`DEFERRALS.md`](DEFERRALS.md) (anchored to their revisit-stage). Source: the 17-seed reconciliation (`DECISIONS-FOR-OWNER.md`).

**Ratified 2026-06-09** (owner: Gabi) — all 12 MVP-critical advisories accepted.

| # | Decision (ratified) | Applies to | Creates deferral |
|---|---|---|---|
| R1 | **Var→writer identity on the wire = stable `GlobalVarId(agent:localId)`** (`payload_serializer.cs:85-88`), never raw heap ints (heap addresses are unstable across restart and meaningless across instances). | #2, #5 | — |
| R2 | **Unbound-var encoding in Suspended results = display-only for MVP** (`null` = top-level-unbound + separate suspension fields). | #5 | **DEF-C2** (full round-trip) |
| R3 | **Output field = length-prefixed UTF-8 blob, included in the MVP envelope.** | #5, #6 | — |
| R4 | **Dart-mirror byte-parity for the result codec = deferred** (ship #6 C#-only; keep the codec parity-able). | #5, #6 | **DEF-A1** |
| R5 | **Host layout = a new `glp_engine_host/` project** (not a `--server-mode` flag on `glp_repl`). | #6 | — |
| R6 | **Envelope field set for MVP = ground-only subset**; server pre-renders bindings to strings. | #6 | **DEF-C1** (full §2.3 set) |
| R7 | **Client = thin terminal** (no local `self.glp` context). | #6 | — |
| R8 | **Per-seed metric table = shared Markdown template** `name \| kind \| tool \| threshold`. | #1a | — |
| R9 | **Shapiro-criteria mapping = mandatory** for language/semantics/wire seeds; **advisory (N/A + justification)** for host/infra seeds (#8, #10). | #1a | — |
| R10 | **Lean 4 on Windows = add a WSL2/container setup note** to #1a (Lean-LSP-MCP / Lean Copilot are Linux/Mac-first; cwd is `D:\`). | #1a | **DEF-F-tooling** (AutoRocq adaptation if Rocq chosen) |
| R11 | **Formal Lean/Rocq proofs are OFF the MVP critical path** (#6 is a source-text split); proofs gate only language-touching seeds (#4, #11, #12). | #1a, #6 | **DEF-B1** |
| R12 | **Binding depth-truncation bound = 32 for MVP** (sets the Lean proof scope; revisit if cycles/large terms surface). | #2 | **DEF-C-depth** (revisit bound) |

**How a seed's spec consumes this:** at `/buildkit-specify` for seed N, `buildkit-roadmap brief <id>` shows a `PRE-SPECIFY` pointer to this log + `DEFERRALS.md`. Apply every R-row whose "Applies to" includes N, and action every DEF-row anchored at N.

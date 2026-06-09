# Quickstart: Iterative Refinement & Verification Framework

**Feature**: `027-refinement-verification-framework`

Two audiences: (A) an **engineer specifying a successor seed** (#2–#16) who needs to instantiate the
framework; (B) a **reviewer re-running the three validation spikes** to confirm the methodology empirically.

---

## A. Instantiate the framework for a successor seed

At the start of a successor seed's `/buildkit-specify`:

1. **Pull the PRE-SPECIFY pointer** — `buildkit-roadmap brief <seed-id>` surfaces `DECISIONS-LOG.md`
   (apply every R-row whose "Applies to" includes this seed) and `DEFERRALS.md` (action every DEF anchored
   at this seed). *(FR-061)*
2. **Copy the metric-combination template** from
   `docs/research/repl-engine-separation/reconciliation/METRIC-COMBINATION-TEMPLATE.md` and fill one table:
   `name | kind (pragmatic|formal) | tool | threshold`. *(FR-003)*
   - Language/wire/byte seed → **must** include ≥1 `formal` row (Lean 4, byte-parity oracle, or SPIN). *(FR-021)*
   - Host/infra seed (#8/#10) → may omit formal, but record a per-Shapiro-criterion N/A justification. *(R9)*
3. **Pick the protocol tool** (if the seed has a wire/concurrency surface) from
   `PROTOCOL-VERIFICATION-ARMOURY.md` — SPIN is the default; escalate to TLA+/UPPAAL/nuXMV/mCRL2/FDR4/CADP per
   the selection guidance — and record the choice + rationale. *(FR-079)*
4. **Map the Shapiro criteria** for the seed type from `REFINEMENT-METHOD.md` §5 — mandatory vs advisory. *(FR-050)*
5. **Run the refinement loop** per `REFINEMENT-METHOD.md` §1 (the `optimize.py:257–335` shape): generate →
   propose → evaluate against the table → repeat, budget-capped, **all LM steps in Claude/MCP, no API**. *(FR-010–013)*
6. **Owner confirms** the table at the interactive spec step; the confirmed table is recorded in the seed's
   spec **before** any task is generated. *(FR-060)*

---

## B. Re-run the three validation spikes

Each spike lives under `docs/research/repl-engine-separation/spikes/<tool>/` with a committed reproduction
command, pinned `tool-versions.txt`, and a recorded `RESULT.md`. Real tools required — desk research does NOT
satisfy R13/R14.

### B1. Lean 4 tactic-loop spike  *(FR-035 → SC-006/010)*
```
# In WSL2/Ubuntu (Lean tooling is Linux/Mac-first, R10):
#   elan installs Lean; lake builds the project; Lean-LSP-MCP exposes the kernel to Claude.
cd docs/research/repl-engine-separation/spikes/lean
bash run.sh          # drives the bounded Claude-over-MCP tactic loop on one GLP property
cat RESULT.md        # outcome (proved | sorry-isolated) + tactic-attempt count
```
Expected: the chosen GLP property (SRSW preservation on a toy clause) is proved, or `sorry`-isolated and
escalated as an owner obligation — never silently dropped, never unbounded (budget start 20, tuned).

### B2. MLIR round-trip spike  *(FR-043 → SC-007/010)*
```
# pip wheel if available, else WSL2 LLVM build with -DMLIR_ENABLE_BINDINGS_PYTHON=ON
cd docs/research/repl-engine-separation/spikes/mlir
python harness.py    # builds the 4 GLP/FCP primitives; asserts decode(encode(p)) == p
cat RESULT.md        # pass/fail on the minimal IL fragment
```
Expected: round-trip identity holds on the minimal fragment (Claude generates structure; the deterministic
oracle decides pass/fail).

### B3. Promela/SPIN handshake spike  *(FR-080 → SC-011/012/010)*
```
cd docs/research/repl-engine-separation/spikes/spin
bash run.sh          # spin -a front_back.pml && gcc -o pan pan.c && ./pan -a   (or WSL2 fallback)
cat RESULT.md        # deadlock-freedom + progress verdict, or counterexample trace
```
Expected: no deadlock, no unspecified receptions, named progress/liveness property satisfied (or a
counterexample is surfaced). Minimal handshake only — full protocol model deferred to #5/#6 (DEF-A3).

---

## Done-ness check (maps to Success Criteria)
- Template + worked example present → **SC-001/008**
- Loop matches `optimize.py` seams; no-API grep clean → **SC-002/003**
- Six tooling slots enumerated → **SC-004**; five Shapiro criteria mapped → **SC-005**
- Three `RESULT.md` files recorded against real tools, reproducible → **SC-006/007/009/010/011**
- Armoury ≥7 tools with paradigm/engine/strength/best-for → **SC-012**

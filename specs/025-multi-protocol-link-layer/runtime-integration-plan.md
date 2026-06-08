# Runtime Integration & Two-Instance Real-Link Plan (feature 025)

**Created:** 2026-06-08 · **Owner directive:** Gabi (this session) · **Status:** ACTIVE, executing.
**Decision (Gabi):** vehicle for "two instances / real link" = **TCP over IPv4 localhost (127.0.0.1)**
(cross-process, same host; simpler than file — no file-tailing/monitoring; stream API maps directly onto
the seam). File transport (T041) and ws/wss (T060) come later. No auto-regen of `out/csharp` (codeconv is
one-way, manual) → wiring the kernels directly into the converted C# REPL boot is safe; making codeconv
preserve hand-edits is a separate future concern.

Terminology pinned: **C# / "ZHash"** = the .NET reference runtime (`out/csharp` + `csharp/glp_link`);
**".NET" in the directive** = the **Dart** runtime (`glp_runtime`), the *mirror* ("Dart equivalent ie
mirror implementation"). So: C# reference first, **Dart mirror** second, regression on **both**.

This plan **pulls forward** the Dart mirror (was T080, Phase 8) and integration/acceptance testing,
ahead of the C# transport leaves (T060–T065) and security (T070–T077). It supersedes the strict
phase order in `tasks.md` for the duration of this effort; `tasks.md` remains the canonical task list.

---

## 🔴 CURRENT STATUS — for restart (2026-06-08, head = `52d1c8ae`)

**Trust git log + this file + `tasks.md`; `marathon resume` is STALE (T012).** Resume order:
`buildkit-roadmap next` → spec dir `specs/025-…` → this file → `tasks.md` WIP pointer.

**DONE (committed, all green):**
- **Phase A** ✅ — kernels wired into the C# REPL boot (`out/csharp/glp_repl/Program.cs` sets
  `GlpRuntime.Repl.Program.AfterEngineCreated` → `LinkKernels.Install(engine.Runtime)` + registers
  `TcpTransport`/`LoopbackTransport`; the hook field + its invoke are in `out/csharp/bin/glp_repl.cs`).
  `_link_*` mirrored into C# `out/csharp/lib/analysis/type_checker/prelude.cs` builtinProcedures.
- **Phase B** ✅ — `csharp/glp_link/transports/TcpTransport.cs` (raw TCP/IPv4 127.0.0.1; 4-byte length
  framing; connect-retry; graceful close) + `LinkScheme.Tcp`. 99/99 xUnit (incl. 4 TcpTransport tests).
- Link types+wrappers **relocated into root `programs/self.glp`** (Gabi option A); Dart baseline 524/525.
- **Phase C (partial)** ✅ — `programs/tests/link/pc.glp` role-boot demo + **scripted driver**
  `test/link/run_link_tests.sh` → **4/4 two-process PASS** over real TCP: integers, strings,
  compound terms, and the explicit `link_send/3` wrapper (`producer_ls`). Results → `test/link/results/`.
- Two real bugs fixed (only surfaced via the live REPL, xUnit hid them): kernels now **deep-deref**
  nested struct args (`LinkTerms.GroundResolve`); GLP string constants carry quotes by design →
  kernels **strip** them (`LinkTerms.Unquote`).

**NEXT (in order):**
1. **Debug the explicit `link_recv`-chain consumer** — `link_recv` alone suspends correctly and
   `link_send` works, but a consumer doing 3 concurrent `link_recv` (threaded Link→Link1→Link2 with a
   `[A?,B?,C?]` head) fails fast before `server_listener` binds. Was the removed `sr.glp`; re-add once fixed.
2. **More examples** → driver: fault-monitor + close (likely needs the pump to fan `closed(eos)` to
   monitor cursors on a peer graceful-close), path-B request/accept over TCP, bidirectional (FR-003).
3. **Phase D — the Dart mirror** (`glp_runtime/lib/link/`): the 7 kernels + seam + reliability bundle +
   Loopback/Tcp transports + wire `LinkKernels.Install` into `glp_runtime/bin/glp_repl.dart` boot.
   The Dart kernels MUST replicate the two fixes above (deep-deref + quote-strip). Then run the same
   examples Dart↔Dart and cross-runtime Dart↔C#.
4. **Phase E — full regression** both runtimes (classical 1-instance + link 2-instance), scripted+captured.

**How to re-run the proof:** `(cd out/csharp/glp_repl && dotnet build)` then `bash test/link/run_link_tests.sh`.
Use `dart run bin/glp_repl.dart` (NOT the stale `glp_repl.exe`) for the Dart REPL.

---

## The 5 directive steps → sequenced phases (each ends in a verification GATE)

### Phase A — Wire link kernels into the C# REPL boot  *(directive step 1)*
- **A1.** `out/csharp/glp_repl/glp_repl.csproj` → add a project reference to `csharp/glp_link/GlpLink.csproj`.
- **A2.** In the converted REPL boot (`out/csharp/bin/glp_repl.cs`), at engine construction, call
  `LinkKernels.Install(engine)` and register the transports (loopback now; file transport after Phase B)
  into the returned `LinkRuntime.Transports`.
- **GATE A:** C# REPL builds; `dart`-side baseline untouched; load `programs/lib/link.glp` on the **C#
  REPL** → type-checks clean; a single-process smoke (in-process loopback, two engines or the xUnit path)
  shows a kernel actually executing. New task id: **T038** (boot wiring, C#).

### Phase B — TCP transport (cross-process, IPv4 localhost), C# side  *(enables directive step 2)*
- **B1.** New `csharp/glp_link/transports/TcpTransport.cs` (+ `TcpEndpoint`) implementing `ILinkTransport`
  for a new `LinkScheme` `tcp`: `ListenAsync` = `TcpListener` bound to 127.0.0.1:port (from
  `LinkId.Endpoint = ep("127.0.0.1", port)`); `ConnectAsync` = `TcpClient` to the same. One bidirectional
  socket per link. `SendBytesAsync` = write a length/CRC frame (reuse `FrameCodec`); `RecvBytesAsync` =
  `await` read one frame (naturally blocks, NO polling); `CloseAsync` = `Shutdown`/`Close` → peer read
  returns null (graceful). Abrupt = socket reset → fault. IPv4 only. P2P to the immediate peer (no broker).
- **B2.** Register `TcpTransport` (+ keep `LoopbackTransport`) in the C# REPL boot (Phase A) + the xUnit rig.
- **GATE B:** xUnit tests for `TcpTransport` (two endpoints over 127.0.0.1: round-trip, FIFO/in-order/
  exactly-once, fragment/reassembly, graceful+abrupt close). Advances **T041-analog** (first real leaf).

### Phase C — Example programs + role-boot, then two-process real-link tests  *(directive step 2)*
- **C1.** `programs/lib/` or `programs/tests/typed/link/` — role-parameterized boot program(s): one source,
  role chosen by ground `AgentId` (`=?=`), producer=connector / consumer=listener over `file` (**T037**).
  All exemplar GLP clears `contracts/glp-correctness-review.md` discipline; type-checks on both REPLs.
- **C2.** Numerous example pairs (producer/consumer; request/accept path-B; fault-monitor; close) — each a
  pair of goals run on **two `glp_repl.exe` processes** linked over the file transport.
- **C3.** A driver script (`test/link/` — PowerShell/bash) that starts the two processes, feeds goals,
  captures stdout, and asserts the split output == the unsplit single-instance baseline (SC-001 byte-identical).
- **GATE C:** ≥N example pairs pass two-process on C#; results captured to files. Advances **T040/T042-ish**.

### Phase D — Dart mirror: kernels + file transport + boot  *(directive step 3)*
- **D1.** `glp_runtime/lib/link/` — Dart mirror of `csharp/glp_link/` (the 7 kernels + seam + reliability
  bundle actually used + LoopbackTransport + FileTransport), behaviour/byte-identical (**T080**).
- **D2.** Wire Dart `LinkKernels.Install` + transport registration into `glp_runtime/bin/glp_repl.dart` boot.
  (Resolve the feature-020 "Dart golden frozen" note: confirm with Gabi if it blocks; link kernels are a
  new feature-025 capability both runtimes need.)
- **GATE D:** Dart REPL loads `link.glp` AND **runs** the Phase-C examples two-process on Dart; Dart↔Dart
  split == baseline. Then the **cross-runtime** Dart↔C# pair over file (**T042/T081** parity gate).

### Phase E — Full regression, both runtimes, scripted + captured  *(directive steps 4 & 5)*
- **E1.** One scripted rig runs **all** GLP code — every tutorial + test program — on **both C# and Dart**:
  classical examples single-instance; every new real-link example two-instance. Section R in
  `test/run_all_tests.sh` flipped on (**T051**), plus a captured cross-runtime acceptance log.
- **E2.** Capture/verify results to disk (pass/fail matrix per program × runtime × instance-count).
- **GATE E (acceptance):** all classical green on both runtimes (baseline 524/525 known-AOT preserved on
  Dart; C# suite green); all new link examples green two-process on both; cross-runtime parity green.
  Advances **T083** (full regression both REPLs) + **T081** (executed real-transport round-trip).

---

### Phase F — WebSocket/HTTP leaf  *(next iteration, after TCP proves out; Gabi)*
- `ws`/`wss` transport (HTTP upgrade + WS framing) as the web-interop leaf (**T060**), once the raw-TCP
  link is proven end-to-end on both runtimes. Same seam; adds the handshake + WS frame protocol on TCP.

> **Confirmed wired:** the C# `GlpEngine` run loop already drains `InboundPump` (`glp_engine.cs:554-560`,
> `708-714`: `while (pump.HasPendingOrLive) pump.TryApplyNext(InboundPumpWait)`), so once kernels are
> installed + a link is live, inbound frames apply on the runner thread — no extra C# core wiring for execution.

## Standing rules honored
- Spec-first; every new `.glp` clears the GLP-correctness discipline before promotion to a runnable test.
- Baseline gate (FR-067/SC-017): `bash test/run_all_tests.sh` green before AND after each core-touching change.
- Commit per task with the suites run; never push/merge without authorization.
- GLP invariants preserved exactly (SRSW / writer-MGU / three-valued / suspend-reactivate / bind-once / FIFO).

## Open items to confirm as they arise
- Dart REPL "golden frozen" (feature 020 R10) vs adding link boot wiring (D2) — confirm with Gabi if it bites.
- Exact file-transport close/rotation semantics (B1) — pick the simplest correct scheme, document it.
- "Numerous examples" / N for GATE C — start with producer/consumer + request/accept + monitor + close;
  expand until coverage is convincing.

---

## Phase D — DISCOVERED SCOPE & EXECUTION (2026-06-08, post-`/clear` session)

Read-only scoping of `glp_runtime` vs `csharp/glp_link` (~3077 LOC, 40 files) settled the
feasibility questions:

- **Body-kernel seam ALREADY EXISTS in Dart.** `glp_runtime/lib/runtime/body_kernels.dart`
  (`BodyKernelRegistry`, line 37) is the original the C# `body_kernels.cs` was converted from;
  `GlpRuntime.bodyKernels` is dispatched at `runner.dart:2806`. Registering the 7 link kernels
  needs **no core edit** — same injection-seam approach as C# (`LinkKernels.Install`).
- **Inbound-pump seam DOES NOT exist in Dart** — it was added only to `out/csharp`
  (`inbound_pump.cs` + `GlpRuntimeEngine.InboundPump` + drain in `glp_engine.cs`, Option B).
  Mirroring it is the **one core Dart change**: a null-guarded `IInboundPump` field on the Dart
  runtime/engine + a drain hook in the async scheduler loop. **Null-guarded ⇒ zero behavior/trace
  change for every non-link run** (the feature-020 "golden frozen" / sibling-convergence concern is
  satisfied by construction — identical bytes out for classical programs). **This is the D2 gate
  decision (core-GLP change ⇒ explicit Gabi approval per CLAUDE.md).**
- **Dart engine is already async** (`glp_engine.dart` `runGoal`/`_runSingleGoal`/`_runConjunction`
  are `Future…async` over `scheduler.drainAsyncWithStatus`) — no sync→async rewrite needed; the
  pump drains alongside the existing async scheduler drain.

### File inventory to mirror into `glp_runtime/lib/link/` (this repo only — sibling untouched)
| group | C# LOC | Dart files (snake_case) | notes |
|---|---|---|---|
| seam | 397 | link_scheme, link_id, link_role, link_address, link_fault, link_options, i_link_transport, i_link_endpoint | pure types/interfaces |
| reliability | 751 | crc32, frame_codec, frame_exception, link_sequencer, inbound_ordering, frame_reassembler, cycle_guard, fencing_registry, send_window, link_reclaimer, resource_snapshot | **parity-critical: byte-identical wire (FR-060/061)** |
| primitives | 1545 | link_terms, transport_registry, link_registry, link_handle, link_runtime, link_pump, link_establish, link_egress, link_faults, link_teardown + 7 kernels (setup/send/request/listen/accept/monitor/close) + link_kernels | bind to Dart heap/runner APIs |
| transports | 384 | loopback_transport(+endpoint), tcp_transport(+endpoint) | TCP = chosen real vehicle |
| **core (1 file)** | — | `runtime/inbound_pump.dart` seam + null-guarded drain in `engine/glp_engine.dart` | **D2 decision** |
| boot | — | `LinkKernels.install(rt)` + register Tcp/Loopback in `bin/glp_repl.dart` | mirror of C# boot |

Must replicate the **two live-REPL fixes** the C# kernels carry (already in `LinkTerms.cs`):
deep-deref nested struct args (`GroundResolve`) + quote-strip string constants (`Unquote`).

### Workflow-tool structure (marathon composes it; durable checkpoints per sub-step)
1. **seam + reliability** (runtime-agnostic, parallel-safe by file-group) — fan-out.
2. **transports** (depend on seam) — fan-out Loopback + Tcp.
3. **primitives** (kernels/pump/establish; coupled to heap/runner APIs) — fewer, larger units.
4. **core pump seam + boot wiring** (D2) — single coherent unit.
5. **build + GATE D**: `dart analyze`/build; Dart↔Dart two-process over TCP == single-instance
   baseline; then cross-runtime Dart↔C# (on-wire frame/message bytes must match — FR-060/061).

GATE D decisions for Gabi: **(a)** approve the null-guarded core inbound-pump seam (D2);
**(b)** confirm Workflow-driven execution under the marathon (standing grants already recorded).

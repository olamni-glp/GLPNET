# T040 — /bk-codexreview over the full 064 diff

Run `20260803T214953Z` · branch `064-post-wave-gap-closure` · base `develop` ·
strategy `standard-code-review` · scope `diff` · mode `fix`.
Artifacts: `reviews/064-post-wave-gap-closure/20260803T214953Z/` (gitignored).

## Method actually run

| Stage | What happened |
|---|---|
| Preflight | ok; clean tree, non-protected branch, codex `codex-cli 0.145.0` ready, 0 secrets |
| Brief | size-invariant: 124 changed files + `spec.md` path, **8 356 chars** — no diff body (a 615 k-insertion diff reviewed without context overflow) |
| Plan (cycle 1) | independent Claude plan (25 items) + independent codex plan (29 items) → `plan-merge` unioned to **43 items**, 12 deduped, **4 must_do contradictions** resolved conservatively (kept must-do). Proposed cycle range [3, 6]; **3 approved** (min-passes 2) |
| Execute (cycle 2) | codex pass (16 findings) + 2 Claude reviewers over the same brief+plan, ownership split C# / Gleam (21 findings) |
| Cross-critique | codex adjudicated the 21 Claude findings (18 CONFIRM / 1 REFUTE / 2 ESCALATE); Claude adjudicated the 16 codex findings (10 CONFIRM / 2 REFUTE / 4 ESCALATE) |
| Merge | 37 combined identities, **3 refuted-and-dropped**, **34 surfaced**, not converged |
| Fix | two fixers, both distinct from the reviewers (C# + Gleam), scope frozen in `cycle02/FIX-LIST.md` |
| Verify (cycle 3) | a third Claude reviewer adversarially verified each applied fix and hunted fix-introduced defects; codex ran a second execution pass |
| Verdict | **capped@3** (2 execution cycles), 19 residual identities — `verdict.md` |

Deviation recorded: the cycle-3 cross-critique was not run (the cycle-3 Claude pass was
itself the adversarial verification of the fixes). Deterministic convergence was not
reached — after the fixes the `(path, line-range, class)` identities shift, so repeats
re-enter as "new". Residuals are dispositioned below rather than by the counter.

## Fixes applied (all verified green)

| Item | Where | Fix |
|---|---|---|
| F1 socket-handle leak | `csharp/glp_engine_host/EngineServer.cs` | departed multi-client sessions are now `DisposeAsync`'d in the recv-pump `finally` (they were removed from `sessions` before the teardown loop, so every disconnect leaked TcpClient/NetworkStream/SemaphoreSlim) |
| F2 single-reader channel race | `csharp/glp_engine_host/ClientSession.cs` | `_outbound` → `SingleReader = false` (two real readers: the serve loop's drain and `CloseAsync`'s discard drain on the recv-pump thread); stale doc comment corrected |
| F4 bind-failure swallowed | `csharp/glp_quick_host/{BridgeAcceptor,Program}.cs`, `csharp/glp_link/transports/TcpTransport.cs` | `BRIDGE_READY` was printed before the lazy accept iterator bound, and a bind failure was swallowed (exit 0, bridgeless host). New `onBound` seam fires after `listener.Start()`; a pre-bind failure faults the gate TCS → `ERR bridge_bind_failed` + exit 7 |
| F5 double terminator strip | `csharp/glp_repl_client/IlSession.cs` | the trailing `.` is stripped in exactly one layer now; `foo..` fails loudly on the IL path exactly as on the text path |
| F7 SC-003 scope honesty | `csharp/glp_split_protocol.tests/CorpusEquivalenceTests.cs`, `DEFERRALS.md` | no test weakened. The gate's true scope is now stated in the test file and recorded as **D064-10** |
| G1 unbounded frame allocation | `glp_gleam/src/glp/link/transports/tcp.gleam` | the decoded 32-bit length prefix is bounded **before** the body `recv`, mirroring the normative C# `FrameCodec.MaxPayloadBytes + 1024`; over-bound → loud `Permanent` fault. Covers the BE listener, the FE client and link_pump (one shared read path) |
| G2 socket reclaim on stop | `glp_gleam/src/glp/link/transports/multi_accept.gleam`, `glp_link_tcp_ffi.erl` | accepted-but-untaken sockets are now closed on `stop` by the pump (their controlling process). Letting the pump/broker *exit* is **not** done — see escalations |
| G3 accept-error classification | `glp_link_tcp_ffi.erl`, `multi_accept.gleam` | `emfile`/`enfile`/`enomem`/`enotsock`/`ebadf` latch a `Permanent` fault; `timeout`/`closed` and **everything unclassified** retry. Previously every error collapsed into a silent ~100 Hz retry |
| G4 wire-rule-2 echo divergence | `glp_gleam/src/glp/fe/client.gleam` | the `request_id == 0` exemption is now gated on `ProtocolError` only — a zero-id RESULT is no longer rendered as this goal's answer |
| G5 embeddability CWD panic | `glp_gleam/src/glp_embed.gleam` | typed `PreludeNotFound` error instead of a panic; the CWD-independent `load_with_prelude` seam is now public |
| G6 lint phase desync | `glp_gleam/src/glp/bytecode/lint.gleam` | a pre-commit `proceed` resyncs the phase (§9.3 closes the clause), so one defect no longer cascades into spurious findings on well-formed following clauses |

### Regressions introduced by the fixes — found by cycle 3, fixed

1. `multi_accept.gleam` — G3's first cut latched on *any* unclassified error, turning a transient
   `econnaborted` into a permanently dead listener, and the instant `Failed` reply busy-spun the
   BE's accept loop at 100 % CPU. Fixed by inverting the classifier (explicit permanent list,
   default retry) and pacing the `Failed` reply like `Empty` (50 ms slices against the caller's
   budget). The BE loop's semantics were **not** touched.
2. `glp_quick_host/Program.cs` — F4's first cut awaited `bridgeBound` but tested `bridge.IsFaulted`,
   which the acceptor faults *after* the TCS; on the losing interleaving the host served bridgeless
   instead of exiting 7. Fixed to a single synchronization point (the TCS carries the bind outcome
   and can never be left unresolved).

## Not applied — reported instead

| Item | Why |
|---|---|
| F3 bridge frame decode | no delta exists: bridge ingress is already byte-identical to the QUIC path (both raw-bytes into `Mesh.RouteAsync`); adding a decode would make the bridge *diverge*. Needs an engineer ruling on whether the mesh speaks FrameCodec at all |
| F6 IL export filtering / module context | the text path's filter is driven by compiler-built `ModuleInfo` that never crosses the wire, and `contracts/il-request-kind.md` rule 1 pins the 062 envelope ("no new envelope"). Carrying exports would change that envelope — a spec decision, not an alignment. Not guessed |
| F8 unused `Draining` state | `data-model.md:16` normatively lists it and `ClientSessionTests` pins the transition; removing it would contradict the spec |
| G7 BE blocking read / dead exit path | assess-only by instruction. `febe-split.md` is silent on timeouts and the FE blocks identically, so a BE read deadline would need a protocol rule; the `65` arm is genuinely dead but making it live is a lifecycle-semantics decision |

## Escalated — engineer rulings needed (no code changed)

1. **Unauthenticated bridge ingress** — `glp_quick_host/BridgeAcceptor.cs`, `Program.cs`
   (`--bridge-addr`). Plain, unauthenticated TCP admits peers into the certificate-authenticated
   QUIC mesh, and the flag accepts any host. Both teams escalated: the flag is deliberate operator
   surface, the spec is silent, and the Gleam BE enforces the opposite policy on the same seam.
2. **IL/text path latch scope** — `RequestDispatcher.cs:54`. Does "session" in
   `contracts/il-request-kind.md` rule 3 mean the client session (064 data-model) or the engine
   session? Per-client choice would mix text and IL modules in one shared engine.
3. **Path selection before validation** — `RequestDispatcher.cs:120`. Whether the 062 "session state
   unchanged" obligation covers path selection (module registration already honours it).
4. **Cross-runtime binding rendering** — this is **D064-7** verbatim, already gated.

## Refuted by cross-critique (dropped, logged)

- `preserve-query-bindings-on-il-goal-results` — non-nullary goal_refs are refused loudly at
  `IlExecutePath.cs:117` before execution; nullary goals have no query vars on either path.
- `serialize-direct-errors-with-normal-session-replies` — only observable to a pipelining client;
  061 `wire-protocol.md` rule 2 promises no pipelining semantics and both shipped clients are
  strictly send-then-receive.
- `unreachable-deliverable` (`lint.gleam:100`).

Also checked and explicitly **not** reported: `RunGoalIlBody.Decode` length narrowing (all `Slice`
casts provably in range), `RequestResponseCodec` ordinal kind-range checks (no unassigned byte
admitted), C#↔Gleam wire validation order and `hex()`/`X2` parity, `{exit_on_close,false}`
inheritance (verified in `glp_link_tcp_ffi.erl`, not taken from the comment), "the TCP endpoint
serializes writers" (verified at `TcpEndpoint._sendLock`), and label/PC shift on program
concatenation (all jump operands are symbolic labels, re-indexed per build).

## Post-fix test evidence

| Suite | Result |
|---|---|
| REPL unified (`test/run_all_tests.sh`) | **547 total / 546 passed**. The single failure was Section I, caused by the mixed-OTP `glp_gleam/build/` collision (the WSL OTP-25 rebuild ran during the sweep), not by code |
| Section I re-run in isolation after `rm -rf glp_gleam/build` | **0 failures — ALL CROSS-RUNTIME (Gleam × C#) TESTS PASSED** (link_both_ways 4/4, round_trip, mismatch 2/2) |
| Gleam (WSL, OTP 25.3.2.8) | **625 passed, 0 failures** (618 pre-review → +7 regression tests) |
| C# affected projects | `glp_link.tests` 172/172, `glp_split_protocol.tests` 47/47, `glp_engine_host.tests` 73/73. Whole C# tree observed at **815 passed / 0 failed** |

Two `LinkRecvIngressTests` timeout assertions flaked in 1 of 6 `glp_link.tests` runs while the REPL
suite ran concurrently on the same host (11–13 s vs the usual 4–5 s); they touch nothing this review
changed. Recorded rather than hidden — the fleet norm of **serial** C#/Gleam suite runs applies.

## Checkpoints

- `26bebdd4` refine(codexreview): cycle 2/3 — the F/G fix set
- `5cce0109` refine(codexreview): cycle 3 regression fixes — accept-fault classification+pacing, bridge bind-gate race

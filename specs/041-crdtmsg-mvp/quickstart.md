# Quickstart — crdtmsg-mvp

How to build, test, and run the end-to-end demonstrator once implemented. All C# lives in `csharp/`; run from repo root.

## Build
```
dotnet build csharp/glp_wire_registry/GlpWireRegistry.csproj
dotnet build csharp/glp_crdtmsg/GlpCrdtMsg.csproj
```

## Test (xUnit) — the acceptance gates
```
dotnet test csharp/glp_wire_registry.tests
dotnet test csharp/glp_crdtmsg.tests
```
Test suites map 1:1 to success criteria:
- **Conformance matrix** (16 cells, 4 surfaces) → SC-001; unknown-field preservation.
- **Loud-fail fuzz** (extends `LoudFailFuzzTests`) → SC-002.
- **Convergence property** (randomized op permutations, 2 replicas) → SC-003.
- **Crash-rebuild** (interrupt + WAL replay) → SC-004.
- **Tamper/signature** (byte flip, sub-block remove/reorder, transcode) → SC-005/011.
- **Capability** (satisfy / unsatisfiable / un-understood + refusal recorded) → SC-006.
- **@name loud-fail** (unknown name → error, no fallback) → SC-007.
- **v1-reader / v2-envelope** (skips additive capability slot) → SC-008.
- **Fugue no-interleaving** → SC-012; **Peritext unknown-mark preservation** → SC-013.
- **Registry single-source** (no duplicated constants) → SC-010.

## Run the end-to-end demonstrator (SC-009) — single host, two clients
```
# terminal 1 — peer A (QUIC host, from 036)
csharp/glp_quick_host/bin/.../glp_quick_host.exe --peer A --listen
# terminal 2 — peer B
csharp/glp_quick_host/bin/.../glp_quick_host.exe --peer B --connect A
```
Then send one rich-text message (a `seq-insert` + `mark-add` op) from A; assert the CRDT document converges on both peers and the op is durable in each store's op-WAL.

## GLP policy guard (proposal only)
`programs/crdtmsg/policy-guard-proposal.glp` is a **proposal artifact** — do NOT load/run it as a guard until Gabi approves the concrete signature under DISCIPLINE §1.14. The shipped routing uses the fixed `{targets, waypoints, excludes}` matcher.

## Order of implementation (per plan §7 / store-first)
store → crdt (ops + Fugue/Peritext) → envelope/header + wire-registry → cap/sig → route → schema(dual-DSL). Baseline-green before each change; scoped marathon checkpoints after clarify/analyze/implement/close + after MVP within implement.

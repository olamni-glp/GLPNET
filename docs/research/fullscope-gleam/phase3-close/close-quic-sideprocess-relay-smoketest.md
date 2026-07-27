<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK

SPDX-License-Identifier: MIT
-->

# Close evidence — `T098 close-quic-sideprocess-relay-smoketest` (ruling-2026-07-27)

**Feature**: 059 · **Wave**: 3 (close) · **Run**: `mrun-7e6cfbf0a9fb` · **Date**: 2026-07-27
**Ruling**: `rule-quic-sideprocess-relay` Disposition 2 (escalation-register.md, rulings.md)
**Gates**: T084 / T085 / T086 + all Wave-4 QUIC dependents (FR-011)

## Ruling requirement

> Disposition 2. No Wave-4 WP may depend on the QUIC OS-port relay until a minimal in-corpus
> smoke test exercising `glpq_ffi.erl` — **long-line reassembly + stdio byte-identity to the C#
> stack** — exists in the corpus and passes. Environment-fragility is acknowledged … where it
> cannot run, that is classified **environment** … recorded, and the dependency stays **blocked**
> — never silently waived.

## What was closed

The delivered reassembly harness (`gleam_quic/test/glpq_ffi_reassembly_test.escript` +
`run_glpq_ffi_reassembly_test.sh` + byte-faithful stand-in `emit_big_envelope.py`) was **wired
into a runnable corpus gate** — new `gleam_quic/smoke.sh` (peer to `glp_gleam/smoke.sh`,
`test/run_all_tests.sh`) — with the ruling's env-classification discipline built in.

## Runnable evidence (fresh-session reproducible, this host)

| Dimension | Command | Result |
|---|---|---|
| **Long-line reassembly** | `ERLANG_BIN="/c/Program Files/Erlang OTP/bin" bash gleam_quic/smoke.sh` | **PASS, rc=0** — 2,097,154-byte `{…}` envelope arrives WHOLE on stdout; `READY test` control line on stderr; `{` never leaks to stderr (finding #7 non-regressed) |
| **Live-C#-stack byte-identity** | (same gate, dimension 2) | **ENVIRONMENT-BLOCKED (msquic)** — recorded, dependency stays blocked |

The relay `gleam_quic/src/glpq_ffi.erl` (`relay/3`, `{line,1048576}` accumulate `{noeol}`/`{eol}`
runs; `classify/1` demux `{`-prefix→stdout/data else→stderr/control) reassembles a line larger than
its 1 MiB line buffer and routes it byte-intact — the property the pre-fix relay violated.

## Environment classification (dimension 2 — never silent-waived)

The byte-identity dimension is proven against `emit_big_envelope.py`, which emits **exactly** the
`glp_quick_host` framing (one `READY` control line with no `{` prefix + one >1 MiB `{…}` data
envelope; binary writes, no CRLF translation). Driving the **real** `csharp/glp_quick_host` for a
live byte-identity run is **environment-blocked**: the host requires msquic and exits immediately
with `ERR quic_unsupported … real QUIC only (FR-001)` when `QuicListener.IsSupported=false`
(`csharp/glp_quick_host/Program.cs:38`). This is the **same msquic env-block** that gates
T084/T085/T086. It is recorded here and the live dimension stays BLOCKED until msquic is
provisioned (WSL/Linux quicer NIF) — per the ruling, not waived.

## Gate status for the dependents

The reassembly gate **passes**, satisfying the ruling's "exists in the corpus and passes" bar. The
QUIC leaf/interop closes (T084 in-process client, T085 live transport interop, T086 live repl
bridge) inherit the **same msquic environment block** — their env-gated live slices are recorded
environment-blocked, while their non-NIF slices (e.g. T085 RFC-6455 framing tests) remain buildable
independently. The relay itself is now gate-covered and no longer an un-tested dependency.

**Close status: CLOSED** — reassembly gate green + wired into the corpus; live-C# byte-identity
recorded ENVIRONMENT-blocked (msquic), dependency held blocked per Disposition 2.

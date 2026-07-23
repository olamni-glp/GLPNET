<!--
SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
SPDX-License-Identifier: MIT
-->

# Verify verdict — `verify-quicws-link-completion-live-repl-bridge` (WP b3-c1-009, wave 2)

**Date**: 2026-07-23
**Method**: `ls -R gleam_quic/` + `rg 'websocket|rfc6455|quic'` (glp_gleam/src, gleam_quic) + source-verification + mesh-scope citation (`specs/050-glp-native-quic-link/spec.md`) + **bounded WSL Profile-C build attempt** (per the WP's env-vs-absence risk note).
**Paired close**: `close-quicws-link-completion-live-repl-bridge` (b3-c2-034, L) — **ACTIVATED** (primary blocker = Profile-C environment, not WS-framing code-absence).

## Verdict table

| # | detail_id | verdict | basis |
|---|-----------|---------|-------|
| 1 | `websocket-framing` | **PRESENT in gleam_quic, NOT wired to the glp_gleam engine** | RFC 6455 framing is implemented in `gleam_quic/src/glpq_quic.erl` (Profile-C: `ws_send`/`ws_frame`/`ws_len`, binary FIN=1 unmasked sends / masked-on-recv, 16 MiB max — mirroring the C# `WebSocketOverQuic` wire contract, `specs/036/contracts/wire-contract.md`). But the **glp_gleam engine has no `quic_ws.gleam` transport** (T055 open); `glp_gleam/src/glp/link/seam/link_scheme.gleam` carries only the `"quic"` scheme token (T055 placeholder). |
| 2 | `profile-c-quic-acceptance` | **ENVIRONMENT-BLOCKED (not code-absent)** | Client code present (`glpq_quic.erl` — quicer/MsQuic in-process client). NIF **unbuildable on this host**: Windows has no MSVC (`profile_c/README.md`: "scaffolded, NOT built"; adapter returns `profile_c_not_built`); WSL (OTP 25, quicer 0.2.15) build-hook **fails** — see run below. Deferred per feature-049 ruling (Profile-C = client-only, WSL-only). |
| 3 | `quic-host` | **No Gleam host role (C#-hosted)** | `specs/050-glp-native-quic-link/spec.md` Q1: "The **C# reference REPL** … terminates genuine QUIC in-process … Dart `glp_repl` participation is out of MVP scope." The Gleam stack is a **data-plane relay** (Profile A, delegating to the C# `glp_quick_host` side-process) or an **in-process client** (Profile C, unbuilt) — never the control-plane host. No recorded Gleam host role. |
| 4 | `mesh-full-mesh-native-quic` | **OUT-OF-SCOPE (G5-ruled)** | `rulings.md` G5: out-of-scope, duplicate-of the promoted `glp-native-quic-link` (C# REPL host, no Gleam mesh role). The spec confirms: the all-pairs 5-endpoint mesh = 2 C# CLI REPLs + 3 MAUI C# device apps — no Gleam participant. |
| 5 | `link-completion-live-repl-bridge` | **ABSENT (captured residual ambition)** | Gap-inventory `b1-c1-071` / roadmap `http3-quic-ws-link-completion` (state **captured**, WSJF/RICE = —, blocked-by `http3-quic-ws-channel-link-proto`): "HTTP3/QUIC+WS link completion (live glp_repl bridge, mesh fix, rebuild and re-verify) is a captured, **unspecified** residual ambition." No spec, no Gleam code. |

## Evidence

### `gleam_quic/` layout
- `src/`: `glp_quick_gleam.gleam` (Profile-A data-plane: channel-link role, delegates QUIC+WS termination to the C# `glp_quick_host` side-process over line-delimited IPC — `quic_termination: side_process`, honest per Decision 8), `glpq_ffi.erl` (the OS-port relay — the untested Profile-A relay of open escalation `rule-quic-sideprocess-relay`), `glpq_quic.erl` (Profile-C in-process quicer client + RFC 6455 WS framing).
- `profile_c/`: `src/glpq_profile_c.app.src` + `rebar.config` (`{deps,[{quicer,"0.2.15"}]}`) + `README.md` (Status: scaffolded, NOT built). `_build/` holds only a stale **Windows** CMake attempt (`quicer/c_build_win/…`), no `.so`.

### Bounded WSL Profile-C build attempt (env-vs-absence classification)
WSL2 present (OTP 25, rebar3, gleam, cmake). `cd gleam_quic/profile_c && rebar3 compile` →
```
===> Verifying dependencies...
./build.sh 'v2.3.8'
make: ./build.sh: No such file or directory
make: *** [Makefile:15: build-nif] Error 127
===> Hook for compile failed!
```
No `.so` produced. ⇒ **Environment/build-tooling failure** (quicer 0.2.15 NIF build hook + MsQuic provisioning), **not code-absence**. Attempted once, not retried (infra rabbit-hole). This is exactly the classification the WP risk note requires before feeding SC-008 planning.

### Engine-side (glp_gleam) QUIC-WS status
No `glp_gleam/src/glp/link/transports/quic_ws.gleam` (T055 unchecked in `tasks.md`, consistent with `verify-link-inbound-pump`). The only QUIC references in glp_gleam are the `"quic"` `LinkScheme` token and T055 forward-reference comments in the seam.

## Activation

`close-quicws-link-completion-live-repl-bridge` (b3-c2-034) — **ACTIVATED**. Boundary for the close:
1. **`websocket-framing`** — the RFC 6455 codec exists in `gleam_quic/glpq_quic.erl`; the close's split-point ("RFC 6455 framing tests in isolation first") is achievable **without** the QUIC NIF. The remaining work is wiring a `quic_ws.gleam` transport leaf into the glp_gleam engine link seam (T055) + `WebSocketFramingTests`-equivalent Gleam tests.
2. **`profile-c-quic-acceptance` = the gating blocker, and it is ENVIRONMENT, not code** — the live Gleam↔C# QUIC-WS interop (close acceptance) needs a working quicer/MsQuic NIF (MSVC on Windows, or a WSL/Linux quicer build that fetches MsQuic). Until provisioned, the close's second slice ("live Gleam-to-C# interop link") is infra-stalled, exactly as the close risk note anticipates.
3. **`quic-host`** — no Gleam host to build (C# is host); the Gleam role is client/relay interop only.
4. **`mesh-full-mesh-native-quic`** — resolved by **G5** (out-of-scope); `rule-quicws-mesh-full-mesh-native-quic` records it; no Gleam work unless the ruling is reopened.
5. **`link-completion-live-repl-bridge`** — a captured, unspecified residual; the close should either specify+deliver or rule it forward (it is `#-`/captured on the roadmap, WSJF/RICE unset).

Cross-refs: the Profile-A relay (`glpq_ffi.erl`) carries the open escalation `rule-quic-sideprocess-relay` (untested, zero tests) — independent of this WP but adjacent on the QUIC line.

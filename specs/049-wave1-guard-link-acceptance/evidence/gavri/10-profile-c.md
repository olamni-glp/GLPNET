# 049 gavri evidence — 10 Profile C (US2, 036 T032)

**Host**: GAVRIELLAS (gavri) · **Platform for US2**: WSL2 Ubuntu 24.04 on gavri (per 00-environment.md
provisioning decision — Windows-native quicer is MSVC-blocked; `gleam_quic/profile_c/README.md`
sanctions "target Linux where quicer builds cleanly").

## Installs performed (recorded per task prompt)

| # | What | How | Result |
|---|---|---|---|
| 1 | dotnet SDK 10.0.301 (WSL) | `dotnet-install.sh --channel 10.0` → `~/.dotnet` | OK |
| 2 | libmsquic 2.5.9 (WSL) | MS apt repo (`packages-microsoft-prod.deb` for Ubuntu 24.04) + `apt-get install libmsquic`, as root | OK |
| 3 | python venv + `glp_quick[dev]` (WSL) | `python3 -m venv glp_quick/.venv && pip install -e "glp_quick[dev]"` | OK (py 3.12.3) |
| 4 | Windows venv + `glp_quick[dev]` | `py -3 -m venv glp_quick/.venv && ...` | OK (py 3.14.3) — for US3 |
| 5 | C# host build (Windows + WSL) | `dotnet build csharp/glp_quick_host/glp_quick_host.csproj -c Debug` | OK, 0 errors both sides |
| 6 | gleam stack (WSL) | `cd gleam_quic && gleam build` | OK (gleam 1.17.0, OTP 25) |

WSL working copy: `~/glpnet-049`, cloned from the Windows repo on branch `049a-gavri-us2-us3`
(local clone — clean LF checkout for the BEAM builds; evidence is committed from the Windows repo).

## Certificate (same-host runs)

```
glp_quick/.venv/bin/glp-quick cert generate --out ./glpquick-cert
SPKI SHA-256 pin: VVLYUZQAQL2uInaBLVxrAl5Wa9ku4Gr2Gw1OKQXCMTE=
```

**Observation (not fixed — invocation nuance, no code change)**: `demo --stack gleam` with a
*relative* `--cert ./glpquick-cert` fails with `cert_load: ... gleam_quic/glpquick-cert/glpquick.pfx`
— the gleam adapter spawns `gleam run` with `cwd=gleam_quic`, so the relative path re-resolves under
`gleam_quic/`. Absolute `--cert` works. The quickstart commands should be read with an absolute cert
path for `--stack gleam`.

## Profile A baseline (reference for SC-005 comparison) — 2026-07-08

```
$ glp_quick/.venv/bin/glp-quick demo --addr 127.0.0.1 --port 8443 \
    --cert /home/gavri/glpnet-049/glpquick-cert --stack gleam --profile a --clients 3
GLP-Quick conformance demo
  SC-001 real on-wire QUIC/HTTP-3 handshake (not loopback-sim)        PASS
  SC-002 full-duplex GLP-message exchange                             PASS
  SC-005 shared self-signed cert (SPKI pin) is the only trust anchor  PASS
  SC-003 ≥3 concurrent isolated clients                               PASS
  SC-002b peer-to-peer duplex mesh (to-routing + broadcast)           PASS
  SC-004 single-client-failure resilience (siblings unaffected)       PASS
  SC-006 cross-stack csharp ≡ gleam (Profile a)                       PASS
  two-host LAN acceptance (T040)                                      NOT-RUN (expected; US3 covers it)
  => PASS (run criteria)
```

**Profile A baseline verdict: PASS** — this is the equal-results bar for Profile C.

## Carried 036 findings status on this branch (FR-015, checked before any fix)

- **#5** (demo harness AttributeError on handshake timeout): already fixed on branch —
  `glp_quick/src/glp_quick/demo.py` handles a `None` recv (`if m is None: break`).
- **#6** (pre-readiness stdout-pipe hang): already fixed on branch — `spawn_handle` starts the
  stdout pump at process spawn (`stacks/csharp.py`, comment "A5/#6").
- **#7** (gleam relay >1 MiB line-mode misroute): already fixed on branch — `glpq_ffi.erl`
  reassembles `{noeol, Frag}` runs before classifying.
- **#3** (mesh duplicate `endpoint_id` eviction): to be verified in `Program.cs` during this task.

## Profile C — quicer provisioning (FR-010 path, reproducible)

Per the task prompt order: **prebuilt/hex artifact first, source build second**. Outcome:
- hex has no prebuilt NIF binaries for this platform — the `quicer` hex package carries the C
  sources and builds the NIF during `rebar3 compile` (cmake + gcc; msquic vendored).
- **quicer 0.4.3** (latest): the NIF + vendored **msquic 2.5.7** built clean (94 cmake steps,
  `libquicer_nif.so.0.4.3` linked), but its *Erlang* code needs OTP ≥ 26 (`dynamic()` type errors
  in `quicer_types.hrl`) — this WSL carries OTP 25.
- **quicer 0.2.15** (newest OTP-25-compatible): full clean build — NIF + msquic + Erlang code.

Provisioning artifact (committed): `gleam_quic/profile_c/rebar.config` +
`src/glpq_profile_c.app.src` — a minimal rebar3 app whose only job is carrying the quicer dep.
Reproduce with: `cd gleam_quic/profile_c && rebar3 compile` (Linux/WSL; **not** buildable on
MSVC-less Windows — the same 036 blocker, unchanged).

## Profile C — implementation (what was wired, per profile_c/README step 2)

Spec basis quoted before implementing:
- 049 FR-009: *"The BEAM runtime MUST complete the 036 conformance flow (connect, pin-verify,
  full-duplex) using an in-process native QUIC transport (Profile C), with pass criteria equal to
  the recorded Profile A baseline."*
- profile_c/README §To complete Profile C later: *"rebar3 add `quicer` as a dep; wire a
  `quic_link` Gleam module to its NIF API (open listener, connect, bidi stream) mirroring the C#
  `QuicTransport` contract."*
- Task prompt Task A.6: *"QUIC running IN-PROCESS on the BEAM via quicer (**no C# side-process for
  the client data plane**)"* + Task A.3: *"the C# host is the conformance reference server"*.

Design (recorded): the **client** data plane terminates QUIC in-process on the BEAM; the **server**
role remains the verified C# reference host (identical to the Profile A baseline server), so the
only variable under test is the client data plane.

Files changed (all on `049a-gavri-us2-us3`):
| File | Change |
|---|---|
| `gleam_quic/profile_c/rebar.config`, `.../src/glpq_profile_c.app.src` | NEW — quicer provisioning app |
| `gleam_quic/src/glpq_quic.erl` | NEW — the in-process client: quicer connect (ALPN `h3`, mutual shared-cert TLS), **SPKI-SHA256 pin verified mid-handshake** via `custom_verify` + `complete_cert_validation` (pin mismatch ⇒ TLS alert bad_certificate — never a blanket accept), `GLPQUICK/1` bootstrap, RFC 6455 framing (binary FIN=1 unmasked sends per T017; masked accepted; ping→pong; 16 MiB cap; continuation reassembly), stdio seam + FR-019 tokens/exit codes identical to the C# host client role |
| `gleam_quic/src/glp_quick_gleam.gleam` | `profile-c` dispatch arm → `glpq_quic:client/1` |
| `glp_quick/src/glp_quick/stacks/gleam.py` | profile c + role client → `gleam run -- profile-c ...` with `ERL_LIBS` at the quicer build; honesty gate `profile_c_not_built` when the build is absent; server role unchanged (reference host) |
| `glp_quick/tests/test_gleam.py` | `test_profile_c_not_built_is_clear` now build-aware; NEW `test_profile_c_client_in_process_to_csharp_server` (FR-009 leg) + `test_profile_c_pin_mismatch_rejected` (FR-003/SC-005 negative control) |

## Profile C — test + acceptance runs (WSL Ubuntu on gavri, 2026-07-08)

- `pytest tests/test_gleam.py`: **5 passed, 1 skipped** (skip = the not-built guard, correctly
  inapplicable where Profile C IS built). Includes the in-process full-duplex leg and the
  pin-mismatch rejection (loud `cert_mismatch`, handshake refused).
- Full `glp_quick` suite (WSL): **179 passed, 2 skipped** — no regressions.
- Full `glp_quick` suite (Windows, gleam absent → module skips): **175 passed, 6 skipped**.

```
$ glp-quick demo --addr 127.0.0.1 --port 8444 --cert /home/gavri/glpnet-049/glpquick-cert \
    --stack gleam --profile c --clients 3
GLP-Quick conformance demo
  SC-001 real on-wire QUIC/HTTP-3 handshake (not loopback-sim)        PASS
  SC-002 full-duplex GLP-message exchange                             PASS
  SC-005 shared self-signed cert (SPKI pin) is the only trust anchor  PASS
  SC-003 ≥3 concurrent isolated clients                               PASS
  SC-002b peer-to-peer duplex mesh (to-routing + broadcast)           PASS
  SC-004 single-client-failure resilience (siblings unaffected)       PASS
  SC-006 cross-stack csharp ≡ gleam (Profile c)                       PASS
  two-host LAN acceptance (T040)                                      NOT-RUN (US3 covers it)
  => PASS (run criteria)
```

**In-process proof** (no C# side-process on the client data plane): the process table during the
run shows exactly ONE `glp_quick_host.dll` process (`--role server`, the reference server) and NO
`--role client` dotnet processes — the three clients were BEAM-only (quicer NIF in-process).

### Verdict table — spec SC-005 (Profile C)

| Criterion | Profile A baseline | Profile C | Equal? |
|---|---|---|---|
| SC-001 real handshake | PASS | PASS | ✓ |
| SC-002 full-duplex | PASS | PASS | ✓ |
| SC-005 SPKI pin only trust anchor | PASS | PASS | ✓ |
| SC-003 ≥3 isolated clients | PASS | PASS | ✓ |
| SC-002b mesh (to + broadcast) | PASS | PASS | ✓ |
| SC-004 kill-one resilience | PASS | PASS | ✓ |
| SC-006 cross-stack equivalence | PASS | PASS | ✓ |

**US2 / SC-005 verdict: PASS** — Profile C conformance equals the Profile A baseline, QUIC
in-process on the full BEAM (quicer 0.2.15 / msquic 2.5.7 / OTP 25, WSL2 Ubuntu 24.04 on gavri).
FR-009 satisfied; FR-010 provisioning path documented above and reproducible.

### Observations (not fixed — recorded for the primary session)

1. Relative `--cert` breaks `--stack gleam` (cwd re-resolution) — see the baseline section.
2. `terminate_tree` on Linux (`stacks/csharp.py`) uses `proc.terminate()`, which does not kill
   grandchildren (gleam→erl→dotnet); killed demo runs can orphan a server holding the UDP port.
   On Windows `taskkill /T /F` handles the tree. Pre-existing, Linux-only, outside this task's
   minimal-fix scope — surfaced here for 049/beyond.

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

## Profile C — provisioning + acceptance

(in progress — quicer provisioning next)

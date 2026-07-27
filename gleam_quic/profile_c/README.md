# Gleam Profile C — full BEAM + `quicer`/MsQuic (genuine in-process QUIC)

**Status: scaffolded; NIF not yet built on this host (honest — constitution II).**
The *reason* changed on 2026-07-27 — see below. **Windows-native is the TARGET, not a fallback.**

Profile C terminates genuine QUIC **in-process** on the full BEAM via the
[`quicer`](https://github.com/emqx/quic) NIF (a binding over Microsoft's MsQuic). `capabilities()`
would be `{real_quic: true, quic_termination: "in_process"}`.

## 🔴 Corrections, 2026-07-27 (Gabi-directed) — the old blockers were STALE

This file previously said the host had "**no MSVC**", and `specs/050-*/research.md` said QUIC runtime
testing is "WSL-only". **Both claims are wrong and are withdrawn.** Verified on OLAMNIT this date:

| Old claim | Actual |
|---|---|
| "no MSVC on this host" | **MSVC IS present** — VS 18 **BuildTools** at `C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools`, with a bundled `ninja.exe` under `Common7\IDE\CommonExtensions\Microsoft\CMake\Ninja\` |
| "Profile-C is WSL-only" | **Windows-native is the target.** Running on Windows is the point; WSL is *optional and also available*, never a constraint. Windows-container QUIC is the ideal test and should be attempted where possible |
| toolchain unavailable | CMake `C:\Program Files\CMake`, Erlang OTP `C:\Program Files\Erlang OTP` (erl/escript), rebar3 `C:\Users\ariel\bin\rebar3` — the last is **off PATH**, prefix per session (same post-rebuild loss class as the Dart SDK and the codeconv venv) |

## The REAL blocker (what actually stops a build today)

🔴 **`rebar3 compile` exits 0 and builds no NIF on Windows.** A green exit means "the Erlang modules
compiled", NOT "QUIC works". Three findings, 2026-07-27:

1. **The NIF hook never fires.** `quicer`'s own `rebar.config` gates it to
   `{pre_hooks, [{"(linux|darwin|solaris)", compile, "make build-nif"}]}` — Windows is not in that
   regex, so `make build-nif` is skipped silently. Confirm via
   `_build/default/lib/quicer/priv/`: if it holds only `.gitignore`, there is **no**
   `quicer_nif.dll` and nothing QUIC-capable exists.
2. **The vendored `msquic/` source tree is EMPTY** — `get-msquic.sh <version>` has never run, so
   there are no MsQuic sources to build even once the toolchain is right.
3. **`c_build_win/CMakeCache.txt` is poisoned.** It records a toolchain absent from this host:
   `ninja.exe` under `C:/qp/qtools/...` and `cl.exe` under VS 18 **Insiders** (not BuildTools). A
   CMake cache pins absolute tool paths, so it cannot be reused — **delete `c_build_win/` and
   re-configure** against the tools that exist.

## To complete Profile C (the corrected recipe)

Prereqs are all present; the work is driving the C build **manually**, because `rebar3 compile`
will not do it on Windows (finding 1).

1. Enter the MSVC environment: `VsDevCmd.bat -arch=amd64` from **BuildTools**, via a `cmd` batch
   launcher (PowerShell quoting of that path is a known foot-gun — write a `.bat` and `cmd /c` it).
2. `rebar3 compile` once for the Erlang side (`PATH` must include `C:\Users\ariel\bin`).
3. Fetch MsQuic sources into `_build/default/lib/quicer/`: `bash get-msquic.sh <version>`.
4. Apply the vendored `windows-msvc-cmake.patch` — quicer 0.2.15 ships **no** Windows branch; this
   is a local minimal MSVC port (Windows `target_link_libraries`, the openssl-quic include dir, and
   suppression of the GCC-only `-ggdb3`).
5. Configure + build with CMake (`-G Ninja`, BuildTools ninja + cl), `QUIC_TLS=openssl`, and
   `Erlang_OTP_ROOT_DIR` → `C:\Program Files\Erlang OTP` so `erl_nif.h` resolves.
6. Land `quicer_nif.dll` in `_build/default/lib/quicer/priv/`.
7. Wire a `quic_link` Gleam module to the NIF API (open listener, connect, bidi stream) mirroring
   the C# `QuicTransport` / `WebSocketOverQuic.cs` contract — this is glpnet **T055**
   (`glp_gleam/src/glp/link/transports/quic_ws.gleam`, behind the T045 seam).
8. Set `GleamStackAdapter(profile="c")` `capabilities()` to `in_process` and run the cross-stack
   conformance (`test_gleam`) against `--profile c`.

## What ships meanwhile (and is verified)

**Profile A** (`../src/`) delivers genuine QUIC honestly today: Gleam/BEAM channel-link logic + the
verified C# `glp_quick_host` as a **native genuine-QUIC side-process**
(`quic_termination: "side_process"`). It passes the full conformance demo and is interchangeable with
the C# stack (SC-006). See `gleam_quic/src/glp_quick_gleam.gleam`.

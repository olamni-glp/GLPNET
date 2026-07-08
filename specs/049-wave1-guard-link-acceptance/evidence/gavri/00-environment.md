# 049 gavri evidence — 00 Environment discovery

**Host**: `GAVRIELLAS` (the "gavri" host) · **User**: `gavri` · **Date**: 2026-07-08
**Branch**: `049a-gavri-us2-us3` (off `049-wave1-guard-link-acceptance` @ `00661ec0`)
**Scope**: US2 (Profile C) + US3 (two-host LAN) per `gavri-task-prompt.md`.

## OS / arch

| Item | Value | Command |
|---|---|---|
| OS | Microsoft Windows 11 Pro, build 26200 (NT 10.0.26200.0) | `systeminfo` |
| Arch | x64 (AMD64) | `$env:PROCESSOR_ARCHITECTURE` |
| Virtualization | WSL2 available; distros: **Ubuntu 24.04.3 LTS** (kernel 6.6.87.2-microsoft-standard-WSL2 x86_64), AlmaLinux-10 — both stopped, both v2 | `wsl -l -v` |

> NOTE: the task prompt anticipated a Linux/macOS host ("on Linux/macOS this builds where
> Olamnit's missing MSVC blocked it"). gavri is **Windows 11**, same as Olamnit — the Linux
> path exists here only via WSL2.

## Windows-native toolchain

| Tool | Status | Version / path |
|---|---|---|
| erlang (`erl`) | **MISSING** | — |
| gleam | **MISSING** | — |
| rebar3 | **MISSING** | — |
| cmake | present | 4.3.2 (`C:\Program Files\CMake\bin\cmake.exe`) |
| MSVC (`cl.exe`) | **MISSING** (see below) | — |
| gcc | present (MinGW **i686** — 32-bit, unusable for msquic) | 15.2.0 (`C:\qp\qtools\qtools\mingw32\bin\gcc.exe`) |
| dotnet | present | SDKs **8.0.422**, **10.0.200-preview**; runtimes NETCore.App 8.0.23/8.0.28/9.0.12/**10.0.2** |
| python | present | **3.14.3** (`python`/`python3`); `py` launcher present. glp_quick requires `>=3.11` ✓ |
| node | present | v22.22.2 |
| git | present | `C:\Program Files\Git\cmd\git.exe` |
| package managers | choco (present), winget (present), scoop (missing) | — |

### MSVC status (decisive for a Windows-native quicer build)

Chocolatey lists `visualstudio2022buildtools 117.14.24` + `visualstudio2022-workload-vctools 1.0.0`
as installed, **but**:
- `vswhere -all -products *` returns **no instances**;
- neither `C:\Program Files (x86)\Microsoft Visual Studio\2022` nor
  `C:\Program Files\Microsoft Visual Studio\2022` exists;
- no `cl.exe` found under any BuildTools path.

Verdict: the choco packages are registered but the VC toolset is **not actually on disk** —
gavri is effectively MSVC-less, the same blocker that stopped Profile C on Olamnit (036).

## WSL2 Ubuntu 24.04 toolchain (pre-provisioned)

| Tool | Status | Version |
|---|---|---|
| erlang (`erl`) | present | **OTP 25** (Erts 13.2.2.5; apt `1:25.3.2.8+dfsg-1ubuntu4.6`) |
| gleam | present | **1.17.0** (`/usr/local/bin/gleam`) |
| rebar3 | present | **3.19.0** |
| cmake | present | 3.28.3 |
| gcc | present | 13.3.0 (x86_64) |
| python3 | present | 3.12.3 |
| git, curl | present | — |
| dotnet | **MISSING** | — |

## C# host build target

`csharp/glp_quick_host/glp_quick_host.csproj` targets **net10.0** — buildable on Windows with the
installed 10.0.200-preview SDK and runnable on the 10.0.2 runtime.

## Provisioning decision (FR-010 path, recorded before acting)

- **US2 / Profile C**: Windows-native quicer is blocked (no MSVC; MinGW is not a supported
  quicer/msquic path — `gleam_quic/profile_c/README.md`). The README's sanctioned alternative is
  **"target Linux where quicer builds cleanly"** → provision the full stack (erlang+gleam+rebar3
  already present; add dotnet SDK 10 + python venv) inside **WSL2 Ubuntu 24.04** on gavri and run
  the Profile A baseline + Profile C acceptance there. quicer provisioning order per task prompt:
  prebuilt/hex artifact for OTP 25 first, source build (cmake+gcc) second.
- **US3 / two-host**: runs **Windows-native** (python 3.14 + dotnet 10 SDK, csharp stack per
  quickstart §7) — no WSL dependency for the LAN acceptance; WSL2 NAT is thereby avoided for the
  cross-host wire evidence.

## Installs performed so far

None yet. Planned (each will be recorded here as performed):
1. dotnet SDK 10.x inside WSL Ubuntu (dotnet-install script or apt, whichever provides net10.0).
2. python3-venv inside WSL Ubuntu if absent.
3. `quicer` hex/prebuilt or source dep in the gleam_quic/profile_c build.

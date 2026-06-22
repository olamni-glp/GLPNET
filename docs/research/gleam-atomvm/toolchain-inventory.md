# Toolchain Inventory — Gleam Port Spike

**Feature**: 031-gleam-port-spike · **Status**: pinned · **Date observed**: 2026-06-22
**Contract**: written against `specs/031-gleam-port-spike/contracts/toolchain-inventory.schema.md`.

> **Evidence-recording convention (FR-009, SC-002)**: every version field below is the
> **observed exact** value (no "latest"). Every "works here" claim is a **command + its
> observed output** (see `hello-glp-term/README.md`). Citations are used only for
> documentary values (e.g., a documented AtomVM subset limit).

---

## Versions (exact)

| Field | Value | How observed |
|---|---|---|
| `gleam_version` | **1.17.0** | `gleam --version` → `gleam 1.17.0` |
| `erlang_otp_version` | **25.3.2.8** (OTP release 25, ERTS 13.2.2.5) | apt pkg `erlang-base` = `1:25.3.2.8+dfsg-1ubuntu4.6`; `erlang:system_info(otp_release)`=`25`, `…(version)`=`13.2.2.5` |
| `atomvm_build` | **v0.6.6**, prebuilt asset `AtomVM-linux-x86_64-static-mbedtls-v0.6.6` (host/generic build) | `./AtomVM-static -v` → `0.6.6` |
| `build_tooling` | `rebar3` **3.19.0** (apt `3.19.0-1`); Gleam built-in build tool (v1.17.0); **node v18.19.1** (JS backend) | `rebar3 version`; `node --version` → `v18.19.1` |
| Gleam deps (resolved, `manifest.toml`) | `gleam_stdlib` 1.0.3 · `gleam_erlang` 1.3.0 · `gleam_otp` 1.2.0 · `gleeunit` 1.11.0 | `gleam deps download` / `manifest.toml` |
| `environment` | **WSL Ubuntu 24.04.3 LTS (noble), x86_64** — see environment finding below | `/etc/os-release`, `uname -m` |

---

## Environment finding (FR-007, research R1, edge case) — FIRST-CLASS

**Windows-native was attempted first (per R1) but not used; the toolchain was stood up on
WSL Ubuntu (Linux).** This is a recorded finding for downstream features, not a silent
fallback. Precise reasons (so the finding is not overstated):

- **Gleam and Erlang/OTP both ship first-class Windows binaries** — a developer with
  administrator rights installs them natively on Windows without difficulty (this is *not*
  a "Gleam doesn't run on Windows" finding).
- In *this automated session* the Windows path was constrained by two **environmental**
  factors, not language ones: (a) the shell was **non-elevated**, so the admin-required
  Erlang/OTP Windows installer could not run; (b) the no-admin `scoop` bootstrap (download
  + execute remote installer script) was blocked by the session's command policy.
- **WSL is also the correct home for the AtomVM attempt** regardless — AtomVM's prebuilt
  host releases and source build are Linux-centric (R3). The host machine is Windows 11
  (build 26200, AMD64) with WSL2 (Ubuntu 24.04.3 + AlmaLinux-10 available).

**Downstream implication**: F2/F3 should standardize on the **Linux/WSL** environment with
the pinned versions above (or an `asdf`/`mise`-pinned equivalent). Native-Windows is viable
for a developer with admin rights but was not exercised here.

---

## `setup_commands` (reproducible)

```bash
# Host: Windows 11 + WSL2. Enter the Ubuntu distro as root (avoids sudo password prompts):
#   wsl -d Ubuntu -u root -- bash

# 1) Erlang/OTP 25.3.2.8 + rebar3 3.19.0 (official Ubuntu noble packages)
export DEBIAN_FRONTEND=noninteractive
apt-get update -y
apt-get install -y erlang rebar3

# 2) Gleam 1.17.0 (official prebuilt static binary)
TAG=v1.17.0
curl -fsSL -o /tmp/gleam.tar.gz \
  "https://github.com/gleam-lang/gleam/releases/download/${TAG}/gleam-${TAG}-x86_64-unknown-linux-musl.tar.gz"
tar -xzf /tmp/gleam.tar.gz -C /tmp
install -m 0755 /tmp/gleam /usr/local/bin/gleam

# 3) Node (JavaScript backend) — already present: v18.19.1  (apt-get install -y nodejs)

# 4) AtomVM host build v0.6.6 — prebuilt, STATIC-mbedtls variant.
#    (The dynamic AtomVM-linux-x86_64 build needs libmbedtls.so.10, which Ubuntu noble
#     does not ship — it has mbedtls 3.x. The static-mbedtls asset avoids this.)
mkdir -p /opt/atomvm && cd /opt/atomvm
BASE=https://github.com/atomvm/AtomVM/releases/download/v0.6.6
curl -fsSL -o AtomVM-static "$BASE/AtomVM-linux-x86_64-static-mbedtls-v0.6.6"
curl -fsSL -o atomvmlib-v0.6.6.avm "$BASE/atomvmlib-v0.6.6.avm"
chmod +x AtomVM-static
```

## `build_commands`

```bash
cd docs/research/gleam-atomvm/hello-glp-term
gleam build --target erlang        # compile to BEAM
gleam build --target javascript     # JS backend (full smoke fails — see README; functional subset compiles)
```

## `run_commands`

```bash
# Erlang/BEAM (the test runtime)
gleam run  --target erlang          # observe the term + the unbound->bound bind
gleam test --target erlang          # 4 passed, no failures

# AtomVM host build (effort-bounded attempt) — AtomVM accepts .beam/.avm directly.
# Entry shim (AtomVM calls start/0; Gleam's entry is main/0):
printf '%s\n' '-module(glp_start).' '-export([start/0]).' 'start() -> hello_glp_term:main().' > /tmp/glp_start.erl
erlc -o /tmp /tmp/glp_start.erl
BEAMS=$(find build/dev/erlang -path '*/ebin/*.beam' | tr '\n' ' ')
/opt/atomvm/AtomVM-static /tmp/glp_start.beam $BEAMS /opt/atomvm/atomvmlib-v0.6.6.avm
```

---

## Invariants check

- [x] Every version field is exact (no "latest"). *(FR-003)*
- [x] Environment honesty: the Windows→WSL/Linux fallback is recorded as a first-class finding. *(Edge case; FR-007)*
- [x] setup+build+run blocks reproduce the smoke for a second person (verified by clean rebuild — see README §Reproducibility). *(SC-002)*
- [x] Citations used only for documentary values; "works here" claims are command+observed-output. *(FR-009)*

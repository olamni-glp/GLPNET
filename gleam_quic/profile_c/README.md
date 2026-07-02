# Gleam Profile C — full BEAM + `quicer`/MsQuic (genuine in-process QUIC)

**Status: scaffolded, NOT built on this host (honest — constitution II).**

Profile C terminates genuine QUIC **in-process** on the full BEAM via the
[`quicer`](https://github.com/emqx/quic) NIF (a binding over Microsoft's MsQuic). `capabilities()`
would be `{real_quic: true, quic_termination: "in_process"}`.

## Why it is not built here

`quicer` builds MsQuic from source via CMake and a C/C++ toolchain. On Windows MsQuic expects MSVC
(SChannel TLS); this host has CMake + Erlang/OTP 28 + rebar3 + MSYS2/MinGW, but **no MSVC** — and a
MinGW MsQuic build is not a supported `quicer` path. Rather than fake in-process QUIC, Profile C is
left unbuilt and the `gleam` adapter returns a clear `profile_c_not_built` error for `--profile c`.

## What ships instead (and is verified)

**Profile A** (`../src/`) delivers genuine QUIC honestly today: Gleam/BEAM channel-link logic + the
verified C# `glp_quick_host` as a **native genuine-QUIC side-process**
(`quic_termination: "side_process"`). It passes the full conformance demo and is interchangeable with
the C# stack (SC-006). See `gleam_quic/src/glp_quick_gleam.gleam`.

## To complete Profile C later

1. Provide an MSVC toolchain (VS Build Tools) so `quicer`'s MsQuic build succeeds, **or** target Linux
   where `quicer` builds cleanly.
2. `rebar3` add `quicer` as a dep; wire a `quic_link` Gleam module to its NIF API (open listener,
   connect, bidi stream) mirroring the C# `QuicTransport` contract.
3. Set `GleamStackAdapter(profile="c")` `capabilities()` to `in_process` and run the cross-stack
   conformance (`test_gleam`) against `--profile c`.

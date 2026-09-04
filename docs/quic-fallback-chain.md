<!-- SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK -->
<!-- SPDX-License-Identifier: MIT -->

# The QUIC fallback chain — and the Linux equivalent of libmsquic

**Repo:** GLPNET · **Component:** `csharp/ynet_transport/Link/` · **Host measured:** `shiras`
(Ubuntu 26.04.1 LTS "resolute", .NET 11.0.100-preview.7, OpenSSL 3.5.5) · **Measured:** 2026-09-04

## 1 · The question

*"Find and add the Linux equivalent of libmsquic as the ultimate QUIC fallback if iroh net fails
completely."*

## 2 · What the gap actually is

Not "Linux has no QUIC" — libmsquic builds and runs on Linux, and `libmsquic.so.2` is installed on
this host. The gap is **distribution**: libmsquic ships only from `packages.microsoft.com`. It is
absent from Ubuntu apt and absent from the .NET install, so every new Linux host reintroduces a
manual, elevated provisioning step.

Ruling `Q-glpnetshiras-38` names the same hazard for the primary: *"if iroh is vendored as Rust
rather than consumed as a prebuilt native library it creates a new per-host, per-platform system
prerequisite — the SAME CLASS as the `libmsquic` gap it was meant to remove."*

**A fallback that shares the primary's failure mode is not a fallback.** So the requirement for the
ultimate tier is not "another QUIC library" but *a QUIC stack whose provisioning fails independently
of both iroh's Rust toolchain and Microsoft's package feed.*

## 3 · The answer: ngtcp2 + ngtcp2_crypto_ossl

`libngtcp2-16`, `libngtcp2-crypto-ossl0` and `libnghttp3-9` are in the **Ubuntu archive itself**
(universe). No third-party feed, no cargo, no Rust.

Measured on shiras, all executed rather than inferred:

| check | result |
|---|---|
| `apt-get download` of all three | ✅ succeeds **without root** |
| `dlopen` of `libngtcp2.so.16` | ✅ loads |
| required versioned exports (`ngtcp2_conn_client_new_versioned`, `…server_new…`, `…read_pkt…`, `…writev_stream…`) | ✅ all present |
| `ngtcp2_version()` | ✅ **1.16.0** |
| `dlopen` of `libngtcp2_crypto_ossl.so.0` + its 3 exports | ✅ loads |
| OpenSSL with the QUIC TLS API `ngtcp2_crypto_ossl` needs | ✅ 3.5.5 |

### Alternatives, and why they lost

| candidate | verdict |
|---|---|
| **quiche** (Cloudflare) | Cleanest C ABI of the three, but **no distro package** — needs cargo. That is iroh's failure mode again. |
| **lsquic** (LiteSpeed) | Mature, but likewise unpackaged, and a larger engine API than ngtcp2. |
| **picoquic** | Research-grade. |
| **vendor `libmsquic.so.2` beside the binary** | Worth doing, and **done** (§5) — but it keeps the Microsoft feed in the provisioning path, so it strengthens tier 1 rather than replacing it. |

## 4 · The chain

```
tier 0  iroh / noq (quinn)   — primary once it lands (Q-glpnetshiras-38 keeps the STACK at L1)
tier 1  MsQuic               — System.Net.Quic; bundled on Windows, a Microsoft-feed package on Linux
tier 2  ngtcp2 + ossl        — the distribution's own QUIC engine; the ULTIMATE fallback
```

The three fail **independently**: a Rust runtime, a Microsoft feed, the distro archive. A host that
loses one keeps the others.

`QuicProviderChain` selects the lowest available tier, and when none can serve it throws
`QuicUnavailableException` naming **every** tier and its reason. iroh registers itself at tier 0 when
its stack lands; the chain order needs no edit.

## 5 · The loader-path fix that makes tier 1 real for services

Measured 2026-09-04, same binary, twice:

```
default loader path            QuicListener.IsSupported = False
LD_LIBRARY_PATH=~/.local/lib   QuicListener.IsSupported = True
```

**A systemd unit does not inherit an interactive shell's `LD_LIBRARY_PATH`.** That env var therefore
greens the tests and leaves the broker, guardian and oracle deaf — they register, then refuse at
first link. At `n=4` with PBFT margin already zero, one such host takes `f=1 → f=0` **with no
signal**.

`MsQuicNativeResolver` closes it by resolving msquic from paths that travel with the build output
(`runtimes/<rid>/native/`, then the app directory), before falling back to the per-user lib dir and
the system loader. **Ordering is load-bearing**: `QuicConnection.IsSupported` runs MsQuic's static
initialiser, so the resolver registers from a `[ModuleInitializer]` at assembly load — a resolver
registered later has no effect.

## 6 · Honest state of tier 2

**Provisioning: done. Managed interop: not built.**

ngtcp2 is a protocol engine only — it owns neither sockets nor TLS — so the binding is a substantial
piece of work (callback table, versioned settings/transport-params structs, the OpenSSL QUIC
handshake, a UDP pump, timers, path validation), not a thin P/Invoke.

Therefore `Ngtcp2Provider.Probe()` **reports unavailable** even when the native engine is present,
and says which of the two reasons applies. It deliberately does not report the tier healthy on the
strength of the library being loadable: *a provider that probes green and then refuses at bind is
precisely the "green check, deaf service" failure the chain exists to prevent.*
`ProbeNative()` exposes the engine state separately, for provisioning checks only.

Outstanding work to make tier 2 carry a link: the `ngtcp2` managed interop.

## 7 · One load-order trap, verified

`libngtcp2_crypto_ossl.so.0` carries `DT_NEEDED libngtcp2.so.16`, and the install directory is not on
the loader search path. Loading the **engine first** puts it in the process link map, which then
satisfies the crypto library by soname. Probing the two in the other order — or in separate
processes — reports a **false "crypto missing"**. Both the provider and the provisioning script load
engine-first for this reason.

## 8 · Provisioning

```sh
scripts/provision-quic-native-linux.sh            # rootless: apt-get download + extract to ~/.local/lib/ynet-quic
scripts/provision-quic-native-linux.sh --check    # probe only; exit 0 iff ngtcp2 is loadable
scripts/provision-quic-native-linux.sh --stage <publish-dir>   # copy beside the build output, so a service needs no env var
```

This belongs in the **provisioning path, not a runbook step someone repeats** — it recurs on every
new Linux host.

## 9 · Ownership

Authored from lane `shiras-qhstate` at the engineer's direct instruction. The code lives in GLPNET,
whose lane is `shiras-glpnet`; spec 056 `spec.md:240` bars qhstate from implementing QUIC itself, and
this change respects that — it is in the ruled owner's repo (`Q-shiras0904c-01`, tier-2 build on
`ynet_transport`), not in qhstate. **@shiras-glpnet should ACK before it is released.**

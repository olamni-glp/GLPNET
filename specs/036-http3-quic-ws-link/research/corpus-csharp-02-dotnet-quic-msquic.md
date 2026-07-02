# Corpus — C#/.NET stack — Cluster 02: System.Net.Quic + MsQuic packaging/availability

2026-06-27

Scope: availability, packaging, platform requirements, and client QUIC usage of `System.Net.Quic` + native MsQuic for feature 036 (real HTTP/3 QUIC + WebSocket channel-link prototype, C#/.NET reference stack). Sources are close-read from Microsoft Learn, the dotnet/runtime GitHub repo, dotnet blog posts, the microsoft/msquic docs, and NuGet.

---

### [1] QUIC support in .NET (overview)
- **URL**: https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/quic/quic-overview
- **Type / version / date**: Microsoft Learn docs; .NET 5→9 (viewed `?view=net-9.0`); page ms.date 2023-03-15, updated 2024-11-18
- **Architectural concern**: packaging
- **Close-read findings**:
  - Status gate, verbatim: "In .NET 7.0 and 8.0, the APIs were published as preview features. Starting with .NET 9, these APIs are no longer considered preview features and are now deemed stable." Library existed since .NET 5 but was internal until .NET 7 made it public.
  - `System.Net.Quic` "depends on MsQuic, the native implementation of QUIC protocol" and "platform support and dependencies are inherited from MsQuic." Windows: "msquic.dll is distributed as part of the .NET runtime, and no other steps are required to install it." Linux: "you must manually install `libmsquic` via an appropriate package manager."
  - **Windows requirement**: "Windows 11, Windows Server 2022, or later. (Earlier Windows versions are missing the cryptographic APIs required to support QUIC.)" — i.e. Schannel TLS 1.3 for QUIC needs Win11/WS2022+.
  - Linux: ".NET 7+ is only compatible with 2.2+ versions of libmsquic"; package via packages.microsoft.com or Alpine; pulls OpenSSL 3+/1.1 + libnuma1. macOS: partial via Homebrew `libmsquic` + `DYLD_FALLBACK_LIBRARY_PATH`.
  - Runtime support gate: both `QuicListener.IsSupported` and `QuicConnection.IsSupported` static bools must be checked first — they are false when libmsquic is missing or TLS 1.3 unavailable. Three public types: `QuicListener` (server accept), `QuicConnection` (client+server), `QuicStream`.
- **Informs**: FR-001 (establish a real QUIC connection), FR-003 (cert auth), FR-009/FR-010 (C# is one of two interchangeable stacks), FR-018 (reuse spec-025 seam)
- **Confidence**: high

### [2] QuicConnection / System.Net.Quic API reference
- **URL**: https://learn.microsoft.com/en-us/dotnet/api/system.net.quic.quicconnection?view=net-9.0 (and namespace https://learn.microsoft.com/en-us/dotnet/api/system.net.quic?view=net-9.0)
- **Type / version / date**: Microsoft Learn API docs; .NET 9.0
- **Architectural concern**: handshake
- **Close-read findings**:
  - `QuicConnection` does not itself send/receive; it opens/accepts `QuicStream`s. Client side via static `ConnectAsync(QuicClientConnectionOptions, CancellationToken)`; server side handed out by `QuicListener.AcceptConnectionAsync`. Connections returned are fully connected (TLS handshake complete).
  - Properties expose `LocalEndPoint`, `RemoteEndPoint`, and `RemoteCertificate` (the peer cert after handshake — useful for FR-003 mutual-auth verification).
  - Close protocol mandates an application error code: `CloseAsync(long, CancellationToken)`, else `DisposeAsync()` uses `DefaultCloseErrorCode`. `DisposeAsync()` is mandatory to release native resources.
  - Outbound stream: `OpenOutboundStreamAsync(QuicStreamType.Bidirectional|Unidirectional)`; inbound: `AcceptInboundStreamAsync()` — but opening a stream only reserves it; the peer is not notified until data is written (gotcha: `AcceptInboundStreamAsync` hangs if no data is sent).
- **Informs**: FR-001, FR-003, FR-009/FR-010
- **Confidence**: high

### [3] Use HTTP/3 with HttpClient
- **URL**: https://learn.microsoft.com/en-us/dotnet/core/extensions/httpclient-http3
- **Type / version / date**: Microsoft Learn docs; .NET 6→9; updated 2026-03-30
- **Architectural concern**: ALPN
- **Close-read findings**:
  - Enable via `HttpRequestMessage.Version = 3.0` or `HttpClient.DefaultRequestVersion = HttpVersion.Version30`, plus a version policy. Example uses `DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact` (forces HTTP/3). For graceful fallback the doc recommends `HttpVersionPolicy.RequestVersionOrHigher` with Version 1.1 so routers/proxies that lack HTTP/3 still work.
  - ".NET implementation of HTTP/3 uses MsQuic" — same platform deps as raw QUIC. "If the platform that HttpClient is running on doesn't have all the requirements for HTTP/3, then it's disabled." (silent fallback, not an exception).
  - **.NET 6 only** needed the opt-in `System.Net.SocketsHttpHandler.Http3Support` switch (project `RuntimeHostConfigurationOption`, `AppContext.SetSwitch`, or `DOTNET_SYSTEM_NET_HTTP_SOCKETSHTTPHANDLER_HTTP3SUPPORT=true`). This opt-in is no longer required in .NET 7+/9.
  - Public interop test server for the client: https://cloudflare-quic.com. HTTP/3 in HttpClient/Kestrel does not support network transitions.
- **Informs**: FR-001, FR-009/FR-010
- **Confidence**: high

### [4] System.Net.Quic readme.md (dotnet/runtime)
- **URL**: https://github.com/dotnet/runtime/blob/main/src/libraries/System.Net.Quic/readme.md
- **Type / version / date**: GitHub source (dotnet/runtime, main branch)
- **Architectural concern**: packaging
- **Close-read findings**:
  - Requires **MsQuic 2.1 or later** (overview doc states 2.2+ for the Linux package on .NET 7+).
  - Windows requirement restated: Windows 11 / Windows Server 2022 (or a sufficiently new Win10 Insider build > 2004 / OS Build 20145.1000); TLS 1.3 must be enabled (default); uses Schannel.
  - Distribution, verbatim intent: "we ship `libmsquic.dll` as part of .NET runtime on Windows"; Linux users `apt install libmsquic` (etc.) from packages.microsoft.com — not bundled.
  - Officially released Windows MsQuic builds are published to NuGet via `Microsoft.Native.Quic.MsQuic.Schannel` — the Schannel-backed native binary the runtime consumes.
- **Informs**: FR-001, FR-009/FR-010, FR-018
- **Confidence**: high

### [5] .NET 9 Networking Improvements (blog)
- **URL**: https://devblogs.microsoft.com/dotnet/dotnet-9-networking-improvements/
- **Type / version / date**: dotnet blog; .NET 9; 2024
- **Architectural concern**: backpressure/flow-control
- **Close-read findings**:
  - Confirms stabilization: System.Net.Quic is "generally available without any opt-in switches" in .NET 9; the `PreviewFeature`/`[RequiresPreviewFeatures]` attribute is gone — no `<EnablePreviewFeatures>` needed (this was required in .NET 7/8).
  - New `QuicConnectionOptions` knobs: `HandshakeTimeout` (default 10s), `KeepAliveInterval` (default infinite — PING frames when positive), `InitialReceiveWindowSizes` (flow-control receive limits; values must be powers of 2). Defaults derive from MsQuic.
  - New `StreamCapacityCallback` on `QuicConnectionOptions` — fires when peer grants more stream capacity via MAX_STREAMS; underpins `SocketsHttpHandler.EnableMultipleHttp3Connections`.
  - Perf: peer certificate validation now runs async on the .NET thread pool (no longer blocks MsQuic threads); MsQuic configuration caching reuses native structures across same-config connections, disableable via `DOTNET_SYSTEM_NET_QUIC_DISABLE_CONFIGURATION_CACHE=1` or AppContext switch.
- **Informs**: FR-001, FR-003, FR-009/FR-010
- **Confidence**: high

### [6] QUIC configuration options in .NET
- **URL**: https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/quic/quic-options
- **Type / version / date**: Microsoft Learn docs; .NET 9; updated 2025-01-21
- **Architectural concern**: cert-trust
- **Close-read findings**:
  - **ALPN is mandatory**: `QuicListenerOptions.ApplicationProtocols` (and the `Ssl*AuthenticationOptions.ApplicationProtocols`) must contain ≥1 `SslApplicationProtocol` (RFC 7301). A custom protocol-link app picks its own ALPN string; HTTP/3 uses `h3`.
  - **Cert/TLS reuse the SslStream model**: server uses `QuicServerConnectionOptions.ServerAuthenticationOptions` (`SslServerAuthenticationOptions` — needs a valid cert via `ServerCertificate` / `ServerCertificateContext` / `ServerCertificateSelectionCallback`). Client uses `QuicClientConnectionOptions.ClientAuthenticationOptions` (`SslClientAuthenticationOptions`), with `TargetHost` for SNI/validation; client certs and `RemoteCertificateValidationCallback` come "the same as" SslStream — this directly informs FR-003.
  - Cipher suites: if `CipherSuitesPolicy` set, must include one of TLS_AES_128_GCM_SHA256 / TLS_AES_256_GCM_SHA384 / TLS_CHACHA20_POLY1305_SHA256 (TLS 1.3 suites); default null lets MsQuic use OS-supported QUIC suites.
  - Flow control: `QuicReceiveWindowSizes` { `Connection` (default 64 MB), `LocallyInitiatedBidirectionalStream`/`RemotelyInitiatedBidirectionalStream`/`UnidirectionalStream` (default 64 KB) }, all powers of 2 (MsQuic limitation). Concurrency: `MaxInboundBidirectionalStreams` (client default 0 / server 100), `MaxInboundUnidirectionalStreams` (client 0 / server 10), translated to MAX_STREAMS frames. `IdleTimeout` default 30s; mandatory `DefaultStreamErrorCode` / `DefaultCloseErrorCode`.
  - Gotcha: MsQuic listener "always binds to a dual-stack wildcard socket regardless of" `ListenEndPoint`.
- **Informs**: FR-003, FR-001, FR-009/FR-010
- **Confidence**: high

### [7] msquic.dll no longer included in the Windows shared runtime (issue #81447)
- **URL**: https://github.com/dotnet/runtime/issues/81447
- **Type / version / date**: GitHub issue (dotnet/runtime); .NET 8 alpha, 2023; resolved
- **Architectural concern**: packaging
- **Close-read findings**:
  - A regression between .NET 8 alpha builds `8.0.0-alpha.1.23061.6` → `.11` dropped msquic.dll from the Windows runtime artifacts, which "effectively disabled System.Net.Quic." Suspect PR #80164; fixed by PR #81490.
  - Failure mode worth noting for the prototype: when msquic.dll is absent, `QuicListener.IsSupported`/`QuicConnection.IsSupported` silently return false and QUIC tests skip rather than fail — i.e. a missing native dep degrades silently. The prototype must assert `IsSupported == true` explicitly, not rely on connection errors.
  - Confirms the shipping model is "msquic.dll bundled in the Windows shared runtime" — and that bundling has historically been fragile, a packaging risk to verify in the target runtime.
- **Informs**: FR-001, FR-009/FR-010, FR-018
- **Confidence**: med

### [8] MsQuic Platforms (microsoft/msquic docs)
- **URL**: https://microsoft.github.io/msquic/msquicdocs/docs/Platforms.html
- **Type / version / date**: GitHub Pages docs (microsoft/msquic); current
- **Architectural concern**: cert-trust
- **Close-read findings**:
  - Windows: default TLS backend is **Schannel** (built-in TLS 1.3); min "Windows Server 2022, Windows 11 or the latest Windows Insider Preview Builds." Schannel limitation: **does not support 0-RTT**.
  - Windows alternative: an OpenSSL (quictls) build "removes the OS dependency" and runs on most Windows 10+ — relevant if the prototype must run on pre-Win11 Windows, but it is not the runtime-shipped binary.
  - Linux: TLS via OpenSSL 1.1/3.1 through a specialized fork (quictls); "fully supports 0-RTT." Mainline OpenSSL QUIC support still pending.
  - macOS: not officially supported (build-for-test only, no support/bug-fix guarantees). All configs require TLS 1.3.
- **Informs**: FR-003, FR-001, FR-009/FR-010
- **Confidence**: high

### [9] HTTP/3 support in .NET 6 (blog — history)
- **URL**: https://devblogs.microsoft.com/dotnet/http-3-support-in-dotnet-6/
- **Type / version / date**: dotnet blog; .NET 6; 2021
- **Architectural concern**: packaging
- **Close-read findings**:
  - Historical baseline: in .NET 6 HTTP/3 was preview ("does not meet the quality standards of the rest of .NET 6") and System.Net.Quic APIs were not public. Required the `System.Net.SocketsHttpHandler.Http3Support` opt-in.
  - **Version pin trap**: ".NET 6 is only compatible with the 1.9.x versions of libmsquic. Libmsquic 2.x is not compatible due to breaking changes." (Contrast: .NET 7+ needs libmsquic 2.2+.) Confirms runtime↔msquic versions are tightly coupled — do not mix.
  - TLS: Windows = Schannel (needs Win11 build 22000+/WS2022 RTM); Linux = QuicTLS (minimal OpenSSL fork) statically linked into libmsquic; macOS unsupported (SecureTransport lacks QUIC TLS APIs). Windows ships msquic in the runtime; Linux separate.
- **Informs**: FR-001, FR-009/FR-010
- **Confidence**: high

### [10] NuGet — Microsoft.Native.Quic.MsQuic.Schannel
- **URL**: https://www.nuget.org/packages/Microsoft.Native.Quic.MsQuic.Schannel
- **Type / version / date**: NuGet package page; latest stable 2.5.8 (prerelease 2.5.9-rc/rc2); as of 2026-05-11
- **Architectural concern**: packaging
- **Close-read findings**:
  - This is the official Schannel-backed Windows native MsQuic binary package; top dependents are dotnet/runtime and dotnet/dotnet — i.e. it is the upstream source of the msquic.dll the .NET Windows runtime ships.
  - Current stable line is MsQuic 2.5.x — well past the .NET 7+ ">= 2.2" floor; supports parallel streams, congestion control, IP mobility, RSS, UDP send/recv coalescing.
  - Implication for the prototype: pinning/overriding the native MsQuic is possible via this NuGet (e.g. to force a known version or get a Schannel build on a non-default host), but normally the runtime-bundled copy is used and no package reference is needed on Windows.
- **Informs**: FR-001, FR-018, FR-009/FR-010
- **Confidence**: med

### [11] When will QUIC be supported on all platforms? (discussion #79736)
- **URL**: https://github.com/dotnet/runtime/discussions/79736
- **Type / version / date**: GitHub discussion (dotnet/runtime); maintainer (wfurt) statements; 2022–2023
- **Architectural concern**: failure-modes
- **Close-read findings**:
  - Maintainer wfurt: "from .NET perspective the support depends on MsQuic availability." Windows = out-of-box on Win11/WS2022; older Windows lacks native support. Linux = manual libmsquic install (he notes MsQuic 2.1.8 Linux package fixed its dependency declarations; OpenSSL 1.1.1 then, OpenSSL 3.x in MsQuic 2.2).
  - macOS "should work but there are no binary packages, primarily because signing and packaging issues." Mobile + WebAssembly: not supported.
  - On universal packaging: "the library should be provided just like another OS package IMHO. But since Quic is in early adopter state that did not happen yet" — explains why Linux needs a separate install rather than runtime bundling.
- **Informs**: FR-009/FR-010, FR-001, FR-018
- **Confidence**: med

---

## Cluster feasibility verdict

- **Genuine client + server QUIC is production-supported in .NET 9 on Windows — not preview-gated.** Per Microsoft Learn: in .NET 7/8 the `System.Net.Quic` APIs were preview features; "Starting with .NET 9, these APIs are no longer considered preview features and are now deemed stable," and the .NET 9 blog states they are "generally available without any opt-in switches." No `[RequiresPreviewFeatures]` / `<EnablePreviewFeatures>` and no `SocketsHttpHandler.Http3Support` switch (that was a .NET 6-only requirement). So both raw QUIC (`QuicListener` server + `QuicConnection` client) and HTTP/3 via `HttpClient` are first-class for feature 036's C# stack.
- **MsQuic is shipped WITH the runtime on Windows, separate on Linux.** On Windows "msquic.dll is distributed as part of the .NET runtime, and no other steps are required." On Linux you must `apt/apk/dnf install libmsquic` from packages.microsoft.com (.NET 7+ needs libmsquic ≥ 2.2; current upstream is 2.5.x via the `Microsoft.Native.Quic.MsQuic.Schannel`/OpenSSL NuGets). macOS is partial (Homebrew + `DYLD_FALLBACK_LIBRARY_PATH`), unsupported by Microsoft. Caveat: the bundled msquic.dll has historically been fragile (issue #81447 dropped it from an alpha and silently disabled QUIC).
- **What the prototype must configure/enable**: (1) gate every code path on `QuicConnection.IsSupported` / `QuicListener.IsSupported` (false ⇒ missing libmsquic or no TLS 1.3 — fail loudly, since failures are otherwise silent); (2) set mandatory ALPN (`ApplicationProtocols`, ≥1 `SslApplicationProtocol` — `h3` for HTTP/3, a custom string for the raw channel-link); (3) supply a server cert (`ServerAuthenticationOptions.ServerCertificate*`) and client trust via `ClientAuthenticationOptions` (`TargetHost`, optional client cert, `RemoteCertificateValidationCallback`) — the SslStream model, satisfying FR-003; (4) set mandatory `DefaultStreamErrorCode`/`DefaultCloseErrorCode`; (5) for HTTP/3 set `HttpVersion.Version30` + a `HttpVersionPolicy` (`RequestVersionExact` to force, or `RequestVersionOrHigher` with 1.1 for fallback). Remember: opening a stream sends nothing until first write (else `AcceptInboundStreamAsync` hangs).
- **TLS 1.3 on Windows is via Schannel and requires Windows 11 / Windows Server 2022 or later** ("Earlier Windows versions are missing the cryptographic APIs required"). Schannel does not support 0-RTT. If the dev/CI host is pre-Win11, raw QUIC will report `IsSupported == false`; an OpenSSL/quictls MsQuic build can lift that OS dependency but is not the runtime-shipped binary. This is the single biggest platform gotcha for the Windows reference stack — confirm the target runs Win11/WS2022+.
- **Version coupling is strict**: runtime↔msquic versions must match (.NET 6 ⇒ libmsquic 1.9.x only; .NET 7+/9 ⇒ 2.2+; never mix). On Windows this is handled by the bundled dll; on Linux it is the installer's responsibility.
- **Net assessment for FR-001/003/009/010/018**: a real, stable, production-supported QUIC client+server is achievable in .NET 9 on Windows 11 with zero preview flags and no extra install, making the C# stack a legitimate interchangeable peer (FR-009/FR-010). Flow-control/concurrency knobs (`InitialReceiveWindowSizes`, `MaxInbound*Streams`, `KeepAliveInterval`, `HandshakeTimeout`, `StreamCapacityCallback`) are all exposed for tuning backpressure/keepalive at the spec-025 seam (FR-018).

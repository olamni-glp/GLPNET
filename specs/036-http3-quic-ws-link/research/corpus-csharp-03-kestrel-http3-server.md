# Corpus — C#/.NET stack — Cluster 03: Kestrel HTTP/3 server config

2026-06-27

Scope: server-side HTTP/3 in ASP.NET Core Kestrel — enabling it, the UDP listener +
Alt-Svc behaviour, the HTTPS/TLS 1.3 + certificate requirement, platform prerequisites
(Windows 11 / Windows Server 2022 + msquic, Linux libmsquic), binding a specific UDP
port, and the server-side status of WebSocket-over-HTTP/3 (Extended CONNECT / RFC 9220).
Sources are Microsoft Learn, dotnet/aspnetcore GitHub, and the .NET blog, supplemented
by one practitioner blog and one protocol-status reference.

---

### [1] Use HTTP/3 with the ASP.NET Core Kestrel web server (Microsoft Learn)
- **URL**: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/http3?view=aspnetcore-10.0
- **Type / version / date**: Microsoft Learn docs; .NET 7–11 (moniker range, defaultMoniker aspnetcore-10.0); ms.date 2026-04-14
- **Architectural concern**: handshake
- **Close-read findings**:
  - HTTP/3 is fully supported in .NET 7+ but **not enabled by default**. Enable per-endpoint: `listenOptions.Protocols = HttpProtocols.Http1AndHttp2AndHttp3; listenOptions.UseHttps();` inside `builder.WebHost.ConfigureKestrel((ctx, options) => options.ListenAnyIP(5001, listenOptions => {...}))`. Doc explicitly: "HTTP/3 requires HTTPS."
  - HTTP/3 **mandates TLS 1.3**; encryption is built into QUIC (combined transport+crypto handshake). The implementation depends on **MsQuic**; "If the platform that Kestrel is running on doesn't have all the requirements for HTTP/3, Kestrel disables HTTP/3 and falls back to other HTTP protocols" — i.e. silent graceful degradation, not a hard error.
  - QUIC transport tuning via `builder.WebHost.UseQuic(options => { options.MaxBidirectionalStreamCount = 200; })` (the QUIC options were preview-gated CA2252 in earlier versions). `QuicTransportOptions` defaults: `MaxBidirectionalStreamCount` 100, `MaxUnidirectionalStreamCount` 10, `MaxReadBufferSize` 1 MB, `MaxWriteBufferSize` 64 KB, `Backlog` 512, `DefaultStreamErrorCode` 0x010c (H3_REQUEST_CANCELLED), `DefaultCloseErrorCode` 0x100 (H3_NO_ERROR).
  - **Alt-Svc**: "Kestrel automatically adds the `alt-svc` header if HTTP/3 is enabled." HTTP/3 is discovered as an *upgrade* — the first request normally uses HTTP/1.1 or HTTP/2, then the client switches to HTTP/3. There is no pure-H3 first contact in the browser flow.
  - **Localhost gotcha**: browsers do not accept self-signed certs (incl. the Kestrel dev cert) over HTTP/3 — must test with `HttpClient` (set `HttpRequestMessage.Version = 3.0` or `VersionPolicy = RequestVersionOrHigher`).
  - **HTTP/3 HTTPS limitations** (carried from .NET 6 notes): when using HTTP/3, `HttpsConnectionAdapterOptions.HandshakeTimeout` and `OnAuthenticate` are **no-ops**; the `ServerOptionsSelectionCallback` and `TlsHandshakeCallbackOptions` overloads of `UseHttps` **throw** under HTTP/3. (No per-SNI dynamic cert selection callback on H3.)
- **Informs**: FR-001 (real QUIC connection), FR-002 (WebSocket over it — server side), FR-003 (server cert), FR-009/FR-010 (C# stack), SC-005
- **Confidence**: high

### [2] QUIC support in .NET (Microsoft Learn — System.Net.Quic + platform deps)
- **URL**: https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/quic/quic-overview
- **Type / version / date**: Microsoft Learn docs; .NET 5→9 (APIs stable in .NET 9, preview in 7/8); ms.date 2023-03-15, updated 2024-11-18
- **Architectural concern**: cert-trust
- **Close-read findings**:
  - **Windows**: "Windows 11, Windows Server 2022, or later. (Earlier Windows versions are missing the cryptographic APIs required to support QUIC.)" `msquic.dll` ships inside the .NET runtime on Windows — no separate install. (This is the dependency Kestrel HTTP/3 inherits.)
  - **Linux**: must install `libmsquic` (`apt/apk/dnf/zypper/yum install libmsquic`) from packages.microsoft.com; .NET 7+ requires libmsquic **2.2+** (note: the older Kestrel doc's "1.9.x only / 2.x incompatible" applies to .NET 6 specifically). Depends on OpenSSL 3+ or 1.1 + libnuma1. **macOS**: only partial support via Homebrew (`brew install libmsquic`) with `DYLD_FALLBACK_LIBRARY_PATH` set — not in the test matrix.
  - **Capability probe**: `QuicListener.IsSupported` (server) and `QuicConnection.IsSupported` (client) — check before use; false when libmsquic is missing or TLS 1.3 unavailable. This is the mechanism Kestrel uses to decide fallback.
  - Server certificate is supplied through `SslServerAuthenticationOptions.ServerCertificate` (or `ServerCertificateContext` / `ServerCertificateSelectionCallback`) on `QuicServerConnectionOptions`. **ALPN** is mandatory: `ApplicationProtocols = [new SslApplicationProtocol("...")]` on both `QuicListenerOptions` and the connection options (for HTTP/3 this is "h3"). QUIC mandates TLS 1.3 (RFC 9001).
  - Low-level server API shape (what Kestrel wraps): `QuicListener.ListenAsync(QuicListenerOptions)` with `ListenEndPoint = new IPEndPoint(addr, port)` → `AcceptConnectionAsync()` → per-connection `AcceptInboundStreamAsync()`. Confirms a custom non-Kestrel QUIC server is also viable in pure C# if Kestrel's HTTP/3 surface is insufficient.
- **Informs**: FR-001 (real QUIC connection), FR-003 (server cert), FR-009/FR-010 (C# stack), SC-005
- **Confidence**: high

### [3] Configure endpoints for the ASP.NET Core Kestrel web server (Microsoft Learn)
- **URL**: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-10.0
- **Type / version / date**: Microsoft Learn docs; .NET 10; current
- **Architectural concern**: packaging
- **Close-read findings**:
  - Bind a specific port with `serverOptions.Listen(IPAddress.Loopback, 5001, lo => lo.UseHttps(...))`, `ListenAnyIP(port, ...)`, or `ListenLocalhost(port, ...)`. Per-endpoint protocol via `ListenOptions.Protocols`; default for the server is `HttpProtocols.Http1AndHttp2`. Enum values: `Http1`, `Http2`, `Http3` (TLS required), `Http1AndHttp2`, `Http1AndHttp2AndHttp3`.
  - **`Http3` alone is a valid endpoint protocol** — Kestrel can be configured HTTP/3-only on an endpoint (relevant to a LAN demo that wants pure QUIC, though browsers still need Alt-Svc bootstrapping from an H1/H2 endpoint).
  - **UDP/TCP port coupling**: dynamic (port 0) binding is "not available in some scenarios," specifically when binding "TCP-based HTTP/1.1 or HTTP/2 with QUIC-based HTTP/3" together — must use an explicit port. With `Http1AndHttp2AndHttp3`, H1/H2 run on TCP and H3 runs on the same port number over **UDP**; the UDP listener is a separate socket. The demo must open the chosen **UDP** port in any firewall, not just TCP.
  - Certificate config via `UseHttps("cert.pfx","pwd")`, `UseHttps("cert.pem","key.pem","pwd")`, `UseHttps(StoreName.My,"subject")`, or `UseHttps(x509cert)`; also declaratively in `appsettings.json` under `Kestrel:Endpoints:<name>` with `Protocols` and a `Certificate` block. (Note from [1]: callback-based `UseHttps` overloads are unsupported under H3.)
- **Informs**: FR-001 (real QUIC connection), FR-003 (server cert), FR-009/FR-010 (C# stack), SC-005
- **Confidence**: high

### [4] HTTP/3: CONNECT method (dotnet/aspnetcore issue #32004)
- **URL**: https://github.com/dotnet/aspnetcore/issues/32004
- **Type / version / date**: GitHub issue; opened 2021-04-21 by JamesNK; ASP.NET Core / Kestrel
- **Architectural concern**: WS-framing
- **Close-read findings**:
  - This is the tracking issue for the HTTP/3 CONNECT method (the bootstrap RFC 9220 / RFC 8441 Extended CONNECT needs for WebSocket-over-HTTP/3). The repeatedly-stated position from the team: **"Kestrel has not supported CONNECT in any protocol version to date."** That is, no plain CONNECT and no Extended CONNECT — so **no server-side WebSocket-over-HTTP/3 and no WebSocket-over-HTTP/2 accept path that depends on it via Kestrel's own WebSocket middleware**.
  - The issue was deferred / kept open to collect feedback on impact rather than implemented; as of this corpus there is **no shipped Kestrel server-side Extended CONNECT for HTTP/3**. (Page comment thread did not fully load on fetch; the "no CONNECT in any version" statement is corroborated by the surrounding search index and by [8].)
  - Practical consequence: a WebSocket handshake over HTTP/3 cannot be accepted by Kestrel's `app.UseWebSockets()` / `HttpContext.WebSockets.AcceptWebSocketAsync()` path, because that path is the HTTP/1.1 Upgrade (and HTTP/2 Extended CONNECT) mechanism — neither bootstraps over H3 today.
- **Informs**: FR-002 (WebSocket over it — server side), FR-009/FR-010 (C# stack), SC-005
- **Confidence**: high

### [5] Experimental WebTransport over HTTP/3 support in Kestrel (.NET Blog)
- **URL**: https://devblogs.microsoft.com/dotnet/experimental-webtransport-over-http-3-support-in-kestrel/
- **Type / version / date**: .NET blog; .NET 7 RC1; 2022-09-29
- **Architectural concern**: WS-framing
- **Close-read findings**:
  - Kestrel's *experimental* answer to "bidirectional messaging over HTTP/3" is **WebTransport, not WebSocket**. WebTransport is described as "a transport protocol similar to WebSockets that allows the usage of multiple streams per connection" over QUIC/UDP, avoiding head-of-line blocking.
  - Server API: `IHttpWebTransportFeature` (`feature.IsWebTransportRequest`, `feature.AcceptAsync()`), then `session.AcceptStreamAsync()`, `session.OpenUnidirectionalStreamAsync()`, `IStreamDirectionFeature`. WebTransport *does* use Extended CONNECT internally — so the H3 Extended CONNECT plumbing exists for WebTransport even though it is **not** exposed for classic WebSocket.
  - Strictly opt-in/experimental: requires `<EnablePreviewFeatures>true</EnablePreviewFeatures>` and `RuntimeHostConfigurationOption Include="Microsoft.AspNetCore.Server.Kestrel.Experimental.WebTransportAndH3Datagrams"`. Not a stable, production-supported surface.
  - Implication for FR-002: if the prototype must carry WS-style framing over real QUIC in C#, the closest first-party path is WebTransport (experimental) — but it is API-incompatible with the WebSocket framing/handshake; it is not "WebSocket over HTTP/3."
- **Informs**: FR-002 (WebSocket over it — server side), FR-009/FR-010 (C# stack), SC-005
- **Confidence**: high

### [6] Reconsider enabling HTTP/3 by default in .NET 8 (dotnet/aspnetcore issue #50131)
- **URL**: https://github.com/dotnet/aspnetcore/issues/50131
- **Type / version / date**: GitHub issue; opened 2023-08-17; milestone 8.0-rc1; .NET 8
- **Architectural concern**: failure-modes
- **Close-read findings**:
  - Team decision: **keep HTTP/3 disabled by default** (Option 3 — status quo, opt-in only). So a prototype must explicitly set `Http1AndHttp2AndHttp3` (or `Http3`) — it will never light up implicitly.
  - Root cause of friction: launching an H3 Kestrel app triggers a **Windows Defender / firewall prompt** because "msquic ... has to grab a wildcard listener up front because of how it supports multiplexing multiple apps on the same UDP port." Relevant operational gotcha for a LAN demo on Windows 11: expect a firewall prompt for the UDP listener on first run, and the listener may bind wildcard UDP rather than a single address.
  - Reiterates the localhost/self-signed-cert problem as a reason H3 is awkward to demo locally in a browser.
- **Informs**: FR-001 (real QUIC connection), FR-009/FR-010 (C# stack), SC-005
- **Confidence**: high

### [7] HTTP/3: Enable in Kestrel by default (dotnet/aspnetcore issue #36486)
- **URL**: https://github.com/dotnet/aspnetcore/issues/36486
- **Type / version / date**: GitHub issue; ".NET 8 Planning" milestone; closed (ref PR #44217)
- **Architectural concern**: packaging
- **Close-read findings**:
  - The "turn H3 on by default" proposal traces .NET 7→8 history; it was ultimately resolved as **closed without flipping the default on** (consistent with [6]). Confirms the opt-in posture is intentional and durable, not an oversight.
  - Reinforces that explicit endpoint configuration (`HttpProtocols.Http1AndHttp2AndHttp3`) is the supported and expected way to enable HTTP/3 for the foreseeable .NET versions the prototype will target.
- **Informs**: FR-001 (real QUIC connection), FR-009/FR-010 (C# stack)
- **Confidence**: med

### [8] WebSocket over HTTP/3 / RFC 9220 status (search synthesis — dotnet/aspnetcore issue index)
- **URL**: https://github.com/dotnet/aspnetcore/issues/43697 (HTTP/2 WebSockets and body streams; Extended CONNECT semantics)
- **Type / version / date**: GitHub issue index + WebSearch synthesis; ASP.NET Core; 2022–2026
- **Architectural concern**: WS-framing
- **Close-read findings**:
  - Kestrel **does** support WebSockets over **HTTP/2** via Extended CONNECT (RFC 8441) since .NET 7; issue #43697 concerns hardening that path (e.g. throwing from `Response.Body.WriteAsync()` on a 200 Extended-CONNECT request). This is the existing, supported "WS over a multiplexed transport" story — but it is HTTP/2 (TCP), **not** HTTP/3.
  - RFC 9220 (WebSocket over HTTP/3 via Extended CONNECT with `:protocol: websocket`) is defined but, per [9], has essentially no production server implementations; Kestrel is no exception (see [4]).
  - Net: the only first-party Kestrel WebSocket-over-a-modern-multiplexed-transport that actually works today is **WS-over-HTTP/2**, not WS-over-HTTP/3.
- **Informs**: FR-002 (WebSocket over it — server side), FR-009/FR-010 (C# stack), SC-005
- **Confidence**: med

### [9] The Future of WebSockets: HTTP/3, WebTransport & Beyond (WebSocket.org)
- **URL**: https://websocket.org/guides/future-of-websockets/
- **Type / version / date**: Vendor guide (Ably); current as of early 2026
- **Architectural concern**: WS-framing
- **Close-read findings**:
  - "As of early 2026, no major browser or web server has shipped a production implementation" of WebSocket-over-HTTP/3 (RFC 9220). Chrome at "Intent to Prototype"; Firefox/Safari none; nginx/Apache/LiteSpeed lsquic do not implement RFC 9220; Caddy blocked upstream. "WebSocket over HTTP/3 cannot be used in production environments today."
  - WebSocket-over-H3 and WebTransport are distinct: WS-over-H3 keeps the WebSocket API over QUIC; WebTransport is a new API offering datagrams + multiplexed streams. Cross-checks [5]: the WebTransport route is the only practically-shipping bidirectional-over-QUIC option, and it is not WebSocket.
  - Industry-wide corroboration that the prototype cannot rely on browser-or-server WS-over-H3 interop — any WS-over-H3 would have to be a bespoke C#-client ↔ C#-server arrangement, and even server-side Kestrel does not expose it.
- **Informs**: FR-002 (WebSocket over it — server side), SC-005
- **Confidence**: med

### [10] Using HTTP/3 (QUIC) in .NET (Meziantou's blog)
- **URL**: https://www.meziantou.net/using-http-3-quic-in-dotnet.htm
- **Type / version / date**: Practitioner blog; .NET 6; 2022-01-10 (early; verify against current .NET)
- **Architectural concern**: ALPN
- **Close-read findings**:
  - Confirms the canonical enable snippet (`ListenAnyIP(5001, lo => { lo.Protocols = Http1AndHttp2AndHttp3; lo.UseHttps(); })`) and that `UseHttps()` with no args relies on default cert handling.
  - **Verification technique for the demo**: confirm H3 by inspecting the `alt-svc` response header for `h3`; enable W3C logging to see negotiated protocol versions per connection. Browser H3 is restricted on localhost — verify via `HttpClient` with `DefaultRequestVersion = HttpVersion.Version30`.
  - Client-side (older .NET 6) needed `<RuntimeHostConfigurationOption Include="System.Net.SocketsHttpHandler.Http3Support" Value="true"/>`; later .NET versions enable H3 client support without this. Useful for a C#-to-C# LAN test harness.
- **Informs**: FR-001 (real QUIC connection), FR-009/FR-010 (C# stack), SC-005
- **Confidence**: med

### [11] .NET 6 / .NET 7 Networking Improvements — HTTP/3 + QUIC (search synthesis, .NET Blog)
- **URL**: https://devblogs.microsoft.com/dotnet/dotnet-6-networking-improvements/
- **Type / version / date**: .NET blog; .NET 6 (and successor .NET 7 posts); 2021–2022
- **Architectural concern**: concurrency
- **Close-read findings**:
  - Establishes the lineage: QUIC arrived as internal `System.Net.Quic` in .NET 5 (HTTP/3 only), made public/preview in .NET 7, stable in .NET 9 — so target **.NET 8+ (ideally 9+)** for a non-preview QUIC/HTTP/3 surface in the prototype.
  - Reiterates QUIC's native stream multiplexing as the concurrency model Kestrel HTTP/3 exposes (per-request independent streams, governed by `MaxBidirectionalStreamCount`/`MaxUnidirectionalStreamCount` from [1]). Each GLP REPL link/channel mapped onto an H3 request stream gets independent flow control and no cross-stream head-of-line blocking.
- **Informs**: FR-001 (real QUIC connection), FR-009/FR-010 (C# stack)
- **Confidence**: med

### [12] Enabling HTTP/3 support on Windows Server 2022 (Microsoft Community Hub / techcommunity)
- **URL**: https://techcommunity.microsoft.com/blog/networkingblog/enabling-http3-support-on-windows-server-2022/2676880
- **Type / version / date**: Microsoft community blog; Windows Server 2022; 2021
- **Architectural concern**: packaging
- **Close-read findings**:
  - Confirms the OS-level prerequisite that Kestrel inherits: HTTP/3 + QUIC + TLS 1.3 require **Windows Server 2022 / Windows 11** (Schannel TLS 1.3 for QUIC is absent on earlier Windows). For the glpnet dev box (Windows 11 Pro 10.0.26200 per env), this prerequisite is **met**.
  - HTTP/3 on Windows uses the same msquic/Schannel stack whether via IIS/http.sys or Kestrel; the chosen UDP port must be reachable (firewall) for the QUIC listener.
- **Informs**: FR-001 (real QUIC connection), FR-003 (server cert / TLS 1.3), FR-009/FR-010 (C# stack)
- **Confidence**: med

---

## Cluster feasibility verdict

- **Can Kestrel host HTTP/3 on a chosen UDP port for a LAN demo? Yes.** Set `listenOptions.Protocols = HttpProtocols.Http3` (H3-only) or `Http1AndHttp2AndHttp3` on an explicit port via `Listen`/`ListenAnyIP`, plus `UseHttps()` with a TLS 1.3 server cert. H3 must use an explicit (non-zero) port, runs over **UDP**, and the UDP port must be opened in the firewall (expect a Windows Defender prompt — msquic grabs a wildcard UDP listener). The glpnet box (Windows 11 26200) already satisfies the msquic/Schannel/TLS-1.3 prerequisite; msquic.dll ships in the runtime, so no separate install on Windows. Target .NET 9+ to get a non-preview QUIC surface.
- **Server-side WebSocket-over-HTTP/3 (Extended CONNECT / RFC 9220): NOT supported by Kestrel today.** The team's standing position is "Kestrel has not supported CONNECT in any protocol version to date" (issue #32004); RFC 9220 has no production server implementations industry-wide as of early 2026. Kestrel's `UseWebSockets()` accept path does **not** bootstrap over H3.
- **What Kestrel WS support actually exists: WebSocket-over-HTTP/2 (RFC 8441 Extended CONNECT, TCP) since .NET 7 — not over H3.** The only first-party "bidirectional over QUIC" option is **WebTransport over HTTP/3, and it is experimental** (`EnablePreviewFeatures` + the `WebTransportAndH3Datagrams` runtime option) and API-incompatible with the WebSocket handshake/framing.
- **Exact prerequisites for FR-001/FR-003**: HTTPS with **TLS 1.3** + a server certificate (browsers reject self-signed/dev certs over H3 — for a browser-facing demo use a trusted cert; for C#-to-C# use `HttpClient` with `HttpVersion.Version30`). Platform: Windows 11 / Windows Server 2022 (met), or Linux with `libmsquic` 2.2+. ALPN "h3" is negotiated automatically by Kestrel. Alt-Svc is emitted automatically, but it means clients bootstrap over H1/H2 first unless they connect H3-directly.
- **Fallback implication for FR-002 (the load-bearing finding)**: a literal "WebSocket frames over a real HTTP/3 QUIC connection, accepted by Kestrel" is **not achievable with stock Kestrel**. Three viable fallbacks for the prototype, in order of fidelity-to-intent: (a) **WS-over-HTTP/2** for the WebSocket leg (fully supported, TCP) while still demonstrating a real QUIC/HTTP/3 endpoint for plain request/response (FR-001 satisfied independently); (b) **WebTransport-over-HTTP/3** (experimental) to carry GLP channel framing over real QUIC, accepting that it is not the WebSocket API; (c) **raw `System.Net.Quic` (`QuicListener`/`QuicStream`)** for a bespoke C#-server ↔ C#-client channel-link over QUIC, bypassing Kestrel's HTTP layer entirely — highest control, no browser interop.
- **Recommendation**: split the spec's "HTTP/3 QUIC + WebSocket" goal into independently-real parts — real QUIC/HTTP/3 via Kestrel (FR-001/FR-003, solid) and the WS framing via either WS-over-HTTP/2 (safe demo) or raw `QuicStream`/WebTransport (true-over-QUIC, lower interop). Do not assume Kestrel will accept a WebSocket handshake over H3; confirm the chosen fallback before committing FR-002.

## Sources
- [Use HTTP/3 with the ASP.NET Core Kestrel web server (Microsoft Learn)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/http3?view=aspnetcore-10.0)
- [QUIC support in .NET (Microsoft Learn)](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/quic/quic-overview)
- [Configure endpoints for Kestrel (Microsoft Learn)](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/endpoints?view=aspnetcore-10.0)
- [HTTP/3: CONNECT method — issue #32004](https://github.com/dotnet/aspnetcore/issues/32004)
- [Experimental WebTransport over HTTP/3 in Kestrel (.NET Blog)](https://devblogs.microsoft.com/dotnet/experimental-webtransport-over-http-3-support-in-kestrel/)
- [Reconsider enabling HTTP/3 by default in .NET 8 — issue #50131](https://github.com/dotnet/aspnetcore/issues/50131)
- [HTTP/3: Enable in Kestrel by default — issue #36486](https://github.com/dotnet/aspnetcore/issues/36486)
- [HTTP/2 WebSockets and body streams — issue #43697](https://github.com/dotnet/aspnetcore/issues/43697)
- [Future of WebSockets: HTTP/3, WebTransport & Beyond (WebSocket.org)](https://websocket.org/guides/future-of-websockets/)
- [Using HTTP/3 (QUIC) in .NET (Meziantou)](https://www.meziantou.net/using-http-3-quic-in-dotnet.htm)
- [.NET 6 Networking Improvements (.NET Blog)](https://devblogs.microsoft.com/dotnet/dotnet-6-networking-improvements/)
- [Enabling HTTP/3 support on Windows Server 2022 (techcommunity)](https://techcommunity.microsoft.com/blog/networkingblog/enabling-http3-support-on-windows-server-2022/2676880)

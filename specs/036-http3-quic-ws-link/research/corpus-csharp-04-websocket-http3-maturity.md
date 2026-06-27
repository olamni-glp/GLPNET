# Corpus — C#/.NET stack — Cluster 04: WebSocket-over-HTTP/3 (RFC 9220) maturity + fallback

2026-06-27

Scope: the project's highest-risk question — can a real WebSocket be bootstrapped over HTTP/3
(RFC 9220, Extended CONNECT) using .NET 9 `ClientWebSocket` (client) and Kestrel (server)
TODAY, and if not, is the fallback (RFC 6455-style framing carried over a single bidirectional
`QuicStream`, reusing spec-025's `FrameCodec`) sound and standards-honest? Sources are weighted
toward Microsoft Learn, the .NET blog, dotnet/runtime + dotnet/aspnetcore, and the RFCs. Dates
are load-bearing throughout.

---

### [1] WebSockets support in .NET — Microsoft Learn
- **URL**: https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/websockets
- **Type / version / date**: Official docs (conceptual); doc `ms.date` 2022-10-27, **last updated 2025-01-31** (covers .NET 7/8/9)
- **Architectural concern**: handshake
- **Close-read findings**:
  - The document enumerates exactly two transport modes for `ClientWebSocket`: "Differences in HTTP/1.1 and HTTP/2 WebSockets." HTTP/1.1 cites RFC 6455; HTTP/2 cites **RFC 8441** ("WebSockets are established per stream"). **HTTP/3 / RFC 9220 is never mentioned** — a strong negative signal from the canonical client-side reference as of 2025-01.
  - Version selection: "By default, `ClientWebSocket` uses HTTP/1.1 ... and allows downgrade. **In .NET 7 web sockets over HTTP/2 are available.**" The shown example sets `ws.Options.HttpVersion = HttpVersion.Version20` with `HttpVersionPolicy.RequestVersionOrHigher`. There is no `Version30` example or note — the documented ceiling is HTTP/2.
  - .NET 9's only WebSocket-layer addition described here is the **PING/PONG keep-alive strategy** (`KeepAliveTimeout` complementing `KeepAliveInterval`) — a liveness feature, orthogonal to transport version. No HTTP/3 work shipped.
  - HTTP/2 multiplexing is enabled via the `ConnectAsync(Uri, HttpMessageInvoker, CancellationToken)` overload so a WS stream can share a pooled connection with ordinary HTTP/2 streams.
- **Informs**: FR-002 (WebSocket over the connection), FR-018 (fallback rationale), SC-007
- **Confidence**: high

### [2] .NET 9 Networking Improvements — .NET Blog
- **URL**: https://devblogs.microsoft.com/dotnet/dotnet-9-networking-improvements/
- **Type / version / date**: Official .NET blog; **published 2025-02-06** (.NET 9 GA wrap-up)
- **Architectural concern**: stream-multiplexing
- **Close-read findings**:
  - **`System.Net.Quic` public APIs became generally available (GA) in .NET 9** — `QuicConnection`/`QuicStream` are now supported public API, no longer preview. This is the load-bearing fact for the fallback path: a raw bidirectional QUIC stream is a first-class, supported building block in .NET 9.
  - QUIC options were expanded: `QuicConnectionOptions.HandshakeTimeout`, `KeepAliveInterval`, and a `StreamCapacityCallback` for managing the peer's stream-limit budget.
  - The only WebSocket change in .NET 9 is the **PING/PONG keep-alive** strategy (`KeepAliveTimeout`). The post discusses QUIC and WebSockets as **separate** improvements — there is **no WebSocket-over-HTTP/3 integration** anywhere in the .NET 9 networking release notes.
  - Implication: .NET 9 gives you the QUIC primitives but does not give you RFC 9220 wiring; you must frame WebSocket semantics yourself on top of `QuicStream`.
- **Informs**: FR-002, FR-018 (reuse 025 FrameCodec on QuicStream), SC-007
- **Confidence**: high

### [3] RFC 9220 — Bootstrapping WebSockets with HTTP/3
- **URL**: https://www.rfc-editor.org/rfc/rfc9220.html
- **Type / version / date**: IETF Standards-Track RFC; **published June 2022**
- **Architectural concern**: handshake
- **Close-read findings**:
  - RFC 9220 adapts RFC 8441's Extended CONNECT mechanism to HTTP/3: a client sends an Extended `CONNECT` with the `:protocol` pseudo-header (value `websocket`) and modified `:path`/`:authority` semantics. "The semantics of the pseudo-header fields and setting are identical to those in HTTP/2."
  - Capability is advertised by the server via **`SETTINGS_ENABLE_CONNECT_PROTOCOL` (value 0x08)**, registered separately in the HTTP/3 settings registry (RFC 9114) but with identical meaning to HTTP/2.
  - **Stream mapping**: a WebSocket maps to **a single bidirectional HTTP/3 (QUIC) stream**, replacing RFC 6455's TCP connection. Orderly close uses the stream FIN; abrupt termination uses `H3_REQUEST_CANCELLED` — TCP-like close semantics expressed through QUIC stream lifecycle. This is exactly the shape the fallback emulates.
  - The RFC deliberately does **not** redefine frame-level transport — RFC 6455 message framing (opcodes/FIN/payload-length) rides inside the stream's byte payload unchanged. So "WS over H3" and "WS framing over a raw QUIC bidi stream" carry an identical payload format; the only thing RFC 9220 adds is the Extended-CONNECT negotiation handshake.
- **Informs**: FR-002, FR-018 (fallback parity with the standard), SC-007
- **Confidence**: high

### [4] Experimental WebTransport over HTTP/3 support in Kestrel — .NET Blog
- **URL**: https://devblogs.microsoft.com/dotnet/experimental-webtransport-over-http-3-support-in-kestrel/
- **Type / version / date**: Official .NET blog; **published 2022-09-29** (.NET 7 RC1)
- **Architectural concern**: stream-multiplexing
- **Close-read findings**:
  - The only HTTP/3 application-layer protocol Kestrel offers beyond raw HTTP is **WebTransport, and it is explicitly "experimental"** — a *different* protocol from WebSocket (multiple streams per connection, not a single bootstrapped stream). It is **not** RFC 9220 WebSocket-over-HTTP/3.
  - Enabling it requires opting into preview: `<EnablePreviewFeatures>True</EnablePreviewFeatures>` plus a `RuntimeHostConfigurationOption` `Microsoft.AspNetCore.Server.Kestrel.Experimental.WebTransportAndH3Datagrams = true`.
  - "The default Kestrel development certificate cannot be used for WebTransport connections" — a cert-trust gotcha that also applies generally to HTTP/3/QUIC local testing (informs cert-fingerprint pinning work).
  - Takeaway: server-side, .NET has invested in WebTransport-over-H3, **not** WebSocket-over-H3. If the prototype wanted a standards path over H3 it would more realistically be WebTransport — but that is still preview and is not what FR-002 ("a WebSocket link") asks for.
- **Informs**: FR-002 (server-side WS-over-H3 absent), FR-018, SC-007
- **Confidence**: high

### [5] dotnet/runtime #69669 — [API Proposal]: WebSockets over HTTP/2
- **URL**: https://github.com/dotnet/runtime/issues/69669
- **Type / version / date**: GitHub API proposal (dotnet/runtime); **opened 2022-05-23, closed (shipped .NET 7) 2022-07-12**
- **Architectural concern**: handshake
- **Close-read findings**:
  - This is the *only* shipped "WebSockets over a newer HTTP version" feature in the runtime. It added `ClientWebSocketOptions.HttpVersion` + `HttpVersionPolicy`, the `ConnectAsync(Uri, HttpMessageInvoker, ...)` overload, public `HttpMethod.Connect`, and `HttpRequestHeaders.Protocol` (the `:protocol` pseudo-header). It cites **RFC 8441** (HTTP/2) only.
  - Every API-usage example pins `HttpVersion.Version20`. There is **no `Version30` story** in the proposal, and no follow-up "WebSockets over HTTP/3" API proposal was found in dotnet/runtime (issue search for `WebSocket over HTTP/3` / `Support WebSockets HTTP/3` returns no dedicated tracking item, only this HTTP/2 work). Absence of a tracked deliverable is itself evidence RFC 9220 is not on the near-term runtime roadmap.
  - The machinery (`HttpVersion`, `:protocol` header, Extended CONNECT plumbing through `SocketsHttpHandler`) is the exact substrate RFC 9220 would reuse — so the gap is "H3 not wired through `ClientWebSocket`," not a missing primitive. But until that wiring ships, setting `HttpVersion.Version30` on `ClientWebSocket` is not a supported, documented path.
- **Informs**: FR-002, FR-018, SC-007
- **Confidence**: high

### [6] dotnet/runtime #43495 — Networking stack: Technical roadmap
- **URL**: https://github.com/dotnet/runtime/issues/43495
- **Type / version / date**: GitHub roadmap issue (dotnet/runtime); long-lived (opened 2020)
- **Architectural concern**: packaging
- **Close-read findings**:
  - Confirms `ClientWebSocket` is a **fully managed implementation** ("`ClientWebSocket` implemented in managed code, with hard-wired, websocket-specific HTTP protocol handling"), shared cross-platform — so WS framing is .NET's own code, which means the prototype can legitimately reimplement RFC 6455 framing in C# without fighting an OS WinHTTP layer.
  - The roadmap groups `HttpClient` and `ClientWebSocket` under one "Web Stack" with no listed HTTP/3 WebSocket milestone — corroborating [5] that WS-over-H3 is not a tracked deliverable.
- **Informs**: FR-018, SC-007
- **Confidence**: med

### [7] Use HTTP/3 with the ASP.NET Core Kestrel web server — Microsoft Learn
- **URL**: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/http3?view=aspnetcore-9.0
- **Type / version / date**: Official docs (ASP.NET Core 9.0 moniker); current
- **Architectural concern**: ALPN
- **Close-read findings**:
  - HTTP/3 is **fully supported in Kestrel** since .NET 7; the ASP.NET Core HTTP/3 implementation **depends on MsQuic** for QUIC, requiring the native MsQuic library (libmsquic on Linux; bundled on Windows). This is a packaging dependency for any H3 server (informs the MsQuic-packaging research item).
  - HTTP/3 advertises via `Alt-Svc`/ALPN (`h3`); endpoints opt in with `HttpProtocols.Http1AndHttp2AndHttp3`. Nothing in the Kestrel HTTP/3 doc describes server-side WebSocket-over-HTTP/3 (Extended CONNECT) acceptance — server WS support is documented only for HTTP/1.1 and HTTP/2.
  - Confirms the QUIC transport exists and is production-grade in Kestrel; it's the *WebSocket bootstrap over it* that's missing — again pointing at the QuicStream fallback.
- **Informs**: FR-002 (server side), FR-018, SC-007
- **Confidence**: med

### [8] QuicStream Class (System.Net.Quic) — Microsoft Learn
- **URL**: https://learn.microsoft.com/en-us/dotnet/api/system.net.quic.quicstream?view=net-9.0
- **Type / version / date**: Official API reference; net-9.0 moniker, doc updated 2026-06-12
- **Architectural concern**: WS-framing
- **Close-read findings**:
  - `public sealed class QuicStream : System.IO.Stream` — a `QuicStream` **is a `Stream`** (`ReadAsync`/`WriteAsync`), so spec-025's `FrameCodec` (which frames over a byte stream) can sit directly on top with no adapter beyond what TCP already uses. A `QuicStream` can be **bidirectional** ("allows both sides to write"), which is exactly RFC 9220's single-bidi-stream model.
  - QUIC-specific lifecycle maps cleanly onto WebSocket close semantics: `CompleteWrites()` gracefully half-closes the write side (≈ WS Close handshake direction), `Abort(QuicAbortDirection, errorCode)` for abrupt teardown (≈ RFC 9220's `H3_REQUEST_CANCELLED`), and `WritesClosed`/`ReadsClosed` tasks signal each direction's completion.
  - A QUIC stream delivers **reliable, in-order** bytes within the stream (QUIC per-stream guarantee), so RFC 6455 framing carried inside it sees the same ordering/reliability contract it assumes over TCP — the fallback does not need spec-025's reorder/dedup just for in-stream correctness (those remain relevant across epochs/reconnects).
  - `Length`/`Position`/`Seek`/`SetLength` throw `NotSupportedException` (non-seekable, like any network stream) — `FrameCodec` must already tolerate this for TCP, so no new constraint.
- **Informs**: FR-018 (reuse 025 FrameCodec on QuicStream — the fallback mechanism), FR-002, SC-007
- **Confidence**: high

### [9] WebSockets support in ASP.NET Core — Microsoft Learn
- **URL**: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-9.0
- **Type / version / date**: Official docs (ASP.NET Core 9.0 moniker); current
- **Architectural concern**: handshake
- **Close-read findings**:
  - Server-side WebSocket acceptance in ASP.NET Core is documented for **HTTP/1.1 (GET Upgrade) and HTTP/2 (Extended CONNECT, since .NET 7)** only. "HTTP/2 WebSockets use CONNECT requests rather than GET." No HTTP/3 acceptance path is documented.
  - HTTP/2 WebSocket support must be explicitly enabled and negotiated; it is the server-side mirror of runtime #69669. The absence of any HTTP/3 equivalent on the server confirms the gap is symmetric (neither client `ClientWebSocket` nor Kestrel server speaks RFC 9220).
- **Informs**: FR-002 (server side), FR-018, SC-007
- **Confidence**: high

### [10] The WebSocket Protocol (RFC 6455 framing) — WebSocket.org guide
- **URL**: https://websocket.org/guides/websocket-protocol/
- **Type / version / date**: Engineering reference guide; current (2025)
- **Architectural concern**: WS-framing
- **Close-read findings**:
  - Frame layout the fallback must preserve: FIN bit + RSV + **opcode**, MASK bit, variable payload-length, optional masking key, payload. Opcodes: continuation **0x0**, **text 0x1**, **binary 0x2**, **close 0x8**, **ping 0x9**, **pong 0xA**. These are the message-type semantics FR-002's GLP interactions ride on.
  - Payload-length encoding: 0–125 inline (7-bit); 126 → following 16-bit length; 127 → following 64-bit length. Fragmentation via FIN=0 + continuation frames. The fallback FrameCodec must reproduce text/binary/close/ping/pong + FIN/fragmentation to be a faithful RFC 6455 message layer.
  - **Masking**: "Client-to-server masking uses a random 32-bit key XORed with the payload to prevent cache-poisoning attacks on intermediary proxies." This anti-cache-poisoning measure targets TCP proxies that might misinterpret unmasked client bytes. On a dedicated, TLS-encrypted QUIC stream with no intermediary parsing the byte stream, masking is **not applicable** — the fallback can (and per the threat model, should) omit it without violating RFC 6455's intent. (Consistent with how the H3/H2 bootstrap relies on the encrypted transport rather than per-frame masking.)
- **Informs**: FR-018 (RFC 6455 semantics the fallback FrameCodec must preserve), FR-002, SC-007
- **Confidence**: high

### [11] Future of WebSockets: HTTP/3, WebTransport & Beyond — WebSocket.org
- **URL**: https://websocket.org/guides/future-of-websockets/
- **Type / version / date**: Engineering survey; current as of **early 2026** (explicit in text)
- **Architectural concern**: failure-modes (ecosystem maturity / interop risk)
- **Close-read findings**:
  - "As of early 2026, WebSocket over HTTP/3 (RFC 9220) has **no production implementations** in browsers or most web servers." Chrome is only at "Intent to Prototype"; Firefox/Safari have no announced plans; Nginx "In Development", Caddy blocked on Go, LiteSpeed's lsquic does not support RFC 9220.
  - On .NET specifically: ".NET's MsQuic implementation has HTTP/3 available but **WebSocket support is 'In Development.'**" — i.e. the QUIC/H3 transport is there, RFC 9220 WS bootstrap is not.
  - "Bottom line: Use HTTP/1.1 or HTTP/2 WebSockets in production today." WebTransport "complements" rather than replaces WebSockets and has better adoption (Chrome/Edge since v97) but limited Safari support.
  - For a prototype where both endpoints are ours (C# client + C# server, no browser, no proxy), the broad-ecosystem immaturity matters less — but it confirms there is no interop partner to test RFC 9220 against, reinforcing the fallback choice.
- **Informs**: FR-002, FR-018, SC-007 (corpus distillation on maturity)
- **Confidence**: med (secondary source, but consistent with all primary sources above)

### [12] RFC 8441 — Bootstrapping WebSockets with HTTP/2
- **URL**: https://datatracker.ietf.org/doc/html/rfc8441
- **Type / version / date**: IETF Standards-Track RFC; **published September 2018**
- **Architectural concern**: handshake
- **Close-read findings**:
  - The HTTP/2 precedent RFC 9220 explicitly builds on and the one .NET *does* implement (runtime #69669, .NET 7+). Defines Extended CONNECT, the `:protocol` pseudo-header, and `SETTINGS_ENABLE_CONNECT_PROTOCOL` — RFC 9220 reuses all of this verbatim, only re-registering the setting for HTTP/3.
  - Establishes that, post-handshake, **RFC 6455 frames flow unchanged** over the single (here HTTP/2) stream. This is the conceptual proof that the fallback's "RFC 6455 framing over one bidi stream" is the standards-sanctioned payload model — the prototype simply substitutes a `QuicStream` for the H2/H3 Extended-CONNECT stream and skips the (currently unimplemented in .NET) Extended-CONNECT negotiation.
  - Useful for the "standards-honest" framing: the fallback is RFC 6455 message semantics over a stream, identical in payload to what RFC 8441/9220 carry; only the negotiation handshake differs.
- **Informs**: FR-018, FR-002, SC-007
- **Confidence**: high

---

## Cluster feasibility verdict

- **RFC 9220 WebSocket-over-HTTP/3 is NOT usable in .NET 9, client or server, today.** Client side: `ClientWebSocket`'s documented and shipped ceiling is HTTP/2 (`HttpVersion.Version20`, RFC 8441); the Learn docs (updated 2025-01-31) and the .NET 9 networking blog (2025-02-06) mention no HTTP/3 WebSocket support, and the only shipped multi-version work is runtime #69669 "WebSockets over HTTP/2" (closed 2022, .NET 7). No `Version30` WebSocket path is documented or tracked. Confidence: high.
- **Server side is equally absent.** ASP.NET Core documents server WebSocket acceptance only over HTTP/1.1 and HTTP/2 Extended CONNECT; Kestrel's only HTTP/3 application protocol beyond plain HTTP is **WebTransport, which is still "experimental"** (preview flags required) and is a *different* protocol, not RFC 9220 WebSocket. WebSocket.org (early-2026) corroborates: ".NET's MsQuic implementation has HTTP/3 available but WebSocket support is 'In Development.'" Confidence: high.
- **The QUIC primitives the fallback needs ARE production-ready in .NET 9.** `System.Net.Quic` went GA in .NET 9; `QuicStream : System.IO.Stream` is a reliable, in-order, **bidirectional** byte stream with graceful (`CompleteWrites`) and abrupt (`Abort`) close that map cleanly onto WebSocket/RFC 9220 close semantics. (Server-side QUIC/H3 also depends on the native **MsQuic** library — a packaging dependency to track separately.) Confidence: high.
- **The fallback is sound and standards-honest.** RFC 9220 and RFC 8441 both carry **unchanged RFC 6455 frames over a single bidirectional stream**; the *only* thing they add over the fallback is the Extended-CONNECT negotiation handshake (which .NET has not wired for H3). So "RFC 6455 framing over one `QuicStream` + spec-025 `FrameCodec`" is byte-for-byte the same payload model as the standard, minus a handshake .NET cannot currently perform. The fallback must preserve RFC 6455 message semantics: opcodes text 0x1 / binary 0x2 / close 0x8 / ping 0x9 / pong 0xA, the FIN bit + continuation (0x0) fragmentation, and variable payload-length encoding. **Masking is N/A** — RFC 6455 masking exists only to defeat TCP intermediary cache-poisoning; on a TLS-encrypted, intermediary-free QUIC stream it is unnecessary, and omitting it does not violate the spec's intent. Confidence: high.
- **Recommendation (updates research.md Decision 3): adopt the QUIC-stream fallback as the primary implementation, not a contingency.** Implement WebSocket-style RFC 6455 framing over a single bidirectional `QuicStream`, reusing spec-025's `FrameCodec`/`Crc32`/sequencing as a new transport leaf alongside `LoopbackTransport`/`TcpTransport` (FR-018). Document explicitly that this is **not** RFC 9220 (no Extended CONNECT) to stay standards-honest, and isolate the negotiation seam so a real RFC 9220 `ClientWebSocket`/Kestrel path can be slotted in later if/when .NET ships it. Confidence: high.
- **Residual risk / watch items:** (1) MsQuic native-library packaging for the server H3/QUIC listener; (2) `StreamCapacityCallback`/peer stream limits if more than one logical link multiplexes onto one QUIC connection; (3) no external RFC 9220 interop partner exists to test against, so the fallback's wire format is self-defined between our two C# endpoints — acceptable for a prototype, but pin it in the contracts. Confidence: med.

## Sources
- [WebSockets support in .NET — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/fundamentals/networking/websockets)
- [.NET 9 Networking Improvements — .NET Blog](https://devblogs.microsoft.com/dotnet/dotnet-9-networking-improvements/)
- [RFC 9220 — Bootstrapping WebSockets with HTTP/3](https://www.rfc-editor.org/rfc/rfc9220.html)
- [Experimental WebTransport over HTTP/3 support in Kestrel — .NET Blog](https://devblogs.microsoft.com/dotnet/experimental-webtransport-over-http-3-support-in-kestrel/)
- [dotnet/runtime #69669 — WebSockets over HTTP/2](https://github.com/dotnet/runtime/issues/69669)
- [dotnet/runtime #43495 — Networking stack technical roadmap](https://github.com/dotnet/runtime/issues/43495)
- [Use HTTP/3 with the ASP.NET Core Kestrel web server — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/kestrel/http3?view=aspnetcore-9.0)
- [QuicStream Class (System.Net.Quic) — Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/system.net.quic.quicstream?view=net-9.0)
- [WebSockets support in ASP.NET Core — Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-9.0)
- [The WebSocket Protocol (RFC 6455 framing) — WebSocket.org](https://websocket.org/guides/websocket-protocol/)
- [Future of WebSockets: HTTP/3, WebTransport & Beyond — WebSocket.org](https://websocket.org/guides/future-of-websockets/)
- [RFC 8441 — Bootstrapping WebSockets with HTTP/2](https://datatracker.ietf.org/doc/html/rfc8441)

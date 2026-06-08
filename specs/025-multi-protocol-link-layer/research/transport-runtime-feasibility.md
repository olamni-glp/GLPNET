# Transport-Leaf Library Feasibility — Dart vs C#/.NET (feature 025)

**Question (Gabi):** Which runtime — Dart or C#/.NET — lets the MOST transport leaves be
built with trusted, mature, easy, low-risk libraries, and where does each MISS a link?

**Calibration prior:** the LANGUAGE layer (the 3 core fixes + comparison guards + the link
primitives) is **near-trivial in Dart** (existing runtime, small changes). The open
question is the **transport leaves**: does Dart have trusted libraries for ALL of them, or
does it miss some (AMQP 1.0, DDS, HTTP/3, BLE LE-Audio/L2CAP) that C#/.NET covers better?
This document answers that with library-level evidence.

**Framing held fixed (do not re-litigate):** every link is PEER-TO-PEER to the IMMEDIATE
peer. A broker/server (MQTT, AMQP, XMPP) is a separate node/level, OUT OF SCOPE — judge the
leaf as a P2P byte-pipe to one peer, with TLS. Each leaf sits behind ONE uniform per-runtime
seam — `open / send-bytes / recv-bytes / close + fault` selected by scheme (**FR-058**) — so
leaves can be authored per-runtime and need not be auto-converted. Acceptance = a leaf runs
on at least ONE of Windows OR Android (**FR-063**); a leaf infeasible on BOTH is documented
with rationale (**FR-019/FR-064**). Inter-host links are TLS-by-default (**FR-029**).

Evaluated leaves = the full **FR-012** lineup (spec.md L131–138).

---

## 1. Comparison matrix

Status legend: **trivial** | **moderate** | **hard** | **missing** (infeasible — no trusted
library / no app-level byte channel).

| # | Transport (FR-012) | Dart status | C#/.NET status | Platform (feasible end) | Verdict | Leading lib (Dart \| C#) |
|---|---|---|---|---|---|---|
| 1 | **WebSocket** (ws + wss) | trivial | trivial | Win + Android (server end = Win desktop) | **TIE** | web_socket_channel 3.0.3 + dart:io server \| System.Net.WebSockets ClientWebSocket + HttpListener/Kestrel |
| 2 | **HTTP/2** (h2 + h2c) | moderate | moderate | Win + Android (server = Win desktop) | **TIE — slight Dart edge** | package:http2 2.3.1 (bare client+server over SecureSocket) \| HttpClient h2 + gRPC/Kestrel for duplex |
| 3 | **HTTP/3 (QUIC)** | **hard** | moderate | C#: **Win 11+** only; Dart: none trusted | **C# WINS — Dart MISSES** | flutter_quic 1.0.0 (immature FFI, 78 dl) \| System.Net.Quic (in-box, stable since .NET 9) |
| 4 | **MQTT** (3.1.1 / 5.0) | trivial | trivial | Win + Android | **TIE — slight C# edge** | mqtt_client 10.x + mqtt5_client 4.x \| MQTTnet 5.1 (29.9M dl, + embedded broker) |
| 5 | **AMQP 1.0** (genuine 1.0) | **missing** | trivial | C#: Win + Android | **C# WINS — Dart MISSES** | — (only dart_amqp = 0.9.1, unmaintained) \| AMQPNetLite 2.5.3 (client + ContainerHost listener) |
| 6 | **XMPP** | moderate | moderate | Win + Android (needs server relay) | **TIE** | whixp 3.3.1 (maintained, low adoption) \| XmppDotNet 3.2.6 / Waher |
| 7 | **DDS** (OMG / RTPS) | **missing** | **hard** (commercial) | C#: Win + Android (licensed) | **C#-only, HARD — Dart MISSES** | — (pub.dev "dds" is unrelated) \| Rti.ConnextDds 7.7.0 (commercial license + native dep) |
| 8 | **CoAP** (+ DTLS, blockwise) | **hard** (client-only) | moderate | C#: Win + Android; Dart: client end only | **C# WINS — Dart MISSES server end** | coap 9.2.1 + dtls2 (client-only) \| CoAPnet 1.2.0 (client+server, stale) |
| 9 | **SSH tunnelling** | moderate | moderate | Win + Android (client); server = C# only | **TIE — slight C# edge** | dartssh2 2.17.1 (client-only) \| SSH.NET 2025.1.0 + FxSsh server |
| 10 | **FTP** | trivial | moderate | Win + Android | **DART WINS** | ftpconnect 2.0.10 + ftp_server 2.3.2 (both ends!) \| FluentFTP 54.2.0 (client-only) |
| 11 | **SFTP** | moderate | moderate | Win + Android (client); server = C# only | **TIE — slight C# edge** | dartssh2 SFTP (client-only) \| SSH.NET SftpClient + FxSsh server |
| 12 | **File endpoints** (bin+text r/w/search) | trivial | trivial | Win + Android | **TIE** | dart:io (stdlib) \| System.IO (BCL) |
| 13 | **BLE GATT** (r/w/notify) | moderate | trivial | Win + Android (both ends) | **C# edge** | bluetooth_low_energy 6.2.1 (dual-role) \| WinRT GATT + Android BluetoothGattServer |
| 14 | **BR/EDR SPP** (RFCOMM) | **hard** | trivial | C#: Win + Android; Dart: client/Android-leaning | **C# WINS** | flutter_bluetooth_* (client-only/immature) \| 32feet.NET + WinRT StreamSocket |
| 15 | **L2CAP CoC** | hard | moderate | **Android only** (both runtimes; Win blocked) | **C# edge (Android)** | blev 0.0.7 / l2cap_ble 0.0.1 (pre-1.0) \| Android.Bluetooth Create/ListenL2capChannel |
| 16 | **BLE LE-Audio BIS** | **missing** | **missing** | **NEITHER** (no app ISO channel) | **TIE — both BLOCKED** | none \| none (FR-019/FR-041 platform wall) |
| 17 | **BLE LE-Audio CIS** | **missing** | **missing** | **NEITHER** as literal CIS | **TIE — both BLOCKED** (intent → L2CAP CoC) | none \| none (FR-019/FR-042; use L2CAP CoC) |

---

## 2. Verdict — answers to Gabi's five questions

### (1) Is Dart near-trivial with trusted/easy libraries for ALL the links? — **FALSE.**

Dart is near-trivial for the *language* layer, but the *transport leaves* are NOT all
covered. Dart has trusted/easy libraries for the common web + file + MQTT + FTP leaves, but
it **MISSES four leaves outright** (no trusted library or no usable server end) and is
**materially harder** on several Bluetooth leaves. The prior worry is confirmed by evidence.

### (2) Which links does Dart MISS, or only do with major difficulty?

**Dart MISSES (no trusted library / no usable byte-channel end):**
- **AMQP 1.0** — NO Dart AMQP 1.0 library exists at all. Only `dart_amqp` (AMQP **0.9.1**,
  unmaintained ~2 yr), a different, broker-mediated protocol that cannot model a brokerless
  bilateral P2P link (**FR-005/FR-015**). Hard gap.
- **DDS** — NO OMG DDS / RTPS implementation for Dart, period. (The pub.dev package named
  "dds" is the *Dart Development Service* debug proxy — an unrelated name collision, a trap.)
- **HTTP/3 (QUIC)** — no trusted Dart option. `dart:io` HTTP/3 is closed-not-planned
  (dart-lang/sdk #38595); `cronet_http` is Android-only, client-only, request/response (no
  byte seam, fails FR-003); `flutter_quic` is the only both-ends/byte-stream candidate but is
  immature (78 downloads, 10 likes, single maintainer, "basic TLS validation only").
- **CoAP server end** — the Dart `coap` package is fundamentally a CLIENT; there is no
  trusted pure-Dart CoAP **server** (listen/serve-resources). A bilateral link whose Dart end
  must be the listener has no leaf (**FR-003/FR-004**). Dart CoAP DTLS is also experimental
  and not native on Windows (must bundle OpenSSL binaries).

**Dart does only with major difficulty / trust risk (server end or maturity tax):**
- **BR/EDR SPP** — Dart Classic packages are client-only, Android-leaning, immature
  (single-digit likes); cannot cleanly satisfy FR-003 both-ends on Windows.
- **L2CAP CoC** — only pre-1.0, low-adoption, Android-only plugins (`blev` 0.0.7 /
  `l2cap_ble` 0.0.1). Feasible on Android with trust risk; Windows blocked for BOTH runtimes.
- **SSH / SFTP server end** — `dartssh2` is explicitly client-only ("❌ Server"); a
  Dart↔Dart tunnel needs an external/foreign SSH server.
- **BLE GATT** — feasible dual-role on Win+Android, but only via the mid-tier
  `bluetooth_low_energy`; the popular `flutter_blue_plus` is central-only / no native Windows.

**Blocked on BOTH runtimes (platform/OS wall, not a Dart-specific gap):**
- **BLE LE-Audio BIS** and **CIS** — neither OS surfaces an application-visible isochronous
  byte channel. Identical wall on Dart and C#. This is the **FR-019/FR-041** documented block,
  not a runtime-choice differentiator.

### (3) Which links are C#-only or clearly easier in C#?

**C#-only (Dart has no viable leaf):**
- **AMQP 1.0** — AMQPNetLite 2.5.3 (genuine 1.0, client + `ContainerHost` listener =
  brokerless P2P, TLS+SASL). No Dart counterpart exists.
- **DDS** — Rti.ConnextDds 7.7.0 is the only production-grade option, but **HARD**:
  commercial license + native dependency + RTPS-discovery impedance vs a bilateral link.
  Strongest **drop/defer** candidate; cannot satisfy the Dart↔C# parity gate (no Dart end).
- **HTTP/3 (QUIC)** — System.Net.Quic is in-box, **stable since .NET 9**, byte-pipe
  (`QuicStream : Stream`), mandatory TLS 1.3, symmetric both-ends, with per-stream
  multiplexing (directly serves FR-025, no HOL blocking across links). Constraint: Windows
  11/Server 2022+ floor; Android effectively absent (document per FR-064).
- **CoAP server end** — only C# can be BOTH CoAP ends with DTLS + blockwise.

**Clearly easier in C# (Dart feasible but harder / less trusted):**
- **BR/EDR SPP** — 32feet.NET (`BluetoothClient` + `BluetoothListener` over `System.IO.Stream`)
  + WinRT `StreamSocket`/`StreamSocketListener`: mature, both-ends, Stream-based on Windows;
  RFCOMM on Android. Best-covered leaf in the Bluetooth family.
- **BLE GATT** — first-party dual-role on both Windows (WinRT) and Android, no
  package-choice tax.
- **L2CAP CoC (Android)** — first-party `Android.Bluetooth` binding (trust edge over Dart's
  pre-1.0 plugins).
- **SSH / SFTP** — C# can host the **server** end (FxSsh) → self-contained both-ends tunnel.
- **MQTT** — slight edge: MQTTnet bundles an embedded broker (handy for the parity test).

### (4) Counting links buildable without major issues — which runtime to START with?

**Count of leaves buildable WITHOUT major issues (trivial or moderate, with a trusted lib):**

| Runtime | trivial/moderate (buildable) | hard | missing/blocked |
|---|---|---|---|
| **C#/.NET** | **13** (WS, HTTP/2, HTTP/3, MQTT, AMQP1.0, XMPP, CoAP, SSH, SFTP, File, GATT, SPP, L2CAP) | 1 (DDS) | 3 (FTP=moderate not missing; BIS, CIS) |
| **Dart** | **9** (WS, HTTP/2, MQTT, XMPP, SSH-client, SFTP-client, FTP, File, GATT) | 3 (HTTP/3, CoAP, SPP, L2CAP) | 4 (AMQP1.0, DDS; + BIS, CIS) |

(FTP is C#-moderate — FluentFTP client is best-in-class but C# has no blessed embedded FTP
server; counted as buildable. BIS/CIS blocked on both. DDS is C#-hard, Dart-missing.)

**RECOMMENDATION: START with C#/.NET as the REFERENCE runtime.** C# can build the most
transport leaves with trusted, mature, low-risk libraries — decisively so on the four leaves
Dart cannot do at all (AMQP 1.0, DDS, HTTP/3, CoAP-server) and on the Bluetooth family (SPP,
GATT, L2CAP). This corroborates the already-RULED B3 decision (C#-first reference, Dart mirror
after).

**Important qualifier — this does NOT make Dart second-class:**
- The language layer is near-trivial in Dart **either way** — the runtime choice is about
  transport-leaf library coverage, not about the GLP runtime.
- Because each leaf sits behind ONE per-runtime seam (**FR-058**) and leaves MAY be authored
  per-platform/native (not auto-converted), leaves can be authored **per-runtime**. The
  Dart-feasible leaves (WebSocket, HTTP/2, MQTT, XMPP, FTP, File, GATT, SSH/SFTP clients) can
  be **mirrored trivially in Dart** for the FR-059/FR-062 Dart↔C# parity gate.
- The runtime choice changes **feasibility** (not mere convenience) on exactly these leaves:
  **HTTP/3, AMQP 1.0, DDS, CoAP-server** — and these are precisely the leaves the open
  question predicted. For WebSocket + HTTP/2 + MQTT + File the runtimes are at parity (Dart
  marginally cleaner for HTTP/2's bare symmetric byte pipe).

**Suggested bring-up order (lowest-risk first):**
1. **File endpoints** — trivial both runtimes; the natural FIRST FR-016 feasibility test +
   SC-001 loopback gate.
2. **WebSocket** — trivial both; lowest-risk first **network** transport and the cleanest
   FR-062 Dart↔C# parity gate.
3. **MQTT** then **HTTP/2** — both feasible both runtimes (parity).
4. C#-only leaves (HTTP/3, AMQP 1.0, CoAP, SPP, GATT, L2CAP) authored C#-first.
5. **DDS** — defer/drop candidate (commercial, hard, no Dart parity).

### (5) Per-leaf implementation insights — see §3 below (package + seam map + gotchas, both runtimes).

---

## 3. The GAP list (FR-019 documentation candidates)

| Leaf | Dart | C# | FR-019/FR-063/FR-064 disposition |
|---|---|---|---|
| **AMQP 1.0** | INFEASIBLE (no 1.0 lib) | feasible (AMQPNetLite) | Accept C#-only on Win+Android; document Dart-infeasible. No Dart end for parity. |
| **DDS** | INFEASIBLE (no lib) | HARD (commercial + native) | Accept C#-only (licensed) OR **defer/drop**; document Dart-infeasible. No cross-runtime parity possible. Strongest drop candidate. |
| **HTTP/3 (QUIC)** | HARD (no trusted lib) | feasible (System.Net.Quic) | Accept C#-only on **Windows 11+**; document Dart-infeasible + Android gap. Revisit Dart mirror if flutter_quic matures or #38595 reverses. |
| **CoAP (server end)** | HARD (client-only; DTLS exp.) | moderate (CoAPnet, both ends) | Accept C#-both-ends; document Dart-server-infeasible. Dart usable as client end only. |
| **L2CAP CoC** | hard (pre-1.0 plugins) | moderate (Android binding) | Accept **Android-only** (BOTH runtimes); document **Windows blocked** (no WinRT BLE L2CAP CoC). |
| **BR/EDR SPP** | HARD (client-only/immature on Win) | trivial (32feet/WinRT) | Accept C# on Windows; document Dart-Windows-both-ends gap. |
| **BLE GATT** | moderate (dual-role via niche lib) | trivial (first-party) | Accept BOTH on Win+Android; note Dart package-choice tax. |
| **BLE LE-Audio BIS** | INFEASIBLE | INFEASIBLE | **Blocked on BOTH platforms/runtimes** — no app ISO byte channel. FR-041 open co-design item; ship broadcast as N bilateral ground-copy links (FR-040). |
| **BLE LE-Audio CIS** | INFEASIBLE (literal CIS) | INFEASIBLE (literal CIS) | **Blocked as literal CIS on both**; satisfy FR-042 bilateral intent via **L2CAP CoC** instead. |

**Note on BIS/CIS:** this is a **platform/OS wall, identical on both runtimes** — NOT a
library or language gap and NOT a runtime-choice differentiator. Microsoft's own docs note
the Bluetooth core spec (≤5.3) defines no standard HCI for host ISO data; Windows routes
LE-Audio only through vendor audio drivers (VSAP/ACX); Android's `BluetoothLeAudio` is
codec/state-only and Auracast is system-privileged.

---

## 4. Per-leaf implementation insights (both runtimes — to seed implementation)

All leaves map to the FR-058 seam: **open / send-bytes / recv-bytes / close + fault**. Frames
are already self-delimiting per FR-022 (version-byte + length + CRC + seq#), so message-framed
transports carry one frame per message and the reliability sublayer (FR-018/020/021) owns
FIFO/dedup/ordering where the carrier provides none.

### Web transports

- **WebSocket** — *open*: `WebSocketChannel.connect(Uri('wss://…'))` / `HttpServer.bind +
  WebSocketTransformer.upgrade` (Dart); `ClientWebSocket.ConnectAsync` / `HttpListener
  .AcceptWebSocketAsync` or Kestrel (C#). *send*: `sink.add(List<int>)` /
  `SendAsync(buffer, Binary)`. *recv*: `stream.listen` / `ReceiveAsync` loop reassembling
  `EndOfMessage`. *close*: `close(code,reason)` / `CloseAsync`. *fault*: onDone+onError /
  `WebSocketException` + **.NET 9 PING/PONG keep-alive** (`KeepAliveTimeout`) → tempFail/
  permFail. wss/TLS via SecurityContext (Dart) / transparent client + Kestrel cert (C#).
  Send binary frames carrying the opaque PayloadSerializer blob.

- **HTTP/2** — one link = one HTTP/2 **stream** (bidirectional) = one LinkId. *open* (Dart):
  `SecureSocket.connect(…, supportedProtocols:['h2'])` then `ClientTransportConnection
  .viaSocket` / server `ServerTransportConnection.viaStreams` over raw SecureServerSocket —
  bare symmetric byte pipe, ALPN h2 = TLS-by-default. *open* (C#): `HttpClient` `Version20`
  (client) / Kestrel endpoint or **gRPC duplex streaming** (server — bare HttpClient has a
  duplex-content sharp edge, dotnet/runtime #1511/#29255). *send*: `stream.sendData(bytes,
  endStream:false)`. *recv*: listen for `DataStreamMessage`. mTLS via `SecurityContext` /
  `SslClientAuthenticationOptions`. Both "moderate" — the duplex-stream-as-byte-pipe mapping
  is the non-trivial part. Pin http2 2.3.1 (quieter adoption).

- **HTTP/3 (QUIC) — C#-only** — *open*: `QuicListener.ListenAsync` / `QuicConnection
  .ConnectAsync` then `OpenOutboundStreamAsync(Bidirectional)`; one LinkId per `QuicStream`,
  many links multiplex on one `QuicConnection` (no cross-link HOL → FR-025). *send/recv*:
  `QuicStream.WriteAsync/ReadAsync(Memory<byte>)` — byte pipe directly. *close*:
  `CloseAsync(code)` + `DisposeAsync`. *fault*: `QuicException` + `ReadsClosed/WritesClosed`
  Tasks. ALWAYS TLS 1.3 → FR-029 by construction. **MUST guard** with
  `QuicListener.IsSupported`/`QuicConnection.IsSupported` (false if MsQuic missing or no
  TLS 1.3) → emit clean infeasible/permFail rather than crash. Windows 11+ floor (FR-064).

### Messaging

- **MQTT** — client-only both runtimes (broker = out-of-scope node). *open* (Dart):
  `MqttServerClient.withPort(host,id,port)` + `secure=true` + `securityContext`, `connect()`,
  subscribe per-link topic. *send*: `publishMessage(topic, atLeastOnce, payload bytes)`.
  *recv*: `client.updates` Stream → `MqttPublishMessage.payload.message` (Uint8List).
  *fault*: `onDisconnected`/`onConnected`/`connectionStatus`. *(C# MQTTnet):* `ConnectAsync`
  with `.WithTlsOptions(…)`; `PublishAsync(…WithPayload(byte[]))`;
  `ApplicationMessageReceivedAsync` → `args.ApplicationMessage.PayloadSegment`;
  `DisconnectedAsync`. MQTTnet `MqttServer` can host the broker in-process for the parity test.
  Gotcha: QoS0 can drop and there is no broker ordering across topics → end-to-end FIFO must
  come from the FR-018/FR-022 sublayer.

- **AMQP 1.0 — C#-only** (AMQPNetLite, brokerless P2P): *server*: `new ContainerHost(new
  Uri("amqps://0.0.0.0:5671")); host.RegisterLinkProcessor(proc); host.Open();` TLS via
  `ConnectionListener` + `X509Certificate2`; SASL on the listener. *client*: `Connection
  .Factory.CreateAsync(new Address("amqps://host:5671"))` → `Session` → `SenderLink`/
  `ReceiverLink`. *send*: `sender.Send(new Message{ BodySection = new Data{ Binary = byte[] }})`
  — AMQP Data body carries the opaque blob. *recv*: `await receiver.ReceiveAsync()` →
  `((Data)msg.BodySection).Binary` + `receiver.Accept(msg)`. *fault*: `Connection.Closed`
  event with Error. Single-link delivery sequence preserves FR-018 FIFO naturally. **No Dart
  counterpart** — a Dart parity end would have to hand-roll AMQP 1.0 framing.

- **XMPP** — moderate both; inherently client-to-server (server = relay under one endpoint
  pair, same shape as the MQTT broker). Seam is awkward: encode the opaque blob as **base64**
  in a `<message>` body (or a namespaced extension). *(Dart Whixp):* `Whixp(jid, pw, host,
  port)` + TLS, `connect()`; `whixp.send(MessageStanza(to: peerJid, body: base64(frame)))`;
  message-received stream → decode. *(C# XmppDotNet):* `XmppClient` (TLS on by default),
  `ConnectAsync`; `SendAsync(new Message{To=peerJid, Body=base64(frame)})`;
  `XmppXElementReceived` Rx → filter Message → decode. XEP-0198 Stream Management gives
  resumption/ack (helps FR-020). Both clients federate through the same server → one of the
  EASIER cross-runtime parity leaves despite the byte-mapping. Requires a deployed XMPP server
  (ejabberd/Prosody) — out-of-scope node but a real deployment dependency.

- **DDS — C#-only, HARD** (RTI Connext): *open*: `DomainParticipant` via factory; for a P2P
  byte link constrain to ONE Topic of an IDL type wrapping `sequence<octet>` + a matched
  Partition so only the one peer matches; create `DataWriter` (writer end) + `DataReader`
  (reader end). *send*: `writer.Write(sample)` with `sample.Data = byte[]`. *recv*:
  `reader.Take()`/DataAvailable → `sample.Data`; QoS RELIABLE + KEEP_ALL + HISTORY → ordered
  reliable on one writer/reader (FR-018/020). *fault*: Liveliness/SubscriptionMatched/
  RequestedDeadlineMissed listeners. TLS via RTI DDS Security plugins. Gotchas: (1) commercial
  license gate; (2) native `Rti.ConnextDds.Native` per-platform packaging (Android = NDK
  burden); (3) RTPS auto-discovery/multicast must be PINNED (initial-peers / disable
  multicast) to keep the link bilateral; (4) NO Dart peer → permanently outside parity.

### Constrained / tunnelling / file

- **CoAP (+DTLS, blockwise)** — *open*: `CoapClient` (both runtimes) / `CoapServer`
  (C# only). *send*: POST/PUT (CON) carrying byte[] in the CoAP payload. *recv*: server
  resource `OnPost` handler / client `Observe` notification. **Blockwise (RFC7959
  Block1/Block2)** is the native fit for FR-022 fragmentation under the ~1 KB MTU — let the
  library do it. DTLS-PSK = simplest mTLS-equivalent (FR-029). CoAP is request/response, not
  duplex: run a resource accepting POSTed frames + Observe for the reverse direction. Dart
  `coap` is client-only + experimental Windows DTLS (must bundle OpenSSL DLLs).

- **SSH tunnelling** — the SSH layer is just an encrypted carrier (covers FR-029); the GLP
  peer is whatever the forwarded channel reaches. *open*: `SSHClient`(dartssh2) / `SshClient`
  (SSH.NET) connect, open a **direct-tcpip** channel (treat as a duplex byte stream feeding
  send/recv directly — cleaner than exec or file-drop). *fault*: connection-closed callback →
  LinkFaultSignal. dartssh2 = `Stream<Uint8List>`; SSH.NET = blocking streams wrapped in Task.
  **Gotcha:** dartssh2 cannot listen → a Dart↔Dart tunnel needs OpenSSH sshd or **FxSsh**
  (C# embedded SSH server). C# can be self-contained both-ends.

- **FTP — Dart wins** (only runtime with trusted client AND server in-language): *open*:
  `FTPConnect.connect` / `ftp_server FtpServer.start` (Dart) ; `AsyncFtpClient.Connect`
  (FluentFTP, client only). TLS via `securityType=FTPES` (FR-029); ftp_server does explicit/
  implicit FTPS + mTLS. FTP is file-oriented, not a byte stream: *send* = STOR a seq#-named
  file containing the frame into an agreed dir; *recv* = peer LIST/poll + RETR + DELE. No
  push → recv polls; leans entirely on the reliability sublayer for FIFO/dedup. Prefer SFTP
  if a tunnel is already present.

- **SFTP** — same file-as-frame idiom over the encrypted SSH channel (no FTPS cert dance,
  atomic-ish rename for visibility). *open*: `SSHClient.sftp()` (dartssh2, client only) /
  `new SftpClient().Connect` (SSH.NET) ; server end **C#-only** via FxSsh + SFTP subsystem
  (freesftpsharp). Reuses the SSH leaf's connection/fault plumbing. If an SSH tunnel exists,
  prefer a direct-tcpip byte stream over file-drop.

- **File endpoints** — trivial/built-in both (`dart:io` File/RandomAccessFile/Directory.watch
  vs `System.IO` FileStream/FileSystemWatcher). *open*: open a file or a directory "mailbox".
  *send*: append a length-prefixed frame (self-delimiting per FR-022) OR one seq#-named file
  per frame. *recv*: `Directory.watch`/`FileSystemWatcher` fires on new frame → read+advance
  offset (or read+delete spool file) — push-style, no polling. *fault*: watcher error /
  file-locked / path-gone. "search" (bin+text) = enumerate + per-file content scan → useful
  for recovery/GC (FR-024) + dedup detection of leftover frames after a crash.
  Single-writer-append + reader-tracks-offset gives per-link FIFO (FR-018) for free.
  **Canonical bring-up/loopback transport — the FIRST FR-016 test + SC-001 gate.**

### Bluetooth

- **BLE GATT** — *open*: publish service + advertise (server) / connect + discover (client).
  Use a **Write** characteristic (C2S frames) + a **Notify** characteristic (S2C frames).
  *send*: `WriteValueAsync` (C2S) / `NotifyValueAsync` (S2C). *recv*: `WriteRequested` event
  (server) / `CharacteristicValueChanged` (client). *fault*: connection-status-changed.
  Encryption = bonded/encrypted link (`GattProtectionLevel`) → FR-029 intent.
  **Shared gotcha:** ATT MTU ~20–23 B default (~244 negotiated) → FR-022 fragmentation
  mandatory. C# trivial (first-party WinRT `GattServiceProvider` + Android `BluetoothGattServer`,
  both roles, both OSes); Dart moderate (must use `bluetooth_low_energy`, NOT the popular
  central-only `flutter_blue_plus`).

- **BR/EDR SPP — C# clear win** (32feet.NET / WinRT, Stream-based = byte-frame seam is free):
  *server*: `new BluetoothListener(serviceGuid); Start(); AcceptBluetoothClient()`. *client*:
  `new BluetoothClient(); Connect(addr, BluetoothService.SerialPort)`. *send/recv*:
  `stream.Write/Read`. *fault*: `IOException`. WinRT alt: `StreamSocketListener
  .BindServiceNameAsync(rfcommServiceId)` + `ConnectionReceived`; `StreamSocket.ConnectAsync`
  with `SocketProtectionLevel.BluetoothEncryptionAllowNullAuthentication` (FR-029). RFCOMM on
  Android. 32feet caveat: one connection per service (fine for one bilateral link, FR-005).
  Dart would need a custom platform channel for the Windows listener half.

- **L2CAP CoC (Android only, both runtimes)** — *server*: `adapter.ListenUsingL2capChannel()`
  then `serverSocket.accept()`. *client*: `device.CreateL2capChannel(psm).connect()`. *send/
  recv*: `socket.OutputStream.Write` / `socket.InputStream.Read` (blocking → wrap in Task/
  Stream). *fault*: `IOException`. Secure variant = authenticated+encrypted (FR-029).
  **Gotcha:** PSM is dynamic → the listener's PSM must be exchanged out-of-band (GATT
  characteristic or rendezvous link) before the connector calls CreateL2capChannel. C# uses
  the first-party `Android.Bluetooth` binding (no plugin-trust risk); Dart only has pre-1.0
  `blev`/`l2cap_ble`. **Windows blocked for BOTH** (WinRT has RFCOMM via StreamSocket but no
  app-level BLE L2CAP CoC; only kernel profile drivers reach it).

- **BLE LE-Audio BIS / CIS** — **NO seam to bind on either OS.** No application-visible
  isochronous byte channel. BIS = connectionless one-to-many broadcast (no per-reader ACK) →
  collides with FR-005 bilateral + SRSW/FR-018; keep as the FR-041 open co-design item, ship
  broadcast as N bilateral ground-copy links (FR-040). CIS's bilateral intent (FR-042) is best
  realized by **L2CAP CoC** (see that row), not a literal CIS transport.

---

## 5. Sources

Web group: pub.dev (web_socket_channel, http2, cronet_http, flutter_quic), dart-lang/sdk
#38595, learn.microsoft.com (WebSockets, QUIC overview, httpclient-http3, Kestrel/http3,
Kestrel), dotnet/runtime #1511/#29255, dotnet/maui #8952, api.flutter.dev WebSocketTransformer.

Messaging group: pub.dev (mqtt_client, mqtt5_client, dart_amqp, whixp, xmpp_stone, dds),
codeberg moxxmpp, nuget (MQTTnet, AMQPNetLite, XmppDotNet, Waher.Networking.XMPP,
Rti.ConnextDds), github (shamblett/mqtt_client, dotnet/MQTTnet, Azure/amqpnetlite,
agnauck/XmppDotNet, eclipse-cyclonedds #2167, eProsima/Fast-DDS),
rticommunity.github.io connector-cs.

Constrained/tunnel/file group: pub.dev (coap, dartssh2, ftpconnect, ftp_server), nuget
(CoAPnet, CoAPnet.Extensions.DTLS, Com.AugustCellars.CoAP, SSH.NET, FxSsh, FluentFTP), github
(shamblett/coap, chkr1011/CoAPnet, Com-AugustCellars/CoAP-CSharp, TerminalStudio/dartssh2,
sshnet/SSH.NET 2025.1.0, Aimeast/FxSsh, mikaelliljedahl/freesftpsharp, robinrodricks/FluentFTP).

Bluetooth group: pub.dev (flutter_blue_plus, bluetooth_low_energy, blev, l2cap_ble,
flutter_bluetooth_classic_serial, bluetooth_classic), nuget (InTheHand.Net.Bluetooth /
32feet.NET), learn.microsoft.com (gatt-server, GattServiceProvider, BluetoothLEDevice,
RFCOMM send/receive, StreamSocket(Listener), Android.Bluetooth Create/ListenUsingL2capChannel,
BluetoothLeAudio, BLE-audio driver, L2CAP client driver), developer.android.com
(BluetoothLeAudio, ble-audio overview), blogs.windows.com GATT-server announcement,
github inthehand/32feet wiki, en.wikipedia.org DDS.

*(Full per-leaf URL list retained in the group-finding inputs that seeded this synthesis.)*

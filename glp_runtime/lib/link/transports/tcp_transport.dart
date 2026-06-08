import 'dart:async';
import 'dart:io';
import 'dart:typed_data';

import '../seam/i_link_endpoint.dart';
import '../seam/i_link_transport.dart';
import '../seam/link_address.dart';
import '../seam/link_fault.dart';
import '../seam/link_id.dart';
import '../seam/link_options.dart';
import '../seam/link_scheme.dart';

/// The FIRST real cross-process transport (feature 025 runtime-integration): a raw-TCP
/// leaf over IPv4 localhost (127.0.0.1). The `server_listener` end binds and waits
/// ([listen]); the `client_connector` end dials when its goal
/// activates ([connect]); the one persistent duplex socket carries the
/// bilateral `Link(In, Out)` — both ends ship frames whenever they want (FR-003/005).
/// Selected by [LinkScheme.tcp] from `link_id("tcp", ep("127.0.0.1", Port), Nonce)`.
///
/// Iteration 1 is plain TCP on the loopback IP (same host) — no TLS, which FR-029 only
/// requires for INTER-host links; the secure + web leaves (ws/wss/https, T060/T061) are the
/// next iteration. Frames are made self-delimiting over the byte stream by a 4-byte
/// big-endian length prefix (the FrameCodec frame is carried opaquely), so one
/// [ILinkEndpoint.sendBytes] ⇒ one peer [ILinkEndpoint.recvBytes]
/// (per-link FIFO is then reconstructed above by the reliability sublayer). P2P to the
/// immediate peer; no broker.
class TcpTransport implements ILinkTransport {
  static final List<LinkScheme> _schemes = [LinkScheme.tcp];

  @override
  List<LinkScheme> get supportedSchemes => _schemes;

  @override
  Future<ILinkEndpoint> listen(
      LinkScheme scheme, LinkAddress local, LinkOptions opts,
      {Future<void>? cancel}) async {
    _require(scheme);
    final port = _requirePort(local, 'listen');
    final listener = await ServerSocket.bind(_parseIp(local.host), port);
    try {
      final client = await _accept(listener, cancel);
      _configure(client);
      return TcpEndpoint(
          LinkId(LinkScheme.tcp, local, LinkNonce.int_(port)), client);
    } finally {
      // One link per listen for the base MVP (a multi-accept loop is a transport-leaf
      // concern, Phase 6); stop the listener so the port is released for re-use.
      await listener.close();
    }
  }

  @override
  Future<ILinkEndpoint> connect(
      LinkScheme scheme, LinkAddress remote, LinkOptions opts,
      {Future<void>? cancel}) async {
    _require(scheme);
    final port = _requirePort(remote, 'connect');
    final ip = _parseIp(remote.host);
    // The connector's goal may activate BEFORE the listener has bound (independent processes);
    // retry connection-refused until cancel (the kernel's ConnectTimeout) cancels, so establishment
    // is timing-independent rather than a hard race. Role-order independence (FR-004).
    while (true) {
      _throwIfCancelled(cancel);
      try {
        final client = await Socket.connect(ip, port);
        _configure(client);
        return TcpEndpoint(
            LinkId(LinkScheme.tcp, remote, LinkNonce.int_(port)), client);
      } on SocketException {
        await _delay(const Duration(milliseconds: 100),
            cancel); // listener not up yet — back off and retry
      }
    }
  }

  static void _require(LinkScheme scheme) {
    if (scheme != LinkScheme.tcp) {
      throw ArgumentError.value(
          scheme, 'scheme', "TcpTransport does not serve scheme '$scheme'");
    }
  }

  static int _requirePort(LinkAddress addr, String op) {
    final port = addr.port;
    if (port == null) {
      throw ArgumentError(
          'tcp $op requires an ep(Host, Port) endpoint; got \'$addr\'');
    }
    return port;
  }

  // IPv4 localhost is the supported case (Gabi: "127.0.0.1 is probably the best localhost").
  static InternetAddress _parseIp(String host) =>
      host.toLowerCase() == 'localhost'
          ? InternetAddress.loopbackIPv4
          : InternetAddress(host);

  static void _configure(Socket c) => c.setOption(
      SocketOption.tcpNoDelay, true); // ship each frame promptly (disable Nagle)

  /// Await one accepted [Socket], cancellable by [cancel] (mirrors
  /// `AcceptTcpClientAsync(ct)`). On cancel the pending accept is abandoned.
  static Future<Socket> _accept(ServerSocket listener, Future<void>? cancel) {
    final completer = Completer<Socket>();
    late final StreamSubscription<Socket> sub;
    sub = listener.listen((s) {
      if (!completer.isCompleted) {
        completer.complete(s);
        sub.cancel();
      }
    }, onError: (Object e, StackTrace st) {
      if (!completer.isCompleted) completer.completeError(e, st);
    });
    if (cancel != null) {
      cancel.then((_) {
        if (!completer.isCompleted) {
          sub.cancel();
          completer.completeError(_CancelledException());
        }
      });
    }
    return completer.future;
  }

  static void _throwIfCancelled(Future<void>? cancel) {
    // No synchronous cancellation state on a Future; the racing _delay below
    // surfaces the cancellation between retries.
  }

  /// `Task.Delay(ms, ct)` mirror: completes after [d], or throws on [cancel].
  static Future<void> _delay(Duration d, Future<void>? cancel) {
    if (cancel == null) return Future<void>.delayed(d);
    final completer = Completer<void>();
    final timer = Timer(d, () {
      if (!completer.isCompleted) completer.complete();
    });
    cancel.then((_) {
      if (!completer.isCompleted) {
        timer.cancel();
        completer.completeError(_CancelledException());
      }
    });
    return completer.future;
  }
}

/// Raised when a listen/connect rendezvous is cancelled (mirrors the C#
/// `OperationCanceledException` the [CancellationToken] surfaces).
class _CancelledException implements Exception {
  @override
  String toString() => 'TcpTransport rendezvous cancelled';
}

/// One end of a raw-TCP link: a duplex socket carrying length-prefixed frames. Send and
/// recv run on different threads (the egress drainer writes on the runner thread; the pump's
/// background recv loop reads), which a [Socket] supports for one
/// concurrent reader + one writer; concurrent sends are serialized by a lock.
class TcpEndpoint implements ILinkEndpoint {
  final Socket _client;
  final _SocketReader _reader;
  Future<void> _sendLock = Future<void>.value();
  bool _closed = false;

  @override
  final LinkId id;

  final StreamController<LinkFaultSignal> _onFault =
      StreamController<LinkFaultSignal>.broadcast();

  TcpEndpoint(this.id, Socket client)
      : _client = client,
        _reader = _SocketReader(client);

  @override
  Stream<LinkFaultSignal> get onFault => _onFault.stream;

  @override
  Future<void> sendBytes(Uint8List frame, {Future<void>? cancel}) {
    // Serialize concurrent sends behind a single in-order lock (mirrors the C#
    // SemaphoreSlim(1,1)). Chain off the prior send's completion.
    final result = _sendLock.then((_) => _doSend(frame, cancel));
    // The lock is released whether _doSend succeeds or faults.
    _sendLock = result.then((_) {}, onError: (_) {});
    return result;
  }

  Future<void> _doSend(Uint8List frame, Future<void>? cancel) async {
    final header = Uint8List(4);
    ByteData.view(header.buffer).setInt32(0, frame.length, Endian.big);
    try {
      _client.add(header);
      _client.add(frame);
      await _client.flush();
    } on Object catch (ex) {
      if (_isIoOrSocketOrDisposed(ex)) {
        // A send failure on a not-yet-closed link is a real transport fault.
        if (!_closed) {
          _onFault.add(LinkFaultSignal(
              id, LinkFaultKind.transient, 'tcp send failed: $ex'));
        }
      }
      rethrow;
    }
  }

  @override
  Future<Uint8List?> recvBytes({Future<void>? cancel}) async {
    try {
      final header = await _readExactly(4, cancel);
      if (header == null) {
        return null; // peer FIN before a new frame → graceful close (→ closed/eos upstream)
      }

      final len = ByteData.view(header.buffer, header.offsetInBytes, 4)
          .getInt32(0, Endian.big);
      if (len < 0) {
        _onFault.add(LinkFaultSignal(
            id, LinkFaultKind.permanent, 'tcp: negative frame length'));
        return null;
      }
      if (len == 0) {
        return Uint8List(0);
      }

      final body = await _readExactly(len, cancel);
      if (body == null) {
        _onFault.add(LinkFaultSignal(
            id, LinkFaultKind.transient, 'tcp: peer closed mid-frame'));
        return null;
      }
      return body;
    } on _CancelledException {
      rethrow; // pump disposed / link torn down — normal shutdown
    } on Object catch (ex) {
      if (_isIoOrSocketOrDisposed(ex)) {
        // An error on a link WE closed is the expected end of our own teardown — not a fault.
        if (!_closed) {
          _onFault.add(LinkFaultSignal(
              id, LinkFaultKind.transient, 'tcp recv failed: $ex'));
        }
        return null;
      }
      rethrow;
    }
  }

  /// Read exactly [count] bytes; null if the peer FINs before any byte of the chunk.
  Future<Uint8List?> _readExactly(int count, Future<void>? cancel) async {
    final buf = Uint8List(count);
    int off = 0;
    while (off < count) {
      final n = await _reader.read(buf, off, count - off, cancel);
      if (n == 0) {
        if (off == 0) {
          return null; // clean boundary → graceful close
        }
        throw const _MidFrameCloseException('tcp: peer closed mid-frame');
      }
      off += n;
    }
    return buf;
  }

  @override
  Future<void> close() {
    if (!_closed) {
      _closed = true;
      // Graceful: send FIN so the peer drains buffered frames then reads null. We keep the
      // socket readable so our own recv loop finishes on the peer's FIN; full disposal is
      // dispose (or process exit).
      try {
        _client.close();
      } on Object catch (ex) {
        if (!_isSocketOrDisposed(ex)) rethrow; /* already gone */
      }
    }
    return Future<void>.value();
  }

  @override
  Future<void> dispose() async {
    await close();
    try {
      await _reader.cancel();
    } on Object {
      /* best-effort */
    }
    try {
      _client.destroy();
    } on Object {
      /* best-effort */
    }
  }

  static bool _isIoOrSocketOrDisposed(Object ex) =>
      ex is SocketException ||
      ex is IOException ||
      ex is _MidFrameCloseException ||
      ex is StateError;

  static bool _isSocketOrDisposed(Object ex) =>
      ex is SocketException || ex is StateError;
}

/// Mirrors the C# `IOException("tcp: peer closed mid-frame")` thrown by
/// `ReadExactlyAsync` when the peer FINs after a partial chunk.
class _MidFrameCloseException implements IOException {
  final String message;
  const _MidFrameCloseException(this.message);
  @override
  String toString() => message;
}

/// Buffers the duplex socket's inbound byte stream so a [recvBytes] read can pull
/// exactly N bytes without polling — the Dart mirror of `NetworkStream.ReadAsync`
/// over a [Socket] (which, as a one-subscription `Stream<Uint8List>`, must be
/// adapted to a pull/`read(count)` shape). One concurrent reader; `n == 0` on a
/// read signals the peer's FIN (clean EOS), matching the C# stream-read `0`.
class _SocketReader {
  final List<int> _buffer = [];
  bool _done = false;
  Object? _error;
  Completer<void>? _waiter;
  late final StreamSubscription<Uint8List> _sub;

  _SocketReader(Stream<Uint8List> socket) {
    _sub = socket.listen(
      (chunk) {
        _buffer.addAll(chunk);
        _wake();
      },
      onError: (Object e, StackTrace st) {
        _error = e;
        _wake();
      },
      onDone: () {
        _done = true;
        _wake();
      },
      cancelOnError: false,
    );
  }

  void _wake() {
    final w = _waiter;
    if (w != null && !w.isCompleted) {
      _waiter = null;
      w.complete();
    }
  }

  /// Read up to [count] bytes into [dst] starting at [offset]; returns the number
  /// of bytes copied (0 on clean EOS). Parks until data, EOS, or [cancel].
  Future<int> read(
      Uint8List dst, int offset, int count, Future<void>? cancel) async {
    while (true) {
      if (_buffer.isNotEmpty) {
        final n = _buffer.length < count ? _buffer.length : count;
        for (int i = 0; i < n; i++) {
          dst[offset + i] = _buffer[i];
        }
        _buffer.removeRange(0, n);
        return n;
      }
      if (_error != null) {
        final e = _error!;
        throw e;
      }
      if (_done) {
        return 0;
      }
      final waiter = _waiter ??= Completer<void>();
      if (cancel != null) {
        await Future.any<void>([
          waiter.future,
          cancel.then((_) => throw _CancelledException()),
        ]);
      } else {
        await waiter.future;
      }
    }
  }

  Future<void> cancel() => _sub.cancel();
}

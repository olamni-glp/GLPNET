import 'dart:async';
import 'dart:typed_data';

import '../seam/i_link_endpoint.dart';
import '../seam/i_link_transport.dart';
import '../seam/link_address.dart';
import '../seam/link_fault.dart';
import '../seam/link_id.dart';
import '../seam/link_options.dart';
import '../seam/link_scheme.dart';

/// Deterministic in-process loopback transport (T026), a concrete
/// [ILinkTransport] for [LinkScheme.loopback]. A
/// [listen] and a [connect] on the same channel
/// name (the [LinkAddress.host]) rendezvous into one bilateral link;
/// the two ends share an equivalent [LinkId] (FR-002) and exchange
/// frames over two ordered in-memory channels — FIFO/in-order/exactly-once by
/// default (FR-018/020). It is the substrate for the headline-split and
/// integration tests (Phase 4/5); a T052 fault decorator wraps the produced
/// endpoints to inject drop/reorder/duplicate/delay deterministically (seeded).
///
/// Establishment role is irrelevant to loopback pairing (FR-004): whichever side
/// arrives first parks until the other appears, so listener-first and
/// connector-first both succeed. One transport instance is the rendezvous hub for
/// all its links; share a single instance between the two ends in a test.
class LoopbackTransport implements ILinkTransport {
  static final List<LinkScheme> _schemes = [LinkScheme.loopback];

  final Map<String, _PendingSide> _pending = {};
  int _nonce = 0;

  @override
  List<LinkScheme> get supportedSchemes => _schemes;

  @override
  Future<ILinkEndpoint> listen(
          LinkScheme scheme, LinkAddress local, LinkOptions opts,
          {Future<void>? cancel}) =>
      _rendezvous(scheme, local, cancel);

  @override
  Future<ILinkEndpoint> connect(
          LinkScheme scheme, LinkAddress remote, LinkOptions opts,
          {Future<void>? cancel}) =>
      _rendezvous(scheme, remote, cancel);

  Future<ILinkEndpoint> _rendezvous(
      LinkScheme scheme, LinkAddress addr, Future<void>? cancel) {
    if (scheme != LinkScheme.loopback) {
      throw ArgumentError.value(scheme, 'scheme',
          "LoopbackTransport does not serve scheme '$scheme'");
    }

    final channel = addr.host;

    final other = _pending.remove(channel);
    if (other != null) {
      // Second arrival: pair the two ends over two ordered channels.
      final a2b = _Channel<Uint8List>();
      final b2a = _Channel<Uint8List>();
      final id = LinkId(LinkScheme.loopback, addr, LinkNonce.int_(++_nonce));

      final firstEnd = LoopbackEndpoint._(id, a2b.writer, b2a.reader);
      final secondEnd = LoopbackEndpoint._(id, b2a.writer, a2b.reader);

      other.completer.complete(firstEnd);
      return Future<ILinkEndpoint>.value(secondEnd);
    }

    // First arrival: park until the peer shows up.
    final completer = Completer<ILinkEndpoint>();
    final side = _PendingSide(completer, addr);
    _pending[channel] = side;

    if (cancel != null) {
      cancel.then((_) {
        final p = _pending[channel];
        if (p != null && identical(p, side)) {
          _pending.remove(channel);
        }
        if (!completer.isCompleted) {
          completer.completeError(_CancelledException());
        }
      });
    }

    return completer.future;
  }

  /// Channels currently awaiting a peer (no rendezvous yet).
  int get pendingCount => _pending.length;
}

class _PendingSide {
  final Completer<ILinkEndpoint> completer;
  final LinkAddress addr;
  _PendingSide(this.completer, this.addr);
}

/// Raised when a parked rendezvous is cancelled (mirrors the C#
/// `OperationCanceledException` the [CancellationToken] registration throws).
class _CancelledException implements Exception {
  @override
  String toString() => 'LoopbackTransport rendezvous cancelled';
}

/// One end of an in-process loopback link (T026). Writes go to the peer's inbound
/// channel; reads drain this end's inbound channel. FIFO, in-order, exactly-once by
/// construction (a [_Channel] preserves order and never drops or
/// duplicates), so it is the deterministic substrate for hermetic tests. Fault
/// behaviour (drop/reorder/duplicate/delay) is added by a T052 decorator wrapping
/// this [ILinkEndpoint] — the loopback itself never deviates.
class LoopbackEndpoint implements ILinkEndpoint {
  final _ChannelWriter<Uint8List> _toPeer;
  final _ChannelReader<Uint8List> _fromPeer;

  @override
  final LinkId id;

  final StreamController<LinkFaultSignal> _onFault =
      StreamController<LinkFaultSignal>.broadcast();

  LoopbackEndpoint._(this.id, this._toPeer, this._fromPeer);

  @override
  Stream<LinkFaultSignal> get onFault => _onFault.stream;

  @override
  Future<void> sendBytes(Uint8List frame, {Future<void>? cancel}) async {
    // Copy: the caller may reuse its buffer once the await returns.
    final copy = Uint8List.fromList(frame);
    try {
      await _toPeer.write(copy, cancel: cancel);
    } on _ChannelClosedException {
      // Peer closed its receive side. Report as a clean close, not a Fail.
      _onFault.add(LinkFaultSignal(id, LinkFaultKind.closed, 'peer closed'));
      rethrow;
    }
  }

  @override
  Future<Uint8List?> recvBytes({Future<void>? cancel}) async {
    try {
      return await _fromPeer.read(cancel: cancel);
    } on _ChannelClosedException {
      // Peer completed the stream: graceful end-of-stream → null (closed/eos upstream).
      return null;
    }
  }

  @override
  Future<void> close() {
    // Complete the channel the peer reads from: it drains buffered frames, then
    // its recvBytes returns null (graceful close).
    _toPeer.tryComplete();
    return Future<void>.value();
  }

  @override
  Future<void> dispose() async => await close();
}

/// An ordered, single-reader / single-writer in-memory channel — the Dart mirror
/// of the unbounded `System.Threading.Channels.Channel<byte[]>` the C# loopback
/// uses (FIFO, never drops or duplicates). Reads park until an item is written or
/// the writer completes; once completed, a buffered read drains the remaining
/// items, then further reads throw [_ChannelClosedException] (the C# completed
/// channel surfaces `ChannelClosedException` to the reader after the buffer
/// empties — the endpoint maps that to graceful `null`).
class _Channel<T> {
  final List<T> _buffer = [];
  Completer<void>? _waiter;
  bool _completed = false;

  late final _ChannelWriter<T> writer = _ChannelWriter<T>(this);
  late final _ChannelReader<T> reader = _ChannelReader<T>(this);

  void _write(T item) {
    if (_completed) {
      // Writing to a completed channel: surface the same closed signal the C#
      // ChannelWriter.WriteAsync raises after TryComplete.
      throw _ChannelClosedException();
    }
    _buffer.add(item);
    final w = _waiter;
    if (w != null && !w.isCompleted) {
      _waiter = null;
      w.complete();
    }
  }

  bool _tryComplete() {
    if (_completed) return false;
    _completed = true;
    final w = _waiter;
    if (w != null && !w.isCompleted) {
      _waiter = null;
      w.complete();
    }
    return true;
  }

  Future<T> _read(Future<void>? cancel) async {
    while (true) {
      if (_buffer.isNotEmpty) {
        return _buffer.removeAt(0);
      }
      if (_completed) {
        // Buffer drained and writer completed → channel closed.
        throw _ChannelClosedException();
      }
      final waiter = _waiter ??= Completer<void>();
      if (cancel != null) {
        await Future.any<void>([waiter.future, cancel]);
      } else {
        await waiter.future;
      }
    }
  }
}

class _ChannelWriter<T> {
  final _Channel<T> _channel;
  _ChannelWriter(this._channel);

  Future<void> write(T item, {Future<void>? cancel}) async {
    _channel._write(item);
  }

  bool tryComplete() => _channel._tryComplete();
}

class _ChannelReader<T> {
  final _Channel<T> _channel;
  _ChannelReader(this._channel);

  Future<T> read({Future<void>? cancel}) => _channel._read(cancel);
}

/// Mirrors `System.Threading.Channels.ChannelClosedException`: raised to a writer
/// that writes after completion, and to a reader once a completed channel's buffer
/// is drained.
class _ChannelClosedException implements Exception {
  @override
  String toString() => 'channel closed';
}

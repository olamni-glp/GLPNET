import '../../multiagent/payload_serializer.dart';
import '../../runtime/heap_fcp.dart';
import '../../runtime/terms.dart';
import '../reliability/frame_codec.dart';
import 'link_handle.dart';

/// The one ground-relay egress routine shared by BOTH sender faces (feature 025,
/// T031): the channel face (`link_send/3` → the `Out`-stream egress
/// drainer armed by `'_link_setup'`) and the LinkId face (`out_relay/3` →
/// the `'_link_send'/3` kernel). Keeping a single ship path closes risk R-5
/// (two egress paths diverging): both serialize a GROUND copy (FR-010), frame +
/// sequence it, and write every fragment to the link endpoint in send order.
class LinkEgress {
  LinkEgress._();

  /// Resolve [msg] to a VarRef-free ground tree, serialize it,
  /// frame + sequence it, and ship every fragment over [handle]'s
  /// endpoint (FR-010 ground-relay, per-link FIFO FR-018/053). Throws
  /// [StateError] if the term is not ground (an unbound
  /// reader/writer reached the wire) — the ground gate. Callers decide what a gate
  /// violation means: a dropped frame for the stream drainer, a caller-bug Abort
  /// for the kernel (the GLP `ground(Msg?)` guard should have excluded it).
  static void shipGround(HeapFCP heap, LinkHandle handle, Term msg) {
    // Deep-resolve against the heap so a ground STRUCT (whose args may be VarRefs
    // into bound cells) serializes — serializeAgentMessage throws on ANY VarRef,
    // so the term handed to it must be VarRef-free. Resolution doubles as the
    // ground gate: an unbound cell at any depth throws here, never on the wire.
    final Term ground = _resolveGround(heap, msg);

    // Empty origin: the per-link endpoint identity, not a global-name stamp, keys
    // delivery on the base relay (consistent with the T030 Out-stream drainer).
    final List<int> payload = PayloadSerializer('').serializeAgentMessage(ground);

    final int seq = handle.sequencer.next();
    // NOTE: SendWindow backpressure (FR-025) is intentionally NOT gated here yet —
    // credit release rides the inbound ack path, which lands with the monitor
    // primitive (T034). Acquiring a credit with no Release would freeze the runner
    // thread at the window bound. Framing + monotone sequencing are real today.
    //
    // Endpoint readiness gate (async establish): the transport endpoint is DEFERRED
    // until the async connect/listen rendezvous resolves, so a frame may be ready to
    // ship before the link has connected. Chain every send on handle.ready, and chain
    // that off the per-handle egress tail so frames serialize in SUBMISSION order even
    // across the connect gate (FR-018/053 per-link FIFO). The framing + sequencing
    // above already happened synchronously on the runner thread, so monotone sequence
    // order matches submission order; the tail preserves it through to the wire.
    for (final frame
        in FrameCodec.encode(payload, seq, handle.options.maxFrameBytes)) {
      handle.egressTail = handle.egressTail.then(
          (_) => handle.ready.then((ep) => ep.sendBytes(frame)),
          // A failed connect (ready rejects) must not poison the tail; the fault is
          // surfaced on the monitor by the establish path. Swallow so later chains run.
          onError: (Object _) {});
    }
  }

  /// Dereference [t] recursively into a VarRef-free tree of
  /// [ConstTerm]/[StructTerm]. An unbound cell at any depth
  /// is the ground-relay gate violation: throw rather than place a placeholder on
  /// the wire (FR-010 — no `_w`/`_r`/embedded reader ever crosses).
  static Term _resolveGround(HeapFCP heap, Term t) {
    final Term d = heap.dereference(t);
    if (d is ConstTerm) {
      return d;
    } else if (d is StructTerm) {
      final args = List<Term>.filled(d.args.length, ConstTerm('nil'));
      for (int i = 0; i < d.args.length; i++) {
        args[i] = _resolveGround(heap, d.args[i]);
      }
      return StructTerm(d.functor, args);
    } else if (d is VarRef) {
      throw StateError(
          'non-ground term reached egress: unbound cell ${d.addr} (ground-relay gate)');
    } else {
      throw StateError('cannot ground-resolve term ${d.runtimeType}');
    }
  }
}

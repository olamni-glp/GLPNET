import '../../runtime/body_kernels.dart' show BodyKernelResult;
import '../../runtime/runtime.dart';
import '../../runtime/terms.dart';
import '../seam/i_link_endpoint.dart';
import '../seam/link_id.dart';
import '../seam/link_options.dart';
import 'link_egress.dart';
import 'link_faults.dart';
import 'link_handle.dart';
import 'link_runtime.dart';
import 'link_teardown.dart';
import 'link_terms.dart';

/// The ONE canonical establish-and-wire core every establishment path converges on
/// (feature 025, T033): listen/connect (`'_link_setup'`), and the path-B
/// handshake (`'_link_request'` connector + `'_link_accept'` adopting a
/// pending connection). Centralizing it is what makes the two FR-002 paths yield an
/// INDISTINGUISHABLE established link and closes risk R-5 ("two paths, one registry"):
/// given a ground [LinkId], an endpoint factory, and the three heap stream
/// holes, it registers the handle idempotently (FR-007), wires the In/Out/Faults
/// cursors, arms the Out egress drainer, starts the ingress pump, and installs it.
class LinkEstablish {
  LinkEstablish._();

  static const String _listCons = '.';
  static const String _nil = 'nil';

  /// Establish (or idempotently reuse) the link for [id] and wire it
  /// to the three heap holes. [establish] runs only on first
  /// establishment (it opens/adopts the transport endpoint). Returns
  /// [BodyKernelResult.success] once wired, or
  /// [BodyKernelResult.abort] on a malformed hole / transport failure /
  /// FR-007 re-establishment (cell-aliasing unspecified — surfaced, not guessed).
  static BodyKernelResult wireEstablishedLink(
      GlpRuntime rt,
      LinkRuntime link,
      LinkId id,
      ILinkEndpoint Function() establish,
      Object? inArg,
      Object? outArg,
      Object? faultsArg,
      String who) {
    final heap = rt.heap;

    // Resolve the three output cells to their RAW heap addresses. Use the bare arg
    // VarRef address, NOT heap.dereference: a local reader dereferences to its paired
    // WRITER (derefAddr canonicalizes reader→writer), erasing the Out? reader identity.
    if (inArg is! VarRef || !heap.isWriter(inArg.addr)) {
      return abort(who, 'In must be an unbound writer cell');
    }
    final inVr = inArg;
    if (outArg is! VarRef || !heap.isReader(outArg.addr)) {
      return abort(who, 'Out? must be an unbound reader cell');
    }
    final outVr = outArg;
    if (faultsArg is! VarRef || !heap.isWriter(faultsArg.addr)) {
      return abort(who, 'Faults must be an unbound writer cell');
    }
    final faultsVr = faultsArg;

    // Idempotency at link-identity (FR-007): re-establishment of the same ground
    // LinkId reuses the handle rather than opening a duplicate.
    final bool firstEstablishment = !link.links.contains(id);
    LinkHandle handle;
    try {
      handle = link.links.getOrEstablish(
          id, () => LinkHandle(id, establish(), LinkOptions.default_));
    } catch (ex) {
      return abort(who, 'transport establishment failed for $id: ${_unwrap(ex)}');
    }

    if (!firstEstablishment) {
      return abort(who,
          're-establishment of already-established $id: cell-aliasing unspecified (FR-007) — first only');
    }

    // Wire the heap stream cursors onto the handle.
    handle.inWriterAddr = inVr.addr;
    handle.outReaderAddr = outVr.addr;
    handle.faultsWriterAddr = faultsVr.addr;

    // Register the establishment `Faults` stream as a live monitor cursor (T034): a
    // transport fault fans out to it as well as to any later `link_monitor` stream
    // (FR-008). No `ok` is pushed here — the cell stays lazily unbound until a real
    // fault, so an unmonitored goal stays safely suspended (FR-044).
    LinkFaults.register(handle, faultsVr.addr);

    // Distributed-GC hook (T024/T035, FR-024): on close/permFail the reclaimer removes
    // this link's registry entry so the runtime returns to baseline. Other subsystems
    // (send-registry goals, reply-table) register their own hooks here when they gain live
    // per-link state; the base ground-relay path has only the registry entry today.
    link.reclaimer.register(id, () => link.links.remove(id));

    // Egress: observe binds on the writer the program fills as `Out` (egress rides the
    // ordinary drain, no pump needed). Ingress: start the background receive loop.
    final int? outWriterAddr = heap.tryWriterForReader(outVr.addr);
    if (outWriterAddr == null) {
      return abort(who, 'Out reader has no paired writer to drain');
    }
    armEgress(rt, link, handle, outWriterAddr);
    link.pump.addLink(handle);
    rt.inboundPump ??= link.pump;

    return BodyKernelResult.success;
  }

  /// Arm the egress drainer: when the program binds the `Out` writer to a stream
  /// cons, ship the ground head (shared [LinkEgress.shipGround]) and re-arm
  /// on the tail; an `Out = []` bind is the graceful stream-end close (FR-010).
  static void armEgress(
      GlpRuntime rt, LinkRuntime link, LinkHandle handle, int outWriterAddr) {
    rt.heap.onBind(outWriterAddr, (bound) => _onOutboundBind(rt, link, handle, bound));
  }

  static void _onOutboundBind(
      GlpRuntime rt, LinkRuntime link, LinkHandle handle, Term bound) {
    final heap = rt.heap;
    final value = heap.dereference(bound);

    // `Out = []` → graceful stream-end close (the program closed its sender). Runs the
    // SAME teardown as the abrupt '_link_close' kernel (T035) with reason `eos`: emit the
    // terminal closed(LinkId, eos) on the monitor, end it, close transport, run GC (FR-024).
    if (value is ConstTerm && value.value == _nil) {
      LinkTeardown.teardown(rt, link, handle, LinkTerms.gracefulReason);
      return;
    }

    if (value is! StructTerm ||
        value.functor != _listCons ||
        value.args.length != 2) {
      return; // not a stream cons — nothing to ship (defensive; the wrapper only conses)
    }
    final cons = value;

    // Ground-relay ship (FR-010), shared with the '_link_send'/3 kernel face. If a
    // non-ground head slips past the GLP `ground(Msg?)` guard, shipGround throws and
    // the channel face drops the frame rather than placing a partial term on the wire.
    try {
      LinkEgress.shipGround(heap, handle, cons.args[0]);
    } on StateError {
      print(
          '[link egress] non-ground term reached Out — ground-relay gate violated; dropped');
      return;
    }

    // Re-arm on the tail's writer for the next value.
    final tail = heap.dereference(cons.args[1]);
    if (tail is VarRef) {
      final int? tailWriter = heap.isReader(tail.addr)
          ? heap.tryWriterForReader(tail.addr)
          : (heap.isWriter(tail.addr) ? tail.addr : null);
      if (tailWriter != null) {
        armEgress(rt, link, handle, tailWriter);
      }
    }
  }

  static String _unwrap(Object ex) => ex.toString();

  static BodyKernelResult abort(String who, String why) {
    print('[ABORT] $who: $why');
    return BodyKernelResult.abort;
  }
}

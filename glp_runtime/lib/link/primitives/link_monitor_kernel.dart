import '../../runtime/body_kernels.dart' show BodyKernelResult;
import '../../runtime/runtime.dart';
import '../../runtime/terms.dart';
import '../seam/link_id.dart';
import 'link_establish.dart';
import 'link_faults.dart';
import 'link_runtime.dart';
import 'link_terms.dart';

/// The `'_link_monitor'/2` system-predicate (feature 025, T034) — the host body of the
/// GLP wrapper
/// `link_monitor(LinkId, Faults?) :- ground(LinkId?) | '_link_monitor'(LinkId?, Faults).`
/// Given a ground, already-established [LinkId], it hands back a per-link fault MONITOR
/// STREAM on which faults surface as ordinary bound ground terms (`ok` / `closed` /
/// `tempFail` / `permFail`) read with existing guards (FR-008/043-046). The stream is
/// independent of the data path and of any other monitor stream for the same link
/// (FR-008): it shares only the ground [LinkId] (a registry lookup), so there is no
/// cell aliasing and no SRSW interference.
///
/// Arguments (ground-guarded by the wrapper): `args[0]` LinkId, `args[1]` the `Faults`
/// writer the host extends (the program reads the paired reader hole). The link MUST
/// already be established — monitoring an unestablished link is a caller bug surfaced
/// as an abort, not tolerated. On registration the kernel pushes one `ok` term: the
/// lattice's healthy baseline (contracts/link-primitives.md §2.7), so a watcher's
/// `[ok|Rest]` clause is immediately reducible and the link is observably healthy until
/// a fault.
class LinkMonitorKernel {
  LinkMonitorKernel._();

  static const String _who = "'_link_monitor'/2";

  static BodyKernelResult linkMonitor(
      GlpRuntime rt, List<Object?> args, LinkRuntime link) {
    if (args.length != 2) {
      return LinkEstablish.abort(_who, 'expected 2 arguments, got ${args.length}');
    }

    final heap = rt.heap;
    LinkId id;
    try {
      id = LinkTerms.parseLinkId(LinkTerms.groundResolve(heap, args[0]! as Term));
    } on ArgumentError catch (ex) {
      return LinkEstablish.abort(_who, ex.message.toString());
    }

    // The Faults output hole: a body writer the host extends (program reads the paired
    // reader). Use the raw VarRef addr, not dereference (a reader derefs to its writer)
    // — same discipline as LinkEstablish's three holes.
    final faultsArg = args[1];
    if (faultsArg is! VarRef || !heap.isWriter(faultsArg.addr)) {
      return LinkEstablish.abort(_who, 'Faults must be an unbound writer cell');
    }

    // The link must already be established (FR-007 registry): monitor observes an
    // existing link, it does not create one.
    final handle = link.links.tryGet(id);
    if (handle == null) {
      return LinkEstablish.abort(_who, 'monitor of unestablished link $id');
    }

    // Register the independent observer cursor, then push the healthy-baseline `ok`.
    final int index = handle.monitorCursors.length;
    LinkFaults.register(handle, faultsArg.addr);
    LinkFaults.extend(heap, rt, handle.monitorCursors, index, LinkTerms.ok());

    return BodyKernelResult.success;
  }
}

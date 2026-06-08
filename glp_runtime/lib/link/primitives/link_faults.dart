import '../../runtime/heap_fcp.dart';
import '../../runtime/runtime.dart';
import '../../runtime/terms.dart';
import 'link_handle.dart';

/// The per-link fault-monitor delivery core (feature 025, T034). Faults surface as
/// ORDINARY BOUND GROUND TERMS on a per-link monitor stream over the lattice
/// `ok` / `closed(LinkId,Reason)` / `tempFail(LinkId,Reason)` /
/// `permFail(LinkId,Reason)` — read with existing guards, NEVER a fourth
/// unification verdict and NEVER a new guard outcome (FR-043). A disconnect is
/// delivered here as a bound term, so it NEVER maps to a logical Fail (FR-044); a
/// goal that does not read the stream simply stays suspended on its unbound head.
///
/// Delivery is a plain stream-tail bind (the same idiom as the inbound `In`
/// stream and `mad_context`'s serializer-assignment): mint a fresh
/// (writer, reader) pair, cons `[term | freshReader]`, bind the current writer,
/// enqueue any reactivated goals, and advance the cursor to the fresh writer. This is
/// why a fault cannot become a fourth verdict — it is a normal bind. All mutation runs
/// on the runner thread: the kernel ([LinkMonitorKernel]) registers a
/// cursor and the pump ([LinkPump]) fans an asynchronous fault out to
/// every registered cursor.
class LinkFaults {
  LinkFaults._();

  static const String _listCons = '.';
  static const String _nil = 'nil';

  /// Register [faultWriterAddr] as a live monitor cursor for
  /// [handle]. Used at establishment to track the `Faults`
  /// stream and by `'_link_monitor'` to add an independent observer (FR-008).
  static void register(LinkHandle handle, int faultWriterAddr) =>
      handle.monitorCursors.add(faultWriterAddr);

  /// Extend ONE monitor cursor (the entry at [index] in
  /// [cursors]) by one ground fault term, advancing it to the fresh
  /// writer. Reactivated goals (a reader suspended on this stream's head) are enqueued.
  static void extend(HeapFCP heap, GlpRuntime engine, List<int> cursors,
      int index, Term term) {
    final (freshWriter, freshReader) = heap.allocateVariable();
    final cons = StructTerm(_listCons, <Term>[term, VarRef(freshReader)]);
    for (final act in heap.bindVariable(cursors[index], cons)) {
      engine.gq.enqueue(act);
    }
    cursors[index] = freshWriter;
  }

  /// Fan a fault term out to EVERY monitor cursor of [handle] — the
  /// establishment `Faults` stream and all `link_monitor` streams — so each
  /// independent observer sees it on its own stream (FR-008). Runner thread only.
  static void deliverFault(
      HeapFCP heap, GlpRuntime engine, LinkHandle handle, Term term) {
    for (int i = 0; i < handle.monitorCursors.length; i++) {
      extend(heap, engine, handle.monitorCursors, i, term);
    }
  }

  /// End EVERY monitor stream of [handle] with `[]` (nil): bind each
  /// live cursor to the empty list so a watcher sees end-of-stream and reduces its `[]`
  /// clause. Used after a terminal `closed(LinkId,Reason)` on close (T035): a close is
  /// terminal, so the monitor stream is closed too. Runner thread only.
  static void endAll(HeapFCP heap, GlpRuntime engine, LinkHandle handle) {
    for (final cursor in handle.monitorCursors) {
      for (final act in heap.bindVariable(cursor, ConstTerm(_nil))) {
        engine.gq.enqueue(act);
      }
    }
  }
}

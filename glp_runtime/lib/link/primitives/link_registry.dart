import '../seam/link_id.dart';
import 'link_handle.dart';

/// The per-instance LinkId→[LinkHandle] registry that makes
/// `'_link_setup'` idempotent at link-identity (FR-007): re-invoking setup
/// with the same ground [LinkId] returns the already-established handle
/// rather than opening a conflicting duplicate. Keyed by the never-reused ground
/// [LinkId] (value equality). One registry per instance; not
/// thread-safe (the runner is single-threaded).
class LinkRegistry {
  final Map<LinkId, LinkHandle> _handles = <LinkId, LinkHandle>{};

  /// Look up an established link by identity. Returns `null` when none exists.
  LinkHandle? tryGet(LinkId id) => _handles[id];

  /// Return the existing handle for [id] (idempotent reuse,
  /// FR-007), or create + store one via [establish] on first
  /// setup. [establish] runs only when no handle exists yet.
  LinkHandle getOrEstablish(LinkId id, LinkHandle Function() establish) {
    final existing = _handles[id];
    if (existing != null) {
      return existing;
    }
    final handle = establish();
    if (handle.id != id) {
      throw StateError('established handle id ${handle.id} != requested $id');
    }
    _handles[id] = handle;
    return handle;
  }

  /// Store a freshly-built (endpoint-deferred) handle under its identity. Used by
  /// the async establish path, which builds the handle and wires the heap cursors
  /// SYNCHRONOUSLY (the FR-007 first-establishment check having already run) before
  /// the background connect resolves. Asserts the id matches and that no handle was
  /// already registered (idempotency is enforced by the caller's `contains` check).
  void put(LinkId id, LinkHandle handle) {
    if (handle.id != id) {
      throw StateError('established handle id ${handle.id} != requested $id');
    }
    if (_handles.containsKey(id)) {
      throw StateError('put over an already-established $id (idempotency violated)');
    }
    _handles[id] = handle;
  }

  /// True if this exact handle was newly stored by the last [getOrEstablish] call for an id.
  bool contains(LinkId id) => _handles.containsKey(id);

  /// Remove a link's handle (distributed GC on permFail/close, T024).
  bool remove(LinkId id) => _handles.remove(id) != null;

  /// Number of established links.
  int get count => _handles.length;

  /// Every established handle (clean-shutdown iteration — dispose each endpoint's
  /// transport socket). Order-agnostic; the caller does not mutate the registry mid-walk.
  Iterable<LinkHandle> get handles => _handles.values;
}

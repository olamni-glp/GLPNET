/// A cyclic term was encountered while serializing for the wire. FR-022/FR-028:
/// a cyclic term must terminate serialization with a CLEAN error rather than loop
/// forever or overflow the stack. Surfaced as a transport fault, never a GLP Fail.
class CyclicTermException implements Exception {
  final String message;
  CyclicTermException(this.message);

  @override
  String toString() => 'CyclicTermException: $message';
}

/// Reference-identity visited-set used by the link send path to guard term-graph
/// traversal against cycles before handing a payload to the serializer (FR-022).
/// Mirrors the runner's existing cycle-safe `_termsEqual` visited-set
/// (runner.dart:4699-4700) so the link layer terminates on the same inputs the
/// core already treats as cyclic. Decoupled from the `Term` model (operates
/// on object references) so it stays trivially mirrorable to Dart and unit-testable
/// on its own; the T031 send walker supplies the nodes.
///
/// Use the scoped pattern: `final s = guard.enter(node); try { /* recurse */ }
/// finally { s.dispose(); }`. The node is removed from the active set when the
/// scope disposes, so sharing a subterm via two different parents (a DAG, not a
/// cycle) is permitted — only a node currently on the active recursion path
/// triggers the error.
class CycleGuard {
  // Reference-identity set: keyed on object identity, not value equality.
  final Set<Object> _active = Set<Object>.identity();

  /// Enter [node] on the active recursion path. Throws
  /// [CyclicTermException] if it is already on the path (a cycle).
  /// Dispose the returned scope when leaving the node.
  CycleGuardScope enter(Object node) {
    if (!_active.add(node)) {
      throw CyclicTermException(
          'cyclic term detected at ${node.runtimeType} during serialization');
    }
    return CycleGuardScope._(this, node);
  }

  /// Number of nodes currently on the active recursion path.
  int get depth => _active.length;
}

/// RAII scope that removes a node from the active path on dispose.
class CycleGuardScope {
  final CycleGuard _guard;
  final Object _node;
  CycleGuardScope._(this._guard, this._node);
  void dispose() => _guard._active.remove(_node);
}

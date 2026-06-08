namespace GlpRuntime.Link.Reliability;

/// <summary>
/// A cyclic term was encountered while serializing for the wire. FR-022/FR-028:
/// a cyclic term must terminate serialization with a CLEAN error rather than loop
/// forever or overflow the stack. Surfaced as a transport fault, never a GLP Fail.
/// </summary>
public sealed class CyclicTermException : Exception
{
    public CyclicTermException(string message) : base(message) { }
}

/// <summary>
/// Reference-identity visited-set used by the link send path to guard term-graph
/// traversal against cycles before handing a payload to the serializer (FR-022).
/// Mirrors the runner's existing cycle-safe <c>_termsEqual</c> visited-set
/// (runner.dart:4699-4700) so the link layer terminates on the same inputs the
/// core already treats as cyclic. Decoupled from the <c>Term</c> model (operates
/// on object references) so it stays trivially mirrorable to Dart and unit-testable
/// on its own; the T031 send walker supplies the nodes.
/// </summary>
/// <remarks>
/// Use the scoped pattern: <c>using (guard.Enter(node)) { /* recurse */ }</c>. The
/// node is removed from the active set when the scope disposes, so sharing a
/// subterm via two different parents (a DAG, not a cycle) is permitted — only a
/// node currently on the active recursion path triggers the error.
/// </remarks>
public sealed class CycleGuard
{
    private readonly HashSet<object> _active = new(ReferenceEqualityComparer.Instance);

    /// <summary>
    /// Enter <paramref name="node"/> on the active recursion path. Throws
    /// <see cref="CyclicTermException"/> if it is already on the path (a cycle).
    /// Dispose the returned scope when leaving the node.
    /// </summary>
    public Scope Enter(object node)
    {
        if (!_active.Add(node))
            throw new CyclicTermException($"cyclic term detected at {node.GetType().Name} during serialization");
        return new Scope(this, node);
    }

    /// <summary>Number of nodes currently on the active recursion path.</summary>
    public int Depth => _active.Count;

    /// <summary>RAII scope that removes a node from the active path on dispose.</summary>
    public readonly struct Scope : IDisposable
    {
        private readonly CycleGuard _guard;
        private readonly object _node;
        internal Scope(CycleGuard guard, object node) { _guard = guard; _node = node; }
        public void Dispose() => _guard._active.Remove(_node);
    }
}

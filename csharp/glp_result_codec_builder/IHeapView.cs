// Engine-abstraction seam (owner decision B) — feature 038.
//
// The minimal, read-only heap view the envelope builder needs for server-side
// deep-resolve. Depending on this interface (not the concrete HeapFCP) keeps the
// builder unit-testable against a fake heap without constructing a live engine.

using Rt = GlpRuntime.Runtime;

namespace GlpRuntime.ResultCodec.Builder;

/// <summary>
/// The read-only heap operations deep-resolve requires: shallow dereference of a
/// runtime term and a boundness probe for a writer id. Implemented over the live
/// engine by <see cref="HeapFcpView"/>, or by a fake in tests.
/// </summary>
public interface IHeapView
{
    /// <summary>Shallow-dereference a runtime term (a bound var → its value; unbound → itself).</summary>
    Rt.Term Dereference(Rt.Term term);

    /// <summary>True iff the writer at <paramref name="varId"/> is bound.</summary>
    bool IsBound(int varId);
}

// Live-engine adapter for IHeapView (owner decision B) — feature 038.
//
// The ONLY type coupled to the concrete engine heap. Consumers with a live
// GlpRuntimeEngine wrap its Heap here and hand the view to ResultEnvelopeBuilder.

using System;

using Rt = GlpRuntime.Runtime;

namespace GlpRuntime.ResultCodec.Builder;

/// <summary>Adapts the live engine <see cref="Rt.HeapFCP"/> to <see cref="IHeapView"/>.</summary>
public sealed class HeapFcpView : IHeapView
{
    private readonly Rt.HeapFCP _heap;

    public HeapFcpView(Rt.HeapFCP heap)
    {
        _heap = heap ?? throw new ArgumentNullException(nameof(heap));
    }

    public Rt.Term Dereference(Rt.Term term) => _heap.Dereference(term);

    public bool IsBound(int varId) => _heap.IsBound(varId);
}

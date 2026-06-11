using GlpRuntime.Bytecode;
using GlpRuntime.Runtime;

namespace GlpRuntime.IlCodec;

/// <summary>A compiled program located embedded in a <see cref="ModuleTerm"/> on the engine heap.</summary>
public sealed record EmbeddedProgram(int Address, string Name, BytecodeProgram Program);

/// <summary>
/// PHASE b (FR-009b) — locate <see cref="ModuleTerm"/>-embedded programs reached as
/// heap data. Module activation stores a <c>ModuleTerm</c> (whose <c>.Bytecode</c> is
/// a <see cref="BytecodeProgram"/>) directly as a <c>ValueTag</c> heap cell
/// (<c>glp_activation.ActivateModule</c> → <c>HeapFCP.StoreTermOnHeap</c>). This walk
/// scans the heap cells for them. It is bounded to *finding + reading* embedded
/// programs — it does NOT define #7's full heap-snapshot envelope (D5).
/// </summary>
public static class HeapWalk
{
    public static IReadOnlyList<EmbeddedProgram> FindEmbeddedPrograms(HeapFCP heap)
    {
        ArgumentNullException.ThrowIfNull(heap);
        var found = new List<EmbeddedProgram>();
        var cells = heap.Cells;
        for (int addr = 0; addr < cells.Count; addr++)
        {
            if (cells[addr].Content is ModuleTerm mt && mt.Bytecode is BytecodeProgram bp)
                found.Add(new EmbeddedProgram(addr, mt.Name, bp));
        }
        return found;
    }
}

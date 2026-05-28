/// <summary>
/// GLP-level module activation.
/// <para>
/// Creates a GLP channel, spawns serve(Module, ChannelReader?), and
/// returns a handle for sending goals on the channel.
/// </para>
/// <para>
/// Phase 4 of dynamic module dispatch (docs/modules/dynamic-dispatch-implementation-plan.md).
/// </para>
/// </summary>
namespace GlpRuntime.Runtime;

using System.Collections.Generic;
using GlpRuntime.Bytecode;

/// <summary>
/// Handle for a GLP module channel.
/// <para>
/// Holds the writer end of the channel for sending goal terms.
/// Each <see cref="Send"/> call extends the stream: [goal | newTail].
/// </para>
/// </summary>
public class GlpChannelHandle
{
    private readonly HeapFCP _heap;
    private int _writerAddr;

    public GlpChannelHandle(HeapFCP heap, int writerAddr)
    {
        _heap = heap;
        _writerAddr = writerAddr;
    }

    /// <summary>Current writer address (for debugging/testing).</summary>
    public int WriterAddr => _writerAddr;

    /// <summary>
    /// Send a goal term on the channel.
    /// <para>
    /// Binds current writer to [goal | newTail], advances writer to newTail.
    /// Returns goals woken up by the injection (must be enqueued by caller).
    /// </para>
    /// </summary>
    public IReadOnlyList<GoalRef> Send(Term goal)
    {
        var (tailWriterAddr, _) = _heap.AllocateVariable();
        var consCell = new StructTerm(".", new List<Term> { goal, new VarRef(tailWriterAddr) });
        var activations = _heap.BindVariable(_writerAddr, consCell);
        _writerAddr = tailWriterAddr;
        return activations;
    }

    /// <summary>
    /// Close the channel (bind writer to nil / empty list).
    /// <para>
    /// Returns goals woken up by the closure (must be enqueued by caller).
    /// </para>
    /// </summary>
    public IReadOnlyList<GoalRef> Close() => _heap.BindVariable(_writerAddr, new ConstTerm("nil"));
}

/// <summary>
/// Hosting static class for the file-level activation function.
/// </summary>
public static class GlpActivation
{
    /// <summary>
    /// Activate a module at the GLP level.
    /// <para>
    /// Creates a GLP channel, constructs a ModuleTerm from compiled bytecode,
    /// spawns serve(ModuleTerm, ChannelReader?), and returns the channel handle.
    /// </para>
    /// <para>
    /// The serve runner is registered in rt.Runners so the scheduler can find it.
    /// The caller must drain the scheduler to execute the spawned serve goal.
    /// </para>
    /// </summary>
    public static GlpChannelHandle ActivateModule(
        GlpRuntimeEngine rt,
        BytecodeProgram serveBytecode,
        BytecodeProgram moduleBytecode,
        string moduleName)
    {
        // 1. Create GLP channel (writer/reader pair)
        var (writerAddr, readerAddr) = rt.Heap.AllocateVariable();

        // 2. Construct ModuleTerm and store on heap
        var moduleTerm = new ModuleTerm(moduleBytecode, name: moduleName);
        var moduleAddr = rt.Heap.StoreTermOnHeap(moduleTerm);

        // 3. Spawn serve(Module, ChannelReader?)
        var goalId = rt.NextGoalId++;
        var env = new CallEnv(args: new Dictionary<int, Term> { { 0, new VarRef(moduleAddr) }, { 1, new VarRef(readerAddr) } });
        rt.SetGoalEnv(goalId, env);
        rt.SetGoalProgram(goalId, serveBytecode);

        var servePc = serveBytecode.Labels["serve/2"]!;
        rt.Gq.Enqueue(new GoalRef(goalId, servePc));

        // 4. Tag as infrastructure goal (spec §3.4, §3.5)
        rt.InfrastructureGoalIds.Add(goalId);

        // 5. Register serve runner if not already present
        if (!rt.Runners.ContainsKey(serveBytecode))
        {
            rt.Runners[serveBytecode] = new BytecodeRunner(serveBytecode);
        }

        // 6. Register channel handle in rt.GlpChannels for RPC routing (Phase 5)
        var channel = new GlpChannelHandle(rt.Heap, writerAddr);
        rt.GlpChannels[moduleName] = channel;

        // 7. Return channel handle (writer end)
        return channel;
    }
}

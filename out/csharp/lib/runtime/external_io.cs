/// <summary>
/// External I/O for GLP - Phase 0 Implementation
/// <para>
/// Provides mechanism for Dart to inject input and observe output from GLP streams.
/// Based on docs/glp-io-spec.md
/// </para>
/// <para>
/// Per heap-pointer-architecture-spec.md Section 1.1, CGLP paper Definition 5.5,
/// and bytecode spec Sections 8.1-8.2.
/// </para>
/// </summary>
namespace GlpRuntime.Runtime;

using System;
using System.Collections.Generic;

/// <summary>
/// An External Channel is a bidirectional connection between Dart and GLP.
/// <para>
/// Each channel has:
/// - Input stream: Dart injects terms, GLP reads them
/// - Output stream: GLP writes terms, Dart observes them
/// </para>
/// <para>
/// Per heap-pointer-architecture-spec.md Section 1.1:
/// both writer and reader addresses are stored explicitly — no address arithmetic.
/// </para>
/// </summary>
public class ExternalChannel
{
    /// <summary>Channel name — 'user' or 'net'.</summary>
    public string Name { get; }

    /// <summary>Input writer address: Dart holds the writer (to inject terms), GLP receives the reader.</summary>
    public long InputWriterAddr { get; }

    /// <summary>Input reader address.</summary>
    public long InputReaderAddr { get; }

    /// <summary>Output writer address: GLP holds the writer (to produce terms), Dart holds the reader.</summary>
    public long OutputWriterAddr { get; }

    /// <summary>Output reader address.</summary>
    public long OutputReaderAddr { get; }

    public ExternalChannel(string name, long inputWriterAddr, long inputReaderAddr, long outputWriterAddr, long outputReaderAddr)
    {
        Name = name;
        InputWriterAddr = inputWriterAddr;
        InputReaderAddr = inputReaderAddr;
        OutputWriterAddr = outputWriterAddr;
        OutputReaderAddr = outputReaderAddr;
    }

    public override string ToString() =>
        $"ExternalChannel({Name}, in=({InputWriterAddr},{InputReaderAddr}), out=({OutputWriterAddr},{OutputReaderAddr}))";
}

/// <summary>
/// Hosting static class for file-level factory functions (Dart top-level functions → C# static methods).
/// </summary>
public static class ExternalIO
{
    /// <summary>
    /// Factory: create an ExternalChannel with fresh heap variables for both streams.
    /// </summary>
    public static ExternalChannel CreateExternalChannel(HeapFCP heap, string name)
    {
        // Create input stream variable (Dart=writer, GLP=reader)
        var (inputWriterAddr, inputReaderAddr) = heap.AllocateVariable();

        // Create output stream variable (GLP=writer, Dart=reader)
        var (outputWriterAddr, outputReaderAddr) = heap.AllocateVariable();

        return new ExternalChannel(
            name: name,
            inputWriterAddr: inputWriterAddr,
            inputReaderAddr: inputReaderAddr,
            outputWriterAddr: outputWriterAddr,
            outputReaderAddr: outputReaderAddr);
    }

    /// <summary>
    /// Build ch(In, Out) term for GLP.
    /// <para>
    /// Per bytecode spec Section 8.1-8.2 and CGLP paper Definition 5.5:
    /// ch(Reader, Writer) — InputReader in writer-mode HEAD position,
    /// OutputWriter in reader-mode HEAD position.
    /// </para>
    /// </summary>
    public static Term BuildChannelTerm(ExternalChannel channel)
    {
        return new StructTerm("ch", new List<Term>
        {
            new VarRef((int)channel.InputReaderAddr),   // In  - READER
            new VarRef((int)channel.OutputWriterAddr),  // Out - WRITER
        });
    }
}

/// <summary>
/// Injects terms into a GLP input stream.
/// <para>
/// Dart holds the writer end of the input stream.
/// Each Inject() call extends the stream: [term | newTail].
/// </para>
/// </summary>
public class InputInjector
{
    /// <summary>The heap this injector writes into.</summary>
    public HeapFCP Heap { get; }

    /// <summary>Channel name (for debugging).</summary>
    public string ChannelName { get; }

    private long _currentWriterId;

    public InputInjector(HeapFCP heap, string channelName, long initialWriterId)
    {
        Heap = heap;
        ChannelName = channelName;
        _currentWriterId = initialWriterId;
    }

    /// <summary>Current writer variable ID (for debugging).</summary>
    public long CurrentWriterId => _currentWriterId;

    /// <summary>
    /// Inject a term into the input stream.
    /// <para>
    /// Binds current writer to [term | newTail], advances writer to newTail.
    /// Returns list of goals woken up by the injection (should be enqueued).
    /// </para>
    /// </summary>
    public IReadOnlyList<GoalRef> Inject(Term term)
    {
        // Allocate fresh variable for tail — discard reader address
        var (tailWriterAddr, _) = Heap.AllocateVariable();

        // Build list cell: [term | tail] using '.' functor (GLP cons convention)
        var listCell = new StructTerm(".", new List<Term> { term, new VarRef((int)tailWriterAddr) });

        // Bind current writer to list cell — may wake suspended goals
        var activations = Heap.BindVariable((int)_currentWriterId, listCell);

        // Advance writer to tail for next injection
        _currentWriterId = tailWriterAddr;

        return activations;
    }

    /// <summary>
    /// Close the input stream (no more input).
    /// <para>Binds current writer to nil. Returns woken goals.</para>
    /// </summary>
    public IReadOnlyList<GoalRef> Close() =>
        Heap.BindVariable((int)_currentWriterId, new ConstTerm("nil"));
}

/// <summary>
/// Observes terms written to a GLP output stream.
/// <para>
/// Dart holds the reader end of the output stream.
/// When GLP binds the writer to [term | newTail], the callback fires.
/// </para>
/// <para>
/// Dispose() is intentionally NOT part of IDisposable — the Dart source
/// declares no disposal interface; cleanup is caller-driven via AgentIOContext.
/// </para>
/// </summary>
public class OutputObserver
{
    /// <summary>The heap this observer reads from.</summary>
    public HeapFCP Heap { get; }

    /// <summary>Channel name (for debugging).</summary>
    public string ChannelName { get; }

    /// <summary>
    /// Callback invoked for each observed term.
    /// Non-nullable (Dart source: <c>void Function(Term) onTerm</c> — no '?').
    /// </summary>
    public Action<Term> OnTerm { get; }

    /// <summary>
    /// Callback invoked when stream is closed.
    /// Non-nullable (Dart source: <c>void Function() onClose</c> — no '?').
    /// </summary>
    public Action OnClose { get; }

    private long _currentReaderId;
    private bool _closed = false;

    public OutputObserver(HeapFCP heap, string channelName, long initialReaderId, Action<Term> onTerm, Action onClose)
    {
        Heap = heap;
        ChannelName = channelName;
        _currentReaderId = initialReaderId;
        OnTerm = onTerm;
        OnClose = onClose;
        _ObserveNext();
    }

    /// <summary>Current reader variable ID (for debugging).</summary>
    public long CurrentReaderId => _currentReaderId;

    /// <summary>Whether the stream has been closed.</summary>
    public bool IsClosed => _closed;

    private void _ObserveNext()
    {
        if (_closed) return;

        // Convert reader to writer per spec Section 7.1
        long? writerAddr = Heap.TryWriterForReader((int)_currentReaderId);
        if (writerAddr == null)
        {
            // Imported reader — no local writer to observe; handle gracefully
            return;
        }

        // Register callback for when writer is bound
        Heap.OnBind((int)writerAddr.Value, (Term value) =>
        {
            if (_closed) return;

            if (value is StructTerm st && st.Functor == ".")
            {
                // Got [Head | Tail] — cons cell
                var head = st.Args[0];
                var tail = st.Args[1];

                // Notify observer of term
                OnTerm(head);

                // Continue observing tail
                if (tail is VarRef vr)
                {
                    _currentReaderId = vr.Addr;
                    _ObserveNext();
                }
                else if (tail is ConstTerm ct && ct.Value is "nil")
                {
                    // Stream closed with []
                    _closed = true;
                    OnClose();
                }
                else if (tail is StructTerm sn && sn.Functor == ".")
                {
                    // Nested cons — process iteratively
                    _ProcessNestedCons(sn);
                }
            }
            else if (value is ConstTerm ct2 && ct2.Value is "nil")
            {
                // Empty list — stream closed
                _closed = true;
                OnClose();
            }
        });
    }

    /// <summary>Process nested cons cells (when multiple terms are bound at once).</summary>
    private void _ProcessNestedCons(StructTerm cons)
    {
        var current = cons;
        while (true)
        {
            var head = current.Args[0];
            var tail = current.Args[1];

            OnTerm(head);

            if (tail is VarRef vr)
            {
                _currentReaderId = vr.Addr;
                _ObserveNext();
                break;
            }
            else if (tail is ConstTerm ct && ct.Value is "nil")
            {
                _closed = true;
                OnClose();
                break;
            }
            else if (tail is StructTerm sn && sn.Functor == ".")
            {
                current = sn;
            }
            else
            {
                // Unexpected tail type
                break;
            }
        }
    }

    /// <summary>Stop observing (cleanup). Sets closed flag and removes registered callback.</summary>
    public void Dispose()
    {
        _closed = true;
        long? writerAddr = Heap.TryWriterForReader((int)_currentReaderId);
        if (writerAddr != null)
        {
            Heap.RemoveBindCallback((int)writerAddr.Value);
        }
    }
}

/// <summary>
/// Context for an agent with both user and network channels.
/// <para>Provides convenient access to all I/O components.</para>
/// </summary>
public class AgentIOContext
{
    /// <summary>Agent identifier.</summary>
    public string AgentId { get; }

    /// <summary>The shared heap.</summary>
    public HeapFCP Heap { get; }

    // User channel (UI)
    /// <summary>User-facing external channel descriptor.</summary>
    public ExternalChannel UserChannel { get; }

    /// <summary>Injector for user input stream.</summary>
    public InputInjector UserInput { get; }

    /// <summary>
    /// Observer for user output stream.
    /// <c>late final</c> analogue: assigned exactly once in Create() before the instance escapes.
    /// </summary>
    public OutputObserver UserOutput { get; private set; } = null!;

    // Network channel
    /// <summary>Network-facing external channel descriptor.</summary>
    public ExternalChannel NetChannel { get; }

    /// <summary>Injector for net input stream.</summary>
    public InputInjector NetInput { get; }

    /// <summary>
    /// Observer for net output stream.
    /// <c>late final</c> analogue: assigned exactly once in Create() before the instance escapes.
    /// </summary>
    public OutputObserver NetOutput { get; private set; } = null!;

    // Collected output for testing
    private readonly List<Term> _userOutputTerms = new List<Term>();
    private readonly List<Term> _netOutputTerms = new List<Term>();

    /// <summary>Terms collected from the user output stream (read-only view).</summary>
    public IReadOnlyList<Term> UserOutputTerms => _userOutputTerms;

    /// <summary>Terms collected from the net output stream (read-only view).</summary>
    public IReadOnlyList<Term> NetOutputTerms => _netOutputTerms;

    /// <summary>True when the user output stream has been closed.</summary>
    public bool UserOutputClosed { get; set; } = false;

    /// <summary>True when the net output stream has been closed.</summary>
    public bool NetOutputClosed { get; set; } = false;

    /// <summary>
    /// Private constructor (corresponds to Dart <c>AgentIOContext._({ required ... })</c>).
    /// Only invoked by the in-class Create factory.
    /// </summary>
    private AgentIOContext(string agentId, HeapFCP heap, ExternalChannel userChannel, InputInjector userInput, ExternalChannel netChannel, InputInjector netInput)
    {
        AgentId = agentId;
        Heap = heap;
        UserChannel = userChannel;
        UserInput = userInput;
        NetChannel = netChannel;
        NetInput = netInput;
    }

    /// <summary>
    /// Create an AgentIOContext with both channels set up.
    /// <para>
    /// The output observers collect terms into lists for easy testing.
    /// </para>
    /// </summary>
    public static AgentIOContext Create(HeapFCP heap, string agentId)
    {
        // Create channels
        var userChannel = ExternalIO.CreateExternalChannel(heap, "user");
        var netChannel = ExternalIO.CreateExternalChannel(heap, "net");

        // Create input injectors (Dart holds writer, injects via writer address)
        var userInput = new InputInjector(heap, "user", userChannel.InputWriterAddr);
        var netInput = new InputInjector(heap, "net", netChannel.InputWriterAddr);

        var context = new AgentIOContext(agentId, heap, userChannel, userInput, netChannel, netInput);

        // Create output observers that collect terms (Dart observes via reader address).
        // The callbacks close over context — this is why OutputObserver cannot be
        // constructed before context exists (the late final pattern).
        context.UserOutput = new OutputObserver(
            heap,
            "user",
            userChannel.OutputReaderAddr,
            (term) => context._userOutputTerms.Add(term),
            () => context.UserOutputClosed = true);

        context.NetOutput = new OutputObserver(
            heap,
            "net",
            netChannel.OutputReaderAddr,
            (term) => context._netOutputTerms.Add(term),
            () => context.NetOutputClosed = true);

        return context;
    }

    /// <summary>Build the ch(UserIn, UserOut) term for GLP.</summary>
    public Term UserChannelTerm => ExternalIO.BuildChannelTerm(UserChannel);

    /// <summary>Build the ch(NetIn, NetOut) term for GLP.</summary>
    public Term NetChannelTerm => ExternalIO.BuildChannelTerm(NetChannel);

    /// <summary>Dispose all observers.</summary>
    public void Dispose()
    {
        UserOutput.Dispose();
        NetOutput.Dispose();
    }

    public override string ToString() => $"AgentIOContext({AgentId})";
}

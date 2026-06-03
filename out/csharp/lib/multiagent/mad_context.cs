// madGLP Agent Context
//
// Provides agent-level context for multiagent GLP communication.
// Each agent has W_p (global writers table) and M_p (message queue).
//
// Specification: /docs/ma/madGLP-spec.md

using System;
using System.Collections.Generic;
using System.Linq;
using GlpRuntime.Runtime;

namespace GlpRuntime.Multiagent;

/// <summary>Callback for delivering messages to other agents.</summary>
public delegate void MessageDeliveryCallback(string destination, OutboundMessage message);

/// <summary>
/// madGLP Agent Context.
/// <para>Wraps a GlpRuntimeEngine with W_p and M_p for multiagent operation.
/// Each agent has its own MadContext with unique agentId.</para>
/// </summary>
/// <remarks>
/// CONCURRENCY MODEL (load-bearing): MadContext is the per-agent owner — created once
/// per agent by the isolate manager inside that agent's owning execution context, accessed
/// only by code running on the agent's owning Channel&lt;T&gt; consumer Task (escalation #5,
/// commit 12a468f5). ALL state reachable through this instance (Wp, Mp, GlobalSendRegistry,
/// _serializer, the runtime heap and goal queue, the callback fields) is per-instance and
/// isolate-local. The C# port MUST NOT add lock/SemaphoreSlim/Monitor/ConcurrentDictionary
/// inside any method of MadContext — compound check-then-act sequences (HandleMadAssignment,
/// FlushMessages snapshot-then-drain, _fireGlobalSendGoalIfExists lookup-then-spawn) must
/// remain atomic at the isolate-mailbox-consumption boundary.
/// </remarks>
public class MadContext
{
    /// <summary>Unique identifier for this agent (e.g., "alice", "bob").</summary>
    public string AgentId { get; }

    /// <summary>Underlying GLP runtime.</summary>
    public GlpRuntimeEngine Runtime { get; }

    /// <summary>Global writers table W_p: tracks writers awaiting incoming assignments.</summary>
    public GlobalWritersTable Wp { get; }

    /// <summary>Message queue M_p: outbound messages to other agents.</summary>
    public MessageQueue Mp { get; }

    /// <summary>Registry for pending global_send goals (watches readers, sends when known).</summary>
    public GlobalSendRegistry GlobalSendRegistry { get; }

    /// <summary>Payload serializer for message encoding.</summary>
    private readonly PayloadSerializer _serializer;

    /// <summary>Optional callback for message delivery (set by coordinator).</summary>
    public MessageDeliveryCallback? OnMessageReady;

    /// <summary>
    /// Optional trace sink for MAD infrastructure output.
    /// When set, MAD debug output goes through this callback instead of print().
    /// </summary>
    public Action<string>? TraceSink;

    /// <summary>
    /// Initializes a new MadContext for the given agent.
    /// </summary>
    /// <param name="agentId">Unique identifier for this agent.</param>
    /// <param name="runtime">Underlying GLP runtime instance.</param>
    public MadContext(string agentId, GlpRuntimeEngine runtime)
    {
        AgentId = agentId;
        Runtime = runtime;
        Wp = new GlobalWritersTable(agentId);
        Mp = new MessageQueue();
        GlobalSendRegistry = new GlobalSendRegistry(agentId);
        _serializer = new PayloadSerializer(agentId);
    }

    // =========================================================================
    // Private trace helper
    // =========================================================================

    /// <summary>Send MAD trace output to TraceSink if set, otherwise silent.</summary>
    private void Trace(string msg) => TraceSink?.Invoke(msg);

    // =========================================================================
    // Writer Binding Observation
    // =========================================================================

    /// <summary>
    /// Called when a writer is bound to a value.
    /// <para>Checks for global_send goals watching this writer's reader and fires
    /// them if found (per madGLP-spec.md Section 4).</para>
    /// </summary>
    public void OnWriterBound(int writerId, Term value)
    {
        Trace($"[MAD {AgentId}] onWriterBound: writerId={writerId}, value={value}");
        _fireGlobalSendGoalIfExists(writerId, value);
    }

    // =========================================================================
    // Message Queue Operations (Send Transaction)
    // =========================================================================

    /// <summary>
    /// Flush all queued messages via the OnMessageReady callback.
    /// <para>Returns the number of messages flushed.</para>
    /// </summary>
    public int FlushMessages()
    {
        var cb = OnMessageReady;
        if (cb == null) return 0;

        int count = 0;
        // LOAD-BEARING snapshot: Mp.Poll(dest) may remove empty destination buckets,
        // which would invalidate an enumerator over Mp.Destinations directly.
        var destinations = Mp.Destinations.ToList();
        Trace($"[MAD {AgentId}] flushMessages: mp.totalLength={Mp.TotalLength}, destinations=[{string.Join(", ", destinations)}]");

        foreach (var dest in destinations)
        {
            while (true)
            {
                var msg = Mp.Poll(dest);
                if (msg == null) break;
                Trace($"[MAD {AgentId}] flushMessages: sending {msg.Type} to {dest}");
                cb.Invoke(dest, msg);
                count++;
            }
        }
        Trace($"[MAD {AgentId}] flushMessages: flushed {count} messages");
        return count;
    }

    // =========================================================================
    // madGLP global_send Mechanism (Phase 3)
    // =========================================================================

    /// <summary>
    /// Fire global_send goal if one is watching this writer's reader.
    /// <para>
    /// When a writer is bound, its paired reader becomes "known". If there's
    /// a global_send goal watching that reader, fire it now.
    /// </para>
    /// <remarks>
    /// Spec Section 12 (Goal Atomicity): globalization and message creation happen
    /// atomically — new goals for nested variables are registered before this operation
    /// completes. Atomicity is guaranteed structurally by the isolate-ownership invariant.
    /// </remarks>
    /// </summary>
    private void _fireGlobalSendGoalIfExists(int writerAddr, Term value)
    {
        // Check if there's a global_send goal watching this writer's reader
        var result = GlobalSendRegistry.OnWriterBound(
            writerAddr: writerAddr,
            value: value,
            table: Wp,
            extractVariables: (object? val) =>
            {
                var vars = new List<TermVar>();
                if (val is Term term)
                {
                    _ExtractTermVarsRecursive(term, vars);
                }
                return vars;
            });

        if (result == null)
        {
            return;
        }

        Trace($"[MAD {AgentId}] global_send FIRED: {result.GlobalName} -> {result.Destination}");

        // Globalize the value term: replace local VarRefs with GlobalNames
        // This is needed so the receiver can localize nested global names
        var globalizedValue = MadHelpers.GlobalizeTermWithResult(
            (Term)result.Value!,
            result.ExtractedVariables,
            result.GlobalizeResult);

        // Queue the assignment message
        var payload = _serializer.CreateGlobalSendPayload(
            result.GlobalName,
            globalizedValue,
            Runtime.Heap.IsReader,
            lookupVariable: _LookupVariableForSerialization);

        Mp.Add(new OutboundMessage(
            destination: result.Destination,
            type: MessageType.Assignment,
            payload: new List<byte>(payload)));

        // Register any new goals spawned for nested variables
        // Must set up both registry entry AND onBind callback (same as RegisterGlobalSendSpawns)
        foreach (var newGoal in result.NewGoals)
        {
            Trace($"[MAD {AgentId}] global_send spawned new goal: {newGoal.GlobalName}");
            GlobalSendRegistry.Register(newGoal);
            var capturedAddr = newGoal.ReaderAddr;
            Runtime.Heap.OnBind(newGoal.ReaderAddr, (Term v) => OnWriterBound(capturedAddr, v));
        }
    }

    /// <summary>
    /// Lookup variable info for serialization.
    /// <para>For local variables, use the current agent as creator.
    /// This is a simplified version — extend as needed for imported vars.</para>
    /// </summary>
    private VariableLookup _LookupVariableForSerialization(int addr) =>
        new VariableLookup(Creator: AgentId, CreatorLocalId: addr, IsReader: Runtime.Heap.IsReader(addr));

    /// <summary>
    /// Extract TermVars from a term for globalization.
    /// <para>Each TermVar carries both the writer and reader addresses of its pair,
    /// looked up via the heap's cross-pointers.</para>
    /// </summary>
    private void _ExtractTermVarsRecursive(Term term, List<TermVar> result)
    {
        if (term is VarRef varRef)
        {
            var isReader = Runtime.Heap.IsReader(varRef.Addr);
            if (isReader)
            {
                var writerAddr = Runtime.Heap.TryWriterForReader(varRef.Addr);
                result.Add(TermVar.Reader(varRef.Addr, writerAddr: writerAddr ?? varRef.Addr));
            }
            else
            {
                var readerAddr = Runtime.Heap.PairedReaderAddr(varRef.Addr);
                result.Add(TermVar.Writer(varRef.Addr, readerAddr: readerAddr));
            }
        }
        else if (term is StructTerm structTerm)
        {
            foreach (var arg in structTerm.Args)
            {
                _ExtractTermVarsRecursive(arg, result);
            }
        }
        // ConstTerm has no variables
    }

    /// <summary>
    /// Register global_send goals from GlobalSendSpawn info.
    /// <para>
    /// Called after Globalize() or Localize() to register any spawned goals.
    /// Per spec Section 4: global_send(T, G, Q) fires when T (the reader) becomes
    /// known, i.e., when the paired writer is bound. We register both the goal in
    /// the GlobalSendRegistry and an onBind callback on the heap so that
    /// OnWriterBound is called when the writer is assigned.
    /// </para>
    /// </summary>
    public void RegisterGlobalSendSpawns(IReadOnlyList<GlobalSendSpawn> spawns)
    {
        foreach (var spawn in spawns)
        {
            GlobalSendRegistry.Register(GlobalSendGoal.FromSpawn(spawn));
            var capturedReaderAddr = spawn.ReaderAddr;
            Runtime.Heap.OnBind(spawn.ReaderAddr, (Term value) => OnWriterBound(capturedReaderAddr, value));
            Trace($"[MAD {AgentId}] registered global_send goal: {spawn.GlobalName} -> {spawn.DestAgent}");
        }
    }

    // =========================================================================
    // madGLP Receive Transaction (Phase 4)
    // =========================================================================

    /// <summary>
    /// Handle incoming madGLP assignment message.
    /// <para>Per spec Section 8.3, handles three cases:</para>
    /// <remarks>
    /// Case <c>_w(p, 0) := [T | _w(p,0)]</c> (Serializer): Cold-call to our network input.
    /// Case <c>_w(p, i) := T</c> with i &gt; 0: We globalized writer Y, find entry (Y, q) at index i.
    /// Case <c>_r(p, i) := T</c>: We localized _r(p,i), search for entry (Z_q, p, i).
    ///
    /// Both non-serializer cases: localize T, bind writer, register spawned goals, remove entry.
    /// Serializer case: extend network input stream, update entry (don't remove).
    /// </remarks>
    /// </summary>
    public void HandleMadAssignment(GlobalName globalName, Term value, string fromAgent)
    {
        Trace($"[MAD {AgentId}] handleMadAssignment: {globalName} := {value} from {fromAgent}");

        // ORDER IS LOAD-BEARING: serializer case (IsWriter && Index == 0) MUST test first,
        // otherwise the writer case (IsWriter alone) would swallow the serializer case.
        if (globalName.IsWriter && globalName.Index == 0)
        {
            // Case _w(p, 0) := [T | _w(p,0)] - serializer (cold-call to network input)
            _HandleSerializerAssignment(value, fromAgent);
        }
        else if (globalName.IsWriter)
        {
            // Case _w(p, i) := T with i > 0 - we globalized this writer, direct lookup
            _HandleWriterAssignment(globalName, value, fromAgent);
        }
        else
        {
            // Case _r(p, i) := T - we localized this, search for entry
            _HandleReaderAssignment(globalName, value, fromAgent);
        }
    }

    /// <summary>
    /// Handle serializer assignment _w(p, 0) := [T | _w(p,0)].
    /// <para>This is a cold-call to our network input stream.</para>
    /// <remarks>
    /// Spec Section 8.3: "Agent q finds the permanent entry (N_q, *) at index 0.
    /// Localize T↑ by q to get T_q↓. Assign N_q := [T_q↓ | N'_q] where N'_q is a
    /// fresh writer. Update the entry to (N'_q, *) at index 0."
    /// The permanent entry is NEVER removed — only UpdateSerializerWriter is called.
    /// </remarks>
    /// </summary>
    private void _HandleSerializerAssignment(Term value, string fromAgent)
    {
        Trace($"[MAD {AgentId}] _handleSerializerAssignment: cold-call from {fromAgent}");

        // Get current serializer writer from index-0 entry
        var currentWriter = Wp.SerializerWriterAddr;
        if (currentWriter == null)
        {
            throw new InvalidOperationException("Serializer entry not initialized at index 0");
        }
        Trace($"[MAD {AgentId}] _handleSerializerAssignment: current serializer writer={currentWriter}");

        // The value should be [T | serializer_marker] - extract the content T
        // The serializer marker is a special constant we recognize
        Term content;
        if (value is StructTerm structValue && structValue.Functor == "." && structValue.Args.Count == 2)
        {
            content = structValue.Args[0]; // Head is the actual content
            // Tail (structValue.Args[1]) should be the serializer marker, we ignore it
            Trace($"[MAD {AgentId}] _handleSerializerAssignment: extracted content from list cell");
        }
        else
        {
            // If not wrapped in list cell, use the value directly (for compatibility)
            content = value;
            Trace($"[MAD {AgentId}] _handleSerializerAssignment: using value directly (no list wrapper)");
        }

        // Localize the content: replace global names with local variables
        // Per spec Section 8.3: "Localize T↑ by q to get T_q↓"
        var globalNames = MadHelpers.ExtractGlobalNames(content);
        if (globalNames.Count > 0)
        {
            Trace($"[MAD {AgentId}] _handleSerializerAssignment: found {globalNames.Count} global names to localize");
            var localizeResult = MadHelpers.Localize(
                globalNames: globalNames,
                localAgent: AgentId,
                table: Wp,
                freshAddrAllocator: () => Runtime.Heap.AllocateVariable());

            // Register spawned goals from localization (_r cases)
            RegisterGlobalSendSpawns(localizeResult.Spawns);

            // Replace global names with local variables in content
            content = MadHelpers.LocalizeTermWithResult(content, globalNames, localizeResult);
            Trace($"[MAD {AgentId}] _handleSerializerAssignment: localized content = {content}");
        }

        // Allocate fresh writer for stream continuation
        var (freshWriter, freshReader) = Runtime.Heap.AllocateVariable();
        Trace($"[MAD {AgentId}] _handleSerializerAssignment: fresh stream continuation ({freshWriter},{freshReader})");

        // Build the list cell [content | freshReader]
        // This extends the network input stream by one element
        var listCell = new StructTerm(".", new List<Term> { content, new VarRef(freshReader) });

        // Bind current writer to extend the stream: N_p := [content | N'_p?]
        var activations = Runtime.Heap.BindVariable(currentWriter.Value, listCell);
        Trace($"[MAD {AgentId}] _handleSerializerAssignment: bound stream, {activations.Count} activations");

        // Update the serializer entry to point to the fresh writer
        // This entry is permanent - never removed
        Wp.UpdateSerializerWriter(freshWriter);
        Trace($"[MAD {AgentId}] _handleSerializerAssignment: updated serializer entry to writer={freshWriter}");

        // Reactivate suspended goals waiting on the network input stream
        foreach (var act in activations)
        {
            Runtime.EnqueueReactivatedGoal(act);
        }
    }

    /// <summary>
    /// Handle _w(p, i) := T assignment with i &gt; 0 (we globalized writer Y).
    /// <para>We are agent p. We globalized writer Y, creating GlobalizeEntry (Y, q) at index i.
    /// Now q's gs fires and sends _w(p, i) := T to us. Direct index lookup.</para>
    /// <remarks>Per spec Section 8.3: "Agent p finds entry (X, q) at index i in W_p."</remarks>
    /// </summary>
    private void _HandleWriterAssignment(GlobalName globalName, Term value, string fromAgent)
    {
        var entry = Wp.LookupByIndex(globalName.Index);
        if (entry == null)
        {
            throw new InvalidOperationException(
                $"No GlobalizeEntry at index {globalName.Index} for {globalName}");
        }

        var writerAddr = entry.WriterAddr;
        Trace($"[MAD {AgentId}] _handleWriterAssignment: globalize-writer entry, writerAddr={writerAddr}");

        // Localize the value: replace global names with local variables
        // Per spec Section 8.3: "Localize T↑ by p to get T_p↓"
        Term localizedValue = value;
        var globalNames = MadHelpers.ExtractGlobalNames(value);
        if (globalNames.Count > 0)
        {
            Trace($"[MAD {AgentId}] _handleWriterAssignment: localizing {globalNames.Count} nested global names");
            var localizeResult = MadHelpers.Localize(
                globalNames: globalNames,
                localAgent: AgentId,
                table: Wp,
                freshAddrAllocator: () => Runtime.Heap.AllocateVariable());
            RegisterGlobalSendSpawns(localizeResult.Spawns);
            localizedValue = MadHelpers.LocalizeTermWithResult(value, globalNames, localizeResult);
            Trace($"[MAD {AgentId}] _handleWriterAssignment: localized value = {localizedValue}");
        }

        // Bind the writer with localized value
        var activations = Runtime.Heap.BindVariable(writerAddr, localizedValue);
        Trace($"[MAD {AgentId}] _handleWriterAssignment: bound writer, {activations.Count} activations");

        // Reactivate suspended goals
        foreach (var act in activations)
        {
            Runtime.EnqueueReactivatedGoal(act);
        }

        // Remove the entry (transient — unlike the permanent serializer entry)
        Wp.RemoveGlobalizeEntry(globalName.Index);
        Trace($"[MAD {AgentId}] _handleWriterAssignment: entry removed");
    }

    /// <summary>
    /// Handle _r(p, i) := T assignment (we localized _r(p, i)).
    /// <para>We are agent q. We localized _r(p, i), creating LocalizeEntry (Z_q, p, i).
    /// Now p's gs fires and sends _r(p, i) := T to us. Search by (p, i).</para>
    /// <remarks>Per spec Section 8.3: "Agent q searches for entry (X_q, p, i)."</remarks>
    /// </summary>
    private void _HandleReaderAssignment(GlobalName globalName, Term value, string fromAgent)
    {
        var entry = Wp.FindByRemote(globalName.Agent, globalName.Index);
        if (entry == null)
        {
            throw new InvalidOperationException(
                $"No LocalizeEntry for {globalName}: expected entry with " +
                $"(remoteAgent={globalName.Agent}, remoteIndex={globalName.Index})");
        }

        Trace($"[MAD {AgentId}] _handleReaderAssignment: localize-reader entry, writerAddr={entry.WriterAddr}");

        // Localize the value: replace global names with local variables
        // Per spec Section 8.3: "Localize T↑ by q to get T_q↓"
        Term localizedValue = value;
        var globalNames = MadHelpers.ExtractGlobalNames(value);
        if (globalNames.Count > 0)
        {
            Trace($"[MAD {AgentId}] _handleReaderAssignment: localizing {globalNames.Count} nested global names");
            var localizeResult = MadHelpers.Localize(
                globalNames: globalNames,
                localAgent: AgentId,
                table: Wp,
                freshAddrAllocator: () => Runtime.Heap.AllocateVariable());
            RegisterGlobalSendSpawns(localizeResult.Spawns);
            localizedValue = MadHelpers.LocalizeTermWithResult(value, globalNames, localizeResult);
            Trace($"[MAD {AgentId}] _handleReaderAssignment: localized value = {localizedValue}");
        }

        // Bind the writer with localized value
        var activations = Runtime.Heap.BindVariable(entry.WriterAddr, localizedValue);
        Trace($"[MAD {AgentId}] _handleReaderAssignment: bound writer, {activations.Count} activations");

        // Reactivate suspended goals
        foreach (var act in activations)
        {
            Runtime.EnqueueReactivatedGoal(act);
        }

        // Remove the entry (transient)
        Wp.RemoveLocalizeEntry(globalName.Agent, globalName.Index);
        Trace($"[MAD {AgentId}] _handleReaderAssignment: entry removed");
    }

    /// <summary>
    /// Handle madGLP assignment with nested global names.
    /// <para>Extended version that handles nested global names in the value term,
    /// creating LocalizeEntries and spawning global_send goals as needed.</para>
    /// <remarks>
    /// ORDER IS LOAD-BEARING: outer-localize (steps 1-2) MUST execute BEFORE the inner
    /// dispatcher call (step 3). The externally-supplied nestedGlobalNames may differ from
    /// names found inside the value term by the inner dispatcher's own ExtractGlobalNames pass.
    /// </remarks>
    /// </summary>
    public void HandleMadAssignmentWithGlobalNames(
        GlobalName globalName,
        Term value,
        IReadOnlyList<GlobalName> nestedGlobalNames,
        string fromAgent)
    {
        Trace($"[MAD {AgentId}] handleMadAssignmentWithGlobalNames: {globalName} with {nestedGlobalNames.Count} nested names");

        // Localize the nested global names
        var localizeResult = MadHelpers.Localize(
            globalNames: nestedGlobalNames,
            localAgent: AgentId,
            table: Wp,
            freshAddrAllocator: () => Runtime.Heap.AllocateVariable());

        // Register spawned goals from localization
        RegisterGlobalSendSpawns(localizeResult.Spawns);

        // Now handle the main assignment
        HandleMadAssignment(globalName, value, fromAgent);
    }

    // =========================================================================
    // Export/Import Operations (for Flutter app compatibility)
    // =========================================================================

    /// <summary>
    /// Export a term for sending to another agent.
    /// <para>
    /// Extracts variables and registers onBind callbacks for writers so assignments
    /// can be routed. For madGLP, this prepares the term for serialization.
    /// </para>
    /// <remarks>
    /// Spec Section 5.1 writer-passive / reader-active asymmetry (LOAD-BEARING):
    /// Only WRITERS get onBind callbacks here. Readers do NOT — the push model
    /// means we wait for the assignment to arrive from the writer's owner.
    /// </remarks>
    /// </summary>
    public void ExportTerm(Term term)
    {
        var vars = new List<TermVar>();
        _ExtractTermVarsRecursive(term, vars);

        // Register onBind callbacks for any writers in the term
        foreach (var v in vars)
        {
            if (v.IsWriter)
            {
                // Set up callback so when this writer is bound, we can route the assignment
                var capturedAddr = v.Addr;
                Runtime.Heap.OnBind(v.Addr, (Term value) => OnWriterBound(capturedAddr, value));
                Trace($"[MAD {AgentId}] exportTerm: registered callback for writer {v.Addr}");
            }
        }
    }

    /// <summary>
    /// Process suspension — in madGLP, blocking readers don't trigger immediate requests.
    /// <para>
    /// The madGLP model is push-based: assignments are sent when writers are bound,
    /// not when readers request. However, for compatibility with the UI, we log
    /// blocking readers.
    /// </para>
    /// <remarks>
    /// INTENTIONALLY a no-op modulo logging (push model — no request message sent).
    /// Codegen MUST NOT add any side effect here.
    /// </remarks>
    /// </summary>
    public void ProcessSuspension(ISet<int> blockingReaders)
    {
        // In madGLP, suspension means we're waiting for assignments to arrive
        // The push model means we don't send read requests - we just wait
        foreach (var readerId in blockingReaders)
        {
            Trace($"[MAD {AgentId}] processSuspension: waiting for assignment to reader {readerId}");
        }
        // No explicit request messages needed in madGLP push model
    }

    // =========================================================================
    // Unified Send ('_send' builtin implementation)
    // =========================================================================

    /// <summary>
    /// Unified send operation per spec Section 11.5.
    /// <para>
    /// The <c>'_send'(T, G, Q)</c> builtin behavior depends on whether G is a serializer
    /// address (index 0) or a normal global name (index &gt; 0):
    /// </para>
    /// <remarks>
    /// Case G = <c>_w(q, 0)</c> (Serializer):
    /// 1. Globalizes term T for remote agent Q
    /// 2. Adds message (_w(q,0) := [T↑ | _w(q,0)], Q) to M_p — content wrapped in list cell
    ///
    /// Case G = <c>_w(p, i)</c> or <c>_r(p, i)</c> with i &gt; 0 (Normal):
    /// 1. Globalizes term T for remote agent Q
    /// 2. Adds message (G := T↑, Q) to M_p — content sent directly
    /// </remarks>
    /// </summary>
    public void Send(Term term, bool isWriter, string gnAgent, int gnIndex, string destAgent)
    {
        Trace($"[MAD {AgentId}] send: term={term}, isWriter={isWriter}, gnAgent={gnAgent}, gnIndex={gnIndex}, dest={destAgent}");

        // Extract variables from the term for globalization
        var vars = new List<TermVar>();
        _ExtractTermVarsRecursive(term, vars);
        Trace($"[MAD {AgentId}] send: found {vars.Count} variables in term");

        // Globalize the term for the destination agent
        // This creates entries for writers and spawns global_send goals for readers
        var globalizeResult = MadHelpers.Globalize(
            variables: vars,
            localAgent: AgentId,
            remoteAgent: destAgent,
            table: Wp);

        // Register the spawned global_send goals for readers
        RegisterGlobalSendSpawns(globalizeResult.Spawns);

        // For globalize-writer entries: NO onBind is registered here.
        // Per spec Section 5.1: when Y is a writer, p creates an entry (Y, q) and
        // waits for the assignment to arrive. The global_send goal is spawned at q
        // (by localize), not at p. Agent p does not send anything for _w entries.

        // Transform the term to use global names
        var globalizedTerm = MadHelpers.GlobalizeTermWithResult(term, vars, globalizeResult);
        Trace($"[MAD {AgentId}] send: globalized term = {globalizedTerm}");

        // Build the global name structure
        var globalName = isWriter
            ? GlobalName.Writer(gnAgent, gnIndex)
            : GlobalName.Reader(gnAgent, gnIndex);

        // Create payload based on serializer (index 0) vs normal (index > 0)
        List<byte> payload;
        if (isWriter && gnIndex == 0)
        {
            // Serializer case: wrap in list cell [T↑ | _w(q,0)]
            Trace($"[MAD {AgentId}] send: serializer case, wrapping in list cell");
            payload = new List<byte>(_serializer.CreateSerializerPayload(
                globalName,
                globalizedTerm,
                Runtime.Heap.IsReader,
                lookupVariable: _LookupVariableForSerialization));
        }
        else
        {
            // Normal case: send directly
            Trace($"[MAD {AgentId}] send: normal case, sending directly");
            payload = new List<byte>(_serializer.CreateGlobalSendPayload(
                globalName,
                globalizedTerm,
                Runtime.Heap.IsReader,
                lookupVariable: _LookupVariableForSerialization));
        }

        // Queue the message for delivery
        Mp.Add(new OutboundMessage(
            destination: destAgent,
            type: MessageType.Assignment,
            payload: payload));

        Trace($"[MAD {AgentId}] send: queued message to {destAgent}, mp.totalLength={Mp.TotalLength}");
    }
}

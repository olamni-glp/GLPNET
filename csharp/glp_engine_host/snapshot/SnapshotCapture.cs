// SnapshotCapture — the FR-010/DEF-D1 complete-state capture (T018).
//
// Walks the quiescent engine into the format_version-1 section payloads:
// heap cells VERBATIM (addresses preserved, FR-011/DEF-E2), goal queue
// (recorded for integrity — empty at quiescence), suspended-goal index +
// per-goal tables, both goal-id counters, loaded IL units (as their exact
// source text — recompilation is deterministic and the unit list is the
// host's own load record), timers as remaining-duration entries (FR-015,
// from Quiescence.DisarmTimersForCapture), InfrastructureGoalIds,
// GlpChannels, and link definitions + cursor positions (section 0x09).
//
// Reaches runtime state through public surface plus the 061 additive
// accessors ONLY (IV-b) — nothing is removed or changed on the runtime.
//
// Loud-fail discipline: state this format cannot represent (a madGLP
// VariableEntry cell, an unknown Term subtype, an unmappable program key)
// throws SnapshotException — never a silently lossy snapshot (FR-014's
// "MUST NOT emit an inconsistent snapshot").

using GlpRuntime.Engine;
using GlpRuntime.Link.Primitives;
using GlpRuntime.ResultCodec;
using GlpRuntime.Runtime;

using RtTerm = GlpRuntime.Runtime.Term;
using RtConstTerm = GlpRuntime.Runtime.ConstTerm;
using RtStructTerm = GlpRuntime.Runtime.StructTerm;
using RtVarRef = GlpRuntime.Runtime.VarRef;

namespace GlpRuntime.EngineHost.Snapshot;

/// <summary>One client-loaded program unit: the host's own load record (0x05).</summary>
public sealed record LoadedUnit(string Name, string Source);

public static class SnapshotCapture
{
    /// <summary>
    /// Capture the complete resumable state of a quiescent engine into a blob.
    /// The caller (RequestDispatcher via Quiescence) has already verified
    /// quiescence and disarmed the timers; <paramref name="disarmedTimers"/> is
    /// that disarm's remaining-duration record.
    /// </summary>
    public static SnapshotBlob Capture(
        GlpEngine engine,
        LinkRuntime? linkRuntime,
        IReadOnlyList<LoadedUnit> loadedUnits,
        string rootSelfSource,
        IReadOnlyList<DisarmedTimer> disarmedTimers,
        string engineIdentity,
        ulong seq,
        IReadOnlyList<RestoredLinkDefinition>? outstandingLinks = null)
    {
        var rt = engine.Runtime;
        var moduleNameByProgram = BuildModuleNameMap(rt);

        var sections = new Dictionary<byte, byte[]>
        {
            [SnapshotSection.HeapCells] = EncodeHeap(rt, moduleNameByProgram),
            [SnapshotSection.GoalQueue] = EncodeGoalQueue(rt),
            [SnapshotSection.SuspendedGoals] = EncodeSuspendedAndTables(engine, rt, moduleNameByProgram),
            [SnapshotSection.NextGoalId] = EncodeCounters(engine, rt),
            [SnapshotSection.LoadedIlUnits] = EncodeUnits(rootSelfSource, loadedUnits),
            [SnapshotSection.Timers] = EncodeTimers(rt, disarmedTimers),
            [SnapshotSection.InfrastructureGoalIds] = EncodeInfraIds(rt),
            [SnapshotSection.GlpChannels] = EncodeChannels(rt),
            [SnapshotSection.LinkDefinitions] = EncodeLinks(linkRuntime, outstandingLinks),
        };

        return new SnapshotBlob(
            SnapshotBlob.FormatVersion1,
            engineIdentity,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            seq,
            sections);
    }

    // ------------------------------------------------------------- 0x01 heap

    private static byte[] EncodeHeap(
        GlpRuntimeEngine rt, IReadOnlyDictionary<object, string> moduleNameByProgram)
    {
        var heap = rt.Heap;
        if (heap.Hp != heap.Cells.Count)
            throw new SnapshotException(
                $"inconsistent heap: Hp={heap.Hp} but Cells.Count={heap.Cells.Count}");

        // Pass 1 — tables. Suspension RECORD identity is load-bearing (disarm
        // propagates through the shared object), so records are serialized once
        // and chains reference them by index. Same for shared-mutable MutualRefs.
        var recordIndex = new Dictionary<SuspensionRecord, int>(ReferenceEqualityComparer.Instance);
        var records = new List<SuspensionRecord>();
        var mutualRefs = new Dictionary<int, int>(); // id → currentWriterAddr

        foreach (var cell in heap.Cells)
        {
            switch (cell.Content)
            {
                case SuspensionListNode chain:
                    IndexChain(chain, recordIndex, records);
                    break;
                case WriterContent wc when wc.Suspensions != null:
                    IndexChain(wc.Suspensions, recordIndex, records);
                    break;
                case RtTerm t:
                    IndexMutualRefs(t, mutualRefs);
                    break;
            }
        }

        var w = new ByteWriter();
        w.WriteVarUInt(records.Count);
        foreach (var rec in records)
        {
            w.WriteInt64LE(rec.GoalId ?? long.MinValue); // MinValue = disarmed (null)
            w.WriteVarUInt(rec.ResumePC);
        }
        w.WriteVarUInt(mutualRefs.Count);
        foreach (var (id, addr) in mutualRefs.OrderBy(kv => kv.Key))
        {
            w.WriteVarUInt(id);
            w.WriteVarUInt(addr);
        }

        // Pass 2 — cells, verbatim in address order.
        w.WriteVarUInt(heap.Hp);
        for (int addr = 0; addr < heap.Cells.Count; addr++)
        {
            var cell = heap.Cells[addr];
            w.WriteByte(cell.Tag switch
            {
                HeapCellTag.WrtTag => (byte)0,
                HeapCellTag.RoTag => (byte)1,
                HeapCellTag.ValueTag => (byte)2,
                _ => throw new SnapshotException($"unknown heap cell tag {cell.Tag} at address {addr}"),
            });
            switch (cell.Content)
            {
                case null:
                    w.WriteByte(SnapshotCellContent.Null);
                    break;
                case Pointer p:
                    w.WriteByte(SnapshotCellContent.Pointer);
                    w.WriteVarUInt(p.TargetAddr);
                    break;
                case SuspensionListNode chain:
                    w.WriteByte(SnapshotCellContent.SuspensionChain);
                    EncodeChain(w, chain, recordIndex);
                    break;
                case WriterContent wc:
                    w.WriteByte(SnapshotCellContent.WriterContent);
                    w.WriteVarUInt(wc.ReaderAddr);
                    EncodeChain(w, wc.Suspensions, recordIndex);
                    break;
                case RtTerm t:
                    w.WriteByte(SnapshotCellContent.Term);
                    EncodeTerm(w, t, moduleNameByProgram);
                    break;
                default:
                    // madGLP VariableEntry (imported variables) and any future
                    // content kind: not representable in format_version 1.
                    throw new SnapshotException(
                        $"heap cell at address {addr} holds unsupported content " +
                        $"{cell.Content.GetType().Name} (format_version 1 cannot capture it)");
            }
        }
        return w.TakeBytes();
    }

    private static void IndexChain(
        SuspensionListNode? node,
        Dictionary<SuspensionRecord, int> recordIndex,
        List<SuspensionRecord> records)
    {
        for (; node != null; node = node.Next)
        {
            if (!recordIndex.ContainsKey(node.Record))
            {
                recordIndex[node.Record] = records.Count;
                records.Add(node.Record);
            }
        }
    }

    private static void EncodeChain(
        ByteWriter w, SuspensionListNode? node, Dictionary<SuspensionRecord, int> recordIndex)
    {
        var indices = new List<int>();
        for (; node != null; node = node.Next)
            indices.Add(recordIndex[node.Record]);
        w.WriteVarUInt(indices.Count);
        foreach (var i in indices)
            w.WriteVarUInt(i);
    }

    private static void IndexMutualRefs(RtTerm term, Dictionary<int, int> mutualRefs)
    {
        switch (term)
        {
            case MutualRefTerm m:
                if (mutualRefs.TryGetValue(m.Id, out var existing) && existing != m.CurrentWriterAddr)
                    throw new SnapshotException(
                        $"MutualRef#{m.Id} observed with two writer addresses ({existing}, {m.CurrentWriterAddr})");
                mutualRefs[m.Id] = m.CurrentWriterAddr;
                break;
            case RtStructTerm s:
                foreach (var a in s.Args)
                    IndexMutualRefs(a, mutualRefs);
                break;
        }
    }

    internal static void EncodeTerm(
        ByteWriter w, RtTerm term, IReadOnlyDictionary<object, string> moduleNameByProgram)
    {
        switch (term)
        {
            case RtConstTerm ct:
                switch (ct.Value)
                {
                    case null:
                        w.WriteByte(SnapshotTermTag.ConstNull);
                        break;
                    case int i:
                        w.WriteByte(SnapshotTermTag.ConstInt32);
                        w.WriteInt64LE(i);
                        break;
                    case long l:
                        w.WriteByte(SnapshotTermTag.ConstInt64);
                        w.WriteInt64LE(l);
                        break;
                    case double d:
                        w.WriteByte(SnapshotTermTag.ConstDouble);
                        w.WriteDoubleBits(d);
                        break;
                    case string s:
                        w.WriteByte(SnapshotTermTag.ConstString);
                        w.WriteString(s);
                        break;
                    case bool b:
                        w.WriteByte(SnapshotTermTag.ConstBool);
                        w.WriteByte((byte)(b ? 1 : 0));
                        break;
                    default:
                        throw new SnapshotException(
                            $"unsupported ConstTerm value type {ct.Value.GetType().FullName}");
                }
                break;
            case RtStructTerm st:
                w.WriteByte(SnapshotTermTag.Struct);
                w.WriteString(st.Functor);
                w.WriteVarUInt(st.Args.Count);
                foreach (var a in st.Args)
                    EncodeTerm(w, a, moduleNameByProgram);
                break;
            case RtVarRef vr:
                w.WriteByte(SnapshotTermTag.VarRef);
                w.WriteVarUInt(vr.Addr);
                break;
            case MutualRefTerm m:
                w.WriteByte(SnapshotTermTag.MutualRef);
                w.WriteVarUInt(m.Id); // address lives in the mutual-ref table
                break;
            case ModuleTerm mt:
                w.WriteByte(SnapshotTermTag.Module);
                w.WriteString(ResolveModuleName(mt, moduleNameByProgram));
                break;
            default:
                throw new SnapshotException($"unsupported Term subtype {term.GetType().FullName}");
        }
    }

    private static string ResolveModuleName(
        ModuleTerm mt, IReadOnlyDictionary<object, string> moduleNameByProgram)
    {
        if (mt.Name.Length > 0)
            return mt.Name;
        if (moduleNameByProgram.TryGetValue(mt.Bytecode, out var name))
            return name;
        throw new SnapshotException("ModuleTerm with no name and an unmapped bytecode program");
    }

    /// <summary>
    /// Map merged-module bytecode OBJECTS back to module names by walking the
    /// heap's ModuleTerms (each carries its name) — the reverse of what restore
    /// rebuilds via GlpEngine.BuildMergedModuleBytecode.
    /// </summary>
    private static IReadOnlyDictionary<object, string> BuildModuleNameMap(GlpRuntimeEngine rt)
    {
        var map = new Dictionary<object, string>(ReferenceEqualityComparer.Instance);
        foreach (var cell in rt.Heap.Cells)
            if (cell.Content is ModuleTerm mt && mt.Name.Length > 0)
                map[mt.Bytecode] = mt.Name;
        return map;
    }

    // ------------------------------------------------------- 0x02 goal queue

    private static byte[] EncodeGoalQueue(GlpRuntimeEngine rt)
    {
        var w = new ByteWriter();
        var items = rt.Gq.Items.ToList();
        w.WriteVarUInt(items.Count);
        foreach (var g in items)
        {
            w.WriteVarUInt(g.Id);
            w.WriteVarUInt(g.Pc);
        }
        return w.TakeBytes();
    }

    // ------------------------------- 0x03 suspended index + per-goal tables

    private static byte[] EncodeSuspendedAndTables(
        GlpEngine engine, GlpRuntimeEngine rt, IReadOnlyDictionary<object, string> moduleNameByProgram)
    {
        var w = new ByteWriter();

        // Suspended index: readerAddr → goal refs.
        w.WriteVarUInt(rt.Suspended.Count);
        foreach (var (readerAddr, refs) in rt.Suspended.OrderBy(kv => kv.Key))
        {
            w.WriteVarUInt(readerAddr);
            w.WriteVarUInt(refs.Count);
            foreach (var g in refs.OrderBy(g => g.Id).ThenBy(g => g.Pc))
            {
                w.WriteVarUInt(g.Id);
                w.WriteVarUInt(g.Pc);
            }
        }

        // Per-goal CallEnvs.
        w.WriteVarUInt(rt.GoalEnvsView.Count);
        foreach (var (goalId, env) in rt.GoalEnvsView.OrderBy(kv => kv.Key))
        {
            w.WriteVarUInt(goalId);
            w.WriteVarUInt(env.Args.Count);
            foreach (var (slot, term) in env.Args.OrderBy(kv => kv.Key))
            {
                w.WriteVarUInt(slot);
                EncodeTerm(w, term, moduleNameByProgram);
            }
        }

        // Per-goal program keys.
        w.WriteVarUInt(rt.GoalProgramsView.Count);
        foreach (var (goalId, program) in rt.GoalProgramsView.OrderBy(kv => kv.Key))
        {
            w.WriteVarUInt(goalId);
            switch (program)
            {
                case null:
                    w.WriteByte(SnapshotProgramKey.None);
                    break;
                case string s:
                    w.WriteByte(SnapshotProgramKey.Name);
                    w.WriteString(s);
                    break;
                case GlpRuntime.Bytecode.BytecodeProgram bp when ReferenceEquals(bp, engine.ServeBytecode):
                    w.WriteByte(SnapshotProgramKey.Serve);
                    break;
                case GlpRuntime.Bytecode.BytecodeProgram bp when moduleNameByProgram.TryGetValue(bp, out var name):
                    w.WriteByte(SnapshotProgramKey.Module);
                    w.WriteString(name);
                    break;
                default:
                    throw new SnapshotException(
                        $"goal {goalId} has an unmappable program key ({program.GetType().Name}) — " +
                        "format_version 1 cannot capture it");
            }
        }

        // Per-goal module contexts (rebuilt deterministically from the module name).
        var contexts = rt.GoalModuleContextsView.Where(kv => kv.Value != null).ToList();
        w.WriteVarUInt(contexts.Count);
        foreach (var (goalId, ctx) in contexts.OrderBy(kv => kv.Key))
        {
            if (ctx is not GlpRuntime.Bytecode.ReplModuleContext rmc)
                throw new SnapshotException(
                    $"goal {goalId} has an unsupported module-context type {ctx!.GetType().Name}");
            w.WriteVarUInt(goalId);
            w.WriteString(rmc.ModuleName);
        }

        // Per-goal tail-recursion budgets.
        w.WriteVarUInt(rt.BudgetsView.Count);
        foreach (var (goalId, budget) in rt.BudgetsView.OrderBy(kv => kv.Key))
        {
            w.WriteVarUInt(goalId);
            w.WriteVarUInt(budget);
        }

        return w.TakeBytes();
    }

    // ----------------------------------------------------- 0x04 id counters

    private static byte[] EncodeCounters(GlpEngine engine, GlpRuntimeEngine rt)
    {
        var w = new ByteWriter();
        w.WriteVarUInt(engine.NextReplGoalId);
        w.WriteVarUInt(rt.NextGoalId);
        return w.TakeBytes();
    }

    // --------------------------------------------------------- 0x05 IL units

    private static byte[] EncodeUnits(string rootSelfSource, IReadOnlyList<LoadedUnit> units)
    {
        var w = new ByteWriter();
        w.WriteString(rootSelfSource); // restore-time prelude-drift integrity check
        w.WriteVarUInt(units.Count);
        foreach (var u in units)
        {
            w.WriteString(u.Name);
            w.WriteString(u.Source);
        }
        return w.TakeBytes();
    }

    // ----------------------------------------------------------- 0x06 timers

    private static byte[] EncodeTimers(GlpRuntimeEngine rt, IReadOnlyList<DisarmedTimer> timers)
    {
        var w = new ByteWriter();
        w.WriteVarUInt(rt.WaitReadersView.Count);
        foreach (var (goalId, readerId) in rt.WaitReadersView.OrderBy(kv => kv.Key))
        {
            w.WriteVarUInt(goalId);
            w.WriteVarUInt(readerId);
        }
        w.WriteVarUInt(timers.Count);
        foreach (var t in timers.OrderBy(t => t.WriterAddr))
        {
            w.WriteVarUInt(t.WriterAddr);
            w.WriteInt64LE(t.RemainingMs);
        }
        return w.TakeBytes();
    }

    // -------------------------------------------- 0x07 infrastructure goals

    private static byte[] EncodeInfraIds(GlpRuntimeEngine rt)
    {
        var w = new ByteWriter();
        w.WriteVarUInt(rt.InfrastructureGoalIds.Count);
        foreach (var id in rt.InfrastructureGoalIds.OrderBy(i => i))
            w.WriteVarUInt(id);
        return w.TakeBytes();
    }

    // --------------------------------------------------- 0x08 GLP channels

    private static byte[] EncodeChannels(GlpRuntimeEngine rt)
    {
        var w = new ByteWriter();
        w.WriteVarUInt(rt.GlpChannels.Count);
        foreach (var (name, handle) in rt.GlpChannels.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        {
            w.WriteString(name);
            w.WriteVarUInt(handle.WriterAddr);
        }
        return w.TakeBytes();
    }

    // ----------------------------------------------- 0x09 link definitions

    private static byte[] EncodeLinks(
        LinkRuntime? linkRuntime, IReadOnlyList<RestoredLinkDefinition>? outstandingLinks)
    {
        var w = new ByteWriter();
        var handles = linkRuntime?.Links.Handles
            ?? (IReadOnlyDictionary<GlpRuntime.Link.Seam.LinkId, LinkHandle>)
               new Dictionary<GlpRuntime.Link.Seam.LinkId, LinkHandle>();
        // Un-re-established definitions from a prior restore stay DURABLE: a link
        // whose peer was unreachable this incarnation is re-emitted verbatim so
        // the NEXT restore retries it — never silently dropped from 0x09
        // (US4 edge case; codexreview 20260730T070051Z).
        var carried = (outstandingLinks ?? Array.Empty<RestoredLinkDefinition>())
            .Where(d => !handles.ContainsKey(d.Id))
            .ToList();

        w.WriteVarUInt(handles.Count + carried.Count);
        foreach (var (id, handle) in handles)
        {
            // Role first: restore cannot re-establish (listen vs connect) without it
            // (contracts/snapshot-store.md 0x09: "LinkId, role, endpoint params, cursor
            // positions"). A handle without a stamped role is un-re-establishable —
            // loud-fail rather than persist a snapshot that cannot restore (FR-014).
            w.WriteByte(handle.Role switch
            {
                GlpRuntime.Link.Seam.LinkRole.Listener => (byte)0,
                GlpRuntime.Link.Seam.LinkRole.Connector => (byte)1,
                null => throw new SnapshotException(
                    $"link {id} has no recorded establishment role — cannot persist a re-establishable definition"),
                _ => throw new SnapshotException($"link {id} has unknown role {handle.Role}"),
            });
            w.WriteString(id.Scheme.Name);
            w.WriteString(id.Endpoint.Host);
            WriteNullableAddr(w, id.Endpoint.Port);
            if (id.Nonce.IsInteger)
            {
                w.WriteByte(0);
                w.WriteInt64LE(id.Nonce.IntValue);
            }
            else
            {
                w.WriteByte(1);
                w.WriteString(id.Nonce.StringValue!);
            }
            WriteNullableAddr(w, handle.InWriterAddr);
            WriteNullableAddr(w, handle.OutReaderAddr);
            WriteNullableAddr(w, handle.FaultsWriterAddr);
            w.WriteVarUInt(handle.MonitorCursors.Count);
            foreach (var c in handle.MonitorCursors)
                w.WriteVarUInt(c);
            w.WriteVarUInt(handle.EgressShippedCount); // restore-resume egress cursor (FR-032)
        }
        foreach (var def in carried)
        {
            w.WriteByte(def.Role switch
            {
                GlpRuntime.Link.Seam.LinkRole.Listener => (byte)0,
                GlpRuntime.Link.Seam.LinkRole.Connector => (byte)1,
                _ => throw new SnapshotException($"carried link {def.Id} has unknown role {def.Role}"),
            });
            w.WriteString(def.Id.Scheme.Name);
            w.WriteString(def.Id.Endpoint.Host);
            WriteNullableAddr(w, def.Id.Endpoint.Port);
            if (def.Id.Nonce.IsInteger)
            {
                w.WriteByte(0);
                w.WriteInt64LE(def.Id.Nonce.IntValue);
            }
            else
            {
                w.WriteByte(1);
                w.WriteString(def.Id.Nonce.StringValue!);
            }
            WriteNullableAddr(w, def.InWriterAddr);
            WriteNullableAddr(w, def.OutReaderAddr);
            WriteNullableAddr(w, def.FaultsWriterAddr);
            w.WriteVarUInt(def.MonitorCursors.Count);
            foreach (var c in def.MonitorCursors)
                w.WriteVarUInt(c);
            w.WriteVarUInt(def.EgressShippedCount);
        }
        return w.TakeBytes();
    }

    private static void WriteNullableAddr(ByteWriter w, int? addr)
    {
        if (addr is int a)
        {
            w.WriteByte(1);
            w.WriteVarUInt(a);
        }
        else
        {
            w.WriteByte(0);
        }
    }
}

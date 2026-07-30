// SnapshotRestore — rebuild a runtime from a format_version-1 blob (T019).
//
// Restore order (FR-030 — nothing serves until everything is back):
//   0. Section completeness check (RequireAllSections).
//   1. Prelude integrity: the snapshot's root-self source must match the
//      engine's programs/self.glp byte-for-byte (verbatim heap addresses are
//      only valid against the same prelude).
//   2. Reload IL units from their captured source with activation SUPPRESSED —
//      activation side effects (channels, serve goals, ModuleTerm cells) are
//      already in the snapshotted heap; re-running them would corrupt it.
//   3. Heap cells verbatim (FR-011), suspension records rebuilt with their
//      sharing preserved, MutualRefs rebuilt identity-preserving.
//   4. Per-goal tables, suspended index, counters, infrastructure ids,
//      channels, runners (serve + merged module bytecode, identity-consistent
//      with the restored ModuleTerm cells).
//   5. Goal queue.
//   6. Timers re-armed with their snapshotted REMAINING durations (FR-015) —
//      last, because a zero-remaining timer fires immediately against the
//      restored heap.
//   7. Link re-establishment: arrives with US4 (T031 RewireHandle + T032
//      restore-order gating). Until then a snapshot carrying live link
//      definitions is refused loudly — an honest refusal, never a silent
//      partial restore.

using GlpRuntime.Bytecode;
using GlpRuntime.Engine;
using GlpRuntime.Link.Seam;
using GlpRuntime.ResultCodec;
using GlpRuntime.Runtime;

using RtTerm = GlpRuntime.Runtime.Term;
using RtConstTerm = GlpRuntime.Runtime.ConstTerm;
using RtStructTerm = GlpRuntime.Runtime.StructTerm;
using RtVarRef = GlpRuntime.Runtime.VarRef;

namespace GlpRuntime.EngineHost.Snapshot;

/// <summary>
/// One durable link definition decoded from snapshot section 0x09 (US4/T032):
/// everything the re-wire path needs to re-establish the transport per the
/// recorded role and adopt the restored cursors (contracts/snapshot-store.md).
/// </summary>
public sealed record RestoredLinkDefinition(
    LinkId Id,
    LinkRole Role,
    int? InWriterAddr,
    int? OutReaderAddr,
    int? FaultsWriterAddr,
    IReadOnlyList<int> MonitorCursors);

/// <summary>
/// A restored engine + the reloaded unit list (the host re-records it for future
/// snapshots) + the link definitions awaiting re-establishment (US4: the caller
/// hands them to the rewirer AFTER the link layer is installed — T032 order).
/// </summary>
public sealed record RestoredEngine(
    GlpEngine Engine,
    IReadOnlyList<LoadedUnit> Units,
    IReadOnlyList<RestoredLinkDefinition> Links);

public static class SnapshotRestore
{
    /// <summary>
    /// Restore a fresh engine from <paramref name="blob"/>. The caller holds the
    /// session in `restoring` until this returns (wire rule 4 / FR-030).
    /// </summary>
    public static RestoredEngine Restore(SnapshotBlob blob, string rootSelfGlpPath)
    {
        blob.RequireAllSections(); // FR-030 — completeness before anything else

        // ---- 0x05: prelude integrity + unit reload (activation suppressed) ----
        var unitsReader = new ByteReader(blob.Section(SnapshotSection.LoadedIlUnits));
        string snapshotRootSelf = unitsReader.ReadString();
        string currentRootSelf = File.Exists(rootSelfGlpPath) ? File.ReadAllText(rootSelfGlpPath) : "";
        if (!string.Equals(snapshotRootSelf, currentRootSelf, StringComparison.Ordinal))
            throw new SnapshotException(
                "prelude drift: the snapshot was taken against a different programs/self.glp — " +
                "verbatim heap state cannot be restored against a changed prelude");

        var engine = new GlpEngine(rootSelfGlpPath);
        var rt = engine.Runtime;
        if (rt.Heap.Hp != 0 || rt.Heap.Cells.Count != 0)
            throw new SnapshotException(
                $"restore requires a fresh engine but the heap already holds {rt.Heap.Cells.Count} cell(s)");

        var units = new List<LoadedUnit>();
        int unitCount = unitsReader.ReadVarUInt();
        engine.SuppressActivation = true;
        try
        {
            for (int i = 0; i < unitCount; i++)
            {
                var name = unitsReader.ReadString();
                var source = unitsReader.ReadString();
                engine.LoadSource(source, filename: name);
                units.Add(new LoadedUnit(name, source));
            }
        }
        finally
        {
            engine.SuppressActivation = false;
        }

        // Merged module bytecode: ONE object per module, reused for ModuleTerm
        // cells, goal program keys, and runner registration — identity-consistent.
        var mergedByModule = new Dictionary<string, BytecodeProgram>(StringComparer.Ordinal);
        BytecodeProgram MergedFor(string moduleName)
        {
            if (!mergedByModule.TryGetValue(moduleName, out var p))
            {
                p = engine.BuildMergedModuleBytecode(moduleName);
                mergedByModule[moduleName] = p;
            }
            return p;
        }

        // ---- 0x01: heap, verbatim ----
        var heapReader = new ByteReader(blob.Section(SnapshotSection.HeapCells));
        int recordCount = heapReader.ReadVarUInt();
        var records = new List<SuspensionRecord>(recordCount);
        for (int i = 0; i < recordCount; i++)
        {
            long goalIdRaw = heapReader.ReadInt64LE();
            int resumePc = heapReader.ReadVarUInt();
            records.Add(new SuspensionRecord(
                goalIdRaw == long.MinValue ? null : checked((int)goalIdRaw), resumePc));
        }

        int mutualCount = heapReader.ReadVarUInt();
        var mutualRefs = new Dictionary<int, MutualRefTerm>();
        for (int i = 0; i < mutualCount; i++)
        {
            int id = heapReader.ReadVarUInt();
            int addr = heapReader.ReadVarUInt();
            mutualRefs[id] = new MutualRefTerm(addr, id); // 061 restore ctor — Id preserved
        }

        int hp = heapReader.ReadVarUInt();
        for (int addr = 0; addr < hp; addr++)
        {
            byte tagByte = heapReader.ReadByte();
            var tag = tagByte switch
            {
                0 => HeapCellTag.WrtTag,
                1 => HeapCellTag.RoTag,
                2 => HeapCellTag.ValueTag,
                _ => throw new SnapshotException($"corrupt snapshot: unknown cell tag {tagByte} at address {addr}"),
            };
            byte content = heapReader.ReadByte();
            object? value = content switch
            {
                SnapshotCellContent.Null => null,
                SnapshotCellContent.Pointer => new Pointer(heapReader.ReadVarUInt()),
                SnapshotCellContent.SuspensionChain => DecodeChain(heapReader, records),
                SnapshotCellContent.WriterContent => DecodeWriterContent(heapReader, records),
                SnapshotCellContent.Term => DecodeTerm(heapReader, mutualRefs, MergedFor),
                _ => throw new SnapshotException(
                    $"corrupt snapshot: unknown cell content variant 0x{content:X2} at address {addr}"),
            };
            rt.Heap.Cells.Add(new HeapCell(value, tag));
        }
        rt.Heap.Hp = hp;
        if (!heapReader.AtEnd)
            throw new SnapshotException("corrupt snapshot: trailing bytes in the heap section");

        // ---- 0x03: suspended index + per-goal tables ----
        var tables = new ByteReader(blob.Section(SnapshotSection.SuspendedGoals));
        int suspendedCount = tables.ReadVarUInt();
        rt.Suspended.Clear();
        for (int i = 0; i < suspendedCount; i++)
        {
            int readerAddr = tables.ReadVarUInt();
            int refCount = tables.ReadVarUInt();
            var set = new HashSet<GoalRef>();
            for (int j = 0; j < refCount; j++)
                set.Add(new GoalRef(tables.ReadVarUInt(), tables.ReadVarUInt()));
            rt.Suspended[readerAddr] = set;
        }

        int envCount = tables.ReadVarUInt();
        for (int i = 0; i < envCount; i++)
        {
            int goalId = tables.ReadVarUInt();
            int slotCount = tables.ReadVarUInt();
            var slots = new Dictionary<int, RtTerm>();
            for (int j = 0; j < slotCount; j++)
            {
                int slot = tables.ReadVarUInt();
                slots[slot] = DecodeTerm(tables, mutualRefs, MergedFor);
            }
            rt.SetGoalEnv(goalId, new CallEnv(slots));
        }

        bool anyServeGoal = false;
        int programCount = tables.ReadVarUInt();
        for (int i = 0; i < programCount; i++)
        {
            int goalId = tables.ReadVarUInt();
            byte kind = tables.ReadByte();
            object? program = kind switch
            {
                SnapshotProgramKey.None => null,
                SnapshotProgramKey.Name => tables.ReadString(),
                SnapshotProgramKey.Serve => engine.ServeBytecode,
                SnapshotProgramKey.Module => MergedFor(tables.ReadString()),
                _ => throw new SnapshotException($"corrupt snapshot: unknown program-key kind 0x{kind:X2}"),
            };
            if (kind == SnapshotProgramKey.Serve) anyServeGoal = true;
            rt.SetGoalProgram(goalId, program);
        }

        int ctxCount = tables.ReadVarUInt();
        for (int i = 0; i < ctxCount; i++)
        {
            int goalId = tables.ReadVarUInt();
            string moduleName = tables.ReadString();
            rt.SetGoalModuleContext(goalId, engine.BuildModuleContextForRestore(moduleName));
        }

        int budgetCount = tables.ReadVarUInt();
        for (int i = 0; i < budgetCount; i++)
        {
            int goalId = tables.ReadVarUInt();
            rt.SetBudget(goalId, tables.ReadVarUInt());
        }
        if (!tables.AtEnd)
            throw new SnapshotException("corrupt snapshot: trailing bytes in the suspended-goals section");

        // ---- 0x04: id counters ----
        var counters = new ByteReader(blob.Section(SnapshotSection.NextGoalId));
        engine.NextReplGoalId = counters.ReadVarUInt();
        rt.NextGoalId = counters.ReadVarUInt();

        // ---- 0x07: infrastructure goal ids ----
        var infra = new ByteReader(blob.Section(SnapshotSection.InfrastructureGoalIds));
        int infraCount = infra.ReadVarUInt();
        for (int i = 0; i < infraCount; i++)
            rt.InfrastructureGoalIds.Add(infra.ReadVarUInt());

        // ---- 0x08: GLP channels (writer cursors into the restored heap) ----
        var channels = new ByteReader(blob.Section(SnapshotSection.GlpChannels));
        int channelCount = channels.ReadVarUInt();
        for (int i = 0; i < channelCount; i++)
        {
            string name = channels.ReadString();
            int writerAddr = channels.ReadVarUInt();
            rt.GlpChannels[name] = new GlpChannelHandle(rt.Heap, writerAddr);
        }

        // Runners: identity-consistent registration for serve + module goals.
        if (anyServeGoal || channelCount > 0)
            rt.Runners[engine.ServeBytecode] = new BytecodeRunner(engine.ServeBytecode);
        foreach (var merged in mergedByModule.Values)
            rt.Runners[merged] = new BytecodeRunner(merged);

        // ---- 0x02: goal queue (usually empty at quiescence) ----
        var queue = new ByteReader(blob.Section(SnapshotSection.GoalQueue));
        int queueCount = queue.ReadVarUInt();
        for (int i = 0; i < queueCount; i++)
            rt.Gq.Enqueue(new GoalRef(queue.ReadVarUInt(), queue.ReadVarUInt()));

        // ---- 0x09: link definitions — decoded for the re-wire path (T031/T032).
        // Re-establishment itself runs AFTER the link layer is installed on the
        // restored engine (Program.cs order: restore → LinkKernels.Install →
        // LinkRewirer), so this section only decodes the durable definitions.
        var links = new ByteReader(blob.Section(SnapshotSection.LinkDefinitions));
        int linkCount = links.ReadVarUInt();
        var linkDefs = new List<RestoredLinkDefinition>(linkCount);
        for (int i = 0; i < linkCount; i++)
        {
            byte roleByte = links.ReadByte();
            var role = roleByte switch
            {
                0 => LinkRole.Listener,
                1 => LinkRole.Connector,
                _ => throw new SnapshotException(
                    $"corrupt snapshot: unknown link role 0x{roleByte:X2} in section 0x09"),
            };
            var scheme = LinkScheme.Of(links.ReadString());
            string host = links.ReadString();
            int? port = ReadNullableAddr(links);
            var nonce = links.ReadByte() switch
            {
                0 => LinkNonce.Int(links.ReadInt64LE()),
                1 => LinkNonce.Str(links.ReadString()),
                var b => throw new SnapshotException(
                    $"corrupt snapshot: unknown link nonce kind 0x{b:X2} in section 0x09"),
            };
            int? inWriter = ReadNullableAddr(links);
            int? outReader = ReadNullableAddr(links);
            int? faultsWriter = ReadNullableAddr(links);
            int cursorCount = links.ReadVarUInt();
            var cursors = new List<int>(cursorCount);
            for (int j = 0; j < cursorCount; j++)
                cursors.Add(links.ReadVarUInt());
            linkDefs.Add(new RestoredLinkDefinition(
                new LinkId(scheme, new LinkAddress(host, port), nonce),
                role, inWriter, outReader, faultsWriter, cursors));
        }
        if (!links.AtEnd)
            throw new SnapshotException("corrupt snapshot: trailing bytes in the link-definitions section");

        // ---- 0x06: timers, LAST (a zero-remaining timer fires immediately) ----
        var timers = new ByteReader(blob.Section(SnapshotSection.Timers));
        int waitCount = timers.ReadVarUInt();
        for (int i = 0; i < waitCount; i++)
        {
            int goalId = timers.ReadVarUInt();
            int readerId = timers.ReadVarUInt();
            rt.SetWaitReader(goalId, readerId);
        }
        int timerCount = timers.ReadVarUInt();
        for (int i = 0; i < timerCount; i++)
        {
            int writerAddr = timers.ReadVarUInt();
            long remaining = timers.ReadInt64LE();
            rt.IncrementPendingTimers();
            BytecodeRunner.StartGlpTimer((int)Math.Min(remaining, int.MaxValue), rt, writerAddr);
        }

        return new RestoredEngine(engine, units, linkDefs);
    }

    // ---------------------------------------------------------------- decode helpers

    private static int? ReadNullableAddr(ByteReader r) =>
        r.ReadByte() switch
        {
            0 => null,
            1 => r.ReadVarUInt(),
            var b => throw new SnapshotException(
                $"corrupt snapshot: invalid nullable-address flag 0x{b:X2} in section 0x09"),
        };

    private static SuspensionListNode? DecodeChain(ByteReader r, List<SuspensionRecord> records)
    {
        int count = r.ReadVarUInt();
        SuspensionListNode? head = null, tail = null;
        for (int i = 0; i < count; i++)
        {
            var node = new SuspensionListNode(RecordAt(r, records));
            if (head == null) head = tail = node;
            else { tail!.Next = node; tail = node; }
        }
        return head;
    }

    private static WriterContent DecodeWriterContent(ByteReader r, List<SuspensionRecord> records)
    {
        int readerAddr = r.ReadVarUInt();
        return new WriterContent(readerAddr, DecodeChain(r, records));
    }

    private static SuspensionRecord RecordAt(ByteReader r, List<SuspensionRecord> records)
    {
        int idx = r.ReadVarUInt();
        if (idx >= records.Count)
            throw new SnapshotException($"corrupt snapshot: suspension-record index {idx} out of range");
        return records[idx];
    }

    private static RtTerm DecodeTerm(
        ByteReader r,
        IReadOnlyDictionary<int, MutualRefTerm> mutualRefs,
        Func<string, BytecodeProgram> mergedFor)
    {
        byte tag = r.ReadByte();
        switch (tag)
        {
            case SnapshotTermTag.ConstNull:
                return new RtConstTerm(null);
            case SnapshotTermTag.ConstInt32:
                return new RtConstTerm(checked((int)r.ReadInt64LE()));
            case SnapshotTermTag.ConstInt64:
                return new RtConstTerm(r.ReadInt64LE());
            case SnapshotTermTag.ConstDouble:
                return new RtConstTerm(r.ReadDoubleBits());
            case SnapshotTermTag.ConstString:
                return new RtConstTerm(r.ReadString());
            case SnapshotTermTag.ConstBool:
                return new RtConstTerm(r.ReadByte() != 0);
            case SnapshotTermTag.Struct:
            {
                string functor = r.ReadString();
                int arity = r.ReadVarUInt();
                var args = new List<RtTerm>();
                for (int i = 0; i < arity; i++)
                    args.Add(DecodeTerm(r, mutualRefs, mergedFor));
                return new RtStructTerm(functor, args);
            }
            case SnapshotTermTag.VarRef:
                return new RtVarRef(r.ReadVarUInt());
            case SnapshotTermTag.MutualRef:
            {
                int id = r.ReadVarUInt();
                return mutualRefs.TryGetValue(id, out var m)
                    ? m
                    : throw new SnapshotException($"corrupt snapshot: MutualRef#{id} missing from the table");
            }
            case SnapshotTermTag.Module:
            {
                string moduleName = r.ReadString();
                return new ModuleTerm(mergedFor(moduleName), name: moduleName);
            }
            default:
                throw new SnapshotException($"corrupt snapshot: unknown term tag 0x{tag:X2}");
        }
    }
}

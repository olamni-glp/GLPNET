// Helper types and operations for madGLP
//
// Provides Globalize and Localize operations as specified in
// madGLP-spec.md Sections 5.1 and 5.2.
//
// These operations transform terms between local and global representations
// for inter-agent communication.

using System.Collections.Generic;
using System.Linq;
using GlpRuntime.Runtime;

namespace GlpRuntime.Multiagent
{
    /// <summary>Type of global variable name</summary>
    public enum GlobalNameType
    {
        /// <summary><c>_w(p, i)</c> - writer globalized at p</summary>
        Writer,

        /// <summary><c>_r(p, i)</c> - reader globalized at p</summary>
        Reader,
    }

    /// <summary>
    /// Global variable name: <c>_w(p, i)</c> or <c>_r(p, i)</c>
    /// <para>See: madGLP-spec.md Section 2</para>
    /// </summary>
    public sealed class GlobalName
    {
        public GlobalNameType Type { get; }
        public string Agent { get; }
        public int Index { get; }

        private GlobalName(GlobalNameType type, string agent, int index)
        {
            Type = type;
            Agent = agent;
            Index = index;
        }

        /// <summary>Create a writer global name <c>_w(agent, index)</c></summary>
        public static GlobalName Writer(string agent, int index) =>
            new GlobalName(GlobalNameType.Writer, agent, index);

        /// <summary>Create a reader global name <c>_r(agent, index)</c></summary>
        public static GlobalName Reader(string agent, int index) =>
            new GlobalName(GlobalNameType.Reader, agent, index);

        public bool IsWriter => Type == GlobalNameType.Writer;
        public bool IsReader => Type == GlobalNameType.Reader;

        public override string ToString() =>
            Type == GlobalNameType.Writer
                ? $"_w({Agent}, {Index})"
                : $"_r({Agent}, {Index})";

        public override bool Equals(object? other) =>
            other is GlobalName g &&
            Type == g.Type &&
            Agent == g.Agent &&
            Index == g.Index;

        public override int GetHashCode() => HashCode.Combine(Type, Agent, Index);
    }

    /// <summary>
    /// Information needed to spawn a global_send goal
    /// <para>Represents the goal: <c>global_send(readerAddr, globalName, destAgent)</c></para>
    /// <para>See: madGLP-spec.md Section 4</para>
    /// </summary>
    public class GlobalSendSpawn
    {
        /// <summary>Address of the reader to watch (the ? end of the variable pair)</summary>
        public int ReaderAddr { get; }

        /// <summary>Global name identifying the link</summary>
        public GlobalName GlobalName { get; }

        /// <summary>Destination agent</summary>
        public string DestAgent { get; }

        public GlobalSendSpawn(int readerAddr, GlobalName globalName, string destAgent)
        {
            ReaderAddr = readerAddr;
            GlobalName = globalName;
            DestAgent = destAgent;
        }

        public override string ToString() =>
            $"GlobalSendSpawn(reader={ReaderAddr}, name={GlobalName}, dest={DestAgent})";
    }

    /// <summary>
    /// A variable reference in a term (for globalize/localize)
    /// <para>Represents either a writer (addr) or reader (addr with IsReader=true).</para>
    /// <para>Always carries both writer and reader addresses of the pair.</para>
    /// </summary>
    public class TermVar
    {
        public int Addr { get; }
        public bool IsReader { get; }

        /// <summary>Writer address of the variable pair</summary>
        public int WriterAddr { get; }

        /// <summary>Reader address of the variable pair</summary>
        public int ReaderAddr { get; }

        private TermVar(int addr, bool isReader, int writerAddr, int readerAddr)
        {
            Addr = addr;
            IsReader = isReader;
            WriterAddr = writerAddr;
            ReaderAddr = readerAddr;
        }

        /// <summary>Create a writer variable reference</summary>
        public static TermVar Writer(int addr, int readerAddr) =>
            new TermVar(addr: addr, isReader: false, writerAddr: addr, readerAddr: readerAddr);

        /// <summary>Create a reader variable reference</summary>
        public static TermVar Reader(int addr, int writerAddr) =>
            new TermVar(addr: addr, isReader: true, writerAddr: writerAddr, readerAddr: addr);

        public bool IsWriter => !IsReader;

        /// <summary>Get the paired reader address</summary>
        public int PairedReaderAddr => ReaderAddr;

        public override string ToString() => IsReader
            ? $"TermVar.reader({Addr}, writer={WriterAddr})"
            : $"TermVar.writer({Addr}, reader={ReaderAddr})";
    }

    /// <summary>
    /// Result of a Globalize operation
    /// <para>See: madGLP-spec.md Section 5.1</para>
    /// </summary>
    public class GlobalizeResult
    {
        /// <summary>Global names substituted for variables, in order of occurrence</summary>
        public IReadOnlyList<GlobalName> GlobalNames { get; }

        /// <summary>Spawns needed for reader variables</summary>
        public IReadOnlyList<GlobalSendSpawn> Spawns { get; }

        public GlobalizeResult(IReadOnlyList<GlobalName> globalNames, IReadOnlyList<GlobalSendSpawn> spawns)
        {
            GlobalNames = globalNames;
            Spawns = spawns;
        }
    }

    /// <summary>A fresh variable pair created during localization</summary>
    public class FreshPair
    {
        public int WriterAddr { get; }
        public int ReaderAddr { get; }

        public FreshPair(int writerAddr, int readerAddr)
        {
            WriterAddr = writerAddr;
            ReaderAddr = readerAddr;
        }

        public override string ToString() =>
            $"FreshPair(writer={WriterAddr}, reader={ReaderAddr})";
    }

    /// <summary>
    /// Result of a Localize operation
    /// <para>See: madGLP-spec.md Section 5.2</para>
    /// </summary>
    public class LocalizeResult
    {
        /// <summary>Fresh variable pairs created, one per global name</summary>
        public IReadOnlyList<FreshPair> FreshPairs { get; }

        /// <summary>
        /// What to substitute in the term: writer addr or reader addr per position.
        /// false = use writer (Y_q) for _w names, true = use reader (Z_q?) for _r names.
        /// PARALLEL-INDEXED with FreshPairs.
        /// </summary>
        public IReadOnlyList<bool> UseReader { get; }

        /// <summary>Spawns needed for _w(p,i) global names</summary>
        public IReadOnlyList<GlobalSendSpawn> Spawns { get; }

        public LocalizeResult(IReadOnlyList<FreshPair> freshPairs, IReadOnlyList<bool> useReader, IReadOnlyList<GlobalSendSpawn> spawns)
        {
            FreshPairs = freshPairs;
            UseReader = useReader;
            Spawns = spawns;
        }
    }

    // ============================================================================
    // Term Transformation Functions
    // ============================================================================

    /// <summary>
    /// Static host for madGLP helper operations (Globalize, Localize, and term transformations).
    /// </summary>
    public static class MadHelpers
    {
        /// <summary>
        /// Globalize operation.
        /// <para>
        /// Given agent p, remote agent q, and a list of variables occurring in term T,
        /// produces the globalized representation T_p↑.
        /// </para>
        /// <para>Spec Section 5.1:</para>
        /// <list type="bullet">
        /// <item>If Y is a writer: allocate index i, create entry (Y, q) at index i,
        ///   replace with _w(p, i). No goal is spawned.</item>
        /// <item>If Y? is a reader: allocate index i, replace with _r(p, i),
        ///   spawn global_send(Y?, _r(p,i), q). No entry is created.</item>
        /// </list>
        /// <para>Returns GlobalizeResult with global names and spawn info.</para>
        /// <para>Updates the GlobalWritersTable with entries for writers.</para>
        /// </summary>
        public static GlobalizeResult Globalize(
            IReadOnlyList<TermVar> variables,
            string localAgent,
            string remoteAgent,
            GlobalWritersTable table)
        {
            var globalNames = new List<GlobalName>();
            var spawns = new List<GlobalSendSpawn>();

            foreach (var v in variables)
            {
                if (v.IsWriter)
                {
                    // Writer: create entry, no spawn
                    // Spec: "allocate the next index i, create entry (Y, q) at index i
                    //        in W'_p, and replace Y in T_p↑ with _w(p, i).
                    //        No goal is spawned—p will receive the assignment on this link."
                    // Entry stores the writer address Y for later binding when value arrives.
                    var index = table.AddGlobalizeEntry(v.WriterAddr, remoteAgent);
                    globalNames.Add(GlobalName.Writer(localAgent, index));
                }
                else
                {
                    // Reader: spawn global_send, no entry
                    // Spec: "allocate the next index i, replace Y? in T_p↑ with _r(p, i),
                    //        and spawn global_send(Y?, _r(p,i), q). No entry is created—
                    //        the global_send goal handles outgoing communication."
                    var index = table.AllocateIndex();
                    var globalName = GlobalName.Reader(localAgent, index);
                    globalNames.Add(globalName);

                    // Spawn global_send(Y?, _r(p,i), q)
                    // Note: GlobalSendSpawn.readerAddr is used as the key for heap.onBind(),
                    // which is indexed by *writer* address. We pass writerAddr so the
                    // callback fires when bindVariable is called on Y.
                    spawns.Add(new GlobalSendSpawn(
                        readerAddr: v.WriterAddr,
                        globalName: globalName,
                        destAgent: remoteAgent));
                }
            }

            return new GlobalizeResult(globalNames: globalNames, spawns: spawns);
        }

        /// <summary>
        /// Localize operation.
        /// <para>
        /// Given agent q, remote agent p, and a list of global names from term T_p↑,
        /// produces the localized representation T_q↓.
        /// </para>
        /// <para>Spec Section 5.2:</para>
        /// <list type="bullet">
        /// <item>If _w(p, i): create fresh pair (Y_q, Y_q?), replace with Y_q (writer),
        ///   spawn global_send(Y_q?, _w(p,i), p). No entry is created.</item>
        /// <item>If _r(p, i): create fresh pair (Z_q, Z_q?), add entry (Z_q, p, i),
        ///   replace with Z_q? (reader). No goal is spawned.</item>
        /// </list>
        /// <para>Returns LocalizeResult with fresh pairs, usage info, and spawn info.</para>
        /// <para>Updates the GlobalWritersTable with entries for _r names.</para>
        /// <para>The <paramref name="freshAddrAllocator"/> function is called to allocate each fresh variable pair,
        /// returning (writerAddr, readerAddr).</para>
        /// </summary>
        public static LocalizeResult Localize(
            IReadOnlyList<GlobalName> globalNames,
            string localAgent,
            GlobalWritersTable table,
            Func<(int writerAddr, int readerAddr)> freshAddrAllocator)
        {
            var freshPairs = new List<FreshPair>();
            var useReader = new List<bool>();
            var spawns = new List<GlobalSendSpawn>();

            foreach (var gn in globalNames)
            {
                // Allocate fresh pair
                var (writerAddr, readerAddr) = freshAddrAllocator();
                var pair = new FreshPair(writerAddr, readerAddr);
                freshPairs.Add(pair);

                if (gn.IsWriter)
                {
                    // _w(p, i): spawn global_send, use writer
                    // Spec: "create fresh local pair (Y_q, Y_q?), replace _w(p, i) with
                    //        Y_q (the writer) in T_q↓, and spawn global_send(Y_q?, _w(p,i), p).
                    //        No entry is created—the global_send goal handles outgoing communication."
                    useReader.Add(false); // Use Y_q (writer)

                    // Spawn global_send(Y_q?, _w(p,i), p)
                    // When q assigns Y_q, Y_q? becomes known, gs fires and sends value to p.
                    // Note: GlobalSendSpawn.readerAddr is used as the key for heap.onBind(),
                    // which is indexed by *writer* address. We pass writerAddr so the callback
                    // fires when bindVariable(writerAddr, ...) is called.
                    spawns.Add(new GlobalSendSpawn(
                        readerAddr: writerAddr,
                        globalName: gn,
                        destAgent: gn.Agent)); // Send back to agent p who created the name
                }
                else
                {
                    // _r(p, i): create entry, use reader
                    // Spec: "create fresh local pair (Z_q, Z_q?), allocate the next index k
                    //        in W'_q, add entry (Z_q, p, i), and replace _r(p, i) with Z_q?
                    //        (the reader) in T_q↓. No goal is spawned—q will receive the
                    //        assignment on this link."
                    table.AddLocalizeEntry(writerAddr, gn.Agent, gn.Index);
                    useReader.Add(true); // Use Z_q? (reader)
                }
            }

            return new LocalizeResult(
                freshPairs: freshPairs,
                useReader: useReader,
                spawns: spawns);
        }

        /// <summary>
        /// Transform a term by replacing variables with their global names.
        /// <para>
        /// Takes the original term and the GlobalizeResult from Globalize().
        /// Returns a new term with VarRefs replaced by StructTerms representing
        /// global names (_w(agent, index) or _r(agent, index)).
        /// </para>
        /// </summary>
        public static Term GlobalizeTermWithResult(
            Term term,
            IReadOnlyList<TermVar> variables,
            GlobalizeResult result)
        {
            var varToGlobalName = new Dictionary<int, GlobalName>(variables.Count);
            for (int i = 0; i < variables.Count; i++)
            {
                varToGlobalName[variables[i].Addr] = result.GlobalNames[i];
            }
            return _SubstituteGlobalNames(term, varToGlobalName);
        }

        /// <summary>
        /// Extract global name structures from a term.
        /// <para>
        /// Finds all _w(agent, index) and _r(agent, index) structures in the term.
        /// Returns the list of GlobalNames in order of occurrence.
        /// </para>
        /// </summary>
        public static IReadOnlyList<GlobalName> ExtractGlobalNames(Term term)
        {
            var result = new List<GlobalName>();
            _ExtractGlobalNamesRecursive(term, result);
            return result;
        }

        /// <summary>
        /// Transform a term by replacing global names with local variables.
        /// <para>
        /// Takes the globalized term and the LocalizeResult from Localize().
        /// Returns a new term with global name structures replaced by VarRefs.
        /// </para>
        /// <remarks>
        /// KEY CASING CONTRACT: the composite key uses lower-case "writer"/"reader"
        /// (via ToLowerInvariant()) to match the literal strings used in _SubstituteLocalVars.
        /// </remarks>
        /// </summary>
        public static Term LocalizeTermWithResult(
            Term term,
            IReadOnlyList<GlobalName> globalNames,
            LocalizeResult result)
        {
            var globalNameToLocal = new Dictionary<string, int>();
            for (int i = 0; i < globalNames.Count; i++)
            {
                var gn = globalNames[i];
                var pair = result.FreshPairs[i];
                var useReader = result.UseReader[i];
                globalNameToLocal[$"{gn.Type.ToString().ToLowerInvariant()}:{gn.Agent}:{gn.Index}"] =
                    useReader ? pair.ReaderAddr : pair.WriterAddr;
            }
            return _SubstituteLocalVars(term, globalNameToLocal);
        }

        private static Term _SubstituteGlobalNames(Term term, Dictionary<int, GlobalName> mapping)
        {
            if (term is VarRef varRef)
            {
                if (mapping.TryGetValue(varRef.Addr, out var gn))
                {
                    var functor = gn.IsWriter ? "_w" : "_r";
                    return new StructTerm(functor, new List<Term> { new ConstTerm(gn.Agent), new ConstTerm(gn.Index) });
                }
                return term;
            }
            else if (term is StructTerm structTerm)
            {
                var newArgs = structTerm.Args.Select(a => _SubstituteGlobalNames(a, mapping)).ToList();
                return new StructTerm(structTerm.Functor, newArgs);
            }
            return term; // ConstTerm unchanged
        }

        private static void _ExtractGlobalNamesRecursive(Term term, List<GlobalName> result)
        {
            if (term is StructTerm structTerm)
            {
                if ((structTerm.Functor == "_w" || structTerm.Functor == "_r") && structTerm.Args.Count == 2)
                {
                    var agentArg = structTerm.Args[0];
                    var indexArg = structTerm.Args[1];
                    if (agentArg is ConstTerm agentConst && indexArg is ConstTerm indexConst)
                    {
                        var agent = (string)agentConst.Value;
                        var index = Convert.ToInt32(indexConst.Value);
                        result.Add(structTerm.Functor == "_w"
                            ? GlobalName.Writer(agent, index)
                            : GlobalName.Reader(agent, index));
                    }
                }
                else
                {
                    foreach (var arg in structTerm.Args)
                    {
                        _ExtractGlobalNamesRecursive(arg, result);
                    }
                }
            }
        }

        private static Term _SubstituteLocalVars(Term term, Dictionary<string, int> mapping)
        {
            if (term is StructTerm structTerm)
            {
                if ((structTerm.Functor == "_w" || structTerm.Functor == "_r") && structTerm.Args.Count == 2)
                {
                    var agentArg = structTerm.Args[0];
                    var indexArg = structTerm.Args[1];
                    if (agentArg is ConstTerm agentConst && indexArg is ConstTerm indexConst)
                    {
                        var type = structTerm.Functor == "_w" ? "writer" : "reader";
                        var key = $"{type}:{agentConst.Value}:{indexConst.Value}";
                        if (mapping.TryGetValue(key, out var localAddr))
                        {
                            return new VarRef(localAddr);
                        }
                    }
                }
                var newArgs = structTerm.Args.Select(a => _SubstituteLocalVars(a, mapping)).ToList();
                return new StructTerm(structTerm.Functor, newArgs);
            }
            return term;
        }
    }
}

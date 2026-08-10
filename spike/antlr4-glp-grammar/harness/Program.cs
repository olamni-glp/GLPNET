// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// Coverage + IL-parity harness for the antlr4-glp-grammar spike.
//   (no flags)           legacy accept/reject coverage table (feature 065, SC-001-coverage)
//   --parity [--corpus D] IL-parity over the 7-file spike corpus (default) or every *.glp under D;
//                         writes ../RESULTS.md (feature 069, SC-001/SC-002, FR-003/004/009)
//   --fuzz [--budget N]   bounded generative fuzz (feature 069 US2, T017/T018)

using System;
using System.Collections.Generic;
using System.IO;
using Antlr4.Runtime;
using GlpGrammarSpike.Parity;

namespace GlpGrammarSpike
{
    internal sealed class Counting : IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
    {
        public int Errors;
        public string First;
        public void SyntaxError(TextWriter o, IRecognizer r, int sym, int line, int col, string msg, RecognitionException e)
        { Errors++; if (First == null) First = line + ":" + col + " " + msg; }
        public void SyntaxError(TextWriter o, IRecognizer r, IToken sym, int line, int col, string msg, RecognitionException e)
        { Errors++; if (First == null) First = line + ":" + col + " " + msg; }
    }

    internal static class Program
    {
        private const string Toolchain = "dotnet 10.0.301; Antlr4.Runtime.Standard 4.13.1; ANTLR 4.13.2 (gen, -visitor); Java 17 (jdk-17.0.19+10)";

        // The 7-file spike corpus (MANIFEST.md); #7 is the parse-accept / semantic-reject negative control.
        private static readonly string[] SpikeCorpus =
        {
            "programs/tests/typed/append_dl.glp",
            "programs/tests/typed/arith_comparison.glp",
            "programs/tests/typed/arith_diseq.glp",
            "programs/tests/typed/arith_guard_ground.glp",
            "programs/tests/typed/abandon_stream.glp",
            "programs/tests/typed/cssg_precise/typed_social_agent.glp",
            "programs/tests/typed/abandon_reader_bad.glp",
        };

        private static int Main(string[] args)
        {
            string root = FindRepoRoot();
            if (root == null) { Console.Error.WriteLine("repo root (with programs/tests/typed) not found"); return 2; }

            bool parity = HasFlag(args, "--parity");
            bool fuzz = HasFlag(args, "--fuzz");
            string corpusDir = OptValue(args, "--corpus");

            string genIdx = OptValue(args, "--gen");
            if (genIdx != null && int.TryParse(genIdx, out int gi))
            {
                uint gseed = GlpGrammarSpike.Parity.GrammarFuzzer.DefaultSeed;
                string gss = OptValue(args, "--seed");
                if (gss != null && uint.TryParse(gss, out uint gsv)) gseed = gsv;
                var gen = GlpGrammarSpike.Parity.GrammarFuzzer.Generate(gi, gseed);
                Console.WriteLine("----- fuzz#" + gi + " (seed=" + gseed + ") -----");
                Console.WriteLine(gen.Source);
                var d = IlParityComparator.Diagnose(gen.Source, root);
                Console.WriteLine("ANTLR : parse=" + d.AParse + (d.AParseErr != null ? " [" + d.AParseErr + "]" : "") + "; compile=" + d.ACompile + (d.ACompErr != null ? " [" + d.ACompErr + "]" : ""));
                Console.WriteLine("hand  : parse=" + d.BParse + (d.BParseErr != null ? " [" + d.BParseErr + "]" : "") + "; compile=" + d.BCompile + (d.BCompErr != null ? " [" + d.BCompErr + "]" : ""));
                return 0;
            }

            if (fuzz)
            {
                int budget = 10000;
                string bs = OptValue(args, "--budget");
                if (bs != null && int.TryParse(bs, out int bv) && bv > 0) budget = bv;
                uint seed = GlpGrammarSpike.Parity.GrammarFuzzer.DefaultSeed;
                string ss = OptValue(args, "--seed");
                if (ss != null && uint.TryParse(ss, out uint sv)) seed = sv;
                return RunFuzz(root, budget, seed);
            }
            if (parity) return RunParity(root, corpusDir);
            return RunCoverage(root);
        }

        // ── IL parity (feature 069) ──────────────────────────────────────────────
        private static int RunParity(string root, string corpusDir)
        {
            // G1 static assertion: every Glp.g4 rule has a lowering method (contract, T012).
            GlpGrammarSpike.Bridge.GlpLoweringVisitor.AssertRuleCoverage();

            var inputs = new List<(string id, string path)>();
            if (!string.IsNullOrEmpty(corpusDir))
            {
                string dir = Path.IsPathRooted(corpusDir) ? corpusDir : Path.Combine(root, corpusDir);
                if (!Directory.Exists(dir)) { Console.Error.WriteLine("corpus dir not found: " + dir); return 2; }
                foreach (var f in Directory.GetFiles(dir, "*.glp", SearchOption.AllDirectories))
                    inputs.Add((Path.GetFileName(f), f));
                inputs.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            }
            else
            {
                foreach (var rel in SpikeCorpus)
                {
                    string p = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
                    inputs.Add((rel.Substring(rel.LastIndexOf('/') + 1), p));
                }
            }

            var results = new List<ParityResult>();
            foreach (var (id, path) in inputs)
            {
                if (!File.Exists(path)) { results.Add(new ParityResult { InputId = id, Verdict = Verdict.Diverge, Cause = "MISSING file" }); continue; }
                string src = File.ReadAllText(path);
                results.Add(IlParityComparator.Compare(id, src, root));
            }

            // Reviewable artifact (SC-006).
            string resultsPath = Path.Combine(root, "spike", "antlr4-glp-grammar", "RESULTS.md");
            string title = string.IsNullOrEmpty(corpusDir)
                ? "Representative corpus (7 files) — SC-001"
                : ("Expanded corpus (" + corpusDir + ") — SC-002");
            ResultsWriter.Write(resultsPath, title, results, Toolchain);

            // Console echo.
            Console.WriteLine("| # | input | verdict | first-diff | cause |");
            Console.WriteLine("|---|-------|---------|-----------|-------|");
            int i = 0, match = 0, unCaused = 0;
            foreach (var r in results)
            {
                i++;
                if (r.IsMatch) match++;
                if (!r.IsMatch && string.IsNullOrEmpty(r.Cause)) unCaused++;
                string diff = r.FirstDiffOffset >= 0 ? ("@" + r.FirstDiffOffset) : "";
                Console.WriteLine("| " + i + " | " + r.InputId + " | " + (r.IsMatch ? "MATCH" : "DIVERGE") + " | " + diff + " | " + (r.Cause ?? "") + " |");
            }
            Console.WriteLine();
            Console.WriteLine("IL parity: " + match + "/" + results.Count + " MATCH; un-caused divergences: " + unCaused + ".");
            Console.WriteLine("Results written to " + resultsPath);
            return (match == results.Count) ? 0 : 1;
        }

        // ── Bounded generative fuzz (feature 069 US2, T017/T018) ─────────────────
        // Deterministic (index+seed) valid-program generation through the SAME comparator; halt on the
        // first un-caused divergence with the reproducing input captured (FR-006, contract F2/F4).
        private static int RunFuzz(string root, int budget, uint seed)
        {
            // G1 static assertion: every Glp.g4 rule has a lowering method (contract, T012).
            GlpGrammarSpike.Bridge.GlpLoweringVisitor.AssertRuleCoverage();

            int matched = 0, bothReject = 0;
            ParityResult firstDiverge = null;
            string reproSrc = null;
            int reproIndex = -1;

            for (int i = 0; i < budget; i++)
            {
                var gen = GlpGrammarSpike.Parity.GrammarFuzzer.Generate(i, seed);
                var r = IlParityComparator.Compare(gen.Id, gen.Source, root);
                if (r.IsMatch)
                {
                    // A "both reject" MATCH means the generated program was invalid — count it separately so
                    // a low valid-yield (fuzz not actually exercising IL) is visible, never silently hidden.
                    if (r.Cause != null && r.Cause.StartsWith("both reject")) bothReject++;
                    else matched++;
                    continue;
                }
                firstDiverge = r; reproSrc = gen.Source; reproIndex = i;
                break; // halt-on-divergence (F4)
            }

            string resultsPath = Path.Combine(root, "spike", "antlr4-glp-grammar", "RESULTS.md");
            const string title = "Bounded fuzz — SC-003";

            if (firstDiverge != null)
            {
                string dir = Path.Combine(root, "spike", "antlr4-glp-grammar", "fuzz-repro");
                Directory.CreateDirectory(dir);
                string reproPath = Path.Combine(dir, "fuzz-" + reproIndex + ".glp");
                File.WriteAllText(reproPath, reproSrc);
                ResultsWriter.WriteFuzz(resultsPath, title, budget, matched, bothReject, reproIndex, firstDiverge, seed, reproPath, Toolchain);

                string off = firstDiverge.FirstDiffOffset >= 0 ? (" @" + firstDiverge.FirstDiffOffset) : "";
                Console.WriteLine("FUZZ DIVERGENCE at index " + reproIndex + ": " + firstDiverge.Cause + off);
                Console.WriteLine("Repro written to " + reproPath);
                Console.WriteLine("valid IL-parity MATCH before halt: " + matched + "; both-reject (invalid gen): " + bothReject
                    + "; budget consumed: " + (reproIndex + 1) + "/" + budget);
                Console.WriteLine("Results written to " + resultsPath);
                return 1;
            }

            ResultsWriter.WriteFuzz(resultsPath, title, budget, matched, bothReject, -1, null, seed, null, Toolchain);
            Console.WriteLine("Bounded fuzz complete: budget=" + budget + ", seed=" + seed + ".");
            Console.WriteLine("valid IL-parity MATCH: " + matched + "; both-reject (invalid gen): " + bothReject + "; un-caused divergences: 0.");
            Console.WriteLine("Results written to " + resultsPath);
            return 0;
        }

        // ── Legacy accept/reject coverage (feature 065) ──────────────────────────
        private static int RunCoverage(string root)
        {
            Console.WriteLine("| # | corpus file | ANTLR | hand | parity |");
            Console.WriteLine("|---|-------------|-------|------|--------|");
            int i = 0, agree = 0;
            foreach (var rel in SpikeCorpus)
            {
                i++;
                string path = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path)) { Console.WriteLine("| " + i + " | " + rel + " | MISSING | MISSING | - |"); continue; }
                string src = File.ReadAllText(path);
                bool antlrOk = ParseAntlr(src);
                bool handOk = ParseHand(src);
                bool parity = antlrOk == handOk;
                if (parity) agree++;
                string name = rel.Substring(rel.LastIndexOf('/') + 1);
                Console.WriteLine("| " + i + " | " + name + " | " + (antlrOk ? "accept" : "reject") + " | " + (handOk ? "accept" : "reject") + " | " + (parity ? "MATCH" : "DIVERGE") + " |");
            }
            Console.WriteLine();
            Console.WriteLine("SC-001 coverage: " + agree + "/" + SpikeCorpus.Length + " accept/reject parity.");
            return agree == SpikeCorpus.Length ? 0 : 1;
        }

        private static bool ParseAntlr(string src)
        {
            var lexer = new GlpLexer(CharStreams.fromString(src));
            var listener = new Counting();
            lexer.RemoveErrorListeners(); lexer.AddErrorListener(listener);
            var parser = new GlpParser(new CommonTokenStream(lexer));
            parser.RemoveErrorListeners(); parser.AddErrorListener(listener);
            try { parser.module(); } catch (Exception) { return false; }
            return listener.Errors == 0;
        }

        private static bool ParseHand(string src)
        {
            try
            {
                var toks = new GlpRuntime.Compiler.Lexer(src).Tokenize();
                var _ = new GlpRuntime.Compiler.Parser(toks).ParseModule();
                return true;
            }
            catch (Exception) { return false; }
        }

        private static bool HasFlag(string[] args, string flag)
        {
            foreach (var a in args) if (a == flag) return true;
            return false;
        }

        private static string OptValue(string[] args, string opt)
        {
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == opt) return args[i + 1];
            return null;
        }

        private static string FindRepoRoot()
        {
            var d = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (d != null)
            {
                if (Directory.Exists(Path.Combine(d.FullName, "programs", "tests", "typed"))) return d.FullName;
                d = d.Parent;
            }
            return null;
        }
    }
}

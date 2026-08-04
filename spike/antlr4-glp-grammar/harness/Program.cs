// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// Coverage harness for the antlr4-glp-grammar spike (T012/T013).
// For each corpus file, parse with (a) the ANTLR-generated GlpParser and (b) the
// production hand-written parser (ground truth), and report accept/reject parity
// per example (SC-001 coverage). IL parity (SC-002) additionally needs an
// ANTLR-tree -> engine-AST lowering bridge — its cost is assessed in REPORT.md.

using System;
using System.Collections.Generic;
using System.IO;
using Antlr4.Runtime;

// Disambiguate: `Parser` is ambiguous (Antlr4.Runtime.Parser vs the engine's
// GlpRuntime.Compiler.Parser) — always fully-qualify the engine types below.

namespace GlpGrammarSpike
{
    // Error listener that counts syntax errors from both lexer (int) and parser (IToken).
    internal sealed class Counting : IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
    {
        public int Errors;
        public string First;
        public void SyntaxError(TextWriter o, IRecognizer r, int sym, int line, int col, string msg, RecognitionException e)
        { Errors++; First ??= $"{line}:{col} {msg}"; }
        public void SyntaxError(TextWriter o, IRecognizer r, IToken sym, int line, int col, string msg, RecognitionException e)
        { Errors++; First ??= $"{line}:{col} {msg}"; }
    }

    internal static class Program
    {
        // Corpus (MANIFEST.md); #7 is the negative control (hand-parser/checker rejects).
        private static readonly (string rel, bool negControl)[] Corpus = new[]
        {
            ("programs/tests/typed/append_dl.glp", false),
            ("programs/tests/typed/arith_comparison.glp", false),
            ("programs/tests/typed/arith_diseq.glp", false),
            ("programs/tests/typed/arith_guard_ground.glp", false),
            ("programs/tests/typed/abandon_stream.glp", false),
            ("programs/tests/typed/cssg_precise/typed_social_agent.glp", false),
            ("programs/tests/typed/abandon_reader_bad.glp", true),
        };

        private static int Main(string[] args)
        {
            string root = args.Length > 0 ? args[0] : FindRepoRoot();
            if (root == null) { Console.Error.WriteLine("repo root (with programs/tests/typed) not found"); return 2; }

            Console.WriteLine("| # | corpus file | neg? | ANTLR | hand | parity |");
            Console.WriteLine("|---|-------------|------|-------|------|--------|");

            int i = 0, agree = 0;
            foreach (var (rel, neg) in Corpus)
            {
                i++;
                string path = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path)) { Console.WriteLine($"| {i} | {rel} | - | MISSING | MISSING | - |"); continue; }
                string src = File.ReadAllText(path);

                string antlrErr;
                bool antlrOk = ParseAntlr(src, out antlrErr);
                bool handOk = ParseHand(src);
                bool parity = antlrOk == handOk;
                if (parity) agree++;
                string name = rel.Substring(rel.LastIndexOf('/') + 1);
                Console.WriteLine($"| {i} | {name} | {(neg ? "yes" : "")} | {(antlrOk ? "accept" : "reject")} | {(handOk ? "accept" : "reject")} | {(parity ? "MATCH" : "DIVERGE")} |");
                if (!parity && antlrErr != null) Console.WriteLine($"    ANTLR first error: {antlrErr}");
            }

            Console.WriteLine();
            Console.WriteLine($"SC-001 coverage: {agree}/{Corpus.Length} accept/reject parity (ANTLR vs hand-written).");
            return agree == Corpus.Length ? 0 : 1;
        }

        private static bool ParseAntlr(string src, out string firstErr)
        {
            var input = CharStreams.fromString(src);
            var lexer = new GlpLexer(input);
            var listener = new Counting();
            lexer.RemoveErrorListeners(); lexer.AddErrorListener(listener);
            var tokens = new CommonTokenStream(lexer);
            var parser = new GlpParser(tokens);
            parser.RemoveErrorListeners(); parser.AddErrorListener(listener);
            try { parser.module(); }
            catch (Exception e) { firstErr = e.Message; return false; }
            firstErr = listener.First;
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

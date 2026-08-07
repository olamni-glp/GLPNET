// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// IlParityComparator (feature 069, T005) — the IL-parity oracle (contract parity-oracle-contract.md).
// For one .glp source, run BOTH front-ends through the IDENTICAL PipelineDriver, serialize each
// BytecodeProgram with glp_il_codec, and byte-compare (FR-003/FR-004):
//   side A: ANTLR GlpParser -> GlpLoweringVisitor.Lower -> PipelineDriver.CompileModule
//   side B: production Lexer/Parser.ParseModule            -> PipelineDriver.CompileModule
// Byte-identity is the standard (P1); a DIVERGE carries the first differing offset (P2); acceptance
// must agree on both front-ends (P4); no divergence is silently accepted (P3 — an un-caused DIVERGE
// is a defect the caller records and diagnoses).

using System;
using Antlr4.Runtime;
using GlpGrammarSpike.Bridge;
using GlpRuntime.Bytecode;
using GlpRuntime.IlCodec;
using Compiler = GlpRuntime.Compiler;

namespace GlpGrammarSpike.Parity
{
    public enum Verdict { Match, Diverge }

    public sealed class ParityResult
    {
        public string InputId;
        public Verdict Verdict;
        public int FirstDiffOffset = -1;   // byte offset of first difference (DIVERGE only), else -1
        public string Cause;               // set for MATCH-notes and for DIVERGE explanations
        public bool IsMatch => Verdict == Verdict.Match;
    }

    // ANTLR error listener that counts lexer (int) + parser (IToken) syntax errors.
    internal sealed class CountingListener : IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
    {
        public int Errors;
        public string First;
        public void SyntaxError(System.IO.TextWriter o, IRecognizer r, int sym, int line, int col, string msg, RecognitionException e)
        { Errors++; if (First == null) First = line + ":" + col + " " + msg; }
        public void SyntaxError(System.IO.TextWriter o, IRecognizer r, IToken sym, int line, int col, string msg, RecognitionException e)
        { Errors++; if (First == null) First = line + ":" + col + " " + msg; }
    }

    public static class IlParityComparator
    {
        public static ParityResult Compare(string inputId, string source, string repoRoot)
        {
            PipelineDriver.EnsureInitialized(repoRoot);

            // --- Parse level (P4: acceptance must agree) ---
            string aErr, bErr;
            GlpParser.ModuleContext aCtx = ParseAntlr(source, out aErr);
            bool aParsed = aCtx != null;
            Compiler.Module bMod = ParseHand(source, out bErr);
            bool bParsed = bMod != null;

            if (aParsed != bParsed)
                return Diverge(inputId, "parse-acceptance divergence (ANTLR=" + Acc(aParsed) + ", hand=" + Acc(bParsed) + "): " + (aErr ?? bErr));
            if (!aParsed)
                return Match(inputId, "both reject at parse");

            // --- Downstream compile (P4: identical rejection required) ---
            BytecodeProgram ilA = TryCompileA(aCtx, out aErr);
            BytecodeProgram ilB = TryCompileB(bMod, out bErr);
            bool aC = ilA != null, bC = ilB != null;

            if (aC != bC)
                return Diverge(inputId, "downstream-acceptance divergence (ANTLR=" + Acc(aC) + ", hand=" + Acc(bC) + "): " + (aErr ?? bErr));
            if (!aC)
                return Match(inputId, "both reject downstream");

            // --- Byte-identical IL (P1/P2) ---
            byte[] ba = IlCodec.Encode(ilA, null);   // variableMap=null => canonical bytes
            byte[] bb = IlCodec.Encode(ilB, null);
            int diff = FirstDiff(ba, bb);
            if (diff < 0)
                return Match(inputId, null);

            var res = Diverge(inputId, "IL bytes differ (lenA=" + ba.Length + ", lenB=" + bb.Length + ")");
            res.FirstDiffOffset = diff;
            return res;
        }

        private static GlpParser.ModuleContext ParseAntlr(string src, out string firstErr)
        {
            var input = CharStreams.fromString(src);
            var lexer = new GlpLexer(input);
            var listener = new CountingListener();
            lexer.RemoveErrorListeners(); lexer.AddErrorListener(listener);
            var tokens = new CommonTokenStream(lexer);
            var parser = new GlpParser(tokens);
            parser.RemoveErrorListeners(); parser.AddErrorListener(listener);
            GlpParser.ModuleContext ctx;
            try { ctx = parser.module(); }
            catch (Exception e) { firstErr = e.Message; return null; }
            firstErr = listener.First;
            return listener.Errors == 0 ? ctx : null;
        }

        private static Compiler.Module ParseHand(string src, out string firstErr)
        {
            try
            {
                var toks = new Compiler.Lexer(src).Tokenize();
                firstErr = null;
                return new Compiler.Parser(toks).ParseModule();
            }
            catch (Exception e) { firstErr = e.Message; return null; }
        }

        private static BytecodeProgram TryCompileA(GlpParser.ModuleContext ctx, out string err)
        {
            try
            {
                var module = new GlpLoweringVisitor().Lower(ctx);
                err = null;
                return PipelineDriver.CompileModule(module);
            }
            catch (Exception e) { err = e.Message; return null; }
        }

        private static BytecodeProgram TryCompileB(Compiler.Module module, out string err)
        {
            try { err = null; return PipelineDriver.CompileModule(module); }
            catch (Exception e) { err = e.Message; return null; }
        }

        private static int FirstDiff(byte[] a, byte[] b)
        {
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++) if (a[i] != b[i]) return i;
            return a.Length == b.Length ? -1 : n;
        }

        private static string Acc(bool ok) => ok ? "accept" : "reject";
        private static ParityResult Match(string id, string cause) => new ParityResult { InputId = id, Verdict = Verdict.Match, Cause = cause };
        private static ParityResult Diverge(string id, string cause) => new ParityResult { InputId = id, Verdict = Verdict.Diverge, Cause = cause };
    }
}

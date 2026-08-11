// SPDX-FileCopyrightText: Copyright (c) 2026 by Marcelle Kress von Wendland, The Olamni Research Group and Bancstreet Capital Partners Ltd, London, UK
// SPDX-License-Identifier: MIT
//
// PipelineDriver (feature 069, T004) — compile a lowered engine Module to a BytecodeProgram
// via the IDENTICAL shared downstream pipeline the production facade runs post-parse
// (GlpCompiler.CompileWithMetadata, compiler.cs:56-132), minus lex/parse:
//     Program -> Analyzer.Analyze (SRSW + partial-eval + annotation) -> CodeGenerator.GenerateWithMetadata.
// No new engine capability is introduced (FR-002, contract G3). Both front-ends — the bridge
// (side A) and the production hand-written parser (side B) — call CompileModule, so any IL
// difference is attributable to the front-end alone (data-model invariant).

using System;
using System.IO;
using GlpRuntime.Compiler;
using GlpRuntime.Bytecode;
using GlpRuntime.Engine;

namespace GlpGrammarSpike.Bridge
{
    public static class PipelineDriver
    {
        private static readonly object _gate = new object();
        private static bool _initialized;

        // Installs the process-global prelude state exactly as GlpEngine does at startup:
        // PreludeUnitClauses + TypeEnvironmentBuilder are internal statics set as a side effect
        // of the engine constructor (glp_engine.cs:265-266), so defined-guard partial evaluation
        // matches production. Both front-ends share this global state, so parity holds regardless.
        // Idempotent; the constructed engine is otherwise unused.
        public static void EnsureInitialized(string repoRoot)
        {
            if (_initialized) return;
            lock (_gate)
            {
                if (_initialized) return;
                string selfGlp = Path.Combine(repoRoot, "programs", "self.glp");
                if (!File.Exists(selfGlp))
                    throw new FileNotFoundException("root prelude programs/self.glp not found for pipeline init", selfGlp);
                _ = new GlpEngine(selfGlp);
                _initialized = true;
            }
        }

        // Compile a parsed module (from EITHER front-end) to bytecode via the shared pipeline.
        public static BytecodeProgram CompileModule(Module module)
        {
            // Mirror the facade's post-parse steps (compiler.cs:71,117-132). The Analyzer runs
            // SRSW validation and re-runs defined-guard partial evaluation internally on this AST.
            var program = new Program(module.Procedures, module.Line, module.Column);
            var annotated = new Analyzer().Analyze(
                program,
                generateReduce: module.CompileMode != CompileMode.System,
                procDeclarations: module.ProcDeclarations,
                compileMode: module.CompileMode);
            var result = new CodeGenerator().GenerateWithMetadata(annotated);
            return result.Program;
        }
    }
}

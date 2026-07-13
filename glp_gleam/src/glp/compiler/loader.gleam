//// glp/compiler/loader — the single-entry load pipeline (feature 050, T020).
////
//// Contract: specs/050-full-gleam-combined/contracts/gleam-instance-surface.md
//// — the stage order is FIXED and no stage is skippable:
////
////     parse → SRSW check → partial evaluation → type check → compile(v2.16) → load
////
//// A failing stage yields a staged diagnostic (glp/diagnostics.StagedError)
//// naming the stage, the rejection class, the source location, and the reason;
//// later stages DO NOT run. There is no standalone checker/compiler entry point
//// — the pipeline is reachable only through `load` (single-tool discipline,
//// same as the Dart and C# reference instances).
////
//// Dart reference (the sequencing oracle): glp_runtime/lib/compiler/compiler.dart
//// `GlpCompiler.compileWithMetadata` — Phase 1 lex, Phase 2 parse, Phase 2.4 PE
//// (`PartialEvaluator.transformDefinedGuards`, the analyzer copy that feeds BOTH
//// type check and codegen), Phase 2.5 `checkModule(..., transformedProcedures:)`,
//// then codegen over the partially-evaluated program. The Dart `Analyzer.analyze`
//// step folds SRSW + PE + annotation together; the Gleam port splits those into
//// `srsw` (T016), `partial_eval` (T017), `type_checker` (T018), and `codegen`
//// (T019), and this module wires them in the contract's fixed order.
////
//// No global prelude state (FR-009: "no process dictionary, no global ETS state
//// for engine semantics"). The Dart pipeline relies on globals set at engine init
//// (`setPreludeUnitClauseSource` / `setPreludeEnvironmentSource` over
//// programs/self.glp); here the prelude source is threaded in explicitly by the
//// caller (the engine facade, T029, owns reading programs/self.glp from disk).

import gleam/dict.{type Dict}
import gleam/option.{Some}
import gleam/result
import gleam/string
import glp/analysis/srsw
import glp/analysis/type_ast.{Pos}
import glp/analysis/type_checker/type_checker
import glp/analysis/type_checker/type_environment_builder as teb
import glp/bytecode/program.{type BytecodeProgram}
import glp/compiler/codegen
import glp/compiler/partial_eval
import glp/diagnostics.{type StagedError}
import glp/parser/ast
import glp/parser/lexer
import glp/parser/parser

/// A successful load: the compiled v2.16 program plus any type-check warnings.
/// The reference prints warnings (`[TYPE WARNING] …`) but does not reject on
/// them (Dart compiler.dart lines 97–101); the REPL surface (T031–T033) renders
/// them. Errors, by contrast, reject the load with a staged diagnostic.
pub type LoadOutcome {
  LoadOutcome(
    program: BytecodeProgram,
    warnings: List(type_checker.TypeWarning),
  )
}

/// Run the full load pipeline over `source`, with `prelude_source` supplying the
/// programs/self.glp definitions (pass "" for the bare instance — the Dart
/// `typePrelude` default). Returns the compiled program on success, or the
/// first staged diagnostic on the earliest stage that rejects.
pub fn load(
  source: String,
  prelude_source: String,
) -> Result(LoadOutcome, StagedError) {
  // Stage 1 — parse (lex + parse).
  use module <- result.try(parse_stage(source))
  // Stage 2 — SRSW, on the ORIGINAL parse (before PE: guard readers still count
  // for pairing — srsw.check contract).
  use _ <- result.try(srsw_stage(module))
  // Stage 3 — partial evaluation (analyzer copy: unfold defined guards; the same
  // transformed procedures feed both the type checker and codegen).
  let prelude_units = prelude_unit_clauses(prelude_source)
  use transformed <- result.try(pe_stage(module, prelude_units))
  // Stage 4 — type check, against the PE-transformed procedures so coverage sees
  // the expanded guards (Dart checkModule transformedProcedures:).
  use warnings <- result.try(type_check_stage(
    module,
    transformed,
    prelude_source,
  ))
  // Stage 5 — compile the PE-transformed module to v2.16 bytecode.
  let compiled_module = ast.SourceModule(..module, procedures: transformed)
  let prog = codegen.generate(compiled_module)
  // Stage 6 — load: for the standalone instance the compiled program IS the
  // registration; the engine facade (T029) installs it. No load-stage error
  // is producible here.
  Ok(LoadOutcome(prog, warnings))
}

/// Compile the prelude (programs/self.glp) to bytecode, WITHOUT the fatal
/// type-check stage: parse → SRSW → PE → codegen.
///
/// Dart oracle: `GlpEngine._loadRootSelf` compiles the root self.glp with
/// `GlpCompiler.compile` under the default `CompileOptions()` — where
/// `typeCheck = false` (compiler.dart:27). The prelude is therefore NEVER
/// type-checked as a module: its clauses call host kernels (`_now/1`, `_add/3`,
/// `_list_to_tuple/2`, …) that are deliberately absent from `builtinProcedures`,
/// so a user-style type check would (spuriously) reject it. The stage order and
/// error mapping here reuse `load`'s stage functions; only the type-check stage
/// is elided.
///
/// The prelude source is a trusted engine invariant (FR-009 threads it in from
/// the facade, which reads programs/self.glp from disk). A parse / SRSW / PE
/// rejection of self.glp is a bug in the prelude, surfaced as a staged
/// diagnostic exactly as `load` would — the facade fails loudly (Dart
/// `_loadRootSelf` throws a `StateError` on the same conditions).
pub fn compile_prelude(
  prelude_source: String,
) -> Result(BytecodeProgram, StagedError) {
  use module <- result.try(parse_stage(prelude_source))
  use _ <- result.try(srsw_stage(module))
  let prelude_units = prelude_unit_clauses(prelude_source)
  use transformed <- result.try(pe_stage(module, prelude_units))
  let compiled_module = ast.SourceModule(..module, procedures: transformed)
  Ok(codegen.generate(compiled_module))
}

// ── Stage 1: parse ──────────────────────────────────────────────────────────

fn parse_stage(source: String) -> Result(ast.SourceModule, StagedError) {
  case lexer.tokenize(source) {
    Error(lexer.LexError(message, line, column)) ->
      Error(diagnostics.StagedError(
        diagnostics.ParseStage,
        diagnostics.ParseError,
        Pos(line, column),
        message,
      ))
    Ok(tokens) ->
      case parser.parse_module(tokens) {
        Error(parser.ParseError(message, line, column)) ->
          Error(diagnostics.StagedError(
            diagnostics.ParseStage,
            diagnostics.ParseError,
            Pos(line, column),
            message,
          ))
        Ok(module) -> Ok(module)
      }
  }
}

// ── Stage 2: SRSW ───────────────────────────────────────────────────────────
//
// A body-only construct in guard position (`GuardPositionError`) is the
// reference's "invalid guard" class (corpus section E) → GuardViolation; the
// aggregated multiplicity/pairing report and a reserved-constant use are SRSW
// violations (section D) → SrswViolation.

fn srsw_stage(module: ast.SourceModule) -> Result(Nil, StagedError) {
  case srsw.check(module) {
    Ok(Nil) -> Ok(Nil)
    Error(srsw.SrswViolations(message, pos)) ->
      Error(diagnostics.StagedError(
        diagnostics.SrswStage,
        diagnostics.SrswViolation,
        pos,
        message,
      ))
    Error(srsw.GuardPositionError(message, pos)) ->
      Error(diagnostics.StagedError(
        diagnostics.SrswStage,
        diagnostics.GuardViolation,
        pos,
        message,
      ))
    Error(srsw.ReservedConstantError(message, pos)) ->
      Error(diagnostics.StagedError(
        diagnostics.SrswStage,
        diagnostics.SrswViolation,
        pos,
        message,
      ))
  }
}

// ── Stage 3: partial evaluation ─────────────────────────────────────────────
//
// The analyzer copy keeps every non-unit guard untouched (KeepAll admission),
// so only `DefinedGuardError` is producible here; both PE error variants are an
// invalid guard (section E) → GuardViolation.

fn pe_stage(
  module: ast.SourceModule,
  prelude_units: Dict(String, List(ast.Term)),
) -> Result(List(ast.Procedure), StagedError) {
  case
    partial_eval.transform_defined_guards_analyzer(
      module.procedures,
      prelude_units,
    )
  {
    Ok(procedures) -> Ok(procedures)
    Error(partial_eval.DefinedGuardError(message, pos)) ->
      Error(diagnostics.StagedError(
        diagnostics.PartialEvalStage,
        diagnostics.GuardViolation,
        pos,
        message,
      ))
    Error(partial_eval.GuardAdmissionError(message, pos)) ->
      Error(diagnostics.StagedError(
        diagnostics.PartialEvalStage,
        diagnostics.GuardViolation,
        pos,
        message,
      ))
  }
}

// ── Stage 4: type check ─────────────────────────────────────────────────────

fn type_check_stage(
  module: ast.SourceModule,
  transformed: List(ast.Procedure),
  prelude_source: String,
) -> Result(List(type_checker.TypeWarning), StagedError) {
  use prelude_env <- result.try(
    case teb.build_prelude_environment(prelude_source) {
      Ok(env) -> Ok(env)
      Error(env_error) -> Error(env_error_to_staged(env_error))
    },
  )
  case type_checker.check_module(module, Some(transformed), Some(prelude_env)) {
    Error(env_error) -> Error(env_error_to_staged(env_error))
    Ok(result) ->
      case result.errors {
        [] -> Ok(result.warnings)
        [first, ..] ->
          Error(diagnostics.StagedError(
            diagnostics.TypeCheckStage,
            diagnostics.TypeError,
            Pos(first.line, first.column),
            first.message,
          ))
      }
  }
}

fn env_error_to_staged(env_error: teb.TypeEnvError) -> StagedError {
  let #(message, line, column) = case env_error {
    teb.RedefinitionError(m, l, c) -> #(m, l, c)
    teb.CircularAliasError(m, l, c) -> #(m, l, c)
    teb.NonDeterministicTypeError(m, l, c) -> #(m, l, c)
    teb.AliasExpansionError(m, l, c) -> #(m, l, c)
  }
  diagnostics.StagedError(
    diagnostics.TypeCheckStage,
    diagnostics.TypeError,
    Pos(line, column),
    message,
  )
}

// ── Prelude unit clauses (for the PE stage) ─────────────────────────────────
//
// The prelude source is trusted (an engine invariant): a lex/parse failure of
// programs/self.glp is a bug, not a user diagnostic — mirror
// build_prelude_environment and fail loudly. An empty prelude contributes no
// unit clauses.

fn prelude_unit_clauses(
  prelude_source: String,
) -> Dict(String, List(ast.Term)) {
  case prelude_source == "" {
    True -> dict.new()
    False -> {
      let tokens = case lexer.tokenize(prelude_source) {
        Ok(t) -> t
        Error(e) ->
          panic as { "loader: prelude lex error: " <> string.inspect(e) }
      }
      let module = case parser.parse_module(tokens) {
        Ok(m) -> m
        Error(e) ->
          panic as { "loader: prelude parse error: " <> string.inspect(e) }
      }
      partial_eval.collect_unit_clauses(module.procedures)
    }
  }
}

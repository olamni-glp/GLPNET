//// glp/diagnostics — staged load-pipeline diagnostics (feature 050, T011).
////
//// Contract: specs/050-full-gleam-combined/contracts/gleam-instance-surface.md —
//// stage order is FIXED (parse → SRSW check → partial evaluation → type check →
//// compile(v2.16) → load); a failing stage rejects with a staged diagnostic
//// naming the stage, the source location, and the reason, and later stages do
//// not run. Rejection classes must match the reference runtime's classification
//// for corpus negatives (test sections C/D/E shape).
////
//// Dart reference: glp_runtime/lib/compiler/error.dart (CompileError with
//// `phase`) + the analyzer/type-checker error surfaces.

import gleam/int
import glp/analysis/type_ast.{type Pos}

/// One pipeline stage, in the fixed order no stage may skip.
pub type Stage {
  ParseStage
  SrswStage
  PartialEvalStage
  TypeCheckStage
  CompileStage
  LoadStage
}

/// The stage name as printed in diagnostics.
pub fn stage_name(stage: Stage) -> String {
  case stage {
    ParseStage -> "parse"
    SrswStage -> "srsw"
    PartialEvalStage -> "partial_eval"
    TypeCheckStage -> "type_check"
    CompileStage -> "compile"
    LoadStage -> "load"
  }
}

/// Rejection classification matching the reference runtime's corpus-negative
/// classes (contract: parse error, SRSW violation, type error, guard
/// violation).
pub type RejectionClass {
  ParseError
  SrswViolation
  TypeError
  GuardViolation
}

/// The class name as printed in diagnostics.
pub fn class_name(class: RejectionClass) -> String {
  case class {
    ParseError -> "parse error"
    SrswViolation -> "SRSW violation"
    TypeError -> "type error"
    GuardViolation -> "guard violation"
  }
}

/// A staged diagnostic: the stage that rejected, the rejection class, the
/// source location, and the reason. This is the `StagedError` of the
/// engine-value surface (`load(Engine, source) -> Result(Engine, StagedError)`).
pub type StagedError {
  StagedError(stage: Stage, class: RejectionClass, pos: Pos, reason: String)
}

/// Render a diagnostic one-liner: `stage: class at line:column — reason`.
pub fn to_string(error: StagedError) -> String {
  stage_name(error.stage)
  <> ": "
  <> class_name(error.class)
  <> " at "
  <> int.to_string(error.pos.line)
  <> ":"
  <> int.to_string(error.pos.column)
  <> " — "
  <> error.reason
}

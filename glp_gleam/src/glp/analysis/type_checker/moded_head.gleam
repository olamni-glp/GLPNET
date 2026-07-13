//// glp/analysis/type_checker/moded_head — moded head construction for GLP type
//// checking (feature 050, T018).
////
//// Dart source of truth: glp_runtime/lib/analysis/type_checker/moded_head.dart
//// (spec: docs/type system/moded-head.md v0.8; paper Definition 5.5). Builds a
//// moded head H' from a clause head H and a procedure declaration: construct an
//// I/O-moded term (root ↓) then unconditionally flip every variable (X↔X?),
//// capturing the inverted roles at the call boundary. `produced_term` builds the
//// caller's-perspective moded term for a body atom (root ↑, variables NOT
//// flipped).
////
//// Port notes:
////   * Dart drives mode computation with an `Object`-typed `TypeEnvironment?`
////     and a module-level MUTABLE `_anonVarCounter` (reset in `modedHead`, left
////     running across `producedTerm` calls so a whole clause's `_` occurrences
////     get distinct `_#N` names — Bug 4 / paper Remark 3.1). Gleam has no mutable
////     module state, so the counter is threaded functionally: both entry points
////     return the ending counter, and the caller feeds `moded_head`'s ending
////     counter into the first `produced_term`, and so on. This matters: a head
////     `_` writer is flipped to a reader, so a body `_` reusing its name would
////     spuriously pair with it in the duality check.
////   * Dart throws `ArityMismatchError` (caught by well_typed_clause and turned
////     into a diagnostic) → genuine error channel → `Result`. Dart's
////     `InvalidHeadError` (unknown term kind) is unreachable here: `ast.Term` is
////     a closed union, fully matched.
////   * `type_ast` exports `Output`/`Input` (TypeClassification) that collide with
////     `mode`'s `Output`/`Input`; only `mode`'s constructors are imported
////     unqualified, and all `type_ast` constructors are referenced qualified.

import gleam/int
import gleam/list
import gleam/option.{type Option, None, Some}
import glp/analysis/type_ast.{type ProcDecl, type TypeEnvironment, type TypeExpr}
import glp/analysis/type_checker/mode.{type Mode, Input, Output}
import glp/analysis/type_checker/moded_term.{
  type ModedTerm, ModedCompound, ModedConstant, ModedVariable,
}
import glp/parser/ast

/// Error raised when a head/atom arity does not match its declaration (Dart
/// `ArityMismatchError`, caught and rendered as a diagnostic by the caller).
pub type ModedHeadError {
  ArityMismatch(message: String)
}

// =============================================================================
// Public entry points
// =============================================================================

/// Construct a moded head H' from clause head `head` per Definition 5.5. Returns
/// the moded term (root ↓, all variables flipped) and the ending anon-var
/// counter (starts at 0, Dart `resetAnonVarCounter`). Dart `modedHead`.
pub fn moded_head(
  head: ast.Goal,
  decl: ProcDecl,
  type_env: Option(TypeEnvironment),
) -> Result(#(ModedTerm, Int), ModedHeadError) {
  let args = ast.goal_args(head)
  let head_arity = list.length(args)
  case head_arity == type_ast.arity(decl) {
    False ->
      Error(ArityMismatch(
        "Head arity "
        <> int.to_string(head_arity)
        <> " does not match declaration arity "
        <> int.to_string(type_ast.arity(decl)),
      ))
    True -> {
      // Step 1: I/O moded term (root mode = consume), counter starts at 0.
      let #(io_term, counter) =
        build_io_moded_term(ast.goal_functor(head), args, decl, Input, type_env, 0)
      // Step 2: unconditionally flip every variable.
      Ok(#(ensure_variables_match_modes(io_term), counter))
    }
  }
}

/// Construct a produced moded term for a body atom (root ↑, variables NOT
/// flipped — the caller's perspective). `anon_start` is the incoming anon-var
/// counter (Dart `producedTerm` does not reset it). Dart `producedTerm`.
pub fn produced_term(
  atom: ast.Goal,
  decl: ProcDecl,
  type_env: Option(TypeEnvironment),
  anon_start: Int,
) -> Result(#(ModedTerm, Int), ModedHeadError) {
  let args = ast.goal_args(atom)
  let atom_arity = list.length(args)
  case atom_arity == type_ast.arity(decl) {
    False ->
      Error(ArityMismatch(
        "Atom arity "
        <> int.to_string(atom_arity)
        <> " does not match declaration arity "
        <> int.to_string(type_ast.arity(decl)),
      ))
    True ->
      // Root mode = produce, no variable flip for body atoms.
      Ok(build_io_moded_term(
        ast.goal_functor(atom),
        args,
        decl,
        Output,
        type_env,
        anon_start,
      ))
  }
}

// =============================================================================
// I/O moded term construction
// =============================================================================

/// Build an I/O moded term (Dart `_buildIOModedTerm`): each argument's mode is
/// consume for `Type?` and produce for `Type`; embedded modes inside structures
/// combine via involution.
fn build_io_moded_term(
  functor: String,
  args: List(ast.Term),
  decl: ProcDecl,
  parent_mode: Mode,
  type_env: Option(TypeEnvironment),
  counter: Int,
) -> #(ModedTerm, Int) {
  // Arity has been checked, so args and arg_types are the same length.
  let #(counter2, moded_args) =
    list.map_fold(list.zip(args, decl.arg_types), counter, fn(c, pair) {
      let #(arg_term, arg_type) = pair
      // Input (Type? / _?) → consume; Output (Type / _) → produce.
      let arg_mode = mode_of_type(arg_type)
      let #(moded_arg, c2) =
        build_moded_subterm(arg_term, arg_mode, Some(arg_type), type_env, c)
      #(c2, moded_arg)
    })
  #(ModedCompound(parent_mode, functor, list.length(args), moded_args), counter2)
}

/// Build a moded subterm (Dart `_buildModedSubterm`). A wildcard / unknown
/// expected type routes to opaque construction (all subterms inherit one mode).
fn build_moded_subterm(
  term: ast.Term,
  mode: Mode,
  expected_type: Option(TypeExpr),
  type_env: Option(TypeEnvironment),
  counter: Int,
) -> #(ModedTerm, Int) {
  case expected_type {
    // Wildcard (`_`/`_?`) or unknown type: no type-driven mode computation.
    None -> build_opaque_moded_term(term, mode, counter)
    Some(type_ast.PrimitiveModeAlt(_, _)) ->
      build_opaque_moded_term(term, mode, counter)
    Some(exp) ->
      case term {
        ast.VarTerm(name, is_reader, _) -> #(
          ModedVariable(name, is_reader, mode),
          counter,
        )
        ast.StructTerm(functor, sub_args, _) -> {
          let ar = list.length(sub_args)
          let subterm_modes =
            get_subterm_modes(functor, ar, mode, Some(exp), type_env)
          let #(c2, moded_args) =
            list.map_fold(
              list.zip(sub_args, subterm_modes),
              counter,
              fn(c, pair) {
                let #(sub_term, #(sub_mode, sub_type)) = pair
                let #(m, c3) =
                  build_moded_subterm(sub_term, sub_mode, sub_type, type_env, c)
                #(c3, m)
              },
            )
          #(ModedCompound(mode, functor, ar, moded_args), c2)
        }
        ast.ListTerm(None, None, _) -> #(moded_term.nil(mode), counter)
        ast.ListTerm(Some(head_t), Some(tail_t), _) -> {
          let #(head_info, tail_info) =
            get_list_subterm_modes(mode, Some(exp), type_env)
          let #(head_mode, head_type) = head_info
          let #(tail_mode, tail_type) = tail_info
          let #(head, c1) =
            build_moded_subterm(head_t, head_mode, head_type, type_env, counter)
          let #(tail, c2) =
            build_moded_subterm(tail_t, tail_mode, tail_type, type_env, c1)
          #(moded_term.list_cons(mode, head, tail), c2)
        }
        // Dart would dereference a null head/tail here — an invariant violation
        // (the parser only ever emits proper cons cells or nil).
        ast.ListTerm(_, _, _) -> panic as "moded_head: malformed list term"
        ast.ConstTerm(value, _) -> #(ModedConstant(mode, value), counter)
        ast.UnderscoreTerm(_, _) -> {
          // Each `_` is a fresh writer (paper Remark 3.1 / Bug 4).
          let #(name, c2) = fresh_anon(counter)
          #(ModedVariable(name, False, mode), c2)
        }
      }
  }
}

/// Build a moded term without type-driven mode computation (Dart
/// `_buildOpaqueModedTerm`): every subterm inherits `mode`.
fn build_opaque_moded_term(
  term: ast.Term,
  mode: Mode,
  counter: Int,
) -> #(ModedTerm, Int) {
  case term {
    ast.VarTerm(name, is_reader, _) -> #(
      ModedVariable(name, is_reader, mode),
      counter,
    )
    ast.ConstTerm(value, _) -> #(ModedConstant(mode, value), counter)
    ast.UnderscoreTerm(_, _) -> {
      let #(name, c2) = fresh_anon(counter)
      #(ModedVariable(name, False, mode), c2)
    }
    ast.ListTerm(None, None, _) -> #(moded_term.nil(mode), counter)
    ast.ListTerm(Some(head_t), Some(tail_t), _) -> {
      let #(head, c1) = build_opaque_moded_term(head_t, mode, counter)
      let #(tail, c2) = build_opaque_moded_term(tail_t, mode, c1)
      #(moded_term.list_cons(mode, head, tail), c2)
    }
    ast.ListTerm(_, _, _) ->
      panic as "moded_head: malformed list term (opaque)"
    ast.StructTerm(functor, sub_args, _) -> {
      let #(c2, moded_args) =
        list.map_fold(sub_args, counter, fn(c, arg) {
          let #(m, c3) = build_opaque_moded_term(arg, mode, c)
          #(c3, m)
        })
      #(ModedCompound(mode, functor, list.length(sub_args), moded_args), c2)
    }
  }
}

// =============================================================================
// Subterm-mode computation (type-driven, via involution)
// =============================================================================

/// Subterm modes for a structure by looking up its type definition (Dart
/// `_getSubtermModes`). `combinedMode = parentMode ⊕ embeddedMode`; for a dual
/// (`T?`) parent, subterm types are dualized for DFA leaf checking. Falls back
/// to propagating the parent mode when no type info resolves.
fn get_subterm_modes(
  functor: String,
  arity: Int,
  parent_mode: Mode,
  expected_type: Option(TypeExpr),
  type_env: Option(TypeEnvironment),
) -> List(#(Mode, Option(TypeExpr))) {
  let default = list.repeat(#(parent_mode, None), arity)
  case type_env, expected_type {
    Some(env), Some(type_ast.TypeRef(type_name, is_dual, _, _)) ->
      case type_ast.get_type(env, type_name) {
        Ok(type_def) ->
          case
            list.find_map(type_def.alternatives, fn(alt) {
              match_struct_alt(alt, functor, arity, parent_mode, is_dual)
            })
          {
            Ok(modes) -> modes
            Error(_) -> default
          }
        Error(_) -> default
      }
    _, _ -> default
  }
}

fn match_struct_alt(
  alt: TypeExpr,
  functor: String,
  arity: Int,
  parent_mode: Mode,
  is_dual: Bool,
) -> Result(List(#(Mode, Option(TypeExpr))), Nil) {
  case alt {
    type_ast.StructAlt(alt_functor, alt_args, _) ->
      case alt_functor == functor && list.length(alt_args) == arity {
        True ->
          Ok(
            list.map(alt_args, fn(arg_type) {
              #(
                mode.combine_mode(parent_mode, get_embedded_mode(arg_type)),
                Some(dual_if(arg_type, is_dual)),
              )
            }),
          )
        False -> Error(Nil)
      }
    // DiffList `\`/2.
    type_ast.DiffListAlt(content, hole, _) ->
      case functor == "\\" && arity == 2 {
        True ->
          Ok([
            #(
              mode.combine_mode(parent_mode, get_embedded_mode(content)),
              Some(dual_if(content, is_dual)),
            ),
            #(
              mode.combine_mode(parent_mode, get_embedded_mode(hole)),
              Some(dual_if(hole, is_dual)),
            ),
          ])
        False -> Error(Nil)
      }
    _ -> Error(Nil)
  }
}

/// Subterm modes for a list `[H|T]` by looking up its `ListConsAlt` (Dart
/// `_getListSubtermModes`).
fn get_list_subterm_modes(
  parent_mode: Mode,
  expected_type: Option(TypeExpr),
  type_env: Option(TypeEnvironment),
) -> #(#(Mode, Option(TypeExpr)), #(Mode, Option(TypeExpr))) {
  let default = #(#(parent_mode, None), #(parent_mode, None))
  case type_env, expected_type {
    Some(env), Some(type_ast.TypeRef(type_name, is_dual, _, _)) ->
      case type_ast.get_type(env, type_name) {
        Ok(type_def) ->
          case
            list.find_map(type_def.alternatives, fn(alt) {
              match_list_cons_alt(alt, parent_mode, is_dual)
            })
          {
            Ok(modes) -> modes
            Error(_) -> default
          }
        Error(_) -> default
      }
    _, _ -> default
  }
}

fn match_list_cons_alt(
  alt: TypeExpr,
  parent_mode: Mode,
  is_dual: Bool,
) -> Result(#(#(Mode, Option(TypeExpr)), #(Mode, Option(TypeExpr))), Nil) {
  case alt {
    type_ast.ListConsAlt(head, tail, _) ->
      Ok(#(
        #(
          mode.combine_mode(parent_mode, get_embedded_mode(head)),
          Some(dual_if(head, is_dual)),
        ),
        #(
          mode.combine_mode(parent_mode, get_embedded_mode(tail)),
          Some(dual_if(tail, is_dual)),
        ),
      ))
    _ -> Error(Nil)
  }
}

/// Embedded mode of a type expression (Dart `_getEmbeddedMode`): input-moded →
/// consume (↓ = Input), otherwise produce (↑ = Output).
fn get_embedded_mode(expr: TypeExpr) -> Mode {
  case type_ast.is_input_mode(expr) {
    True -> Input
    False -> Output
  }
}

/// The dual of a type expression when `when` is true, else the expression
/// unchanged (Dart `_dualType` guarded by the `isDual` flag).
fn dual_if(expr: TypeExpr, when: Bool) -> TypeExpr {
  case when {
    True -> type_ast.dual(expr)
    False -> expr
  }
}

// =============================================================================
// Variable complementation (Definition 5.5 step 2)
// =============================================================================

/// Unconditionally flip every variable (Dart `_ensureVariablesMatchModes`):
/// head writers become readers (bound BY the goal), head readers become writers
/// (bound BY the body). Constants and structure modes are unchanged.
fn ensure_variables_match_modes(term: ModedTerm) -> ModedTerm {
  case term {
    ModedCompound(mode, functor, arity, args) ->
      ModedCompound(
        mode,
        functor,
        arity,
        list.map(args, ensure_variables_match_modes),
      )
    ModedConstant(_, _) -> term
    ModedVariable(name, is_reader, structural_mode) ->
      ModedVariable(name, !is_reader, structural_mode)
  }
}

// =============================================================================
// Helpers
// =============================================================================

/// Argument mode from its declared type (Dart `ProcDecl.isInputArg`): input →
/// consume (↓ = Input), output → produce (↑ = Output).
fn mode_of_type(arg_type: TypeExpr) -> Mode {
  case type_ast.is_input_mode(arg_type) {
    True -> Input
    False -> Output
  }
}

/// Generate the next anonymous-variable name (Dart `_freshAnonVarName`):
/// `_#1`, `_#2`, … Returns the name and the advanced counter.
fn fresh_anon(counter: Int) -> #(String, Int) {
  let next = counter + 1
  #("_#" <> int.to_string(next), next)
}

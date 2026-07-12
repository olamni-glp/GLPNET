//// glp/analysis/type_checker/moded_term — moded terms for GLP type checking
//// (feature 050, T018).
////
//// Dart source of truth: glp_runtime/lib/analysis/type_checker/moded_term.dart
//// (spec: docs/modules/moded-term.md v0.5; paper Definition 4.2). A moded term
//// is a GLP term with a data-flow mode annotation (↓ consume / ↑ produce) on
//// every non-variable subterm; variables carry a structural mode inherited from
//// their enclosing type context.
////
//// Port notes:
////   * Dart `ModedConstant.value` is a dynamic `Object` (int/double/String/atom).
////     Gleam has no `Object`, so the port carries the same typed constant
////     `glp/runtime/terms.Constant` that `ast.ConstTerm` already holds — this is
////     exactly what `moded_head` passes through. `nil` is `ConstAtom("[]")`
////     (Dart's literal `'[]'` marker), matching Dart `ModedConstant.nil`.
////   * Dart accesses `.mode` uniformly across all three subterm shapes; for a
////     variable that getter returns the *structural* mode. Gleam has no
////     cross-variant field access, so `mode_of/1` provides it.
////   * The Dart visitor pattern (`accept`) is replaced by direct `case`
////     recursion — same traversal, no dispatch object.

import gleam/float
import gleam/int
import gleam/list
import gleam/set.{type Set}
import gleam/string
import glp/analysis/type_checker/mode.{type Mode, Input, Output}
import glp/runtime/terms.{type Constant}

// =============================================================================
// Moded Term
// =============================================================================

/// A moded term: a GLP term with mode annotations (paper Definition 4.2).
pub type ModedTerm {
  /// A compound term with mode annotation, e.g. `↓merge(↓[↓X?|Xs?], Ys?, ↑[↑X|Zs])`.
  /// List cons `[H|T]` uses functor `"[|]"`, arity 2 (`list_cons`).
  ModedCompound(mode: Mode, functor: String, arity: Int, args: List(ModedTerm))
  /// A constant (integer, real, string, atom) with mode annotation.
  ModedConstant(mode: Mode, value: Constant)
  /// A variable (reader `X?` or writer `X`) with structural mode (Remark 4.1:
  /// structural mode is stored; implicit mode is computed from reader/writer).
  ModedVariable(name: String, is_reader: Bool, structural_mode: Mode)
}

/// The mode annotation of a moded term (Dart `.mode` getter). For a variable
/// this is its structural mode (used in path traversal for automaton lookup).
pub fn mode_of(term: ModedTerm) -> Mode {
  case term {
    ModedCompound(mode, _, _, _) -> mode
    ModedConstant(mode, _) -> mode
    ModedVariable(_, _, structural_mode) -> structural_mode
  }
}

/// List cons `[H|T]` convenience constructor (Dart `ModedCompound.listCons`).
pub fn list_cons(mode: Mode, head: ModedTerm, tail: ModedTerm) -> ModedTerm {
  ModedCompound(mode, "[|]", 2, [head, tail])
}

/// Is this compound a list cons `[|]`/2? (Dart `ModedCompound.isListCons`.)
pub fn is_list_cons(term: ModedTerm) -> Bool {
  case term {
    ModedCompound(_, "[|]", 2, _) -> True
    _ -> False
  }
}

/// Nil `[]` convenience constructor (Dart `ModedConstant.nil`).
pub fn nil(mode: Mode) -> ModedTerm {
  ModedConstant(mode, terms.ConstAtom("[]"))
}

/// Reader `X?` convenience constructor (Dart `ModedVariable.reader`).
pub fn reader(name: String, structural_mode: Mode) -> ModedTerm {
  ModedVariable(name, True, structural_mode)
}

/// Writer `X` convenience constructor (Dart `ModedVariable.writer`).
pub fn writer(name: String, structural_mode: Mode) -> ModedTerm {
  ModedVariable(name, False, structural_mode)
}

// -----------------------------------------------------------------------------
// ModedConstant classification (Dart getters)
// -----------------------------------------------------------------------------

/// Is this constant nil `[]`? (Dart `ModedConstant.isNil`.)
pub fn constant_is_nil(value: Constant) -> Bool {
  value == terms.ConstAtom("[]")
}

/// Is this constant an integer literal? (Dart `ModedConstant.isInteger`.)
pub fn constant_is_integer(value: Constant) -> Bool {
  case value {
    terms.ConstInt(_) -> True
    _ -> False
  }
}

/// Is this constant a real (floating-point) literal? (Dart `.isReal`.)
pub fn constant_is_real(value: Constant) -> Bool {
  case value {
    terms.ConstReal(_) -> True
    _ -> False
  }
}

/// Is this constant numeric (integer or real)? (Dart `.isNumeric`.)
pub fn constant_is_numeric(value: Constant) -> Bool {
  constant_is_integer(value) || constant_is_real(value)
}

/// Is this constant a quoted string? (Dart `.isString`: the raw value is a
/// String wrapped in `"` or `'`.) A typed `ConstString` renders quote-wrapped;
/// a `ConstAtom` counts only if its own text is already quote-wrapped.
pub fn constant_is_string(value: Constant) -> Bool {
  case value {
    terms.ConstString(_) -> True
    terms.ConstAtom(name) -> looks_quoted(name)
    _ -> False
  }
}

/// Is this constant an unquoted atom (includes nil)? (Dart `.isAtom`: the raw
/// value is a String that is not a quoted string.)
pub fn constant_is_atom(value: Constant) -> Bool {
  case value {
    terms.ConstAtom(name) -> !looks_quoted(name)
    _ -> False
  }
}

// -----------------------------------------------------------------------------
// ModedVariable classification (Dart getters)
// -----------------------------------------------------------------------------

/// Implicit mode from reader/writer form (Dart `ModedVariable.implicitMode`):
/// reader → consume (↓ = Input), writer → produce (↑ = Output).
pub fn implicit_mode(is_reader: Bool) -> Mode {
  case is_reader {
    True -> Input
    False -> Output
  }
}

/// Whether a variable is mode-consistent: implicit mode equals structural mode
/// (Dart `ModedVariable.isModeConsistent`). Convenience only; the authoritative
/// check lives in the well-typed-term module.
pub fn variable_is_mode_consistent(
  is_reader: Bool,
  structural_mode: Mode,
) -> Bool {
  implicit_mode(is_reader) == structural_mode
}

// =============================================================================
// Moded Path
// =============================================================================

/// A single step in a moded path (Dart `PathStep`).
pub type PathStep {
  PathStep(
    /// functor/arity for a compound (`"merge/3"`, `"[|]/2"`), the value for a
    /// constant (`"42"`, `"[]"`), or the variable name (`"X"`, `"X?"`).
    symbol: String,
    /// 0 for the root, 1-based for arguments.
    arg_index: Int,
    mode: Mode,
    /// True when this step is a variable leaf.
    is_variable: Bool,
    /// When `is_variable`, whether it is a reader `X?`.
    is_reader: Bool,
  )
}

/// A non-leaf/non-variable path step (Dart `PathStep` default constructor with
/// `isVariable`/`isReader` defaulting to false).
pub fn path_step(symbol: String, arg_index: Int, mode: Mode) -> PathStep {
  PathStep(symbol, arg_index, mode, False, False)
}

/// Whether this step is a writer variable (Dart `PathStep.isWriter`).
pub fn step_is_writer(step: PathStep) -> Bool {
  step.is_variable && !step.is_reader
}

/// A path through a moded term: root to leaf (Dart `ModedPath`).
pub type ModedPath {
  ModedPath(steps: List(PathStep))
}

/// The root step (Dart `ModedPath.root`). Assumes a non-empty path.
pub fn path_root(path: ModedPath) -> PathStep {
  case path.steps {
    [first, ..] -> first
    [] -> panic as "ModedPath.root on empty path"
  }
}

/// The leaf step (Dart `ModedPath.leaf`). Assumes a non-empty path.
pub fn path_leaf(path: ModedPath) -> PathStep {
  case list.last(path.steps) {
    Ok(step) -> step
    Error(_) -> panic as "ModedPath.leaf on empty path"
  }
}

/// Whether this path starts with consume mode — an input path (Dart
/// `ModedPath.isInputPath`).
pub fn path_is_input(path: ModedPath) -> Bool {
  path_root(path).mode == Input
}

/// Whether this path starts with produce mode — an output path (Dart
/// `ModedPath.isOutputPath`).
pub fn path_is_output(path: ModedPath) -> Bool {
  path_root(path).mode == Output
}

/// Path length in steps (Dart `ModedPath.length`).
pub fn path_length(path: ModedPath) -> Int {
  list.length(path.steps)
}

// =============================================================================
// Classification: isConsumed, isProduced, isIO (spec moded-term.md v0.5)
// =============================================================================

/// True iff every mode annotation in `term` is consume (↓) (Dart `isConsumed`).
pub fn is_consumed(term: ModedTerm) -> Bool {
  case term {
    ModedCompound(mode, _, _, args) ->
      mode == Input && list.all(args, is_consumed)
    ModedConstant(mode, _) -> mode == Input
    ModedVariable(_, _, structural_mode) -> structural_mode == Input
  }
}

/// True iff every mode annotation in `term` is produce (↑) (Dart `isProduced`).
pub fn is_produced(term: ModedTerm) -> Bool {
  case term {
    ModedCompound(mode, _, _, args) ->
      mode == Output && list.all(args, is_produced)
    ModedConstant(mode, _) -> mode == Output
    ModedVariable(_, _, structural_mode) -> structural_mode == Output
  }
}

/// True iff `term` is an I/O moded term: root mode is consume (↓) and on every
/// path the mode transitions only ↓→↑, never ↑→↓ (Dart `isIO`).
pub fn is_io(term: ModedTerm) -> Bool {
  case mode_of(term) == Input {
    False -> False
    True -> all_paths_valid_io(term, Input)
  }
}

/// Valid transitions: ↓→↓, ↓→↑, ↑→↑. Invalid: ↑→↓ (Dart `_allPathsValidIO`).
fn all_paths_valid_io(term: ModedTerm, parent_mode: Mode) -> Bool {
  let current_mode = mode_of(term)
  case parent_mode == Output && current_mode == Input {
    True -> False
    False ->
      case term {
        ModedCompound(_, _, _, args) ->
          list.all(args, fn(arg) { all_paths_valid_io(arg, current_mode) })
        _ -> True
      }
  }
}

// =============================================================================
// Dual (Definition 5.1)
// =============================================================================

/// The dual `T?`: flip every mode annotation (↓↔↑) and replace every variable
/// by its paired variable (X↔X?). An involution — `dual(dual(t)) == t` (Dart
/// `dual`).
pub fn dual(term: ModedTerm) -> ModedTerm {
  case term {
    ModedCompound(mode, functor, arity, args) ->
      ModedCompound(mode.flip(mode), functor, arity, list.map(args, dual))
    ModedConstant(mode, value) -> ModedConstant(mode.flip(mode), value)
    ModedVariable(name, is_reader, structural_mode) ->
      ModedVariable(name, !is_reader, mode.flip(structural_mode))
  }
}

// =============================================================================
// Path extraction
// =============================================================================

/// All moded paths from root to leaves (Dart `paths`).
pub fn paths(term: ModedTerm) -> Set(ModedPath) {
  let root_step =
    PathStep(
      symbol_of(term),
      0,
      mode_of(term),
      is_variable(term),
      reader_flag(term),
    )
  extract_paths(term, [root_step], set.new())
}

/// All moded paths from root to leaves in depth-first order (Dart `paths`
/// iterated in `Set` insertion order). `paths/1` returns the same paths as an
/// unordered `Set`; the well-typed-term/clause checkers need the DFS order so
/// their diagnostics come out in the same order as the Dart oracle. No leaf
/// yields a duplicate path (siblings differ in `arg_index`, depths differ in
/// length), so this list carries exactly the members of `paths/1`.
pub fn paths_ordered(term: ModedTerm) -> List(ModedPath) {
  let root_step =
    PathStep(
      symbol_of(term),
      0,
      mode_of(term),
      is_variable(term),
      reader_flag(term),
    )
  extract_paths_ordered(term, [root_step], [])
}

/// Recursively extract paths in DFS order (Dart `_extractPaths` with a list
/// accumulator). `prefix` is root→current; completed paths are appended at leaves.
fn extract_paths_ordered(
  term: ModedTerm,
  prefix: List(PathStep),
  result: List(ModedPath),
) -> List(ModedPath) {
  case term {
    ModedCompound(_, _, _, args) ->
      list.index_fold(args, result, fn(acc, child, i) {
        let child_step =
          PathStep(
            symbol_of(child),
            i + 1,
            mode_of(child),
            is_variable(child),
            reader_flag(child),
          )
        extract_paths_ordered(child, list.append(prefix, [child_step]), acc)
      })
    _ -> list.append(result, [ModedPath(prefix)])
  }
}

/// Recursively extract paths (Dart `_extractPaths`). `prefix` is root→current.
fn extract_paths(
  term: ModedTerm,
  prefix: List(PathStep),
  result: Set(ModedPath),
) -> Set(ModedPath) {
  case term {
    ModedCompound(_, _, _, args) ->
      // Recurse into each argument with a 1-based index.
      list.index_fold(args, result, fn(acc, child, i) {
        let child_step =
          PathStep(
            symbol_of(child),
            i + 1,
            mode_of(child),
            is_variable(child),
            reader_flag(child),
          )
        extract_paths(child, list.append(prefix, [child_step]), acc)
      })
    // Leaf (constant or variable): add the completed path.
    _ -> set.insert(result, ModedPath(prefix))
  }
}

/// The symbol representation of a moded term (Dart `_symbolOf`).
fn symbol_of(term: ModedTerm) -> String {
  case term {
    ModedCompound(_, functor, arity, _) -> functor <> "/" <> int.to_string(arity)
    ModedConstant(_, value) -> constant_to_string(value)
    ModedVariable(name, True, _) -> name <> "?"
    ModedVariable(name, False, _) -> name
  }
}

fn is_variable(term: ModedTerm) -> Bool {
  case term {
    ModedVariable(_, _, _) -> True
    _ -> False
  }
}

fn reader_flag(term: ModedTerm) -> Bool {
  case term {
    ModedVariable(_, is_reader, _) -> is_reader
    _ -> False
  }
}

// =============================================================================
// Rendering (Dart toString)
// =============================================================================

/// Dart `ModedTerm.toString()`.
pub fn to_string(term: ModedTerm) -> String {
  case term {
    ModedCompound(mode, functor, arity, args) -> {
      let prefix = mode_symbol(mode)
      case is_list_cons(term), args {
        True, [head, tail] ->
          prefix <> "[" <> to_string(head) <> "|" <> to_string(tail) <> "]"
        _, _ ->
          case arity {
            0 -> prefix <> functor
            _ ->
              prefix
              <> functor
              <> "("
              <> string.join(list.map(args, to_string), ", ")
              <> ")"
          }
      }
    }
    ModedConstant(mode, value) ->
      mode_symbol(mode) <> constant_to_string(value)
    // Dart ModedVariable.toString has no mode prefix.
    ModedVariable(name, True, _) -> name <> "?"
    ModedVariable(name, False, _) -> name
  }
}

/// Dart `ModedPath.toString()`.
pub fn path_to_string(path: ModedPath) -> String {
  path.steps
  |> list.map(fn(s) {
    "("
    <> s.symbol
    <> ", "
    <> int.to_string(s.arg_index)
    <> ", "
    <> mode.to_string(s.mode)
    <> ")"
  })
  |> string.join(" → ")
}

/// Dart `PathStep.toString()`.
pub fn step_to_string(step: PathStep) -> String {
  "("
  <> step.symbol
  <> ", "
  <> int.to_string(step.arg_index)
  <> ", "
  <> mode_symbol(step.mode)
  <> ")"
}

/// Dart mode glyph: consume (↓ = Input) / produce (↑ = Output).
fn mode_symbol(mode: Mode) -> String {
  case mode {
    Input -> "↓"
    Output -> "↑"
  }
}

/// Dart `Object.toString()` for a `ModedConstant.value` (atoms bare, strings
/// quote-wrapped, numbers plain). Mirrors `ast.const_value_to_string`.
fn constant_to_string(value: Constant) -> String {
  case value {
    terms.ConstAtom(name) -> name
    terms.ConstString(s) -> "\"" <> s <> "\""
    terms.ConstInt(i) -> int.to_string(i)
    terms.ConstReal(r) -> float.to_string(r)
  }
}

fn looks_quoted(s: String) -> Bool {
  { string.starts_with(s, "\"") && string.ends_with(s, "\"") }
  || { string.starts_with(s, "'") && string.ends_with(s, "'") }
}

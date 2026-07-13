//// glp/analysis/srsw — SRSW (Single-Reader Single-Writer) checker (feature
//// 050, T016).
////
//// Hand-port of the Dart analyzer's STEP-1 SRSW validation path
//// (glp_runtime/lib/compiler/analyzer.dart: VariableTable +
//// _collectSRSWViolationsForProcedure + _analyzeGuard/_analyzeTerm +
//// _markConstantTypeVars). Semantics unchanged, including the two SRSW
//// relaxations (constant-type head arguments per paper §5.2.1 / manual §3,
//// ground-implying guards — ruling D6) — and NO escape mechanism of any kind
//// (the Dart `skipGlobalSRSW`/`skipSRSW` flags are deliberately not ported;
//// the check always runs on the parsed module, before partial evaluation).
////
//// Error surface: the Dart analyzer throws CompileError(phase 'analyzer')
//// immediately for guard-position misuse, illegal guard negation, and
//// reserved '_'-constants in user mode; SRSW violations aggregate across the
//// whole module into one report. The variants below keep those channels
//// distinct so the loader (T020) can map them onto staged-diagnostic classes
//// (SRSW violation vs guard violation).

import gleam/dict.{type Dict}
import gleam/int
import gleam/list
import gleam/option.{None, Some}
import gleam/result
import gleam/set.{type Set}
import gleam/string
import glp/analysis/type_ast.{type Pos, type ProcDecl, Pos}
import glp/parser/ast
import glp/runtime/terms

/// An SRSW-stage rejection (Dart CompileError with phase 'analyzer').
pub type SrswError {
  /// Aggregated SRSW violations across the module (Dart reports at 0:0):
  /// `SRSW violations found:\n  • sig: Line N: …`
  SrswViolations(message: String, pos: Pos)
  /// A body-only construct in guard position, or an illegally negated guard.
  GuardPositionError(message: String, pos: Pos)
  /// A reserved '_'-prefixed constant in user mode.
  ReservedConstantError(message: String, pos: Pos)
}

/// Per-variable occurrence record (Dart VariableInfo — only the counters the
/// SRSW checks consume: total writer/reader occurrences for multiplicity and
/// pairing, head/body-only reader occurrences for the multiplicity check).
type VarInfo {
  VarInfo(
    writer_occurrences: Int,
    reader_occurrences: Int,
    reader_head_body: Int,
    first_pos: Pos,
  )
}

/// Per-clause variable table (Dart VariableTable). `order` keeps first-seen
/// order (reversed) — violation output order matches the Dart insertion-
/// ordered map.
type Table {
  Table(
    order: List(String),
    vars: Dict(String, VarInfo),
    grounded: Set(String),
    type_grounded: Set(String),
  )
}

fn new_table() -> Table {
  Table([], dict.new(), set.new(), set.new())
}

/// Check a parsed module. Runs on the original parse (before partial
/// evaluation — guard readers count for pairing) and either passes or
/// rejects; there is no way to skip it.
pub fn check(module: ast.SourceModule) -> Result(Nil, SrswError) {
  let proc_decls =
    list.fold(module.proc_declarations, dict.new(), fn(acc, decl) {
      dict.insert(acc, type_ast.key(decl), decl)
    })
  use violations <- result.try(
    list.try_fold(module.procedures, [], fn(acc, procedure) {
      use procedure_violations <- result.try(check_procedure(
        procedure,
        proc_decls,
        module.compile_mode,
      ))
      Ok(list.append(acc, procedure_violations))
    }),
  )
  case violations {
    [] -> Ok(Nil)
    _ -> {
      let bullet_lines =
        violations
        |> list.map(fn(violation) { "  • " <> violation })
        |> string.join("\n")
      Error(SrswViolations("SRSW violations found:\n" <> bullet_lines, Pos(0, 0)))
    }
  }
}

/// Per-clause variable → register-index map: the Dart Analyzer's
/// `VariableTable` + `_assignRegisters` output, exposed for codegen (T019).
///
/// Register index = the variable's position in first-occurrence order across
/// the clause traversal HEAD → GUARDS → BODY (Dart `_analyzeClause` order;
/// `getAllVars()` iterates `_vars` in insertion order, `_assignRegisters`
/// numbers them 0,1,2,…). Anonymous `_`-prefixed variables are excluded (Dart
/// skips them in the table; codegen allocates temp registers ≥10 for them).
/// Reader `X?` and writer `X` share one register (both keyed by base name).
///
/// Grounding does not affect register ORDER (Dart `markTypeGrounded` touches
/// only the grounded set, never `_vars`), so proc-decls are irrelevant here and
/// an empty set is passed. Codegen runs after the loader's SRSW/type stages
/// have already passed, so for well-formed input `check_clause` does not surface
/// a rejection; a residual guard/reserved-constant error is propagated
/// unchanged rather than swallowed.
pub fn clause_register_map(
  clause: ast.Clause,
  compile_mode: ast.CompileMode,
) -> Result(Dict(String, Int), SrswError) {
  use table <- result.map(check_clause(clause, "", dict.new(), compile_mode))
  table.order
  |> list.reverse
  |> list.index_fold(dict.new(), fn(acc, name, index) {
    dict.insert(acc, name, index)
  })
}

/// Collect SRSW violations for one procedure (Dart
/// `_collectSRSWViolationsForProcedure`), each prefixed `name/arity: `.
fn check_procedure(
  procedure: ast.Procedure,
  proc_decls: Dict(String, ProcDecl),
  compile_mode: ast.CompileMode,
) -> Result(List(String), SrswError) {
  let signature = ast.signature(procedure)
  list.try_fold(procedure.clauses, [], fn(acc, clause) {
    use table <- result.try(check_clause(
      clause,
      signature,
      proc_decls,
      compile_mode,
    ))
    let violations =
      collect_violations(table)
      |> list.map(fn(violation) { signature <> ": " <> violation })
    Ok(list.append(acc, violations))
  })
}

fn check_clause(
  clause: ast.Clause,
  signature: String,
  proc_decls: Dict(String, ProcDecl),
  compile_mode: ast.CompileMode,
) -> Result(Table, SrswError) {
  // Type-based SRSW relaxation: mark head variables with constant types
  let table = mark_constant_type_vars(clause.head, signature, proc_decls, new_table())
  // Head
  use table <- result.try(analyze_args(clause.head.args, table, True, compile_mode))
  // Guards — guard readers ARE counted (for pairing; not for multiplicity)
  use table <- result.try(case clause.guards {
    Some(guards) ->
      list.try_fold(guards, table, fn(table, guard) {
        analyze_guard(guard, table, compile_mode)
      })
    None -> Ok(table)
  })
  // Body
  case clause.body {
    Some(goals) ->
      list.try_fold(goals, table, fn(table, goal) {
        analyze_args(ast.goal_args(goal), table, True, compile_mode)
      })
    None -> Ok(table)
  }
}

// ── Occurrence recording ────────────────────────────────────────────────────

fn upsert(
  table: Table,
  name: String,
  pos: Pos,
  update: fn(VarInfo) -> VarInfo,
) -> Table {
  case dict.get(table.vars, name) {
    Ok(info) -> Table(..table, vars: dict.insert(table.vars, name, update(info)))
    Error(_) ->
      Table(
        ..table,
        order: [name, ..table.order],
        vars: dict.insert(table.vars, name, update(VarInfo(0, 0, 0, pos))),
      )
  }
}

/// Record a writer occurrence; anonymous ('_'-prefixed) variables are exempt.
fn record_writer(table: Table, name: String, pos: Pos) -> Table {
  case string.starts_with(name, "_") {
    True -> table
    False ->
      upsert(table, name, pos, fn(info) {
        VarInfo(..info, writer_occurrences: info.writer_occurrences + 1)
      })
  }
}

/// Record a reader occurrence; `in_head_or_body` gates the multiplicity
/// counter (guard readers count only toward pairing).
fn record_reader(
  table: Table,
  name: String,
  pos: Pos,
  in_head_or_body: Bool,
) -> Table {
  case string.starts_with(name, "_") {
    True -> table
    False ->
      upsert(table, name, pos, fn(info) {
        VarInfo(
          ..info,
          reader_occurrences: info.reader_occurrences + 1,
          reader_head_body: case in_head_or_body {
            True -> info.reader_head_body + 1
            False -> info.reader_head_body
          },
        )
      })
  }
}

fn mark_grounded(table: Table, name: String) -> Table {
  Table(..table, grounded: set.insert(table.grounded, name))
}

fn allows_multiple(table: Table, name: String) -> Bool {
  set.contains(table.grounded, name) || set.contains(table.type_grounded, name)
}

// ── Term analysis (Dart _analyzeTerm) ───────────────────────────────────────

fn analyze_args(
  args: List(ast.Term),
  table: Table,
  in_head_or_body: Bool,
  compile_mode: ast.CompileMode,
) -> Result(Table, SrswError) {
  list.try_fold(args, table, fn(table, term) {
    analyze_term(term, table, in_head_or_body, compile_mode)
  })
}

fn analyze_term(
  term: ast.Term,
  table: Table,
  in_head_or_body: Bool,
  compile_mode: ast.CompileMode,
) -> Result(Table, SrswError) {
  case term {
    ast.VarTerm(name, is_reader, pos) ->
      case is_reader {
        True -> Ok(record_reader(table, name, pos, in_head_or_body))
        False -> Ok(record_writer(table, name, pos))
      }
    ast.StructTerm(_, args, _) ->
      analyze_args(args, table, in_head_or_body, compile_mode)
    ast.ListTerm(head, tail, _) -> {
      use table <- result.try(case head {
        Some(head_term) ->
          analyze_term(head_term, table, in_head_or_body, compile_mode)
        None -> Ok(table)
      })
      case tail {
        Some(tail_term) ->
          analyze_term(tail_term, table, in_head_or_body, compile_mode)
        None -> Ok(table)
      }
    }
    ast.ConstTerm(value, pos) ->
      // Reserved constants in user mode (Dart _analyzeTerm ConstTerm branch;
      // only atoms can carry a leading '_' — string literals are typed apart)
      case compile_mode, value {
        ast.User, terms.ConstAtom(name) ->
          case string.starts_with(name, "_") {
            True ->
              Error(ReservedConstantError(
                "Constants starting with '_' are reserved for system use: '"
                  <> name
                  <> "'. Use -mode(system). directive for system code.",
                pos,
              ))
            False -> Ok(table)
          }
        _, _ -> Ok(table)
      }
    ast.UnderscoreTerm(_, _) -> Ok(table)
  }
}

// ── Guard analysis (Dart _analyzeGuard) ─────────────────────────────────────

/// Body-only constructs that are NOT valid guards.
fn is_invalid_in_guard_position(predicate: String) -> Bool {
  case predicate {
    "true" | "false" | "fail" -> True
    _ -> False
  }
}

/// Guards that cannot be negated (type-error semantics or special behavior);
/// the @<-family ruling: the natural complement of @< is @>= (OQ-G2).
fn is_non_negatable(predicate: String) -> Bool {
  case predicate {
    "<" | ">" | "=<" | ">=" | "=:=" | "=\\=" -> True
    "@<" | "@>" | "@=<" | "@>=" -> True
    "otherwise" | "wait" | "wait_until" | "satisfiable" -> True
    _ -> False
  }
}

/// Unary guards whose success implies the argument is ground (or safely
/// re-readable): ground itself, the type-check guards, module, is_mutual_ref,
/// unknown, wait_until, wait.
fn is_unary_grounding_guard(predicate: String) -> Bool {
  case predicate {
    "ground" -> True
    "number" | "integer" | "atom" | "string" | "list" | "tuple" | "compound"
    | "constant" -> True
    "module" | "is_mutual_ref" | "unknown" | "wait_until" | "wait" -> True
    _ -> False
  }
}

/// Comparison guards — ground-implying for BOTH operands, recursively through
/// arithmetic expressions (Dart comparisonOps + _extractAndMarkGroundedVars).
fn is_comparison_guard(predicate: String) -> Bool {
  case predicate {
    "<" | ">" | "=<" | ">=" | "=:=" | "=\\=" -> True
    "@<" | "@>" | "@=<" | "@>=" -> True
    _ -> False
  }
}

fn analyze_guard(
  guard: ast.Guard,
  table: Table,
  compile_mode: ast.CompileMode,
) -> Result(Table, SrswError) {
  use _ <- result.try(case is_invalid_in_guard_position(guard.predicate) {
    True ->
      Error(GuardPositionError(
        "\""
          <> guard.predicate
          <> "\" is not a guard - it can only appear in body position",
        guard.pos,
      ))
    False -> Ok(Nil)
  })
  use _ <- result.try(case guard.negated && is_non_negatable(guard.predicate) {
    True ->
      Error(GuardPositionError(
        "Guard \""
          <> guard.predicate
          <> "\" cannot be negated (type-error semantics)",
        guard.pos,
      ))
    False -> Ok(Nil)
  })
  // Ground-implying markings
  let table = case guard.predicate, guard.args {
    predicate, [ast.VarTerm(name, _, _)] ->
      case is_unary_grounding_guard(predicate) {
        True -> mark_grounded(table, name)
        False -> table
      }
    predicate, [left, right] ->
      case is_comparison_guard(predicate) {
        True ->
          table
          |> mark_grounded_recursive(left)
          |> mark_grounded_recursive(right)
        False ->
          // Ground equality =?= marks its (shallow) variable arguments
          case predicate {
            "=?=" ->
              list.fold([left, right], table, fn(table, arg) {
                case arg {
                  ast.VarTerm(name, _, _) -> mark_grounded(table, name)
                  _ -> table
                }
              })
            _ -> table
          }
      }
    _, _ -> table
  }
  // Guard occurrences do NOT count toward SRSW multiplicity (in_head_or_body
  // False) but DO count toward reader/writer pairing.
  analyze_args(guard.args, table, False, compile_mode)
}

/// Mark every variable in a term (writer or reader name) as grounded —
/// comparison guard arguments may be arithmetic expressions
/// (Dart _extractAndMarkGroundedVars).
fn mark_grounded_recursive(table: Table, term: ast.Term) -> Table {
  case term {
    ast.VarTerm(name, _, _) -> mark_grounded(table, name)
    ast.StructTerm(_, args, _) -> list.fold(args, table, mark_grounded_recursive)
    ast.ListTerm(head, tail, _) -> {
      let table = case head {
        Some(head_term) -> mark_grounded_recursive(table, head_term)
        None -> table
      }
      case tail {
        Some(tail_term) -> mark_grounded_recursive(table, tail_term)
        None -> table
      }
    }
    ast.ConstTerm(_, _) | ast.UnderscoreTerm(_, _) -> table
  }
}

// ── Constant-type relaxation (Dart _markConstantTypeVars) ──────────────────

fn is_constant_type(name: String) -> Bool {
  case name {
    "Integer" | "Real" | "Number" | "String" | "Constant" -> True
    _ -> False
  }
}

fn mark_constant_type_vars(
  head: ast.Atom,
  signature: String,
  proc_decls: Dict(String, ProcDecl),
  table: Table,
) -> Table {
  case dict.get(proc_decls, signature) {
    Error(_) -> table
    Ok(decl) ->
      list.zip(head.args, decl.arg_types)
      |> list.fold(table, fn(table, pair) {
        let #(arg, arg_type) = pair
        case type_ast.type_name(arg_type) {
          Some(type_name) ->
            case is_constant_type(type_name) {
              True -> mark_type_grounded_recursive(table, arg)
              False -> table
            }
          None -> table
        }
      })
  }
}

fn mark_type_grounded_recursive(table: Table, term: ast.Term) -> Table {
  case term {
    ast.VarTerm(name, _, _) ->
      Table(..table, type_grounded: set.insert(table.type_grounded, name))
    ast.StructTerm(_, args, _) ->
      list.fold(args, table, mark_type_grounded_recursive)
    ast.ListTerm(head, tail, _) -> {
      let table = case head {
        Some(head_term) -> mark_type_grounded_recursive(table, head_term)
        None -> table
      }
      case tail {
        Some(tail_term) -> mark_type_grounded_recursive(table, tail_term)
        None -> table
      }
    }
    ast.ConstTerm(_, _) | ast.UnderscoreTerm(_, _) -> table
  }
}

// ── Violation collection (Dart collectSRSWViolations) ──────────────────────

fn collect_violations(table: Table) -> List(String) {
  table.order
  |> list.reverse
  |> list.flat_map(fn(name) {
    let assert Ok(info) = dict.get(table.vars, name)
    let line = int.to_string(info.first_pos.line)
    let multiple_ok = allows_multiple(table, name)
    let writer_violation = case
      info.writer_occurrences > 1 && !multiple_ok
    {
      True -> [
        "Line "
        <> line
        <> ": Writer variable \""
        <> name
        <> "\" occurs "
        <> int.to_string(info.writer_occurrences)
        <> " times without ground guard or constant type",
      ]
      False -> []
    }
    let reader_violation = case info.reader_head_body > 1 && !multiple_ok {
      True -> [
        "Line "
        <> line
        <> ": Reader variable \""
        <> name
        <> "?\" occurs "
        <> int.to_string(info.reader_head_body)
        <> " times without ground guard or constant type",
      ]
      False -> []
    }
    let no_writer = case info.writer_occurrences == 0 {
      True -> [
        "Line "
        <> line
        <> ": Variable \""
        <> name
        <> "\" has no writer (must have exactly one)",
      ]
      False -> []
    }
    // Guard occurrences count toward pairing — total reader occurrences here.
    let no_reader = case
      info.reader_occurrences == 0 && info.writer_occurrences > 0
    {
      True -> [
        "Line "
        <> line
        <> ": Variable \""
        <> name
        <> "\" has no reader (must have exactly one)",
      ]
      False -> []
    }
    list.flatten([writer_violation, reader_violation, no_writer, no_reader])
  })
}

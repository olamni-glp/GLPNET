//// glp/compiler/codegen — v2.16 bytecode code generation (feature 050, T019).
////
//// Dart source of truth: glp_runtime/lib/compiler/codegen.dart (`CodeGenerator`).
//// Faithful port of the emit sequence: each Dart `_generate*` method becomes a
//// Gleam function threading an immutable `Ctx` (the Dart `CodeGenContext` is
//// mutable — here `emit` prepends to a reversed accumulator, reversed once at
//// the end; `allocateTemp` returns `#(Ctx, Int)`). No instruction is added,
//// reordered, or renamed relative to the reference.
////
//// Register model (Dart `Analyzer._assignRegisters`): the per-clause variable →
//// register-index map is supplied by `srsw.clause_register_map` — the single,
//// SRSW-verified source of the first-occurrence traversal order (T019 folds the
//// register-allocation prerequisite here per the option-(a) ruling). Anonymous
//// `_`-vars carry no named register; codegen allocates temporaries ≥ 10 for them
//// exactly as the reference does (`resetTemps`).
////
//// One deliberate divergence from the reference, escalated separately and NOT
//// exercised by the P5 merge smoke check: Dart emits `UnifyConstant(<runtime
//// StructTerm>)` for a GROUND list literal appearing inside a BODY argument
//// structure (codegen.dart:710) as an optimization. The Gleam `UnifyConstant`
//// holds only a `Constant` (atom/int/real/string), never a structure, so such
//// ground lists are built structurally like non-ground ones — semantically
//// identical, differing only in the emitted opcode sequence for that one case.

import gleam/dict.{type Dict}
import gleam/int
import gleam/list
import gleam/option.{type Option, None, Some}
import gleam/set.{type Set}
import gleam/string
import glp/analysis/srsw
import glp/bytecode/guard_defs.{
  type GTerm, type GuardProcSpec, GConst, GGuardSpec, GStruct, GVar,
  GuardClauseSpec, GuardProcSpec,
}
import glp/bytecode/opcodes.{type Op}
import glp/bytecode/program.{type BytecodeProgram}
import glp/compiler/partial_eval
import glp/parser/ast
import glp/runtime/terms.{ConstAtom}

// ============================================================================
// Code-generation context (Dart CodeGenContext)
// ============================================================================

/// Threaded generator state. `rev_ops` is the instruction stream in REVERSE
/// (prepend on `emit`, reversed once at the end). `next_temp`/`seen_head`/`regs`
/// are clause-scoped (reset at each clause start — Dart `resetTemps` +
/// `seenHeadVars.clear`); `imports`/`next_import` are program-scoped (Dart
/// `ImportTable`).
type Ctx {
  Ctx(
    rev_ops: List(Op),
    next_temp: Int,
    seen_head: Set(String),
    regs: Dict(String, Int),
    imports: Dict(String, Int),
    next_import: Int,
    mode: ast.CompileMode,
  )
}

fn emit(ctx: Ctx, op: Op) -> Ctx {
  Ctx(..ctx, rev_ops: [op, ..ctx.rev_ops])
}

/// Allocate a fresh temporary X register (Dart `allocateTemp`: `nextTempVar++`).
fn alloc_temp(ctx: Ctx) -> #(Ctx, Int) {
  #(Ctx(..ctx, next_temp: ctx.next_temp + 1), ctx.next_temp)
}

/// Register index of a named variable. Present by construction — the register
/// map is built from the SAME clause traversal, and codegen runs after the
/// loader's SRSW/type stages have passed (Dart throws `CompileError` on a miss;
/// here it is an internal invariant).
fn reg(ctx: Ctx, name: String) -> Int {
  let assert Ok(index) = dict.get(ctx.regs, name)
  index
}

/// Look up or assign a 1-indexed import position (Dart `ImportTable.addImport`).
fn add_import(ctx: Ctx, name: String) -> #(Ctx, Int) {
  case dict.get(ctx.imports, name) {
    Ok(index) -> #(ctx, index)
    Error(_) -> {
      let index = ctx.next_import
      #(
        Ctx(
          ..ctx,
          imports: dict.insert(ctx.imports, name, index),
          next_import: index + 1,
        ),
        index,
      )
    }
  }
}

/// Base variable name (Dart `baseVarName`: strip a trailing reader `?`). The
/// Gleam AST keeps the reader mark in `is_reader`, so names carry no `?`; the
/// strip mirrors the reference defensively.
fn base_name(name: String) -> String {
  case string.ends_with(name, "?") {
    True -> string.drop_end(name, 1)
    False -> name
  }
}

// ============================================================================
// Entry point (Dart CodeGenerator.generate / generateWithMetadata)
// ============================================================================

/// Compile a (parsed, partially-evaluated) module to a v2.16 bytecode program.
pub fn generate(module: ast.SourceModule) -> BytecodeProgram {
  let defined_guards = build_defined_guards(module)
  let ctx0 =
    Ctx(
      rev_ops: [],
      next_temp: 10,
      seen_head: set.new(),
      regs: dict.new(),
      imports: dict.new(),
      next_import: 1,
      mode: module.compile_mode,
    )
  let ctx =
    list.fold(module.procedures, ctx0, fn(ctx, procedure) {
      gen_procedure(ctx, procedure)
    })
  // Surface the module-RPC import table (feature 059 T078): invert the codegen
  // `name → 1-based index` map into the runtime `index → name` table a `Distribute`
  // opcode resolves against.
  let imports =
    dict.fold(ctx.imports, dict.new(), fn(acc, name, index) {
      dict.insert(acc, index, name)
    })
  program.from_ops(list.reverse(ctx.rev_ops), defined_guards)
  |> program.with_imports(imports)
}

// ============================================================================
// Runtime-defined guard side table (Dart _collectDefinedGuardSpecs)
// ============================================================================

fn build_defined_guards(
  module: ast.SourceModule,
) -> Dict(String, GuardProcSpec) {
  let keys = partial_eval.collect_test_only_procedures(module.procedures)
  case set.is_empty(keys) {
    True -> dict.new()
    False ->
      list.fold(module.procedures, dict.new(), fn(acc, procedure) {
        let key = ast.signature(procedure)
        case set.contains(keys, key) {
          False -> acc
          True -> {
            let clauses =
              list.map(procedure.clauses, fn(clause) {
                let head_args = list.map(clause.head.args, term_to_gterm)
                let guards = case clause.guards {
                  Some(gs) ->
                    list.map(gs, fn(g) {
                      GGuardSpec(
                        g.predicate,
                        list.map(g.args, term_to_gterm),
                        g.negated,
                      )
                    })
                  None -> []
                }
                GuardClauseSpec(head_args, guards)
              })
            dict.insert(
              acc,
              key,
              GuardProcSpec(procedure.name, procedure.arity, clauses),
            )
          }
        }
      })
  }
}

/// Dart `_termToGTerm`: neutral guard-spec shape; lists become '.'/2 structures
/// with a 'nil' terminator.
fn term_to_gterm(term: ast.Term) -> GTerm {
  case term {
    ast.VarTerm(name, is_reader, _) -> GVar(name, is_reader)
    ast.UnderscoreTerm(_, _) -> GVar("_", False)
    ast.ConstTerm(value, _) -> GConst(value)
    ast.ListTerm(None, None, _) -> GConst(ConstAtom("nil"))
    ast.ListTerm(head, tail, _) -> {
      let h = case head {
        Some(t) -> term_to_gterm(t)
        None -> GConst(ConstAtom("nil"))
      }
      let t = case tail {
        Some(t) -> term_to_gterm(t)
        None -> GConst(ConstAtom("nil"))
      }
      GStruct(".", [h, t])
    }
    ast.StructTerm(functor, args, _) ->
      GStruct(functor, list.map(args, term_to_gterm))
  }
}

// ============================================================================
// Procedure / clause (Dart _generateProcedure / _generateClause)
// ============================================================================

fn gen_procedure(ctx: Ctx, procedure: ast.Procedure) -> Ctx {
  let entry_label = ast.signature(procedure)
  let ctx = emit(ctx, opcodes.Label(entry_label))
  let ctx =
    list.index_fold(procedure.clauses, ctx, fn(ctx, clause, i) {
      gen_clause(ctx, clause, entry_label, i)
    })
  let ctx = emit(ctx, opcodes.Label(entry_label <> "_end"))
  emit(ctx, opcodes.NoMoreClauses)
}

fn gen_clause(
  ctx: Ctx,
  clause: ast.Clause,
  entry_label: String,
  clause_index: Int,
) -> Ctx {
  // Register allocation for this clause (Dart resetTemps over varTable).
  let assert Ok(regs) = srsw.clause_register_map(clause, ctx.mode)
  let reg_count = dict.size(regs)
  let next_temp = case reg_count > 10 {
    True -> reg_count
    False -> 10
  }
  let ctx = Ctx(..ctx, regs: regs, next_temp: next_temp, seen_head: set.new())

  // Label for non-first clauses (Dart "_c<index>").
  let ctx = case clause_index > 0 {
    True ->
      emit(
        ctx,
        opcodes.Label(entry_label <> "_c" <> int.to_string(clause_index)),
      )
    False -> ctx
  }

  // CLAUSE_TRY (Si=∅, σ̂w=∅).
  let ctx = emit(ctx, opcodes.ClauseTry)

  // HEAD phase.
  let ctx =
    list.index_fold(clause.head.args, ctx, fn(ctx, arg, i) {
      gen_head_arg(ctx, arg, i)
    })

  // GUARD phase.
  let ctx = case clause.guards {
    Some(guards) -> list.fold(guards, ctx, gen_guard)
    None -> ctx
  }

  // COMMIT (apply σ̂w, enter BODY).
  let ctx = emit(ctx, opcodes.Commit)

  // BODY phase. A body that is exactly `true/0` is a fact — just proceed.
  case clause.body {
    Some([ast.Goal("true", [], _)]) -> emit(ctx, opcodes.Proceed)
    Some(goals) -> gen_body(ctx, goals)
    None -> emit(ctx, opcodes.Proceed)
  }
}

// ============================================================================
// HEAD arguments (Dart _generateHeadArgument)
// ============================================================================

fn gen_head_arg(ctx: Ctx, term: ast.Term, arg_slot: Int) -> Ctx {
  case term {
    ast.VarTerm(name, is_reader, _) -> {
      let r = reg(ctx, name)
      let base = base_name(name)
      case set.contains(ctx.seen_head, base) {
        False -> {
          let ctx = emit(ctx, opcodes.GetVariable(r, arg_slot, is_reader))
          Ctx(..ctx, seen_head: set.insert(ctx.seen_head, base))
        }
        True -> emit(ctx, opcodes.GetValue(r, arg_slot, is_reader))
      }
    }
    ast.ConstTerm(value, _) -> emit(ctx, opcodes.HeadConstant(value, arg_slot))
    ast.ListTerm(None, None, _) -> emit(ctx, opcodes.HeadNil(arg_slot))
    ast.ListTerm(head, tail, _) -> {
      let ctx = emit(ctx, opcodes.HeadStructure(".", 2, arg_slot))
      let ctx = gen_struct_elem_opt(ctx, head, True)
      gen_struct_elem_opt(ctx, tail, True)
    }
    ast.StructTerm(functor, args, _) -> {
      // Extract the argument into a temp, then match the structure there
      // (avoids overlapping HeadStructure on the argument register).
      let #(ctx, temp) = alloc_temp(ctx)
      let ctx = emit(ctx, opcodes.GetVariable(temp, arg_slot, False))
      let ctx =
        emit(ctx, opcodes.HeadStructure(functor, list.length(args), temp))
      list.fold(args, ctx, fn(ctx, sub) { gen_struct_elem(ctx, sub, True) })
    }
    // Anonymous variable as a direct head argument: not extracted.
    ast.UnderscoreTerm(_, _) -> ctx
  }
}

// ============================================================================
// Structure elements under S (Dart _generateStructureElement)
// ============================================================================

fn gen_struct_elem_opt(ctx: Ctx, term: Option(ast.Term), in_head: Bool) -> Ctx {
  case term {
    Some(t) -> gen_struct_elem(ctx, t, in_head)
    None -> ctx
  }
}

fn gen_struct_elem(ctx: Ctx, term: ast.Term, in_head: Bool) -> Ctx {
  case term {
    ast.VarTerm(name, is_reader, _) ->
      emit(ctx, opcodes.UnifyVariable(reg(ctx, name), is_reader))
    ast.ConstTerm(value, _) -> emit(ctx, opcodes.UnifyConstant(value))
    ast.ListTerm(None, None, _) ->
      emit(ctx, opcodes.UnifyConstant(ConstAtom("nil")))
    ast.ListTerm(head, tail, _) ->
      case in_head {
        True -> {
          let #(ctx, save) = alloc_temp(ctx)
          let ctx = emit(ctx, opcodes.Push(save))
          let ctx = emit(ctx, opcodes.UnifyStructure(".", 2))
          let ctx = gen_struct_elem_opt(ctx, head, True)
          let ctx = gen_struct_elem_opt(ctx, tail, True)
          let ctx = emit(ctx, opcodes.Pop(save))
          emit(ctx, opcodes.UnifyVariable(save, False))
        }
        False -> {
          let #(ctx, temp) = alloc_temp(ctx)
          let ctx = emit(ctx, opcodes.PutStructure(".", 2, temp))
          let ctx = gen_struct_elem_opt(ctx, head, False)
          let ctx = gen_struct_elem_opt(ctx, tail, False)
          emit(ctx, opcodes.UnifyVariable(temp, False))
        }
      }
    ast.StructTerm(functor, args, _) ->
      case in_head {
        True -> {
          let #(ctx, save) = alloc_temp(ctx)
          let ctx = emit(ctx, opcodes.Push(save))
          let ctx =
            emit(ctx, opcodes.UnifyStructure(functor, list.length(args)))
          let ctx =
            list.fold(args, ctx, fn(ctx, sub) { gen_struct_elem(ctx, sub, True) })
          let ctx = emit(ctx, opcodes.Pop(save))
          emit(ctx, opcodes.UnifyVariable(save, False))
        }
        False -> {
          let #(ctx, temp) = alloc_temp(ctx)
          let ctx =
            emit(ctx, opcodes.PutStructure(functor, list.length(args), temp))
          let ctx =
            list.fold(args, ctx, fn(ctx, sub) {
              gen_struct_elem(ctx, sub, False)
            })
          emit(ctx, opcodes.UnifyVariable(temp, False))
        }
      }
    ast.UnderscoreTerm(_, _) -> emit(ctx, opcodes.UnifyVoid(1))
  }
}

// ============================================================================
// Guards (Dart _generateGuard)
// ============================================================================

fn gen_guard(ctx: Ctx, guard: ast.Guard) -> Ctx {
  case guard.predicate, guard.args {
    "ground", [ast.VarTerm(name, _, _)] ->
      case dict.get(ctx.regs, name) {
        Ok(r) -> emit(ctx, opcodes.Ground(r, guard.negated))
        Error(_) -> generic_guard(ctx, guard)
      }
    "known", [ast.VarTerm(name, _, _)] ->
      case dict.get(ctx.regs, name) {
        Ok(r) -> emit(ctx, opcodes.Known(r, guard.negated))
        Error(_) -> generic_guard(ctx, guard)
      }
    "no_readers", [ast.VarTerm(name, _, _)] ->
      case dict.get(ctx.regs, name) {
        Ok(r) -> emit(ctx, opcodes.NoReaders(r, guard.negated))
        Error(_) -> generic_guard(ctx, guard)
      }
    "otherwise", [] -> emit(ctx, opcodes.Otherwise)
    "=?=", [ast.VarTerm(left, _, _), ast.VarTerm(right, _, _)] ->
      case dict.get(ctx.regs, left), dict.get(ctx.regs, right) {
        Ok(lr), Ok(rr) -> emit(ctx, opcodes.GroundEqual(lr, rr, guard.negated))
        _, _ -> generic_guard(ctx, guard)
      }
    _, _ -> generic_guard(ctx, guard)
  }
}

fn generic_guard(ctx: Ctx, guard: ast.Guard) -> Ctx {
  let ctx =
    list.index_fold(guard.args, ctx, fn(ctx, arg, i) {
      gen_put_arg(ctx, arg, i)
    })
  emit(
    ctx,
    opcodes.Guard(guard.predicate, list.length(guard.args), guard.negated),
  )
}

// ============================================================================
// BODY goals (Dart _generateBody / _generateRemoteGoal)
// ============================================================================

fn gen_body(ctx: Ctx, goals: List(ast.Goal)) -> Ctx {
  let ctx =
    list.fold(goals, ctx, fn(ctx, goal) {
      case goal {
        ast.RemoteGoal(_, _, _) -> gen_remote_goal(ctx, goal)
        ast.SpawnGoal(inner, _, _) -> {
          // dGLP: ignore the @AgentId annotation, run the inner goal.
          let ctx =
            list.index_fold(inner.args, ctx, fn(ctx, arg, j) {
              gen_put_arg(ctx, arg, j)
            })
          let arity = list.length(inner.args)
          emit(
            ctx,
            opcodes.Spawn(inner.functor <> "/" <> int.to_string(arity), arity),
          )
        }
        ast.Goal(functor, args, _) -> {
          let ctx =
            list.index_fold(args, ctx, fn(ctx, arg, j) {
              gen_put_arg(ctx, arg, j)
            })
          let arity = list.length(args)
          emit(
            ctx,
            opcodes.Spawn(functor <> "/" <> int.to_string(arity), arity),
          )
        }
      }
    })
  emit(ctx, opcodes.Proceed)
}

fn gen_remote_goal(ctx: Ctx, remote: ast.Goal) -> Ctx {
  case remote {
    ast.RemoteGoal(module_term, inner, _) -> {
      let inner_functor = ast.goal_functor(inner)
      let inner_args = ast.goal_args(inner)
      let inner_arity = list.length(inner_args)
      let ctx =
        list.index_fold(inner_args, ctx, fn(ctx, arg, j) {
          gen_put_arg(ctx, arg, j)
        })
      case module_term {
        // Dynamic module: transmit # {ModuleVar, Goal}.
        ast.VarTerm(name, _, _) ->
          emit(ctx, opcodes.Transmit(reg(ctx, name), inner_functor, inner_arity))
        // Static module: distribute # {Index, Goal}.
        _ -> {
          let #(ctx, index) = add_import(ctx, static_module_name(module_term))
          emit(ctx, opcodes.Distribute(index, inner_functor, inner_arity))
        }
      }
    }
    _ -> ctx
  }
}

/// The static module name of a `Module # Goal` head (Dart `staticModuleName`):
/// a `ConstTerm` atom. Any other term is malformed for a static remote and is
/// rendered for a best-effort import key.
fn static_module_name(term: ast.Term) -> String {
  case term {
    ast.ConstTerm(terms.ConstAtom(name), _) -> name
    _ -> ast.term_to_string(term)
  }
}

// ============================================================================
// BODY argument construction (Dart _generatePutArgument)
// ============================================================================

fn gen_put_arg(ctx: Ctx, term: ast.Term, arg_slot: Int) -> Ctx {
  case term {
    ast.VarTerm(name, is_reader, _) ->
      emit(ctx, opcodes.PutVariable(reg(ctx, name), arg_slot, is_reader))
    ast.ConstTerm(value, _) -> emit(ctx, opcodes.PutBoundConst(value, arg_slot))
    ast.ListTerm(None, None, _) -> emit(ctx, opcodes.PutBoundNil(arg_slot))
    ast.ListTerm(head, tail, _) -> {
      let ctx = emit(ctx, opcodes.PutStructure(".", 2, arg_slot))
      let ctx = gen_arg_struct_elem_opt(ctx, head)
      gen_arg_struct_elem_opt(ctx, tail)
    }
    ast.StructTerm(functor, args, _) -> {
      let ctx =
        emit(ctx, opcodes.PutStructure(functor, list.length(args), arg_slot))
      list.fold(args, ctx, gen_arg_struct_elem)
    }
    ast.UnderscoreTerm(_, _) -> {
      let #(ctx, temp) = alloc_temp(ctx)
      emit(ctx, opcodes.PutVariable(temp, arg_slot, False))
    }
  }
}

// ============================================================================
// Argument-structure elements (Dart _generateArgumentStructureElement)
// ============================================================================

fn gen_arg_struct_elem_opt(ctx: Ctx, term: Option(ast.Term)) -> Ctx {
  case term {
    Some(t) -> gen_arg_struct_elem(ctx, t)
    None -> ctx
  }
}

fn gen_arg_struct_elem(ctx: Ctx, term: ast.Term) -> Ctx {
  case term {
    ast.VarTerm(name, is_reader, _) ->
      emit(ctx, opcodes.UnifyVariable(reg(ctx, name), is_reader))
    ast.ConstTerm(value, _) -> emit(ctx, opcodes.UnifyConstant(value))
    ast.ListTerm(None, None, _) ->
      emit(ctx, opcodes.UnifyConstant(ConstAtom("nil")))
    // Non-empty list built as '.'/2 (see module note re: the ground-list
    // constant optimization, deliberately not reproduced).
    ast.ListTerm(head, tail, _) -> {
      let ctx = emit(ctx, opcodes.PutStructure(".", 2, -1))
      let ctx = gen_struct_elem_in_body_opt(ctx, head)
      gen_list_tail_in_body_opt(ctx, tail)
    }
    ast.StructTerm(functor, args, _) -> {
      let ctx = emit(ctx, opcodes.PutStructure(functor, list.length(args), -1))
      list.fold(args, ctx, gen_struct_elem_in_body)
    }
    ast.UnderscoreTerm(_, _) -> emit(ctx, opcodes.UnifyVoid(1))
  }
}

// ============================================================================
// BODY structure elements (Dart _generateStructureElementInBody)
// ============================================================================

fn gen_struct_elem_in_body_opt(ctx: Ctx, term: Option(ast.Term)) -> Ctx {
  case term {
    Some(t) -> gen_struct_elem_in_body(ctx, t)
    None -> ctx
  }
}

fn gen_struct_elem_in_body(ctx: Ctx, term: ast.Term) -> Ctx {
  case term {
    ast.VarTerm(name, is_reader, _) ->
      emit(ctx, opcodes.SetVariable(reg(ctx, name), is_reader))
    ast.ConstTerm(value, _) -> emit(ctx, opcodes.SetConstant(value))
    ast.ListTerm(None, None, _) ->
      emit(ctx, opcodes.SetConstant(ConstAtom("nil")))
    ast.ListTerm(head, tail, _) -> {
      let ctx = emit(ctx, opcodes.PutStructure(".", 2, -1))
      let ctx = gen_struct_elem_in_body_opt(ctx, head)
      gen_list_tail_in_body_opt(ctx, tail)
    }
    ast.StructTerm(functor, args, _) -> {
      let ctx = emit(ctx, opcodes.PutStructure(functor, list.length(args), -1))
      list.fold(args, ctx, gen_struct_elem_in_body)
    }
    ast.UnderscoreTerm(_, _) -> {
      let #(ctx, temp) = alloc_temp(ctx)
      emit(ctx, opcodes.SetVariable(temp, False))
    }
  }
}

// ============================================================================
// BODY list tails (Dart _generateListTailInBody)
// ============================================================================

fn gen_list_tail_in_body_opt(ctx: Ctx, term: Option(ast.Term)) -> Ctx {
  case term {
    Some(t) -> gen_list_tail_in_body(ctx, t)
    None -> ctx
  }
}

fn gen_list_tail_in_body(ctx: Ctx, term: ast.Term) -> Ctx {
  case term {
    ast.ListTerm(None, None, _) ->
      emit(ctx, opcodes.SetConstant(ConstAtom("nil")))
    ast.ListTerm(head, tail, _) -> {
      let ctx = emit(ctx, opcodes.PutStructure(".", 2, -1))
      let ctx = gen_struct_elem_in_body_opt(ctx, head)
      gen_list_tail_in_body_opt(ctx, tail)
    }
    _ -> gen_struct_elem_in_body(ctx, term)
  }
}

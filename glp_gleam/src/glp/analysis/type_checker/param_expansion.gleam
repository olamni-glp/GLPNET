//// glp/analysis/type_checker/param_expansion — expand parameterized types to
//// monomorphic equivalents (feature 050, T018).
////
//// Dart source of truth: glp_runtime/lib/analysis/type_checker/
//// param_expansion.dart (spec: docs type system/typed-program.md
//// "Parameterized Types"; paper §8 Definition 8.1). Runs after parsing and
//// before type-automaton construction; a pure transformation — missing
//// templates and arity mismatches are skipped here and caught by the type
//// checker later.
////
//// Port notes: the Dart `instantiations` map is insertion-ordered and is
//// MUTATED during substitution (newly discovered instantiations join the
//// worklist) — the port threads an order-keeping `Insts` value through
//// substitution instead. Expanded names use the Dart `TypeExpr.toString()`
//// rendering (type_ast.type_expr_to_string) so canonical names like
//// `Stream<Integer>` are byte-identical. Dart's ProcDecl/TypeDef copies drop
//// `isBuiltin`/`typeParams` (not propagated through the constructors) — the
//// port mirrors that exactly.

import gleam/dict.{type Dict}
import gleam/list
import gleam/set.{type Set}
import gleam/string
import glp/analysis/type_ast.{
  type ProcDecl, type TypeDef, type TypeExpr, ProcDecl, TypeDef,
}
import glp/parser/ast

/// Insertion-ordered instantiation registry (Dart's LinkedHashMap
/// `expanded name -> type args` with putIfAbsent semantics).
type Insts {
  Insts(order_reversed: List(String), by_name: Dict(String, List(TypeExpr)))
}

fn insts_new() -> Insts {
  Insts([], dict.new())
}

/// Dart `putIfAbsent`: first registration wins (value AND order).
fn insts_put_if_absent(
  insts: Insts,
  name: String,
  type_args: List(TypeExpr),
) -> Insts {
  case dict.has_key(insts.by_name, name) {
    True -> insts
    False ->
      Insts(
        order_reversed: [name, ..insts.order_reversed],
        by_name: dict.insert(insts.by_name, name, type_args),
      )
  }
}

fn insts_order(insts: Insts) -> List(String) {
  list.reverse(insts.order_reversed)
}

/// Expand all parameterized types in a module to monomorphic equivalents
/// (Dart `expandParameterizedTypes`). Returns a new module with only
/// monomorphic type definitions and procedure declarations; parameterized
/// procedure declarations are preserved as templates in `param_proc_decls`
/// for call-site inference (Case B).
pub fn expand_parameterized_types(
  module: ast.SourceModule,
  known_type_names: Set(String),
  external_templates: Dict(String, TypeDef),
) -> ast.SourceModule {
  // Step 1: separate templates from monomorphic types; local templates take
  // precedence over external (prelude/ancestor) ones.
  let #(local_templates, mono_type_defs) =
    list.fold(module.type_defs, #(dict.new(), []), fn(acc, td) {
      let #(templates, monos) = acc
      case type_ast.is_parameterized_def(td) {
        True -> #(dict.insert(templates, td.name, td), monos)
        False -> #(templates, [td, ..monos])
      }
    })
  let mono_type_defs = list.reverse(mono_type_defs)
  let templates =
    dict.fold(external_templates, local_templates, fn(acc, name, td) {
      case dict.has_key(acc, name) {
        True -> acc
        False -> dict.insert(acc, name, td)
      }
    })

  // Known monomorphic type names: used to collapse all-wildcard expansions
  // (e.g. Stream(_) → Stream) when a monomorphic version exists.
  let mono_names =
    list.fold(mono_type_defs, known_type_names, fn(acc, td) {
      set.insert(acc, td.name)
    })

  // Step 2: collect all instantiations from type defs and proc decls.
  let insts =
    list.fold(mono_type_defs, insts_new(), fn(insts, td) {
      list.fold(td.alternatives, insts, fn(insts, alt) {
        collect_instantiations(alt, templates, insts)
      })
    })
  let insts =
    list.fold(module.proc_declarations, insts, fn(insts, pd) {
      let proc_type_params =
        detect_proc_type_params(pd, templates, mono_type_defs, known_type_names)
      case proc_type_params {
        [] ->
          list.fold(pd.arg_types, insts, fn(insts, arg) {
            collect_instantiations(arg, templates, insts)
          })
        _ ->
          list.fold(pd.arg_types, insts, fn(insts, arg) {
            collect_instantiations_in_template(
              arg,
              templates,
              insts,
              proc_type_params,
            )
          })
      }
    })
  let insts =
    dict.fold(templates, insts, fn(insts, _, td) {
      list.fold(td.alternatives, insts, fn(insts, alt) {
        collect_instantiations_in_template(alt, templates, insts, td.type_params)
      })
    })

  // Step 3: expand each instantiation using a worklist.
  let #(insts, expanded, expanded_defs_reversed) =
    expand_worklist(insts, set.new(), [], templates, mono_names)

  // Step 4: replace references in monomorphic type defs (the copies drop
  // typeParams — Dart constructs TypeDef without them).
  let replaced_type_defs =
    list.map(mono_type_defs, fn(td) {
      let new_alts =
        list.map(td.alternatives, fn(alt) {
          replace_param_refs(alt, templates, mono_names)
        })
      TypeDef(td.name, [], new_alts, td.pos)
    })

  // Step 5: replace references in procedure declarations. Parameterized proc
  // decls yield a wildcard-instantiated concrete version (for checking their
  // own clauses) AND keep the parameterized template.
  let #(replaced_decls_reversed, param_templates_reversed, insts) =
    list.fold(module.proc_declarations, #([], [], insts), fn(acc, pd) {
      let #(replaced, param_templates, insts) = acc
      let proc_type_params =
        detect_proc_type_params(pd, templates, mono_type_defs, known_type_names)
      case proc_type_params {
        [] -> {
          let new_arg_types =
            list.map(pd.arg_types, fn(arg) {
              replace_param_refs(arg, templates, mono_names)
            })
          #([copy_decl(pd, new_arg_types, []), ..replaced], param_templates, insts)
        }
        _ -> {
          let param_template = copy_decl(pd, pd.arg_types, proc_type_params)
          // Substitute each type param with `_` (PrimitiveModeAlt output).
          let wildcard_subst =
            list.fold(proc_type_params, dict.new(), fn(acc, tp) {
              dict.insert(
                acc,
                tp,
                type_ast.PrimitiveModeAlt(False, type_ast.Pos(0, 0)),
              )
            })
          let #(wildcard_args_reversed, insts) =
            list.fold(pd.arg_types, #([], insts), fn(arg_acc, arg) {
              let #(done, insts) = arg_acc
              let #(substituted, insts) =
                substitute_type_expr(arg, wildcard_subst, templates, insts, mono_names)
              #([replace_param_refs(substituted, templates, mono_names), ..done], insts)
            })
          #(
            [copy_decl(pd, list.reverse(wildcard_args_reversed), []), ..replaced],
            [param_template, ..param_templates],
            insts,
          )
        }
      }
    })

  // Expand any new instantiations generated by wildcard substitution.
  let #(_, _, expanded_defs_reversed) =
    expand_worklist(insts, expanded, expanded_defs_reversed, templates, mono_names)

  ast.SourceModule(
    declaration: module.declaration,
    type_defs: list.append(replaced_type_defs, list.reverse(expanded_defs_reversed)),
    proc_declarations: list.reverse(replaced_decls_reversed),
    param_proc_decls: list.reverse(param_templates_reversed),
    procedures: module.procedures,
    compile_mode: module.compile_mode,
    pos: module.pos,
  )
}

/// The Dart ProcDecl copy constructors do not propagate `isBuiltin` — copies
/// come out with is_builtin: False. Mirrored exactly.
fn copy_decl(
  pd: ProcDecl,
  arg_types: List(TypeExpr),
  type_params: List(String),
) -> ProcDecl {
  ProcDecl(
    name: pd.name,
    arg_types: arg_types,
    type_params: type_params,
    pos: pd.pos,
    is_builtin: False,
    exported: pd.exported,
    imported: pd.imported,
    module_path: pd.module_path,
  )
}

/// One while-pass over a snapshot of the pending instantiations; expansion may
/// register more, so repeat until nothing is pending (Dart
/// `while (instantiations.length > expanded.length)`).
fn expand_worklist(
  insts: Insts,
  expanded: Set(String),
  defs_reversed: List(TypeDef),
  templates: Dict(String, TypeDef),
  mono_names: Set(String),
) -> #(Insts, Set(String), List(TypeDef)) {
  let pending =
    insts_order(insts)
    |> list.filter(fn(name) { !set.contains(expanded, name) })
  case pending {
    [] -> #(insts, expanded, defs_reversed)
    _ -> {
      let #(insts, expanded, defs_reversed) =
        list.fold(pending, #(insts, expanded, defs_reversed), fn(acc, name) {
          expand_one(acc, name, templates, mono_names)
        })
      expand_worklist(insts, expanded, defs_reversed, templates, mono_names)
    }
  }
}

fn expand_one(
  acc: #(Insts, Set(String), List(TypeDef)),
  expanded_name: String,
  templates: Dict(String, TypeDef),
  mono_names: Set(String),
) -> #(Insts, Set(String), List(TypeDef)) {
  let #(insts, expanded, defs_reversed) = acc
  let assert Ok(type_args) = dict.get(insts.by_name, expanded_name)
  let template_name = template_name_from_expanded(expanded_name)
  case dict.get(templates, template_name) {
    // Referenced template not found — skip (caught by the type checker).
    Error(_) -> #(insts, set.insert(expanded, expanded_name), defs_reversed)
    Ok(template) ->
      case list.length(template.type_params) == list.length(type_args) {
        False -> #(insts, set.insert(expanded, expanded_name), defs_reversed)
        True -> {
          let substitution =
            list.zip(template.type_params, type_args)
            |> list.fold(dict.new(), fn(acc, pair) {
              dict.insert(acc, pair.0, pair.1)
            })
          let #(new_alts_reversed, insts) =
            list.fold(template.alternatives, #([], insts), fn(alt_acc, alt) {
              let #(done, insts) = alt_acc
              let #(substituted, insts) =
                substitute_type_expr(alt, substitution, templates, insts, mono_names)
              #([substituted, ..done], insts)
            })
          let processed_alts =
            new_alts_reversed
            |> list.reverse
            |> list.map(fn(alt) { replace_param_refs(alt, templates, mono_names) })
          #(
            insts,
            set.insert(expanded, expanded_name),
            [TypeDef(expanded_name, [], processed_alts, template.pos), ..defs_reversed],
          )
        }
      }
  }
}

/// Detect type parameters in a procedure declaration (Dart
/// `_detectProcTypeParams`): names appearing as bare typeArgs inside a
/// parameterized TypeRef that are not known defined types. Bare top-level
/// unknown types are NOT type params. First-seen order preserved (Dart
/// LinkedHashSet → toList).
fn detect_proc_type_params(
  pd: ProcDecl,
  templates: Dict(String, TypeDef),
  mono_type_defs: List(TypeDef),
  external_known_types: Set(String),
) -> List(String) {
  let known =
    dict.keys(templates)
    |> list.fold(external_known_types, set.insert)
  let known =
    list.fold(mono_type_defs, known, fn(acc, td) { set.insert(acc, td.name) })
  let known =
    ["Integer", "Real", "Number", "String", "Constant"]
    |> list.fold(known, set.insert)
  list.fold(pd.arg_types, [], fn(acc, arg) {
    collect_inner_type_param_candidates(arg, known, acc)
  })
  |> list.reverse
}

fn add_candidate(candidates_reversed: List(String), name: String) -> List(String) {
  case list.contains(candidates_reversed, name) {
    True -> candidates_reversed
    False -> [name, ..candidates_reversed]
  }
}

fn collect_inner_type_param_candidates(
  expr: TypeExpr,
  known_types: Set(String),
  candidates_reversed: List(String),
) -> List(String) {
  case expr {
    type_ast.TypeRef(_, _, [], _) -> candidates_reversed
    type_ast.TypeRef(_, _, type_args, _) ->
      list.fold(type_args, candidates_reversed, fn(acc, arg) {
        let acc = case arg {
          type_ast.TypeRef(arg_name, _, [], _) ->
            case set.contains(known_types, arg_name) {
              True -> acc
              False -> add_candidate(acc, arg_name)
            }
          _ -> acc
        }
        collect_inner_type_param_candidates(arg, known_types, acc)
      })
    type_ast.StructAlt(_, args, _) ->
      list.fold(args, candidates_reversed, fn(acc, arg) {
        collect_inner_type_param_candidates(arg, known_types, acc)
      })
    type_ast.ListConsAlt(head, tail, _) ->
      candidates_reversed
      |> collect_inner_type_param_candidates(head, known_types, _)
      |> collect_inner_type_param_candidates(tail, known_types, _)
    type_ast.DiffListAlt(content, hole, _) ->
      candidates_reversed
      |> collect_inner_type_param_candidates(content, known_types, _)
      |> collect_inner_type_param_candidates(hole, known_types, _)
    type_ast.ConstantAlt(_, _)
    | type_ast.ListNilAlt(_)
    | type_ast.PrimitiveModeAlt(_, _) -> candidates_reversed
  }
}

/// Expanded name: Stream + [Integer] → "Stream<Integer>" (Dart
/// `_expandedName`; nested parameterized refs recurse in angle-bracket form).
fn expanded_name(template_name: String, type_args: List(TypeExpr)) -> String {
  template_name
  <> "<"
  <> { type_args |> list.map(type_expr_to_canonical) |> string.join(",") }
  <> ">"
}

fn type_expr_to_canonical(expr: TypeExpr) -> String {
  case expr {
    type_ast.TypeRef(name, is_input, [_, ..] as type_args, _) -> {
      let suffix = case is_input {
        True -> "?"
        False -> ""
      }
      name
      <> "<"
      <> { type_args |> list.map(type_expr_to_canonical) |> string.join(",") }
      <> ">"
      <> suffix
    }
    _ -> type_ast.type_expr_to_string(expr)
  }
}

/// "Stream<Integer>" → "Stream" (Dart `_templateNameFromExpanded`).
fn template_name_from_expanded(name: String) -> String {
  case string.split_once(name, "<") {
    Ok(#(before, _)) -> before
    Error(_) -> name
  }
}

/// A TypeRef references a template iff it has type args AND the template
/// exists AND the arity matches (Dart `_isTemplateRef`).
fn template_for_ref(
  name: String,
  type_args: List(TypeExpr),
  templates: Dict(String, TypeDef),
) -> Result(TypeDef, Nil) {
  case type_args {
    [] -> Error(Nil)
    _ ->
      case dict.get(templates, name) {
        Ok(template) ->
          case list.length(type_args) == list.length(template.type_params) {
            True -> Ok(template)
            False -> Error(Nil)
          }
        Error(_) -> Error(Nil)
      }
  }
}

/// Collect parameterized type references (Dart `_collectInstantiations`).
fn collect_instantiations(
  expr: TypeExpr,
  templates: Dict(String, TypeDef),
  insts: Insts,
) -> Insts {
  case expr {
    type_ast.TypeRef(name, _, type_args, _) -> {
      let insts = case template_for_ref(name, type_args, templates) {
        Ok(_) -> {
          let insts =
            insts_put_if_absent(insts, expanded_name(name, type_args), type_args)
          // Recurse into type args (nested parameterized types).
          list.fold(type_args, insts, fn(insts, arg) {
            collect_instantiations(arg, templates, insts)
          })
        }
        Error(_) -> insts
      }
      // Also recurse into typeArgs even if not a template (nested refs).
      list.fold(type_args, insts, fn(insts, arg) {
        collect_instantiations(arg, templates, insts)
      })
    }
    type_ast.StructAlt(_, args, _) ->
      list.fold(args, insts, fn(insts, arg) {
        collect_instantiations(arg, templates, insts)
      })
    type_ast.ListConsAlt(head, tail, _) -> {
      let insts = collect_instantiations(head, templates, insts)
      collect_instantiations(tail, templates, insts)
    }
    type_ast.DiffListAlt(content, hole, _) -> {
      let insts = collect_instantiations(content, templates, insts)
      collect_instantiations(hole, templates, insts)
    }
    type_ast.ConstantAlt(_, _)
    | type_ast.ListNilAlt(_)
    | type_ast.PrimitiveModeAlt(_, _) -> insts
  }
}

/// Collect instantiations from template bodies, skipping recursive
/// self-references whose args are all bare template parameters (Dart
/// `_collectInstantiationsInTemplate` — note it never registers the outer ref
/// itself; concrete refs in template bodies are registered during
/// substitution when the enclosing template expands).
fn collect_instantiations_in_template(
  expr: TypeExpr,
  templates: Dict(String, TypeDef),
  insts: Insts,
  template_params: List(String),
) -> Insts {
  case expr {
    type_ast.TypeRef(name, _, type_args, _) -> {
      let insts = case template_for_ref(name, type_args, templates) {
        Ok(_) -> {
          let all_param_refs =
            list.all(type_args, fn(arg) {
              case arg {
                type_ast.TypeRef(arg_name, _, [], _) ->
                  list.contains(template_params, arg_name)
                _ -> False
              }
            })
          case all_param_refs {
            True -> insts
            False ->
              list.fold(type_args, insts, fn(insts, arg) {
                collect_instantiations_in_template(
                  arg,
                  templates,
                  insts,
                  template_params,
                )
              })
          }
        }
        Error(_) -> insts
      }
      list.fold(type_args, insts, fn(insts, arg) {
        collect_instantiations_in_template(arg, templates, insts, template_params)
      })
    }
    type_ast.StructAlt(_, args, _) ->
      list.fold(args, insts, fn(insts, arg) {
        collect_instantiations_in_template(arg, templates, insts, template_params)
      })
    type_ast.ListConsAlt(head, tail, _) -> {
      let insts =
        collect_instantiations_in_template(head, templates, insts, template_params)
      collect_instantiations_in_template(tail, templates, insts, template_params)
    }
    type_ast.DiffListAlt(content, hole, _) -> {
      let insts =
        collect_instantiations_in_template(
          content,
          templates,
          insts,
          template_params,
        )
      collect_instantiations_in_template(hole, templates, insts, template_params)
    }
    type_ast.ConstantAlt(_, _)
    | type_ast.ListNilAlt(_)
    | type_ast.PrimitiveModeAlt(_, _) -> insts
  }
}

/// Substitute type parameters in a TypeExpr (Dart `_substituteTypeExpr`).
/// Newly encountered template references are registered as instantiations —
/// the returned `Insts` carries them into the worklist.
fn substitute_type_expr(
  expr: TypeExpr,
  substitution: Dict(String, TypeExpr),
  templates: Dict(String, TypeDef),
  insts: Insts,
  mono_names: Set(String),
) -> #(TypeExpr, Insts) {
  case expr {
    type_ast.TypeRef(name, is_input, type_args, pos) ->
      case type_args, dict.get(substitution, name) {
        // A bare type parameter: substitute, applying isInput from the
        // original reference.
        [], Ok(replacement) ->
          case is_input, replacement {
            True, type_ast.TypeRef(r_name, _, r_args, r_pos) -> #(
              type_ast.TypeRef(r_name, True, r_args, r_pos),
              insts,
            )
            True, type_ast.PrimitiveModeAlt(_, r_pos) -> #(
              type_ast.PrimitiveModeAlt(True, r_pos),
              insts,
            )
            _, _ -> #(replacement, insts)
          }
        _, _ ->
          case template_for_ref(name, type_args, templates) {
            Ok(_) -> {
              let #(subst_args_reversed, insts) =
                list.fold(type_args, #([], insts), fn(acc, arg) {
                  let #(done, insts) = acc
                  let #(substituted, insts) =
                    substitute_type_expr(arg, substitution, templates, insts, mono_names)
                  #([substituted, ..done], insts)
                })
              let subst_args = list.reverse(subst_args_reversed)
              // Stream(_) ≡ Stream when a monomorphic Stream exists.
              let all_wildcards =
                list.all(subst_args, fn(arg) {
                  case arg {
                    type_ast.PrimitiveModeAlt(_, _) -> True
                    _ -> False
                  }
                })
              case all_wildcards && set.contains(mono_names, name) {
                True -> #(type_ast.TypeRef(name, is_input, [], pos), insts)
                False -> {
                  let new_name = expanded_name(name, subst_args)
                  #(
                    type_ast.TypeRef(new_name, is_input, [], pos),
                    insts_put_if_absent(insts, new_name, subst_args),
                  )
                }
              }
            }
            Error(_) -> #(expr, insts)
          }
      }
    type_ast.StructAlt(functor, args, pos) -> {
      let #(new_args_reversed, insts) =
        list.fold(args, #([], insts), fn(acc, arg) {
          let #(done, insts) = acc
          let #(substituted, insts) =
            substitute_type_expr(arg, substitution, templates, insts, mono_names)
          #([substituted, ..done], insts)
        })
      #(type_ast.StructAlt(functor, list.reverse(new_args_reversed), pos), insts)
    }
    type_ast.ListConsAlt(head, tail, pos) -> {
      let #(new_head, insts) =
        substitute_type_expr(head, substitution, templates, insts, mono_names)
      let #(new_tail, insts) =
        substitute_type_expr(tail, substitution, templates, insts, mono_names)
      #(type_ast.ListConsAlt(new_head, new_tail, pos), insts)
    }
    type_ast.DiffListAlt(content, hole, pos) -> {
      let #(new_content, insts) =
        substitute_type_expr(content, substitution, templates, insts, mono_names)
      let #(new_hole, insts) =
        substitute_type_expr(hole, substitution, templates, insts, mono_names)
      #(type_ast.DiffListAlt(new_content, new_hole, pos), insts)
    }
    // PrimitiveModeAlt, ConstantAlt, ListNilAlt — no substitution needed.
    _ -> #(expr, insts)
  }
}

/// Replace parameterized type refs with expanded names (Dart
/// `_replaceParamRefs` — for non-template types and proc decls).
fn replace_param_refs(
  expr: TypeExpr,
  templates: Dict(String, TypeDef),
  mono_names: Set(String),
) -> TypeExpr {
  case expr {
    type_ast.TypeRef(name, is_input, type_args, pos) ->
      case template_for_ref(name, type_args, templates) {
        Ok(_) -> {
          let replaced_args =
            list.map(type_args, fn(arg) {
              replace_param_refs(arg, templates, mono_names)
            })
          let all_wildcards =
            list.all(replaced_args, fn(arg) {
              case arg {
                type_ast.PrimitiveModeAlt(_, _) -> True
                _ -> False
              }
            })
          case all_wildcards && set.contains(mono_names, name) {
            True -> type_ast.TypeRef(name, is_input, [], pos)
            False ->
              type_ast.TypeRef(expanded_name(name, replaced_args), is_input, [], pos)
          }
        }
        Error(_) ->
          case type_args {
            [] -> expr
            _ ->
              type_ast.TypeRef(
                name,
                is_input,
                list.map(type_args, fn(arg) {
                  replace_param_refs(arg, templates, mono_names)
                }),
                pos,
              )
          }
      }
    type_ast.StructAlt(functor, args, pos) ->
      type_ast.StructAlt(
        functor,
        list.map(args, fn(arg) { replace_param_refs(arg, templates, mono_names) }),
        pos,
      )
    type_ast.ListConsAlt(head, tail, pos) ->
      type_ast.ListConsAlt(
        replace_param_refs(head, templates, mono_names),
        replace_param_refs(tail, templates, mono_names),
        pos,
      )
    type_ast.DiffListAlt(content, hole, pos) ->
      type_ast.DiffListAlt(
        replace_param_refs(content, templates, mono_names),
        replace_param_refs(hole, templates, mono_names),
        pos,
      )
    _ -> expr
  }
}

//// glp/analysis/type_conversion — Term AST → TypeExpr AST conversion for type
//// definitions (feature 050, T014).
////
//// Dart source of truth: glp_runtime/lib/analysis/type_checker/
//// type_conversion.dart (spec: docs type system/type-conversion.md). A pure
//// structural conversion with no semantic validation; semantic checks
//// (determinism, alias prohibition) happen in the type environment (T018).
////
//// Conformance note: Dart `ConstantAlt.value` is `Object` (with string
//// literals carried quote-wrapped); the Gleam port maps the typed
//// `terms.Constant` onto the typed `type_ast.ConstValue` — same information.

import gleam/list
import gleam/option.{None, Some}
import gleam/string
import glp/analysis/type_ast.{type TypeExpr}
import glp/parser/ast.{type Term}
import glp/runtime/terms

/// Convert a Term AST node to a TypeExpr AST node (Dart `termToTypeExpr`).
pub fn term_to_type_expr(term: Term) -> TypeExpr {
  case term {
    // Variable → TypeRef
    ast.VarTerm(name, is_reader, pos) ->
      type_ast.TypeRef(name, is_reader, [], pos)

    // Anonymous _ or _? → PrimitiveModeAlt
    ast.UnderscoreTerm(is_reader, pos) ->
      type_ast.PrimitiveModeAlt(is_reader, pos)

    // Constant → ConstantAlt
    ast.ConstTerm(value, pos) ->
      type_ast.ConstantAlt(const_value(value), pos)

    // Empty list
    ast.ListTerm(None, None, pos) -> type_ast.ListNilAlt(pos)

    // List cons (Dart `term.head!`/`term.tail!` — the parser never builds a
    // half-open ListTerm, so the mixed shapes are unreachable; Dart would
    // throw a null-check error there and this port panics the same way).
    ast.ListTerm(Some(head), Some(tail), pos) ->
      type_ast.ListConsAlt(
        term_to_type_expr(head),
        term_to_type_expr(tail),
        pos,
      )
    ast.ListTerm(_, _, _) ->
      panic as "type_conversion: half-open ListTerm is unreachable from the parser"

    ast.StructTerm(functor, args, pos) -> struct_to_type_expr(functor, args, pos)
  }
}

fn struct_to_type_expr(
  functor: String,
  args: List(Term),
  pos: type_ast.Pos,
) -> TypeExpr {
  // Difference list: A \ B
  case functor, args {
    "\\", [content, hole] ->
      type_ast.DiffListAlt(
        term_to_type_expr(content),
        term_to_type_expr(hole),
        pos,
      )
    _, _ -> {
      // Parameterized type reference — uppercase-initial functor (possibly
      // with trailing '?' encoded in the name by the parser, e.g. "Stream?").
      // In type definition bodies, an uppercase functor with arguments is
      // always a parameterized type reference, never a struct alternative
      // (struct functors are lowercase atoms).
      let #(base, is_input) = case string.ends_with(functor, "?") {
        True -> #(string.drop_end(functor, 1), True)
        False -> #(functor, False)
      }
      case starts_with_uppercase_letter(base) {
        True ->
          type_ast.TypeRef(
            base,
            is_input,
            list.map(args, term_to_type_expr),
            pos,
          )
        False ->
          // Regular structure (lowercase functor, including conjunction ',')
          type_ast.StructAlt(functor, list.map(args, term_to_type_expr), pos)
      }
    }
  }
}

fn const_value(value: terms.Constant) -> type_ast.ConstValue {
  case value {
    terms.ConstAtom(name) -> type_ast.CvAtom(name)
    terms.ConstInt(i) -> type_ast.CvInt(i)
    terms.ConstReal(r) -> type_ast.CvReal(r)
    terms.ConstString(s) -> type_ast.CvString(s)
  }
}

/// A-Z check on the first character (Dart `_isUppercaseLetter` — excludes
/// operators and underscore).
fn starts_with_uppercase_letter(name: String) -> Bool {
  case string.to_utf_codepoints(name) {
    [cp, ..] -> {
      let n = string.utf_codepoint_to_int(cp)
      n >= 65 && n <= 90
    }
    [] -> False
  }
}

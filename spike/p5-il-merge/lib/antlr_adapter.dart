/// Phase B adapter: ANTLR (merge.g4) parse tree -> the SAME glp_runtime AstNode
/// hierarchy used in Phase A. The generated parser (lib/antlr_gen/) is driven,
/// then its tree is walked into Program/Procedure/Clause/Atom/Goal/Term nodes.
library;

import 'package:antlr4/antlr4.dart';
import 'package:glp_runtime/compiler/ast.dart';

import 'antlr_gen/mergeLexer.dart';
import 'antlr_gen/mergeParser.dart';

/// Parse GLP clause text with the ANTLR-generated parser and adapt the tree into
/// a glp_runtime [Program] containing one [Procedure] (functor/arity inferred
/// from the first clause head). Throws on any syntax error.
Program parseMergeToAst(String source) {
  final input = InputStream.fromString(source);
  final lexer = mergeLexer(input);
  final tokens = CommonTokenStream(lexer);
  final parser = mergeParser(tokens);
  parser.errorHandler = BailErrorStrategy(); // fail loud on syntax error
  final tree = parser.program();

  final clauses = <Clause>[];
  for (final c in tree.clauses()) {
    clauses.add(_clause(c));
  }
  if (clauses.isEmpty) {
    throw StateError('ANTLR adapter: no clauses parsed');
  }
  final head0 = clauses.first.head;
  final proc = Procedure(head0.functor, head0.arity, clauses, 1, 0);
  return Program([proc], 1, 0);
}

Clause _clause(ClauseContext c) {
  final head = _atom(c.head()!.compound()!);
  List<Guard>? guards;
  List<Goal>? body;
  final gctx = c.guards();
  if (gctx != null) {
    guards = [for (final g in gctx.goals()) _guard(g.compound()!)];
  }
  final bctx = c.body();
  if (bctx != null) {
    body = [for (final g in bctx.goals()) _goal(g.compound()!)];
  }
  return Clause(head, guards: guards, body: body, line: 1, column: 0);
}

Atom _atom(CompoundContext ctx) {
  final functor = ctx.ATOM()!.text!;
  final args = _termListArgs(ctx);
  return Atom(functor, args, 1, 0);
}

Goal _goal(CompoundContext ctx) {
  final functor = ctx.ATOM()!.text!;
  final args = _termListArgs(ctx);
  return Goal(functor, args, 1, 0);
}

Guard _guard(CompoundContext ctx) {
  final pred = ctx.ATOM()!.text!;
  final args = _termListArgs(ctx);
  return Guard(pred, args, 1, 0);
}

List<Term> _termListArgs(CompoundContext ctx) {
  final tl = ctx.termList();
  if (tl == null) return const [];
  return [for (final t in tl.terms()) _term(t)];
}

Term _term(TermContext t) {
  final v = t.var_();
  if (v != null) {
    final name = v.VAR()!.text!;
    final isReader = v.QUESTION() != null;
    return VarTerm(name, isReader, 1, 0);
  }
  final l = t.list();
  if (l != null) return _list(l);
  final cmp = t.compound();
  if (cmp != null) {
    final functor = cmp.ATOM()!.text!;
    return StructTerm(functor, _termListArgs(cmp), 1, 0);
  }
  final a = t.atom();
  if (a != null) {
    return ConstTerm(a.ATOM()!.text!, 1, 0);
  }
  throw StateError('ANTLR adapter: empty term context');
}

Term _list(ListContext l) {
  final items = l.listItems();
  if (items == null) {
    return ListTerm(null, null, 1, 0); // []
  }
  final terms = [for (final t in items.terms()) _term(t)];
  final barCtx = items.BAR();
  // With a BAR, the LAST parsed term is the explicit tail; the others are heads.
  Term tail;
  List<Term> heads;
  if (barCtx != null) {
    tail = terms.last;
    heads = terms.sublist(0, terms.length - 1);
  } else {
    tail = ListTerm(null, null, 1, 0); // implicit nil tail
    heads = terms;
  }
  // Build right-nested cons: [h0, h1, ... | tail]
  Term acc = tail;
  for (int i = heads.length - 1; i >= 0; i--) {
    acc = ListTerm(heads[i], acc, 1, 0);
  }
  return acc;
}

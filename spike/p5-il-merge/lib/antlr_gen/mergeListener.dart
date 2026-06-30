// Generated from merge.g4 by ANTLR 4.13.2
// ignore_for_file: unused_import, unused_local_variable, prefer_single_quotes
import 'package:antlr4/antlr4.dart';

import 'mergeParser.dart';

/// This abstract class defines a complete listener for a parse tree produced by
/// [mergeParser].
abstract class mergeListener extends ParseTreeListener {
  /// Enter a parse tree produced by [mergeParser.program].
  /// [ctx] the parse tree
  void enterProgram(ProgramContext ctx);
  /// Exit a parse tree produced by [mergeParser.program].
  /// [ctx] the parse tree
  void exitProgram(ProgramContext ctx);

  /// Enter a parse tree produced by [mergeParser.clause].
  /// [ctx] the parse tree
  void enterClause(ClauseContext ctx);
  /// Exit a parse tree produced by [mergeParser.clause].
  /// [ctx] the parse tree
  void exitClause(ClauseContext ctx);

  /// Enter a parse tree produced by [mergeParser.head].
  /// [ctx] the parse tree
  void enterHead(HeadContext ctx);
  /// Exit a parse tree produced by [mergeParser.head].
  /// [ctx] the parse tree
  void exitHead(HeadContext ctx);

  /// Enter a parse tree produced by [mergeParser.guards].
  /// [ctx] the parse tree
  void enterGuards(GuardsContext ctx);
  /// Exit a parse tree produced by [mergeParser.guards].
  /// [ctx] the parse tree
  void exitGuards(GuardsContext ctx);

  /// Enter a parse tree produced by [mergeParser.body].
  /// [ctx] the parse tree
  void enterBody(BodyContext ctx);
  /// Exit a parse tree produced by [mergeParser.body].
  /// [ctx] the parse tree
  void exitBody(BodyContext ctx);

  /// Enter a parse tree produced by [mergeParser.goal].
  /// [ctx] the parse tree
  void enterGoal(GoalContext ctx);
  /// Exit a parse tree produced by [mergeParser.goal].
  /// [ctx] the parse tree
  void exitGoal(GoalContext ctx);

  /// Enter a parse tree produced by [mergeParser.compound].
  /// [ctx] the parse tree
  void enterCompound(CompoundContext ctx);
  /// Exit a parse tree produced by [mergeParser.compound].
  /// [ctx] the parse tree
  void exitCompound(CompoundContext ctx);

  /// Enter a parse tree produced by [mergeParser.termList].
  /// [ctx] the parse tree
  void enterTermList(TermListContext ctx);
  /// Exit a parse tree produced by [mergeParser.termList].
  /// [ctx] the parse tree
  void exitTermList(TermListContext ctx);

  /// Enter a parse tree produced by [mergeParser.term].
  /// [ctx] the parse tree
  void enterTerm(TermContext ctx);
  /// Exit a parse tree produced by [mergeParser.term].
  /// [ctx] the parse tree
  void exitTerm(TermContext ctx);

  /// Enter a parse tree produced by [mergeParser.var].
  /// [ctx] the parse tree
  void enterVar(VarContext ctx);
  /// Exit a parse tree produced by [mergeParser.var].
  /// [ctx] the parse tree
  void exitVar(VarContext ctx);

  /// Enter a parse tree produced by [mergeParser.atom].
  /// [ctx] the parse tree
  void enterAtom(AtomContext ctx);
  /// Exit a parse tree produced by [mergeParser.atom].
  /// [ctx] the parse tree
  void exitAtom(AtomContext ctx);

  /// Enter a parse tree produced by [mergeParser.list].
  /// [ctx] the parse tree
  void enterList(ListContext ctx);
  /// Exit a parse tree produced by [mergeParser.list].
  /// [ctx] the parse tree
  void exitList(ListContext ctx);

  /// Enter a parse tree produced by [mergeParser.listItems].
  /// [ctx] the parse tree
  void enterListItems(ListItemsContext ctx);
  /// Exit a parse tree produced by [mergeParser.listItems].
  /// [ctx] the parse tree
  void exitListItems(ListItemsContext ctx);
}
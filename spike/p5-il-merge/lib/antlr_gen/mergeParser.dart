// Generated from merge.g4 by ANTLR 4.13.2
// ignore_for_file: unused_import, unused_local_variable, prefer_single_quotes
import 'package:antlr4/antlr4.dart';

import 'mergeListener.dart';
import 'mergeBaseListener.dart';
const int RULE_program = 0, RULE_clause = 1, RULE_head = 2, RULE_guards = 3, 
          RULE_body = 4, RULE_goal = 5, RULE_compound = 6, RULE_termList = 7, 
          RULE_term = 8, RULE_var = 9, RULE_atom = 10, RULE_list = 11, RULE_listItems = 12;
class mergeParser extends Parser {
  static final checkVersion = () => RuntimeMetaData.checkVersion('4.13.2', RuntimeMetaData.VERSION);
  static const int TOKEN_EOF = IntStream.EOF;

  static final List<DFA> _decisionToDFA = List.generate(
      _ATN.numberOfDecisions, (i) => DFA(_ATN.getDecisionState(i), i));
  static final PredictionContextCache _sharedContextCache = PredictionContextCache();
  static const int TOKEN_NECK = 1, TOKEN_QUESTION = 2, TOKEN_LBRACK = 3, 
                   TOKEN_RBRACK = 4, TOKEN_BAR = 5, TOKEN_LPAREN = 6, TOKEN_RPAREN = 7, 
                   TOKEN_COMMA = 8, TOKEN_DOT = 9, TOKEN_ATOM = 10, TOKEN_VAR = 11, 
                   TOKEN_LINE_COMMENT = 12, TOKEN_WS = 13;

  @override
  final List<String> ruleNames = [
    'program', 'clause', 'head', 'guards', 'body', 'goal', 'compound', 'termList', 
    'term', 'var', 'atom', 'list', 'listItems'
  ];

  static final List<String?> _LITERAL_NAMES = [
      null, "':-'", "'?'", "'['", "']'", "'|'", "'('", "')'", "','", "'.'"
  ];
  static final List<String?> _SYMBOLIC_NAMES = [
      null, "NECK", "QUESTION", "LBRACK", "RBRACK", "BAR", "LPAREN", "RPAREN", 
      "COMMA", "DOT", "ATOM", "VAR", "LINE_COMMENT", "WS"
  ];
  static final Vocabulary VOCABULARY = VocabularyImpl(_LITERAL_NAMES, _SYMBOLIC_NAMES);

  @override
  Vocabulary get vocabulary {
    return VOCABULARY;
  }

  @override
  String get grammarFileName => 'merge.g4';

  @override
  List<int> get serializedATN => _serializedATN;

  @override
  ATN getATN() {
   return _ATN;
  }

  mergeParser(TokenStream input) : super(input) {
    interpreter = ParserATNSimulator(this, _ATN, _decisionToDFA, _sharedContextCache);
  }

  ProgramContext program() {
    dynamic _localctx = ProgramContext(context, state);
    enterRule(_localctx, 0, RULE_program);
    int _la;
    try {
      enterOuterAlt(_localctx, 1);
      state = 27; 
      errorHandler.sync(this);
      _la = tokenStream.LA(1)!;
      do {
        state = 26;
        clause();
        state = 29; 
        errorHandler.sync(this);
        _la = tokenStream.LA(1)!;
      } while (_la == TOKEN_ATOM);
      state = 31;
      match(TOKEN_EOF);
    } on RecognitionException catch (re) {
      _localctx.exception = re;
      errorHandler.reportError(this, re);
      errorHandler.recover(this, re);
    } finally {
      exitRule();
    }
    return _localctx;
  }

  ClauseContext clause() {
    dynamic _localctx = ClauseContext(context, state);
    enterRule(_localctx, 2, RULE_clause);
    int _la;
    try {
      enterOuterAlt(_localctx, 1);
      state = 33;
      head();
      state = 41;
      errorHandler.sync(this);
      _la = tokenStream.LA(1)!;
      if (_la == TOKEN_NECK) {
        state = 34;
        match(TOKEN_NECK);
        state = 38;
        errorHandler.sync(this);
        switch (interpreter!.adaptivePredict(tokenStream, 1, context)) {
        case 1:
          state = 35;
          guards();
          state = 36;
          match(TOKEN_BAR);
          break;
        }
        state = 40;
        body();
      }

      state = 43;
      match(TOKEN_DOT);
    } on RecognitionException catch (re) {
      _localctx.exception = re;
      errorHandler.reportError(this, re);
      errorHandler.recover(this, re);
    } finally {
      exitRule();
    }
    return _localctx;
  }

  HeadContext head() {
    dynamic _localctx = HeadContext(context, state);
    enterRule(_localctx, 4, RULE_head);
    try {
      enterOuterAlt(_localctx, 1);
      state = 45;
      compound();
    } on RecognitionException catch (re) {
      _localctx.exception = re;
      errorHandler.reportError(this, re);
      errorHandler.recover(this, re);
    } finally {
      exitRule();
    }
    return _localctx;
  }

  GuardsContext guards() {
    dynamic _localctx = GuardsContext(context, state);
    enterRule(_localctx, 6, RULE_guards);
    int _la;
    try {
      enterOuterAlt(_localctx, 1);
      state = 47;
      goal();
      state = 52;
      errorHandler.sync(this);
      _la = tokenStream.LA(1)!;
      while (_la == TOKEN_COMMA) {
        state = 48;
        match(TOKEN_COMMA);
        state = 49;
        goal();
        state = 54;
        errorHandler.sync(this);
        _la = tokenStream.LA(1)!;
      }
    } on RecognitionException catch (re) {
      _localctx.exception = re;
      errorHandler.reportError(this, re);
      errorHandler.recover(this, re);
    } finally {
      exitRule();
    }
    return _localctx;
  }

  BodyContext body() {
    dynamic _localctx = BodyContext(context, state);
    enterRule(_localctx, 8, RULE_body);
    int _la;
    try {
      enterOuterAlt(_localctx, 1);
      state = 55;
      goal();
      state = 60;
      errorHandler.sync(this);
      _la = tokenStream.LA(1)!;
      while (_la == TOKEN_COMMA) {
        state = 56;
        match(TOKEN_COMMA);
        state = 57;
        goal();
        state = 62;
        errorHandler.sync(this);
        _la = tokenStream.LA(1)!;
      }
    } on RecognitionException catch (re) {
      _localctx.exception = re;
      errorHandler.reportError(this, re);
      errorHandler.recover(this, re);
    } finally {
      exitRule();
    }
    return _localctx;
  }

  GoalContext goal() {
    dynamic _localctx = GoalContext(context, state);
    enterRule(_localctx, 10, RULE_goal);
    try {
      enterOuterAlt(_localctx, 1);
      state = 63;
      compound();
    } on RecognitionException catch (re) {
      _localctx.exception = re;
      errorHandler.reportError(this, re);
      errorHandler.recover(this, re);
    } finally {
      exitRule();
    }
    return _localctx;
  }

  CompoundContext compound() {
    dynamic _localctx = CompoundContext(context, state);
    enterRule(_localctx, 12, RULE_compound);
    try {
      enterOuterAlt(_localctx, 1);
      state = 65;
      match(TOKEN_ATOM);
      state = 66;
      match(TOKEN_LPAREN);
      state = 67;
      termList();
      state = 68;
      match(TOKEN_RPAREN);
    } on RecognitionException catch (re) {
      _localctx.exception = re;
      errorHandler.reportError(this, re);
      errorHandler.recover(this, re);
    } finally {
      exitRule();
    }
    return _localctx;
  }

  TermListContext termList() {
    dynamic _localctx = TermListContext(context, state);
    enterRule(_localctx, 14, RULE_termList);
    int _la;
    try {
      enterOuterAlt(_localctx, 1);
      state = 70;
      term();
      state = 75;
      errorHandler.sync(this);
      _la = tokenStream.LA(1)!;
      while (_la == TOKEN_COMMA) {
        state = 71;
        match(TOKEN_COMMA);
        state = 72;
        term();
        state = 77;
        errorHandler.sync(this);
        _la = tokenStream.LA(1)!;
      }
    } on RecognitionException catch (re) {
      _localctx.exception = re;
      errorHandler.reportError(this, re);
      errorHandler.recover(this, re);
    } finally {
      exitRule();
    }
    return _localctx;
  }

  TermContext term() {
    dynamic _localctx = TermContext(context, state);
    enterRule(_localctx, 16, RULE_term);
    try {
      state = 82;
      errorHandler.sync(this);
      switch (interpreter!.adaptivePredict(tokenStream, 6, context)) {
      case 1:
        enterOuterAlt(_localctx, 1);
        state = 78;
        var_();
        break;
      case 2:
        enterOuterAlt(_localctx, 2);
        state = 79;
        list();
        break;
      case 3:
        enterOuterAlt(_localctx, 3);
        state = 80;
        compound();
        break;
      case 4:
        enterOuterAlt(_localctx, 4);
        state = 81;
        atom();
        break;
      }
    } on RecognitionException catch (re) {
      _localctx.exception = re;
      errorHandler.reportError(this, re);
      errorHandler.recover(this, re);
    } finally {
      exitRule();
    }
    return _localctx;
  }

  VarContext var_() {
    dynamic _localctx = VarContext(context, state);
    enterRule(_localctx, 18, RULE_var);
    int _la;
    try {
      enterOuterAlt(_localctx, 1);
      state = 84;
      match(TOKEN_VAR);
      state = 86;
      errorHandler.sync(this);
      _la = tokenStream.LA(1)!;
      if (_la == TOKEN_QUESTION) {
        state = 85;
        match(TOKEN_QUESTION);
      }

    } on RecognitionException catch (re) {
      _localctx.exception = re;
      errorHandler.reportError(this, re);
      errorHandler.recover(this, re);
    } finally {
      exitRule();
    }
    return _localctx;
  }

  AtomContext atom() {
    dynamic _localctx = AtomContext(context, state);
    enterRule(_localctx, 20, RULE_atom);
    try {
      enterOuterAlt(_localctx, 1);
      state = 88;
      match(TOKEN_ATOM);
    } on RecognitionException catch (re) {
      _localctx.exception = re;
      errorHandler.reportError(this, re);
      errorHandler.recover(this, re);
    } finally {
      exitRule();
    }
    return _localctx;
  }

  ListContext list() {
    dynamic _localctx = ListContext(context, state);
    enterRule(_localctx, 22, RULE_list);
    int _la;
    try {
      enterOuterAlt(_localctx, 1);
      state = 90;
      match(TOKEN_LBRACK);
      state = 92;
      errorHandler.sync(this);
      _la = tokenStream.LA(1)!;
      if ((((_la) & ~0x3f) == 0 && ((1 << _la) & 3080) != 0)) {
        state = 91;
        listItems();
      }

      state = 94;
      match(TOKEN_RBRACK);
    } on RecognitionException catch (re) {
      _localctx.exception = re;
      errorHandler.reportError(this, re);
      errorHandler.recover(this, re);
    } finally {
      exitRule();
    }
    return _localctx;
  }

  ListItemsContext listItems() {
    dynamic _localctx = ListItemsContext(context, state);
    enterRule(_localctx, 24, RULE_listItems);
    int _la;
    try {
      enterOuterAlt(_localctx, 1);
      state = 96;
      term();
      state = 101;
      errorHandler.sync(this);
      _la = tokenStream.LA(1)!;
      while (_la == TOKEN_COMMA) {
        state = 97;
        match(TOKEN_COMMA);
        state = 98;
        term();
        state = 103;
        errorHandler.sync(this);
        _la = tokenStream.LA(1)!;
      }
      state = 106;
      errorHandler.sync(this);
      _la = tokenStream.LA(1)!;
      if (_la == TOKEN_BAR) {
        state = 104;
        match(TOKEN_BAR);
        state = 105;
        term();
      }

    } on RecognitionException catch (re) {
      _localctx.exception = re;
      errorHandler.reportError(this, re);
      errorHandler.recover(this, re);
    } finally {
      exitRule();
    }
    return _localctx;
  }

  static const List<int> _serializedATN = [
      4,1,13,109,2,0,7,0,2,1,7,1,2,2,7,2,2,3,7,3,2,4,7,4,2,5,7,5,2,6,7,6,
      2,7,7,7,2,8,7,8,2,9,7,9,2,10,7,10,2,11,7,11,2,12,7,12,1,0,4,0,28,8,
      0,11,0,12,0,29,1,0,1,0,1,1,1,1,1,1,1,1,1,1,3,1,39,8,1,1,1,3,1,42,8,
      1,1,1,1,1,1,2,1,2,1,3,1,3,1,3,5,3,51,8,3,10,3,12,3,54,9,3,1,4,1,4,
      1,4,5,4,59,8,4,10,4,12,4,62,9,4,1,5,1,5,1,6,1,6,1,6,1,6,1,6,1,7,1,
      7,1,7,5,7,74,8,7,10,7,12,7,77,9,7,1,8,1,8,1,8,1,8,3,8,83,8,8,1,9,1,
      9,3,9,87,8,9,1,10,1,10,1,11,1,11,3,11,93,8,11,1,11,1,11,1,12,1,12,
      1,12,5,12,100,8,12,10,12,12,12,103,9,12,1,12,1,12,3,12,107,8,12,1,
      12,0,0,13,0,2,4,6,8,10,12,14,16,18,20,22,24,0,0,108,0,27,1,0,0,0,2,
      33,1,0,0,0,4,45,1,0,0,0,6,47,1,0,0,0,8,55,1,0,0,0,10,63,1,0,0,0,12,
      65,1,0,0,0,14,70,1,0,0,0,16,82,1,0,0,0,18,84,1,0,0,0,20,88,1,0,0,0,
      22,90,1,0,0,0,24,96,1,0,0,0,26,28,3,2,1,0,27,26,1,0,0,0,28,29,1,0,
      0,0,29,27,1,0,0,0,29,30,1,0,0,0,30,31,1,0,0,0,31,32,5,0,0,1,32,1,1,
      0,0,0,33,41,3,4,2,0,34,38,5,1,0,0,35,36,3,6,3,0,36,37,5,5,0,0,37,39,
      1,0,0,0,38,35,1,0,0,0,38,39,1,0,0,0,39,40,1,0,0,0,40,42,3,8,4,0,41,
      34,1,0,0,0,41,42,1,0,0,0,42,43,1,0,0,0,43,44,5,9,0,0,44,3,1,0,0,0,
      45,46,3,12,6,0,46,5,1,0,0,0,47,52,3,10,5,0,48,49,5,8,0,0,49,51,3,10,
      5,0,50,48,1,0,0,0,51,54,1,0,0,0,52,50,1,0,0,0,52,53,1,0,0,0,53,7,1,
      0,0,0,54,52,1,0,0,0,55,60,3,10,5,0,56,57,5,8,0,0,57,59,3,10,5,0,58,
      56,1,0,0,0,59,62,1,0,0,0,60,58,1,0,0,0,60,61,1,0,0,0,61,9,1,0,0,0,
      62,60,1,0,0,0,63,64,3,12,6,0,64,11,1,0,0,0,65,66,5,10,0,0,66,67,5,
      6,0,0,67,68,3,14,7,0,68,69,5,7,0,0,69,13,1,0,0,0,70,75,3,16,8,0,71,
      72,5,8,0,0,72,74,3,16,8,0,73,71,1,0,0,0,74,77,1,0,0,0,75,73,1,0,0,
      0,75,76,1,0,0,0,76,15,1,0,0,0,77,75,1,0,0,0,78,83,3,18,9,0,79,83,3,
      22,11,0,80,83,3,12,6,0,81,83,3,20,10,0,82,78,1,0,0,0,82,79,1,0,0,0,
      82,80,1,0,0,0,82,81,1,0,0,0,83,17,1,0,0,0,84,86,5,11,0,0,85,87,5,2,
      0,0,86,85,1,0,0,0,86,87,1,0,0,0,87,19,1,0,0,0,88,89,5,10,0,0,89,21,
      1,0,0,0,90,92,5,3,0,0,91,93,3,24,12,0,92,91,1,0,0,0,92,93,1,0,0,0,
      93,94,1,0,0,0,94,95,5,4,0,0,95,23,1,0,0,0,96,101,3,16,8,0,97,98,5,
      8,0,0,98,100,3,16,8,0,99,97,1,0,0,0,100,103,1,0,0,0,101,99,1,0,0,0,
      101,102,1,0,0,0,102,106,1,0,0,0,103,101,1,0,0,0,104,105,5,5,0,0,105,
      107,3,16,8,0,106,104,1,0,0,0,106,107,1,0,0,0,107,25,1,0,0,0,11,29,
      38,41,52,60,75,82,86,92,101,106
  ];

  static final ATN _ATN =
      ATNDeserializer().deserialize(_serializedATN);
}
class ProgramContext extends ParserRuleContext {
  TerminalNode? EOF() => getToken(mergeParser.TOKEN_EOF, 0);
  List<ClauseContext> clauses() => getRuleContexts<ClauseContext>();
  ClauseContext? clause(int i) => getRuleContext<ClauseContext>(i);
  ProgramContext([ParserRuleContext? parent, int? invokingState]) : super(parent, invokingState);
  @override
  int get ruleIndex => RULE_program;
  @override
  void enterRule(ParseTreeListener listener) {
    if (listener is mergeListener) listener.enterProgram(this);
  }
  @override
  void exitRule(ParseTreeListener listener) {
    if (listener is mergeListener) listener.exitProgram(this);
  }
}

class ClauseContext extends ParserRuleContext {
  HeadContext? head() => getRuleContext<HeadContext>(0);
  TerminalNode? DOT() => getToken(mergeParser.TOKEN_DOT, 0);
  TerminalNode? NECK() => getToken(mergeParser.TOKEN_NECK, 0);
  BodyContext? body() => getRuleContext<BodyContext>(0);
  GuardsContext? guards() => getRuleContext<GuardsContext>(0);
  TerminalNode? BAR() => getToken(mergeParser.TOKEN_BAR, 0);
  ClauseContext([ParserRuleContext? parent, int? invokingState]) : super(parent, invokingState);
  @override
  int get ruleIndex => RULE_clause;
  @override
  void enterRule(ParseTreeListener listener) {
    if (listener is mergeListener) listener.enterClause(this);
  }
  @override
  void exitRule(ParseTreeListener listener) {
    if (listener is mergeListener) listener.exitClause(this);
  }
}

class HeadContext extends ParserRuleContext {
  CompoundContext? compound() => getRuleContext<CompoundContext>(0);
  HeadContext([ParserRuleContext? parent, int? invokingState]) : super(parent, invokingState);
  @override
  int get ruleIndex => RULE_head;
  @override
  void enterRule(ParseTreeListener listener) {
    if (listener is mergeListener) listener.enterHead(this);
  }
  @override
  void exitRule(ParseTreeListener listener) {
    if (listener is mergeListener) listener.exitHead(this);
  }
}

class GuardsContext extends ParserRuleContext {
  List<GoalContext> goals() => getRuleContexts<GoalContext>();
  GoalContext? goal(int i) => getRuleContext<GoalContext>(i);
  List<TerminalNode> COMMAs() => getTokens(mergeParser.TOKEN_COMMA);
  TerminalNode? COMMA(int i) => getToken(mergeParser.TOKEN_COMMA, i);
  GuardsContext([ParserRuleContext? parent, int? invokingState]) : super(parent, invokingState);
  @override
  int get ruleIndex => RULE_guards;
  @override
  void enterRule(ParseTreeListener listener) {
    if (listener is mergeListener) listener.enterGuards(this);
  }
  @override
  void exitRule(ParseTreeListener listener) {
    if (listener is mergeListener) listener.exitGuards(this);
  }
}

class BodyContext extends ParserRuleContext {
  List<GoalContext> goals() => getRuleContexts<GoalContext>();
  GoalContext? goal(int i) => getRuleContext<GoalContext>(i);
  List<TerminalNode> COMMAs() => getTokens(mergeParser.TOKEN_COMMA);
  TerminalNode? COMMA(int i) => getToken(mergeParser.TOKEN_COMMA, i);
  BodyContext([ParserRuleContext? parent, int? invokingState]) : super(parent, invokingState);
  @override
  int get ruleIndex => RULE_body;
  @override
  void enterRule(ParseTreeListener listener) {
    if (listener is mergeListener) listener.enterBody(this);
  }
  @override
  void exitRule(ParseTreeListener listener) {
    if (listener is mergeListener) listener.exitBody(this);
  }
}

class GoalContext extends ParserRuleContext {
  CompoundContext? compound() => getRuleContext<CompoundContext>(0);
  GoalContext([ParserRuleContext? parent, int? invokingState]) : super(parent, invokingState);
  @override
  int get ruleIndex => RULE_goal;
  @override
  void enterRule(ParseTreeListener listener) {
    if (listener is mergeListener) listener.enterGoal(this);
  }
  @override
  void exitRule(ParseTreeListener listener) {
    if (listener is mergeListener) listener.exitGoal(this);
  }
}

class CompoundContext extends ParserRuleContext {
  TerminalNode? ATOM() => getToken(mergeParser.TOKEN_ATOM, 0);
  TerminalNode? LPAREN() => getToken(mergeParser.TOKEN_LPAREN, 0);
  TermListContext? termList() => getRuleContext<TermListContext>(0);
  TerminalNode? RPAREN() => getToken(mergeParser.TOKEN_RPAREN, 0);
  CompoundContext([ParserRuleContext? parent, int? invokingState]) : super(parent, invokingState);
  @override
  int get ruleIndex => RULE_compound;
  @override
  void enterRule(ParseTreeListener listener) {
    if (listener is mergeListener) listener.enterCompound(this);
  }
  @override
  void exitRule(ParseTreeListener listener) {
    if (listener is mergeListener) listener.exitCompound(this);
  }
}

class TermListContext extends ParserRuleContext {
  List<TermContext> terms() => getRuleContexts<TermContext>();
  TermContext? term(int i) => getRuleContext<TermContext>(i);
  List<TerminalNode> COMMAs() => getTokens(mergeParser.TOKEN_COMMA);
  TerminalNode? COMMA(int i) => getToken(mergeParser.TOKEN_COMMA, i);
  TermListContext([ParserRuleContext? parent, int? invokingState]) : super(parent, invokingState);
  @override
  int get ruleIndex => RULE_termList;
  @override
  void enterRule(ParseTreeListener listener) {
    if (listener is mergeListener) listener.enterTermList(this);
  }
  @override
  void exitRule(ParseTreeListener listener) {
    if (listener is mergeListener) listener.exitTermList(this);
  }
}

class TermContext extends ParserRuleContext {
  VarContext? var_() => getRuleContext<VarContext>(0);
  ListContext? list() => getRuleContext<ListContext>(0);
  CompoundContext? compound() => getRuleContext<CompoundContext>(0);
  AtomContext? atom() => getRuleContext<AtomContext>(0);
  TermContext([ParserRuleContext? parent, int? invokingState]) : super(parent, invokingState);
  @override
  int get ruleIndex => RULE_term;
  @override
  void enterRule(ParseTreeListener listener) {
    if (listener is mergeListener) listener.enterTerm(this);
  }
  @override
  void exitRule(ParseTreeListener listener) {
    if (listener is mergeListener) listener.exitTerm(this);
  }
}

class VarContext extends ParserRuleContext {
  TerminalNode? VAR() => getToken(mergeParser.TOKEN_VAR, 0);
  TerminalNode? QUESTION() => getToken(mergeParser.TOKEN_QUESTION, 0);
  VarContext([ParserRuleContext? parent, int? invokingState]) : super(parent, invokingState);
  @override
  int get ruleIndex => RULE_var;
  @override
  void enterRule(ParseTreeListener listener) {
    if (listener is mergeListener) listener.enterVar(this);
  }
  @override
  void exitRule(ParseTreeListener listener) {
    if (listener is mergeListener) listener.exitVar(this);
  }
}

class AtomContext extends ParserRuleContext {
  TerminalNode? ATOM() => getToken(mergeParser.TOKEN_ATOM, 0);
  AtomContext([ParserRuleContext? parent, int? invokingState]) : super(parent, invokingState);
  @override
  int get ruleIndex => RULE_atom;
  @override
  void enterRule(ParseTreeListener listener) {
    if (listener is mergeListener) listener.enterAtom(this);
  }
  @override
  void exitRule(ParseTreeListener listener) {
    if (listener is mergeListener) listener.exitAtom(this);
  }
}

class ListContext extends ParserRuleContext {
  TerminalNode? LBRACK() => getToken(mergeParser.TOKEN_LBRACK, 0);
  TerminalNode? RBRACK() => getToken(mergeParser.TOKEN_RBRACK, 0);
  ListItemsContext? listItems() => getRuleContext<ListItemsContext>(0);
  ListContext([ParserRuleContext? parent, int? invokingState]) : super(parent, invokingState);
  @override
  int get ruleIndex => RULE_list;
  @override
  void enterRule(ParseTreeListener listener) {
    if (listener is mergeListener) listener.enterList(this);
  }
  @override
  void exitRule(ParseTreeListener listener) {
    if (listener is mergeListener) listener.exitList(this);
  }
}

class ListItemsContext extends ParserRuleContext {
  List<TermContext> terms() => getRuleContexts<TermContext>();
  TermContext? term(int i) => getRuleContext<TermContext>(i);
  List<TerminalNode> COMMAs() => getTokens(mergeParser.TOKEN_COMMA);
  TerminalNode? COMMA(int i) => getToken(mergeParser.TOKEN_COMMA, i);
  TerminalNode? BAR() => getToken(mergeParser.TOKEN_BAR, 0);
  ListItemsContext([ParserRuleContext? parent, int? invokingState]) : super(parent, invokingState);
  @override
  int get ruleIndex => RULE_listItems;
  @override
  void enterRule(ParseTreeListener listener) {
    if (listener is mergeListener) listener.enterListItems(this);
  }
  @override
  void exitRule(ParseTreeListener listener) {
    if (listener is mergeListener) listener.exitListItems(this);
  }
}


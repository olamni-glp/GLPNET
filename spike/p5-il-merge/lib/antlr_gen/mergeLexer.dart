// Generated from merge.g4 by ANTLR 4.13.2
// ignore_for_file: unused_import, unused_local_variable, prefer_single_quotes
import 'package:antlr4/antlr4.dart';


class mergeLexer extends Lexer {
  static final checkVersion = () => RuntimeMetaData.checkVersion('4.13.2', RuntimeMetaData.VERSION);

  static final List<DFA> _decisionToDFA = List.generate(
        _ATN.numberOfDecisions, (i) => DFA(_ATN.getDecisionState(i), i));
  static final PredictionContextCache _sharedContextCache = PredictionContextCache();
  static const int
    TOKEN_NECK = 1, TOKEN_QUESTION = 2, TOKEN_LBRACK = 3, TOKEN_RBRACK = 4, 
    TOKEN_BAR = 5, TOKEN_LPAREN = 6, TOKEN_RPAREN = 7, TOKEN_COMMA = 8, 
    TOKEN_DOT = 9, TOKEN_ATOM = 10, TOKEN_VAR = 11, TOKEN_LINE_COMMENT = 12, 
    TOKEN_WS = 13;
  @override
  final List<String> channelNames = [
    'DEFAULT_TOKEN_CHANNEL', 'HIDDEN'
  ];

  @override
  final List<String> modeNames = [
    'DEFAULT_MODE'
  ];

  @override
  final List<String> ruleNames = [
    'NECK', 'QUESTION', 'LBRACK', 'RBRACK', 'BAR', 'LPAREN', 'RPAREN', 'COMMA', 
    'DOT', 'ATOM', 'VAR', 'LINE_COMMENT', 'WS'
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


  mergeLexer(CharStream input) : super(input) {
    interpreter = LexerATNSimulator(_ATN, _decisionToDFA, _sharedContextCache, recog: this);
  }

  @override
  List<int> get serializedATN => _serializedATN;

  @override
  String get grammarFileName => 'merge.g4';

  @override
  ATN getATN() { return _ATN; }

  static const List<int> _serializedATN = [
      4,0,13,76,6,-1,2,0,7,0,2,1,7,1,2,2,7,2,2,3,7,3,2,4,7,4,2,5,7,5,2,6,
      7,6,2,7,7,7,2,8,7,8,2,9,7,9,2,10,7,10,2,11,7,11,2,12,7,12,1,0,1,0,
      1,0,1,1,1,1,1,2,1,2,1,3,1,3,1,4,1,4,1,5,1,5,1,6,1,6,1,7,1,7,1,8,1,
      8,1,9,1,9,5,9,49,8,9,10,9,12,9,52,9,9,1,10,1,10,5,10,56,8,10,10,10,
      12,10,59,9,10,1,11,1,11,5,11,63,8,11,10,11,12,11,66,9,11,1,11,1,11,
      1,12,4,12,71,8,12,11,12,12,12,72,1,12,1,12,0,0,13,1,1,3,2,5,3,7,4,
      9,5,11,6,13,7,15,8,17,9,19,10,21,11,23,12,25,13,1,0,5,1,0,97,122,4,
      0,48,57,65,90,95,95,97,122,2,0,65,90,95,95,2,0,10,10,13,13,3,0,9,10,
      13,13,32,32,79,0,1,1,0,0,0,0,3,1,0,0,0,0,5,1,0,0,0,0,7,1,0,0,0,0,9,
      1,0,0,0,0,11,1,0,0,0,0,13,1,0,0,0,0,15,1,0,0,0,0,17,1,0,0,0,0,19,1,
      0,0,0,0,21,1,0,0,0,0,23,1,0,0,0,0,25,1,0,0,0,1,27,1,0,0,0,3,30,1,0,
      0,0,5,32,1,0,0,0,7,34,1,0,0,0,9,36,1,0,0,0,11,38,1,0,0,0,13,40,1,0,
      0,0,15,42,1,0,0,0,17,44,1,0,0,0,19,46,1,0,0,0,21,53,1,0,0,0,23,60,
      1,0,0,0,25,70,1,0,0,0,27,28,5,58,0,0,28,29,5,45,0,0,29,2,1,0,0,0,30,
      31,5,63,0,0,31,4,1,0,0,0,32,33,5,91,0,0,33,6,1,0,0,0,34,35,5,93,0,
      0,35,8,1,0,0,0,36,37,5,124,0,0,37,10,1,0,0,0,38,39,5,40,0,0,39,12,
      1,0,0,0,40,41,5,41,0,0,41,14,1,0,0,0,42,43,5,44,0,0,43,16,1,0,0,0,
      44,45,5,46,0,0,45,18,1,0,0,0,46,50,7,0,0,0,47,49,7,1,0,0,48,47,1,0,
      0,0,49,52,1,0,0,0,50,48,1,0,0,0,50,51,1,0,0,0,51,20,1,0,0,0,52,50,
      1,0,0,0,53,57,7,2,0,0,54,56,7,1,0,0,55,54,1,0,0,0,56,59,1,0,0,0,57,
      55,1,0,0,0,57,58,1,0,0,0,58,22,1,0,0,0,59,57,1,0,0,0,60,64,5,37,0,
      0,61,63,8,3,0,0,62,61,1,0,0,0,63,66,1,0,0,0,64,62,1,0,0,0,64,65,1,
      0,0,0,65,67,1,0,0,0,66,64,1,0,0,0,67,68,6,11,0,0,68,24,1,0,0,0,69,
      71,7,4,0,0,70,69,1,0,0,0,71,72,1,0,0,0,72,70,1,0,0,0,72,73,1,0,0,0,
      73,74,1,0,0,0,74,75,6,12,0,0,75,26,1,0,0,0,5,0,50,57,64,72,1,0,1,0
  ];

  static final ATN _ATN =
      ATNDeserializer().deserialize(_serializedATN);
}
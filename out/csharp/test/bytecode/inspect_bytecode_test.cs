import 'package:test/test.dart';
import 'package:glp_runtime/compiler/compiler.dart';

void main() {
  test('Inspect bytecode for r(a,[b])', () {
    final compiler = GlpCompiler();
    
    print('\n=== r(a,[b]). DETAILED ===');
    final prog = compiler.compile('r(a,[b]).');
    for (int i = 0; i < prog.ops.length; i++) {
      final op = prog.ops[i];
      print('  $i: $op (${op.runtimeType})');
    }
  });
}

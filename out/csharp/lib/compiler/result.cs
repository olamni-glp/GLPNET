using GlpRuntime.Bytecode;

namespace GlpRuntime.Compiler;

/// <summary>Result of compilation including bytecode and metadata</summary>
public class CompilationResult
{
    public BytecodeProgram Program { get; }
    public Dictionary<string, long> VariableMap { get; } // Variable name -> register index

    public CompilationResult(BytecodeProgram program, Dictionary<string, long> variableMap)
    {
        Program = program;
        VariableMap = variableMap;
    }
}

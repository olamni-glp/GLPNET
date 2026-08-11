// IlSession — the client-side compile+ship half of the 064 US3 IL path (T022;
// contracts/il-request-kind.md).
//
// In a `--path il` session THE COMPILER LIVES HERE (contract rule 5): the
// client runs the full local pipeline (parse → typecheck → compile via
// GlpEngine, exactly the engine host's text-path pipeline) and ships the
// compiled form to the engine framed in the shipped 062 CompiledIlEnvelope —
// the engine executes without a compiler on its IL path.
//
//   session start → compile programs/self.glp locally, LOAD_IL "__root_self__"
//                   (the engine cannot compile the prelude on the IL path).
//   load f.glp    → local compile (typecheck included), LOAD_IL with
//                   source_metadata = the load path (the module_ref).
//   goal `name`   → RUN_GOAL_IL goal_ref "name/0" (a compiled nullary entry).
//   goal with args/conjunction
//                 → compiled locally into a one-shot wrapper
//                   `'__goal__' :- <goal>.` and shipped INLINE in the
//                   RUN_GOAL_IL body; goal_ref "__goal__/0".
//
// The path choice is PER SESSION and never mixed per module (contract rule 3):
// an IL session never sends LOAD_SOURCE/RUN_GOAL, and a refusal from the
// engine is rendered typed — never silently retried over the text path.
//
// Known IL-path shape (documented, not silent): a compiled goal carries no
// query variables, so results render status + captured output with NO binding
// lines — use the text path when bindings matter (rule 3's deprecation window
// keeps it available).

using GlpRuntime.Compiler;
using GlpRuntime.Engine;
using GlpRuntime.IlCodec;
using GlpRuntime.SplitProtocol;

namespace GlpRuntime.ReplClient;

public sealed class IlSession
{
    public const string PreludeModuleRef = "__root_self__";
    public const string OneShotGoalRef = "__goal__/0";

    private readonly GlpEngine _engine;

    private IlSession(GlpEngine engine)
    {
        _engine = engine;
    }

    /// <summary>
    /// Create the local compile front-end. Loud-fail when programs/self.glp is
    /// not reachable: an IL session deliberately carries the language context
    /// client-side; without it there is nothing to compile with.
    /// </summary>
    public static IlSession Create()
    {
        var rootSelf = ResolveRootSelfGlpPath();
        var engine = new GlpEngine(rootSelf)
        {
            // Compile front-end only: never activate modules on the client's
            // local runtime — execution is the ENGINE's job.
            SuppressActivation = true,
        };
        return new IlSession(engine);
    }

    /// <summary>The compiled prelude envelope (ship FIRST, once per session).</summary>
    public byte[] PreludeEnvelope()
    {
        if (!_engine.LoadedPrograms.TryGetValue(PreludeModuleRef, out var prelude))
            throw new InvalidOperationException(
                "programs/self.glp did not compile into the local engine (prelude missing)");
        return CompiledIlEnvelopeCodec.Wrap(prelude, PreludeModuleRef);
    }

    /// <summary>
    /// Compile one source unit through the full local pipeline (typecheck
    /// included — GlpEngine.LoadSource) and frame it for LOAD_IL. Compile/type
    /// errors propagate to the caller for loud rendering.
    /// </summary>
    public byte[] CompileUnit(string source, string moduleRef)
    {
        _engine.LoadSource(source, filename: moduleRef);
        var program = _engine.LoadedPrograms[moduleRef];
        return CompiledIlEnvelopeCodec.Wrap(program, moduleRef);
    }

    /// <summary>
    /// Build the RUN_GOAL_IL body for a goal that has ALREADY had its terminating
    /// `.` removed (the REPL line reader owns that strip — Program.cs — exactly as
    /// it does for the text path; stripping again here would silently accept a
    /// malformed `foo..` that the text path reports as a parse error). A bare
    /// `name` runs the compiled nullary entry by reference; anything else
    /// (arguments, a conjunction) is compiled into a one-shot inline wrapper.
    /// </summary>
    public RunGoalIlBody GoalBody(string goal)
    {
        var trimmed = goal.Trim();

        if (IsBareNullary(trimmed))
            return new RunGoalIlBody($"{trimmed}/0", null);

        // One-shot: compile `'__goal__' :- <goal>.` locally, ship it inline.
        // -mode(system): '__goal__' is deliberately an '_'-reserved name so it
        // can never collide with a user procedure (same rule the engine's
        // embedded system sources ride).
        var wrapper = $"-mode(system).\n'__goal__' :- {trimmed}.\n";
        var program = new GlpCompiler().Compile(wrapper);
        var envelope = CompiledIlEnvelopeCodec.Wrap(program, "__goal__");
        return new RunGoalIlBody(OneShotGoalRef, envelope);
    }

    private static bool IsBareNullary(string goal) =>
        goal.Length > 0 && goal.All(c => char.IsLetterOrDigit(c) || c == '_') &&
        char.IsLower(goal[0]);

    /// <summary>
    /// Walk up from AppContext.BaseDirectory (then the current directory) to
    /// the repo root containing programs/self.glp — the same fail-loud
    /// resolution the engine host uses (glp_engine_host/Program.cs).
    /// </summary>
    internal static string ResolveRootSelfGlpPath()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var dir = new DirectoryInfo(start);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "programs", "self.glp");
                if (File.Exists(candidate))
                    return Path.GetFullPath(candidate);
                dir = dir.Parent;
            }
        }
        throw new InvalidOperationException(
            "could not locate programs/self.glp by walking up from " +
            $"{AppContext.BaseDirectory} or {Environment.CurrentDirectory}; an IL session " +
            "compiles locally and must run from within a checkout of the glpnet repository.");
    }
}

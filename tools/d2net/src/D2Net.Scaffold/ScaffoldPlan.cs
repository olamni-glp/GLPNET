using System.Collections.Generic;

namespace D2Net.Scaffold;

/// <summary>
/// One file the scaffold will copy into the target tree. <c>RelPath</c> is
/// the forward-slash repo-rooted path (mirrors <c>dart_files.full_path</c>);
/// <c>AbsSourcePath</c> is the absolute path of the source file on disk;
/// <c>AbsTargetPath</c> is the absolute path of the target file under the
/// configured target tree, native separators.
/// </summary>
public sealed record FileTarget(
    string RelPath,
    string AbsSourcePath,
    string AbsTargetPath,
    string TargetParentDirAbsNative);

/// <summary>
/// One <c>.dart</c> file the scaffold will copy. Inherits the file fields and
/// adds the working-directory name (<c>__&lt;basename&gt;</c>) and absolute path.
/// </summary>
public sealed record DartFileTarget(
    string RelPath,
    string AbsSourcePath,
    string AbsTargetPath,
    string TargetParentDirAbsNative,
    string WorkdirName,
    string WorkdirAbsPath);

/// <summary>
/// One <c>__&lt;basename&gt;</c>-collision detected during pre-walk.
/// </summary>
public sealed record ScaffoldCollision(
    string DartFileRelPath,
    string ConflictingRelPath,
    string Detail);

/// <summary>
/// Pure planner output. Captures every decision the runner needs to make
/// before any IO mutation: which paths to add, which to remove, which dart
/// files get a <c>__</c> workdir, which non-dart files copy verbatim, what
/// collisions exist, and whether the live target is scaffold-managed.
/// </summary>
public sealed record ScaffoldPlan(
    IReadOnlyList<DartFileTarget> DartFiles,
    IReadOnlyList<FileTarget> NonDartFiles,
    IReadOnlyList<string> AddPaths,
    IReadOnlyList<string> RemovePaths,
    IReadOnlyList<ScaffoldCollision> Collisions,
    bool TargetIsManaged,
    bool TargetExists);

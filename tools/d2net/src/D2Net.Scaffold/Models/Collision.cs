namespace D2Net.Scaffold.Models;

public sealed record Collision(
    string DartFileRelPath,
    string CollidingExtension,
    string ExistingFileRelPath);

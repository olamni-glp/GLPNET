using System.Text.Json;
using D2Net.Scaffold;
using D2Net.Scaffold.Models;
using D2Net.Scaffold.Tests.Fixtures;

namespace D2Net.Scaffold.Tests;

public class TrackerSchemaTests
{
    [Fact]
    public void Tracker_TopLevel_IsArray()
    {
        using var fx = new FixtureBuilder();
        fx.AddDartFile("lib/a.dart", "// a");
        ScaffoldRunner.Run(new ScaffoldOptions(fx.SourceRoot, fx.TargetRoot, false));

        using var doc = JsonDocument.Parse(File.ReadAllText(fx.TargetPath("d2net-tracker.json")));
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public void EveryRecord_HasSourceEndingInDartSrc_AndCompanionsObjectWithExactlyNineKeys()
    {
        using var fx = new FixtureBuilder();
        fx.AddDartFile("lib/a.dart", "// a");
        fx.AddDartFile("lib/b/c.dart", "// c");
        ScaffoldRunner.Run(new ScaffoldOptions(fx.SourceRoot, fx.TargetRoot, false));

        using var doc = JsonDocument.Parse(File.ReadAllText(fx.TargetPath("d2net-tracker.json")));
        var expectedExts = new HashSet<string>(CompanionExtensions.All, StringComparer.Ordinal);

        foreach (var record in doc.RootElement.EnumerateArray())
        {
            var source = record.GetProperty("source").GetString();
            Assert.NotNull(source);
            Assert.EndsWith(".dart.src", source);
            Assert.DoesNotContain("\\", source);  // forward slashes only

            var companions = record.GetProperty("companions");
            var actualKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var prop in companions.EnumerateObject())
            {
                actualKeys.Add(prop.Name);
                var status = prop.Value.GetString();
                Assert.Equal(CompanionStatus.Todo, status);
            }
            Assert.Equal(expectedExts, actualKeys);
        }
    }

    [Fact]
    public void RecordCount_EqualsDartFileCount_OutsidePrunedDirs()
    {
        using var fx = new FixtureBuilder();
        fx.AddDartFile("lib/one.dart", "// one");
        fx.AddDartFile("lib/two.dart", "// two");
        fx.AddDartFile("test/three.dart", "// three");
        fx.AddDartFile(".dart_tool/excluded.dart", "// excluded");
        ScaffoldRunner.Run(new ScaffoldOptions(fx.SourceRoot, fx.TargetRoot, false));

        using var doc = JsonDocument.Parse(File.ReadAllText(fx.TargetPath("d2net-tracker.json")));
        Assert.Equal(3, doc.RootElement.GetArrayLength());
    }
}

using D2Net.Init;

namespace D2Net.Init.Tests;

public class DbConnectionStringBuilderTests
{
    [Fact]
    public void BuildWrapsTheDbFilePathInDataSource()
    {
        var s = DbConnectionStringBuilder.Build(@"D:\repo\.D2NET\pgdb\workspace.sqlite");
        Assert.Equal(@"Data Source=D:\repo\.D2NET\pgdb\workspace.sqlite", s);
    }

    [Fact]
    public void EngineNameIsSqlite()
    {
        Assert.Equal("sqlite", DbConnectionStringBuilder.EngineName);
    }
}

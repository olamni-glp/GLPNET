using D2Net.Init;

namespace D2Net.Init.Tests;

public class DbConnectionStringBuilderTests
{
    [Fact]
    public void EngineNameIsPglite()
    {
        Assert.Equal("pglite", DbConnectionStringBuilder.EngineName);
    }

    [Fact]
    public void BuildNpgsqlComposesStandardConnectionString()
    {
        var opts = BridgeOptions.ForDataDir(@"D:\repo\.D2NET\pgdb", port: 54400);
        var s = DbConnectionStringBuilder.BuildNpgsql(opts);
        Assert.Contains("Host=127.0.0.1", s);
        Assert.Contains("Port=54400", s);
        Assert.Contains("Database=d2net", s);
        Assert.Contains("Username=d2net", s);
        Assert.Contains("Password=d2net", s);
        Assert.Contains("SSL Mode=Disable", s);
    }

    [Fact]
    public void BuildOdbcUsesModernUnicodeDriver()
    {
        var opts = BridgeOptions.ForDataDir(@"D:\repo\.D2NET\pgdb", port: 54400);
        var s = DbConnectionStringBuilder.BuildOdbc(opts);
        Assert.StartsWith("Driver={PostgreSQL ODBC Driver(UNICODE)}", s);
        Assert.Contains("Server=127.0.0.1", s);
        Assert.Contains("Port=54400", s);
        Assert.Contains("Database=d2net", s);
        Assert.Contains("Uid=d2net", s);
        Assert.Contains("Pwd=d2net", s);
        Assert.Contains("SSLmode=disable", s);
    }
}

// SC-007 — @name loud-fail addressing (feature 041-crdtmsg-mvp, T043).

using GlpRuntime.CrdtMsg.Envelope;
using GlpRuntime.CrdtMsg.Route;

namespace GlpRuntime.CrdtMsg.Tests;

public sealed class AddressingTests
{
    private static AddressBook Book() => new(
        new HashSet<string> { "A", "B", "C" },
        new Dictionary<string, IReadOnlyList<string>> { ["@editors"] = new[] { "B", "C" } });

    [Fact]
    public void Bare_peer_and_group_resolve()
    {
        Assert.Equal(new[] { "B" }, Book().Resolve("B"));
        Assert.Equal(new[] { "B", "C" }, Book().Resolve("@editors"));
    }

    [Fact]
    public void Unknown_at_name_is_a_reported_error_not_a_fallback()
    {
        Assert.Throws<CrdtMsgException>(() => Book().Resolve("@ghost"));
    }

    [Fact]
    public void Unknown_peer_is_a_reported_error_not_a_fallback()
    {
        Assert.Throws<CrdtMsgException>(() => Book().Resolve("Z"));
    }

    [Fact]
    public void Group_member_not_authenticated_fails_loud()
    {
        var book = new AddressBook(
            new HashSet<string> { "A" },
            new Dictionary<string, IReadOnlyList<string>> { ["@g"] = new[] { "A", "X" } });
        Assert.Throws<CrdtMsgException>(() => book.Resolve("@g"));
    }
}

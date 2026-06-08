using GlpRuntime.Link.Reliability;
using Xunit;

namespace GlpRuntime.Link.Tests;

/// <summary>T025 bounded-backpressure tests (FR-025, SC-013).</summary>
public class SendWindowTests
{
    [Fact]
    public void DefaultWindow_IsEight()
    {
        using var w = new SendWindow();
        Assert.Equal(8, w.Capacity);
        Assert.Equal(8, w.Available);
        Assert.Equal(0, w.InFlight);
    }

    [Fact]
    public void Acquires_UpToCapacity_ThenRefuses()
    {
        using var w = new SendWindow(3);
        Assert.True(w.TryAcquire());
        Assert.True(w.TryAcquire());
        Assert.True(w.TryAcquire());
        Assert.False(w.TryAcquire());       // window full → producer must suspend
        Assert.Equal(3, w.InFlight);
        Assert.Equal(0, w.Available);
    }

    [Fact]
    public void Release_FreesCredit()
    {
        using var w = new SendWindow(2);
        w.TryAcquire();
        w.TryAcquire();
        Assert.False(w.TryAcquire());
        w.Release();                        // ack one in-flight frame
        Assert.True(w.TryAcquire());
    }

    [Fact]
    public async Task AcquireAsync_SuspendsWhenFull_ResumesOnRelease()
    {
        using var w = new SendWindow(1);
        Assert.True(w.TryAcquire());        // window now full

        var pending = w.AcquireAsync();     // producer suspends here
        Assert.False(pending.IsCompleted);

        w.Release();                        // consumer acks → credit frees
        await pending.WaitAsync(TimeSpan.FromSeconds(5)); // resumes
        Assert.True(pending.IsCompletedSuccessfully);
    }

    [Fact]
    public void IndependentLinks_NoHeadOfLineBlock()
    {
        using var a = new SendWindow(1);
        using var b = new SendWindow(1);
        Assert.True(a.TryAcquire());
        Assert.False(a.TryAcquire());       // link A saturated
        Assert.True(b.TryAcquire());        // link B unaffected — no cross-link HoL block
    }

    [Fact]
    public void OverRelease_Throws()
    {
        using var w = new SendWindow(2);
        w.TryAcquire();
        w.Release();
        Assert.Throws<SemaphoreFullException>(() => w.Release()); // ack for a non-in-flight frame
    }

    [Fact]
    public void Window_MustBePositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SendWindow(0));
    }

    [Fact]
    public async Task AcquireAsync_RespectsCancellation()
    {
        using var w = new SendWindow(1);
        w.TryAcquire();
        using var cts = new CancellationTokenSource();
        var pending = w.AcquireAsync(cts.Token);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }
}

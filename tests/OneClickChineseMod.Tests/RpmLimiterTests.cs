using System.Collections.Concurrent;
using OneClickChineseMod.Core;

namespace OneClickChineseMod.Tests;

public class RpmLimiterTests
{
    [Fact]
    public void TryAcquire_UnderLimit_ReturnsTrue()
    {
        var limiter = new RpmLimiter(10);

        for (int i = 0; i < 10; i++)
        {
            Assert.True(limiter.TryAcquire(), $"第 {i + 1} 次获取应该成功");
        }
    }

    [Fact]
    public void TryAcquire_AtLimit_ReturnsFalse()
    {
        var limiter = new RpmLimiter(3);

        Assert.True(limiter.TryAcquire());
        Assert.True(limiter.TryAcquire());
        Assert.True(limiter.TryAcquire());
        Assert.False(limiter.TryAcquire());
    }

    [Fact]
    public void TryAcquire_AtLimit_AllowsNoMore()
    {
        var limiter = new RpmLimiter(2);

        limiter.TryAcquire();
        limiter.TryAcquire();

        for (int i = 0; i < 5; i++)
        {
            Assert.False(limiter.TryAcquire());
        }
    }

    [Fact]
    public void GetCurrentRpm_Empty_ReturnsZero()
    {
        var limiter = new RpmLimiter(100);

        Assert.Equal(0, limiter.GetCurrentRpm());
    }

    [Fact]
    public void GetCurrentRpm_AfterAcquires_ReturnsCorrectCount()
    {
        var limiter = new RpmLimiter(100);

        limiter.TryAcquire();
        limiter.TryAcquire();
        limiter.TryAcquire();

        Assert.Equal(3, limiter.GetCurrentRpm());
    }

    [Fact]
    public void MaxRpm_ReturnsConfiguredValue()
    {
        var limiter1 = new RpmLimiter(500);
        var limiter2 = new RpmLimiter(800);
        var limiter3 = new RpmLimiter(60);

        Assert.Equal(500, limiter1.MaxRpm);
        Assert.Equal(800, limiter2.MaxRpm);
        Assert.Equal(60, limiter3.MaxRpm);
    }

    [Fact]
    public async Task TryAcquire_IsThreadSafe()
    {
        var limiter = new RpmLimiter(100);
        var tasks = new List<Task>();

        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 10; j++)
                {
                    limiter.TryAcquire();
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(100, limiter.GetCurrentRpm());
    }

    [Fact]
    public async Task GetCurrentRpm_IsThreadSafe()
    {
        var limiter = new RpmLimiter(200);
        var counts = new ConcurrentBag<int>();
        var tasks = new List<Task>();

        for (int i = 0; i < 5; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 100; j++)
                {
                    limiter.TryAcquire();
                }
            }));
        }

        await Task.WhenAll(tasks);

        Assert.Equal(200, limiter.GetCurrentRpm());
    }

    [Fact]
    public void TryAcquire_ZeroMaxRpm_AlwaysReturnsFalse()
    {
        var limiter = new RpmLimiter(0);

        Assert.False(limiter.TryAcquire());
        Assert.False(limiter.TryAcquire());
    }

    [Fact]
    public void TryAcquire_OneMaxRpm_AllowsOneThenBlocks()
    {
        var limiter = new RpmLimiter(1);

        Assert.True(limiter.TryAcquire());
        Assert.False(limiter.TryAcquire());
        Assert.Equal(1, limiter.GetCurrentRpm());
    }
}

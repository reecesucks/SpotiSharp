namespace SpotiSharpBackend.Tests;

public class RatelimiterTests
{
    // One sequential test: the cooldown is process-global state, so ordering within a single
    // fact is the only way to keep the assertions deterministic.
    [Fact]
    public async Task A_429_cooldown_refuses_calls_can_be_waited_out_and_expires()
    {
        Ratelimiter.NotifyRetryAfter(TimeSpan.FromSeconds(3));

        Assert.True(Ratelimiter.InCooldown);
        Assert.True(Ratelimiter.CooldownRemaining > TimeSpan.Zero);
        Assert.False(Ratelimiter.CanRequestCall());
        Assert.False(Ratelimiter.RequestCall());

        // a capped wait gives up while the cooldown still runs
        var capped = await Ratelimiter.WaitOutCooldownAsync(TimeSpan.FromMilliseconds(300));
        Assert.True(capped >= TimeSpan.FromMilliseconds(200));
        Assert.True(capped < TimeSpan.FromSeconds(3));
        Assert.True(Ratelimiter.InCooldown);

        // an uncapped wait sees it through
        var waited = await Ratelimiter.WaitOutCooldownAsync(TimeSpan.FromSeconds(30));
        Assert.True(waited > TimeSpan.Zero);
        Assert.False(Ratelimiter.InCooldown);
        Assert.Equal(TimeSpan.Zero, Ratelimiter.CooldownRemaining);
        Assert.True(Ratelimiter.CanRequestCall());
    }
}

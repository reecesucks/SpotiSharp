namespace SpotiSharpBackend;

public static class Ratelimiter
{
    private const int MAX_API_CALLS_PER_SCOND = 5;
    private static int _currentCallCount;

     private static long _cooldownUntilUtcTicks;

    static Ratelimiter()
    {
        var thread = new Thread(ResetLimit);
        thread.Start();
    }

    private static void ResetLimit()
    {
        while (true)
        {
            Thread.Sleep(500);
            _currentCallCount = 0;
        }
    }

    public static void NotifyRetryAfter(TimeSpan retryAfter)
    {
        if (retryAfter <= TimeSpan.Zero) retryAfter = TimeSpan.FromSeconds(1);
        long until = DateTime.UtcNow.Add(retryAfter).Ticks;

        // several calls can hit 429 at once; keep whichever cooldown reaches furthest
        long current;
        while (until > (current = Interlocked.Read(ref _cooldownUntilUtcTicks)))
        {
            if (Interlocked.CompareExchange(ref _cooldownUntilUtcTicks, until, current) == current) break;
        }
    }

    public static bool InCooldown => DateTime.UtcNow.Ticks < Interlocked.Read(ref _cooldownUntilUtcTicks);

    public static bool RequestCall()
    {
        if (!CanRequestCall()) return false;
        _currentCallCount++;
        return true;
    }

    public static bool CanRequestCall()
    {
        return !InCooldown && _currentCallCount < MAX_API_CALLS_PER_SCOND;
    }

}

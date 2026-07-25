namespace SpotiSharpBackend;

public static class Ratelimiter
{
    private const int MAX_API_CALLS_PER_SCOND = 5;
    private static int _currentCallCount;

     private static long _cooldownUntilUtcTicks;

    static Ratelimiter()
    {
        var thread = new Thread(ResetLimit) { IsBackground = true };
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
        DiagnosticLog.Write($"[Ratelimiter] 429 from Spotify, cooling down for {retryAfter.TotalSeconds:0.#}s");
        long until = DateTime.UtcNow.Add(retryAfter).Ticks;

        // several calls can hit 429 at once; keep whichever cooldown reaches furthest
        long current;
        while (until > (current = Interlocked.Read(ref _cooldownUntilUtcTicks)))
        {
            if (Interlocked.CompareExchange(ref _cooldownUntilUtcTicks, until, current) == current) break;
        }
    }

    public static bool InCooldown => CooldownRemaining > TimeSpan.Zero;

    public static TimeSpan CooldownRemaining
    {
        get
        {
            long remaining = Interlocked.Read(ref _cooldownUntilUtcTicks) - DateTime.UtcNow.Ticks;
            return remaining > 0 ? TimeSpan.FromTicks(remaining) : TimeSpan.Zero;
        }
    }

    // For flows off the shared loop thread that would rather wait out a cooldown than fail.
    // Returns how long it actually waited so callers can extend their own deadlines by it.
    public static async Task<TimeSpan> WaitOutCooldownAsync(TimeSpan cap)
    {
        var waited = TimeSpan.Zero;
        while (waited < cap)
        {
            var remaining = CooldownRemaining;
            if (remaining <= TimeSpan.Zero) break;
            if (remaining > cap - waited) remaining = cap - waited;
            await Task.Delay(remaining);
            waited += remaining;
        }
        return waited;
    }

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

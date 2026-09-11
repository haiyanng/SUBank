namespace SUBank.Client.Services;

public sealed class IdempotencyIntentTracker<TIntent>
    where TIntent : notnull
{
    private TIntent? currentIntent;
    private string? currentKey;

    public string GetOrCreateKey(TIntent intent)
    {
        if (currentKey is null ||
            !EqualityComparer<TIntent>.Default.Equals(currentIntent!, intent))
        {
            currentIntent = intent;
            currentKey = Guid.NewGuid().ToString("N");
        }

        return currentKey;
    }

    public void Complete()
    {
        currentIntent = default;
        currentKey = null;
    }
}

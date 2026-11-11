public readonly struct DamageInfo
{
    public float Amount { get; }
    public ulong SourceClientId { get; }

    public DamageInfo(float amount, ulong sourceClientId)
    {
        Amount = amount;
        SourceClientId = sourceClientId;
    }
}
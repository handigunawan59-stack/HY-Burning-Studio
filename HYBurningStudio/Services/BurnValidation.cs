namespace HYBurningStudio.Services;
public static class BurnValidation
{
    public static bool AudioSectorsFit(IReadOnlyList<long> lengths, long capacity) =>
        lengths.Count is > 0 and <= 99 && lengths.All(n => n > 0 && n % 2352 == 0) &&
        lengths.Sum(n => n / 2352) + Math.Max(0, lengths.Count - 1) * 150L <= capacity;
    public static bool ValidIsoLength(long bytes) => bytes > 0 && bytes % 2048 == 0;
    // Track-at-once inserts a two-second gap between successive tracks and pads short tracks to four seconds.
    public static bool AudioFits(IReadOnlyList<TimeSpan> durations, int capacitySeconds) =>
        durations.Count is > 0 and <= 99 && capacitySeconds > 0 &&
        durations.All(d => d > TimeSpan.Zero) &&
        durations.Sum(d => Math.Max(4, Math.Ceiling(d.TotalSeconds))) + Math.Max(0, durations.Count - 1) * 2 <= capacitySeconds;
}

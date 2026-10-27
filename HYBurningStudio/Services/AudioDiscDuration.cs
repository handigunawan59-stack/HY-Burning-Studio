namespace HYBurningStudio.Services;

/// <summary>Estimated Audio CD duration before decoded PCM length is known.</summary>
public static class AudioDiscDuration
{
    public const int MaximumTracks = 99;

    public static double EstimateSeconds(IReadOnlyCollection<TimeSpan> durations) =>
        durations.Sum(d => Math.Max(4, Math.Ceiling(d.TotalSeconds))) + Math.Max(0, durations.Count - 1) * 2;
}

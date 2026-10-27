namespace HYBurningStudio.Services;

public static class WriteSpeed
{
    public static int[] Normalize(IEnumerable<long> speeds) => speeds
        .Where(speed => speed > 0 && speed <= int.MaxValue)
        .Select(speed => (int)speed).Distinct().Order().ToArray();

    public static void Validate(int? selected, IEnumerable<int> supported)
    {
        if (selected.HasValue && !supported.Contains(selected.Value))
            throw new InvalidOperationException("Kecepatan yang dipilih tidak lagi didukung oleh drive atau disc. Periksa disc dan pilih kecepatan lagi.");
    }
}

using System.IO;
namespace HYBurningStudio.Models;

public sealed class ProjectFiles(bool audio)
{
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase) { ".mp3", ".wav", ".flac" };
    public List<string> Items { get; } = [];
    public long TotalBytes => Items.Sum(SizeOf);

    public bool Add(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (Items.Contains(fullPath, StringComparer.OrdinalIgnoreCase)) return false;
        if (audio && (!File.Exists(fullPath) || !AudioExtensions.Contains(Path.GetExtension(fullPath)))) return false;
        if (!audio && !File.Exists(fullPath) && !Directory.Exists(fullPath)) return false;
        Items.Add(fullPath);
        return true;
    }
    public void Remove(string path) => Items.Remove(path);
    public static long SizeOf(string path)
    {
        if (File.Exists(path)) return new FileInfo(path).Length;
        if (Directory.Exists(path)) return Directory.EnumerateFiles(path, "*", new EnumerationOptions { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint }).Sum(p => new FileInfo(p).Length);
        return 0;
    }
}

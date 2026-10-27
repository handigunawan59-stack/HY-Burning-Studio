using System.IO;

namespace HYBurningStudio.Services;

public static class ThemePreference
{
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HY Burning Studio", "theme.txt");

    public static string Load(string path)
    {
        try { return File.ReadAllText(path).Trim() == "dark" ? "dark" : "light"; }
        catch (IOException) { return "light"; }
        catch (UnauthorizedAccessException) { return "light"; }
    }

    public static void Save(string path, string value)
    {
        if (value is not ("light" or "dark")) throw new ArgumentException("Tema tidak dikenali.", nameof(value));
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, value); File.Move(temporary, full, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}

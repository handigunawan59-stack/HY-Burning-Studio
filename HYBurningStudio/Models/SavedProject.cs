using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HYBurningStudio.Models;

/// <summary>A project records paths, not copies of the user's source files.</summary>
public sealed class SavedProject
{
    [JsonRequired] public int Version { get; set; } = 1;
    [JsonRequired] public string Kind { get; set; } = "Data";
    [JsonRequired] public string VolumeName { get; set; } = "HY_BURNING";
    [JsonRequired] public List<string> Paths { get; set; } = [];

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public void Save(string file)
    {
        Validate(false);
        string full = Path.GetFullPath(file);
        string temporary = Path.Combine(Path.GetDirectoryName(full)!, ".hyburn-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(this, JsonOptions));
            File.Move(temporary, full, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public static SavedProject Load(string file)
    {
        if (new FileInfo(file).Length > 4 * 1024 * 1024) throw new InvalidDataException("File proyek terlalu besar.");
        SavedProject project;
        try { project = JsonSerializer.Deserialize<SavedProject>(File.ReadAllText(file), JsonOptions) ?? throw new InvalidDataException("File proyek kosong."); }
        catch (JsonException ex) { throw new InvalidDataException("Format file proyek tidak valid.", ex); }
        project.Validate(true);
        return project;
    }

    private void Validate(bool requireSources)
    {
        if (Version != 1 || Kind is not ("Data" or "Audio" or "Iso") || VolumeName is null || VolumeName.Length > 32 || Paths is null || Paths.Count > 5000 || (Kind == "Iso" && Paths.Count > 1))
            throw new InvalidDataException("Isi atau versi file proyek tidak didukung.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in Paths)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !seen.Add(path)) throw new InvalidDataException("Daftar file proyek tidak valid.");
            string extension = Path.GetExtension(path);
            if (Kind == "Audio" && !new[] { ".mp3", ".wav", ".flac" }.Contains(extension, StringComparer.OrdinalIgnoreCase)) throw new InvalidDataException("Proyek audio memiliki format file yang tidak didukung.");
            if (Kind == "Iso" && !extension.Equals(".iso", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Proyek ISO harus menunjuk file ISO.");
            if (requireSources && !File.Exists(path) && (Kind != "Data" || !Directory.Exists(path))) throw new FileNotFoundException("File atau folder sumber tidak ditemukan. Proyek yang sedang terbuka tidak diubah.", path);
        }
    }
}

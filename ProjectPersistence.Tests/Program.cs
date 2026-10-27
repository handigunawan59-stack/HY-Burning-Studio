using HYBurningStudio.Models;

static void Check(bool value, string message) { if (!value) throw new Exception(message); }
static void Throws<T>(Action action, string message) where T : Exception
{
    try { action(); } catch (T) { return; }
    throw new Exception(message);
}
var root = Path.Combine(Path.GetTempPath(), "HYBurningStudio-project-test-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var file1 = Path.Combine(root, "satu.mp3"); var file2 = Path.Combine(root, "dua.flac");
    var iso = Path.Combine(root, "image.iso");
    File.WriteAllText(file1, "sample"); File.WriteAllText(file2, "sample"); File.WriteAllText(iso, "sample");
    var saved = Path.Combine(root, "test.hyburn");
    foreach (var kind in new[] { "Data", "Audio", "Iso" })
    {
        var paths = kind == "Iso" ? new List<string> { iso } : new List<string> { file2, file1 };
        var project = new SavedProject { Kind = kind, VolumeName = "CONTOH", Paths = paths };
        project.Save(saved);
        var read = SavedProject.Load(saved);
        Check(read.Kind == kind && read.VolumeName == "CONTOH" && read.Paths.SequenceEqual(paths), "Roundtrip must preserve type, label and file order");
    }
    var missingProject = new SavedProject { Kind = "Data", Paths = [Path.Combine(root, "missing.txt")] };
    missingProject.Save(saved);
    Throws<FileNotFoundException>(() => SavedProject.Load(saved), "Missing sources must be reported");
    var existing = File.ReadAllText(saved);
    Throws<InvalidDataException>(() => new SavedProject { Kind = "bad" }.Save(saved), "Invalid project must be rejected");
    Check(File.ReadAllText(saved) == existing, "A rejected save must not alter an existing project");
    File.WriteAllText(saved, "{ invalid json");
    Throws<InvalidDataException>(() => SavedProject.Load(saved), "Corrupt project files must be rejected");
    File.WriteAllText(saved, "{}");
    Throws<InvalidDataException>(() => SavedProject.Load(saved), "Missing project fields must not silently create an empty project");
    Console.WriteLine("Project save/open: data, audio, ISO, order, missing source and corrupt file tests passed.");
}
finally { Directory.Delete(root, true); }

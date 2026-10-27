using HYBurningStudio.Models;

var root = Path.Combine(Path.GetTempPath(), "hy-burning-tests-" + Guid.NewGuid());
Directory.CreateDirectory(root);
try
{
    string mp3 = Path.Combine(root, "track.MP3"), text = Path.Combine(root, "doc.txt");
    File.WriteAllBytes(mp3, new byte[10]); File.WriteAllBytes(text, new byte[20]);
    var audio = new ProjectFiles(true);
    if (!audio.Add(mp3) || audio.Add(text) || audio.Add(mp3) || audio.Items.Count != 1)
        throw new Exception("Audio accepts MP3, rejects non-audio and duplicate files");
    var data = new ProjectFiles(false);
    if (!data.Add(text) || data.TotalBytes != 20 || data.Add(text))
        throw new Exception("Data totals actual file sizes and rejects duplicates");
    data.Remove(text);
    if (data.TotalBytes != 0) throw new Exception("Removing an item updates data size");
    Console.WriteLine("Project file tests passed.");
}
finally { Directory.Delete(root, true); }

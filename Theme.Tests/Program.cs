using System.Reflection;

var type = Assembly.GetExecutingAssembly().GetType("HYBurningStudio.Services.ThemePreference");
if (type is null) throw new Exception("Theme preference service is missing");
var directory = Path.Combine(Path.GetTempPath(), "HY-theme-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    var path = Path.Combine(directory, "theme.txt");
    var load = type.GetMethod("Load")!;
    var save = type.GetMethod("Save")!;
    if ((string)load.Invoke(null, [path])! != "light") throw new Exception("Missing preference should default to light");
    save.Invoke(null, [path, "dark"]);
    if ((string)load.Invoke(null, [path])! != "dark") throw new Exception("Dark preference did not persist");
    save.Invoke(null, [path, "light"]);
    if ((string)load.Invoke(null, [path])! != "light") throw new Exception("Light preference did not persist");
    File.WriteAllText(path, "invalid");
    if ((string)load.Invoke(null, [path])! != "light") throw new Exception("Invalid preference should default to light");
    Console.WriteLine("Theme preference persistence and invalid fallback passed.");
}
finally { Directory.Delete(directory, true); }

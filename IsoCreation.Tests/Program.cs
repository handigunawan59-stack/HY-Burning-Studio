using System.Reflection;
using System.Text;

static void Check(bool x, string message) { if (!x) throw new Exception(message); }
var type = Assembly.GetExecutingAssembly().GetType("HYBurningStudio.Services.IsoCreationService");
Check(type is not null, "ISO creation service must be available");
var method = type!.GetMethod("CreateFromFiles")!;
var validate = type.GetMethod("ValidateDiscLayout")!;
var toc = new byte[20]; toc[2] = 1; toc[3] = 1; toc[5] = 4; toc[6] = 1; toc[14] = 0xAA; toc[18] = 4; toc[19] = 0;
var session = new byte[12]; session[2] = 1; session[3] = 1;
Check((long)validate.Invoke(null, [toc, session])! == 1024, "Single data track should report lead-out sectors");
session[3] = 2;
try { validate.Invoke(null, [toc, session]); throw new Exception("Multisession must be rejected"); }
catch (TargetInvocationException ex) when (ex.InnerException is NotSupportedException) { }
session[3] = 1; toc[5] = 0;
try { validate.Invoke(null, [toc, session]); throw new Exception("Audio track must be rejected"); }
catch (TargetInvocationException ex) when (ex.InnerException is NotSupportedException) { }
string root = Path.Combine(Path.GetTempPath(), "HYISO-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var input = Path.Combine(root, "example.txt"); File.WriteAllText(input, "HELLO HY BURNING STUDIO");
    var output = Path.Combine(root, "result.iso");
    method.Invoke(null, [new[] { input }, "HY_TEST", output, false, null]);
    var image = File.ReadAllBytes(output);
    Check(image.Length > 17 * 2048 && image.Length % 2048 == 0, "Image must consist of complete sectors");
    Check(Encoding.ASCII.GetString(image, 16 * 2048 + 1, 5) == "CD001", "ISO9660 primary descriptor missing");
    Check(Encoding.ASCII.GetString(image).Contains("HELLO HY BURNING STUDIO"), "Image must contain source contents");
    var before = File.ReadAllBytes(output);
    try { method.Invoke(null, [new[] { input }, "HY_TEST", output, false, null]); throw new Exception("Existing output must be protected"); }
    catch (TargetInvocationException ex) when (ex.InnerException is IOException) { }
    Check(before.SequenceEqual(File.ReadAllBytes(output)), "An existing ISO must remain unchanged");
    try { method.Invoke(null, [new[] { input }, "HY_TEST", input, true, null]); throw new Exception("Source must not be overwritten"); }
    catch (TargetInvocationException ex) when (ex.InnerException is IOException) { }
    Check(File.ReadAllText(input) == "HELLO HY BURNING STUDIO", "Source must be preserved");
    string folder = Path.Combine(root, "folder"); Directory.CreateDirectory(folder);
    File.WriteAllText(Path.Combine(folder, "nested.txt"), "NESTED SOURCE");
    var nestedOutput = Path.Combine(root, "nested.iso");
    method.Invoke(null, [new[] { folder }, "HY_TEST", nestedOutput, false, null]);
    Check(Encoding.ASCII.GetString(File.ReadAllBytes(nestedOutput)).Contains("NESTED SOURCE"), "ISO must contain nested file contents");
    try { method.Invoke(null, [new[] { folder }, "HY_TEST", Path.Combine(folder, "result.iso"), true, null]); throw new Exception("ISO inside source folder must be rejected"); }
    catch (TargetInvocationException ex) when (ex.InnerException is IOException) { }
    Console.WriteLine("ISO from files/folders, ISO descriptor, contents, layout validation, and overwrite safety passed.");
}
finally { Directory.Delete(root, true); }

using HYBurningStudio.Services;

static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
Check(BurnValidation.ValidIsoLength(2048), "A one-sector image must be valid");
Check(!BurnValidation.ValidIsoLength(0), "An empty image must be rejected");
Check(!BurnValidation.ValidIsoLength(2049), "An image must contain whole sectors");
Check(BurnValidation.AudioFits(new[] { TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(40) }, 80 * 60), "Tracks fitting a CD must be accepted");
Check(!BurnValidation.AudioFits(new[] { TimeSpan.FromMinutes(81) }, 80 * 60), "Tracks exceeding capacity must be rejected");
Check(!BurnValidation.AudioFits(Array.Empty<TimeSpan>(), 80 * 60), "Empty track list must be rejected");
Check(BurnValidation.AudioSectorsFit(new long[] { 75 * 2352, 75 * 2352 }, 301), "Audio and track gaps fitting capacity must be accepted");
Check(!BurnValidation.AudioSectorsFit(new long[] { 75 * 2352, 75 * 2352 }, 299), "Converted PCM and gaps exceeding capacity must be rejected");
Console.WriteLine("Burn validation tests passed.");
var service = new BurnService();
var drives = service.GetDrives();
Console.WriteLine($"Drive enumeration completed: {drives.Count} drive(s).");
var verification = typeof(BurnService).GetMethod("Verification", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
object dataWriter = Activator.CreateInstance(Type.GetTypeFromProgID("IMAPI2.MsftDiscFormat2Data", true)!)!;
object audioWriter = Activator.CreateInstance(Type.GetTypeFromProgID("IMAPI2.MsftDiscFormat2TrackAtOnce", true)!)!;
try
{
    verification.Invoke(null, new object[] { dataWriter, true, false });
    verification.Invoke(null, new object[] { audioWriter, true, true });
    Console.WriteLine("IMAPI verification interfaces accepted full data and quick audio settings.");
    var eventId = new Guid("2735413C-7F64-5B0F-8F00-5D77AFBE261E");
    Action<object, object> callback = (_, _) => { };
    System.Runtime.InteropServices.ComEventsHelper.Combine(dataWriter, eventId, 0x200, callback);
    System.Runtime.InteropServices.ComEventsHelper.Remove(dataWriter, eventId, 0x200, callback);
    Console.WriteLine("Attached and detached IMAPI data progress callback.");
}
finally { System.Runtime.InteropServices.Marshal.ReleaseComObject(dataWriter); System.Runtime.InteropServices.Marshal.ReleaseComObject(audioWriter); }
dynamic image = Activator.CreateInstance(Type.GetTypeFromProgID("IMAPI2FS.MsftFileSystemImage", true)!)!;
image.FileSystemsToCreate = 7;
image.VolumeName = "TEST_HY";
dynamic root = image.Root;
root.AddDirectory("Contoh");
dynamic folder = root["Contoh"];
var sample = Path.GetTempFileName();
try
{
    File.WriteAllText(sample, "contoh");
    var open = typeof(BurnService).GetMethod("OpenStream", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
    object stream = open.Invoke(null, new object[] { sample })!;
    folder.AddFile("satu.txt", stream);
    var tree = Path.Combine(Path.GetTempPath(), "HY-test-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tree);
    try
    {
        File.WriteAllText(Path.Combine(tree, "dua.txt"), "contoh"); folder.AddTree(tree, false);
        dynamic result = image.CreateResultImage();
        Check((int)result.TotalBlocks > 0, "IMAPI filesystem image must contain blocks");
        System.Runtime.InteropServices.Marshal.ReleaseComObject(result);
    }
    finally { Directory.Delete(tree, true); }
    System.Runtime.InteropServices.Marshal.ReleaseComObject(stream);
    Console.WriteLine("Created IMAPI filesystem image with a folder and file.");
}
finally { File.Delete(sample); System.Runtime.InteropServices.Marshal.ReleaseComObject(folder); System.Runtime.InteropServices.Marshal.ReleaseComObject(root); System.Runtime.InteropServices.Marshal.ReleaseComObject(image); }
if (args.Length > 0)
{
    var converter = typeof(BurnService).GetMethod("ConvertTrack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
    foreach (var path in args)
    {
        var duration = service.GetAudioDuration(path);
        Check(duration > TimeSpan.Zero, $"Unable to read duration for {path}");
        var pcm = Path.GetTempFileName();
        try
        {
            converter.Invoke(null, new object[] { path, pcm });
            var length = new FileInfo(pcm).Length;
            Check(length >= 44100 * 4 * 4 && length % 2352 == 0, $"Invalid audio CD PCM data for {path}");
            Console.WriteLine($"Decoded {Path.GetExtension(path)}: {duration.TotalSeconds:F2}s; {length} bytes PCM.");
        }
        finally { File.Delete(pcm); }
    }
}

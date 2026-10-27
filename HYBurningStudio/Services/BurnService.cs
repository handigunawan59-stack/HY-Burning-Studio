using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using NAudio.Wave;

namespace HYBurningStudio.Services;

public record DriveInfo(string Id, string Name, string Media, long FreeBytes, bool IsBlank, bool IsCd, int AudioCapacitySeconds);
public record BurnProgress(string Status, double? Percent);

/// <summary>Writes optical media through Windows IMAPI2. Burning operations are synchronous and should run on a worker thread.</summary>
public sealed class BurnService
{
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateStreamOnFileEx(string fileName, uint mode, uint attributes, bool create, IStream? template, out IStream stream);

    [ComImport, Guid("D2FFD834-958B-426d-8470-2A13879C6A91"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IBurnVerification
    {
        void SetLevel(int level);
        void GetLevel(out int level);
    }

    private static dynamic NewCom(string progId) => Activator.CreateInstance(Type.GetTypeFromProgID(progId, true)!)!;
    private static IStream OpenStream(string path) { SHCreateStreamOnFileEx(path, 0x20, 0, false, null, out var stream); return stream; }
    private static dynamic Recorder(string id) { dynamic recorder = NewCom("IMAPI2.MsftDiscRecorder2"); recorder.InitializeDiscRecorder(id); return recorder; }
    private static dynamic DataWriter(dynamic recorder) { dynamic writer = NewCom("IMAPI2.MsftDiscFormat2Data"); writer.Recorder = recorder; writer.ClientName = "HY Burning Studio"; return writer; }
    private static void Verification(object writer, bool verify, bool audio = false) => ((IBurnVerification)writer).SetLevel(verify ? (audio ? 1 : 2) : 0);
    private static void Release(object? value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }

    public IReadOnlyList<DriveInfo> GetDrives()
    {
        var drives = new List<DriveInfo>();
        dynamic master = NewCom("IMAPI2.MsftDiscMaster2");
        try
        {
            for (var i = 0; i < (int)master.Count; i++)
            {
                string id = master[i]; dynamic? recorder = null; dynamic? writer = null;
                try
                {
                    recorder = Recorder(id);
                    string name = ((string)recorder.ProductId).Trim();
                    var roots = (Array)recorder.VolumePathNames;
                    if (roots.Length > 0) name += $" ({roots.GetValue(0)})";
                    string media = DriveStatusText.UnknownMedia; long freeBytes = 0; bool blank = false; bool cd = false; int seconds = 0;
                    try
                    {
                        writer = DataWriter(recorder);
                        if ((bool)writer.IsCurrentMediaSupported(recorder))
                        {
                            int type = writer.CurrentPhysicalMediaType;
                            cd = type is 2 or 3;
                            media = MediaName(type);
                            blank = writer.MediaHeuristicallyBlank;
                            if (blank && IsSupportedDisc(type))
                            {
                                int sectors = writer.FreeSectorsOnMedia;
                                freeBytes = (long)sectors * 2048;
                                if (cd) seconds = sectors / 75;
                            }
                        }
                    }
                    catch (COMException) { /* Drive exists but media is absent or unreadable. */ }
                    drives.Add(new DriveInfo(id, name, media, freeBytes, blank, cd, seconds));
                }
                finally { Release(writer); Release(recorder); }
            }
        }
        finally { Release(master); }
        return drives;
    }

    public TimeSpan GetAudioDuration(string path)
    {
        CheckAudioPath(path);
        using var reader = new MediaFoundationReader(path);
        return reader.TotalTime;
    }

    public void BurnData(string driveId, IReadOnlyList<string> paths, string volumeName, bool verify, bool eject, IProgress<BurnProgress>? progress = null)
    {
        if (paths.Count == 0) throw new ArgumentException("Tambahkan setidaknya satu file atau folder.");
        if (string.IsNullOrWhiteSpace(volumeName)) throw new ArgumentException("Nama disc tidak boleh kosong.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) throw new FileNotFoundException("File atau folder tidak ditemukan.", path);
            if (!names.Add(Path.GetFileName(Path.TrimEndingDirectorySeparator(path)))) throw new ArgumentException("Nama file atau folder di tingkat utama tidak boleh sama.");
        }
        dynamic? recorder = null, writer = null, image = null, root = null, result = null;
        var openStreams = new List<IStream>();
        try
        {
            progress?.Report(new("Memeriksa disc", null)); recorder = Recorder(driveId); writer = DataWriter(recorder); RequireBlank(writer, recorder);
            image = NewCom("IMAPI2FS.MsftFileSystemImage"); image.ChooseImageDefaults(recorder); image.FileSystemsToCreate = 7; image.VolumeName = volumeName; root = image.Root;
            foreach (var path in paths)
            {
                string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
                if (Directory.Exists(path)) { root.AddDirectory(name); dynamic dir = root[name]; try { dir.AddTree(path, false); } finally { Release(dir); } }
                else { var stream = OpenStream(path); openStreams.Add(stream); root.AddFile(name, stream); }
            }
            progress?.Report(new("Menyiapkan image disc", null)); result = image.CreateResultImage();
            long blocks = result.TotalBlocks; long free = writer.FreeSectorsOnMedia;
            if (blocks > free) throw new InvalidOperationException("Ukuran proyek melebihi kapasitas disc yang tersedia.");
            Verification(writer, verify); IStream streamImage = result.ImageStream;
            try { WriteData(writer, streamImage, progress); } finally { Release(streamImage); }
            if (eject) recorder.EjectMedia();
            progress?.Report(new("Selesai", 100));
        }
        finally { Release(result); Release(root); Release(image); foreach (var stream in openStreams) Release(stream); Release(writer); Release(recorder); }
    }

    public void BurnIso(string driveId, string isoPath, bool verify, bool eject, IProgress<BurnProgress>? progress = null)
    {
        var file = new FileInfo(isoPath);
        if (!file.Exists) throw new FileNotFoundException("File ISO tidak ditemukan.", isoPath);
        if (!BurnValidation.ValidIsoLength(file.Length)) throw new InvalidDataException("Ukuran file ISO harus berisi sektor 2048 byte yang lengkap.");
        dynamic? recorder = null, writer = null; IStream? stream = null;
        try
        {
            progress?.Report(new("Memeriksa disc", null)); recorder = Recorder(driveId); writer = DataWriter(recorder); RequireBlank(writer, recorder);
            long free = writer.FreeSectorsOnMedia;
            if (file.Length / 2048 > free) throw new InvalidOperationException("File ISO melebihi kapasitas disc yang tersedia.");
            Verification(writer, verify); stream = OpenStream(isoPath); WriteData(writer, stream, progress);
            if (eject) recorder.EjectMedia(); progress?.Report(new("Selesai", 100));
        }
        finally { Release(stream); Release(writer); Release(recorder); }
    }

    public void BurnAudio(string driveId, IReadOnlyList<string> paths, bool verify, bool eject, IProgress<BurnProgress>? progress = null)
    {
        if (paths.Count is 0 or > 99) throw new ArgumentException("Audio CD harus berisi 1 sampai 99 lagu.");
        var durations = paths.Select(GetAudioDuration).ToArray();
        dynamic? recorder = null, data = null, writer = null; string? temp = null; bool prepared = false;
        try
        {
            progress?.Report(new("Memeriksa CD", null)); recorder = Recorder(driveId); data = DataWriter(recorder); RequireBlank(data, recorder);
            int type = data.CurrentPhysicalMediaType;
            if (type is not (2 or 3)) throw new InvalidOperationException("Audio CD memerlukan CD-R atau CD-RW kosong.");
            int freeSectors = data.FreeSectorsOnMedia;
            int seconds = freeSectors / 75;
            if (!BurnValidation.AudioFits(durations, seconds)) throw new InvalidOperationException("Durasi lagu dan jeda melebihi kapasitas CD.");
            temp = Path.Combine(Path.GetTempPath(), "HYBurningStudio-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(temp);
            var tracks = new List<string>();
            for (int i = 0; i < paths.Count; i++)
            {
                progress?.Report(new($"Mengonversi lagu {i + 1} dari {paths.Count}", null));
                var raw = Path.Combine(temp, $"{i:D3}.pcm"); ConvertTrack(paths[i], raw); tracks.Add(raw);
            }
            if (!BurnValidation.AudioSectorsFit(tracks.Select(p => new FileInfo(p).Length).ToArray(), freeSectors))
                throw new InvalidOperationException("Ukuran audio yang dikonversi dan jeda melebihi kapasitas CD.");
            writer = NewCom("IMAPI2.MsftDiscFormat2TrackAtOnce"); writer.Recorder = recorder; writer.ClientName = "HY Burning Studio";
            if (!(bool)writer.IsCurrentMediaSupported(recorder)) throw new InvalidOperationException("CD ini tidak didukung untuk Audio CD.");
            Verification(writer, verify, true); writer.PrepareMedia(); prepared = true;
            for (int i = 0; i < tracks.Count; i++)
            {
                progress?.Report(new($"Menulis lagu {i + 1} dari {tracks.Count}", (double)i * 100 / tracks.Count));
                var stream = OpenStream(tracks[i]); try { writer.AddAudioTrack(stream); } finally { Release(stream); }
            }
            progress?.Report(new("Menyelesaikan Audio CD", null)); writer.ReleaseMedia(); prepared = false;
            if (eject) recorder.EjectMedia(); progress?.Report(new("Selesai", 100));
        }
        finally
        {
            if (prepared && writer != null) { try { ((dynamic)writer!).ReleaseMedia(); } catch { } }
            Release(writer); Release(data); Release(recorder);
            if (temp != null) { try { Directory.Delete(temp, true); } catch (IOException) { } }
        }
    }

    public void Erase(string driveId, bool fullErase, IProgress<BurnProgress>? progress = null)
    {
        dynamic? recorder = null, eraser = null;
        try
        {
            recorder = Recorder(driveId); eraser = NewCom("IMAPI2.MsftDiscFormat2Erase"); eraser.Recorder = recorder; eraser.ClientName = "HY Burning Studio";
            if (!(bool)eraser.IsCurrentMediaSupported(recorder)) throw new InvalidOperationException("Disc tidak dapat dihapus oleh drive ini.");
            eraser.FullErase = fullErase; progress?.Report(new("Menghapus disc", null)); eraser.EraseMedia(); progress?.Report(new("Disc berhasil dihapus", 100));
        }
        finally { Release(eraser); Release(recorder); }
    }

    private static void RequireBlank(dynamic writer, dynamic recorder)
    {
        if (!(bool)writer.IsCurrentMediaSupported(recorder)) throw new InvalidOperationException("Disc tidak tersedia atau tidak didukung.");
        int type = writer.CurrentPhysicalMediaType;
        if (!IsSupportedDisc(type)) throw new InvalidOperationException("Gunakan CD atau DVD yang dapat ditulis.");
        if (!(bool)writer.MediaHeuristicallyBlank) throw new InvalidOperationException("Gunakan disc kosong. Hapus CD-RW atau DVD-RW terlebih dahulu jika diperlukan.");
    }

    private static bool IsSupportedDisc(int type) => type is 2 or 3 or 5 or 6 or 7 or 8 or 9 or 10 or 11;
    private static string MediaName(int type) => type switch { 2 => "CD-R", 3 => "CD-RW", 5 => "DVD-RAM", 6 => "DVD+R", 7 => "DVD+RW", 8 => "DVD+R DL", 9 => "DVD-R", 10 => "DVD-RW", 11 => "DVD-R DL", _ => "Disc tidak didukung" };
    private static void CheckAudioPath(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("File audio tidak ditemukan.", path);
        if (!new[] { ".mp3", ".wav", ".flac" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase)) throw new ArgumentException("Format audio harus MP3, WAV, atau FLAC.");
    }
    private static void ConvertTrack(string input, string output)
    {
        using var reader = new MediaFoundationReader(input);
        using var resampler = new MediaFoundationResampler(reader, new WaveFormat(44100, 16, 2));
        using var file = File.Create(output); var buffer = new byte[65536]; int read;
        while ((read = resampler.Read(buffer, 0, buffer.Length)) > 0) file.Write(buffer, 0, read);
        if (file.Length == 0) throw new InvalidDataException("File audio kosong atau tidak dapat dikonversi.");
        long target = Math.Max(44100L * 4 * 4, ((file.Length + 2351) / 2352) * 2352);
        if (target > file.Length) { file.Position = target - 1; file.WriteByte(0); }
    }
    private delegate void UpdateEvent(object sender, object args);
    private static void WriteData(dynamic writer, IStream stream, IProgress<BurnProgress>? progress)
    {
        var eventId = new Guid("2735413C-7F64-5B0F-8F00-5D77AFBE261E");
        UpdateEvent handler = (_, args) =>
        {
            try
            {
                dynamic p = args; int action = p.CurrentAction; int count = p.SectorCount; int start = p.StartLba; int last = p.LastWrittenLba;
                string status = action switch { 0 => "Memeriksa disc", 1 => "Menyiapkan disc", 2 => "Menyiapkan drive", 3 => "Kalibrasi", 4 => "Menulis disc", 5 => "Menyelesaikan disc", 6 => "Selesai", 7 => "Memverifikasi disc", _ => "Memproses disc" };
                double? percent = action == 4 && count > 0 ? Math.Clamp((last - start + 1d) * 100 / count, 0, 100) : null;
                progress?.Report(new(status, percent));
            }
            catch { /* A progress callback must never interrupt a write. */ }
        };
        bool attached = false;
        try
        {
            try { ComEventsHelper.Combine((object)writer, eventId, 0x200, handler); attached = true; } catch (COMException) { }
            progress?.Report(new("Menulis disc", null)); writer.Write(stream);
        }
        finally { if (attached) ComEventsHelper.Remove((object)writer, eventId, 0x200, handler); }
    }
}

using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace HYBurningStudio.Services;

public static class IsoCreationService
{
    private const int SectorSize = 2048;
    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
    private static extern void SHCreateStreamOnFileEx(string fileName, uint mode, uint attributes, bool create, IStream? template, out IStream stream);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(SafeFileHandle device, uint code, byte[]? input, int inputLength, byte[] output, int outputLength, out int returned, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile(SafeFileHandle device, IntPtr buffer, int count, out int read, IntPtr overlapped);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint allocationType, uint protect);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint freeType);

    private static dynamic NewImage() => Activator.CreateInstance(Type.GetTypeFromProgID("IMAPI2FS.MsftFileSystemImage", true)!)!;
    private static void Release(object? value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }

    public static void CreateFromFiles(string[] paths, string volume, string destination, bool overwrite, Action<string, double?>? progress)
    {
        if (paths.Length == 0) throw new ArgumentException("Tambahkan file atau folder terlebih dahulu.");
        if (string.IsNullOrWhiteSpace(volume)) throw new ArgumentException("Nama ISO tidak boleh kosong.");
        string output = Path.GetFullPath(destination);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string input in paths)
        {
            string path = Path.GetFullPath(input);
            if (!File.Exists(path) && !Directory.Exists(path)) throw new FileNotFoundException("Sumber tidak ditemukan.", path);
            if (!names.Add(Path.GetFileName(Path.TrimEndingDirectorySeparator(path)))) throw new IOException("Nama file atau folder sumber pada tingkat utama tidak boleh sama.");
            if (string.Equals(path, output, StringComparison.OrdinalIgnoreCase) || (Directory.Exists(path) && output.StartsWith(Path.TrimEndingDirectorySeparator(path) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                throw new IOException("Simpan ISO di luar file dan folder sumber agar sumber tidak tertimpa.");
        }
        dynamic? image = null, root = null, result = null;
        var streams = new List<IStream>();
        try
        {
            progress?.Invoke("Menyiapkan isi ISO", null);
            image = NewImage(); image.FileSystemsToCreate = 7; image.VolumeName = volume; root = image.Root;
            foreach (string path in paths)
            {
                string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
                if (Directory.Exists(path)) { root.AddDirectory(name); dynamic child = root[name]; try { child.AddTree(path, false); } finally { Release(child); } }
                else { SHCreateStreamOnFileEx(path, 0x20, 0, false, null, out var stream); streams.Add(stream); root.AddFile(name, stream); }
            }
            result = image.CreateResultImage(); long size = (long)result.TotalBlocks * SectorSize;
            IStream source = result.ImageStream;
            try { WriteAtomic(output, overwrite, stream => CopyComStream(source, stream, size, progress)); }
            finally { Release(source); }
        }
        finally { Release(result); Release(root); Release(image); foreach (var stream in streams) Release(stream); }
    }

    public static void CreateFromDisc(string drive, string destination, bool overwrite, Action<string, double?>? progress)
    {
        if (drive.Length != 2 || !char.IsAsciiLetter(drive[0]) || drive[1] != ':') throw new ArgumentException("Pilih drive CD/DVD yang memiliki huruf drive.");
        string output = Path.GetFullPath(destination);
        if (output.StartsWith(drive + "\\", StringComparison.OrdinalIgnoreCase)) throw new IOException("Simpan ISO di drive lain, bukan pada disc sumber.");
        using var source = File.OpenHandle(@"\\.\" + drive, FileMode.Open, FileAccess.Read, FileShare.Read);
        byte[] toc = new byte[804], session = new byte[12];
        byte[] query = new byte[4];
        // Prevent removal for the duration of the copy. If the drive cannot guarantee this,
        // fail closed instead of silently producing an image from a changed disc.
        if (!DeviceIoControl(source, 0x2D4804, [1], 1, [], 0, out _, IntPtr.Zero))
            throw new NotSupportedException("Drive tidak dapat mengunci disc selama pembacaan.", new Win32Exception(Marshal.GetLastWin32Error()));
        try
        {
        if (!DeviceIoControl(source, 0x24054, query, query.Length, toc, toc.Length, out int tocRead, IntPtr.Zero) || tocRead < 20)
            throw new NotSupportedException("Drive tidak dapat memastikan susunan track; disc tidak dapat disalin dengan aman.", new Win32Exception(Marshal.GetLastWin32Error()));
        if (!DeviceIoControl(source, 0x24038, null, 0, session, session.Length, out int sessionRead, IntPtr.Zero) || sessionRead < 12)
            throw new NotSupportedException("Drive tidak dapat memastikan jumlah sesi; disc tidak dapat disalin dengan aman.", new Win32Exception(Marshal.GetLastWin32Error()));
        long bytes = checked(ValidateDiscLayout(toc, session) * SectorSize);
        if (bytes < 32 * SectorSize) throw new InvalidDataException("Disc terlalu kecil untuk image data yang didukung.");
        // Volume reads may be noncached even without FILE_FLAG_NO_BUFFERING. VirtualAlloc
        // returns page-aligned memory and all reads are multiples of the 2048-byte sector.
        _ = DeviceIoControl(source, 0x90083, null, 0, [], 0, out _, IntPtr.Zero);
        IntPtr buffer = VirtualAlloc(IntPtr.Zero, (UIntPtr)65536, 0x3000, 0x04);
        if (buffer == IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
        byte[] probe = new byte[32 * SectorSize]; ReadDisc(source, buffer, probe, probe.Length);
        bool recognized = false;
        for (int i = 16; i < 32; i++)
        {
            string marker = Encoding.ASCII.GetString(probe, i * SectorSize + 1, 5);
            if (marker is "CD001" or "NSR02" or "NSR03") { recognized = true; break; }
        }
        if (!recognized) throw new NotSupportedException("Disc bukan ISO 9660 atau UDF yang dikenali; Audio CD dan format khusus tidak didukung.");
        WriteAtomic(output, overwrite, stream =>
        {
            stream.Write(probe); long done = probe.Length; var chunk = new byte[65536];
            while (done < bytes)
            {
                int request = (int)Math.Min(chunk.Length, bytes - done);
                ReadDisc(source, buffer, chunk, request); stream.Write(chunk, 0, request);
                done += request; progress?.Invoke("Membaca disc ke ISO", done * 100d / bytes);
            }
        });
        }
        finally { VirtualFree(buffer, UIntPtr.Zero, 0x8000); }
        }
        finally { _ = DeviceIoControl(source, 0x2D4804, [0], 1, [], 0, out _, IntPtr.Zero); }
    }

    public static long ValidateDiscLayout(byte[] toc, byte[] session)
    {
        if (toc.Length < 20 || session.Length < 12) throw new NotSupportedException("Informasi disc tidak lengkap.");
        if (session[2] == 0 || session[2] != session[3]) throw new NotSupportedException("Disc multisession tidak didukung untuk penyalinan ISO.");
        if (toc[2] != 1 || toc[3] != 1 || toc[6] != 1 || toc[14] != 0xAA || (toc[5] & 4) == 0) throw new NotSupportedException("Hanya disc satu track data yang didukung; Audio CD dan disc campuran tidak didukung.");
        long start = ReadBigEndian(toc, 8), end = ReadBigEndian(toc, 16);
        if (start != 0 || end <= start) throw new NotSupportedException("Susunan sektor disc tidak didukung.");
        return end;
    }
    private static long ReadBigEndian(byte[] b, int i) => ((long)b[i] << 24) | ((long)b[i + 1] << 16) | ((long)b[i + 2] << 8) | b[i + 3];

    private static void WriteAtomic(string output, bool overwrite, Action<FileStream> write)
    {
        string full = Path.GetFullPath(output); string dir = Path.GetDirectoryName(full)!;
        if (!overwrite && File.Exists(full)) throw new IOException("File tujuan sudah ada.");
        string temp = Path.Combine(dir, ".hy-iso-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var target = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) write(target);
            File.Move(temp, full, overwrite);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private static void CopyComStream(IStream source, Stream destination, long size, Action<string, double?>? progress)
    {
        var buffer = new byte[65536]; IntPtr count = Marshal.AllocCoTaskMem(sizeof(int)); long done = 0;
        try
        {
            while (done < size)
            {
                int request = (int)Math.Min(buffer.Length, size - done); source.Read(buffer, request, count); int n = Marshal.ReadInt32(count);
                if (n <= 0 || n > request) throw new IOException("Image ISO tidak dapat dibaca sampai selesai.");
                destination.Write(buffer, 0, n); done += n; progress?.Invoke("Membuat file ISO", done * 100d / size);
            }
        }
        finally { Marshal.FreeCoTaskMem(count); }
    }
    private static void ReadDisc(SafeFileHandle source, IntPtr alignedBuffer, byte[] target, int request)
    {
        if (!ReadFile(source, alignedBuffer, request, out int n, IntPtr.Zero)) throw new IOException("Disc tidak dapat dibaca. Periksa kondisi disc dan drive.", new Win32Exception(Marshal.GetLastWin32Error()));
        if (n != request) throw new IOException("Disc tidak dapat dibaca sampai selesai. Periksa kondisi disc dan drive.");
        Marshal.Copy(alignedBuffer, target, 0, request);
    }
}

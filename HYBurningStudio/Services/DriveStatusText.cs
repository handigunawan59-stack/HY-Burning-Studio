using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace HYBurningStudio.Services;

public static class DriveStatusText
{
    public const string UnknownMedia = "Tidak ada disc atau disc tidak didukung";

    public static string Describe(string? name, string? media, bool blank, long freeBytes)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Drive CD/DVD tidak ditemukan. Hubungkan drive, lalu pilih Perbarui drive.";
        if (string.IsNullOrWhiteSpace(media) || media == UnknownMedia)
            return $"{name} · {UnknownMedia}. Masukkan disc yang sesuai dan periksa kembali.";
        if (!blank) return $"{name} · {media} · Disc tidak terdeteksi kosong. Periksa disc; hapus terlebih dahulu jika media dapat ditulis ulang.";
        string capacity = freeBytes > 0 ? $" · Ruang tersedia {FormatBytes(freeBytes)}" : " · Kapasitas belum tersedia";
        return $"{name} · {media} kosong{capacity}";
    }

    private static string FormatBytes(long bytes)
    {
        var culture = CultureInfo.GetCultureInfo("id-ID");
        return bytes >= 1L << 30 ? $"{(bytes / (double)(1L << 30)).ToString("0.##", culture)} GB"
            : $"{(bytes / (double)(1L << 20)).ToString("0.##", culture)} MB";
    }

    public static string ExplainError(Exception exception)
    {
        if (exception is FileNotFoundException or DirectoryNotFoundException)
            return "File atau folder tidak ditemukan. Periksa lokasi sumber, lalu pilih kembali.\n\nDetail: " + exception.Message;
        if (exception is UnauthorizedAccessException)
            return "Akses ke file atau folder ditolak. Periksa izin dan coba lagi.\n\nDetail: " + exception.Message;
        if (exception is COMException com)
        {
            string hint = unchecked((uint)com.HResult) switch
            {
                0xC0AA0404 => "Ukuran proyek melebihi ruang yang tersedia. Gunakan disc berkapasitas lebih besar atau kurangi file.",
                0xC0AA0405 => "Disc tidak kosong. Gunakan disc kosong atau hapus media tulis ulang terlebih dahulu.",
                0xC0AA0406 => "Disc tidak didukung atau tidak siap. Periksa jenis dan kondisi disc, lalu perbarui drive.",
                0xC0AA0407 => "Drive tidak mendukung operasi ini. Coba drive CD/DVD yang dapat menulis media tersebut.",
                0xC0AA0007 => "Verifikasi gagal. Hasil disc mungkin tidak dapat digunakan; coba lagi dengan disc baru.",
                _ => "Operasi drive gagal. Periksa sambungan drive, jenis dan kondisi disc, lalu coba lagi."
            };
            return $"{hint}\n\nKode: 0x{unchecked((uint)com.HResult):X8}\nDetail: {com.Message}";
        }
        return $"Operasi tidak dapat diselesaikan. Periksa file, drive, dan disc, lalu coba lagi.\n\nDetail: {exception.Message}";
    }
}

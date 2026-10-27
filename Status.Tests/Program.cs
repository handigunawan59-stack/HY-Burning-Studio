using System.Reflection;
using System.Runtime.InteropServices;

var type = Assembly.GetExecutingAssembly().GetType("HYBurningStudio.Services.DriveStatusText")
    ?? throw new Exception("Status drive yang informatif belum tersedia.");
string Display(string? name, string? media, bool blank, long bytes) =>
    (string)type.GetMethod("Describe")!.Invoke(null, [name, media, blank, bytes])!;
string Error(Exception ex) => (string)type.GetMethod("ExplainError")!.Invoke(null, [ex])!;
void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
Check(Display(null, null, false, 0).Contains("Hubungkan"), "Drive yang tidak ada harus memiliki langkah tindak lanjut.");
Check(Display("DVD D:", "CD-R", true, 700L * 1024 * 1024).Contains("kosong"), "Disc kosong harus ditandai.");
Check(Display("DVD D:", "CD-R", true, 700L * 1024 * 1024).Contains("700 MB"), "Kapasitas harus ditampilkan.");
Check(Display("DVD D:", "CD-RW", false, 0).Contains("tidak terdeteksi kosong"), "Disc yang tidak blank tidak boleh dianggap kosong.");
Check(Display("DVD D:", "Tidak ada disc atau disc tidak didukung", false, 0).Contains("periksa"), "Media yang tidak diketahui harus menyarankan pemeriksaan.");
Check(Error(new FileNotFoundException()).Contains("tidak ditemukan"), "Pesan file hilang harus membantu.");
Check(Error(new COMException("gagal", unchecked((int)0xC0AA0406))).Contains("0xC0AA0406"), "Kode IMAPI harus dipertahankan untuk diagnosis.");
Console.WriteLine("Drive status and error guidance tests passed.");

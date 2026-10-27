# HY Burning Studio

Aplikasi desktop Windows berbahasa Indonesia untuk menyiapkan dan membakar CD atau DVD. Antarmuka dibuat dengan WPF dan proses burning menggunakan Windows Image Mastering API (IMAPI2). Aplikasi berjalan tanpa koneksi internet setelah dipasang.

## Fitur

- Data Disc untuk file dan folder, Audio CD dari MP3, WAV, dan FLAC, serta pembakaran file ISO.
- Deteksi drive dan media, pemeriksaan kapasitas, peninjauan sebelum burning, progres, dan penghapusan media CD-RW/DVD-RW yang didukung drive.
- Verifikasi penuh untuk Data Disc dan ISO; Audio CD menggunakan verifikasi cepat IMAPI yang memeriksa struktur disc, bukan perbandingan penuh isi audio.
- Satu jendela aplikasi per sesi Windows. Pembukaan berikutnya mengaktifkan jendela yang sudah ada.
- Installer offline untuk Windows 10 dan 11 **64-bit**. Runtime .NET disertakan.

## Status pengujian

Build dan uji logika dijalankan di Windows. Pembacaan dan konversi contoh MP3, WAV, dan FLAC diuji melalui Windows Media Foundation. Drive CD/DVD fisik tidak tersedia di komputer pengembangan, sehingga burning, verifikasi hasil, dan penghapusan pada disc fisik **belum diuji**. Periksa dengan media uji sebelum mengandalkannya untuk data penting.

Audio CD memerlukan CD-R atau CD-RW kosong. Konversi audio menggunakan Windows Media Foundation; dukungan codec dapat bergantung pada edisi dan konfigurasi Windows. Windows N mungkin memerlukan Media Feature Pack. Aplikasi saat ini menggunakan kecepatan burning otomatis; tema gelap, pratinjau audio, dan penyimpanan proyek belum tersedia.

## Instalasi

Unduh installer `HY-Burning-Studio-Setup-x64.exe` dari halaman Releases, lalu jalankan di Windows 10/11 64-bit. Drive yang mendukung penulisan CD/DVD diperlukan untuk burning. Installer tidak memerlukan akses internet ketika dijalankan.

## Membuat dari source

Persyaratan: Windows 64-bit, .NET SDK 8, dan NSIS 3. Restore paket pada mesin pembuat membutuhkan akses ke sumber paket NuGet yang sesuai.

```powershell
dotnet build ./HYBurningStudio/HYBurningStudio.csproj -c Release
dotnet run --project ./HYBurningStudio.Tests
dotnet run --project ./BurnEngine.Tests
dotnet run --project ./SingleInstance.Tests
./installer/build-installer.ps1 -OutputDirectory ./dist
```

## Komponen pihak ketiga

Aplikasi menggunakan NAudio 2.2.1 berlisensi MIT. Salinan lisensinya disertakan sebagai `NAudio-LICENSE.txt` dalam aplikasi dan installer.

# Membuat installer

Di Windows x64, instal .NET SDK yang sesuai dengan proyek dan NSIS 3. Jalankan:

```powershell
./installer/build-installer.ps1 -OutputDirectory ./dist
```

Skrip mem-publish aplikasi untuk `win-x64` dengan runtime .NET disertakan, lalu menghasilkan `HY-Burning-Studio-Setup-x64.exe`. Restore paket pada komputer pembuat mungkin membutuhkan internet; installer yang dihasilkan tidak mengunduh komponen saat dipasang. Installer dipasang untuk pengguna saat ini dan menyediakan penghapusan melalui Pengaturan Windows.

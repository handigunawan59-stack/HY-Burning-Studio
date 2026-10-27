using System.IO;
using System.Windows;
using HYBurningStudio.Models;
using Microsoft.Win32;

namespace HYBurningStudio;

public partial class MainWindow
{
    private void SaveProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Title = "Simpan proyek", Filter = "Proyek HY Burning Studio (*.hyburn)|*.hyburn", DefaultExt = ".hyburn", AddExtension = true, FileName = "Proyek HY" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var paths = currentKind == ProjectKind.Iso ? (isoPath is null ? new List<string>() : [isoPath]) : CurrentFiles.Items.ToList();
            new SavedProject { Kind = currentKind.ToString(), VolumeName = VolumeName.Text, Paths = paths }.Save(dialog.FileName);
            MessageBox.Show(this, "Proyek disimpan. File proyek berisi lokasi file sumber, bukan salinan file. Jangan pindahkan atau hapus file sumber.", "Proyek disimpan", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { MessageBox.Show(this, $"Proyek tidak dapat disimpan.\n\n{ex.Message}", "Gagal menyimpan", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private void OpenSavedProject_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Buka proyek", Filter = "Proyek HY Burning Studio (*.hyburn)|*.hyburn", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var project = SavedProject.Load(dialog.FileName);
            var kind = Enum.Parse<ProjectKind>(project.Kind);
            // Validate and decode the complete project before changing the current workspace.
            var staged = new ProjectFiles(kind == ProjectKind.Audio);
            var stagedDurations = new Dictionary<string, TimeSpan>(StringComparer.OrdinalIgnoreCase);
            var stagedIsoPath = kind == ProjectKind.Iso ? project.Paths.FirstOrDefault() : null;
            var stagedIsoHint = stagedIsoPath is null ? "Seret file ke sini atau pilih dari komputer" : FormatBytes(new FileInfo(stagedIsoPath).Length);
            if (kind != ProjectKind.Iso)
            {
                foreach (string path in project.Paths)
                {
                    if (!staged.Add(path)) throw new IOException($"File atau folder tidak dapat dibuka: {path}");
                    if (kind == ProjectKind.Audio) stagedDurations[Path.GetFullPath(path)] = service.GetAudioDuration(path);
                }
            }
            bool hasExisting = kind == ProjectKind.Data ? dataFiles.Items.Count > 0 : kind == ProjectKind.Audio ? audioFiles.Items.Count > 0 : isoPath != null;
            if (hasExisting && MessageBox.Show(this, "Proyek yang dibuka akan mengganti daftar pada jenis proyek ini. Simpan perubahan terlebih dahulu jika diperlukan. Lanjutkan?", "Buka proyek", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            if (kind == ProjectKind.Iso)
            {
                isoPath = stagedIsoPath;
                IsoName.Text = isoPath is null ? "Pilih file ISO" : Path.GetFileName(isoPath);
                IsoHint.Text = stagedIsoHint;
                Iso_Click(sender, e);
            }
            else
            {
                if (kind == ProjectKind.Data) VolumeName.Text = project.VolumeName;
                var target = kind == ProjectKind.Audio ? audioFiles : dataFiles;
                target.Items.Clear(); target.Items.AddRange(staged.Items);
                if (kind == ProjectKind.Audio)
                {
                    durations.Clear();
                    foreach (var entry in stagedDurations) durations.Add(entry.Key, entry.Value);
                }
                OpenProject(kind);
            }
        }
        catch (Exception ex) { MessageBox.Show(this, $"Proyek tidak dapat dibuka; proyek yang sedang ada tetap dipertahankan. Pastikan semua file sumber masih tersedia.\n\n{ex.Message}", "Gagal membuka proyek", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}

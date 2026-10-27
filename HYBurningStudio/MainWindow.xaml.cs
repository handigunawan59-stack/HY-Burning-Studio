using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using HYBurningStudio.Models;
using HYBurningStudio.Services;
using OpticalDrive = HYBurningStudio.Services.DriveInfo;

namespace HYBurningStudio;

public partial class MainWindow : Window
{
    private enum ProjectKind { Data, Audio, Iso }
    private readonly BurnService service = new();
    private readonly ProjectFiles dataFiles = new(false);
    private readonly ProjectFiles audioFiles = new(true);
    private readonly Dictionary<string, TimeSpan> durations = new(StringComparer.OrdinalIgnoreCase);
    private readonly ObservableCollection<ItemRow> rows = [];
    private List<OpticalDrive> drives = [];
    private string? selectedDriveId;
    private string? isoPath;
    private ProjectKind currentKind = ProjectKind.Data;
    private bool busy;
    private sealed record ItemRow(string Path, string Name, string Kind, string Detail);

    public MainWindow()
    {
        InitializeComponent();
        ItemsList.ItemsSource = rows;
        Closing += (_, e) => { if (busy) { e.Cancel = true; MessageBox.Show(this, "Tunggu sampai proses selesai sebelum menutup aplikasi.", "HY Burning Studio", MessageBoxButton.OK, MessageBoxImage.Information); } };
        Loaded += (_, _) => RefreshDrives();
        ShowPage(HomePage, HomeNav, "BERANDA  /  MULAI");
    }

    private void ShowPage(UIElement page, Button nav, string title)
    {
        if (busy && page != ProgressPage) return;
        foreach (var p in new UIElement[] { HomePage, ProjectPage, IsoPage, ReviewPage, ProgressPage, SettingsPage }) p.Visibility = p == page ? Visibility.Visible : Visibility.Collapsed;
        foreach (var n in new[] { HomeNav, DataNav, AudioNav, IsoNav, SettingsNav })
        {
            n.Background = (Brush)new BrushConverter().ConvertFromString(n == nav ? "#DDE8FF" : "Transparent")!;
            n.Foreground = (Brush)new BrushConverter().ConvertFromString(n == nav ? "#1E5FE2" : "#64748B")!;
        }
        Breadcrumb.Text = title;
        SaveProjectButton.Visibility = page == ProjectPage || page == IsoPage ? Visibility.Visible : Visibility.Collapsed;
    }
    private void Home_Click(object sender, RoutedEventArgs e) => ShowPage(HomePage, HomeNav, "BERANDA  /  MULAI");
    private void Data_Click(object sender, RoutedEventArgs e) => OpenProject(ProjectKind.Data);
    private void Audio_Click(object sender, RoutedEventArgs e) => OpenProject(ProjectKind.Audio);
    private void Iso_Click(object sender, RoutedEventArgs e) { currentKind = ProjectKind.Iso; ShowPage(IsoPage, IsoNav, "PROYEK  /  BURN ISO"); }
    private void Settings_Click(object sender, RoutedEventArgs e) => ShowPage(SettingsPage, SettingsNav, "APLIKASI  /  PENGATURAN");
    private void OpenProject(ProjectKind kind)
    {
        currentKind = kind;
        bool audio = kind == ProjectKind.Audio;
        ProjectTitle.Text = audio ? "Audio CD" : "Data Disc";
        ProjectHint.Text = audio ? "Susun lagu untuk CD audio. Durasi belum termasuk jeda antar lagu." : "Kumpulkan file dan folder untuk disc Anda.";
        AddFileButton.Content = audio ? "+ Tambah lagu" : "+ Tambah file";
        AddFolderButton.Visibility = audio ? Visibility.Collapsed : Visibility.Visible;
        VolumePanel.Visibility = audio ? Visibility.Hidden : Visibility.Visible;
        MoveUpButton.Visibility = MoveDownButton.Visibility = audio ? Visibility.Visible : Visibility.Collapsed;
        ShowPage(ProjectPage, audio ? AudioNav : DataNav, audio ? "PROYEK  /  AUDIO CD" : "PROYEK  /  DATA DISC");
        UpdateRows();
    }
    private ProjectFiles CurrentFiles => currentKind == ProjectKind.Audio ? audioFiles : dataFiles;
    private void RefreshDrives_Click(object sender, RoutedEventArgs e) => RefreshDrives();
    private void RefreshDrives()
    {
        try
        {
            drives = service.GetDrives().ToList();
            selectedDriveId = drives.FirstOrDefault(d => d.Id == selectedDriveId)?.Id ?? drives.FirstOrDefault()?.Id;
            foreach (var picker in new[] { DrivePicker, ReviewDrivePicker, EraseDrivePicker }) { picker.ItemsSource = drives; picker.SelectedItem = drives.FirstOrDefault(d => d.Id == selectedDriveId); }
            var drive = drives.FirstOrDefault(d => d.Id == selectedDriveId);
            UpdateDriveStatus(drive);
            UpdateCapacity();
        }
        catch (Exception ex) { drives.Clear(); selectedDriveId = null; DriveStatus.Text = "Drive tidak dapat diperiksa"; HomeDrive.Text = "Periksa sambungan drive, lalu pilih Perbarui drive."; UpdateCapacity(); ShowError(ex); }
    }
    private void UpdateDriveStatus(OpticalDrive? drive)
    {
        DriveStatus.Text = drive == null ? "Tidak ada drive CD/DVD" : $"{drive.Name}\n{drive.Media}";
        HomeDrive.TextWrapping = TextWrapping.Wrap;
        HomeDrive.Text = DriveStatusText.Describe(drive?.Name, drive?.Media, drive?.IsBlank ?? false, drive?.FreeBytes ?? 0);
    }
    private void DrivePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DrivePicker.SelectedItem is OpticalDrive drive) { selectedDriveId = drive.Id; UpdateDriveStatus(drive); UpdateCapacity(); }
    }
    private void ReviewDrivePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ReviewDrivePicker.SelectedItem is OpticalDrive drive) { selectedDriveId = drive.Id; UpdateDriveStatus(drive); ReviewMedia.TextWrapping = TextWrapping.Wrap; ReviewMedia.Text = DriveStatusText.Describe(drive.Name, drive.Media, drive.IsBlank, drive.FreeBytes); }
    }
    private void AddFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Multiselect = true, Filter = currentKind == ProjectKind.Audio ? "File audio (*.mp3;*.wav;*.flac)|*.mp3;*.wav;*.flac" : "Semua file (*.*)|*.*" };
        if (dialog.ShowDialog(this) == true) AddPaths(dialog.FileNames);
    }
    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Pilih folder" };
        if (dialog.ShowDialog(this) == true) AddPaths([dialog.FolderName]);
    }
    private void ItemsList_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths) AddPaths(paths);
    }
    private void AddPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            try
            {
                if (currentKind == ProjectKind.Audio)
                {
                    if (!audioFiles.Add(path)) continue;
                    try { durations[Path.GetFullPath(path)] = service.GetAudioDuration(path); }
                    catch { audioFiles.Remove(Path.GetFullPath(path)); throw; }
                }
                else dataFiles.Add(path);
            }
            catch (Exception ex) { MessageBox.Show(this, $"Tidak dapat menambahkan {Path.GetFileName(path)}.\n\n{DriveStatusText.ExplainError(ex)}", "File tidak dapat dibaca", MessageBoxButton.OK, MessageBoxImage.Warning); }
        }
        UpdateRows();
    }
    private void RemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (ItemsList.SelectedItem is not ItemRow row) return;
        CurrentFiles.Remove(row.Path); durations.Remove(row.Path); UpdateRows();
    }
    private void MoveUp_Click(object sender, RoutedEventArgs e) => Move(-1);
    private void MoveDown_Click(object sender, RoutedEventArgs e) => Move(1);
    private void Move(int delta)
    {
        if (ItemsList.SelectedItem is not ItemRow row) return;
        var items = CurrentFiles.Items; int index = items.IndexOf(row.Path), next = index + delta;
        if (index < 0 || next < 0 || next >= items.Count) return;
        (items[index], items[next]) = (items[next], items[index]); UpdateRows(); ItemsList.SelectedIndex = next;
    }
    private void UpdateRows()
    {
        rows.Clear();
        foreach (string path in CurrentFiles.Items)
        {
            try
            {
                bool folder = Directory.Exists(path);
                string detail = currentKind == ProjectKind.Audio && durations.TryGetValue(path, out var duration) ? FormatTime(duration) : FormatBytes(ProjectFiles.SizeOf(path));
                rows.Add(new(path, Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)), folder ? "Folder" : Path.GetExtension(path).TrimStart('.').ToUpperInvariant(), detail));
            }
            catch (Exception ex) { rows.Add(new(path, Path.GetFileName(path), "Tidak dapat dibaca", ex.Message)); }
        }
        UpdateCapacity();
    }
    private void UpdateCapacity()
    {
        if (CapacityText == null) return;
        var drive = drives.FirstOrDefault(d => d.Id == selectedDriveId);
        try
        {
            double used, total;
            if (currentKind == ProjectKind.Audio)
            {
                used = audioFiles.Items.Sum(p => durations.GetValueOrDefault(p).TotalSeconds);
                total = drive?.AudioCapacitySeconds ?? 0;
                CapacityText.Text = total > 0 ? $"{FormatTime(TimeSpan.FromSeconds(used))} dari {FormatTime(TimeSpan.FromSeconds(total))} menit" : $"{FormatTime(TimeSpan.FromSeconds(used))} · masukkan CD kosong";
            }
            else
            {
                used = dataFiles.TotalBytes; total = drive?.FreeBytes ?? 0;
                CapacityText.Text = total > 0 ? $"{FormatBytes((long)used)} dari {FormatBytes((long)total)}" : $"{FormatBytes((long)used)} · masukkan disc kosong";
            }
            CapacityBar.Value = total > 0 ? Math.Min(100, used * 100 / total) : 0;
        }
        catch (Exception ex) { CapacityText.Text = "Ukuran file tidak dapat dibaca: " + ex.Message; CapacityBar.Value = 0; }
    }
    private static string FormatBytes(long bytes) => bytes >= 1L << 30 ? $"{bytes / (double)(1L << 30):0.##} GB" : bytes >= 1 << 20 ? $"{bytes / (double)(1 << 20):0.##} MB" : $"{bytes / 1024d:0.##} KB";
    private static string FormatTime(TimeSpan time) => $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
    private void SelectIso_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "File ISO (*.iso)|*.iso" };
        if (dialog.ShowDialog(this) == true) SetIso(dialog.FileName);
    }
    private void Iso_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] paths && paths.Length == 1) SetIso(paths[0]);
    }
    private void SetIso(string path)
    {
        if (!File.Exists(path) || !string.Equals(Path.GetExtension(path), ".iso", StringComparison.OrdinalIgnoreCase)) { MessageBox.Show(this, "Pilih satu file ISO yang valid."); return; }
        isoPath = path; IsoName.Text = Path.GetFileName(path); IsoHint.Text = FormatBytes(new FileInfo(path).Length);
    }
    private void Review_Click(object sender, RoutedEventArgs e)
    {
        if (currentKind == ProjectKind.Iso ? isoPath == null : CurrentFiles.Items.Count == 0) { MessageBox.Show(this, "Tambahkan file terlebih dahulu.", "Proyek kosong", MessageBoxButton.OK, MessageBoxImage.Information); return; }
        if (drives.Count == 0) { MessageBox.Show(this, "Drive CD/DVD tidak ditemukan. Hubungkan drive dan perbarui daftar.", "Drive tidak ditemukan", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        ReviewDrivePicker.ItemsSource = drives; ReviewDrivePicker.SelectedItem = drives.FirstOrDefault(d => d.Id == selectedDriveId) ?? drives[0];
        VerifyHint.Text = currentKind == ProjectKind.Audio ? "Audio CD menggunakan pemeriksaan cepat; bukan perbandingan penuh setiap byte." : "Verifikasi penuh melalui drive setelah penulisan.";
        ReviewSummary.Text = currentKind switch { ProjectKind.Data => $"Data Disc · {dataFiles.Items.Count} item · {VolumeName.Text}", ProjectKind.Audio => $"Audio CD · {audioFiles.Items.Count} lagu", _ => $"Burn ISO · {Path.GetFileName(isoPath)}" };
        ShowPage(ReviewPage, currentKind == ProjectKind.Data ? DataNav : currentKind == ProjectKind.Audio ? AudioNav : IsoNav, "PROYEK  /  TINJAU");
    }
    private void Back_Click(object sender, RoutedEventArgs e) { if (currentKind == ProjectKind.Iso) Iso_Click(sender, e); else OpenProject(currentKind); }
    private async void Burn_Click(object sender, RoutedEventArgs e)
    {
        if (ReviewDrivePicker.SelectedItem is not OpticalDrive drive) { MessageBox.Show(this, "Pilih drive tujuan."); return; }
        if (!drive.IsBlank) { MessageBox.Show(this, "Masukkan disc kosong yang sesuai. Jika disc dapat ditulis ulang, hapus isinya di Pengaturan.", "Disc belum siap", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        if (currentKind == ProjectKind.Audio && !drive.IsCd) { MessageBox.Show(this, "Audio CD memerlukan disc CD kosong.", "Disc tidak sesuai", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
        try
        {
            long bytes = currentKind == ProjectKind.Iso ? new FileInfo(isoPath!).Length : currentKind == ProjectKind.Data ? dataFiles.TotalBytes : 0;
            double seconds = audioFiles.Items.Sum(p => durations.GetValueOrDefault(p).TotalSeconds);
            if ((currentKind != ProjectKind.Audio && drive.FreeBytes > 0 && bytes > drive.FreeBytes) || (currentKind == ProjectKind.Audio && drive.AudioCapacitySeconds > 0 && seconds > drive.AudioCapacitySeconds)) { MessageBox.Show(this, "Proyek melebihi kapasitas disc.", "Kapasitas tidak cukup", MessageBoxButton.OK, MessageBoxImage.Warning); return; }
            var kind = currentKind; string driveId = drive.Id, volume = VolumeName.Text.Trim(); string? image = isoPath;
            var paths = (kind == ProjectKind.Audio ? audioFiles : dataFiles).Items.ToArray(); bool verify = VerifyCheck.IsChecked == true, eject = EjectCheck.IsChecked == true;
            if (kind == ProjectKind.Data && string.IsNullOrWhiteSpace(volume)) { MessageBox.Show(this, "Isi nama disc terlebih dahulu."); return; }
            await RunOperationAsync($"{drive.Name} · {drive.Media}", p =>
            {
                if (kind == ProjectKind.Data) service.BurnData(driveId, paths, volume, verify, eject, p);
                else if (kind == ProjectKind.Audio) service.BurnAudio(driveId, paths, verify, eject, p);
                else service.BurnIso(driveId, image!, verify, eject, p);
            });
        }
        catch (Exception ex) { ShowError(ex); }
    }
    private async void Erase_Click(object sender, RoutedEventArgs e)
    {
        if (EraseDrivePicker.SelectedItem is not OpticalDrive drive) { MessageBox.Show(this, "Pilih drive terlebih dahulu."); return; }
        if (MessageBox.Show(this, $"Hapus seluruh isi disc pada {drive.Name}? Tindakan ini tidak dapat dibatalkan.", "Konfirmasi penghapusan", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await RunOperationAsync(drive.Name, p => service.Erase(drive.Id, false, p));
    }
    private async Task RunOperationAsync(string details, Action<IProgress<BurnProgress>> operation)
    {
        ShowPage(ProgressPage, currentKind == ProjectKind.Audio ? AudioNav : currentKind == ProjectKind.Iso ? IsoNav : DataNav, "PROYEK  /  PROSES");
        busy = true; SetNavigationEnabled(false); ProgressStatus.Text = "Mempersiapkan..."; ProgressDetails.Text = details; ProgressDone.Visibility = Visibility.Collapsed; BurnProgressBar.IsIndeterminate = true;
        var progress = new Progress<BurnProgress>(p => { if (!busy) return; ProgressStatus.Text = p.Status; BurnProgressBar.IsIndeterminate = p.Percent == null; if (p.Percent != null) BurnProgressBar.Value = Math.Clamp(p.Percent.Value, 0, 100); });
        try { await Task.Run(() => operation(progress)); ProgressStatus.Text = "Proses selesai"; BurnProgressBar.IsIndeterminate = false; BurnProgressBar.Value = 100; }
        catch (Exception ex) { ProgressStatus.Text = "Proses gagal"; ProgressDetails.Text = "Periksa rincian kesalahan dan coba lagi."; BurnProgressBar.IsIndeterminate = false; BurnProgressBar.Value = 0; MessageBox.Show(this, DriveStatusText.ExplainError(ex), "Proses gagal", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { busy = false; SetNavigationEnabled(true); ProgressDone.Visibility = Visibility.Visible; RefreshDrives(); }
    }
    private void SetNavigationEnabled(bool enabled) { foreach (var button in new[] { HomeNav, DataNav, AudioNav, IsoNav, SettingsNav, RefreshDriveButton, OpenProjectButton, SaveProjectButton }) button.IsEnabled = enabled; }
    private void ShowError(Exception ex) => MessageBox.Show(this, DriveStatusText.ExplainError(ex), "HY Burning Studio", MessageBoxButton.OK, MessageBoxImage.Error);
}

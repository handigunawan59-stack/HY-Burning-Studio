using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using HYBurningStudio.Services;

namespace HYBurningStudio;

public partial class CreateIsoWindow : Window
{
    private readonly ObservableCollection<string> paths = [];
    private bool busy;
    public CreateIsoWindow()
    {
        InitializeComponent(); Items.ItemsSource = paths; RefreshDrives();
        Closing += (_, e) => { if (busy) { e.Cancel = true; MessageBox.Show(this, "Tunggu sampai pembuatan ISO selesai."); } };
    }
    private void Source_Changed(object sender, RoutedEventArgs e)
    {
        if (FilesPanel is null || DiscPanel is null) return;
        FilesPanel.Visibility = FromFiles.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        DiscPanel.Visibility = FromFiles.IsChecked == true ? Visibility.Collapsed : Visibility.Visible;
    }
    private void AddPaths(IEnumerable<string> entries)
    {
        foreach (string path in entries.Select(Path.GetFullPath))
            if (!paths.Contains(path, StringComparer.OrdinalIgnoreCase)) paths.Add(path);
    }
    private void AddFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Multiselect = true, Filter = "Semua file (*.*)|*.*" };
        if (dialog.ShowDialog(this) == true) AddPaths(dialog.FileNames);
    }
    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Pilih folder sumber" };
        if (dialog.ShowDialog(this) == true) AddPaths([dialog.FolderName]);
    }
    private void Remove_Click(object sender, RoutedEventArgs e) { if (Items.SelectedItem is string path) paths.Remove(path); }
    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshDrives();
    private void RefreshDrives()
    {
        var list = System.IO.DriveInfo.GetDrives().Where(x => x.DriveType == DriveType.CDRom).Select(x => new DiscEntry(x.Name[..2], x.Name)).ToArray();
        DiscPicker.ItemsSource = list; DiscPicker.SelectedIndex = list.Length > 0 ? 0 : -1;
    }
    private sealed record DiscEntry(string Root, string Name);
    private async void Create_Click(object sender, RoutedEventArgs e)
    {
        bool fromFiles = FromFiles.IsChecked == true;
        if (fromFiles && paths.Count == 0) { MessageBox.Show(this, "Tambahkan file atau folder terlebih dahulu."); return; }
        if (!fromFiles && DiscPicker.SelectedItem is not DiscEntry) { MessageBox.Show(this, "Drive CD/DVD tidak ditemukan. Hubungkan drive lalu perbarui daftar."); return; }
        var dialog = new SaveFileDialog { Title = "Simpan file ISO", Filter = "Image ISO (*.iso)|*.iso", DefaultExt = ".iso", AddExtension = true, OverwritePrompt = true, FileName = "HY_BURNING.iso" };
        if (dialog.ShowDialog(this) != true) return;
        string output = dialog.FileName, volume = Volume.Text.Trim(), drive = (DiscPicker.SelectedItem as DiscEntry)?.Root ?? "";
        string[] source = paths.ToArray();
        busy = true; CreateButton.IsEnabled = CloseButton.IsEnabled = false; FilesPanel.IsEnabled = DiscPanel.IsEnabled = FromDisc.IsEnabled = FromFiles.IsEnabled = false;
        Progress.Visibility = Visibility.Visible; Progress.IsIndeterminate = true; Status.Text = "Menyiapkan ISO...";
        var progress = new Progress<(string text, double? value)>(value => { if (!busy) return; Status.Text = value.text; Progress.IsIndeterminate = value.value is null; if (value.value is double number) Progress.Value = number; });
        try
        {
            await Task.Run(() => { Action<string, double?> report = (text, value) => ((IProgress<(string, double?)>)progress).Report((text, value)); if (fromFiles) IsoCreationService.CreateFromFiles(source, volume, output, true, report); else IsoCreationService.CreateFromDisc(drive, output, true, report); });
            Status.Text = "ISO berhasil dibuat: " + output; Progress.IsIndeterminate = false; Progress.Value = 100;
            MessageBox.Show(this, "File ISO berhasil dibuat.\n\n" + output, "Buat ISO", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) { Status.Text = "Pembuatan ISO gagal; file sumber dan tujuan sebelumnya tetap aman."; Progress.IsIndeterminate = false; Progress.Value = 0; MessageBox.Show(this, DriveStatusText.ExplainError(ex), "Gagal membuat ISO", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { busy = false; CreateButton.IsEnabled = CloseButton.IsEnabled = true; FilesPanel.IsEnabled = DiscPanel.IsEnabled = FromDisc.IsEnabled = FromFiles.IsEnabled = true; }
    }
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}

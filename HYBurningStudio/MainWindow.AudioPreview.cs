using System.Windows;
using System.Windows.Controls;
using NAudio.Wave;

namespace HYBurningStudio;

public partial class MainWindow
{
    private MediaFoundationReader? previewReader;
    private WaveOutEvent? previewOutput;

    private void PreviewAudio_Click(object sender, RoutedEventArgs e)
    {
        if (currentKind != ProjectKind.Audio || ItemsList.SelectedItem is not ItemRow row)
        {
            MessageBox.Show(this, "Pilih lagu yang ingin didengarkan.", "Pratinjau lagu", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        StopAudioPreview();
        try
        {
            previewReader = new MediaFoundationReader(row.Path);
            previewOutput = new WaveOutEvent();
            previewOutput.PlaybackStopped += PreviewPlaybackStopped;
            previewOutput.Init(previewReader);
            previewOutput.Play();
        }
        catch (Exception ex)
        {
            StopAudioPreview();
            MessageBox.Show(this, $"Lagu tidak dapat diputar. Periksa file dan perangkat audio.\n\nDetail: {ex.Message}", "Pratinjau lagu", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void StopPreview_Click(object sender, RoutedEventArgs e) => StopAudioPreview();

    private void AudioSelectionChanged(object sender, SelectionChangedEventArgs e) => StopAudioPreview();

    private void PreviewPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!ReferenceEquals(previewOutput, sender)) return;
            StopAudioPreview();
            if (e.Exception != null)
                MessageBox.Show(this, $"Pemutaran berhenti karena terjadi kesalahan.\n\nDetail: {e.Exception.Message}", "Pratinjau lagu", MessageBoxButton.OK, MessageBoxImage.Warning);
        }));
    }

    private void StopAudioPreview()
    {
        var output = previewOutput;
        var reader = previewReader;
        previewOutput = null;
        previewReader = null;
        if (output != null)
        {
            output.PlaybackStopped -= PreviewPlaybackStopped;
            try { output.Stop(); } catch (InvalidOperationException) { }
            output.Dispose();
        }
        reader?.Dispose();
    }
}

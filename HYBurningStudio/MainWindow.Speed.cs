using HYBurningStudio.Services;

namespace HYBurningStudio;

public partial class MainWindow
{
    private sealed record SpeedOption(int? Value, string Label);

    private void UpdateSpeedPicker(DriveInfo? drive)
    {
        if (SpeedPicker is null || SpeedHint is null) return;
        var options = new List<SpeedOption> { new(null, "Otomatis (disarankan)") };
        string hint = "Kecepatan sebenarnya dapat disesuaikan oleh drive.";
        if (drive is not null && drive.IsBlank)
        {
            try
            {
                var speeds = service.GetWriteSpeeds(drive.Id, currentKind == ProjectKind.Audio);
                options.AddRange(speeds.Select(speed => new SpeedOption(speed, $"{speed:N0} sektor/detik")));
                if (speeds.Length == 0) hint = "Daftar kecepatan tidak tersedia; gunakan otomatis.";
            }
            catch (Exception) { hint = "Kecepatan tidak dapat dibaca; gunakan otomatis atau periksa drive dan disc."; }
        }
        else hint = "Masukkan disc kosong yang didukung untuk melihat pilihan kecepatan.";
        SpeedPicker.ItemsSource = options;
        SpeedPicker.SelectedIndex = 0;
        SpeedHint.Text = hint;
    }
}

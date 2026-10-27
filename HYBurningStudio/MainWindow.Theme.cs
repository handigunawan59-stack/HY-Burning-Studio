using System.Windows;
using System.Windows.Controls;
using HYBurningStudio.Services;

namespace HYBurningStudio;

public partial class MainWindow
{
    private bool themeReady;

    private void InitializeTheme()
    {
        ThemePicker.SelectedIndex = ThemePreference.Load(ThemePreference.DefaultPath) == "dark" ? 1 : 0;
        themeReady = true;
    }

    private void ThemePicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!themeReady) return;
        var theme = ThemePicker.SelectedIndex == 1 ? "dark" : "light";
        ThemePalette.Apply(theme);
        try { ThemePreference.Save(ThemePreference.DefaultPath, theme); }
        catch (Exception ex) { MessageBox.Show(this, "Tema diterapkan, tetapi pengaturan tidak dapat disimpan.\n\n" + ex.Message, "Pengaturan tema", MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}

using System.Windows;

namespace HYBurningStudio;

public partial class MainWindow
{
    private void CreateIso_Click(object sender, RoutedEventArgs e) => new CreateIsoWindow { Owner = this }.ShowDialog();
}

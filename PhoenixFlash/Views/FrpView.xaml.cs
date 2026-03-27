using System.Windows;
using System.Windows.Controls;

namespace PhoenixFlash.Views
{
    public partial class FrpView : UserControl
    {
        public FrpView()
        {
            InitializeComponent();
        }

        private void BtnDoFrp_Click(object sender, RoutedEventArgs e)
        {
            var main = (MainWindow)Window.GetWindow(this);
            if (!main.DeviceConnected)
            { main.AddLog("WARN", "Aucun appareil connecté"); return; }
            main.AddLog("INFO", "FRP Bypass démarré...");
            main.AddLog("WARN", "Ne pas déconnecter l'appareil!");
        }
    }
}

using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PhoenixFlash.Views
{
    public partial class FlashView : UserControl
    {
        private string _selectedFile = "";

        public FlashView()
        {
            InitializeComponent();
        }

        public void AddLog(string type, string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            TxtLog.Text += $"[{time}] [{type,-4}]  {message}\n";
            LogScroller.ScrollToBottom();
        }

        private void SelectFirmware(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Sélectionner le firmware",
                Filter = "Firmware files (*.bin;*.zip)|*.bin;*.zip|All files (*.*)|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                _selectedFile = dialog.FileName;
                var info = new FileInfo(_selectedFile);
                TxtFileName.Text = info.Name;
                TxtFileSize.Text = $"{info.Length / 1024 / 1024} MB";
                AddLog("OK", $"Firmware chargé: {info.Name}");
            }
        }

        private void BtnDoFlash_Click(object sender, RoutedEventArgs e)
        {
            var main = (MainWindow)Window.GetWindow(this);
            if (string.IsNullOrEmpty(_selectedFile))
            { main.AddLog("WARN", "Aucun firmware sélectionné"); return; }
            if (!main.DeviceConnected)
            { main.AddLog("WARN", "Aucun appareil connecté"); return; }
            main.AddLog("INFO", "Démarrage du flash...");
            main.AddLog("WARN", "Ne pas déconnecter l'appareil!");
        }

        private void BtnBackup_Click(object sender, RoutedEventArgs e)
        {
            var main = (MainWindow)Window.GetWindow(this);
            if (!main.DeviceConnected)
            { main.AddLog("WARN", "Aucun appareil connecté"); return; }
            main.AddLog("INFO", "Sauvegarde en cours...");
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            var main = (MainWindow)Window.GetWindow(this);
            if (!main.DeviceConnected)
            { main.AddLog("WARN", "Aucun appareil connecté"); return; }
            var result = MessageBox.Show(
                "Êtes-vous sûr de vouloir effectuer un factory reset?\nToutes les données seront supprimées.",
                "Confirmation requise", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
                main.AddLog("INFO", "Factory reset lancé...");
        }

        private void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            var main = (MainWindow)Window.GetWindow(this);
            main.AddLog("INFO", "Test de connexion...");
            main.AddLog(main.DeviceConnected ? "OK" : "WARN",
                   main.DeviceConnected ? "Appareil répondant correctement" : "Aucun appareil détecté");
        }
    }
}

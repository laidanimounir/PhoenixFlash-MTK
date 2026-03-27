using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PhoenixFlash.Views.Sauvegarde
{
    public class AppItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string AppName { get; set; } = "";
        public string PackageName { get; set; } = "";
        public string Size { get; set; } = "";
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class AppsView : UserControl
    {
        private List<AppItem> _apps = new();

        public AppsView()
        {
            InitializeComponent();
        }

        private void BtnLoadApps_Click(object sender, RoutedEventArgs e)
        {
            var main = (MainWindow)Window.GetWindow(this);
            if (!main.DeviceConnected)
            { main.AddLog("WARN", "Aucun appareil connecté"); return; }

            main.AddLog("INFO", "Chargement des applications...");
            _apps.Clear();

            // Get list of user-installed packages
            string raw = main.RunAdb("shell pm list packages -3");
            var lines = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                string pkg = line.Replace("package:", "").Trim();
                if (string.IsNullOrWhiteSpace(pkg)) continue;

                // Try to get app label
                string label = main.RunAdb(
                    $"shell dumpsys package {pkg} | grep -i 'applicationInfo' | head -1").Trim();

                // Get APK size
                string apkPath = main.RunAdb(
                    $"shell pm path {pkg}").Replace("package:", "").Trim();
                string size = "";
                if (!string.IsNullOrWhiteSpace(apkPath))
                {
                    string sizeRaw = main.RunAdb(
                        $"shell stat -c %s {apkPath} 2>/dev/null").Trim();
                    if (long.TryParse(sizeRaw, out long bytes))
                        size = $"{bytes / 1024 / 1024} MB";
                }

                _apps.Add(new AppItem
                {
                    PackageName = pkg,
                    AppName = string.IsNullOrWhiteSpace(label) ? pkg.Split('.')[^1] : pkg.Split('.')[^1],
                    Size = string.IsNullOrWhiteSpace(size) ? "?" : size,
                    IsSelected = false
                });
            }

            AppsList.ItemsSource = null;
            AppsList.ItemsSource = _apps;
            TxtAppCount.Text = $"{_apps.Count} applications";
            main.AddLog("OK", $"{_apps.Count} applications trouvées");
        }

        private void ChkSelectAll_Changed(object sender, RoutedEventArgs e)
        {
            bool val = ChkSelectAll.IsChecked == true;
            foreach (var app in _apps)
                app.IsSelected = val;
        }

        private void BtnBackupApps_Click(object sender, RoutedEventArgs e)
        {
            var main = (MainWindow)Window.GetWindow(this);
            if (!main.DeviceConnected)
            { main.AddLog("WARN", "Aucun appareil connecté"); return; }

            var selected = _apps.FindAll(a => a.IsSelected);
            if (selected.Count == 0)
            { main.AddLog("WARN", "Aucune application sélectionnée"); return; }

            var dialog = new Microsoft.Win32.OpenFolderDialog
            { Title = "Dossier de sauvegarde" };
            if (dialog.ShowDialog() != true) return;

            string dest = dialog.FolderName;
            string packages = string.Join(" ", selected.ConvertAll(a => a.PackageName));

            main.AddLog("INFO", $"Backup de {selected.Count} applications...");
            string outFile = Path.Combine(dest, $"apps_{DateTime.Now:yyyyMMdd_HHmm}.ab");
            main.RunAdb($"backup -apk -f \"{outFile}\" {packages}");
            main.AddLog("OK", $"Backup sauvegardé: {outFile}");
            main.AddLog("WARN", "Confirmez sur l'écran du téléphone si demandé");

            MessageBox.Show(
                $"{selected.Count} application(s) sauvegardées.\nFichier: {outFile}\n\nConfirmez sur le téléphone si une boîte de dialogue apparaît.",
                "Backup terminé", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnRestoreApps_Click(object sender, RoutedEventArgs e)
        {
            var main = (MainWindow)Window.GetWindow(this);
            if (!main.DeviceConnected)
            { main.AddLog("WARN", "Aucun appareil connecté"); return; }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Sélectionner le fichier .ab",
                Filter = "ADB Backup (*.ab)|*.ab|Tous les fichiers (*.*)|*.*"
            };
            if (dialog.ShowDialog() != true) return;

            string file = dialog.FileName;
            main.AddLog("INFO", $"Restauration depuis: {Path.GetFileName(file)}");
            main.AddLog("WARN", "Confirmez la restauration sur l'écran du téléphone!");
            main.RunAdb($"restore \"{file}\"");
            main.AddLog("OK", "Restauration lancée — vérifiez le téléphone");
        }
    }
}
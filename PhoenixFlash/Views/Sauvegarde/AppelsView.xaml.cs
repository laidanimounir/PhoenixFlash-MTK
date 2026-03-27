using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PhoenixFlash.Views.Sauvegarde
{
    public class AppelItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string Number { get; set; } = "";
        public string TypeLabel { get; set; } = "";
        public string TypeIcon { get; set; } = "";
        public string TypeColor { get; set; } = "#4CAF50";
        public string Date { get; set; } = "";
        public string Duration { get; set; } = "";
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class AppelsView : UserControl
    {
        private List<AppelItem> _appels = new();

        public AppelsView()
        {
            InitializeComponent();
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            var main = (MainWindow)Window.GetWindow(this);
            if (!main.DeviceConnected)
            { main.AddLog("WARN", "Aucun appareil connecté"); return; }

            main.AddLog("INFO", "Chargement du journal d'appels...");
            _appels.Clear();

            // Query call log: number, type (1=incoming,2=outgoing,3=missed), date, duration
            string raw = main.RunAdb(
                "shell content query --uri content://call_log/calls " +
                "--projection number:type:date:duration " +
                "--sort \"date DESC\" --limit 100").Trim();

            var lines = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (!line.Contains("number=")) continue;

                string number = "", type = "", date = "", duration = "";
                foreach (var part in line.Split(','))
                {
                    var p = part.Trim();
                    if (p.StartsWith("number="))
                        number = p.Replace("number=", "").Trim();
                    else if (p.StartsWith("type="))
                        type = p.Replace("type=", "").Trim();
                    else if (p.StartsWith("date="))
                    {
                        string ts = p.Replace("date=", "").Trim();
                        if (long.TryParse(ts, out long ms))
                        {
                            var dt = DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime;
                            date = dt.ToString("dd/MM HH:mm");
                        }
                    }
                    else if (p.StartsWith("duration="))
                        duration = p.Replace("duration=", "").Trim();
                }

                if (string.IsNullOrWhiteSpace(number)) continue;

                // Format duration
                string durStr = "";
                if (int.TryParse(duration, out int secs) && secs > 0)
                    durStr = secs >= 60
                        ? $"{secs / 60}min {secs % 60}s"
                        : $"{secs}s";

                // Type mapping
                string icon, label, color;
                switch (type)
                {
                    case "1": icon = "📞"; label = "Entrant"; color = "#4CAF50"; break;
                    case "2": icon = "📲"; label = "Sortant"; color = "#2196F3"; break;
                    case "3": icon = "📵"; label = "Manqué"; color = "#F44336"; break;
                    default: icon = "📞"; label = "Inconnu"; color = "#9E9E9E"; break;
                }

                _appels.Add(new AppelItem
                {
                    Number = string.IsNullOrWhiteSpace(number) ? "Inconnu" : number,
                    TypeLabel = label,
                    TypeIcon = icon,
                    TypeColor = color,
                    Date = date,
                    Duration = durStr,
                    IsSelected = false
                });
            }

            AppelsList.ItemsSource = null;
            AppelsList.ItemsSource = _appels;
            TxtCount.Text = $"{_appels.Count} appels";
            main.AddLog("OK", $"{_appels.Count} appels chargés");
        }

        private void ChkSelectAll_Changed(object sender, RoutedEventArgs e)
        {
            bool val = ChkSelectAll.IsChecked == true;
            foreach (var a in _appels)
                a.IsSelected = val;
        }

        private void BtnBackupAppels_Click(object sender, RoutedEventArgs e)
        {
            var main = (MainWindow)Window.GetWindow(this);
            if (!main.DeviceConnected)
            { main.AddLog("WARN", "Aucun appareil connecté"); return; }

            var dialog = new Microsoft.Win32.OpenFolderDialog
            { Title = "Dossier de sauvegarde journal d'appels" };
            if (dialog.ShowDialog() != true) return;

            string dest = dialog.FolderName;
            string outFile = Path.Combine(dest, $"calllog_{DateTime.Now:yyyyMMdd_HHmm}.ab");

            main.AddLog("INFO", "Backup journal d'appels...");
            main.RunAdb(
                $"backup -noapk com.android.providers.contacts -f \"{outFile}\"");
            main.AddLog("OK", $"Journal d'appels sauvegardé: {outFile}");

            MessageBox.Show(
                $"Journal d'appels sauvegardé!\n{outFile}",
                "Backup Appels", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
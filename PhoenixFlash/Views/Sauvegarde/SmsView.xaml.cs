using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PhoenixFlash.Views.Sauvegarde
{
    public class SmsItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string Address { get; set; } = "";
        public string LastMessage { get; set; } = "";
        public string Date { get; set; } = "";
        public string Count { get; set; } = "";
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class SmsView : UserControl
    {
        private List<SmsItem> _sms = new();

        public SmsView()
        {
            InitializeComponent();
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            var main = (MainWindow)Window.GetWindow(this);
            if (!main.DeviceConnected)
            { main.AddLog("WARN", "Aucun appareil connecté"); return; }

            main.AddLog("INFO", "Chargement des SMS...");
            _sms.Clear();

            // Query SMS grouped by address
            string raw = main.RunAdb(
                "shell content query --uri content://sms " +
                "--projection address:body:date:type " +
                "--sort \"date DESC\" --limit 200").Trim();

            var lines = raw.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var seen = new Dictionary<string, SmsItem>();

            foreach (var line in lines)
            {
                if (!line.Contains("address=")) continue;

                string address = "", body = "", date = "";
                foreach (var part in line.Split(','))
                {
                    var p = part.Trim();
                    if (p.StartsWith("address="))
                        address = p.Replace("address=", "").Trim();
                    else if (p.StartsWith("body="))
                        body = p.Replace("body=", "").Trim();
                    else if (p.StartsWith("date="))
                    {
                        string ts = p.Replace("date=", "").Trim();
                        if (long.TryParse(ts, out long ms))
                        {
                            var dt = DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime;
                            date = dt.ToString("dd/MM/yyyy");
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(address)) continue;

                if (seen.ContainsKey(address))
                {
                    int cnt = int.TryParse(seen[address].Count, out int n) ? n + 1 : 1;
                    seen[address].Count = cnt.ToString();
                }
                else
                {
                    var item = new SmsItem
                    {
                        Address = address,
                        LastMessage = body.Length > 60 ? body[..60] + "..." : body,
                        Date = date,
                        Count = "1",
                        IsSelected = false
                    };
                    seen[address] = item;
                    _sms.Add(item);
                }
            }

            SmsList.ItemsSource = null;
            SmsList.ItemsSource = _sms;
            TxtCount.Text = $"{_sms.Count} conversations";
            main.AddLog("OK", $"{_sms.Count} conversations SMS chargées");
        }

        private void ChkSelectAll_Changed(object sender, RoutedEventArgs e)
        {
            bool val = ChkSelectAll.IsChecked == true;
            foreach (var s in _sms)
                s.IsSelected = val;
        }

        private void BtnBackupSms_Click(object sender, RoutedEventArgs e)
        {
            var main = (MainWindow)Window.GetWindow(this);
            if (!main.DeviceConnected)
            { main.AddLog("WARN", "Aucun appareil connecté"); return; }

            var dialog = new Microsoft.Win32.OpenFolderDialog
            { Title = "Dossier de sauvegarde SMS" };
            if (dialog.ShowDialog() != true) return;

            string dest = dialog.FolderName;
            string outFile = Path.Combine(dest, $"sms_{DateTime.Now:yyyyMMdd_HHmm}.ab");

            main.AddLog("INFO", "Backup SMS en cours...");
            main.AddLog("WARN", "Confirmez sur l'écran du téléphone si demandé!");
            main.RunAdb(
                $"backup -noapk com.android.providers.telephony -f \"{outFile}\"");
            main.AddLog("OK", $"SMS sauvegardés: {outFile}");

            MessageBox.Show(
                $"SMS sauvegardés!\nFichier: {outFile}\n\nConfirmez sur le téléphone si une boîte de dialogue apparaît.",
                "Backup SMS", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnRestoreSms_Click(object sender, RoutedEventArgs e)
        {
            var main = (MainWindow)Window.GetWindow(this);
            if (!main.DeviceConnected)
            { main.AddLog("WARN", "Aucun appareil connecté"); return; }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Sélectionner le backup SMS",
                Filter = "ADB Backup (*.ab)|*.ab|Tous les fichiers (*.*)|*.*"
            };
            if (dialog.ShowDialog() != true) return;

            string file = dialog.FileName;
            main.AddLog("INFO", $"Restauration SMS depuis: {Path.GetFileName(file)}");
            main.AddLog("WARN", "Confirmez la restauration sur l'écran du téléphone!");
            main.RunAdb($"restore \"{file}\"");
            main.AddLog("OK", "Restauration SMS lancée — vérifiez le téléphone");

            MessageBox.Show(
                "Restauration SMS lancée.\nConfirmez sur l'écran du téléphone.",
                "Restore SMS", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
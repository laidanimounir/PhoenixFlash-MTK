using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using WinForms = System.Windows.Forms;

namespace PhoenixFlash
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer? _clock;
        private DispatcherTimer? _usbTimer;
        private string _selectedFile = "";
        private bool _deviceConnected = false;
        private string _adbPath = "";

        public MainWindow()
        {
            InitializeComponent();
            ShowPanel("flash");
            InitAdb();
            StartClock();
            StartUsbWatcher();
            AddLog("OK", "Application démarrée");
            AddLog("OK", "Drivers ADB chargés");
            AddLog("INFO", "Attente d'un appareil...");
        }

        private void InitAdb()
        {
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            _adbPath = Path.Combine(exeDir, "adb", "adb.exe");
            if (!File.Exists(_adbPath))
                _adbPath = Path.GetFullPath(
                    Path.Combine(exeDir, "..", "..", "..", "adb", "adb.exe"));
            if (!File.Exists(_adbPath))
            { AddLog("ERR", $"adb.exe introuvable: {_adbPath}"); return; }
            AddLog("OK", $"ADB trouvé: {_adbPath}");
            RunAdb("start-server");
        }

        private string RunAdb(string args)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = _adbPath,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = Process.Start(psi);
                if (process == null) return "";
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);
                return output.Trim();
            }
            catch (Exception ex)
            { AddLog("ERR", $"ADB error: {ex.Message}"); return ""; }
        }

        private void StartUsbWatcher()
        {
            _usbTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _usbTimer.Tick += (s, e) => CheckAdbDevice();
            _usbTimer.Start();
        }

        private void CheckAdbDevice()
        {
            string result = RunAdb("devices");
            bool found = result.Contains("device") &&
                         !result.Trim().Equals("List of devices attached");
            if (found && !_deviceConnected)
            { _deviceConnected = true; LoadDeviceInfo(); }
            else if (!found && _deviceConnected)
            { _deviceConnected = false; ClearDeviceInfo(); AddLog("WARN", "Appareil déconnecté"); }
        }

        private void LoadDeviceInfo()
        {
            AddLog("INFO", "Lecture des informations...");
            string brand = RunAdb("shell getprop ro.product.brand").Trim();
            string model = RunAdb("shell getprop ro.product.model").Trim();
            string android = RunAdb("shell getprop ro.build.version.release").Trim();
            string cpu = RunAdb("shell getprop ro.hardware").Trim();
            string serial = RunAdb("get-serialno").Trim();

            string imei = "Non disponible";
            try
            {
                string imei1 = RunAdb("shell service call iphonesubinfo 1 i32 1")
                               .Split('\'').Length > 1
                               ? RunAdb("shell service call iphonesubinfo 1 i32 1")
                                 .Split('\'')[1].Replace(".", "").Trim() : "";
                if (imei1.Length >= 15) imei = imei1.Substring(0, 15);
                else
                {
                    string imei2 = RunAdb("shell getprop persist.radio.imei").Trim();
                    if (!string.IsNullOrWhiteSpace(imei2) && imei2.Length >= 15) imei = imei2;
                    else
                    {
                        string imei3 = RunAdb("shell getprop ril.imei").Trim();
                        if (!string.IsNullOrWhiteSpace(imei3) && imei3.Length >= 15) imei = imei3;
                    }
                }
            }
            catch { imei = "Non disponible"; }

            string deviceName = $"{brand} {model}".Trim();
            if (string.IsNullOrWhiteSpace(deviceName)) deviceName = "Android Device";

            TxtDeviceName.Text = deviceName.Length > 28 ? deviceName.Substring(0, 28) : deviceName;
            TxtDeviceChip.Text = string.IsNullOrWhiteSpace(cpu) ? "MTK" : cpu.ToUpper();
            TxtCpu.Text = string.IsNullOrWhiteSpace(cpu) ? "---" : cpu.ToUpper();
            TxtAndroid.Text = string.IsNullOrWhiteSpace(android) ? "---" : $"Android {android}";
            TxtImei.Text = imei;
            TxtSerial.Text = string.IsNullOrWhiteSpace(serial) ? "---" : serial;

            string storage = RunAdb("shell df /data").Trim();
            try
            {
                var lines = storage.Split('\n');
                if (lines.Length >= 2)
                {
                    var parts = lines[1].Split(
                        new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 4)
                    {
                        long total = long.Parse(parts[1]) / 1024;
                        long used = long.Parse(parts[2]) / 1024;
                        TxtStorage.Text = $"{used} MB / {total} MB";
                    }
                    else TxtStorage.Text = "---";
                }
                else TxtStorage.Text = "---";
            }
            catch { TxtStorage.Text = "---"; }

            AddLog("OK", $"Appareil: {deviceName}");
            AddLog("OK", $"Android {android} | CPU: {cpu}");
            AddLog("INFO", $"Série: {serial}");
            AddLog("INFO", $"IMEI: {imei}");
        }

        private void ClearDeviceInfo()
        {
            TxtDeviceName.Text = "En attente...";
            TxtDeviceChip.Text = "---";
            TxtCpu.Text = "---";
            TxtAndroid.Text = "---";
            TxtStorage.Text = "---";
            TxtImei.Text = "---";
            TxtSerial.Text = "---";
        }

        private void StartClock()
        {
            _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _clock.Tick += (s, e) => TxtClock.Text = DateTime.Now.ToString("HH:mm:ss");
            _clock.Start();
        }

        private void AddLog(string type, string message)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            TxtLog.Text += $"[{time}] [{type,-4}]  {message}\n";
            LogScroller.ScrollToBottom();
        }

        private void ShowPanel(string panel)
        {
            PanelFlash.Visibility = panel == "flash" ? Visibility.Visible : Visibility.Collapsed;
            PanelFrp.Visibility = panel == "frp" ? Visibility.Visible : Visibility.Collapsed;
            PanelSettings.Visibility = panel == "settings" ? Visibility.Visible : Visibility.Collapsed;
            PanelSauvegarde.Visibility = panel == "sauvegarde" ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnFlash_Click(object sender, RoutedEventArgs e) => ShowPanel("flash");
        private void BtnFrp_Click(object sender, RoutedEventArgs e) => ShowPanel("frp");
        private void BtnSettings_Click(object sender, RoutedEventArgs e) => ShowPanel("settings");
        private void BtnSauvegarde_Click(object sender, RoutedEventArgs e) => ShowPanel("sauvegarde");

        private void SelectFirmware(object sender,
            System.Windows.Input.MouseButtonEventArgs e)
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
            if (string.IsNullOrEmpty(_selectedFile))
            { AddLog("WARN", "Aucun firmware sélectionné"); return; }
            if (!_deviceConnected)
            { AddLog("WARN", "Aucun appareil connecté"); return; }
            AddLog("INFO", "Démarrage du flash...");
            AddLog("WARN", "Ne pas déconnecter l'appareil!");
        }

        private void BtnBackup_Click(object sender, RoutedEventArgs e)
        {
            if (!_deviceConnected)
            { AddLog("WARN", "Aucun appareil connecté"); return; }
            AddLog("INFO", "Sauvegarde en cours...");
        }

        private void BtnReset_Click(object sender, RoutedEventArgs e)
        {
            if (!_deviceConnected)
            { AddLog("WARN", "Aucun appareil connecté"); return; }
            var result = System.Windows.MessageBox.Show(
                "Êtes-vous sûr de vouloir effectuer un factory reset?\nToutes les données seront supprimées.",
                "Confirmation requise", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
                AddLog("INFO", "Factory reset lancé...");
        }

        private void BtnTest_Click(object sender, RoutedEventArgs e)
        {
            AddLog("INFO", "Test de connexion...");
            AddLog(_deviceConnected ? "OK" : "WARN",
                   _deviceConnected ? "Appareil répondant correctement" : "Aucun appareil détecté");
        }

        private void BtnDoFrp_Click(object sender, RoutedEventArgs e)
        {
            if (!_deviceConnected)
            { AddLog("WARN", "Aucun appareil connecté"); return; }
            AddLog("INFO", "FRP Bypass démarré...");
            AddLog("WARN", "Ne pas déconnecter l'appareil!");
        }

        private void BtnAnalyser_Click(object sender, RoutedEventArgs e)
        {
            if (!_deviceConnected)
            { AddLog("WARN", "Aucun appareil connecté"); return; }

            AddLog("INFO", "Analyse en cours...");

            string photosCount = RunAdb(
                "shell find /sdcard/DCIM -name '*.jpg' -o -name '*.jpeg' -o -name '*.png' | wc -l").Trim();
            string photosSize = RunAdb(
                "shell du -sh /sdcard/DCIM 2>/dev/null | cut -f1").Trim();
            TxtPhotosInfo.Text = $"{photosCount} fichiers — {photosSize}";
            AddLog("OK", $"Photos: {photosCount} ({photosSize})");

            string contacts = RunAdb(
                "shell content query --uri content://contacts/people | grep -c 'Row'").Trim();
            TxtContactsInfo.Text = $"{contacts} contacts";
            AddLog("OK", $"Contacts: {contacts}");

            string sms = RunAdb(
                "shell content query --uri content://sms | grep -c 'Row'").Trim();
            TxtSmsInfo.Text = $"{sms} messages";
            AddLog("OK", $"SMS: {sms}");

            string apps = RunAdb("shell pm list packages -3 | wc -l").Trim();
            TxtAppsInfo.Text = $"{apps} applications";
            AddLog("OK", $"Apps: {apps}");

            AddLog("OK", "Analyse terminée!");
        }

        private void BtnBackupSelection_Click(object sender, RoutedEventArgs e)
        {
            if (!_deviceConnected)
            { AddLog("WARN", "Aucun appareil connecté"); return; }

            var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "Choisir le dossier de sauvegarde"
            };
            if (dialog.ShowDialog() != WinForms.DialogResult.OK) return;

            string dest = dialog.SelectedPath;
            string deviceFolder = Path.Combine(dest,
                $"{TxtDeviceName.Text}_{DateTime.Now:yyyyMMdd_HHmm}");
            Directory.CreateDirectory(deviceFolder);
            AddLog("INFO", $"Sauvegarde vers: {deviceFolder}");

            if (ChkPhotos.IsChecked == true)
            {
                AddLog("INFO", "Sauvegarde photos...");
                RunAdb($"pull /sdcard/DCIM \"{Path.Combine(deviceFolder, "Photos")}\"");
                AddLog("OK", "Photos sauvegardées");
            }
            if (ChkContacts.IsChecked == true)
            {
                AddLog("INFO", "Sauvegarde contacts...");
                RunAdb($"pull /sdcard/contacts.vcf \"{Path.Combine(deviceFolder, "contacts.vcf")}\"");
                AddLog("OK", "Contacts sauvegardés");
            }
            if (ChkSms.IsChecked == true)
            {
                AddLog("INFO", "Sauvegarde SMS...");
                RunAdb($"backup -noapk com.android.providers.telephony -f \"{Path.Combine(deviceFolder, "sms.ab")}\"");
                AddLog("OK", "SMS sauvegardés");
            }
            if (ChkApps.IsChecked == true)
            {
                AddLog("INFO", "Sauvegarde applications...");
                RunAdb($"backup -apk -all -f \"{Path.Combine(deviceFolder, "apps.ab")}\"");
                AddLog("OK", "Applications sauvegardées");
            }

            AddLog("OK", "Sauvegarde terminée!");
            System.Windows.MessageBox.Show(
                $"Sauvegarde terminée!\nDossier: {deviceFolder}",
                "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
        }


        private void BtnVoirPhotos_Click(object sender, RoutedEventArgs e)
        {
            if (!_deviceConnected)
            { AddLog("WARN", "Aucun appareil connecté"); return; }

            AddLog("INFO", "Téléchargement des photos...");

        
            string tempFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "PhoenixFlash_Photos");
            Directory.CreateDirectory(tempFolder);

           
            RunAdb($"pull /sdcard/DCIM/Camera \"{tempFolder}\"");

            AddLog("OK", $"Photos copiées vers: {tempFolder}");

            
            Process.Start("explorer.exe", tempFolder);
        }

        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (!_deviceConnected)
            { AddLog("WARN", "Aucun appareil connecté"); return; }

            var dialog = new WinForms.FolderBrowserDialog
            {
                Description = "Choisir le dossier de sauvegarde à restaurer"
            };
            if (dialog.ShowDialog() != WinForms.DialogResult.OK) return;

            string src = dialog.SelectedPath;
            AddLog("INFO", $"Restauration depuis: {src}");

            string photosPath = Path.Combine(src, "Photos");
            if (Directory.Exists(photosPath))
            {
                AddLog("INFO", "Restauration photos...");
                RunAdb($"push \"{photosPath}\" /sdcard/DCIM");
                AddLog("OK", "Photos restaurées");
            }

            string contactsPath = Path.Combine(src, "contacts.vcf");
            if (File.Exists(contactsPath))
            {
                AddLog("INFO", "Restauration contacts...");
                RunAdb($"push \"{contactsPath}\" /sdcard/contacts.vcf");
                AddLog("OK", "Contacts restaurés");
            }

            AddLog("OK", "Restauration terminée!");
            System.Windows.MessageBox.Show("Restauration terminée!",
                "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnFormatFlash_Click(object sender, RoutedEventArgs e)
        {
            if (!_deviceConnected)
            { AddLog("WARN", "Aucun appareil connecté"); return; }

            var result = System.Windows.MessageBox.Show(
                "Cette opération va effectuer un factory reset.\nToutes les données seront supprimées.\n\nÊtes-vous sûr?",
                "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                AddLog("INFO", "Format en cours...");
                RunAdb("shell am broadcast -a android.intent.action.MASTER_CLEAR");
                AddLog("OK", "Format lancé — l'appareil va redémarrer");
            }
        }
    }
}
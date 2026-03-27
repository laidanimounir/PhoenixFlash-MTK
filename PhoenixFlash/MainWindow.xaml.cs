using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace PhoenixFlash
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer? _clock;
        private DispatcherTimer? _usbTimer;
        private string _adbPath = "";

        public bool DeviceConnected { get; private set; } = false;
        public string DeviceName => TxtDeviceName.Text;

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

        public string RunAdb(string args)
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
            if (found && !DeviceConnected)
            { DeviceConnected = true; LoadDeviceInfo(); }
            else if (!found && DeviceConnected)
            { DeviceConnected = false; ClearDeviceInfo(); AddLog("WARN", "Appareil déconnecté"); }
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

        public void AddLog(string type, string message)
        {
            PanelFlash?.AddLog(type, message);
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
    }
}
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PhoenixFlash.Views.Sauvegarde
{
    public partial class SauvegardeView : UserControl
    {
        public SauvegardeView()
        {
            InitializeComponent();
        }

        private void BtnAnalyser_Click(object sender, RoutedEventArgs e)
        {
            var main = (MainWindow)Window.GetWindow(this);
            if (!main.DeviceConnected)
            { main.AddLog("WARN", "Aucun appareil connecté"); return; }

            main.AddLog("INFO", "Analyse en cours...");

            string photosCount = main.RunAdb(
                "shell find /sdcard/DCIM -name '*.jpg' -o -name '*.jpeg' -o -name '*.png' | wc -l").Trim();
            string photosSize = main.RunAdb(
                "shell du -sh /sdcard/DCIM 2>/dev/null | cut -f1").Trim();
            TxtPhotosInfo.Text = $"{photosCount} fichiers — {photosSize}";
            main.AddLog("OK", $"Photos: {photosCount} ({photosSize})");

            string contacts = main.RunAdb(
                "shell content query --uri content://contacts/people | grep -c 'Row'").Trim();
            TxtContactsInfo.Text = $"{contacts} contacts";
            main.AddLog("OK", $"Contacts: {contacts}");

            string sms = main.RunAdb(
                "shell content query --uri content://sms | grep -c 'Row'").Trim();
            TxtSmsInfo.Text = $"{sms} messages";
            main.AddLog("OK", $"SMS: {sms}");

            string apps = main.RunAdb("shell pm list packages -3 | wc -l").Trim();
            TxtAppsInfo.Text = $"{apps} applications";
            main.AddLog("OK", $"Apps: {apps}");

            main.AddLog("OK", "Analyse terminée!");
        }

        private void BtnBackupSelection_Click(object sender, RoutedEventArgs e)
        {
            var main = (MainWindow)Window.GetWindow(this);
            if (!main.DeviceConnected)
            { main.AddLog("WARN", "Aucun appareil connecté"); return; }

            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Choisir le dossier de sauvegarde"
            };
            if (dialog.ShowDialog() != true) return;

            string dest = dialog.FolderName;
            string deviceFolder = Path.Combine(dest,
                $"{main.DeviceName}_{DateTime.Now:yyyyMMdd_HHmm}");
            Directory.CreateDirectory(deviceFolder);
            main.AddLog("INFO", $"Sauvegarde vers: {deviceFolder}");

            if (ChkPhotos.IsChecked == true)
            {
                main.AddLog("INFO", "Sauvegarde photos...");
                main.RunAdb($"pull /sdcard/DCIM \"{Path.Combine(deviceFolder, "Photos")}\"");
                main.AddLog("OK", "Photos sauvegardées");
            }
            if (ChkContacts.IsChecked == true)
            {
                main.AddLog("INFO", "Sauvegarde contacts...");
                main.RunAdb($"pull /sdcard/contacts.vcf \"{Path.Combine(deviceFolder, "contacts.vcf")}\"");
                main.AddLog("OK", "Contacts sauvegardés");
            }
            if (ChkSms.IsChecked == true)
            {
                main.AddLog("INFO", "Sauvegarde SMS...");
                main.RunAdb($"backup -noapk com.android.providers.telephony -f \"{Path.Combine(deviceFolder, "sms.ab")}\"");
                main.AddLog("OK", "SMS sauvegardés");
            }
            if (ChkApps.IsChecked == true)
            {
                main.AddLog("INFO", "Sauvegarde applications...");
                main.RunAdb($"backup -apk -all -f \"{Path.Combine(deviceFolder, "apps.ab")}\"");
                main.AddLog("OK", "Applications sauvegardées");
            }

            main.AddLog("OK", "Sauvegarde terminée!");
            MessageBox.Show(
                $"Sauvegarde terminée!\nDossier: {deviceFolder}",
                "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnVoirPhotos_Click(object sender, RoutedEventArgs e)
        {
            var main = (MainWindow)Window.GetWindow(this);
            if (!main.DeviceConnected)
            { main.AddLog("WARN", "Aucun appareil connecté"); return; }

            main.AddLog("INFO", "Téléchargement des photos...");

        
            string tempFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "PhoenixFlash_Photos");
            Directory.CreateDirectory(tempFolder);

           
            main.RunAdb($"pull /sdcard/DCIM/Camera \"{tempFolder}\"");

            main.AddLog("OK", $"Photos copiées vers: {tempFolder}");

            
            Process.Start("explorer.exe", tempFolder);
        }

        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            var main = (MainWindow)Window.GetWindow(this);
            if (!main.DeviceConnected)
            { main.AddLog("WARN", "Aucun appareil connecté"); return; }

            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Choisir le dossier de sauvegarde à restaurer"
            };
            if (dialog.ShowDialog() != true) return;

            string src = dialog.FolderName;
            main.AddLog("INFO", $"Restauration depuis: {src}");

            string photosPath = Path.Combine(src, "Photos");
            if (Directory.Exists(photosPath))
            {
                main.AddLog("INFO", "Restauration photos...");
                main.RunAdb($"push \"{photosPath}\" /sdcard/DCIM");
                main.AddLog("OK", "Photos restaurées");
            }

            string contactsPath = Path.Combine(src, "contacts.vcf");
            if (File.Exists(contactsPath))
            {
                main.AddLog("INFO", "Restauration contacts...");
                main.RunAdb($"push \"{contactsPath}\" /sdcard/contacts.vcf");
                main.AddLog("OK", "Contacts restaurés");
            }

            main.AddLog("OK", "Restauration terminée!");
            MessageBox.Show("Restauration terminée!",
                "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnFormatFlash_Click(object sender, RoutedEventArgs e)
        {
            var main = (MainWindow)Window.GetWindow(this);
            if (!main.DeviceConnected)
            { main.AddLog("WARN", "Aucun appareil connecté"); return; }

            var result = MessageBox.Show(
                "Cette opération va effectuer un factory reset.\nToutes les données seront supprimées.\n\nÊtes-vous sûr?",
                "Confirmation", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                main.AddLog("INFO", "Format en cours...");
                main.RunAdb("shell am broadcast -a android.intent.action.MASTER_CLEAR");
                main.AddLog("OK", "Format lancé — l'appareil va redémarrer");
            }
        }
    }
}

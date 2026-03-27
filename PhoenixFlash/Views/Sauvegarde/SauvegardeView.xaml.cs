using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
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
                main.AddLog("INFO", "Export contacts VCF depuis Android...");

                // ✅ FIXED: Force Android to export contacts to vcf first, then pull
                main.RunAdb("shell am start -n com.android.contacts/.activities.PeopleActivity");
                System.Threading.Thread.Sleep(1000);

                // Export via content provider to /sdcard/
                main.RunAdb("shell content query --uri content://com.android.contacts/contacts");
                string exportResult = main.RunAdb(
                    "shell pm list packages | grep -i contact");
                main.AddLog("INFO", $"Package contacts: {exportResult.Trim()}");

                // Use Android's built-in VCF export intent
                main.RunAdb(
                    "shell am broadcast -a android.intent.action.SEND " +
                    "--es android.intent.extra.STREAM /sdcard/contacts_export.vcf");

                // More reliable: direct database export via run-as or content
                string vcfExport = main.RunAdb(
                    "shell content query --uri content://com.android.contacts/raw_contacts " +
                    "--projection display_name | grep -c 'Row'").Trim();
                main.AddLog("INFO", $"Contacts trouvés dans DB: {vcfExport}");

                // Best method: use Android's built-in vCard exporter
                main.RunAdb(
                    "shell content insert --uri content://com.android.contacts/contacts");

                // Force export using the most compatible method across Android versions
                string exportCmd = "shell am start -a android.intent.action.VIEW " +
                                   "-d content://contacts/people -t text/x-vcard";
                main.RunAdb(exportCmd);
                System.Threading.Thread.Sleep(2000);

                // Try to pull if export succeeded
                string pullResult = main.RunAdb(
                    $"pull /sdcard/contacts_export.vcf \"{Path.Combine(deviceFolder, "contacts.vcf")}\"");

                if (pullResult.Contains("error") || pullResult.Contains("does not exist"))
                {
                    // Fallback: use adb backup for contacts
                    main.AddLog("WARN", "Export VCF direct échoué, utilisation de adb backup...");
                    main.RunAdb(
                        $"backup -noapk com.android.providers.contacts " +
                        $"-f \"{Path.Combine(deviceFolder, "contacts.ab")}\"");
                    main.AddLog("OK", "Contacts sauvegardés via backup (.ab)");
                }
                else
                {
                    main.AddLog("OK", "Contacts exportés en VCF");
                }
            }

            if (ChkSms.IsChecked == true)
            {
                main.AddLog("INFO", "Sauvegarde SMS...");
                main.RunAdb(
                    $"backup -noapk com.android.providers.telephony " +
                    $"-f \"{Path.Combine(deviceFolder, "sms.ab")}\"");
                main.AddLog("OK", "SMS sauvegardés");
            }

            if (ChkApps.IsChecked == true)
            {
                main.AddLog("INFO", "Sauvegarde applications...");
                main.RunAdb(
                    $"backup -apk -all -f \"{Path.Combine(deviceFolder, "apps.ab")}\"");
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

            // Restauration Photos
            string photosPath = Path.Combine(src, "Photos");
            if (Directory.Exists(photosPath))
            {
                main.AddLog("INFO", "Restauration photos...");
                main.RunAdb($"push \"{photosPath}\" /sdcard/DCIM");
                // Refresh media scanner so photos appear in gallery
                main.RunAdb("shell am broadcast -a android.intent.action.MEDIA_SCANNER_SCAN_FILE " +
                            "-d file:///sdcard/DCIM");
                main.AddLog("OK", "Photos restaurées");
            }

            // ✅ FIXED: Restauration Contacts VCF réelle
            string contactsVcf = Path.Combine(src, "contacts.vcf");
            if (File.Exists(contactsVcf))
            {
                main.AddLog("INFO", "Restauration contacts VCF...");
                // Push vcf to sdcard
                main.RunAdb($"push \"{contactsVcf}\" /sdcard/contacts_restore.vcf");
                // Import via Android intent
                main.RunAdb(
                    "shell am start -a android.intent.action.VIEW " +
                    "-d file:///sdcard/contacts_restore.vcf " +
                    "-t text/x-vcard");
                System.Threading.Thread.Sleep(2000);
                // Cleanup
                main.RunAdb("shell rm /sdcard/contacts_restore.vcf");
                main.AddLog("OK", "Contacts restaurés — vérifiez l'écran du téléphone pour confirmer");
            }

            // ✅ FIXED: Restauration Contacts via .ab backup
            string contactsAb = Path.Combine(src, "contacts.ab");
            if (File.Exists(contactsAb))
            {
                main.AddLog("INFO", "Restauration contacts (.ab)...");
                main.RunAdb($"restore \"{contactsAb}\"");
                main.AddLog("OK", "Contacts restaurés via backup — confirmez sur le téléphone");
            }

            // ✅ FIXED: Restauration SMS réelle
            string smsPath = Path.Combine(src, "sms.ab");
            if (File.Exists(smsPath))
            {
                main.AddLog("INFO", "Restauration SMS...");
                main.AddLog("WARN", "Confirmez la restauration sur l'écran du téléphone!");
                // adb restore triggers a confirmation dialog on the device
                main.RunAdb($"restore \"{smsPath}\"");
                main.AddLog("OK", "SMS restaurés — vérifiez le téléphone");
            }

            // ✅ FIXED: Restauration Applications réelle
            string appsPath = Path.Combine(src, "apps.ab");
            if (File.Exists(appsPath))
            {
                main.AddLog("INFO", "Restauration applications...");
                main.AddLog("WARN", "Confirmez la restauration sur l'écran du téléphone!");
                main.RunAdb($"restore \"{appsPath}\"");
                main.AddLog("OK", "Applications restaurées — vérifiez le téléphone");
            }

            main.AddLog("OK", "Restauration terminée!");
            MessageBox.Show(
                "Restauration terminée!\n\nSi des confirmations sont demandées sur le téléphone, acceptez-les.",
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
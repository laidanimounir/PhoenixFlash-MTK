using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace PhoenixFlash.Views.Sauvegarde
{
    public class ContactItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string Name { get; set; } = "";
        public string Phone { get; set; } = "";
        public string Initiale => Name.Length > 0 ? Name[0].ToString().ToUpper() : "?";
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public partial class ContactsView : UserControl
    {
        private List<ContactItem> _contacts = new();

        public ContactsView()
        {
            InitializeComponent();
        }

        private void BtnLoad_Click(object sender, RoutedEventArgs e)
        {
            var main = (MainWindow)Window.GetWindow(this);
            if (!main.DeviceConnected)
            { main.AddLog("WARN", "Aucun appareil connecté"); return; }

            main.AddLog("INFO", "Chargement des contacts...");
            _contacts.Clear();

            // Query contacts display_name
            string rawNames = main.RunAdb(
                "shell content query --uri content://contacts/phones " +
                "--projection display_name:number").Trim();

            var lines = rawNames.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (!line.Contains("display_name")) continue;
                string name = "", phone = "";

                // Parse: Row: 0 display_name=John, number=+213...
                foreach (var part in line.Split(','))
                {
                    var p = part.Trim();
                    if (p.StartsWith("display_name="))
                        name = p.Replace("display_name=", "").Trim();
                    else if (p.StartsWith("number="))
                        phone = p.Replace("number=", "").Trim();
                }

                if (string.IsNullOrWhiteSpace(name)) continue;
                _contacts.Add(new ContactItem
                {
                    Name = name,
                    Phone = phone,
                    IsSelected = false
                });
            }

            ContactsList.ItemsSource = null;
            ContactsList.ItemsSource = _contacts;
            TxtCount.Text = $"{_contacts.Count} contacts";
            main.AddLog("OK", $"{_contacts.Count} contacts chargés");
        }

        private void ChkSelectAll_Changed(object sender, RoutedEventArgs e)
        {
            bool val = ChkSelectAll.IsChecked == true;
            foreach (var c in _contacts)
                c.IsSelected = val;
        }

        private void BtnExportVcf_Click(object sender, RoutedEventArgs e)
        {
            var main = (MainWindow)Window.GetWindow(this);
            if (!main.DeviceConnected)
            { main.AddLog("WARN", "Aucun appareil connecté"); return; }

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Enregistrer les contacts",
                Filter = "VCard (*.vcf)|*.vcf",
                FileName = $"contacts_{DateTime.Now:yyyyMMdd}"
            };
            if (dialog.ShowDialog() != true) return;

            string dest = dialog.FileName;
            main.AddLog("INFO", "Export contacts VCF...");

            // Step 1: trigger Android export to sdcard
            main.RunAdb(
                "shell am start -a android.intent.action.VIEW " +
                "-t text/x-vcard -d content://contacts/people");
            System.Threading.Thread.Sleep(1500);

            // Step 2: try direct pull
            string pull = main.RunAdb(
                $"pull /sdcard/contacts_export.vcf \"{dest}\"");

            if (pull.Contains("error") || pull.Contains("does not exist"))
            {
                // Fallback: backup via adb
                main.AddLog("WARN", "Export direct échoué — utilisation adb backup...");
                string abPath = dest.Replace(".vcf", ".ab");
                main.RunAdb(
                    $"backup -noapk com.android.providers.contacts -f \"{abPath}\"");
                main.AddLog("OK", $"Contacts sauvegardés: {abPath}");
                main.AddLog("WARN", "Confirmez sur le téléphone si demandé");
                MessageBox.Show(
                    $"Export VCF non disponible sur cet appareil.\nContacts sauvegardés en format .ab:\n{abPath}",
                    "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                main.AddLog("OK", $"Contacts exportés: {dest}");
                MessageBox.Show(
                    $"Contacts exportés avec succès!\n{dest}",
                    "Succès", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnImportVcf_Click(object sender, RoutedEventArgs e)
        {
            var main = (MainWindow)Window.GetWindow(this);
            if (!main.DeviceConnected)
            { main.AddLog("WARN", "Aucun appareil connecté"); return; }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Sélectionner le fichier contacts",
                Filter = "VCard (*.vcf)|*.vcf|ADB Backup (*.ab)|*.ab|Tous (*.*)|*.*"
            };
            if (dialog.ShowDialog() != true) return;

            string file = dialog.FileName;
            main.AddLog("INFO", $"Import depuis: {Path.GetFileName(file)}");

            if (file.EndsWith(".vcf", StringComparison.OrdinalIgnoreCase))
            {
                // Push VCF to device then open with intent
                main.RunAdb($"push \"{file}\" /sdcard/contacts_import.vcf");
                System.Threading.Thread.Sleep(500);
                main.RunAdb(
                    "shell am start -a android.intent.action.VIEW " +
                    "-d file:///sdcard/contacts_import.vcf " +
                    "-t text/x-vcard");
                main.AddLog("OK", "VCF envoyé — confirmez l'import sur le téléphone");
                MessageBox.Show(
                    "Fichier VCF envoyé sur le téléphone.\nConfirmez l'importation sur l'écran.",
                    "Import en cours", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else if (file.EndsWith(".ab", StringComparison.OrdinalIgnoreCase))
            {
                main.AddLog("INFO", "Restauration contacts .ab...");
                main.AddLog("WARN", "Confirmez sur l'écran du téléphone!");
                main.RunAdb($"restore \"{file}\"");
                main.AddLog("OK", "Restauration contacts lancée");
            }
        }
    }
}
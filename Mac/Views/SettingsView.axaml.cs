using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using eCheque.MICO360.Mac.ViewModels;

namespace eCheque.MICO360.Mac.Views
{
    public partial class SettingsView : UserControl
    {
        public SettingsView() => InitializeComponent();

        async void OnBrowsePdf(object? sender, RoutedEventArgs e)
        {
            var path = await PickFolderAsync("Select PDF output folder");
            if (path != null && DataContext is SettingsViewModel vm) vm.PdfPath = path;
        }

        async void OnBrowseBackup(object? sender, RoutedEventArgs e)
        {
            var path = await PickFolderAsync("Select backup folder");
            if (path != null && DataContext is SettingsViewModel vm) vm.BackupPath = path;
        }

        async System.Threading.Tasks.Task<string?> PickFolderAsync(string title)
        {
            var top = TopLevel.GetTopLevel(this);
            if (top == null) return null;
            var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            });
            var folder = folders?.FirstOrDefault();
            if (folder == null) return null;
            try { return folder.Path.LocalPath; }
            catch { return folder.Path.ToString(); }
        }
    }
}

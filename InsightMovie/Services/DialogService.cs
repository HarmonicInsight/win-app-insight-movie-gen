using System.Windows;
using Microsoft.Win32;
using InsightMovie.Core;
using InsightMovie.Models;
using InsightMovie.Views;

namespace InsightMovie.Services
{
    public class DialogService : IDialogService
    {
        private readonly Window _owner;

        public DialogService(Window owner)
        {
            _owner = owner;
        }

        public string? ShowOpenFileDialog(string title, string filter, string? defaultExt = null)
        {
            var dlg = new OpenFileDialog { Title = title, Filter = filter };
            if (defaultExt != null) dlg.DefaultExt = defaultExt;
            return dlg.ShowDialog(_owner) == true ? dlg.FileName : null;
        }

        public string? ShowSaveFileDialog(string title, string filter, string? defaultExt = null, string? fileName = null)
        {
            var dlg = new SaveFileDialog { Title = title, Filter = filter };
            if (defaultExt != null) dlg.DefaultExt = defaultExt;
            if (fileName != null) dlg.FileName = fileName;
            return dlg.ShowDialog(_owner) == true ? dlg.FileName : null;
        }

        public bool ShowConfirmation(string message, string title)
        {
            return MessageBox.Show(_owner, message, title,
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
        }

        public void ShowInfo(string message, string title)
        {
            MessageBox.Show(_owner, message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void ShowWarning(string message, string title)
        {
            MessageBox.Show(_owner, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public void ShowError(string message, string title)
        {
            MessageBox.Show(_owner, message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public bool ShowYesNo(string message, string title)
        {
            return MessageBox.Show(_owner, message, title,
                MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes;
        }

        public BGMSettings? ShowBgmDialog(BGMSettings? currentSettings)
        {
            var dlg = new BGMDialog(currentSettings) { Owner = _owner };
            return dlg.ShowDialog() == true ? dlg.GetSettings() : null;
        }

        public TextStyle? ShowTextStyleDialog(TextStyle? currentStyle)
        {
            var dlg = new TextStyleDialog(currentStyle) { Owner = _owner };
            return dlg.ShowDialog() == true ? dlg.GetSelectedStyle() : null;
        }

        public void ShowLicenseDialog(Config config)
        {
            var dlg = new LicenseDialog(config) { Owner = _owner };
            dlg.ShowDialog();
        }
    }
}

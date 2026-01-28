using System.Windows;
using Microsoft.Win32;
using InsightMovie.Core;
using InsightMovie.Models;
using InsightMovie.Views;
using InsightCommon.License;
using InsightCommon.UI;
using InsightCommon.Theme;

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
            // InsightCommon 共通ライセンスダイアログを使用
            var licenseManager = new InsightLicenseManager("INMV", "InsightMovie");
            var dialog = new InsightLicenseDialog(new LicenseDialogOptions
            {
                ProductCode = "INMV",
                ProductName = "InsightMovie",
                ThemeMode = InsightThemeMode.Light,
                Locale = "ja",
                LicenseManager = licenseManager,
                Features = new[]
                {
                    new FeatureDefinition("subtitle", "字幕機能"),
                    new FeatureDefinition("subtitle_style", "字幕スタイル選択"),
                    new FeatureDefinition("transition", "トランジション効果"),
                    new FeatureDefinition("pptx_import", "PPTX取込"),
                },
                FeatureMatrix = new Dictionary<string, InsightCommon.License.PlanCode[]>
                {
                    ["subtitle"]       = new[] { InsightCommon.License.PlanCode.Trial, InsightCommon.License.PlanCode.Pro, InsightCommon.License.PlanCode.Ent },
                    ["subtitle_style"] = new[] { InsightCommon.License.PlanCode.Trial, InsightCommon.License.PlanCode.Pro, InsightCommon.License.PlanCode.Ent },
                    ["transition"]     = new[] { InsightCommon.License.PlanCode.Trial, InsightCommon.License.PlanCode.Pro, InsightCommon.License.PlanCode.Ent },
                    ["pptx_import"]    = new[] { InsightCommon.License.PlanCode.Trial, InsightCommon.License.PlanCode.Pro, InsightCommon.License.PlanCode.Ent },
                },
            });
            dialog.Owner = _owner;
            dialog.ShowDialog();

            // 共通ライセンスマネージャーの結果をアプリConfigに同期
            var license = licenseManager.CurrentLicense;
            if (license.IsValid && !string.IsNullOrEmpty(license.Key))
            {
                config.BeginUpdate();
                config.LicenseKey = license.Key;
                config.LicenseEmail = license.Email ?? "";
                config.EndUpdate();
            }
        }
    }
}

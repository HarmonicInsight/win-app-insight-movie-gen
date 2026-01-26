using System;
using System.Globalization;
using System.Windows;
using Microsoft.Win32;
using InsightMovie.Models;

namespace InsightMovie.Views
{
    public partial class BGMDialog : Window
    {
        private BGMSettings _settings;

        public BGMDialog()
        {
            InitializeComponent();
            _settings = new BGMSettings();
            Loaded += BGMDialog_Loaded;
        }

        public BGMDialog(BGMSettings? initialSettings) : this()
        {
            if (initialSettings != null)
            {
                _settings = new BGMSettings
                {
                    FilePath = initialSettings.FilePath,
                    Volume = initialSettings.Volume,
                    FadeInEnabled = initialSettings.FadeInEnabled,
                    FadeInDuration = initialSettings.FadeInDuration,
                    FadeInType = initialSettings.FadeInType,
                    FadeOutEnabled = initialSettings.FadeOutEnabled,
                    FadeOutDuration = initialSettings.FadeOutDuration,
                    FadeOutType = initialSettings.FadeOutType,
                    LoopEnabled = initialSettings.LoopEnabled,
                    DuckingEnabled = initialSettings.DuckingEnabled,
                    DuckingVolume = initialSettings.DuckingVolume,
                    DuckingAttack = initialSettings.DuckingAttack,
                    DuckingRelease = initialSettings.DuckingRelease
                };
            }
        }

        public BGMSettings GetSettings()
        {
            ReadControlsIntoSettings();
            return new BGMSettings
            {
                FilePath = _settings.FilePath,
                Volume = _settings.Volume,
                FadeInEnabled = _settings.FadeInEnabled,
                FadeInDuration = _settings.FadeInDuration,
                FadeInType = _settings.FadeInType,
                FadeOutEnabled = _settings.FadeOutEnabled,
                FadeOutDuration = _settings.FadeOutDuration,
                FadeOutType = _settings.FadeOutType,
                LoopEnabled = _settings.LoopEnabled,
                DuckingEnabled = _settings.DuckingEnabled,
                DuckingVolume = _settings.DuckingVolume,
                DuckingAttack = _settings.DuckingAttack,
                DuckingRelease = _settings.DuckingRelease
            };
        }

        private void BGMDialog_Loaded(object sender, RoutedEventArgs e)
        {
            LoadSettingsIntoControls();
        }

        private void LoadSettingsIntoControls()
        {
            FilePathLabel.Text = string.IsNullOrEmpty(_settings.FilePath)
                ? "（未選択）" : _settings.FilePath;

            MainVolumeSlider.Value = _settings.Volume * 100;
            MainVolumeLabel.Text = $"{(int)(_settings.Volume * 100)}%";

            DuckingCheckBox.IsChecked = _settings.DuckingEnabled;
            DuckingPanel.Visibility = _settings.DuckingEnabled ? Visibility.Visible : Visibility.Collapsed;
            DuckingVolumeSlider.Value = _settings.DuckingVolume * 100;
            DuckingVolumeLabel.Text = $"{(int)(_settings.DuckingVolume * 100)}%";
            AttackTextBox.Text = ((int)(_settings.DuckingAttack * 1000)).ToString();
            ReleaseTextBox.Text = ((int)(_settings.DuckingRelease * 1000)).ToString();

            FadeInCheckBox.IsChecked = _settings.FadeInEnabled;
            FadeInPanel.Visibility = _settings.FadeInEnabled ? Visibility.Visible : Visibility.Collapsed;
            FadeInDurationTextBox.Text = _settings.FadeInDuration.ToString("F1");
            SelectComboItem(FadeInTypeCombo, _settings.FadeInType == FadeType.Exponential ? "Exponential" : "Linear");

            FadeOutCheckBox.IsChecked = _settings.FadeOutEnabled;
            FadeOutPanel.Visibility = _settings.FadeOutEnabled ? Visibility.Visible : Visibility.Collapsed;
            FadeOutDurationTextBox.Text = _settings.FadeOutDuration.ToString("F1");
            SelectComboItem(FadeOutTypeCombo, _settings.FadeOutType == FadeType.Exponential ? "Exponential" : "Linear");

            LoopCheckBox.IsChecked = _settings.LoopEnabled;
        }

        private void ReadControlsIntoSettings()
        {
            _settings.Volume = MainVolumeSlider.Value / 100.0;
            _settings.DuckingEnabled = DuckingCheckBox.IsChecked == true;
            _settings.DuckingVolume = DuckingVolumeSlider.Value / 100.0;

            if (int.TryParse(AttackTextBox.Text, out int attack))
                _settings.DuckingAttack = Math.Clamp(attack, 0, 5000) / 1000.0;
            if (int.TryParse(ReleaseTextBox.Text, out int release))
                _settings.DuckingRelease = Math.Clamp(release, 0, 5000) / 1000.0;

            _settings.FadeInEnabled = FadeInCheckBox.IsChecked == true;
            if (double.TryParse(FadeInDurationTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double fadeInDur))
                _settings.FadeInDuration = Math.Clamp(fadeInDur, 0.1, 30.0);
            _settings.FadeInType = GetSelectedComboText(FadeInTypeCombo, "Linear") == "Exponential"
                ? FadeType.Exponential : FadeType.Linear;

            _settings.FadeOutEnabled = FadeOutCheckBox.IsChecked == true;
            if (double.TryParse(FadeOutDurationTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double fadeOutDur))
                _settings.FadeOutDuration = Math.Clamp(fadeOutDur, 0.1, 30.0);
            _settings.FadeOutType = GetSelectedComboText(FadeOutTypeCombo, "Linear") == "Exponential"
                ? FadeType.Exponential : FadeType.Linear;

            _settings.LoopEnabled = LoopCheckBox.IsChecked == true;
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "BGMファイルを選択",
                Filter = "音声ファイル|*.mp3;*.wav;*.ogg;*.m4a;*.aac;*.flac;*.wma|すべてのファイル|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                _settings.FilePath = dialog.FileName;
                FilePathLabel.Text = dialog.FileName;
            }
        }

        private void ClearFileButton_Click(object sender, RoutedEventArgs e)
        {
            _settings.FilePath = null;
            FilePathLabel.Text = "（未選択）";
        }

        private void MainVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (MainVolumeLabel != null)
                MainVolumeLabel.Text = $"{(int)MainVolumeSlider.Value}%";
        }

        private void DuckingCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (DuckingPanel != null)
                DuckingPanel.Visibility = DuckingCheckBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void DuckingVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (DuckingVolumeLabel != null)
                DuckingVolumeLabel.Text = $"{(int)DuckingVolumeSlider.Value}%";
        }

        private void AttackDown_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(AttackTextBox.Text, out int val))
                AttackTextBox.Text = Math.Max(0, val - 50).ToString();
        }

        private void AttackUp_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(AttackTextBox.Text, out int val))
                AttackTextBox.Text = Math.Min(5000, val + 50).ToString();
        }

        private void ReleaseDown_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(ReleaseTextBox.Text, out int val))
                ReleaseTextBox.Text = Math.Max(0, val - 50).ToString();
        }

        private void ReleaseUp_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(ReleaseTextBox.Text, out int val))
                ReleaseTextBox.Text = Math.Min(5000, val + 50).ToString();
        }

        private void FadeInCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (FadeInPanel != null)
                FadeInPanel.Visibility = FadeInCheckBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void FadeInDurationDown_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(FadeInDurationTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                FadeInDurationTextBox.Text = Math.Max(0.1, val - 0.5).ToString("F1");
        }

        private void FadeInDurationUp_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(FadeInDurationTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                FadeInDurationTextBox.Text = Math.Min(30.0, val + 0.5).ToString("F1");
        }

        private void FadeOutCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (FadeOutPanel != null)
                FadeOutPanel.Visibility = FadeOutCheckBox.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void FadeOutDurationDown_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(FadeOutDurationTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                FadeOutDurationTextBox.Text = Math.Max(0.1, val - 0.5).ToString("F1");
        }

        private void FadeOutDurationUp_Click(object sender, RoutedEventArgs e)
        {
            if (double.TryParse(FadeOutDurationTextBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double val))
                FadeOutDurationTextBox.Text = Math.Min(30.0, val + 0.5).ToString("F1");
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ReadControlsIntoSettings();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static void SelectComboItem(System.Windows.Controls.ComboBox combo, string value)
        {
            for (int i = 0; i < combo.Items.Count; i++)
            {
                if (combo.Items[i] is System.Windows.Controls.ComboBoxItem item
                    && item.Content?.ToString() == value)
                {
                    combo.SelectedIndex = i;
                    return;
                }
            }
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
        }

        private static string GetSelectedComboText(System.Windows.Controls.ComboBox combo, string defaultValue)
        {
            if (combo.SelectedItem is System.Windows.Controls.ComboBoxItem item)
                return item.Content?.ToString() ?? defaultValue;
            return defaultValue;
        }
    }
}

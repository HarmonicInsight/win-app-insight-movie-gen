using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using InsightCast.Core;
using InsightCast.Models;
using InsightCast.Services;
using InsightCast.Video;
using InsightCast.ViewModels;
using InsightCast.VoiceVox;

namespace InsightCast.Views
{
    public partial class QuickModeWindow : Window
    {
        private readonly QuickModeViewModel _vm;
        private readonly VoiceVoxClient _voiceVoxClient;
        private readonly int _speakerId;
        private readonly FFmpegWrapper? _ffmpegWrapper;
        private readonly Config _config;

        private static readonly string[] SupportedExtensions =
        {
            ".pptx", ".png", ".jpg", ".jpeg", ".bmp", ".gif",
            ".mp4", ".avi", ".mov", ".wmv", ".mkv", ".txt", ".md"
        };

        public QuickModeWindow(VoiceVoxClient voiceVoxClient, int speakerId,
                               FFmpegWrapper? ffmpegWrapper, Config config)
        {
            InitializeComponent();

            _voiceVoxClient = voiceVoxClient;
            _speakerId = speakerId;
            _ffmpegWrapper = ffmpegWrapper;
            _config = config;

            _vm = new QuickModeViewModel(voiceVoxClient, speakerId, ffmpegWrapper, config);
            DataContext = _vm;

            // Set version label dynamically
            var version = typeof(QuickModeWindow).Assembly.GetName().Version;
            if (version != null)
                VersionLabel.Text = $"v{version.Major}.{version.Minor}.{version.Build}";

            _vm.Logger.LogReceived += OnLogReceived;
            _vm.OpenEditorRequested += OnOpenEditorRequested;
            _vm.OpenFileRequested += OnOpenFileRequested;

            // Fix 2: Wire speaker preview audio events
            _vm.PlayAudioRequested += OnPlayAudioRequested;
            _vm.StopAudioRequested += OnStopAudioRequested;

            Loaded += async (_, _) =>
            {
                // Fix 3: Set DialogService
                _vm.SetDialogService(new DialogService(this));
                await _vm.InitializeAsync();
            };
        }

        #region Drag & Drop (Fix 7: overlay for second drop)

        private bool HasSupportedFiles(DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return false;
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            return files.Any(f =>
                SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));
        }

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (HasSupportedFiles(e))
            {
                e.Effects = DragDropEffects.Copy;

                if (_vm.HasProject)
                {
                    // Fix 7: Show overlay when project already loaded
                    DragOverlay.Visibility = Visibility.Visible;
                }
                else
                {
                    // Original: highlight drop zone border
                    DropZone.BorderBrush = (SolidColorBrush)FindResource("BrandPrimary");
                    DropZone.BorderThickness = new Thickness(3);
                    DropZone.Background = (SolidColorBrush)FindResource("BrandLight");
                }
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void Window_DragLeave(object sender, DragEventArgs e)
        {
            DragOverlay.Visibility = Visibility.Collapsed;

            if (DropZone.Visibility == Visibility.Visible)
            {
                DropZone.BorderBrush = (SolidColorBrush)FindResource("BorderDark");
                DropZone.BorderThickness = new Thickness(2);
                DropZone.Background = (SolidColorBrush)FindResource("BgSecondary");
            }
        }

        private async void Window_Drop(object sender, DragEventArgs e)
        {
            // Reset all visual states
            DragOverlay.Visibility = Visibility.Collapsed;
            if (DropZone.Visibility == Visibility.Visible)
            {
                DropZone.BorderBrush = (SolidColorBrush)FindResource("BorderDark");
                DropZone.BorderThickness = new Thickness(2);
                DropZone.Background = (SolidColorBrush)FindResource("BgSecondary");
            }

            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            var supported = files
                .Where(f => SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .ToArray();

            if (supported.Length == 0) return;

            await _vm.HandleFileDropAsync(supported);
        }

        #endregion

        #region File Selection

        private async void SelectFile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = LocalizationService.GetString("QVM.SelectFile"),
                Filter = LocalizationService.GetString("QVM.FileFilter"),
                Multiselect = true
            };

            if (dialog.ShowDialog(this) == true && dialog.FileNames.Length > 0)
            {
                await _vm.HandleFileDropAsync(dialog.FileNames);
            }
        }

        #endregion

        #region Audio (Fix 2: speaker preview)

        private void OnPlayAudioRequested(string path)
        {
            Dispatcher.Invoke(() =>
            {
                AudioPlayer.Source = new Uri(path, UriKind.Absolute);
                AudioPlayer.Play();
            });
        }

        private void OnStopAudioRequested()
        {
            Dispatcher.Invoke(() =>
            {
                AudioPlayer.Stop();
                AudioPlayer.Source = null;
            });
        }

        private void AudioPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            AudioPlayer.Stop();
            AudioPlayer.Source = null;
        }

        #endregion

        #region Events

        private void OnOpenEditorRequested(Project project)
        {
            OnStopAudioRequested();
            var mainWindow = new MainWindow(_voiceVoxClient, _speakerId, _ffmpegWrapper, _config);
            mainWindow.LoadProject(project);
            mainWindow.Show();
            Close();
        }

        private void OnOpenFileRequested(string filePath)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _vm.Logger.LogError(LocalizationService.GetString("VM.File.OpenError"), ex);
            }
        }

        private const int MaxLogLength = 100_000;

        private void OnLogReceived(string message)
        {
            Dispatcher.Invoke(() =>
            {
                if (LogTextBox.Text.Length > MaxLogLength)
                {
                    LogTextBox.Text = LogTextBox.Text[^(MaxLogLength / 2)..];
                }
                if (LogTextBox.Text.Length > 0) LogTextBox.AppendText(Environment.NewLine);
                LogTextBox.AppendText(message);
                LogTextBox.ScrollToEnd();
            });
        }

        #endregion

        #region Title Bar

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
            => WindowState = WindowState.Minimized;

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal : WindowState.Maximized;
            UpdateMaximizeIcon();
        }

        private void UpdateMaximizeIcon()
        {
            if (MaximizeIcon != null)
            {
                MaximizeIcon.Data = WindowState == WindowState.Maximized
                    ? System.Windows.Media.Geometry.Parse("M3,3 L3,0 L10,0 L10,7 L7,7 M0,3 L7,3 L7,10 L0,10 Z")
                    : System.Windows.Media.Geometry.Parse("M0,0 L10,0 L10,10 L0,10 Z");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
            => Close();

        private void LangSwitchButton_Click(object sender, RoutedEventArgs e)
        {
            var newLang = LocalizationService.ToggleLanguage();
            _config.Language = newLang;
        }

        #endregion

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_vm.CanClose())
            {
                e.Cancel = true;
                return;
            }

            OnStopAudioRequested();

            // Unsubscribe event handlers to prevent memory leaks
            _vm.Logger.LogReceived -= OnLogReceived;
            _vm.OpenEditorRequested -= OnOpenEditorRequested;
            _vm.OpenFileRequested -= OnOpenFileRequested;
            _vm.PlayAudioRequested -= OnPlayAudioRequested;
            _vm.StopAudioRequested -= OnStopAudioRequested;
        }
    }
}

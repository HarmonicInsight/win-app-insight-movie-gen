using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using InsightMovie.Core;
using InsightMovie.Models;
using InsightMovie.Video;
using InsightMovie.ViewModels;
using InsightMovie.VoiceVox;

namespace InsightMovie.Views
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

            _vm.Logger.LogReceived += OnLogReceived;
            _vm.OpenEditorRequested += OnOpenEditorRequested;
            _vm.OpenFileRequested += OnOpenFileRequested;

            Loaded += async (_, _) => await _vm.InitializeAsync();
        }

        #region Drag & Drop

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                bool hasSupported = files.Any(f =>
                    SupportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

                if (hasSupported)
                {
                    e.Effects = DragDropEffects.Copy;
                    DropZone.BorderBrush = (SolidColorBrush)FindResource("BrandPrimary");
                    DropZone.BorderThickness = new Thickness(3);
                    DropZone.Background = (SolidColorBrush)FindResource("BrandLight");
                }
                else
                {
                    e.Effects = DragDropEffects.None;
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
            DropZone.BorderBrush = (SolidColorBrush)FindResource("BorderDark");
            DropZone.BorderThickness = new Thickness(2);
            DropZone.Background = (SolidColorBrush)FindResource("BgSecondary");
        }

        private async void Window_Drop(object sender, DragEventArgs e)
        {
            // Reset visual state
            DropZone.BorderBrush = (SolidColorBrush)FindResource("BorderDark");
            DropZone.BorderThickness = new Thickness(2);
            DropZone.Background = (SolidColorBrush)FindResource("BgSecondary");

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
                Title = "資料ファイルを選択",
                Filter = "対応ファイル|*.pptx;*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.mp4;*.avi;*.mov;*.txt;*.md|" +
                         "PowerPoint|*.pptx|画像|*.png;*.jpg;*.jpeg;*.bmp;*.gif|" +
                         "動画|*.mp4;*.avi;*.mov|テキスト|*.txt;*.md|" +
                         "すべてのファイル|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog(this) == true && dialog.FileNames.Length > 0)
            {
                await _vm.HandleFileDropAsync(dialog.FileNames);
            }
        }

        #endregion

        #region Events

        private void OnOpenEditorRequested(Project project)
        {
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
            catch { }
        }

        private void OnLogReceived(string message)
        {
            Dispatcher.Invoke(() =>
            {
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
            => WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal : WindowState.Maximized;

        private void CloseButton_Click(object sender, RoutedEventArgs e)
            => Close();

        #endregion

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_vm.CanClose())
                e.Cancel = true;
        }
    }
}

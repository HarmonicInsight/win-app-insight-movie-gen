using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using InsightMovie.Core;
using InsightMovie.Models;
using InsightMovie.Services;
using InsightMovie.Video;
using InsightMovie.ViewModels;
using InsightMovie.VoiceVox;

namespace InsightMovie.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainWindowViewModel _vm;

        public MainWindow(VoiceVoxClient voiceVoxClient, int speakerId,
                          FFmpegWrapper? ffmpegWrapper, Config config)
        {
            InitializeComponent();

            _vm = new MainWindowViewModel(voiceVoxClient, speakerId, ffmpegWrapper, config);
            DataContext = _vm;

            // Wire up ViewModel events for UI-specific operations
            _vm.PlayAudioRequested += OnPlayAudioRequested;
            _vm.StopAudioRequested += OnStopAudioRequested;
            _vm.ThumbnailUpdateRequested += OnThumbnailUpdateRequested;
            _vm.StylePreviewUpdateRequested += OnStylePreviewUpdateRequested;
            _vm.OpenFileRequested += OnOpenFileRequested;
            _vm.ExitRequested += () => Close();

            // Wire up logger to log TextBox
            _vm.Logger.LogReceived += OnLogReceived;

            Loaded += async (_, _) =>
            {
                _vm.SetDialogService(new DialogService(this));
                await _vm.InitializeAsync();
            };
        }

        /// <summary>
        /// Loads an externally-created project (e.g. from QuickMode) into the editor.
        /// </summary>
        public void LoadProject(Project project)
        {
            _vm.LoadProject(project);
        }

        #region ViewModel Event Handlers (UI-specific)

        private void OnPlayAudioRequested(string path, double speed)
        {
            Dispatcher.Invoke(() =>
            {
                double actualSpeed = speed;
                if (SpeedComboBox.SelectedItem is ComboBoxItem speedItem &&
                    speedItem.Tag is string tagStr &&
                    double.TryParse(tagStr, out var parsed))
                    actualSpeed = parsed;

                AudioPlayer.SpeedRatio = actualSpeed;
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

        private void OnThumbnailUpdateRequested(string? imagePath)
        {
            Dispatcher.Invoke(() =>
            {
                if (string.IsNullOrEmpty(imagePath) || !File.Exists(imagePath))
                {
                    PreviewImage.Source = null;
                    return;
                }
                try
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(imagePath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = 240;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    PreviewImage.Source = bitmap;
                }
                catch
                {
                    PreviewImage.Source = null;
                }
            });
        }

        private void OnStylePreviewUpdateRequested(TextStyle style)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    var textColor = Color.FromRgb(
                        (byte)style.TextColor[0],
                        (byte)style.TextColor[1],
                        (byte)style.TextColor[2]);
                    StylePreviewLabel.Foreground = new SolidColorBrush(textColor);
                    StylePreviewLabel.FontWeight = style.FontBold ? FontWeights.Bold : FontWeights.Normal;
                }
                catch
                {
                    StylePreviewLabel.Foreground = Brushes.White;
                }
            });
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
                _vm.Logger.LogError("ファイルを開けませんでした", ex);
            }
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

        #region Media Element Events

        private void AudioPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            AudioPlayer.Stop();
            AudioPlayer.Source = null;
        }

        #endregion

        #region ApplicationCommand Handlers (delegate to ViewModel)

        private void NewProject_Executed(object sender, ExecutedRoutedEventArgs e)
            => _vm.NewProjectCommand.Execute(null);

        private void OpenProject_Executed(object sender, ExecutedRoutedEventArgs e)
            => _vm.OpenProjectCommand.Execute(null);

        private void SaveProject_Executed(object sender, ExecutedRoutedEventArgs e)
            => _vm.SaveProjectCommand.Execute(null);

        #endregion

        #region Keyboard Shortcuts

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                _vm.SaveProjectAsCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _vm.AddSceneCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Delete && Keyboard.Modifiers == ModifierKeys.None
                && Keyboard.FocusedElement is not TextBox)
            {
                _vm.RemoveSceneCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Up && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _vm.MoveSceneUpCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Down && Keyboard.Modifiers == ModifierKeys.Control)
            {
                _vm.MoveSceneDownCommand.Execute(null);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.F1 && Keyboard.Modifiers == ModifierKeys.None)
            {
                _vm.ShowTutorialCommand.Execute(null);
                e.Handled = true;
            }
        }

        #endregion

        #region Custom Title Bar

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            // Update maximize button icon: restore ↔ maximize
            if (MaximizeIcon != null)
            {
                MaximizeIcon.Data = Geometry.Parse(
                    WindowState == WindowState.Maximized
                        ? "M0,2 H8 V10 H0 Z M2,2 V0 H10 V8 H8"   // Restore (two overlapping squares)
                        : "M0,0 H10 V10 H0 Z");                     // Maximize (single square)
                MaximizeButton.ToolTip = WindowState == WindowState.Maximized
                    ? "元に戻す"
                    : "最大化";
            }
        }

        #endregion

        #region Window Lifecycle

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!_vm.CanClose())
            {
                e.Cancel = true;
                return;
            }
            OnStopAudioRequested();
        }

        #endregion
    }
}

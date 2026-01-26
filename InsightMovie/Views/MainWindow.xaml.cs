using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using InsightMovie.Core;
using InsightMovie.Models;
using InsightMovie.Video;
using InsightMovie.VoiceVox;

namespace InsightMovie.Views
{
    public class SpeakerItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public int StyleId { get; set; }
        public override string ToString() => DisplayName;
    }

    public partial class MainWindow : Window
    {
        #region Fields

        private readonly VoiceVoxClient _voiceVoxClient;
        private readonly int _defaultSpeakerId;
        private readonly FFmpegWrapper? _ffmpegWrapper;
        private readonly Config _config;

        private Models.Project _project;
        private Scene? _currentScene;
        private readonly AudioCache _audioCache;
        private Dictionary<int, string> _speakerStyles = new();
        private Models.TextStyle _defaultSubtitleStyle;
        private readonly Dictionary<string, Models.TextStyle> _sceneSubtitleStyles = new();

        private LicenseInfo? _licenseInfo;
        private PlanCode _currentPlan = PlanCode.Free;

        private bool _isLoadingScene;
        private bool _isExporting;
        private CancellationTokenSource? _exportCts;

        #endregion

        #region Constructor

        public MainWindow(VoiceVoxClient voiceVoxClient, int speakerId,
                          FFmpegWrapper? ffmpegWrapper, Config config)
        {
            InitializeComponent();

            _voiceVoxClient = voiceVoxClient;
            _defaultSpeakerId = speakerId;
            _ffmpegWrapper = ffmpegWrapper;
            _config = config;

            _audioCache = new AudioCache();
            _defaultSubtitleStyle = Models.TextStyle.PRESET_STYLES[0];

            _project = new Models.Project();
            _project.InitializeDefaultScenes();

            UpdateStatusLabel();
            LoadLicense();
            PopulateTransitionCombo();
            LoadSceneList();

            if (SceneListBox.Items.Count > 0)
                SceneListBox.SelectedIndex = 0;

            Loaded += async (_, _) => await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            await LoadSpeakers();
            LoadSceneSpeakers();
            LogMessage("初期化完了");
        }

        #endregion

        #region Status

        private void UpdateStatusLabel()
        {
            var vvStatus = "VOICEVOX: ✓接続OK";
            var ffStatus = _ffmpegWrapper?.CheckAvailable() == true
                ? "ffmpeg: ✓検出OK"
                : "ffmpeg: ✗未検出";
            StatusLabel.Text = $"{vvStatus} • {ffStatus}";
        }

        #endregion

        #region Scene List Management

        private void LoadSceneList()
        {
            _isLoadingScene = true;
            var selectedIndex = SceneListBox.SelectedIndex;
            SceneListBox.Items.Clear();

            for (int i = 0; i < _project.Scenes.Count; i++)
            {
                var scene = _project.Scenes[i];
                var label = $"シーン {i + 1}";
                if (!string.IsNullOrEmpty(scene.NarrationText))
                {
                    var preview = scene.NarrationText.Length > 12
                        ? scene.NarrationText[..12] + "..."
                        : scene.NarrationText;
                    label += $" - {preview}";
                }
                SceneListBox.Items.Add(new ListBoxItem { Content = label, Tag = scene });
            }

            if (selectedIndex >= 0 && selectedIndex < SceneListBox.Items.Count)
                SceneListBox.SelectedIndex = selectedIndex;
            else if (SceneListBox.Items.Count > 0)
                SceneListBox.SelectedIndex = 0;

            _isLoadingScene = false;
        }

        private void SceneListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingScene) return;
            OnSceneSelected();
        }

        private void OnSceneSelected()
        {
            if (SceneListBox.SelectedItem is not ListBoxItem item) return;
            if (item.Tag is not Scene scene) return;

            _isLoadingScene = true;
            _currentScene = scene;

            if (scene.HasMedia)
            {
                MediaNameLabel.Text = Path.GetFileName(scene.MediaPath);
                UpdateThumbnail(scene.MediaPath);
            }
            else
            {
                MediaNameLabel.Text = "（未選択）";
                PreviewImage.Source = null;
            }

            NarrationTextBox.Text = scene.NarrationText ?? string.Empty;
            NarrationPlaceholder.Visibility = string.IsNullOrEmpty(scene.NarrationText)
                ? Visibility.Visible : Visibility.Collapsed;

            SelectSceneSpeaker(scene.SpeakerId);
            KeepAudioCheckBox.IsChecked = scene.KeepOriginalAudio;

            SubtitleTextBox.Text = scene.SubtitleText ?? string.Empty;
            SubtitlePlaceholder.Visibility = string.IsNullOrEmpty(scene.SubtitleText)
                ? Visibility.Visible : Visibility.Collapsed;
            SubtitleOverlayLabel.Text = scene.SubtitleText ?? string.Empty;

            UpdateStylePreview();

            if (scene.DurationMode == DurationMode.Fixed)
            {
                FixedDurationRadio.IsChecked = true;
                AutoDurationRadio.IsChecked = false;
            }
            else
            {
                AutoDurationRadio.IsChecked = true;
                FixedDurationRadio.IsChecked = false;
            }
            DurationSecondsTextBox.Text = scene.FixedSeconds.ToString("F1", CultureInfo.InvariantCulture);

            SelectTransition(scene.TransitionType);
            TransitionDurationTextBox.Text = scene.TransitionDuration.ToString("F1", CultureInfo.InvariantCulture);

            _isLoadingScene = false;
        }

        private void AddScene_Click(object sender, RoutedEventArgs e) => AddScene();

        private void AddScene()
        {
            _project.AddScene();
            LoadSceneList();
            SceneListBox.SelectedIndex = _project.Scenes.Count - 1;
            LogMessage($"シーン {_project.Scenes.Count} を追加しました。");
        }

        private void RemoveScene_Click(object sender, RoutedEventArgs e) => RemoveScene();

        private void RemoveScene()
        {
            if (_project.Scenes.Count <= 1)
            {
                MessageBox.Show(this, "最低1つのシーンが必要です。", "削除不可",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var idx = SceneListBox.SelectedIndex;
            if (idx < 0) return;

            _project.RemoveScene(idx);
            LoadSceneList();
            LogMessage($"シーン {idx + 1} を削除しました。");
        }

        private void MoveSceneUp_Click(object sender, RoutedEventArgs e) => MoveScene(-1);
        private void MoveSceneDown_Click(object sender, RoutedEventArgs e) => MoveScene(1);

        private void MoveScene(int direction)
        {
            var idx = SceneListBox.SelectedIndex;
            if (idx < 0) return;

            int newIdx = idx + direction;
            if (newIdx < 0 || newIdx >= _project.Scenes.Count) return;

            _project.MoveScene(idx, newIdx);
            LoadSceneList();
            SceneListBox.SelectedIndex = newIdx;
        }

        #endregion

        #region Scene Editing

        private void OnNarrationChanged(object sender, TextChangedEventArgs e)
        {
            NarrationPlaceholder.Visibility = string.IsNullOrEmpty(NarrationTextBox.Text)
                ? Visibility.Visible : Visibility.Collapsed;

            if (_isLoadingScene || _currentScene == null) return;
            _currentScene.NarrationText = NarrationTextBox.Text;

            var idx = SceneListBox.SelectedIndex;
            if (idx >= 0 && idx < SceneListBox.Items.Count)
            {
                var label = $"シーン {idx + 1}";
                var text = _currentScene.NarrationText;
                if (!string.IsNullOrEmpty(text))
                {
                    var preview = text.Length > 12 ? text[..12] + "..." : text;
                    label += $" - {preview}";
                }
                ((ListBoxItem)SceneListBox.Items[idx]).Content = label;
            }
        }

        private void OnSubtitleChanged(object sender, TextChangedEventArgs e)
        {
            SubtitlePlaceholder.Visibility = string.IsNullOrEmpty(SubtitleTextBox.Text)
                ? Visibility.Visible : Visibility.Collapsed;

            if (_isLoadingScene || _currentScene == null) return;
            _currentScene.SubtitleText = SubtitleTextBox.Text;
            SubtitleOverlayLabel.Text = SubtitleTextBox.Text;
        }

        private void SelectMedia_Click(object sender, RoutedEventArgs e) => SelectMedia();

        private void SelectMedia()
        {
            if (_currentScene == null) return;

            var dlg = new OpenFileDialog
            {
                Title = "素材ファイルを選択",
                Filter = "画像・動画ファイル|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.mp4;*.avi;*.mov;*.wmv;*.mkv|" +
                         "画像ファイル|*.png;*.jpg;*.jpeg;*.bmp;*.gif|" +
                         "動画ファイル|*.mp4;*.avi;*.mov;*.wmv;*.mkv|" +
                         "すべてのファイル|*.*"
            };

            if (dlg.ShowDialog(this) != true) return;

            var ext = Path.GetExtension(dlg.FileName).ToLowerInvariant();
            var imageExts = new HashSet<string> { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };
            var videoExts = new HashSet<string> { ".mp4", ".avi", ".mov", ".wmv", ".mkv" };

            _currentScene.MediaPath = dlg.FileName;
            _currentScene.MediaType = imageExts.Contains(ext) ? MediaType.Image
                                    : videoExts.Contains(ext) ? MediaType.Video
                                    : MediaType.None;

            MediaNameLabel.Text = Path.GetFileName(dlg.FileName);
            OnMediaChanged();
            LogMessage($"素材を設定: {Path.GetFileName(dlg.FileName)}");
        }

        private void ClearMedia_Click(object sender, RoutedEventArgs e) => ClearMedia();

        private void ClearMedia()
        {
            if (_currentScene == null) return;
            _currentScene.MediaPath = null;
            _currentScene.MediaType = MediaType.None;
            MediaNameLabel.Text = "（未選択）";
            PreviewImage.Source = null;
            LogMessage("素材をクリアしました。");
        }

        private void OnMediaChanged()
        {
            if (_currentScene?.MediaPath == null) return;
            if (_currentScene.MediaType == MediaType.Image)
                UpdateThumbnail(_currentScene.MediaPath);
            else
                PreviewImage.Source = null;
        }

        private void OnDurationModeChanged(object sender, RoutedEventArgs e)
        {
            if (_isLoadingScene || _currentScene == null) return;
            _currentScene.DurationMode = FixedDurationRadio.IsChecked == true
                ? DurationMode.Fixed : DurationMode.Auto;
        }

        private void OnDurationSecondsChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingScene || _currentScene == null) return;
            if (double.TryParse(DurationSecondsTextBox.Text, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var seconds))
            {
                seconds = Math.Clamp(seconds, 0.1, 60.0);
                _currentScene.FixedSeconds = seconds;
            }
        }

        private void OnTransitionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingScene || _currentScene == null) return;
            if (TransitionComboBox.SelectedItem is ComboBoxItem item &&
                item.Tag is TransitionType tt)
            {
                _currentScene.TransitionType = tt;
            }
        }

        private void OnTransitionDurationChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoadingScene || _currentScene == null) return;
            if (double.TryParse(TransitionDurationTextBox.Text, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var dur))
            {
                dur = Math.Clamp(dur, 0.2, 2.0);
                _currentScene.TransitionDuration = dur;
            }
        }

        private void OnSceneSpeakerChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingScene || _currentScene == null) return;
            if (SceneSpeakerComboBox.SelectedItem is SpeakerItem si)
                _currentScene.SpeakerId = si.StyleId == -1 ? null : si.StyleId;
        }

        private void OnKeepAudioChanged(object sender, RoutedEventArgs e)
        {
            if (_isLoadingScene || _currentScene == null) return;
            _currentScene.KeepOriginalAudio = KeepAudioCheckBox.IsChecked == true;
        }

        #endregion

        #region Speakers

        private async Task LoadSpeakers()
        {
            try
            {
                var speakers = await _voiceVoxClient.GetSpeakersAsync();
                _speakerStyles.Clear();
                var exportItems = new List<SpeakerItem>();

                foreach (var speaker in speakers)
                {
                    if (!speaker.TryGetProperty("name", out var nameProp)) continue;
                    var speakerName = nameProp.GetString() ?? "Unknown";
                    if (!speaker.TryGetProperty("styles", out var styles)) continue;

                    foreach (var style in styles.EnumerateArray())
                    {
                        if (!style.TryGetProperty("id", out var idProp)) continue;
                        var styleId = idProp.GetInt32();
                        var styleName = style.TryGetProperty("name", out var snProp)
                            ? snProp.GetString() ?? "ノーマル" : "ノーマル";
                        var displayName = $"{speakerName} ({styleName})";
                        _speakerStyles[styleId] = displayName;
                        exportItems.Add(new SpeakerItem { DisplayName = displayName, StyleId = styleId });
                    }
                }

                ExportSpeakerComboBox.Items.Clear();
                foreach (var item in exportItems)
                    ExportSpeakerComboBox.Items.Add(item);

                for (int i = 0; i < ExportSpeakerComboBox.Items.Count; i++)
                {
                    if (ExportSpeakerComboBox.Items[i] is SpeakerItem si && si.StyleId == _defaultSpeakerId)
                    {
                        ExportSpeakerComboBox.SelectedIndex = i;
                        break;
                    }
                }

                if (ExportSpeakerComboBox.SelectedIndex < 0 && ExportSpeakerComboBox.Items.Count > 0)
                    ExportSpeakerComboBox.SelectedIndex = 0;

                LogMessage($"話者一覧を読み込みました ({exportItems.Count} スタイル)。");
            }
            catch (Exception ex)
            {
                LogMessage($"話者一覧の読み込みに失敗: {ex.Message}");
            }
        }

        private void LoadSceneSpeakers()
        {
            SceneSpeakerComboBox.Items.Clear();
            SceneSpeakerComboBox.Items.Add(new SpeakerItem
            {
                DisplayName = "デフォルト（プロジェクト設定を使用）",
                StyleId = -1
            });

            foreach (var kvp in _speakerStyles)
                SceneSpeakerComboBox.Items.Add(new SpeakerItem { DisplayName = kvp.Value, StyleId = kvp.Key });

            SceneSpeakerComboBox.SelectedIndex = 0;
        }

        private void SelectSceneSpeaker(int? speakerId)
        {
            if (speakerId == null) { SceneSpeakerComboBox.SelectedIndex = 0; return; }
            for (int i = 0; i < SceneSpeakerComboBox.Items.Count; i++)
            {
                if (SceneSpeakerComboBox.Items[i] is SpeakerItem si && si.StyleId == speakerId.Value)
                {
                    SceneSpeakerComboBox.SelectedIndex = i;
                    return;
                }
            }
            SceneSpeakerComboBox.SelectedIndex = 0;
        }

        #endregion

        #region Transitions

        private void PopulateTransitionCombo()
        {
            TransitionComboBox.Items.Clear();
            foreach (var kvp in TransitionNames.DisplayNames)
            {
                TransitionComboBox.Items.Add(new ComboBoxItem { Content = kvp.Value, Tag = kvp.Key });
            }
            if (TransitionComboBox.Items.Count > 0)
                TransitionComboBox.SelectedIndex = 0;
        }

        private void SelectTransition(TransitionType type)
        {
            for (int i = 0; i < TransitionComboBox.Items.Count; i++)
            {
                if (TransitionComboBox.Items[i] is ComboBoxItem item &&
                    item.Tag is TransitionType tt && tt == type)
                {
                    TransitionComboBox.SelectedIndex = i;
                    return;
                }
            }
        }

        #endregion

        #region Style

        private void OpenStyleDialog_Click(object sender, RoutedEventArgs e) => OpenStyleDialog();

        private void OpenStyleDialog()
        {
            if (_currentScene == null) return;

            var currentStyle = GetStyleForScene(_currentScene);
            var currentIdx = Models.TextStyle.PRESET_STYLES.FindIndex(s => s.Id == currentStyle.Id);
            if (currentIdx < 0) currentIdx = 0;

            var dlg = new TextStyleDialog(currentStyle);
            dlg.Owner = this;

            if (dlg.ShowDialog() == true)
            {
                var selectedStyle = dlg.GetSelectedStyle();
                _currentScene.SubtitleStyleId = selectedStyle.Id;
                _sceneSubtitleStyles[_currentScene.Id] = selectedStyle;
                UpdateStylePreview();
                LogMessage($"字幕スタイルを「{selectedStyle.Name}」に変更しました。");
            }
        }

        private Models.TextStyle GetStyleForScene(Scene scene)
        {
            if (scene.SubtitleStyleId != null &&
                _sceneSubtitleStyles.TryGetValue(scene.Id, out var style))
                return style;

            if (scene.SubtitleStyleId != null)
            {
                var preset = Models.TextStyle.PRESET_STYLES.FirstOrDefault(s => s.Id == scene.SubtitleStyleId);
                if (preset != null) return preset;
            }
            return _defaultSubtitleStyle;
        }

        private void UpdateStylePreview()
        {
            if (_currentScene == null) return;
            var style = GetStyleForScene(_currentScene);
            try
            {
                var textColor = Color.FromRgb(
                    (byte)style.TextColor[0], (byte)style.TextColor[1], (byte)style.TextColor[2]);
                StylePreviewLabel.Foreground = new SolidColorBrush(textColor);
                StylePreviewLabel.FontWeight = style.FontBold ? FontWeights.Bold : FontWeights.Normal;
            }
            catch
            {
                StylePreviewLabel.Foreground = Brushes.White;
            }
        }

        #endregion

        #region Preview / Audio Playback

        private async void PreviewAudio_Click(object sender, RoutedEventArgs e)
            => await PreviewCurrentScene();

        private async Task PreviewCurrentScene()
        {
            if (_currentScene == null || string.IsNullOrWhiteSpace(_currentScene.NarrationText))
            {
                MessageBox.Show(this, "ナレーションテキストを入力してください。",
                    "音声プレビュー", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var speakerId = _currentScene.SpeakerId ?? _defaultSpeakerId;
            var text = _currentScene.NarrationText!;

            try
            {
                PlayAudioButton.IsEnabled = false;
                LogMessage("音声を生成中...");

                string audioPath;
                if (_audioCache.Exists(text, speakerId))
                {
                    audioPath = _audioCache.GetCachePath(text, speakerId);
                    LogMessage("キャッシュから音声を読み込みました。");
                }
                else
                {
                    var audioData = await _voiceVoxClient.GenerateAudioAsync(text, speakerId);
                    audioPath = _audioCache.Save(text, speakerId, audioData);
                    LogMessage("音声を生成しました。");
                }

                _currentScene.AudioCachePath = audioPath;

                double speed = 1.0;
                if (SpeedComboBox.SelectedItem is ComboBoxItem speedItem &&
                    speedItem.Tag is string tagStr &&
                    double.TryParse(tagStr, out var parsed))
                    speed = parsed;

                AudioPlayer.SpeedRatio = speed;
                AudioPlayer.Source = new Uri(audioPath, UriKind.Absolute);
                AudioPlayer.Play();
                LogMessage("再生中...");
            }
            catch (Exception ex)
            {
                LogMessage($"音声プレビューエラー: {ex.Message}");
                MessageBox.Show(this, $"音声の生成に失敗しました:\n{ex.Message}",
                    "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                PlayAudioButton.IsEnabled = true;
            }
        }

        private void StopPreview_Click(object sender, RoutedEventArgs e) => StopScenePreview();

        private void StopScenePreview()
        {
            AudioPlayer.Stop();
            AudioPlayer.Source = null;
        }

        private void AudioPlayer_MediaEnded(object sender, RoutedEventArgs e)
        {
            AudioPlayer.Stop();
            AudioPlayer.Source = null;
        }

        #endregion

        #region Export

        private async void ExportVideo_Click(object sender, RoutedEventArgs e) => await ExportVideo();

        private async Task ExportVideo()
        {
            if (_isExporting)
            {
                MessageBox.Show(this, "書き出し処理が実行中です。", "書き出し中",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dlg = new SaveFileDialog
            {
                Title = "動画ファイルの保存先を選択",
                Filter = "MP4ファイル|*.mp4|すべてのファイル|*.*",
                DefaultExt = ".mp4",
                FileName = "InsightMovie.mp4"
            };

            if (dlg.ShowDialog(this) != true) return;

            var outputPath = dlg.FileName;

            int fps = 30;
            if (int.TryParse(FpsTextBox.Text, out var parsedFps))
                fps = Math.Clamp(parsedFps, 15, 60);

            var resolution = "1080x1920";
            if (ResolutionComboBox.SelectedItem is ComboBoxItem resItem && resItem.Tag is string resTag)
                resolution = resTag;

            int exportSpeakerId = _defaultSpeakerId;
            if (ExportSpeakerComboBox.SelectedItem is SpeakerItem si)
                exportSpeakerId = si.StyleId;

            _isExporting = true;
            _exportCts = new CancellationTokenSource();
            ExportProgressBar.Visibility = Visibility.Visible;
            ExportButton.IsEnabled = false;

            var progress = new Progress<string>(OnGenerationProgress);
            var ct = _exportCts.Token;

            LogMessage($"書き出しを開始: {outputPath}");

            try
            {
                var success = await Task.Run(() =>
                    VideoGenerationTask(outputPath, resolution, fps,
                        exportSpeakerId, progress, ct), ct);
                OnGenerationFinished(success, outputPath);
            }
            catch (OperationCanceledException)
            {
                OnGenerationFinished(false, "書き出しがキャンセルされました。");
            }
            catch (Exception ex)
            {
                OnGenerationFinished(false, $"書き出しエラー: {ex.Message}");
            }
            finally
            {
                _isExporting = false;
                ExportProgressBar.Visibility = Visibility.Collapsed;
                ExportButton.IsEnabled = true;
                _exportCts?.Dispose();
                _exportCts = null;
            }
        }

        private bool VideoGenerationTask(string outputPath, string resolution, int fps,
            int speakerId, IProgress<string> progress, CancellationToken ct)
        {
            progress.Report("動画生成を準備中...");

            if (_ffmpegWrapper == null || !_ffmpegWrapper.CheckAvailable())
            {
                progress.Report("エラー: ffmpegが検出されていません。");
                return false;
            }

            var sceneGen = new SceneGenerator(_ffmpegWrapper);
            var composer = new VideoComposer(_ffmpegWrapper);
            var tempDir = Path.Combine(Path.GetTempPath(), "insightmovie_build");
            Directory.CreateDirectory(tempDir);

            var scenePaths = new List<string>();
            var transitions = new List<(TransitionType, double)>();

            for (int i = 0; i < _project.Scenes.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var scene = _project.Scenes[i];
                progress.Report($"シーン {i + 1}/{_project.Scenes.Count}: 音声を生成中...");

                string? audioPath = null;
                if (scene.HasNarration && !scene.KeepOriginalAudio)
                {
                    var sid = scene.SpeakerId ?? speakerId;
                    if (!_audioCache.Exists(scene.NarrationText!, sid))
                    {
                        var audioData = _voiceVoxClient
                            .GenerateAudioAsync(scene.NarrationText!, sid)
                            .GetAwaiter().GetResult();
                        audioPath = _audioCache.Save(scene.NarrationText!, sid, audioData);
                    }
                    else
                    {
                        audioPath = _audioCache.GetCachePath(scene.NarrationText!, sid);
                    }
                    scene.AudioCachePath = audioPath;
                }

                progress.Report($"シーン {i + 1}/{_project.Scenes.Count}: 動画を生成中...");

                double duration = scene.DurationMode == DurationMode.Fixed
                    ? scene.FixedSeconds
                    : (audioPath != null ? _audioCache.GetDuration(scene.NarrationText!, scene.SpeakerId ?? speakerId) + 2.0 : 3.0);

                var scenePath = Path.Combine(tempDir, $"scene_{i:D4}.mp4");
                var style = GetStyleForScene(scene);

                var success = sceneGen.GenerateScene(scene, scenePath, duration,
                    resolution, fps, audioPath, style);

                if (!success)
                {
                    progress.Report($"シーン {i + 1} の生成に失敗しました。");
                    return false;
                }

                scenePaths.Add(scenePath);
                if (i > 0)
                    transitions.Add((scene.TransitionType, scene.TransitionDuration));
            }

            progress.Report("動画を結合中...");
            ct.ThrowIfCancellationRequested();

            bool concatOk;
            if (transitions.Any(t => t.Item1 != TransitionType.None))
            {
                concatOk = composer.ConcatWithTransitions(scenePaths, transitions, outputPath);
            }
            else
            {
                concatOk = composer.ConcatVideos(scenePaths, outputPath);
            }

            if (!concatOk)
            {
                progress.Report("動画の結合に失敗しました。");
                return false;
            }

            if (_project.Bgm.HasBgm)
            {
                progress.Report("BGMを追加中...");
                var withBgm = outputPath + ".bgm.mp4";
                var bgmOk = composer.AddBgm(outputPath, withBgm, _project.Bgm);
                if (bgmOk)
                {
                    File.Delete(outputPath);
                    File.Move(withBgm, outputPath);
                }
            }

            progress.Report("書き出し完了");
            return true;
        }

        private void OnGenerationProgress(string message)
        {
            LogMessage(message);
        }

        private void OnGenerationFinished(bool success, string message)
        {
            if (success)
            {
                LogMessage($"書き出し成功: {message}");
                var result = MessageBox.Show(this,
                    $"動画の書き出しが完了しました。\n\n{message}\n\nファイルを開きますか？",
                    "書き出し完了", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = message,
                            UseShellExecute = true
                        });
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"ファイルを開けませんでした: {ex.Message}");
                    }
                }
            }
            else
            {
                LogMessage($"書き出し失敗: {message}");
                MessageBox.Show(this, $"動画の書き出しに失敗しました:\n{message}",
                    "書き出しエラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Project Operations

        private void NewProject_Click(object sender, RoutedEventArgs e) => NewProject();
        private void NewProject_Executed(object sender, ExecutedRoutedEventArgs e) => NewProject();

        private void NewProject()
        {
            var result = MessageBox.Show(this,
                "現在のプロジェクトを破棄して新規作成しますか？\n保存されていない変更は失われます。",
                "新規プロジェクト", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            _project = new Models.Project();
            _project.InitializeDefaultScenes();
            _sceneSubtitleStyles.Clear();
            Title = "InsightMovie - 新規プロジェクト";
            LoadSceneList();
            LogMessage("新規プロジェクトを作成しました。");
        }

        private void OpenProject_Click(object sender, RoutedEventArgs e) => OpenProject();
        private void OpenProject_Executed(object sender, ExecutedRoutedEventArgs e) => OpenProject();

        private void OpenProject()
        {
            var dlg = new OpenFileDialog
            {
                Title = "プロジェクトファイルを開く",
                Filter = "JSONファイル|*.json|すべてのファイル|*.*",
                DefaultExt = ".json"
            };

            if (dlg.ShowDialog(this) != true) return;

            try
            {
                _project = Models.Project.Load(dlg.FileName);
                _sceneSubtitleStyles.Clear();
                Title = $"InsightMovie - {Path.GetFileNameWithoutExtension(dlg.FileName)}";
                LoadSceneList();
                UpdateBgmStatus();
                LogMessage($"プロジェクトを開きました: {dlg.FileName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"プロジェクトファイルの読み込みに失敗しました:\n{ex.Message}",
                    "読み込みエラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveProject_Click(object sender, RoutedEventArgs e) => SaveProject();
        private void SaveProject_Executed(object sender, ExecutedRoutedEventArgs e) => SaveProject();

        private void SaveProject()
        {
            if (string.IsNullOrEmpty(_project.ProjectPath))
            {
                SaveProjectAs();
                return;
            }

            try
            {
                _project.Save();
                LogMessage($"プロジェクトを保存しました: {_project.ProjectPath}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"保存に失敗しました:\n{ex.Message}",
                    "保存エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveProjectAs_Click(object sender, RoutedEventArgs e) => SaveProjectAs();

        private void SaveProjectAs()
        {
            var dlg = new SaveFileDialog
            {
                Title = "プロジェクトファイルを保存",
                Filter = "JSONファイル|*.json|すべてのファイル|*.*",
                DefaultExt = ".json",
                FileName = "InsightMovie.json"
            };

            if (dlg.ShowDialog(this) != true) return;

            try
            {
                _project.Save(dlg.FileName);
                Title = $"InsightMovie - {Path.GetFileNameWithoutExtension(dlg.FileName)}";
                LogMessage($"プロジェクトを保存しました: {dlg.FileName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"保存に失敗しました:\n{ex.Message}",
                    "保存エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportPptx_Click(object sender, RoutedEventArgs e) => ImportPptx();

        private void ImportPptx()
        {
            if (!License.CanUseFeature(_currentPlan, "pptx_import"))
            {
                MessageBox.Show(this,
                    "PPTX取込機能はProプラン以上でご利用いただけます。\nライセンスをアップグレードしてください。",
                    "機能制限", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new OpenFileDialog
            {
                Title = "PowerPointファイルを選択",
                Filter = "PowerPointファイル|*.pptx|すべてのファイル|*.*",
                DefaultExt = ".pptx"
            };

            if (dlg.ShowDialog(this) != true) return;

            try
            {
                var importer = new Utils.PptxImporter();
                var slides = importer.ExtractNotes(dlg.FileName);

                if (slides.Count == 0)
                {
                    MessageBox.Show(this, "スライドが見つかりませんでした。",
                        "取込結果", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                foreach (var slide in slides)
                {
                    var scene = new Scene { NarrationText = slide.Notes };
                    if (!string.IsNullOrEmpty(slide.ImagePath))
                    {
                        scene.MediaPath = slide.ImagePath;
                        scene.MediaType = MediaType.Image;
                    }
                    _project.Scenes.Add(scene);
                }

                LoadSceneList();
                LogMessage($"PPTXからシーンを取り込みました ({slides.Count} スライド): {dlg.FileName}");
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"PPTX取込に失敗しました:\n{ex.Message}",
                    "取込エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region BGM

        private void BgmSettings_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new BGMDialog(_project.Bgm);
            dlg.Owner = this;

            if (dlg.ShowDialog() == true)
            {
                _project.Bgm = dlg.GetSettings();
                UpdateBgmStatus();
                LogMessage("BGM設定を更新しました。");
            }
        }

        private void UpdateBgmStatus()
        {
            if (_project.Bgm.HasBgm)
            {
                var fileName = Path.GetFileName(_project.Bgm.FilePath);
                BgmStatusLabel.Text = $"BGM: {fileName}";
                BgmStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x99, 0x33));
            }
            else
            {
                BgmStatusLabel.Text = "BGM: 未設定";
                BgmStatusLabel.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
            }
        }

        #endregion

        #region Menu Handlers

        private void ExitApp_Click(object sender, RoutedEventArgs e) => Close();

        private void ShowTutorial_Click(object sender, RoutedEventArgs e) =>
            MessageBox.Show(this,
                "InsightMovie チュートリアル\n─────────────────────\n\n" +
                "1. シーンを追加: 左パネルの「＋追加」ボタンでシーンを追加します。\n\n" +
                "2. 素材を設定: 「選択」ボタンで画像または動画を選びます。\n\n" +
                "3. ナレーション入力: テキストエリアに話させたい内容を入力します。\n\n" +
                "4. 音声プレビュー: 「▶音声再生」ボタンで音声を確認できます。\n\n" +
                "5. 字幕設定: 字幕テキストとスタイルを設定します。\n\n" +
                "6. 書き出し: 「動画を書き出し」ボタンで動画を生成します。",
                "チュートリアル", MessageBoxButton.OK, MessageBoxImage.Information);

        private void ShowFaq_Click(object sender, RoutedEventArgs e) =>
            MessageBox.Show(this,
                "よくある質問 (FAQ)\n─────────────────────\n\n" +
                "Q: VOICEVOXが接続できません。\n" +
                "A: VOICEVOXエンジンが起動していることを確認してください。\n\n" +
                "Q: 動画の書き出しに失敗します。\n" +
                "A: ffmpegがインストールされ、PATHに含まれているか確認してください。\n\n" +
                "Q: ライセンスキーの入力方法は？\n" +
                "A: メニュー「ヘルプ」→「ライセンス管理」から入力してください。",
                "FAQ", MessageBoxButton.OK, MessageBoxImage.Information);

        private void ShowLicenseManager_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new LicenseDialog(_config);
            dlg.Owner = this;
            dlg.ShowDialog();
            LoadLicense();
        }

        private void ShowLicense_Click(object sender, RoutedEventArgs e)
        {
            var planName = License.GetPlanDisplayName(_currentPlan);
            var expiry = _licenseInfo?.ExpiresAt?.ToString("yyyy-MM-dd") ?? "N/A";
            var status = _licenseInfo?.IsValid == true ? "有効" : "無効/未登録";

            MessageBox.Show(this,
                $"ライセンス情報\n─────────────────────\n\n" +
                $"プラン: {planName}\n状態: {status}\n有効期限: {expiry}",
                "ライセンス情報", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ShowAbout_Click(object sender, RoutedEventArgs e) =>
            MessageBox.Show(this,
                "InsightMovie v1.0.0\n\n" +
                "VOICEVOX音声エンジンを使用した動画自動生成ツール\n\n" +
                "テキストを入力するだけで、ナレーション付き動画を\n簡単に作成できます。\n\n" +
                "Copyright (C) 2026 InsightMovie\nAll rights reserved.",
                "InsightMovieについて", MessageBoxButton.OK, MessageBoxImage.Information);

        #endregion

        #region License

        private void LoadLicense()
        {
            var key = _config.LicenseKey;
            _licenseInfo = License.ValidateLicenseKey(key);
            _currentPlan = _licenseInfo?.IsValid == true ? _licenseInfo.Plan : PlanCode.Free;
            UpdateFeatureAccess();
        }

        private void UpdateFeatureAccess()
        {
            bool canSubtitle = License.CanUseFeature(_currentPlan, "subtitle");
            bool canSubtitleStyle = License.CanUseFeature(_currentPlan, "subtitle_style");
            bool canTransition = License.CanUseFeature(_currentPlan, "transition");
            bool canPptx = License.CanUseFeature(_currentPlan, "pptx_import");

            SubtitleTextBox.IsEnabled = canSubtitle;
            SubtitlePlaceholder.Text = canSubtitle
                ? "画面下部に表示される字幕"
                : "字幕機能はProプラン以上で利用可能です";
            SelectStyleButton.IsEnabled = canSubtitleStyle;
            TransitionComboBox.IsEnabled = canTransition;
            TransitionDurationTextBox.IsEnabled = canTransition;
            ImportPptxButton.IsEnabled = canPptx;
        }

        #endregion

        #region Helpers

        private void UpdateThumbnail(string? imagePath)
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
            catch { PreviewImage.Source = null; }
        }

        private void LogMessage(string text)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            var line = $"[{timestamp}] {text}";
            Dispatcher.Invoke(() =>
            {
                if (LogTextBox.Text.Length > 0) LogTextBox.AppendText(Environment.NewLine);
                LogTextBox.AppendText(line);
                LogTextBox.ScrollToEnd();
            });
        }

        #endregion

        #region Keyboard Shortcuts

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.S && Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            { SaveProjectAs(); e.Handled = true; return; }
            if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control)
            { AddScene(); e.Handled = true; return; }
            if (e.Key == Key.Delete && Keyboard.Modifiers == ModifierKeys.None && Keyboard.FocusedElement is not TextBox)
            { RemoveScene(); e.Handled = true; return; }
            if (e.Key == Key.Up && Keyboard.Modifiers == ModifierKeys.Control)
            { MoveScene(-1); e.Handled = true; return; }
            if (e.Key == Key.Down && Keyboard.Modifiers == ModifierKeys.Control)
            { MoveScene(1); e.Handled = true; return; }
            if (e.Key == Key.F1 && Keyboard.Modifiers == ModifierKeys.None)
            { ShowTutorial_Click(sender, e); e.Handled = true; }
        }

        #endregion

        #region Window Events

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_isExporting)
            {
                var result = MessageBox.Show(this,
                    "書き出し処理が実行中です。中断して終了しますか？",
                    "終了確認", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) { e.Cancel = true; return; }
                _exportCts?.Cancel();
            }
            StopScenePreview();
        }

        #endregion
    }
}

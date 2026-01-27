using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using InsightMovie.Core;
using InsightMovie.Infrastructure;
using InsightMovie.Models;
using InsightMovie.Services;
using InsightMovie.Video;
using InsightMovie.VoiceVox;

namespace InsightMovie.ViewModels
{
    public class SpeakerItem
    {
        public string DisplayName { get; set; } = string.Empty;
        public int StyleId { get; set; }
        public override string ToString() => DisplayName;
    }

    public class MainWindowViewModel : ViewModelBase
    {
        #region Services

        private readonly VoiceVoxClient _voiceVoxClient;
        private readonly int _defaultSpeakerId;
        private readonly FFmpegWrapper? _ffmpegWrapper;
        private readonly Config _config;
        private readonly AudioCache _audioCache;
        private readonly IAppLogger _logger;
        private IDialogService? _dialogService;

        #endregion

        #region Observable State

        private Project _project;
        private int _selectedSceneIndex = -1;
        private Scene? _currentScene;
        private bool _isLoadingScene;
        private bool _isExporting;
        private CancellationTokenSource? _exportCts;

        private string _windowTitle = "InsightMovie - 新規プロジェクト";
        private string _statusText = string.Empty;
        private string _mediaName = "（未選択）";
        private string _narrationText = string.Empty;
        private string _subtitleText = string.Empty;
        private bool _keepOriginalAudio;
        private bool _isAutoDuration = true;
        private bool _isFixedDuration;
        private string _durationSeconds = "3.0";
        private int _selectedTransitionIndex;
        private string _transitionDuration = "0.5";
        private int _selectedSceneSpeakerIndex;
        private string _bgmStatusText = "BGM: 未設定";
        private bool _bgmActive;
        private string _fpsText = "30";
        private int _selectedResolutionIndex;
        private int _selectedExportSpeakerIndex;
        private bool _exportProgressVisible;

        // License state
        private LicenseInfo? _licenseInfo;
        private PlanCode _currentPlan = PlanCode.Free;
        private bool _canSubtitle;
        private bool _canSubtitleStyle;
        private bool _canTransition;
        private bool _canPptx;
        private string _subtitlePlaceholder = "画面下部に表示される字幕";

        // Style
        private TextStyle _defaultSubtitleStyle;
        private readonly Dictionary<string, TextStyle> _sceneSubtitleStyles = new();
        private Dictionary<int, string> _speakerStyles = new();

        #endregion

        #region Collections

        public ObservableCollection<SceneListItem> SceneItems { get; } = new();
        public ObservableCollection<SpeakerItem> ExportSpeakers { get; } = new();
        public ObservableCollection<SpeakerItem> SceneSpeakers { get; } = new();

        #endregion

        #region Properties

        public string WindowTitle
        {
            get => _windowTitle;
            set => SetProperty(ref _windowTitle, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public int SelectedSceneIndex
        {
            get => _selectedSceneIndex;
            set
            {
                if (SetProperty(ref _selectedSceneIndex, value))
                    OnSceneSelected();
            }
        }

        public string MediaName
        {
            get => _mediaName;
            set => SetProperty(ref _mediaName, value);
        }

        public string NarrationText
        {
            get => _narrationText;
            set
            {
                if (SetProperty(ref _narrationText, value))
                    OnNarrationChanged();
            }
        }

        public bool NarrationPlaceholderVisible => string.IsNullOrEmpty(_narrationText);

        public string SubtitleText
        {
            get => _subtitleText;
            set
            {
                if (SetProperty(ref _subtitleText, value))
                    OnSubtitleChanged();
            }
        }

        public bool SubtitlePlaceholderVisible => string.IsNullOrEmpty(_subtitleText);

        public string SubtitlePlaceholder
        {
            get => _subtitlePlaceholder;
            set => SetProperty(ref _subtitlePlaceholder, value);
        }

        public bool KeepOriginalAudio
        {
            get => _keepOriginalAudio;
            set
            {
                if (SetProperty(ref _keepOriginalAudio, value) && !_isLoadingScene && _currentScene != null)
                    _currentScene.KeepOriginalAudio = value;
            }
        }

        public bool IsAutoDuration
        {
            get => _isAutoDuration;
            set
            {
                if (SetProperty(ref _isAutoDuration, value))
                    OnDurationModeChanged();
            }
        }

        public bool IsFixedDuration
        {
            get => _isFixedDuration;
            set => SetProperty(ref _isFixedDuration, value);
        }

        public string DurationSeconds
        {
            get => _durationSeconds;
            set
            {
                if (SetProperty(ref _durationSeconds, value))
                    OnDurationSecondsChanged();
            }
        }

        public int SelectedTransitionIndex
        {
            get => _selectedTransitionIndex;
            set
            {
                if (SetProperty(ref _selectedTransitionIndex, value))
                    OnTransitionChanged();
            }
        }

        public string TransitionDuration
        {
            get => _transitionDuration;
            set
            {
                if (SetProperty(ref _transitionDuration, value))
                    OnTransitionDurationChanged();
            }
        }

        public int SelectedSceneSpeakerIndex
        {
            get => _selectedSceneSpeakerIndex;
            set
            {
                if (SetProperty(ref _selectedSceneSpeakerIndex, value))
                    OnSceneSpeakerChanged();
            }
        }

        public int SelectedExportSpeakerIndex
        {
            get => _selectedExportSpeakerIndex;
            set => SetProperty(ref _selectedExportSpeakerIndex, value);
        }

        public int SelectedResolutionIndex
        {
            get => _selectedResolutionIndex;
            set => SetProperty(ref _selectedResolutionIndex, value);
        }

        public string FpsText
        {
            get => _fpsText;
            set => SetProperty(ref _fpsText, value);
        }

        public string BgmStatusText
        {
            get => _bgmStatusText;
            set => SetProperty(ref _bgmStatusText, value);
        }

        public bool BgmActive
        {
            get => _bgmActive;
            set => SetProperty(ref _bgmActive, value);
        }

        public bool IsExporting
        {
            get => _isExporting;
            private set => SetProperty(ref _isExporting, value);
        }

        public bool ExportProgressVisible
        {
            get => _exportProgressVisible;
            set => SetProperty(ref _exportProgressVisible, value);
        }

        // License feature flags
        public bool CanSubtitle
        {
            get => _canSubtitle;
            set => SetProperty(ref _canSubtitle, value);
        }

        public bool CanSubtitleStyle
        {
            get => _canSubtitleStyle;
            set => SetProperty(ref _canSubtitleStyle, value);
        }

        public bool CanTransition
        {
            get => _canTransition;
            set => SetProperty(ref _canTransition, value);
        }

        public bool CanPptx
        {
            get => _canPptx;
            set => SetProperty(ref _canPptx, value);
        }

        // Current scene for UI binding
        public Scene? CurrentScene => _currentScene;
        public Project Project => _project;

        #endregion

        #region Events (for UI-specific actions the ViewModel can't handle directly)

        /// <summary>Raised when audio preview should play. Args: (path, speed).</summary>
        public event Action<string, double>? PlayAudioRequested;

        /// <summary>Raised when audio preview should stop.</summary>
        public event Action? StopAudioRequested;

        /// <summary>Raised when thumbnail should update. Args: media path or null.</summary>
        public event Action<string?>? ThumbnailUpdateRequested;

        /// <summary>Raised when subtitle style preview should update. Args: TextStyle.</summary>
        public event Action<TextStyle>? StylePreviewUpdateRequested;

        /// <summary>Raised when user wants to open exported file. Args: file path.</summary>
        public event Action<string>? OpenFileRequested;

        #endregion

        #region Commands

        public ICommand NewProjectCommand { get; }
        public ICommand OpenProjectCommand { get; }
        public ICommand SaveProjectCommand { get; }
        public ICommand SaveProjectAsCommand { get; }
        public ICommand ImportPptxCommand { get; }
        public ICommand AddSceneCommand { get; }
        public ICommand RemoveSceneCommand { get; }
        public ICommand MoveSceneUpCommand { get; }
        public ICommand MoveSceneDownCommand { get; }
        public ICommand SelectMediaCommand { get; }
        public ICommand ClearMediaCommand { get; }
        public ICommand OpenStyleDialogCommand { get; }
        public ICommand PreviewAudioCommand { get; }
        public ICommand StopPreviewCommand { get; }
        public ICommand ExportVideoCommand { get; }
        public ICommand BgmSettingsCommand { get; }
        public ICommand ShowTutorialCommand { get; }
        public ICommand ShowFaqCommand { get; }
        public ICommand ShowLicenseManagerCommand { get; }
        public ICommand ShowLicenseInfoCommand { get; }
        public ICommand ShowAboutCommand { get; }
        public ICommand ExitCommand { get; }

        #endregion

        #region Constructor

        public MainWindowViewModel(VoiceVoxClient voiceVoxClient, int speakerId,
                                    FFmpegWrapper? ffmpegWrapper, Config config)
        {
            _voiceVoxClient = voiceVoxClient;
            _defaultSpeakerId = speakerId;
            _ffmpegWrapper = ffmpegWrapper;
            _config = config;
            _audioCache = new AudioCache();
            _defaultSubtitleStyle = TextStyle.PRESET_STYLES[0];
            _logger = new AppLogger();

            _project = new Project();
            _project.InitializeDefaultScenes();

            // Commands
            NewProjectCommand = new RelayCommand(NewProject);
            OpenProjectCommand = new RelayCommand(OpenProject);
            SaveProjectCommand = new RelayCommand(SaveProject);
            SaveProjectAsCommand = new RelayCommand(SaveProjectAs);
            ImportPptxCommand = new RelayCommand(ImportPptx);
            AddSceneCommand = new RelayCommand(AddScene);
            RemoveSceneCommand = new RelayCommand(RemoveScene);
            MoveSceneUpCommand = new RelayCommand(MoveSceneUp);
            MoveSceneDownCommand = new RelayCommand(MoveSceneDown);
            SelectMediaCommand = new RelayCommand(SelectMedia);
            ClearMediaCommand = new RelayCommand(ClearMedia);
            OpenStyleDialogCommand = new RelayCommand(OpenStyleDialog);
            PreviewAudioCommand = new AsyncRelayCommand(PreviewCurrentScene);
            StopPreviewCommand = new RelayCommand(() => StopAudioRequested?.Invoke());
            ExportVideoCommand = new AsyncRelayCommand(ExportVideo);
            BgmSettingsCommand = new RelayCommand(OpenBgmSettings);
            ShowTutorialCommand = new RelayCommand(ShowTutorial);
            ShowFaqCommand = new RelayCommand(ShowFaq);
            ShowLicenseManagerCommand = new RelayCommand(ShowLicenseManager);
            ShowLicenseInfoCommand = new RelayCommand(ShowLicenseInfo);
            ShowAboutCommand = new RelayCommand(ShowAbout);
            ExitCommand = new RelayCommand(() => ExitRequested?.Invoke());

            UpdateStatusText();
            LoadLicense();
            RefreshSceneList();

            // Force initial scene selection after _isLoadingScene is cleared.
            // RefreshSceneList sets SelectedSceneIndex=0 while _isLoadingScene=true,
            // so OnSceneSelected() is skipped. Reset and re-set to trigger it.
            if (SceneItems.Count > 0)
            {
                _selectedSceneIndex = -1;
                SelectedSceneIndex = 0;
            }
        }

        /// <summary>Raised when window should close.</summary>
        public event Action? ExitRequested;

        public IAppLogger Logger => _logger;

        public void SetDialogService(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        public async Task InitializeAsync()
        {
            await LoadSpeakers();
            LoadSceneSpeakers();
            _logger.Log("初期化完了");
        }

        #endregion

        #region Status

        private void UpdateStatusText()
        {
            var vvStatus = "VOICEVOX: ✓接続OK";
            var ffStatus = _ffmpegWrapper?.CheckAvailable() == true
                ? "ffmpeg: ✓検出OK"
                : "ffmpeg: ✗未検出";
            StatusText = $"{vvStatus} • {ffStatus}";
        }

        #endregion

        #region Scene List Management

        public void RefreshSceneList()
        {
            _isLoadingScene = true;
            var selectedIndex = _selectedSceneIndex;
            SceneItems.Clear();

            for (int i = 0; i < _project.Scenes.Count; i++)
            {
                SceneItems.Add(new SceneListItem(_project.Scenes[i], i));
            }

            if (selectedIndex >= 0 && selectedIndex < SceneItems.Count)
                SelectedSceneIndex = selectedIndex;
            else if (SceneItems.Count > 0)
                SelectedSceneIndex = 0;
            else
                SelectedSceneIndex = -1;

            _isLoadingScene = false;
        }

        private void OnSceneSelected()
        {
            if (_isLoadingScene) return;
            if (_selectedSceneIndex < 0 || _selectedSceneIndex >= SceneItems.Count) return;

            _isLoadingScene = true;
            var item = SceneItems[_selectedSceneIndex];
            _currentScene = item.Scene;
            OnPropertyChanged(nameof(CurrentScene));

            if (_currentScene.HasMedia)
            {
                MediaName = Path.GetFileName(_currentScene.MediaPath);
                ThumbnailUpdateRequested?.Invoke(_currentScene.MediaPath);
            }
            else
            {
                MediaName = "（未選択）";
                ThumbnailUpdateRequested?.Invoke(null);
            }

            NarrationText = _currentScene.NarrationText ?? string.Empty;
            OnPropertyChanged(nameof(NarrationPlaceholderVisible));

            SelectSceneSpeaker(_currentScene.SpeakerId);
            KeepOriginalAudio = _currentScene.KeepOriginalAudio;

            SubtitleText = _currentScene.SubtitleText ?? string.Empty;
            OnPropertyChanged(nameof(SubtitlePlaceholderVisible));

            UpdateStylePreview();

            if (_currentScene.DurationMode == DurationMode.Fixed)
            {
                IsFixedDuration = true;
                IsAutoDuration = false;
            }
            else
            {
                IsAutoDuration = true;
                IsFixedDuration = false;
            }
            DurationSeconds = _currentScene.FixedSeconds.ToString("F1", CultureInfo.InvariantCulture);

            SelectTransition(_currentScene.TransitionType);
            TransitionDuration = _currentScene.TransitionDuration.ToString("F1", CultureInfo.InvariantCulture);

            _isLoadingScene = false;
        }

        private void AddScene()
        {
            _project.AddScene();
            RefreshSceneList();
            SelectedSceneIndex = _project.Scenes.Count - 1;
            _logger.Log($"シーン {_project.Scenes.Count} を追加しました。");
        }

        private void RemoveScene()
        {
            if (_project.Scenes.Count <= 1)
            {
                _dialogService?.ShowWarning("最低1つのシーンが必要です。", "削除不可");
                return;
            }

            var idx = _selectedSceneIndex;
            if (idx < 0) return;

            _project.RemoveScene(idx);
            RefreshSceneList();
            _logger.Log($"シーン {idx + 1} を削除しました。");
        }

        private void MoveSceneUp() => MoveScene(-1);
        private void MoveSceneDown() => MoveScene(1);

        private void MoveScene(int direction)
        {
            var idx = _selectedSceneIndex;
            if (idx < 0) return;

            int newIdx = idx + direction;
            if (newIdx < 0 || newIdx >= _project.Scenes.Count) return;

            _project.MoveScene(idx, newIdx);
            RefreshSceneList();
            SelectedSceneIndex = newIdx;
        }

        #endregion

        #region Scene Editing

        private void OnNarrationChanged()
        {
            OnPropertyChanged(nameof(NarrationPlaceholderVisible));

            if (_isLoadingScene || _currentScene == null) return;
            _currentScene.NarrationText = _narrationText;

            var idx = _selectedSceneIndex;
            if (idx >= 0 && idx < SceneItems.Count)
            {
                SceneItems[idx].UpdateLabel(idx);
            }
        }

        private void OnSubtitleChanged()
        {
            OnPropertyChanged(nameof(SubtitlePlaceholderVisible));

            if (_isLoadingScene || _currentScene == null) return;
            _currentScene.SubtitleText = _subtitleText;
        }

        private void SelectMedia()
        {
            if (_currentScene == null || _dialogService == null) return;

            var path = _dialogService.ShowOpenFileDialog(
                "素材ファイルを選択",
                "画像・動画ファイル|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.mp4;*.avi;*.mov;*.wmv;*.mkv|" +
                "画像ファイル|*.png;*.jpg;*.jpeg;*.bmp;*.gif|" +
                "動画ファイル|*.mp4;*.avi;*.mov;*.wmv;*.mkv|" +
                "すべてのファイル|*.*");

            if (path == null) return;

            var ext = Path.GetExtension(path).ToLowerInvariant();
            var imageExts = new HashSet<string> { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };
            var videoExts = new HashSet<string> { ".mp4", ".avi", ".mov", ".wmv", ".mkv" };

            _currentScene.MediaPath = path;
            _currentScene.MediaType = imageExts.Contains(ext) ? MediaType.Image
                                    : videoExts.Contains(ext) ? MediaType.Video
                                    : MediaType.None;

            MediaName = Path.GetFileName(path);

            if (_currentScene.MediaType == MediaType.Image)
                ThumbnailUpdateRequested?.Invoke(_currentScene.MediaPath);
            else
                ThumbnailUpdateRequested?.Invoke(null);

            _logger.Log($"素材を設定: {Path.GetFileName(path)}");
        }

        private void ClearMedia()
        {
            if (_currentScene == null) return;
            _currentScene.MediaPath = null;
            _currentScene.MediaType = MediaType.None;
            MediaName = "（未選択）";
            ThumbnailUpdateRequested?.Invoke(null);
            _logger.Log("素材をクリアしました。");
        }

        private void OnDurationModeChanged()
        {
            if (_isLoadingScene || _currentScene == null) return;
            _currentScene.DurationMode = _isFixedDuration ? DurationMode.Fixed : DurationMode.Auto;
        }

        private void OnDurationSecondsChanged()
        {
            if (_isLoadingScene || _currentScene == null) return;
            if (double.TryParse(_durationSeconds, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var seconds))
            {
                _currentScene.FixedSeconds = Math.Clamp(seconds, 0.1, 60.0);
            }
        }

        private void OnTransitionChanged()
        {
            if (_isLoadingScene || _currentScene == null) return;
            if (_selectedTransitionIndex >= 0)
            {
                var types = TransitionNames.DisplayNames.Keys.ToList();
                if (_selectedTransitionIndex < types.Count)
                    _currentScene.TransitionType = types[_selectedTransitionIndex];
            }
        }

        private void OnTransitionDurationChanged()
        {
            if (_isLoadingScene || _currentScene == null) return;
            if (double.TryParse(_transitionDuration, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var dur))
            {
                _currentScene.TransitionDuration = Math.Clamp(dur, 0.2, 2.0);
            }
        }

        private void OnSceneSpeakerChanged()
        {
            if (_isLoadingScene || _currentScene == null) return;
            if (_selectedSceneSpeakerIndex >= 0 && _selectedSceneSpeakerIndex < SceneSpeakers.Count)
            {
                var si = SceneSpeakers[_selectedSceneSpeakerIndex];
                _currentScene.SpeakerId = si.StyleId == -1 ? null : si.StyleId;
            }
        }

        #endregion

        #region Speakers

        private async Task LoadSpeakers()
        {
            try
            {
                var speakers = await _voiceVoxClient.GetSpeakersAsync();
                _speakerStyles.Clear();
                ExportSpeakers.Clear();

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
                        ExportSpeakers.Add(new SpeakerItem { DisplayName = displayName, StyleId = styleId });
                    }
                }

                for (int i = 0; i < ExportSpeakers.Count; i++)
                {
                    if (ExportSpeakers[i].StyleId == _defaultSpeakerId)
                    {
                        SelectedExportSpeakerIndex = i;
                        break;
                    }
                }

                if (_selectedExportSpeakerIndex < 0 && ExportSpeakers.Count > 0)
                    SelectedExportSpeakerIndex = 0;

                _logger.Log($"話者一覧を読み込みました ({ExportSpeakers.Count} スタイル)。");
            }
            catch (Exception ex)
            {
                _logger.LogError("話者一覧の読み込みに失敗", ex);
            }
        }

        private void LoadSceneSpeakers()
        {
            SceneSpeakers.Clear();
            SceneSpeakers.Add(new SpeakerItem
            {
                DisplayName = "デフォルト（プロジェクト設定を使用）",
                StyleId = -1
            });

            foreach (var kvp in _speakerStyles)
                SceneSpeakers.Add(new SpeakerItem { DisplayName = kvp.Value, StyleId = kvp.Key });

            SelectedSceneSpeakerIndex = 0;
        }

        private void SelectSceneSpeaker(int? speakerId)
        {
            if (speakerId == null) { SelectedSceneSpeakerIndex = 0; return; }
            for (int i = 0; i < SceneSpeakers.Count; i++)
            {
                if (SceneSpeakers[i].StyleId == speakerId.Value)
                {
                    SelectedSceneSpeakerIndex = i;
                    return;
                }
            }
            SelectedSceneSpeakerIndex = 0;
        }

        #endregion

        #region Transitions

        public List<string> TransitionDisplayNames { get; } =
            TransitionNames.DisplayNames.Values.ToList();

        private void SelectTransition(TransitionType type)
        {
            var types = TransitionNames.DisplayNames.Keys.ToList();
            var idx = types.IndexOf(type);
            SelectedTransitionIndex = idx >= 0 ? idx : 0;
        }

        #endregion

        #region Style

        private void OpenStyleDialog()
        {
            if (_currentScene == null || _dialogService == null) return;

            var currentStyle = GetStyleForScene(_currentScene);
            var selectedStyle = _dialogService.ShowTextStyleDialog(currentStyle);

            if (selectedStyle != null)
            {
                _currentScene.SubtitleStyleId = selectedStyle.Id;
                _sceneSubtitleStyles[_currentScene.Id] = selectedStyle;
                UpdateStylePreview();
                _logger.Log($"字幕スタイルを「{selectedStyle.Name}」に変更しました。");
            }
        }

        public TextStyle GetStyleForScene(Scene scene)
        {
            if (scene.SubtitleStyleId != null &&
                _sceneSubtitleStyles.TryGetValue(scene.Id, out var style))
                return style;

            if (scene.SubtitleStyleId != null)
            {
                var preset = TextStyle.PRESET_STYLES.FirstOrDefault(s => s.Id == scene.SubtitleStyleId);
                if (preset != null) return preset;
            }
            return _defaultSubtitleStyle;
        }

        private void UpdateStylePreview()
        {
            if (_currentScene == null) return;
            var style = GetStyleForScene(_currentScene);
            StylePreviewUpdateRequested?.Invoke(style);
        }

        #endregion

        #region Audio Preview

        private async Task PreviewCurrentScene()
        {
            if (_currentScene == null || string.IsNullOrWhiteSpace(_currentScene.NarrationText))
            {
                _dialogService?.ShowInfo("ナレーションテキストを入力してください。", "音声プレビュー");
                return;
            }

            var speakerId = _currentScene.SpeakerId ?? _defaultSpeakerId;
            var text = _currentScene.NarrationText!;

            try
            {
                _logger.Log("音声を生成中...");

                string audioPath;
                if (_audioCache.Exists(text, speakerId))
                {
                    audioPath = _audioCache.GetCachePath(text, speakerId);
                    _logger.Log("キャッシュから音声を読み込みました。");
                }
                else
                {
                    var audioData = await _voiceVoxClient.GenerateAudioAsync(text, speakerId);
                    audioPath = _audioCache.Save(text, speakerId, audioData);
                    _logger.Log("音声を生成しました。");
                }

                _currentScene.AudioCachePath = audioPath;
                PlayAudioRequested?.Invoke(audioPath, 1.0);
                _logger.Log("再生中...");
            }
            catch (Exception ex)
            {
                _logger.LogError("音声プレビューエラー", ex);
                _dialogService?.ShowError($"音声の生成に失敗しました:\n{ex.Message}", "エラー");
            }
        }

        #endregion

        #region Export

        private async Task ExportVideo()
        {
            if (_isExporting)
            {
                _dialogService?.ShowWarning("書き出し処理が実行中です。", "書き出し中");
                return;
            }

            if (_dialogService == null) return;

            var outputPath = _dialogService.ShowSaveFileDialog(
                "動画ファイルの保存先を選択",
                "MP4ファイル|*.mp4|すべてのファイル|*.*",
                ".mp4",
                "InsightMovie.mp4");

            if (outputPath == null) return;

            if (_ffmpegWrapper == null || !_ffmpegWrapper.CheckAvailable())
            {
                _dialogService.ShowError(
                    "ffmpegが検出されていません。\n動画の書き出しにはffmpegが必要です。",
                    "書き出しエラー");
                return;
            }

            int fps = 30;
            if (int.TryParse(_fpsText, out var parsedFps))
                fps = Math.Clamp(parsedFps, 15, 60);

            string resolution = "1080x1920";
            if (_selectedResolutionIndex == 1)
                resolution = "1920x1080";

            int exportSpeakerId = _defaultSpeakerId;
            if (_selectedExportSpeakerIndex >= 0 && _selectedExportSpeakerIndex < ExportSpeakers.Count)
                exportSpeakerId = ExportSpeakers[_selectedExportSpeakerIndex].StyleId;

            IsExporting = true;
            _exportCts = new CancellationTokenSource();
            ExportProgressVisible = true;

            var progress = new Progress<string>(msg => _logger.Log(msg));
            var ct = _exportCts.Token;

            _logger.Log($"書き出しを開始: {outputPath}");

            // Snapshot project data to avoid race conditions with UI thread
            var projectSnapshot = _project.Clone();
            var styleSnapshot = new Dictionary<string, TextStyle>(_sceneSubtitleStyles);
            var defaultStyle = _defaultSubtitleStyle;

            TextStyle GetStyleSnapshot(Scene scene)
            {
                if (scene.SubtitleStyleId != null &&
                    styleSnapshot.TryGetValue(scene.Id, out var style))
                    return style;
                if (scene.SubtitleStyleId != null)
                {
                    var preset = TextStyle.PRESET_STYLES.FirstOrDefault(s => s.Id == scene.SubtitleStyleId);
                    if (preset != null) return preset;
                }
                return defaultStyle;
            }

            try
            {
                var ffmpeg = _ffmpegWrapper!; // null already checked above
                var exportService = new ExportService(ffmpeg, _voiceVoxClient, _audioCache);
                var success = await Task.Run(() =>
                    exportService.Export(projectSnapshot, outputPath, resolution, fps,
                        exportSpeakerId, GetStyleSnapshot, progress, ct), ct);

                OnExportFinished(success, outputPath);
            }
            catch (OperationCanceledException)
            {
                OnExportFinished(false, "書き出しがキャンセルされました。");
            }
            catch (Exception ex)
            {
                OnExportFinished(false, $"書き出しエラー: {ex.Message}");
            }
            finally
            {
                IsExporting = false;
                ExportProgressVisible = false;
                _exportCts?.Dispose();
                _exportCts = null;
            }
        }

        private void OnExportFinished(bool success, string message)
        {
            if (success)
            {
                _logger.Log($"書き出し成功: {message}");
                if (_dialogService?.ShowYesNo(
                    $"動画の書き出しが完了しました。\n\n{message}\n\nファイルを開きますか？",
                    "書き出し完了") == true)
                {
                    OpenFileRequested?.Invoke(message);
                }
            }
            else
            {
                _logger.Log($"書き出し失敗: {message}");
                _dialogService?.ShowError($"動画の書き出しに失敗しました:\n{message}", "書き出しエラー");
            }
        }

        public void CancelExport()
        {
            _exportCts?.Cancel();
        }

        #endregion

        #region Project Operations

        private void NewProject()
        {
            if (_dialogService == null) return;

            if (!_dialogService.ShowConfirmation(
                "現在のプロジェクトを破棄して新規作成しますか？\n保存されていない変更は失われます。",
                "新規プロジェクト"))
                return;

            _project = new Project();
            _project.InitializeDefaultScenes();
            _sceneSubtitleStyles.Clear();
            WindowTitle = "InsightMovie - 新規プロジェクト";
            RefreshSceneList();
            _logger.Log("新規プロジェクトを作成しました。");
        }

        private void OpenProject()
        {
            if (_dialogService == null) return;

            var path = _dialogService.ShowOpenFileDialog(
                "プロジェクトファイルを開く",
                "JSONファイル|*.json|すべてのファイル|*.*",
                ".json");

            if (path == null) return;

            try
            {
                _project = Project.Load(path);
                _sceneSubtitleStyles.Clear();
                WindowTitle = $"InsightMovie - {Path.GetFileNameWithoutExtension(path)}";
                RefreshSceneList();
                UpdateBgmStatus();
                _logger.Log($"プロジェクトを開きました: {path}");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"プロジェクトファイルの読み込みに失敗しました:\n{ex.Message}", "読み込みエラー");
            }
        }

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
                _logger.Log($"プロジェクトを保存しました: {_project.ProjectPath}");
            }
            catch (Exception ex)
            {
                _dialogService?.ShowError($"保存に失敗しました:\n{ex.Message}", "保存エラー");
            }
        }

        private void SaveProjectAs()
        {
            if (_dialogService == null) return;

            var path = _dialogService.ShowSaveFileDialog(
                "プロジェクトファイルを保存",
                "JSONファイル|*.json|すべてのファイル|*.*",
                ".json",
                "InsightMovie.json");

            if (path == null) return;

            try
            {
                _project.Save(path);
                WindowTitle = $"InsightMovie - {Path.GetFileNameWithoutExtension(path)}";
                _logger.Log($"プロジェクトを保存しました: {path}");
            }
            catch (Exception ex)
            {
                _dialogService?.ShowError($"保存に失敗しました:\n{ex.Message}", "保存エラー");
            }
        }

        private void ImportPptx()
        {
            if (_dialogService == null) return;

            if (!License.CanUseFeature(_currentPlan, "pptx_import"))
            {
                _dialogService.ShowInfo(
                    "PPTX取込機能はProプラン以上でご利用いただけます。\nライセンスをアップグレードしてください。",
                    "機能制限");
                return;
            }

            var path = _dialogService.ShowOpenFileDialog(
                "PowerPointファイルを選択",
                "PowerPointファイル|*.pptx|すべてのファイル|*.*",
                ".pptx");

            if (path == null) return;

            try
            {
                var importer = new Utils.PptxImporter();
                var slides = importer.ExtractNotes(path);

                if (slides.Count == 0)
                {
                    _dialogService.ShowInfo("スライドが見つかりませんでした。", "取込結果");
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

                RefreshSceneList();
                _logger.Log($"PPTXからシーンを取り込みました ({slides.Count} スライド): {path}");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError($"PPTX取込に失敗しました:\n{ex.Message}", "取込エラー");
            }
        }

        #endregion

        #region BGM

        private void OpenBgmSettings()
        {
            if (_dialogService == null) return;

            var result = _dialogService.ShowBgmDialog(_project.Bgm);
            if (result != null)
            {
                _project.Bgm = result;
                UpdateBgmStatus();
                _logger.Log("BGM設定を更新しました。");
            }
        }

        private void UpdateBgmStatus()
        {
            if (_project.Bgm.HasBgm)
            {
                BgmStatusText = $"BGM: {Path.GetFileName(_project.Bgm.FilePath)}";
                BgmActive = true;
            }
            else
            {
                BgmStatusText = "BGM: 未設定";
                BgmActive = false;
            }
        }

        #endregion

        #region License

        public void LoadLicense()
        {
            var key = _config.LicenseKey;
            _licenseInfo = License.ValidateLicenseKey(key);
            _currentPlan = _licenseInfo?.IsValid == true ? _licenseInfo.Plan : PlanCode.Free;
            UpdateFeatureAccess();
        }

        private void UpdateFeatureAccess()
        {
            CanSubtitle = License.CanUseFeature(_currentPlan, "subtitle");
            CanSubtitleStyle = License.CanUseFeature(_currentPlan, "subtitle_style");
            CanTransition = License.CanUseFeature(_currentPlan, "transition");
            CanPptx = License.CanUseFeature(_currentPlan, "pptx_import");

            SubtitlePlaceholder = _canSubtitle
                ? "画面下部に表示される字幕"
                : "字幕機能はProプラン以上で利用可能です";
        }

        #endregion

        #region Menu Handlers

        private void ShowTutorial()
        {
            _dialogService?.ShowInfo(
                "InsightMovie チュートリアル\n─────────────────────\n\n" +
                "1. シーンを追加: 左パネルの「＋追加」ボタンでシーンを追加します。\n\n" +
                "2. 素材を設定: 「選択」ボタンで画像または動画を選びます。\n\n" +
                "3. ナレーション入力: テキストエリアに話させたい内容を入力します。\n\n" +
                "4. 音声プレビュー: 「▶音声再生」ボタンで音声を確認できます。\n\n" +
                "5. 字幕設定: 字幕テキストとスタイルを設定します。\n\n" +
                "6. 書き出し: 「動画を書き出し」ボタンで動画を生成します。",
                "チュートリアル");
        }

        private void ShowFaq()
        {
            _dialogService?.ShowInfo(
                "よくある質問 (FAQ)\n─────────────────────\n\n" +
                "Q: VOICEVOXが接続できません。\n" +
                "A: VOICEVOXエンジンが起動していることを確認してください。\n\n" +
                "Q: 動画の書き出しに失敗します。\n" +
                "A: ffmpegがインストールされ、PATHに含まれているか確認してください。\n\n" +
                "Q: ライセンスキーの入力方法は？\n" +
                "A: メニュー「ヘルプ」→「ライセンス管理」から入力してください。",
                "FAQ");
        }

        private void ShowLicenseManager()
        {
            _dialogService?.ShowLicenseDialog(_config);
            LoadLicense();
        }

        private void ShowLicenseInfo()
        {
            var planName = License.GetPlanDisplayName(_currentPlan);
            var expiry = _licenseInfo?.ExpiresAt?.ToString("yyyy-MM-dd") ?? "N/A";
            var status = _licenseInfo?.IsValid == true ? "有効" : "無効/未登録";

            _dialogService?.ShowInfo(
                $"ライセンス情報\n─────────────────────\n\n" +
                $"プラン: {planName}\n状態: {status}\n有効期限: {expiry}",
                "ライセンス情報");
        }

        private void ShowAbout()
        {
            _dialogService?.ShowInfo(
                "InsightMovie v1.0.0\n\n" +
                "VOICEVOX音声エンジンを使用した動画自動生成ツール\n\n" +
                "テキストを入力するだけで、ナレーション付き動画を\n簡単に作成できます。\n\n" +
                "Copyright (C) 2026 InsightMovie\nAll rights reserved.",
                "InsightMovieについて");
        }

        #endregion

        #region Window Lifecycle

        public bool CanClose()
        {
            if (!_isExporting) return true;

            if (_dialogService?.ShowConfirmation(
                "書き出し処理が実行中です。中断して終了しますか？",
                "終了確認") == true)
            {
                _exportCts?.Cancel();
                return true;
            }
            return false;
        }

        #endregion
    }
}

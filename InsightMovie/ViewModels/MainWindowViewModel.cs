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
        private Timer? _autoSaveTimer;
        private bool _isDirty;
        private int _selectedSceneIndex = -1;
        private Scene? _currentScene;
        private bool _isLoadingScene;
        private bool _isExporting;
        private CancellationTokenSource? _exportCts;

        private string _windowTitle = "InsightCast - 新規プロジェクト";
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
        private double _exportProgressValue;

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

        // Intro/Outro/Watermark
        private string _introFilePath = string.Empty;
        private string _outroFilePath = string.Empty;
        private string _watermarkFilePath = string.Empty;
        private int _selectedWatermarkPosIndex = 3; // bottom-right

        // Overlay
        private int _selectedOverlayIndex = -1;
        private string _overlayText = string.Empty;
        private string _overlayXPercent = "50.0";
        private string _overlayYPercent = "50.0";
        private string _overlayFontSize = "64";
        private int _selectedAlignmentIndex;
        private int _selectedOverlayColorIndex;
        private bool _isLoadingOverlay;

        #endregion

        #region Collections

        public ObservableCollection<SceneListItem> SceneItems { get; } = new();
        public ObservableCollection<SpeakerItem> ExportSpeakers { get; } = new();
        public ObservableCollection<SpeakerItem> SceneSpeakers { get; } = new();
        public ObservableCollection<OverlayListItem> OverlayItems { get; } = new();

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

        public double ExportProgressValue
        {
            get => _exportProgressValue;
            set => SetProperty(ref _exportProgressValue, value);
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

        // Overlay properties
        public int SelectedOverlayIndex
        {
            get => _selectedOverlayIndex;
            set
            {
                if (SetProperty(ref _selectedOverlayIndex, value))
                    OnOverlaySelected();
            }
        }

        public string OverlayText
        {
            get => _overlayText;
            set
            {
                if (SetProperty(ref _overlayText, value))
                    OnOverlayTextChanged();
            }
        }

        public string OverlayXPercent
        {
            get => _overlayXPercent;
            set
            {
                if (SetProperty(ref _overlayXPercent, value))
                    OnOverlayPositionChanged();
            }
        }

        public string OverlayYPercent
        {
            get => _overlayYPercent;
            set
            {
                if (SetProperty(ref _overlayYPercent, value))
                    OnOverlayPositionChanged();
            }
        }

        public string OverlayFontSize
        {
            get => _overlayFontSize;
            set
            {
                if (SetProperty(ref _overlayFontSize, value))
                    OnOverlayFontSizeChanged();
            }
        }

        public int SelectedAlignmentIndex
        {
            get => _selectedAlignmentIndex;
            set
            {
                if (SetProperty(ref _selectedAlignmentIndex, value))
                    OnOverlayAlignmentChanged();
            }
        }

        public int SelectedOverlayColorIndex
        {
            get => _selectedOverlayColorIndex;
            set
            {
                if (SetProperty(ref _selectedOverlayColorIndex, value))
                    OnOverlayColorChanged();
            }
        }

        public bool OverlayListVisible => OverlayItems.Count > 0;
        public bool OverlayEditorVisible => _selectedOverlayIndex >= 0 && _selectedOverlayIndex < OverlayItems.Count;

        public List<string> AlignmentOptions { get; } = new() { "中央", "左", "右" };

        public List<string> OverlayColorOptions { get; } = new()
        {
            "白", "黒", "赤", "青", "黄", "金", "ピンク", "水色"
        };

        private static readonly int[][] OverlayColorValues =
        {
            new[] { 255, 255, 255 }, // 白
            new[] { 0, 0, 0 },       // 黒
            new[] { 255, 0, 0 },     // 赤
            new[] { 0, 0, 255 },     // 青
            new[] { 255, 255, 0 },   // 黄
            new[] { 212, 175, 55 },  // 金
            new[] { 255, 105, 180 }, // ピンク
            new[] { 0, 191, 255 },   // 水色
        };

        // Intro/Outro properties
        public string IntroFilePath
        {
            get => _introFilePath;
            set => SetProperty(ref _introFilePath, value);
        }

        public string OutroFilePath
        {
            get => _outroFilePath;
            set => SetProperty(ref _outroFilePath, value);
        }

        public bool HasIntro => !string.IsNullOrEmpty(_introFilePath);
        public bool HasOutro => !string.IsNullOrEmpty(_outroFilePath);
        public string IntroFileName => string.IsNullOrEmpty(_introFilePath)
            ? "（なし）" : Path.GetFileName(_introFilePath);
        public string OutroFileName => string.IsNullOrEmpty(_outroFilePath)
            ? "（なし）" : Path.GetFileName(_outroFilePath);

        // Watermark properties
        public string WatermarkFilePath
        {
            get => _watermarkFilePath;
            set => SetProperty(ref _watermarkFilePath, value);
        }

        public bool HasWatermark => !string.IsNullOrEmpty(_watermarkFilePath);
        public string WatermarkFileName => string.IsNullOrEmpty(_watermarkFilePath)
            ? "（なし）" : Path.GetFileName(_watermarkFilePath);

        public int SelectedWatermarkPosIndex
        {
            get => _selectedWatermarkPosIndex;
            set => SetProperty(ref _selectedWatermarkPosIndex, value);
        }

        public List<string> WatermarkPositionOptions { get; } = new()
        {
            "左上", "右上", "左下", "右下", "中央"
        };

        private static readonly string[] WatermarkPositionValues =
            { "top-left", "top-right", "bottom-left", "bottom-right", "center" };

        // Speech speed
        public List<string> SpeechSpeedOptions { get; } = new()
        {
            "0.8x", "1.0x", "1.2x", "1.5x"
        };

        private static readonly double[] SpeechSpeedValues = { 0.8, 1.0, 1.2, 1.5 };

        private int _selectedSpeechSpeedIndex = 1;
        public int SelectedSpeechSpeedIndex
        {
            get => _selectedSpeechSpeedIndex;
            set
            {
                if (SetProperty(ref _selectedSpeechSpeedIndex, value))
                    OnSpeechSpeedChanged();
            }
        }

        private void OnSpeechSpeedChanged()
        {
            if (_isLoadingScene || _currentScene == null) return;
            if (_selectedSpeechSpeedIndex >= 0 && _selectedSpeechSpeedIndex < SpeechSpeedValues.Length)
                _currentScene.SpeechSpeed = SpeechSpeedValues[_selectedSpeechSpeedIndex];
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

        /// <summary>Raised when scene preview video is ready. Args: video file path.</summary>
        public event Action<string>? PreviewVideoReady;

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
        public ICommand PreviewSceneCommand { get; }
        public ICommand StopPreviewCommand { get; }
        public ICommand ExportVideoCommand { get; }
        public ICommand AddOverlayCommand { get; }
        public ICommand RemoveOverlayCommand { get; }
        public ICommand AddCoverTemplateCommand { get; }
        public ICommand SelectIntroCommand { get; }
        public ICommand ClearIntroCommand { get; }
        public ICommand SelectOutroCommand { get; }
        public ICommand ClearOutroCommand { get; }
        public ICommand SelectWatermarkCommand { get; }
        public ICommand ClearWatermarkCommand { get; }
        public ICommand SaveTemplateCommand { get; }
        public ICommand LoadTemplateCommand { get; }
        public ICommand OpenOutputFolderCommand { get; }
        public ICommand BgmSettingsCommand { get; }
        public ICommand ShowTutorialCommand { get; }
        public ICommand ShowFaqCommand { get; }
        public ICommand ShowLicenseManagerCommand { get; }
        public ICommand ShowLicenseInfoCommand { get; }
        public ICommand ShowAboutCommand { get; }
        public ICommand ShowShortcutsCommand { get; }
        public ICommand ShowTermsCommand { get; }
        public ICommand ShowPrivacyCommand { get; }
        public ICommand OpenRecentFileCommand { get; }
        public ICommand CancelExportCommand { get; }
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
            ImportPptxCommand = new AsyncRelayCommand(ImportPptxAsync);
            AddSceneCommand = new RelayCommand(AddScene);
            RemoveSceneCommand = new RelayCommand(RemoveScene);
            MoveSceneUpCommand = new RelayCommand(MoveSceneUp);
            MoveSceneDownCommand = new RelayCommand(MoveSceneDown);
            SelectMediaCommand = new RelayCommand(SelectMedia);
            ClearMediaCommand = new RelayCommand(ClearMedia);
            OpenStyleDialogCommand = new RelayCommand(OpenStyleDialog);
            PreviewAudioCommand = new AsyncRelayCommand(PreviewCurrentScene);
            PreviewSceneCommand = new AsyncRelayCommand(PreviewCurrentSceneVideo);
            StopPreviewCommand = new RelayCommand(() => StopAudioRequested?.Invoke());
            ExportVideoCommand = new AsyncRelayCommand(ExportVideo);
            AddOverlayCommand = new RelayCommand(AddOverlay);
            RemoveOverlayCommand = new RelayCommand(RemoveOverlay);
            AddCoverTemplateCommand = new RelayCommand(AddCoverTemplate);
            SelectIntroCommand = new RelayCommand(SelectIntro);
            ClearIntroCommand = new RelayCommand(ClearIntro);
            SelectOutroCommand = new RelayCommand(SelectOutro);
            ClearOutroCommand = new RelayCommand(ClearOutro);
            SelectWatermarkCommand = new RelayCommand(SelectWatermark);
            ClearWatermarkCommand = new RelayCommand(ClearWatermark);
            SaveTemplateCommand = new RelayCommand(SaveTemplate);
            LoadTemplateCommand = new RelayCommand(LoadTemplate);
            OpenOutputFolderCommand = new RelayCommand(OpenOutputFolder);
            BgmSettingsCommand = new RelayCommand(OpenBgmSettings);
            ShowTutorialCommand = new RelayCommand(ShowTutorial);
            ShowFaqCommand = new RelayCommand(ShowFaq);
            ShowLicenseManagerCommand = new RelayCommand(ShowLicenseManager);
            ShowLicenseInfoCommand = new RelayCommand(ShowLicenseInfo);
            ShowAboutCommand = new RelayCommand(ShowAbout);
            ShowShortcutsCommand = new RelayCommand(ShowShortcuts);
            ShowTermsCommand = new RelayCommand(ShowTerms);
            ShowPrivacyCommand = new RelayCommand(ShowPrivacy);
            OpenRecentFileCommand = new RelayCommand(p => { if (p is string path) OpenRecentFile(path); });
            CancelExportCommand = new RelayCommand(CancelExport, () => _isExporting);
            ExitCommand = new RelayCommand(() => ExitRequested?.Invoke());

            // Auto-save every 5 minutes
            _autoSaveTimer = new Timer(_ => AutoSave(), null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));

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

        private async void UpdateStatusText()
        {
            string vvStatus;
            try
            {
                var version = await _voiceVoxClient.CheckConnectionAsync();
                vvStatus = version != null ? $"VOICEVOX: ✓接続OK (v{version})" : "VOICEVOX: ✗未接続";
            }
            catch
            {
                vvStatus = "VOICEVOX: ✗未接続";
            }

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
                MediaName = Path.GetFileName(_currentScene.MediaPath) ?? string.Empty;
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

            RefreshOverlayList();
            if (_currentScene.TextOverlays.Count > 0)
                SelectedOverlayIndex = 0;
            else
                SelectedOverlayIndex = -1;

            // Speech speed
            SelectedSpeechSpeedIndex = Array.IndexOf(SpeechSpeedValues, _currentScene.SpeechSpeed);
            if (_selectedSpeechSpeedIndex < 0) SelectedSpeechSpeedIndex = 1;

            _isLoadingScene = false;
        }

        private void AddScene()
        {
            _isDirty = true;
            _project.AddScene();
            RefreshSceneList();
            SelectedSceneIndex = _project.Scenes.Count - 1;
            _logger.Log($"シーン {_project.Scenes.Count} を追加しました。");
        }

        private void RemoveScene()
        {
            _isDirty = true;
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
            _isDirty = true;
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

        #region Text Overlays

        private void RefreshOverlayList()
        {
            OverlayItems.Clear();
            if (_currentScene == null) return;

            for (int i = 0; i < _currentScene.TextOverlays.Count; i++)
            {
                OverlayItems.Add(new OverlayListItem(_currentScene.TextOverlays[i], i));
            }

            OnPropertyChanged(nameof(OverlayListVisible));
            OnPropertyChanged(nameof(OverlayEditorVisible));
        }

        private void AddOverlay()
        {
            if (_currentScene == null) return;

            var overlay = new TextOverlay { Text = "テキスト" };
            _currentScene.TextOverlays.Add(overlay);
            RefreshOverlayList();
            SelectedOverlayIndex = _currentScene.TextOverlays.Count - 1;
            _logger.Log("テキストオーバーレイを追加しました。");
        }

        private void RemoveOverlay()
        {
            if (_currentScene == null || _selectedOverlayIndex < 0 ||
                _selectedOverlayIndex >= _currentScene.TextOverlays.Count)
                return;

            _currentScene.TextOverlays.RemoveAt(_selectedOverlayIndex);
            RefreshOverlayList();

            if (_currentScene.TextOverlays.Count > 0)
                SelectedOverlayIndex = Math.Min(_selectedOverlayIndex, _currentScene.TextOverlays.Count - 1);
            else
                SelectedOverlayIndex = -1;

            _logger.Log("テキストオーバーレイを削除しました。");
        }

        private void AddCoverTemplate()
        {
            if (_currentScene == null) return;

            _currentScene.TextOverlays.Clear();
            _currentScene.TextOverlays.Add(TextOverlay.CreateTitle());
            _currentScene.TextOverlays.Add(TextOverlay.CreateSubheading());

            RefreshOverlayList();
            SelectedOverlayIndex = 0;
            _logger.Log("表紙テンプレートを適用しました。");
        }

        private void OnOverlaySelected()
        {
            OnPropertyChanged(nameof(OverlayEditorVisible));

            if (_selectedOverlayIndex < 0 || _currentScene == null ||
                _selectedOverlayIndex >= _currentScene.TextOverlays.Count)
                return;

            _isLoadingOverlay = true;
            var overlay = _currentScene.TextOverlays[_selectedOverlayIndex];

            OverlayText = overlay.Text;
            OverlayXPercent = overlay.XPercent.ToString("F1", CultureInfo.InvariantCulture);
            OverlayYPercent = overlay.YPercent.ToString("F1", CultureInfo.InvariantCulture);
            OverlayFontSize = overlay.FontSize.ToString();

            SelectedAlignmentIndex = overlay.Alignment switch
            {
                Models.TextAlignment.Left => 1,
                Models.TextAlignment.Right => 2,
                _ => 0
            };

            // Find matching color
            SelectedOverlayColorIndex = FindColorIndex(overlay.TextColor);

            _isLoadingOverlay = false;
        }

        private int FindColorIndex(int[] color)
        {
            for (int i = 0; i < OverlayColorValues.Length; i++)
            {
                if (OverlayColorValues[i][0] == color[0] &&
                    OverlayColorValues[i][1] == color[1] &&
                    OverlayColorValues[i][2] == color[2])
                    return i;
            }
            return 0; // default to white
        }

        private TextOverlay? GetSelectedOverlay()
        {
            if (_currentScene == null || _selectedOverlayIndex < 0 ||
                _selectedOverlayIndex >= _currentScene.TextOverlays.Count)
                return null;
            return _currentScene.TextOverlays[_selectedOverlayIndex];
        }

        private void OnOverlayTextChanged()
        {
            if (_isLoadingOverlay) return;
            var overlay = GetSelectedOverlay();
            if (overlay == null) return;
            overlay.Text = _overlayText;

            if (_selectedOverlayIndex >= 0 && _selectedOverlayIndex < OverlayItems.Count)
                OverlayItems[_selectedOverlayIndex].UpdateLabel(_selectedOverlayIndex);
        }

        private void OnOverlayPositionChanged()
        {
            if (_isLoadingOverlay) return;
            var overlay = GetSelectedOverlay();
            if (overlay == null) return;

            if (double.TryParse(_overlayXPercent, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var x))
                overlay.XPercent = Math.Clamp(x, 0, 100);

            if (double.TryParse(_overlayYPercent, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var y))
                overlay.YPercent = Math.Clamp(y, 0, 100);
        }

        private void OnOverlayFontSizeChanged()
        {
            if (_isLoadingOverlay) return;
            var overlay = GetSelectedOverlay();
            if (overlay == null) return;

            if (int.TryParse(_overlayFontSize, out var size))
                overlay.FontSize = Math.Clamp(size, 8, 200);
        }

        private void OnOverlayAlignmentChanged()
        {
            if (_isLoadingOverlay) return;
            var overlay = GetSelectedOverlay();
            if (overlay == null) return;

            overlay.Alignment = _selectedAlignmentIndex switch
            {
                1 => Models.TextAlignment.Left,
                2 => Models.TextAlignment.Right,
                _ => Models.TextAlignment.Center
            };
        }

        private void OnOverlayColorChanged()
        {
            if (_isLoadingOverlay) return;
            var overlay = GetSelectedOverlay();
            if (overlay == null) return;

            if (_selectedOverlayColorIndex >= 0 && _selectedOverlayColorIndex < OverlayColorValues.Length)
                overlay.TextColor = (int[])OverlayColorValues[_selectedOverlayColorIndex].Clone();
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

        #region Scene Preview

        private async Task PreviewCurrentSceneVideo()
        {
            if (_currentScene == null)
            {
                _dialogService?.ShowInfo("シーンを選択してください。", "シーンプレビュー");
                return;
            }

            if (_ffmpegWrapper == null || !_ffmpegWrapper.CheckAvailable())
            {
                _dialogService?.ShowError(
                    "ffmpegが検出されていません。\nシーンプレビューにはffmpegが必要です。",
                    "プレビューエラー");
                return;
            }

            var previewDir = Path.Combine(Path.GetTempPath(), "insightmovie_cache", "preview");
            Directory.CreateDirectory(previewDir);

            // Clean up old preview files to prevent disk bloat
            try
            {
                foreach (var old in Directory.GetFiles(previewDir, "preview_*.mp4"))
                    File.Delete(old);
            }
            catch { /* best-effort cleanup */ }

            var previewPath = Path.Combine(previewDir, $"preview_{Guid.NewGuid():N}.mp4");

            string resolution = _selectedResolutionIndex == 1 ? "1920x1080" : "1080x1920";
            int exportSpeakerId = _defaultSpeakerId;
            if (_selectedExportSpeakerIndex >= 0 && _selectedExportSpeakerIndex < ExportSpeakers.Count)
                exportSpeakerId = ExportSpeakers[_selectedExportSpeakerIndex].StyleId;

            var style = GetStyleForScene(_currentScene);
            var sceneSnapshot = _project.Clone().Scenes
                .ElementAtOrDefault(_selectedSceneIndex) ?? _currentScene;

            var progress = new Progress<string>(msg => _logger.Log(msg));

            _logger.Log("シーンプレビューを生成中...");
            ExportProgressVisible = true;

            try
            {
                var exportService = new ExportService(_ffmpegWrapper, _voiceVoxClient, _audioCache);
                var success = await Task.Run(() =>
                    exportService.GeneratePreview(sceneSnapshot, previewPath, resolution, 30,
                        exportSpeakerId, style, progress, CancellationToken.None));

                if (success && File.Exists(previewPath))
                {
                    _logger.Log("シーンプレビュー完了");
                    PreviewVideoReady?.Invoke(previewPath);
                }
                else
                {
                    _dialogService?.ShowError("シーンプレビューの生成に失敗しました。", "プレビューエラー");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError("シーンプレビューエラー", ex);
                _dialogService?.ShowError($"プレビューエラー:\n{ex.Message}", "エラー");
            }
            finally
            {
                ExportProgressVisible = false;
            }
        }

        #endregion

        #region Intro/Outro/Watermark/Template

        private void SelectIntro()
        {
            if (_dialogService == null) return;
            var path = _dialogService.ShowOpenFileDialog(
                "イントロ画像/動画を選択",
                "メディアファイル|*.png;*.jpg;*.jpeg;*.bmp;*.mp4;*.mov|すべてのファイル|*.*");
            if (!string.IsNullOrEmpty(path))
            {
                IntroFilePath = path;
                _project.IntroMediaPath = path;
                OnPropertyChanged(nameof(HasIntro));
                OnPropertyChanged(nameof(IntroFileName));
                _logger.Log($"イントロ設定: {Path.GetFileName(path)}");
            }
        }

        private void ClearIntro()
        {
            IntroFilePath = string.Empty;
            _project.IntroMediaPath = null;
            OnPropertyChanged(nameof(HasIntro));
            OnPropertyChanged(nameof(IntroFileName));
        }

        private void SelectOutro()
        {
            if (_dialogService == null) return;
            var path = _dialogService.ShowOpenFileDialog(
                "アウトロ画像/動画を選択",
                "メディアファイル|*.png;*.jpg;*.jpeg;*.bmp;*.mp4;*.mov|すべてのファイル|*.*");
            if (!string.IsNullOrEmpty(path))
            {
                OutroFilePath = path;
                _project.OutroMediaPath = path;
                OnPropertyChanged(nameof(HasOutro));
                OnPropertyChanged(nameof(OutroFileName));
                _logger.Log($"アウトロ設定: {Path.GetFileName(path)}");
            }
        }

        private void ClearOutro()
        {
            OutroFilePath = string.Empty;
            _project.OutroMediaPath = null;
            OnPropertyChanged(nameof(HasOutro));
            OnPropertyChanged(nameof(OutroFileName));
        }

        private void SelectWatermark()
        {
            if (_dialogService == null) return;
            var path = _dialogService.ShowOpenFileDialog(
                "ロゴ画像を選択",
                "画像ファイル|*.png;*.jpg;*.jpeg;*.bmp;*.gif|すべてのファイル|*.*");
            if (!string.IsNullOrEmpty(path))
            {
                WatermarkFilePath = path;
                SyncWatermarkToProject();
                OnPropertyChanged(nameof(HasWatermark));
                OnPropertyChanged(nameof(WatermarkFileName));
                _logger.Log($"ロゴ設定: {Path.GetFileName(path)}");
            }
        }

        private void ClearWatermark()
        {
            WatermarkFilePath = string.Empty;
            _project.Watermark = new WatermarkSettings();
            OnPropertyChanged(nameof(HasWatermark));
            OnPropertyChanged(nameof(WatermarkFileName));
        }

        private void SyncWatermarkToProject()
        {
            _project.Watermark.Enabled = true;
            _project.Watermark.ImagePath = _watermarkFilePath;
            _project.Watermark.Position = (_selectedWatermarkPosIndex >= 0 &&
                _selectedWatermarkPosIndex < WatermarkPositionValues.Length)
                ? WatermarkPositionValues[_selectedWatermarkPosIndex]
                : "bottom-right";
        }

        private void OpenOutputFolder()
        {
            if (_dialogService == null) return;
            var folder = _project.Output.OutputPath;
            if (!string.IsNullOrEmpty(folder))
            {
                var dir = Path.GetDirectoryName(folder);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    OpenFileRequested?.Invoke(dir);
            }
        }

        private void SaveTemplate()
        {
            var name = $"テンプレート_{DateTime.Now:yyyyMMdd_HHmmss}";
            if (HasWatermark) SyncWatermarkToProject();

            var template = TemplateService.CreateFromProject(_project, name);
            TemplateService.SaveTemplate(template);
            _logger.Log($"テンプレート保存: {name}");
        }

        private void LoadTemplate()
        {
            var templates = TemplateService.LoadAllTemplates();
            if (templates.Count == 0)
            {
                _dialogService?.ShowInfo("保存済みテンプレートがありません。", "テンプレート");
                return;
            }

            ProjectTemplate template;
            if (templates.Count == 1)
            {
                template = templates[0];
            }
            else
            {
                var names = templates.Select(t => $"{t.Name}  ({t.CreatedAt:yyyy/MM/dd HH:mm})").ToArray();
                var idx = _dialogService?.ShowListSelectDialog("テンプレートを選択", names) ?? -1;
                if (idx < 0) return;
                template = templates[idx];
            }

            TemplateService.ApplyToProject(template, _project);

            // Sync project settings back to UI
            if (_project.HasIntro)
            {
                IntroFilePath = _project.IntroMediaPath!;
                OnPropertyChanged(nameof(HasIntro));
                OnPropertyChanged(nameof(IntroFileName));
            }
            if (_project.HasOutro)
            {
                OutroFilePath = _project.OutroMediaPath!;
                OnPropertyChanged(nameof(HasOutro));
                OnPropertyChanged(nameof(OutroFileName));
            }
            if (_project.Watermark.HasWatermark)
            {
                WatermarkFilePath = _project.Watermark.ImagePath!;
                OnPropertyChanged(nameof(HasWatermark));
                OnPropertyChanged(nameof(WatermarkFileName));
            }

            UpdateBgmStatus();
            _logger.Log($"テンプレート適用: {template.Name}");
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
                "InsightCast.mp4");

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
            ExportProgressValue = 0;
            ExportProgressVisible = true;

            var progress = new Progress<string>(msg =>
            {
                _logger.Log(msg);
                // Parse "[N/M]" progress format to update progress bar
                if (msg.StartsWith("[") && msg.Contains('/'))
                {
                    var bracket = msg.IndexOf(']');
                    if (bracket > 0)
                    {
                        var parts = msg[1..bracket].Split('/');
                        if (parts.Length == 2
                            && int.TryParse(parts[0], out int current)
                            && int.TryParse(parts[1], out int total)
                            && total > 0)
                        {
                            ExportProgressValue = (double)current / total * 100;
                        }
                    }
                }
            });
            var ct = _exportCts.Token;

            _logger.Log($"書き出しを開始: {outputPath}");

            // Sync UI settings to project before snapshot
            if (HasWatermark) SyncWatermarkToProject();

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
                var exportResult = await Task.Run(() =>
                    exportService.ExportFull(projectSnapshot, outputPath, resolution, fps,
                        exportSpeakerId, GetStyleSnapshot, progress, ct), ct);

                if (exportResult.Success)
                {
                    var extras = new List<string>();
                    if (!string.IsNullOrEmpty(exportResult.ThumbnailPath))
                        extras.Add($"サムネイル: {exportResult.ThumbnailPath}");
                    if (!string.IsNullOrEmpty(exportResult.ChapterFilePath))
                        extras.Add($"チャプター: {exportResult.ChapterFilePath}");
                    if (!string.IsNullOrEmpty(exportResult.MetadataFilePath))
                        extras.Add($"メタデータ: {exportResult.MetadataFilePath}");
                    foreach (var extra in extras) _logger.Log(extra);
                }

                OnExportFinished(exportResult.Success, outputPath);
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
                _project.Output.OutputPath = message;
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

        /// <summary>
        /// Loads a project created externally (e.g. from QuickMode).
        /// </summary>
        public void LoadProject(Project project)
        {
            _project = project;
            _sceneSubtitleStyles.Clear();
            WindowTitle = "InsightCast - 取込プロジェクト";
            RefreshSceneList();
            UpdateBgmStatus();
            SyncProjectToUI();
            _logger.Log($"プロジェクトを読み込みました ({project.Scenes.Count} シーン)");
        }

        private void SyncProjectToUI()
        {
            if (_project.HasIntro)
            {
                IntroFilePath = _project.IntroMediaPath!;
                OnPropertyChanged(nameof(HasIntro));
                OnPropertyChanged(nameof(IntroFileName));
            }
            if (_project.HasOutro)
            {
                OutroFilePath = _project.OutroMediaPath!;
                OnPropertyChanged(nameof(HasOutro));
                OnPropertyChanged(nameof(OutroFileName));
            }
            if (_project.Watermark.HasWatermark)
            {
                WatermarkFilePath = _project.Watermark.ImagePath!;
                OnPropertyChanged(nameof(HasWatermark));
                OnPropertyChanged(nameof(WatermarkFileName));
            }
        }

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
            IntroFilePath = string.Empty;
            OutroFilePath = string.Empty;
            WatermarkFilePath = string.Empty;
            OnPropertyChanged(nameof(HasIntro));
            OnPropertyChanged(nameof(IntroFileName));
            OnPropertyChanged(nameof(HasOutro));
            OnPropertyChanged(nameof(OutroFileName));
            OnPropertyChanged(nameof(HasWatermark));
            OnPropertyChanged(nameof(WatermarkFileName));
            WindowTitle = "InsightCast - 新規プロジェクト";
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
                _config.AddRecentFile(path);
                _sceneSubtitleStyles.Clear();
                WindowTitle = $"InsightCast - {Path.GetFileNameWithoutExtension(path)}";
                RefreshSceneList();
                UpdateBgmStatus();
                SyncProjectToUI();
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
                _isDirty = false;
                _config.AddRecentFile(_project.ProjectPath!);
                _logger.Log($"プロジェクトを保存しました: {_project.ProjectPath}");
            }
            catch (Exception ex)
            {
                _dialogService?.ShowError($"保存に失敗しました:\n{ex.Message}", "保存エラー");
            }
        }

        public List<string> RecentFiles => _config.RecentFiles;

        private void OpenRecentFile(string path)
        {
            if (!File.Exists(path))
            {
                _dialogService?.ShowWarning($"ファイルが見つかりません:\n{path}", "最近使ったファイル");
                return;
            }

            try
            {
                _project = Project.Load(path);
                _config.AddRecentFile(path);
                _sceneSubtitleStyles.Clear();
                WindowTitle = $"InsightCast - {Path.GetFileNameWithoutExtension(path)}";
                RefreshSceneList();
                UpdateBgmStatus();
                SyncProjectToUI();
                _logger.Log($"プロジェクトを開きました: {path}");
            }
            catch (Exception ex)
            {
                _dialogService?.ShowError($"プロジェクトファイルの読み込みに失敗しました:\n{ex.Message}", "読み込みエラー");
            }
        }

        private void AutoSave()
        {
            try
            {
                var autoSaveDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "InsightCast", "AutoSave");
                Directory.CreateDirectory(autoSaveDir);
                var autoSavePath = Path.Combine(autoSaveDir, "autosave.json");
                _project.Save(autoSavePath);
            }
            catch
            {
                // Auto-save failure should not disrupt the user
            }
        }

        private void SaveProjectAs()
        {
            if (_dialogService == null) return;

            var path = _dialogService.ShowSaveFileDialog(
                "プロジェクトファイルを保存",
                "JSONファイル|*.json|すべてのファイル|*.*",
                ".json",
                "InsightCast.json");

            if (path == null) return;

            try
            {
                _project.Save(path);
                _isDirty = false;
                _config.AddRecentFile(path);
                WindowTitle = $"InsightCast - {Path.GetFileNameWithoutExtension(path)}";
                _logger.Log($"プロジェクトを保存しました: {path}");
            }
            catch (Exception ex)
            {
                _dialogService?.ShowError($"保存に失敗しました:\n{ex.Message}", "保存エラー");
            }
        }

        private async Task ImportPptxAsync()
        {
            if (_dialogService == null) return;

            if (!License.CanUseFeature(_currentPlan, "pptx_import"))
            {
                _dialogService.ShowInfo(
                    "PPTX取込機能はTrial・Proプラン以上でご利用いただけます。\nライセンスをアップグレードしてください。",
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
                _logger.Log($"PPTX取込を開始: {path}");

                var outputDir = Path.Combine(
                    Path.GetTempPath(),
                    "insightmovie_cache",
                    "pptx_slides",
                    $"import_{Guid.NewGuid():N}");

                var importer = new Utils.PptxImporter(
                    (current, total, msg) => _logger.Log(msg));

                var slides = await Task.Run(() => importer.ImportPptx(path, outputDir));

                if (slides.Count == 0)
                {
                    _dialogService.ShowInfo("スライドが見つかりませんでした。", "取込結果");
                    return;
                }

                foreach (var slide in slides)
                {
                    var scene = new Scene
                    {
                        NarrationText = slide.Notes,
                        SubtitleText = string.IsNullOrWhiteSpace(slide.SlideText) ? null : slide.SlideText
                    };
                    if (!string.IsNullOrEmpty(slide.ImagePath) && File.Exists(slide.ImagePath))
                    {
                        scene.MediaPath = slide.ImagePath;
                        scene.MediaType = MediaType.Image;
                    }
                    _project.Scenes.Add(scene);
                }

                RefreshSceneList();

                int imageCount = slides.Count(s => !string.IsNullOrEmpty(s.ImagePath) && File.Exists(s.ImagePath));
                _logger.Log($"PPTXからシーンを取り込みました ({slides.Count} スライド, {imageCount} 画像): {path}");

                if (imageCount == 0)
                {
                    _dialogService.ShowInfo(
                        $"{slides.Count} 件のスライドのノートを取り込みました。\n" +
                        "スライド画像の取込にはPowerPointのインストールが必要です。",
                        "取込結果");
                }
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
            var email = _config.LicenseEmail;
            _licenseInfo = License.ValidateLicenseKey(key, email);
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
                : "字幕機能はTrial・Proプラン以上で利用可能です";
        }

        #endregion

        #region Menu Handlers

        private void ShowTutorial()
        {
            _dialogService?.ShowInfo(
                "InsightCast チュートリアル\n─────────────────────\n\n" +
                "■ かんたんモード（QuickMode）\n" +
                "  1. ファイルをドラッグ＆ドロップ、またはファイル選択ボタンで素材を取り込み\n" +
                "  2. 話者・解像度・速度などを設定\n" +
                "  3.「生成」ボタンで動画を自動生成\n\n" +
                "■ 詳細エディタ（MainWindow）\n" +
                "  1. 左パネルの「＋追加」でシーンを追加\n" +
                "  2.「選択」ボタンで画像・動画を設定\n" +
                "  3. ナレーション欄にテキストを入力\n" +
                "  4.「▶音声再生」でプレビュー確認\n" +
                "  5. 字幕テキスト・スタイルを設定\n" +
                "  6.「動画を書き出し」で生成\n\n" +
                "■ テキストオーバーレイ\n" +
                "  各シーンに自由なテキストを配置できます。\n" +
                "  位置（X/Y%）、サイズ、色、配置を個別設定可能。\n" +
                "  「表紙テンプレート」ボタンでタイトル＋サブタイトルを一括追加。\n\n" +
                "■ テンプレート\n" +
                "  BGM・透かし・解像度などの設定をテンプレートとして保存・読込可能。\n\n" +
                "■ PPTX取込\n" +
                "  PowerPointファイルをドロップすると、スライドごとにシーンが自動作成されます。\n" +
                "  （Trial以上のプランが必要）",
                "チュートリアル");
        }

        private void ShowFaq()
        {
            _dialogService?.ShowInfo(
                "よくある質問 (FAQ)\n─────────────────────\n\n" +
                "Q: VOICEVOXが接続できません。\n" +
                "A: VOICEVOXエンジンが起動しているか確認してください。\n" +
                "   初回起動時にセットアップウィザードで接続設定を行います。\n\n" +
                "Q: 動画の書き出しに失敗します。\n" +
                "A: FFmpegがインストールされ、PATHに含まれているか確認してください。\n" +
                "   起動時にFFmpegが見つからない場合は警告が表示されます。\n\n" +
                "Q: ライセンスキーの入力方法は？\n" +
                "A: メニュー「ヘルプ」→「ライセンス管理」から入力してください。\n\n" +
                "Q: 字幕やトランジションが使えません。\n" +
                "A: Trial以上のプランが必要です。「ヘルプ」→「ライセンス管理」から\n" +
                "   ライセンスをアクティベートしてください。\n\n" +
                "Q: 対応ファイル形式は？\n" +
                "A: 画像: PNG, JPG, BMP, GIF / 動画: MP4, AVI, MOV, WMV, MKV\n" +
                "   音声(BGM): MP3, WAV, OGG, FLAC, AAC, M4A\n" +
                "   その他: PPTX, TXT, MD\n\n" +
                "Q: 書き出しをキャンセルしたい。\n" +
                "A: 進捗バー横の「キャンセル」ボタンをクリックしてください。\n\n" +
                "Q: テンプレートはどこに保存されますか？\n" +
                "A: %LOCALAPPDATA%\\InsightCast\\Templates に保存されます。\n\n" +
                "Q: お問い合わせ先は？\n" +
                "A: support@h-insight.jp までご連絡ください。",
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
            var version = typeof(MainWindowViewModel).Assembly.GetName().Version;
            var versionStr = version != null ? $"v{version.Major}.{version.Minor}.{version.Build}" : "v1.0.0";

            _dialogService?.ShowInfo(
                $"InsightCast {versionStr}\n\n" +
                "VOICEVOX音声エンジンを使用した動画自動生成ツール\n\n" +
                "テキストを入力するだけで、ナレーション付き動画を\n簡単に作成できます。\n\n" +
                "開発元: Harmonic Insight Inc.\n" +
                "Web: https://h-insight.jp\n" +
                "サポート: support@h-insight.jp\n\n" +
                "Copyright (C) 2024-2026 Harmonic Insight Inc.\nAll rights reserved.",
                "InsightCastについて");
        }

        private void ShowShortcuts()
        {
            _dialogService?.ShowInfo(
                "キーボードショートカット一覧\n─────────────────────\n\n" +
                "Ctrl+N          新規プロジェクト\n" +
                "Ctrl+O          プロジェクトを開く\n" +
                "Ctrl+S          プロジェクトを保存\n" +
                "Ctrl+Shift+S    名前を付けて保存\n" +
                "Ctrl+T          シーンを追加\n" +
                "Delete          シーンを削除\n" +
                "Ctrl+Up         シーンを上へ移動\n" +
                "Ctrl+Down       シーンを下へ移動\n" +
                "F1              チュートリアルを表示",
                "キーボードショートカット");
        }

        private void ShowTerms()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://h-insight.jp/terms",
                    UseShellExecute = true
                });
            }
            catch
            {
                _dialogService?.ShowInfo(
                    "利用規約は以下のURLからご確認ください:\nhttps://h-insight.jp/terms\n\n" +
                    "お問い合わせ: info@h-insight.jp",
                    "利用規約");
            }
        }

        private void ShowPrivacy()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://h-insight.jp/privacy",
                    UseShellExecute = true
                });
            }
            catch
            {
                _dialogService?.ShowInfo(
                    "プライバシーポリシーは以下のURLからご確認ください:\nhttps://h-insight.jp/privacy\n\n" +
                    "お問い合わせ: info@h-insight.jp",
                    "プライバシーポリシー");
            }
        }

        #endregion

        #region Window Lifecycle

        public bool CanClose()
        {
            if (_isExporting)
            {
                if (_dialogService?.ShowConfirmation(
                    "書き出し処理が実行中です。中断して終了しますか？",
                    "終了確認") == true)
                {
                    _exportCts?.Cancel();
                    return true;
                }
                return false;
            }

            if (_isDirty)
            {
                if (_dialogService?.ShowConfirmation(
                    "変更が保存されていません。保存せずに終了しますか？",
                    "未保存の変更") != true)
                {
                    return false;
                }
            }

            // Dispose auto-save timer
            _autoSaveTimer?.Dispose();
            _autoSaveTimer = null;

            // Clean up preview files and PPTX temp directories
            try
            {
                var cacheBase = Path.Combine(Path.GetTempPath(), "insightmovie_cache");
                var previewDir = Path.Combine(cacheBase, "preview");
                if (Directory.Exists(previewDir))
                    Directory.Delete(previewDir, true);

                var pptxDir = Path.Combine(cacheBase, "pptx_slides");
                if (Directory.Exists(pptxDir))
                    Directory.Delete(pptxDir, true);
            }
            catch { /* best-effort */ }

            return true;
        }

        #endregion
    }
}

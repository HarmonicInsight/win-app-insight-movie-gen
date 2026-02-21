using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// <summary>Thumbnail data for the scene preview strip.</summary>
    public class ThumbnailItem : ViewModelBase
    {
        public int Index { get; set; }
        public string? ImagePath { get; set; }
        public string Label { get; set; } = string.Empty;
        public bool HasImage => !string.IsNullOrEmpty(ImagePath) && File.Exists(ImagePath);
    }

    public class QuickModeViewModel : ViewModelBase
    {
        private readonly VoiceVoxClient _voiceVoxClient;
        private readonly int _defaultSpeakerId;
        private readonly FFmpegWrapper? _ffmpegWrapper;
        private readonly Config _config;
        private readonly AudioCache _audioCache;
        private readonly IAppLogger _logger;
        private IDialogService? _dialogService;

        private Project? _project;
        private bool _isGenerating;
        private bool _isImporting;
        private bool _hasProject;
        private string _statusText = string.Empty;
        private string _importSummary = string.Empty;
        private int _selectedSpeakerIndex;
        private int _selectedResolutionIndex;
        private double _progressValue;
        private bool _progressVisible;
        private string _progressText = string.Empty;
        private bool _isComplete;
        private string _outputPath = string.Empty;
        private CancellationTokenSource? _exportCts;

        // Enhanced settings
        private int _selectedTransitionIndex;
        private string _bgmFilePath = string.Empty;
        private double _bgmVolume = 0.3;
        private double _speechSpeed = 1.0;
        private bool _generateThumbnail = true;
        private string _thumbnailPath = string.Empty;
        private string _metadataPath = string.Empty;

        // Intro/Outro/Watermark
        private string _introFilePath = string.Empty;
        private string _outroFilePath = string.Empty;
        private string _watermarkFilePath = string.Empty;
        private int _selectedWatermarkPosIndex = 3; // bottom-right default

        public QuickModeViewModel(VoiceVoxClient voiceVoxClient, int speakerId,
                                   FFmpegWrapper? ffmpegWrapper, Config config)
        {
            _voiceVoxClient = voiceVoxClient;
            _defaultSpeakerId = speakerId;
            _ffmpegWrapper = ffmpegWrapper;
            _config = config;
            _audioCache = new AudioCache();
            _logger = new AppLogger();

            GenerateCommand = new AsyncRelayCommand(GenerateVideo, () => _hasProject && !_isGenerating);
            OpenEditorCommand = new RelayCommand(() =>
            {
                ApplySettingsToProject();
                OpenEditorRequested?.Invoke(_project!);
            }, () => _hasProject);
            OpenOutputCommand = new RelayCommand(OpenOutput, () => _isComplete);
            GenerateAnotherCommand = new RelayCommand(ResetForRegenerate);
            ResetAllCommand = new RelayCommand(ResetAll);
            CancelCommand = new RelayCommand(Cancel, () => _isGenerating);
            PreviewSpeakerCommand = new AsyncRelayCommand(PreviewSpeaker);
            SelectBgmCommand = new RelayCommand(SelectBgm);
            ClearBgmCommand = new RelayCommand(ClearBgm);
            BatchImportCommand = new AsyncRelayCommand(BatchImport);
            SelectIntroCommand = new RelayCommand(SelectIntro);
            ClearIntroCommand = new RelayCommand(ClearIntro);
            SelectOutroCommand = new RelayCommand(SelectOutro);
            ClearOutroCommand = new RelayCommand(ClearOutro);
            SelectWatermarkCommand = new RelayCommand(SelectWatermark);
            ClearWatermarkCommand = new RelayCommand(ClearWatermark);
            OpenOutputFolderCommand = new RelayCommand(OpenOutputFolder);
            SaveTemplateCommand = new RelayCommand(SaveTemplate);
            LoadTemplateCommand = new RelayCommand(LoadTemplate);
        }

        public void SetDialogService(IDialogService dialogService) => _dialogService = dialogService;

        #region Properties

        public ObservableCollection<SpeakerItem> Speakers { get; } = new();
        public ObservableCollection<ThumbnailItem> Thumbnails { get; } = new();

        public int SelectedSpeakerIndex
        {
            get => _selectedSpeakerIndex;
            set => SetProperty(ref _selectedSpeakerIndex, value);
        }

        public int SelectedResolutionIndex
        {
            get => _selectedResolutionIndex;
            set => SetProperty(ref _selectedResolutionIndex, value);
        }

        public bool HasProject
        {
            get => _hasProject;
            private set
            {
                if (SetProperty(ref _hasProject, value))
                    OnPropertyChanged(nameof(ShowSettings));
            }
        }

        public bool IsGenerating
        {
            get => _isGenerating;
            private set
            {
                if (SetProperty(ref _isGenerating, value))
                    OnPropertyChanged(nameof(ShowSettings));
            }
        }

        public bool IsImporting
        {
            get => _isImporting;
            private set => SetProperty(ref _isImporting, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        public string ImportSummary
        {
            get => _importSummary;
            set => SetProperty(ref _importSummary, value);
        }

        public double ProgressValue
        {
            get => _progressValue;
            set => SetProperty(ref _progressValue, value);
        }

        public bool ProgressVisible
        {
            get => _progressVisible;
            set => SetProperty(ref _progressVisible, value);
        }

        public string ProgressText
        {
            get => _progressText;
            set => SetProperty(ref _progressText, value);
        }

        public bool IsComplete
        {
            get => _isComplete;
            private set
            {
                if (SetProperty(ref _isComplete, value))
                    OnPropertyChanged(nameof(ShowSettings));
            }
        }

        /// <summary>True when settings + generate button should be visible.</summary>
        public bool ShowSettings => _hasProject && !_isGenerating && !_isComplete;

        public string OutputPath
        {
            get => _outputPath;
            private set => SetProperty(ref _outputPath, value);
        }

        public IAppLogger Logger => _logger;

        // Enhanced settings properties
        public int SelectedTransitionIndex
        {
            get => _selectedTransitionIndex;
            set => SetProperty(ref _selectedTransitionIndex, value);
        }

        public string BgmFilePath
        {
            get => _bgmFilePath;
            set => SetProperty(ref _bgmFilePath, value);
        }

        public double BgmVolume
        {
            get => _bgmVolume;
            set => SetProperty(ref _bgmVolume, value);
        }

        public double SpeechSpeed
        {
            get => _speechSpeed;
            set => SetProperty(ref _speechSpeed, value);
        }

        public bool GenerateThumbnailEnabled
        {
            get => _generateThumbnail;
            set => SetProperty(ref _generateThumbnail, value);
        }

        public string ThumbnailPath
        {
            get => _thumbnailPath;
            private set => SetProperty(ref _thumbnailPath, value);
        }

        public string MetadataPath
        {
            get => _metadataPath;
            private set => SetProperty(ref _metadataPath, value);
        }

        public bool HasBgm => !string.IsNullOrEmpty(_bgmFilePath);
        public string BgmFileName => string.IsNullOrEmpty(_bgmFilePath)
            ? "（なし）"
            : Path.GetFileName(_bgmFilePath);

        public List<string> TransitionOptions { get; } =
            TransitionNames.DisplayNames.Values.ToList();

        public List<string> SpeedOptions { get; } = new()
        {
            "0.8x (ゆっくり)", "1.0x (標準)", "1.2x (やや速い)", "1.5x (速い)"
        };

        private static readonly double[] SpeedValues = { 0.8, 1.0, 1.2, 1.5 };

        private int _selectedSpeedIndex = 1;
        public int SelectedSpeedIndex
        {
            get => _selectedSpeedIndex;
            set
            {
                if (SetProperty(ref _selectedSpeedIndex, value) && value >= 0 && value < SpeedValues.Length)
                    SpeechSpeed = SpeedValues[value];
            }
        }

        public bool HasExtraOutputs => !string.IsNullOrEmpty(_thumbnailPath) || !string.IsNullOrEmpty(_metadataPath);

        // Intro/Outro
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

        // Watermark
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

        // Templates
        public ObservableCollection<string> TemplateNames { get; } = new();

        #endregion

        #region Commands

        public ICommand GenerateCommand { get; }
        public ICommand OpenEditorCommand { get; }
        public ICommand OpenOutputCommand { get; }
        public ICommand GenerateAnotherCommand { get; }
        public ICommand ResetAllCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand PreviewSpeakerCommand { get; }
        public ICommand SelectBgmCommand { get; }
        public ICommand ClearBgmCommand { get; }
        public ICommand BatchImportCommand { get; }
        public ICommand SelectIntroCommand { get; }
        public ICommand ClearIntroCommand { get; }
        public ICommand SelectOutroCommand { get; }
        public ICommand ClearOutroCommand { get; }
        public ICommand SelectWatermarkCommand { get; }
        public ICommand ClearWatermarkCommand { get; }
        public ICommand OpenOutputFolderCommand { get; }
        public ICommand SaveTemplateCommand { get; }
        public ICommand LoadTemplateCommand { get; }

        #endregion

        #region Events

        public event Action<Project>? OpenEditorRequested;
        public event Action<string>? OpenFileRequested;
        /// <summary>Raised when speaker audio should play. Args: wav file path.</summary>
        public event Action<string>? PlayAudioRequested;
        /// <summary>Raised when audio playback should stop.</summary>
        public event Action? StopAudioRequested;

        #endregion

        #region Initialization

        public async Task InitializeAsync()
        {
            await LoadSpeakers();
            _logger.Log("準備完了 — 資料をドロップしてください");
        }

        private async Task LoadSpeakers()
        {
            try
            {
                var speakers = await _voiceVoxClient.GetSpeakersAsync();
                Speakers.Clear();

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
                        Speakers.Add(new SpeakerItem { DisplayName = displayName, StyleId = styleId });
                    }
                }

                for (int i = 0; i < Speakers.Count; i++)
                {
                    if (Speakers[i].StyleId == _defaultSpeakerId)
                    {
                        SelectedSpeakerIndex = i;
                        break;
                    }
                }

                if (_selectedSpeakerIndex < 0 && Speakers.Count > 0)
                    SelectedSpeakerIndex = 0;
            }
            catch (Exception ex)
            {
                _logger.LogError("話者一覧の読み込みに失敗", ex);
            }
        }

        #endregion

        #region Speaker Preview (Fix 2)

        private async Task PreviewSpeaker()
        {
            if (_selectedSpeakerIndex < 0 || _selectedSpeakerIndex >= Speakers.Count) return;

            var speakerId = Speakers[_selectedSpeakerIndex].StyleId;
            var sampleText = "こんにちは、これはサンプル音声です。";

            try
            {
                StopAudioRequested?.Invoke();
                _logger.Log("試聴音声を生成中...");

                string audioPath;
                if (_audioCache.Exists(sampleText, speakerId))
                {
                    audioPath = _audioCache.GetCachePath(sampleText, speakerId);
                }
                else
                {
                    var audioData = await _voiceVoxClient.GenerateAudioAsync(sampleText, speakerId);
                    audioPath = _audioCache.Save(sampleText, speakerId, audioData);
                }

                PlayAudioRequested?.Invoke(audioPath);
                _logger.Log("試聴再生中...");
            }
            catch (Exception ex)
            {
                _logger.LogError("試聴エラー", ex);
            }
        }

        #endregion

        #region File Drop Handling

        public async Task HandleFileDropAsync(string[] files)
        {
            if (_isGenerating) return;

            // Fix 4: Show import progress
            IsImporting = true;
            ProgressVisible = true;
            ProgressValue = 0;
            ProgressText = "ファイルを読み込んでいます...";

            _project = new Project();
            _project.Scenes.Clear();

            int pptxCount = 0;
            int imageCount = 0;
            int videoCount = 0;
            int textCount = 0;

            for (int fi = 0; fi < files.Length; fi++)
            {
                var file = files[fi];
                var ext = Path.GetExtension(file).ToLowerInvariant();
                ProgressValue = (double)(fi) / files.Length * 100;
                ProgressText = $"読込中... ({fi + 1}/{files.Length}) {Path.GetFileName(file)}";

                switch (ext)
                {
                    case ".pptx":
                        await ImportPptxAsync(file);
                        pptxCount++;
                        break;

                    case ".png" or ".jpg" or ".jpeg" or ".bmp" or ".gif":
                        _project.Scenes.Add(new Scene
                        {
                            MediaPath = file,
                            MediaType = MediaType.Image
                        });
                        imageCount++;
                        break;

                    case ".mp4" or ".avi" or ".mov" or ".wmv" or ".mkv":
                        _project.Scenes.Add(new Scene
                        {
                            MediaPath = file,
                            MediaType = MediaType.Video,
                            KeepOriginalAudio = true
                        });
                        videoCount++;
                        break;

                    case ".txt" or ".md":
                        ImportTextFile(file);
                        textCount++;
                        break;
                }
            }

            IsImporting = false;
            ProgressVisible = false;
            ProgressValue = 0;

            if (_project.Scenes.Count == 0)
            {
                StatusText = "対応するファイルが見つかりませんでした";
                return;
            }

            HasProject = true;
            IsComplete = false;

            // Fix 1: Build thumbnails
            BuildThumbnails();

            var parts = new List<string>();
            if (pptxCount > 0) parts.Add($"PPTX {pptxCount}件");
            if (imageCount > 0) parts.Add($"画像 {imageCount}件");
            if (videoCount > 0) parts.Add($"動画 {videoCount}件");
            if (textCount > 0) parts.Add($"テキスト {textCount}件");

            ImportSummary = $"{_project.Scenes.Count} シーン（{string.Join("、", parts)}）";
            StatusText = "設定を確認して「動画を生成」をクリック";
            _logger.Log($"取込完了: {ImportSummary}");
        }

        private void BuildThumbnails()
        {
            Thumbnails.Clear();
            if (_project == null) return;

            for (int i = 0; i < _project.Scenes.Count; i++)
            {
                var scene = _project.Scenes[i];
                var narration = scene.NarrationText ?? "";
                var label = narration.Length > 20 ? narration.Substring(0, 20) + "..." : narration;
                if (string.IsNullOrWhiteSpace(label)) label = $"シーン {i + 1}";

                Thumbnails.Add(new ThumbnailItem
                {
                    Index = i,
                    ImagePath = scene.HasMedia ? scene.MediaPath : null,
                    Label = label
                });
            }
        }

        private async Task ImportPptxAsync(string path)
        {
            try
            {
                _logger.Log($"PPTX読込中: {Path.GetFileName(path)}");
                ProgressText = $"PowerPointを読み込んでいます... {Path.GetFileName(path)}";

                var outputDir = Path.Combine(
                    Path.GetTempPath(),
                    "insightmovie_cache",
                    "pptx_slides",
                    $"import_{Guid.NewGuid():N}");

                var importer = new Utils.PptxImporter(
                    (current, total, msg) =>
                    {
                        _logger.Log(msg);
                        if (total > 0)
                            ProgressText = $"PPTX: {msg}";
                    });

                var slides = await Task.Run(() => importer.ImportPptx(path, outputDir));

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
                    _project!.Scenes.Add(scene);
                }

                _logger.Log($"PPTX取込完了: {slides.Count} スライド");
            }
            catch (Exception ex)
            {
                _logger.LogError("PPTX取込エラー", ex);
            }
        }

        private void ImportTextFile(string path)
        {
            try
            {
                var text = File.ReadAllText(path);
                var paragraphs = text.Split(
                    new[] { "\r\n\r\n", "\n\n" },
                    StringSplitOptions.RemoveEmptyEntries);

                foreach (var para in paragraphs)
                {
                    var trimmed = para.Trim();
                    if (string.IsNullOrWhiteSpace(trimmed)) continue;

                    _project!.Scenes.Add(new Scene
                    {
                        NarrationText = trimmed
                    });
                }

                _logger.Log($"テキスト取込: {paragraphs.Length} 段落");
            }
            catch (Exception ex)
            {
                _logger.LogError("テキスト取込エラー", ex);
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
                OnPropertyChanged(nameof(HasIntro));
                OnPropertyChanged(nameof(IntroFileName));
                _logger.Log($"イントロ設定: {Path.GetFileName(path)}");
            }
        }

        private void ClearIntro()
        {
            IntroFilePath = string.Empty;
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
                OnPropertyChanged(nameof(HasOutro));
                OnPropertyChanged(nameof(OutroFileName));
                _logger.Log($"アウトロ設定: {Path.GetFileName(path)}");
            }
        }

        private void ClearOutro()
        {
            OutroFilePath = string.Empty;
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
                OnPropertyChanged(nameof(HasWatermark));
                OnPropertyChanged(nameof(WatermarkFileName));
                _logger.Log($"ロゴ設定: {Path.GetFileName(path)}");
            }
        }

        private void ClearWatermark()
        {
            WatermarkFilePath = string.Empty;
            OnPropertyChanged(nameof(HasWatermark));
            OnPropertyChanged(nameof(WatermarkFileName));
        }

        private void OpenOutputFolder()
        {
            if (string.IsNullOrEmpty(_outputPath)) return;
            var folder = Path.GetDirectoryName(_outputPath);
            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                OpenFileRequested?.Invoke(folder);
            }
        }

        private void SaveTemplate()
        {
            if (_project == null) return;

            var name = $"テンプレート_{DateTime.Now:yyyyMMdd_HHmmss}";
            // Apply current QuickMode settings to project before saving
            ApplySettingsToProject();

            var template = Services.TemplateService.CreateFromProject(_project, name,
                $"話者: {((_selectedSpeakerIndex >= 0 && _selectedSpeakerIndex < Speakers.Count) ? Speakers[_selectedSpeakerIndex].DisplayName : "デフォルト")}");
            Services.TemplateService.SaveTemplate(template);
            RefreshTemplateList();
            _logger.Log($"テンプレート保存: {name}");
        }

        private void LoadTemplate()
        {
            var templates = Services.TemplateService.LoadAllTemplates();
            if (templates.Count == 0)
            {
                _logger.Log("保存済みテンプレートがありません。");
                return;
            }

            if (_project == null)
                _project = new Project();

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

            Services.TemplateService.ApplyToProject(template, _project);

            // Sync template settings back to QuickMode UI
            if (!string.IsNullOrEmpty(_project.Bgm.FilePath))
            {
                BgmFilePath = _project.Bgm.FilePath;
                BgmVolume = _project.Bgm.Volume;
                OnPropertyChanged(nameof(HasBgm));
                OnPropertyChanged(nameof(BgmFileName));
            }

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

            if (_project.Watermark?.HasWatermark == true)
            {
                WatermarkFilePath = _project.Watermark.ImagePath!;
                OnPropertyChanged(nameof(HasWatermark));
                OnPropertyChanged(nameof(WatermarkFileName));
            }

            _logger.Log($"テンプレート適用: {template.Name}");
        }

        private void RefreshTemplateList()
        {
            TemplateNames.Clear();
            foreach (var t in Services.TemplateService.LoadAllTemplates())
                TemplateNames.Add(t.Name);
        }

        #endregion

        #region BGM & Settings

        private void SelectBgm()
        {
            if (_dialogService == null) return;
            var path = _dialogService.ShowOpenFileDialog(
                "BGMファイルを選択",
                "音声ファイル|*.mp3;*.wav;*.ogg;*.flac;*.aac;*.m4a|すべてのファイル|*.*");
            if (!string.IsNullOrEmpty(path))
            {
                BgmFilePath = path;
                OnPropertyChanged(nameof(HasBgm));
                OnPropertyChanged(nameof(BgmFileName));
                _logger.Log($"BGM選択: {Path.GetFileName(path)}");
            }
        }

        private void ClearBgm()
        {
            BgmFilePath = string.Empty;
            OnPropertyChanged(nameof(HasBgm));
            OnPropertyChanged(nameof(BgmFileName));
        }

        private async Task BatchImport()
        {
            if (_dialogService == null) return;
            var paths = _dialogService.ShowOpenFileDialogMultiple(
                "ファイルを選択（複数可）",
                "対応ファイル|*.pptx;*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.mp4;*.avi;*.mov;*.txt;*.md|すべてのファイル|*.*");
            if (paths != null && paths.Length > 0)
            {
                await HandleFileDropAsync(paths);
            }
        }

        private void ApplySettingsToProject()
        {
            if (_project == null) return;

            var transition = GetSelectedTransition();
            _project.DefaultTransition = transition;
            _project.DefaultTransitionDuration = 0.5;

            if (HasBgm)
            {
                _project.Bgm.FilePath = _bgmFilePath;
                _project.Bgm.Volume = _bgmVolume;
                _project.Bgm.DuckingEnabled = true;
            }

            _project.GenerateThumbnail = _generateThumbnail;
            _project.GenerateChapters = true;

            // Intro/Outro
            if (HasIntro)
                _project.IntroMediaPath = _introFilePath;
            if (HasOutro)
                _project.OutroMediaPath = _outroFilePath;

            // Watermark
            if (HasWatermark)
            {
                _project.Watermark.Enabled = true;
                _project.Watermark.ImagePath = _watermarkFilePath;
                _project.Watermark.Position = (_selectedWatermarkPosIndex >= 0 &&
                    _selectedWatermarkPosIndex < WatermarkPositionValues.Length)
                    ? WatermarkPositionValues[_selectedWatermarkPosIndex]
                    : "bottom-right";
            }

            // Apply speech speed and transition to all scenes
            foreach (var scene in _project.Scenes)
            {
                scene.SpeechSpeed = _speechSpeed;
                if (scene.TransitionType == TransitionType.None && transition != TransitionType.None)
                {
                    scene.TransitionType = transition;
                    scene.TransitionDuration = 0.5;
                }
            }
        }

        private TransitionType GetSelectedTransition()
        {
            return _selectedTransitionIndex switch
            {
                1 => TransitionType.Fade,
                2 => TransitionType.Dissolve,
                3 => TransitionType.WipeLeft,
                4 => TransitionType.WipeRight,
                5 => TransitionType.SlideLeft,
                6 => TransitionType.SlideRight,
                7 => TransitionType.ZoomIn,
                _ => TransitionType.None
            };
        }

        #endregion

        #region Video Generation

        private async Task GenerateVideo()
        {
            if (_project == null || _isGenerating) return;

            if (_ffmpegWrapper == null || !_ffmpegWrapper.CheckAvailable())
            {
                StatusText = "ffmpegが見つかりません。動画生成にはffmpegが必要です。";
                return;
            }

            // Fix 3: Use DialogService for SaveFileDialog
            var defaultName = $"InsightCast_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
            string? savePath;
            if (_dialogService != null)
            {
                savePath = _dialogService.ShowSaveFileDialog(
                    "動画の保存先を選択",
                    "MP4ファイル|*.mp4|すべてのファイル|*.*",
                    ".mp4",
                    defaultName);
            }
            else
            {
                var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                savePath = Path.Combine(desktopPath, defaultName);
            }

            if (string.IsNullOrEmpty(savePath))
                return;

            int speakerId = _defaultSpeakerId;
            if (_selectedSpeakerIndex >= 0 && _selectedSpeakerIndex < Speakers.Count)
                speakerId = Speakers[_selectedSpeakerIndex].StyleId;

            string resolution = _selectedResolutionIndex == 1 ? "1920x1080" : "1080x1920";

            // Apply all QuickMode settings to project
            ApplySettingsToProject();

            IsGenerating = true;
            ProgressVisible = true;
            ProgressValue = 0;
            _exportCts = new CancellationTokenSource();

            var totalScenes = _project.Scenes.Count;
            var progress = new Progress<string>(msg =>
            {
                ProgressText = msg;
                _logger.Log(msg);

                // Parse structured progress: [n/total] description
                if (msg.StartsWith("[") && msg.Contains(']'))
                {
                    var bracket = msg.Substring(1, msg.IndexOf(']') - 1);
                    var parts = bracket.Split('/');
                    if (parts.Length == 2 &&
                        int.TryParse(parts[0], out var current) &&
                        int.TryParse(parts[1], out var total))
                    {
                        ProgressValue = (double)current / total * 100;
                    }
                }
                else if (msg.Contains("結合中"))
                {
                    ProgressValue = 85;
                }
                else if (msg.Contains("サムネイル"))
                {
                    ProgressValue = 92;
                }
                else if (msg.Contains("メタデータ"))
                {
                    ProgressValue = 96;
                }
                else if (msg.Contains("完了"))
                {
                    ProgressValue = 100;
                }
            });

            var ct = _exportCts.Token;

            try
            {
                StatusText = "動画を生成しています...";

                var projectSnapshot = _project.Clone();
                var defaultStyle = TextStyle.PRESET_STYLES[0];

                TextStyle GetStyle(Scene scene)
                {
                    if (scene.SubtitleStyleId != null)
                    {
                        var preset = TextStyle.PRESET_STYLES.FirstOrDefault(s => s.Id == scene.SubtitleStyleId);
                        if (preset != null) return preset;
                    }
                    return defaultStyle;
                }

                var ffmpeg = _ffmpegWrapper!;
                var exportService = new ExportService(ffmpeg, _voiceVoxClient, _audioCache);
                var exportResult = await Task.Run(() =>
                    exportService.ExportFull(projectSnapshot, savePath, resolution, 30,
                        speakerId, GetStyle, progress, ct), ct);

                if (exportResult.Success)
                {
                    IsComplete = true;
                    OutputPath = savePath;
                    ThumbnailPath = exportResult.ThumbnailPath ?? string.Empty;
                    MetadataPath = exportResult.MetadataFilePath ?? string.Empty;
                    OnPropertyChanged(nameof(HasExtraOutputs));

                    var extras = new List<string>();
                    if (!string.IsNullOrEmpty(exportResult.ThumbnailPath))
                        extras.Add("サムネイル");
                    if (!string.IsNullOrEmpty(exportResult.ChapterFilePath))
                        extras.Add("チャプター");
                    if (!string.IsNullOrEmpty(exportResult.MetadataFilePath))
                        extras.Add("メタデータ");

                    var extrasText = extras.Count > 0 ? $" + {string.Join("・", extras)}" : "";
                    StatusText = $"動画が完成しました！{extrasText}";
                    ProgressText = "完了";
                    ProgressValue = 100;
                    _logger.Log($"書き出し成功: {savePath}");
                    if (!string.IsNullOrEmpty(exportResult.ThumbnailPath))
                        _logger.Log($"サムネイル: {exportResult.ThumbnailPath}");
                    if (!string.IsNullOrEmpty(exportResult.MetadataFilePath))
                        _logger.Log($"メタデータ: {exportResult.MetadataFilePath}");
                }
                else
                {
                    ProgressVisible = false;
                    StatusText = "動画の生成に失敗しました。ログを確認してください。";
                }
            }
            catch (OperationCanceledException)
            {
                ProgressVisible = false;
                StatusText = "生成をキャンセルしました";
                _logger.Log("生成キャンセル");
            }
            catch (Exception ex)
            {
                ProgressVisible = false;
                StatusText = $"エラー: {ex.Message}";
                _logger.LogError("動画生成エラー", ex);
            }
            finally
            {
                IsGenerating = false;
                _exportCts?.Dispose();
                _exportCts = null;
            }
        }

        #endregion

        #region Actions

        private void OpenOutput()
        {
            if (!string.IsNullOrEmpty(_outputPath) && File.Exists(_outputPath))
            {
                OpenFileRequested?.Invoke(_outputPath);
            }
        }

        /// <summary>
        /// Fix 5: Keep project and settings, only reset generation state.
        /// User can change speaker/resolution and regenerate.
        /// </summary>
        private void ResetForRegenerate()
        {
            IsComplete = false;
            ProgressVisible = false;
            ProgressValue = 0;
            ProgressText = string.Empty;
            OutputPath = string.Empty;
            StatusText = "設定を変更して「動画を生成」をクリック";
            // HasProject stays true, _project stays, Thumbnails stay, settings stay
        }

        /// <summary>Full reset back to the drop zone.</summary>
        private void ResetAll()
        {
            _project = null;
            Thumbnails.Clear();
            HasProject = false;
            IsComplete = false;
            ProgressVisible = false;
            ProgressValue = 0;
            ProgressText = string.Empty;
            ImportSummary = string.Empty;
            OutputPath = string.Empty;
            StatusText = string.Empty;

            // Clear BGM, intro/outro, and watermark
            BgmFilePath = string.Empty;
            OnPropertyChanged(nameof(HasBgm));
            OnPropertyChanged(nameof(BgmFileName));

            IntroFilePath = string.Empty;
            OutroFilePath = string.Empty;
            OnPropertyChanged(nameof(HasIntro));
            OnPropertyChanged(nameof(HasOutro));
            OnPropertyChanged(nameof(IntroFileName));
            OnPropertyChanged(nameof(OutroFileName));

            WatermarkFilePath = string.Empty;
            OnPropertyChanged(nameof(HasWatermark));
            OnPropertyChanged(nameof(WatermarkFileName));
        }

        private void Cancel()
        {
            _exportCts?.Cancel();
        }

        public bool CanClose()
        {
            if (!_isGenerating) return true;

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

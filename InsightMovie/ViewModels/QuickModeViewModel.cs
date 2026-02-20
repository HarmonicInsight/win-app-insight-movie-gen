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
    public class QuickModeViewModel : ViewModelBase
    {
        private readonly VoiceVoxClient _voiceVoxClient;
        private readonly int _defaultSpeakerId;
        private readonly FFmpegWrapper? _ffmpegWrapper;
        private readonly Config _config;
        private readonly AudioCache _audioCache;
        private readonly IAppLogger _logger;

        private Project? _project;
        private bool _isGenerating;
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
        private Dictionary<int, string> _speakerStyles = new();

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
            OpenEditorCommand = new RelayCommand(() => OpenEditorRequested?.Invoke(_project!),
                () => _hasProject);
            OpenOutputCommand = new RelayCommand(OpenOutput, () => _isComplete);
            GenerateAnotherCommand = new RelayCommand(Reset);
            CancelCommand = new RelayCommand(Cancel, () => _isGenerating);
        }

        #region Properties

        public ObservableCollection<SpeakerItem> Speakers { get; } = new();

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

        /// <summary>True when settings + generate button should be visible (has project, not generating, not complete).</summary>
        public bool ShowSettings => _hasProject && !_isGenerating && !_isComplete;

        public string OutputPath
        {
            get => _outputPath;
            private set => SetProperty(ref _outputPath, value);
        }

        public IAppLogger Logger => _logger;

        #endregion

        #region Commands

        public ICommand GenerateCommand { get; }
        public ICommand OpenEditorCommand { get; }
        public ICommand OpenOutputCommand { get; }
        public ICommand GenerateAnotherCommand { get; }
        public ICommand CancelCommand { get; }

        #endregion

        #region Events

        /// <summary>Raised when user clicks "Open in editor". Passes the current project.</summary>
        public event Action<Project>? OpenEditorRequested;

        /// <summary>Raised when generated file should be opened/shown.</summary>
        public event Action<string>? OpenFileRequested;

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
                _speakerStyles.Clear();
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
                        _speakerStyles[styleId] = displayName;
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

        #region File Drop Handling

        public async Task HandleFileDropAsync(string[] files)
        {
            if (_isGenerating) return;

            _project = new Project();
            _project.Scenes.Clear();

            int pptxCount = 0;
            int imageCount = 0;
            int videoCount = 0;

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();

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
                        break;
                }
            }

            if (_project.Scenes.Count == 0)
            {
                StatusText = "対応するファイルが見つかりませんでした";
                return;
            }

            HasProject = true;
            IsComplete = false;

            var parts = new List<string>();
            if (pptxCount > 0) parts.Add($"PPTX {pptxCount}件");
            if (imageCount > 0) parts.Add($"画像 {imageCount}件");
            if (videoCount > 0) parts.Add($"動画 {videoCount}件");

            ImportSummary = $"{_project.Scenes.Count} シーン（{string.Join("、", parts)}）";
            StatusText = "設定を確認して「動画を生成」をクリック";
            _logger.Log($"取込完了: {ImportSummary}");
        }

        private async Task ImportPptxAsync(string path)
        {
            try
            {
                _logger.Log($"PPTX読込中: {Path.GetFileName(path)}");
                StatusText = "PowerPointを読み込んでいます...";

                var outputDir = Path.Combine(
                    Path.GetTempPath(),
                    "insightmovie_cache",
                    "pptx_slides",
                    $"import_{Guid.NewGuid():N}");

                var importer = new Utils.PptxImporter(
                    (current, total, msg) => _logger.Log(msg));

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

        #region Video Generation

        private async Task GenerateVideo()
        {
            if (_project == null || _isGenerating) return;

            if (_ffmpegWrapper == null || !_ffmpegWrapper.CheckAvailable())
            {
                StatusText = "ffmpegが見つかりません。動画生成にはffmpegが必要です。";
                return;
            }

            // Ask for output path
            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            var defaultName = $"InsightCast_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
            var savePath = Path.Combine(desktopPath, defaultName);

            // Use a simple save dialog via event - for now use desktop as default
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "動画の保存先を選択",
                Filter = "MP4ファイル|*.mp4|すべてのファイル|*.*",
                DefaultExt = ".mp4",
                FileName = defaultName,
                InitialDirectory = desktopPath
            };

            if (dialog.ShowDialog() != true)
                return;

            savePath = dialog.FileName;

            int speakerId = _defaultSpeakerId;
            if (_selectedSpeakerIndex >= 0 && _selectedSpeakerIndex < Speakers.Count)
                speakerId = Speakers[_selectedSpeakerIndex].StyleId;

            string resolution = _selectedResolutionIndex == 1 ? "1920x1080" : "1080x1920";

            IsGenerating = true;
            ProgressVisible = true;
            ProgressValue = 0;
            _exportCts = new CancellationTokenSource();

            var totalScenes = _project.Scenes.Count;
            var progress = new Progress<string>(msg =>
            {
                ProgressText = msg;
                _logger.Log(msg);

                // Parse progress from message pattern: "シーン X/Y: ..."
                if (msg.StartsWith("シーン ") && msg.Contains('/'))
                {
                    var parts = msg.Split('/');
                    if (parts.Length >= 1)
                    {
                        var numStr = parts[0].Replace("シーン ", "").Trim();
                        if (int.TryParse(numStr, out var current))
                        {
                            ProgressValue = (double)current / totalScenes * 100;
                        }
                    }
                }
                else if (msg.Contains("結合中"))
                {
                    ProgressValue = 90;
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
                var success = await Task.Run(() =>
                    exportService.Export(projectSnapshot, savePath, resolution, 30,
                        speakerId, GetStyle, progress, ct), ct);

                if (success)
                {
                    IsComplete = true;
                    OutputPath = savePath;
                    StatusText = "動画が完成しました！";
                    ProgressText = "完了";
                    ProgressValue = 100;
                    _logger.Log($"書き出し成功: {savePath}");
                }
                else
                {
                    StatusText = "動画の生成に失敗しました。ログを確認してください。";
                }
            }
            catch (OperationCanceledException)
            {
                StatusText = "生成をキャンセルしました";
                _logger.Log("生成キャンセル");
            }
            catch (Exception ex)
            {
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

        private void Reset()
        {
            _project = null;
            HasProject = false;
            IsComplete = false;
            ProgressVisible = false;
            ProgressValue = 0;
            ProgressText = string.Empty;
            ImportSummary = string.Empty;
            OutputPath = string.Empty;
            StatusText = "資料をドロップしてください";
        }

        private void Cancel()
        {
            _exportCts?.Cancel();
        }

        public bool CanClose()
        {
            if (!_isGenerating) return true;
            _exportCts?.Cancel();
            return true;
        }

        #endregion
    }
}

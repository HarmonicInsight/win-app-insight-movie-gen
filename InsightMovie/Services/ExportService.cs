using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using InsightMovie.Models;
using InsightMovie.Video;
using InsightMovie.VoiceVox;

namespace InsightMovie.Services
{
    public class ExportResult
    {
        public bool Success { get; set; }
        public string? VideoPath { get; set; }
        public string? ThumbnailPath { get; set; }
        public string? ChapterFilePath { get; set; }
        public string? MetadataFilePath { get; set; }
    }

    public class ExportService
    {
        private readonly FFmpegWrapper _ffmpeg;
        private readonly VoiceVoxClient _voiceVoxClient;
        private readonly AudioCache _audioCache;

        public ExportService(FFmpegWrapper ffmpeg, VoiceVoxClient voiceVoxClient, AudioCache audioCache)
        {
            _ffmpeg = ffmpeg;
            _voiceVoxClient = voiceVoxClient;
            _audioCache = audioCache;
        }

        public bool Export(
            Project project,
            string outputPath,
            string resolution,
            int fps,
            int defaultSpeakerId,
            Func<Scene, TextStyle> getStyleForScene,
            IProgress<string> progress,
            CancellationToken ct)
        {
            var result = ExportFull(project, outputPath, resolution, fps,
                defaultSpeakerId, getStyleForScene, progress, ct);
            return result.Success;
        }

        public ExportResult ExportFull(
            Project project,
            string outputPath,
            string resolution,
            int fps,
            int defaultSpeakerId,
            Func<Scene, TextStyle> getStyleForScene,
            IProgress<string> progress,
            CancellationToken ct)
        {
            var result = new ExportResult();
            progress.Report("動画生成を準備中...");

            if (!_ffmpeg.CheckAvailable())
            {
                progress.Report("エラー: ffmpegが検出されていません。");
                return result;
            }

            var sceneGen = new SceneGenerator(_ffmpeg);
            var composer = new VideoComposer(_ffmpeg);
            var tempDir = Path.Combine(Path.GetTempPath(), "insightmovie_build");
            Directory.CreateDirectory(tempDir);

            var scenePaths = new List<string>();
            var transitions = new List<(TransitionType, double)>();
            var chapterTimes = new List<(double StartTime, string Title)>();
            double cumulativeDuration = 0;

            // Total steps: scenes + intro? + outro? + concat + bgm? + thumbnail? + metadata?
            int totalSteps = project.Scenes.Count + 2; // concat + finalize
            if (project.HasIntro) totalSteps++;
            if (project.HasOutro) totalSteps++;
            int currentStep = 0;

            // Step: Generate intro scene if configured
            if (project.HasIntro && File.Exists(project.IntroMediaPath))
            {
                currentStep++;
                progress.Report($"[{currentStep}/{totalSteps}] イントロを生成中...");
                ct.ThrowIfCancellationRequested();

                var introScene = new Scene
                {
                    MediaPath = project.IntroMediaPath,
                    MediaType = IsVideoFile(project.IntroMediaPath) ? MediaType.Video : MediaType.Image,
                    DurationMode = DurationMode.Fixed,
                    FixedSeconds = project.IntroDuration
                };

                var introPath = Path.Combine(tempDir, "scene_intro.mp4");
                var introSuccess = sceneGen.GenerateScene(introScene, introPath,
                    project.IntroDuration, resolution, fps, watermark: project.Watermark);

                if (introSuccess)
                {
                    scenePaths.Add(introPath);
                    chapterTimes.Add((0, "イントロ"));
                    cumulativeDuration += project.IntroDuration;
                    transitions.Add((project.DefaultTransition, project.DefaultTransitionDuration));
                }
            }

            // Step: Generate each content scene
            for (int i = 0; i < project.Scenes.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                currentStep++;
                var scene = project.Scenes[i];
                progress.Report($"[{currentStep}/{totalSteps}] シーン {i + 1}/{project.Scenes.Count}: 音声を生成中...");

                string? audioPath = null;
                if (scene.HasNarration && !scene.KeepOriginalAudio)
                {
                    var sid = scene.SpeakerId ?? defaultSpeakerId;
                    double speed = scene.SpeechSpeed;

                    // Use speed-aware cache key
                    string cacheKey = Math.Abs(speed - 1.0) > 0.01
                        ? $"{scene.NarrationText!}__spd{speed:F2}"
                        : scene.NarrationText!;

                    if (!_audioCache.Exists(cacheKey, sid))
                    {
                        var audioData = _voiceVoxClient
                            .GenerateAudioAsync(scene.NarrationText!, sid, speed)
                            .GetAwaiter().GetResult();
                        audioPath = _audioCache.Save(cacheKey, sid, audioData);
                    }
                    else
                    {
                        audioPath = _audioCache.GetCachePath(cacheKey, sid);
                    }
                    scene.AudioCachePath = audioPath;
                }

                progress.Report($"[{currentStep}/{totalSteps}] シーン {i + 1}/{project.Scenes.Count}: 動画を生成中...");

                double duration = scene.DurationMode == DurationMode.Fixed
                    ? scene.FixedSeconds
                    : (audioPath != null
                        ? (_audioCache.GetDuration(
                               Math.Abs(scene.SpeechSpeed - 1.0) > 0.01
                                   ? $"{scene.NarrationText!}__spd{scene.SpeechSpeed:F2}"
                                   : scene.NarrationText!,
                               scene.SpeakerId ?? defaultSpeakerId) ?? 1.0) + 2.0
                        : 3.0);

                // Chapter marker
                string chapterTitle = scene.HasNarration
                    ? (scene.NarrationText!.Length > 30
                        ? scene.NarrationText![..30] + "..."
                        : scene.NarrationText!)
                    : $"シーン {i + 1}";
                chapterTimes.Add((cumulativeDuration, chapterTitle));

                var scenePath = Path.Combine(tempDir, $"scene_{i:D4}.mp4");
                var style = getStyleForScene(scene);

                var success = sceneGen.GenerateScene(scene, scenePath, duration,
                    resolution, fps, audioPath, style, project.Watermark);

                if (!success)
                {
                    progress.Report($"シーン {i + 1} の生成に失敗しました。");
                    return result;
                }

                scenePaths.Add(scenePath);
                cumulativeDuration += duration;

                // Add transition (use scene-level or project default)
                if (scenePaths.Count > 1)
                {
                    var transType = scene.TransitionType != TransitionType.None
                        ? scene.TransitionType
                        : project.DefaultTransition;
                    var transDur = scene.TransitionType != TransitionType.None
                        ? scene.TransitionDuration
                        : project.DefaultTransitionDuration;
                    transitions.Add((transType, transDur));
                }
            }

            // Step: Generate outro scene if configured
            if (project.HasOutro && File.Exists(project.OutroMediaPath))
            {
                currentStep++;
                progress.Report($"[{currentStep}/{totalSteps}] アウトロを生成中...");
                ct.ThrowIfCancellationRequested();

                var outroScene = new Scene
                {
                    MediaPath = project.OutroMediaPath,
                    MediaType = IsVideoFile(project.OutroMediaPath) ? MediaType.Video : MediaType.Image,
                    DurationMode = DurationMode.Fixed,
                    FixedSeconds = project.OutroDuration
                };

                var outroPath = Path.Combine(tempDir, "scene_outro.mp4");
                var outroSuccess = sceneGen.GenerateScene(outroScene, outroPath,
                    project.OutroDuration, resolution, fps, watermark: project.Watermark);

                if (outroSuccess)
                {
                    chapterTimes.Add((cumulativeDuration, "エンディング"));
                    transitions.Add((project.DefaultTransition, project.DefaultTransitionDuration));
                    scenePaths.Add(outroPath);
                    cumulativeDuration += project.OutroDuration;
                }
            }

            // Step: Concatenate all scenes
            currentStep++;
            progress.Report($"[{currentStep}/{totalSteps}] 動画を結合中...");
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
                return result;
            }

            // Step: Add BGM
            if (project.Bgm.HasBgm)
            {
                progress.Report("BGMを追加中...");
                var withBgm = outputPath + ".bgm.mp4";
                var bgmOk = composer.AddBgm(outputPath, withBgm, project.Bgm);
                if (bgmOk)
                {
                    File.Delete(outputPath);
                    File.Move(withBgm, outputPath);
                }
            }

            result.Success = true;
            result.VideoPath = outputPath;

            // Step: Generate thumbnail
            if (project.GenerateThumbnail)
            {
                progress.Report("サムネイルを生成中...");
                var thumbPath = Path.ChangeExtension(outputPath, ".jpg");
                if (sceneGen.ExtractThumbnail(outputPath, thumbPath, 1.0))
                {
                    result.ThumbnailPath = thumbPath;
                }
            }

            // Step: Generate chapter file
            if (project.GenerateChapters && chapterTimes.Count > 1)
            {
                progress.Report("チャプターファイルを生成中...");
                var chapterPath = Path.ChangeExtension(outputPath, ".chapters.txt");
                WriteChapterFile(chapterPath, chapterTimes);
                result.ChapterFilePath = chapterPath;
            }

            // Step: Generate YouTube metadata
            progress.Report("メタデータを生成中...");
            var metadataPath = Path.ChangeExtension(outputPath, ".metadata.txt");
            WriteYouTubeMetadata(metadataPath, project, chapterTimes);
            result.MetadataFilePath = metadataPath;

            progress.Report("書き出し完了");
            return result;
        }

        /// <summary>
        /// Generates a preview for a single scene (no concat, no BGM).
        /// </summary>
        public bool GeneratePreview(
            Scene scene,
            string outputPath,
            string resolution,
            int fps,
            int defaultSpeakerId,
            TextStyle textStyle,
            IProgress<string> progress,
            CancellationToken ct)
        {
            progress.Report("プレビューを生成中...");

            if (!_ffmpeg.CheckAvailable())
            {
                progress.Report("エラー: ffmpegが検出されていません。");
                return false;
            }

            var sceneGen = new SceneGenerator(_ffmpeg);
            string? audioPath = null;

            if (scene.HasNarration && !scene.KeepOriginalAudio)
            {
                var sid = scene.SpeakerId ?? defaultSpeakerId;
                double speed = scene.SpeechSpeed;
                string cacheKey = Math.Abs(speed - 1.0) > 0.01
                    ? $"{scene.NarrationText!}__spd{speed:F2}"
                    : scene.NarrationText!;

                if (!_audioCache.Exists(cacheKey, sid))
                {
                    progress.Report("音声を生成中...");
                    var audioData = _voiceVoxClient
                        .GenerateAudioAsync(scene.NarrationText!, sid, speed)
                        .GetAwaiter().GetResult();
                    audioPath = _audioCache.Save(cacheKey, sid, audioData);
                }
                else
                {
                    audioPath = _audioCache.GetCachePath(cacheKey, sid);
                }
            }

            double duration = scene.DurationMode == DurationMode.Fixed
                ? scene.FixedSeconds
                : (audioPath != null
                    ? (_audioCache.GetDuration(
                           Math.Abs(scene.SpeechSpeed - 1.0) > 0.01
                               ? $"{scene.NarrationText!}__spd{scene.SpeechSpeed:F2}"
                               : scene.NarrationText!,
                           scene.SpeakerId ?? defaultSpeakerId) ?? 1.0) + 2.0
                    : 3.0);

            progress.Report("シーンを生成中...");
            var success = sceneGen.GenerateScene(scene, outputPath, duration,
                resolution, fps, audioPath, textStyle);

            if (success)
                progress.Report("プレビュー完了");
            else
                progress.Report("プレビュー生成に失敗しました。");

            return success;
        }

        private static void WriteChapterFile(string path, List<(double StartTime, string Title)> chapters)
        {
            var sb = new StringBuilder();
            foreach (var (startTime, title) in chapters)
            {
                var ts = TimeSpan.FromSeconds(startTime);
                sb.AppendLine($"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2} {title}");
            }
            File.WriteAllText(path, sb.ToString());
        }

        private static void WriteYouTubeMetadata(
            string path, Project project, List<(double StartTime, string Title)> chapters)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== YouTube メタデータ ===");
            sb.AppendLine();

            // Title suggestion
            var firstNarration = project.Scenes
                .Where(s => s.HasNarration)
                .Select(s => s.NarrationText!)
                .FirstOrDefault() ?? "動画タイトル";
            if (firstNarration.Length > 60)
                firstNarration = firstNarration[..60];
            sb.AppendLine($"【タイトル案】");
            sb.AppendLine(firstNarration);
            sb.AppendLine();

            // Description
            sb.AppendLine("【説明文】");
            sb.AppendLine("この動画はInsightCastで自動生成されました。");
            sb.AppendLine();

            // Chapter markers for YouTube
            if (chapters.Count > 1)
            {
                sb.AppendLine("【チャプター（説明欄に貼り付け）】");
                foreach (var (startTime, title) in chapters)
                {
                    var ts = TimeSpan.FromSeconds(startTime);
                    sb.AppendLine($"{ts.Hours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2} {title}");
                }
                sb.AppendLine();
            }

            // Tags
            sb.AppendLine("【タグ候補】");
            var tags = new List<string> { "教育", "研修", "解説" };
            var narrations = project.Scenes.Where(s => s.HasNarration)
                .SelectMany(s => s.NarrationText!.Split(new[] { '、', '。', '！', '？', ' ' },
                    StringSplitOptions.RemoveEmptyEntries))
                .Where(w => w.Length >= 2 && w.Length <= 10)
                .Distinct()
                .Take(7);
            tags.AddRange(narrations);
            sb.AppendLine(string.Join(", ", tags));
            sb.AppendLine();

            sb.AppendLine($"【動画情報】");
            sb.AppendLine($"シーン数: {project.Scenes.Count}");
            sb.AppendLine($"解像度: {project.Output.Resolution}");
            sb.AppendLine($"生成日時: {DateTime.Now:yyyy-MM-dd HH:mm}");

            File.WriteAllText(path, sb.ToString(), Encoding.UTF8);
        }

        private static bool IsVideoFile(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ext is ".mp4" or ".avi" or ".mov" or ".wmv" or ".mkv";
        }
    }
}

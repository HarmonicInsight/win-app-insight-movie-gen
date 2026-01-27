using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using InsightMovie.Models;
using InsightMovie.Video;
using InsightMovie.VoiceVox;

namespace InsightMovie.Services
{
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
            progress.Report("動画生成を準備中...");

            if (!_ffmpeg.CheckAvailable())
            {
                progress.Report("エラー: ffmpegが検出されていません。");
                return false;
            }

            var sceneGen = new SceneGenerator(_ffmpeg);
            var composer = new VideoComposer(_ffmpeg);
            var tempDir = Path.Combine(Path.GetTempPath(), "insightmovie_build");
            Directory.CreateDirectory(tempDir);

            var scenePaths = new List<string>();
            var transitions = new List<(TransitionType, double)>();

            for (int i = 0; i < project.Scenes.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var scene = project.Scenes[i];
                progress.Report($"シーン {i + 1}/{project.Scenes.Count}: 音声を生成中...");

                string? audioPath = null;
                if (scene.HasNarration && !scene.KeepOriginalAudio)
                {
                    var sid = scene.SpeakerId ?? defaultSpeakerId;
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

                progress.Report($"シーン {i + 1}/{project.Scenes.Count}: 動画を生成中...");

                double duration = scene.DurationMode == DurationMode.Fixed
                    ? scene.FixedSeconds
                    : (audioPath != null
                        ? (_audioCache.GetDuration(scene.NarrationText!, scene.SpeakerId ?? defaultSpeakerId) ?? 1.0) + 2.0
                        : 3.0);

                var scenePath = Path.Combine(tempDir, $"scene_{i:D4}.mp4");
                var style = getStyleForScene(scene);

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

            progress.Report("書き出し完了");
            return true;
        }
    }
}

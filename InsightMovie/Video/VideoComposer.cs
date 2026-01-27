namespace InsightMovie.Video;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using InsightMovie.Models;

/// <summary>
/// Composes multiple video segments into a final output, with support for
/// concatenation, transitions, and background music.
/// </summary>
public class VideoComposer
{
    private readonly FFmpegWrapper _ffmpeg;

    /// <summary>
    /// Creates a new VideoComposer.
    /// </summary>
    /// <param name="ffmpeg">FFmpeg wrapper instance for executing commands.</param>
    public VideoComposer(FFmpegWrapper ffmpeg)
    {
        _ffmpeg = ffmpeg;
    }

    // -----------------------------------------------------------------------
    // Concatenation
    // -----------------------------------------------------------------------

    /// <summary>
    /// Concatenates video files using the ffmpeg concat demuxer (no re-encoding).
    /// All videos must have the same codec, resolution, and frame rate.
    /// </summary>
    /// <param name="videoPaths">List of video file paths to concatenate.</param>
    /// <param name="outputPath">Output file path.</param>
    /// <returns>True if concatenation succeeded.</returns>
    public bool ConcatVideos(List<string> videoPaths, string outputPath)
    {
        if (videoPaths == null || videoPaths.Count == 0)
        {
            return false;
        }

        if (videoPaths.Count == 1)
        {
            File.Copy(videoPaths[0], outputPath, overwrite: true);
            return true;
        }

        // Create a concat list file
        string listFile = Path.Combine(
            Path.GetTempPath(),
            $"concat_list_{Guid.NewGuid():N}.txt");

        try
        {
            // Write file list in concat demuxer format
            var lines = videoPaths.Select(
                p => $"file '{p.Replace("\\", "/").Replace("'", "'\\''")}'");
            File.WriteAllLines(listFile, lines);

            var args = new List<string>
            {
                "-y",
                "-f", "concat",
                "-safe", "0",
                "-i", $"\"{listFile}\"",
                "-c", "copy",
                $"\"{outputPath}\""
            };

            return _ffmpeg.RunCommand(args);
        }
        finally
        {
            CleanupTempFile(listFile);
        }
    }

    /// <summary>
    /// Concatenates videos with xfade transitions between each pair.
    /// </summary>
    /// <param name="videoPaths">List of video file paths.</param>
    /// <param name="transitions">
    /// List of (TransitionType, duration) tuples, one per transition between consecutive videos.
    /// If fewer transitions than needed, the last one is repeated.
    /// </param>
    /// <param name="outputPath">Output file path.</param>
    /// <returns>True if concatenation with transitions succeeded.</returns>
    public bool ConcatWithTransitions(
        List<string> videoPaths,
        List<(TransitionType Type, double Duration)> transitions,
        string outputPath)
    {
        if (videoPaths == null || videoPaths.Count == 0)
        {
            return false;
        }

        if (videoPaths.Count == 1)
        {
            File.Copy(videoPaths[0], outputPath, overwrite: true);
            return true;
        }

        // Filter out None transitions, defaulting to Fade
        var effectiveTransitions = transitions
            .Select(t => t.Type == TransitionType.None
                ? (Type: TransitionType.Fade, Duration: t.Duration)
                : t)
            .ToList();

        if (effectiveTransitions.Count == 0)
        {
            effectiveTransitions.Add((TransitionType.Fade, TransitionSettings.DEFAULT_TRANSITION_DURATION));
        }

        if (videoPaths.Count == 2)
        {
            var t = effectiveTransitions[0];
            return XfadeTwoVideos(videoPaths[0], videoPaths[1], outputPath, t.Type, t.Duration);
        }

        // For more than 2 videos, chain xfade filters sequentially
        return ChainXfadeTransitions(videoPaths, effectiveTransitions, outputPath);
    }

    /// <summary>
    /// Re-encodes each video to a common format and then concatenates them.
    /// Useful when source videos have different codecs, resolutions, or frame rates.
    /// </summary>
    /// <param name="videoPaths">List of video file paths.</param>
    /// <param name="outputPath">Output file path.</param>
    /// <param name="resolution">Target resolution (e.g., "1080x1920").</param>
    /// <param name="fps">Target frame rate.</param>
    /// <returns>True if re-encode and concatenation succeeded.</returns>
    public bool ConcatVideosWithReEncode(
        List<string> videoPaths,
        string outputPath,
        string resolution = "1080x1920",
        int fps = 30)
    {
        if (videoPaths == null || videoPaths.Count == 0)
        {
            return false;
        }

        string[] resParts = resolution.Split('x');
        int width = int.Parse(resParts[0]);
        int height = int.Parse(resParts[1]);

        var reEncodedPaths = new List<string>();

        try
        {
            // Re-encode each video to a common format
            for (int i = 0; i < videoPaths.Count; i++)
            {
                string tempPath = Path.Combine(
                    Path.GetTempPath(),
                    $"reencode_{i}_{Guid.NewGuid():N}.mp4");

                var args = new List<string>
                {
                    "-y",
                    "-i", $"\"{videoPaths[i]}\"",
                    "-vf", $"\"scale={width}:{height}:force_original_aspect_ratio=decrease," +
                           $"pad={width}:{height}:(ow-iw)/2:(oh-ih)/2:black\"",
                    "-c:v", "libx264",
                    "-pix_fmt", "yuv420p",
                    "-r", fps.ToString(),
                    "-c:a", "aac",
                    "-b:a", "192k",
                    "-ar", "44100",
                    "-ac", "2",
                    $"\"{tempPath}\""
                };

                if (!_ffmpeg.RunCommand(args))
                {
                    Console.Error.WriteLine(
                        $"Failed to re-encode video: {videoPaths[i]}");
                    return false;
                }

                reEncodedPaths.Add(tempPath);
            }

            // Concatenate re-encoded videos
            return ConcatVideos(reEncodedPaths, outputPath);
        }
        finally
        {
            foreach (string path in reEncodedPaths)
            {
                CleanupTempFile(path);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Background Music
    // -----------------------------------------------------------------------

    /// <summary>
    /// Adds background music to a video with volume control, fade in/out, optional looping,
    /// and ducking (sidechain compression) to lower BGM when narration is present.
    /// </summary>
    /// <param name="videoPath">Input video file path (may already have audio).</param>
    /// <param name="outputPath">Output file path.</param>
    /// <param name="bgmPath">Background music audio file path.</param>
    /// <param name="volume">BGM volume level (0.0 to 1.0).</param>
    /// <param name="fadeInDuration">BGM fade-in duration in seconds.</param>
    /// <param name="fadeOutDuration">BGM fade-out duration in seconds.</param>
    /// <param name="loop">Whether to loop the BGM to match video length.</param>
    /// <param name="enableDucking">Whether to apply sidechain compression (ducking).</param>
    /// <param name="duckingThreshold">Threshold for sidechain compressor (e.g., 0.03).</param>
    /// <param name="duckingRatio">Compression ratio for ducking (e.g., 8).</param>
    /// <returns>True if BGM was added successfully.</returns>
    public bool AddBgm(
        string videoPath,
        string outputPath,
        BGMSettings bgm)
    {
        if (!bgm.HasBgm) return false;
        return AddBgm(videoPath, outputPath, bgm.FilePath!,
            bgm.Volume,
            bgm.FadeInEnabled ? bgm.FadeInDuration : 0,
            bgm.FadeOutEnabled ? bgm.FadeOutDuration : 0,
            bgm.LoopEnabled,
            bgm.DuckingEnabled,
            0.03, 8.0);
    }

    /// <returns>True if BGM was added successfully.</returns>
    public bool AddBgm(
        string videoPath,
        string outputPath,
        string bgmPath,
        double volume = 0.15,
        double fadeInDuration = 2.0,
        double fadeOutDuration = 3.0,
        bool loop = true,
        bool enableDucking = false,
        double duckingThreshold = 0.03,
        double duckingRatio = 8.0)
    {
        // Get video duration for fade-out timing
        double videoDuration = GetVideoDuration(videoPath);
        if (videoDuration <= 0)
        {
            Console.Error.WriteLine("Failed to determine video duration.");
            return false;
        }

        var args = new List<string>();
        args.Add("-y");

        // Input: video
        args.AddRange(new[] { "-i", $"\"{videoPath}\"" });

        // Input: BGM (with optional loop)
        if (loop)
        {
            args.AddRange(new[] { "-stream_loop", "-1" });
        }
        args.AddRange(new[] { "-i", $"\"{bgmPath}\"" });

        // Build complex audio filter graph
        string filterGraph = BuildBgmFilterGraph(
            videoDuration, volume, fadeInDuration, fadeOutDuration,
            enableDucking, duckingThreshold, duckingRatio);

        string durationStr = videoDuration.ToString("F2", CultureInfo.InvariantCulture);

        args.AddRange(new[]
        {
            "-filter_complex", $"\"{filterGraph}\"",
            "-map", "0:v:0",
            "-map", "[audio_out]",
            "-c:v", "copy",
            "-c:a", "aac",
            "-b:a", "192k",
            "-t", durationStr,
            $"\"{outputPath}\""
        });

        return _ffmpeg.RunCommand(args);
    }

    /// <summary>
    /// Gets the duration of a video file in seconds using ffprobe.
    /// </summary>
    /// <param name="videoPath">Path to the video file.</param>
    /// <returns>Duration in seconds, or 0 if not determinable.</returns>
    public double GetVideoDuration(string videoPath)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = _ffmpeg.FfprobePath,
                Arguments = $"-v error -show_entries format=duration " +
                            $"-of default=noprint_wrappers=1:nokey=1 \"{videoPath}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process != null)
            {
                var stderrTask = process.StandardError.ReadToEndAsync();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                stderrTask.GetAwaiter().GetResult();

                if (process.ExitCode == 0 &&
                    double.TryParse(output.Trim(), NumberStyles.Float,
                        CultureInfo.InvariantCulture, out double duration))
                {
                    return duration;
                }
            }
        }
        catch
        {
            // Fallback: try using ffmpeg -i
        }

        // Fallback: parse from ffmpeg -i output
        var info = _ffmpeg.GetVideoInfo(videoPath);
        if (info.TryGetValue("duration", out object? durationObj) && durationObj is double d)
        {
            return d;
        }

        return 0;
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Concatenates exactly two videos using the concat demuxer (no re-encoding).
    /// </summary>
    private bool ConcatTwoVideos(string video1, string video2, string outputPath)
    {
        return ConcatVideos(new List<string> { video1, video2 }, outputPath);
    }

    /// <summary>
    /// Gets the ffmpeg xfade filter name for a TransitionType using FfmpegTransitionMap.
    /// Falls back to "fade" if the type is not mapped.
    /// </summary>
    private static string GetFfmpegTransitionName(TransitionType type)
    {
        if (FfmpegTransitionMap.FilterNames.TryGetValue(type, out string? filterName)
            && filterName != null)
        {
            return filterName;
        }

        // Fallback
        return "fade";
    }

    /// <summary>
    /// Applies an xfade transition between exactly two videos.
    /// </summary>
    private bool XfadeTwoVideos(
        string video1, string video2, string outputPath,
        TransitionType transition, double transitionDuration)
    {
        double duration1 = GetVideoDuration(video1);
        if (duration1 <= 0)
        {
            Console.Error.WriteLine($"Cannot get duration of {video1}, falling back to concat.");
            return ConcatTwoVideos(video1, video2, outputPath);
        }

        // xfade offset = duration of first video minus transition duration
        double offset = Math.Max(0, duration1 - transitionDuration);
        string transitionName = GetFfmpegTransitionName(transition);

        string durationFmt = transitionDuration.ToString("F2", CultureInfo.InvariantCulture);
        string offsetFmt = offset.ToString("F2", CultureInfo.InvariantCulture);

        // Video xfade filter
        string videoFilter =
            $"[0:v][1:v]xfade=transition={transitionName}:" +
            $"duration={durationFmt}:" +
            $"offset={offsetFmt}[outv]";

        // Audio crossfade
        string audioFilter =
            $"[0:a][1:a]acrossfade=d={durationFmt}[outa]";

        string filterComplex = $"{videoFilter};{audioFilter}";

        var args = new List<string>
        {
            "-y",
            "-i", $"\"{video1}\"",
            "-i", $"\"{video2}\"",
            "-filter_complex", $"\"{filterComplex}\"",
            "-map", "[outv]",
            "-map", "[outa]",
            "-c:v", "libx264",
            "-pix_fmt", "yuv420p",
            "-c:a", "aac",
            "-b:a", "192k",
            $"\"{outputPath}\""
        };

        return _ffmpeg.RunCommand(args);
    }

    /// <summary>
    /// Chains xfade transitions across multiple videos by building a complex filter graph.
    /// </summary>
    private bool ChainXfadeTransitions(
        List<string> videoPaths,
        List<(TransitionType Type, double Duration)> transitions,
        string outputPath)
    {
        // Get all durations
        var durations = new List<double>();
        foreach (string path in videoPaths)
        {
            double dur = GetVideoDuration(path);
            if (dur <= 0)
            {
                Console.Error.WriteLine(
                    $"Cannot determine duration of {path}. Falling back to re-encode concat.");
                return ConcatVideosWithReEncode(videoPaths, outputPath);
            }
            durations.Add(dur);
        }

        int numTransitions = videoPaths.Count - 1;

        // Build input arguments
        var args = new List<string> { "-y" };
        foreach (string path in videoPaths)
        {
            args.AddRange(new[] { "-i", $"\"{path}\"" });
        }

        // Build filter_complex string
        var videoFilters = new List<string>();
        var audioFilters = new List<string>();
        double cumulativeOffset = 0;

        for (int i = 0; i < numTransitions; i++)
        {
            var (transType, transDuration) = i < transitions.Count
                ? transitions[i]
                : transitions[^1]; // repeat last transition

            string transName = GetFfmpegTransitionName(transType);

            // Calculate offset for this transition
            cumulativeOffset += durations[i];
            double offset = cumulativeOffset - transDuration;
            // Adjust cumulative to account for overlap
            cumulativeOffset -= transDuration;

            string durationFmt = transDuration.ToString("F2", CultureInfo.InvariantCulture);
            string offsetFmt = offset.ToString("F2", CultureInfo.InvariantCulture);

            // Video xfade label naming
            string inputA = i == 0 ? "[0:v]" : $"[xfv{i - 1}]";
            string inputB = $"[{i + 1}:v]";
            string outputLabel = i < numTransitions - 1 ? $"[xfv{i}]" : "[outv]";

            videoFilters.Add(
                $"{inputA}{inputB}xfade=" +
                $"transition={transName}:" +
                $"duration={durationFmt}:" +
                $"offset={offsetFmt}" +
                outputLabel);

            // Audio crossfade
            string audioA = i == 0 ? "[0:a]" : $"[xfa{i - 1}]";
            string audioB = $"[{i + 1}:a]";
            string audioOut = i < numTransitions - 1 ? $"[xfa{i}]" : "[outa]";

            audioFilters.Add(
                $"{audioA}{audioB}acrossfade=d={durationFmt}" +
                audioOut);
        }

        string filterComplex = string.Join(";", videoFilters.Concat(audioFilters));

        args.AddRange(new[]
        {
            "-filter_complex", $"\"{filterComplex}\"",
            "-map", "[outv]",
            "-map", "[outa]",
            "-c:v", "libx264",
            "-pix_fmt", "yuv420p",
            "-c:a", "aac",
            "-b:a", "192k",
            $"\"{outputPath}\""
        });

        return _ffmpeg.RunCommand(args);
    }

    /// <summary>
    /// Builds the ffmpeg filter graph string for BGM mixing.
    /// </summary>
    private static string BuildBgmFilterGraph(
        double videoDuration,
        double volume,
        double fadeInDuration,
        double fadeOutDuration,
        bool enableDucking,
        double duckingThreshold,
        double duckingRatio)
    {
        string vol = volume.ToString("F2", CultureInfo.InvariantCulture);
        string fadeIn = fadeInDuration.ToString("F2", CultureInfo.InvariantCulture);
        double fadeOutStart = Math.Max(0, videoDuration - fadeOutDuration);
        string fadeOutStartStr = fadeOutStart.ToString("F2", CultureInfo.InvariantCulture);
        string fadeOut = fadeOutDuration.ToString("F2", CultureInfo.InvariantCulture);
        string dur = videoDuration.ToString("F2", CultureInfo.InvariantCulture);

        // BGM processing: volume, fade in, fade out, trim to video duration
        string bgmChain =
            $"[1:a]volume={vol}," +
            $"afade=t=in:st=0:d={fadeIn}," +
            $"afade=t=out:st={fadeOutStartStr}:d={fadeOut}," +
            $"atrim=0:{dur},asetpts=PTS-STARTPTS[bgm_processed]";

        if (enableDucking)
        {
            // With sidechain compression: original audio controls BGM level
            string threshold = duckingThreshold.ToString("F4", CultureInfo.InvariantCulture);
            string ratio = duckingRatio.ToString("F1", CultureInfo.InvariantCulture);

            string mainAudio = "[0:a]aresample=44100[main_audio]";

            string sidechain =
                $"[bgm_processed][main_audio]sidechaincompress=" +
                $"threshold={threshold}:ratio={ratio}:" +
                $"attack=10:release=200[bgm_ducked]";

            string mix =
                "[main_audio][bgm_ducked]amix=inputs=2:duration=first[audio_out]";

            return $"{bgmChain};{mainAudio};{sidechain};{mix}";
        }
        else
        {
            // Simple mixing without ducking
            string mix =
                "[0:a]aresample=44100[main_audio];" +
                "[main_audio][bgm_processed]amix=inputs=2:" +
                "duration=first:dropout_transition=0[audio_out]";

            return $"{bgmChain};{mix}";
        }
    }

    /// <summary>
    /// Safely deletes a temporary file if it exists.
    /// </summary>
    private static void CleanupTempFile(string? path)
    {
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }
}

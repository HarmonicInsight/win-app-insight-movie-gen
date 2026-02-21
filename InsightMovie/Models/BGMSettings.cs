using System.Collections.Generic;
using System.Globalization;
using System.Text.Json.Serialization;

namespace InsightMovie.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum FadeType
    {
        None,
        Linear,
        Exponential
    }

    public class BGMSettings
    {
        [JsonPropertyName("filePath")]
        public string? FilePath { get; set; }

        [JsonPropertyName("volume")]
        public double Volume { get; set; } = 0.3;

        [JsonPropertyName("fadeInEnabled")]
        public bool FadeInEnabled { get; set; } = true;

        [JsonPropertyName("fadeInDuration")]
        public double FadeInDuration { get; set; } = 2.0;

        [JsonPropertyName("fadeInType")]
        public FadeType FadeInType { get; set; } = FadeType.Linear;

        [JsonPropertyName("fadeOutEnabled")]
        public bool FadeOutEnabled { get; set; } = true;

        [JsonPropertyName("fadeOutDuration")]
        public double FadeOutDuration { get; set; } = 3.0;

        [JsonPropertyName("fadeOutType")]
        public FadeType FadeOutType { get; set; } = FadeType.Linear;

        [JsonPropertyName("loopEnabled")]
        public bool LoopEnabled { get; set; } = true;

        [JsonPropertyName("duckingEnabled")]
        public bool DuckingEnabled { get; set; } = true;

        [JsonPropertyName("duckingVolume")]
        public double DuckingVolume { get; set; } = 0.15;

        [JsonPropertyName("duckingAttack")]
        public double DuckingAttack { get; set; } = 0.3;

        [JsonPropertyName("duckingRelease")]
        public double DuckingRelease { get; set; } = 0.5;

        [JsonIgnore]
        public bool HasBgm => !string.IsNullOrEmpty(FilePath);

        public string GetFfmpegVolumeFilter()
        {
            var volumeStr = Volume.ToString("F2", CultureInfo.InvariantCulture);
            var filter = $"volume={volumeStr}";

            if (FadeInEnabled && FadeInDuration > 0)
            {
                var fadeInDur = FadeInDuration.ToString("F2", CultureInfo.InvariantCulture);
                var curve = FadeInType == FadeType.Exponential ? "exp" : "lin";
                filter += $",afade=t=in:d={fadeInDur}:curve={curve}";
            }

            return filter;
        }

        public static readonly Dictionary<string, string> BGM_CATEGORIES = new()
        {
            { "all", "すべて" },
            { "bright", "明るい・ポップ" },
            { "calm", "落ち着いた・リラックス" },
            { "dramatic", "ドラマチック・壮大" },
            { "electronic", "エレクトロニック" },
            { "jazz", "ジャズ・ラウンジ" },
            { "classical", "クラシック" },
            { "ambient", "アンビエント" }
        };

        public static readonly List<string> SUPPORTED_AUDIO_FORMATS = new()
        {
            ".mp3",
            ".wav",
            ".ogg",
            ".flac",
            ".aac",
            ".m4a",
            ".wma"
        };
    }
}

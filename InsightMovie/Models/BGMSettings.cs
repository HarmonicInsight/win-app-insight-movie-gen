using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
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

        public Dictionary<string, object?> ToDict()
        {
            return new Dictionary<string, object?>
            {
                { "filePath", FilePath },
                { "volume", Volume },
                { "fadeInEnabled", FadeInEnabled },
                { "fadeInDuration", FadeInDuration },
                { "fadeInType", FadeInType.ToString() },
                { "fadeOutEnabled", FadeOutEnabled },
                { "fadeOutDuration", FadeOutDuration },
                { "fadeOutType", FadeOutType.ToString() },
                { "loopEnabled", LoopEnabled },
                { "duckingEnabled", DuckingEnabled },
                { "duckingVolume", DuckingVolume },
                { "duckingAttack", DuckingAttack },
                { "duckingRelease", DuckingRelease }
            };
        }

        public static BGMSettings FromDict(Dictionary<string, object?> dict)
        {
            var settings = new BGMSettings();

            if (dict.TryGetValue("filePath", out var filePath) && filePath != null)
                settings.FilePath = filePath.ToString();

            if (dict.TryGetValue("volume", out var volume) && volume != null)
            {
                if (volume is JsonElement vElem) settings.Volume = vElem.GetDouble();
                else if (double.TryParse(volume.ToString(), out var v)) settings.Volume = v;
            }

            if (dict.TryGetValue("fadeInEnabled", out var fadeInEnabled) && fadeInEnabled != null)
            {
                if (fadeInEnabled is JsonElement fieElem) settings.FadeInEnabled = fieElem.GetBoolean();
                else if (bool.TryParse(fadeInEnabled.ToString(), out var fie)) settings.FadeInEnabled = fie;
            }

            if (dict.TryGetValue("fadeInDuration", out var fadeInDuration) && fadeInDuration != null)
            {
                if (fadeInDuration is JsonElement fidElem) settings.FadeInDuration = fidElem.GetDouble();
                else if (double.TryParse(fadeInDuration.ToString(), out var fid)) settings.FadeInDuration = fid;
            }

            if (dict.TryGetValue("fadeInType", out var fadeInType) && fadeInType != null)
            {
                if (Enum.TryParse<FadeType>(fadeInType.ToString(), true, out var fit))
                    settings.FadeInType = fit;
            }

            if (dict.TryGetValue("fadeOutEnabled", out var fadeOutEnabled) && fadeOutEnabled != null)
            {
                if (fadeOutEnabled is JsonElement foeElem) settings.FadeOutEnabled = foeElem.GetBoolean();
                else if (bool.TryParse(fadeOutEnabled.ToString(), out var foe)) settings.FadeOutEnabled = foe;
            }

            if (dict.TryGetValue("fadeOutDuration", out var fadeOutDuration) && fadeOutDuration != null)
            {
                if (fadeOutDuration is JsonElement fodElem) settings.FadeOutDuration = fodElem.GetDouble();
                else if (double.TryParse(fadeOutDuration.ToString(), out var fod)) settings.FadeOutDuration = fod;
            }

            if (dict.TryGetValue("fadeOutType", out var fadeOutType) && fadeOutType != null)
            {
                if (Enum.TryParse<FadeType>(fadeOutType.ToString(), true, out var fot))
                    settings.FadeOutType = fot;
            }

            if (dict.TryGetValue("loopEnabled", out var loopEnabled) && loopEnabled != null)
            {
                if (loopEnabled is JsonElement leElem) settings.LoopEnabled = leElem.GetBoolean();
                else if (bool.TryParse(loopEnabled.ToString(), out var le)) settings.LoopEnabled = le;
            }

            if (dict.TryGetValue("duckingEnabled", out var duckingEnabled) && duckingEnabled != null)
            {
                if (duckingEnabled is JsonElement deElem) settings.DuckingEnabled = deElem.GetBoolean();
                else if (bool.TryParse(duckingEnabled.ToString(), out var de)) settings.DuckingEnabled = de;
            }

            if (dict.TryGetValue("duckingVolume", out var duckingVolume) && duckingVolume != null)
            {
                if (duckingVolume is JsonElement dvElem) settings.DuckingVolume = dvElem.GetDouble();
                else if (double.TryParse(duckingVolume.ToString(), out var dv)) settings.DuckingVolume = dv;
            }

            if (dict.TryGetValue("duckingAttack", out var duckingAttack) && duckingAttack != null)
            {
                if (duckingAttack is JsonElement daElem) settings.DuckingAttack = daElem.GetDouble();
                else if (double.TryParse(duckingAttack.ToString(), out var da)) settings.DuckingAttack = da;
            }

            if (dict.TryGetValue("duckingRelease", out var duckingRelease) && duckingRelease != null)
            {
                if (duckingRelease is JsonElement drElem) settings.DuckingRelease = drElem.GetDouble();
                else if (double.TryParse(duckingRelease.ToString(), out var dr)) settings.DuckingRelease = dr;
            }

            return settings;
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

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace InsightMovie.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TransitionType
    {
        None,
        Fade,
        Dissolve,
        WipeLeft,
        WipeRight,
        SlideLeft,
        SlideRight,
        ZoomIn
    }

    public static class TransitionNames
    {
        public static readonly Dictionary<TransitionType, string> DisplayNames = new()
        {
            { TransitionType.None, "なし" },
            { TransitionType.Fade, "フェード" },
            { TransitionType.Dissolve, "ディゾルブ" },
            { TransitionType.WipeLeft, "ワイプ（左）" },
            { TransitionType.WipeRight, "ワイプ（右）" },
            { TransitionType.SlideLeft, "スライド（左）" },
            { TransitionType.SlideRight, "スライド（右）" },
            { TransitionType.ZoomIn, "ズームイン" }
        };
    }

    public static class FfmpegTransitionMap
    {
        public static readonly Dictionary<TransitionType, string?> FilterNames = new()
        {
            { TransitionType.None, null },
            { TransitionType.Fade, "fade" },
            { TransitionType.Dissolve, "dissolve" },
            { TransitionType.WipeLeft, "wipeleft" },
            { TransitionType.WipeRight, "wiperight" },
            { TransitionType.SlideLeft, "slideleft" },
            { TransitionType.SlideRight, "slideright" },
            { TransitionType.ZoomIn, "zoomin" }
        };
    }

    public class TransitionSettings
    {
        public const double DEFAULT_TRANSITION_DURATION = 0.5;

        [JsonPropertyName("type")]
        public TransitionType Type { get; set; } = TransitionType.None;

        [JsonPropertyName("duration")]
        public double Duration { get; set; } = DEFAULT_TRANSITION_DURATION;

        public TransitionSettings()
        {
        }

        public TransitionSettings(TransitionType type, double duration = DEFAULT_TRANSITION_DURATION)
        {
            Type = type;
            Duration = duration;
        }

        [JsonIgnore]
        public string DisplayName =>
            TransitionNames.DisplayNames.TryGetValue(Type, out var name) ? name : Type.ToString();

        [JsonIgnore]
        public string? FfmpegName =>
            FfmpegTransitionMap.FilterNames.TryGetValue(Type, out var name) ? name : null;

        [JsonIgnore]
        public bool HasTransition => Type != TransitionType.None;

    }

    public static class PresetTransitions
    {
        public static readonly List<(TransitionType Type, string Name, string Description)> PRESET_TRANSITIONS = new()
        {
            (TransitionType.None, "なし", "トランジションなし"),
            (TransitionType.Fade, "フェード", "フェードイン・フェードアウト"),
            (TransitionType.Dissolve, "ディゾルブ", "クロスディゾルブ"),
            (TransitionType.WipeLeft, "ワイプ（左）", "左方向へのワイプ"),
            (TransitionType.WipeRight, "ワイプ（右）", "右方向へのワイプ"),
            (TransitionType.SlideLeft, "スライド（左）", "左方向へのスライド"),
            (TransitionType.SlideRight, "スライド（右）", "右方向へのスライド"),
            (TransitionType.ZoomIn, "ズームイン", "ズームインエフェクト")
        };
    }
}

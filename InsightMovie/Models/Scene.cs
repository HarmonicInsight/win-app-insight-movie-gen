using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InsightMovie.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum MediaType
    {
        Image,
        Video,
        None
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum DurationMode
    {
        Auto,
        Fixed
    }

    public class Scene
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("mediaPath")]
        public string? MediaPath { get; set; }

        [JsonPropertyName("mediaType")]
        public MediaType MediaType { get; set; } = MediaType.None;

        [JsonPropertyName("narrationText")]
        public string? NarrationText { get; set; }

        [JsonPropertyName("subtitleText")]
        public string? SubtitleText { get; set; }

        [JsonPropertyName("speakerId")]
        public int? SpeakerId { get; set; }

        [JsonPropertyName("keepOriginalAudio")]
        public bool KeepOriginalAudio { get; set; } = false;

        [JsonPropertyName("subtitleStyleId")]
        public string? SubtitleStyleId { get; set; }

        [JsonPropertyName("transitionType")]
        public TransitionType TransitionType { get; set; } = TransitionType.None;

        [JsonPropertyName("transitionDuration")]
        public double TransitionDuration { get; set; } = TransitionSettings.DEFAULT_TRANSITION_DURATION;

        [JsonPropertyName("durationMode")]
        public DurationMode DurationMode { get; set; } = DurationMode.Auto;

        [JsonPropertyName("fixedSeconds")]
        public double FixedSeconds { get; set; } = 3.0;

        [JsonPropertyName("audioCachePath")]
        public string? AudioCachePath { get; set; }

        [JsonPropertyName("videoCachePath")]
        public string? VideoCachePath { get; set; }

        [JsonPropertyName("textOverlays")]
        public List<TextOverlay> TextOverlays { get; set; } = new();

        [JsonIgnore]
        public bool HasMedia => !string.IsNullOrEmpty(MediaPath);

        [JsonIgnore]
        public bool HasTextOverlays => TextOverlays.Count > 0 && TextOverlays.Any(o => o.HasText);

        [JsonIgnore]
        public bool HasNarration => !string.IsNullOrEmpty(NarrationText);

        [JsonIgnore]
        public bool HasSubtitle => !string.IsNullOrEmpty(SubtitleText);

        public Scene()
        {
        }

        public Dictionary<string, object?> ToDict()
        {
            return new Dictionary<string, object?>
            {
                { "id", Id },
                { "mediaPath", MediaPath },
                { "mediaType", MediaType.ToString() },
                { "narrationText", NarrationText },
                { "subtitleText", SubtitleText },
                { "speakerId", SpeakerId },
                { "keepOriginalAudio", KeepOriginalAudio },
                { "subtitleStyleId", SubtitleStyleId },
                { "transitionType", TransitionType.ToString() },
                { "transitionDuration", TransitionDuration },
                { "durationMode", DurationMode.ToString() },
                { "fixedSeconds", FixedSeconds },
                { "audioCachePath", AudioCachePath },
                { "videoCachePath", VideoCachePath },
                { "textOverlays", TextOverlays.Select(o => o.ToDict()).ToList() }
            };
        }

        public static Scene FromDict(Dictionary<string, object?> dict)
        {
            var scene = new Scene();

            if (dict.TryGetValue("id", out var id) && id != null)
                scene.Id = id.ToString()!;

            if (dict.TryGetValue("mediaPath", out var mediaPath) && mediaPath != null)
                scene.MediaPath = mediaPath.ToString();

            if (dict.TryGetValue("mediaType", out var mediaType) && mediaType != null)
            {
                if (Enum.TryParse<MediaType>(mediaType.ToString(), true, out var mt))
                    scene.MediaType = mt;
            }

            if (dict.TryGetValue("narrationText", out var narrationText) && narrationText != null)
                scene.NarrationText = narrationText.ToString();

            if (dict.TryGetValue("subtitleText", out var subtitleText) && subtitleText != null)
                scene.SubtitleText = subtitleText.ToString();

            if (dict.TryGetValue("speakerId", out var speakerId) && speakerId != null)
            {
                if (speakerId is JsonElement spElem)
                {
                    if (spElem.ValueKind == JsonValueKind.Number)
                        scene.SpeakerId = spElem.GetInt32();
                    else if (spElem.ValueKind == JsonValueKind.Null)
                        scene.SpeakerId = null;
                }
                else if (int.TryParse(speakerId.ToString(), out var sp))
                {
                    scene.SpeakerId = sp;
                }
            }

            if (dict.TryGetValue("keepOriginalAudio", out var keepAudio) && keepAudio != null)
            {
                if (keepAudio is JsonElement kaElem) scene.KeepOriginalAudio = kaElem.GetBoolean();
                else if (bool.TryParse(keepAudio.ToString(), out var ka)) scene.KeepOriginalAudio = ka;
            }

            if (dict.TryGetValue("subtitleStyleId", out var subtitleStyleId) && subtitleStyleId != null)
                scene.SubtitleStyleId = subtitleStyleId.ToString();

            if (dict.TryGetValue("transitionType", out var transType) && transType != null)
            {
                if (Enum.TryParse<TransitionType>(transType.ToString(), true, out var tt))
                    scene.TransitionType = tt;
            }

            if (dict.TryGetValue("transitionDuration", out var transDur) && transDur != null)
            {
                if (transDur is JsonElement tdElem) scene.TransitionDuration = tdElem.GetDouble();
                else if (double.TryParse(transDur.ToString(), out var td)) scene.TransitionDuration = td;
            }

            if (dict.TryGetValue("durationMode", out var durMode) && durMode != null)
            {
                if (Enum.TryParse<DurationMode>(durMode.ToString(), true, out var dm))
                    scene.DurationMode = dm;
            }

            if (dict.TryGetValue("fixedSeconds", out var fixedSec) && fixedSec != null)
            {
                if (fixedSec is JsonElement fsElem) scene.FixedSeconds = fsElem.GetDouble();
                else if (double.TryParse(fixedSec.ToString(), out var fs)) scene.FixedSeconds = fs;
            }

            if (dict.TryGetValue("audioCachePath", out var audioCache) && audioCache != null)
                scene.AudioCachePath = audioCache.ToString();

            if (dict.TryGetValue("videoCachePath", out var videoCache) && videoCache != null)
                scene.VideoCachePath = videoCache.ToString();

            if (dict.TryGetValue("textOverlays", out var overlays) && overlays != null)
            {
                if (overlays is JsonElement jsonArr && jsonArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in jsonArr.EnumerateArray())
                    {
                        var overlayDict = JsonSerializer.Deserialize<Dictionary<string, object?>>(elem.GetRawText());
                        if (overlayDict != null)
                            scene.TextOverlays.Add(TextOverlay.FromDict(overlayDict));
                    }
                }
            }

            return scene;
        }
    }
}

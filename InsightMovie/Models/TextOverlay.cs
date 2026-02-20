using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InsightMovie.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TextAlignment
    {
        Left,
        Center,
        Right
    }

    public class TextOverlay
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("text")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("xPercent")]
        public double XPercent { get; set; } = 50.0;

        [JsonPropertyName("yPercent")]
        public double YPercent { get; set; } = 50.0;

        [JsonPropertyName("fontFamily")]
        public string FontFamily { get; set; } = "Yu Gothic UI";

        [JsonPropertyName("fontSize")]
        public int FontSize { get; set; } = 64;

        [JsonPropertyName("fontBold")]
        public bool FontBold { get; set; } = true;

        [JsonPropertyName("textColor")]
        public int[] TextColor { get; set; } = new[] { 255, 255, 255 };

        [JsonPropertyName("strokeColor")]
        public int[] StrokeColor { get; set; } = new[] { 0, 0, 0 };

        [JsonPropertyName("strokeWidth")]
        public int StrokeWidth { get; set; } = 2;

        [JsonPropertyName("alignment")]
        public TextAlignment Alignment { get; set; } = TextAlignment.Center;

        [JsonPropertyName("shadowEnabled")]
        public bool ShadowEnabled { get; set; } = true;

        [JsonPropertyName("shadowColor")]
        public int[] ShadowColor { get; set; } = new[] { 0, 0, 0 };

        [JsonPropertyName("shadowOffset")]
        public int[] ShadowOffset { get; set; } = new[] { 2, 2 };

        public TextOverlay()
        {
        }

        [JsonIgnore]
        public bool HasText => !string.IsNullOrWhiteSpace(Text);

        [JsonIgnore]
        public string DisplayLabel => string.IsNullOrWhiteSpace(Text)
            ? "(空)"
            : Text.Length > 20 ? Text[..20] + "..." : Text;

        public Dictionary<string, object?> ToDict()
        {
            return new Dictionary<string, object?>
            {
                { "id", Id },
                { "text", Text },
                { "xPercent", XPercent },
                { "yPercent", YPercent },
                { "fontFamily", FontFamily },
                { "fontSize", FontSize },
                { "fontBold", FontBold },
                { "textColor", TextColor },
                { "strokeColor", StrokeColor },
                { "strokeWidth", StrokeWidth },
                { "alignment", Alignment.ToString() },
                { "shadowEnabled", ShadowEnabled },
                { "shadowColor", ShadowColor },
                { "shadowOffset", ShadowOffset }
            };
        }

        public static TextOverlay FromDict(Dictionary<string, object?> dict)
        {
            var overlay = new TextOverlay();

            if (dict.TryGetValue("id", out var id) && id != null)
                overlay.Id = id.ToString()!;

            if (dict.TryGetValue("text", out var text) && text != null)
                overlay.Text = text.ToString()!;

            if (dict.TryGetValue("xPercent", out var xp) && xp != null)
            {
                if (xp is JsonElement xpElem) overlay.XPercent = xpElem.GetDouble();
                else if (double.TryParse(xp.ToString(), out var x)) overlay.XPercent = x;
            }

            if (dict.TryGetValue("yPercent", out var yp) && yp != null)
            {
                if (yp is JsonElement ypElem) overlay.YPercent = ypElem.GetDouble();
                else if (double.TryParse(yp.ToString(), out var y)) overlay.YPercent = y;
            }

            if (dict.TryGetValue("fontFamily", out var ff) && ff != null)
                overlay.FontFamily = ff.ToString()!;

            if (dict.TryGetValue("fontSize", out var fs) && fs != null)
            {
                if (fs is JsonElement fsElem) overlay.FontSize = fsElem.GetInt32();
                else if (int.TryParse(fs.ToString(), out var f)) overlay.FontSize = f;
            }

            if (dict.TryGetValue("fontBold", out var fb) && fb != null)
            {
                if (fb is JsonElement fbElem) overlay.FontBold = fbElem.GetBoolean();
                else if (bool.TryParse(fb.ToString(), out var b)) overlay.FontBold = b;
            }

            overlay.TextColor = ParseIntArray(dict, "textColor", new[] { 255, 255, 255 });
            overlay.StrokeColor = ParseIntArray(dict, "strokeColor", new[] { 0, 0, 0 });

            if (dict.TryGetValue("strokeWidth", out var sw) && sw != null)
            {
                if (sw is JsonElement swElem) overlay.StrokeWidth = swElem.GetInt32();
                else if (int.TryParse(sw.ToString(), out var s)) overlay.StrokeWidth = s;
            }

            if (dict.TryGetValue("alignment", out var align) && align != null)
            {
                if (Enum.TryParse<TextAlignment>(align.ToString(), true, out var a))
                    overlay.Alignment = a;
            }

            if (dict.TryGetValue("shadowEnabled", out var se) && se != null)
            {
                if (se is JsonElement seElem) overlay.ShadowEnabled = seElem.GetBoolean();
                else if (bool.TryParse(se.ToString(), out var s)) overlay.ShadowEnabled = s;
            }

            overlay.ShadowColor = ParseIntArray(dict, "shadowColor", new[] { 0, 0, 0 });
            overlay.ShadowOffset = ParseIntArray(dict, "shadowOffset", new[] { 2, 2 });

            return overlay;
        }

        private static int[] ParseIntArray(Dictionary<string, object?> dict, string key, int[] defaultValue)
        {
            if (!dict.TryGetValue(key, out var val) || val == null)
                return defaultValue;

            if (val is JsonElement jsonElem && jsonElem.ValueKind == JsonValueKind.Array)
                return jsonElem.EnumerateArray().Select(e => e.GetInt32()).ToArray();

            if (val is int[] intArr)
                return intArr;

            if (val is object[] objArr)
            {
                return objArr.Select(o =>
                {
                    if (o is JsonElement je) return je.GetInt32();
                    return int.Parse(o?.ToString() ?? "0");
                }).ToArray();
            }

            return defaultValue;
        }

        /// <summary>
        /// Creates a title overlay preset for cover pages.
        /// </summary>
        public static TextOverlay CreateTitle(string text = "タイトル")
        {
            return new TextOverlay
            {
                Text = text,
                XPercent = 50.0,
                YPercent = 40.0,
                FontSize = 72,
                FontBold = true,
                Alignment = TextAlignment.Center,
                TextColor = new[] { 255, 255, 255 },
                StrokeColor = new[] { 0, 0, 0 },
                StrokeWidth = 3,
                ShadowEnabled = true,
                ShadowColor = new[] { 0, 0, 0 },
                ShadowOffset = new[] { 3, 3 }
            };
        }

        /// <summary>
        /// Creates a subtitle overlay preset for cover pages.
        /// </summary>
        public static TextOverlay CreateSubheading(string text = "サブタイトル")
        {
            return new TextOverlay
            {
                Text = text,
                XPercent = 50.0,
                YPercent = 55.0,
                FontSize = 40,
                FontBold = false,
                Alignment = TextAlignment.Center,
                TextColor = new[] { 220, 220, 220 },
                StrokeColor = new[] { 0, 0, 0 },
                StrokeWidth = 1,
                ShadowEnabled = true,
                ShadowColor = new[] { 0, 0, 0 },
                ShadowOffset = new[] { 2, 2 }
            };
        }
    }
}

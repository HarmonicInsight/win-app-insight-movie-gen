using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace InsightMovie.Models
{
    public class TextStyle
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonPropertyName("name")]
        public string Name { get; set; } = "デフォルト";

        [JsonPropertyName("fontFamily")]
        public string FontFamily { get; set; } = "Yu Gothic UI";

        [JsonPropertyName("fontSize")]
        public int FontSize { get; set; } = 48;

        [JsonPropertyName("fontBold")]
        public bool FontBold { get; set; } = true;

        [JsonPropertyName("textColor")]
        public int[] TextColor { get; set; } = new[] { 255, 255, 255 };

        [JsonPropertyName("strokeColor")]
        public int[] StrokeColor { get; set; } = new[] { 0, 0, 0 };

        [JsonPropertyName("strokeWidth")]
        public int StrokeWidth { get; set; } = 3;

        [JsonPropertyName("backgroundColor")]
        public int[] BackgroundColor { get; set; } = new[] { 0, 0, 0 };

        [JsonPropertyName("backgroundOpacity")]
        public double BackgroundOpacity { get; set; } = 0.7;

        [JsonPropertyName("shadowEnabled")]
        public bool ShadowEnabled { get; set; } = true;

        [JsonPropertyName("shadowColor")]
        public int[] ShadowColor { get; set; } = new[] { 0, 0, 0 };

        [JsonPropertyName("shadowOffset")]
        public int[] ShadowOffset { get; set; } = new[] { 2, 2 };

        public TextStyle()
        {
        }

        public TextStyle(string id, string name)
        {
            Id = id;
            Name = name;
        }

        [JsonIgnore]
        public string HexTextColor => GetHexTextColor();

        [JsonIgnore]
        public string HexStrokeColor => GetHexStrokeColor();

        public string GetHexTextColor()
        {
            return $"#{TextColor[0]:X2}{TextColor[1]:X2}{TextColor[2]:X2}";
        }

        public string GetHexStrokeColor()
        {
            return $"#{StrokeColor[0]:X2}{StrokeColor[1]:X2}{StrokeColor[2]:X2}";
        }

        public Dictionary<string, object?> ToDict()
        {
            return new Dictionary<string, object?>
            {
                { "id", Id },
                { "name", Name },
                { "fontFamily", FontFamily },
                { "fontSize", FontSize },
                { "fontBold", FontBold },
                { "textColor", TextColor },
                { "strokeColor", StrokeColor },
                { "strokeWidth", StrokeWidth },
                { "backgroundColor", BackgroundColor },
                { "backgroundOpacity", BackgroundOpacity },
                { "shadowEnabled", ShadowEnabled },
                { "shadowColor", ShadowColor },
                { "shadowOffset", ShadowOffset }
            };
        }

        public static TextStyle FromDict(Dictionary<string, object?> dict)
        {
            var style = new TextStyle();

            if (dict.TryGetValue("id", out var id) && id != null)
                style.Id = id.ToString()!;

            if (dict.TryGetValue("name", out var name) && name != null)
                style.Name = name.ToString()!;

            if (dict.TryGetValue("fontFamily", out var fontFamily) && fontFamily != null)
                style.FontFamily = fontFamily.ToString()!;

            if (dict.TryGetValue("fontSize", out var fontSize) && fontSize != null)
            {
                if (fontSize is JsonElement fsElem)
                    style.FontSize = fsElem.GetInt32();
                else if (int.TryParse(fontSize.ToString(), out var fs))
                    style.FontSize = fs;
            }

            if (dict.TryGetValue("fontBold", out var fontBold) && fontBold != null)
            {
                if (fontBold is JsonElement fbElem)
                    style.FontBold = fbElem.GetBoolean();
                else if (bool.TryParse(fontBold.ToString(), out var fb))
                    style.FontBold = fb;
            }

            style.TextColor = ParseIntArray(dict, "textColor", new[] { 255, 255, 255 });
            style.StrokeColor = ParseIntArray(dict, "strokeColor", new[] { 0, 0, 0 });

            if (dict.TryGetValue("strokeWidth", out var strokeWidth) && strokeWidth != null)
            {
                if (strokeWidth is JsonElement swElem)
                    style.StrokeWidth = swElem.GetInt32();
                else if (int.TryParse(strokeWidth.ToString(), out var sw))
                    style.StrokeWidth = sw;
            }

            style.BackgroundColor = ParseIntArray(dict, "backgroundColor", new[] { 0, 0, 0 });

            if (dict.TryGetValue("backgroundOpacity", out var bgOpacity) && bgOpacity != null)
            {
                if (bgOpacity is JsonElement boElem)
                    style.BackgroundOpacity = boElem.GetDouble();
                else if (double.TryParse(bgOpacity.ToString(), out var bo))
                    style.BackgroundOpacity = bo;
            }

            if (dict.TryGetValue("shadowEnabled", out var shadowEnabled) && shadowEnabled != null)
            {
                if (shadowEnabled is JsonElement seElem)
                    style.ShadowEnabled = seElem.GetBoolean();
                else if (bool.TryParse(shadowEnabled.ToString(), out var se))
                    style.ShadowEnabled = se;
            }

            style.ShadowColor = ParseIntArray(dict, "shadowColor", new[] { 0, 0, 0 });
            style.ShadowOffset = ParseIntArray(dict, "shadowOffset", new[] { 2, 2 });

            return style;
        }

        private static int[] ParseIntArray(Dictionary<string, object?> dict, string key, int[] defaultValue)
        {
            if (!dict.TryGetValue(key, out var val) || val == null)
                return defaultValue;

            if (val is JsonElement jsonElem && jsonElem.ValueKind == JsonValueKind.Array)
            {
                return jsonElem.EnumerateArray().Select(e => e.GetInt32()).ToArray();
            }

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

        // --- Presets ---

        public static readonly List<TextStyle> PRESET_STYLES = new()
        {
            new TextStyle
            {
                Id = "default",
                Name = "デフォルト",
                FontFamily = "Yu Gothic UI",
                FontSize = 48,
                FontBold = true,
                TextColor = new[] { 255, 255, 255 },
                StrokeColor = new[] { 0, 0, 0 },
                StrokeWidth = 3,
                BackgroundColor = new[] { 0, 0, 0 },
                BackgroundOpacity = 0.7,
                ShadowEnabled = true,
                ShadowColor = new[] { 0, 0, 0 },
                ShadowOffset = new[] { 2, 2 }
            },
            new TextStyle
            {
                Id = "news",
                Name = "ニュース風",
                FontFamily = "Yu Gothic UI",
                FontSize = 42,
                FontBold = true,
                TextColor = new[] { 255, 255, 255 },
                StrokeColor = new[] { 0, 0, 128 },
                StrokeWidth = 2,
                BackgroundColor = new[] { 0, 0, 128 },
                BackgroundOpacity = 0.8,
                ShadowEnabled = false,
                ShadowColor = new[] { 0, 0, 0 },
                ShadowOffset = new[] { 0, 0 }
            },
            new TextStyle
            {
                Id = "cinema",
                Name = "映画風",
                FontFamily = "Yu Mincho",
                FontSize = 52,
                FontBold = false,
                TextColor = new[] { 255, 255, 255 },
                StrokeColor = new[] { 0, 0, 0 },
                StrokeWidth = 1,
                BackgroundColor = new[] { 0, 0, 0 },
                BackgroundOpacity = 0.0,
                ShadowEnabled = true,
                ShadowColor = new[] { 0, 0, 0 },
                ShadowOffset = new[] { 3, 3 }
            },
            new TextStyle
            {
                Id = "variety",
                Name = "バラエティ風",
                FontFamily = "Yu Gothic UI",
                FontSize = 56,
                FontBold = true,
                TextColor = new[] { 255, 255, 0 },
                StrokeColor = new[] { 255, 0, 0 },
                StrokeWidth = 4,
                BackgroundColor = new[] { 0, 0, 0 },
                BackgroundOpacity = 0.0,
                ShadowEnabled = true,
                ShadowColor = new[] { 0, 0, 0 },
                ShadowOffset = new[] { 3, 3 }
            },
            new TextStyle
            {
                Id = "documentary",
                Name = "ドキュメンタリー風",
                FontFamily = "Yu Gothic UI",
                FontSize = 44,
                FontBold = false,
                TextColor = new[] { 255, 255, 255 },
                StrokeColor = new[] { 0, 0, 0 },
                StrokeWidth = 2,
                BackgroundColor = new[] { 0, 0, 0 },
                BackgroundOpacity = 0.5,
                ShadowEnabled = false,
                ShadowColor = new[] { 0, 0, 0 },
                ShadowOffset = new[] { 0, 0 }
            },
            new TextStyle
            {
                Id = "education",
                Name = "教育・解説風",
                FontFamily = "Yu Gothic UI",
                FontSize = 46,
                FontBold = true,
                TextColor = new[] { 51, 51, 51 },
                StrokeColor = new[] { 255, 255, 255 },
                StrokeWidth = 3,
                BackgroundColor = new[] { 255, 255, 255 },
                BackgroundOpacity = 0.9,
                ShadowEnabled = false,
                ShadowColor = new[] { 0, 0, 0 },
                ShadowOffset = new[] { 0, 0 }
            },
            new TextStyle
            {
                Id = "horror",
                Name = "ホラー風",
                FontFamily = "Yu Mincho",
                FontSize = 50,
                FontBold = true,
                TextColor = new[] { 255, 0, 0 },
                StrokeColor = new[] { 0, 0, 0 },
                StrokeWidth = 2,
                BackgroundColor = new[] { 0, 0, 0 },
                BackgroundOpacity = 0.6,
                ShadowEnabled = true,
                ShadowColor = new[] { 128, 0, 0 },
                ShadowOffset = new[] { 2, 2 }
            },
            new TextStyle
            {
                Id = "cute",
                Name = "かわいい風",
                FontFamily = "Yu Gothic UI",
                FontSize = 44,
                FontBold = true,
                TextColor = new[] { 255, 105, 180 },
                StrokeColor = new[] { 255, 255, 255 },
                StrokeWidth = 3,
                BackgroundColor = new[] { 255, 228, 225 },
                BackgroundOpacity = 0.7,
                ShadowEnabled = false,
                ShadowColor = new[] { 0, 0, 0 },
                ShadowOffset = new[] { 0, 0 }
            },
            new TextStyle
            {
                Id = "tech",
                Name = "テック風",
                FontFamily = "Consolas",
                FontSize = 40,
                FontBold = false,
                TextColor = new[] { 0, 255, 0 },
                StrokeColor = new[] { 0, 0, 0 },
                StrokeWidth = 1,
                BackgroundColor = new[] { 0, 0, 0 },
                BackgroundOpacity = 0.85,
                ShadowEnabled = true,
                ShadowColor = new[] { 0, 128, 0 },
                ShadowOffset = new[] { 1, 1 }
            },
            new TextStyle
            {
                Id = "elegant",
                Name = "エレガント風",
                FontFamily = "Yu Mincho",
                FontSize = 46,
                FontBold = false,
                TextColor = new[] { 212, 175, 55 },
                StrokeColor = new[] { 0, 0, 0 },
                StrokeWidth = 1,
                BackgroundColor = new[] { 0, 0, 0 },
                BackgroundOpacity = 0.4,
                ShadowEnabled = true,
                ShadowColor = new[] { 0, 0, 0 },
                ShadowOffset = new[] { 2, 2 }
            }
        };

        public static readonly List<(string FontName, string DisplayName)> AVAILABLE_FONTS = new()
        {
            ("Yu Gothic UI", "游ゴシック UI"),
            ("Yu Gothic", "游ゴシック"),
            ("Yu Mincho", "游明朝"),
            ("Meiryo", "メイリオ"),
            ("Meiryo UI", "メイリオ UI"),
            ("MS Gothic", "ＭＳ ゴシック"),
            ("MS Mincho", "ＭＳ 明朝"),
            ("MS PGothic", "ＭＳ Ｐゴシック"),
            ("MS PMincho", "ＭＳ Ｐ明朝"),
            ("BIZ UDGothic", "BIZ UDゴシック"),
            ("BIZ UDMincho", "BIZ UD明朝"),
            ("Consolas", "Consolas"),
            ("Arial", "Arial"),
            ("Segoe UI", "Segoe UI")
        };

        public static readonly List<(string Name, int[] Color)> PRESET_TEXT_COLORS = new()
        {
            ("白", new[] { 255, 255, 255 }),
            ("黒", new[] { 0, 0, 0 }),
            ("赤", new[] { 255, 0, 0 }),
            ("青", new[] { 0, 0, 255 }),
            ("黄", new[] { 255, 255, 0 }),
            ("緑", new[] { 0, 128, 0 }),
            ("ピンク", new[] { 255, 105, 180 }),
            ("オレンジ", new[] { 255, 165, 0 }),
            ("金", new[] { 212, 175, 55 }),
            ("水色", new[] { 0, 191, 255 })
        };

        public static readonly List<(string Name, int[] Color)> PRESET_STROKE_COLORS = new()
        {
            ("黒", new[] { 0, 0, 0 }),
            ("白", new[] { 255, 255, 255 }),
            ("紺", new[] { 0, 0, 128 }),
            ("赤", new[] { 255, 0, 0 }),
            ("緑", new[] { 0, 128, 0 }),
            ("灰色", new[] { 128, 128, 128 }),
            ("茶色", new[] { 139, 69, 19 }),
            ("紫", new[] { 128, 0, 128 })
        };
    }
}

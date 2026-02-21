using System;
using System.Collections.Generic;
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

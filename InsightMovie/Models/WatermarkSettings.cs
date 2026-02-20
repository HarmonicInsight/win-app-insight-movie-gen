using System.Text.Json.Serialization;

namespace InsightMovie.Models
{
    public class WatermarkSettings
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = false;

        [JsonPropertyName("imagePath")]
        public string? ImagePath { get; set; }

        [JsonPropertyName("position")]
        public string Position { get; set; } = "bottom-right";

        [JsonPropertyName("opacity")]
        public double Opacity { get; set; } = 0.7;

        [JsonPropertyName("scale")]
        public double Scale { get; set; } = 0.12;

        [JsonPropertyName("marginPercent")]
        public double MarginPercent { get; set; } = 2.0;

        [JsonIgnore]
        public bool HasWatermark => Enabled && !string.IsNullOrEmpty(ImagePath);

        public static readonly Dictionary<string, string> PositionNames = new()
        {
            { "top-left", "左上" },
            { "top-right", "右上" },
            { "bottom-left", "左下" },
            { "bottom-right", "右下" },
            { "center", "中央" }
        };
    }
}

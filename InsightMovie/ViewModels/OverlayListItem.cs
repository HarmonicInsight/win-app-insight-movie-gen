using InsightMovie.Infrastructure;
using InsightMovie.Models;
using InsightMovie.Services;

namespace InsightMovie.ViewModels
{
    public class OverlayListItem : ViewModelBase
    {
        private string _displayLabel = string.Empty;

        public TextOverlay Overlay { get; }

        public string DisplayLabel
        {
            get => _displayLabel;
            set => SetProperty(ref _displayLabel, value);
        }

        public OverlayListItem(TextOverlay overlay, int index)
        {
            Overlay = overlay;
            UpdateLabel(index);
        }

        public void UpdateLabel(int index)
        {
            var text = string.IsNullOrWhiteSpace(Overlay.Text) ? LocalizationService.GetString("Overlay.Empty") : Overlay.Text;
            if (text.Length > 15)
                text = text[..15] + "...";

            DisplayLabel = $"[{index + 1}] {text}  ({Overlay.XPercent:F0}%, {Overlay.YPercent:F0}%)";
        }
    }
}

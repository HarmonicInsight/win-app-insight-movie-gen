using InsightMovie.Infrastructure;
using InsightMovie.Models;
using InsightMovie.Services;

namespace InsightMovie.ViewModels
{
    public class SceneListItem : ViewModelBase
    {
        private string _label = string.Empty;

        public Scene Scene { get; }

        public string Label
        {
            get => _label;
            set => SetProperty(ref _label, value);
        }

        public SceneListItem(Scene scene, int index)
        {
            Scene = scene;
            UpdateLabel(index);
        }

        public void UpdateLabel(int index)
        {
            var label = LocalizationService.GetString("Scene.Label", index + 1);
            if (!string.IsNullOrEmpty(Scene.NarrationText))
            {
                var preview = Scene.NarrationText.Length > 12
                    ? Scene.NarrationText[..12] + "..."
                    : Scene.NarrationText;
                label += $" - {preview}";
            }
            Label = label;
        }
    }
}

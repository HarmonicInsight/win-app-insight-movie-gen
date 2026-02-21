using InsightMovie.Models;

namespace InsightMovie.Services
{
    public interface IDialogService
    {
        string? ShowOpenFileDialog(string title, string filter, string? defaultExt = null);
        string[]? ShowOpenFileDialogMultiple(string title, string filter, string? defaultExt = null);
        string? ShowSaveFileDialog(string title, string filter, string? defaultExt = null, string? fileName = null);
        bool ShowConfirmation(string message, string title);
        void ShowInfo(string message, string title);
        void ShowWarning(string message, string title);
        void ShowError(string message, string title);
        bool ShowYesNo(string message, string title);
        BGMSettings? ShowBgmDialog(BGMSettings? currentSettings);
        TextStyle? ShowTextStyleDialog(TextStyle? currentStyle);
        void ShowLicenseDialog(Core.Config config);
        /// <summary>Shows a selection dialog for a list of items. Returns the selected index or -1 if cancelled.</summary>
        int ShowListSelectDialog(string title, string[] items);
    }
}

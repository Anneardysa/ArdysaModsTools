using System;
using System.Threading.Tasks;

namespace ArdysaModsTools.UI.Interfaces
{
    public interface IDota2PerformanceView
    {
        Task LoadSettingsAsync(string jsonSettings);
        Task ShowToastAsync(string message, string type);

        Task ShowCfgBannerAsync(string message, string state);
        void InvokeSafeClose();
        void StartDrag();
        void CopyTextToClipboard(string text);
        string? PromptForExportPath();

        event EventHandler OnViewShown;
        event EventHandler<string> OnApplySettingsRequested;
        event EventHandler<string> OnExportCfgRequested;
        event EventHandler OnDeleteCfgRequested;
    }
}

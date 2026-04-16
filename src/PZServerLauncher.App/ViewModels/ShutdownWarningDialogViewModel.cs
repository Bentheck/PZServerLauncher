namespace PZServerLauncher.App.ViewModels;

public sealed class ShutdownWarningDialogViewModel : ViewModelBase
{
    public string DialogTitle => "Close PZ Server Launcher?";

    public string DialogSummary =>
        "Closing the app now will stop the embedded host, shut down any running Project Zomboid servers, and end any active launcher operations.";

    public string DialogGuidance =>
        "Send to tray if you want the launcher to keep running in the background. Choose Close Everything only when you are ready to stop the runtime and every managed server on this machine.";

    public string TrayGuidance =>
        "Sending the launcher to the tray keeps the runtime, running servers, and live operations active. You can reopen the window from the tray icon at any time.";

    public string ImpactList =>
        "What closes with the app:\n- Running game servers\n- The local management API\n- Live console/event streaming";
}

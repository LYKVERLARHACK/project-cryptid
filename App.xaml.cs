using System.Windows;
using RealityCheckDesktop.Services;

namespace RealityCheckDesktop;

public partial class App : System.Windows.Application
{
    private TrayHost? _tray;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        IAnalysisBackend backend = e.Args.Contains("--demo", StringComparer.OrdinalIgnoreCase) ? new DemoAnalysisBackend() : new YouTubeAnalysisBackend();
        _tray = new TrayHost(backend);
        _tray.Initialize(e.Args.Contains("--preview", StringComparer.OrdinalIgnoreCase));
    }
    protected override void OnExit(ExitEventArgs e) { _tray?.Dispose(); base.OnExit(e); }
}

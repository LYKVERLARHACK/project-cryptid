using System.Drawing;
using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using RealityCheckDesktop.Models;

namespace RealityCheckDesktop.Services;

public sealed class TrayHost(IAnalysisBackend? backend) : IDisposable
{
    private Forms.NotifyIcon? _icon;
    private OverlayWindow? _window;
    private readonly OverlayApi _api = new();
    private readonly IncidentStore _incidentStore = new();
    private readonly IncidentChatService _incidentChat = new();
    private readonly DispatcherTimer _foregroundUrlTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private string? _lastActiveYouTubeUrl;

    public void Initialize(bool preview = false)
    {
        _icon = new Forms.NotifyIcon { Text = "Reality Check · AI Detector", Icon = System.Drawing.SystemIcons.Shield, Visible = true,
            ContextMenuStrip = new Forms.ContextMenuStrip() };
        _icon.ContextMenuStrip.Items.Add("Open analysis", null, (_, _) => Open());
        _icon.ContextMenuStrip.Items.Add("Exit", null, (_, _) => System.Windows.Application.Current.Shutdown());
        _icon.MouseClick += (_, e) => { if (e.Button == Forms.MouseButtons.Left) Open(); };
        _window = new OverlayWindow(_api, backend, _incidentStore, _incidentChat, Close, showInTaskbar: preview);
        _foregroundUrlTimer.Tick += (_, _) => CaptureForegroundYouTubeUrl();
        _foregroundUrlTimer.Start();
        if (preview) Open();
    }
    private void Open()
    {
        if (_window is null || _icon is null) return;
        if (!_window.IsVisible) CaptureForegroundYouTubeUrl();
        var cursor = Forms.Cursor.Position;
        var screen = Forms.Screen.FromPoint(cursor);
        _window.SuggestYouTubeUrl(_lastActiveYouTubeUrl);
        var dpi = PopupPlacement.DpiAt(cursor);
        var scale = dpi / 96d;
        var physicalSize = new System.Drawing.Size((int)Math.Ceiling(_window.Width * scale), (int)Math.Ceiling(_window.Height * scale));
        var point = PopupPlacement.AboveTaskbar(screen.WorkingArea, physicalSize);
        _window.PlaceAt(point, physicalSize, screen.WorkingArea);
        _window.Show();
        _window.Activate();
    }
    private void CaptureForegroundYouTubeUrl()
    {
        var url = ActiveYouTubeUrl.TryRead(out var browserIsForeground);
        if (!browserIsForeground) return;
        _lastActiveYouTubeUrl = url;
        _window?.SuggestYouTubeUrl(url);
    }
    private void Close() => _window?.Dismiss();
    public void Dispose()
    {
        _foregroundUrlTimer.Stop();
        if (_icon is not null) { _icon.Visible = false; _icon.Dispose(); }
        _window?.ClosePermanently();
    }
}

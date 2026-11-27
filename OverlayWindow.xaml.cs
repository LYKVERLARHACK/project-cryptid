using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using Microsoft.Data.Sqlite;
using RealityCheckDesktop.Models;
using RealityCheckDesktop.Services;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;
using Button = System.Windows.Controls.Button;
using Orientation = System.Windows.Controls.Orientation;

namespace RealityCheckDesktop;

public partial class OverlayWindow : Window
{
    private readonly OverlayApi _api;
    private readonly IAnalysisBackend? _backend;
    private readonly IncidentStore _incidentStore;
    private readonly IncidentChatService _incidentChat;
    private readonly Action _onDismiss;
    private string _youtubeUrl = "";
    private CancellationTokenSource? _analysisCancellation;
    private bool _permanentClose;
    private bool _hasPlacement;
    private int _leftPx;
    private int _bottomPx;
    private System.Drawing.Rectangle _workingArea;
    private double _dpiScale = 1;
    private OverlayTheme _theme = OverlayTheme.Default;
    private HistoryWindow? _historyWindow;

    public OverlayWindow(OverlayApi api, IAnalysisBackend? backend, IncidentStore incidentStore,
        IncidentChatService incidentChat, Action onDismiss, bool showInTaskbar = false)
    {
        InitializeComponent();
        _api = api;
        _backend = backend;
        _incidentStore = incidentStore;
        _incidentChat = incidentChat;
        _onDismiss = onDismiss;
        try
        {
            _theme = UserPreferences.SelectedTheme;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            System.Windows.MessageBox.Show(this, $"Could not load the theme preference: {ex.Message}", "Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        if (showInTaskbar) ShowInTaskbar = true;
        _api.State.Changed += State_Changed;
        Loaded += (_, _) => { Focus(); PlayOpen(); Render(_api.State.Current); };
        Render(_api.State.Current);
    }

    public void PlaceAt(Services.PopupPoint point, System.Drawing.Size physicalSize, System.Drawing.Rectangle workingArea)
    {
        _leftPx = point.X;
        _bottomPx = point.Y + physicalSize.Height;
        _workingArea = workingArea;
        _dpiScale = physicalSize.Width / Width;
        _hasPlacement = true;
        UpdateNativePlacement();
    }
    public void SuggestYouTubeUrl(string? url)
    {
        _youtubeUrl = YouTubeUrl.TryNormalize(url, out var normalized) ? normalized : "";
    }
    public void Dismiss() { _analysisCancellation?.Cancel(); _youtubeUrl = ""; _api.Reset(); Hide(); }
    public void ClosePermanently() { _permanentClose = true; _analysisCancellation?.Cancel(); _api.State.Changed -= State_Changed; Close(); }
    protected override void OnClosed(EventArgs e) { if (!_permanentClose) _onDismiss(); base.OnClosed(e); }
    protected override void OnPreviewKeyDown(System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Escape) { Dismiss(); e.Handled = true; return; } base.OnPreviewKeyDown(e); }

    private void PlayOpen()
    {
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(170));
        BeginAnimation(OpacityProperty, fade);
    }
    private void CloseButton_Click(object sender, RoutedEventArgs e) => Dismiss();
    private void RealityButton_Click(object sender, RoutedEventArgs e) => SelectModeAndRun(AnalysisMode.RealityCheck);
    private void DetectorButton_Click(object sender, RoutedEventArgs e) => SelectModeAndRun(AnalysisMode.AiDetector);
    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new System.Windows.Controls.ContextMenu();
        var themeMenu = new System.Windows.Controls.MenuItem { Header = "Theme" };
        foreach (var theme in Enum.GetValues<OverlayTheme>())
        {
            var item = new System.Windows.Controls.MenuItem
            {
                Header = ThemeName(theme),
                IsCheckable = true,
                IsChecked = _theme == theme
            };
            item.Click += (_, _) => SelectTheme(theme);
            themeMenu.Items.Add(item);
        }
        var hide = new System.Windows.Controls.MenuItem { Header = "Hide overlay" };
        hide.Click += (_, _) => Dismiss();
        var history = new System.Windows.Controls.MenuItem { Header = "Incident history" };
        history.Click += (_, _) => OpenHistory();
        var about = new System.Windows.Controls.MenuItem { Header = "About Reality Check" };
        about.Click += (_, _) => System.Windows.MessageBox.Show(this, "Reality Check Desktop\nLocal video analysis for claims and visual authenticity.", "About", MessageBoxButton.OK, MessageBoxImage.Information);
        var exit = new System.Windows.Controls.MenuItem { Header = "Exit" };
        exit.Click += (_, _) => System.Windows.Application.Current.Shutdown();
        menu.Items.Add(themeMenu);
        menu.Items.Add(new Separator());
        menu.Items.Add(history);
        menu.Items.Add(new Separator());
        menu.Items.Add(hide);
        menu.Items.Add(new Separator());
        menu.Items.Add(about);
        menu.Items.Add(new Separator());
        menu.Items.Add(exit);
        SettingsButton.ContextMenu = menu;
        menu.IsOpen = true;
    }

    private void OpenHistory()
    {
        if (_historyWindow is { IsVisible: true })
        {
            _historyWindow.Activate();
            return;
        }

        _historyWindow = new HistoryWindow(_incidentStore, _incidentChat) { Owner = this };
        _historyWindow.Closed += (_, _) => _historyWindow = null;
        _historyWindow.Show();
        _historyWindow.Activate();
    }

    private void SelectTheme(OverlayTheme theme)
    {
        try
        {
            UserPreferences.SaveTheme(theme);
            _theme = theme;
            Render(_api.State.Current);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException or InvalidOperationException)
        {
            System.Windows.MessageBox.Show(this, $"Could not save the theme preference: {ex.Message}", "Settings", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string ThemeName(OverlayTheme theme) => theme switch
    {
        OverlayTheme.Kids => "Kids mode",
        OverlayTheme.Inferno => "Inferno",
        OverlayTheme.Tech => "Tech",
        OverlayTheme.White => "White",
        _ => "Default"
    };

    private void UpdateNativePlacement()
    {
        if (!_hasPlacement) return;
        var width = (int)Math.Ceiling(Width * _dpiScale);
        var height = (int)Math.Ceiling(Height * _dpiScale);
        var top = Math.Max(_workingArea.Top + 8, _bottomPx - height);
        var handle = new WindowInteropHelper(this).EnsureHandle();
        SetWindowPos(handle, new IntPtr(-1), _leftPx, top, width, height, 0x0010 | 0x0040);
    }

    private void SelectModeAndRun(AnalysisMode mode)
    {
        _api.Select(mode);
        Run_Click(this, new RoutedEventArgs());
    }

    private async void Run_Click(object sender, RoutedEventArgs e)
    {
        var mode = _api.State.Current.Mode;
        if (_backend is null)
        {
            FailImmediately(mode, "The analysis service is not available.");
            return;
        }
        if (!YouTubeUrl.TryNormalize(_youtubeUrl, out var normalized))
        {
            FailImmediately(mode, "No active YouTube video was detected. Open a YouTube video in a supported browser, then reopen the checker.");
            return;
        }
        _youtubeUrl = normalized;
        var request = _api.Begin(mode, normalized);
        var cts = new CancellationTokenSource(); _analysisCancellation = cts;
        var progress = new Progress<string>(message => { try { _api.ReportProgress(request.RequestId, message); } catch (InvalidOperationException) { } });
        try
        {
            var result = await _backend.AnalyzeAsync(request, progress, cts.Token);
            if (!cts.IsCancellationRequested)
            {
                SaveIncident(request, result);
                _api.SubmitResult(new(request.RequestId, request.Mode, result));
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (!cts.IsCancellationRequested)
            {
                SaveFailure(request, ex.Message);
                try { _api.ReportFailure(new(request.RequestId, ex.Message)); } catch (InvalidOperationException) { }
            }
        }
        finally { if (ReferenceEquals(_analysisCancellation, cts)) _analysisCancellation = null; cts.Dispose(); }
    }

    private void SaveIncident(AnalysisRequest request, AnalysisResult result)
    {
        try
        {
            _incidentStore.Save(request, result);
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException or DllNotFoundException)
        {
            System.Windows.MessageBox.Show(this, $"The result is ready, but could not be saved to incident history: {ex.Message}",
                "Incident history", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SaveFailure(AnalysisRequest request, string message)
    {
        try
        {
            _incidentStore.SaveFailure(request, message);
        }
        catch (Exception ex) when (ex is SqliteException or IOException or UnauthorizedAccessException or InvalidOperationException or DllNotFoundException)
        {
            System.Windows.MessageBox.Show(this, $"Could not save this failed check to incident history: {ex.Message}",
                "Incident history", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void FailImmediately(AnalysisMode mode, string message)
    {
        var request = _api.Begin(mode);
        _api.ReportFailure(new(request.RequestId, message));
    }

    private void State_Changed(object? sender, OverlaySnapshot snapshot)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(() => Render(snapshot)); return; }
        Render(snapshot);
    }

    private void Render(OverlaySnapshot state)
    {
        if (ContentHost is null) return;
        ApplyTheme(state.Status);
        StyleMode(RealityButton, state.Mode == AnalysisMode.RealityCheck);
        StyleMode(DetectorButton, state.Mode == AnalysisMode.AiDetector);
        UpdateWindowHeight(state.Status);
        PopupCard.Visibility = state.Status == AnalysisStatus.Idle ? Visibility.Collapsed : Visibility.Visible;
        ResultHeader.Visibility = state.Status == AnalysisStatus.Complete ? Visibility.Visible : Visibility.Collapsed;
        ProgressPanel.Visibility = state.Status == AnalysisStatus.Checking ? Visibility.Visible : Visibility.Collapsed;
        DetailPanel.Visibility = state.Status is AnalysisStatus.Complete or AnalysisStatus.Error ? Visibility.Visible : Visibility.Collapsed;
        ContentHost.Children.Clear();
        switch (state.Status)
        {
            case AnalysisStatus.Checking: RenderChecking(state); break;
            case AnalysisStatus.Complete: RenderResult(state); break;
            case AnalysisStatus.Error: RenderError(state); break;
            default: RenderIdle(); break;
        }
    }

    private void UpdateWindowHeight(AnalysisStatus status)
    {
        var target = status switch
        {
            AnalysisStatus.Checking => 180d,
            AnalysisStatus.Complete => 380d,
            AnalysisStatus.Error => 225d,
            _ => _theme == OverlayTheme.Kids ? 255d : 72d
        };
        if (Math.Abs(Height - target) < 0.5) return;
        Height = target;
        UpdateNativePlacement();
    }

    private void ApplyTheme(AnalysisStatus status)
    {
        var palette = GetPalette(_theme);
        var isKids = _theme == OverlayTheme.Kids;
        var showBorders = _theme != OverlayTheme.Default;
        FontFamily = new System.Windows.Media.FontFamily(isKids ? "Comic Sans MS" : "Segoe UI");

        DockBar.Background = BrushColor(palette.DockBackground);
        DockBar.BorderBrush = BrushColor(palette.DockBorder);
        DockBar.BorderThickness = new Thickness(isKids ? 3 : 1);
        DockBar.CornerRadius = new CornerRadius(isKids ? 22 : 24);
        PopupCard.Background = BrushColor(palette.PopupBackground);
        PopupCard.BorderBrush = BrushColor(palette.PopupBorder);
        PopupCard.BorderThickness = new Thickness(isKids ? 2 : 1);
        PopupCard.CornerRadius = new CornerRadius(isKids ? 20 : 19);
        ProgressPanel.Background = BrushColor(palette.PanelBackground);
        DetailPanel.Background = BrushColor(palette.PanelBackground);
        DetailPanel.BorderBrush = BrushColor(palette.PanelBorder);
        DetailPanel.BorderThickness = new Thickness(isKids ? 1.5 : showBorders ? 1 : 0);
        VerdictPanel.Background = BrushColor(palette.VerdictBackground);
        ScoreBadge.Background = BrushColor(palette.ScoreBackground);
        ScoreLabel.Foreground = BrushColor(palette.ScoreForeground);
        PopupEyebrow.Foreground = BrushColor(palette.PrimaryText);
        FinalVerdictLabel.Foreground = BrushColor(palette.SecondaryText);
        VerdictText.Foreground = BrushColor(palette.PrimaryText);
        ProgressText.Foreground = BrushColor(palette.PrimaryText);
        DetailCaption.Foreground = BrushColor(palette.SecondaryText);
        AnalysisProgressBar.Foreground = BrushColor(palette.ProgressForeground);
        AnalysisProgressBar.Background = BrushColor(palette.ProgressBackground);
        AnalysisProgressBar.BorderBrush = BrushColor(palette.PanelBorder);
        AnalysisProgressBar.BorderThickness = new Thickness(showBorders ? 1 : 0);
        CloseButton.Background = BrushColor(palette.CloseBackground);
        CloseButton.BorderBrush = BrushColor(palette.CloseBorder);
        CloseButton.Foreground = BrushColor(palette.PrimaryText);
        SettingsButton.Background = BrushColor(palette.SettingsBackground);
        SettingsButton.BorderBrush = BrushColor(palette.SettingsBorder);
        SettingsButton.Foreground = BrushColor(palette.PrimaryText);
        SettingsButton.BorderThickness = new Thickness(isKids ? 2 : 1);
        RealityButton.Margin = isKids ? new Thickness(0) : new Thickness(0, 0, 4, 0);
        DetectorButton.Margin = isKids ? new Thickness(0) : new Thickness(4, 0, 4, 0);
        RealityButton.BorderThickness = new Thickness(isKids ? 2 : 1);
        DetectorButton.BorderThickness = new Thickness(isKids ? 2 : 1);
        SetModeButtonCorner(RealityButton, isKids ? new CornerRadius(18, 0, 0, 18) : new CornerRadius(14));
        SetModeButtonCorner(DetectorButton, isKids ? new CornerRadius(0, 18, 18, 0) : new CornerRadius(14));
        KidsCat.Visibility = isKids && status == AnalysisStatus.Idle ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void SetModeButtonCorner(Button button, CornerRadius cornerRadius)
    {
        button.ApplyTemplate();
        if (button.Template.FindName("ModeButtonSurface", button) is Border surface)
        {
            surface.CornerRadius = cornerRadius;
        }
    }

    private void RenderIdle()
    {
        PopupEyebrow.Text = "READY WHEN YOU ARE";
    }

    private void RenderChecking(OverlaySnapshot state)
    {
        PopupEyebrow.Text = "ANALYSING";
        ProgressText.Text = state.Progress ?? "Checking the video…";
    }

    private void RenderError(OverlaySnapshot state)
    {
        PopupEyebrow.Text = "ANALYSIS NEEDS ATTENTION";
        DetailCaption.Text = "WHAT HAPPENED:";
        AddText(state.Error ?? "Please try again.", 12, "#F4F7FB", false, 0, 8, wrap: true, line: 1.4);
    }

    private void RenderResult(OverlaySnapshot state)
    {
        PopupEyebrow.Text = "DONE ANALYSING";
        DetailCaption.Text = "DETAILED ANALYSIS:";
        if (state.Result is RealityCheckResult reality)
        {
            ScoreLabel.Text = $"{reality.RealismPercent}%";
            VerdictText.Text = reality.VerdictTitle;
            AddVideoTitle(reality.VideoTitle, reality.VideoUrl);
            AddText(reality.Explanation, 12, "#E4ECF6", false, 0, 10, wrap: true, line: 1.4);
            AddText(reality.EvidenceSearched ? "WEB SOURCES FOUND · STILL REVIEW THE EVIDENCE" : "NO WEB SOURCES FOUND · PRELIMINARY ESTIMATE", 9, "#FFD07A", true, 0, 10, wrap: true);
            if (reality.Claims.Count > 0) { AddSection("CLAIMS REVIEWED"); foreach (var c in reality.Claims.Take(4)) AddFinding(c.Claim, $"{c.Assessment} · {c.ConfidencePercent}%", "#CCD2DB"); }
            AddSources(reality.Sources);
        }
        else if (state.Result is AiDetectorResult detector)
        {
            ScoreLabel.Text = $"{detector.AiLikelihoodPercent}%";
            VerdictText.Text = detector.VerdictTitle;
            AddVideoTitle(detector.VideoTitle, detector.VideoUrl);
            if (!string.IsNullOrWhiteSpace(detector.VideoSummary))
            {
                AddText(detector.VideoSummary, 12, "#E4ECF6", false, 0, 3, wrap: true, line: 1.4);
            }
            AddSection("WHY THIS ESTIMATE");
            AddText(detector.Explanation, 12, "#E4ECF6", false, 0, 7, wrap: true, line: 1.4);
            if (detector.Findings.Count > 0) { AddSection("VISUAL FINDINGS"); foreach (var finding in detector.Findings.Take(5)) AddFinding(finding.Label, FindingText(finding), FindingColor(finding.State), finding.Timestamp); }
            AddText(detector.AudioAnalyzed
                ? $"Transcript used: {detector.TranscriptSource}. Speech content was reviewed; audio authenticity was not."
                : "No audio transcript was available for this review.", 10, "#BED0E5", false, 0, 8, wrap: true);
            AddText("Uncalibrated estimate · not a probability or proof.", 10, "#FFD07A", false, 0, 8, wrap: true);
            AddSources(detector.Sources);
        }
    }

    private void AddSources(IReadOnlyList<EvidenceSource> sources)
    {
        if (sources.Count == 0) return;
        AddSection("SOURCES & EVIDENCE");
        foreach (var source in sources.Take(4))
        {
            var link = new TextBlock { Text = source.Publisher is null ? source.Title : $"{source.Publisher}  ·  {source.Title}", Foreground = BrushColor(ThemeTextColor("#9BC9FA")), FontSize = 10, TextDecorations = TextDecorations.Underline, Cursor = System.Windows.Input.Cursors.Hand, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 7) };
            link.MouseLeftButtonUp += (_, _) => { try { Process.Start(new ProcessStartInfo(source.Url.AbsoluteUri) { UseShellExecute = true }); } catch { } };
            ContentHost.Children.Add(link);
        }
    }
    private void AddVideoTitle(string title, string url)
    {
        if (string.IsNullOrWhiteSpace(title)) return;
        AddText(title, 12, "#DDE2E9", true, 0, 4, wrap: true);
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Host.EndsWith("youtube.com", StringComparison.OrdinalIgnoreCase))
        {
            var link = new TextBlock { Text = "Open video on YouTube ↗", Foreground = BrushColor(ThemeTextColor("#9BC9FA")), FontSize = 9, TextDecorations = TextDecorations.Underline, Cursor = System.Windows.Input.Cursors.Hand, Margin = new Thickness(0, 0, 0, 10) };
            link.MouseLeftButtonUp += (_, _) => { try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); } catch { } };
            ContentHost.Children.Add(link);
        }
    }
    private void AddSection(string title) => AddText(title, 9, "#B8D0EA", true, 9, 6);
    private void AddFinding(string title, string detail, string detailColor, string? timestamp = null)
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        grid.ColumnDefinitions.Add(new ColumnDefinition()); grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.Children.Add(new TextBlock { Text = title, Foreground = BrushColor(ThemeTextColor("#E4ECF6")), FontSize = 10, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 7, 0) });
        var right = new StackPanel { Orientation = Orientation.Horizontal };
        if (!string.IsNullOrWhiteSpace(timestamp)) right.Children.Add(new TextBlock { Text = timestamp + "  ", Foreground = BrushColor(ThemeTextColor("#B8D0EA")), FontSize = 9 });
        right.Children.Add(new TextBlock { Text = detail, Foreground = BrushColor(ThemeTextColor(detailColor)), FontSize = 9, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, MaxWidth = 135 });
        Grid.SetColumn(right, 1); grid.Children.Add(right); ContentHost.Children.Add(grid);
    }
    private void AddText(string text, double size, string color, bool bold, double top, double bottom, bool wrap = false, double line = 1.15)
    {
        var scaledSize = size * 0.9;
        ContentHost.Children.Add(new TextBlock { Text = text, FontSize = scaledSize, Foreground = BrushColor(ThemeTextColor(color)), FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextWrapping = wrap ? TextWrapping.Wrap : TextWrapping.NoWrap, LineHeight = scaledSize * line, Margin = new Thickness(0, top * 0.9, 0, bottom * 0.9) });
    }
    private void StyleMode(Button button, bool selected)
    {
        var palette = GetPalette(_theme);
        button.BorderBrush = BrushColor(selected ? palette.ButtonSelectedBorder : palette.ButtonIdleBorder);
        button.Background = BrushColor(selected ? palette.ButtonSelectedBackground : palette.ButtonIdleBackground);
        button.Foreground = BrushColor(palette.PrimaryText);
    }

    private string ThemeTextColor(string color)
    {
        if (_theme == OverlayTheme.Default) return color;
        var palette = GetPalette(_theme);
        return color switch
        {
            "#FFD07A" or "#F0BE75" => palette.WarningText,
            "#8EE3D2" => palette.PositiveText,
            "#A4ADBA" or "#B8D0EA" => palette.MutedText,
            "#9BC9FA" => palette.LinkText,
            _ => palette.PrimaryText
        };
    }

    private static ThemePalette GetPalette(OverlayTheme theme) => theme switch
    {
        OverlayTheme.Kids => new(
            "#F3E4A1", "#191713", "#E1C24E", "#F8EAB0", "#191713", "#191713",
            "#F8EAB0", "#191713", "#FFF1C7", "#332A18", "#E9D477",
            "#D5A937", "#211B0F", "#2A251B", "#3A3020", "#51472F", "#1B5785", "#805300", "#185F4C",
            "#D5A937", "#FFFAE9", "#E9D477", "#332A18", "#E2C456", "#191713"),
        OverlayTheme.Inferno => new(
            "#1B1110", "#5A2119", "#FF4C24", "#3B1713", "#FF9B42", "#85402A",
            "#24110F", "#75301F", "#351713", "#72321E", "#5D2115",
            "#FF6A2B", "#1D0B06", "#FFF1E8", "#FFD7C5", "#E5AA91", "#FFB68D", "#FFC078", "#A8E3D2",
            "#FF6336", "#4A201A", "#461A14", "#8B3D25", "#F05A24", "#FFAA59"),
        OverlayTheme.Tech => new(
            "#101214", "#3A4045", "#30363B", "#191D20", "#6B7379", "#444B51",
            "#121416", "#3A4045", "#1A1D20", "#3A4045", "#292D31",
            "#51585E", "#F4F6F7", "#ECEFF1", "#D2D6D9", "#ABB1B6", "#CED4D8", "#E0E2E4", "#C3CDD3",
            "#B8C0C6", "#42484D", "#262A2E", "#555C62", "#292D31", "#535B61"),
        OverlayTheme.White => new(
            "#FFFFFF", "#171717", "#DCEBFA", "#F3F5F7", "#1565C0", "#30343A",
            "#FFFFFF", "#252525", "#F4F5F7", "#D8DBE0", "#E7EBF0",
            "#1565C0", "#FFFFFF", "#1A1D20", "#333840", "#636A73", "#155AA0", "#8A5000", "#1D6A50",
            "#1565C0", "#E2E6EB", "#EDF0F3", "#C4C9D0", "#E9EDF2", "#AAB2BC"),
        _ => new(
            "#171B22", "#242A34", "#075BC3", "#153B6A", "#1674E8", "#214F7F",
            "#102B4C", "#09192D", "#173B67", "#173B67", "#174477",
            "#E99416", "#FFFFFF", "#F3F7FC", "#E8EFF8", "#B8D0EA", "#9BC9FA", "#FFD07A", "#8EE3D2",
            "#0B62D1", "#F4F7FB", "#173A63", "#254B75", "#075BC3", "#126BD7")
    };

    private sealed record ThemePalette(
        string DockBackground, string DockBorder,
        string ButtonSelectedBackground, string ButtonIdleBackground,
        string ButtonSelectedBorder, string ButtonIdleBorder,
        string PopupBackground, string PopupBorder,
        string PanelBackground, string PanelBorder, string VerdictBackground,
        string ScoreBackground, string ScoreForeground,
        string PrimaryText, string SecondaryText, string MutedText, string LinkText,
        string WarningText, string PositiveText,
        string ProgressForeground, string ProgressBackground,
        string CloseBackground, string CloseBorder,
        string SettingsBackground, string SettingsBorder);

    private static string FindingText(DetectorFinding finding) => finding.State switch { FindingState.Detected => "Detected", FindingState.NotDetected => "Not detected", FindingState.Unavailable => "Unavailable", _ => "Inconclusive" } + (string.IsNullOrWhiteSpace(finding.Detail) ? "" : $" · {finding.Detail}");
    private static string FindingColor(FindingState state) => state switch { FindingState.Detected => "#F0BE75", FindingState.NotDetected => "#8EE3D2", _ => "#A4ADBA" };
    private static SolidColorBrush BrushColor(string hex) => new((Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));

    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}

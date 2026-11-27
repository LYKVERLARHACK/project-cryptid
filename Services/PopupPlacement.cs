using System.Drawing;
using System.Runtime.InteropServices;

namespace RealityCheckDesktop.Services;

public readonly record struct PopupPoint(int X, int Y);
public static class PopupPlacement
{
    public static int DpiAt(Point point)
    {
        var monitor = MonitorFromPoint(point, 2);
        return GetDpiForMonitor(monitor, 0, out var dpiX, out _) == 0 ? (int)dpiX : 96;
    }
    /// <summary>Right-aligns above the taskbar work-area edge on the monitor selected by the tray click.</summary>
    public static PopupPoint AboveTaskbar(Rectangle workingArea, Size popupSize, int gap = 10)
    {
        if (popupSize.Width <= 0 || popupSize.Height <= 0) throw new ArgumentOutOfRangeException(nameof(popupSize));
        int x = Math.Clamp(workingArea.Right - popupSize.Width - gap, workingArea.Left + gap, Math.Max(workingArea.Left + gap, workingArea.Right - popupSize.Width - gap));
        int y = workingArea.Bottom - popupSize.Height - gap;
        return new(x, Math.Max(workingArea.Top + gap, y));
    }
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(Point point, uint flags);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);
}

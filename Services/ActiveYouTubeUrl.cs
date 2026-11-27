using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace RealityCheckDesktop.Services;

/// <summary>Best-effort read of the foreground browser address bar; never scans background windows.</summary>
public static class ActiveYouTubeUrl
{
    private static readonly HashSet<string> BrowserProcesses = new(StringComparer.OrdinalIgnoreCase)
        { "brave", "chrome", "msedge", "firefox", "vivaldi", "opera" };

    public static string? TryRead(out bool browserIsForeground)
    {
        browserIsForeground = false;
        try
        {
            var hwnd = GetForegroundWindow();
            if (hwnd == IntPtr.Zero) return null;
            GetWindowThreadProcessId(hwnd, out var processId);
            using var process = Process.GetProcessById((int)processId);
            if (!BrowserProcesses.Contains(process.ProcessName)) return null;
            browserIsForeground = true;
            var root = AutomationElement.FromHandle(hwnd);
            var edits = root.FindAll(TreeScope.Descendants,
                new OrCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit),
                                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ComboBox),
                                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Custom)));
            foreach (AutomationElement element in edits)
            {
                try
                {
                    if (!element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern)) continue;
                    var value = ((ValuePattern)pattern).Current.Value;
                    if (YouTubeUrl.TryNormalize(value, out var normalized)) return normalized;
                }
                catch (ElementNotAvailableException) { }
                catch (InvalidOperationException) { }
                catch (ArgumentException) { }
                catch (COMException) { }
            }
        }
        catch (ElementNotAvailableException) { }
        catch (InvalidOperationException) { }
        catch (ArgumentException) { }
        catch (COMException) { }
        return null;
    }

    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
}

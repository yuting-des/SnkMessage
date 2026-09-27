using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;

namespace SnkMessage
{
    internal static class ClipboardActivity
    {
        private static DateTime suppressedUntil = DateTime.MinValue;
        public static bool IsSuppressed { get { return DateTime.UtcNow < suppressedUntil; } }
        public static void Suppress(int milliseconds) { suppressedUntil = DateTime.UtcNow.AddMilliseconds(milliseconds); }
    }

    internal sealed class SelectionContext
    {
        public string Text;
        public Rect Bounds;
        public IntPtr TargetWindow;
        public AutomationElement SourceElement;
        public bool SourceIsEditable;
    }

    internal sealed class GlobalSelectionWatcher : IDisposable
    {
        private readonly NativeMethods.HookProc callback;
        private IntPtr hook;
        private NativeMethods.POINT down;
        private bool pressed;
        public event Action<int, int, bool> MouseCompleted;

        public GlobalSelectionWatcher()
        {
            callback = HookCallback;
            hook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, callback, IntPtr.Zero, 0);
            if (hook == IntPtr.Zero) throw new InvalidOperationException("Unable to install mouse selection hook.");
        }

        private IntPtr HookCallback(int code, IntPtr wParam, IntPtr lParam)
        {
            if (code >= 0)
            {
                var data = (NativeMethods.MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(NativeMethods.MSLLHOOKSTRUCT));
                if (wParam.ToInt32() == NativeMethods.WM_LBUTTONDOWN)
                {
                    pressed = true; down = data.pt;
                }
                else if (wParam.ToInt32() == NativeMethods.WM_LBUTTONUP && pressed)
                {
                    pressed = false;
                    bool dragged = Math.Abs(data.pt.X - down.X) > 4 || Math.Abs(data.pt.Y - down.Y) > 4;
                    var handler = MouseCompleted;
                    if (handler != null) handler(data.pt.X, data.pt.Y, dragged);
                }
            }
            return NativeMethods.CallNextHookEx(hook, code, wParam, lParam);
        }

        public void Dispose()
        {
            if (hook != IntPtr.Zero) NativeMethods.UnhookWindowsHookEx(hook);
            hook = IntPtr.Zero;
        }
    }

    internal static class SelectionService
    {
        public static async Task<SelectionContext> CaptureAsync(int x, int y, bool allowClipboardFallback)
        {
            await Task.Delay(90);
            var point = new NativeMethods.POINT { X = x, Y = y };
            IntPtr hwnd = NativeMethods.GetAncestor(NativeMethods.WindowFromPoint(point), NativeMethods.GA_ROOT);
            uint pid; NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
            if (pid == (uint)Process.GetCurrentProcess().Id) return null;

            AutomationElement element = null;
            try { element = AutomationElement.FromPoint(new System.Windows.Point(x, y)); } catch { }
            var context = TryAutomationSelection(element, hwnd);
            if (context != null) return context;
            if (!allowClipboardFallback || !ClipboardFallbackAllowed(pid)) return null;

            string text = await CopySelectionPreservingClipboard();
            if (String.IsNullOrWhiteSpace(text)) return null;
            return new SelectionContext {
                Text = text.Trim(), TargetWindow = hwnd, SourceElement = element,
                SourceIsEditable = IsEditable(element), Bounds = new Rect(x, y, 1, 1)
            };
        }

        public static SelectionContext FromCopiedText(string text, IntPtr hwnd, int x, int y)
        {
            if (String.IsNullOrWhiteSpace(text)) return null;
            uint pid=0;
            if(hwnd!=IntPtr.Zero) NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
            if (pid != 0 && pid == (uint)Process.GetCurrentProcess().Id) return null;
            AutomationElement element = null;
            try { element = AutomationElement.FromPoint(new System.Windows.Point(x, y)); } catch { }
            return new SelectionContext { Text=text.Trim(), TargetWindow=hwnd, SourceElement=element, SourceIsEditable=IsEditable(element), Bounds=new Rect(x,y,1,1) };
        }

        private static bool ClipboardFallbackAllowed(uint processId)
        {
            try
            {
                string name = Process.GetProcessById((int)processId).ProcessName.ToLowerInvariant();
                string[] allowed = { "wechat", "weixin", "wechatappex", "notepad", "winword", "chrome", "msedge", "firefox", "teams", "slack" };
                foreach (string candidate in allowed) if (name.Contains(candidate)) return true;
            }
            catch { }
            return false;
        }

        private static SelectionContext TryAutomationSelection(AutomationElement start, IntPtr hwnd)
        {
            var candidates = new List<AutomationElement>();
            if (start != null) candidates.Add(start);
            try { if (AutomationElement.FocusedElement != null) candidates.Add(AutomationElement.FocusedElement); } catch { }
            AutomationElement current = start;
            for (int i = 0; i < 4 && current != null; i++)
            {
                try { current = TreeWalker.ControlViewWalker.GetParent(current); if (current != null) candidates.Add(current); } catch { break; }
            }

            foreach (var element in candidates)
            {
                try
                {
                    if ((bool)element.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty)) continue;
                    object raw;
                    if (!element.TryGetCurrentPattern(TextPattern.Pattern, out raw)) continue;
                    var ranges = ((TextPattern)raw).GetSelection();
                    if (ranges == null || ranges.Length == 0) continue;
                    string text = ranges[0].GetText(-1);
                    if (String.IsNullOrWhiteSpace(text)) continue;
                    Rect[] rectangles = ranges[0].GetBoundingRectangles();
                    Rect bounds = rectangles.Length > 0 ? rectangles[0] : Rect.Empty;
                    return new SelectionContext { Text=text.Trim(), Bounds=bounds, TargetWindow=hwnd, SourceElement=element, SourceIsEditable=IsEditable(element) };
                }
                catch { }
            }
            return null;
        }

        private static bool IsEditable(AutomationElement element)
        {
            if (element == null) return false;
            try
            {
                object raw;
                if (!element.TryGetCurrentPattern(ValuePattern.Pattern, out raw)) return false;
                return !((ValuePattern)raw).Current.IsReadOnly;
            }
            catch { return false; }
        }

        private static async Task<string> CopySelectionPreservingClipboard()
        {
            ClipboardActivity.Suppress(900);
            System.Windows.IDataObject original = null;
            try { original = Clipboard.GetDataObject(); Clipboard.Clear(); } catch { }
            NativeMethods.SendShortcut(NativeMethods.VK_C);
            await Task.Delay(150);
            string text = null;
            try { if (Clipboard.ContainsText()) text = Clipboard.GetText(); } catch { }
            try { if (original != null) Clipboard.SetDataObject(original, true); else Clipboard.Clear(); } catch { }
            return text;
        }
    }
}

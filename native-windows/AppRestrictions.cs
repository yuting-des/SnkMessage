using System;
using System.Diagnostics;

namespace SnkMessage
{
    internal static class AppRestrictions
    {
        // Keep this restriction isolated so a future release can disable it without
        // changing selection, clipboard, or overlay behavior.
        internal static readonly bool WeChatOnly = true;

        internal static bool IsAllowedWindow(IntPtr hwnd)
        {
            if (!WeChatOnly) return true;
            if (hwnd == IntPtr.Zero) return false;
            uint pid;
            NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
            if (pid == 0) return false;
            try
            {
                string name = Process.GetProcessById((int)pid).ProcessName.ToLowerInvariant();
                return name.Contains("wechat") || name.Contains("weixin");
            }
            catch { return false; }
        }
    }
}

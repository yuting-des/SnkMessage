using System;
using System.Runtime.InteropServices;

namespace SnkMessage
{
    internal static class NativeMethods
    {
        internal const int WH_MOUSE_LL = 14;
        internal const int WM_LBUTTONDOWN = 0x0201;
        internal const int WM_LBUTTONUP = 0x0202;
        internal const int GWL_EXSTYLE = -20;
        internal const int WS_EX_TOOLWINDOW = 0x00000080;
        internal const int WS_EX_NOACTIVATE = 0x08000000;
        internal const uint GA_ROOT = 2;
        internal const byte VK_CONTROL = 0x11;
        internal const byte VK_C = 0x43;
        internal const byte VK_V = 0x56;
        internal const uint KEYEVENTF_KEYUP = 0x0002;
        internal const uint KEYEVENTF_UNICODE = 0x0004;
        internal const uint INPUT_KEYBOARD = 1;
        internal const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        internal const uint MOUSEEVENTF_LEFTUP = 0x0004;

        internal delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        internal struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public UIntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct INPUT { public uint type; public INPUTUNION data; }

        [StructLayout(LayoutKind.Explicit)]
        internal struct INPUTUNION { [FieldOffset(0)] public KEYBDINPUT keyboard; }

        [StructLayout(LayoutKind.Sequential)]
        internal struct KEYBDINPUT
        {
            public ushort virtualKey;
            public ushort scanCode;
            public uint flags;
            public uint time;
            public UIntPtr extraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern IntPtr SetWindowsHookEx(int idHook, HookProc callback, IntPtr module, uint threadId);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool UnhookWindowsHookEx(IntPtr hook);

        [DllImport("user32.dll")]
        internal static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        internal static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(out POINT point);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        internal static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);

        [DllImport("user32.dll", SetLastError=true)]
        internal static extern uint SendInput(uint count, INPUT[] inputs, int size);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll")]
        internal static extern uint GetClipboardSequenceNumber();

        [DllImport("user32.dll")]
        internal static extern bool SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

        [DllImport("user32.dll")]
        internal static extern int GetWindowLong(IntPtr hwnd, int index);

        [DllImport("user32.dll")]
        internal static extern int SetWindowLong(IntPtr hwnd, int index, int value);

        [DllImport("user32.dll")]
        internal static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);

        internal static void SendShortcut(byte key)
        {
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event(key, 0, 0, UIntPtr.Zero);
            keybd_event(key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        internal static void ClickAt(int x,int y)
        {
            SetCursorPos(x,y);mouse_event(MOUSEEVENTF_LEFTDOWN,0,0,0,UIntPtr.Zero);mouse_event(MOUSEEVENTF_LEFTUP,0,0,0,UIntPtr.Zero);
        }

        internal static bool SendUnicodeText(string text)
        {
            if(String.IsNullOrEmpty(text))return false;
            var inputs=new INPUT[text.Length*2];
            for(int i=0;i<text.Length;i++)
            {
                inputs[i*2]=new INPUT{type=INPUT_KEYBOARD,data=new INPUTUNION{keyboard=new KEYBDINPUT{scanCode=text[i],flags=KEYEVENTF_UNICODE}}};
                inputs[i*2+1]=new INPUT{type=INPUT_KEYBOARD,data=new INPUTUNION{keyboard=new KEYBDINPUT{scanCode=text[i],flags=KEYEVENTF_UNICODE|KEYEVENTF_KEYUP}}};
            }
            return SendInput((uint)inputs.Length,inputs,Marshal.SizeOf(typeof(INPUT)))==(uint)inputs.Length;
        }
    }
}

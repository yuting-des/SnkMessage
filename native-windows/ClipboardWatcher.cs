using System;
using System.Windows;
using System.Windows.Threading;
using System.IO;

namespace SnkMessage
{
    internal sealed class ClipboardWatcher : IDisposable
    {
        private readonly DispatcherTimer timer;
        private readonly Action<string, IntPtr, int, int> onTextCopied;
        private uint lastSequence;
        private string lastText;
        private DateTime lastRaised = DateTime.MinValue;
        public ClipboardWatcher(Action<string, IntPtr, int, int> callback)
        {
            onTextCopied=callback;
            lastSequence=NativeMethods.GetClipboardSequenceNumber();
            timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(180)};
            timer.Tick+=delegate
            {
                uint current=NativeMethods.GetClipboardSequenceNumber();
                if(current==0||current==lastSequence)return;
                lastSequence=current;
                Mark("sequence");
                ReadClipboard();
            };
            timer.Start();
        }

        private void ReadClipboard()
        {
            if (ClipboardActivity.IsSuppressed) return;
            string text = null;
            try { if (Clipboard.ContainsText()) text=Clipboard.GetText(); } catch { return; }
            if (String.IsNullOrWhiteSpace(text)) return;
            Mark("clipboard");
            if (text==lastText && (DateTime.UtcNow-lastRaised).TotalMilliseconds<700) return;
            lastText=text;lastRaised=DateTime.UtcNow;
            NativeMethods.POINT point;
            if (!NativeMethods.GetCursorPos(out point)) point=new NativeMethods.POINT{X=0,Y=0};
            if (onTextCopied!=null) onTextCopied(text,NativeMethods.GetForegroundWindow(),point.X,point.Y);
        }

        public void Dispose()
        {
            timer.Stop();
        }

        private static void Mark(string name)
        {
            if(Environment.GetEnvironmentVariable("SNKMESSAGE_DIAGNOSTICS")=="1")
                File.WriteAllText(Path.Combine(Path.GetTempPath(),"SnkMessage."+name),DateTime.UtcNow.ToString("O"));
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;

namespace SnkMessage
{
    internal static class ContextCapture
    {
        internal static bool Enabled = true;
    }

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
        public IReadOnlyList<ConversationTurn> Context = Array.Empty<ConversationTurn>();
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
        public static async Task<SelectionContext> CaptureAsync(int x, int y)
        {
            await Task.Delay(180);
            var point = new NativeMethods.POINT { X = x, Y = y };
            IntPtr hwnd = NativeMethods.GetAncestor(NativeMethods.WindowFromPoint(point), NativeMethods.GA_ROOT);
            uint pid; NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
            if (pid == (uint)Process.GetCurrentProcess().Id) return null;

            AutomationElement element = null;
            try { element = AutomationElement.FromPoint(new System.Windows.Point(x, y)); } catch { }
            var context = TryAutomationSelection(element, hwnd);
            if (context != null) return context;
            return null;
        }

        public static SelectionContext FromCopiedText(string text, IntPtr hwnd, int x, int y)
        {
            if (String.IsNullOrWhiteSpace(text)) return null;
            uint pid=0;
            if(hwnd!=IntPtr.Zero) NativeMethods.GetWindowThreadProcessId(hwnd, out pid);
            if (pid != 0 && pid == (uint)Process.GetCurrentProcess().Id) return null;
            AutomationElement element = null;
            try { element = AutomationElement.FromPoint(new System.Windows.Point(x, y)); } catch { }
            string selectedText=text.Trim();
            return new SelectionContext { Text=selectedText, TargetWindow=hwnd, SourceElement=element, SourceIsEditable=IsEditable(element), Bounds=new Rect(x,y,1,1), Context=CollectNearbyContext(element,hwnd,selectedText,new Rect(x,y,1,1)) };
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
                    string selectedText=text.Trim();
                    return new SelectionContext { Text=selectedText, Bounds=bounds, TargetWindow=hwnd, SourceElement=element, SourceIsEditable=IsEditable(element), Context=CollectNearbyContext(element,hwnd,selectedText,bounds) };
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

        private sealed class NearbyText
        {
            public string Text;
            public Rect Bounds;
        }

        private static IReadOnlyList<ConversationTurn> CollectNearbyContext(AutomationElement element,IntPtr hwnd,string selectedText,Rect selectedBounds)
        {
            if(!ContextCapture.Enabled||element==null||hwnd==IntPtr.Zero)return Array.Empty<ConversationTurn>();
            try
            {
                AutomationElement root=element;
                for(int i=0;i<5;i++)
                {
                    AutomationElement parent=TreeWalker.ControlViewWalker.GetParent(root);
                    if(parent==null)break;
                    root=parent;
                    int nativeHandle=(int)root.GetCurrentPropertyValue(AutomationElement.NativeWindowHandleProperty,true);
                    if(nativeHandle!=0&&new IntPtr(nativeHandle)==hwnd)break;
                }

                var condition=new OrCondition(
                    new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Text),
                    new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.ListItem));
                AutomationElementCollection nodes=root.FindAll(TreeScope.Descendants,condition);
                var nearby=new List<NearbyText>();
                int limit=Math.Min(nodes.Count,120);
                for(int i=0;i<limit;i++)
                {
                    AutomationElement node=nodes[i];
                    string text=ReadElementText(node);
                    if(String.IsNullOrWhiteSpace(text))continue;
                    text=text.Trim();
                    if(text.Length>500||Normalize(text)==Normalize(selectedText))continue;
                    Rect bounds=(Rect)node.GetCurrentPropertyValue(AutomationElement.BoundingRectangleProperty,true);
                    if(bounds.IsEmpty||bounds.Width<2||bounds.Height<2)continue;
                    double anchorY=selectedBounds.IsEmpty?bounds.Top:selectedBounds.Top;
                    if(Math.Abs(bounds.Top-anchorY)>900)continue;
                    if(nearby.Exists(item=>Normalize(item.Text)==Normalize(text)))continue;
                    nearby.Add(new NearbyText{Text=text,Bounds=bounds});
                }

                nearby.Sort((a,b)=>a.Bounds.Top.CompareTo(b.Bounds.Top));
                int selectedIndex=nearby.FindIndex(item=>item.Bounds.Top>=selectedBounds.Top);
                if(selectedIndex<0)selectedIndex=nearby.Count;
                int start=Math.Max(0,selectedIndex-4);
                var chosen=nearby.Skip(start).Take(5).ToList();
                NativeMethods.RECT windowRect;
                double center=NativeMethods.GetWindowRect(hwnd,out windowRect)?(windowRect.Left+windowRect.Right)/2.0:0;
                return chosen.Select(item=>new ConversationTurn{Role=center>0&&item.Bounds.Left+item.Bounds.Width/2>center?"user":"other",Text=item.Text}).ToArray();
            }
            catch{return Array.Empty<ConversationTurn>();}
        }

        private static string ReadElementText(AutomationElement element)
        {
            try
            {
                object raw;
                if(element.TryGetCurrentPattern(TextPattern.Pattern,out raw))
                {
                    string value=((TextPattern)raw).DocumentRange.GetText(500);
                    if(!String.IsNullOrWhiteSpace(value))return value;
                }
                return element.Current.Name;
            }
            catch{return String.Empty;}
        }

        private static string Normalize(string value)
        {
            return new string((value??String.Empty).Where(character=>!Char.IsWhiteSpace(character)&&!Char.IsPunctuation(character)).ToArray()).ToLowerInvariant();
        }

    }
}

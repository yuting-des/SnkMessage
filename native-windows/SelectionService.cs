using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
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
        public string ContextDiagnostic;
        public string ContextSource;
        public string ConversationLabel;
        public string OcrImageBase64;
        public int OcrImageWidth;
        public int OcrImageHeight;
        public int OcrContentTop;
        public Rect OcrBounds;
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
            if(!IsFromTargetProcess(element,hwnd))element=null;
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
            if(!IsFromTargetProcess(element,hwnd))element=null;
            string selectedText=text.Trim();
            string diagnostic;
            IReadOnlyList<ConversationTurn> nearby=CollectNearbyContext(element,hwnd,selectedText,new Rect(x,y,1,1),out diagnostic);
            var result=new SelectionContext { Text=selectedText, TargetWindow=hwnd, SourceElement=element, SourceIsEditable=IsEditable(element), Bounds=new Rect(x,y,1,1), Context=nearby, ContextDiagnostic=diagnostic, ContextSource=nearby.Count>0?"uia":null };
            AttachOcrFallback(result);return result;
        }

        private static SelectionContext TryAutomationSelection(AutomationElement start, IntPtr hwnd)
        {
            var candidates = new List<AutomationElement>();
            if (IsFromTargetProcess(start,hwnd)) candidates.Add(start);
            try
            {
                AutomationElement focused=AutomationElement.FocusedElement;
                if(IsFromTargetProcess(focused,hwnd))candidates.Add(focused);
            }
            catch { }
            AutomationElement current = start;
            for (int i = 0; i < 4 && current != null; i++)
            {
                try
                {
                    current=TreeWalker.ControlViewWalker.GetParent(current);
                    if(!IsFromTargetProcess(current,hwnd))break;
                    candidates.Add(current);
                }
                catch { break; }
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
                    string diagnostic;
                    IReadOnlyList<ConversationTurn> nearby=CollectNearbyContext(element,hwnd,selectedText,bounds,out diagnostic);
                    var result=new SelectionContext { Text=selectedText, Bounds=bounds, TargetWindow=hwnd, SourceElement=element, SourceIsEditable=IsEditable(element), Context=nearby, ContextDiagnostic=diagnostic, ContextSource=nearby.Count>0?"uia":null };
                    AttachOcrFallback(result);return result;
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

        private static IReadOnlyList<ConversationTurn> CollectNearbyContext(AutomationElement element,IntPtr hwnd,string selectedText,Rect selectedBounds,out string diagnostic)
        {
            diagnostic=String.Empty;
            if(!ContextCapture.Enabled){diagnostic="上下文功能已关闭";return Array.Empty<ConversationTurn>();}
            if(!IsFromTargetProcess(element,hwnd)||hwnd==IntPtr.Zero){diagnostic="目标微信控件未暴露 UI Automation";return Array.Empty<ConversationTurn>();}
            try
            {
                AutomationElement windowRoot=AutomationElement.FromHandle(hwnd);
                AutomationElement root=FindChatMessageList(windowRoot,hwnd);
                bool messageListFound=root!=null;
                if(root==null)
                {
                    root=element;
                    for(int i=0;i<8;i++)
                    {
                        AutomationElement parent=TreeWalker.ControlViewWalker.GetParent(root);
                        if(!IsFromTargetProcess(parent,hwnd))break;
                        root=parent;
                        int nativeHandle=(int)root.GetCurrentPropertyValue(AutomationElement.NativeWindowHandleProperty,true);
                        if(nativeHandle!=0&&new IntPtr(nativeHandle)==hwnd)break;
                    }
                }

                AutomationElementCollection nodes=root.FindAll(TreeScope.Descendants,System.Windows.Automation.Condition.TrueCondition);
                var nearby=new List<NearbyText>();
                int limit=Math.Min(nodes.Count,2000);
                for(int i=0;i<limit;i++)
                {
                    AutomationElement node=nodes[i];
                    if(!IsFromTargetProcess(node,hwnd))continue;
                    ControlType controlType=node.GetCurrentPropertyValue(AutomationElement.ControlTypeProperty,true) as ControlType;
                    bool readable=controlType==ControlType.Text||controlType==ControlType.ListItem||controlType==ControlType.DataItem||controlType==ControlType.Custom;
                    if(!readable)continue;
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
                ConversationTurn[] result=chosen.Select(item=>new ConversationTurn{Role=center>0&&item.Bounds.Left+item.Bounds.Width/2>center?"user":"other",Text=item.Text}).ToArray();
                diagnostic=result.Length>0?"已从微信消息列表读取":messageListFound?"已找到消息列表，但没有可读文本":"未找到新版微信消息列表锚点";
                return result;
            }
            catch{diagnostic="微信 UI Automation 读取异常";return Array.Empty<ConversationTurn>();}
        }

        private static AutomationElement FindChatMessageList(AutomationElement windowRoot,IntPtr hwnd)
        {
            if(!IsFromTargetProcess(windowRoot,hwnd))return null;
            try
            {
                AutomationElementCollection nodes=windowRoot.FindAll(TreeScope.Descendants,System.Windows.Automation.Condition.TrueCondition);
                AutomationElement best=null;double bestArea=0;
                int limit=Math.Min(nodes.Count,2500);
                for(int i=0;i<limit;i++)
                {
                    AutomationElement node=nodes[i];
                    if(!IsFromTargetProcess(node,hwnd))continue;
                    string automationId=PropertyString(node,AutomationElement.AutomationIdProperty);
                    string className=PropertyString(node,AutomationElement.ClassNameProperty);
                    bool matches=automationId.IndexOf("chat_message_list",StringComparison.OrdinalIgnoreCase)>=0||className.IndexOf("ChatMessage",StringComparison.OrdinalIgnoreCase)>=0;
                    if(!matches)continue;
                    Rect bounds=(Rect)node.GetCurrentPropertyValue(AutomationElement.BoundingRectangleProperty,true);
                    double area=bounds.IsEmpty?0:bounds.Width*bounds.Height;
                    if(area>bestArea){best=node;bestArea=area;}
                }
                return best;
            }
            catch{return null;}
        }

        private static string PropertyString(AutomationElement element,AutomationProperty property)
        {
            try
            {
                object value=element.GetCurrentPropertyValue(property,true);
                return value==null||value==AutomationElement.NotSupported?String.Empty:value.ToString();
            }
            catch{return String.Empty;}
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

        private static void AttachOcrFallback(SelectionContext context)
        {
            if(!ContextCapture.Enabled||context==null||context.Context.Count>0||context.TargetWindow==IntPtr.Zero)return;
            try
            {
                NativeMethods.RECT window;
                if(!NativeMethods.GetWindowRect(context.TargetWindow,out window))return;
                int windowWidth=window.Right-window.Left,windowHeight=window.Bottom-window.Top;
                if(windowWidth<320||windowHeight<240)return;
                uint dpi=96;try{uint actual=NativeMethods.GetDpiForWindow(context.TargetWindow);if(actual>0)dpi=actual;}catch{}
                double scale=dpi/96.0;
                int navigationWidth=Math.Min(windowWidth-260,(int)Math.Round(315*scale));
                int left=window.Left+Math.Max(260,navigationWidth);
                int top=window.Top+(int)Math.Round(30*scale);
                int contentTop=(int)Math.Round(62*scale);
                double selectedY=context.Bounds.IsEmpty?window.Bottom-180:context.Bounds.Top;
                int bottom=Math.Min(window.Bottom-110,(int)selectedY+120);
                if(bottom-top<180)bottom=Math.Min(window.Bottom-80,top+Math.Min(700,windowHeight-130));
                int width=Math.Min(1600,window.Right-left-12),height=Math.Min(1050,bottom-top);
                if(width<200||height<160)return;
                using var bitmap=new System.Drawing.Bitmap(width,height,System.Drawing.Imaging.PixelFormat.Format24bppRgb);
                using(var graphics=System.Drawing.Graphics.FromImage(bitmap))graphics.CopyFromScreen(left,top,0,0,new System.Drawing.Size(width,height),System.Drawing.CopyPixelOperation.SourceCopy);
                using var stream=new MemoryStream();
                bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Png);
                context.OcrImageBase64=Convert.ToBase64String(stream.ToArray());
                context.OcrImageWidth=width;context.OcrImageHeight=height;
                context.OcrContentTop=contentTop;context.OcrBounds=new Rect(left,top,width,height);
                context.ContextDiagnostic="新版微信未提供消息结构，将使用本地 OCR";
            }
            catch { }
        }

        private static bool IsFromTargetProcess(AutomationElement element,IntPtr hwnd)
        {
            if(element==null||hwnd==IntPtr.Zero)return false;
            try
            {
                uint targetProcessId;
                NativeMethods.GetWindowThreadProcessId(hwnd,out targetProcessId);
                int elementProcessId=(int)element.GetCurrentPropertyValue(AutomationElement.ProcessIdProperty,true);
                return targetProcessId!=0&&elementProcessId>0&&targetProcessId==(uint)elementProcessId;
            }
            catch{return false;}
        }

    }
}

using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.IO;
using Forms = System.Windows.Forms;

namespace SnkMessage
{
    internal sealed class AppController : IDisposable
    {
        private readonly Application app;
        private readonly OverlayWindow overlay;
        private GlobalSelectionWatcher watcher;
        private ClipboardWatcher clipboardWatcher;
        private Forms.NotifyIcon tray;
        private bool capturing;

        public AppController(Application application)
        {
            app=application;overlay=new OverlayWindow();overlay.SuggestionChosen+=OnSuggestionChosen;
        }

        public void Start()
        {
            tray=new Forms.NotifyIcon{Icon=SystemIcons.Application,Text="SnkMessage",Visible=true};
            var menu=new Forms.ContextMenuStrip();
            menu.Items.Add("显示使用说明",null,delegate{tray.ShowBalloonTip(4500,"SnkMessage","在微信中拖动选中文字；若该区域无法直接读取，按 Ctrl+C 后 AI Bar 会出现。",Forms.ToolTipIcon.Info);});
            menu.Items.Add("退出",null,delegate{app.Shutdown();});tray.ContextMenuStrip=menu;
            tray.ShowBalloonTip(3500,"SnkMessage 已启动","选中文字即可唤起；无法直接读取的微信消息请按 Ctrl+C。",Forms.ToolTipIcon.Info);
            watcher=new GlobalSelectionWatcher();watcher.MouseCompleted+=OnMouseCompleted;
            clipboardWatcher=new ClipboardWatcher(OnTextCopied);
        }

        public void RestoreTray()
        {
            if(tray==null)return;
            tray.Visible=false;tray.Visible=true;
            if(Environment.GetEnvironmentVariable("SNKMESSAGE_DIAGNOSTICS")=="1")
                File.WriteAllText(Path.Combine(Path.GetTempPath(),"SnkMessage.reactivated"),DateTime.UtcNow.ToString("O"));
            tray.ShowBalloonTip(3000,"SnkMessage 已在运行","托盘图标已恢复。选中微信文字即可使用 AI Bar。",Forms.ToolTipIcon.Info);
        }

        private void OnTextCopied(string text,IntPtr hwnd,int x,int y)
        {
            if(Environment.GetEnvironmentVariable("SNKMESSAGE_DIAGNOSTICS")=="1")
                File.WriteAllText(Path.Combine(Path.GetTempPath(),"SnkMessage.copied"),DateTime.UtcNow.ToString("O"));
            if(capturing||!AppRestrictions.IsAllowedWindow(hwnd))return;
            var selected=SelectionService.FromCopiedText(text,hwnd,x,y);
            if(selected!=null)overlay.ShowFor(selected,x,y);
        }

        private void OnMouseCompleted(int x,int y,bool dragged)
        {
            app.Dispatcher.BeginInvoke(new Action(async delegate
            {
                if(!dragged){overlay.HandleGlobalClick(x,y);return;}
                if(capturing)return;capturing=true;
                try
                {
                    var point=new NativeMethods.POINT{X=x,Y=y};
                    var target=NativeMethods.GetAncestor(NativeMethods.WindowFromPoint(point),NativeMethods.GA_ROOT);
                    if(!AppRestrictions.IsAllowedWindow(target))return;
                    var selected=await SelectionService.CaptureAsync(x,y);
                    if(selected==null)return;
                    double left=selected.Bounds.IsEmpty?x:selected.Bounds.Left;
                    double top=selected.Bounds.IsEmpty?y:selected.Bounds.Top;
                    overlay.ShowFor(selected,left,top);
                }
                finally{capturing=false;}
            }));
        }

        private async void OnSuggestionChosen(string text,SelectionContext context,AiMode selectedMode)
        {
            bool inserted=await InsertSuggestion(text,context,selectedMode);
            if(!inserted)
            {
                ClipboardActivity.Suppress(350);
                try{Clipboard.SetText(text);}catch{}
                tray.ShowBalloonTip(3000,"建议已复制","未能自动定位输入框，请在微信输入框中粘贴。",Forms.ToolTipIcon.Info);
            }
        }

        private static async Task<bool> InsertSuggestion(string text,SelectionContext context,AiMode selectedMode)
        {
            try
            {
                if(context.SourceIsEditable && context.SourceElement!=null)
                {
                    context.SourceElement.SetFocus();await Task.Delay(60);return NativeMethods.SendUnicodeText(text);
                }
                var root=AutomationElement.FromHandle(context.TargetWindow);
                var edits=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Edit));
                AutomationElement best=null;double bestY=Double.MinValue;
                foreach(AutomationElement edit in edits)
                {
                    try
                    {
                        if(!(bool)edit.GetCurrentPropertyValue(AutomationElement.IsEnabledProperty))continue;
                        var rect=edit.Current.BoundingRectangle;if(rect.Bottom>bestY){best=edit;bestY=rect.Bottom;}
                    }catch{}
                }
                if(best==null)return await PasteIntoWeChatComposer(text,context,selectedMode);
                object raw;
                if(best.TryGetCurrentPattern(ValuePattern.Pattern,out raw)&&!((ValuePattern)raw).Current.IsReadOnly)
                {
                    best.SetFocus();((ValuePattern)raw).SetValue(text);return true;
                }
                best.SetFocus();await Task.Delay(60);return NativeMethods.SendUnicodeText(text);
            }
            catch{return false;}
        }

        private static async Task<bool> PasteIntoWeChatComposer(string text,SelectionContext context,AiMode selectedMode)
        {
            try
            {
                NativeMethods.RECT rect;if(!NativeMethods.GetWindowRect(context.TargetWindow,out rect))return false;
                NativeMethods.SetForegroundWindow(context.TargetWindow);await Task.Delay(80);
                bool selectionIsInComposer=context.Bounds.Top>rect.Top+(rect.Bottom-rect.Top)*0.62;
                if(selectedMode!=AiMode.Polish||!selectionIsInComposer)
                {
                    int x=rect.Left+(rect.Right-rect.Left)*2/3;
                    int y=rect.Bottom-Math.Max(70,(rect.Bottom-rect.Top)/9);
                    NativeMethods.ClickAt(x,y);await Task.Delay(100);
                }
                return NativeMethods.SendUnicodeText(text);
            }
            catch{return false;}
        }

        public void Dispose()
        {
            if(watcher!=null)watcher.Dispose();
            if(clipboardWatcher!=null)clipboardWatcher.Dispose();
            overlay.Close();
            if(tray!=null){tray.Visible=false;tray.Dispose();}
        }
    }
}

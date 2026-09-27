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
            menu.Items.Add("显示使用说明",null,delegate{tray.ShowBalloonTip(3500,"SnkMessage","在微信或其他应用中拖动选中文字，AI Bar 会出现在选区附近。",Forms.ToolTipIcon.Info);});
            menu.Items.Add("退出",null,delegate{app.Shutdown();});tray.ContextMenuStrip=menu;
            tray.ShowBalloonTip(2500,"SnkMessage 已启动","拖动选中文字即可唤起 AI Bar。",Forms.ToolTipIcon.Info);
            watcher=new GlobalSelectionWatcher();watcher.MouseCompleted+=OnMouseCompleted;
            clipboardWatcher=new ClipboardWatcher(OnTextCopied);
        }

        private void OnTextCopied(string text,IntPtr hwnd,int x,int y)
        {
            if(Environment.GetEnvironmentVariable("SNKMESSAGE_DIAGNOSTICS")=="1")
                File.WriteAllText(Path.Combine(Path.GetTempPath(),"SnkMessage.copied"),DateTime.UtcNow.ToString("O"));
            if(capturing)return;
            var selected=SelectionService.FromCopiedText(text,hwnd,x,y);
            if(selected!=null)overlay.ShowFor(selected,x,y);
        }

        private void OnMouseCompleted(int x,int y,bool dragged)
        {
            app.Dispatcher.BeginInvoke(new Action(async delegate
            {
                if(!dragged){if(overlay.IsVisible&&!overlay.IsMouseOver)overlay.Hide();return;}
                if(capturing)return;capturing=true;
                try
                {
                    var selected=await SelectionService.CaptureAsync(x,y,false);
                    if(selected==null)return;
                    double left=selected.Bounds.IsEmpty?x:selected.Bounds.Left;
                    double top=selected.Bounds.IsEmpty?y:selected.Bounds.Top;
                    overlay.ShowFor(selected,left,top);
                }
                finally{capturing=false;}
            }));
        }

        private async void OnSuggestionChosen(string text,SelectionContext context)
        {
            bool inserted=await InsertSuggestion(text,context);
            if(!inserted)
            {
                ClipboardActivity.Suppress(700);
                try{Clipboard.SetText(text);}catch{}
                tray.ShowBalloonTip(3000,"建议已复制","未能自动定位输入框，请在微信输入框中粘贴。",Forms.ToolTipIcon.Info);
            }
        }

        private static async Task<bool> InsertSuggestion(string text,SelectionContext context)
        {
            try
            {
                if(context.SourceIsEditable && context.SourceElement!=null)
                {
                    context.SourceElement.SetFocus();return await PasteText(text,context.TargetWindow);
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
                if(best==null)return false;
                object raw;
                if(best.TryGetCurrentPattern(ValuePattern.Pattern,out raw)&&!((ValuePattern)raw).Current.IsReadOnly)
                {
                    best.SetFocus();((ValuePattern)raw).SetValue(text);return true;
                }
                best.SetFocus();return await PasteText(text,context.TargetWindow);
            }
            catch{return false;}
        }

        private static async Task<bool> PasteText(string text,IntPtr target)
        {
            System.Windows.IDataObject old=null;
            try{ClipboardActivity.Suppress(900);old=Clipboard.GetDataObject();Clipboard.SetText(text);NativeMethods.SetForegroundWindow(target);await Task.Delay(80);NativeMethods.SendShortcut(NativeMethods.VK_V);await Task.Delay(120);if(old!=null)Clipboard.SetDataObject(old,true);return true;}
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

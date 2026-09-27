using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace SnkMessage
{
    internal enum AiMode { Interpret, Reply, Polish }

    internal sealed class OverlayWindow : Window
    {
        private readonly Border shell;
        private SelectionContext context;
        private AiMode mode = AiMode.Interpret;
        public event Action<string, SelectionContext> SuggestionChosen;

        public OverlayWindow()
        {
            Width=88; Height=34; WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize;
            AllowsTransparency=true; Background=Brushes.Transparent; Topmost=true; ShowInTaskbar=false;
            ShowActivated=false;
            shell = new Border {
                CornerRadius=new CornerRadius(10), BorderThickness=new Thickness(1), BorderBrush=Brush("#C4C0FD"),
                Background=Brush("#F8F7FF"), Padding=new Thickness(5), Effect=new System.Windows.Media.Effects.DropShadowEffect {Color=Color.FromRgb(90,45,252),BlurRadius=15,Opacity=.28,ShadowDepth=2}
            };
            Content=shell;
            Deactivated += delegate { if (IsVisible && !IsMouseOver) Hide(); };
            PreviewKeyDown += delegate(object sender, KeyEventArgs e) { if(e.Key==Key.Escape) Hide(); };
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            var hwnd = new WindowInteropHelper(this).Handle;
            int style = NativeMethods.GetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE);
            NativeMethods.SetWindowLong(hwnd, NativeMethods.GWL_EXSTYLE, style | NativeMethods.WS_EX_TOOLWINDOW | NativeMethods.WS_EX_NOACTIVATE);
        }

        public void ShowFor(SelectionContext selection, double x, double y)
        {
            context=selection; ShowBar();
            Left=Math.Max(8, Math.Min(x, SystemParameters.VirtualScreenWidth-Width-8));
            Top=Math.Max(8, Math.Min(y-Height-8, SystemParameters.VirtualScreenHeight-Height-8));
            Show();
        }

        private void ShowBar()
        {
            Width = mode==AiMode.Interpret ? 88 : mode==AiMode.Reply ? 114 : 112; Height=34;
            var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(24)});
            var action=FlatButton("✦  "+ModeName(mode),13);action.Foreground=Brush("#5A2DFC");action.Click+=async delegate { await RunAsync(); };
            var menuButton=FlatButton("⌄",13);Grid.SetColumn(menuButton,1);menuButton.Click+=delegate { OpenModeMenu(menuButton); };
            grid.Children.Add(action);grid.Children.Add(menuButton);shell.Child=grid;
        }

        private void OpenModeMenu(Button owner)
        {
            var menu=new ContextMenu();
            AddMode(menu,"解读",AiMode.Interpret);AddMode(menu,"回复建议",AiMode.Reply);AddMode(menu,"表达优化",AiMode.Polish);
            menu.PlacementTarget=owner;menu.Placement=System.Windows.Controls.Primitives.PlacementMode.Top;menu.IsOpen=true;
        }

        private void AddMode(ContextMenu menu,string label,AiMode value)
        {
            var item=new MenuItem{Header=(mode==value?"✓  ":"    ")+label};
            item.Click+=delegate{mode=value;ShowBar();};menu.Items.Add(item);
        }

        private async Task RunAsync()
        {
            Width=mode==AiMode.Interpret?160:154;Height=36;
            shell.Child=new TextBlock{Text=mode==AiMode.Interpret?"◌  正在分析选中内容":mode==AiMode.Reply?"◌  正在生成回复建议":"◌  正在优化表达",Foreground=Brush("#5A2DFC"),FontSize=13,VerticalAlignment=VerticalAlignment.Center};
            await Task.Delay(650);
            ShowResult();
        }

        private void ShowResult()
        {
            Width=mode==AiMode.Interpret?286:300;Height=mode==AiMode.Interpret?106:190;
            var panel=new StackPanel();
            var header=new DockPanel{Margin=new Thickness(2,0,2,5)};
            var title=new TextBlock{Text="✦  "+ModeName(mode),FontSize=13,FontWeight=FontWeights.SemiBold};header.Children.Add(title);
            var retry=FlatButton("↻ 重新生成",12);retry.Foreground=Brush("#5A2DFC");DockPanel.SetDock(retry,Dock.Right);retry.Click+=async delegate{await RunAsync();};header.Children.Add(retry);panel.Children.Add(header);
            if(mode==AiMode.Interpret)
            {
                panel.Children.Add(ResultText(Interpret(context.Text)));
            }
            else
            {
                foreach(string suggestion in Suggestions(context.Text,mode))
                {
                    var button=FlatButton(suggestion,12);button.HorizontalContentAlignment=HorizontalAlignment.Left;button.Padding=new Thickness(8);button.Margin=new Thickness(0,0,0,4);button.Background=Brush("#FDFDFF");
                    button.Click+=delegate { var h=SuggestionChosen;if(h!=null)h(suggestion,context);Hide(); };panel.Children.Add(button);
                }
            }
            shell.Child=panel;
        }

        private static string Interpret(string text)
        {
            if(text.Contains("问题")||text.Contains("考虑"))return "对方可能希望你重新评估，但没有明确指出具体问题。";
            if(text.Contains("谢谢")||text.Contains("收到"))return "看起来是在确认信息并表达礼貌回应。";
            return "这段话的具体含义可能需要结合前后文进一步确认。";
        }

        private static IEnumerable<string> Suggestions(string text,AiMode selectedMode)
        {
            if(selectedMode==AiMode.Polish)
            {
                yield return "我想先进一步了解具体情况，再给出明确回复。";
                yield return "方便再说明一下具体问题吗？我会据此调整。";
                yield return "我的理解是还需要进一步确认，您看是否准确？";
            }
            else
            {
                yield return "您觉得具体是哪部分需要调整？";
                yield return "好的，我再梳理一下，稍后和您确认。";
                yield return "我们方便一起过一下具体问题吗？";
            }
        }

        private static TextBlock ResultText(string text){return new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,FontSize=12,LineHeight=19,Padding=new Thickness(8),Background=Brush("#FDFDFF")};}
        private static Button FlatButton(string text,double size){return new Button{Content=text,FontSize=size,Background=Brush("#00FFFFFF"),BorderThickness=new Thickness(0),Cursor=Cursors.Hand,Padding=new Thickness(4)};}
        private static string ModeName(AiMode value){return value==AiMode.Interpret?"解读":value==AiMode.Reply?"回复建议":"表达优化";}
        private static Brush Brush(string hex){return new BrushConverter().ConvertFromString(hex) as Brush;}
    }
}

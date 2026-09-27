using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Data;
using System.IO;

namespace SnkMessage
{
    internal enum AiMode { Interpret, Reply, Polish }

    internal sealed class OverlayWindow : Window
    {
        private readonly Border shell;
        private readonly WindowHighlight highlight;
        private SelectionContext context;
        private AiMode mode = AiMode.Interpret;
        public event Action<string, SelectionContext> SuggestionChosen;

        public OverlayWindow()
        {
            Width=84; Height=30; WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize; Title="SnkMessage AI";
            AllowsTransparency=true; Background=Brushes.Transparent; Topmost=true; ShowInTaskbar=false;
            ShowActivated=false;
            shell = new Border {
                CornerRadius=new CornerRadius(10), BorderThickness=new Thickness(1), BorderBrush=Brush("#C4C0FD"),
                Background=Brush("#EEFBFBFF"), Padding=new Thickness(4), Effect=new System.Windows.Media.Effects.DropShadowEffect {Color=Color.FromRgb(85,39,253),BlurRadius=14,Opacity=.30,ShadowDepth=2}
            };
            Content=shell;
            highlight=new WindowHighlight();
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
            if(!IsVisible){Opacity=0;Show();}
            var logical=DeviceToLogical(new Point(x,y));
            Left=Math.Max(SystemParameters.VirtualScreenLeft+8,Math.Min(logical.X+10,SystemParameters.VirtualScreenLeft+SystemParameters.VirtualScreenWidth-Width-8));
            double above=logical.Y-Height-10;
            Top=above<SystemParameters.VirtualScreenTop+8?logical.Y+16:above;
            Top=Math.Max(SystemParameters.VirtualScreenTop+8,Math.Min(Top,SystemParameters.VirtualScreenTop+SystemParameters.VirtualScreenHeight-Height-8));
            Opacity=1;
            if(Environment.GetEnvironmentVariable("SNKMESSAGE_DIAGNOSTICS")=="1")
                File.WriteAllText(Path.Combine(Path.GetTempPath(),"SnkMessage.overlay"),DateTime.UtcNow.ToString("O"));
        }

        private void ShowBar()
        {
            highlight.Hide();
            shell.Background=Brush("#EEFBFBFF");
            Width = mode==AiMode.Interpret ? 84 : mode==AiMode.Reply ? 110 : 108; Height=30;
            var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(24)});
            var action=FlatButton("✦  "+ModeName(mode),13);action.Foreground=Brush("#5A2DFC");action.Click+=async delegate { await RunAsync(); };
            var menuButton=FlatButton("⌄",12);menuButton.Foreground=Brush("#5A2DFC");Grid.SetColumn(menuButton,1);menuButton.Click+=delegate { OpenModeMenu(menuButton); };
            grid.Children.Add(action);grid.Children.Add(menuButton);shell.Child=grid;
        }

        private void OpenModeMenu(Button owner)
        {
            var menu=new ContextMenu{Width=144,Padding=new Thickness(4),Background=Brush("#F8F6FF"),BorderBrush=Brush("#C4C0FD"),BorderThickness=new Thickness(1)};
            AddMode(menu,"解读",AiMode.Interpret);AddMode(menu,"回复建议",AiMode.Reply);AddMode(menu,"表达优化",AiMode.Polish);
            menu.PlacementTarget=owner;menu.Placement=System.Windows.Controls.Primitives.PlacementMode.Top;menu.IsOpen=true;
        }

        private void AddMode(ContextMenu menu,string label,AiMode value)
        {
            var item=new MenuItem{Header=(mode==value?"✓  ":"    ")+label,Padding=new Thickness(8,6,8,6),FontSize=12};
            item.MouseEnter+=delegate{item.Background=Brush("#E9E2FF");};
            item.MouseLeave+=delegate{item.Background=Brush("#00FFFFFF");};
            item.PreviewMouseLeftButtonDown+=delegate{item.Background=Brush("#E1D9FF");};
            item.Click+=async delegate{mode=value;ShowBar();await RunAsync();};menu.Items.Add(item);
        }

        private async Task RunAsync()
        {
            highlight.ShowAround(context.TargetWindow,DeviceToLogical);
            Width=mode==AiMode.Interpret?174:166;Height=34;
            shell.Child=new TextBlock{Text=mode==AiMode.Interpret?"◌  正在分析当前聊天":mode==AiMode.Reply?"◌  正在生成回复建议":"◌  正在优化表达",Foreground=Brush("#5A2DFC"),FontSize=13,VerticalAlignment=VerticalAlignment.Center,Padding=new Thickness(6,0,6,0)};
            await Task.Delay(650);
            highlight.Hide();
            ShowResult();
        }

        private void ShowResult()
        {
            Width=mode==AiMode.Interpret?267:280;Height=mode==AiMode.Interpret?84:170;
            shell.Background=new LinearGradientBrush(Color.FromArgb(238,247,240,254),Color.FromArgb(238,230,229,253),0);
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
                    button.BorderThickness=new Thickness(1);button.BorderBrush=Brush("#00FFFFFF");
                    ApplyInteractionColors(button,"#E9E2FF","#E1D9FF","#FDFDFF",true);
                    button.Click+=delegate { var h=SuggestionChosen;if(h!=null)h(suggestion,context);Hide(); };panel.Children.Add(button);
                }
            }
            shell.Child=panel;
        }

        public new void Hide(){highlight.Hide();base.Hide();}

        private Point DeviceToLogical(Point point)
        {
            var source=PresentationSource.FromVisual(this);
            if(source!=null && source.CompositionTarget!=null)return source.CompositionTarget.TransformFromDevice.Transform(point);
            return point;
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

        private static TextBlock ResultText(string text){return new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,FontSize=12,LineHeight=17,Padding=new Thickness(6),Background=Brush("#CCFFFFFF")};}
        private static Button FlatButton(string text,double size)
        {
            var button=new Button{Content=text,FontSize=size,Background=Brush("#00FFFFFF"),BorderThickness=new Thickness(0),Cursor=Cursors.Hand,Padding=new Thickness(4),Template=ButtonTemplate()};
            ApplyInteractionColors(button,"#E9E2FF","#E1D9FF","#00FFFFFF",false);return button;
        }
        private static ControlTemplate ButtonTemplate()
        {
            var border=new FrameworkElementFactory(typeof(Border));
            border.SetBinding(Border.BackgroundProperty,new Binding("Background"){RelativeSource=RelativeSource.TemplatedParent});
            border.SetBinding(Border.BorderBrushProperty,new Binding("BorderBrush"){RelativeSource=RelativeSource.TemplatedParent});
            border.SetBinding(Border.BorderThicknessProperty,new Binding("BorderThickness"){RelativeSource=RelativeSource.TemplatedParent});
            border.SetValue(Border.CornerRadiusProperty,new CornerRadius(6));
            var content=new FrameworkElementFactory(typeof(ContentPresenter));
            content.SetBinding(ContentPresenter.ContentProperty,new Binding("Content"){RelativeSource=RelativeSource.TemplatedParent});
            content.SetBinding(ContentPresenter.MarginProperty,new Binding("Padding"){RelativeSource=RelativeSource.TemplatedParent});
            content.SetBinding(ContentPresenter.HorizontalAlignmentProperty,new Binding("HorizontalContentAlignment"){RelativeSource=RelativeSource.TemplatedParent});
            content.SetBinding(ContentPresenter.VerticalAlignmentProperty,new Binding("VerticalContentAlignment"){RelativeSource=RelativeSource.TemplatedParent});
            border.AppendChild(content);return new ControlTemplate(typeof(Button)){VisualTree=border};
        }
        private static void ApplyInteractionColors(Button button,string hover,string pressed,string normal,bool showBorder)
        {
            bool down=false;
            button.MouseEnter+=delegate{if(!down)button.Background=Brush(hover);if(showBorder)button.BorderBrush=Brush("#C4C0FD");};
            button.MouseLeave+=delegate{down=false;button.Background=Brush(normal);if(showBorder)button.BorderBrush=Brush("#00FFFFFF");};
            button.PreviewMouseLeftButtonDown+=delegate{down=true;button.Background=Brush(pressed);};
            button.PreviewMouseLeftButtonUp+=delegate{down=false;button.Background=button.IsMouseOver?Brush(hover):Brush(normal);};
        }
        private static string ModeName(AiMode value){return value==AiMode.Interpret?"解读":value==AiMode.Reply?"回复建议":"表达优化";}
        private static Brush Brush(string hex){return new BrushConverter().ConvertFromString(hex) as Brush;}
    }

    internal sealed class WindowHighlight : Window
    {
        public WindowHighlight()
        {
            WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;
            Topmost=true;ShowInTaskbar=false;ShowActivated=false;IsHitTestVisible=false;
            Content=new Border{BorderBrush=new SolidColorBrush(Color.FromRgb(196,192,253)),BorderThickness=new Thickness(2),CornerRadius=new CornerRadius(9),Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=Color.FromRgb(90,45,252),BlurRadius=18,Opacity=.45,ShadowDepth=0}};
        }
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);var hwnd=new WindowInteropHelper(this).Handle;int style=NativeMethods.GetWindowLong(hwnd,NativeMethods.GWL_EXSTYLE);NativeMethods.SetWindowLong(hwnd,NativeMethods.GWL_EXSTYLE,style|NativeMethods.WS_EX_TOOLWINDOW|NativeMethods.WS_EX_NOACTIVATE);
        }
        public void ShowAround(IntPtr hwnd,Func<Point,Point> convert)
        {
            if(hwnd==IntPtr.Zero)return;NativeMethods.RECT rect;if(!NativeMethods.GetWindowRect(hwnd,out rect))return;
            Point a=convert(new Point(rect.Left,rect.Top)),b=convert(new Point(rect.Right,rect.Bottom));
            Left=a.X;Top=a.Y;Width=Math.Max(1,b.X-a.X);Height=Math.Max(1,b.Y-a.Y);Show();
        }
    }
}

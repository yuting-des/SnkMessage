using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace SnkMessage
{
    internal enum AiMode { Interpret, Reply, Polish }

    internal sealed class OverlayWindow : Window
    {
        private readonly Border shell;
        private readonly WindowHighlight highlight;
        private Popup modePopup;
        private SelectionContext context;
        private AiMode mode=AiMode.Interpret;
        private int operationVersion;
        public event Action<string,SelectionContext,AiMode> SuggestionChosen;

        public OverlayWindow()
        {
            Width=84;Height=30;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;Title="SnkMessage AI";
            AllowsTransparency=true;Background=Brushes.Transparent;Topmost=true;ShowInTaskbar=false;ShowActivated=false;
            shell=new Border{CornerRadius=new CornerRadius(10),BorderThickness=new Thickness(1),BorderBrush=Brush("#C4C0FD"),Background=Brush("#EEFBFBFF"),Padding=new Thickness(4),Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=Color.FromRgb(85,39,253),BlurRadius=14,Opacity=.30,ShadowDepth=2}};
            Content=shell;highlight=new WindowHighlight();
            PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Escape)Hide();};
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);var hwnd=new WindowInteropHelper(this).Handle;int style=NativeMethods.GetWindowLong(hwnd,NativeMethods.GWL_EXSTYLE);NativeMethods.SetWindowLong(hwnd,NativeMethods.GWL_EXSTYLE,style|NativeMethods.WS_EX_TOOLWINDOW|NativeMethods.WS_EX_NOACTIVATE);
        }

        public void ShowFor(SelectionContext selection,double x,double y)
        {
            operationVersion++;context=selection;ShowBar();if(!IsVisible){Opacity=0;Show();}
            var logical=DeviceToLogical(new Point(x,y));Left=Math.Max(SystemParameters.VirtualScreenLeft+8,Math.Min(logical.X+10,SystemParameters.VirtualScreenLeft+SystemParameters.VirtualScreenWidth-Width-8));
            double above=logical.Y-Height-10;Top=above<SystemParameters.VirtualScreenTop+8?logical.Y+16:above;Top=Math.Max(SystemParameters.VirtualScreenTop+8,Math.Min(Top,SystemParameters.VirtualScreenTop+SystemParameters.VirtualScreenHeight-Height-8));Opacity=1;
            if(Environment.GetEnvironmentVariable("SNKMESSAGE_DIAGNOSTICS")=="1")System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(),"SnkMessage.overlay"),DateTime.UtcNow.ToString("O"));
        }

        public void HandleGlobalClick(int x,int y)
        {
            if(!IsVisible||IsInsideWindow(x,y)||IsInsidePopup(x,y))return;Hide();
        }
        private bool IsInsideWindow(int x,int y){var hwnd=new WindowInteropHelper(this).Handle;NativeMethods.RECT r;return hwnd!=IntPtr.Zero&&NativeMethods.GetWindowRect(hwnd,out r)&&x>=r.Left&&x<=r.Right&&y>=r.Top&&y<=r.Bottom;}
        private bool IsInsidePopup(int x,int y)
        {
            if(modePopup==null||!modePopup.IsOpen||modePopup.Child==null)return false;
            try{Point a=modePopup.Child.PointToScreen(new Point(0,0));Point b=modePopup.Child.PointToScreen(new Point(modePopup.Child.RenderSize.Width,modePopup.Child.RenderSize.Height));return x>=a.X&&x<=b.X&&y>=a.Y&&y<=b.Y;}catch{return false;}
        }

        private void ShowBar()
        {
            CloseMenu();highlight.Hide();shell.Padding=new Thickness(1);shell.Background=Brush("#EEFBFBFF");Width=mode==AiMode.Interpret?84:mode==AiMode.Reply?110:108;Height=30;
            var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(24)});
            var action=FlatButton(IconLabel(SparkleIcon(20),ModeName(mode),13),13);action.Foreground=Brush("#5A2DFC");action.Click+=async delegate{await RunAsync();};
            var menuButton=FlatButton(DownIcon(),12);menuButton.Foreground=Brush("#5A2DFC");Grid.SetColumn(menuButton,1);menuButton.Click+=delegate{OpenModeMenu(menuButton);};grid.Children.Add(action);grid.Children.Add(menuButton);shell.Child=grid;
        }

        private void OpenModeMenu(Button owner)
        {
            CloseMenu();var panel=new StackPanel();AddMode(panel,"解读",AiMode.Interpret);AddMode(panel,"回复建议",AiMode.Reply);AddMode(panel,"表达优化",AiMode.Polish);
            var surface=new Border{Width=144,Padding=new Thickness(4),CornerRadius=new CornerRadius(8),Background=Brush("#FFF8F6FF"),BorderBrush=Brush("#C4C0FD"),BorderThickness=new Thickness(1),Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=Color.FromRgb(85,39,253),BlurRadius=14,Opacity=.25,ShadowDepth=2},Child=panel};
            bool below=Top<130;modePopup=new Popup{PlacementTarget=owner,Placement=below?PlacementMode.Bottom:PlacementMode.Top,VerticalOffset=below?6:-6,AllowsTransparency=true,StaysOpen=true,Child=surface};modePopup.IsOpen=true;
        }

        private void AddMode(Panel panel,string label,AiMode value)
        {
            var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(18)});row.Children.Add(new TextBlock{Text=label,FontSize=12,VerticalAlignment=VerticalAlignment.Center});
            if(mode==value){var check=new TextBlock{Text="✓",FontSize=12,Foreground=Brush("#5A2DFC"),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(check,1);row.Children.Add(check);}
            var item=FlatButton(row,12);item.Height=32;item.HorizontalContentAlignment=HorizontalAlignment.Stretch;item.Padding=new Thickness(8,0,6,0);item.Foreground=mode==value?Brush("#5A2DFC"):Brush("#433956");item.Margin=new Thickness(0,0,0,2);item.Click+=async delegate{CloseMenu();mode=value;ShowBar();await RunAsync();};panel.Children.Add(item);
        }

        private async Task RunAsync()
        {
            int version=++operationVersion;CloseMenu();highlight.ShowAround(context.TargetWindow,DeviceToLogical);Width=mode==AiMode.Interpret?174:166;Height=34;shell.Padding=new Thickness(6);
            shell.Child=IconLabel(LoadingIcon(),mode==AiMode.Interpret?"正在分析当前聊天":mode==AiMode.Reply?"正在生成回复建议":"正在优化表达",13);
            await Task.Delay(650);if(version!=operationVersion||!IsVisible)return;highlight.Hide();ShowResult();
        }

        private void ShowResult()
        {
            Width=mode==AiMode.Interpret?267:280;Height=mode==AiMode.Interpret?84:170;shell.Padding=new Thickness(6);shell.Background=new LinearGradientBrush(Color.FromArgb(238,247,240,254),Color.FromArgb(238,230,229,253),0);
            var panel=new StackPanel();var header=new Grid{Height=20,Margin=new Thickness(0,0,0,4)};header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            header.Children.Add(IconLabel(SparkleIcon(20),ModeName(mode),13));var retry=FlatButton(IconLabel(ReloadIcon(),"重新思考",12),12);retry.Foreground=Brush("#5A2DFC");retry.Padding=new Thickness(4,0,4,0);Grid.SetColumn(retry,1);retry.Click+=async delegate{await RunAsync();};header.Children.Add(retry);panel.Children.Add(header);
            if(mode==AiMode.Interpret)panel.Children.Add(ResultText(Interpret(context.Text)));
            else foreach(string suggestion in Suggestions(context.Text,mode))
            {
                var button=FlatButton(new TextBlock{Text=suggestion,TextWrapping=TextWrapping.Wrap,FontSize=12,LineHeight=17},12);button.Height=40;button.HorizontalContentAlignment=HorizontalAlignment.Left;button.Padding=new Thickness(6,2,6,2);button.Margin=new Thickness(0,0,0,4);button.Background=Brush("#CCFFFFFF");button.BorderThickness=new Thickness(1);button.BorderBrush=Brush("#00FFFFFF");ApplyInteractionColors(button,"#E9E2FF","#E1D9FF","#CCFFFFFF",true);
                button.Click+=delegate{var h=SuggestionChosen;var selectedMode=mode;Hide();if(h!=null)h(suggestion,context,selectedMode);};panel.Children.Add(button);
            }
            shell.Child=panel;
        }

        public new void Hide(){operationVersion++;CloseMenu();highlight.Hide();base.Hide();}
        private void CloseMenu(){if(modePopup!=null){modePopup.IsOpen=false;modePopup=null;}}
        private Point DeviceToLogical(Point point){var source=PresentationSource.FromVisual(this);return source!=null&&source.CompositionTarget!=null?source.CompositionTarget.TransformFromDevice.Transform(point):point;}

        private static Border ResultText(string text){return new Border{MinHeight=46,Padding=new Thickness(6),CornerRadius=new CornerRadius(6),Background=Brush("#CCFFFFFF"),Child=new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,FontSize=12,LineHeight=17}};}
        private static string Interpret(string text){if(text.Contains("问题")||text.Contains("考虑"))return "对方可能希望你重新评估，但没有明确指出具体问题。";if(text.Contains("谢谢")||text.Contains("收到"))return "看起来是在确认信息并表达礼貌回应。";return "这段话的具体含义可能需要结合前后文进一步确认。";}
        private static IEnumerable<string> Suggestions(string text,AiMode selectedMode)
        {
            if(selectedMode==AiMode.Polish){yield return "我想先进一步了解具体情况，再给出明确回复。";yield return "方便再说明一下具体问题吗？我会据此调整。";yield return "我的理解是还需要进一步确认，您看是否准确？";}
            else{yield return "您觉得具体是哪部分需要调整？";yield return "好的，我再梳理一下，稍后和您确认。";yield return "我们方便一起过一下具体问题吗？";}
        }

        private static StackPanel IconLabel(UIElement icon,string label,double size){var p=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};p.Children.Add(icon);p.Children.Add(new TextBlock{Text=label,FontSize=size,Margin=new Thickness(4,0,0,0),VerticalAlignment=VerticalAlignment.Center});return p;}
        private static Viewbox SparkleIcon(double size)
        {
            var canvas=new Canvas{Width=20,Height=20};var fill=Brush("#5A2DFC");canvas.Children.Add(new Path{Fill=fill,Data=Geometry.Parse("M5.86486,10.1982 L7.15314,6.33333 L7.94371,6.33333 L9.232,10.1982 L13.0969,11.4865 L13.0969,12.277 L9.232,13.5653 L7.94371,17.4302 L7.15314,17.4302 L5.86486,13.5653 L2,12.277 L2,11.4865 Z")});canvas.Children.Add(new Path{Fill=fill,Data=Geometry.Parse("M13.1565,4.98986 L13.8198,3 L14.6104,3 L15.2737,4.98986 L17.2635,5.65314 L17.2635,6.44371 L15.2737,7.107 L14.6104,9.09686 L13.8198,9.09686 L13.1565,7.107 L11.1667,6.44371 L11.1667,5.65314 Z")});return new Viewbox{Width=size,Height=size,Child=canvas};
        }
        private static Viewbox DownIcon(){var canvas=new Canvas{Width=16,Height=16};canvas.Children.Add(new Path{Stroke=Brush("#CC5A2DFC"),StrokeThickness=1.4,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Data=Geometry.Parse("M4,6 L8,10 L12,6")});return new Viewbox{Width=16,Height=16,Child=canvas};}
        private static Viewbox ReloadIcon(){var canvas=new Canvas{Width=12,Height=12};canvas.Children.Add(new Path{Stroke=Brush("#5A2DFC"),StrokeThickness=1.1,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Data=Geometry.Parse("M10,4 A4,4 0 1 0 10,8 M10,4 L10,1 M10,4 L7,4")});return new Viewbox{Width=12,Height=12,Child=canvas};}
        private static Grid LoadingIcon()
        {
            var grid=new Grid{Width=20,Height=20,RenderTransformOrigin=new Point(.5,.5)};var canvas=new Canvas{Width=20,Height=20};for(int i=0;i<8;i++){double angle=i*Math.PI/4;var dot=new Ellipse{Width=3.2,Height=3.2,Fill=Brush("#5A2DFC"),Opacity=1-i*.1};Canvas.SetLeft(dot,8.4+6.5*Math.Sin(angle));Canvas.SetTop(dot,8.4-6.5*Math.Cos(angle));canvas.Children.Add(dot);}grid.Children.Add(canvas);var rotate=new RotateTransform();grid.RenderTransform=rotate;rotate.BeginAnimation(RotateTransform.AngleProperty,new DoubleAnimation(0,360,new Duration(TimeSpan.FromSeconds(1))){RepeatBehavior=RepeatBehavior.Forever});return grid;
        }

        private static Button FlatButton(object content,double size){var button=new Button{Content=content,FontSize=size,Background=Brush("#00FFFFFF"),BorderThickness=new Thickness(0),Cursor=Cursors.Hand,Padding=new Thickness(4,0,4,0),HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center,Template=ButtonTemplate()};ApplyInteractionColors(button,"#E9E2FF","#E1D9FF","#00FFFFFF",false);return button;}
        private static ControlTemplate ButtonTemplate()
        {
            var border=new FrameworkElementFactory(typeof(Border));border.SetBinding(Border.BackgroundProperty,new Binding("Background"){RelativeSource=RelativeSource.TemplatedParent});border.SetBinding(Border.BorderBrushProperty,new Binding("BorderBrush"){RelativeSource=RelativeSource.TemplatedParent});border.SetBinding(Border.BorderThicknessProperty,new Binding("BorderThickness"){RelativeSource=RelativeSource.TemplatedParent});border.SetValue(Border.CornerRadiusProperty,new CornerRadius(6));var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetBinding(ContentPresenter.ContentProperty,new Binding("Content"){RelativeSource=RelativeSource.TemplatedParent});content.SetBinding(ContentPresenter.MarginProperty,new Binding("Padding"){RelativeSource=RelativeSource.TemplatedParent});content.SetBinding(ContentPresenter.HorizontalAlignmentProperty,new Binding("HorizontalContentAlignment"){RelativeSource=RelativeSource.TemplatedParent});content.SetBinding(ContentPresenter.VerticalAlignmentProperty,new Binding("VerticalContentAlignment"){RelativeSource=RelativeSource.TemplatedParent});border.AppendChild(content);return new ControlTemplate(typeof(Button)){VisualTree=border};
        }
        private static void ApplyInteractionColors(Button button,string hover,string pressed,string normal,bool showBorder){bool down=false;button.MouseEnter+=delegate{if(!down)button.Background=Brush(hover);if(showBorder)button.BorderBrush=Brush("#C4C0FD");};button.MouseLeave+=delegate{down=false;button.Background=Brush(normal);if(showBorder)button.BorderBrush=Brush("#00FFFFFF");};button.PreviewMouseLeftButtonDown+=delegate{down=true;button.Background=Brush(pressed);};button.PreviewMouseLeftButtonUp+=delegate{down=false;button.Background=button.IsMouseOver?Brush(hover):Brush(normal);};}
        private static string ModeName(AiMode value){return value==AiMode.Interpret?"解读":value==AiMode.Reply?"回复建议":"表达优化";}
        private static Brush Brush(string hex){return new BrushConverter().ConvertFromString(hex) as Brush;}
    }

    internal sealed class WindowHighlight:Window
    {
        public WindowHighlight(){WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;Topmost=true;ShowInTaskbar=false;ShowActivated=false;IsHitTestVisible=false;Content=new Border{BorderBrush=new SolidColorBrush(Color.FromRgb(196,192,253)),BorderThickness=new Thickness(2),CornerRadius=new CornerRadius(9),Effect=new System.Windows.Media.Effects.DropShadowEffect{Color=Color.FromRgb(90,45,252),BlurRadius=18,Opacity=.45,ShadowDepth=0}};}
        protected override void OnSourceInitialized(EventArgs e){base.OnSourceInitialized(e);var hwnd=new WindowInteropHelper(this).Handle;int style=NativeMethods.GetWindowLong(hwnd,NativeMethods.GWL_EXSTYLE);NativeMethods.SetWindowLong(hwnd,NativeMethods.GWL_EXSTYLE,style|NativeMethods.WS_EX_TOOLWINDOW|NativeMethods.WS_EX_NOACTIVATE);}
        public void ShowAround(IntPtr hwnd,Func<Point,Point> convert){if(hwnd==IntPtr.Zero)return;NativeMethods.RECT rect;if(!NativeMethods.GetWindowRect(hwnd,out rect))return;Point a=convert(new Point(rect.Left,rect.Top)),b=convert(new Point(rect.Right,rect.Bottom));Left=a.X;Top=a.Y;Width=Math.Max(1,b.X-a.X);Height=Math.Max(1,b.Y-a.Y);Show();}
    }
}

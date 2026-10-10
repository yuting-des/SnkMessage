using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace SnkMessage
{
    internal sealed class ContextEditorWindow:Window
    {
        private sealed class Row
        {
            internal bool Included=true;
            internal ConversationTurn Turn;
            internal Button Toggle;
            internal Button Sentence;
        }

        private readonly SelectionContext context;
        private readonly List<Row> rows=new List<Row>();

        internal ContextEditorWindow(SelectionContext context)
        {
            this.context=context;
            Title="查看上下文";Width=380;MaxHeight=650;SizeToContent=SizeToContent.Height;
            WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;
            AllowsTransparency=true;Background=Brushes.Transparent;Topmost=true;ShowInTaskbar=false;FontFamily=new FontFamily("Segoe UI Variable Text");UseLayoutRounding=true;SnapsToDevicePixels=true;

            var root=new DockPanel{Margin=new Thickness(14)};
            var footer=new Grid{Margin=new Thickness(0,12,0,0)};
            footer.ColumnDefinitions.Add(new ColumnDefinition());footer.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(10)});footer.ColumnDefinitions.Add(new ColumnDefinition());
            DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            var cancel=RoundedButton("取消",Brush("#EEE8F8"),Brush("#4D435C"),12);cancel.Height=40;cancel.FontSize=12;cancel.FontWeight=FontWeights.SemiBold;cancel.Click+=delegate{DialogResult=false;};footer.Children.Add(cancel);
            var save=RoundedButton("应用并重新生成",Brush("#5A2DFC"),Brushes.White,12);save.Height=40;save.FontSize=12;save.FontWeight=FontWeights.SemiBold;Grid.SetColumn(save,2);save.Click+=delegate{Save();};footer.Children.Add(save);

            var content=new StackPanel();
            var title=new TextBlock{Text="查看上下文",FontSize=14,FontWeight=FontWeights.SemiBold,Foreground=Brush("#32283F"),LineHeight=22,Cursor=Cursors.SizeAll};title.MouseLeftButtonDown+=delegate{try{DragMove();}catch{}};content.Children.Add(title);
            string subject=String.IsNullOrWhiteSpace(context.ConversationLabel)?"未识别聊天对象":"聊天对象："+context.ConversationLabel;
            content.Children.Add(new TextBlock{Text=subject,FontSize=11,FontWeight=FontWeights.SemiBold,Foreground=Brush("#665B76"),Margin=new Thickness(0,3,0,0)});
            content.Children.Add(new TextBlock{Text="勾选本次要使用的消息，点击语句可以修改。",FontSize=11,Foreground=Brush("#81778E"),Margin=new Thickness(0,2,0,10)});
            foreach(ConversationTurn turn in context.Context??Array.Empty<ConversationTurn>())AddTurn(content,turn);
            if(rows.Count==0)
            {
                content.Children.Add(new Border{CornerRadius=new CornerRadius(10),Background=Brush("#CCFFFFFF"),Padding=new Thickness(12,18,12,18),Child=new TextBlock{Text="本次没有识别到附近消息。",FontSize=11,Foreground=Brush("#81798E"),HorizontalAlignment=HorizontalAlignment.Center}});
            }
            root.Children.Add(new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,MaxHeight=500});
            Content=new Border{CornerRadius=new CornerRadius(14),BorderThickness=new Thickness(1),BorderBrush=Brush("#C4C0FD"),Background=new LinearGradientBrush(Color.FromArgb(250,247,240,254),Color.FromArgb(250,230,229,253),0),Padding=new Thickness(2),ClipToBounds=true,Child=root};
        }

        private void AddTurn(Panel panel,ConversationTurn source)
        {
            var row=new Row{Turn=Clone(source)};
            var grid=new Grid{Margin=new Thickness(0,0,0,6)};
            grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(30)});grid.ColumnDefinitions.Add(new ColumnDefinition());
            row.Toggle=RoundedButton("✓",Brush("#5A2DFC"),Brushes.White,10);row.Toggle.Width=20;row.Toggle.Height=20;row.Toggle.Padding=new Thickness(0);row.Toggle.FontSize=11;row.Toggle.HorizontalAlignment=HorizontalAlignment.Left;row.Toggle.VerticalAlignment=VerticalAlignment.Center;
            row.Toggle.Click+=delegate{row.Included=!row.Included;UpdateRow(row);};grid.Children.Add(row.Toggle);
            row.Sentence=RoundedButton(String.Empty,Brush("#CCFFFFFF"),Brush("#433956"),10);row.Sentence.MinHeight=42;row.Sentence.Padding=new Thickness(10,8,10,8);row.Sentence.HorizontalContentAlignment=HorizontalAlignment.Left;row.Sentence.FontSize=12;row.Sentence.Cursor=Cursors.IBeam;
            row.Sentence.Click+=delegate{EditRow(row);};Grid.SetColumn(row.Sentence,1);grid.Children.Add(row.Sentence);
            rows.Add(row);UpdateRow(row);panel.Children.Add(grid);
        }

        private void UpdateRow(Row row)
        {
            row.Toggle.Content=row.Included?"✓":String.Empty;
            row.Toggle.Background=Brush(row.Included?"#5A2DFC":"#FFF8F6FF");
            row.Toggle.BorderBrush=Brush(row.Included?"#5A2DFC":"#BEB3D3");row.Toggle.BorderThickness=new Thickness(1.5);
            row.Sentence.Opacity=row.Included?1:.55;
            string who=row.Turn.Role=="user"?"我":String.IsNullOrWhiteSpace(row.Turn.Speaker)?"对方":"对方（"+row.Turn.Speaker.Trim()+"）";
            row.Sentence.Content=new TextBlock{Text=who+"："+(row.Turn.Text??String.Empty),TextWrapping=TextWrapping.Wrap,LineHeight=17,LineStackingStrategy=LineStackingStrategy.BlockLineHeight};
        }

        private void EditRow(Row row)
        {
            var editor=new TurnEditorWindow(row.Turn){Owner=this};
            if(editor.ShowDialog()==true)
            {
                row.Turn=editor.Result;
                UpdateRow(row);
            }
        }

        private void Save()
        {
            context.Context=rows.Where(row=>row.Included&&!String.IsNullOrWhiteSpace(row.Turn.Text)).Select(row=>Clone(row.Turn)).ToArray();
            context.ContextDiagnostic="已手动修正上下文";context.ContextSource="manual";context.OcrImageBase64=null;DialogResult=true;
        }

        private static ConversationTurn Clone(ConversationTurn turn){return new ConversationTurn{Role=turn.Role=="user"?"user":"other",Speaker=turn.Speaker??String.Empty,Text=turn.Text??String.Empty};}
        internal static Button RoundedButton(object content,Brush background,Brush foreground,double radius)
        {
            var button=new Button{Content=content,Background=background,Foreground=foreground,BorderThickness=new Thickness(0),Cursor=Cursors.Hand,HorizontalContentAlignment=HorizontalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center};
            var border=new FrameworkElementFactory(typeof(Border));border.SetBinding(Border.BackgroundProperty,new Binding("Background"){RelativeSource=RelativeSource.TemplatedParent});border.SetBinding(Border.BorderBrushProperty,new Binding("BorderBrush"){RelativeSource=RelativeSource.TemplatedParent});border.SetBinding(Border.BorderThicknessProperty,new Binding("BorderThickness"){RelativeSource=RelativeSource.TemplatedParent});border.SetValue(Border.CornerRadiusProperty,new CornerRadius(radius));
            var presenter=new FrameworkElementFactory(typeof(ContentPresenter));presenter.SetBinding(ContentPresenter.ContentProperty,new Binding("Content"){RelativeSource=RelativeSource.TemplatedParent});presenter.SetBinding(ContentPresenter.MarginProperty,new Binding("Padding"){RelativeSource=RelativeSource.TemplatedParent});presenter.SetBinding(ContentPresenter.HorizontalAlignmentProperty,new Binding("HorizontalContentAlignment"){RelativeSource=RelativeSource.TemplatedParent});presenter.SetBinding(ContentPresenter.VerticalAlignmentProperty,new Binding("VerticalContentAlignment"){RelativeSource=RelativeSource.TemplatedParent});border.AppendChild(presenter);button.Template=new ControlTemplate(typeof(Button)){VisualTree=border};return button;
        }
        internal static Brush Brush(string value){return new BrushConverter().ConvertFromString(value) as Brush;}
    }

    internal sealed class TurnEditorWindow:Window
    {
        private readonly Button me;
        private readonly Button other;
        private readonly TextBox speaker;
        private readonly TextBox text;
        private string role;
        internal ConversationTurn Result { get; private set; }

        internal TurnEditorWindow(ConversationTurn turn)
        {
            role=turn.Role=="user"?"user":"other";
            Title="修改语句";Width=360;SizeToContent=SizeToContent.Height;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;AllowsTransparency=true;Background=Brushes.Transparent;Topmost=true;ShowInTaskbar=false;FontFamily=new FontFamily("Segoe UI Variable Text");UseLayoutRounding=true;SnapsToDevicePixels=true;
            var root=new StackPanel{Margin=new Thickness(14)};
            var title=new TextBlock{Text="修改语句",FontSize=14,FontWeight=FontWeights.SemiBold,Foreground=ContextEditorWindow.Brush("#32283F"),LineHeight=22,Margin=new Thickness(0,0,0,10),Cursor=Cursors.SizeAll};title.MouseLeftButtonDown+=delegate{try{DragMove();}catch{}};root.Children.Add(title);
            var roleGrid=new Grid{Margin=new Thickness(0,0,0,8)};roleGrid.ColumnDefinitions.Add(new ColumnDefinition());roleGrid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(8)});roleGrid.ColumnDefinitions.Add(new ColumnDefinition());
            me=ContextEditorWindow.RoundedButton("我",Brushes.Transparent,ContextEditorWindow.Brush("#433956"),10);me.Height=36;me.FontSize=12;me.FontWeight=FontWeights.SemiBold;me.Click+=delegate{SetRole("user");};roleGrid.Children.Add(me);
            other=ContextEditorWindow.RoundedButton("对方",Brushes.Transparent,ContextEditorWindow.Brush("#433956"),10);other.Height=36;other.FontSize=12;other.FontWeight=FontWeights.SemiBold;other.Click+=delegate{SetRole("other");};Grid.SetColumn(other,2);roleGrid.Children.Add(other);root.Children.Add(roleGrid);
            speaker=new TextBox{Text=turn.Speaker??String.Empty,Height=36,Padding=new Thickness(10,7,10,7),Margin=new Thickness(0,0,0,8),FontSize=12,ToolTip="对方姓名（可选）"};root.Children.Add(speaker);
            text=new TextBox{Text=turn.Text??String.Empty,MinHeight=88,MaxHeight=200,TextWrapping=TextWrapping.Wrap,AcceptsReturn=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Padding=new Thickness(10,8,10,8),FontSize=12};root.Children.Add(text);
            var actions=new Grid{Margin=new Thickness(0,12,0,0)};actions.ColumnDefinitions.Add(new ColumnDefinition());actions.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(10)});actions.ColumnDefinitions.Add(new ColumnDefinition());
            var cancel=ContextEditorWindow.RoundedButton("取消",ContextEditorWindow.Brush("#EEE8F8"),ContextEditorWindow.Brush("#4D435C"),12);cancel.Height=40;cancel.FontSize=12;cancel.FontWeight=FontWeights.SemiBold;cancel.Click+=delegate{DialogResult=false;};actions.Children.Add(cancel);
            var done=ContextEditorWindow.RoundedButton("完成",ContextEditorWindow.Brush("#5A2DFC"),Brushes.White,12);done.Height=40;done.FontSize=12;done.FontWeight=FontWeights.SemiBold;done.Click+=delegate{Save();};Grid.SetColumn(done,2);actions.Children.Add(done);root.Children.Add(actions);
            Content=new Border{CornerRadius=new CornerRadius(14),BorderThickness=new Thickness(1),BorderBrush=ContextEditorWindow.Brush("#C4C0FD"),Background=new LinearGradientBrush(Color.FromArgb(250,247,240,254),Color.FromArgb(250,230,229,253),0),Padding=new Thickness(2),ClipToBounds=true,Child=root};SetRole(role);Loaded+=delegate{text.Focus();text.SelectAll();};
        }

        private void SetRole(string value)
        {
            role=value;bool mine=role=="user";
            me.Background=ContextEditorWindow.Brush(mine?"#5A2DFC":"#EEE8F8");me.Foreground=ContextEditorWindow.Brush(mine?"#FFFFFF":"#433956");
            other.Background=ContextEditorWindow.Brush(!mine?"#5A2DFC":"#EEE8F8");other.Foreground=ContextEditorWindow.Brush(!mine?"#FFFFFF":"#433956");
            speaker.Visibility=mine?Visibility.Collapsed:Visibility.Visible;
        }
        private void Save()
        {
            if(String.IsNullOrWhiteSpace(text.Text))return;
            Result=new ConversationTurn{Role=role,Speaker=role=="other"?(speaker.Text??String.Empty).Trim():String.Empty,Text=text.Text.Trim()};DialogResult=true;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SnkMessage
{
    internal sealed class ContextEditorWindow:Window
    {
        private sealed class Row
        {
            internal CheckBox Included;
            internal ComboBox Role;
            internal TextBox Speaker;
            internal TextBox Text;
        }

        private readonly SelectionContext context;
        private readonly TextBox subject;
        private readonly List<Row> rows=new List<Row>();

        internal ContextEditorWindow(SelectionContext context)
        {
            this.context=context;
            Title="查看上下文";Width=520;MinHeight=320;MaxHeight=700;SizeToContent=SizeToContent.Height;
            WindowStyle=WindowStyle.ToolWindow;ResizeMode=ResizeMode.CanResize;WindowStartupLocation=WindowStartupLocation.CenterOwner;
            Background=Brush("#FFF8F6FF");Topmost=true;ShowInTaskbar=false;

            var root=new DockPanel{Margin=new Thickness(14)};
            var footer=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};
            DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
            var cancel=new Button{Content="取消",MinWidth=72,Height=30,Margin=new Thickness(0,0,8,0)};cancel.Click+=delegate{DialogResult=false;};footer.Children.Add(cancel);
            var save=new Button{Content="应用并重新生成",MinWidth=126,Height=30,Background=Brush("#5A2DFC"),Foreground=Brushes.White};save.Click+=delegate{Save();};footer.Children.Add(save);

            var content=new StackPanel();
            content.Children.Add(new TextBlock{Text="聊天对象或会话",FontSize=12,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,5)});
            subject=new TextBox{Text=context.ConversationLabel??String.Empty,Height=30,Padding=new Thickness(7,4,7,4),Margin=new Thickness(0,0,0,12)};content.Children.Add(subject);
            content.Children.Add(new TextBlock{Text="附近消息",FontSize=12,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,4)});
            content.Children.Add(new TextBlock{Text="取消勾选可排除消息；可以修正角色、发送者和识别文字。",FontSize=11,Foreground=Brush("#756D80"),Margin=new Thickness(0,0,0,8)});
            foreach(ConversationTurn turn in context.Context??Array.Empty<ConversationTurn>())AddTurn(content,turn);
            if(rows.Count==0)content.Children.Add(new TextBlock{Text="本次没有识别到附近消息。仍可修正聊天对象。",FontSize=11,Foreground=Brush("#81798E"),Margin=new Thickness(0,6,0,6)});
            root.Children.Add(new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MaxHeight=540});Content=root;
        }

        private void AddTurn(Panel panel,ConversationTurn turn)
        {
            var grid=new Grid{Margin=new Thickness(0,0,0,7)};
            grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(28)});
            grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(76)});
            grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(96)});
            grid.ColumnDefinitions.Add(new ColumnDefinition());
            var included=new CheckBox{IsChecked=true,VerticalAlignment=VerticalAlignment.Center};grid.Children.Add(included);
            var role=new ComboBox{ItemsSource=new[]{"我","对方"},SelectedIndex=turn.Role=="user"?0:1,Height=30,Margin=new Thickness(0,0,6,0)};Grid.SetColumn(role,1);grid.Children.Add(role);
            var speaker=new TextBox{Text=turn.Speaker??String.Empty,Height=30,Padding=new Thickness(5,3,5,3),Margin=new Thickness(0,0,6,0),ToolTip="发送者（可选）"};Grid.SetColumn(speaker,2);grid.Children.Add(speaker);
            var text=new TextBox{Text=turn.Text??String.Empty,MinHeight=30,MaxHeight=74,TextWrapping=TextWrapping.Wrap,AcceptsReturn=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Padding=new Thickness(5,3,5,3)};Grid.SetColumn(text,3);grid.Children.Add(text);
            role.SelectionChanged+=delegate{speaker.IsEnabled=role.SelectedIndex==1;};speaker.IsEnabled=role.SelectedIndex==1;
            rows.Add(new Row{Included=included,Role=role,Speaker=speaker,Text=text});panel.Children.Add(grid);
        }

        private void Save()
        {
            context.ConversationLabel=(subject.Text??String.Empty).Trim();
            context.Context=rows.Where(row=>row.Included.IsChecked==true&&!String.IsNullOrWhiteSpace(row.Text.Text)).Select(row=>new ConversationTurn
            {
                Role=row.Role.SelectedIndex==0?"user":"other",
                Speaker=row.Role.SelectedIndex==1?(row.Speaker.Text??String.Empty).Trim():String.Empty,
                Text=row.Text.Text.Trim()
            }).ToArray();
            context.ContextDiagnostic="已手动修正上下文";
            context.ContextSource="manual";
            context.OcrImageBase64=null;
            DialogResult=true;
        }

        private static Brush Brush(string value){return new BrushConverter().ConvertFromString(value) as Brush;}
    }
}

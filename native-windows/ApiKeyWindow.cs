using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SnkMessage
{
    internal sealed class ApiKeyWindow:Window
    {
        private readonly PasswordBox input;
        internal bool Saved { get; private set; }

        internal ApiKeyWindow()
        {
            Title="SnkMessage 设置";Width=420;Height=210;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterScreen;Topmost=true;
            Background=new SolidColorBrush(Color.FromRgb(249,248,253));
            var panel=new StackPanel{Margin=new Thickness(22)};
            panel.Children.Add(new TextBlock{Text="OpenRouter API Key",FontSize=16,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,8)});
            panel.Children.Add(new TextBlock{Text=CredentialStore.HasApiKey?"密钥已保存在 Windows 凭据管理器中。输入新密钥可替换。":"输入密钥后将安全保存在 Windows 凭据管理器中。",TextWrapping=TextWrapping.Wrap,Foreground=new SolidColorBrush(Color.FromRgb(83,75,99)),Margin=new Thickness(0,0,0,12)});
            input=new PasswordBox{Height=32,Padding=new Thickness(7,5,7,5)};panel.Children.Add(input);
            var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,16,0,0)};
            var cancel=new Button{Content="取消",Width=72,Height=30,Margin=new Thickness(0,0,8,0)};cancel.Click+=delegate{Close();};
            var save=new Button{Content="保存",Width=72,Height=30,Background=new SolidColorBrush(Color.FromRgb(90,45,252)),Foreground=Brushes.White};save.Click+=Save;
            buttons.Children.Add(cancel);buttons.Children.Add(save);panel.Children.Add(buttons);Content=panel;
        }

        private void Save(object sender,RoutedEventArgs e)
        {
            if(String.IsNullOrWhiteSpace(input.Password)){MessageBox.Show(this,"请输入 API Key。","SnkMessage",MessageBoxButton.OK,MessageBoxImage.Information);return;}
            try{CredentialStore.SaveApiKey(input.Password);Saved=true;DialogResult=true;Close();}
            catch(Exception error){MessageBox.Show(this,error.Message,"保存失败",MessageBoxButton.OK,MessageBoxImage.Error);}
        }
    }
}
